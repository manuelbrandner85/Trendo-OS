using Trendo.Core;
using Xunit;

public class W5_Spec11_Finance
{
    const string Bank = "Kontoinhaber;Max\n\nBuchungstag;Valuta;Auftraggeber/Empfänger;Verwendungszweck;Betrag\n01.03.2026;01.03.2026;Kunde A;\"Rechnung RE-1; danke\";1.190,00\n05.03.2026;05.03.2026;Adobe;Adobe Creative Cloud;-59,49\nkaputt;x;y;z;1\n02.04.2026;02.04.2026;Bank;Kontoführung;-5,00\n";

    [Fact(DisplayName = "SPEC-11: Bank-CSV (deutsches Format, Anführungszeichen) wird gelesen, Kategorien geraten")]
    public void Import()
    {
        var e = Finance.ImportCsv(Bank);
        Assert.Equal(3, e.Count);
        Assert.Equal(1190m, e[0].Amount); Assert.Equal("Rechnung RE-1; danke", e[0].Text); Assert.Equal("Einnahmen", e[0].Category);
        Assert.Equal(-59.49m, e[1].Amount); Assert.Equal("Software", e[1].Category); Assert.Equal("Gebühren", e[2].Category);
        Assert.Equal(1234.5m, Finance.ParseAmount("1,234.50")); Assert.Equal(-3m, Finance.ParseAmount("-3 €"));
        Assert.Throws<InvalidDataException>(() => Finance.ImportCsv("a;b\n1;2"));
    }

    [Fact(DisplayName = "SPEC-11: Netto/MwSt, Monats- und Jahresübersicht stimmen; Export-Import ohne Verlust; doppelte übersprungen")]
    public void Sums()
    {
        var e = new List<Entry> { new(new(2026, 3, 1), "A", 119m, "Einnahmen", 19m), new(new(2026, 3, 9), "B", -10.70m, "Material", 7m), new(new(2026, 4, 1), "C", -5m, "Gebühren"), new(new(2025, 12, 1), "alt", 50m) };
        Assert.Equal(100m, e[0].Net); Assert.Equal(19m, e[0].Vat); Assert.Equal(-10m, e[1].Net);
        var m = Finance.Months(e); Assert.Equal(3, m.Count); Assert.Equal(108.30m, m[1].Result);
        var y = Finance.Year(e, 2026);
        Assert.Equal(100m, y.IncomeNet); Assert.Equal(-15m, y.ExpenseNet); Assert.Equal(19m, y.VatIn); Assert.Equal(-0.70m, y.VatOut);
        var back = Finance.ImportCsv(Finance.ExportCsv(e));
        Assert.Equal(e.Sum(x => x.Amount), back.Sum(x => x.Amount));
        var l = new Ledger(Path.Combine(Path.GetTempPath(), "trendo-fin-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(4, l.Import(e)); Assert.Equal(0, l.Import(e)); Assert.Equal(4, l.All().Count);
    }

    [Fact(DisplayName = "SPEC-11: Rechnung – fortlaufende Nummer, Summen, Kleinunternehmer-Hinweis, keine doppelte Nummer")]
    public void Invoices()
    {
        var d = new DateOnly(2026, 10, 21);
        Assert.Equal("RE-2026-001", Finance.NextInvoiceNumber([], d));
        Assert.Equal("RE-2026-013", Finance.NextInvoiceNumber(["RE-2026-012", "RE-2025-099", "RE-2026-003"], d));
        var i = new Invoice("RE-2026-001", d, "Ich", "<Kunde>", [new("Webseite", 2, 150.255m), new("Pflege", 1, 49.99m)], 19m, false);
        var (net, vat, gross) = Finance.Totals(i);
        Assert.Equal(350.50m, net); Assert.Equal(66.60m, vat); Assert.Equal(417.10m, gross);
        var h = Finance.InvoiceHtml(i); Assert.Contains("&lt;Kunde&gt;", h); Assert.Contains("417,10 €", h); Assert.DoesNotContain("§ 19", h);
        Assert.Contains("§ 19 UStG", Finance.InvoiceHtml(i with { SmallBusiness = true }));
        Assert.Equal(350.50m, Finance.Totals(i with { SmallBusiness = true }).Gross);
        var l = new Ledger(Path.Combine(Path.GetTempPath(), "trendo-inv-" + Guid.NewGuid().ToString("N")));
        l.WriteInvoice(i); Assert.Throws<IOException>(() => l.WriteInvoice(i)); Assert.Equal(["RE-2026-001"], l.InvoiceNumbers());
    }
}
