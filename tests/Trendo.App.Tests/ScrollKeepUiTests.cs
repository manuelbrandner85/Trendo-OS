using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// Wunsch 03.11.2026: Nach einem Klick (z. B. „Sender sperren“) baut Trendo die Seite neu auf – die Scrollposition muss dabei stehen bleiben.
public class ScrollKeep_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    /// <summary>Größter sichtbarer Bildlaufbereich im aktiven Fenster.</summary>
    static ScrollViewer Main(MainWindow w) =>
        w.Find<Control>("win-" + w.Windows.Active!.Id).GetVisualDescendants().OfType<ScrollViewer>().Where(v => v.IsEffectivelyVisible).OrderByDescending(v => v.Extent.Height - v.Viewport.Height).First();

    [AvaloniaFact(DisplayName = "Scrollen bleibt stehen: dieselbe Seite neu aufgebaut (wie nach einem Knopfdruck) behält die Position – auch zweimal hintereinander")]
    public void Keeps_scroll_on_rerender()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("components"); Pump();
            var sv = Main(w);
            Assert.True(sv.Extent.Height - sv.Viewport.Height > 1000);
            sv.Offset = new Vector(0, 900); Pump();
            Assert.Equal(900, sv.Offset.Y, 1);
            w.Show("components"); Pump(); // Neuaufbau wie nach „Sperren“, „Speichern“ …
            Assert.NotSame(sv, Main(w)); // wirklich neu aufgebaut
            Assert.Equal(900, Main(w).Offset.Y, 1);
            w.Show("components"); Pump();
            Assert.Equal(900, Main(w).Offset.Y, 1);
        }
        finally { w.Close(); }
    }
}
