using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.40 – Zeitleiste, Hell/Dunkel wie Windows, Töne, Claude zeigt wo es arbeitet, Aufklapp-Felder, genauere Leser, Hilfe auf jeder Seite. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudePolish_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<int, string> answer, bool write = true) => async (args, dir, line, _) =>
    {
        calls.Add(args);
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        if (write)
        {
            var path = Path.Combine(dir, "notiz.md");
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Read", input = new { file_path = path } } } } }));
            await Task.Delay(1200); // damit die Zeitleiste echte Abstände hat
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = path, content = "x" } } } } }));
            File.WriteAllText(path, "x" + calls.Count);
            line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
        }
        var text = answer(calls.Count);
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
        return 0;
    };
    static void End(MainWindow w) { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.SoundOverride = null; MainWindow.ClaudeStepDelay = 350; w.Close(); }

    [AvaloniaFact(DisplayName = "Zeitleiste mit Dauer und Tönen: fertig, Rückfrage, abschaltbar")]
    public async Task Timeline_and_sounds()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>(); var sounds = new List<string>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.SoundOverride = sounds.Add;
        MainWindow.ClaudeRunOverride = Fake(calls, n => n == 2 ? "Gern.\nFrage: Lang oder kurz? [Lang | Kurz]" : "Fertig.");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("text"); w.ClaudeDockToggle("SYS-10"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Schreib eine Notiz"));
            var steps = w.Find<Expander>("claude-chat-steps");
            Assert.Matches(@"^\d+ Arbeitsschritte · \d+ s$", (string)steps.Header!);
            Assert.NotNull(w.TryFind<StackPanel>("claude-timeline-steps"));
            Assert.True(w.ClaudeChatCurrent()!.Messages[^1].Steps.All(s => s.At is not null));
            Assert.Equal(["fertig"], sounds);
            await Run(w.ClaudeChatSend("Noch eine"));
            Assert.Equal("frage", sounds[^1]);
            KiSourceSettings.Update(w.Core.DataRoot, x => x.ClaudeSounds = false);
            await Run(w.ClaudeChatSend("Und noch eine"));
            Assert.Equal(2, sounds.Count); // abgeschaltet
            var wav = MainWindow.Chime([784, 1175]);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4)); Assert.True(wav.Length > 10000);
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen")]
    public async Task Flash_and_flyout_fields()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = Fake(calls, _ => "Ich trage den Speicherort ein.\n[[trendo:feld | prog-saveas-path | Plaene\\Wohnung]]", write: false);
        try
        {
            await Run(w.ClaudeCheck(true));
            var page = MainWindow.Programs.Keys.First(k => k == "architektur" || MainWindow.Programs[k].Studio == "SPEC-01");
            w.Show(page); w.ProgramNew(page); w.Show(page);
            Assert.Contains(w.ClaudeReadPage(page)!.Fields, f => f.Name == "prog-saveas-path"); // steckt im Aufklapp-Menü „Speichern unter …“
            w.ClaudeDockToggle("SPEC-01"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Trag den Speicherort ein"));
            await Run(w.ClaudeRunPlan());
            Assert.Equal("prog-saveas-path", w.ClaudeLastFlashed);
            Assert.Contains("„Plaene\\Wohnung“", w.ClaudeChatCurrent()!.Messages[^1].Text);
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe")]
    public void Readers_theme_help()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("kalender"); Assert.StartsWith("Kalender, ", w.ClaudeFocus("kalender"));
            var page = MainWindow.Programs.Keys.First(k => MainWindow.Programs[k].Studio == "CORE-06");
            w.Show(page); w.ProgramNew(page); Assert.Contains("Spiele bauen: Dokument „", w.ClaudeFocus(page));

            MainWindow.SystemLightOverride = () => true; Assert.Equal("hell", MainWindow.EffectiveTheme("system"));
            MainWindow.SystemLightOverride = () => false; Assert.Equal("dunkel", MainWindow.EffectiveTheme("system"));
            MainWindow.SystemLightOverride = null;
            Assert.Contains("system", UserSettings.Themes);
            w.Show("settings"); Assert.NotNull(w.TryFind<Button>("theme-system"));

            w.Show("uhr"); w.Find<Button>("btn-help").Press();
            Assert.NotNull(w.TryFind<Border>("hint")); // vorher ohne Wirkung
            w.Find<Button>("uhr-wc-add").Press();
            Assert.Contains("Wähle zuerst eine Stadt", string.Join("|", w.GetLogicalTexts()));
        }
        finally { MainWindow.SystemLightOverride = null; w.Close(); }
    }
}
