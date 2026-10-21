using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Klickt jede Studio-Kachel über den echten Eingabeweg (Maus runter/hoch an der Bildschirmstelle), nicht über das Click-Ereignis.</summary>
public class StudioClickTests
{
    static void RealClick(Window w, Control c)
    {
        Dispatcher.UIThread.RunJobs();
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w) ?? throw new InvalidOperationException("nicht sichtbar: " + c.Name);
        // Was liegt wirklich an dieser Stelle? (fängt verdeckte/überlagerte Kacheln ab)
        var hit = w.InputHitTest(p) as Visual;
        Assert.True(hit is not null && (hit == c || hit.GetVisualAncestors().Contains(c)), $"{c.Name}: an der Stelle liegt {hit?.GetType().Name} statt der Kachel");
        w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
        for (var i = 0; i < 20; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
    }

    static async Task Settle() { for (var i = 0; i < 50; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    static List<Button> Buttons(Window w, string prefix) => w.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith(prefix) == true && b.IsEffectivelyVisible).ToList();

    [AvaloniaFact(DisplayName = "Startseite: jede Studio-Kachel öffnet per echtem Mausklick eine andere Seite")]
    public async Task Startseite_Kacheln()
    {
        var (w, _, _) = Ui.Open();
        w.Show("start"); await Settle();
        var names = Buttons(w, "tile-").Select(b => b.Name!).ToList();
        Assert.Equal(9, names.Count);
        foreach (var n in names)
        {
            w.Show("start"); await Settle();
            RealClick(w, Buttons(w, n).Single()); await Settle();
            Assert.NotEqual("start", w.CurrentView);
            Assert.NotEqual("studios", w.CurrentView);
        }
    }

    [AvaloniaFact(DisplayName = "Alle Studios + Bereiche: jede Karte ist klickbar und führt aus der Liste heraus")]
    public async Task Alle_Studios_Karten()
    {
        var (w, _, _) = Ui.Open();
        w.Show("studios"); await Settle();
        var names = Buttons(w, "studio-card-").Select(b => b.Name!).ToList();
        Assert.True(names.Count > 20, $"nur {names.Count} Karten");
        foreach (var n in names)
        {
            w.Show("studios"); await Settle();
            var card = Buttons(w, n).Single();
            card.BringIntoView(); await Settle();
            RealClick(w, card); await Settle();
            Assert.NotEqual("studios", w.CurrentView);
        }
    }

    [AvaloniaFact(DisplayName = "Jedes Studio mit Status IMPLEMENTED hat einen eigenen Bereich (keine Sackgasse)")]
    public void Kein_gebautes_Studio_ohne_Seite()
    {
        var (w, _, _) = Ui.Open();
        var missing = w.Studios.Where(s => s.ImplementationStatus is "IMPLEMENTED" or "TESTED" or "ACCEPTED" or "RELEASED" or "IN_PROGRESS" && w.PageForStudio(s.Id) is null).Select(s => s.Id).ToList();
        Assert.True(missing.Count == 0, "ohne Seite: " + string.Join(", ", missing));
    }
}
