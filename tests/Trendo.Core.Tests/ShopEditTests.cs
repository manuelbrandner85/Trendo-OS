using System.Diagnostics;
using System.Text;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0753: Online verkaufen – Artikel, Kunden, Bestellungen ändern; Varianten; Rückgängig; CSV im Shopify-Format; POD-Übernahme; POD-Testlauf.</summary>
public class ShopEditTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0753-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static readonly DateOnly Day = new(2026, 11, 23);
    // 1×1-Pixel-PNG
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    static Shop Filled(string d)
    {
        var s = new Shop(d, "idee1");
        s.AddCustomer(new BizContact("Erika Muster", "", "erika@example.org", "Kunde", null, 0, "", "Hauptstraße 1, 10115 Berlin"));
        s.AddProduct(new Product("Kaffee", "Rösterei", 4, 10, 5, 0, 0, "4006381333931", "Ganze Bohne, 500 g"));
        s.AddProduct(new Product("Shirt", "", 0, 0, 0, 0, 0, "", "", "", [new("M", 20, 3, "SH-M"), new("L", 22, 1, "SH-L")]));
        return s;
    }

    [Fact(DisplayName = "T-0753: Artikel umbenennen und Preis ändern – Bestellung behält alten Preis, Lieferung trifft den umbenannten Artikel")]
    public void Rename_keeps_order_price()
    {
        var d = Tmp();
        try
        {
            var s = Filled(d);
            var o = s.PlaceOrder("Erika Muster", Day, ("Kaffee", 2));
            s.UpdateProduct("Kaffee", s.Products.All()[0] with { Name = "Kaffee Bohne", Price = 12 });
            var after = s.Orders().Single();
            Assert.Equal("Kaffee Bohne", after.Lines[0].Product);
            Assert.Equal(10m, after.Lines[0].PriceNet); // alter Preis bleibt
            s.Deliver(o.Id, Day);
            Assert.Equal(3, s.Products.All().Single(p => p.Name == "Kaffee Bohne").Stock);
            Assert.Throws<ArgumentException>(() => s.UpdateProduct("Kaffee Bohne", s.Products.All()[0] with { Name = "Shirt" })); // Name schon vergeben
            Assert.Throws<ArgumentException>(() => s.UpdateProduct("Shirt", s.Products.All()[1] with { Variants = [new("M", 20, 3, "4006381333931")] })); // Nummer schon vergeben
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: Varianten – eigener Preis, Bestand je Variante sinkt bei Lieferung, Summe stimmt, vorgemerkte Variante nicht entfernbar")]
    public void Variants_stock()
    {
        var d = Tmp();
        try
        {
            var s = Filled(d);
            Assert.Equal(["Kaffee", "Shirt – M", "Shirt – L"], s.OrderableItems());
            Assert.Equal(4, s.Products.All()[1].Stock);
            var o = s.PlaceOrder("Erika Muster", Day, ("Shirt – L", 1));
            Assert.Equal(22m, o.Lines[0].PriceNet);
            Assert.Equal(1m, s.Reserved("Shirt"));
            Assert.Throws<InvalidOperationException>(() => s.UpdateProduct("Shirt", s.Products.All()[1] with { Variants = [new("M", 20, 3, "SH-M")] }));
            Assert.Throws<InvalidOperationException>(() => s.RemoveProduct("Shirt"));
            s.Deliver(o.Id, Day);
            var shirt = s.Products.All()[1];
            Assert.Equal((3, 0, 3), (shirt.Variants![0].Stock, shirt.Variants[1].Stock, shirt.Stock));
            s.Cancel(o.Id, Day); // Rücknahme: Bestand der Variante wieder hoch
            Assert.Equal(1, s.Products.All()[1].Variants![1].Stock);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: Kunde umbenennen ändert seine Bestellungen; Kunde mit Bestellung nicht löschbar; offene Bestellung ändern")]
    public void Customers_and_orders()
    {
        var d = Tmp();
        try
        {
            var s = Filled(d);
            var o = s.PlaceOrder("Erika Muster", Day, ("Kaffee", 1));
            s.UpdateCustomer("Erika Muster", s.Customers.All()[0] with { Name = "Erika Beispiel", Company = "Muster GmbH" });
            Assert.Equal("Erika Beispiel", s.Orders()[0].Customer);
            Assert.Throws<InvalidOperationException>(() => s.RemoveCustomer("Erika Beispiel"));
            s.UpdateOrder(o.Id, "Erika Beispiel", ("Shirt – M", 2));
            Assert.Equal(("Shirt – M", 2m, 20m), (s.Orders()[0].Lines[0].Product, s.Orders()[0].Lines[0].Qty, s.Orders()[0].Lines[0].PriceNet));
            s.Deliver(o.Id, Day);
            Assert.Throws<InvalidOperationException>(() => s.UpdateOrder(o.Id, "Erika Beispiel", ("Kaffee", 1)));
            s.AddCustomer(new BizContact("Max Beispiel", "", "", "Interessent", null, 0));
            s.RemoveCustomer("Max Beispiel");
            Assert.Single(s.Customers.All());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: Rückgängig holt gelöschten Artikel zurück; nach einer Lieferung wird Rückgängig abgelehnt")]
    public void Undo_guarded()
    {
        var d = Tmp();
        try
        {
            var s = Filled(d);
            s.RemoveProduct("Kaffee");
            Assert.Equal("Artikel „Kaffee“ löschen", s.UndoLabel);
            Assert.Equal("Artikel „Kaffee“ löschen", s.Undo());
            Assert.Contains(s.Products.All(), p => p.Name == "Kaffee" && p.Stock == 5);
            var o = s.PlaceOrder("Erika Muster", Day, ("Kaffee", 1));
            s.Deliver(o.Id, Day); // Bestand und Bestellung geändert, ohne Rückgängig-Eintrag
            var e = Assert.Throws<InvalidOperationException>(() => s.Undo());
            Assert.Contains("nicht mehr möglich", e.Message);
            Assert.Equal(4, s.Products.All().Single(p => p.Name == "Kaffee").Stock);
            Assert.Null(s.UndoLabel);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: Artikel-CSV im Shopify-Format – Spalten, Varianten, Bild, Beschreibung mit Komma und Umlaut, Rundreise in eine andere Idee")]
    public void Products_csv_roundtrip()
    {
        var d = Tmp();
        try
        {
            var s = Filled(d);
            var src = Path.Combine(d, "foto.png"); File.WriteAllBytes(src, Png);
            var k = s.Products.All()[0];
            s.UpdateProduct("Kaffee", k with { Image = s.ImportImage(src), Description = "Kräftig, \"dunkel\" geröstet\nzweite Zeile" });
            File.Delete(src); // Bild liegt im Bilderordner der Idee – Ursprung darf weg
            Assert.NotNull(s.ImagePath(s.Products.All()[0]));
            var outDir = Path.Combine(d, "export"); Directory.CreateDirectory(outDir);
            var csv = s.ExportProductsCsv(Path.Combine(outDir, "artikel.csv"));
            var text = File.ReadAllText(csv);
            Assert.StartsWith(string.Join(",", Shop.ProductColumns.Select(c => c.Contains(' ') ? c : c)) + "\n", text);
            Assert.DoesNotContain("\r\n", text);
            var rows = Shop.ParseCsv(text);
            Assert.Equal(4, rows.Count); // Kopf + Kaffee + 2 Varianten
            Assert.All(rows, r => Assert.Equal(Shop.ProductColumns.Length, r.Length));
            Assert.Equal(("shirt", "", "L", "22.00", "1"), (rows[3][1], rows[3][0], rows[3][10], rows[3][11], rows[3][15]));
            Assert.StartsWith("bilder/", rows[1][19]);
            Assert.True(File.Exists(Path.Combine(outDir, rows[1][19])));

            var t = new Shop(d, "idee2");
            var r2 = t.ImportProductsCsv(csv);
            Assert.Equal((2, 0), (r2.Added, r2.Updated)); Assert.Empty(r2.Warnings);
            var a = t.Products.All();
            Assert.Equal(("Kaffee", 10.0, 5, "4006381333931", 4.0, "Rösterei"), (a[0].Name, a[0].Price, a[0].Stock, a[0].Sku, a[0].Cost, a[0].Supplier));
            Assert.Equal("Kräftig, \"dunkel\" geröstet\nzweite Zeile", a[0].Description);
            Assert.Equal(Png, File.ReadAllBytes(t.ImagePath(a[0])!));
            Assert.Equal(["M", "L"], a[1].Variants!.Select(v => v.Name));
            Assert.Equal((22.0, 1, "SH-L"), (a[1].Variants![1].Price, a[1].Variants[1].Stock, a[1].Variants[1].Sku));
            // erneut laden: aktualisiert statt doppelt
            Assert.Equal((0, 2), (t.ImportProductsCsv(csv).Added, t.ImportProductsCsv(csv).Updated));
            Assert.Equal(2, t.Products.All().Count);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: Shopify-Export mit älteren Spaltennamen (Handle, Body (HTML), Variant Price …) und Semikolon-Tabelle werden gelesen")]
    public void Import_legacy_columns()
    {
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "products_export.csv");
            File.WriteAllText(f, "Handle,Title,Body (HTML),Vendor,Option1 Name,Option1 Value,Variant SKU,Variant Inventory Qty,Variant Price,Image Src\r\n" +
                "tasse,Tasse Wald,\"<p>Steingut, <strong>350 ml</strong></p>\",Druckerei,Farbe,Weiß,TW-1,4,12.90,https://cdn.example.org/tasse.jpg\r\n" +
                "tasse,,,,,Schwarz,TW-2,2,13.90,\r\n" +
                "poster,Poster,,,,Default Title,,0,9.50,\r\n", new UTF8Encoding(true));
            var s = new Shop(d, "i");
            var r = s.ImportProductsCsv(f);
            Assert.Equal(2, r.Added);
            var t = s.Products.All()[0];
            Assert.Equal(("Tasse Wald", "Steingut, 350 ml", "https://cdn.example.org/tasse.jpg", 6), (t.Name, t.Description, t.Image, t.Stock));
            Assert.Equal(["Weiß", "Schwarz"], t.Variants!.Select(v => v.Name));
            Assert.Null(s.Products.All()[1].Variants);
            Assert.Equal(9.5, s.Products.All()[1].Price);

            var g = Path.Combine(d, "tabelle.csv");
            File.WriteAllText(g, "Title;Price;Inventory quantity;SKU\nKerze;4,50;10;\n");
            Assert.Equal(1, s.ImportProductsCsv(g).Added);
            Assert.Equal((4.5, 10), (s.Products.All()[2].Price, s.Products.All()[2].Stock));
            File.WriteAllText(g, "Name;Preis\nx;1\n");
            Assert.Throws<ArgumentException>(() => s.ImportProductsCsv(g));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: 200 Artikel aus CSV in unter 5 Sekunden, doppelte Artikelnummer wird gemeldet")]
    public void Import_200_fast()
    {
        var d = Tmp();
        try
        {
            var sb = new StringBuilder("Title,URL handle,SKU,Price,Inventory quantity\n");
            for (var i = 0; i < 200; i++) sb.Append($"Artikel {i},artikel-{i},A{(i == 150 ? 7 : i)},{i}.50,{i % 7}\n");
            var f = Path.Combine(d, "viele.csv"); File.WriteAllText(f, sb.ToString());
            var s = new Shop(d, "i");
            var sw = Stopwatch.StartNew();
            var r = s.ImportProductsCsv(f);
            sw.Stop();
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString());
            Assert.Equal(200, r.Added);
            Assert.Contains(r.Warnings, w => w.Contains("A7") && w.Contains("doppelt"));
            Assert.Equal("", s.Products.All()[150].Sku);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: Kunden-CSV im Shopify-Format hin und zurück, Bestellungen als CSV")]
    public void Customers_and_orders_csv()
    {
        var d = Tmp();
        try
        {
            var s = Filled(d);
            var o = s.PlaceOrder("Erika Muster", Day, ("Shirt – M", 2));
            var f = s.ExportCustomersCsv(Path.Combine(d, "kunden.csv"));
            var rows = Shop.ParseCsv(File.ReadAllText(f));
            Assert.Equal(Shop.CustomerColumns, rows[0]);
            Assert.Equal(("Erika", "Muster", "Hauptstraße 1", "Berlin", "10115", "DE", "1"), (rows[1][0], rows[1][1], rows[1][5], rows[1][7], rows[1][10], rows[1][9], rows[1][16]));
            var t = new Shop(d, "idee2");
            Assert.Equal(1, t.ImportCustomersCsv(f).Added);
            Assert.Equal(("Erika Muster", "Hauptstraße 1, 10115 Berlin", "erika@example.org"), (t.Customers.All()[0].Name, t.Customers.All()[0].Address, t.Customers.All()[0].Email));
            var of = s.ExportOrdersCsv(Path.Combine(d, "bestellungen.csv"));
            var or = Shop.ParseCsv(File.ReadAllText(of));
            Assert.Equal(Shop.OrderColumns, or[0]);
            Assert.Equal(("#" + o.Id, "erika@example.org", "pending", "unfulfilled", "40.00", "2", "Shirt – M", "SH-M"), (or[1][0], or[1][1], or[1][2], or[1][3], or[1][5], or[1][8], or[1][9], or[1][11]));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: Webshop-Vorschau zeigt den echten Stand – Bild eingebettet, Beschreibung, Varianten mit Preis und Bestand")]
    public void Shop_html_real_state()
    {
        var d = Tmp();
        try
        {
            var s = Filled(d);
            var src = Path.Combine(d, "foto.png"); File.WriteAllBytes(src, Png);
            s.UpdateProduct("Kaffee", s.Products.All()[0] with { Image = s.ImportImage(src) });
            var html = Shop.ShopHtml("Muster", s.Products.All(), smallBusiness: true, s.ImageDir);
            Assert.Contains("data:image/png;base64," + Convert.ToBase64String(Png), html);
            Assert.Contains("Ganze Bohne, 500 g", html);
            Assert.Contains("ab 20,00 €", html); Assert.Contains("<td>L</td><td>22,00 €</td><td class=s>1 vorrätig</td>", html);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0753: POD-Produkt wird Artikel – netto aus dem Bruttopreis, Varianten, Bild-Adresse, „auf Bestellung“")]
    public void Pod_takeover()
    {
        var d = Tmp();
        try
        {
            var s = new Shop(d, "i");
            var item = new PodShopItem("x", "Shirt Wald", 29.75m, "https://files.cdn.printful.com/m.png", 5, [new("S", 1), new("M", 2)]);
            var p = s.TakeOverPod(item, smallBusiness: false);
            Assert.Equal((25.0, "Print on Demand · Printful", "https://files.cdn.printful.com/m.png"), (p.Price, p.Supplier, p.Image));
            Assert.Equal(["S", "M"], p.Variants!.Select(v => v.Name));
            Assert.True(Shop.MadeToOrder(p));
            Assert.Contains("auf Bestellung", Shop.ShopHtml("x", s.Products.All(), false));
            s.TakeOverPod(item with { Price = 35.70m }, smallBusiness: false); // zweites Mal: aktualisiert
            Assert.Equal(30.0, Assert.Single(s.Products.All()).Price);
            var png = Path.Combine(d, "druck.png"); File.WriteAllBytes(png, Png);
            var e = s.TakeOverDesign("Tasse Fuchs", 19.99m, "", png, smallBusiness: true);
            Assert.Equal(19.99, e.Price); Assert.NotNull(s.ImagePath(e));
        }
        finally { Directory.Delete(d, true); }
    }

    sealed class Count : IWebSend
    {
        public readonly List<string> Calls = [];
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add(method + " " + url);
            var json = url.Contains("/checkout/sessions") ? """{"data":[],"has_more":false}""" : url.EndsWith("/shops.json") ? """[{"id":1,"title":"Mein Shop"}]""" : """{"result":{}}""";
            return Task.FromResult(new WebReply(200, Encoding.UTF8.GetBytes(json)));
        }
    }

    [Fact(DisplayName = "T-0753: POD-Testlauf ohne Schlüssel – alle BLOCKIERT mit Anleitung, keine einzige Anfrage; Stripe-Live-Schlüssel abgelehnt")]
    public async Task Testrun_without_keys()
    {
        var net = new Count();
        var r = await PodTestRun.RunAsync(net, _ => null);
        Assert.Empty(net.Calls);
        Assert.Equal(["Printful", "Printify", "Stripe", "Netlify", "Gelato"], r.Select(p => p.Service));
        Assert.All(r.Take(4), p => { Assert.Equal(PodProbeState.Blockiert, p.State); Assert.StartsWith("BLOCKIERT", p.Message); Assert.True(p.Steps.Count >= 3); });
        Assert.Equal(PodProbeState.Fehlt, r[4].State);
        var live = await PodTestRun.RunAsync(net, k => k == PodTestRun.StripeKey ? "sk_live_abc" : null);
        Assert.Empty(net.Calls);
        Assert.Equal(PodProbeState.Abgelehnt, live[2].State);
        var test = await PodTestRun.RunAsync(net, k => k == PodTestRun.StripeKey ? "sk_test_abc" : k == PodTestRun.PrintifyKey ? "t" : null);
        Assert.Equal(PodProbeState.Bereit, test[2].State); Assert.Equal(PodProbeState.Bereit, test[1].State);
        Assert.All(net.Calls, c => Assert.StartsWith("GET ", c)); // nur lesend
    }
}
