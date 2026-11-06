using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0269/T-0281: 3D-Ansicht eines Dreiecksnetzes (glTF/GLB, OBJ, STL; Z oben, Zentimeter) – Licht wie in <see cref="Shading"/>, weicher Bodenschatten,
/// Studio-Boden mit Raster (1 m, 25 cm oder 2,5 cm je nach Größe), wahlweise Drahtgitter. Ziehen dreht, Mausrad zoomt, Doppelklick setzt zurück. Maleralgorithmus ohne Grafikkarte, höchstens 60 000 Dreiecke.</summary>
public sealed class MeshView : Control
{
    const int MaxTris = 60_000;
    public Mesh? Model { get; set; }
    public double Yaw { get; set; } = -35;
    public double Pitch { get; set; } = 22;
    public double Zoom { get; set; } = 1;
    public bool Wire { get; set; }
    /// <summary>Vorschau aus Augenhöhe (cm über dem Boden, Modell in Zentimetern): Perspektive wie mit einer Brille, waagrechter Blick, Abstand <see cref="EyeDistanceCm"/>. null = normale Ansicht.</summary>
    public double? EyeHeightCm { get; set; }
    public double EyeDistanceCm { get; set; } = 200;
    /// <summary>Zusätzliche Punkte (z. B. Kamerapositionen beim Scan), als kleine goldene Kameras gezeichnet.</summary>
    public IReadOnlyList<Vec3>? Markers { get; set; }
    Point? _drag;
    Mesh? _shadowFor, _shadow;

    public MeshView() { ClipToBounds = true; Focusable = true; }

    public void SetView(string v) { (Yaw, Pitch) = v switch { "vorne" => (0, 0), "oben" => (0, 89), "seite" => (-90, 0), _ => (-35, 22) }; Zoom = 1; InvalidateVisual(); }

