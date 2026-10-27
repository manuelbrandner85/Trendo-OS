using System.Net;
using System.Text;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Musik-KI: Anfrage und Antworten wie in der API-Doku von ACE-Step 1.5; nur der lokale Server.</summary>
public class AceStepClientTests
{
    /// <summary>Nachgebildeter ACE-Step-Server (Antworten im Format aus docs/en/API.md).</summary>
    public sealed class FakeAce : HttpMessageHandler
    {
        public string? LastRequest; public int Queries;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            string Wrap(string data) => "{\"data\":" + data + ",\"code\":200,\"error\":null,\"timestamp\":1,\"extra\":null}";
            var path = r.RequestUri!.AbsolutePath;
            if (path == "/health") return Json("{\"status\":\"ok\"}");
            if (path == "/release_task") { LastRequest = await r.Content!.ReadAsStringAsync(ct); return Json(Wrap("{\"task_id\":\"t-1\",\"status\":\"queued\"}")); }
            if (path == "/query_result")
            {
                Queries++;
                var result = "[{\\\"file\\\": \\\"/v1/audio?path=lied.mp3\\\", \\\"status\\\": 1}]";
                return Json(Wrap(Queries < 2 ? "[{\"task_id\":\"t-1\",\"status\":0,\"result\":\"[]\"}]" : $"[{{\"task_id\":\"t-1\",\"status\":1,\"result\":\"{result}\"}}]"));
            }
            if (path == "/v1/audio") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("ID3-lied")) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        static HttpResponseMessage Json(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
    }

    [Fact(DisplayName = "Musik-KI: Anfrage mit Stil, Liedtext, Länge (10–600 s), Sprache; leerer Text = instrumental")]
    public void Request_fields()
    {
        var j = AceStepClient.Request(new("Pop mit Klavier", "", 5000, "de"));
        Assert.Equal("Pop mit Klavier", j["prompt"]!.ToString());
        Assert.Equal("[Instrumental]", j["lyrics"]!.ToString());
        Assert.Equal(600, (int)j["audio_duration"]!);
        Assert.Equal("de", j["vocal_language"]!.ToString());
        Assert.Equal("mp3", j["audio_format"]!.ToString());
        Assert.Equal(1, (int)j["batch_size"]!);
    }

    [Fact(DisplayName = "Musik-KI: Auftrag abgeben, Stand abfragen, Lied holen – nur von 127.0.0.1 und /v1/audio")]
    public async Task Full_flow()
    {
        var fake = new FakeAce();
        var c = new AceStepClient(new HttpClient(fake));
        Assert.True(await c.HealthyAsync(TestContext.Current.CancellationToken));
        var id = await c.SubmitAsync(new("Jazz", "[Verse]\nHallo", 30, "de"), TestContext.Current.CancellationToken);
        Assert.Equal("t-1", id);
        Assert.Contains("\"Jazz\"", fake.LastRequest);
        Assert.Equal(0, (await c.QueryAsync(id, TestContext.Current.CancellationToken)).Status);
        var done = await c.QueryAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(1, done.Status);
        Assert.Equal("/v1/audio?path=lied.mp3", done.AudioUrl);
        var f = Path.Combine(Path.GetTempPath(), "trendo-ace-" + Guid.NewGuid().ToString("N") + ".mp3");
        try
        {
            await c.DownloadAsync(done.AudioUrl!, f, TestContext.Current.CancellationToken);
            Assert.Equal("ID3-lied", File.ReadAllText(f));
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.DownloadAsync("https://evil.example/x.mp3", f, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.DownloadAsync("/etc/passwd", f, TestContext.Current.CancellationToken));
        }
        finally { File.Delete(f); }
    }

    [Fact(DisplayName = "Musik-KI: Fehler des Servers wird verständlich gemeldet")]
    public void Failed_state()
    {
        var s = AceStepClient.ParseState("{\"data\":[{\"task_id\":\"x\",\"status\":2,\"error\":\"zu wenig Grafikspeicher\"}],\"code\":200}", "x");
        Assert.Equal(2, s.Status);
        Assert.Contains("Grafikspeicher", s.Error);
        Assert.Throws<InvalidOperationException>(() => AceStepClient.ParseState("{\"data\":null,\"code\":500,\"error\":\"kaputt\"}", "x"));
    }
}
