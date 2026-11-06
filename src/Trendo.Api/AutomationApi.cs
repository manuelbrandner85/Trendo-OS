using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Trendo.Core;
using Trendo.Core.Automation;

namespace Trendo.Api;

/// <summary>Was die App der Automatik bereitstellt (Meldung anzeigen, Trendo-Abläufe, Brief-Entwurf ablegen).</summary>
public sealed class AutomationHost
{
    public Action<string>? Notify { get; init; }
    public Func<IReadOnlyList<object>>? ListWorkflows { get; init; }
    public Func<string, bool, Task<object>>? RunWorkflow { get; init; }
    public Func<string, string, string, string>? SaveLetterDraft { get; init; }
    public HttpMessageHandler? KiHandler { get; init; }
}

/// <summary>n8n und Node-RED sprechen mit Trendo (Freigabe 06.11.2026, N2/N3): „Trendo KI“ als OpenAI-kompatible Adresse für alle lokalen KIs,
/// dazu Trendo-Aktionen. Gleiche Regeln wie die übrige lokale API: nur 127.0.0.1, Schlüssel nötig, Rechte je Rolle, alles im Protokoll. Nichts geht nach außen.</summary>
public static class AutomationApi
{
    static Principal P(HttpContext c) => (Principal)c.Items["principal"]!;

