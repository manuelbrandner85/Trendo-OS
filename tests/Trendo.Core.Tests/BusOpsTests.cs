using Trendo.Core;
using Xunit;

public class W5_Bus_Ops
{
    [Fact(DisplayName = "BUS-017/018: Monatsumsatz, Rechte, Teams getrennt; IBAN-Prüfung, GiroCode nach EPC, Gebühren")]
    public void Members_Pay()
    {
        var tiers = new[] { new PlanTier("Basis", 9.9), new PlanTier("Pro", 29) };
        var d = new DateOnly(2026, 1, 1);
        var m = new[] { new Member("A", "", "Basis", "T1", "Inhaber", d), new Member("B", "", "Pro", "T1", "Mitglied", d), new Member("C", "", "Pro", "T2", "Mitglied", d, false) };
        Assert.Equal(38.9, BusinessOps.Mrr(m, tiers), 3);
        Assert.True(BusinessOps.CanManage("Verwalter")); Assert.False(BusinessOps.CanManage("Mitglied"));
        Assert.Equal(2, BusinessOps.VisibleTo(m, m[0]).Count); Assert.Single(BusinessOps.VisibleTo(m, m[2]));
        Assert.True(BusinessOps.IbanValid("DE89 3704 0044 0532 0130 00")); Assert.False(BusinessOps.IbanValid("DE89 3704 0044 0532 0130 01"));
        var g = BusinessOps.GiroCode("Max Muster", "DE89370400440532013000", 12.5m, "RE-2026-001").Split('\n');
        Assert.Equal(["BCD", "002", "1", "SCT", "", "Max Muster", "DE89370400440532013000", "EUR12.50", "", "", "RE-2026-001"], g);
        Assert.Throws<ArgumentException>(() => BusinessOps.GiroCode("x", "DE00", 1, ""));
        Assert.Equal(0.85, BusinessOps.Fee(BusinessOps.PayOptions[1], 20));
    }

    [Fact(DisplayName = "BUS-019/020/023: Fällige Kontakte, Angebotswert, Nachbestellen, Anfragen-Fristen und FAQ")]
    public void Crm_Stock_Support()
    {
        var t = new DateOnly(2026, 10, 21);
        var c = new[] { new BizContact("A", "", "", "Angebot", t, 1000), new BizContact("B", "", "", "Interessent", t.AddDays(3), 500), new BizContact("C", "", "", "Verloren", t, 9) };
        Assert.Equal(["A"], BusinessOps.Due(c, t).Select(x => x.Name)); Assert.Equal(550, BusinessOps.PipelineValue(c));
        var p = new Product("Tasse", "L", 3, 12, 20, 10, 2);
        Assert.Equal(24, BusinessOps.ReorderPoint(p)); Assert.True(BusinessOps.NeedsOrder(p)); Assert.Equal(60, BusinessOps.OrderQty(p)); Assert.Equal(0.75, BusinessOps.UnitMargin(p));
        var now = DateTimeOffset.Now;
        Assert.True(BusinessOps.Overdue(new("x", "", "hoch", now.AddHours(-5)), now)); Assert.False(BusinessOps.Overdue(new("x", "", "normal", now.AddHours(-5)), now));
        Assert.False(BusinessOps.Overdue(new("x", "", "hoch", now.AddDays(-5), "gelöst"), now));
        var faq = BusinessOps.Faq([new("Wie storniere ich?", "", "normal", now, "gelöst", "Unter Konto → Stornieren."), new("wie storniere ich?", "", "normal", now, "gelöst", "Im Konto."), new("Offen", "", "hoch", now)]);
        Assert.Contains("## Wie storniere ich?", faq); Assert.DoesNotContain("Offen", faq);
    }

    [Fact(DisplayName = "BUS-024/025: Zahlen-CSV, Trend 7 gegen 7 Tage, Plan gegen echte Buchungen")]
    public void Analytics()
    {
        var csv = string.Join('\n', Enumerable.Range(1, 14).Select(k => $"{k:00}.10.2026;100;{k};{(k > 7 ? 4 : 2)}")) + "\nkaputt;x;y;z";
        var s = BusinessOps.ImportStats(csv); Assert.Equal(14, s.Count);
        var tr = BusinessOps.Analyze(s); Assert.Equal(0.04, tr.SaleRate, 3); Assert.Equal(0.02, tr.PrevSaleRate, 3); Assert.Contains("aufwärts", tr.Text);
        var i = new BusinessIdea { Price = 10, CustomersMonth = 100, FixedCostMonth = 200 };
        var pv = BusinessOps.Compare(i, [new(new(2026, 1, 5), "a", 1000m), new(new(2026, 2, 5), "b", -200m)], new(2026, 1, 1), new(2026, 2, 28));
        Assert.Equal(800, pv.PlanProfitMonth); Assert.Equal(400, pv.ActualProfitMonth); Assert.Contains("Deutlich unter", pv.Text);
    }
}
