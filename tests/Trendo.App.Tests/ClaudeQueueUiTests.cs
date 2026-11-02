using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Weiterschreiben, während Claude arbeitet (wie im Original): Nachricht wartet und kommt gleich danach dran; „Stopp & jetzt“ hält an und nimmt sie sofort.
/// Dazu: Claude-App-Knopf erkennt die installierte App (auch als App-Paket) und öffnet sonst die Download-Seite. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeQueue_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static string J(object o) => JsonSerializer.Serialize(o);

    [AvaloniaFact(DisplayName = "Claude: während der Arbeit weiterschreiben – Nachricht wartet sichtbar und kommt gleich danach dran; „Stopp & jetzt“ nimmt sie sofort")]
    public async Task Queue_and_stop_now()
    {
        var (w, _, _) = Ui.Open();
        var prompts = new List<string>(); var gate = new TaskCompletionSource();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = async (args, _, line, ct) =>
        {
            prompts.Add(args[1]);
            if (prompts.Count is 1 or 3) await gate.Task.WaitAsync(ct); // erster und dritter Lauf arbeiten „lange“
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Erledigt: " + args[1].Split('\n')[0] } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s1", num_turns = 1 }));
            return 0;
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var first = w.ClaudeChatSend("Baue eine Startseite");
            await Until(() => w.ClaudeChatRunning());
            Assert.True(w.Find<TextBox>("claude-chat-input").IsEnabled); // Eingabe bleibt offen
            w.ClaudeChatSubmit("Und mach die Überschrift blau");
            Assert.Equal(1, w.ClaudeChatQueued()); Assert.Contains("⏳ wartet", string.Join("|", w.GetLogicalTexts()));
            gate.SetResult(); await Run(first);
            Assert.Equal(2, prompts.Count); Assert.StartsWith("Und mach die Überschrift blau", prompts[1]); Assert.Equal(0, w.ClaudeChatQueued());

            gate = new TaskCompletionSource();
            var third = w.ClaudeChatSend("Füge ein Menü hinzu");
            await Until(() => w.ClaudeChatRunning() && prompts.Count == 3);
            w.ClaudeChatSubmit("Stopp – lieber ein Kontaktformular");
            w.Find<Button>("claude-chat-queue-now-0").Press(); // Claude hält an …
            await Run(third);
            Assert.Equal(4, prompts.Count); Assert.StartsWith("Stopp – lieber ein Kontaktformular", prompts[3]); // … und nimmt die neue Nachricht sofort
            Assert.Contains(w.ClaudeChatCurrent()!.Messages, m => m.Text.Contains("Angehalten"));
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude-App-Knopf: installiert (auch App-Paket über claude://) → öffnen; sonst die offizielle Download-Seite")]
    public void Claude_app_detect()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            MainWindow.ClaudeAppProbe = () => "claude://";
            w.Find<Button>("claude-chat-app").Press(); Assert.Contains("wird geöffnet", w.ClaudeMessage);
            MainWindow.ClaudeAppProbe = () => null;
            w.Find<Button>("claude-chat-app").Press(); Assert.Contains("nicht installiert", w.ClaudeMessage); Assert.Contains("Download-Seite", w.ClaudeMessage);
            Assert.Equal("https://claude.ai/download", MainWindow.ClaudeDownload);
        }
        finally { MainWindow.ClaudeAppProbe = null; w.Close(); }
    }
}
