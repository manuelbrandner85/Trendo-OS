using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Trendo-Logo B „Saphir-Kristall“ (gewählt am 26.10.2026, Vorlage docs/design/logo-vorschlaege/saphir-kristall.jpg).
/// Ein facettiertes T aus Saphir auf einer dunklen Navy-Kachel, viewBox 64×64. Einzige Quelle der Geometrie: Programm-Logo, SVG und App-Symbol
/// werden daraus erzeugt (<see cref="Svg"/>, tools/Trendo.Render mit TRENDO_LOGO).</summary>
public static class TrendoLogo
{
    /// <summary>Umriss des T im Uhrzeigersinn, außen (Kante) und innen (Tafel). Die Facetten liegen zwischen beiden.</summary>
    static readonly Point[] Outer =
    [
        new(12, 13), new(52, 13), new(55, 16), new(55, 21), new(52, 24), new(39, 24), new(39, 49),
        new(35, 54), new(29, 54), new(25, 49), new(25, 24), new(12, 24), new(9, 21), new(9, 16),
    ];
    static readonly Point[] Inner =
    [
        new(13.5, 15.6), new(50.5, 15.6), new(52.6, 17.2), new(52.6, 19.8), new(50.5, 21.4), new(36.6, 21.4), new(36.6, 48.2),
        new(33.6, 51.6), new(30.4, 51.6), new(27.4, 48.2), new(27.4, 21.4), new(13.5, 21.4), new(11.4, 19.8), new(11.4, 17.2),
    ];

    public record Facet(Point[] Points, string Fill, double Opacity = 1);

    /// <summary>Licht von links oben: Facetten, die zum Licht zeigen, sind hell, abgewandte tief blau.</summary>
    static string Shade(Point a, Point b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var len = Math.Sqrt(dx * dx + dy * dy);
        var (nx, ny) = (dy / len, -dx / len); // Außennormale (Uhrzeigersinn, y nach unten)
        var t = -0.45 * nx - 0.89 * ny;
        return t > 0.6 ? "#C7E6FF" : t > 0.2 ? "#7CB8FF" : t > -0.2 ? "#3B82F6" : t > -0.6 ? "#1E40AF" : "#172F75";
    }

    /// <summary>Alle Flächen des Kristalls in Zeichenreihenfolge.</summary>
    public static IReadOnlyList<Facet> Facets()
    {
        var list = new List<Facet> { new(Inner, "url(#tafel)") };
        // Innere Reflexe der Tafel: Querbalken mit Rauten vom Licht aus, Stamm mit Mittelgrat, unten der Funke
        list.Add(new([new(13.5, 15.6), new(32, 15.6), new(22, 21.4)], "#60A5FA", 0.4));
        list.Add(new([new(32, 15.6), new(22, 21.4), new(42, 21.4)], "#BFDBFE", 0.5));
        list.Add(new([new(32, 15.6), new(50.5, 15.6), new(42, 21.4)], "#0B1F5C", 0.45));
        list.Add(new([new(13.5, 15.6), new(22, 21.4), new(13.5, 21.4)], "#0B1F5C", 0.35));
        list.Add(new([new(27.4, 21.4), new(32, 21.4), new(32, 36)], "#93C5FD", 0.4));
        list.Add(new([new(32, 21.4), new(36.6, 21.4), new(32, 36)], "#0B1F5C", 0.45));
        list.Add(new([new(27.4, 30), new(32, 36), new(27.4, 48.2)], "#0B1F5C", 0.55));
        list.Add(new([new(36.6, 30), new(32, 36), new(36.6, 48.2)], "#93C5FD", 0.4));
        list.Add(new([new(32, 43), new(27.4, 48.2), new(30.4, 51.6)], "#60A5FA", 0.6));
        list.Add(new([new(30.4, 51.6), new(33.6, 51.6), new(32, 43)], "#E0F2FF", 0.9));
        for (var i = 0; i < Outer.Length; i++)
        {
            var j = (i + 1) % Outer.Length;
            list.Add(new([Outer[i], Outer[j], Inner[j], Inner[i]], Shade(Outer[i], Outer[j])));
        }
        return list;
    }

