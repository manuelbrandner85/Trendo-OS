using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_Bus_Launch_Ui
{
    [AvaloniaFact(DisplayName = "BUS-016/026/028/029/030 (UI): Angaben, Webseite bauen, Prüfung grün, Paket, Sicherung")]
    public void Launch()
    {
        var (w, _, _) = Ui.Open();
        w.Show("biz-start");
        w.Find<TextBox>("ln-owner").Text = "Max Muster"; w.Find<TextBox>("ln-street").Text = "Hauptstr. 1"; w.Find<TextBox>("ln-city").Text = "10115 Berlin"; w.Find<TextBox>("ln-mail").Text = "max@example.org";
        w.Find<CheckBox>("ln-consumers").IsChecked = false;
        w.Find<Button>("ln-build").Press();
        Assert.True(File.Exists(Path.Combine(w.BizSite, "impressum.html")));
        Assert.StartsWith("✓", w.Find<TextBlock>("ln-check-0").Text);
        w.Find<Button>("ln-package").Press();
        Assert.True(File.Exists(w.BizSite + ".zip"));
        w.Find<Button>("ln-backup").Press();
        Assert.NotNull(w.TryFind<Button>("ln-restore"));
        Assert.Contains("startklar", w.Find<TextBlock>("ln-ready").Text);
        w.Close();
    }
}
