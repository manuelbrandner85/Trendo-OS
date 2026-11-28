using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core.Wissen;
using static Trendo.Core.Wissen.TimelineKit;

namespace Trendo.App;

/// <summary>T-0788: Arbeitsfläche des Studios „Stammbaum“. Zeichnet dieselben Bausteine wie SVG, PNG und PDF (<see cref="FamilyKit.Ancestors"/>, <see cref="FamilyKit.Descendants"/>).
/// Mausrad = vergrößern/verkleinern um die Mausstelle, Ziehen = verschieben, Klick auf eine Person = wählen, Doppelklick = zur Mitte des Baums machen,
/// Klick auf „+ Vater“/„+ Mutter“ = Elternteil anlegen. Tasten: + / − zoomen, 0 = einpassen, Pfeile verschieben, Entf löscht die gewählte Person.</summary>
public sealed class FamilyTreeCanvas : Control
{
    public Scene Scene { get; set; } = new(600, 200, 0, 0, [], []);
    public string? Selected { get; set; }
    /// <summary>Punkt der Szene, der beim Einpassen in die Mitte kommt (Person in der Mitte des Baums).</summary>
    public (double X, double Y)? Focus { get; set; }
    /// <summary>Ansicht: Faktor und Verschiebung (Bildschirm = Szene · Faktor + Verschiebung). null = einpassen.</summary>
    public (double Zoom, double X, double Y)? View { get; set; }
    public Action<double, double, double>? ViewChanged { get; set; }
    public Action<string?>? Clicked { get; set; }
    public Action<string>? Activated { get; set; }
    public Action<string>? Placeholder { get; set; }
    public Action? DeleteSelected { get; set; }

    enum Mode { None, Pan }
    Mode _mode; Point _down, _at; (double Z, double X, double Y) _v0; string? _hit;

