using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_Bus_Brand_Ui
{
    [AvaloniaFact(DisplayName = "BUS-013/015/021/022 (UI): Name bewertet, Kundenweg-Schritt, SEO-Titel, Trichter-Ergebnis")]
    public void Brand()
    {
        var (w, _, _) = Ui.Open();
        w.Show("biz-marke");
        Assert.Contains("/100", w.Find<TextBlock>("br-name-score").Text);
        w.Find<TextBox>("br-t-where").Text = "Google"; w.Find<Button>("br-t-add").Press();
        Assert.Single(w.Touchpoints.All());
        w.Find<TextBox>("br-seo-kw").Text = "fahrrad reparatur"; w.Find<Button>("br-seo-check").Press();
        Assert.StartsWith("Titel: fahrrad reparatur", w.Find<TextBlock>("br-seo-title").Text);
        Assert.Contains("Interessenten", w.Find<TextBlock>("br-f-result").Text);
        w.Close();
    }
}
