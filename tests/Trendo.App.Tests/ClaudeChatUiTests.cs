using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude-Chat im Studio wie in Claude: Projekte entstehen mit der ersten Nachricht, Antwort mit Arbeitsschritten, Fortsetzen über die Sitzung,
/// Zurücknehmen, Löschen (Ordner in den Papierkorb). Claude wird vollständig nachgebildet – kein Test nutzt das echte Abo.
[Collection("Claude")]
public class ClaudeChat_Ui
{
    static Task<ClaudeCode.Status> St(bool inst, bool on) => Task.FromResult(inst ? new ClaudeCode.Status(true, on, on ? "claude.ai" : "", on ? "max" : "", @"C:\x\claude.exe") : ClaudeCode.Status.Missing);
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    /// <summary>Vollständige Seite (Titel, Sprache, Handy-Ansicht) – sonst bessert Claude nach Trendos Prüfung automatisch nach.</summary>
    internal static string Page(string body) => "<!doctype html><html lang=\"de\"><head><meta name=\"viewport\" content=\"width=device-width\"><title>Seite</title></head><body>" + body + "</body></html>";

    /// <summary>Nachgebildetes Claude Code: meldet Sitzung, schreibt eine Datei im Projektordner, antwortet.</summary>
    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> FakeClaude(List<IReadOnlyList<string>> calls, string file = "kontakt.html") => (args, dir, line, _) =>
    {
        calls.Add(args);
        line(J(new { type = "system", subtype = "init", session_id = "sitzung-1", model = "claude" }));
        var path = Path.Combine(dir, file);
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = path, content = Page("<h1>Kontakt</h1>") } } } } }));
        File.WriteAllText(path, Page("<h1>Kontakt</h1>"));
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Fertig: Die Kontaktseite ist angelegt.\n```html\n<h1>Kontakt</h1>\n```" } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig", session_id = "sitzung-1", num_turns = 2 }));
        return Task.FromResult(0);
    };

    [AvaloniaFact(DisplayName = "Claude-Chat: wie in Claude – erste Nachricht legt das Projekt an, Antwort mit Arbeitsschritten, Fortsetzen über die Sitzung, Zurücknehmen, Löschen in den Papierkorb")]
    public async Task Chat_like_claude()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>(); var trashed = new List<string>();
        MainWindow.ClaudeStatusOverride = () => St(true, true); MainWindow.ClaudeRunOverride = FakeClaude(calls); MainWindow.ChatTrashOverride = trashed.Add;
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("baukasten");
            w.Find<TextBox>("auftrag-prompt").Text = "Baue eine Kontaktseite";
            w.Find<Button>("auftrag-claude").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var win = w.ActiveWindowId!;
            Assert.Equal("chat", w.ClaudeDockTab(win)); Assert.True(w.Find<Border>("claude-chat-pane").IsVisible);
            Assert.Equal("Baue eine Kontaktseite", w.Find<TextBox>("claude-chat-input").Text); // Auftrag steht bereit, nichts kopiert, nichts abgeschickt
            Assert.Empty(calls); Assert.NotNull(w.TryFind<TextBlock>("claude-chat-welcome"));

            // erste Nachricht: Projekt entsteht automatisch
            await Run(w.ClaudeChatSend("Baue eine Kontaktseite"));
            var chat = w.ClaudeChatCurrent()!;
            Assert.Equal("Baue eine Kontaktseite", chat.Title); Assert.True(File.Exists(Path.Combine(chat.Folder, "kontakt.html")));
            Assert.Equal(["user", "assistant"], chat.Messages.Select(m => m.Role)); Assert.Equal("sitzung-1", chat.Session);
            Assert.StartsWith("Fertig: Die Kontaktseite", chat.Messages[1].Text); Assert.Contains(chat.Messages[1].Steps, s => s.Changes && s.Text.Contains("kontakt.html"));
            Assert.Equal(["kontakt.html (neu)"], chat.Messages[1].Changed);
            var a0 = calls[0].ToList();
            Assert.DoesNotContain("--resume", a0); Assert.DoesNotContain("--restricted", a0); Assert.Equal("auto", a0[a0.IndexOf("--permission-mode") + 1]); // Vorgabe: bei Bedarf Internet und Befehle
            Assert.Contains("ausschließlich für das Studio", a0[a0.IndexOf("--append-system-prompt") + 1]); Assert.Contains("Apps zusammenklicken", a0[a0.IndexOf("--append-system-prompt") + 1]);
            Assert.NotNull(w.TryFind<Button>("claude-chat-undo")); Assert.Contains("Baue eine Kontaktseite", string.Join("|", w.GetLogicalTexts()));

            // zurücknehmen
            w.Find<Button>("claude-chat-undo").Press();
            Assert.False(File.Exists(Path.Combine(chat.Folder, "kontakt.html"))); Assert.Null(w.TryFind<Button>("claude-chat-undo"));

            // fortsetzen: dieselbe Sitzung
            await Run(w.ClaudeChatSend("Und jetzt ein Formular"));
            var a1 = calls[1].ToList(); Assert.Equal("sitzung-1", a1[a1.IndexOf("--resume") + 1]);
            Assert.Equal(chat.Id, w.ClaudeChatCurrent()!.Id);

            // neues Projekt, dann beide in der Liste; löschen nach Rückfrage
            w.Find<Button>("claude-chat-new").Press(); Assert.Null(w.ClaudeChatCurrent());
            await Run(w.ClaudeChatSend("Ein Rechner für Preise"));
            Assert.Equal(2, new ClaudeChats(w.Core.DataRoot).All("X-03").Count);
            Assert.NotEqual(chat.Folder, w.ClaudeChatCurrent()!.Folder);
            w.Find<Button>("claude-chat-del-1").Press(); // ältestes = erstes Projekt
            w.Find<Button>("claude-chat-del-yes").Press();
            Assert.Single(new ClaudeChats(w.Core.DataRoot).All("X-03"));
            Assert.Equal([chat.Folder], trashed);
            Assert.Contains(w.Core.Audit.ReadAll(), x => x.Action == "claude.chat.geloescht");

            // fortsetzen nach Neuöffnen: zuletzt benutztes Projekt
            w.Find<Button>("claude-dock-close").Press(); w.Find<Button>("auftrag-claude").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("Ein Rechner für Preise", w.ClaudeChatCurrent()!.Title);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ChatTrashOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude-Chat: ohne Claude Code „einrichten“, ohne Anmeldung „Mit Claude anmelden“ – im Testmodus startet nie ein echtes Claude")]
    public async Task Chat_not_ready()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            MainWindow.ClaudeStatusOverride = () => St(false, false); await Run(w.ClaudeCheck(true));
            w.Show("baukasten"); w.Find<Button>("auftrag-claude").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.TryFind<Button>("claude-chat-install")); Assert.False(w.Find<TextBox>("claude-chat-input").IsEnabled);
            MainWindow.ClaudeStatusOverride = () => St(true, false);
            w.Find<Button>("claude-chat-recheck").Press(); await Run(w.ClaudeCheck(true)); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            w.Find<Button>("claude-dock-close").Press(); w.Find<Button>("auftrag-claude").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.TryFind<Button>("claude-chat-login"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => w.ClaudeRun(["-p", "x"], Path.GetTempPath(), _ => { }, CancellationToken.None)); // Testmodus: nie echtes Claude
        }
        finally { MainWindow.ClaudeStatusOverride = null; w.Close(); }
    }
}
