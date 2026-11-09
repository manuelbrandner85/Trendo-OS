using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0630: Ohne eine Auswahl arbeitet das Team, die Engine ist gewählt und sichtbar, und der Bau folgt der Wahl.</summary>
public class GameTools_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Automatik: Fachleute stehen ab Start auf „Automatisch“, die KI bekommt die Engine-Anweisung, Übernehmen hält die Wahl fest und zeigt sie")]
    public async Task Prompt_decides_engine()
    {
        var (w, _, _) = Ui.Open();
        w.EngineProbeOverride = () => EngineTargets.Summary([("5.8", "U")], _ => "U.exe", [], [("4.7", "G.exe")], "B.exe", _ => true);
        var asked = "";
        MainWindow.GamePromptOverride = (sys, _) =>
        {
            asked = sys;
            return Task.FromResult("```json\n{\"title\":\"Kleine Welt\",\"scale\":\"indie3d\",\"genre\":\"Abenteuer\",\"engine\":\"godot\",\"engineReason\":\"Kleines 3D-Spiel, Godot reicht\",\"blender\":true}\n```");
        };
        try
        {
            w.Show(GK); Pump();
            Assert.Equal("auto", w.GameCouncilMode);
            w.Find<TextBox>("game-prompt").Text = "Ein kleines 3D-Abenteuer";
            w.Find<Button>("game-prompt-new").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Contains("entscheidest du selbst", asked); Assert.Contains("Godot", asked);
            Assert.Contains("Engine: – → godot", w.GamePromptPreview);
            w.Find<Button>("game-prompt-accept").Press(); Pump();
            var d = w.ProgramActive(GK)!;
            Assert.Equal("godot", d.Text("engine")); Assert.True(d.Flag("blender"));
            var line = w.Find<TextBlock>("game-tool-choice").Text!;
            Assert.Contains("Godot", line); Assert.Contains("Blender", line); Assert.Contains("Kleines 3D-Spiel", line);
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "Automatik: gewählte Godot-Engine beim Plattformer ergibt beim Bauen ohne weiteren Klick das Godot-Projekt")]
    public void Build_follows_choice()
    {
        var (w, _, _) = Ui.Open();
        w.EngineProbeOverride = () => EngineTargets.Summary([], _ => "", [], [("4.7", "G.exe")], null, _ => true);
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        d.Set("engine", "godot"); Pump();
        w.Find<Button>("game-sec-bauen").Press(); Pump();
        w.Find<Button>("game-build").Press(); Pump();
        Assert.True(File.Exists(Path.Combine(d.Text("godotProject"), "project.godot")));
    }

    [AvaloniaFact(DisplayName = "Automatik: Claude erhält im Studio die Anweisung, sein Team ohne Rückfrage einzusetzen")]
    public void Claude_uses_team()
    {
        var (w, _, _) = Ui.Open();
        var role = w.ClaudeRoleFor("CORE-06");
        Assert.Contains(".claude/agents", role); Assert.Contains("ohne Rückfrage", role);
    }
}
