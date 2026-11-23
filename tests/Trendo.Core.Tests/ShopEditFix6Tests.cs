using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753 sechste Prüfung: M-G (Rückgängig nach einem Storno stellt keinen falschen Bestand her), Import zählt „unverändert“.</summary>
public class ShopEditFix6Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753s-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static readonly DateOnly Day = new(2026, 11, 23);

    [Fact(DisplayName = "M-G Fall 1: Bon verkauft, Artikel gelöscht, Bon storniert – Rückgängig des Löschens wird abgelehnt statt Bestand 3 herzustellen")]
    public void Undo_after_bon_storno()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d);
            k.Shop.AddProduct(new Product("X", "", 0, 2, 5, 0, 0));
            var bon = k.Checkout([("X", 2)], "bar", 10, DateTime.Now, ledger, true);
            k.Shop.RemoveProduct("X");
            k.Cancel(bon.Id, DateTime.Now, ledger);
            Assert.Equal(["X"], k.NotRestocked);
            Assert.Contains("verkauft", Assert.Throws<InvalidOperationException>(() => k.Shop.Undo()).Message);
            Assert.Empty(k.Shop.Products.All()); // nichts mit falschem Bestand zurückgeholt
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-G Fall 2: gelieferte Bestellung, Artikel gelöscht, Bestellung storniert – Rückgängig des Löschens wird abgelehnt")]
    public void Undo_after_order_storno()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddCustomer(new BizContact("Erika Muster", "", "", "Kunde", null, 0));
            s.AddProduct(new Product("X", "", 0, 2, 5, 0, 0));
            var o = s.PlaceOrder("Erika Muster", Day, ("X", 2)); s.Deliver(o.Id, Day);
            s.RemoveProduct("X");
            s.Cancel(o.Id, Day);
            Assert.Equal(["X"], s.LastNotRestocked);
            Assert.Throws<InvalidOperationException>(() => s.Undo());
            Assert.Empty(s.Products.All());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "H-1 bleibt: Neuschreiben mit gleichem Inhalt sperrt nicht – zwei Schritte zurück gehen")]
    public void Same_content_rewrite_does_not_block()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddProduct(new Product("A", "", 0, 2, 5, 0, 0));
            s.AddProduct(new Product("B", "", 0, 2, 5, 0, 0));
            Thread.Sleep(20); s.Products.Save(s.Products.All()); // gleicher Inhalt, neue Schreibzeit
            Assert.Equal("Artikel „B“ anlegen", s.Undo());
            Assert.Equal("Artikel „A“ anlegen", s.Undo());
            Assert.Empty(s.Products.All());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Niedrig: Import mit reiner Titelzeile zählt „unverändert“ statt „aktualisiert“")]
    public void Import_counts_unchanged()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddProduct(new Product("Tasse", "", 2, 6, 3, 0, 0));
            var f = Path.Combine(d, "a.csv"); File.WriteAllText(f, "Title\nTasse\n");
            var r = s.ImportProductsCsv(f);
            Assert.Equal((0, 0, 1), (r.Added, r.Updated, r.Unchanged));
        }
        finally { Directory.Delete(d, true); }
    }
}
