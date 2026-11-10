using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Printify im POD-Studio und im Shop, ohne echtes Konto.</summary>
public class PodPrintify_Ui
{
    const string K = "pod-programm";

    sealed class Fake : IWebSend
    {
        public readonly List<string> Calls = [];
        public int ShopCode = 200;
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            Calls.Add(method + " " + url);
            var (c, s) = url switch
            {
                var u when u.EndsWith("/shops.json") => (ShopCode, """[{"id":7,"title":"Mein Shop"}]"""),
                var u when u.EndsWith("/catalog/blueprints.json") => (200, """[{"id":3,"title":"Shirt"}]"""),
                var u when u.EndsWith("/print_providers.json") => (200, """[{"id":29,"title":"Druck"}]"""),
                var u when u.EndsWith("/variants.json") => (200, """{"variants":[{"id":40,"title":"M","placeholders":[{"position":"front"}]}]}"""),
                var u when u.EndsWith("/uploads/images.json") => (200, """{"id":"img"}"""),
                var u when u.EndsWith("/products.json") => (200, """{"id":"p1","images":[{"src":"https://x/a.png"}]}"""),
                var u when u.EndsWith("/orders.json") => (200, """{"id":"o1"}"""),
                var u when u.EndsWith("/prices") => (200, """{"id":"price_1"}"""),
                _ => (200, "{}"),
            };
            return Task.FromResult(new WebReply(c, Encoding.UTF8.GetBytes(s)));
        }
    }

    static async Task<(MainWindow w, Fake f, Trendo.Core.Programs.ProgramDocument d)> Ready()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1400;
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-tshirt")); Settle();
        var f = new Fake();
        w.PodWebOverride = f; w.PodDelayOverride = (_, _) => Task.CompletedTask; w.PodPngOverride = _ => [1, 2, 3];
        Assert.True(await w.PodPrintifyConnect("t"));
        Assert.True(await w.PodPrintifyCatalog());
        Assert.True(await w.PodPrintifyChoose(new(3, "Shirt", "", "")));
        return (w, f, w.ProgramActive(K)!);
    }

    [AvaloniaFact(DisplayName = "Printify: Ohne gültigen Schlüssel wird nichts gespeichert")]
    public async Task Connect_rejected()
    {
        var (w, _, _) = Ui.Open();
        var f = new Fake { ShopCode = 401 }; w.PodWebOverride = f;
        Assert.False(await w.PodPrintifyConnect("falsch"));
        Assert.False(w.PodPrintifyOn);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Printify: Produkt anlegen zeigt Mockups und steht danach im Shop; kein Bestellaufruf")]
    public async Task Create()
    {
        var (w, f, d) = await Ready();
        Assert.True(await w.PodPrintifyCreate(d, "Mein Shirt", 24.9m));
        Assert.Equal(["https://x/a.png"], w.PodPrintifyMockups);
        var item = Assert.Single(w.PodShopItems.All());
        Assert.Equal(("printify", "p1"), (item.Provider, item.RemoteProduct));
        Assert.DoesNotContain(f.Calls, c => c.EndsWith("/orders.json"));
        Assert.False(await w.PodPrintifyCreate(d, "", 10m));
        Assert.False(await w.PodPrintifyCreate(d, "X", 12.345m));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Printify: Ohne bestätigte manuelle Freigabe entsteht keine Bestellung; mit ihr erst Anlegen, dann ein eigener Fertigungsaufruf")]
    public async Task Order_gate()
    {
        var (w, f, d) = await Ready();
        await w.PodPrintifyCreate(d, "Mein Shirt", 24.9m);
        var item = w.PodShopItems.All()[0];
        w.PodShopItems.Replace(item with { Variants = [item.Variants[0] with { PayId = "plink" }] });
        var order = new PodPaidOrder("cs_1", "plink", "E", new("Erika Muster", "Weg", "Ort", "1", "DE"), "e@x");

        Assert.False(await w.PodDraftOrder(order));
        Assert.Contains("manuelle Freigabe", w.PodShopMessage);
        Assert.DoesNotContain(f.Calls, c => c.EndsWith("/orders.json"));

        w.PodPrintifySetManual(true);
        Assert.True(await w.PodDraftOrder(order));
        Assert.Single(f.Calls, c => c.EndsWith("/orders.json"));
        Assert.DoesNotContain(f.Calls, c => c.Contains("send_to_production"));
        Assert.True(await w.PodConfirmOrder(order));
        Assert.Contains(f.Calls, c => c.EndsWith("/orders/o1/send_to_production.json"));
        Assert.False(await w.PodConfirmOrder(order));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Printify: Die Oberfläche bietet erst den Schlüssel, danach Katalog und die Freigabe-Bestätigung")]
    public async Task Panel()
    {
        var (w, _, _) = await Ready();
        Assert.Null(w.TryFind<Button>("pod-pfy-get"));
        Assert.NotNull(w.TryFind<CheckBox>("pod-pfy-manual"));
        Assert.NotNull(w.TryFind<Button>("pod-pfy-prod-3"));
        w.PodPrintifyDisconnect(); Settle();
        Assert.False(w.PodPrintifyOn);
        Assert.NotNull(w.TryFind<Button>("pod-pfy-get"));
        w.Close();
    }
}