    public const string TileTop = "#0E1B3A", TileBottom = "#050B18", TileStroke = "#29436F", Glow = "#3B82F6";

    /// <summary>Glanzlinie oben auf dem Querbalken.</summary>
    public static readonly (Point A, Point B) Glint = (new(14, 13.9), new(36, 13.9));

    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Das Logo als SVG (Assets/logo/trendo-logo-b.svg).</summary>
    public static string Svg()
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\"><defs>");
        sb.Append($"<linearGradient id=\"kachel\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"{TileTop}\"/><stop offset=\"1\" stop-color=\"{TileBottom}\"/></linearGradient>");
        sb.Append("<linearGradient id=\"tafel\" x1=\"0\" y1=\"0\" x2=\"0.3\" y2=\"1\"><stop offset=\"0\" stop-color=\"#2F6BF0\"/><stop offset=\"1\" stop-color=\"#0E2A78\"/></linearGradient>");
        sb.Append($"<radialGradient id=\"schein\"><stop offset=\"0\" stop-color=\"{Glow}\" stop-opacity=\"0.55\"/><stop offset=\"1\" stop-color=\"{Glow}\" stop-opacity=\"0\"/></radialGradient></defs>\n");
        sb.Append($"<rect x=\"1\" y=\"1\" width=\"62\" height=\"62\" rx=\"15\" fill=\"url(#kachel)\" stroke=\"{TileStroke}\" stroke-width=\"0.75\"/>\n");
        sb.Append("<circle cx=\"32\" cy=\"32\" r=\"28\" fill=\"url(#schein)\"/>\n");
        foreach (var f in Facets())
        {
            sb.Append("<polygon points=\"").Append(string.Join(" ", f.Points.Select(p => F(p.X) + "," + F(p.Y)))).Append("\" fill=\"").Append(f.Fill).Append('"');
            if (f.Opacity < 1) sb.Append(" fill-opacity=\"").Append(F(f.Opacity)).Append('"');
            sb.Append("/>\n");
        }
        sb.Append($"<line x1=\"{F(Glint.A.X)}\" y1=\"{F(Glint.A.Y)}\" x2=\"{F(Glint.B.X)}\" y2=\"{F(Glint.B.Y)}\" stroke=\"#FFFFFF\" stroke-opacity=\"0.85\" stroke-width=\"0.8\" stroke-linecap=\"round\"/>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <summary>Das Logo als Avalonia-Zeichnung (64×64), optional ohne Kachel.</summary>
    public static Canvas Build(bool tile = true)
    {
        var canvas = new Canvas { Width = 64, Height = 64 };
        var tafel = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.3, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#2F6BF0"), 0), new GradientStop(Color.Parse("#0E2A78"), 1) },
        };
        if (tile)
        {
            var bg = new Border
            {
                Width = 62, Height = 62, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(0.75), BorderBrush = Brush.Parse(TileStroke),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.Parse(TileTop), 0), new GradientStop(Color.Parse(TileBottom), 1) },
                },
            };
            Canvas.SetLeft(bg, 1); Canvas.SetTop(bg, 1); canvas.Children.Add(bg);
        }
        var glow = new Ellipse
        {
            Width = 56, Height = 56,
            Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#8C3B82F6"), 0), new GradientStop(Color.Parse("#003B82F6"), 1) } },
        };
        Canvas.SetLeft(glow, 4); Canvas.SetTop(glow, 4); canvas.Children.Add(glow);
        foreach (var f in Facets())
        {
            canvas.Children.Add(new Polygon
            {
                Points = f.Points.ToList(), Opacity = f.Opacity,
                Fill = f.Fill.StartsWith("url(", StringComparison.Ordinal) ? tafel : Brush.Parse(f.Fill),
            });
        }
        canvas.Children.Add(new Line { StartPoint = Glint.A, EndPoint = Glint.B, Stroke = Brushes.White, Opacity = 0.85, StrokeThickness = 0.8, StrokeLineCap = PenLineCap.Round });
        return canvas;
    }
}
