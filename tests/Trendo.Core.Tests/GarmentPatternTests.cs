using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>SPEC-08: Grundschnitte aus Körpermaßen.</summary>
public class GarmentPatternTests
{
    static readonly BodyMeasures M = new(168, 92, 74, 98, true);

    static double Dist(PatPt a, PatPt b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    [Theory(DisplayName = "Grundschnitt: Kanten schließen lückenlos, jedes Teil hat Fläche")]
    [InlineData("tshirt")]
    [InlineData("rock")]
    public void Closed(string kind)
    {
        var g = GarmentBlocks.Build(kind, M);
        foreach (var p in g.Parts)
        {
            Assert.True(p.Area > 100, p.Name);
            for (var i = 0; i < p.Edges.Count; i++) Assert.True(Dist(p.Edges[i].Points[^1], p.Edges[(i + 1) % p.Edges.Count].Points[0]) < 1e-6, $"{p.Name}: {p.Edges[i].Name}");
        }
    }

    [Theory(DisplayName = "Grundschnitt: Jede Naht verbindet vorhandene Kanten, zusammengenähte Kanten sind passend lang")]
    [InlineData("tshirt")]
    [InlineData("rock")]
    public void Seams_fit(string kind)
    {
        var g = GarmentBlocks.Build(kind, M);
        foreach (var s in g.Seams)
        {
            var a = g.Parts.First(p => p.Name == s.PartA).Edge(s.EdgeA).Length; var b = g.Parts.First(p => p.Name == s.PartB).Edge(s.EdgeB).Length;
            Assert.InRange(a / b, 0.93, 1.07);
        }
    }

    [Fact(DisplayName = "T-Shirt: Brustweite ist Körpermaß plus 8 cm Bewegungsweite; Vorderteil tiefer ausgeschnitten als Rücken; Größen wachsen mit")]
    public void Tshirt_measures()
    {
        var g = GarmentBlocks.Build("tshirt", M);
        var front = g.Parts[0]; var back = g.Parts[1];
        var width = front.Edge("hem").Length + back.Edge("hem").Length;
        Assert.Equal(M.Chest + 8, width, 3);
        Assert.True(front.Edge("neck").Length > back.Edge("neck").Length);
        var big = GarmentBlocks.Build("tshirt", M with { Chest = 110 });
        Assert.True(big.Parts[0].Area > front.Area);
    }

    [Fact(DisplayName = "T-Shirt: Armkugel ist etwas länger als das Armloch")]
    public void Sleeve_ease()
    {
        var g = GarmentBlocks.Build("tshirt", M);
        var arm = g.Parts[0].Edge("armR").Length / 2 + g.Parts[1].Edge("armR").Length / 2;
        var cap = g.Parts[2].Edge("capF").Length;
        Assert.InRange(cap / arm, 1.0, 1.05);
    }

    [Fact(DisplayName = "Rock: Bund passt zur Taille plus Mehrweite, Saum weiter als Hüfte")]
    public void Skirt_measures()
    {
        var g = GarmentBlocks.Build("rock", M);
        var waist = g.Parts.Sum(p => p.Edge("waist").Length); var hem = g.Parts.Sum(p => p.Edge("hem").Length);
        Assert.Equal(M.Waist + 2, waist, 3); Assert.True(hem > M.Hip);
    }

    [Theory(DisplayName = "Grundschnitt: Unsinnige Maße und unbekannte Schnitte werden abgelehnt")]
    [InlineData("tshirt", 10)]
    [InlineData("gibtsnicht", 92)]
    public void Rejects(string kind, double chest) => Assert.Throws<ArgumentException>(() => GarmentBlocks.Build(kind, M with { Chest = chest }));

    [Fact(DisplayName = "Grundschnitt: Datensatz für Blender enthält Teile, Kanten und Nähte")]
    public void Json()
    {
        var j = GarmentBlocks.ToJson(GarmentBlocks.Build("tshirt", M), M);
        Assert.Equal(4, j["parts"]!.AsArray().Count); Assert.Equal(10, j["seams"]!.AsArray().Count); Assert.Equal(92d, (double)j["body"]!["chest"]!);
    }
}
