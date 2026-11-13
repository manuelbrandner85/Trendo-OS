using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

public class Lizenz_Ui
{
    [AvaloniaFact(DisplayName = "Commercial Phase 2: Seite Konto, Lizenz & Vollversion in den Einstellungen, Vorabversion, kaputter Code abgelehnt")]
    public void Lizenz_page()
    {
        var (w, _, _) = Ui.Open();
        Assert.Contains(MainWindow.SettingsGroups.SelectMany(g => g.Pages), p => p.Key == "lizenz");
        w.Show("lizenz");
        Assert.Equal("lizenz", w.CurrentView);
        Assert.Equal("Vorabversion: alle Studios frei", w.Find<TextBlock>("lic-headline").Text);
        Assert.NotEmpty(w.Find<StackPanel>("lic-third-party").Children);
        w.Find<TextBox>("lic-code").Text = "TRENDO1.kaputt.x";
        w.Find<Button>("lic-apply").Press();
        Assert.False(string.IsNullOrEmpty(w.Find<TextBlock>("lic-msg").Text));
        Assert.Null(w.Licenses.Current());
        Assert.Contains(w.SettingsSearch("vollversion"), p => p.Key == "lizenz");
    }
}
