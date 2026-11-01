using System.Xml.Linq;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Schnitt-Editor: Größen ableiten, Stoffbedarf je Größe, Schnittlage als SVG im Maßstab 1:1.
public class MarkerGradeTests
{
    static readonly List<Marker.Piece> Shirt = [new("Vorderteil", 55, 75, 1), new("Rückenteil", 55, 75, 1), new("Ärmel", 40, 25, 2)];

    [Fact(DisplayName = "Schnitt-Editor: Gradierung – je Größe Breite + Umfangsprung/4 und Länge + 1 cm, nie unter 1 cm; Basis bleibt gleich")]
    public void Grade()
    {
        Assert.Equal(Shirt, Marker.Grade(Shirt, 0, 4));
        var xl = Marker.Grade(Shirt, 2, 4);
        Assert.Equal((57, 77), (xl[0].W, xl[0].H)); Assert.Equal(2, xl[2].Qty);
        var tiny = Marker.Grade([new("Kragen", 2, 1.5, 1)], -3, 8, 1);
        Assert.Equal((1, 1), (tiny[0].W, tiny[0].H));
    }

    [Fact(DisplayName = "Schnitt-Editor: Stoffbedarf je Größe steigt mit der Größe; mittlere Größe ist die Basis; zu breites Teil ergibt „unbekannt“")]
    public void Size_run()
    {
        var run = Marker.SizeRun(Shirt, ["S", "M", "L", "XL"], 140, 4);
        Assert.Equal(["S", "M", "L", "XL"], run.Select(r => r.Size));
        var basis = Marker.Length(Shirt, Marker.AutoLayout(Shirt, 140)) / 100;
        Assert.Equal(Math.Round(basis, 2), run[2].Meters);
        Assert.True(run[0].Meters <= run[2].Meters && run[2].Meters <= run[3].Meters);
        Assert.True(double.IsNaN(Marker.SizeRun([new("Bahn", 200, 180, 1)], ["M"], 140, 4)[0].Meters));
    }

    [Fact(DisplayName = "Schnitt-Editor: SVG ist gültiges XML in cm (Breite = Stoffbreite, Höhe = Verbrauch), je Teil Schnittkante, Nahtlinie und Name; Sonderzeichen maskiert")]
    public void Svg()
    {
        var pieces = new List<Marker.Piece>([.. Shirt, new("Tasche <klein> & Co", 15, 15, 1)]);
        var lay = Marker.AutoLayout(pieces, 140);
        var x = XDocument.Parse(Marker.Svg("T-Shirt", pieces, lay, 140));
        XNamespace n = "http://www.w3.org/2000/svg";
        Assert.Equal("140cm", x.Root!.Attribute("width")!.Value);
        Assert.Equal($"{Marker.Length(pieces, lay).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}cm", x.Root.Attribute("height")!.Value);
        var groups = x.Descendants(n + "g").Where(g => g.Attribute("id")?.Value.StartsWith("teil-") == true).ToList();
        Assert.Equal(5, groups.Count);
        Assert.All(groups, g => Assert.Equal(2, g.Elements(n + "rect").Count()));
        Assert.Contains(x.Descendants(n + "text"), t => t.Value == "Tasche <klein> & Co" || t.Value == "Tasche <klein> & Co ↻");
    }
}
