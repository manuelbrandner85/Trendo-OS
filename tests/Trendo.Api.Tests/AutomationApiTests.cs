using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Api;
using Trendo.Core;
using Xunit;

namespace Trendo.Api.Tests;

/// <summary>n8n/Node-RED-Anbindung (N2/N3): „Trendo KI“ leitet an die lokale KI weiter, Trendo-Aktionen mit Schlüssel und Rechten, nichts ohne Schlüssel.</summary>
public class AutomationApiTests
{
    sealed class FakeKi : HttpMessageHandler
    {
        public string? LastBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var u = r.RequestUri!.AbsoluteUri;
            if (u.StartsWith("http://127.0.0.1:11434/v1/models")) return new(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"id":"llama3.2"}]}""") };
            if (u.StartsWith("http://127.0.0.1:11434/v1/chat/completions")) { LastBody = await r.Content!.ReadAsStringAsync(ct); return new(HttpStatusCode.OK) { Content = new StringContent("""{"choices":[{"message":{"role":"assistant","content":"Hallo von Ollama"}}]}""", Encoding.UTF8, "application/json") }; }
            return new(HttpStatusCode.NotFound);
        }
    }

    [Fact(DisplayName = "API Automatik: Trendo KI – Modelle aller lokalen KIs mit Anbieter-Vorsatz, Chat wird an Ollama weitergeleitet (Vorsatz entfernt); ohne Schlüssel 401; Aktionen Meldung, Termin, Notiz, Brief-Entwurf, Trendo-Ablauf")]
    public async Task Trendo_ki_and_actions()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-auto-" + Guid.NewGuid().ToString("N")[..6]);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()); var tokens = new ApiTokens(core.Secrets);
        var ki = new FakeKi(); var notes = new List<string>(); var ran = new List<(string, bool)>();
        var host = new AutomationHost
        {
            KiHandler = ki, Notify = notes.Add,
            ListWorkflows = () => [new { id = "w1", name = "Fotos ordnen" }],
            RunWorkflow = (id, dry) => { ran.Add((id, dry)); return Task.FromResult<object>(new { ok = true }); },
            SaveLetterDraft = (to, s, b) => Path.Combine(root, "entwurf.json"),
        };
        await using var api = await LocalApi.StartAsync(core, tokens, "", new Probe(), 0, host);
        using var http = new HttpClient { BaseAddress = api.BaseAddress };
        HttpRequestMessage Req(HttpMethod m, string url, object? body = null, bool auth = true)
        {
            var q = new HttpRequestMessage(m, url); if (auth) q.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetOrCreate(Role.User));
            if (body is not null) q.Content = JsonContent.Create(body); return q;
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Req(HttpMethod.Get, "/v1/models", auth: false))).StatusCode);
        var models = JsonNode.Parse(await (await http.SendAsync(Req(HttpMethod.Get, "/v1/models"))).Content.ReadAsStringAsync())!;
        Assert.Equal("ollama/llama3.2", models["data"]![0]!["id"]!.GetValue<string>());
        var chat = await http.SendAsync(Req(HttpMethod.Post, "/v1/chat/completions", new { model = "ollama/llama3.2", messages = new[] { new { role = "user", content = "Hallo" } } }));
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode); Assert.Contains("Hallo von Ollama", await chat.Content.ReadAsStringAsync());
        Assert.Equal("llama3.2", JsonNode.Parse(ki.LastBody!)!["model"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(Req(HttpMethod.Post, "/v1/chat/completions", new { model = "lmstudio/x", messages = Array.Empty<object>() }))).StatusCode); // LM Studio läuft nicht
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Req(HttpMethod.Post, "/api/actions/notify", new { text = "Druck fertig" }))).StatusCode); Assert.Equal(["Druck fertig"], notes);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Req(HttpMethod.Post, "/api/actions/calendar", new { title = "Rechnung zahlen", start = "2026-11-20T09:00", minutes = 15 }))).StatusCode);
        Assert.Contains(new Trendo.Core.Sys.Calendar(root).All(), e => e.Title == "Rechnung zahlen" && e.Start == new DateTime(2026, 11, 20, 9, 0, 0));
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(Req(HttpMethod.Post, "/api/actions/calendar", new { title = "x", start = "morgen" }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Req(HttpMethod.Post, "/api/actions/note", new { title = "Nachrichten", text = "• Punkt" }))).StatusCode);
        Assert.Contains(new KnowledgeStore(root, core.Audit).All(), i => i.Title == "Nachrichten");
        var draft = JsonNode.Parse(await (await http.SendAsync(Req(HttpMethod.Post, "/api/actions/letter-draft", new { recipient = "Amt", subject = "Antrag", body = "…" }))).Content.ReadAsStringAsync())!;
        Assert.False(draft["sent"]!.GetValue<bool>());
        Assert.Contains("Fotos ordnen", await (await http.SendAsync(Req(HttpMethod.Get, "/api/workflows"))).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Req(HttpMethod.Post, "/api/workflows/w1/run", new { dryRun = true }))).StatusCode); Assert.Equal([("w1", true)], ran);
        Assert.Contains(core.Audit.ReadAll(), a => a.Action == "trendo-ki.anfrage");
        Assert.Contains(core.Audit.ReadAll(), a => a.Action == "automatik.termin");
    }
}
