using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Pod;

public sealed record PodShopVariant(string Name, long SyncId, string? PayUrl = null, string? PayId = null);
public sealed record PodShopItem(string Id, string Name, decimal Price, string Image, long PrintfulId, List<PodShopVariant> Variants);
public sealed record PodShopLegal(string Owner, string Address, string Email, string VatId = "");
public sealed record PodAddress(string Name, string Line1, string City, string Zip, string Country, string Line2 = "");
public sealed record PodPaidOrder(string SessionId, string PayLinkId, string Customer, PodAddress? Address, string Email, bool Live = true)
{
    /// <summary>Kurzes Kennzeichen zur Anzeige, ohne Kundendaten.</summary>
    public string Code => SessionId.Length <= 8 ? SessionId : SessionId[^8..];
}
public sealed record PodDraft(long Id, decimal? Cost);
public sealed record PodOrderState(long PrintfulId, bool Confirmed, decimal? Cost);
public sealed record PodPending(long ProductId, PodShopItem Item);

/// <summary>Produkte aus dem Anbieter, die im eigenen Shop stehen.</summary>
public sealed class PodShopStore(string dataRoot)
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    string File_ => Path.Combine(dataRoot, "print-on-demand", "shop.json");

    public List<PodShopItem> All()
    {
        if (!File.Exists(File_)) return [];
        try { return JsonSerializer.Deserialize<List<PodShopItem>>(File.ReadAllText(File_)) ?? []; }
        catch (JsonException) { File.Copy(File_, File_ + ".kaputt", true); return []; }
    }

    void Save(List<PodShopItem> items) { Directory.CreateDirectory(Path.GetDirectoryName(File_)!); File.WriteAllText(File_, JsonSerializer.Serialize(items, Json)); }

    public void Add(PodShopItem item) { var l = All(); l.RemoveAll(x => x.PrintfulId == item.PrintfulId); l.Add(item); Save(l); }
    public void Remove(string id) { var l = All(); l.RemoveAll(x => x.Id == id); Save(l); }
    public void Replace(PodShopItem item) { var l = All(); var i = l.FindIndex(x => x.Id == item.Id); if (i >= 0) { l[i] = item; Save(l); } }
}

/// <summary>Merkt sich Entwürfe und Freigaben über einen Neustart hinweg.</summary>
public sealed class PodOrderStore(string dataRoot)
{
    string File_ => Path.Combine(dataRoot, "print-on-demand", "bestellungen.json");

    public Dictionary<string, PodOrderState> All()
    {
        if (!File.Exists(File_)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, PodOrderState>>(File.ReadAllText(File_)) ?? []; }
        catch (JsonException) { File.Copy(File_, File_ + ".kaputt", true); throw new PodServiceException("Die Liste der Bestellungen ist beschädigt. Prüfe die Entwürfe bei Printful."); }
    }

    public void Set(string session, PodOrderState state)
    {
        var all = All(); all[session] = state;
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, JsonSerializer.Serialize(all));
    }
}

/// <summary>Produkt, dessen Datei Printful noch nicht übernommen hat. Bis dahin bleibt die Ablage unberührt und das Produkt außerhalb des Shops.</summary>
public sealed class PodPendingStore(string dataRoot)
{
    string File_ => Path.Combine(dataRoot, "print-on-demand", "offen.json");

    public PodPending? Get()
    {
        if (!File.Exists(File_)) return null;
        try { return JsonSerializer.Deserialize<PodPending>(File.ReadAllText(File_)); } catch (JsonException) { return null; }
    }

    public void Set(PodPending p) { Directory.CreateDirectory(Path.GetDirectoryName(File_)!); File.WriteAllText(File_, JsonSerializer.Serialize(p)); }
    public void Clear() { if (File.Exists(File_)) File.Delete(File_); }
}