    public static void Map(WebApplication app, TrendoCore core, AutomationHost host)
    {
        var http = host.KiHandler is null ? new HttpClient { Timeout = TimeSpan.FromMinutes(10) } : new HttpClient(host.KiHandler) { Timeout = TimeSpan.FromMinutes(10) };

        async Task<List<string>> Available()
        {
            var res = new List<string>();
            foreach (var b in N8n.Backends)
                try { using var r = await http.GetAsync(b.BaseUrl + "/models"); if (r.IsSuccessStatusCode) res.Add(b.Id); } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
            return res;
        }

        // ── N2: Trendo KI ──
        app.MapGet("/v1/models", async (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.RequestAction, core.Audit);
            var data = new JsonArray();
            foreach (var id in await Available())
            {
                var b = N8n.Backends.First(x => x.Id == id);
                try
                {
                    using var r = await http.GetAsync(b.BaseUrl + "/models");
                    if (JsonNode.Parse(await r.Content.ReadAsStringAsync())?["data"] is JsonArray models)
                        foreach (var m in models.OfType<JsonObject>()) data.Add(new JsonObject { ["id"] = $"{b.Id}/{m["id"]}", ["object"] = "model", ["owned_by"] = b.Label });
                }
                catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException) { }
            }
            return Results.Json(new JsonObject { ["object"] = "list", ["data"] = data });
        });
        app.MapPost("/v1/chat/completions", async (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.RequestAction, core.Audit);
            var body = JsonNode.Parse(await new StreamReader(c.Request.Body).ReadToEndAsync()) as JsonObject ?? throw new ArgumentException("Leere Anfrage.");
            var route = N8n.Route(body["model"]?.GetValue<string>() ?? "", await Available()) ?? throw new InvalidOperationException("Keine lokale KI erreichbar – Ollama oder LM Studio starten.");
            body["model"] = route.Model;
            core.Audit.Append(P(c).Name, "trendo-ki.anfrage", $"{route.Backend.Id}/{route.Model}");
            using var req = new HttpRequestMessage(HttpMethod.Post, route.Backend.BaseUrl + "/chat/completions") { Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json") };
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            c.Response.StatusCode = (int)res.StatusCode; c.Response.ContentType = res.Content.Headers.ContentType?.ToString() ?? "application/json";
            await (await res.Content.ReadAsStreamAsync()).CopyToAsync(c.Response.Body);
        });

        // ── N3: Trendo-Aktionen ──
        app.MapPost("/api/actions/notify", async (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.RequestAction, core.Audit);
            var o = await Body(c); var text = Str(o, "text"); if (text.Length == 0) throw new ArgumentException("„text“ fehlt.");
            host.Notify?.Invoke(text.Length > 500 ? text[..500] + " …" : text); core.Audit.Append(P(c).Name, "automatik.meldung", text.Length > 80 ? text[..80] : text);
            return Results.Ok(new { ok = true });
        });
        app.MapPost("/api/actions/calendar", async (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.RequestAction, core.Audit);
            var o = await Body(c);
            if (!DateTime.TryParse(Str(o, "start"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start)) throw new ArgumentException("„start“ bitte als JJJJ-MM-TTTHH:MM.");
            var title = Str(o, "title"); if (title.Length == 0) throw new ArgumentException("„title“ fehlt.");
            var minutes = int.TryParse(Str(o, "minutes"), out var m) ? Math.Clamp(m, 5, 24 * 60) : 60; var remind = int.TryParse(Str(o, "remindMin"), out var rm) ? Math.Clamp(rm, 0, 30 * 24 * 60) : 30;
            var ev = new Trendo.Core.Sys.CalEvent(Guid.NewGuid().ToString("N"), start, start.AddMinutes(minutes), title.Length > 120 ? title[..120] : title, Notes: "Von einem Ablauf (n8n/Node-RED) eingetragen", RemindMin: remind);
            new Trendo.Core.Sys.Calendar(core.DataRoot).Add(ev); core.Audit.Append(P(c).Name, "automatik.termin", $"{start:yyyy-MM-dd HH:mm} {ev.Title}");
            return Results.Ok(new { ok = true, uid = ev.Uid });
        });
        app.MapPost("/api/actions/note", async (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.RequestAction, core.Audit);
            var o = await Body(c); var title = Str(o, "title"); var text = Str(o, "text");
            if (title.Length == 0 || text.Length == 0) throw new ArgumentException("„title“ und „text“ nötig.");
            var item = new KnowledgeStore(core.DataRoot, core.Audit).Add(title, text.Length > 50_000 ? text[..50_000] : text, "Ablauf (n8n/Node-RED)", "automatik");
            return Results.Ok(new { ok = true, id = item.Id });
        });
        app.MapPost("/api/actions/letter-draft", async (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.RequestAction, core.Audit);
            var o = await Body(c);
            if (host.SaveLetterDraft is null) throw new InvalidOperationException("Brief-Entwürfe gehen nur, solange Trendo läuft.");
            var file = host.SaveLetterDraft(Str(o, "recipient"), Str(o, "subject"), Str(o, "body"));
            core.Audit.Append(P(c).Name, "automatik.briefentwurf", Path.GetFileName(file));
            return Results.Ok(new { ok = true, file = Path.GetFileName(file), sent = false });
        });
        app.MapGet("/api/workflows", (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.ViewProjects, core.Audit);
            return Results.Ok(host.ListWorkflows?.Invoke() ?? []);
        });
        app.MapPost("/api/workflows/{id}/run", async (HttpContext c, string id) =>
        {
            Rbac.Demand(P(c), Permission.RequestAction, core.Audit);
            if (host.RunWorkflow is null) throw new InvalidOperationException("Trendo-Abläufe laufen nur, solange Trendo läuft.");
            var o = await Body(c, allowEmpty: true);
            return Results.Ok(await host.RunWorkflow(id, Str(o, "dryRun") is "true" or "True"));
        });
    }

    static async Task<JsonObject> Body(HttpContext c, bool allowEmpty = false)
    {
        var t = await new StreamReader(c.Request.Body).ReadToEndAsync();
        if (t.Length > 200_000) throw new ArgumentException("Anfrage zu groß.");
        if (t.Trim().Length == 0 && allowEmpty) return [];
        try { return JsonNode.Parse(t) as JsonObject ?? throw new ArgumentException("JSON-Objekt erwartet."); } catch (JsonException) { throw new ArgumentException("Kein gültiges JSON."); }
    }
    static string Str(JsonObject o, string k) => o[k] is JsonValue v ? v.ToString() : "";
}
