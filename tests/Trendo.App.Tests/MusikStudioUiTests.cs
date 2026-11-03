using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Studio „Trendo Musik“ (freigegeben 03.11.2026): Lieder mit ACE-Step – Einfach/Profi, Varianten, Abschnitt neu, Cover, Bibliothek. ACE-Step ist nachgebildet.
public class MusikStudio_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static MainWindow? _w;
    static async Task Until(Func<bool> ok) { for (var i = 0; i < 300 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Pump(); Assert.True(ok(), "Meldung: " + _w?.TryFind<TextBlock>("ms-msg")?.Text); }

    /// <summary>Nachgebildetes ACE-Step: merkt sich jeden Auftrag; Varianten-Aufträge liefern 2 Lieder, Abschnitt/Cover (hochgeladen) 1.</summary>
    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Requests = []; public readonly List<string> ContentTypes = []; int _n;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            static string W(string d) => "{\"data\":" + d + ",\"code\":200,\"error\":null}";
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task":
                    var body = await r.Content!.ReadAsStringAsync(ct);
                    lock (Requests) { Requests.Add(body); ContentTypes.Add(r.Content.Headers.ContentType?.MediaType ?? ""); }
                    return J(W($"{{\"task_id\":\"t{++_n}\"}}"));
                case "/query_result":
                    var two = Requests[^1].Contains("\"batch_size\":2");
                    var files = two ? "[{\\\"file\\\":\\\"/v1/audio?path=a.mp3\\\"},{\\\"file\\\":\\\"/v1/audio?path=b.mp3\\\"}]" : "[{\\\"file\\\":\\\"/v1/audio?path=c.mp3\\\"}]";
                    return J(W($"[{{\"task_id\":\"t{_n}\",\"status\":1,\"result\":\"{files}\"}}]"));
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("ID3")) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    [AvaloniaFact(DisplayName = "Trendo Musik: Einfach – ein Satz → zwei Varianten in „Meine Lieder“; ohne Beschreibung nur Hinweis; Profi mit Tempo, Tonart, Takt, [Strophe]/[Refrain] und Spanisch")]
    public async Task Create_simple_and_pro()
    {
        var (w, _, _) = Ui.Open(); _w = w;
        var fake = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(fake));
        try
        {
            w.Show("musik-studio"); Pump();
            Assert.NotNull(w.TryFind<TextBlock>("ms-empty"));
            w.Find<Button>("ms-create").Press(); Pump();
            Assert.Contains("Beschreibe zuerst", w.Find<TextBlock>("ms-msg").Text); Assert.Empty(fake.Requests);

            w.Find<TextBox>("ms-desc").Text = "fröhliches Sommerlied über einen Roadtrip";
            w.Find<Button>("ms-create").Press();
            await Until(() => new SongLibrary(w.Core.DataRoot).Visible().Count == 2);
            var req = fake.Requests[0];
            Assert.Contains("\"batch_size\":2", req); Assert.Contains("\"vocal_language\":\"de\"", req); Assert.Contains("\"thinking\":true", req); Assert.Contains("\"audio_duration\":180", req);
            Assert.Contains("Varianten fertig", w.Find<TextBlock>("ms-msg").Text);
            Assert.NotNull(w.TryFind<Button>("ms-play-1")); Assert.Equal("Fröhliches Sommerlied über einen Roadtrip (1)", w.Find<TextBlock>("ms-detail-title").Text);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "musik.studio.fertig");

            // Profi
            w.Find<Button>("ms-mode-profi").Press(); Pump();
            w.Find<TextBox>("ms-style").Text = "Flamenco-Pop, spanische Gitarre";
            w.Find<Button>("ms-tag-Strophe").Press(); Pump();
            w.Find<TextBox>("ms-lyrics").Text = "[Strophe]\nBajo la luna\n[Refrain]\nAy corazón";
            w.Find<TextBox>("ms-bpm").Text = "500"; w.Find<Button>("ms-create").Press(); Pump();
            Assert.Contains("zwischen 30 und 300", w.Find<TextBlock>("ms-msg").Text);
            w.Find<TextBox>("ms-bpm").Text = "98";
            w.Find<ComboBox>("ms-key").SelectedIndex = 1 + Array.IndexOf(AceStepClient.Keys, "A minor");
            w.Find<ComboBox>("ms-time").SelectedIndex = 2; // 3/4
            w.Find<ComboBox>("ms-language").SelectedIndex = Array.FindIndex(AceStepClient.Languages, x => x.Code == "es");
            w.Find<ComboBox>("ms-variants").SelectedIndex = 0;
            w.Find<TextBox>("ms-title").Text = "Bajo la luna";
            w.Find<Button>("ms-create").Press();
            await Until(() => new SongLibrary(w.Core.DataRoot).Visible().Count == 3);
            var pro = fake.Requests[^1];
            Assert.Contains("\"bpm\":98", pro); Assert.Contains("\"key_scale\":\"A minor\"", pro); Assert.Contains("\"time_signature\":\"3\"", pro);
            Assert.Contains("\"vocal_language\":\"es\"", pro); Assert.Contains("[Verse]", pro); Assert.Contains("[Chorus]", pro); Assert.DoesNotContain("[Strophe]", pro);
            Assert.Equal("Bajo la luna", new SongLibrary(w.Core.DataRoot).Visible()[0].Title);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Trendo Musik: Abschnitt neu und Cover laden das Lied hoch (kein Dateipfad), neue Fassung verweist aufs Original; Liedtext mit KI; Favorit, Export, Löschen in den Papierkorb")]
    public async Task Edit_and_manage()
    {
        var (w, _, root) = Ui.Open(); _w = w;
        var fake = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(fake));
        var recycled = new List<string>(); MainWindow.SongRecycleOverride = recycled.Add;
        string? exported = null; MainWindow.SongExportOverride = (src, dst) => { File.WriteAllText(dst, "RIFF"); exported = dst; return new Cli.Result(0, "", ""); };
        w.MusicLyricsOverride = p => Task.FromResult("[Strophe]\nDie Straße glänzt\n[Refrain]\nSommer, nimm uns mit");
        try
        {
            var lib = new SongLibrary(w.Core.DataRoot);
            var f = Path.Combine(root, "lied.mp3"); File.WriteAllText(f, "ID3");
            var orig = lib.Add(new MusicSong { Title = "Sommer", Style = "Pop", Lyrics = "[Verse]\nx", Seconds = 30, File = f });
            w.MusicStudioSelect(orig.Id); w.Show("musik-studio"); Pump();

            w.Find<NumericUpDown>("ms-repaint-from").Value = 10; w.Find<NumericUpDown>("ms-repaint-to").Value = 20;
            w.Find<Button>("ms-repaint").Press();
            await Until(() => lib.Visible().Any(s => s.Kind == "abschnitt"));
            Assert.Equal("multipart/form-data", fake.ContentTypes[^1]);
            Assert.Contains("repaint", fake.Requests[^1]); Assert.Contains("10.0", fake.Requests[^1]); Assert.Contains("20.0", fake.Requests[^1]);
            Assert.Contains("name=src_audio", fake.Requests[^1]); Assert.DoesNotContain("src_audio_path", fake.Requests[^1]);
            Assert.Equal(orig.Id, lib.Visible().First(s => s.Kind == "abschnitt").ParentId);

            w.MusicStudioSelect(orig.Id); w.Show("musik-studio"); Pump();
            w.Find<Button>("ms-cover").Press(); Pump();
            Assert.Contains("neuen Stil", w.Find<TextBlock>("ms-msg").Text);
            w.Find<TextBox>("ms-cover-style").Text = "Jazz-Ballade, Klavier";
            w.Find<Button>("ms-cover").Press();
            await Until(() => lib.Visible().Any(s => s.Kind == "cover"));
            Assert.Contains("cover", fake.Requests[^1]); Assert.Contains("audio_cover_strength", fake.Requests[^1]);

            // Liedtext mit KI
            w.Find<Button>("ms-mode-profi").Press(); Pump();
            w.Find<TextBox>("ms-style").Text = "Sommer-Pop";
            w.Find<Button>("ms-lyrics-ki").Press();
            await Until(() => w.TryFind<TextBox>("ms-lyrics")?.Text?.Contains("Die Straße glänzt") == true);

            // verwalten
            w.MusicStudioSelect(orig.Id); w.Show("musik-studio"); Pump();
            var n = lib.Visible().FindIndex(s => s.Id == orig.Id);
            w.Find<Button>($"ms-star-{n}").Press(); Pump(); Assert.True(lib.Get(orig.Id)!.Favorite);
            w.Find<Button>("ms-export-wav").Press(); Pump(); Assert.EndsWith(".wav", exported);
            w.Find<TextBox>("ms-rename").Text = "Sommer 2026"; w.Find<Button>("ms-rename-ok").Press(); Pump();
            Assert.Equal("Sommer 2026", lib.Get(orig.Id)!.Title);
            w.Find<Button>("ms-delete").Press(); Pump();
            Assert.Null(lib.Get(orig.Id)); Assert.Equal([f], recycled);
        }
        finally { MainWindow.SongRecycleOverride = null; MainWindow.SongExportOverride = null; w.Close(); }
    }

    [Fact(DisplayName = "Trendo Musik: Aufträge an ACE-Step geprüft – Grenzen für Länge, Tempo, Varianten; Abschnitt mind. 1 s; über 8 Minuten ohne Sprachmodell; unbekannte Tonart/Takt/Aufgabe abgelehnt")]
    public void Job_fields()
    {
        var f = AceStepClient.Fields(new("Pop", "", 900, "de", 5, Bpm: 400));
        Assert.Equal("600", f["audio_duration"]); Assert.Equal("2", f["batch_size"]); Assert.Equal("300", f["bpm"]); Assert.Equal("false", f["thinking"]); Assert.Equal("[Instrumental]", f["lyrics"]);
        Assert.Equal("true", AceStepClient.Fields(new("Pop", "", 120, "de"))["thinking"]);
        Assert.Equal("false", AceStepClient.Fields(new("Pop", "x", 120, "de", Task: "cover"))["thinking"]);
        Assert.Equal("0.10", AceStepClient.Fields(new("Pop", "x", 120, "de", Task: "cover", CoverStrength: 0))["audio_cover_strength"]);
        Assert.Throws<ArgumentException>(() => AceStepClient.Fields(new("Pop", "x", 30, "de", Task: "repaint", RepaintStart: 10, RepaintEnd: 10.5)));
        Assert.Throws<ArgumentException>(() => AceStepClient.Fields(new("Pop", "x", 30, "de", Key: "H moll")));
        Assert.Throws<ArgumentException>(() => AceStepClient.Fields(new("Pop", "x", 30, "de", TimeSignature: "5")));
        Assert.Throws<ArgumentException>(() => AceStepClient.Fields(new("Pop", "x", 30, "de", Task: "extract"))); // erprobt: mit dem Turbo-Modell unbrauchbar
        Assert.Equal(24, AceStepClient.Keys.Length);
    }
}
