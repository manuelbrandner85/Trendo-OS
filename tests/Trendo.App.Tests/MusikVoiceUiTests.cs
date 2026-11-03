using System.Net;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 03.11.2026: Im Einfach-Modus entsteht Musik mit Stimme – nur „Instrumental“ angehakt macht sie ohne Gesang.</summary>
[Collection("Claude")]
public class MusikVoiceUiTests
{
    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task": var b0 = await r.Content!.ReadAsStringAsync(ct); lock (Bodies) Bodies.Add(b0); return J("{\"data\":{\"task_id\":\"t1\"},\"code\":200}");
                case "/query_result":
                    var res = "[{\\\"file\\\":\\\"/v1/audio?path=a.mp3\\\",\\\"lyrics\\\":\\\"[Verse 1]\\\\nAm Strand im Wind\\\",\\\"prompt\\\":\\\"bright pop\\\",\\\"metas\\\":{\\\"duration\\\":118.0}}]";
                    return J($"{{\"data\":[{{\"task_id\":\"t1\",\"status\":1,\"result\":\"{res}\"}}],\"code\":200}}");
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3"u8.ToArray()) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    static async Task<(MainWindow W, string Root, FakeAce Ace)> Open()
    {
        var (w, _, root) = Ui.Open();
        var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace));
        w.Show("musik-studio"); for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        return (w, root, ace);
    }

    [AvaloniaFact(DisplayName = "Einfach-Modus mit Stimme: die Text-KI schreibt den Liedtext (Sprache, Länge, Abschnitte) – an ACE-Step geht dieser Text, nicht „[Instrumental]“, die Länge bleibt")]
    public async Task Simple_with_lyrics_from_text_ai()
    {
        var (w, root, ace) = await Open();
        string? asked = null;
        w.MusicLyricsOverride = p => { asked = p; return Task.FromResult("[Strophe]\nDie Sonne lacht am Meer\n[Refrain]\nSommer, bleib bei mir"); };
        try
        {
            var made = await w.MsCreateWithVoice(new AceStepClient.Job("fröhliches Sommerlied am Meer", "", 60, "de"), "Sommer am Meer");
            Assert.Single(made);
            Assert.Contains("Deutsch", asked); Assert.Contains("60 Sekunden", asked);
            var body = ace.Bodies.Single();
            Assert.Contains("Sonne lacht", body); Assert.DoesNotContain("[Instrumental]", body); Assert.DoesNotContain("sample_query", body);
            Assert.Contains("\"audio_duration\":60", body);
            var s = new SongLibrary(root).Get(made[0].Id)!;
            Assert.Contains("Die Sonne lacht", s.Lyrics); Assert.Equal(60, s.Seconds);
        }
        finally { w.MusicLyricsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Einfach-Modus ohne Text-KI: ACE-Step schreibt und singt selbst (sample_query in der Sprache), gesungener Text und echte Länge landen beim Lied")]
    public async Task Simple_with_ace_sample_mode()
    {
        var (w, root, ace) = await Open();
        w.MusicLyricsOverride = _ => Task.FromResult<string>("");
        try
        {
            var made = await w.MsCreateWithVoice(new AceStepClient.Job("fröhliches Sommerlied am Meer, Frauenstimme", "", 60, "de"), "Sommer am Meer");
            var body = ace.Bodies.Single();
            Assert.Equal("fröhliches Sommerlied am Meer, Frauenstimme, sung in German", System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("sample_query").GetString());
            Assert.DoesNotContain("[Instrumental]", body); Assert.DoesNotContain("\"lyrics\"", body);
            var s = new SongLibrary(root).Get(made[0].Id)!;
            Assert.Contains("Am Strand im Wind", s.Lyrics); Assert.Equal(118, s.Seconds);
            Assert.Equal("fröhliches Sommerlied am Meer, Frauenstimme", s.Style);
        }
        finally { w.MusicLyricsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Einfach-Modus mit „Instrumental“: kein Text, kein Gesang – ACE-Step bekommt „[Instrumental]“")]
    public async Task Simple_instrumental()
    {
        var (w, _, ace) = await Open();
        var asked = false; w.MusicLyricsOverride = _ => { asked = true; return Task.FromResult("[Strophe]\nx"); };
        try
        {
            await w.MsCreateWithVoice(new AceStepClient.Job("ruhige Klaviermusik", "[Instrumental]", 60, "de"), "Klavier");
            Assert.Contains("[Instrumental]", ace.Bodies.Single()); Assert.False(asked);
        }
        finally { w.MusicLyricsOverride = null; w.Close(); }
    }
}
