using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W4_X11_Ui
{
    [AvaloniaFact(DisplayName = "X-11 (UI): Vorhaben anlegen, Schritt abhaken, Fortschritt steigt")]
    public void Suite()
    {
        var (w, _, _) = Ui.Open();
        w.Show("vorhaben");
        w.Find<TextBox>("suite-name").Text = "Urlaub";
        w.Find<Button>("suite-kit-video").Press();
        var dir = Path.Combine(w.SuiteFolder, "Urlaub");
        Assert.True(File.Exists(Path.Combine(dir, CreationSuite.ListFile)));
        w.Find<CheckBox>("suite-step-0").IsChecked = true; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(CreationSuite.Percent(dir) > 0);
        Assert.True(w.Find<ProgressBar>("suite-progress").Value > 0);
        w.Close();
    }
}
