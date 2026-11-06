using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S1-05: Fähigkeiten an-/abschalten (ein Rückgängig-Schritt), Aufgabe mit Titel anlegen, Fortschritt im Probespiel, gebautes Spiel trägt beides.</summary>
public class Gameplay_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Gameplay: Doppelsprung einschalten und rückgängig; Aufgabe „Sammler“ anlegen; Probespiel zeigt Fortschritt; gebautes Spiel enthält Fähigkeit und Aufgabe")]
    public void Abilities_and_quests()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.GameSection = "Level"; w.Show(GK); Pump();
        var cb = w.Find<CheckBox>("game-ability-doppelsprung"); cb.IsChecked = true; Pump();
        Assert.Equal(["doppelsprung"], GameDesign.Abilities(d.Data));
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.Empty(GameDesign.Abilities(d.Data));
        w.Find<CheckBox>("game-ability-doppelsprung").IsChecked = true; Pump();
        w.Find<CheckBox>("game-ability-sprint").IsChecked = true; Pump();
        w.Find<TextBox>("game-rule-n").Text = "3"; w.Find<TextBox>("game-rule-title").Text = "Sammler";
        w.Find<Button>("game-rule-add").Press(); Pump();
        Assert.Equal("Sammler", GameDesign.Rules(d.Data).Single().Title);
        w.GamePlayHere(d); w.Show(GK); Pump();
        Assert.True(w.GamePlaytestCanvas!.Sim.Has("doppelsprung"));
        Assert.Contains("Sammler: Münzen gesammelt 0/3", w.Find<TextBlock>("game-play-status").Text);
        w.Find<Button>("game-play-stop").Press(); Pump();
        w.Find<Button>("game-sec-bauen").Press(); Pump(); w.Find<Button>("game-build").Press(); Pump();
        var cfg = File.ReadAllText(Path.Combine(d.Text("built"), "config.js"));
        Assert.Contains("\"abilities\":[\"doppelsprung\",\"sprint\"]", cfg); Assert.Contains("\"title\":\"Sammler\"", cfg);
        w.Close();
    }
}
