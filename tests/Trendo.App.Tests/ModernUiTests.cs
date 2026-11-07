using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Modernisierung 68 (freigegeben 07.11.2026): globale Punkte G1–G12.</summary>
public class Modern_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "G10: KI-Aufträge der Grafikkarten-Warteschlange als Karten – läuft mit Laufzeit, wartet mit Platz; fertig = Karte weg")]
    public async Task Task_cards()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var t0 = DateTimeOffset.Now; w.TaskClockOverride = () => t0.AddSeconds(75);
            Assert.Empty(w.Find<StackPanel>("task-cards").Children);
            var a = await w.Gpu.AcquireAsync("musik", "Lied „Hafen“");
            var waiting = w.Gpu.AcquireAsync("bild", "Cover-Bild");
            Pump(); w.PaintTaskCards();
            var cards = w.Find<StackPanel>("task-cards").Children.OfType<Border>().ToList();
            Assert.Equal(2, cards.Count);
            var states = w.Find<StackPanel>("task-cards").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains(states, s => s!.StartsWith("Musik-KI · läuft seit 1:1"));
            Assert.Contains("Bild-KI · wartet · Platz 1", states);
            Assert.Contains("Lied „Hafen“", states);
            a.Dispose(); var b = await waiting; Pump(); w.PaintTaskCards();
            Assert.Single(w.Find<StackPanel>("task-cards").Children);
            b.Dispose(); Pump(); w.PaintTaskCards();
            Assert.Empty(w.Find<StackPanel>("task-cards").Children);
        }
        finally { w.Close(); }
    }
}
