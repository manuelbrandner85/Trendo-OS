using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S1-05 (Teil 3): Waffe wählen (Rückgängig), Gegner zum Boss machen, im Probespiel mit X angreifen, gebautes Spiel trägt die Waffe.</summary>
public class GameCombat_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Kampf Editor: Schwert wählen und rückgängig; Gegner 3 Trefferpunkte; Probespiel: Schwertschlag trifft, Boss verliert einen Punkt; gebautes Spiel hat die Waffe")]
    public void Weapon_and_boss()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.GameSection = "Level"; w.Show(GK); Pump();
        var weapon = w.Find<ComboBox>("game-weapon"); weapon.SelectedIndex = 1; Pump();
        Assert.Equal("schwert", GameDesign.Weapon(d.Data));
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.Equal("", GameDesign.Weapon(d.Data));
        w.Find<ComboBox>("game-weapon").SelectedIndex = 1; Pump();
        var rows = MainWindow.GameLevelRows(d, 0); var ey = rows.FindIndex(r => r.Contains('E')); var ex = rows[ey].IndexOf('E');
        w.GameObjSel = (ex, ey); w.Show(GK); Pump();
        var hp = w.Find<TextBox>("game-obj-hp"); hp.Text = "3"; hp.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
        w.GamePlayHere(d); w.Show(GK); Pump();
        var sim = w.GamePlaytestCanvas!.Sim; var boss = sim.Foes.First(f => f.Col == ex && f.Row == ey);
        Assert.Equal(3, boss.MaxHp);
        sim.Player.X = boss.X - 30; sim.Player.Y = boss.Y - 6; sim.Player.Facing = 1;
        w.GamePlaytestCanvas.Tick(false, false, false, attack: true);
        Assert.Equal(2, boss.Hp); Assert.True(sim.Swing > 0);
        w.Find<Button>("game-play-stop").Press(); Pump();
        w.Find<Button>("game-sec-bauen").Press(); Pump(); w.Find<Button>("game-build").Press(); Pump();
        var cfg = File.ReadAllText(Path.Combine(d.Text("built"), "config.js"));
        Assert.Contains("\"weapon\":\"schwert\"", cfg); Assert.Contains(":hp\":3", cfg);
        w.Close();
    }
}
