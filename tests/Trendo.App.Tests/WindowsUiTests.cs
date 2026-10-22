using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

/// <summary>Fenster über den echten Eingabeweg: Mausklick auf Kachel, Taskleiste und Fensterknöpfe, Ziehen an der Titelleiste.</summary>
public class Fenster_Ui
{
    static Button Vis(Window w, string name) => V<Button>(w, name);

    [AvaloniaFact(DisplayName = "Fenster: Kachel-Klick öffnet Fenster mit Taskleisten-Eintrag, Startbildschirm bleibt dahinter")]
    public void TileOpensWindow()
    {
        var (w, _, _) = Ui.Open(); w.Show("start"); Settle();
        var tile = w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "tile-SYS-10" && b.IsEffectivelyVisible);
        RealClick(w, tile);
        var win = w.Windows.Active; Assert.NotNull(win);
        Assert.NotNull(w.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "win-" + win!.Id && b.IsEffectivelyVisible));
        Assert.NotNull(w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "task-" + win.Id && b.IsEffectivelyVisible));
        Assert.True(w.GetVisualDescendants().OfType<Control>().First(c => c.Name == "desktop-area").IsEffectivelyVisible);
        // Symbol aus der zentralen Studio-Symbolkarte im Taskleisten-Eintrag
        Assert.NotEmpty(V<Button>(w, "task-" + win.Id).GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        // Minimieren per Klick → Startbildschirm wieder klickbar
        RealClick(w, Vis(w, "win-min-" + win.Id)); Assert.Null(w.Windows.Active); Assert.Equal("start", w.CurrentView);
        RealClick(w, w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "tile-SYS-10" && b.IsEffectivelyVisible));
        Assert.Equal(win.Id, w.Windows.Active?.Id); Assert.Single(w.Windows.TaskOrder);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen")]
    public void SwitchMaxClose()
    {
        var (w, _, _) = Ui.Open();
        w.Show("settings"); w.Show("werkzeuge"); Settle();
        Assert.Equal(2, w.Windows.TaskOrder.Count); Assert.Equal("werkzeuge", w.Windows.Active!.Id);
        RealClick(w, Vis(w, "task-settings"));
        Assert.Equal("settings", w.Windows.Active!.Id); Assert.Equal("settings", w.CurrentView);
        var act = V<Border>(w, "win-settings"); var other = V<Border>(w, "win-werkzeuge");
        Assert.NotEqual(((Avalonia.Media.ISolidColorBrush)act.BorderBrush!).Color, ((Avalonia.Media.ISolidColorBrush)other.BorderBrush!).Color);
        Assert.True(act.ZIndex > other.ZIndex);
        RealClick(w, Vis(w, "task-settings")); Assert.True(w.Windows.Get("settings")!.Minimized); Assert.Equal("werkzeuge", w.Windows.Active!.Id);
        RealClick(w, Vis(w, "task-settings"));
        RealClick(w, Vis(w, "win-max-settings")); Assert.True(w.Windows.Get("settings")!.Maximized);
        Settle();
        var work = V<Panel>(w, "work-area"); var center = V<Canvas>(w, "window-area");
        Assert.Equal(work.Bounds.Width, act.Bounds.Width, 1);
        Assert.Equal(work.Bounds.Height, act.Bounds.Height, 1);
        Assert.True(act.Bounds.Width > center.Bounds.Width + 80, $"maximiert {act.Bounds.Width} soll breiter sein als die Mitte {center.Bounds.Width}");
        RealClick(w, Vis(w, "win-max-settings")); Assert.False(w.Windows.Get("settings")!.Maximized);
        RealClick(w, Vis(w, "win-close-settings")); Assert.Null(w.Windows.Get("settings"));
        Assert.Null(w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "task-settings"));
        Assert.Equal("werkzeuge", w.CurrentView);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster")]
    public void MoveAndFocus()
    {
        var (w, _, _) = Ui.Open();
        w.Show("settings"); w.Show("prog-pflege"); Settle();
        var win = w.Windows.Get("prog-pflege")!; var (x0, y0) = (win.X, win.Y);
        var bar = V<Border>(w, "win-titletext-prog-pflege".Replace("titletext", "title"));
        var p = bar.TranslatePoint(new Point(60, bar.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left); w.MouseMove(p + new Point(40, 30), RawInputModifiers.LeftMouseButton); w.MouseUp(p + new Point(40, 30), MouseButton.Left); Settle();
        Assert.True(win.X > x0 + 10 && win.Y > y0 + 10, $"{x0},{y0} → {win.X},{win.Y}");
        // Hinteres Fenster (Einstellungen) an einer freien Stelle anklicken
        var back = V<Border>(w, "win-settings"); var bp = back.TranslatePoint(new Point(10, back.Bounds.Height - 10), w)!.Value;
        w.MouseDown(bp, MouseButton.Left); w.MouseUp(bp, MouseButton.Left); Settle();
        Assert.Equal("settings", w.Windows.Active!.Id);
        // Registerkarte derselben Gruppe → gleiches Fenster
        w.Show("prog-start"); Assert.Equal("prog-pflege", w.Windows.Active!.Id); Assert.Equal("prog-start", w.CurrentView); Assert.Equal(2, w.Windows.TaskOrder.Count);
        w.Close();
    }

    static void Drag(Window w, Point a, Point b) { w.MouseDown(a, MouseButton.Left); w.MouseMove(a + (b - a) / 2, RawInputModifiers.LeftMouseButton); w.MouseMove(b, RawInputModifiers.LeftMouseButton); w.MouseUp(b, MouseButton.Left); Settle(); }

    [AvaloniaFact(DisplayName = "Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein")]
    public void ResizeAndSnap()
    {
        var (w, _, _) = Ui.Open(); w.Show("settings"); Settle();
        var win = w.Windows.Get("settings")!; var (w0, h0) = (win.W, win.H);
        var corner = V<Border>(w, "win-size-BR-settings"); var c = corner.TranslatePoint(new Point(6, 6), w)!.Value;
        Drag(w, c, c - new Point(80, 60));
        Assert.True(win.W < w0 - 40 && win.H < h0 - 30, $"{w0}x{h0} → {win.W}x{win.H}");
        var left = V<Border>(w, "win-size-L-settings"); var l = left.TranslatePoint(new Point(3, left.Bounds.Height / 2), w)!.Value;
        var x0 = win.X; Drag(w, l, l + new Point(30, 0)); Assert.True(win.X > x0 + 15);
        var area = V<Canvas>(w, "window-area"); var bar = V<Border>(w, "win-title-settings");
        var p = bar.TranslatePoint(new Point(80, 15), w)!.Value; var edge = area.TranslatePoint(new Point(2, 200), w)!.Value;
        Drag(w, p, edge);
        Assert.Equal((0d, 0d), (win.X, win.Y)); Assert.Equal(w.Windows.AreaW / 2, win.W, 1); Assert.Equal(w.Windows.AreaH, win.H, 1);
        p = V<Border>(w, "win-title-settings").TranslatePoint(new Point(80, 15), w)!.Value; var right = area.TranslatePoint(new Point(area.Bounds.Width - 2, 200), w)!.Value;
        Drag(w, p, right); Assert.Equal(w.Windows.AreaW / 2, win.X, 1);
        w.Close();
    }
}

