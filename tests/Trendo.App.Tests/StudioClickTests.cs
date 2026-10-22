using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Klickt jede Studio-Kachel über den echten Eingabeweg (Maus runter/hoch an der Bildschirmstelle), nicht über das Click-Ereignis.</summary>
public class StudioClickTests
{
    const int Teile = 6;

    /// <summary>Wartet, bis die Oberfläche die Bedingung erfüllt. Keine feste Pause von 50×10 ms.</summary>
    static async Task Until(Func<bool> ready)
    {
        var ende = Environment.TickCount64 + 4000;
        while (!ready())
        {
            if (Environment.TickCount64 > ende)
                throw new TimeoutException("Die Oberfläche hat nicht rechtzeitig reagiert.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }
    }

    /// <summary>Schiebt das Element ins Fenster, bevor der Klick die Bildschirmstelle trifft.</summary>
    static async Task InsBild(Window w, Control c)
    {
        c.BringIntoView();
        await Until(() =>
        {
            var p = c.TranslatePoint(new Point(8, 8), w);
            return c.Bounds.Width > 8 && p is { } pt && pt.X >= 0 && pt.Y >= 0 && pt.X < w.Bounds.Width && pt.Y < w.Bounds.Height;
        });
    }

    static void RealClick(Window w, Control c)
    {
        Dispatcher.UIThread.RunJobs();
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w) ?? throw new InvalidOperationException("nicht sichtbar: " + c.Name);
        var hit = w.InputHitTest(p) as Visual;
        Assert.True(hit is not null && (hit == c || hit.GetVisualAncestors().Contains(c)), $"{c.Name}: an der Stelle liegt {hit?.GetType().Name} statt der Kachel");
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    static List<Button> Buttons(Window w, string prefix) => w.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith(prefix) == true && b.IsEffectivelyVisible).ToList();

    /// <summary>Programm-Prüfung im Kartentest: sofort „fehlt“, ohne ffmpeg, Blender und Co. zu starten.</summary>
    static void OhneEchteProgramme(MainWindow w) =>
        w.DependencyProbe = c => Task.FromResult(new ToolCheck(c, ToolState.Missing, null, null, "Kartentest: Programm wird nicht gestartet"));

    [AvaloniaFact(DisplayName = "Startseite: die neun bekannten Studios öffnen aus dem Startmenü per echtem Mausklick eine eigene Seite")]
    public async Task Startseite_Kacheln()
    {
        var (w, _, _) = Ui.Open();
        OhneEchteProgramme(w);
        string[] ids = ["STUDIO-ONLINE-BUSINESS-01", "STUDIO-LETTER-01", "CORE-05", "CORE-06", "SYS-10", "CORE-02", "CORE-08", "CORE-07", "CORE-11"];
        foreach (var id in ids)
        {
            w.Show("start");
            await Until(() => Buttons(w, "start-button").Count == 1);
            RealClick(w, Buttons(w, "start-button").Single());
            await Until(() => w.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "sm-az-" + id));
            var item = w.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "sm-az-" + id);
            await InsBild(w, item);
            RealClick(w, item);
            await Until(() => w.CurrentView != "start" && w.CurrentView != "studios");
            Assert.NotEqual("start", w.CurrentView);
            Assert.NotEqual("studios", w.CurrentView);
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Alle Studios + Bereiche: jede Karte ist klickbar, Teil 1 von 6")]
    public Task Karten_1() => KartenTeil(0);

    [AvaloniaFact(DisplayName = "Alle Studios + Bereiche: jede Karte ist klickbar, Teil 2 von 6")]
    public Task Karten_2() => KartenTeil(1);

    [AvaloniaFact(DisplayName = "Alle Studios + Bereiche: jede Karte ist klickbar, Teil 3 von 6")]
    public Task Karten_3() => KartenTeil(2);

    [AvaloniaFact(DisplayName = "Alle Studios + Bereiche: jede Karte ist klickbar, Teil 4 von 6")]
    public Task Karten_4() => KartenTeil(3);

    [AvaloniaFact(DisplayName = "Alle Studios + Bereiche: jede Karte ist klickbar, Teil 5 von 6")]
    public Task Karten_5() => KartenTeil(4);

    [AvaloniaFact(DisplayName = "Alle Studios + Bereiche: jede Karte ist klickbar, Teil 6 von 6")]
    public Task Karten_6() => KartenTeil(5);

    /// <summary>Jede Karte mit Index i landet in Teil i % 6. Zusammen sind das alle Karten, keine fällt weg.</summary>
    async Task KartenTeil(int teil)
    {
        var (w, _, _) = Ui.Open();
        OhneEchteProgramme(w);
        w.Show("studios");
        await Until(() => Buttons(w, "studio-card-").Count > 20);
        var names = Buttons(w, "studio-card-").Select(b => b.Name!).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(names.Count > 20, $"nur {names.Count} Karten");
        var meine = names.Where((_, i) => i % Teile == teil).ToList();
        var basis = names.Count / Teile;
        var rest = names.Count % Teile;
        Assert.Equal(basis + (teil < rest ? 1 : 0), meine.Count);
        foreach (var n in meine)
        {
            w.Show("studios");
            await Until(() => Buttons(w, "studio-card-").Any(b => b.Name == n));
            var card = Buttons(w, "studio-card-").Single(b => b.Name == n);
            await InsBild(w, card);
            RealClick(w, card);
            await Until(() => w.CurrentView != "studios");
            Assert.NotEqual("studios", w.CurrentView);
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Jedes Studio mit Status IMPLEMENTED hat einen eigenen Bereich (keine Sackgasse)")]
    public void Kein_gebautes_Studio_ohne_Seite()
    {
        var (w, _, _) = Ui.Open();
        var missing = w.Studios.Where(s => s.ImplementationStatus is "IMPLEMENTED" or "TESTED" or "ACCEPTED" or "RELEASED" or "IN_PROGRESS" && w.PageForStudio(s.Id) is null).Select(s => s.Id).ToList();
        Assert.True(missing.Count == 0, "ohne Seite: " + string.Join(", ", missing));
        w.Close();
    }
}
