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

    // ---------- Bearbeiten (3D-Modelle erstellen): Teil anklicken, ausgewähltes Teil mit der Maus verschieben ----------
    /// <summary>Klick auf ein Teil (Index in <see cref="Parts"/>) oder ins Leere (−1).</summary>
    public Action<int>? PartClicked { get; set; }
    /// <summary>Ziehen des ausgewählten Teils beendet: Verschiebung in Weltkoordinaten (cm).</summary>
    public Action<int, Vec3>? PartMoved { get; set; }
    /// <summary>Raster für das Verschieben (cm, 0 = frei).</summary>
    public double Snap { get; set; } = 0.5;
    /// <summary>Feste Bildmitte und Größe (sonst aus den Teilen) – beim Bearbeiten springt die Ansicht so nicht mit.</summary>
    public (Vec3 Center, double Size)? Frame { get; set; }
    public Vec3 DragOffset { get; private set; }
    int _moving = -1; bool _dragged;
    readonly List<(int Part, double D, Point[] Pts)> _hit = [];
    (Vec3 Center, double K, double Cy, double Sy, double Cp, double Sp, double W, double H) _cam;

    /// <summary>Oberstes Teil unter einem Bildpunkt (nach der letzten Darstellung) oder −1.</summary>
    public int PickAt(Point p)
    {
        foreach (var (part, _, pts) in _hit.OrderByDescending(x => x.D)) if (Inside(pts, p)) return part;
        return -1;
    }
    /// <summary>Bildpunkt in der Mitte der sichtbaren Flächen eines Teils (nach der letzten Darstellung).</summary>
    public Point? ScreenOf(int part)
    {
        var top = _hit.Where(x => x.Part == part).ToList(); if (top.Count == 0) return null;
        foreach (var (_, _, pts) in top.OrderByDescending(x => x.D)) { var c = new Point(pts.Average(q => q.X), pts.Average(q => q.Y)); if (PickAt(c) == part) return c; }
        return null;
    }
    static bool Inside(Point[] poly, Point p)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        return inside;
    }
    /// <summary>Bildschirm-Verschiebung → Welt: in der Bodenebene; bei fast waagrechtem Blick hoch/runter statt vor/zurück.</summary>
    public Vec3 ScreenToWorld(double dx, double dy)
    {
        var c = _cam; if (c.K <= 0) return new(0, 0, 0);
        var a = dx / c.K;
        if (Math.Abs(c.Sp) < 0.35) return new(a * c.Cy, -a * c.Sy, -dy / (Math.Max(0.2, c.Cp) * c.K));
        var b = -dy / (c.Sp * c.K);
        return new(a * c.Cy + b * c.Sy, -a * c.Sy + b * c.Cy, 0);
    }
    Vec3 Snapped(Vec3 v) => Snap <= 0 ? v : new(Math.Round(v.X / Snap) * Snap, Math.Round(v.Y / Snap) * Snap, Math.Round(v.Z / Snap) * Snap);

    public CadView() { ClipToBounds = true; Focusable = true; }

    public void SetView(string v) { (Yaw, Pitch) = v switch { "vorne" => (0, 0), "oben" => (0, 90), "rechts" => (-90, 0), _ => (-35, 28) }; Zoom = 1; InvalidateVisual(); }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var pos = e.GetPosition(this); _drag = pos; _dragged = false; _moving = -1; DragOffset = new(0, 0, 0);
        if (e.ClickCount == 2 && PickAt(pos) < 0) { SetView("iso"); return; } // Doppelklick ins Leere: Ansicht zurück; auf einem Teil zählt er als neuer Griff
        // linke Taste auf dem ausgewählten Teil: verschieben; sonst drehen
        if (PartMoved is not null && !e.GetCurrentPoint(this).Properties.IsRightButtonPressed && PickAt(pos) is var hit and >= 0 && Parts[hit].Selected) _moving = hit;
        e.Pointer.Capture(this);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        var pos = e.GetPosition(this);
        if (_moving >= 0 && _dragged) { var off = Snapped(DragOffset); var i = _moving; _moving = -1; DragOffset = new(0, 0, 0); PartMoved?.Invoke(i, off); }
        else if (!_dragged && e.InitialPressMouseButton == MouseButton.Left) PartClicked?.Invoke(PickAt(pos));
        _moving = -1; _drag = null; DragOffset = new(0, 0, 0); InvalidateVisual();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is not { } d) return;
        var p = e.GetPosition(this);
        if (!_dragged && Math.Abs(p.X - d.X) + Math.Abs(p.Y - d.Y) < 4) return;
        _dragged = true;
        if (_moving >= 0) DragOffset += ScreenToWorld(p.X - d.X, p.Y - d.Y);
        else { Yaw += (p.X - d.X) * 0.5; Pitch = Math.Clamp(Pitch + (p.Y - d.Y) * 0.5, -89, 89); }
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
        if (Frame is { } fr) { center = fr.Center; size = Math.Max(10, fr.Size); min = new(min.X, min.Y, Math.Min(min.Z, 0)); }
        var k = Math.Min(w, h) / size * 0.8 * Zoom;
        double cy = Math.Cos(Yaw * Math.PI / 180), sy = Math.Sin(Yaw * Math.PI / 180), cp = Math.Cos(Pitch * Math.PI / 180), sp = Math.Sin(Pitch * Math.PI / 180);
        _cam = (center, k, cy, sy, cp, sp, w, h); _hit.Clear();
        // Kamera: um Z drehen (Yaw), dann um X kippen (Pitch); Bildschirm: x nach rechts, y nach oben, Tiefe zum Betrachter
        (double X, double Y, double D) Proj(Vec3 p)
        {
            var q = p - center;
            var x1 = q.X * cy - q.Y * sy; var y1 = q.X * sy + q.Y * cy; var z1 = q.Z;
            var y2 = z1 * cp + y1 * sp; var d = -y1 * cp + z1 * sp;
            return (w / 2 + x1 * k, h / 2 - y2 * k, d);
        }
        var view = new Vec3(-sy * cp, -cy * cp, sp).Norm(); // Richtung zur Kamera
        var reach = Math.Max(1, (max.Z - min.Z) * 0.25); var groundTol = Math.Max(0.5, size * 0.004);
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
        // Flächen nach Tiefe sortiert; Bodenflächen (waagrecht, ganz unten) zuerst, darauf die Schatten, dann der Rest
        var faces = new List<(double D, Point[] Pts, IBrush Fill, bool Sel, List<(Point, Point)> Edges)>();
        var ground = new List<(double D, Point[] Pts, IBrush Fill, bool Sel, List<(Point, Point)> Edges)>();
        var casters = new List<Poly>(); var groundZ = min.Z;
        for (var pi = 0; pi < Parts.Count; pi++)
        {
            var (color, solid, sel) = Parts[pi];
            var sharp = solid.Smooth ? [] : SharpEdges(solid);
            var baseCol = Color.TryParse(color, out var bc) ? bc : Color.Parse("#2F86F6");
            var off = pi == _moving ? Snapped(DragOffset) : new Vec3(0, 0, 0);
            foreach (var poly in solid.Polys)
            {
                var pts = poly.Verts.Select(v => Proj(v + off)).ToList();
                var n = poly.Plane.Normal;
                // nach hinten zeigende Flächen weglassen
                var a = pts[0]; var b = pts[1]; var c = pts[2];
                var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (cross > 0) continue; // Bildschirm-y zeigt nach unten: Vorderseite hat negatives Kreuzprodukt
                var zMax = poly.Verts.Max(v => v.Z); var isGround = n.Z > 0.9 && zMax <= min.Z + groundTol * 8;
                var col = Shading.Shade(baseCol, n, view, isGround ? 1 : Shading.GroundAo(poly.Verts.Average(v => v.Z) - min.Z, reach), solid.Smooth ? 0.35 : 0.2);
                if (sel && PartClicked is not null) col = Color.FromRgb((byte)(col.R * 0.72 + 250 * 0.28), (byte)(col.G * 0.72 + 204 * 0.28), (byte)(col.B * 0.72 + 21 * 0.28)); // Auswahl beim Bearbeiten sichtbar, auch an runden Teilen
                var edges = new List<(Point, Point)>();
                for (var i = 0; i < poly.Verts.Count; i++) if (sharp.Contains(EdgeKey(poly.Verts[i], poly.Verts[(i + 1) % poly.Verts.Count]))) edges.Add((new Point(pts[i].X, pts[i].Y), new Point(pts[(i + 1) % pts.Count].X, pts[(i + 1) % pts.Count].Y)));
                var screen = pts.Select(x => new Point(x.X, x.Y)).ToArray();
                (isGround ? ground : faces).Add((pts.Average(x => x.D), screen, new SolidColorBrush(col), sel, edges));
                _hit.Add((pi, pts.Average(x => x.D), screen));
                if (isGround) groundZ = Math.Max(groundZ, zMax);
            }
            foreach (var poly in solid.Polys) if (poly.Verts.Max(v => v.Z) > min.Z + groundTol * 8) casters.Add(poly);
        }
        var edge = new Pen(new SolidColorBrush(Color.Parse("#B0101826")), 1); var selEdge = new Pen(new SolidColorBrush(Color.Parse("#FFFACC15")), 1.4);
        void DrawFace((double D, Point[] Pts, IBrush Fill, bool Sel, List<(Point, Point)> Edges) f, Pen edgePen, Pen selPen)
        {
            ctx.DrawGeometry(f.Fill, new Pen(f.Fill, 0.8), new PolylineGeometry(f.Pts, true));
            foreach (var (a, b) in f.Edges) ctx.DrawLine(f.Sel ? selPen : edgePen, a, b);
        }
        foreach (var f in ground.OrderBy(f => f.D)) DrawFace(f, edge, selEdge);
        // Schlagschatten aller Körper auf den Boden (deckend in einer Deckkraft-Ebene – gleichmäßig dicht, auch wo Teile sich überlappen)
        if (casters.Count > 0)
        {
            var shadeBrush = new SolidColorBrush(Color.FromRgb(6, 10, 18));
            using (ctx.PushOpacity(0.32))
                foreach (var poly in casters)
                {
                    var pts = poly.Verts.Select(v => { var g = Proj(Shading.OnGround(v, groundZ)); return new Point(g.X, g.Y); }).ToArray();
                    ctx.DrawGeometry(shadeBrush, new Pen(shadeBrush, 0.8), new PolylineGeometry(pts, true));
                }
        }
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
