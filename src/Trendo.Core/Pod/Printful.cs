using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Pod;

public sealed class PodServiceException(string message) : Exception(message);

public sealed record PfProduct(int Id, string Title, string Type, string Brand, string Image, int Variants);
public sealed record PfVariant(int Id, string Name, string Size, string Color, decimal Price, bool InStock);
public sealed record PfPlacement(string Key, string Label);
public sealed record PfMockupTask(string Key, string Status, IReadOnlyList<string> Urls, string? Error)
{
    public bool Done => Status.Equals("completed", StringComparison.OrdinalIgnoreCase);
    public bool Failed => Status.Equals("failed", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Offizielle Printful-Schnittstelle (1.0) mit dem eigenen Schlüssel der Person. Löst keine Bestellung aus: Es gibt hier keinen Aufruf, der etwas kostet.</summary>
public sealed class PrintfulClient(IWebSend web, string token)
{
    public const string Api = "https://api.printful.com";
    /// <summary>Seite, auf der die Person ihren eigenen Schlüssel erzeugt.</summary>
    public const string TokenPage = "https://developers.printful.com/tokens";

    Dictionary<string, string> Headers => new() { ["Authorization"] = "Bearer " + token };

    async Task<JsonNode?> Call(string method, string path, JsonNode? body, CancellationToken ct)
    {
        var bytes = body is null ? null : Encoding.UTF8.GetBytes(body.ToJsonString());
        var r = await web.SendAsync(method, Api + path, Headers, bytes, body is null ? null : "application/json", ct);
        JsonNode? doc = null;
        try { doc = JsonNode.Parse(r.Body); } catch (JsonException) { }
        if (r.Status == 401) throw new PodServiceException("Der Printful-Schlüssel ist ungültig oder abgelaufen. Hole dir einen neuen.");
        if (r.Status == 403) throw new PodServiceException("Dem Printful-Schlüssel fehlt die nötige Berechtigung.");
        if (r.Status == 429) throw new PodServiceException("Printful bittet um eine kurze Pause. Versuche es in einer Minute erneut.");
        if (!r.Ok) throw new PodServiceException("Printful meldet: " + (doc?["error"]?["message"]?.GetValue<string>() ?? doc?["result"]?.ToString() ?? $"Fehler {r.Status}"));
        return doc?["result"];
    }

    /// <summary>Prüft den Schlüssel, ohne etwas zu ändern. Wirft mit verständlichem Grund, wenn er nicht gilt.</summary>
    public async Task CheckAsync(CancellationToken ct = default) => await Call("GET", "/oauth/scopes", null, ct);

    public async Task<IReadOnlyList<PfProduct>> CatalogAsync(CancellationToken ct = default) =>
        ((await Call("GET", "/products", null, ct)) as JsonArray ?? []).OfType<JsonObject>()
            .Where(p => p["is_discontinued"]?.GetValue<bool>() != true)
            .Select(ReadProduct).ToList();

    static PfProduct ReadProduct(JsonObject p) => new(p["id"]!.GetValue<int>(), Str(p, "title"), Str(p, "type_name"), Str(p, "brand"), Str(p, "image"), p["variant_count"]?.GetValue<int>() ?? 0);

    static string Str(JsonObject o, string key) => o[key]?.GetValue<string>() ?? "";

    public async Task<IReadOnlyList<PfVariant>> VariantsAsync(int productId, CancellationToken ct = default)
    {
        var res = await Call("GET", $"/products/{productId}", null, ct);
        return (res?["variants"] as JsonArray ?? []).OfType<JsonObject>().Select(v => new PfVariant(
            v["id"]!.GetValue<int>(), Str(v, "name"), Str(v, "size"), Str(v, "color"),
            decimal.TryParse(v["price"]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : 0m,
            v["in_stock"]?.GetValue<bool>() ?? true)).ToList();
    }

    public async Task<IReadOnlyList<PfPlacement>> PlacementsAsync(int productId, CancellationToken ct = default)
    {
        var res = await Call("GET", $"/mockup-generator/printfiles/{productId}", null, ct);
        return (res?["available_placements"] as JsonObject ?? []).Select(kv => new PfPlacement(kv.Key, kv.Value?.ToString() ?? kv.Key)).ToList();
    }

    /// <summary>Startet die Mockup-Erzeugung. Printful holt das Design von <paramref name="imageUrl"/> ab.</summary>
    public async Task<PfMockupTask> StartMockupAsync(int productId, IEnumerable<int> variantIds, string placement, string imageUrl, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["variant_ids"] = new JsonArray(variantIds.Select(i => (JsonNode)JsonValue.Create(i)).ToArray()),
            ["format"] = "jpg",
            ["files"] = new JsonArray(new JsonObject { ["placement"] = placement, ["image_url"] = imageUrl }),
        };
        return ReadTask(await Call("POST", $"/mockup-generator/create-task/{productId}", body, ct));
    }

    public async Task<PfMockupTask> MockupAsync(string taskKey, CancellationToken ct = default) =>
        ReadTask(await Call("GET", "/mockup-generator/task?task_key=" + Uri.EscapeDataString(taskKey), null, ct));

    static PfMockupTask ReadTask(JsonNode? r)
    {
        var urls = new List<string>();
        foreach (var m in (r?["mockups"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (m["mockup_url"]?.GetValue<string>() is { Length: > 0 } main) urls.Add(main);
            urls.AddRange((m["extra"] as JsonArray ?? []).OfType<JsonObject>().Select(e => Str(e, "url")).Where(u => u.Length > 0));
        }
        return new(r?["task_key"]?.GetValue<string>() ?? "", r?["status"]?.GetValue<string>() ?? "", urls.Distinct().ToList(), r?["error"]?.ToString() is { Length: > 0 } e ? e : null);
    }

    /// <summary>Legt ein Produkt im eigenen Printful-Bestand an. Das bestellt nichts und kostet nichts.</summary>
    public async Task<long> CreateProductAsync(string name, IEnumerable<(int VariantId, decimal RetailPrice)> variants, string fileUrl, string? thumbnail = null, CancellationToken ct = default)
    {
        var list = new JsonArray();
        foreach (var (id, price) in variants)
            list.Add(new JsonObject { ["variant_id"] = id, ["retail_price"] = price.ToString("0.00", CultureInfo.InvariantCulture), ["files"] = new JsonArray(new JsonObject { ["url"] = fileUrl }) });
        if (list.Count == 0) throw new PodServiceException("Wähle mindestens eine Variante aus.");
        var product = new JsonObject { ["name"] = name };
        if (thumbnail is not null) product["thumbnail"] = thumbnail;
        var res = await Call("POST", "/store/products", new JsonObject { ["sync_product"] = product, ["sync_variants"] = list }, ct);
        return res?["id"]?.GetValue<long>() ?? throw new PodServiceException("Printful hat keine Produktnummer geliefert.");
    }
}

/// <summary>Design-Ablage im eigenen Netlify-Konto: Printful holt Designs nur von einer öffentlichen Adresse ab. Der Dateiname ist nicht erratbar, und nach der Übernahme wird die Ablage wieder geleert.</summary>
public sealed class NetlifyDesignHost(IWebSend web, string token)
{
    public const string Api = "https://api.netlify.com/api/v1";
    public const string TokenPage = "https://app.netlify.com/user/applications#personal-access-tokens";

    Dictionary<string, string> Headers => new() { ["Authorization"] = "Bearer " + token };

    async Task<JsonNode?> Call(string method, string path, byte[]? body, string? type, CancellationToken ct)
    {
        var r = await web.SendAsync(method, Api + path, Headers, body, type, ct);
        if (r.Status == 401) throw new PodServiceException("Der Netlify-Schlüssel ist ungültig oder abgelaufen.");
        if (!r.Ok) throw new PodServiceException($"Netlify meldet Fehler {r.Status}.");
        try { return JsonNode.Parse(r.Body); } catch (JsonException) { return null; }
    }

    /// <summary>Legt die eigene Ablage an und liefert ihre Kennung und Adresse.</summary>
    public async Task<(string Id, string Url)> CreateSiteAsync(CancellationToken ct = default)
    {
        var s = await Call("POST", "/sites", "{}"u8.ToArray(), "application/json", ct);
        var id = s?["id"]?.GetValue<string>() ?? throw new PodServiceException("Netlify hat keine Ablage angelegt.");
        return (id, (s?["ssl_url"] ?? s?["url"])?.GetValue<string>() ?? throw new PodServiceException("Netlify hat keine Adresse geliefert."));
    }

    /// <summary>Nicht erratbarer Dateiname für ein Design.</summary>
    public static string RandomName() => Guid.NewGuid().ToString("N") + ".png";

    /// <summary>Packt Dateien für die Ablage. Eine leere Liste ergibt nur eine leere Startseite, das leert die Ablage.</summary>
    public static byte[] Zip(IEnumerable<(string Name, byte[] Data)> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            using (var s = zip.CreateEntry("index.html").Open()) s.Write("<!doctype html><title>Trendo</title>"u8);
            foreach (var (name, data) in files) { using var s = zip.CreateEntry(name).Open(); s.Write(data); }
        }
        return ms.ToArray();
    }

    /// <summary>Veröffentlicht die Dateien und wartet, bis sie abrufbar sind.</summary>
    public async Task DeployAsync(string siteId, byte[] zip, Func<TimeSpan, CancellationToken, Task>? delay = null, CancellationToken ct = default)
    {
        delay ??= Task.Delay;
        var d = await Call("POST", $"/sites/{siteId}/deploys", zip, "application/zip", ct);
        var id = d?["id"]?.GetValue<string>() ?? throw new PodServiceException("Netlify hat nichts veröffentlicht.");
        var state = d?["state"]?.GetValue<string>();
        for (var i = 0; i < 30 && state != "ready"; i++)
        {
            if (state == "error") throw new PodServiceException("Netlify konnte die Datei nicht bereitstellen.");
            await delay(TimeSpan.FromSeconds(2), ct);
            state = (await Call("GET", "/deploys/" + id, null, null, ct))?["state"]?.GetValue<string>();
        }
        if (state != "ready") throw new PodServiceException("Netlify braucht zu lange. Versuche es gleich noch einmal.");
    }
}