/// <summary>Erzeugt den Shop als einfache Webseiten mit Pflichtseiten. Die Rechtstexte sind Vorlagen und ersetzen keine Beratung.</summary>
public static class PodShopSite
{
    static string H(string s) => WebUtility.HtmlEncode(s);
    static string Page(string title, string body) => $"<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{H(title)}</title><style>body{{font-family:system-ui,sans-serif;max-width:1000px;margin:0 auto;padding:20px;color:#1f2933}}.g{{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:18px}}.c{{border:1px solid #ddd;border-radius:10px;padding:12px}}.c img{{width:100%;border-radius:6px}}.p{{font-size:20px;font-weight:700}}a.b{{display:inline-block;background:#1e4fd8;color:#fff;padding:7px 12px;border-radius:6px;text-decoration:none;margin:3px 3px 0 0}}footer{{margin-top:40px;font-size:13px}}</style></head><body>{body}<footer><a href=\"impressum.html\">Impressum</a> · <a href=\"agb.html\">AGB</a> · <a href=\"widerruf.html\">Widerruf</a> · <a href=\"datenschutz.html\">Datenschutz</a></footer></body></html>";

    /// <summary>Liefert null, wenn die Angaben genügen, sonst was fehlt.</summary>
    public static string? CheckLegal(PodShopLegal l)
    {
        if (string.IsNullOrWhiteSpace(l.Owner)) return "Trage deinen Namen oder deine Firma ein.";
        if (string.IsNullOrWhiteSpace(l.Address)) return "Trage deine ladungsfähige Anschrift ein.";
        return l.Email.Contains('@') ? null : "Trage eine gültige E-Mail-Adresse ein.";
    }

    static string Price(decimal p, bool smallBusiness) => p.ToString("0.00", CultureInfo.GetCultureInfo("de-DE")) + " € " + (smallBusiness ? "(gemäß § 19 UStG keine Umsatzsteuer), inkl. Versand" : "inkl. MwSt. und Versand");

    static bool Safe(string url) => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> Build(string shopName, IEnumerable<PodShopItem> items, PodShopLegal l, bool smallBusiness)
    {
        if (CheckLegal(l) is { } problem) throw new PodServiceException(problem);
        var cards = new StringBuilder();
        foreach (var i in items)
        {
            var buy = string.Concat(i.Variants.Where(v => v.PayUrl is { } u && Safe(u)).Select(v => $"<a class=\"b\" href=\"{H(v.PayUrl!)}\" rel=\"noopener\">{H(v.Name)} zahlungspflichtig bestellen</a>"));
            cards.Append($"<div class=\"c\">{(Safe(i.Image) ? $"<img alt=\"{H(i.Name)}\" src=\"{H(i.Image)}\">" : "")}<h3>{H(i.Name)}</h3><div class=\"p\">{H(Price(i.Price, smallBusiness))}</div>{(buy.Length > 0 ? buy : "<p>Derzeit nicht bestellbar.</p>")}</div>");
        }
        var who = $"<p>{H(l.Owner)}<br>{H(l.Address).Replace("\n", "<br>")}<br>E-Mail: {H(l.Email)}</p>";
        var vat = l.VatId.Length > 0 ? $"<p>Umsatzsteuer-ID: {H(l.VatId)}</p>" : "";
        return new Dictionary<string, string>
        {
            ["index.html"] = Page(shopName, $"<h1>{H(shopName)}</h1><div class=\"g\">{cards}</div>"),
            ["impressum.html"] = Page("Impressum", $"<h1>Impressum</h1>{who}{vat}"),
            ["agb.html"] = Page("AGB", $"<h1>Allgemeine Geschäftsbedingungen</h1><p>Anbieter ist {H(l.Owner)}. Der Vertrag kommt zustande, wenn du den Bestellvorgang mit der Zahlung abschließt. Die Ware wird auf Bestellung gefertigt und nach Zahlungseingang versandt. Es gelten die gesetzlichen Gewährleistungsrechte.</p>"),
            ["widerruf.html"] = Page("Widerruf", $"<h1>Widerrufsbelehrung</h1><p>Du hast das Recht, binnen vierzehn Tagen ohne Angabe von Gründen diesen Vertrag zu widerrufen. Die Frist beträgt vierzehn Tage ab dem Tag, an dem du oder ein von dir benannter Dritter die Ware in Besitz genommen hast. Um dein Widerrufsrecht auszuüben, informiere uns mittels einer eindeutigen Erklärung:</p>{who}"),
            ["datenschutz.html"] = Page("Datenschutz", $"<h1>Datenschutzerklärung</h1><p>Verantwortlich ist {H(l.Owner)}. Zur Abwicklung einer Bestellung werden deine Angaben an den Zahlungsdienst Stripe und an den Fertigungspartner Printful übermittelt. Du hast das Recht auf Auskunft, Berichtigung, Löschung und Beschwerde bei einer Aufsichtsbehörde.</p>{who}"),
        };
    }
}

