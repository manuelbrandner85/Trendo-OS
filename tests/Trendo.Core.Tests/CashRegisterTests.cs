using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0304: Verkaufen als Kasse – Kassieren mit Rückgeld, Bestand, Buchung in „Geld“, Storno, Tagesabschluss, Bon und Abschluss als PDF.
public class CashRegisterTests
{
    static readonly DateTime Now = new(2026, 11, 1, 10, 15, 0);

    static (CashRegister K, Ledger L, string Root) Setup(Fixture f)
    {
        var k = new CashRegister(f.Core.DataRoot, "laden");
        k.Shop.Products.Add(new Product("Kaffee", "", 0, 10.0, 5, 0, 0)); k.Shop.Products.Add(new Product("Tee", "", 0, 4.0, 1, 0, 0));
        return (k, new Ledger(f.Core.DataRoot), f.Core.DataRoot);
    }

    [Fact(DisplayName = "T-0304: Kassieren bar – Bruttopreise, Rückgeld, fortlaufende Bon-Nummer, Bestand sinkt mit Lagerbewegung, Einnahme mit MwSt in „Geld“")]
    public void Checkout()
    {
        using var f = new Fixture(); var (k, l, _) = Setup(f);
        var s = k.Checkout([("Kaffee", 1), ("Tee", 1), ("Kaffee", 1)], "bar", 30, Now, l, smallBusiness: false);
        Assert.Equal("BON-20261101-0001", s.No); Assert.Equal(2, s.Lines.Count);
        Assert.Equal(28.56m, s.Gross); Assert.Equal(1.44m, s.Change); Assert.Equal(4.56m, s.Vat);
        Assert.Equal(3, k.Shop.Products.All().Single(p => p.Name == "Kaffee").Stock); Assert.Equal(0, k.Shop.Products.All().Single(p => p.Name == "Tee").Stock);
        Assert.Equal(2, k.Shop.Moves.All().Count(m => m.Kind == "Kasse"));
        var e = Assert.Single(l.All()); Assert.Equal(28.56m, e.Amount); Assert.Equal("Einnahmen", e.Category); Assert.Equal(19m, e.VatRate);
        Assert.Equal("BON-20261101-0002", k.Checkout([("Kaffee", 1)], "karte", 0, Now.AddMinutes(3), l, false).No);
    }

    [Fact(DisplayName = "T-0304: Ablehnen – nicht vorrätig, zu wenig Geld, leerer Korb, unbekannter Artikel; Kleinunternehmer ohne MwSt")]
    public void Rules()
    {
        using var f = new Fixture(); var (k, l, _) = Setup(f);
        Assert.Throws<InvalidOperationException>(() => k.Checkout([("Tee", 2)], "bar", 100, Now, l, false));
        Assert.Throws<ArgumentException>(() => k.Checkout([("Kaffee", 1)], "bar", 5, Now, l, false));
        Assert.Throws<ArgumentException>(() => k.Checkout([], "bar", 5, Now, l, false));
        Assert.Throws<ArgumentException>(() => k.Checkout([("Kuchen", 1)], "karte", 0, Now, l, false));
        Assert.Empty(l.All()); Assert.Equal(5, k.Shop.Products.All().Single(p => p.Name == "Kaffee").Stock);
        var s = k.Checkout([("Kaffee", 2)], "karte", 0, Now, l, smallBusiness: true);
        Assert.Equal(20m, s.Gross); Assert.Equal(0m, s.Vat); Assert.Equal(0m, s.Change); Assert.Equal(0m, l.All().Single().VatRate);
    }

    [Fact(DisplayName = "T-0304: Storno bucht Bestand und Geld zurück (nur einmal); Tagesabschluss zählt ohne Stornos; Bon und Abschluss als gültige PDF")]
    public void Cancel_day_pdf()
    {
        using var f = new Fixture(); var (k, l, root) = Setup(f);
        var a = k.Checkout([("Kaffee", 2)], "bar", 50, Now, l, false); var b = k.Checkout([("Tee", 1)], "karte", 0, Now.AddMinutes(5), l, false);
        k.Cancel(a.Id, Now.AddMinutes(10), l);
        Assert.Equal(5, k.Shop.Products.All().Single(p => p.Name == "Kaffee").Stock);
        Assert.Equal(b.Gross, FinanceExtra.Balance(l.All()));
        Assert.Throws<InvalidOperationException>(() => k.Cancel(a.Id, Now, l));
        var d = k.Day(DateOnly.FromDateTime(Now));
        Assert.Equal(1, d.Count); Assert.Equal(1, d.Cancelled); Assert.Equal(4.76m, d.Gross); Assert.Equal(0m, d.Cash); Assert.Equal(4.76m, d.Card);
        Assert.Equal([("Tee", 1m, 4.76m)], d.ByProduct);
        var bon = Path.Combine(root, "bon.pdf"); CashRegister.ReceiptPdf(bon, b, "Laden Erika Muster\nHauptstraße 1");
        var day = Path.Combine(root, "tag.pdf"); CashRegister.DayPdf(day, d, k.Of(DateOnly.FromDateTime(Now)), "Laden Erika Muster");
        foreach (var p in new[] { bon, day }) { Assert.True(new FileInfo(p).Length > 800); Assert.StartsWith("%PDF", File.ReadAllText(p)[..4]); Assert.Equal(1, Trendo.Core.Sys.PdfTools.Read(p).Pages); }
    }
}
