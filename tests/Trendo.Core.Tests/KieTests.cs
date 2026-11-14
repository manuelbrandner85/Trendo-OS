using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Kie;
using Xunit;

namespace Trendo.Core.Tests;

public class KieTests
{
    /// <summary>Nachgebautes kie.ai: merkt sich jede Anfrage samt Kopfzeilen.</summary>
    sealed class Fake(Func<string, string, string?, (int, string)> answer) : IWebSend
    {
        public readonly List<(string Method, string Url, string? Body, IReadOnlyDictionary<string, string> Head)> Calls = [];
        public Task<WebReply> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body = null, string? contentType = null, CancellationToken ct = default)
        {
            var b = body is null ? null : Encoding.UTF8.GetString(body);
            Calls.Add((method, url, b, headers));
            var (code, text) = answer(method, url, b);
            return Task.FromResult(new WebReply(code, Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact(DisplayName = "kie.ai: Kontostand, Auftrag, Abfrage – Schlüssel nur im Kopf an kie.ai, Fehlercodes trotz HTTP 200 erkannt")]
    public async Task Client()
    {
        var states = new Queue<string>(["waiting", "generating", "success"]);
        var fake = new Fake((m, url, body) => url switch
        {
            var u when u.EndsWith("/api/v1/chat/credit") => (200, "{\"code\":200,\"msg\":\"success\",\"data\":1234.5}"),
            var u when u.EndsWith("/api/v1/jobs/createTask") => (200, "{\"code\":200,\"data\":{\"taskId\":\"t1\"}}"),
            var u when u.Contains("recordInfo") => (200, $"{{\"code\":200,\"data\":{{\"state\":\"{states.Dequeue()}\",\"resultJson\":\"{{\\\"resultUrls\\\":[\\\"https://tempfile.aiquickdraw.com/a.png\\\"]}}\",\"creditsConsumed\":4}}}}"),
            _ => (404, ""),
        });
        var c = new KieClient(fake, " geheim-123 ");
        Assert.Equal(1234.5, await c.CreditsAsync());
        Assert.Equal("Bearer geheim-123", fake.Calls[0].Head["Authorization"]);
        var m = KieModels.Find("google/nano-banana")!;
        Assert.Equal("t1", await c.CreateAsync(m.Id, m.Input(new KieInput(Prompt: "Leuchtturm", Aspect: "16:9"))));
        var sent = JsonNode.Parse(fake.Calls[1].Body!)!;
        Assert.Equal("google/nano-banana", (string?)sent["model"]);
        Assert.Equal("16:9", (string?)sent["input"]!["image_size"]);
        var waits = new List<TimeSpan>();
        var t = await c.WaitAsync("t1", (d, _) => { waits.Add(d); return Task.CompletedTask; }, TimeSpan.FromMinutes(5));
        Assert.True(t.Ok); Assert.Equal(["https://tempfile.aiquickdraw.com/a.png"], t.Urls); Assert.Equal(4, t.Credits);
        Assert.Equal(2, waits.Count); Assert.True(waits[1] > waits[0]);

        var broke = new KieClient(new Fake((_, _, _) => (200, "{\"code\":402,\"msg\":\"Insufficient\"}")), "k");
        var e = await Assert.ThrowsAsync<KieException>(() => broke.CreditsAsync());
        Assert.Equal(402, e.Code); Assert.Contains("Guthaben", e.Message);
        await Assert.ThrowsAsync<KieException>(() => new KieClient(fake, " ").CreditsAsync());
    }

    [Fact(DisplayName = "kie.ai: Ergebnisadressen aus jeder Form, nur https, ohne Stream-Vorschau und ohne Doppelte")]
    public void Urls()
    {
        Assert.Equal(["https://x/a.mp3", "https://x/c.png"], KieClient.ResultUrls("{\"data\":[{\"audio_url\":\"https://x/a.mp3\",\"stream_audio_url\":\"https://x/s\",\"image_url\":\"https://x/c.png\"},{\"audio_url\":\"https://x/a.mp3\"}]}"));
        Assert.Empty(KieClient.ResultUrls("{\"resultUrls\":[\"http://unsicher/a.png\"]}"));
        Assert.Empty(KieClient.ResultUrls("kaputt"));
        Assert.Empty(KieClient.ResultUrls(null));
    }

    [Fact(DisplayName = "kie.ai: Upload klein als Base64, groß als Mehrteil, Adresse muss https sein")]
    public async Task Upload()
    {
        var fake = new Fake((_, url, _) => (200, "{\"code\":200,\"data\":{\"downloadUrl\":\"https://tempfile.redpandaai.co/x.png\"}}"));
        var c = new KieClient(fake, "k");
        Assert.Equal("https://tempfile.redpandaai.co/x.png", await c.UploadAsync([1, 2, 3], @"C:\irgendwo\bild.png"));
        Assert.EndsWith("/api/file-base64-upload", fake.Calls[0].Url);
        Assert.Contains("\"fileName\":\"bild.png\"", fake.Calls[0].Body);
        await c.UploadAsync(new byte[KieClient.MaxBase64Bytes + 1], "gross.wav");
        Assert.EndsWith("/api/file-stream-upload", fake.Calls[1].Url);
        var bad = new KieClient(new Fake((_, _, _) => (200, "{\"code\":200,\"data\":{\"downloadUrl\":\"http://x\"}}")), "k");
        await Assert.ThrowsAsync<KieException>(() => bad.UploadAsync([1], "a.png"));
    }

    [Fact(DisplayName = "kie.ai: Modelle mit belegter Anfrageform, kein Midjourney, keine Chat-KI; Regeln gegen Nachahmung und für Einwilligung")]
    public void Models()
    {
        Assert.DoesNotContain(KieModels.All, m => m.Id.Contains("midjourney", StringComparison.OrdinalIgnoreCase) || m.Id.Contains("claude", StringComparison.OrdinalIgnoreCase) || m.Id.Contains("gpt-5"));
        Assert.Equal(KieModels.All.Count, KieModels.All.Select(m => m.Id).Distinct().Count());
        foreach (var k in Enum.GetValues<KieKind>()) Assert.NotEmpty(KieModels.Of(k));
        var suno = KieModels.Find("ai-music-api/generate")!;
        var o = suno.Input(new KieInput(Title: new string('x', 90), Style: "Indie-Pop", Lyrics: "eigener Text", Seconds: 999));
        Assert.Equal("V6", (string?)o["model"]); Assert.Equal(80, ((string)o["title"]!).Length); Assert.Equal(360, (int)o["duration"]!);
        Assert.Null(suno.Input(new KieInput(Title: "t", Style: "s", Lyrics: "x", Instrumental: true))["lyrics"]);
        Assert.NotNull(KieModels.Blocked(suno, new KieInput(Title: "t", Style: "klingt wie ein bekannter Sänger"), false));
        Assert.NotNull(KieModels.Blocked(suno, new KieInput(Title: "t"), false));
        Assert.Null(KieModels.Blocked(suno, new KieInput(Title: "t", Style: "ruhiger Indie-Pop"), false));
        var avatar = KieModels.Find("kling/ai-avatar-standard")!;
        var full = new KieInput(ImageUrls: ["a.png"], AudioUrl: "a.mp3");
        Assert.Contains("bestätige", KieModels.Blocked(avatar, full, consent: false));
        Assert.Null(KieModels.Blocked(avatar, full, consent: true));
        Assert.NotNull(KieModels.Blocked(KieModels.Find("google/nano-banana")!, new KieInput(Prompt: "nacktes Model"), false));
        Assert.Equal(".mp4", KieModels.Extension("https://x/y", KieKind.Video));
        Assert.Equal(".webp", KieModels.Extension("https://x/y.webp?sig=1", KieKind.Bild));
        Assert.Contains(KieKind.Musik, KieModels.ForStudio("STUDIO-MUSIK-01"));
        Assert.Contains(KieKind.Bild, KieModels.ForStudio("SYS-99"));
        Assert.Equal(["kling/ai-avatar-standard"], KieModels.Missing("google/nano-banana google/nano-banana-edit recraft/remove-background topaz/image-upscale kling-3-0 suno multilingual-v2"));
        Assert.Contains("Credits", KieModels.CostText(KieModels.Find("google/nano-banana")!));
    }

    [Fact(DisplayName = "kie.ai Prüfung: keine Fehlsperren bei normalen Sätzen, Namen gesperrt, lange Liedtexte erlaubt, Richtpreise, Dateiart, nur öffentliche Adressen")]
    public void Rules()
    {
        var suno = KieModels.Find("ai-music-api/generate")!;
        foreach (var ok in new[] { "ruhig wie ein see", "so schön wie der Morgen", "Lied wie ein schöner sommertag", "im Stil von 80er Synthpop", "warm, Akustikgitarre" })
            Assert.Null(KieModels.Blocked(suno, new KieInput(Title: "t", Style: ok), false));
        foreach (var bad in new[] { "like Erika Muster", "im Stil von Erika Muster", "klingt wie ein bekannter Star", "Stimme von meinem Lieblingssänger" })
            Assert.NotNull(KieModels.Blocked(suno, new KieInput(Title: "t", Style: bad), false));
        Assert.Null(KieModels.Blocked(suno, new KieInput(Title: "t", Style: "Pop", Lyrics: new string('a', 3000)), false));
        Assert.NotNull(KieModels.Blocked(suno, new KieInput(Title: "t", Style: "Pop", Lyrics: new string('a', 5001)), false));

        var kling = KieModels.Find("kling-3.0/video")!;
        Assert.Equal(75.5, KieModels.Estimate(kling, new KieInput(Seconds: 5)));
        Assert.Equal(12, KieModels.Estimate(KieModels.Find("elevenlabs/text-to-speech-multilingual-v2")!, new KieInput(Prompt: new string('x', 1500))));
        Assert.False(KieModels.PriceKnown(suno, new KieInput()));
        Assert.Contains("unbekannt", KieModels.CostText(suno));

        Assert.True(KieModels.LooksLike([0x89, 0x50, 0x4E, 0x47, 1], KieKind.Bild));
        Assert.False(KieModels.LooksLike(Encoding.UTF8.GetBytes("<html>Fehler</html>"), KieKind.Bild));
        Assert.True(KieModels.LooksLike([0, 0, 0, 0x20, 0x66, 0x74, 0x79, 0x70, 1], KieKind.Video));
        Assert.True(KieModels.LooksLike([0x49, 0x44, 0x33, 4], KieKind.Musik));

        Assert.True(KieModels.PublicHttps("https://tempfile.aiquickdraw.com/a.png"));
        foreach (var bad in new[] { "http://tempfile.aiquickdraw.com/a.png", "https://localhost/a", "https://127.0.0.1/a", "https://192.168.1.5/a", "https://10.0.0.1/a", "https://drucker/a", "https://nas.local/a" })
            Assert.False(KieModels.PublicHttps(bad));
    }

    [Fact(DisplayName = "kie.ai Warten: kurze Netzfehler und Wartung werfen den bezahlten Auftrag nicht weg")]
    public async Task Wait_retries()
    {
        var n = 0;
        var fake = new Fake((_, url, _) => ++n switch
        {
            1 => throw new HttpRequestException("weg"),
            2 => (200, "{\"code\":455,\"msg\":\"maintenance\"}"),
            _ => (200, "{\"code\":200,\"data\":{\"state\":\"success\",\"resultJson\":\"{\\\"resultUrls\\\":[\\\"https://x.example/a.png\\\"]}\"}}"),
        });
        var t = await new KieClient(fake, "k").WaitAsync("t", (_, _) => Task.CompletedTask, TimeSpan.FromMinutes(5));
        Assert.True(t.Ok); Assert.Equal(3, n);
        var never = new KieClient(new Fake((_, _, _) => (200, "{\"code\":200,\"data\":{\"state\":\"generating\"}}")), "k");
        var e = await Assert.ThrowsAsync<KieException>(() => never.WaitAsync("t", (_, _) => Task.Delay(5), TimeSpan.FromMilliseconds(30)));
        Assert.Equal(408, e.Code); Assert.Contains("Ergebnis holen", e.Message);
    }
}
