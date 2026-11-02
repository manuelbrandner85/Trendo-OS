using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0270: Roboterarm-Ansicht – links Draufsicht (Reichweite, Wegpunkte, Klick setzt das Ziel), rechts Seitenansicht in der Armebene.
/// Glieder in realen Proportionen (Tischarm: Sockel Ø 90 mm, Gelenkgehäuse Ø 38/32 mm, Glieder 26/20 mm stark), als schattierte Zylinder mit
/// Lichtkante, runden Gelenkdeckeln, Greifer mit zwei Backen und Kontaktschatten auf dem Tisch.</summary>
public sealed class ArmView : Control
{
    public ArmConfig Arm { get; set; } = new();
    public Joints J { get; set; } = new(0, 90, -90);
    public (double X, double Y, double Z)? Target { get; set; }
    public IReadOnlyList<(double X, double Y, double Z)> Points { get; set; } = [];
    public double GripMm { get; set; } = 30;
    public Action<double, double>? Picked;

    public ArmView() { ClipToBounds = true; Focusable = true; }

    double Reach => Arm.L1 + Arm.L2;
    Rect TopRect => new(0, 0, Bounds.Width * 0.42, Bounds.Height);
    Rect SideRect => new(Bounds.Width * 0.42, 0, Bounds.Width * 0.58, Bounds.Height);
    double TopScale => Math.Min(TopRect.Width, TopRect.Height) * 0.44 / Reach;

    public Point TopPoint(double x, double y) { var r = TopRect; var s = TopScale; return new(r.Center.X + x * s, r.Center.Y - y * s); }

