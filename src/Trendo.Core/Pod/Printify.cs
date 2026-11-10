using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Pod;

public sealed record PfyShop(long Id, string Title);
public sealed record PfyBlueprint(int Id, string Title, string Brand, string Image);
public sealed record PfyProvider(int Id, string Title);
public sealed record PfyVariant(int Id, string Title, string Position);
public sealed record PfyProduct(string Id, IReadOnlyList<string> Images);

/// <summary>Offizielle Printify-Schnittstelle mit dem eigenen Schlüssel der Person.
/// Printify kennt keinen kostenlosen Entwurf wie Printful: Neue Shops geben Bestellungen standardmäßig nach 24 Stunden selbst in die Fertigung. Bestellungen dürfen deshalb nur mit bestätigter manueller Freigabe im Shop angelegt werden, das prüft die Oberfläche.</summary>
public sealed class PrintifyClient(IWebSend web, string token)
{
    public const string Api = "https://api.printify.com/v1";
    public const string TokenPage = "https://printify.com/app/account/api";
    /// <summary>Printify empfiehlt für größere Dateien das Hochladen über eine Adresse.</summary>
    public const int MaxBase64Bytes = 5 * 1024 * 1024;

    Dictionary<string, string> Headers => new() { ["Authorization"] = "Bearer " + token, ["User-Agent"] = "TrendoOS" };

    async Task<JsonNode?> Call(string method, string path, JsonNode? body, CancellationToken ct)
    {
        var r = await web.SendAsync(method, Api + path, Headers, body is null ? null : Encoding.UTF8.GetBytes(body.ToJsonString()), body is null ? null : "application/json", ct);
        JsonNode? doc = null;
        try { doc = JsonNode.Parse(r.Body); } catch (JsonException) { }
        if (r.Status == 401) throw new PodServiceException("Der Printify-Schlüssel ist ungültig oder abgelaufen. Hole dir einen neuen.");
        if (r.Status == 403) throw new PodServiceException("Dem Printify-Schlüssel fehlt die nötige Berechtigung.");
        if (r.Status == 409) throw new PodServiceException("Diese Bestellung gibt es bei Printify schon.");
        if (r.Status == 429) throw new PodServiceException("Printify bittet um eine kurze Pause. Versuche es in einer Minute erneut.");
        if (!r.Ok) throw new PodServiceException("Printify meldet: " + (doc?["message"]?.ToString() ?? doc?["error"]?.ToString() ?? $"Fehler {r.Status}"));
        return doc;
    }

    static string Str(JsonNode? o, string key) => o?[key]?.ToString() ?? "";

    public async Task<IReadOnlyList<PfyShop>> ShopsAsync(CancellationToken ct = default) =>
        ((await Call("GET", "/shops.json", null, ct)) as JsonArray ?? []).OfType<JsonObject>().Select(s => new PfyShop(s["id"]!.GetValue<long>(), Str(s, "title"))).ToList();

    public async Task<IReadOnlyList<PfyBlueprint>> CatalogAsync(CancellationToken ct = default) =>
        ((await Call("GET", "/catalog/blueprints.json", null, ct)) as JsonArray ?? []).OfType<JsonObject>()
            .Select(b => new PfyBlueprint(b["id"]!.GetValue<int>(), Str(b, "title"), Str(b, "brand"), (b["images"] as JsonArray)?.FirstOrDefault()?.ToString() ?? "")).ToList();

    public async Task<IReadOnlyList<PfyProvider>> ProvidersAsync(int blueprint, CancellationToken ct = default) =>
        ((await Call("GET", $"/catalog/blueprints/{blueprint}/print_providers.json", null, ct)) as JsonArray ?? []).OfType<JsonObject>()
            .Select(p => new PfyProvider(p["id"]!.GetValue<int>(), Str(p, "title"))).ToList();

    public async Task<IReadOnlyList<PfyVariant>> VariantsAsync(int blueprint, int provider, CancellationToken ct = default)
    {
        var res = await Call("GET", $"/catalog/blueprints/{blueprint}/print_providers/{provider}/variants.json", null, ct);
        return (res?["variants"] as JsonArray ?? []).OfType<JsonObject>().Select(v => new PfyVariant(v["id"]!.GetValue<int>(), Str(v, "title"),
            (v["placeholders"] as JsonArray)?.FirstOrDefault()?["position"]?.ToString() ?? "front")).ToList();
    }

