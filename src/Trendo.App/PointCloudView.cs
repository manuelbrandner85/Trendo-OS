using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0264: 3D-Ansicht einer Punktwolke – linke Maustaste dreht, rechte/mittlere verschiebt, Mausrad zoomt;
/// im Messmodus wählt ein Klick den nächstgelegenen Punkt. Farben aus dem Scan oder nach Höhe. Höchstens 120 000 Punkte werden gezeichnet.</summary>
public sealed class PointCloudView : Control
{
    const int MaxDraw = 120_000;
    public PointCloud? Cloud { get; set; }
    public double Yaw { get; set; } = -35;
    public double Pitch { get; set; } = 28;
    public double Zoom { get; set; } = 1;
    public Point Pan { get; set; }
    public bool ByHeight { get; set; }
    public bool MeasureMode { get; set; }
    public List<P3> Marks { get; } = [];
    public Action<double, double, double>? ViewChanged;
    public Action<P3>? Picked;

    Point? _down; Point _last; bool _dragged; bool _panning;
    static readonly Dictionary<uint, IBrush> Brushes = [];

    public PointCloudView() { ClipToBounds = true; Focusable = true; }

    (P3 C, double R) Frame()
    {
        var i = Cloud!.Info();
        return (i.Center, Math.Max(1e-9, 0.5 * Math.Sqrt(i.SizeX * i.SizeX + i.SizeY * i.SizeY + i.SizeZ * i.SizeZ)));
    }

    /// <summary>Weltpunkt → Bildschirm (x, y) und Tiefe (größer = näher).</summary>
    public (double X, double Y, double Depth) Project(P3 p)
    {
        var (c, r) = Frame();
        double x = p.X - c.X, y = p.Y - c.Y, z = p.Z - c.Z;
        double ya = Yaw * Math.PI / 180, pa = Pitch * Math.PI / 180;
        double x1 = x * Math.Cos(ya) - y * Math.Sin(ya), y1 = x * Math.Sin(ya) + y * Math.Cos(ya);
        double y2 = y1 * Math.Cos(pa) - z * Math.Sin(pa), z2 = y1 * Math.Sin(pa) + z * Math.Cos(pa);
        var s = Math.Min(Bounds.Width, Bounds.Height) * 0.62 / r * Zoom;
        return (Bounds.Width / 2 + Pan.X + x1 * s, Bounds.Height / 2 + Pan.Y - z2 * s, -y2);
    }

