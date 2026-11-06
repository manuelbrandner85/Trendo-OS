using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Paket 01 / S1-14: Schalter „Startmenü, Pause, Einstellungen und Karte“ im Editor – an als Vorgabe, aus und rückgängig, landet im gebauten Spiel.</summary>
public class GameMenus_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Spielmenü Editor: Schalter an als Vorgabe; aus → gebautes Spiel ohne Menü; rückgängig → wieder an; Spiel enthält Menü-Code")]
    public void Menu_switch()
    {
        var (w, _, _) = Ui.Open();
        w.Show(GK); Pump(); w.Find<Button>("prog-tpl-retro-plattform").Press(); Pump();
        var d = w.ProgramActive(GK)!;
        w.GameSection = "Level"; w.Show(GK); Pump();
        Assert.True(w.Find<CheckBox>("game-set-menu").IsChecked);
        w.Find<CheckBox>("game-set-menu").IsChecked = false; Pump();
        w.Find<Button>("game-sec-bauen").Press(); Pump(); w.Find<Button>("game-build").Press(); Pump();
        Assert.Contains("\"menu\":false", File.ReadAllText(Path.Combine(d.Text("built"), "config.js")));
        w.Find<Button>("prog-undo").Press(); Pump(); // Bauen ändert nur „built“, davor der Schalter
        while (d.Data["menu"] is not null && w.TryFind<Button>("prog-undo") is { IsEnabled: true } u && d.Data["menu"]!.GetValue<bool>() == false) { u.Press(); Pump(); }
        w.Find<Button>("game-build").Press(); Pump();
        var built = d.Text("built");
        Assert.Contains("\"menu\":true", File.ReadAllText(Path.Combine(built, "config.js")));
        Assert.Contains("function drawMenu()", File.ReadAllText(Path.Combine(built, "game.js")));
        Assert.Contains("max-width:100%", File.ReadAllText(Path.Combine(built, "index.html")));
        w.Close();
    }
}
