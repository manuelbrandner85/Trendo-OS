using System.Text;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

public class PrintifyTests
{
    sealed class Fake(params (int Code, string Body)[] replies) : IWebSend
    {
        public readonly List<(string Method, string Url, string? Body, IReadOnlyDictionary<string, string> Head)> Calls = [];
        int _i;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add((method, url, body is null ? null : Encoding.UTF8.GetString(body), headers));
            var (c, b) = replies[Math.Min(_i++, replies.Length - 1)];
            return Task.FromResult(new WebReply(c, Encoding.UTF8.GetBytes(b)));
        }
    }

    [Fact(DisplayName = "Printify: Shops und Katalog, Schlüssel und User-Agent werden mitgeschickt")]
    public async Task Shops_catalog()
    {
        var f = new Fake((200, """[{"id":7,"title":"Mein Shop"}]"""), (200, """[{"id":3,"title":"Shirt","brand":"B","images":["i.png"]}]"""));
        var c = new PrintifyClient(f, "geheim");
        Assert.Equal(7, Assert.Single(await c.ShopsAsync()).Id);
        Assert.Equal("i.png", Assert.Single(await c.CatalogAsync()).Image);
        Assert.Equal("Bearer geheim", f.Calls[0].Head["Authorization"]);
        Assert.True(f.Calls[0].Head.ContainsKey("User-Agent"));
        Assert.EndsWith("/shops.json", f.Calls[0].Url);
    }

    [Fact(DisplayName = "Printify: Druckereien und Varianten mit Druckplatz")]
    public async Task Providers_variants()
    {
        var f = new Fake((200, """[{"id":29,"title":"Druck GmbH"}]"""), (200, """{"id":29,"variants":[{"id":40,"title":"M / Schwarz","placeholders":[{"position":"back"}]}]}"""));
        var c = new PrintifyClient(f, "t");
        Assert.Equal(29, Assert.Single(await c.ProvidersAsync(3)).Id);
        var v = Assert.Single(await c.VariantsAsync(3, 29));
        Assert.Equal(("M / Schwarz", "back"), (v.Title, v.Position));
        Assert.EndsWith("/catalog/blueprints/3/print_providers/29/variants.json", f.Calls[1].Url);
    }

    [Fact(DisplayName = "Printify: Hochladen als Base64, zu große Dateien werden abgewiesen")]
    public async Task Upload()
    {
        var f = new Fake((200, """{"id":"img1"}"""));
        var c = new PrintifyClient(f, "t");
        Assert.Equal("img1", await c.UploadAsync("d.png", [1, 2]));
        Assert.Contains(Convert.ToBase64String([1, 2]), f.Calls[0].Body);
        await Assert.ThrowsAsync<PodServiceException>(() => c.UploadAsync("d.png", new byte[PrintifyClient.MaxBase64Bytes + 1]));
    }

    [Fact(DisplayName = "Printify: Produkt mit Preis in Cent, Druckplatz und Bild; Mockups werden gelesen")]
    public async Task Create_product()
    {
        var f = new Fake((200, """{"id":"p1","images":[{"src":"https://x/a.png"}]}"""));
        var p = await new PrintifyClient(f, "t").CreateProductAsync(7, "Shirt", 3, 29, [40, 41], 24.9m, "back", "img1");
        Assert.Equal("p1", p.Id);
        Assert.Equal(["https://x/a.png"], p.Images);
        Assert.Contains("\"price\":2490", f.Calls[0].Body);
        Assert.Contains("\"position\":\"back\"", f.Calls[0].Body);
        Assert.Contains("\"id\":\"img1\"", f.Calls[0].Body);
        Assert.EndsWith("/shops/7/products.json", f.Calls[0].Url);
        await Assert.ThrowsAsync<PodServiceException>(() => new PrintifyClient(f, "t").CreateProductAsync(7, "S", 3, 29, [], 1m, "front", "i"));
    }

    [Fact(DisplayName = "Printify: Bestellung trennt Vor- und Nachname, Fertigung ist ein eigener Aufruf, Doppelte werden verständlich gemeldet")]
    public async Task Order()
    {
        var f = new Fake((200, """{"id":"o1"}"""), (200, """{"id":"o1"}"""));
        var c = new PrintifyClient(f, "t");
        var order = new PodPaidOrder("cs_1", "pl", "E", new("Erika Muster", "Weg 1", "Ort", "12345", "DE", "c/o X"), "e@x");
        Assert.Equal("o1", await c.CreateOrderAsync(7, order, "p1", 40));
        Assert.Contains("\"first_name\":\"Erika\"", f.Calls[0].Body);
        Assert.Contains("\"last_name\":\"Muster\"", f.Calls[0].Body);
        Assert.Contains("\"address2\":\"c/o X\"", f.Calls[0].Body);
        Assert.DoesNotContain("send_to_production", f.Calls[0].Url);
        await c.SendToProductionAsync(7, "o1");
        Assert.EndsWith("/shops/7/orders/o1/send_to_production.json", f.Calls[1].Url);
        var dup = new PrintifyClient(new Fake((409, "{}")), "t");
        Assert.Contains("schon", (await Assert.ThrowsAsync<PodServiceException>(() => dup.CreateOrderAsync(7, order, "p1", 40))).Message);
    }
}
