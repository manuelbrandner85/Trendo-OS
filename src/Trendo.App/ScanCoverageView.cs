using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>S4: Abdeckungsscheibe beim Einscannen – Blick von oben aufs Objekt. Außen „flach“, Mitte „schräg“, innen „von oben“; je 12 Richtungen,
/// rot = kein Foto, gelb = ein Foto, grün = zwei und mehr. 0° (erstes Foto) zeigt nach oben, im Uhrzeigersinn.</summary>
public sealed class ScanCoverageView : Control
{
    public ScanCoverage.Result? Result { get; set; }

    static IBrush Fill(int n) => new SolidColorBrush(Color.Parse(n == 0 ? "#B3EF4444" : n == 1 ? "#CCE8B04B" : "#CC2FBF71"));

    public override void Render(DrawingContext ctx)
    {
        if (Result is not { } r) return;
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2); var R = Math.Min(Bounds.Width, Bounds.Height) / 2 - 4;
        double[] radii = [R, R * 0.68, R * 0.38, R * 0.12]; // Grenzen: flach | schräg | von oben | Mitte
        for (var ring = 0; ring < 3; ring++)
            for (var s = 0; s < ScanCoverage.Sectors; s++)
            {
                double a0 = (s * 30 - 15 - 90) * Math.PI / 180, a1 = (s * 30 + 15 - 90) * Math.PI / 180, ro = radii[ring] - 1.5, ri = radii[ring + 1] + 1.5;
                Point P(double a, double rr) => new(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr);
                var g = new StreamGeometry();
                using (var k = g.Open())
                {
                    k.BeginFigure(P(a0 + 0.02, ro), true);
                    k.ArcTo(P(a1 - 0.02, ro), new Size(ro, ro), 0, false, SweepDirection.Clockwise);
                    k.LineTo(P(a1 - 0.02, ri));
                    k.ArcTo(P(a0 + 0.02, ri), new Size(ri, ri), 0, false, SweepDirection.CounterClockwise);
                    k.EndFigure(true);
                }
                ctx.DrawGeometry(Fill(r.Count(ring, s)), null, g);
            }
        var ft = new FormattedText($"{r.Percent} %", System.Globalization.CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold), 13, Brushes.White);
        ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
        var top = new FormattedText("0°", System.Globalization.CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, new Typeface("Inter"), 10, new SolidColorBrush(Color.Parse("#D1D5DB")));
        ctx.DrawText(top, new Point(c.X - top.Width / 2, c.Y - R - 2));
    }
}
