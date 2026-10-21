using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_X04_Ui
{
    [AvaloniaFact(DisplayName = "X-04 (UI): Beispiel installieren, im Baukasten öffnen, ausschalten sperrt Benutzen")]
    public void Use()
    {
        var (w, _, _) = Ui.Open();
        w.Show("erweiterungen");
        w.Find<Button>("ext-sample").Press();
        Assert.Single(w.Exts.All());
        w.Find<Button>("ext-use-0").Press();
        Assert.Equal("baukasten", w.CurrentView);
        Assert.Equal("Visitenkarte", w.BuilderPage.Title);
        w.Show("erweiterungen");
        w.Find<ToggleSwitch>("ext-on-0").IsChecked = false; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(w.Find<Button>("ext-use-0").IsEnabled);
        w.Close();
    }
}