    static IBrush BrushFor(byte r, byte g, byte b)
    {
        var key = (uint)((r >> 3) << 10 | (g >> 3) << 5 | (b >> 3));
        if (!Brushes.TryGetValue(key, out var br)) Brushes[key] = br = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb((byte)((r >> 3) << 3), (byte)((g >> 3) << 3), (byte)((b >> 3) << 3)));
        return br;
    }

    static (byte, byte, byte) Ramp(double t)
    {
        t = Math.Clamp(t, 0, 1); // blau → türkis → grün → gelb → rot
        (double r, double g, double b) = t < 0.25 ? (0.0, t * 4, 1.0) : t < 0.5 ? (0.0, 1.0, 1 - (t - 0.25) * 4) : t < 0.75 ? ((t - 0.5) * 4, 1.0, 0.0) : (1.0, 1 - (t - 0.75) * 4, 0.0);
        return ((byte)(40 + r * 215), (byte)(40 + g * 215), (byte)(60 + b * 195));
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#2D2D2D"), 0), new GradientStop(Color.Parse("#0A1322"), 1) } }, new Rect(Bounds.Size));
        if (Cloud is not { Points.Count: > 0 } pc) return;
        var info = pc.Info(); var step = Math.Max(1, pc.Points.Count / MaxDraw);
        var idx = new List<(int I, double X, double Y, double D)>(pc.Points.Count / step + 1);
        for (var i = 0; i < pc.Points.Count; i += step) { var (x, y, d) = Project(pc.Points[i]); if (x >= -2 && y >= -2 && x <= Bounds.Width + 2 && y <= Bounds.Height + 2) idx.Add((i, x, y, d)); }
        idx.Sort((a, b) => a.D.CompareTo(b.D)); // hinten zuerst
        var size = pc.Points.Count / step > 40_000 ? 1.6 : 2.4;
        var useCol = pc.Colors is not null && !ByHeight; var zr = Math.Max(1e-9, info.Max.Z - info.Min.Z);
        foreach (var (i, x, y, _) in idx)
        {
            var (r, g, b) = useCol ? pc.Colors![i] : Ramp((pc.Points[i].Z - info.Min.Z) / zr);
            ctx.FillRectangle(BrushFor(r, g, b), new Rect(x - size / 2, y - size / 2, size, size));
        }
        // Begrenzungsrahmen
        var pen = new Pen(new SolidColorBrush(Color.Parse("#557FD3FF")), 1, new DashStyle([3, 3], 0));
        P3[] c = [.. from x in new[] { info.Min.X, info.Max.X } from y in new[] { info.Min.Y, info.Max.Y } from z in new[] { info.Min.Z, info.Max.Z } select new P3(x, y, z)];
        foreach (var (a, b) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
        { var (ax, ay, _) = Project(c[a]); var (bx, by, _) = Project(c[b]); ctx.DrawLine(pen, new Point(ax, ay), new Point(bx, by)); }
        // Achsenkreuz unten links
        var o = new Point(40, Bounds.Height - 40); double ya = Yaw * Math.PI / 180, pa = Pitch * Math.PI / 180;
        foreach (var (dx, dy, dz, col, l) in new[] { (1.0, 0.0, 0.0, "#F05252", "X"), (0.0, 1.0, 0.0, "#4ADE80", "Y"), (0.0, 0.0, 1.0, "#60A5FA", "Z") })
        {
            double x1 = dx * Math.Cos(ya) - dy * Math.Sin(ya), y1 = dx * Math.Sin(ya) + dy * Math.Cos(ya), z2 = y1 * Math.Sin(pa) + dz * Math.Cos(pa);
            var e = new Point(o.X + x1 * 24, o.Y - z2 * 24); ctx.DrawLine(new Pen(Brush.Parse(col), 2), o, e);
            ctx.DrawText(new FormattedText(l, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, Brush.Parse(col)), new Point(e.X + 2, e.Y - 7));
        }
        // Messpunkte und Strecke
        var mark = Brush.Parse("#F5A524");
        for (var k = 0; k < Marks.Count; k++)
        {
            var (mx, my, _) = Project(Marks[k]);
            ctx.DrawEllipse(mark, new Pen(Avalonia.Media.Brushes.White, 1.5), new Point(mx, my), 5, 5);
            if (k > 0) { var (px, py, _) = Project(Marks[k - 1]); ctx.DrawLine(new Pen(mark, 2), new Point(px, py), new Point(mx, my)); }
        }
    }

    /// <summary>Nächster sichtbarer Punkt zu einer Bildschirmstelle (höchstens 10 px entfernt), sonst null.</summary>
    public P3? PickAt(Point at)
    {
        if (Cloud is not { Points.Count: > 0 } pc) return null;
        var step = Math.Max(1, pc.Points.Count / MaxDraw); P3? best = null; double bd = 100, bz = double.MinValue;
        for (var i = 0; i < pc.Points.Count; i += step)
        {
            var (x, y, d) = Project(pc.Points[i]); var dd = (x - at.X) * (x - at.X) + (y - at.Y) * (y - at.Y);
            if (dd < bd - 4 || (dd <= bd + 4 && d > bz)) { bd = dd; bz = d; best = pc.Points[i]; }
        }
        return best;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var pt = e.GetCurrentPoint(this); _down = _last = pt.Position; _dragged = false;
        _panning = pt.Properties.IsRightButtonPressed || pt.Properties.IsMiddleButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        e.Pointer.Capture(this); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_down is null) return;
        var p = e.GetPosition(this); var d = p - _last;
        if (!_dragged && Math.Abs(p.X - _down.Value.X) + Math.Abs(p.Y - _down.Value.Y) < 4) return;
        _dragged = true; _last = p;
        if (_panning) Pan = new Point(Pan.X + d.X, Pan.Y + d.Y);
        else { Yaw = (Yaw + d.X * 0.5) % 360; Pitch = Math.Clamp(Pitch + d.Y * 0.4, -89, 89); }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_down is null) return;
        var click = !_dragged; var at = e.GetPosition(this); _down = null; e.Pointer.Capture(null);
        if (click && MeasureMode && PickAt(at) is { } hit) Picked?.Invoke(hit);
        else if (!click) ViewChanged?.Invoke(Yaw, Pitch, Zoom);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoom = Math.Clamp(Zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.1, 40); InvalidateVisual(); ViewChanged?.Invoke(Yaw, Pitch, Zoom); e.Handled = true;
    }
}
