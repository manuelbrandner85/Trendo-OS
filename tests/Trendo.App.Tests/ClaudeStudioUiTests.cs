using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude und die Studios (0.9.34): Startideen, Rückfragen als Knöpfe, Anhänge, Studio-Zustand, Abo-Verbrauch, automatische Prüfung,
/// Ergebnis links im passenden Programm, volle Funktionen nur mit Freigabe. Claude ist nachgebildet – kein echtes Abo.
[Collection("Claude")]
public class ClaudeStudio_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<int, string, (string? File, string? Content, string Answer)> step) => (args, dir, line, _) =>
    {
        calls.Add(args);
        var (file, content, answer) = step(calls.Count, dir);
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        if (file is not null)
        {
            var path = Path.Combine(dir, file); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = path, content } } } } }));
            File.WriteAllText(path, content);
            line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
        }
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = answer } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = answer, session_id = "s1", num_turns = 1, usage = new { input_tokens = 1200, output_tokens = 300, cache_read_input_tokens = 500 } }));
        return Task.FromResult(0);
    };
    static string Arg(IReadOnlyList<string> a, string name) { var l = a.ToList(); return l[l.IndexOf(name) + 1]; }

    [AvaloniaFact(DisplayName = "Claude im Studio: Startideen, Rückfrage als Knöpfe, Anhang landet im Projekt, Abo-Verbrauch je Antwort")]
    public async Task Ideas_questions_attachments_usage()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, (n, _) => n == 1 ? (null, null, "Gern!\nFrage: Hell oder dunkel? [Hell | Dunkel]") : ("index.html", ClaudeChat_Ui.Page("<h1>Dunkel</h1>"), "Fertig."));
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.TryFind<Button>("claude-chat-idea-0"));
            Assert.Contains("Visitenkarten", (string)w.Find<Button>("claude-chat-idea-0").Content!);
            var pic = Path.Combine(root, "skizze.png"); File.WriteAllBytes(pic, [137, 80, 78, 71]);
            w.ClaudeChatAttach([pic]); Assert.NotNull(w.TryFind<Button>("claude-chat-attachment-del-0"));
            w.Find<Button>("claude-chat-idea-0").Press(); await Run(Task.Delay(50)); while (w.ClaudeChatRunning()) await Run(Task.Delay(20));
            var chat = w.ClaudeChatCurrent()!;
            Assert.True(File.Exists(Path.Combine(chat.Folder, "anhang", "skizze.png"))); Assert.Contains("anhang/skizze.png", calls[0][1]);
            Assert.Equal(["anhang/skizze.png"], chat.Messages[0].Attachments); Assert.Null(w.TryFind<Button>("claude-chat-attachment-del-0"));
            Assert.Equal("Hell oder dunkel?", w.Find<TextBlock>("claude-chat-question").Text);
            Assert.Contains("2.000 Tokens", w.Find<TextBlock>("claude-chat-usage").Text);
            w.Find<Button>("claude-chat-answer-1").Press(); while (w.ClaudeChatRunning() || calls.Count < 2) await Run(Task.Delay(20));
            Assert.StartsWith("Dunkel", calls[1][1]); Assert.Equal("s1", Arg(calls[1], "--resume"));
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude im Studio: Ergebnis erscheint links im passenden Programm – Webseite im Webseiten-Studio, Spiel im Spiele-Studio; Studio wird daneben eingeblendet")]
    public async Task Result_shows_in_studio()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, (_, _) => ("index.html", ClaudeChat_Ui.Page("<canvas></canvas>"), "Fertig."));
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var win = w.ActiveWindowId!; Assert.True(w.ClaudeDockIsMax(win));
            await Run(w.ClaudeChatSend("Baue eine Visitenkarte"));
            Assert.Equal(w.ClaudeChatCurrent()!.Folder, w.WebDir); // links im Webseiten-Studio offen
            Assert.StartsWith(w.DevFolder, w.WebDir);
            Assert.False(w.ClaudeDockIsMax(win)); // Studio daneben eingeblendet
            w.Find<Button>("claude-dock-close").Press();

            w.Show("spiele"); w.ClaudeDockToggle("CORE-06"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ein Snake-Spiel"));
            Assert.StartsWith(w.GameFolder, w.ClaudeChatCurrent()!.Folder);
            Assert.NotNull(w.TryFind<StackPanel>("game-claude")); Assert.NotNull(w.TryFind<Button>("game-claude-play"));
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude im Studio: Trendo prüft das Ergebnis und gibt Fehler einmal automatisch an Claude zurück")]
    public async Task Auto_check_once()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, (n, _) => ("index.html", n == 1 ? "<h1>ohne Kopf</h1>" : "<h1>immer noch ohne</h1>", "Fertig."));
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Seite"));
            Assert.Equal(2, calls.Count); // genau eine automatische Nachbesserung, keine Endlosschleife
            Assert.Contains("kein <title>", calls[1][1]);
            Assert.Equal(["user", "assistant", "check", "assistant"], w.ClaudeChatCurrent()!.Messages.Select(m => m.Role));
            Assert.NotNull(w.TryFind<Border>("claude-chat-check"));
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude im Studio: Auswahl in Trendo Tabelle geht als Zustand mit; Internet und Befehle bei Bedarf immer an, je Projekt auf „sicher“ stellbar; Vorgabe abschaltbar")]
    public async Task Context_and_full_mode()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, (_, _) => (null, null, "Erledigt."));
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("tabelle"); w.SheetSetCell("B3", "Miete"); w.SheetSelect("B3");
            Assert.Contains("B3 = „Miete“", w.ClaudeStudioContext("tabelle"));
            w.ClaudeDockToggle("SYS-10"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Mach die markierte Zelle fett"));
            Assert.Contains("[Gerade im Studio: Trendo Tabelle", calls[0][1]);
            Assert.DoesNotContain("--restricted", calls[0]); Assert.Equal("auto", Arg(calls[0], "--permission-mode")); // Vorgabe: bei Bedarf Internet und Befehle
            Assert.Equal("🔓 Internet & Befehle an", w.Find<Button>("claude-chat-full").Content);
            w.Find<Button>("claude-chat-full").Press(); w.Find<Button>("claude-chat-full-off").Press(); // dieses Projekt auf sicher
            Assert.True(w.ClaudeChatCurrent()!.Safe); Assert.Equal("🔒 Sicher", w.Find<Button>("claude-chat-full").Content);
            await Run(w.ClaudeChatSend("Jetzt sicher"));
            Assert.Contains("--restricted", calls[1]); Assert.Equal("acceptEdits", Arg(calls[1], "--permission-mode"));
            w.Find<Button>("claude-chat-full").Press(); w.Find<Button>("claude-chat-full-yes").Press();
            Assert.False(w.ClaudeChatCurrent()!.Safe); Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.chat.voll.an");
            KiSourceSettings.Update(w.Core.DataRoot, x => x.ClaudeAlwaysFull = false); // Vorgabe aus: neue Projekte sicher
            w.Find<Button>("claude-chat-new").Press(); await Run(w.ClaudeChatSend("Neues Projekt"));
            Assert.Contains("--restricted", calls[2]);
            w.Show("ki-quellen"); Assert.False(w.Find<CheckBox>("claude-always-full").IsChecked); w.Show("tabelle");
            w.Find<Button>("claude-chat-app").Press(); Assert.Contains("Testmodus", w.ClaudeMessage);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
