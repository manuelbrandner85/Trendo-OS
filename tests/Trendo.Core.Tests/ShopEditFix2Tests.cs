using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753 Zweitprüfung: CSV-Import respektiert die Varianten-Sperre (M-A), Bon-/Bestellstorno bei gelöschten oder umbenannten Artikeln (M-B, N-c),
/// Hochkomma (N-a), Zahlen (N-b), Meldung beim Hinzufügen von Varianten (N-d).</summary>
public class ShopEditFix2Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753g-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static readonly DateOnly Day = new(2026, 11, 23);

    [Fact(DisplayName = "M-A: Import benennt vorgemerkte Variante um – übersprungen mit Hinweis, Liefern und Stornieren gehen weiter")]
    public void Import_respects_variant_lock()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddCustomer(new BizContact("Erika Muster", "", "", "Kunde", null, 0));
            s.AddProduct(new Product("Shirt", "", 0, 0, 0, 0, 0, "", "", "", [new("M", 20, 5)]));
            var open = s.PlaceOrder("Erika Muster", Day, ("Shirt – M", 2));
            var done = s.PlaceOrder("Erika Muster", Day, ("Shirt – M", 1)); s.Deliver(done.Id, Day);
            var f = Path.Combine(d, "a.csv");
            File.WriteAllText(f, "Title,URL handle,Option1 name,Option1 value,Price,Inventory quantity\nShirt,shirt,Größe,Medium,20.00,4\nKerze,kerze,,,3.00,2\n");
            var r = s.ImportProductsCsv(f);
            Assert.Equal((1, 0), (r.Added, r.Updated));
            Assert.Contains(r.Warnings, w => w.Contains("„Shirt“ nicht aktualisiert") && w.Contains("Shirt – M"));
            Assert.Equal("M", s.Products.All()[0].Variants![0].Name);
            s.Deliver(open.Id, Day);
            s.Cancel(done.Id, Day);
            Assert.Empty(s.LastNotRestocked);
            Assert.Equal(3, s.Products.All()[0].Variants![0].Stock); // 5 − 1 − 2 + 1
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-B: Bon eines gelöschten Artikels lässt sich stornieren – Geld zurück, Bestand sichtbar nicht")]
    public void Cancel_bon_of_deleted_product()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d);
            k.Shop.AddProduct(new Product("Keks", "", 0, 2, 10, 0, 0));
            var bon = k.Checkout([("Keks", 3)], "bar", 10, DateTime.Now, ledger, true);
            k.Shop.RemoveProduct("Keks");
            var c = k.Cancel(bon.Id, DateTime.Now, ledger);
            Assert.True(c.Cancelled);
            Assert.Equal(["Keks"], k.NotRestocked);
            Assert.Equal(0m, ledger.All().Sum(e => e.Amount)); // Einnahme und Storno
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-B: Variante mit bezahlter Lieferung bleibt gesperrt; nach Erstattung geht das Storno mit Rücknahme")]
    public void Paid_delivery_locks_variant()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i"); var ledger = new Ledger(d);
            s.AddCustomer(new BizContact("Erika Muster", "", "", "Kunde", null, 0));
            s.AddProduct(new Product("Shirt", "", 0, 0, 0, 0, 0, "", "", "", [new("M", 20, 5)]));
            var o = s.PlaceOrder("Erika Muster", Day, ("Shirt – M", 2));
            s.Deliver(o.Id, Day); s.Invoice(o.Id, ledger, "x", true); s.Pay(o.Id, 40, Day, "K1", ledger, true);
            Assert.Throws<InvalidOperationException>(() => s.UpdateProduct("Shirt", s.Products.All()[0] with { Variants = [new("Medium", 20, 3)] }));
            s.Refund(o.Id, 40, Day, "E1", ledger, true);
            s.Cancel(o.Id, Day);
            Assert.Equal(5, s.Products.All()[0].Variants![0].Stock);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N-c: Umbenennen lässt alte Bons unverändert, Storno findet den neuen Namen")]
    public void Rename_keeps_bons()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d);
            k.Shop.AddProduct(new Product("Keks", "", 0, 2, 10, 0, 0));
            var bon = k.Checkout([("Keks", 3)], "bar", 10, DateTime.Now, ledger, true);
            k.Shop.UpdateProduct("Keks", k.Shop.Products.All()[0] with { Name = "Butterkeks" });
            k.Shop.UpdateProduct("Butterkeks", k.Shop.Products.All()[0] with { Name = "Dinkelkeks" });
            Assert.Equal("Keks", k.Sales.All()[0].Lines[0].Product); // Beleg bleibt wie ausgegeben
            Assert.Equal("Dinkelkeks", k.Shop.Current("Keks"));
            k.Cancel(bon.Id, DateTime.Now, ledger);
            Assert.Empty(k.NotRestocked);
            Assert.Equal(10, k.Shop.Products.All()[0].Stock);
            // zurück auf den alten Namen: keine Schleife
            k.Shop.UpdateProduct("Dinkelkeks", k.Shop.Products.All()[0] with { Name = "Keks" });
            Assert.Equal("Keks", k.Shop.Current("Keks")); Assert.Equal("Keks", k.Shop.Current("Butterkeks"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N-a: Text mit führendem Hochkomma übersteht Export und Import")]
    public void Apostrophe_roundtrip()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddProduct(new Product("'-Strich' Shirt", "'Laden", 0, 5, 1, 0, 0, "", "'=nicht rechnen"));
            var f = s.ExportProductsCsv(Path.Combine(d, "a.csv"));
            var t = new Shop(d, "j"); t.ImportProductsCsv(f);
            var p = t.Products.All()[0];
            Assert.Equal(("'-Strich' Shirt", "'Laden", "'=nicht rechnen"), (p.Name, p.Supplier, p.Description));
        }
        finally { Directory.Delete(d, true); }
    }

    [Theory(DisplayName = "N-b: Zahlen – deutsche und englische Tausender mit Dezimalteil, Leerzeichen; mehrdeutige werden abgelehnt")]
    [InlineData("1.000,00", 1000.0)]
    [InlineData("1,000.50", 1000.5)]
    [InlineData("1 000", 1000.0)]
    [InlineData("12,50", 12.5)]
    [InlineData("9.99", 9.99)]
    [InlineData("1.234.567,8", 1234567.8)]
    public void Numbers_ok(string text, double value) => Assert.Equal(value, Shop.ParseNum(text, "Preis", 2), 6);

    [Theory(DisplayName = "N-b: „1.000“ und „1,000“ ohne Dezimalteil sind nicht eindeutig, Meldung neutral")]
    [InlineData("1.000")]
    [InlineData("1,000")]
    public void Numbers_ambiguous(string text) => Assert.Contains("ohne Tausenderpunkt, z. B. 1000 oder 12,50", Assert.Throws<ArgumentException>(() => Shop.ParseNum(text, "Preis", 2)).Message);

    [Fact(DisplayName = "N-d: Varianten an verkauftem Artikel – eigene, passende Meldung")]
    public void Add_variants_message()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i");
            k.Shop.AddProduct(new Product("Tasse", "", 0, 8, 5, 0, 0));
            k.Checkout([("Tasse", 1)], "bar", 10, DateTime.Now, new Ledger(d), true);
            var e = Assert.Throws<InvalidOperationException>(() => k.Shop.UpdateProduct("Tasse", k.Shop.Products.All()[0] with { Variants = [new("Weiß", 8, 3)] }));
            Assert.Contains("nicht nachträglich hinzufügen", e.Message);
            Assert.DoesNotContain("umbenennen", e.Message);
        }
        finally { Directory.Delete(d, true); }
    }
}
