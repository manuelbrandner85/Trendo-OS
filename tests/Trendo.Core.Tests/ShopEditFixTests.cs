using System.Text;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753 Gegenprüfung: Kasse mit Varianten (M1), Varianten mit Bestellungen und Bons gesperrt (M2), Formel-Einschleusung im CSV (M3),
/// POD-Übernahme behält eigene Angaben (M4) sowie N2–N5.</summary>
public class ShopEditFixTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753f-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static readonly DateOnly Day = new(2026, 11, 23);
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    static CashRegister Kasse(string d)
    {
        var k = new CashRegister(d, "i");
        k.Shop.AddCustomer(new BizContact("Erika Muster", "", "", "Kunde", null, 0));
        k.Shop.AddProduct(new Product("Shirt", "", 0, 0, 0, 0, 0, "", "", "", [new("M", 20, 3, "SH-M"), new("L", 22, 1, "SH-L")]));
        return k;
    }

    [Fact(DisplayName = "M1: Kasse verkauft Varianten – Preis und Bestand der Variante, Lieferung danach stimmt, Storno bucht zurück")]
    public void Kasse_variants()
    {
        var d = Tmp();
        try
        {
            var k = Kasse(d); var ledger = new Ledger(d);
            Assert.Equal(["Shirt – M", "Shirt – L"], k.Shop.SaleItems().Select(x => x.Label));
            var s = k.Checkout([("Shirt – M", 2)], "karte", 0, new DateTime(2026, 11, 23, 10, 0, 0), ledger, smallBusiness: true);
            Assert.Equal(40m, s.Gross);
            var p = k.Shop.Products.All()[0];
            Assert.Equal((1, 1, 2), (p.Variants![0].Stock, p.Variants[1].Stock, p.Stock));
            var o = k.Shop.PlaceOrder("Erika Muster", Day, ("Shirt – L", 1));
            k.Shop.Deliver(o.Id, Day);
            p = k.Shop.Products.All()[0];
            Assert.Equal((1, 0, 1), (p.Variants![0].Stock, p.Variants[1].Stock, p.Stock));
            Assert.Throws<InvalidOperationException>(() => k.Checkout([("Shirt – L", 1)], "karte", 0, DateTime.Now, ledger, true)); // nicht vorrätig
            Assert.Throws<ArgumentException>(() => k.Checkout([("Shirt", 1)], "karte", 0, DateTime.Now, ledger, true)); // ohne Variante nicht verkaufbar
            k.Cancel(s.Id, DateTime.Now, ledger);
            Assert.Equal(3, k.Shop.Products.All()[0].Variants![0].Stock);
            Assert.Equal("Shirt – M", k.Shop.FindSaleItem("sh-m")?.Label); // Scanner trifft die Variante
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M2: Variante mit gelieferter Bestellung oder Bon nicht umbenennen/entfernen; Varianten nachträglich nicht über verkaufte Artikel; unbekannte Bezeichnung wird gemeldet")]
    public void Variants_locked_by_history()
    {
        var d = Tmp();
        try
        {
            var k = Kasse(d); var s = k.Shop;
            var o = s.PlaceOrder("Erika Muster", Day, ("Shirt – M", 2));
            s.Deliver(o.Id, Day);
            var e = Assert.Throws<InvalidOperationException>(() => s.UpdateProduct("Shirt", s.Products.All()[0] with { Variants = [new("Medium", 20, 1, "SH-M"), new("L", 22, 1, "SH-L")] }));
            Assert.Contains("Shirt – M", e.Message);
            s.UpdateProduct("Shirt", s.Products.All()[0] with { Name = "Shirt Wald" }); // Artikel umbenennen geht, Bestellung zieht mit
            s.Cancel(o.Id, Day);
            Assert.Equal(3, s.Products.All()[0].Variants![0].Stock); // Rücknahme kommt an
            // ohne Varianten geliefert, danach Varianten anlegen → gesperrt
            s.AddProduct(new Product("Tasse", "", 0, 8, 5, 0, 0));
            var o2 = s.PlaceOrder("Erika Muster", Day, ("Tasse", 2)); s.Deliver(o2.Id, Day);
            Assert.Throws<InvalidOperationException>(() => s.UpdateProduct("Tasse", s.Products.All()[1] with { Variants = [new("Weiß", 8, 3)] }));
            // Bon auf Variante sperrt ebenfalls
            k.Checkout([("Shirt Wald – L", 1)], "bar", 50, DateTime.Now, new Ledger(d), true);
            Assert.Throws<InvalidOperationException>(() => s.UpdateProduct("Shirt Wald", s.Products.All()[0] with { Variants = [new("M", 20, 3, "SH-M")] }));
            // gelöschter Artikel: Storno meldet statt still zu verlieren
            var o3 = s.PlaceOrder("Erika Muster", Day, ("Tasse", 1)); s.Deliver(o3.Id, Day);
            s.RemoveProduct("Tasse");
            var moves = s.Moves.All().Count;
            Assert.Throws<InvalidOperationException>(() => s.Cancel(o3.Id, Day));
            Assert.Equal(moves, s.Moves.All().Count); // keine Bewegung „+1“ ohne Bestand
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M3: CSV-Export entschärft Formeln (=, +, -, @) in Artikeln, Kunden und Bestellungen; reine Zahlen bleiben; Import nimmt das Hochkomma wieder ab")]
    public void Csv_formula_injection()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.AddCustomer(new BizContact("=HYPERLINK(\"http://x\")", "@SUM(1)", "-2+3", "Kunde", null, 0, "=1+1", "Weg 1, 12345 Ort"));
            s.AddProduct(new Product("=1+1", "@x", 0, 5, 1, 0, 0, "", "-2+3"));
            s.PlaceOrder("=HYPERLINK(\"http://x\")", Day, ("=1+1", 1));
            var all = new StringBuilder();
            foreach (var f in new[] { s.ExportProductsCsv(Path.Combine(d, "a.csv")), s.ExportCustomersCsv(Path.Combine(d, "k.csv")), s.ExportOrdersCsv(Path.Combine(d, "b.csv")) })
                foreach (var row in Shop.ParseCsv(File.ReadAllText(f)).Skip(1))
                    foreach (var c in row) { Assert.False(c.Length > 0 && c[0] is '=' or '+' or '@' || (c.StartsWith('-') && !double.TryParse(c, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)), $"Zelle „{c}“ in {Path.GetFileName(f)}"); all.Append(c).Append('|'); }
            Assert.Contains("'=1+1", all.ToString()); Assert.Contains("'@x", all.ToString()); Assert.Contains("'-2+3", all.ToString());
            var t = new Shop(d, "j");
            t.ImportProductsCsv(Path.Combine(d, "a.csv"));
            Assert.Equal(("=1+1", "@x", "-2+3"), (t.Products.All()[0].Name, t.Products.All()[0].Supplier, t.Products.All()[0].Description));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "M4: POD-Artikel aktualisieren behält Beschreibung, Einkaufspreis, Artikelnummer und Varianten-Nummern")]
    public void Pod_update_keeps_own_data()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            var item = new PodShopItem("x", "Shirt Wald", 29.75m, "https://files.cdn.printful.com/m.png", 5, [new("S", 1), new("M", 2)]);
            var p = s.TakeOverPod(item, false);
            s.UpdateProduct(p.Name, p with { Description = "Eigener Text", Cost = 8, Sku = "PW-1", Variants = [p.Variants![0] with { Sku = "PW-S" }, p.Variants[1]] });
            var q = s.TakeOverPod(item with { Price = 35.70m }, false);
            Assert.Equal(("Eigener Text", 8.0, "PW-1", "PW-S", 30.0), (q.Description, q.Cost, q.Sku, q.Variants![0].Sku, q.Price));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N2–N5: alter Name mit Trenner änderbar, mehrdeutige Zahlen abgelehnt, Bildpfade nur unterhalb des CSV-Ordners, Export in den Ideenordner")]
    public void Low_findings()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            s.Products.Save([new Product("Alt – Name", "", 0, 3, 1, 0, 0)]); // Altdaten
            s.UpdateProduct("Alt – Name", s.Products.All()[0] with { Price = 4 }); // N2: Preis ändern geht
            Assert.Equal(4, s.Products.All()[0].Price);
            Assert.Throws<ArgumentException>(() => s.UpdateProduct("Alt – Name", s.Products.All()[0] with { Name = "Neu – Name" }));
            // N3
            var f = Path.Combine(d, "z.csv");
            File.WriteAllText(f, "Title,Price\nKerze,\"1,000\"\n");
            Assert.Contains("mehrdeutig", Assert.Throws<ArgumentException>(() => s.ImportProductsCsv(f)).Message);
            File.WriteAllText(f, "Title,Price\nKerze,1.000\n");
            Assert.Throws<ArgumentException>(() => s.ImportProductsCsv(f));
            // N4
            var outside = Path.Combine(Path.GetTempPath(), "trendo-t0753f-aussen-" + Guid.NewGuid().ToString("N")[..6] + ".png"); File.WriteAllBytes(outside, Png);
            try
            {
                var sub = Path.Combine(d, "csvdir"); Directory.CreateDirectory(sub);
                File.WriteAllBytes(Path.Combine(d, "nachbar.png"), Png);
                File.WriteAllText(Path.Combine(sub, "b.csv"), $"Title,Price,Product image URL\nA,1.00,{outside}\nB,1.00,../nachbar.png\n");
                var r = s.ImportProductsCsv(Path.Combine(sub, "b.csv"));
                Assert.Equal(2, r.Warnings.Count(w => w.Contains("nicht gefunden") || w.Contains("außerhalb")));
                Assert.All(s.Products.All().Where(p => p.Name is "A" or "B"), p => Assert.Equal("", p.Image));
            }
            finally { File.Delete(outside); }
            // N5: Export direkt in den Ideenordner (bilder/ ist der eigene Bilderordner)
            var src = Path.Combine(d, "foto.png"); File.WriteAllBytes(src, Png);
            s.UpdateProduct("Alt – Name", s.Products.All()[0] with { Image = s.ImportImage(src) });
            var csv = s.ExportProductsCsv(Path.Combine(s.ImageDir, "..", "artikel.csv"));
            Assert.True(File.Exists(csv));
        }
        finally { Directory.Delete(d, true); }
    }
}
