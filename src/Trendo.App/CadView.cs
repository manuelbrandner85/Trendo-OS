using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Cad;

namespace Trendo.App;

/// <summary>N23: 3D-Ansicht von Trendo CAD – schattierte Flächen mit Kanten, Boden-Raster und Achsen. Ziehen dreht, Mausrad zoomt,
/// Doppelklick setzt die Ansicht zurück. Zeichnet mit dem Maleralgorithmus (hinten zuerst), ohne Grafikkarte.</summary>
public sealed class CadView : Control
{
    public IReadOnlyList<(string Color, Solid Solid, bool Selected)> Parts { get; set; } = [];
    public double Yaw { get; set; } = -35;
    public double Pitch { get; set; } = 28;
    public double Zoom { get; set; } = 1;
    Point? _drag;

    public CadView() { ClipToBounds = true; Focusable = true; }

    public void SetView(string v) { (Yaw, Pitch) = v switch { "vorne" => (0, 0), "oben" => (0, 90), "rechts" => (-90, 0), _ => (-35, 28) }; Zoom = 1; InvalidateVisual(); }

    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); _drag = e.GetPosition(this); if (e.ClickCount == 2) SetView("iso"); e.Pointer.Capture(this); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); _drag = null; e.Pointer.Capture(null); }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is not { } d) return;
        var p = e.GetPosition(this);
        Yaw += (p.X - d.X) * 0.5; Pitch = Math.Clamp(Pitch + (p.Y - d.Y) * 0.5, -89, 89);
        _drag = p; InvalidateVisual();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Zoom = Math.Clamp(Zoom * (e.Delta.Y > 0 ? 1.12 : 1 / 1.12), 0.1, 20); InvalidateVisual(); e.Handled = true; }

    readonly Dictionary<Solid, HashSet<((long, long, long), (long, long, long))>> _sharp = new(ReferenceEqualityComparer.Instance);

    static ((long, long, long), (long, long, long)) EdgeKey(Vec3 a, Vec3 b)
    {
        static (long, long, long) K(Vec3 p) => ((long)Math.Round(p.X * 1e4), (long)Math.Round(p.Y * 1e4), (long)Math.Round(p.Z * 1e4));
        var ka = K(a); var kb = K(b); return ka.CompareTo(kb) < 0 ? (ka, kb) : (kb, ka);
    }

    /// <summary>Echte Kanten (zwischen verschieden geneigten Flächen), je Körper einmal berechnet.</summary>
    HashSet<((long, long, long), (long, long, long))> SharpEdges(Solid s)
    {
        if (_sharp.TryGetValue(s, out var set)) return set;
        set = CadExport.FeatureEdges(s).Select(e => EdgeKey(e.A, e.B)).ToHashSet();
        if (_sharp.Count > 32) _sharp.Clear();
        return _sharp[s] = set;
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height; if (w < 2 || h < 2) return;
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FF1B2A44"), 0), new GradientStop(Color.Parse("#FF0B1424"), 1) } }, new Rect(Bounds.Size));
        // Umfang aller Teile → Maßstab und Mitte
        var boxes = Parts.Where(p => p.Solid.Polys.Count > 0).Select(p => p.Solid.Bounds()).ToList();
        Vec3 min = boxes.Count == 0 ? new(-20, -20, 0) : new(boxes.Min(b => b.Min.X), boxes.Min(b => b.Min.Y), boxes.Min(b => b.Min.Z));
        Vec3 max = boxes.Count == 0 ? new(20, 20, 20) : new(boxes.Max(b => b.Max.X), boxes.Max(b => b.Max.Y), boxes.Max(b => b.Max.Z));
        var center = (min + max) * 0.5; var size = Math.Max(10, (max - min).Len);
        var k = Math.Min(w, h) / size * 0.8 * Zoom;
        double cy = Math.Cos(Yaw * Math.PI / 180), sy = Math.Sin(Yaw * Math.PI / 180), cp = Math.Cos(Pitch * Math.PI / 180), sp = Math.Sin(Pitch * Math.PI / 180);
        // Kamera: um Z drehen (Yaw), dann um X kippen (Pitch); Bildschirm: x nach rechts, y nach oben, Tiefe zum Betrachter
        (double X, double Y, double D) Proj(Vec3 p)
        {
            var q = p - center;
            var x1 = q.X * cy - q.Y * sy; var y1 = q.X * sy + q.Y * cy; var z1 = q.Z;
            var y2 = z1 * cp + y1 * sp; var d = -y1 * cp + z1 * sp;
            return (w / 2 + x1 * k, h / 2 - y2 * k, d);
        }
        var light = new Vec3(0.4, -0.6, 0.7).Norm();
        // Boden-Raster bei min.Z
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#22FFFFFF")), 1);
        var step = Math.Pow(10, Math.Floor(Math.Log10(size / 2)));
        var gx0 = Math.Floor((center.X - size) / step) * step; var gy0 = Math.Floor((center.Y - size) / step) * step;
        for (var i = 0; i <= 2 * size / step + 1; i++)
        {
            var a = Proj(new Vec3(gx0 + i * step, gy0, min.Z)); var b = Proj(new Vec3(gx0 + i * step, gy0 + 2 * size, min.Z));
            ctx.DrawLine(gridPen, new Point(a.X, a.Y), new Point(b.X, b.Y));
            a = Proj(new Vec3(gx0, gy0 + i * step, min.Z)); b = Proj(new Vec3(gx0 + 2 * size, gy0 + i * step, min.Z));
            ctx.DrawLine(gridPen, new Point(a.X, a.Y), new Point(b.X, b.Y));
        }
        // Flächen nach Tiefe sortiert
        var faces = new List<(double D, Point[] Pts, IBrush Fill, bool Sel, List<(Point, Point)> Edges)>();
        foreach (var (color, solid, sel) in Parts)
        {
            var sharp = SharpEdges(solid);
            var baseCol = Color.TryParse(color, out var bc) ? bc : Color.Parse("#2F86F6");
            foreach (var poly in solid.Polys)
            {
                var pts = poly.Verts.Select(Proj).ToList();
                var n = poly.Plane.Normal;
                // nach hinten zeigende Flächen weglassen
                var a = pts[0]; var b = pts[1]; var c = pts[2];
                var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (cross > 0) continue; // Bildschirm-y zeigt nach unten: Vorderseite hat negatives Kreuzprodukt
                var shade = 0.35 + 0.65 * Math.Max(0, Vec3.Dot(n, light));
                var col = Color.FromRgb((byte)Math.Min(255, baseCol.R * shade + 18), (byte)Math.Min(255, baseCol.G * shade + 18), (byte)Math.Min(255, baseCol.B * shade + 18));
                var edges = new List<(Point, Point)>();
                for (var i = 0; i < poly.Verts.Count; i++) if (sharp.Contains(EdgeKey(poly.Verts[i], poly.Verts[(i + 1) % poly.Verts.Count]))) edges.Add((new Point(pts[i].X, pts[i].Y), new Point(pts[(i + 1) % pts.Count].X, pts[(i + 1) % pts.Count].Y)));
                faces.Add((pts.Average(x => x.D), pts.Select(x => new Point(x.X, x.Y)).ToArray(), new SolidColorBrush(col), sel, edges));
            }
        }
        var edge = new Pen(new SolidColorBrush(Color.Parse("#B0101826")), 1); var selEdge = new Pen(new SolidColorBrush(Color.Parse("#FFFACC15")), 1.4);
        foreach (var f in faces.OrderBy(f => f.D))
        {
            ctx.DrawGeometry(f.Fill, new Pen(f.Fill, 0.8), new PolylineGeometry(f.Pts, true)); // Fläche ohne Fugen
            foreach (var (a, b) in f.Edges) ctx.DrawLine(f.Sel ? selEdge : edge, a, b);
        }
        // Achsenkreuz unten links
        var o = new Point(40, h - 40);
        foreach (var (v, col, label) in new[] { (new Vec3(1, 0, 0), "#EF4444", "X"), (new Vec3(0, 1, 0), "#22C55E", "Y"), (new Vec3(0, 0, 1), "#3B82F6", "Z") })
        {
            var x1 = v.X * cy - v.Y * sy; var y1 = v.X * sy + v.Y * cy; var y2 = v.Z * cp + y1 * sp;
            var e = new Point(o.X + x1 * 26, o.Y - y2 * 26);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse(col)), 2), o, e);
            ctx.DrawText(new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 11, new SolidColorBrush(Color.Parse(col))), e);
        }
    }
}
