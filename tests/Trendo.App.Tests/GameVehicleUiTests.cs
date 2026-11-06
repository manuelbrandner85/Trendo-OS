using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S1-05 (Teil 4): Fahrzeug im Editor setzen, Art „Ufo“ wählen (Rückgängig), im Probespiel einsteigen, fliegen, mit E aussteigen.</summary>
public class GameVehicle_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Fahrzeug Editor: Werkzeug da; Art Ufo wählen und rückgängig; Probespiel: einsteigen, Status zeigt Ufo mit Treibstoff, E steigt aus")]
    public void Vehicle()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.GameSection = "Level"; w.Show(GK); Pump();
        Assert.NotNull(w.TryFind<Button>("game-tile-fahrzeug"));
        var rows = MainWindow.GameLevelRows(d, 0); var sy = rows.FindIndex(r => r.Contains('S')); var sx = rows[sy].IndexOf('S');
        var row = rows[sy].ToCharArray(); row[sx + 1] = 'V';
        d.Set("level", new JsonArray([.. rows.Select((r, i) => (JsonNode)(i == sy ? new string(row) : r))]), "Level malen"); Pump();
        w.GameObjSel = (sx + 1, sy); w.Show(GK); Pump();
        w.Find<ComboBox>("game-obj-vehicle").SelectedIndex = 1; Pump();
        Assert.Equal("ufo", GameDesign.Texts(d.Data)[$"{sx + 1},{sy}:kind"]);
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.False(GameDesign.Texts(d.Data).ContainsKey($"{sx + 1},{sy}:kind"));
        w.Find<Button>("prog-redo").Press(); Pump();
        w.GamePlayHere(d); w.Show(GK); Pump();
        var c = w.GamePlaytestCanvas!; var sim = c.Sim;
        for (var i = 0; i < 200 && sim.Vehicle is null; i++) c.Tick(false, true, false);
        Assert.Equal("ufo", sim.Vehicle);
        Assert.Contains("Ufo (", w.Find<TextBlock>("game-play-status").Text);
        c.Tick(false, false, false, use: true);
        Assert.Null(sim.Vehicle);
        w.Close();
    }
}
