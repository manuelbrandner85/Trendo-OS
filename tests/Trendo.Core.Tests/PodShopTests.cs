using System.Text;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

public class PodShopTests
{
    sealed class Fake(params (int Code, string Body)[] replies) : IWebSend
    {
        public readonly List<(string Method, string Url, string? Body)> Calls = [];
        int _i;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add((method, url, body is null ? null : Encoding.UTF8.GetString(body)));
            var (c, b) = replies[Math.Min(_i++, replies.Length - 1)];
            return Task.FromResult(new WebReply(c, Encoding.UTF8.GetBytes(b)));
        }
    }

    static readonly PodShopLegal Legal = new("Erika Muster", "Musterweg 1\n12345 Musterstadt", "shop@example.org");

    [Fact(DisplayName = "POD-Shop: Seiten enthalten Produkt, Bezahl-Link, Preisangabe und alle Pflichtseiten, Eingaben werden entschärft")]
    public void Site()
    {
        var items = new[] { new PodShopItem("1", "<b>Shirt</b>", 24.9m, "https://x/a.jpg", 5, [new("M", 9, "https://buy.stripe.com/x", "plink_1"), new("L", 10)]) };
        var pages = PodShopSite.Build("Mein Shop", items, Legal, false);
        Assert.Equal(["index.html", "impressum.html", "agb.html", "widerruf.html", "datenschutz.html"], pages.Keys);
        var html = pages["index.html"];
        Assert.Contains("https://buy.stripe.com/x", html);
        Assert.Contains("zahlungspflichtig bestellen", html);
        Assert.Contains("24,90 € inkl. MwSt., zzgl. Versand", html);
        Assert.DoesNotContain("<b>Shirt</b>", html);
        Assert.Contains("Erika Muster", pages["impressum.html"]);
        Assert.Contains("19 UStG", PodShopSite.Build("S", items, Legal, true)["index.html"]);
    }

    [Theory(DisplayName = "POD-Shop: Ohne vollständige Angaben entsteht kein Shop")]
    [InlineData("", "A", "a@b")]
    [InlineData("N", "", "a@b")]
    [InlineData("N", "A", "keine")]
    public void Legal_required(string o, string a, string m) => Assert.Throws<PodServiceException>(() => PodShopSite.Build("S", [], new(o, a, m), false));

    [Fact(DisplayName = "POD-Shop: Liste speichert, ersetzt gleiche Produkte und entfernt")]
    public void Store()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-podshop-" + Guid.NewGuid().ToString("N"));
        try
        {
            var s = new PodShopStore(d);
            s.Add(new("a", "X", 1m, "", 7, [])); s.Add(new("b", "Y", 2m, "", 7, []));
            Assert.Equal("Y", Assert.Single(s.All()).Name);
            s.Remove("b"); Assert.Empty(s.All());
        }
        finally { if (Directory.Exists(d)) Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Stripe: Bezahl-Link legt Preis in Cent an und fragt die Lieferadresse ab")]
    public async Task Pay_link()
    {
        var f = new Fake((200, """{"id":"price_1"}"""), (200, """{"id":"plink_1","url":"https://buy.stripe.com/x"}"""));
        Assert.Equal(("plink_1", "https://buy.stripe.com/x"), await new StripeClient(f, "k").PayLinkAsync("Shirt M", 24.9m));
        Assert.Contains("unit_amount=2490", f.Calls[0].Body);
        Assert.Contains("currency=eur", f.Calls[0].Body);
        Assert.Contains("price_1", f.Calls[1].Body);
        Assert.Contains(Uri.EscapeDataString("shipping_address_collection[allowed_countries][0]") + "=DE", f.Calls[1].Body);
    }

    [Fact(DisplayName = "Stripe: Nur bezahlte Sitzungen zählen, mit Lieferadresse; Fehler sind verständlich")]
    public async Task Paid_orders()
    {
        var f = new Fake((200, """{"data":[{"id":"cs_1","payment_status":"paid","payment_link":"plink_1","customer_details":{"name":"Erika","email":"e@x"},"collected_information":{"shipping_details":{"name":"Erika M","address":{"line1":"Weg 1","city":"Ort","postal_code":"12345","country":"DE"}}}},{"id":"cs_2","payment_status":"unpaid"}]}"""));
        var o = Assert.Single(await new StripeClient(f, "k").PaidOrdersAsync());
        Assert.Equal(("cs_1", "plink_1", "Ort"), (o.SessionId, o.PayLinkId, o.Address!.City));
        await Assert.ThrowsAsync<PodServiceException>(() => new StripeClient(new Fake((401, "{}")), "k").CheckAsync());
    }

    [Fact(DisplayName = "Printful-Bestellung: Anlegen ist ein Entwurf ohne Freigabe; Freigabe ist ein eigener Aufruf; ohne Adresse keine Bestellung")]
    public async Task Draft_then_confirm()
    {
        var f = new Fake((200, """{"result":{"id":99}}"""), (200, """{"result":{"id":99}}"""));
        var o = new PrintfulOrders(f, "t");
        var order = new PodPaidOrder("cs_test_abc", "plink", "Erika", new("Erika M", "Weg 1", "Ort", "12345", "DE"), "e@x");
        Assert.Equal(99, await o.CreateDraftAsync(order, 7));
        Assert.EndsWith("/orders", f.Calls[0].Url);
        Assert.DoesNotContain("confirm", f.Calls[0].Url);
        Assert.Contains("\"sync_variant_id\":7", f.Calls[0].Body);
        await o.ConfirmAsync(99);
        Assert.EndsWith("/orders/99/confirm", f.Calls[1].Url);
        await Assert.ThrowsAsync<PodServiceException>(() => o.CreateDraftAsync(order with { Address = null }, 7));
    }
}
