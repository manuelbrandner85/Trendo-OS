using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0753: „Online verkaufen“ als Arbeitsplatz (Klick bis zur echten Datei) und POD: Testlauf, Übernahme in den Shop, Abgleich mit den dokumentierten Schnittstellen.</summary>
public class SellEdit_Ui
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    static (MainWindow W, string Root) OpenSell()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        w.Show("verkaufen"); Settle(); Click(w, "sell-tab-online");
        return (w, root);
    }
    static void CloseToasts(MainWindow w) { while (w.TryFind<Button>("toast-close") is { IsEffectivelyVisible: true } c) RealClick(w, c); }
    static void Click(MainWindow w, string name) { CloseToasts(w); var c = V<Control>(w, name); c.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text) => w.Find<TextBox>(name).Text = text;
    static string Texts(MainWindow w) => string.Join("|", w.GetLogicalTexts());

    [AvaloniaFact(DisplayName = "T-0753: Klick bis zur Datei – Artikel mit Bild und Varianten anlegen, ändern, Kunde ändern, Bestellung ändern, CSV aus und wieder ein, Rückgängig")]
    public void Click_to_file()
    {
        var (w, root) = OpenSell();
        try
        {
            var img = Path.Combine(root, "foto.png"); File.WriteAllBytes(img, Png);
            w.PickOverride = r => Task.FromResult<string[]?>(r.Field switch { "sell-prod-image" or "sell-pe-image" => [img], _ => null });
            // Kunde
            Type(w, "sell-cust-name", "Erika Muster"); Type(w, "sell-cust-address", "Hauptstraße 1, 10115 Berlin"); Click(w, "sell-cust-add");
            // Artikel mit Bild und Varianten
            Type(w, "sell-prod-name", "Shirt Wald"); Type(w, "sell-prod-price", "20"); Type(w, "sell-prod-desc", "Bio-Baumwolle, bedruckt");
            Click(w, "sell-prod-image");
            Type(w, "sell-prod-variants", "M; 20,00; 3; SW-M\nL; 22,50; 1; SW-L");
            Click(w, "sell-prod-add");
            var p = Assert.Single(w.BizShop.Products.All());
            Assert.Equal("", V<TextBox>(w, "sell-prod-variants").Text ?? ""); Assert.Equal("", V<TextBox>(w, "sell-prod-desc").Text ?? "");
            Assert.Equal((4, 2, "Bio-Baumwolle, bedruckt"), (p.Stock, p.Variants!.Count, p.Description));
            Assert.NotNull(w.BizShop.ImagePath(p));
            Assert.Single(w.Find<WrapPanel>("sell-shop").Children);
            Assert.Contains("ab 20,00 €", Texts(w)); // Vorschau zeigt den echten Stand (Kleinunternehmer: ohne MwSt.)
            // Bestellung auf Variante, dann ändern
            w.Find<ComboBox>("sell-order-customer").SelectedItem = "Erika Muster"; w.Find<ComboBox>("sell-order-product").SelectedItem = "Shirt Wald – L";
            Type(w, "sell-order-qty", "1"); Click(w, "sell-order-add");
            Click(w, "sell-order-edit-0");
            w.Find<ComboBox>("sell-oe-product").SelectedItem = "Shirt Wald – M"; Type(w, "sell-oe-qty", "2"); Click(w, "sell-oe-save");
            Assert.Equal(("Shirt Wald – M", 2m, 20m), (w.BizShop.Orders()[0].Lines[0].Product, w.BizShop.Orders()[0].Lines[0].Qty, w.BizShop.Orders()[0].Lines[0].PriceNet));
            // Artikel ändern: Name und Preis der Variante
            Click(w, "sell-prod-edit-0");
            Type(w, "sell-pe-name", "Shirt Waldlauf"); Type(w, "sell-pe-variants", "M; 21,00; 3; SW-M\nL; 22,50; 1; SW-L");
            Click(w, "sell-pe-save");
            Assert.Equal(21, w.BizShop.Products.All()[0].Variants![0].Price);
            Assert.Equal(("Shirt Waldlauf – M", 20m), (w.BizShop.Orders()[0].Lines[0].Product, w.BizShop.Orders()[0].Lines[0].PriceNet)); // alter Preis bleibt
            // Kunde ändern
            Click(w, "sell-cust-edit-0"); Type(w, "sell-ce-name", "Erika Beispiel"); Click(w, "sell-ce-save");
            Assert.Equal("Erika Beispiel", w.BizShop.Orders()[0].Customer);
            // Rückgängig nimmt die Kundenänderung zurück
            Click(w, "sell-undo");
            Assert.Equal("Erika Muster", w.BizShop.Customers.All()[0].Name);
            Assert.Equal("Erika Muster", w.BizShop.Orders()[0].Customer);
            // CSV aus
            Click(w, "sell-csv-prod-out"); Click(w, "sell-csv-cust-out"); Click(w, "sell-csv-orders-out");
            var csv = Directory.GetFiles(root, "*-Artikel-shopify.csv", SearchOption.AllDirectories).Single();
            var text = File.ReadAllText(csv);
            Assert.StartsWith("Title,URL handle,Description", text);
            Assert.Contains("shirt-waldlauf", text);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(csv)!, "bilder", Path.GetFileName(w.BizShop.ImagePath(w.BizShop.Products.All()[0])!))));
            Assert.Single(Directory.GetFiles(root, "*-Bestellungen-shopify.csv", SearchOption.AllDirectories));
            // Artikel löschen (vorgemerkt → abgelehnt), CSV wieder einlesen nach Löschen eines freien Artikels
            Click(w, "sell-prod-del-0");
            Assert.Equal("verkaufen", w.CurrentView);
            Assert.Contains("vorgemerkt", Texts(w));
            Type(w, "sell-prod-name", "Kerze"); Type(w, "sell-prod-price", "4,50"); Type(w, "sell-prod-stock", "9"); Assert.Equal("", V<TextBox>(w, "sell-prod-variants").Text ?? ""); Assert.Equal("", V<TextBox>(w, "sell-prod-sku").Text ?? ""); Click(w, "sell-prod-add");
            Assert.True(w.BizShop.Products.All().Any(x => x.Name == "Kerze"), string.Join(" § ", w.GetLogicalTexts().SkipWhile(t => !t.Contains("Online verkaufen")).Where(t => t.Length > 25).Take(15)));
            Click(w, "sell-prod-del-1");
            Assert.DoesNotContain(w.BizShop.Products.All(), x => x.Name == "Kerze");
            w.PickOverride = r => Task.FromResult<string[]?>(r.Field == "sell-csv-prod" ? [csv] : null);
            Click(w, "sell-csv-prod-in");
            Assert.Contains("0 neu, 1 aktualisiert", Texts(w));
            // Rechtsklick-Menü vorhanden
            Assert.NotNull(w.Find<Border>("sell-prod-card-0").ContextMenu);
            Assert.NotNull(w.Find<Border>("sell-cust-row-0").ContextMenu);
            // Ablegen einer CSV-Datei (gleicher Weg wie Ziehen und Ablegen)
            var drop = Path.Combine(root, "neu.csv"); File.WriteAllText(drop, "Title,Price,Inventory quantity\nTasse,8.00,4\n");
            Assert.Contains("1 neu", w.SellDropFiles([drop]));
            Assert.Contains(w.BizShop.Products.All(), x => x.Name == "Tasse" && x.Stock == 4);
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } beleg) // Rohbelege für den Fremdprogramm-Test
            {
                var outDir = Path.Combine(beleg, "csv"); Directory.CreateDirectory(Path.Combine(outDir, "bilder"));
                foreach (var f in Directory.GetFiles(Path.GetDirectoryName(csv)!, "*-shopify.csv")) File.Copy(f, Path.Combine(outDir, Path.GetFileName(f).Split('-', 2)[1]), true);
                foreach (var f in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(csv)!, "bilder"))) File.Copy(f, Path.Combine(outDir, "bilder", Path.GetFileName(f)), true);
            }
            // Webseite speichern: Bild ist eingebettet
            Click(w, "sell-shop-save");
            var html = File.ReadAllText(Directory.GetFiles(root, "*-Shop.html", SearchOption.AllDirectories).Single());
            Assert.Contains("data:image/png;base64,", html); Assert.Contains("Shirt Waldlauf", html); Assert.Contains("Tasse", html);
        }
        finally { w.Close(); }
    }

    sealed class Counter : IWebSend
    {
        public int Calls;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        { Interlocked.Increment(ref Calls); throw new InvalidOperationException("Ohne Schlüssel darf nichts gesendet werden."); }
    }

    [AvaloniaFact(DisplayName = "T-0753: POD-Testlauf in der Oberfläche ohne eigene Schlüssel – BLOCKIERT mit Anleitung, keine Anfrage; Entwurf wird Artikel im Shop")]
    public async Task Pod_testrun_and_takeover()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var net = new Counter(); w.PodWebOverride = net;
            w.Show("pod-programm"); Settle(); RealClick(w, w.Find<Button>("prog-tpl-tshirt")); Settle();
            var d = w.ProgramActive("pod-programm")!;
            var probes = await w.PodTestRunAsync(); Settle();
            Assert.Equal(0, net.Calls);
            Assert.All(probes.Take(4), p => Assert.Equal(PodProbeState.Blockiert, p.State));
            Assert.Contains(w.GetLogicalTexts(), t => t == "Printful: BLOCKIERT");
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("sk_test_"));
            // Entwurf in den Shop übernehmen
            RealClick(w, w.Find<Button>("pod-add-rect")); Settle(); // Entwurf nicht leer
            // ERSETZT: die Headless-Testumgebung zeichnet ohne Skia (leeres PNG); die echte Druckdatei prüft PodDesign_Ui bzw. der Render-Beleg
            w.PodPngOverride = _ => Png;
            d.Set("gross", 29.75, "Preis"); d.Set("description", "Motiv Wald", "Beschreibung");
            var p = w.PodToShop(d);
            Assert.Equal((d.Title, 29.75, "Motiv Wald"), (p.Name, p.Price, p.Description)); // Kleinunternehmer: brutto = netto
            var png = w.BizShop.ImagePath(p); Assert.NotNull(png);
            Assert.True(new FileInfo(png!).Length > 8, "Länge " + new FileInfo(png!).Length);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], File.ReadAllBytes(png!)[..4]);
            Assert.True(Shop.MadeToOrder(p));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0753 ERSETZT: Anfragen an Printful, Printify, Stripe (Test), Netlify gegen lokalen Server nach offizieller Dokumentation – keine Abweichung, nichts Kostenpflichtiges")]
    public async Task Requests_match_docs()
    {
        using var srv = new PodDocServer();
        var web = srv.Sender();
        // Printful: Katalog, Varianten, Druckflächen, Mockup, Produkt, Entwurf (ohne Bestätigung)
        var pf = new PrintfulClient(web, "pf-test-token");
        await pf.CheckAsync();
        var cat = await pf.CatalogAsync(); Assert.Single(cat); // eingestelltes Produkt ausgeblendet
        var vars = await pf.VariantsAsync(71); Assert.Equal(2, vars.Count);
        var places = await pf.PlacementsAsync(71); Assert.Equal(["front", "back"], places.Select(x => x.Key));
        var task = await pf.StartMockupAsync(71, [4012], "front", "https://x.netlify.app/0123.png");
        var done = await pf.MockupAsync(task.Key); Assert.True(done.Done); Assert.Equal(2, done.Urls.Count);
        var prod = await pf.CreateProductAsync("Shirt", [(4012, 24.90m)], "https://x.netlify.app/0123.png", "front");
        Assert.Equal(PfFileState.Ready, await pf.ProductFilesStateAsync(prod));
        var sync = await pf.ProductVariantsAsync(prod);
        var order = new PodPaidOrder("cs_live_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "plink", "Erika Muster", new("Erika Muster", "Hauptstraße 1", "Berlin", "10115", "DE"), "erika@example.org");
        var orders = new PrintfulOrders(web, "pf-test-token");
        Assert.Null(await orders.FindAsync(order.SessionId));
        var draft = await orders.CreateDraftAsync(order, sync[0].SyncId); Assert.Equal(12.40m, draft.Cost);
        // Printify
        var pfy = new PrintifyClient(web, "pfy-test-token");
        var shop = Assert.Single(await pfy.ShopsAsync());
        var bp = Assert.Single(await pfy.CatalogAsync());
        var prov = Assert.Single(await pfy.ProvidersAsync(bp.Id));
        var pv = Assert.Single(await pfy.VariantsAsync(bp.Id, prov.Id)); Assert.Equal("front", pv.Position);
        var up = await pfy.UploadAsync("design.png", Png);
        var made = await pfy.CreateProductAsync(shop.Id, "Shirt", bp.Id, prov.Id, [pv.Id], 24.90m, pv.Position, up);
        var remote = await pfy.CreateOrderAsync(shop.Id, order, made.Id, pv.Id);
        Assert.Equal(16.50m, await pfy.OrderCostAsync(shop.Id, remote));
        // Netlify
        var nf = new NetlifyDesignHost(web, "nf-test-token");
        await nf.CheckAsync();
        var (site, _) = await nf.CreateSiteAsync();
        await nf.DeployAsync(site, NetlifyDesignHost.Zip([("a.png", Png)]), (_, _) => Task.CompletedTask);
        // Stripe über die App: Testschlüssel, Bezahl-Link, Testzahlung wird NICHT an Printful gegeben
        var (w, _, _) = Ui.Open();
        try
        {
            w.PodWebOverride = web;
            Assert.True(await w.PodStripeConnect("sk_test_trendo"));
            w.PodShopItems.Add(new("i", "Shirt", 24.9m, "", prod, [new("M", sync[0].SyncId)]));
            Assert.True(await w.PodCreatePayLinks());
            Assert.True(await w.PodFetchOrders());
            var paid = Assert.Single(w.PodShopOrders); Assert.False(paid.Live);
            Assert.False(await w.PodDraftOrder(paid));
            Assert.Contains("Testzahlung", w.PodShopMessage);
            Assert.True(await w.PodPrintfulConnect("pf-test-token"));
            Assert.True(await w.PodPrintifyConnect("pfy-test-token"));
            var probes = await w.PodTestRunAsync();
            Assert.Equal(PodProbeState.Bereit, probes.Single(p => p.Service == "Printful").State);
            Assert.Equal(PodProbeState.Bereit, probes.Single(p => p.Service == "Printify").State);
            Assert.Equal(PodProbeState.Bereit, probes.Single(p => p.Service == "Stripe").State);
            Assert.Equal(PodProbeState.Blockiert, probes.Single(p => p.Service == "Netlify").State); // in der App nicht verbunden
            // POD-Produkt in den Shop übernehmen
            var art = w.PodShopItemToShop("i");
            Assert.Equal(["M"], art.Variants!.Select(v => v.Name));
        }
        finally { w.Close(); }

        Assert.Empty(srv.Deviations);
        Assert.DoesNotContain(srv.Paths, x => x.EndsWith("/confirm") || x.EndsWith("send_to_production.json"));
        Assert.Equal(srv.Paths.Count(x => x == "POST printful/orders"), 1); // nur der eine Entwurf aus dem Kernteil, keiner aus der Testzahlung
        if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            var log = new StringBuilder();
            log.AppendLine("T-0753 Beleg ERSETZT: lokaler Testserver nach offizieller API-Dokumentation (Printful v1, Printify v1, Stripe, Netlify), kein echter Dienst, kein Schlüssel der Person.");
            log.AppendLine($"Zeit: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} · Transport: echter HTTP-Weg der App (HttpFetch/HttpClient) an localhost");
            log.AppendLine($"Anfragen: {srv.Log.Count} · Abweichungen: {srv.Deviations.Count} · kostenpflichtige Aufrufe: {srv.Paths.Count(x => x.EndsWith("/confirm") || x.EndsWith("send_to_production.json"))}");
            foreach (var l in srv.Log) log.AppendLine(l);
            var f = Path.Combine(dir, "pod-docserver.log");
            File.WriteAllText(f, log.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
            File.WriteAllText(f + ".sha256", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant() + "  pod-docserver.log\n");
        }
    }
}
