using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Was Claude baut, live im Programm (Wunsch 01.11.2026): Vorschau rechts im Chat (Webseite/Spiel), vorher/nachher mit Behalten/Zurücknehmen,
/// Studio-Dokument (.tdoc) wird live ins Studio übernommen, neue Texte öffnen gleich in Trendo Text, Lieferformat je Studio. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeLive_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    /// <summary>Nachgebildetes Claude Code: ändert eine Datei im Projektordner (Werkzeug-Aufruf, Ausführung, Ergebnis) und antwortet.</summary>
    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<string, (string File, string Content)> change) => (args, dir, line, _) =>
    {
        calls.Add(args);
        var (file, content) = change(dir);
        var path = Path.Combine(dir, file);
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = path, content } } } } }));
        File.WriteAllText(path, content);
        line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Erledigt." } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = "Erledigt.", session_id = "s1", num_turns = 2 }));
        return Task.FromResult(0);
    };

    [AvaloniaFact(DisplayName = "Claude live: Webseite erscheint in der Vorschau, beim Ändern vorher/nachher – Behalten oder Zurücknehmen; Lieferformat index.html")]
    public async Task Web_preview_and_compare()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>(); var n = 0;
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, _ => ("index.html", ++n == 1 ? "<h1>Hallo</h1>" : "<h1>Hallo Welt</h1>"));
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("baukasten"); w.Find<Button>("auftrag-claude").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Startseite"));
            Assert.EndsWith("index.html", w.ClaudeChatPreviewFile());
            Assert.NotNull(w.TryFind<Border>("claude-preview-web")); Assert.Equal("index.html", w.Find<TextBlock>("claude-preview-name").Text);
            Assert.Null(w.TryFind<Button>("claude-preview-before")); // neu angelegt: nichts zu vergleichen
            var sys = calls[0].ToList(); Assert.Contains("index.html", sys[sys.IndexOf("--append-system-prompt") + 1]);

            await Run(w.ClaudeChatSend("Schreib Hallo Welt"));
            Assert.NotNull(w.TryFind<Button>("claude-preview-before")); Assert.NotNull(w.TryFind<Button>("claude-preview-keep"));
            w.Find<Button>("claude-preview-before").Press(); Assert.Equal("vorher", w.ClaudeChatPreviewSide());
            w.Find<Button>("claude-preview-after").Press(); Assert.Equal("nachher", w.ClaudeChatPreviewSide());
            var file = w.ClaudeChatPreviewFile()!;
            w.Find<Button>("claude-preview-undo").Press();
            Assert.Equal("<h1>Hallo</h1>", File.ReadAllText(file)); // zurückgenommen
            Assert.Null(w.TryFind<Button>("claude-preview-before"));
            w.Find<Button>("claude-preview-close").Press(); w.Find<Button>("claude-chat-preview-toggle").Press(); Assert.NotNull(w.TryFind<TextBlock>("claude-preview-name"));
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude live: offenes Studio-Dokument (.tdoc) – Claude bearbeitet es, das Studio übernimmt die Änderung sofort, Rückgängig im Studio")]
    public async Task Studio_document_live()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, dir =>
        {
            var tdoc = Directory.GetFiles(dir, "*.tdoc").Single();
            var o = JsonNode.Parse(File.ReadAllText(tdoc))!.AsObject();
            o["data"]!.AsObject()["claude"] = "Wohnzimmer hell";
            return (Path.GetFileName(tdoc), o.ToJsonString());
        });
        try
        {
            await Run(w.ClaudeCheck(true));
            var page = MainWindow.Programs.Keys.First(k => k == "architektur" || MainWindow.Programs[k].Studio == "SPEC-01");
            w.Show(page); var doc = w.ProgramNew(page);
            w.ClaudeDockToggle("SPEC-01"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Mach das Wohnzimmer hell"));
            Assert.Equal("Wohnzimmer hell", (string?)w.ProgramActive(page)!.Data["claude"]); // live ins Studio übernommen
            Assert.True(w.ProgramActive(page)!.CanUndo); Assert.True(w.ProgramActive(page)!.Dirty);
            var sys = calls[0].ToList(); Assert.Contains("offene Studio-Dokument", sys[sys.IndexOf("--append-system-prompt") + 1]);
            Assert.True(w.ProgramActive(page)!.Undo()); Assert.Null(w.ProgramActive(page)!.Data["claude"]);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude live: neuer Text öffnet gleich in Trendo Text – aber nie über einen ungespeicherten Text")]
    public async Task Text_opens_in_program()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, _ => ("brief.md", "# Einladung\n\nLiebe Nachbarn, wir feiern am Samstag."));
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("text"); w.ClaudeDockToggle("SYS-10"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Schreib eine Einladung"));
            Assert.Contains("in Trendo Text geöffnet", w.ClaudeMessage);
            Assert.Contains("Einladung", w.TextDocument.PlainText());
            var sys = calls[0].ToList(); Assert.Contains("Markdown", sys[sys.IndexOf("--append-system-prompt") + 1]);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
