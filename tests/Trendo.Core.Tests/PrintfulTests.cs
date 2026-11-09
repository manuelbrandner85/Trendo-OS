using System.IO.Compression;
using System.Text;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

public class PrintfulTests
{
    sealed class Fake(params (int Code, string Body)[] replies) : IWebSend
    {
        public readonly List<(string Method, string Url, string? Body, string? Auth, string? Type)> Calls = [];
        int _i;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add((method, url, body is null ? null : Encoding.UTF8.GetString(body), headers.GetValueOrDefault("Authorization"), contentType));
            var (c, b) = replies[Math.Min(_i++, replies.Length - 1)];
            return Task.FromResult(new WebReply(c, Encoding.UTF8.GetBytes(b)));
        }
    }

    [Fact(DisplayName = "Printful: Katalog liest Produkte, lässt Auslaufmodelle weg und schickt den eigenen Schlüssel")]
    public async Task Catalog()
    {
        var f = new Fake((200, """{"code":200,"result":[{"id":71,"type_name":"T-Shirt","title":"Unisex Shirt","brand":"B","image":"i.jpg","variant_count":3},{"id":9,"title":"Alt","is_discontinued":true}]}"""));
        var list = await new PrintfulClient(f, "geheim").CatalogAsync();
        var p = Assert.Single(list);
        Assert.Equal((71, "Unisex Shirt", "T-Shirt", 3), (p.Id, p.Title, p.Type, p.Variants));
        Assert.Equal("Bearer geheim", f.Calls[0].Auth);
        Assert.Equal("https://api.printful.com/products", f.Calls[0].Url);
    }

    [Fact(DisplayName = "Printful: Varianten mit Preis als Text, Druckplätze")]
    public async Task Variants_and_placements()
    {
        var f = new Fake((200, """{"result":{"product":{},"variants":[{"id":4012,"name":"Shirt / M","size":"M","color":"Black","price":"9.50","in_stock":false}]}}"""),
                         (200, """{"result":{"available_placements":{"front":"Vorderseite","back":"Rückseite"}}}"""));
        var c = new PrintfulClient(f, "t");
        var v = Assert.Single(await c.VariantsAsync(71));
        Assert.Equal(9.50m, v.Price); Assert.False(v.InStock);
        Assert.Equal(["front", "back"], (await c.PlacementsAsync(71)).Select(p => p.Key));
    }

    [Fact(DisplayName = "Printful: Mockup-Auftrag sendet Variante, Platz und Design-Adresse; Ergebnis liest alle Bildadressen")]
    public async Task Mockup()
    {
        var f = new Fake((200, """{"result":{"task_key":"gt-1","status":"pending"}}"""),
                         (200, """{"result":{"task_key":"gt-1","status":"completed","mockups":[{"placement":"front","mockup_url":"https://x/a.jpg","extra":[{"title":"Seite","url":"https://x/b.jpg"}]}]}}"""));
        var c = new PrintfulClient(f, "t");
        var t = await c.StartMockupAsync(71, [4012], "front", "https://d/x.png");
        Assert.False(t.Done);
        Assert.Contains("\"variant_ids\":[4012]", f.Calls[0].Body);
        Assert.Contains("\"image_url\":\"https://d/x.png\"", f.Calls[0].Body);
        Assert.EndsWith("/mockup-generator/create-task/71", f.Calls[0].Url);
        var done = await c.MockupAsync("gt-1");
        Assert.True(done.Done);
        Assert.Equal(["https://x/a.jpg", "https://x/b.jpg"], done.Urls);
    }

    [Theory(DisplayName = "Printful: Fehler werden verständlich gemeldet")]
    [InlineData(401, "ungültig")]
    [InlineData(403, "Berechtigung")]
    [InlineData(429, "Pause")]
    [InlineData(400, "Kaputt")]
    public async Task Errors(int code, string text)
    {
        var f = new Fake((code, """{"code":400,"result":"x","error":{"reason":"r","message":"Kaputt"}}"""));
        var ex = await Assert.ThrowsAsync<PodServiceException>(() => new PrintfulClient(f, "t").CheckAsync());
        Assert.Contains(text, ex.Message);
    }

    [Fact(DisplayName = "Printful: Produkt anlegen sendet Verkaufspreis und Datei, keine Variante wird abgelehnt")]
    public async Task Create_product()
    {
        var f = new Fake((200, """{"result":{"id":123}}"""));
        var c = new PrintfulClient(f, "t");
        Assert.Equal(123, await c.CreateProductAsync("Mein Shirt", [(4012, 24.9m)], "https://d/x.png"));
        Assert.Equal("POST", f.Calls[0].Method);
        Assert.EndsWith("/store/products", f.Calls[0].Url);
        Assert.Contains("\"retail_price\":\"24.90\"", f.Calls[0].Body);
        await Assert.ThrowsAsync<PodServiceException>(() => c.CreateProductAsync("Leer", [], "https://d/x.png"));
    }

    [Fact(DisplayName = "Printful: Der Client kennt keinen Bestellaufruf")]
    public void No_order_call() => Assert.DoesNotContain(typeof(PrintfulClient).GetMethods(), m => m.Name.Contains("Order", StringComparison.OrdinalIgnoreCase));

    [Fact(DisplayName = "Netlify: Ablage anlegen, Datei bereitstellen und auf Fertig warten")]
    public async Task Netlify_flow()
    {
        var f = new Fake((200, """{"id":"s1","ssl_url":"https://a.netlify.app"}"""), (200, """{"id":"d1","state":"uploading"}"""), (200, """{"state":"ready"}"""));
        var h = new NetlifyDesignHost(f, "n");
        Assert.Equal(("s1", "https://a.netlify.app"), await h.CreateSiteAsync());
        await h.DeployAsync("s1", NetlifyDesignHost.Zip([]), (_, _) => Task.CompletedTask);
        Assert.Equal("application/zip", f.Calls[1].Type);
        Assert.EndsWith("/deploys/d1", f.Calls[2].Url);
    }

    [Fact(DisplayName = "Netlify: Zeitüberschreitung und Fehler werden gemeldet")]
    public async Task Netlify_errors()
    {
        var slow = new Fake((200, """{"id":"d","state":"uploading"}"""));
        await Assert.ThrowsAsync<PodServiceException>(() => new NetlifyDesignHost(slow, "n").DeployAsync("s", [], (_, _) => Task.CompletedTask));
        var bad = new Fake((401, "{}"));
        Assert.Contains("ungültig", (await Assert.ThrowsAsync<PodServiceException>(() => new NetlifyDesignHost(bad, "n").CreateSiteAsync())).Message);
    }

    [Fact(DisplayName = "Netlify: Zip enthält Startseite und Dateien, leere Liste leert die Ablage; Dateinamen sind nicht erratbar")]
    public void Zip_and_names()
    {
        using var zip = new ZipArchive(new MemoryStream(NetlifyDesignHost.Zip([("a.png", [1, 2])])));
        Assert.Equal(["index.html", "a.png"], zip.Entries.Select(e => e.FullName));
        using var empty = new ZipArchive(new MemoryStream(NetlifyDesignHost.Zip([])));
        Assert.Single(empty.Entries);
        Assert.NotEqual(NetlifyDesignHost.RandomName(), NetlifyDesignHost.RandomName());
        Assert.Equal(36, NetlifyDesignHost.RandomName().Length);
    }
}
