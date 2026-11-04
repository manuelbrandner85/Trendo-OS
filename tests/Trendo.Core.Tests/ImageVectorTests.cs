using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Selbst nachgebaut statt Inkscape/Krita (04.11.2026): Vektorpfade, Glättung, SVG schreiben und lesen.</summary>
public class ImageVectorTests
{
    [Fact(DisplayName = "Vektorpfad: Pinselstrich wird vereinfacht, geglättet (Bézier), verschiebbar; Pfaddaten sind gültig")]
    public void Path_layer()
    {
        var pts = Enumerable.Range(0, 50).Select(i => (X: i * 4.0, Y: 100 + Math.Sin(i / 5.0) * 20)).ToList();
        var simple = ImageVector.Simplify(pts, 1.5);
        Assert.InRange(simple.Count, 5, 30);
        Assert.Equal(pts[0], simple[0]); Assert.Equal(pts[^1], simple[^1]);
        var d = ImageDoc.New(400, 300);
        var l = ImageVector.AddPath(d, simple, "#E0475B", 6);
        Assert.Equal("path", ImageDoc.S(l["kind"]));
        var pd = ImageVector.PathData(ImageVector.Points(l), false, true); Assert.True(pd.StartsWith("M0 100 C"), pd[..Math.Min(60, pd.Length)]);
        ImageDoc.Move(l, 10, 5);
        Assert.Equal((10.0, 105.0), ImageVector.Points(l)[0]);
        Assert.EndsWith("Z", ImageVector.PathData([(0, 0), (10, 0), (10, 10)], true, false));
        Assert.Throws<ArgumentException>(() => ImageVector.AddPath(d, [(1, 1)], "#000", 1));
    }

    [Fact(DisplayName = "SVG: Rechteck, Ellipse, Text und Pfad werden geschrieben und wieder als Ebenen gelesen; Mischmodus steht im SVG")]
    public void Svg_roundtrip()
    {
        var d = ImageDoc.New(400, 300, "#FFFFFF");
        ImageDoc.AddShape(d, "rect", 10, 10, 100, 50, "#2F86F6");
        ImageDoc.AddShape(d, "ellipse", 150, 20, 80, 80, "#22C55E");
        ImageDoc.AddText(d, "Hallo", 20, 120, 32, "#000000");
        var p = ImageVector.AddPath(d, [(10, 250), (100, 200), (200, 260)], "#E0475B", 4);
        p["blend"] = "multiply";
        var svg = ImageVector.ToSvg(d);
        Assert.Contains("<rect x=\"10\"", svg); Assert.Contains("<ellipse", svg); Assert.Contains(">Hallo<", svg); Assert.Contains("mix-blend-mode:multiply", svg);
        var (back, skipped) = ImageVector.FromSvg(svg);
        Assert.Equal(0, skipped);
        var kinds = ImageDoc.Layers(back).Select(x => ImageDoc.S(x!["kind"])).ToList();
        Assert.Equal(["shape", "shape", "text", "path"], kinds);
        Assert.Equal("#FFFFFF", ImageDoc.S(back["background"])); // volle Fläche wird zum Hintergrund
    }

    [Fact(DisplayName = "SVG-Pfad lesen: M/L/H/V/C/Q/Z, groß und klein; Polygon und Linie")]
    public void Parse_path()
    {
        var (pts, closed) = ImageVector.ParsePath("M10 10 h20 v20 L5 30 c 5 5 10 5 15 0 Z");
        Assert.True(closed);
        Assert.Equal((30.0, 10.0), pts[1]); Assert.Equal((30.0, 30.0), pts[2]); Assert.Equal((5.0, 30.0), pts[3]);
        Assert.Equal((20.0, 30.0), pts[^1]);
        var (doc, skipped) = ImageVector.FromSvg("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><polygon points="0,0 50,0 50,50" fill="red"/><line x1="0" y1="0" x2="10" y2="10" stroke="blue"/><foo/></svg>""");
        Assert.Equal(2, ImageDoc.Layers(doc).Count); Assert.Equal(1, skipped);
        Assert.Equal(100, ImageDoc.N(doc["width"]));
    }
}
