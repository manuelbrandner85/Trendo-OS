using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753 fünfte Prüfung: H-1 (Rückgängig über viele Schritte), M-F (leere Zelle = behalten), N-h (Varianten ohne Groß/klein, entfernte Varianten melden).</summary>
public class ShopEditFix5Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753r-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "H-1: 20× anlegen, 20× Rückgängig – jeder Schritt geht zurück, ohne „seitdem verkauft“")]
    public void Undo_twenty_steps()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            for (var i = 0; i < 20; i++) { s.AddProduct(new Product($"Artikel {i}", "", 0, 2, 1, 0, 0)); Thread.Sleep(20); }
            for (var i = 19; i >= 0; i--)
            {
                Assert.Equal($"Artikel „Artikel {i}“ anlegen", s.Undo());
                Assert.Equal(i, s.Products.All().Count);
            }
            Assert.Null(s.UndoLabel);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "H-1: 5 schnell hintereinander (Artikel, Kunde, Änderung) – 5× Rückgängig klappt; echte fremde Änderung wird weiter erkannt")]
    public void Undo_fast_mixed()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddProduct(new Product("Keks", "", 0, 2, 5, 0, 0));
            s.AddCustomer(new BizContact("Erika Muster", "", "", "Kunde", null, 0));
            s.UpdateProduct("Keks", s.Products.All()[0] with { Price = 3 });
            s.AddProduct(new Product("Tee", "", 0, 2, 5, 0, 0));
            s.UpdateCustomer("Erika Muster", s.Customers.All()[0] with { Email = "e@example.org" });
            for (var i = 0; i < 5; i++) s.Undo();
            Assert.Empty(s.Products.All()); Assert.Empty(s.Customers.All());
            // echte fremde Änderung: Kasse verkauft nach dem Anlegen
            s.AddProduct(new Product("Keks", "", 0, 2, 5, 0, 0));
            new CashRegister(d, "i").Checkout([("Keks", 1)], "bar", 5, DateTime.Now, new Ledger(d), true);
            Assert.Contains("verkauft", Assert.Throws<InvalidOperationException>(() => s.Undo()).Message);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M-F: leere Zelle bei Preis, Einkaufspreis, Bestand und Variante bedeutet behalten; „0“ setzt 0; leere Beschreibung leert; neuer Artikel ohne Preis wird nicht angelegt")]
    public void Empty_cells_keep()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddProduct(new Product("Tasse", "", 2, 6, 3, 0, 0, "", "Steingut"));
            s.AddProduct(new Product("Shirt", "", 0, 0, 0, 0, 0, "", "", "", [new("M", 20, 3, "SH-M")]));
            var f = Path.Combine(d, "a.csv");
            File.WriteAllText(f, "Title,Price,Inventory quantity,Cost per item\nTasse,,8,\n");
            s.ImportProductsCsv(f);
            var t = s.Products.All()[0];
            Assert.Equal((6.0, 8, 2.0, "Steingut"), (t.Price, t.Stock, t.Cost, t.Description));
            File.WriteAllText(f, "Title,Price,Cost per item,Description\nTasse,0,,\n");
            s.ImportProductsCsv(f);
            t = s.Products.All()[0];
            Assert.Equal((0.0, 8, 2.0, ""), (t.Price, t.Stock, t.Cost, t.Description));
            File.WriteAllText(f, "Title,URL handle,Option1 name,Option1 value,Price,Inventory quantity\nShirt,shirt,Größe,M,,\n");
            s.ImportProductsCsv(f);
            Assert.Equal((20.0, 3), (s.Products.All()[1].Variants![0].Price, s.Products.All()[1].Variants![0].Stock));
            File.WriteAllText(f, "Title,Price,Inventory quantity\nNeu,,4\n");
            var r = s.ImportProductsCsv(f);
            Assert.Equal(0, r.Added);
            Assert.Contains(r.Warnings, w => w.Contains("„Neu“") && w.Contains("Preis"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N-h: Variante „m“ trifft „M“ (Name und Art.-Nr. bleiben); entfernte Variante wird mit Name und Bestand gemeldet")]
    public void Variant_case_and_removed()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddProduct(new Product("Shirt", "", 0, 0, 0, 0, 0, "", "", "", [new("M", 20, 3, "SH-M"), new("L", 22, 4, "SH-L")]));
            var f = Path.Combine(d, "a.csv");
            File.WriteAllText(f, "Title,URL handle,Option1 name,Option1 value,Price,Inventory quantity\nShirt,shirt,Größe,m,21.00,5\n");
            var r = s.ImportProductsCsv(f);
            var v = Assert.Single(s.Products.All()[0].Variants!);
            Assert.Equal(("M", 21.0, 5, "SH-M"), (v.Name, v.Price, v.Stock, v.Sku));
            Assert.Contains(r.Warnings, w => w.Contains("„L“") && w.Contains("4"));
        }
        finally { Directory.Delete(d, true); }
    }
}
