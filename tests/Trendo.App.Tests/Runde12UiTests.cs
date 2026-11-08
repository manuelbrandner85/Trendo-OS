using System.Net;
using System.Text;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 12 – X-05: OpenRouter-Weg bis zur HTTP-Antwort, ohne echtes Netz.</summary>
public class Runde12_Ui
{
    sealed class Router(Func<string, HttpResponseMessage> chat) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get)
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"bezahlt/teuer\"},{\"id\":\"a/eins:free\"},{\"id\":\"b/zwei:free\"},{\"id\":\"c/drei:free\"},{\"id\":\"d/vier:free\"}]}", Encoding.UTF8, "application/json") };
            var body = await request.Content!.ReadAsStringAsync(ct);
            var id = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("model").GetString()!;
            Asked.Add(id);
            return chat(id);
        }
    }

    static HttpResponseMessage Ok(string text) => new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"" + text + "\"}}]}", Encoding.UTF8, "application/json") };
    static HttpResponseMessage Status(HttpStatusCode code, string body = "{}", string? remaining = null)
    {
        var r = new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (remaining is not null) r.Headers.Add("X-RateLimit-Remaining", remaining);
        return r;
    }

    static CloudKiBackend OpenRouter(HttpMessageHandler h) => new(KiCatalog.Cloud.First(c => c.Id == "openrouter"), "test-schluessel", h);

    [AvaloniaFact(DisplayName = "OpenRouter: 429 und 503 eines Modells führen zum nächsten kostenlosen Modell; bezahlte Modelle werden nie gefragt")]
    public async Task Switches_on_busy()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var h = new Router(id => id switch { "a/eins:free" => Status(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"temporarily rate-limited upstream\"}}"), "b/zwei:free" => Status(HttpStatusCode.ServiceUnavailable), _ => Ok("Hallo") });
            w.KiBackendsOverride = () => [OpenRouter(h)];
            var a = await w.Intel.AskAsync(new KiRequest("Frage", AllowRemote: true));
            Assert.True(a.Ok, a.Text); Assert.Equal("Hallo", a.Text); Assert.Equal("c/drei:free", a.Model);
            Assert.Equal(["a/eins:free", "b/zwei:free", "c/drei:free"], h.Asked);
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory(DisplayName = "OpenRouter: Grenze des Kontos stoppt sofort, ohne weitere Anfragen zu verbrauchen")]
    [InlineData("{\"error\":{\"message\":\"Rate limit exceeded: free-models-per-day\"}}", null)]
    [InlineData("{}", "0")]
    public async Task Stops_on_account_limit(string body, string? remaining)
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var h = new Router(_ => Status(HttpStatusCode.TooManyRequests, body, remaining));
            w.KiBackendsOverride = () => [OpenRouter(h)];
            Assert.False((await w.Intel.AskAsync(new KiRequest("Frage", AllowRemote: true))).Ok);
            Assert.Single(h.Asked);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Bezahlter Dienst: Fehler 429 wechselt nie das Modell")]
    public async Task Paid_never_switches()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var svc = KiCatalog.Cloud.First(c => c.Id != "openrouter");
            var h = new Router(_ => Status(HttpStatusCode.TooManyRequests));
            var b = new CloudKiBackend(svc with { DefaultModel = "a/eins:free" }, "test-schluessel", h);
            Assert.False(b.CanSwitchModel);
            Assert.True(new CloudKiBackend(KiCatalog.Cloud.First(c => c.Id == "openrouter"), "k", h).CanSwitchModel);
            w.KiBackendsOverride = () => [b];
            Assert.False((await w.Intel.AskAsync(new KiRequest("Frage", AllowRemote: true))).Ok);
            Assert.Single(h.Asked);
        }
        finally { w.Close(); }
    }
}