public class Fenster_Behalten_Ui
{
    [AvaloniaFact(DisplayName = "Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück")]
    public void ReopenKeepsWindows()
    {
        var (w, _, _) = Ui.Open();
        w.Show("werkzeuge"); w.Show("prog-pflege"); w.Show("settings"); Settle();
        w.Windows.Move("werkzeuge", 50, 40); w.Show("prog-pflege"); Settle(); RealClick(w, V<Button>(w, "win-min-prog-pflege")); w.Show("settings"); Settle();
        var before = w.Windows.Snapshot();
        var hell = V<Button>(w, "theme-dunkel"); // übernimmt die Einstellung neu (wie jede Design-Wahl)
        RealClick(w, hell);
        var n = w.ReopenedAs; Assert.NotNull(n); Settle();
        var after = n!.Windows.Snapshot();
        Assert.Equal(before.Windows.Select(x => x.Id), after.Windows.Select(x => x.Id));
        Assert.Equal(before.Windows.Select(x => (x.Page, x.Minimized, x.Maximized)), after.Windows.Select(x => (x.Page, x.Minimized, x.Maximized)));
        var wb = before.Windows.First(x => x.Id == "werkzeuge"); var wa = n.Windows.Get("werkzeuge")!;
        Assert.Equal((wb.X, wb.Y), (wa.X, wa.Y));
        Assert.Equal("settings", n.Windows.Active!.Id); Assert.Equal("settings", n.CurrentView);
        Assert.Equal(3, n.GetVisualDescendants().OfType<Button>().Count(b => b.Name?.StartsWith("task-") == true));
        n.Close();
    }
}

public class Fenster_Erscheint_Ui
{
    [AvaloniaFact(DisplayName = "Start: das Hauptfenster ist sichtbar und kommt aus dem minimierten Zustand zurück")]
    public void Reveal_shows_window()
    {
        var (w, _, _) = Ui.Open();
        Assert.True(w.IsVisible);
        Assert.NotEqual(WindowState.Minimized, w.WindowState);
        w.WindowState = WindowState.Minimized;
        w.Reveal();
        Assert.True(w.IsVisible);
        Assert.Equal(WindowState.Maximized, w.WindowState);
        w.WindowState = WindowState.Normal;
        w.Reveal();
        Assert.Equal(WindowState.Normal, w.WindowState);
        w.Close();
    }
}
