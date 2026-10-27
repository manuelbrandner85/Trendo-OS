using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>N11: Funktionsgraph mit Achsen, Gitter und mehreren Kurven (Lücken bei NaN).</summary>
public sealed class GraphView : Control
{
    public static readonly string[] Palette = ["#2F86F6", "#E5484D", "#30A46C", "#F76B15", "#8E4EC6"];
    public List<(string Label, List<(double X, double Y)> Points)> Series { get; } = [];
    public IBrush Background { get; set; } = Brushes.White;
    public IBrush Foreground { get; set; } = Brushes.Black;

    /// <summary>Sichtbarer y-Bereich: gerundete Grenzen der Werte, Ausreißer (Polstellen) abgeschnitten.</summary>
    public (double Min, double Max) YRange()
    {
        var ys = Series.SelectMany(s => s.Points).Select(p => p.Y).Where(double.IsFinite).Order().ToList();
        if (ys.Count == 0) return (-1, 1);
        double lo = ys[(int)(ys.Count * 0.02)], hi = ys[(int)Math.Min(ys.Count - 1, ys.Count * 0.98)];
        if (hi - lo < 1e-9) { lo -= 1; hi += 1; }
        var pad = (hi - lo) * 0.08; return (lo - pad, hi + pad);
    }

    public override void Render(DrawingContext g)
    {
        var b = Bounds; g.FillRectangle(Background, new Rect(b.Size));
        var pts = Series.SelectMany(s => s.Points).ToList(); if (pts.Count == 0) return;
        double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X); var (y0, y1) = YRange();
        const double m = 34;
        double W = Math.Max(1, b.Width - m - 10), H = Math.Max(1, b.Height - m - 10);
        Point P(double x, double y) => new(m + (x - x0) / (x1 - x0) * W, 10 + (y1 - y) / (y1 - y0) * H);
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 1); var axis = new Pen(Foreground, 1.2);
        var tf = new Typeface("Segoe UI, Inter, DejaVu Sans");
        foreach (var (a, c, vertical) in new[] { (x0, x1, true), (y0, y1, false) })
        {
            var step = Nice((c - a) / 8);
            for (var v = Math.Ceiling(a / step) * step; v <= c; v += step)
            {
                var p = vertical ? P(v, y0) : P(x0, v);
                if (vertical) g.DrawLine(grid, new Point(p.X, 10), new Point(p.X, 10 + H)); else g.DrawLine(grid, new Point(m, p.Y), new Point(m + W, p.Y));
                var ft = new FormattedText(Trendo.Core.Calc.Format(Math.Round(v / step) * step), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 10, Foreground);
                g.DrawText(ft, vertical ? new Point(p.X - ft.Width / 2, 10 + H + 4) : new Point(m - ft.Width - 4, p.Y - ft.Height / 2));
            }
        }
        if (x0 <= 0 && x1 >= 0) g.DrawLine(axis, P(0, y0), P(0, y1));
        if (y0 <= 0 && y1 >= 0) g.DrawLine(axis, P(x0, 0), P(x1, 0));
        using (g.PushClip(new Rect(m, 10, W, H)))
            for (var k = 0; k < Series.Count; k++)
            {
                var pen = new Pen(Brush.Parse(Palette[k % Palette.Length]), 2.2);
                var geo = new StreamGeometry(); using (var ctx = geo.Open())
                {
                    var open = false; var span = y1 - y0;
                    foreach (var (x, y) in Series[k].Points)
                    {
                        if (!double.IsFinite(y) || y > y1 + span * 4 || y < y0 - span * 4) { if (open) ctx.EndFigure(false); open = false; continue; }
                        if (!open) { ctx.BeginFigure(P(x, y), false); open = true; } else ctx.LineTo(P(x, y));
                    }
                    if (open) ctx.EndFigure(false);
                }
                g.DrawGeometry(null, pen, geo);
            }
    }

    static double Nice(double raw)
    {
        if (raw <= 0 || !double.IsFinite(raw)) return 1;
        var e = Math.Pow(10, Math.Floor(Math.Log10(raw))); var f = raw / e;
        return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * e;
    }
}