/// <summary>Bezahl-Links und abgeschlossene Zahlungen über das eigene Stripe-Konto der Person.</summary>
public sealed class StripeClient(IWebSend web, string secretKey)
{
    public const string Api = "https://api.stripe.com/v1";
    public const string KeyPage = "https://dashboard.stripe.com/apikeys";

    static readonly string[] ShipTo = ["DE", "AT", "CH"];

    async Task<JsonNode?> Call(string method, string path, IEnumerable<(string K, string V)>? form, CancellationToken ct)
    {
        var body = form is null ? null : Encoding.UTF8.GetBytes(string.Join('&', form.Select(f => Uri.EscapeDataString(f.K) + "=" + Uri.EscapeDataString(f.V))));
        var r = await web.SendAsync(method, Api + path, new Dictionary<string, string> { ["Authorization"] = "Bearer " + secretKey }, body, body is null ? null : "application/x-www-form-urlencoded", ct);
        JsonNode? doc = null;
        try { doc = JsonNode.Parse(r.Body); } catch (JsonException) { }
        if (r.Status == 401) throw new PodServiceException("Der Stripe-Schlüssel ist ungültig.");
        if (!r.Ok) throw new PodServiceException("Stripe meldet: " + (doc?["error"]?["message"]?.GetValue<string>() ?? $"Fehler {r.Status}"));
        return doc;
    }

    public async Task CheckAsync(CancellationToken ct = default) => await Call("GET", "/balance", null, ct);