    public FamilyTreeCanvas() { ClipToBounds = true; Focusable = true; }

    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 1100 : a.Width, double.IsInfinity(a.Height) ? 560 : a.Height);

    /// <summary>Faktor und Verschiebung, die gerade gelten (eingepasst, wenn keine eigene Ansicht gesetzt ist).</summary>
    public (double Zoom, double X, double Y) Current
    {
        get
        {
            if (View is { } v) return v;
            // einpassen, aber nicht kleiner als lesbar (60 %): passt der Baum dann nicht ganz, steht die Person in der Mitte des Baums in der Mitte der Fläche
            var w = Math.Max(200, Bounds.Width); var h = Math.Max(200, Bounds.Height);
            var z = Math.Clamp(Math.Min((w - 20) / Scene.Width, (h - 20) / Scene.Height), 0.6, 1.15);
            double Axis(double view, double size, double focus) => size * z <= view - 12 ? (view - size * z) / 2 : Math.Clamp(view / 2 - focus * z, view - size * z - 6, 6);
            var (fx, fy) = Focus ?? (Scene.Width / 2, Scene.Height / 2);
            return (z, Axis(w, Scene.Width, fx), Axis(h, Scene.Height, fy));
        }
    }

    public Point ToScene(Point p) { var (z, x, y) = Current; return new((p.X - x) / z, (p.Y - y) / z); }
    public Point ToScreen(Point s) { var (z, x, y) = Current; return new(s.X * z + x, s.Y * z + y); }

    /// <summary>Welche Person (oder welcher Platzhalter) liegt an der Bildschirmstelle?</summary>
    public string? HitAt(Point screen)
    {
        var p = ToScene(screen);
        foreach (var (id, (x, y, w, h)) in Scene.Hits) if (p.X >= x && p.X <= x + w && p.Y >= y && p.Y <= y + h) return id;
        return null;
    }

    void Set((double Z, double X, double Y) v) { View = v; InvalidateVisual(); ViewChanged?.Invoke(v.Z, v.X, v.Y); }

    public void Zoom(double factor, Point? at = null)
    {
        var (z, x, y) = Current; var c = at ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var nz = Math.Clamp(z * factor, 0.1, 4); var k = nz / z;
        Set((nz, c.X - (c.X - x) * k, c.Y - (c.Y - y) * k));
    }

    public void Pan(double dx, double dy) { var (z, x, y) = Current; Set((z, x + dx, y + dy)); }
    public void Fit() { View = null; InvalidateVisual(); var (z, x, y) = Current; ViewChanged?.Invoke(double.NaN, x, y); }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Pan(e.Delta.Y * 60, 0);
        else Zoom(e.Delta.Y > 0 ? 1.2 : 1 / 1.2, e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var p = e.GetPosition(this); var pt = e.GetCurrentPoint(this);
        _down = _at = p; _v0 = Current; _hit = HitAt(p);
        if (pt.Properties.IsRightButtonPressed)
        {
            if (_hit is { } h && !h.StartsWith('+') && h != Selected) { Selected = h; InvalidateVisual(); Clicked?.Invoke(h); }
            _mode = Mode.None; return;
        }
        if (!pt.Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2 && _hit is { } hit && !hit.StartsWith('+')) { Activated?.Invoke(hit); e.Handled = true; _mode = Mode.None; return; }
        _mode = Mode.Pan; e.Pointer.Capture(this); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this);
        if (_mode == Mode.None) { Cursor = new Cursor(HitAt(p) is null ? StandardCursorType.Arrow : StandardCursorType.Hand); return; }
        _at = p;
        View = (_v0.Z, _v0.X + (p.X - _down.X), _v0.Y + (p.Y - _down.Y)); InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        if (_mode != Mode.Pan) return;
        _mode = Mode.None;
        var moved = Math.Abs(_at.X - _down.X) + Math.Abs(_at.Y - _down.Y) >= 4;
        if (moved) { ViewChanged?.Invoke(View!.Value.Zoom, View.Value.X, View.Value.Y); return; }
        if (_hit is { } h && h.StartsWith('+')) { Placeholder?.Invoke(h); return; }
        if (_hit != Selected) { Selected = _hit; InvalidateVisual(); Clicked?.Invoke(_hit); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus: Zoom(1.2); e.Handled = true; break;
            case Key.Subtract or Key.OemMinus: Zoom(1 / 1.2); e.Handled = true; break;
            case Key.D0 or Key.NumPad0: Fit(); e.Handled = true; break;
            case Key.Left: Pan(80, 0); e.Handled = true; break;
            case Key.Right: Pan(-80, 0); e.Handled = true; break;
            case Key.Up: Pan(0, 80); e.Handled = true; break;
            case Key.Down: Pan(0, -80); e.Handled = true; break;
            case Key.Delete when Selected is not null: DeleteSelected?.Invoke(); e.Handled = true; break;
        }
    }

    static Color C(string hex) => Color.TryParse(SafeColor(hex, "#D4A017"), out var c) ? c : Colors.Goldenrod;

    public override void Render(DrawingContext g)
    {
        g.FillRectangle(new SolidColorBrush(Color.Parse("#101014")), new Rect(Bounds.Size));
        var (z, ox, oy) = Current;
        var de = CultureInfo.GetCultureInfo("de-DE");
        using var _ = g.PushTransform(Matrix.CreateScale(z, z) * Matrix.CreateTranslation(ox, oy));
        foreach (var p in Scene.Prims)
        {
            switch (p)
            {
                case PRect r:
                {
                    var sel = r.ItemId is { } id && id == Selected && r.W > 10;
                    IPen? pen = r.Stroke is { } st ? new Pen(new SolidColorBrush(C(sel ? "#D4A017" : st)), sel ? 2.4 : 1.2, r.Dashed ? new DashStyle([5, 3], 0) : null) : null;
                    g.DrawRectangle(new SolidColorBrush(C(r.Fill), r.Opacity), pen, new Rect(r.X, r.Y, Math.Max(0.5, r.W), Math.Max(0.5, r.H)), r.Radius, r.Radius);
                    break;
                }
                case PLine l: g.DrawLine(new Pen(new SolidColorBrush(C(l.Color)), l.Width, l.Dashed ? new DashStyle([4, 3], 0) : null), new Point(l.X1, l.Y1), new Point(l.X2, l.Y2)); break;
                case PText t:
                {
                    var ft = new FormattedText(t.Text, de, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, t.Bold ? FontWeight.SemiBold : FontWeight.Normal), t.Size, new SolidColorBrush(C(t.Color)));
                    var x = t.Anchor < 0 ? t.X : t.Anchor > 0 ? t.X - ft.Width : t.X - ft.Width / 2;
                    g.DrawText(ft, new Point(x, t.Y - ft.Baseline)); break;
                }
                case PCircle c: g.DrawEllipse(new SolidColorBrush(C(c.Fill)), null, new Point(c.Cx, c.Cy), c.R, c.R); break;
            }
        }
    }
}
