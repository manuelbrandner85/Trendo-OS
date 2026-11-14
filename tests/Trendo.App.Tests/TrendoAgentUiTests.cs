using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Trendo-Agent (vereinfacht): Wunsch → Los → Rückfrage als Knopf → setzt um, prüft, bessert selbst nach → „Behalten?“ Ja oder Nein (alles zurück). KI nachgebildet.
[Collection("Claude")]
public class TrendoAgent_Ui
{
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "Trendo-Agent: Los → eine Rückfrage als Knopf → Umsetzen, Prüfung findet Fehler, bessert selbst nach → „Behalten?“; Nein stellt alles wieder her, Ja behält")]
    public async Task Agent_flow()
    {
        var (w, _, _) = Ui.Open();
        var prompts = new List<string>();
        const string Bad = "<html><body><h1>Kontakt</h1></body></html>";
        const string Good = "<!doctype html><html lang=\\\"de\\\"><head><meta name=\\\"viewport\\\" content=\\\"width=device-width\\\"><title>Kontakt</title></head><body><h1>Kontakt</h1></body></html>";
        w.AgentAskOverride = p =>
        {
            prompts.Add(p);
            if (p.Contains("Erstelle zuerst einen kurzen Plan")) return Task.FromResult("{\"zusammenfassung\":\"Kontaktseite\",\"plan\":[\"Seite anlegen\",\"Formular einbauen\"],\"fragen\":[{\"frage\":\"Mit Telefonnummer?\",\"optionen\":[\"Ja\",\"Nein\"]}]}");
            var html = p.Contains("bitte nachbessern") ? Good : Bad;
            return Task.FromResult("{\"zusammenfassung\":\"ok\",\"dateien\":[{\"pfad\":\"kontakt.html\",\"inhalt\":\"" + html + "\"}]}");
        };
        w.Show("baukasten"); var win = w.ActiveWindowId!;
        w.Find<Button>("auftrag-claude").Press();
        w.Find<Button>("claude-tab-agent").Press();
        Assert.NotNull(w.TryFind<Button>("agent-go"));
        var folder = w.ClaudeFolderFor("X-03"); var file = Path.Combine(folder, "kontakt.html");
        Assert.False(File.Exists(file));
        w.Find<TextBox>("agent-goal").Text = "Kontaktseite mit Formular"; w.Find<Button>("agent-go").Press();
        await Until(() => w.AgentStatus(win) == "Mit Telefonnummer?");
        Assert.Equal(["Seite anlegen", "Formular einbauen"], w.AgentSteps(win));
        w.Find<Button>("agent-answer-0").Press(); // „★ Ja“ – danach läuft alles von selbst
        await Until(() => w.AgentDone(win));
        Assert.Equal("Fertig. Behalten?", w.AgentStatus(win));
        Assert.Contains("Mit Telefonnummer?: Ja", prompts[1]);
        Assert.Contains(prompts, p => p.Contains("bitte nachbessern") && p.Contains("kein <title>")); // selbst nachgebessert
        Assert.Contains("<title>Kontakt</title>", File.ReadAllText(file));
        Assert.NotNull(w.TryFind<Button>("agent-keep"));
        // Nein: alles zurück
        w.Find<Button>("agent-undo").Press();
        Assert.False(File.Exists(file)); Assert.Equal("Alles ist wieder wie vorher.", w.AgentStatus(win));
        // nochmal, diesmal behalten
        await w.AgentGo("Kontaktseite mit Formular");
        Assert.Equal("Mit Telefonnummer?", w.AgentStatus(win)); w.Find<Button>("agent-answer-1").Press(); // „Nein“
        await Until(() => w.AgentDone(win));
        w.Find<Button>("agent-keep").Press();
        Assert.True(File.Exists(file)); Assert.Equal("✓ Behalten.", w.AgentStatus(win));
        Assert.Contains(w.Core.Audit.ReadAll(), x => x.Action == "agent.apply");
        Assert.Contains(w.Core.Audit.ReadAll(), x => x.Action == "agent.undo");
        w.Close();
    }
}
