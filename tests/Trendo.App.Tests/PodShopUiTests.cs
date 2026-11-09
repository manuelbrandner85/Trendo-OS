using System.Text;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Shop mit Printful und Stripe, ohne echte Konten.</summary>
public class PodShop_Ui
{
    sealed class Fake : IWebSend
    {
        public readonly List<string> Calls = [];
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add(method + " " + url);
            if (url.Contains("/orders/@")) return Task.FromResult(new WebReply(404, []));
            var s = url switch
            {
                var u when u.EndsWith("/prices") => """{"id":"price_1"}""",
                var u when u.EndsWith("/payment_links") => """{"id":"plink_1","url":"https://buy.stripe.com/x"}""",
                var u when u.Contains("/checkout/sessions") => """{"data":[{"id":"cs_1","livemode":true,"payment_status":"paid","payment_link":"plink_1","customer_details":{"name":"Erika","email":"e@x"},"shipping_details":{"name":"Erika M","address":{"line1":"Weg 1","city":"Ort","postal_code":"12345","country":"DE"}}}]}""",
                var u when u.EndsWith("/sites") => """{"id":"s2","ssl_url":"https://shop.netlify.app"}""",
                var u when u.EndsWith("/deploys") => """{"id":"d","state":"ready"}""",
                var u when u.EndsWith("/orders") => """{"result":{"id":99,"costs":{"total":"12.40"}}}""",
                _ => """{"result":{}}""",
            };
            return Task.FromResult(new WebReply(200, Encoding.UTF8.GetBytes(s)));
        }
    }

    static readonly PodShopLegal Legal = new("Erika Muster", "Weg 1", "e@example.org");

    static async Task<(MainWindow w, Fake f)> Ready()
    {
        var (w, _, _) = Ui.Open();
        var f = new Fake();
        w.PodWebOverride = f; w.PodDelayOverride = (_, _) => Task.CompletedTask;
        Assert.True(await w.PodStripeConnect("sk"));
        w.PodShopItems.Add(new("i", "Shirt", 24.9m, "", 5, [new("M", 9)]));
        return (w, f);
    }

    [AvaloniaFact(DisplayName = "POD-Shop: Bezahl-Links werden angelegt und nicht doppelt")]
    public async Task Pay_links()
    {
        var (w, f) = await Ready();
        Assert.True(await w.PodCreatePayLinks());
        Assert.True(await w.PodCreatePayLinks());
        Assert.Single(f.Calls, c => c.EndsWith("/payment_links"));
        Assert.Equal("https://buy.stripe.com/x", w.PodShopItems.All()[0].Variants[0].PayUrl);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "POD-Shop: Veröffentlichen verlangt geprüfte Rechtstexte und die Ablage")]
    public async Task Publish_needs_check()
    {
        var (w, f) = await Ready();
        Assert.False(await w.PodPublishShop("Shop", Legal, false));
        Assert.Contains("Rechtstexte", w.PodShopMessage);
        Assert.False(await w.PodPublishShop("Shop", Legal, true));
        Assert.DoesNotContain(f.Calls, c => c.EndsWith("/deploys"));
        Assert.True(await w.PodNetlifyConnect("nf"));
        Assert.True(await w.PodPublishShop("Shop", Legal, true));
        Assert.Contains("shop.netlify.app", w.PodShopMessage);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "POD-Shop: Bezahlte Bestellung wird Entwurf, nur ein eigener Klick gibt sie frei, doppelte Entwürfe werden abgewiesen")]
    public async Task Order_flow()
    {
        var (w, f) = await Ready();
        await w.PodCreatePayLinks();
        Assert.True(await w.PodPrintfulConnect("pf"));
        var items = w.PodShopItems.All()[0];
        w.PodShopItems.Replace(items with { Variants = [items.Variants[0] with { PayId = "plink_1" }] });
        Assert.True(await w.PodFetchOrders());
        var order = Assert.Single(w.PodShopOrders);
        Assert.True(await w.PodDraftOrder(order));
        Assert.DoesNotContain(f.Calls, c => c.Contains("/confirm"));
        Assert.False(await w.PodDraftOrder(order));
        Assert.Single(f.Calls, c => c.EndsWith("/orders"));
        Assert.True(await w.PodConfirmOrder(order));
        Assert.Contains(f.Calls, c => c.EndsWith("/orders/99/confirm"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "POD-Shop: Testzahlung wird nie an Printful gegeben; Entwurf übersteht einen Neustart")]
    public async Task Test_payment_and_restart()
    {
        var (w, f) = await Ready();
        Assert.True(await w.PodPrintfulConnect("pf"));
        var item = w.PodShopItems.All()[0];
        w.PodShopItems.Replace(item with { Variants = [item.Variants[0] with { PayId = "plink_1" }] });
        var test = new PodPaidOrder("cs_t", "plink_1", "A", new("A", "B", "C", "1", "DE"), "e@x", Live: false);
        Assert.False(await w.PodDraftOrder(test));
        Assert.Contains("Testzahlung", w.PodShopMessage);
        Assert.DoesNotContain(f.Calls, c => c.EndsWith("/orders"));

        var real = test with { SessionId = "cs_r", Live = true };
        Assert.True(await w.PodDraftOrder(real));
        Assert.Equal(99, new PodOrderStore(w.DataRootForTests).All()["cs_r"].PrintfulId);
        Assert.False(await w.PodDraftOrder(real));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "POD-Shop: Fremde Zahlung ergibt keine Bestellung")]
    public async Task Unknown_payment()
    {
        var (w, f) = await Ready();
        Assert.True(await w.PodPrintfulConnect("pf"));
        Assert.False(await w.PodDraftOrder(new("cs_x", "plink_fremd", "A", new("A", "B", "C", "1", "DE"), "e@x")));
        Assert.DoesNotContain(f.Calls, c => c.EndsWith("/orders"));
        w.Close();
    }
}
