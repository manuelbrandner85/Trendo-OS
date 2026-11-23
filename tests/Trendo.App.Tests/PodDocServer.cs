using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;

namespace Trendo.App.Tests;

/// <summary>T-0753: ERSETZT – lokaler Testserver, der die öffentlich dokumentierten Schnittstellen von Printful (v1), Printify (v1), Stripe und Netlify
/// nachbildet (Pfade, Methoden, Pflichtfelder, Antwortform laut Dokumentation, gelesen am 23.11.2026). Er ersetzt die echten Dienste, weil kein Konto
/// und kein Schlüssel der Person vorliegt. Jede Anfrage wird gegen die Dokumentation geprüft; Abweichungen landen in <see cref="Deviations"/>.
/// Kostenpflichtige Aufrufe (Printful /confirm, Printify send_to_production) sind als „darf nicht kommen“ markiert.</summary>
public sealed class PodDocServer : IDisposable
{
    readonly HttpListener _l = new();
    readonly CancellationTokenSource _stop = new();
    public string Base { get; }
    public List<string> Log { get; } = [];
    public List<string> Deviations { get; } = [];
    public List<string> Paths { get; } = [];

    public PodDocServer()
    {
        var t = new TcpListener(IPAddress.Loopback, 0); t.Start(); var port = ((IPEndPoint)t.LocalEndpoint).Port; t.Stop();
        Base = $"http://localhost:{port}";
        _l.Prefixes.Add(Base + "/");
        _l.Start();
        _ = Task.Run(Loop);
    }

