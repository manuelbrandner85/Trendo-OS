using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Pod;
using IoPath = System.IO.Path;

namespace Trendo.App;

/// <summary>Shop im Studio „Online verkaufen“: Produkte aus Printful, Bezahl-Links über das eigene Stripe-Konto, Pflichtseiten, Veröffentlichen und Bestellungen als Entwurf mit Freigabe je Bestellung.</summary>
public partial class MainWindow
{
    const string StKey = "pod-stripe-key", ShopSite = "pod-shop-site", ShopUrl = "pod-shop-url";
    string _shopMsg = "", _shopArmed = "";
    bool _shopBusy, _shopChecked;
    IReadOnlyList<PodPaidOrder> _shopOrders = [];
    readonly Dictionary<string, string> _shopField = [];

    public string PodShopMessage => _shopMsg;
    public IReadOnlyList<PodPaidOrder> PodShopOrders => _shopOrders;
    public bool PodStripeOn => PodSecret(StKey) is not null;
    public PodShopStore PodShopItems => new(_core.DataRoot);
    public string DataRootForTests => _core.DataRoot;
    PodOrderStore ShopOrderStore => new(_core.DataRoot);
    string ShopFolder => IoPath.Combine(_core.DataRoot, "print-on-demand", "shop-seite");

    async Task<bool> ShopRun(Func<Task> work)
    {
        if (_shopBusy) return false;
        _shopBusy = true;
        try { _shopMsg = ""; await work(); return true; }
        catch (PodServiceException e) { _shopMsg = e.Message; return false; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException) { _shopMsg = "Keine Verbindung. Ist das Internet erreichbar?"; return false; }
        finally { _shopBusy = false; Show(CurrentView); }
    }

    public Task<bool> PodStripeConnect(string key) => ShopRun(async () =>
    {
        key = key.Trim();
        if (key.Length == 0) throw new PodServiceException("Füge zuerst deinen Stripe-Schlüssel ein.");
        await new StripeClient(PodWeb, key).CheckAsync();
        _core.Secrets.Set(StKey, key);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.stripe", "verbunden");
        _shopMsg = key.StartsWith("sk_test", StringComparison.Ordinal) ? "Stripe ist im Testmodus verbunden. Testzahlungen werden nie an Printful gegeben." : "Stripe ist verbunden.";
    });

    void PodStripeDisconnect()
    {
        _core.Secrets.Delete(StKey);
        _shopOrders = [];
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.stripe", "getrennt");
        Show(CurrentView);
    }

    StripeClient Stripe() => new(PodWeb, PodSecret(StKey) ?? throw new PodServiceException("Verbinde zuerst dein Stripe-Konto."));
    PrintfulOrders PfOrders() => new(PodWeb, PodSecret(PfKey) ?? throw new PodServiceException("Verbinde zuerst dein Printful-Konto."));

