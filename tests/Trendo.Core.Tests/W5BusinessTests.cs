using Trendo.Core;
using Xunit;

public class W5_Business
{
    static BusinessIdea Good() => new() { Name = "Hundesitter-App", Problem = "Berufstätige finden kurzfristig keinen Hundesitter.", Audience = "Hundebesitzer in Städten", Solution = "Eine Seite, auf der geprüfte Sitter buchbar sind.",
        ModelId = "marktplatz", Price = 20, VariableCost = 4, FixedCostMonth = 300, CustomersMonth = 60, Competitors = 3, Skill = 4, HoursWeek = 15, Capital = 2500 };

    [Fact(DisplayName = "BUS-005/012: Marge, Gewinn, Gewinnschwelle und Stundenlohn richtig gerechnet")]
    public void Numbers()
    {
        var i = Good();
        Assert.Equal(0.8, BusinessStudio.Margin(i), 3);
        Assert.Equal(16 * 60 - 300, BusinessStudio.ProfitMonth(i));
        Assert.Equal(19, BusinessStudio.BreakEven(i)); // 300/16 = 18,75 → 19
        Assert.Equal(660 / (15 * 4.33), BusinessStudio.HourlyWage(i), 3);
        i.VariableCost = 25; Assert.Null(BusinessStudio.BreakEven(i));
    }

    [Fact(DisplayName = "BUS-010: Gute Idee → Starten; Verlust je Verkauf → Nicht starten; vage Idee → Überarbeiten")]
    public void Decision()
    {
        var v = BusinessStudio.Evaluate(Good());
        Assert.Equal("Starten", v.Decision); Assert.InRange(v.Score, 70, 100); Assert.Equal(100, v.Parts.Sum(p => p.Max));
        var bad = Good(); bad.VariableCost = 30;
        Assert.Equal("Nicht starten", BusinessStudio.Evaluate(bad).Decision);
        Assert.Equal("Überarbeiten", BusinessStudio.Evaluate(new BusinessIdea { Name = "x", HoursWeek = 2 }).Decision);
        var mid = Good(); mid.CustomersMonth = 25; mid.Competitors = 50; mid.Capital = 0;
        Assert.Equal("Erst testen", BusinessStudio.Evaluate(mid).Decision);
    }

    [Fact(DisplayName = "BUS-002/011/014: 10 Modelle, Bauplan enthält Zahlen, Entscheidung und rechtliche Schritte; Ideen speichern")]
    public void Blueprint_and_store()
    {
        Assert.Equal(10, BusinessStudio.Models.Count);
        var md = BusinessStudio.Blueprint(Good());
        Assert.Contains("Gewinnschwelle: 19", md); Assert.Contains("Starten", md); Assert.Contains("Impressum", md); Assert.Contains("keine Rechtsberatung", md);
        var root = Path.Combine(Path.GetTempPath(), "trendo-biz-" + Guid.NewGuid().ToString("N"));
        var s = new IdeaStore(root); var i = Good(); s.Save(i); i.Price = 25; s.Save(i);
        Assert.Single(s.All()); Assert.Equal(25, s.All()[0].Price);
        s.Delete(i.Id); Assert.Empty(s.All());
    }
}
