using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>T-0207: zeigt die einfachen SVG-Grafiken der Fach-Studios (Grundriss, Strecke, Diagramme, Balkenbild, G-Code-Vorschau) direkt im Studio.
/// Unterstützt rect, line, polyline, polygon, path, circle und text – mehr erzeugen Trendos eigene Generatoren nicht.</summary>
public static class SvgLite
{
    static double D(XElement e, string a, double d = 0) =>
        double.TryParse(((string?)e.Attribute(a))?.Replace("px", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;

    static IBrush? Paint(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "none") return null;
        try { return Brush.Parse(s); } catch (FormatException) { return null; }
    }

    public static Control Render(string svg, double maxWidth = 640)
    {
        var root = XElement.Parse(svg);
        var vb = ((string?)root.Attribute("viewBox"))?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        double w = vb is { Length: 4 } ? vb[2] : D(root, "width", 600), h = vb is { Length: 4 } ? vb[3] : D(root, "height", 400);
        double ox = vb is { Length: 4 } ? vb[0] : 0, oy = vb is { Length: 4 } ? vb[1] : 0;
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        foreach (var e in root.Descendants())
        {
            var fill = Paint((string?)e.Attribute("fill") ?? (e.Name.LocalName is "text" ? "#000" : e.Name.LocalName is "line" or "polyline" ? "none" : "#000"));
            var stroke = Paint((string?)e.Attribute("stroke"));
            var sw = D(e, "stroke-width", 1);
            Control? c = e.Name.LocalName switch
            {
                "rect" => Place(new Rectangle { Width = D(e, "width"), Height = D(e, "height"), Fill = fill, Stroke = stroke, StrokeThickness = sw }, D(e, "x") - ox, D(e, "y") - oy),
                "circle" => Place(new Ellipse { Width = 2 * D(e, "r"), Height = 2 * D(e, "r"), Fill = fill, Stroke = stroke, StrokeThickness = sw }, D(e, "cx") - D(e, "r") - ox, D(e, "cy") - D(e, "r") - oy),
                "line" => new Line { StartPoint = new Point(D(e, "x1") - ox, D(e, "y1") - oy), EndPoint = new Point(D(e, "x2") - ox, D(e, "y2") - oy), Stroke = stroke ?? Brushes.Black, StrokeThickness = sw },
                "polyline" or "polygon" => new Polyline { Points = Pts((string?)e.Attribute("points"), ox, oy, e.Name.LocalName == "polygon"), Stroke = stroke, StrokeThickness = sw, Fill = e.Name.LocalName == "polygon" ? fill : null },
                "path" => new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse((string?)e.Attribute("d") ?? ""), Fill = fill, Stroke = stroke, StrokeThickness = sw, RenderTransform = new TranslateTransform(-ox, -oy) },
                "text" => Text(e, fill, ox, oy),
                _ => null,
            };
            if (c is not null) canvas.Children.Add(c);
        }
        return new Viewbox { Child = canvas, MaxWidth = maxWidth, Stretch = Stretch.Uniform, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    }

    static Control Place(Control c, double x, double y) { Canvas.SetLeft(c, x); Canvas.SetTop(c, y); return c; }

    static List<Point> Pts(string? s, double ox, double oy, bool close)
    {
        var n = (s ?? "").Split([' ', ',', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        var list = new List<Point>();
        for (var i = 0; i + 1 < n.Length; i += 2) list.Add(new Point(n[i] - ox, n[i + 1] - oy));
        if (close && list.Count > 0) list.Add(list[0]);
        return list;
    }

    static Control Text(XElement e, IBrush? fill, double ox, double oy)
    {
        var size = D(e, "font-size", 12);
        var tb = new TextBlock { Text = e.Value, FontSize = size, Foreground = fill ?? Brushes.Black };
        tb.Measure(Size.Infinity);
        var x = D(e, "x") - ox; var anchor = (string?)e.Attribute("text-anchor");
        if (anchor == "middle") x -= tb.DesiredSize.Width / 2; else if (anchor == "end") x -= tb.DesiredSize.Width;
        return Place(tb, x, D(e, "y") - oy - size * 0.85);
    }
}
