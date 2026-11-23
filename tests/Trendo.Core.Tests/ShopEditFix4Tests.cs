using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753 Viertprüfung: M-E (fehlende CSV-Spalten lassen eigene Angaben stehen), N-g (alte Bons ohne Kennung: später angelegter
/// oder doppelt benannter Artikel bekommt keinen Bestand).</summary>
public class ShopEditFix4Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753q-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "M-E: nur Preise aktualisieren – Bestand, Einkaufspreis, Beschreibung, Art.-Nr., Hersteller und Varianten bleiben")]
    public void Only_prices()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddProduct(new Product("Kaffee", "Rösterei", 4, 10, 7, 0, 0, "K-1", "Ganze Bohne"));
            s.AddProduct(new Product("Shirt", "Druckerei", 6, 0, 0, 0, 0, "", "Bio", "", [new("M", 20, 3, "SH-M"), new("L", 22, 1, "SH-L")]));
            var f = Path.Combine(d, "preise.csv");
            File.WriteAllText(f, "Title,Price\nKaffee,12.50\nShirt,25.00\n");
            var r = s.ImportProductsCsv(f);
            Assert.Equal((0, 2), (r.Added, r.Updated));
            var k = s.Products.All()[0];
            Assert.Equal((12.5, 7, 4.0, "Ganze Bohne", "K-1", "Rösterei"), (k.Price, k.Stock, k.Cost, k.Description, k.Sku, k.Supplier));
            var sh = s.Products.All()[1];
            Assert.Equal(("Bio", 6.0, "Druckerei"), (sh.Description, sh.Cost, sh.Supplier));
            Assert.Equal([("M", 25.0, 3, "SH-M"), ("L", 25.0, 1, "SH-L")], sh.Variants!.Select(v => (v.Name, v.Price, v.Stock, v.Sku)));
            // Varianten-Zeilen ohne Bestand und Art.-Nr.: diese bleiben je Variante
            File.WriteAllText(f, "Title,URL handle,Option1 name,Option1 value,Price\nShirt,shirt,Größe,M,21.00\n,shirt,,L,23.00\n");
            s.ImportProductsCsv(f);
            Assert.Equal([("M", 21.0, 3, "SH-M"), ("L", 23.0, 1, "SH-L")], s.Products.All()[1].Variants!.Select(v => (v.Name, v.Price, v.Stock, v.Sku)));
            // nur Bestand: Preis bleibt
            File.WriteAllText(f, "Title,Inventory quantity\nKaffee,30\n");
            s.ImportProductsCsv(f);
            Assert.Equal((12.5, 30), (s.Products.All()[0].Price, s.Products.All()[0].Stock));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N-g: alter Bon ohne Kennung – Artikel erst nach dem Bon angelegt oder Name doppelt: nicht zurückbuchen, melden")]
    public void Legacy_bon_guards()
    {
        var d = Tmp();
        try
        {
            var k = new CashRegister(d, "i"); var ledger = new Ledger(d);
            Directory.CreateDirectory(Path.GetDirectoryName(k.Sales.FilePath)!);
            File.WriteAllText(k.Sales.FilePath, """[{"Id":"b1","No":"BON-1","At":"2026-11-01T10:00:00","Lines":[{"Product":"Keks","Qty":2,"PriceGross":2}],"Payment":"bar","Given":4,"VatRate":0,"Cancelled":false},{"Id":"b2","No":"BON-2","At":"2026-11-01T11:00:00","Lines":[{"Product":"Tee","Qty":1,"PriceGross":2}],"Payment":"bar","Given":2,"VatRate":0,"Cancelled":false}]""");
            k.Shop.AddProduct(new Product("Keks", "", 0, 2, 5, 0, 0)); // heute angelegt, also nach dem Bon
            k.Cancel("b1", DateTime.Now, ledger);
            Assert.Equal(["Keks"], k.NotRestocked);
            Assert.Equal(5, k.Shop.Products.All()[0].Stock);
            // doppelt vergebener Name in Altdaten
            File.WriteAllText(k.Shop.Products.FilePath, """[{"Name":"Tee","Supplier":"","Cost":0,"Price":2,"Stock":3,"LeadDays":0,"DailySales":0},{"Name":"Tee","Supplier":"","Cost":0,"Price":2,"Stock":4,"LeadDays":0,"DailySales":0}]""");
            k.Cancel("b2", DateTime.Now, ledger);
            Assert.Equal(["Tee"], k.NotRestocked);
            Assert.Equal([3, 4], k.Shop.Products.All().Select(p => p.Stock));
        }
        finally { Directory.Delete(d, true); }
    }
}