    /// <summary>Legt Preis und Bezahl-Link für eine Variante an.</summary>
    public async Task<(string Id, string Url)> PayLinkAsync(string name, decimal gross, CancellationToken ct = default)
    {
        var price = await Call("POST", "/prices", [("currency", "eur"), ("unit_amount", ((long)Math.Round(gross * 100m, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)), ("product_data[name]", name)], ct);
        var priceId = price?["id"]?.GetValue<string>() ?? throw new PodServiceException("Stripe hat keinen Preis angelegt.");
        var form = new List<(string, string)> { ("line_items[0][price]", priceId), ("line_items[0][quantity]", "1") };
        for (var i = 0; i < ShipTo.Length; i++) form.Add(($"shipping_address_collection[allowed_countries][{i}]", ShipTo[i]));
        var link = await Call("POST", "/payment_links", form, ct);
        return (link?["id"]?.GetValue<string>() ?? throw new PodServiceException("Stripe hat keinen Bezahl-Link angelegt."), link?["url"]?.GetValue<string>() is { Length: > 0 } url ? url : throw new PodServiceException("Stripe hat keine Adresse für den Bezahl-Link geliefert."));
    }

    /// <summary>Gibt es ältere Zahlungen, die der letzte Abruf nicht mehr enthält?</summary>
    public bool HasMore { get; private set; }

    /// <summary>Abgeschlossene und bezahlte Bestellungen, neueste zuerst.</summary>
    public async Task<IReadOnlyList<PodPaidOrder>> PaidOrdersAsync(int limit = 100, CancellationToken ct = default)
    {
        var res = await Call("GET", $"/checkout/sessions?status=complete&limit={limit}", null, ct);
        HasMore = res?["has_more"]?.GetValue<bool>() ?? false;
        var list = new List<PodPaidOrder>();
        foreach (var s in (res?["data"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (s["payment_status"]?.GetValue<string>() != "paid") continue;
            var c = s["customer_details"] as JsonObject;
            var ship = (s["collected_information"]?["shipping_details"] ?? s["shipping_details"]) as JsonObject;
            var a = ship?["address"] as JsonObject;
            PodAddress? addr = a is null ? null : new(ship?["name"]?.GetValue<string>() ?? "", a["line1"]?.GetValue<string>() ?? "", a["city"]?.GetValue<string>() ?? "", a["postal_code"]?.GetValue<string>() ?? "", a["country"]?.GetValue<string>() ?? "", a["line2"]?.GetValue<string>() ?? "");
            list.Add(new(s["id"]!.GetValue<string>(), s["payment_link"]?.GetValue<string>() ?? "", c?["name"]?.GetValue<string>() ?? "", addr, c?["email"]?.GetValue<string>() ?? "", s["livemode"]?.GetValue<bool>() ?? false));
        }
        return list;
    }
}

/// <summary>Bestellungen bei Printful. Anlegen ist immer ein Entwurf ohne Kosten; bestätigen löst die Fertigung und Kosten aus und läuft nur nach ausdrücklichem Klick.</summary>
public sealed class PrintfulOrders(IWebSend web, string token)
{
    Dictionary<string, string> Headers => new() { ["Authorization"] = "Bearer " + token };

    async Task<JsonNode?> Post(string path, JsonNode? body, CancellationToken ct)
    {
        var r = await web.SendAsync("POST", PrintfulClient.Api + path, Headers, body is null ? null : Encoding.UTF8.GetBytes(body.ToJsonString()), body is null ? null : "application/json", ct);
        JsonNode? doc = null;
        try { doc = JsonNode.Parse(r.Body); } catch (JsonException) { }
        if (r.Status == 401) throw new PodServiceException("Der Printful-Schlüssel ist ungültig oder abgelaufen. Hole dir einen neuen.");
        if (!r.Ok) throw new PodServiceException("Printful meldet: " + (doc?["error"]?["message"]?.GetValue<string>() ?? $"Fehler {r.Status}"));
        return doc?["result"];
    }

    static string External(string session)
    {
        var ext = new string(session.Where(char.IsLetterOrDigit).ToArray());
        return ext.Length > 32 ? ext[^32..] : ext;
    }

    static PodDraft ReadDraft(JsonNode? r) => new(r?["id"]?.GetValue<long>() ?? throw new PodServiceException("Printful hat keine Bestellnummer geliefert."),
        decimal.TryParse(r?["costs"]?["total"]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var c) ? c : null);

    /// <summary>Sucht die Bestellung zu einer Zahlung. Schützt vor doppelten Entwürfen, auch wenn eine Antwort verloren ging.</summary>
    public async Task<(PodDraft Draft, bool Confirmed)?> FindAsync(string session, CancellationToken ct = default)
    {
        var r = await web.SendAsync("GET", PrintfulClient.Api + "/orders/@" + External(session), Headers, null, null, ct);
        if (r.Status == 404) return null;
        if (r.Status == 401) throw new PodServiceException("Der Printful-Schlüssel ist ungültig oder abgelaufen. Hole dir einen neuen.");
        if (!r.Ok) throw new PodServiceException($"Printful meldet Fehler {r.Status}.");
        var res = JsonNode.Parse(r.Body)?["result"];
        return (ReadDraft(res), res?["status"]?.GetValue<string>() is { } st && st != "draft" && st != "failed");
    }

    /// <summary>Legt einen Entwurf an. <c>external_id</c> verhindert, dass dieselbe Zahlung doppelt angelegt wird.</summary>
    public async Task<PodDraft> CreateDraftAsync(PodPaidOrder order, long syncVariantId, CancellationToken ct = default)
    {
        if (order.Address is not { } a) throw new PodServiceException("Zu dieser Zahlung fehlt die Lieferadresse.");
        var body = new JsonObject
        {
            ["external_id"] = External(order.SessionId),
            ["recipient"] = new JsonObject { ["name"] = a.Name, ["address1"] = a.Line1, ["address2"] = a.Line2, ["city"] = a.City, ["zip"] = a.Zip, ["country_code"] = a.Country, ["email"] = order.Email },
            ["items"] = new JsonArray(new JsonObject { ["sync_variant_id"] = syncVariantId, ["quantity"] = 1 }),
        };
        return ReadDraft(await Post("/orders", body, ct));
    }

    /// <summary>Gibt einen Entwurf zur Fertigung frei. Das kostet Geld.</summary>
    public async Task ConfirmAsync(long orderId, CancellationToken ct = default) => await Post($"/orders/{orderId}/confirm", null, ct);
}
