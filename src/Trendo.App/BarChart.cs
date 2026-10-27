using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Säulendiagramm für Monatswerte (eine oder zwei Reihen), mit Verlauf, abgerundeten Säulen und Hilfslinien.</summary>
public sealed class BarChart : Control
{
    public string[] Labels { get; set; } = [];
    public List<(string Name, string Color, double[] Values)> Series { get; } = [];
    public IBrush Foreground { get; set; } = Brushes.Gray;
    public IBrush Grid { get; set; } = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));

    public override void Render(DrawingContext g)
    {
        var b = Bounds; if (Series.Count == 0 || Labels.Length == 0) return;
        var tf = new Typeface("Segoe UI, Inter, DejaVu Sans");
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        var max = Series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max();
        if (max <= 0)
        {
            var empty = new FormattedText("Noch keine Werte für dieses Jahr", de, FlowDirection.LeftToRight, tf, 13, Foreground);
            g.DrawText(empty, new Point((b.Width - empty.Width) / 2, (b.Height - empty.Height) / 2)); return;
        }
        var step = Nice(max / 4); max = Math.Ceiling(max / step) * step;
        const double left = 52, bottom = 22, top = 8;
        double W = Math.Max(1, b.Width - left - 6), H = Math.Max(1, b.Height - bottom - top);
        for (var v = 0.0; v <= max + 1e-9; v += step)
        {
            var y = top + H - v / max * H;
            g.DrawLine(new Pen(Grid, 1), new Point(left, y), new Point(left + W, y));
            var ft = new FormattedText(v.ToString("#,##0", de), de, FlowDirection.LeftToRight, tf, 10, Foreground);
            g.DrawText(ft, new Point(left - ft.Width - 6, y - ft.Height / 2));
        }
        var slot = W / Labels.Length; var bw = Math.Max(3, slot * 0.7 / Series.Count);
        for (var i = 0; i < Labels.Length; i++)
        {
            for (var s = 0; s < Series.Count; s++)
            {
                var v = i < Series[s].Values.Length ? Series[s].Values[i] : 0; if (v <= 0) continue;
                var h = v / max * H; var x = left + i * slot + slot * 0.15 + s * bw;
                var c = Color.Parse(Series[s].Color);
                var fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Lighten(c, 0.25), 0), new GradientStop(c, 1) } };
                g.DrawRectangle(fill, null, new RoundedRect(new Rect(x, top + H - h, bw - 2, h), 3, 3, 0, 0));
            }
            var lt = new FormattedText(Labels[i], de, FlowDirection.LeftToRight, tf, 10, Foreground);
            g.DrawText(lt, new Point(left + i * slot + (slot - lt.Width) / 2, top + H + 4));
        }
    }

    static Color Lighten(Color c, double f) => Color.FromArgb(c.A, (byte)(c.R + (255 - c.R) * f), (byte)(c.G + (255 - c.G) * f), (byte)(c.B + (255 - c.B) * f));
    static double Nice(double raw)
    {
        if (raw <= 0 || !double.IsFinite(raw)) return 1;
        var e = Math.Pow(10, Math.Floor(Math.Log10(raw))); var f = raw / e;
        return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * e;
    }
}
