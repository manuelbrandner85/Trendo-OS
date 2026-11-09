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
    string _shopMsg = "";
    bool _shopBusy, _shopChecked;
    IReadOnlyList<PodPaidOrder> _shopOrders = [];
    readonly Dictionary<string, long> _shopDrafts = [];
    readonly HashSet<long> _shopConfirmed = [];

    public string PodShopMessage => _shopMsg;
    public IReadOnlyList<PodPaidOrder> PodShopOrders => _shopOrders;
    public bool PodStripeOn => PodSecret(StKey) is not null;
    public PodShopStore PodShopItems => new(_core.DataRoot);
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
        _shopMsg = "Stripe ist verbunden.";
    });

    StripeClient Stripe() => new(PodWeb, PodSecret(StKey) ?? throw new PodServiceException("Verbinde zuerst dein Stripe-Konto."));

    /// <summary>Legt für alle Varianten ohne Link einen Bezahl-Link an.</summary>
    public Task<bool> PodCreatePayLinks() => ShopRun(async () =>
    {
        var s = Stripe(); var store = PodShopItems; var made = 0;
        foreach (var item in store.All())
        {
            var variants = new List<PodShopVariant>();
            foreach (var v in item.Variants)
            {
                if (v.PayUrl is not null) { variants.Add(v); continue; }
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

    public Task<bool> PodFetchOrders() => ShopRun(async () => { _shopOrders = await Stripe().PaidOrdersAsync(); _shopMsg = $"{_shopOrders.Count} bezahlte Bestellungen."; });

    PodShopVariant? ShopVariantFor(PodPaidOrder o) => PodShopItems.All().SelectMany(i => i.Variants).FirstOrDefault(v => v.PayId == o.PayLinkId);

    /// <summary>Legt zu einer bezahlten Bestellung einen kostenlosen Entwurf bei Printful an.</summary>
    public Task<bool> PodDraftOrder(PodPaidOrder order) => ShopRun(async () =>
    {
        if (_shopDrafts.ContainsKey(order.SessionId)) throw new PodServiceException("Zu dieser Zahlung gibt es schon einen Entwurf.");
        var v = ShopVariantFor(order) ?? throw new PodServiceException("Diese Zahlung gehört zu keinem Produkt aus dem Shop.");
        var id = await new PrintfulOrders(PodWeb, PodSecret(PfKey) ?? throw new PodServiceException("Verbinde zuerst dein Printful-Konto.")).CreateDraftAsync(order, v.SyncId);
        _shopDrafts[order.SessionId] = id;
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.bestellung", "entwurf " + id);
        _shopMsg = "Entwurf angelegt. Kosten entstehen erst, wenn du ihn freigibst.";
    });

    /// <summary>Gibt einen Entwurf frei. Das kostet Geld und läuft nur über diesen ausdrücklichen Klick.</summary>
    public Task<bool> PodConfirmOrder(long printfulId) => ShopRun(async () =>
    {
        await new PrintfulOrders(PodWeb, PodSecret(PfKey) ?? throw new PodServiceException("Verbinde zuerst dein Printful-Konto.")).ConfirmAsync(printfulId);
        _shopConfirmed.Add(printfulId);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.bestellung", "freigegeben " + printfulId);
        _shopMsg = "Bestellung ist zur Fertigung freigegeben.";
    });

    Button ShopBtn(string name, string text, Func<Task> act, bool accent = false)
    {
        var b = new Button { Name = name, Content = text, Padding = new Thickness(10, 4), IsEnabled = !_shopBusy };
        if (accent) b.Classes.Add("accent");
        b.Click += async (_, _) => await act();
        return b;
    }

    Control PodShopCard()
    {
        var sp = new StackPanel { Name = "shop-pod", Spacing = 6 };
        if (_shopMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "shop-msg", Text = _shopMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var items = PodShopItems.All();
        foreach (var i in items)
            sp.Children.Add(P($"{i.Name} · {i.Price:0.00} € · {i.Variants.Count(v => v.PayUrl is not null)} von {i.Variants.Count} Varianten bestellbar", Text, 12));
        if (items.Count == 0) sp.Children.Add(P("Noch keine Produkte. Lege sie im Studio „Print on Demand“ über Printful an.", Muted, 12));

        if (!PodStripeOn)
        {
            var key = new TextBox { Name = "shop-st-key", PasswordChar = '•', PlaceholderText = "Stripe-Schlüssel einfügen", MinWidth = 280 };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ShopBtn("shop-st-get", "Schlüssel bei Stripe holen", () => { Launch(StripeClient.KeyPage); return Task.CompletedTask; }), key, ShopBtn("shop-st-connect", "Verbinden", () => PodStripeConnect(key.Text ?? ""), true) } });
            return sp;
        }

        sp.Children.Add(ShopBtn("shop-paylinks", "Bezahl-Links anlegen", PodCreatePayLinks, true));
        sp.Children.Add(H("Angaben für Impressum und Rechtstexte", 13));
        var shop = new TextBox { Name = "shop-name", PlaceholderText = "Name des Shops", MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        var owner = new TextBox { Name = "shop-owner", PlaceholderText = "Dein Name oder deine Firma", MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        var addr = new TextBox { Name = "shop-address", PlaceholderText = "Straße und Hausnummer, PLZ und Ort", AcceptsReturn = true, MinHeight = 48, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        var mail = new TextBox { Name = "shop-email", PlaceholderText = "E-Mail", MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        var vat = new TextBox { Name = "shop-vat", PlaceholderText = "Umsatzsteuer-ID, falls vorhanden", MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
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
        var nr = 0;
        foreach (var o in _shopOrders)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = $"{o.Customer} · {o.Address?.City}", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
            var order = o; var idx = nr++;
            if (!_shopDrafts.TryGetValue(o.SessionId, out var draft)) row.Children.Add(ShopBtn("shop-draft-" + idx, "Entwurf bei Printful anlegen", () => PodDraftOrder(order)));
            else if (_shopConfirmed.Contains(draft)) row.Children.Add(new TextBlock { Text = "✓ freigegeben", Foreground = Brush.Parse("#3DDC84"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            else row.Children.Add(ShopBtn("shop-confirm-" + idx, "Fertigung freigeben (kostet Geld)", () => PodConfirmOrder(draft), true));
            sp.Children.Add(row);
        }
        return sp;
    }
}