    /// <summary>Legt für alle Varianten ohne Link einen Bezahl-Link an.</summary>
    public Task<bool> PodCreatePayLinks() => ShopRun(async () =>
    {
        var s = Stripe(); var store = PodShopItems; var made = 0;
        foreach (var item in store.All())
        {
            var variants = new List<PodShopVariant>();
            foreach (var v in item.Variants)
            {
                if (v.PayUrl is { Length: > 0 }) { variants.Add(v); continue; }
                var (id, url) = await s.PayLinkAsync($"{item.Name} {v.Name}".Trim(), item.Price);
                variants.Add(v with { PayUrl = url, PayId = id }); made++;
            }
            store.Replace(item with { Variants = variants });
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.stripe", $"{made} bezahl-links");
        _shopMsg = $"{made} Bezahl-Links angelegt.";
    });

    /// <summary>Schreibt den Shop als Webseiten in den eigenen Ordner.</summary>
    public IReadOnlyDictionary<string, string> PodBuildShop(string shopName, PodShopLegal legal)
    {
        var pages = PodShopSite.Build(shopName, PodShopItems.All(), legal, _settings.SmallBusiness);
        Directory.CreateDirectory(ShopFolder);
        foreach (var (name, html) in pages) File.WriteAllText(IoPath.Combine(ShopFolder, name), html);
        return pages;
    }

    /// <summary>Veröffentlicht den Shop im eigenen Netlify-Konto. Nur nach Bestätigung, dass die Rechtstexte geprüft sind.</summary>
    public Task<bool> PodPublishShop(string shopName, PodShopLegal legal, bool legalChecked) => ShopRun(async () =>
    {
        if (!legalChecked) throw new PodServiceException("Bestätige zuerst, dass du die Rechtstexte geprüft hast.");
        if (PodSecret(NfKey) is not { } token) throw new PodServiceException("Richte zuerst im POD-Studio die Ablage bei Netlify ein.");
        var pages = PodBuildShop(shopName, legal);
        var host = new NetlifyDesignHost(PodWeb, token);
        if (PodSecret(ShopSite) is null) { var (id, url) = await host.CreateSiteAsync(); _core.Secrets.Set(ShopSite, id); _core.Secrets.Set(ShopUrl, url); }
        await host.DeployAsync(PodSecret(ShopSite)!, NetlifyDesignHost.Zip(pages.Select(p => (p.Key, System.Text.Encoding.UTF8.GetBytes(p.Value)))), PodWait);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.shop", "veröffentlicht");
        _shopMsg = "Der Shop ist online: " + PodSecret(ShopUrl);
    });

    public Task<bool> PodFetchOrders() => ShopRun(async () =>
    {
        var s = Stripe();
        _shopOrders = await s.PaidOrdersAsync();
        _shopMsg = $"{_shopOrders.Count} bezahlte Bestellungen." + (s.HasMore ? " Ältere Zahlungen findest du im Stripe-Konto." : "");
    });

    (PodShopItem Item, PodShopVariant Variant)? ShopVariantFor(PodPaidOrder o) => PodShopItems.All().SelectMany(i => i.Variants.Select(v => (i, v))).Where(x => x.v.PayId == o.PayLinkId).Select(x => ((PodShopItem, PodShopVariant)?)x).FirstOrDefault();

    /// <summary>Legt zu einer echten, bezahlten Bestellung einen kostenlosen Entwurf bei Printful an. Prüft vorher, ob es zu dieser Zahlung schon einen gibt.</summary>
    public Task<bool> PodDraftOrder(PodPaidOrder order) => ShopRun(async () =>
    {
        if (!order.Live) throw new PodServiceException("Das ist eine Testzahlung. Sie wird nicht an Printful gegeben.");
        if (ShopOrderStore.All().ContainsKey(order.SessionId)) throw new PodServiceException("Zu dieser Zahlung gibt es schon einen Entwurf.");
        var (item, v) = ShopVariantFor(order) ?? throw new PodServiceException("Diese Zahlung gehört zu keinem Produkt aus dem Shop.");
        if (item.Provider == "printify")
        {
            if (!PodPrintifyManual) throw new PodServiceException("Bestätige im Studio „Print on Demand“, dass dein Printify-Shop auf manuelle Freigabe steht. Sonst startet Printify die Fertigung selbst.");
            var remote = await Pfy().CreateOrderAsync(PfyShopId(), order, item.RemoteProduct, v.SyncId);
            ShopOrderStore.Set(order.SessionId, new(0, false, null, remote));
            _core.Audit.Append(Principal.LocalOwner.Name, "pod.bestellung", "printify angelegt");
            _shopMsg = "Bestellung bei Printify angelegt. Gefertigt wird erst nach deiner Freigabe.";
            return;
        }
        var orders = PfOrders();
        if (await orders.FindAsync(order.SessionId) is { } known)
        {
            ShopOrderStore.Set(order.SessionId, new(known.Draft.Id, known.Confirmed, known.Draft.Cost));
            _shopMsg = "Diese Bestellung gab es bei Printful schon, sie ist wieder verknüpft.";
            return;
        }
        var draft = await orders.CreateDraftAsync(order, v.SyncId);
        ShopOrderStore.Set(order.SessionId, new(draft.Id, false, draft.Cost));
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.bestellung", "entwurf " + draft.Id);
        _shopMsg = "Entwurf angelegt. Kosten entstehen erst, wenn du ihn freigibst.";
    });

    /// <summary>Gibt einen Entwurf frei. Das kostet Geld und läuft nur über die zweistufige Bestätigung in der Oberfläche.</summary>
    public Task<bool> PodConfirmOrder(PodPaidOrder order) => ShopRun(async () =>
    {
        if (!ShopOrderStore.All().TryGetValue(order.SessionId, out var st)) throw new PodServiceException("Zu dieser Zahlung gibt es keinen Entwurf.");
        if (st.Confirmed) throw new PodServiceException("Diese Bestellung ist schon freigegeben.");
        if (st.RemoteId is { } remote) await Pfy().SendToProductionAsync(PfyShopId(), remote);
        else await PfOrders().ConfirmAsync(st.PrintfulId);
        ShopOrderStore.Set(order.SessionId, st with { Confirmed = true });
        _shopArmed = "";
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.bestellung", "freigegeben");
        _shopMsg = "Bestellung ist zur Fertigung freigegeben.";
    });

    Button ShopBtn(string name, string text, Func<Task> act, bool accent = false)
    {
        var b = new Button { Name = name, Content = text, Padding = new Thickness(10, 4), IsEnabled = !_shopBusy };
        if (accent) b.Classes.Add("accent");
        b.Click += async (_, _) => await act();
        return b;
    }

    TextBox ShopField(string name, string hint, bool multi = false)
    {
        var t = new TextBox { Name = name, Text = _shopField.GetValueOrDefault(name, ""), PlaceholderText = hint, AcceptsReturn = multi, MinHeight = multi ? 48 : double.NaN, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        t.TextChanged += (_, _) => _shopField[name] = t.Text ?? "";
        return t;
    }

    Control PodShopCard()
    {
        var sp = new StackPanel { Name = "shop-pod", Spacing = 6 };
        if (_shopMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "shop-msg", Text = _shopMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var items = PodShopItems.All();
        foreach (var i in items)
            sp.Children.Add(P($"{i.Name} · {i.Price:0.00} € inkl. Versand · {i.Variants.Count(v => v.PayUrl is { Length: > 0 })} von {i.Variants.Count} Varianten bestellbar", Text, 12));
        if (items.Count == 0) sp.Children.Add(P("Noch keine Produkte. Lege sie im Studio „Print on Demand“ über Printful an.", Muted, 12));

        if (!PodStripeOn)
        {
            var key = new TextBox { Name = "shop-st-key", PasswordChar = '•', PlaceholderText = "Stripe-Schlüssel einfügen", MinWidth = 280 };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ShopBtn("shop-st-get", "Schlüssel bei Stripe holen", () => { Launch(StripeClient.KeyPage); return Task.CompletedTask; }), key, ShopBtn("shop-st-connect", "Verbinden", () => PodStripeConnect(key.Text ?? ""), true) } });
            return sp;
        }

        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ShopBtn("shop-paylinks", "Bezahl-Links anlegen", PodCreatePayLinks, true), ShopBtn("shop-st-off", "Stripe trennen", () => { PodStripeDisconnect(); return Task.CompletedTask; }) } });
        sp.Children.Add(H("Angaben für Impressum und Rechtstexte", 13));
        var shop = ShopField("shop-name", "Name des Shops");
        var owner = ShopField("shop-owner", "Dein Name oder deine Firma");
        var addr = ShopField("shop-address", "Straße und Hausnummer, PLZ und Ort", true);
        var mail = ShopField("shop-email", "E-Mail");
        var vat = ShopField("shop-vat", "Umsatzsteuer-ID, falls vorhanden");
        sp.Children.Add(shop); sp.Children.Add(owner); sp.Children.Add(addr); sp.Children.Add(mail); sp.Children.Add(vat);
        PodShopLegal Legal() => new(owner.Text ?? "", addr.Text ?? "", mail.Text ?? "", vat.Text ?? "");
        var check = new CheckBox { Name = "shop-legal-ok", IsChecked = _shopChecked, Content = new TextBlock { Text = "Die Rechtstexte sind Vorlagen und keine Rechtsberatung. Ich habe sie geprüft oder prüfen lassen.", TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 12 } };
        check.IsCheckedChanged += (_, _) => _shopChecked = check.IsChecked == true;
        sp.Children.Add(check);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            ShopBtn("shop-build", "Vorschau speichern", () => ShopRun(() => { var pages = PodBuildShop(shop.Text is { Length: > 0 } n ? n : "Mein Shop", Legal()); _shopMsg = $"{pages.Count} Seiten gespeichert."; Launch(IoPath.Combine(ShopFolder, "index.html")); return Task.CompletedTask; })),
            ShopBtn("shop-publish", "Shop veröffentlichen", () => PodPublishShop(shop.Text is { Length: > 0 } n ? n : "Mein Shop", Legal(), check.IsChecked == true), true) } });

        sp.Children.Add(H("Bestellungen", 13));
        sp.Children.Add(ShopBtn("shop-orders", "Bezahlte Bestellungen abrufen", PodFetchOrders));
        var states = ShopOrderStore.All();
        foreach (var o in _shopOrders)
        {
            var order = o;
            // Nur Kennzeichen und Land: Namen und Adressen sollen nicht im Seitentext stehen
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = $"Bestellung {o.Code} · {o.Address?.Country}" + (o.Live ? "" : " · Testzahlung"), Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
            if (!o.Live) { }
            else if (!states.TryGetValue(o.SessionId, out var st)) row.Children.Add(ShopBtn("shop-draft-" + o.Code, "Entwurf bei Printful anlegen", () => PodDraftOrder(order)));
            else if (st.Confirmed) row.Children.Add(new TextBlock { Text = "✓ freigegeben", Foreground = Brush.Parse("#3DDC84"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            else if (_shopArmed != o.SessionId)
                row.Children.Add(ShopBtn("shop-arm-" + o.Code, $"Bestellung {o.Code} zur Fertigung freigeben (kostet Geld)", () => { _shopArmed = order.SessionId; Show(CurrentView); return Task.CompletedTask; }));
            else
            {
                row.Children.Add(ShopBtn("shop-disarm-" + o.Code, "Abbrechen", () => { _shopArmed = ""; Show(CurrentView); return Task.CompletedTask; }));
                row.Children.Add(new TextBlock { Text = "Wirklich?", Foreground = Brush.Parse("#F5A524"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(ShopBtn("shop-confirm-" + o.Code, $"Ja, kostenpflichtig fertigen: {o.Code}" + (st.Cost is { } c ? $" für {c:0.00}" : ""), () => PodConfirmOrder(order), true));
            }
            sp.Children.Add(row);
        }
        return sp;
    }
}
