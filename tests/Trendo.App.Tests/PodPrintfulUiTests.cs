using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Printful im POD-Studio, ohne echtes Konto.</summary>
public class PodPrintful_Ui
{
    const string K = "pod-programm";

    sealed class Fake : IWebSend
    {
        public readonly List<string> Calls = [];
        public bool FilesReady = true;
        public int ScopeCode = 200;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add(method + " " + url);
            var (c, s) = url switch
            {
                var u when u.EndsWith("/oauth/scopes") => (ScopeCode, """{"result":{"scopes":[]}}"""),
                var u when u.EndsWith("/sites") => (200, """{"id":"s1","ssl_url":"https://a.netlify.app"}"""),
                var u when u.EndsWith("/deploys") => (200, """{"id":"d1","state":"ready"}"""),
                var u when u.EndsWith("/products") && method == "GET" => (200, """{"result":[{"id":71,"title":"Unisex Shirt","type_name":"T-Shirt","variant_count":2}]}"""),
                var u when u.EndsWith("/products/71") => (200, """{"result":{"variants":[{"id":4012,"size":"M","color":"Black","price":"9.50","in_stock":true}]}}"""),
                var u when u.Contains("/mockup-generator/printfiles/") => (200, """{"result":{"available_placements":{"front":"Vorderseite"}}}"""),
                var u when u.Contains("/create-task/") => (200, """{"result":{"task_key":"gt","status":"pending"}}"""),
                var u when u.Contains("/mockup-generator/task") => (200, """{"result":{"task_key":"gt","status":"completed","mockups":[{"mockup_url":"https://x/a.jpg"}]}}"""),
                var u when u.EndsWith("/store/products") => (200, """{"result":{"id":555}}"""),
                var u when u.EndsWith("/store/products/555") => (200, "{\"result\":{\"sync_variants\":[{\"id\":77,\"name\":\"M\",\"files\":[{\"status\":\"" + (FilesReady ? "ok" : "waiting") + "\"}]}]}}"),
                _ => (200, "{}"),
            };
            return Task.FromResult(new WebReply(c, Encoding.UTF8.GetBytes(s)));
        }
    }

    static (MainWindow w, Fake f, Trendo.Core.Programs.ProgramDocument d) Open()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1400;
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-tshirt")); Settle();
        var f = new Fake();
        w.PodWebOverride = f; w.PodDelayOverride = (_, _) => Task.CompletedTask; w.PodPngOverride = _ => [1, 2, 3]; w.PodMockupGap = TimeSpan.Zero;
        return (w, f, w.ProgramActive(K)!);
    }

    static async Task Ready(MainWindow w)
    {
        Assert.True(await w.PodPrintfulConnect("pf"));
        Assert.True(await w.PodNetlifyConnect("nf"));
        Assert.True(await w.PodLoadCatalog());
    }

    [AvaloniaFact(DisplayName = "Printful: Ungültiger Schlüssel wird nicht gespeichert, gültiger schon, Trennen löscht ihn")]
    public async Task Connect()
    {
        var (w, f, _) = Open();
        f.ScopeCode = 401;
        Assert.False(await w.PodPrintfulConnect("falsch")); Assert.False(w.PodPrintfulOn);
        f.ScopeCode = 200;
        Assert.False(await w.PodPrintfulConnect("  ")); Assert.False(w.PodPrintfulOn);
        Assert.True(await w.PodPrintfulConnect("gut")); Assert.True(w.PodPrintfulOn);
        w.PodPrintfulDisconnect(); Assert.False(w.PodPrintfulOn);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Printful: Mockups laden das Design hoch, erzeugen Bilder und leeren danach die Ablage")]
    public async Task Mockups()
    {
        var (w, f, d) = Open();
        await Ready(w);
        Assert.True(await w.PodChooseProduct(new(71, "Unisex Shirt", "T-Shirt", "", "", 1)));
        Assert.True(await w.PodMakeMockups(d));
        Assert.Equal(["https://x/a.jpg"], w.PodMockupUrls);
        Assert.Equal(2, f.Calls.Count(c => c.EndsWith("/deploys")));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Printful: Produkt anlegen leert die Ablage erst, wenn Printful die Datei hat; nie ein Bestellaufruf")]
    public async Task Create_product()
    {
        var (w, f, d) = Open();
        await Ready(w);
        await w.PodChooseProduct(new(71, "Unisex Shirt", "T-Shirt", "", "", 1));
        f.FilesReady = false;
        Assert.True(await w.PodCreateProduct(d, "Mein Shirt", 24.9m));
        Assert.Equal(555, w.PodLastProduct);
        Assert.Single(f.Calls, c => c.EndsWith("/deploys"));
        f.FilesReady = true;
        Assert.True(await w.PodCreateProduct(d, "Mein Shirt 2", 24.9m));
        Assert.Equal(4, f.Calls.Count(c => c.EndsWith("/deploys")));
        Assert.Equal("Mein Shirt 2", Assert.Single(w.PodShopItems.All()).Name); // die Nachbildung liefert beide Male dieselbe Produktnummer
        Assert.DoesNotContain(f.Calls, c => c.Contains("/orders"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Printful: Ohne Auswahl, Ablage, Name oder Preis passiert nichts")]
    public async Task Guards()
    {
        var (w, f, d) = Open();
        Assert.False(await w.PodMakeMockups(d));
        Assert.True(await w.PodPrintfulConnect("pf"));
        await w.PodChooseProduct(new(71, "Unisex Shirt", "T-Shirt", "", "", 1));
        Assert.False(await w.PodMakeMockups(d));
        Assert.DoesNotContain(f.Calls, c => c.Contains("/create-task/"));
        Assert.False(await w.PodCreateProduct(d, "", 10m));
        Assert.False(await w.PodCreateProduct(d, "X", 0m));
        Assert.False(await w.PodCreateProduct(d, "X", 12.345m));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Printful: Die Oberfläche bietet erst den Schlüssel, nach dem Verbinden den Katalog")]
    public async Task Panel()
    {
        var (w, _, _) = Open();
        Assert.NotNull(w.TryFind<Button>("pod-pf-get"));
        Assert.Null(w.TryFind<Button>("pod-pf-load"));
        // Ein langer Schlüssel darf das Feld nicht verbreitern, „Verbinden“ bleibt sichtbar
        var box = w.Find<TextBox>("pod-pf-token");
        box.Text = new string('x', 400); Settle();
        Assert.True(box.Bounds.Width <= 470, $"Feld ist {box.Bounds.Width} breit");
        Assert.True(w.Find<Button>("pod-pf-connect").Bounds.Width > 0);
        await Ready(w); Settle();
        Assert.Null(w.TryFind<Button>("pod-pf-get"));
        Assert.NotNull(w.TryFind<Button>("pod-pf-prod-71"));
        w.Close();
    }
}
