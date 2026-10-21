using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_Bus_Market_Ui
{
    [AvaloniaFact(DisplayName = "BUS-003/004/009 (UI): Mitbewerber, Problem und Test erfassen – Auswertung erscheint")]
    public void Market()
    {
        var (w, _, _) = Ui.Open();
        w.Show("biz-markt");
        w.Find<TextBox>("mk-c-name").Text = "A"; w.Find<TextBox>("mk-c-price").Text = "10"; w.Find<Button>("mk-c-add").Press();
        Assert.Contains("10", w.Find<TextBlock>("mk-price-pos").Text);
        w.Find<TextBox>("mk-p-text").Text = "Kein Termin frei"; w.Find<Button>("mk-p-add").Press();
        Assert.Contains("Kein Termin frei", w.Find<TextBlock>("mk-niche-0").Text);
        w.Find<Button>("mk-e-suggest").Press();
        w.Find<TextBox>("mk-e-res-2").Text = "4"; w.Find<Button>("mk-e-save-2").Press();
        Assert.StartsWith("1 von 1", w.Find<TextBlock>("mk-validation").Text);
        w.Close();
    }
}
