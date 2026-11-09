using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Print on Demand, Design-Editor (docs/nachbau/kittl/anforderungen.md).</summary>
public class PodDesignTests
{
    static PodItem R(string id, double x = 0, double y = 0, double w = 100, double h = 50, string group = "") => new(id, "rect", x, y, w, h, Group: group);

    [Fact(DisplayName = "K-02: Elemente speichern und laden; unbekannte Art wird abgelehnt")]
    public void Store_load()
    {
        var doc = new JsonObject();
        Assert.Empty(PodDesign.Items(doc));
        var t = PodDesign.New("text", 900, 1200, "Hallo");
        PodDesign.Store(doc, [t, PodDesign.New("ellipse", 900, 1200)]);
        var back = PodDesign.Items(JsonNode.Parse(doc.ToJsonString())!.AsObject());
        Assert.Equal(2, back.Count); Assert.Equal(t, back[0]);
        Assert.Throws<ArgumentException>(() => PodDesign.New("boese", 900, 1200));
    }

    [Fact(DisplayName = "K-05: Reihenfolge einen Schritt und ganz nach vorn oder hinten")]
    public void Arrange()
    {
        PodItem[] l = [R("a"), R("b"), R("c")];
        string O(IEnumerable<PodItem> x) => string.Concat(x.Select(i => i.Id));
        Assert.Equal("bac", O(PodDesign.Arrange(l, new HashSet<string> { "a" }, "up")));
        Assert.Equal("bca", O(PodDesign.Arrange(l, new HashSet<string> { "a" }, "front")));
        Assert.Equal("acb", O(PodDesign.Arrange(l, new HashSet<string> { "c" }, "down")));
        Assert.Equal("cab", O(PodDesign.Arrange(l, new HashSet<string> { "c" }, "back")));
        Assert.Equal("abc", O(PodDesign.Arrange(l, new HashSet<string> { "c" }, "up")));
    }

    [Fact(DisplayName = "K-06: Gruppe geht gemeinsam, eine Auswahl genügt nicht zum Gruppieren, Kopien bekommen eine eigene Gruppe")]
    public void Groups()
    {
        PodItem[] l = [R("a"), R("b"), R("c")];
        var g = PodDesign.Group(l, new HashSet<string> { "a", "b" });
        Assert.Equal(new HashSet<string> { "a", "b" }, PodDesign.WithGroups(g, ["a"]));
        Assert.All(PodDesign.Group(l, new HashSet<string> { "a" }), i => Assert.Equal("", i.Group));
        var copy = PodDesign.Duplicates(g.Take(2));
        Assert.Equal(copy[0].Group, copy[1].Group); Assert.NotEqual(g[0].Group, copy[0].Group); Assert.NotEqual(g[0].Id, copy[0].Id);
        Assert.All(PodDesign.Ungroup(g, new HashSet<string> { "a", "b" }), i => Assert.Equal("", i.Group));
    }

    [Fact(DisplayName = "K-04: Einrasten an Mitte und Kanten der Fläche und anderer Elemente, außerhalb der Toleranz nicht")]
    public void Snap()
    {
        Assert.Equal((0d, 0d), PodDesign.Snap(300, 300, 100, 100, [], 900, 1200, 2));
        Assert.Equal(3d, PodDesign.Snap(397, 300, 100, 100, [], 900, 1200, 8).Dx, 3); // Mitte 447 → 450
        var (dx, _) = PodDesign.Snap(205, 700, 50, 50, [R("o", 100, 0, 100, 50)], 900, 1200, 8);
        Assert.Equal(-5, dx, 3);
        Assert.Equal((0d, 0d), PodDesign.Snap(205, 700, 50, 50, [R("o", 100, 0, 100, 50) with { Hidden = true }], 900, 1200, 2));
    }

    [Fact(DisplayName = "K-03: Treffer berücksichtigt die Drehung")]
    public void Hit()
    {
        var r = new PodItem("a", "rect", 0, 40, 100, 20, Rot: 90); // Mitte 50/50, gedreht: senkrechter Balken
        Assert.True(PodDesign.Hit(r, 50, 5)); Assert.False(PodDesign.Hit(r, 5, 50));
    }

    [Fact(DisplayName = "K-11: Palette nimmt nur gültige Farben, ohne Doppelte")]
    public void Palette()
    {
        var p = PodDesign.AddColor(["#112233"], "#aabbcc");
        Assert.Equal(["#112233", "#AABBCC"], PodDesign.AddColor(p, "#AABBCC"));
        Assert.Throws<ArgumentException>(() => PodDesign.AddColor(p, "rot"));
    }

    [Fact(DisplayName = "K-13: SVG hat Druckmaße, schützt Sonderzeichen und lässt Ausgeblendetes weg")]
    public void Svg()
    {
        var s = PodDesign.Svg([new PodItem("t", "text", 10, 20, 100, 50, Text: "A<B&C", Size: 40), R("h") with { Hidden = true }, new PodItem("r", "rect", 0, 0, 10, 10, Rot: 45)], 900, 1200);
        Assert.Contains("width=\"30cm\"", s); Assert.Contains("A&lt;B&amp;C", s); Assert.Contains("rotate(45", s);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(s, "<rect"));
    }

    [Fact(DisplayName = "K-14: Kittl wird nur auf Wunsch über den offiziellen Zugang eingetragen, ohne Zugangsdaten; andere Studios bleiben unberührt")]
    public void Kittl()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-kittl").FullName;
        try
        {
            var user = new JsonObject();
            Assert.Empty(StudioMcp.EnsureProjectConfig(dir, "STUDIO-POD-01", userConfig: user).Added);
            var r = StudioMcp.EnsureProjectConfig(dir, "STUDIO-POD-01", userConfig: user, kittl: true);
            Assert.Equal(["kittl"], r.Added);
            var s = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, StudioMcp.FileName)))!["mcpServers"]!["trendo-kittl"]!;
            Assert.Equal("http", (string?)s["type"]); Assert.Equal("https://mcp.kittl.com/mcp", (string?)s["url"]);
            Assert.DoesNotContain("token", s.ToJsonString(), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(StudioMcp.EnsureProjectConfig(dir, "CORE-05", userConfig: user, kittl: false).Added);
            Assert.DoesNotContain("kittl", File.ReadAllText(Path.Combine(dir, StudioMcp.FileName)));
        }
        finally { Directory.Delete(dir, true); }
    }
}
