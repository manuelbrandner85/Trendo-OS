using Trendo.Core;
using Xunit;

public class W5_Bus_Market
{
    static readonly List<Competitor> C = [new("A", "", 10, "schnell", "teuer Support langsam", "Hundesitter buchen online in der Stadt"), new("B", "", 30, "gut", "Support langsam, keine App"), new("C", "", 20, "", "keine App")];

    [Fact(DisplayName = "BUS-003: Preisspanne, Median, eigene Position, gemeinsame Schwächen als Marktlücke")]
    public void Prices()
    {
        var p = BusinessMarket.Prices(C, 15)!;
        Assert.Equal((10, 20, 30), (p.Min, p.Median, p.Max)); Assert.Contains("unteren", p.Where);
        Assert.Contains("am teuersten", BusinessMarket.Prices(C, 40)!.Where);
        Assert.Contains(p.CommonWeaknesses, w => w.Word == "support"); Assert.Contains(p.CommonWeaknesses, w => w.Word == "app");
        Assert.Null(BusinessMarket.Prices([], 5));
    }

    [Fact(DisplayName = "BUS-004: Nischen nach Schmerz × Häufigkeit, gleiche Nennungen zusammengefasst")]
    public void Niches()
    {
        var n = BusinessMarket.Niches([new("Pflegekräfte", "Schichtplan ändert sich ständig", 5, 5), new("pflegekräfte", "Schichtplan ändert sich ständig!", 4, 5), new("Eltern", "Kein Babysitter", 3, 2)]);
        Assert.Equal(2, n.Count); Assert.Equal(45, n[0].Score); Assert.Equal(2, n[0].Mentions);
    }

    [Fact(DisplayName = "BUS-006/007/008/009: Varianten, Branchen-Mix, Eigenständigkeit und Testauswertung")]
    public void Rest()
    {
        var i = new BusinessIdea { Name = "Sitterly", Problem = "Hundesitter kurzfristig finden", Solution = "Hundesitter online buchen", Audience = "Hundebesitzer in der Stadt" };
        Assert.Equal(7, BusinessMarket.Variants(i).Count); Assert.All(BusinessMarket.Variants(i), v => Assert.False(string.IsNullOrWhiteSpace(v.Text)));
        Assert.Equal(10, BusinessMarket.Fusions(i).Count); Assert.Contains("Sitterly", BusinessMarket.Fusions(i)[0].Text);
        var o = BusinessMarket.Originality(i, C); Assert.Equal("A", o[0].Name); Assert.True(o[0].Score > o[1].Score);
        Assert.Equal(0, BusinessMarket.TextSimilarity("", "x"));
        Assert.Equal("Noch nicht getestet.", BusinessMarket.Validation([]).Verdict);
        var v = BusinessMarket.Validation([new("a", "x", 3, 5), new("b", "y", 10, 4), new("c", "z", 1, 1)]);
        Assert.Equal((3, 2), (v.Done, v.Confirmed)); Assert.Contains("bestätigt", v.Verdict);
        var root = Path.Combine(Path.GetTempPath(), "trendo-il-" + Guid.NewGuid().ToString("N"));
        var l = new IdeaList<Competitor>(root, "../x", "m"); l.Add(C[0]); Assert.Single(l.All()); Assert.True(Directory.Exists(Path.Combine(root, "business", "x")));
    }
}
