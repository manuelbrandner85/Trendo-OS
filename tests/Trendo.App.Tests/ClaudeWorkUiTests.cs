using System.IO.Compression;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.42 – Unterlagen aus „Wissen“ nur mit Freigabe, große Aufträge in Etappen (nach der Abo-Pause macht Claude weiter), Veröffentlichen mit einem Klick (verpacken, nichts hochladen).
[Collection("Claude")]
public class ClaudeWork_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    static string Sys(IReadOnlyList<string> a) { var l = a.ToList(); return l[l.IndexOf("--append-system-prompt") + 1]; }

    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<int, bool>? limit = null) => (args, dir, line, _) =>
    {
        calls.Add(args);
        if (limit?.Invoke(calls.Count) == true) { line(J(new { type = "result", subtype = "error", is_error = true, result = $"Claude AI usage limit reached|{DateTimeOffset.Now.AddHours(2).ToUnixTimeSeconds()}", session_id = "", num_turns = 0 })); return Task.FromResult(0); }
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        File.WriteAllText(Path.Combine(dir, "index.html"), ClaudeChat_Ui.Page("<h1>Version " + calls.Count + "</h1>"));
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Fertig." } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig.", session_id = "s1", num_turns = 1 }));
        return Task.FromResult(0);
    };

    [AvaloniaFact(DisplayName = "Unterlagen: nur Freigegebenes geht an Claude (als Datei im Projekt); passende Unterlagen werden vorgeschlagen – erst nach Ja")]
    public async Task Documents_with_consent()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls);
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Wissen.Add("Firmenfarben Bäckerei", "Die Firmenfarben der Bäckerei sind Dunkelblau und Gold. Schrift: Serif.", "eigene Notiz");
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Startseite für die Bäckerei in den Firmenfarben"));
            Assert.DoesNotContain(".trendo/unterlagen", Sys(calls[0])); // ohne Freigabe geht nichts mit
            Assert.Contains("Firmenfarben Bäckerei", w.Find<TextBlock>("claude-docs-suggest").Text); // vorgeschlagen
            w.Find<Button>("claude-docs-suggest-yes").Press();
            Assert.Single(w.ClaudeSharedDocs());
            await Run(w.ClaudeChatSend("Mach den Kopf größer"));
            Assert.Contains(".trendo/unterlagen", Sys(calls[^1])); Assert.Contains("Firmenfarben Bäckerei.md", Sys(calls[^1]));
            Assert.Contains("Dunkelblau und Gold", File.ReadAllText(Path.Combine(w.ClaudeChatCurrent()!.Folder, ".trendo", "unterlagen", "Firmenfarben Bäckerei.md")));
            w.Find<Button>("claude-chat-docs").Press();
            w.Find<Button>("claude-docs-toggle-0").Press(); // wieder entziehen
            Assert.Empty(w.ClaudeSharedDocs());
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Etappen: Abo mitten im Auftrag leer – Ersatz-KI übernimmt, Trendo merkt sich das Projekt; ist Claude wieder frei, macht Claude dort weiter")]
    public async Task Stages_after_limit()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls, n => n == 1);
        MainWindow.ClaudeFallbackOverride = (_, _, _) => Task.FromResult(new KiAnswer(true, J(new { zusammenfassung = "Seite angelegt.", dateien = new[] { new { pfad = "index.html", inhalt = ClaudeChat_Ui.Page("<h1>Ersatz</h1>") } } }), "OpenRouter", "frei", [], TimeSpan.Zero));
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue einen großen Online-Shop"));
            var chat = w.ClaudeChatCurrent()!;
            Assert.Single(w.ClaudePendingStages()); Assert.Equal(chat.Id, w.ClaudePendingStages()[0].ChatId);
            await Run(w.ClaudeStagesTick()); Assert.Single(calls); // Abo noch leer: nichts passiert
            w.CLimit.Set(DateTimeOffset.Now.AddSeconds(-1)); // wieder frei
            await Run(w.ClaudeStagesTick());
            Assert.Equal(2, calls.Count); Assert.StartsWith(ClaudeStages.Resume + "Baue einen großen Online-Shop", calls[1][1]);
            Assert.Empty(w.ClaudePendingStages());
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.etappe.weiter");
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeFallbackOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Veröffentlichen: erst nach Ja; Projekt als ZIP im Ordner „Veröffentlicht“, ohne Hilfsdateien – hochgeladen wird nichts")]
    public async Task Publish_one_click()
    {
        var (w, l, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls);
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Visitenkarte"));
            w.Find<Button>("claude-chat-publish").Press();
            Assert.Null(w.LastPublished); // erst nach Ja
            w.Find<Button>("claude-publish-yes").Press();
            Assert.True(File.Exists(w.LastPublished)); Assert.EndsWith(".zip", w.LastPublished);
            using var zip = ZipFile.OpenRead(w.LastPublished!);
            Assert.Contains(zip.Entries, e => e.FullName == "index.html");
            Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith(".trendo") || e.FullName.StartsWith(".claude"));
            Assert.Contains("Hochgeladen wurde nichts", w.ClaudeChatCurrent()!.Messages[^1].Text);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.veroeffentlicht");
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