    (double cx, double cy, double cz, double r, double minZ) Frame()
    {
        var v = Model!.V; double x0 = v.Min(p => p.X), x1 = v.Max(p => p.X), y0 = v.Min(p => p.Y), y1 = v.Max(p => p.Y), z0 = v.Min(p => p.Z), z1 = v.Max(p => p.Z);
        return ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2, Math.Max(1e-6, 0.5 * Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0))), z0);
    }

    // T-0281: Trendo-Netze haben Z oben (Zentimeter) – vorher wurde Y als oben gezeigt, Möbel lagen auf dem Rücken.
    (double X, double Y, double D) P(Vec3 p, (double cx, double cy, double cz, double r, double minZ) f)
    {
        if (EyeHeightCm is { } eye)
        {
            // Kamera steht vor dem Objekt auf Augenhöhe und schaut waagrecht (Drehen per Maus umrundet das Objekt), Bildwinkel 90° senkrecht wie eine Brille
            double ya0 = Yaw * Math.PI / 180, dist = Math.Max(EyeDistanceCm, f.r * 1.6);
            var cam = new Vec3(f.cx - Math.Sin(ya0) * dist, f.cy - Math.Cos(ya0) * dist, f.minZ + eye);
            var q0 = p - cam; double right = q0.X * Math.Cos(ya0) - q0.Y * Math.Sin(ya0), fwd = q0.X * Math.Sin(ya0) + q0.Y * Math.Cos(ya0);
            var z = Math.Max(1, fwd); var focal = Bounds.Height / (2 * Math.Tan(45 * Math.PI / 180)) * Zoom;
            return (Bounds.Width / 2 + right * focal / z, Bounds.Height / 2 - q0.Z * focal / z, -fwd);
        }
        double qx = p.X - f.cx, qy = p.Y - f.cy, qz = p.Z - f.cz, ya = Yaw * Math.PI / 180, pa = Pitch * Math.PI / 180;
        double x1 = qx * Math.Cos(ya) - qy * Math.Sin(ya), y1 = qx * Math.Sin(ya) + qy * Math.Cos(ya);
        double y2 = qz * Math.Cos(pa) + y1 * Math.Sin(pa), d = -y1 * Math.Cos(pa) + qz * Math.Sin(pa);
        var s = Math.Min(Bounds.Width, Bounds.Height) * 0.42 / f.r * Zoom;
        return (Bounds.Width / 2 + x1 * s, Bounds.Height / 2 - y2 * s, d);
    }

    /// <summary>Bildschirmpunkt eines Modellpunkts (für Tests und Treffer).</summary>
    internal Point Screen(Vec3 v) { var q = P(v, Frame()); return new Point(q.X, q.Y); }

    /// <summary>Richtung von der Szene zur Kamera (Welt).</summary>
    Vec3 ViewDir() { double ya = Yaw * Math.PI / 180, pa = EyeHeightCm is null ? Pitch * Math.PI / 180 : 0; return new Vec3(-Math.Sin(ya) * Math.Cos(pa), -Math.Cos(ya) * Math.Cos(pa), Math.Sin(pa)).Norm(); }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#3F3F3F"), 0), new GradientStop(Color.Parse("#0A1322"), 1) } }, new Rect(Bounds.Size));
        if (Model is not { Triangles: > 0 } m) return;
        var f = Frame(); var view = ViewDir();
        Point S(Vec3 v) { var q = P(v, f); return new Point(q.X, q.Y); }
        // Studio-Boden: heller Kreis, der nach außen ausläuft, darüber ein feines Raster (1 m, 25 cm oder 2,5 cm)
        if (EyeHeightCm is not null) // Augenhöhe: Boden bis zum Horizont (Bildmitte), weich ins Dunkle auslaufend
            ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#3A3F48"), 0), new GradientStop(Color.Parse("#6B7280"), 1) } }, new Rect(0, Bounds.Height / 2, Bounds.Width, Bounds.Height / 2));
        else
        foreach (var (k, alpha) in new[] { (1.7, (byte)45), (1.25, (byte)60), (0.85, (byte)70) }) // drei Scheiben übereinander: hell in der Mitte, weich nach außen
        {
            var disc = new StreamGeometry();
            using (var gc = disc.Open()) { for (var i = 0; i < 64; i++) { var a = i * Math.PI / 32; var pt = S(new Vec3(f.cx + Math.Cos(a) * f.r * k, f.cy + Math.Sin(a) * f.r * k, f.minZ)); if (i == 0) gc.BeginFigure(pt, true); else gc.LineTo(pt); } gc.EndFigure(true); }
            ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(alpha, 150, 165, 188)), null, disc);
        }
        var step = f.r > 300 ? 100.0 : f.r > 30 ? 25 : 2.5; var gp = new Pen(new SolidColorBrush(Color.Parse("#16FFFFFF")), 1); var ge = Math.Ceiling(f.r * 1.2 / step) * step;
        for (var g = -ge; g <= ge + 1e-9 && EyeHeightCm is null; g += step)
        {
            ctx.DrawLine(gp, S(new Vec3(f.cx + g, f.cy - ge, f.minZ)), S(new Vec3(f.cx + g, f.cy + ge, f.minZ)));
            ctx.DrawLine(gp, S(new Vec3(f.cx - ge, f.cy + g, f.minZ)), S(new Vec3(f.cx + ge, f.cy + g, f.minZ)));
        }
        var n = m.Triangles; var stride = Math.Max(1, n / MaxTris);
        // Bodenschatten: vereinfachtes Netz entlang des Hauptlichts auf den Boden geworfen, deckend in eine Deckkraft-Ebene gezeichnet
        // (gleichmäßig dicht, Überlappungen addieren sich nicht), dreimal leicht versetzt für einen weichen Halbschatten
        if (!Wire)
        {
            if (!ReferenceEquals(_shadowFor, m)) { _shadowFor = m; _shadow = Trendo.Core.Arch.Plan3D.Simplify(m, f.r / 30); }
            var sm = _shadow!; var shade = new SolidColorBrush(Color.FromRgb(4, 8, 16));
            foreach (var (jx, jy, alpha) in new[] { (0.0, 0.0, 0.42), (0.08, 0.06, 0.18), (-0.07, -0.06, 0.18) })
            {
                var l = new Vec3(Shading.Key.X + jx, Shading.Key.Y + jy, Shading.Key.Z).Norm();
                using (ctx.PushOpacity(alpha))
                    for (var t = 0; t < sm.Triangles; t++)
                    {
                        Point pa = S(Shading.OnGround(sm.V[sm.I[3 * t]], f.minZ, l)), pb = S(Shading.OnGround(sm.V[sm.I[3 * t + 1]], f.minZ, l)), pc = S(Shading.OnGround(sm.V[sm.I[3 * t + 2]], f.minZ, l));
                        var g = new StreamGeometry(); using (var gc = g.Open()) { gc.BeginFigure(pa, true); gc.LineTo(pb); gc.LineTo(pc); gc.EndFigure(true); }
                        ctx.DrawGeometry(shade, new Pen(shade, 0.8), g);
                    }
            }
        }
        var tris = new List<(int T, double D)>(n / stride + 1);
        for (var t = 0; t < n; t += stride)
        {
            var (a, b, c) = (m.V[m.I[3 * t]], m.V[m.I[3 * t + 1]], m.V[m.I[3 * t + 2]]);
            tris.Add((t, P((a + b + c) * (1.0 / 3), f).D));
        }
        tris.Sort((p, q) => p.D.CompareTo(q.D));
        var edge = Wire ? new Pen(new SolidColorBrush(Color.Parse("#AA7FD3FF")), 0.7) : null; var reach = Math.Max(1, f.r * 0.25);
        foreach (var (t, _) in tris)
        {
            var (a, b, c) = (m.V[m.I[3 * t]], m.V[m.I[3 * t + 1]], m.V[m.I[3 * t + 2]]);
            var nn = Vec3.Cross(b - a, c - a); if (nn.Len < 1e-15) continue;
            var nu = nn.Norm(); if (Vec3.Dot(nu, view) < 0) nu = nu * -1; // beidseitig: Rückseiten wie Vorderseiten beleuchten
            byte r = t < m.R.Count ? m.R[t] : m.CR, g = t < m.G.Count ? m.G[t] : m.CG, bl = t < m.B.Count ? m.B[t] : m.CB;
            var col = Shading.Shade(Color.FromRgb(r, g, bl), nu, view, Shading.GroundAo((a.Z + b.Z + c.Z) / 3 - f.minZ, reach));
            var fill = new SolidColorBrush(col);
            var geo = new StreamGeometry(); using (var gc = geo.Open()) { gc.BeginFigure(S(a), true); gc.LineTo(S(b)); gc.LineTo(S(c)); gc.EndFigure(true); }
            ctx.DrawGeometry(Wire ? new SolidColorBrush(Color.FromArgb(150, col.R, col.G, col.B)) : fill, edge ?? new Pen(fill, 0.6), geo);
        }
        if (Markers is { Count: > 0 } mk)
        {
            var gold = new SolidColorBrush(Color.Parse("#FACC15")); var dark = new Pen(new SolidColorBrush(Color.Parse("#AA000000")), 1);
            foreach (var v in mk) { var c = S(v); ctx.DrawGeometry(gold, dark, new PolylineGeometry([new Point(c.X - 6, c.Y - 4), new Point(c.X + 6, c.Y - 4), new Point(c.X + 6, c.Y + 4), new Point(c.X - 6, c.Y + 4)], true)); ctx.DrawEllipse(Brushes.Black, null, c, 2, 2); }
        }
        if (EyeHeightCm is { } eh)
            ctx.DrawText(new FormattedText($"Augenhöhe {eh / 100:0.00} m · Abstand {Math.Max(EyeDistanceCm, f.r * 1.6) / 100:0.0} m", System.Globalization.CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface("Inter"), 12, Brushes.White), new Point(10, 8));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); Focus(); _drag = e.GetPosition(this); if (e.ClickCount == 2) SetView("iso"); e.Pointer.Capture(this); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); _drag = null; e.Pointer.Capture(null); }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_drag is not { } d) return;
        var p = e.GetPosition(this); Yaw = (Yaw + (p.X - d.X) * 0.5) % 360; Pitch = Math.Clamp(Pitch + (p.Y - d.Y) * 0.4, -89, 89); _drag = p; InvalidateVisual();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Zoom = Math.Clamp(Zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.1, 40); InvalidateVisual(); e.Handled = true; }
}
