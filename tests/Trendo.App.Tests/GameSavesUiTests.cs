using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S1-15: Spielstand im Probespiel – speichern, nach „Nochmal“ fortsetzen, kaputte Datei verständlich abgelehnt; gebautes Spiel enthält die Einstellung.</summary>
public class GameSaves_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Spielstand Probespiel: Fortsetzen erst nach Speichern; Speichern hält Punkte/Leben/Level fest; nach „Nochmal“ setzt „Fortsetzen“ fort; kaputte Datei → Meldung; gebautes Spiel speichert")]
    public void Save_and_resume()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.GameSection = "Level"; w.GamePlayHere(d); w.Show(GK); Pump();
        Assert.False(w.Find<Button>("game-play-load").IsEnabled);
        for (var i = 0; i < 90; i++) w.GamePlaytestCanvas!.Sim!.Step(false, true, false); // ein Stück laufen
        w.Find<Button>("game-play-save").Press(); Pump();
        Assert.StartsWith("Gespeichert: Level 1", w.ProgramMessage(GK));
        var saved = GameSaves.Parse(File.ReadAllText(w.GameSavePath(d)), 1);
        Assert.True(w.Find<Button>("game-play-load").IsEnabled);
        w.Find<Button>("game-play-again").Press(); Pump();
        w.Find<Button>("game-play-load").Press(); Pump();
        Assert.StartsWith("Fortgesetzt:", w.ProgramMessage(GK));
        Assert.Equal((saved.Score, saved.Lives), (w.GamePlaytestCanvas!.Sim!.Score, w.GamePlaytestCanvas.Sim.Lives));
        File.WriteAllText(w.GameSavePath(d), "{kaputt");
        w.Find<Button>("game-play-load").Press(); Pump();
        Assert.Contains("beschädigt", w.ProgramMessage(GK));
        // gebautes Browser-Spiel: Spielstand an, Code eingebaut
        w.Find<Button>("game-play-stop").Press(); Pump();
        w.Find<Button>("game-sec-bauen").Press(); Pump(); w.Find<Button>("game-build").Press(); Pump();
        var built = d.Text("built");
        Assert.Contains("\"saves\":true", File.ReadAllText(Path.Combine(built, "config.js")));
        Assert.Contains("function saveGame()", File.ReadAllText(Path.Combine(built, "game.js")));
        w.Close();
    }
}
