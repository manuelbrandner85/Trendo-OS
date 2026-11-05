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
        w.Find<Button>("suite-mode-liste").Press();
        w.Find<CheckBox>("suite-step-0").IsChecked = true; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(CreationSuite.Percent(dir) > 0);
        Assert.True(w.Find<ProgressBar>("suite-progress").Value > 0);
        w.Close();
    }
    [AvaloniaFact(DisplayName = "Vorhaben: „In den Kalender“ trägt die Fälligkeiten ein, zweites Mal ohne Doppelte, erledigter Schritt verschwindet wieder aus dem Kalender")]
    public void Due_to_calendar()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("vorhaben"); w.Find<TextBox>("suite-name").Text = "Urlaub"; w.Find<Button>("suite-kit-video").Press();
            var dir = Path.Combine(w.SuiteFolder, "Urlaub"); var steps = CreationSuite.Steps(dir);
            var withDue = steps.Count(s => s.Due is not null && !s.Done); Assert.True(withDue > 0, "Vorlage ohne Fälligkeiten");
            w.SuiteMode = "uebersicht"; w.Show("vorhaben");
            w.Find<Button>("suite-to-cal").Press();
            Assert.Equal(withDue, w.Cal.All().Count(e => e.Uid.StartsWith("vorhaben-")));
            w.Find<Button>("suite-to-cal").Press();
            Assert.Equal(withDue, w.Cal.All().Count(e => e.Uid.StartsWith("vorhaben-")));
            var first = steps.First(s => s.Due is not null && !s.Done); CreationSuite.Toggle(dir, first.Line);
            w.Show("vorhaben"); w.Find<Button>("suite-to-cal").Press();
            Assert.Equal(withDue - 1, w.Cal.All().Count(e => e.Uid.StartsWith("vorhaben-")));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "vorhaben.kalender");
        }
        finally { w.Close(); }
    }
}
