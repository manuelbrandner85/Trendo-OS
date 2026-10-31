using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0269: 3D-Ansicht eines Dreiecksnetzes (glTF/GLB, OBJ, STL) – schattiert mit Lichtrichtung, wahlweise mit Drahtgitter,
/// Boden-Raster (1 m, 25 cm oder 2,5 cm je nach Größe). Ziehen dreht, Mausrad zoomt, Doppelklick setzt zurück. Maleralgorithmus ohne Grafikkarte, höchstens 60 000 Dreiecke.</summary>
public sealed class MeshView : Control
{
    const int MaxTris = 60_000;
    public Mesh? Model { get; set; }
    public double Yaw { get; set; } = -35;
    public double Pitch { get; set; } = 22;
    public double Zoom { get; set; } = 1;
    public bool Wire { get; set; }
    Point? _drag;

    public MeshView() { ClipToBounds = true; Focusable = true; }

    public void SetView(string v) { (Yaw, Pitch) = v switch { "vorne" => (0, 0), "oben" => (0, 89), "seite" => (-90, 0), _ => (-35, 22) }; Zoom = 1; InvalidateVisual(); }

    (double cx, double cy, double cz, double r, double minY) Frame()
    {
        var v = Model!.V; double x0 = v.Min(p => p.X), x1 = v.Max(p => p.X), y0 = v.Min(p => p.Y), y1 = v.Max(p => p.Y), z0 = v.Min(p => p.Z), z1 = v.Max(p => p.Z);
        return ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2, Math.Max(1e-6, 0.5 * Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0))), y0);
    }

    // glTF: Y ist oben
    (double X, double Y, double D) P(double x, double y, double z, (double cx, double cy, double cz, double r, double minY) f)
    {
        double dx = x - f.cx, dy = y - f.cy, dz = z - f.cz, ya = Yaw * Math.PI / 180, pa = Pitch * Math.PI / 180;
        double x1 = dx * Math.Cos(ya) + dz * Math.Sin(ya), z1 = -dx * Math.Sin(ya) + dz * Math.Cos(ya);
        double y2 = dy * Math.Cos(pa) - z1 * Math.Sin(pa), z2 = dy * Math.Sin(pa) + z1 * Math.Cos(pa);
        var s = Math.Min(Bounds.Width, Bounds.Height) * 0.42 / f.r * Zoom;
        return (Bounds.Width / 2 + x1 * s, Bounds.Height / 2 - y2 * s, z2);
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#1B2C4A"), 0), new GradientStop(Color.Parse("#0A1322"), 1) } }, new Rect(Bounds.Size));
        if (Model is not { Triangles: > 0 } m) return;
        var f = Frame();
        // Boden-Raster (1-m-Raster, bei kleinen Modellen 10 cm)
        var step = f.r > 3 ? 1.0 : f.r > 0.3 ? 0.25 : 0.025; var gp = new Pen(new SolidColorBrush(Color.Parse("#22FFFFFF")), 1); var ext = Math.Ceiling(f.r * 1.5 / step) * step;
        for (var g = -ext; g <= ext + 1e-9; g += step)
        {
            var a = P(f.cx + g, f.minY, f.cz - ext, f); var b = P(f.cx + g, f.minY, f.cz + ext, f); ctx.DrawLine(gp, new Point(a.X, a.Y), new Point(b.X, b.Y));
            a = P(f.cx - ext, f.minY, f.cz + g, f); b = P(f.cx + ext, f.minY, f.cz + g, f); ctx.DrawLine(gp, new Point(a.X, a.Y), new Point(b.X, b.Y));
        }
        var n = m.Triangles; var stride = Math.Max(1, n / MaxTris);
        var tris = new List<(int T, double D)>(n / stride + 1);
        for (var t = 0; t < n; t += stride)
        {
            var (a, b, c) = (m.V[m.I[3 * t]], m.V[m.I[3 * t + 1]], m.V[m.I[3 * t + 2]]);
            tris.Add((t, P((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3, (a.Z + b.Z + c.Z) / 3, f).D));
        }
        tris.Sort((p, q) => p.D.CompareTo(q.D));
        var light = Norm(0.4, 0.8, 0.45); var edge = Wire ? new Pen(new SolidColorBrush(Color.Parse("#AA7FD3FF")), 0.7) : null;
        foreach (var (t, _) in tris)
        {
            var (a, b, c) = (m.V[m.I[3 * t]], m.V[m.I[3 * t + 1]], m.V[m.I[3 * t + 2]]);
            var nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y); var ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z); var nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            var nl = Math.Sqrt(nx * nx + ny * ny + nz * nz); if (nl < 1e-15) continue;
            var lum = 0.35 + 0.65 * Math.Abs((nx * light.X + ny * light.Y + nz * light.Z) / nl);
            byte r = t < m.R.Count ? m.R[t] : m.CR, g = t < m.G.Count ? m.G[t] : m.CG, bl = t < m.B.Count ? m.B[t] : m.CB;
            var fill = new SolidColorBrush(Color.FromRgb((byte)(r * lum), (byte)(g * lum), (byte)(bl * lum)));
            var pa = P(a.X, a.Y, a.Z, f); var pb = P(b.X, b.Y, b.Z, f); var pc = P(c.X, c.Y, c.Z, f);
            var geo = new StreamGeometry(); using (var gc = geo.Open()) { gc.BeginFigure(new Point(pa.X, pa.Y), true); gc.LineTo(new Point(pb.X, pb.Y)); gc.LineTo(new Point(pc.X, pc.Y)); gc.EndFigure(true); }
            ctx.DrawGeometry(Wire ? new SolidColorBrush(Color.FromArgb(150, (byte)(r * lum), (byte)(g * lum), (byte)(bl * lum))) : fill, edge ?? new Pen(fill, 0.6), geo);
        }
    }

    static (double X, double Y, double Z) Norm(double x, double y, double z) { var l = Math.Sqrt(x * x + y * y + z * z); return (x / l, y / l, z / l); }

    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); Focus(); _drag = e.GetPosition(this); if (e.ClickCount == 2) SetView("iso"); e.Pointer.Capture(this); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); _drag = null; e.Pointer.Capture(null); }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_drag is not { } d) return;
        var p = e.GetPosition(this); Yaw = (Yaw + (p.X - d.X) * 0.5) % 360; Pitch = Math.Clamp(Pitch + (p.Y - d.Y) * 0.4, -89, 89); _drag = p; InvalidateVisual();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Zoom = Math.Clamp(Zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.1, 40); InvalidateVisual(); e.Handled = true; }
}
