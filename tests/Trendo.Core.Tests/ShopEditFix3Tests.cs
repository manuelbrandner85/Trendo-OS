using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753 Drittprüfung M-C: Bons und Bestellungen hängen über eine feste Artikel-Kennung an ihrem Artikel – ein später gleichnamiger Artikel
/// bekommt keinen fremden Bestand (Umbenennen, Kette, Löschen, Bestellungen, Altdaten). N-f: negativer Preis aus CSV.</summary>
public class ShopEditFix3Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753h-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static readonly DateOnly Day = new(2026, 11, 23);
    static Product P(string n, int stock) => new(n, "", 0, 2, stock, 0, 0);
    static int Stock(Shop s, string n) => s.Products.All().Single(p => p.Name == n).Stock;

    [Fact(DisplayName = "M-C Fall 1: Keks umbenannt in Cookie, neuer Keks – Storno des neuen Bons trifft den neuen Keks, Sperre gilt für den neuen Keks")]
    public void Rename_then_new_same_name()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d);
            k.Shop.AddProduct(P("Keks", 10));
            var bon1 = k.Checkout([("Keks", 2)], "bar", 10, DateTime.Now, ledger, true);
            k.Shop.UpdateProduct("Keks", k.Shop.Products.All()[0] with { Name = "Cookie" });
            k.Shop.AddProduct(P("Keks", 5));
            var bon2 = k.Checkout([("Keks", 1)], "bar", 10, DateTime.Now, ledger, true);
            Assert.Throws<InvalidOperationException>(() => k.Shop.UpdateProduct("Keks", k.Shop.Products.All().Single(p => p.Name == "Keks") with { Variants = [new("Groß", 2, 4)] }));
            k.Cancel(bon2.Id, DateTime.Now, ledger);
            Assert.Equal((5, 8), (Stock(k.Shop, "Keks"), Stock(k.Shop, "Cookie")));
            k.Cancel(bon1.Id, DateTime.Now, ledger);
            Assert.Equal((5, 10), (Stock(k.Shop, "Keks"), Stock(k.Shop, "Cookie")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-C Fall 2: Kette A→B, neues A, B→X, neues A→B – Storno des alten A-Bons bucht auf X")]
    public void Rename_chain()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d); var s = k.Shop;
            s.AddProduct(P("A", 5));
            var bon = k.Checkout([("A", 1)], "bar", 10, DateTime.Now, ledger, true);
            s.UpdateProduct("A", s.Products.All().Single(p => p.Name == "A") with { Name = "B" });
            s.AddProduct(P("A", 7));
            s.UpdateProduct("B", s.Products.All().Single(p => p.Name == "B") with { Name = "X" });
            s.UpdateProduct("A", s.Products.All().Single(p => p.Name == "A") with { Name = "B" });
            k.Cancel(bon.Id, DateTime.Now, ledger);
            Assert.Empty(k.NotRestocked);
            Assert.Equal((5, 7), (Stock(s, "X"), Stock(s, "B")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-C Fall 3: alter Bon, Keks gelöscht und neu angelegt – Storno meldet „nicht zurückgebucht“, neuer Keks bleibt")]
    public void Delete_then_new_same_name()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d);
            k.Shop.AddProduct(P("Keks", 10));
            var bon = k.Checkout([("Keks", 2)], "bar", 10, DateTime.Now, ledger, true);
            k.Shop.RemoveProduct("Keks");
            k.Shop.AddProduct(P("Keks", 5));
            k.Cancel(bon.Id, DateTime.Now, ledger);
            Assert.Equal(["Keks"], k.NotRestocked);
            Assert.Equal(5, Stock(k.Shop, "Keks"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-C Bestellungen: neuer gleichnamiger Artikel – Storno jeder Bestellung trifft ihren eigenen Artikel")]
    public void Orders_bound_to_item()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddCustomer(new BizContact("Erika Muster", "", "", "Kunde", null, 0));
            s.AddProduct(P("Keks", 10));
            var old = s.PlaceOrder("Erika Muster", Day, ("Keks", 2)); s.Deliver(old.Id, Day);
            s.UpdateProduct("Keks", s.Products.All()[0] with { Name = "Cookie" });
            Assert.Equal("Cookie", s.Orders().Single(o => o.Id == old.Id).Lines[0].Product);
            s.AddProduct(P("Keks", 5));
            var neu = s.PlaceOrder("Erika Muster", Day, ("Keks", 1)); s.Deliver(neu.Id, Day);
            Assert.Equal((4, 8), (Stock(s, "Keks"), Stock(s, "Cookie")));
            s.Cancel(neu.Id, Day);
            Assert.Equal((5, 8), (Stock(s, "Keks"), Stock(s, "Cookie")));
            s.Cancel(old.Id, Day);
            Assert.Equal((5, 10), (Stock(s, "Keks"), Stock(s, "Cookie")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-C Altdaten: Bon und Artikel ohne Kennung bleiben lesbar; Umbenennen bindet den Bon vorher fest, ein neuer Keks bekommt nichts")]
    public void Legacy_files()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d);
            var dir = Path.GetDirectoryName(k.Shop.Products.FilePath)!; Directory.CreateDirectory(dir);
            File.WriteAllText(k.Shop.Products.FilePath, """[{"Name":"Keks","Supplier":"","Cost":0,"Price":2,"Stock":8,"LeadDays":0,"DailySales":0,"Sku":""}]""");
            File.WriteAllText(k.Sales.FilePath, """[{"Id":"b1","No":"BON-1","At":"2026-11-01T10:00:00","Lines":[{"Product":"Keks","Qty":2,"PriceGross":2}],"Payment":"bar","Given":4,"VatRate":0,"Cancelled":false}]""");
            Assert.Equal("", k.Shop.Products.All()[0].Id); Assert.Equal("", k.Sales.All()[0].Lines[0].ItemId);
            k.Shop.UpdateProduct("Keks", k.Shop.Products.All()[0] with { Name = "Cookie" });
            k.Shop.AddProduct(P("Keks", 5));
            Assert.Equal(("Keks", 2m), (k.Sales.All()[0].Lines[0].Product, k.Sales.All()[0].Lines[0].PriceGross)); // Beleg unverändert
            k.Cancel("b1", DateTime.Now, ledger);
            Assert.Equal((5, 10), (Stock(k.Shop, "Keks"), Stock(k.Shop, "Cookie")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N-f/N-e: negativer Preis aus CSV wird nicht übernommen; Ergebnis nennt die Anzahl und alle Hinweise")]
    public void Negative_price_and_all_warnings()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            var f = Path.Combine(d, "a.csv");
            File.WriteAllText(f, "Title,Price\nGut,3.00\n" + string.Concat(Enumerable.Range(1, 8).Select(i => $"Minus{i},-{i}.00\n")));
            var r = s.ImportProductsCsv(f);
            Assert.Equal((1, 8, 8), (r.Added, r.Skipped, r.Warnings.Count));
            Assert.All(s.Products.All(), p => Assert.True(p.Price >= 0));
        }
        finally { Directory.Delete(d, true); }
    }
}
