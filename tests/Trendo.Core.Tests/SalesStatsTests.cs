using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Verkaufen – Vorbild-Funktionen (SumUp, Square, Shopify POS; 04.11.2026): Rabatt, Artikelnummer/EAN, Auswertung.</summary>
public class SalesStatsTests
{
    static (CashRegister K, Ledger L) Setup(Fixture f)
    {
        var k = new CashRegister(f.Core.DataRoot, "laden");
        k.Shop.Products.Add(new Product("Kaffee", "", 0, 10.0, 50, 0, 0, "4006381333931")); k.Shop.Products.Add(new Product("Tee", "", 0, 4.0, 50, 0, 0, "TEE-1"));
        return (k, new Ledger(f.Core.DataRoot));
    }

    [Fact(DisplayName = "STUDIO-ONLINE-BUSINESS-01: Rabatt: 10 % oder 5 € auf den Bon, nie mehr als die Summe; bucht weniger Einnahme, kein Lagerposten; Storno bucht genau zurück; falsche Angaben werden abgelehnt")]
    public void Discount()
    {
        using var f = new Fixture(); var (k, l) = Setup(f);
        var s = k.Checkout([("Kaffee", 2)], "bar", 50, new DateTime(2026, 11, 2, 10, 0, 0), l, smallBusiness: true, discountPercent: 10);
        Assert.Equal(18m, s.Gross); Assert.Equal(32m, s.Change); Assert.Equal("Rabatt 10 %", s.Lines[^1].Product); Assert.True(s.Lines[^1].IsDiscount);
        Assert.Equal(18m, l.All().Single().Amount);
        Assert.Single(k.Shop.Moves.All()); // nur der Kaffee, nicht der Rabatt
        var e = k.Checkout([("Tee", 1)], "karte", 0, new DateTime(2026, 11, 2, 11, 0, 0), l, true, discountEuro: 99);
        Assert.Equal(0m, e.Gross); // höchstens die Summe
        k.Cancel(s.Id, new DateTime(2026, 11, 2, 12, 0, 0), l);
        Assert.Equal(0m, l.All().Sum(x => x.Amount)); Assert.Equal(50, k.Shop.Products.All().Single(p => p.Name == "Kaffee").Stock); // Bestand zurück
        Assert.Throws<ArgumentException>(() => CashRegister.DiscountLine(10, 120, 0));
        Assert.Throws<ArgumentException>(() => CashRegister.DiscountLine(10, 5, 1));
        Assert.Null(CashRegister.DiscountLine(10, 0, 0));
        Assert.DoesNotContain(k.Day(new DateOnly(2026, 11, 2)).ByProduct, x => x.Product.StartsWith("Rabatt"));
    }

    [Fact(DisplayName = "STUDIO-ONLINE-BUSINESS-01: Artikelnummer/EAN: Prüfziffer EAN-13 und EAN-8, falscher Strichcode abgelehnt, freie Nummern erlaubt; Scanner findet zuerst die Nummer, dann den Namen")]
    public void Sku()
    {
        Assert.True(SalesStats.EanValid("4006381333931")); Assert.True(SalesStats.EanValid("96385074"));
        Assert.False(SalesStats.EanValid("4006381333932")); Assert.False(SalesStats.EanValid("40063813339")); Assert.False(SalesStats.EanValid("ABC"));
        Assert.Equal("TEE-1", SalesStats.CheckSku(" TEE-1 ")); Assert.Equal("", SalesStats.CheckSku(""));
        Assert.Contains("Prüfziffer", Assert.Throws<ArgumentException>(() => SalesStats.CheckSku("4006381333932")).Message);
        Assert.Throws<ArgumentException>(() => SalesStats.CheckSku("mit Leerzeichen"));
        Product[] p = [new("Kaffee", "", 0, 1, 1, 0, 0, "4006381333931"), new("Tee", "", 0, 1, 1, 0, 0, "tee-1")];
        Assert.Equal("Kaffee", SalesStats.Find(p, "4006381333931")!.Name); Assert.Equal("Tee", SalesStats.Find(p, "TEE-1")!.Name);
        Assert.Equal("Tee", SalesStats.Find(p, "tee")!.Name); Assert.Null(SalesStats.Find(p, "Kak")); Assert.Null(SalesStats.Find(p, " "));
    }

    [Fact(DisplayName = "STUDIO-ONLINE-BUSINESS-01: Auswertung: Umsatz, Bons, Durchschnittsbon, Rabatte, Bestseller, Wochentage, Stoßzeit; Stornos zählen nicht; Vergleich zum Zeitraum davor")]
    public void Report()
    {
        using var f = new Fixture(); var (k, l) = Setup(f);
        k.Checkout([("Kaffee", 3)], "karte", 0, new DateTime(2026, 10, 27, 9, 0, 0), l, true); // Woche davor
        k.Checkout([("Kaffee", 1), ("Tee", 2)], "karte", 0, new DateTime(2026, 11, 2, 10, 30, 0), l, true); // Montag, 18 €
        k.Checkout([("Kaffee", 2)], "karte", 0, new DateTime(2026, 11, 2, 10, 45, 0), l, true, discountEuro: 2); // Montag, 18 €
        k.Checkout([("Tee", 1)], "bar", 10, new DateTime(2026, 11, 4, 15, 0, 0), l, true); // Mittwoch, 4 €
        var x = k.Checkout([("Kaffee", 5)], "karte", 0, new DateTime(2026, 11, 4, 16, 0, 0), l, true); k.Cancel(x.Id, new DateTime(2026, 11, 4, 16, 5, 0), l);
        var r = SalesStats.Of(k.Sales.All(), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7));
        Assert.Equal((3, 40m, 13.33m, 2m, 1), (r.Bons, r.Gross, r.Average, r.Discounts, r.Cancelled));
        Assert.Equal(("Kaffee", 3m, 30m), r.Top[0]); Assert.Equal(("Tee", 3m, 12m), r.Top[1]);
        Assert.Equal(36m, r.ByWeekday[0]); Assert.Equal(4m, r.ByWeekday[2]); Assert.Equal(10, r.PeakHour);
        Assert.Equal(30m, r.PreviousGross); Assert.Equal(33.3, r.Change);
        Assert.Equal("Mo", SalesStats.Weekdays[0]); Assert.Equal("So", SalesStats.Weekdays[6]);
        var empty = SalesStats.Of([], new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7));
        Assert.Equal((0, 0m, -1), (empty.Bons, empty.Average, empty.PeakHour)); Assert.Null(empty.Change);
    }
}
