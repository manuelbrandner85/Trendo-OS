using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude im Studio-Fenster wie Claude im Web oder in der App: Knopf in jeder Auftragsleiste, echtes claude.ai (im Testmodus nicht geladen), groß wie in der App,
/// kein Terminal, nichts zu kopieren. Einmal anmelden; links die Claude-Projekte des Studios (neu, fortsetzen, entfernen) mit Rolle, Fachregeln und Fachgebiet.
[Collection("Claude")]
public class ClaudeDock_Ui
{
    [AvaloniaFact(DisplayName = "Claude im Studio: wie Claude im Web – claude.ai im ganzen Fenster, kein Terminal; anmelden, Projekte des Studios anlegen, fortsetzen, entfernen")]
    public async Task Dock_like_claude()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var key in new[] { "baukasten", "brief", "vorhaben" }) { w.Show(key); Assert.NotNull(w.TryFind<Button>("auftrag-claude")); }
            w.Show("baukasten");
            w.Find<TextBox>("auftrag-prompt").Text = "Baue eine Kontaktseite";
            w.Find<Button>("auftrag-claude").Press();
            var win = w.ActiveWindowId!;
            Assert.True(w.ClaudeDockOpen(win)); Assert.NotNull(w.TryFind<Grid>("claude-split"));
            Assert.Null(w.LastClipboardText); // nichts kopiert – man tippt direkt in Claude
            Assert.Equal("chat", w.ClaudeDockTab(win)); // startet mit dem Claude-Chat; claude.ai ist der zweite Reiter
            w.Find<Button>("claude-tab-web").Press(); Assert.Equal("web", w.ClaudeDockTab(win));
            Assert.True(w.ClaudeDockIsMax(win)); Assert.Equal("⤡ Studio zeigen", w.Find<Button>("claude-dock-max").Content);
            Assert.Null(w.TryFind<Button>("claude-tab-code")); Assert.Contains("Testmodus", string.Join("|", w.GetLogicalTexts()));
            w.Find<Button>("claude-dock-max").Press(); Assert.False(w.ClaudeDockIsMax(win)); Assert.Equal("⤢ Groß", w.Find<Button>("claude-dock-max").Content);
            w.Find<Button>("claude-dock-max").Press(); Assert.True(w.ClaudeDockIsMax(win));
            var pane = w.Find<Border>("claude-web-pane");
            w.Show("baukasten"); w.Show("baukasten"); Assert.Same(pane, w.Find<Border>("claude-web-pane")); // Neuzeichnen lädt claude.ai nicht neu

            // einmal anmelden – Anmeldelink aus der E-Mail nur von claude.ai
            Assert.Equal(ClaudeWeb.Login, w.ClaudeDockWebUrl(win));
            Assert.NotNull(w.TryFind<Button>("claude-web-login")); Assert.Null(w.TryFind<Button>("claude-newproj-go"));
            w.Find<TextBox>("claude-web-link").Text = "https://boese.example/claude.ai"; w.Find<Button>("claude-web-link-open").Press();
            Assert.Contains("kein Anmeldelink", w.Find<TextBlock>("claude-web-link-msg").Text); Assert.Equal(ClaudeWeb.Login, w.ClaudeDockWebUrl(win));
            w.Find<TextBox>("claude-web-link").Text = "https://claude.ai/magic-link#abc123"; w.Find<Button>("claude-web-link-open").Press();
            Assert.Equal("https://claude.ai/magic-link#abc123", w.ClaudeDockWebUrl(win));
            w.ClaudeWebNavigated("https://claude.ai/login"); Assert.False(w.CWeb.Connected);
            w.ClaudeWebNavigated("https://claude.ai/new"); Assert.True(w.CWeb.Connected);
            Assert.Contains("Leg links ein Projekt an", w.Find<TextBlock>("claude-web-state").Text);

            // Projekt anlegen: Name in Trendo, Anweisungen (Rolle, Fachregeln, Fachgebiet) einmal bereit, Projekte von Claude offen
            w.Find<TextBox>("claude-newproj-name").Text = "Kontaktformular";
            w.Find<Button>("claude-newproj-go").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Contains("ausschließlich für das Studio", w.LastClipboardText); Assert.Contains("Apps zusammenklicken", w.LastClipboardText);
            Assert.Equal(ClaudeWeb.Projects, w.ClaudeDockWebUrl(win)); Assert.StartsWith("Neues Projekt „Kontaktformular“", w.Find<TextBlock>("claude-web-state").Text);
            w.ClaudeWebNavigated("https://claude.ai/project/0f3a9c2e-1111-2222-3333-444455556666");
            Assert.Equal("0f3a9c2e-1111-2222-3333-444455556666", w.CWeb.ProjectOf("X-03"));
            Assert.StartsWith("✓ Projekt „Kontaktformular“", w.Find<TextBlock>("claude-web-msg").Text);
            Assert.Contains("Kontaktformular", string.Join("|", w.GetLogicalTexts()));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.web.projekt");

            // fremdes Projekt nicht von selbst, aber übernehmbar; fortsetzen öffnet das Projekt
            w.ClaudeWebNavigated("https://claude.ai/project/eeeeeeee-1111-2222-3333-444455556666");
            Assert.Single(w.CWeb.ProjectsOf("X-03"));
            w.Find<Button>("claude-proj-take").Press(); Assert.Equal(2, w.CWeb.ProjectsOf("X-03").Count);
            w.Find<Button>("claude-proj-1").Press(); Assert.StartsWith("https://claude.ai/project/", w.ClaudeDockWebUrl(win));
            w.Find<Button>("claude-web-chat").Press(); Assert.StartsWith("https://claude.ai/project/", w.ClaudeDockWebUrl(win)); // neuer Chat im Projekt
            w.Find<Button>("claude-proj-del-0").Press(); Assert.Single(w.CWeb.ProjectsOf("X-03"));
            Assert.Contains("aus der Liste entfernt", w.Find<TextBlock>("claude-web-msg").Text);
            w.Find<Button>("claude-web-code").Press(); Assert.Equal(MainWindow.ClaudeWebCode, w.ClaudeDockWebUrl(win));

            // Trendo-Agent als zweiter Reiter; Schließen
            w.Find<Button>("claude-tab-agent").Press(); Assert.False(w.Find<Border>("claude-web-pane").IsVisible);
            w.Find<Button>("claude-dock-close").Press(); Assert.False(w.ClaudeDockOpen(win)); Assert.Null(w.TryFind<Grid>("claude-split"));

            // KI-Seite zeigt die Verbindung; Trennen meldet wirklich ab
            w.Show("ki-quellen"); Assert.StartsWith("✓ Claude im Web verbunden", w.Find<TextBlock>("claude-web-connected").Text);
            w.Find<Button>("claude-web-disconnect").Press(); Assert.False(w.CWeb.Connected);
            Assert.Equal("claude-verbinden", w.CurrentView); Assert.Equal(ClaudeWeb.Logout, w.ClaudeConnectUrl);
            w.Show("ki-quellen"); w.Find<Button>("claude-web-connect").Press(); Assert.Equal("claude-verbinden", w.CurrentView);
            Assert.NotNull(w.TryFind<Border>("claude-connect-web"));
            w.Find<TextBox>("claude-connect-link").Text = "https://claude.ai/magic-link#xyz"; w.Find<Button>("claude-connect-link-open").Press();
            Assert.Equal("https://claude.ai/magic-link#xyz", w.ClaudeConnectUrl);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.dock");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude im Studio: Fenster schließen schließt Claude")]
    public void Window_close()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("baukasten"); var win = w.ActiveWindowId!;
            w.Find<Button>("auftrag-claude").Press();
            w.CloseWindow(win); Assert.False(w.ClaudeDockOpen(win));
        }
        finally { w.Close(); }
    }
}
