using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>T-0228 E2: Diagramm in Trendo Tabelle (Säule, Linie, Kreis) – zeichnet live aus den Zellen, gleiche Aufteilung wie im PDF.</summary>
public sealed class SheetChartView(SheetChart chart, Func<ChartData> data) : Control
{
    public SheetChart Chart { get; } = chart;

    public override void Render(DrawingContext g)
    {
        var area = new Rect(Bounds.Size);
        g.FillRectangle(Brushes.White, area);
        var d = data();
        var face = new Typeface("Inter"); var bold = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);
        FormattedText T(string s, double size, IBrush b, bool strong = false) => new(s, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, strong ? bold : face, size, b);
        var title = T(Chart.Title, 14, Brushes.Black, true);
        g.DrawText(title, new Point((area.Width - title.Width) / 2, 6));
        IBrush Brush(int i) => new SolidColorBrush(Color.Parse(ChartData.Palette[i % ChartData.Palette.Length]));
        var muted = new SolidColorBrush(Color.Parse("#FF5B6B82"));
        if (d.Labels.Count == 0 || d.Series.Count == 0) { g.DrawText(T("Keine Zahlen im Bereich " + Chart.Range, 12, muted), new Point(12, 40)); return; }
        var plot = new Rect(48, 32, Math.Max(10, area.Width - 64), Math.Max(10, area.Height - (d.Series.Count > 1 ? 72 : 58)));
        if (Chart.Kind == ChartKind.Kreis)
        {
            var vals = d.Series[0].Values.Select(v => Math.Max(0, v)).ToArray(); var sum = vals.Sum(); if (sum <= 0) return;
            var size = Math.Min(plot.Height + 20, plot.Width / 2); var c = new Point(16 + size / 2, plot.Y + size / 2); var rad = size / 2; var a = -Math.PI / 2;
            for (var i = 0; i < vals.Length; i++)
            {
                var sweep = vals[i] / sum * Math.PI * 2; if (sweep <= 0) continue;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(c, true);
                    ctx.LineTo(new Point(c.X + rad * Math.Cos(a), c.Y + rad * Math.Sin(a)));
                    ctx.ArcTo(new Point(c.X + rad * Math.Cos(a + sweep), c.Y + rad * Math.Sin(a + sweep)), new Size(rad, rad), 0, sweep > Math.PI, SweepDirection.Clockwise);
                    ctx.EndFigure(true);
                }
                g.DrawGeometry(Brush(i), new Pen(Brushes.White, 1.5), geo); a += sweep;
                g.FillRectangle(Brush(i), new Rect(32 + size, plot.Y + i * 18, 11, 11));
                g.DrawText(T($"{d.Labels[i]} ({vals[i] / sum:P0})", 12, Brushes.Black), new Point(48 + size, plot.Y + i * 18 - 3));
            }
            return;
        }
        double min = d.Min, max = Math.Max(d.Max, min + 1e-9); var step = ChartData.NiceStep(max - min); max = Math.Ceiling(max / step) * step; min = Math.Floor(min / step) * step;
        double Y(double v) => plot.Bottom - (v - min) / (max - min) * plot.Height;
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#FFE5E7EB")), 1);
        for (var v = min; v <= max + step / 2; v += step)
        {
            g.DrawLine(gridPen, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            var lab = T(ChartData.AxisLabel(v), 11, muted); g.DrawText(lab, new Point(plot.Left - 6 - lab.Width, Y(v) - lab.Height / 2));
        }
        var n = d.Labels.Count; var slot = plot.Width / n;
        for (var i = 0; i < n; i++) { var lab = T(d.Labels[i], 11, muted) ; lab.MaxTextWidth = Math.Max(1, slot); lab.MaxLineCount = 1; lab.Trimming = TextTrimming.CharacterEllipsis; g.DrawText(lab, new Point(plot.X + i * slot + (slot - Math.Min(slot, lab.Width)) / 2, plot.Bottom + 3)); }
        for (var k = 0; k < d.Series.Count; k++)
        {
            var vals = d.Series[k].Values;
            if (Chart.Kind == ChartKind.Linie)
            {
                var pen = new Pen(Brush(k), 2.4);
                for (var i = 1; i < n; i++) g.DrawLine(pen, new Point(plot.X + (i - 0.5) * slot, Y(vals[i - 1])), new Point(plot.X + (i + 0.5) * slot, Y(vals[i])));
                for (var i = 0; i < n; i++) g.DrawEllipse(Brush(k), null, new Point(plot.X + (i + 0.5) * slot, Y(vals[i])), 3, 3);
            }
            else
            {
                var bw = slot * 0.7 / d.Series.Count;
                for (var i = 0; i < n; i++) { var x = plot.X + i * slot + slot * 0.15 + k * bw; var y0 = Y(Math.Max(0, min)); var y1 = Y(vals[i]); g.FillRectangle(Brush(k), new Rect(x, Math.Min(y0, y1), Math.Max(1, bw - 2), Math.Abs(y0 - y1))); }
            }
        }
        if (d.Series.Count > 1)
            for (var k = 0; k < d.Series.Count; k++) { g.FillRectangle(Brush(k), new Rect(plot.X + k * 110, area.Height - 18, 10, 10)); g.DrawText(T(d.Series[k].Name, 11, Brushes.Black), new Point(plot.X + k * 110 + 14, area.Height - 21)); }
    }
}
