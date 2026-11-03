using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik „hochprofessionell“: jedes neue Lied gemastert, Liedtexte ohne KI-Floskeln.</summary>
[Collection("Claude")]
public class MusikProfiUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task": var b = await r.Content!.ReadAsStringAsync(ct); lock (Bodies) Bodies.Add(b); return J("{\"data\":{\"task_id\":\"t1\"},\"code\":200}");
                case "/query_result": return J("{\"data\":[{\"task_id\":\"t1\",\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=a.mp3\\\"}]\"}],\"code\":200}");
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3"u8.ToArray()) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    const string Measure = "{\n \"input_i\" : \"-13.40\", \"input_tp\" : \"-0.70\", \"input_lra\" : \"15.70\", \"input_thresh\" : \"-24.00\", \"target_offset\" : \"0.20\"\n}";

    [AvaloniaFact(DisplayName = "Profi: neues Lied wird gleich gemastert (messen, dann linear auf −14 LUFS), Datei ersetzt, „✓ Gemastert“; ohne FFmpeg bleibt das Lied wie es ist")]
    public async Task Auto_master()
    {
        var (w, _, root) = Ui.Open();
        var runs = new List<string[]>();
        w.AceClientOverride = new AceStepClient(new HttpClient(new FakeAce()));
        w.MasterRunOverride = a =>
        {
            runs.Add(a);
            if (a.Contains("null")) return Task.FromResult(new Cli.Result(0, "", "… loudnorm\n" + Measure));
            File.WriteAllBytes(a[^1], new byte[4000]); // gemasterte Fassung
            return Task.FromResult(new Cli.Result(0, "", ""));
        };
        try
        {
            w.Show("musik-studio"); await Pump();
            var made = await w.MusicStudioCreate(new AceStepClient.Job("pop", "[Refrain]\nla la", 30, "de"), "Sommer"); await Pump();
            var s = new SongLibrary(root).Get(made.Single().Id)!;
            Assert.True(s.Mastered);
            Assert.Equal(2, runs.Count);
            Assert.Contains("measured_I=-13.40", string.Join(" ", runs[1]));
            Assert.Equal(4000, new FileInfo(s.File).Length);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(s.File)!, ".trendo-master-*"));
            Assert.Contains("Gemastert auf −14 LUFS", w.Find<TextBlock>("ms-msg").Text);
            Assert.Equal("✓ Gemastert", w.Find<Button>("ms-master").Content);
            // Messung kaputt: nichts wird ersetzt
            w.MasterRunOverride = _ => Task.FromResult(new Cli.Result(1, "", "Fehler"));
            Assert.False(await w.MsMaster(s.Id));
            Assert.Contains("nicht messen", w.Find<TextBlock>("ms-msg").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Profi: Liedtext der Text-KI mit KI-Floskeln wird einmal gezielt überarbeitet, bevor gesungen wird; die Songwriter-Regeln stehen im Auftrag")]
    public async Task Lyrics_without_floskeln()
    {
        var (w, _, _) = Ui.Open();
        var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace));
        var asked = new List<string>();
        w.MusicLyricsOverride = p =>
        {
            asked.Add(p);
            return Task.FromResult(asked.Count == 1 ? "[Strophe]\nIm Neonlicht der Stadt\n[Refrain]\nHerz in Flammen" : "[Strophe]\nAm Kiosk brennt noch Licht\n[Refrain]\nDu wartest am Gleis");
        };
        try
        {
            w.Show("musik-studio"); await Pump();
            await w.MsCreateWithVoice(new AceStepClient.Job("fröhlicher Pop über den Sommer", "", 60, "de"), "Sommer"); await Pump();
            Assert.Equal(2, asked.Count);
            Assert.Contains("erfahrener Songwriter", asked[0]);
            Assert.Contains("„neonlicht“", asked[1]); Assert.Contains("„herz in flammen“", asked[1]);
            var body = ace.Bodies.Single();
            Assert.Contains("Am Kiosk brennt noch Licht", body); Assert.DoesNotContain("Neonlicht", body);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Y1: YouTube-Link → Titel/Interpret per oEmbed (kein Download), Stil aus dem Web, Titel vorgeschlagen, Profi-Modus; falscher Link = Hinweis")]
    public async Task From_youtube()
    {
        var (w, _, _) = Ui.Open();
        var urls = new List<Uri>();
        w.SongFactsFetchOverride = u => { lock (urls) urls.Add(u); return Task.FromResult<string?>(u.Host == "www.youtube.com"
            ? """{"title":"Erika Muster - Sommerwind (Official Video)","author_name":"Erika Muster"}""" : null); };
        w.MusicLyricsOverride = _ => Task.FromResult("summer pop, acoustic guitar, female vocals");
        try
        {
            w.Show("musik-studio"); await Pump();
            Assert.Equal(("Erika Muster", "Sommerwind"), await w.MsFromYouTube("https://youtu.be/abcdefghijk"));
            Assert.Contains(urls, u => u.AbsolutePath == "/oembed");
            Assert.DoesNotContain(urls, u => u.AbsolutePath.Contains("videoplayback") || u.Host.Contains("googlevideo"));
            await Pump();
            Assert.Equal("Sommerwind (neu)", w.Find<TextBox>("ms-title").Text);
            Assert.Contains("summer pop", w.Find<TextBox>("ms-style").Text);
            Assert.Contains("nichts heruntergeladen", w.Find<TextBlock>("ms-msg").Text);
            Assert.Null(await w.MsFromYouTube("https://evil.example/x"));
            Assert.Contains("kein YouTube-Link", w.Find<TextBlock>("ms-facts-result").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Profi: „KI-Floskeln prüfen“ unter dem Liedtext nennt gefundene Phrasen oder meldet „keine gefunden“")]
    public async Task Check_button()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.MusicStudioProMode(true); w.Show("musik-studio"); await Pump();
            w.Find<TextBox>("ms-lyrics").Text = "[Refrain]\nWir tanzen im Regen"; await Pump();
            w.Find<Button>("ms-lyrics-floskeln").Press(); await Pump();
            Assert.Contains("„tanzen im regen“", w.Find<TextBlock>("ms-msg").Text);
            w.Find<TextBox>("ms-lyrics").Text = "[Refrain]\nDer letzte Bus fährt ohne mich"; await Pump();
            w.Find<Button>("ms-lyrics-floskeln").Press(); await Pump();
            Assert.Contains("Keine typischen KI-Floskeln", w.Find<TextBlock>("ms-msg").Text);
        }
        finally { w.Close(); }
    }
}
