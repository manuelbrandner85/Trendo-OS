using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Iciclecreek.Terminal;
using Trendo.App;
using Trendo.Core;
using Avalonia.VisualTree;
using Xunit;

namespace Trendo.App.Tests;

/// Claude Code im Studio-Fenster: Knopf in jeder Auftragsleiste, Fläche rechts im Fenster; ohne Installation „einrichten“, ohne Anmeldung „anmelden“;
/// mit Claude läuft ein echtes Terminal im Projektordner (hier mit einem Testprogramm statt Claude), bleibt beim Neuzeichnen erhalten und endet mit dem Fenster.
[Collection("Claude")]
public class ClaudeDock_Ui
{
    static Task<ClaudeCode.Status> St(bool inst, bool on) => Task.FromResult(inst ? new ClaudeCode.Status(true, on, on ? "claude.ai" : "", on ? "max" : "", @"C:\x\claude.exe") : ClaudeCode.Status.Missing);
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "Claude im Studio: Knopf in jedem Studio, Fläche rechts; nicht installiert → einrichten, nicht angemeldet → anmelden, Testmodus startet nichts")]
    public async Task Dock_states()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var key in new[] { "baukasten", "brief", "vorhaben" }) { w.Show(key); Assert.NotNull(w.TryFind<Button>("auftrag-claude")); }
            MainWindow.ClaudeStatusOverride = () => St(false, false); await Run(w.ClaudeCheck(true));
            w.Show("baukasten"); w.Find<Button>("auftrag-claude").Press();
            Assert.True(w.ClaudeDockOpen(w.ActiveWindowId!)); Assert.NotNull(w.TryFind<Grid>("claude-split"));
            Assert.NotNull(w.TryFind<Button>("claude-dock-install"));
            w.Find<Button>("claude-dock-close").Press(); Assert.False(w.ClaudeDockOpen(w.ActiveWindowId!)); Assert.Null(w.TryFind<Grid>("claude-split"));
            MainWindow.ClaudeStatusOverride = () => St(true, false); await Run(w.ClaudeCheck(true));
            w.Find<Button>("auftrag-claude").Press(); Assert.NotNull(w.TryFind<Button>("claude-dock-login"));
            w.Find<Button>("claude-dock-close").Press();
            MainWindow.ClaudeStatusOverride = () => St(true, true); await Run(w.ClaudeCheck(true));
            w.Find<Button>("auftrag-claude").Press();
            Assert.Contains("Testmodus", string.Join("|", w.GetLogicalTexts()));
            Assert.Null(w.TryFind<TerminalControl>("claude-term")); // im Testmodus startet kein echtes Claude
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.dock");
            // Reiter „Claude“: erst einmal anmelden, danach eigener Chat je Studio (im Testmodus wird nichts geladen)
            var win = w.ActiveWindowId!;
            Assert.Equal("code", w.ClaudeDockTab(win));
            w.Find<Button>("claude-tab-web").Press();
            Assert.Equal("web", w.ClaudeDockTab(win)); Assert.True(w.Find<Border>("claude-web-pane").IsVisible);
            Assert.Equal(ClaudeWeb.Login, w.ClaudeDockWebUrl(win));
            Assert.NotNull(w.TryFind<Button>("claude-web-login")); Assert.Null(w.TryFind<Button>("claude-web-copy"));
            w.ClaudeWebNavigated("https://claude.ai/login"); Assert.False(w.CWeb.Connected);
            w.ClaudeWebNavigated("https://claude.ai/new"); Assert.True(w.CWeb.Connected); // angemeldet
            Assert.StartsWith("✓ Mit Claude verbunden", w.Find<TextBlock>("claude-web-state").Text);
            w.ClaudeWebNavigated("https://claude.ai/chat/0f3a9c2e-1111-2222-3333-444455556666");
            Assert.Equal("https://claude.ai/chat/0f3a9c2e-1111-2222-3333-444455556666", w.CWeb.StartUrl("X-03"));
            w.Find<TextBox>("auftrag-prompt").Text = "Baue eine Kontaktseite";
            var first = await w.ClaudeWebCopy(); // nur Zwischenablage – Trendo bedient claude.ai nie selbst
            Assert.StartsWith("[Trendo OS · Studio „", first); Assert.EndsWith("Mein Auftrag: Baue eine Kontaktseite", first);
            Assert.Equal(first, w.LastClipboardText); Assert.StartsWith("Kopiert", w.Find<TextBlock>("claude-web-msg").Text);
            Assert.Equal("Und die Farben", await w.ClaudeWebCopy("Und die Farben"));
            w.Find<Button>("claude-web-code").Press(); Assert.Equal(MainWindow.ClaudeWebCode, w.ClaudeDockWebUrl(win));
            w.Find<Button>("claude-tab-code").Press(); Assert.False(w.Find<Border>("claude-web-pane").IsVisible);
            // KI-Seite zeigt die Verbindung
            w.Show("ki-quellen"); Assert.StartsWith("✓ Claude im Web verbunden", w.Find<TextBlock>("claude-web-connected").Text);
            w.Find<Button>("claude-web-disconnect").Press(); Assert.False(w.CWeb.Connected);
            w.Show("ki-quellen"); w.Find<Button>("claude-web-connect").Press(); Assert.Equal("claude-verbinden", w.CurrentView);
            Assert.NotNull(w.TryFind<Border>("claude-connect-web"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.web");
        }
        finally { MainWindow.ClaudeStatusOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude im Studio: echtes Terminal im Projektordner, Auftrag als Eingabe, Fläche bleibt beim Neuzeichnen dieselbe, Fenster schließen beendet sie")]
    public async Task Terminal_runs()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeTerminalOverride = ("cmd.exe", ["/k", "echo TRENDO-TERMINAL"]);
        try
        {
            w.Show("baukasten"); var win = w.ActiveWindowId!;
            w.Find<Button>("auftrag-claude").Press();
            var term = w.ClaudeDockTerminal(win)!; Assert.NotNull(term);
            for (var i = 0; i < 100 && !term.IsLive; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            Assert.True(term.IsLive, $"vorlage={term.Template is not null} imBaum={term.IsAttachedToVisualTree()} groesse={term.Bounds} sichtbar={term.IsEffectivelyVisible} pid={term.Pid} meldung={w.ClaudeMessage}");
            Assert.True(Directory.Exists(w.ClaudeFolderFor("X-03")));
            w.Show("baukasten"); w.Show("baukasten"); // Neuzeichnen
            Assert.Same(term, w.ClaudeDockTerminal(win)); Assert.True(term.IsLive);
            w.ClaudeDockSelectTab("web"); w.ClaudeDockSelectTab("code"); Assert.True(term.IsLive); // Reiterwechsel beendet Claude Code nicht
            var typed = await w.ClaudeDockType("echo ZWEITE\nZEILE"); // nur eintippen – Enter drückt der Nutzer
            Assert.Equal("echo ZWEITE ZEILE", typed); Assert.DoesNotContain('\r', typed!); Assert.DoesNotContain('\n', typed!);
            w.CloseWindow(win);
            Assert.False(w.ClaudeDockOpen(win));
            await Until(() => !term.IsLive);
        }
        finally { MainWindow.ClaudeTerminalOverride = null; w.Close(); }
    }
}