    /// <summary>Leitet die echten Adressen auf diesen Server um; gesendet wird über den echten HTTP-Weg der App (<see cref="HttpFetch"/>).</summary>
    public IWebSend Sender() => new Rewrite(this, new HttpFetch(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }));

    sealed class Rewrite(PodDocServer s, IWebSend inner) : IWebSend
    {
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            var u = url.Replace("https://api.printful.com", s.Base + "/printful").Replace("https://api.printify.com", s.Base + "/printify")
                .Replace("https://api.stripe.com", s.Base + "/stripe").Replace("https://api.netlify.com", s.Base + "/netlify");
            if (u == url) throw new InvalidOperationException("Unerwartete Adresse (nicht umgeleitet): " + new Uri(url).Host);
            return inner.SendAsync(method, u, headers, body, contentType, ct);
        }
    }

    void Dev(string what) { lock (Deviations) Deviations.Add(what); }

    async Task Loop()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext c;
            try { c = await _l.GetContextAsync(); } catch (Exception) { return; }
            try { await Handle(c); }
            catch (Exception e) { Dev("Serverfehler: " + e.Message); c.Response.StatusCode = 500; c.Response.Close(); }
        }
    }

    static JsonNode? Json(byte[] b) { try { return JsonNode.Parse(b); } catch { return null; } }
    static Dictionary<string, string> Form(byte[] b) => Encoding.UTF8.GetString(b).Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2))
        .ToDictionary(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : "");

    async Task Handle(HttpListenerContext c)
    {
        var req = c.Request; var m = req.HttpMethod; var raw = req.RawUrl ?? "/";
        var path = raw.Split('?')[0]; var query = req.QueryString;
        using var ms = new MemoryStream(); await req.InputStream.CopyToAsync(ms); var body = ms.ToArray();
        var auth = req.Headers["Authorization"] ?? ""; var ctype = req.ContentType ?? "";
        var checks = new List<string>();
        void Need(bool ok, string what) { checks.Add((ok ? "ok " : "ABWEICHUNG ") + what); if (!ok) Dev($"{m} {path}: {what}"); }
        var status = 200; string reply;
        var service = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var p = path[(service.Length + 1)..];
        Need(auth.StartsWith("Bearer ", StringComparison.Ordinal) && auth.Length > 8, "Authorization: Bearer <Schlüssel>");
        switch (service)
        {
            case "printful":
                switch (m, p)
                {
                    case ("GET", "/oauth/scopes"): reply = """{"code":200,"result":{"scopes":[{"public":"orders","value":"orders"},{"public":"sync_products","value":"sync_products"}]}}"""; break;
                    case ("GET", "/products"): reply = """{"code":200,"result":[{"id":71,"type":"T-SHIRT","type_name":"T-Shirt","title":"Unisex Staple T-Shirt | Bella + Canvas 3001","brand":"Bella + Canvas","image":"https://files.cdn.printful.com/products/71/product.jpg","variant_count":2,"is_discontinued":false},{"id":1,"title":"Alt","type_name":"Poster","brand":"","image":"","variant_count":1,"is_discontinued":true}]}"""; break;
                    case ("GET", "/products/71"): reply = """{"code":200,"result":{"product":{"id":71},"variants":[{"id":4012,"product_id":71,"name":"Bella + Canvas 3001 (White / M)","size":"M","color":"White","price":"9.25","in_stock":true},{"id":4013,"product_id":71,"name":"Bella + Canvas 3001 (White / L)","size":"L","color":"White","price":"9.25","in_stock":true}]}}"""; break;
                    case ("GET", "/mockup-generator/printfiles/71"): reply = """{"code":200,"result":{"product_id":71,"available_placements":{"front":"Front print","back":"Back print"},"printfiles":[],"variant_printfiles":[]}}"""; break;
                    case ("POST", "/mockup-generator/create-task/71"):
                    {
                        var j = Json(body);
                        Need(ctype.StartsWith("application/json"), "Inhalt als JSON");
                        Need(j?["variant_ids"] is JsonArray { Count: > 0 } va && va.All(x => x?.GetValueKind() == System.Text.Json.JsonValueKind.Number), "variant_ids: Liste von Zahlen");
                        Need(j?["format"]?.ToString() is "jpg" or "png", "format jpg oder png");
                        Need(j?["files"] is JsonArray { Count: > 0 } fa && fa.All(f => f?["placement"] is not null && f["image_url"]?.ToString().StartsWith("https://") == true), "files[].placement und files[].image_url (https)");
                        reply = """{"code":200,"result":{"task_key":"gt-7741","status":"pending"}}"""; break;
                    }
                    case ("GET", "/mockup-generator/task"):
                        Need(query["task_key"] == "gt-7741", "task_key als Abfrageparameter");
                        reply = """{"code":200,"result":{"task_key":"gt-7741","status":"completed","mockups":[{"placement":"front","variant_ids":[4012],"mockup_url":"https://files.cdn.printful.com/mockup/a.jpg","extra":[{"title":"Back","url":"https://files.cdn.printful.com/mockup/b.jpg"}]}]}}"""; break;
                    case ("POST", "/store/products"):
                    {
                        var j = Json(body);
                        Need(j?["sync_product"]?["name"] is not null, "sync_product.name");
                        Need(j?["sync_variants"] is JsonArray { Count: > 0 } sv && sv.All(v => v?["variant_id"] is not null && v["retail_price"] is not null && v["files"] is JsonArray { Count: > 0 } fs && fs.All(f => f?["url"] is not null)), "sync_variants[].variant_id, retail_price, files[].url");
                        reply = """{"code":200,"result":{"id":5551,"external_id":"x","name":"Shirt","variants":1,"synced":1}}"""; break;
                    }
                    case ("GET", "/store/products/5551"): reply = """{"code":200,"result":{"sync_product":{"id":5551,"name":"Shirt"},"sync_variants":[{"id":9001,"name":"Shirt / M","variant_id":4012,"files":[{"type":"default","status":"ok"}]}]}}"""; break;
                    case ("GET", var o) when o.StartsWith("/orders/@"):
                        Need(o.Length - "/orders/@".Length <= 32, "external_id höchstens 32 Zeichen");
                        status = 404; reply = """{"code":404,"result":"Not found","error":{"reason":"NotFound","message":"Not found"}}"""; break;
                    case ("POST", "/orders"):
                    {
                        var j = Json(body);
                        Need(query["confirm"] is null or "false" or "0", "ohne confirm=true – Bestellung bleibt Entwurf (kostenlos)");
                        Need(j?["external_id"]?.ToString() is { Length: > 0 and <= 32 }, "external_id 1–32 Zeichen");
                        var r = j?["recipient"];
                        Need(new[] { "name", "address1", "city", "country_code", "zip" }.All(k => r?[k] is not null), "recipient.name, address1, city, country_code, zip");
                        Need(j?["items"] is JsonArray { Count: > 0 } it && it.All(x => x?["sync_variant_id"] is not null && x["quantity"] is not null), "items[].sync_variant_id und quantity");
                        reply = """{"code":200,"result":{"id":99,"external_id":"x","status":"draft","costs":{"total":"12.40"}}}"""; break;
                    }
                    case ("POST", var o) when o.EndsWith("/confirm"): Dev("KOSTENPFLICHTIG: Printful /confirm wurde aufgerufen"); reply = "{}"; break;
                    default: Dev($"unbekannter Printful-Pfad {m} {p}"); status = 404; reply = "{}"; break;
                }
                break;
            case "printify":
                Need(!string.IsNullOrEmpty(req.Headers["User-Agent"]), "User-Agent gesetzt (Printify verlangt ihn)");
                switch (m, p)
                {
                    case ("GET", "/v1/shops.json"): reply = """[{"id":1820,"title":"Mein Shop","sales_channel":"custom_integration"}]"""; break;
                    case ("GET", "/v1/catalog/blueprints.json"): reply = """[{"id":6,"title":"Unisex Heavy Cotton Tee","description":"","brand":"Gildan","model":"5000","images":["https://images.printify.com/5853fec7ce46f30f8328200a"]}]"""; break;
                    case ("GET", "/v1/catalog/blueprints/6/print_providers.json"): reply = """[{"id":29,"title":"Monster Digital"}]"""; break;
                    case ("GET", "/v1/catalog/blueprints/6/print_providers/29/variants.json"): reply = """{"id":29,"title":"Monster Digital","variants":[{"id":12100,"title":"White / M","options":{"color":"White","size":"M"},"placeholders":[{"position":"front","height":3995,"width":3153}]}]}"""; break;
                    case ("POST", "/v1/uploads/images.json"):
                    {
                        var j = Json(body);
                        Need(j?["file_name"] is not null && (j["contents"] is not null || j["url"] is not null), "file_name und contents (Base64) oder url");
                        if (j?["contents"]?.ToString() is { } b64) Need(Convert.TryFromBase64String(b64, new byte[b64.Length], out _), "contents ist gültiges Base64");
                        reply = """{"id":"5e16d66791287a0006e522b2","file_name":"design.png","height":5979,"width":17045,"size":1138575,"mime_type":"image/png","preview_url":"https://example.org/p.png","upload_time":"2026-11-23 10:00:00"}"""; break;
                    }
                    case ("POST", "/v1/shops/1820/products.json"):
                    {
                        var j = Json(body);
                        Need(j?["title"] is not null && j["description"] is not null && j["blueprint_id"] is not null && j["print_provider_id"] is not null, "title, description, blueprint_id, print_provider_id");
                        Need(j?["variants"] is JsonArray { Count: > 0 } v && v.All(x => x?["id"] is not null && x["price"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number && (long)x["price"]! == (double)x["price"]!), "variants[].id und price als ganze Zahl (Cent)");
                        Need(j?["print_areas"] is JsonArray { Count: > 0 } pa && pa.All(a => a?["variant_ids"] is JsonArray && a["placeholders"] is JsonArray ph && ph.All(h => h?["position"] is not null && h["images"] is JsonArray im && im.All(i => new[] { "id", "x", "y", "scale", "angle" }.All(k => i?[k] is not null)))), "print_areas[].variant_ids, placeholders[].position, images[].id/x/y/scale/angle");
                        reply = """{"id":"5d39b411749d0a000f30e0f4","title":"Shirt","images":[{"src":"https://images-api.printify.com/mockup/1.jpg","variant_ids":[12100],"position":"front","is_default":true}]}"""; break;
                    }
                    case ("POST", "/v1/shops/1820/orders.json"):
                    {
                        var j = Json(body);
                        Need(j?["external_id"] is not null, "external_id");
                        Need(j?["line_items"] is JsonArray { Count: > 0 } li && li.All(x => x?["product_id"] is not null && x["variant_id"] is not null && x["quantity"] is not null), "line_items[].product_id, variant_id, quantity");
                        Need(j?["shipping_method"] is not null && j["send_shipping_notification"] is not null, "shipping_method und send_shipping_notification");
                        Need(new[] { "first_name", "last_name", "email", "country", "address1", "city", "zip" }.All(k => j?["address_to"]?[k] is not null), "address_to.first_name, last_name, email, country, address1, city, zip");
                        reply = """{"id":"5a96f649b2439217d070f507"}"""; break;
                    }
                    case ("GET", "/v1/shops/1820/orders/5a96f649b2439217d070f507.json"): reply = """{"id":"5a96f649b2439217d070f507","status":"on-hold","total_price":1200,"total_shipping":450,"total_tax":0}"""; break;
                    case ("POST", var o) when o.EndsWith("/send_to_production.json"): Dev("KOSTENPFLICHTIG: Printify send_to_production wurde aufgerufen"); reply = "{}"; break;
                    default: Dev($"unbekannter Printify-Pfad {m} {p}"); status = 404; reply = "{}"; break;
                }
                break;
            case "stripe":
                Need(auth.StartsWith("Bearer sk_test_", StringComparison.Ordinal) || auth.StartsWith("Bearer rk_test_", StringComparison.Ordinal), "Testschlüssel (sk_test_/rk_test_)");
                switch (m, p)
                {
                    case ("GET", "/v1/balance"): reply = """{"object":"balance","available":[{"amount":0,"currency":"eur"}],"livemode":false,"pending":[]}"""; break;
                    case ("POST", "/v1/prices"):
                    {
                        Need(ctype.StartsWith("application/x-www-form-urlencoded"), "Inhalt form-codiert");
                        var f = Form(body);
                        Need(f.GetValueOrDefault("currency") == "eur" && long.TryParse(f.GetValueOrDefault("unit_amount"), out _) && f.ContainsKey("product_data[name]"), "currency, unit_amount (ganze Cent), product_data[name]");
                        reply = """{"id":"price_1MoBy5LkdIwHu7ixZhnattbh","object":"price","livemode":false}"""; break;
                    }
                    case ("POST", "/v1/payment_links"):
                    {
                        var f = Form(body);
                        Need(f.ContainsKey("line_items[0][price]") && f.ContainsKey("line_items[0][quantity]"), "line_items[0][price] und [quantity]");
                        Need(f.Keys.Any(k => k.StartsWith("shipping_address_collection[allowed_countries][")), "shipping_address_collection[allowed_countries][]");
                        reply = """{"id":"plink_1MoC3ULkdIwHu7ixZjtGpVl2","object":"payment_link","livemode":false,"url":"https://buy.stripe.com/test_cN25nr0iZ7bUa7meUY"}"""; break;
                    }
                    case ("GET", "/v1/checkout/sessions"):
                        Need(query["status"] == "complete" && int.TryParse(query["limit"], out var lim) && lim is >= 1 and <= 100, "status=complete und limit 1–100");
                        reply = """{"object":"list","has_more":false,"data":[{"id":"cs_test_a1b2c3d4e5","object":"checkout.session","livemode":false,"payment_status":"paid","payment_link":"plink_1MoC3ULkdIwHu7ixZjtGpVl2","customer_details":{"name":"Erika Muster","email":"erika@example.org"},"collected_information":{"shipping_details":{"name":"Erika Muster","address":{"line1":"Hauptstraße 1","line2":null,"city":"Berlin","postal_code":"10115","country":"DE"}}}}]}"""; break;
                    default: Dev($"unbekannter Stripe-Pfad {m} {p}"); status = 404; reply = "{}"; break;
                }
                break;
            case "netlify":
                switch (m, p)
                {
                    case ("GET", "/api/v1/user"): reply = """{"id":"5c1a","full_name":"Erika Muster","email":"erika@example.org"}"""; break;
                    case ("POST", "/api/v1/sites"): reply = """{"id":"3970e0fe-8564-4903-9a55-c5f8de49fb8b","url":"http://x.netlify.app","ssl_url":"https://x.netlify.app"}"""; break;
                    case ("POST", "/api/v1/sites/3970e0fe-8564-4903-9a55-c5f8de49fb8b/deploys"):
                    {
                        Need(ctype.StartsWith("application/zip"), "Content-Type application/zip");
                        var ok = false; try { using var z = new ZipArchive(new MemoryStream(body)); ok = z.Entries.Any(e => e.FullName == "index.html"); } catch (InvalidDataException) { }
                        Need(ok, "Inhalt ist ein ZIP mit index.html");
                        reply = """{"id":"dep-1","state":"ready"}"""; break;
                    }
                    default: Dev($"unbekannter Netlify-Pfad {m} {p}"); status = 404; reply = "{}"; break;
                }
                break;
            default: Dev("unbekannter Dienst " + service); status = 404; reply = "{}"; break;
        }
        lock (Log) { Log.Add($"{m} {service}{p}{(query.Count > 0 ? "?" + string.Join("&", query.AllKeys.Select(k => k + "=…")) : "")} -> {status} | {string.Join("; ", checks)}"); Paths.Add($"{m} {service}{p}"); }
        var bytes = Encoding.UTF8.GetBytes(reply);
        c.Response.StatusCode = status; c.Response.ContentType = "application/json"; c.Response.ContentLength64 = bytes.Length;
        await c.Response.OutputStream.WriteAsync(bytes); c.Response.Close();
    }

    public void Dispose() { _stop.Cancel(); try { _l.Stop(); _l.Close(); } catch (ObjectDisposedException) { } }
}
