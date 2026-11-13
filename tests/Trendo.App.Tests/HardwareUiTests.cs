using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

public class Hardware_Ui
{
    [AvaloniaFact(DisplayName = "Commercial Desktop Edition: Hardware & Leistung prüft den PC, zeigt Profil, Systemcheck und Eignung je Studio; auch vor dem Kauf")]
    public void Hardware_page()
    {
        var (w, _, _) = Ui.Open();
        Assert.Contains(MainWindow.SettingsGroups.SelectMany(g => g.Pages), p => p.Key == "hardware");
        w.Show("hardware");
        Assert.Equal("Dein PC wird geprüft …", w.Find<TextBlock>("hw-profile").Text);
        Assert.True(w.HardwareScanTask!.Wait(10000));
        for (var i = 0; i < 100 && w.LastHardware is null; i++) { Thread.Sleep(20); W2Ui.Settle(); }
        W2Ui.Settle();
        Assert.NotNull(w.LastHardware);
        Assert.StartsWith("Leistungsprofil:", w.Find<TextBlock>("hw-profile").Text);
        Assert.True(w.Find<StackPanel>("hw-checks").Children.Count >= 10);
        Assert.Equal(StudioCatalog.ById.Count + 2, w.Find<StackPanel>("hw-studios").Children.Count);
        Assert.NotNull(w.Find<Button>("hw-internet")); // Internet nur auf Knopfdruck, im Test nicht gedrückt
        w.Show("lizenz");
        Assert.NotEmpty(w.Find<StackPanel>("lic-studios").Children);
        Assert.Contains(w.SettingsSearch("grafikkarte"), p => p.Key == "hardware");
    }
}