    static IBrush Cyl(Color c, Point a, Point b, double w)
    {
        // Querverlauf: Lichtkante oben links, Körperschatten unten rechts – wie ein lackiertes Aluminiumrohr
        var dx = b.X - a.X; var dy = b.Y - a.Y; var l = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy)); var nx = -dy / l * w / 2; var ny = dx / l * w / 2;
        Color M(double k) => Color.FromRgb((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255));
        return new LinearGradientBrush { StartPoint = new RelativePoint(a.X + nx, a.Y + ny, RelativeUnit.Absolute), EndPoint = new RelativePoint(a.X - nx, a.Y - ny, RelativeUnit.Absolute),
            GradientStops = { new GradientStop(M(0.55), 0), new GradientStop(M(1.25), 0.28), new GradientStop(M(1.0), 0.45), new GradientStop(M(0.62), 0.85), new GradientStop(M(0.45), 1) } };
    }

    static void Link(DrawingContext ctx, Point a, Point b, double w, Color c)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var l = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy)); var nx = -dy / l * w / 2; var ny = dx / l * w / 2;
        var g = new StreamGeometry(); using (var gc = g.Open()) { gc.BeginFigure(new Point(a.X + nx, a.Y + ny), true); gc.LineTo(new Point(b.X + nx, b.Y + ny)); gc.LineTo(new Point(b.X - nx, b.Y - ny)); gc.LineTo(new Point(a.X - nx, a.Y - ny)); gc.EndFigure(true); }
        ctx.DrawGeometry(Cyl(c, a, b, w), new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 0.8), g);
    }

    static void Joint(DrawingContext ctx, Point p, double r, Color c)
    {
        ctx.DrawEllipse(new RadialGradientBrush { Center = new RelativePoint(0.35, 0.3, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.3, 0.25, RelativeUnit.Relative), RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromRgb(235, 238, 242), 0), new GradientStop(c, 0.55), new GradientStop(Color.FromRgb((byte)(c.R * 0.5), (byte)(c.G * 0.5), (byte)(c.B * 0.5)), 1) } }, new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 0.8), p, r, r);
        ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(60, 64, 72)), null, p, r * 0.32, r * 0.32); // Achsdeckel
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#383838"), 0), new GradientStop(Color.Parse("#0A1322"), 1) } }, new Rect(Bounds.Size));
        var orange = Color.FromRgb(232, 120, 32); var grey = Color.FromRgb(150, 156, 166); var dark = Color.FromRgb(46, 50, 58);
        // ---- Draufsicht ----
        var tr = TopRect; var ts = TopScale; var c0 = TopPoint(0, 0);
        ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(28, 79, 163, 255)), new Pen(new SolidColorBrush(Color.FromArgb(120, 127, 211, 255)), 1, new DashStyle([4, 3], 0)), c0, Reach * ts, Reach * ts);
        for (var g = 50.0; g < Reach; g += 50) ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1), c0, g * ts, g * ts);
        var (fx, fy, fz) = RobotArm.Forward(Arm, J); var br = J.Base * Math.PI / 180;
        var elbowR = Arm.L1 * Math.Cos(J.Shoulder * Math.PI / 180);
        var e0 = TopPoint(elbowR * Math.Cos(br), elbowR * Math.Sin(br)); var t0 = TopPoint(fx, fy);
        ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, new Point(c0.X + 3, c0.Y + 4), 45 * ts, 45 * ts);
        Link(ctx, c0, e0, 26 * ts, orange); Link(ctx, e0, t0, 20 * ts, orange);
        Joint(ctx, c0, 45 * ts, grey); Joint(ctx, e0, 17 * ts, grey); Joint(ctx, t0, 12 * ts, dark);
        for (var k = 0; k < Points.Count; k++)
        {
            var p = TopPoint(Points[k].X, Points[k].Y); ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#4ADE80")), new Pen(Brushes.White, 1), p, 5, 5);
            ctx.DrawText(new FormattedText((k + 1).ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.White), new Point(p.X + 6, p.Y - 14));
            if (k > 0) { var q = TopPoint(Points[k - 1].X, Points[k - 1].Y); ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(160, 74, 222, 128)), 1.5, new DashStyle([3, 3], 0)), q, p); }
        }
        if (Target is { } tg) { var p = TopPoint(tg.X, tg.Y); var pen = new Pen(new SolidColorBrush(Color.Parse("#F5A524")), 2); ctx.DrawLine(pen, new Point(p.X - 8, p.Y), new Point(p.X + 8, p.Y)); ctx.DrawLine(pen, new Point(p.X, p.Y - 8), new Point(p.X, p.Y + 8)); }
        ctx.DrawText(new FormattedText("Von oben – Klick setzt das Ziel", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 11, new SolidColorBrush(Color.Parse("#8FA3BF"))), new Point(tr.X + 10, tr.Y + 8));
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1), new Point(tr.Right, 10), new Point(tr.Right, tr.Bottom - 10));
        // ---- Seitenansicht in der Armebene ----
        var sr = SideRect; var maxH = Arm.BaseHeight + Reach; var ss = Math.Min(sr.Width * 0.85 / (Reach * 1.25), sr.Height * 0.8 / maxH);
        var floorY = sr.Bottom - 40; var ox = sr.X + sr.Width * 0.28;
        Point S(double r, double z) => new(ox + r * ss, floorY - z * ss);
        ctx.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromRgb(120, 94, 70), 0), new GradientStop(Color.FromRgb(70, 52, 38), 1) } }, new Rect(sr.X + 10, floorY, sr.Width - 20, 18)); // Tischplatte (Holz)
        var rr = Math.Sqrt(fx * fx + fy * fy);
        ctx.DrawEllipse(new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb(110, 0, 0, 0), 0), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1) } }, null, S(0, 0), 70 * ss, 8 * ss + 3); // Kontaktschatten Sockel
        ctx.DrawEllipse(new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb(Math.Clamp((int)(90 - fz * 0.4), 10, 90) is var al ? (byte)al : (byte)40, 0, 0, 0), 0), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1) } }, null, S(rr, 0), 22 * ss, 5 * ss + 2); // Schatten Greifer
        // Sockel: Fuß, Drehteller, Säule
        var foot = new Rect(S(-45, 12).X, S(-45, 12).Y, 90 * ss, 12 * ss);
        ctx.DrawRectangle(Cyl(grey, new Point(foot.Center.X, foot.Top), new Point(foot.Center.X, foot.Bottom), foot.Width), null, foot, 3, 3);
        Link(ctx, S(0, 12), S(0, Arm.BaseHeight), 52 * ss, grey);
        var sh = S(0, Arm.BaseHeight); var s1 = J.Shoulder * Math.PI / 180; var s2 = (J.Shoulder + J.Elbow) * Math.PI / 180;
        var el = S(Arm.L1 * Math.Cos(s1), Arm.BaseHeight + Arm.L1 * Math.Sin(s1)); var wr = S(rr, fz);
        Link(ctx, sh, el, 26 * ss, orange); Link(ctx, el, wr, 20 * ss, orange);
        // Greifer: Handgelenk, zwei Backen senkrecht nach unten
        var gw = Math.Clamp(GripMm, 0, 60) / 2 * ss; var fl = 34 * ss;
        ctx.DrawRectangle(new SolidColorBrush(dark), null, new Rect(wr.X - 14 * ss, wr.Y, 28 * ss, 10 * ss), 2, 2);
        foreach (var sgn in new[] { -1, 1 }) ctx.DrawRectangle(Cyl(Color.FromRgb(70, 74, 82), new Point(wr.X + sgn * gw, wr.Y), new Point(wr.X + sgn * gw, wr.Y + fl), 6 * ss), null, new Rect(wr.X + sgn * gw - 3 * ss, wr.Y + 8 * ss, 6 * ss, fl), 1.5, 1.5);
        Joint(ctx, sh, 19 * ss, grey); Joint(ctx, el, 16 * ss, grey); Joint(ctx, wr, 11 * ss, dark);
        if (Target is { } t2) { var p = S(Math.Sqrt(t2.X * t2.X + t2.Y * t2.Y), t2.Z); ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse("#F5A524")), 2), p, 6, 6); }
        ctx.DrawText(new FormattedText($"Seitenansicht · Greifer bei {fx:0} / {fy:0} / {fz:0} mm", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 11, new SolidColorBrush(Color.Parse("#8FA3BF"))), new Point(sr.X + 10, sr.Y + 8));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus();
        var p = e.GetPosition(this); if (!TopRect.Contains(p)) return;
        var s = TopScale; var c = TopPoint(0, 0); Picked?.Invoke(Math.Round((p.X - c.X) / s), Math.Round((c.Y - p.Y) / s)); e.Handled = true;
    }
}
