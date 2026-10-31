using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Cal = Trendo.Core.Sys.Calendar;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>O3 / Masterprompt 131: Kalender wie ein Kalenderprogramm – Zeitachse mit Ziehen (neu, verschieben, Dauer), Bearbeiten-Feld, Monat: auf anderen Tag ziehen, Rückgängig.</summary>
public class CalendarGrid_Ui
{
    static readonly DateTime Mon = new(2026, 11, 2);
    static Point At(MainWindow w, DateTime t) { Settle(); var g = w.CalGridForTests!; g.BringIntoView(new Rect(g.PointOf(t), new Size(1, 1))); Settle(); return g.TranslatePoint(g.PointOf(t), w)!.Value; }
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); Assert.True(b.IsEffectivelyEnabled, name + " ist gesperrt"); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }
    static void Drag(MainWindow w, Point a, Point b) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(a + (b - a) * (i / 5.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle(); }

    [AvaloniaFact(DisplayName = "Kalender: in der Woche ziehen legt Termin an, benennen, verschieben auf anderen Tag, Unterkante ziehen verlängert, Rückgängig; Monat: Termin auf anderen Tag ziehen")]
    public void Week_drag_edit_undo()
    {
        var (w, _, _) = Ui.Open();
        w.Width = 1600; w.Height = 1000;
        w.CalDay = Mon; w.CalMode = "woche"; w.Show("kalender"); Settle();
        Assert.NotNull(w.TryFind<Grid>("cal-week"));
        // Di 10:00 → 11:30 aufziehen
        Drag(w, At(w, Mon.AddDays(1).AddHours(10).AddMinutes(5)), At(w, Mon.AddDays(1).AddHours(11).AddMinutes(25)));
        var ev = Assert.Single(w.Cal.All());
        Assert.Equal(Mon.AddDays(1).AddHours(10), ev.Start); Assert.Equal(Mon.AddDays(1).AddHours(11.5), ev.End);
        Assert.Equal(ev.Uid, w.CalSelected);
        // rechts benennen und übernehmen
        w.Find<TextBox>("cal-edit-title").Text = "Zahnarzt"; w.Find<TextBox>("cal-edit-loc").Text = "Praxis Muster";
        Click(w, "cal-edit-save");
        ev = w.Cal.Get(ev.Uid)!; Assert.Equal(("Zahnarzt", "Praxis Muster"), (ev.Title, ev.Location)); Assert.Equal(Mon.AddDays(1).AddHours(10), ev.Start);
        // auf Mittwoch 14:00 ziehen (Mitte des Termins greifen)
        var target = At(w, Mon.AddDays(2).AddHours(14).AddMinutes(45)); // Mitte eines 1,5-h-Termins ab 14:00
        var box = w.CalGridForTests!.BoxOf(ev.Uid)!.Value; var grab = w.CalGridForTests.TranslatePoint(box.Center, w)!.Value; // Termin 10:00–11:30 → Mitte 10:45
        Drag(w, grab, target);
        ev = w.Cal.Get(ev.Uid)!; Assert.Equal(Mon.AddDays(2).AddHours(14), ev.Start); Assert.Equal(TimeSpan.FromHours(1.5), ev.End - ev.Start);
        Assert.Contains("verschoben", w.Find<TextBlock>("cal-msg").Text);
        // Unterkante 30 min nach unten
        var edge = At(w, ev.End) - new Point(0, 4); // Unterkante des Termins
        Drag(w, edge, edge + new Point(0, w.CalGridForTests.HourHeight / 2));
        Assert.Equal(Mon.AddDays(2).AddHours(16), w.Cal.Get(ev.Uid)!.End);
        // Rückgängig: Dauer, dann Verschieben
        w.CalGridForTests!.Focus(); w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); w.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); Settle(); // Strg+Z
        Assert.Equal(Mon.AddDays(2).AddHours(15.5), w.Cal.Get(ev.Uid)!.End);
        Click(w, "cal-undo"); Assert.Equal(Mon.AddDays(1).AddHours(10), w.Cal.Get(ev.Uid)!.Start);
        // Monat: Termin vom Dienstag auf Freitag ziehen
        Click(w, "cal-mode-monat"); Settle();
        var chip = V<Button>(w, "cal-ev-0"); var from = chip.TranslatePoint(new Point(chip.Bounds.Width / 2, chip.Bounds.Height / 2), w)!.Value;
        var fri = w.GetVisualDescendants().OfType<Border>().First(b => b.Tag is DateTime d && d == Mon.AddDays(4));
        Drag(w, from, fri.TranslatePoint(new Point(fri.Bounds.Width / 2, fri.Bounds.Height - 10), w)!.Value);
        ev = w.Cal.Get(ev.Uid)!; Assert.Equal(Mon.AddDays(4).AddHours(10), ev.Start); Assert.Equal("Zahnarzt", ev.Title);
        // Löschen im Bearbeiten-Feld, Rückgängig holt ihn zurück
        w.CalSelected = ev.Uid; w.Show("kalender"); Settle();
        Click(w, "cal-edit-delete"); Assert.Empty(w.Cal.All());
        Click(w, "cal-undo"); Assert.Single(w.Cal.All());
    }

    [Fact(DisplayName = "Kalender-Kern: Raster 15 Minuten, Verschieben behält die Dauer, Ende nie vor Beginn + 15 min")]
    public void Core_shift_and_end()
    {
        Assert.Equal(new DateTime(2026, 11, 2, 10, 15, 0), Cal.Snap(new DateTime(2026, 11, 2, 10, 22, 0)));
        Assert.Equal(new DateTime(2026, 11, 2, 10, 30, 0), Cal.Snap(new DateTime(2026, 11, 2, 10, 23, 0)));
        var root = Path.Combine(Path.GetTempPath(), "trendo-cal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var c = new Cal(root); var e = new CalEvent("a", new DateTime(2026, 11, 2, 9, 0, 0), new DateTime(2026, 11, 2, 10, 0, 0), "Probe");
            c.Add(e);
            var n = c.Shift("a", TimeSpan.FromDays(1) + TimeSpan.FromHours(2)); Assert.Equal(new DateTime(2026, 11, 3, 11, 0, 0), n.Start); Assert.Equal(TimeSpan.FromHours(1), n.End - n.Start);
            Assert.Equal(n.Start.AddMinutes(15), c.SetEnd("a", n.Start.AddHours(-3)).End);
            Assert.Throws<InvalidOperationException>(() => { c.Shift("fehlt", TimeSpan.FromHours(1)); });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
