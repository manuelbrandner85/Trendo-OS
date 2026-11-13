using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.38 – Claude in den Programmen: Markieren & fragen, Ziehen in Claude, Annehmen/Verwerfen mit Vorher/Nachher im Programm, Befehlsleiste Strg+K,
/// Claude schlägt vor (abschaltbar), Abläufe aus Wiederholungen, Projekt-Fortsetzung überall. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudePrograms_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    static string Arg(IReadOnlyList<string> a, string name) { var l = a.ToList(); return l[l.IndexOf(name) + 1]; }

    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<int, string, string> answer, Func<string, (string File, string Content)?>? change = null) => (args, dir, line, _) =>
    {
        calls.Add(args);
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        if (change?.Invoke(dir) is { } ch)
        {
            var path = Path.Combine(dir, ch.File);
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = path, content = ch.Content } } } } }));
            File.WriteAllText(path, ch.Content);
            line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
        }
        var text = answer(calls.Count, args[1]);
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
        return Task.FromResult(0);
    };
    static void End(MainWindow w) { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }

    [AvaloniaFact(DisplayName = "Markieren & Claude fragen: Zellbereich in Trendo Tabelle geht mit ins Eingabefeld; Knopf auf der Seite; Strg+Umschalt+A")]
    public async Task Ask_about_selection()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake([], (_, _) => "Ok.");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("tabelle"); w.SheetNew(); w.SheetSetCell("A1", "Brot"); w.SheetSetCell("B1", "2,50"); w.SheetSetCell("A2", "Milch"); w.SheetSetCell("B2", "1,20");
            w.SheetSelect("A1:B2"); w.Show("tabelle");
            Assert.Equal("A1:B2\nBrot\t2,50\nMilch\t1,20", w.ClaudeSelection("tabelle"));
            w.Find<Button>("auftrag-claude-markierung").Press();
            var input = w.Find<TextBox>("claude-chat-input").Text!;
            Assert.StartsWith("Zu dieser Markierung:", input); Assert.Contains("Milch\t1,20", input);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.markierung");
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Ziehen in Claude: Text kommt als Zitat ins Eingabefeld, Dateien werden angehängt")]
    public async Task Drag_into_claude()
    {
        var (w, _, root) = Ui.Open();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake([], (_, _) => "Ok.");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev");
            Assert.True(w.ClaudeDropInto("CORE-07", [], "Herzlich willkommen"));
            Assert.Contains("„Herzlich willkommen“", w.Find<TextBox>("claude-chat-input").Text);
            var pic = Path.Combine(root, "logo.png"); File.WriteAllBytes(pic, [137, 80, 78, 71]);
            Assert.True(w.ClaudeDropInto("CORE-07", [pic], null));
            Assert.NotNull(w.TryFind<Button>("claude-chat-attachment-del-0"));
            Assert.False(w.ClaudeDropInto("CORE-07", [], "  "));
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Annehmen/Verwerfen im Programm: Claude ändert das Studio-Dokument – Vorher/Nachher vergleichen, verwerfen stellt alles her, behalten übernimmt")]
    public async Task Accept_reject_in_program()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>(); var n = 0;
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, (_, _) => "Erledigt.", dir =>
        {
            var tdoc = Directory.GetFiles(dir, "*.tdoc").Single();
            var o = JsonNode.Parse(File.ReadAllText(tdoc))!.AsObject();
            o["data"]!.AsObject()["claude"] = ++n == 1 ? "hell" : "dunkel";
            return (Path.GetFileName(tdoc), o.ToJsonString());
        });
        try
        {
            await Run(w.ClaudeCheck(true));
            var page = MainWindow.Programs.Keys.First(k => k == "architektur" || MainWindow.Programs[k].Studio == "SPEC-01");
            w.Show(page); w.ProgramNew(page);
            w.ClaudeDockToggle("SPEC-01"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Mach es hell"));
            Assert.True(w.ClaudeChangePending(page));
            w.Show(page);
            Assert.NotNull(w.TryFind<Border>("claude-change"));
            w.Find<Button>("claude-change-before").Press();
            Assert.Null(w.ProgramActive(page)!.Data["claude"]); // Stand vorher
            w.Find<Button>("claude-change-after").Press();
            Assert.Equal("hell", (string?)w.ProgramActive(page)!.Data["claude"]);
            w.Find<Button>("claude-change-drop").Press();
            Assert.Null(w.ProgramActive(page)!.Data["claude"]); Assert.False(w.ClaudeChangePending(page)); // verworfen
            Assert.Null(w.TryFind<Border>("claude-change"));

            await Run(w.ClaudeChatSend("Doch lieber dunkel"));
            w.ClaudeChangeDecide(page, "vorher"); w.ClaudeChangeDecide(page, "behalten");
            Assert.Equal("dunkel", (string?)w.ProgramActive(page)!.Data["claude"]); Assert.False(w.ClaudeChangePending(page)); // behalten (auch aus „vorher“)
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.programm.verworfen");
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.programm.behalten");
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Claude schlägt vor: Vorschläge als Knöpfe, Klick schickt sie ab, auch in der Befehlsleiste; abschaltbar")]
    public async Task Suggestions()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls, (n, _) => n == 1 ? "Die Seite steht.\nVorschläge: Kontaktformular ergänzen | Farben anpassen" : "Gemacht.");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Startseite"));
            Assert.Contains("Vorschläge:", Arg(calls[0], "--append-system-prompt"));
            Assert.DoesNotContain("Vorschläge:", string.Join("|", w.GetLogicalTexts()));
            Assert.Equal("Farben anpassen", w.Find<Button>("claude-chat-suggest-1").Content);
            w.OpenPalette("kontakt"); Assert.Contains(w.PaletteResults, r => r.Label == "Kontaktformular ergänzen"); w.ClosePalette();
            w.Find<Button>("claude-chat-suggest-0").Press(); while (calls.Count < 2 || w.ClaudeChatRunning()) await Run(Task.Delay(20));
            Assert.StartsWith("Kontaktformular ergänzen", calls[1][1]);

            KiSourceSettings.Update(w.Core.DataRoot, x => x.ClaudeSuggests = false);
            await Run(w.ClaudeChatSend("Noch was"));
            Assert.DoesNotContain("Vorschläge:", Arg(calls[2], "--append-system-prompt"));
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Wiederholungen automatisieren: Auftrag als Ablauf merken (Prompt-Bibliothek), mit einem Klick wiederholen; Wiederholung wird erkannt")]
    public async Task Flows()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls, (_, _) => "Erledigt.");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Prüfe alle Links und Bilder"));
            Assert.Equal("⟳ Als Ablauf merken", w.Find<Button>("claude-chat-flow-save").Content);
            await Run(w.ClaudeChatSend("Prüfe alle Links und Bilder"));
            Assert.Contains("2×", (string)w.Find<Button>("claude-chat-flow-save").Content!); // Wiederholung erkannt
            w.Find<Button>("claude-chat-flow-save").Press();
            Assert.Equal(["Prüfe alle Links und Bilder"], w.ClaudeFlowNames());
            Assert.Contains(w.Prompts.All(), p => p.Category.StartsWith("Claude-Abläufe ·") && p.Text == "Prüfe alle Links und Bilder");
            Assert.NotNull(w.TryFind<TextBlock>("claude-chat-flow-saved"));
            w.Find<Button>("claude-flow-0").Press(); while (calls.Count < 3 || w.ClaudeChatRunning()) await Run(Task.Delay(20));
            Assert.StartsWith("Prüfe alle Links und Bilder", calls[2][1]);
            w.OpenPalette("ablauf"); Assert.Contains(w.PaletteResults, r => r.Kind == "Claude-Ablauf"); w.ClosePalette();
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Befehlsleiste Strg+K und Projekt-Fortsetzung überall: Claude fragen, weitermachen – auch aus einem anderen Studio")]
    public async Task Palette_and_continue()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls, (_, _) => "Erledigt.");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Startseite für die Bäckerei"));
            var chat = w.ClaudeChatCurrent()!;
            w.ClaudeDockToggle("CORE-07"); // zu
            w.Show("dev");
            Assert.StartsWith("Weiter: Baue eine Startseite", (string)w.Find<Button>("auftrag-claude-weiter").Content!);

            w.Key(Avalonia.Input.PhysicalKey.K, Avalonia.Input.RawInputModifiers.Control);
            Assert.True(w.CommandPaletteOpen);
            w.OpenPalette("Mach den Kopf blau");
            Assert.Equal("Claude: „Mach den Kopf blau“", w.PaletteResults[0].Label);
            w.ClosePalette();

            w.Show("tabelle"); // anderes Studio
            w.OpenPalette("weiter bäckerei");
            Assert.Contains(w.PaletteResults, r => r.Label == "Weiter: " + chat.Title && r.Kind.StartsWith("Claude-Projekt"));
            w.ClosePalette();
            w.ClaudeContinue("CORE-07", chat.Id);
            Assert.Equal("dev", w.CurrentView); Assert.Equal(chat.Id, w.ClaudeChatCurrent()!.Id);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.weiter");
        }
        finally { End(w); }
    }
}
