using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0266: Werkzeugweg-Vorschau – von oben oder schräg (ziehen dreht, Mausrad zoomt). Gefahrene Wege leuchten, Eilgänge gestrichelt grau,
/// der Fräser steht an der Simulationsstelle. Der Arbeitsraum der Maschine ist als Rahmen eingezeichnet.</summary>
public sealed class CncPathView : Control
{
    public IReadOnlyList<CncSeg> Segs { get; set; } = [];
    public int Pos { get; set; } = -1; // -1 = alles gefahren
    public bool Iso { get; set; }
    public double Yaw { get; set; } = -30;
    public double Pitch { get; set; } = 35;
    public double Zoom { get; set; } = 1;
    public double ToolD { get; set; } = 6;
    public (double X, double Y, double Z) Envelope { get; set; } = (300, 300, 60);
    Point? _last;

    public CncPathView() { ClipToBounds = true; Focusable = true; }

    (double cx, double cy, double cz, double r) Frame()
    {
        if (Segs.Count == 0) return (Envelope.X / 2, Envelope.Y / 2, 0, Math.Max(Envelope.X, Envelope.Y) / 2);
        double x0 = Segs.Min(s => Math.Min(s.X0, s.X1)), x1 = Segs.Max(s => Math.Max(s.X0, s.X1)), y0 = Segs.Min(s => Math.Min(s.Y0, s.Y1)), y1 = Segs.Max(s => Math.Max(s.Y0, s.Y1)), z0 = Segs.Min(s => Math.Min(s.Z0, s.Z1)), z1 = Segs.Max(s => Math.Max(s.Z0, s.Z1));
        return ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2, Math.Max(1, 0.5 * Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (Iso ? (z1 - z0) * (z1 - z0) : 0))));
    }

    public Point Project(double x, double y, double z)
    {
        var (cx, cy, cz, r) = Frame(); var s = Math.Min(Bounds.Width, Bounds.Height) * 0.45 / r * Zoom;
        double dx = x - cx, dy = y - cy, dz = z - cz;
        if (!Iso) return new(Bounds.Width / 2 + dx * s, Bounds.Height / 2 - dy * s);
        double ya = Yaw * Math.PI / 180, pa = Pitch * Math.PI / 180;
        double x1 = dx * Math.Cos(ya) - dy * Math.Sin(ya), y1 = dx * Math.Sin(ya) + dy * Math.Cos(ya);
        return new(Bounds.Width / 2 + x1 * s, Bounds.Height / 2 - (y1 * Math.Sin(pa) + dz * Math.Cos(pa)) * s);
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#212121")), new Rect(Bounds.Size));
        // Raster alle 10 mm in der Werkstückebene und Arbeitsraum
        var grid = new Pen(new SolidColorBrush(Color.Parse("#363636")), 1); var env = new Pen(new SolidColorBrush(Color.Parse("#557FD3FF")), 1, new DashStyle([4, 3], 0));
        var (cx, cy, _, r) = Frame(); var step = r > 200 ? 50 : r > 60 ? 10 : 5;
        for (var gx = Math.Floor((cx - r * 1.4) / step) * step; gx <= cx + r * 1.4; gx += step) ctx.DrawLine(grid, Project(gx, cy - r * 1.4, 0), Project(gx, cy + r * 1.4, 0));
        for (var gy = Math.Floor((cy - r * 1.4) / step) * step; gy <= cy + r * 1.4; gy += step) ctx.DrawLine(grid, Project(cx - r * 1.4, gy, 0), Project(cx + r * 1.4, gy, 0));
        var (ex, ey, _) = Envelope; Point[] box = [Project(0, 0, 0), Project(ex, 0, 0), Project(ex, ey, 0), Project(0, ey, 0)];
        for (var i = 0; i < 4; i++) ctx.DrawLine(env, box[i], box[(i + 1) % 4]);
        ctx.DrawLine(new Pen(Brush.Parse("#F05252"), 2), Project(0, 0, 0), Project(Math.Min(ex, r * 0.3 + 5), 0, 0));
        ctx.DrawLine(new Pen(Brush.Parse("#4ADE80"), 2), Project(0, 0, 0), Project(0, Math.Min(ey, r * 0.3 + 5), 0));
        if (Segs.Count == 0) return;
        var done = Pos < 0 ? Segs.Count : Pos + 1;
        double z0 = Segs.Min(s => Math.Min(s.Z0, s.Z1)), z1 = Math.Max(z0 + 1e-6, Segs.Max(s => Math.Max(s.Z0, s.Z1)));
        var rapid = new Pen(new SolidColorBrush(Color.Parse("#66A0AEC0")), 1, new DashStyle([3, 3], 0)); var future = new Pen(new SolidColorBrush(Color.Parse("#334FA3FF")), 1);
        for (var i = 0; i < Segs.Count; i++)
        {
            var s = Segs[i]; var a = Project(s.X0, s.Y0, s.Z0); var b = Project(s.X1, s.Y1, s.Z1);
            if (s.Rapid) ctx.DrawLine(rapid, a, b);
            else if (i >= done) ctx.DrawLine(future, a, b);
            else { var t = ((s.Z0 + s.Z1) / 2 - z0) / (z1 - z0); ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb((byte)(255 - 120 * t), (byte)(140 + 80 * t), (byte)(40 + 200 * t))), 2), a, b); } // tiefer = orange, oben = hellblau
        }
        // Fräser an der Simulationsstelle
        var cur = Segs[Math.Clamp(done - 1, 0, Segs.Count - 1)]; var p = Project(cur.X1, cur.Y1, cur.Z1);
        var (_, _, _, rr) = Frame(); var rad = Math.Max(4, ToolD / 2 * Math.Min(Bounds.Width, Bounds.Height) * 0.45 / rr * Zoom);
        ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#55F5A524")), new Pen(Brush.Parse("#F5A524"), 2), p, rad, Iso ? rad * 0.6 : rad);
        ctx.DrawLine(new Pen(Brush.Parse("#F5A524"), 2), p, new Point(p.X, p.Y - (Iso ? 30 : 0)));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); Focus(); _last = e.GetPosition(this); e.Pointer.Capture(this); } // Fokus: übernimmt den G-Code aus dem Editor
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_last is null || !Iso) return;
        var p = e.GetPosition(this); Yaw = (Yaw + (p.X - _last.Value.X) * 0.5) % 360; Pitch = Math.Clamp(Pitch + (p.Y - _last.Value.Y) * 0.4, 0, 89); _last = p; InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); _last = null; e.Pointer.Capture(null); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Zoom = Math.Clamp(Zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.2, 30); InvalidateVisual(); e.Handled = true; }
}
