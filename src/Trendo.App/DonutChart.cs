using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Ringdiagramm (Anteile je Kategorie) mit Summe in der Mitte; ein Klick auf ein Segment meldet dessen Index, die Auswahl tritt hervor.</summary>
public sealed class DonutChart : Control
{
    public List<(string Name, string Color, double Value)> Slices { get; set; } = [];
    public string Center { get; set; } = "";
    public string CenterSub { get; set; } = "";
    public int Selected { get; set; } = -1;
    public IBrush Foreground { get; set; } = Brushes.White;
    public IBrush Muted { get; set; } = Brushes.Gray;
    public Action<int>? SliceClicked { get; set; }
    int _hover = -1;

    public DonutChart() { Cursor = new Cursor(StandardCursorType.Hand); }
    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 220 : Math.Min(a.Width, 260), 220);

    (Point C, double R, double Inner) Geo() { var s = Math.Min(Bounds.Width, Bounds.Height); return (new Point(Bounds.Width / 2, Bounds.Height / 2), s / 2 - 8, s / 2 - 8 - Math.Max(18, s * 0.14)); }

    /// <summary>Segment unter einem Punkt (−1 = keins).</summary>
    public int HitTest(Point p)
    {
        var (c, r, inner) = Geo(); var d = p - c; var dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (dist < inner - 2 || dist > r + 8) return -1;
        var ang = (Math.Atan2(d.Y, d.X) * 180 / Math.PI + 90 + 360) % 360; var total = Slices.Sum(s => s.Value); if (total <= 0) return -1;
        var acc = 0.0;
        for (var i = 0; i < Slices.Count; i++) { acc += Slices[i].Value / total * 360; if (ang <= acc) return i; }
        return Slices.Count - 1;
    }
    /// <summary>Mittelpunkt eines Segments (für Tests und Hinweise).</summary>
    public Point SliceCenter(int i)
    {
        var (c, r, inner) = Geo(); var total = Slices.Sum(s => s.Value); var start = Slices.Take(i).Sum(s => s.Value) / total * 360; var mid = start + Slices[i].Value / total * 180;
        var a = (mid - 90) * Math.PI / 180; var rr = (r + inner) / 2; return new(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr);
    }

    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); var h = HitTest(e.GetPosition(this)); if (h != _hover) { _hover = h; InvalidateVisual(); } }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); _hover = -1; InvalidateVisual(); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); var h = HitTest(e.GetPosition(this)); if (h >= 0) { e.Handled = true; SliceClicked?.Invoke(h); } }

    public override void Render(DrawingContext g)
    {
        var (c, r, inner) = Geo(); var tf = new Typeface("Segoe UI, Inter, DejaVu Sans"); var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        var total = Slices.Sum(s => s.Value);
        // Grundring mit leichtem Schatten (Tiefe statt flach)
        g.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), r - inner + 6), c + new Vector(0, 3), (r + inner) / 2, (r + inner) / 2);
        g.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), r - inner), c, (r + inner) / 2, (r + inner) / 2);
        if (total > 0)
        {
            var start = -90.0;
            for (var i = 0; i < Slices.Count; i++)
            {
                var sweep = Slices[i].Value / total * 360; if (sweep <= 0) continue;
                var pop = i == Selected || i == _hover ? 6 : 0; var dim = Selected >= 0 && i != Selected;
                var mid = (start + sweep / 2) * Math.PI / 180; var off = new Vector(Math.Cos(mid) * pop, Math.Sin(mid) * pop);
                var col = Color.Parse(Slices[i].Color);
                var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Lighter(col, 0.18), 0), new GradientStop(col, 1) }, Opacity = dim ? 0.35 : 1 };
                g.DrawGeometry(brush, new Pen(new SolidColorBrush(Color.Parse("#FF0B1424")), 2), Arc(c + off, r, inner, start, Math.Min(sweep, 359.99)));
                start += sweep;
            }
        }
        var big = new FormattedText(Center, de, FlowDirection.LeftToRight, new Typeface(tf.FontFamily, FontStyle.Normal, FontWeight.Bold), Math.Max(12, inner * 0.28), Foreground);
        g.DrawText(big, new Point(c.X - big.Width / 2, c.Y - big.Height / 2 - 7));
        var sub = new FormattedText(_hover >= 0 && _hover < Slices.Count ? Slices[_hover].Name : CenterSub, de, FlowDirection.LeftToRight, tf, 11, Muted);
        g.DrawText(sub, new Point(c.X - sub.Width / 2, c.Y + big.Height / 2 - 4));
    }

    static Color Lighter(Color c, double f) => Color.FromArgb(c.A, (byte)(c.R + (255 - c.R) * f), (byte)(c.G + (255 - c.G) * f), (byte)(c.B + (255 - c.B) * f));

    static StreamGeometry Arc(Point c, double r, double inner, double startDeg, double sweepDeg)
    {
        Point P(double rad, double deg) { var a = deg * Math.PI / 180; return new(c.X + Math.Cos(a) * rad, c.Y + Math.Sin(a) * rad); }
        var geo = new StreamGeometry(); var large = sweepDeg > 180;
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(P(r, startDeg), true);
            ctx.ArcTo(P(r, startDeg + sweepDeg), new Size(r, r), 0, large, SweepDirection.Clockwise);
            ctx.LineTo(P(inner, startDeg + sweepDeg));
            ctx.ArcTo(P(inner, startDeg), new Size(inner, inner), 0, large, SweepDirection.CounterClockwise);
            ctx.EndFigure(true);
        }
        return geo;
    }
}