    public async Task<string> UploadAsync(string fileName, byte[] data, CancellationToken ct = default)
    {
        if (data.Length > MaxBase64Bytes) throw new PodServiceException("Die Datei ist für das direkte Hochladen zu groß.");
        return Id(await Call("POST", "/uploads/images.json", new JsonObject { ["file_name"] = fileName, ["contents"] = Convert.ToBase64String(data) }, ct));
    }

    public async Task<string> UploadUrlAsync(string fileName, string url, CancellationToken ct = default) =>
        Id(await Call("POST", "/uploads/images.json", new JsonObject { ["file_name"] = fileName, ["url"] = url }, ct));

    static string Id(JsonNode? r) => r?["id"]?.ToString() is { Length: > 0 } id ? id : throw new PodServiceException("Printify hat keine Kennung geliefert.");

    /// <summary>Legt ein Produkt im eigenen Shop an und liefert die Mockups. Das bestellt nichts.</summary>
    public async Task<PfyProduct> CreateProductAsync(long shop, string title, int blueprint, int provider, IReadOnlyCollection<int> variantIds, decimal gross, string position, string imageId, CancellationToken ct = default)
    {
        if (variantIds.Count == 0) throw new PodServiceException("Wähle mindestens eine Variante aus.");
        var cents = (long)Math.Round(gross * 100m, MidpointRounding.AwayFromZero);
        var body = new JsonObject
        {
            ["title"] = title,
            ["description"] = title,
            ["blueprint_id"] = blueprint,
            ["print_provider_id"] = provider,
            ["variants"] = new JsonArray(variantIds.Select(id => (JsonNode)new JsonObject { ["id"] = id, ["price"] = cents, ["is_enabled"] = true }).ToArray()),
            ["print_areas"] = new JsonArray(new JsonObject
            {
                ["variant_ids"] = new JsonArray(variantIds.Select(i => (JsonNode)JsonValue.Create(i)).ToArray()),
                ["placeholders"] = new JsonArray(new JsonObject { ["position"] = position, ["images"] = new JsonArray(new JsonObject { ["id"] = imageId, ["x"] = 0.5, ["y"] = 0.5, ["scale"] = 1, ["angle"] = 0 }) }),
            }),
        };
        var res = await Call("POST", $"/shops/{shop}/products.json", body, ct);
        return new(Id(res), (res?["images"] as JsonArray ?? []).OfType<JsonObject>().Select(i => Str(i, "src")).Where(s => s.Length > 0).ToList());
    }

    /// <summary>Legt eine Bestellung an. Darf nur bei manueller Freigabe im Printify-Shop genutzt werden, sonst startet Printify die Fertigung selbst.</summary>
    public async Task<string> CreateOrderAsync(long shop, PodPaidOrder order, string productId, long variantId, CancellationToken ct = default)
    {
        if (order.Address is not { } a) throw new PodServiceException("Zu dieser Zahlung fehlt die Lieferadresse.");
        var parts = a.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var body = new JsonObject
        {
            ["external_id"] = order.SessionId,
            ["line_items"] = new JsonArray(new JsonObject { ["product_id"] = productId, ["variant_id"] = variantId, ["quantity"] = 1 }),
            ["shipping_method"] = 1,
            ["send_shipping_notification"] = false,
            ["address_to"] = new JsonObject { ["first_name"] = parts.ElementAtOrDefault(0) ?? "", ["last_name"] = parts.ElementAtOrDefault(1) ?? "", ["email"] = order.Email, ["country"] = a.Country, ["address1"] = a.Line1, ["address2"] = a.Line2, ["city"] = a.City, ["zip"] = a.Zip },
        };
        return Id(await Call("POST", $"/shops/{shop}/orders.json", body, ct));
    }

    /// <summary>Gibt eine Bestellung in die Fertigung. Das kostet Geld.</summary>
    public async Task SendToProductionAsync(long shop, string orderId, CancellationToken ct = default) =>
        await Call("POST", $"/shops/{shop}/orders/{Uri.EscapeDataString(orderId)}/send_to_production.json", new JsonObject(), ct);
}
