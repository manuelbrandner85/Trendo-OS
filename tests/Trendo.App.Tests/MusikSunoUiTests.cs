using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SkiaSharp;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik wie Suno V1–V5 (07.11.2026): Navigation, Erstellen-Spalte, Mehr Optionen, Bibliothek, Remix.</summary>
public class MusikSuno_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static MainWindow? _w;
    static async Task Until(Func<bool> ok) { for (var i = 0; i < 300 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Pump(); Assert.True(ok(), "Meldung: " + _w?.TryFind<TextBlock>("ms-msg")?.Text); }

    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Requests = []; int _n; public byte[]? Audio;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            static string W(string d) => "{\"data\":" + d + ",\"code\":200,\"error\":null}";
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task":
                    var body = r.Content is MultipartFormDataContent mp ? string.Join("|", await Task.WhenAll(mp.Select(async p => p.Headers.ContentDisposition?.Name?.Trim('"') + "=" + (p is StringContent ? await p.ReadAsStringAsync(ct) : "<datei>")))) : await r.Content!.ReadAsStringAsync(ct);
                    lock (Requests) Requests.Add(body);
                    return J(W($"{{\"task_id\":\"t{++_n}\"}}"));
                case "/query_result": return J(W($"[{{\"task_id\":\"t{_n}\",\"status\":1,\"result\":\"[{{\\\"file\\\":\\\"/v1/audio?path=c.mp3\\\"}}]\"}}]"));
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Audio ?? Encoding.ASCII.GetBytes("ID3")) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    [AvaloniaFact(DisplayName = "Suno V1/V2/V3: Navigation (Erstellen, Studio, Bibliothek, Stimmen), Abspielleiste unten; Modellwahl, Bild → Stimmung, Idee-Würfel; Mehr Optionen gehen an die Musik-KI")]
    public async Task Layout_create_options()
    {
        var (w, _, root) = Ui.Open(); _w = w; Directory.CreateDirectory(root);
        var fake = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(fake)); w.AceBaseReadyOverride = false;
        try
        {
            w.Show("musik-studio"); Pump();
            foreach (var p in new[] { "erstellen", "studio", "bibliothek", "stimmen" }) Assert.NotNull(w.TryFind<Button>("ms-nav-" + p));
            Assert.NotNull(w.TryFind<Control>("ms-player")); // Abspielleiste unten
            w.Find<Button>("ms-nav-studio").Press(); Pump(); Assert.Equal("studio", w.MsCurrentPage); Assert.NotNull(w.TryFind<Button>("ms-mode-beat"));
            w.Find<Button>("ms-nav-stimmen").Press(); Pump(); Assert.NotNull(w.TryFind<Control>("ms-voices-page"));
            w.Find<Button>("ms-nav-bibliothek").Press(); Pump(); Assert.NotNull(w.TryFind<Control>("ms-collections"));
            w.Find<Button>("ms-nav-erstellen").Press(); Pump(); Assert.NotNull(w.TryFind<Button>("ms-create")); Assert.Null(w.TryFind<Button>("ms-mode-beat"));
            // Würfel füllt die Beschreibung
            w.Find<Button>("ms-dice").Press(); Pump(); Assert.Contains(" über ", w.Find<TextBox>("ms-desc").Text);
            // Bild → Stimmung (sonniges, warmes Bild)
            var png = Path.Combine(root, "sonne.png");
            using (var bmp = new SKBitmap(32, 32)) { bmp.Erase(new SKColor(255, 222, 125)); using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 90); File.WriteAllBytes(png, data.ToArray()); }
            w.PickOverride = _ => Task.FromResult<string[]?>([png]);
            w.Find<Button>("ms-ref-image").Press(); await Until(() => w.Find<TextBox>("ms-desc").Text!.Contains("sonnig"));
            Assert.Contains("Stimmung aus dem Bild", w.Find<TextBlock>("ms-msg").Text);
            // Gründlich ohne Grundmodell → Hinweis, kein Auftrag
            w.MsModelChoice = "gruendlich"; w.Find<Button>("ms-create").Press(); Pump();
            Assert.Contains("Grundmodell", w.Find<TextBlock>("ms-msg").Text); Assert.Empty(fake.Requests);
            // Mehr Optionen → Auftrag
            w.MsModelChoice = "schnell";
            w.MsOptions = new MusicSuno.Options(Exclude: "Schlagzeug", VoiceGender: MusicSuno.Voice.Male, Seconds: 95, Weirdness: 90);
            w.Show("musik-studio"); Pump();
            w.Find<TextBox>("ms-desc").Text = "Lied über den Hafen, Schlagzeug laut"; w.Find<Button>("ms-create").Press();
            await Until(() => fake.Requests.Count > 0 && new SongLibrary(w.Core.DataRoot).Visible().Count > 0);
            var req = string.Join("\n", fake.Requests);
            Assert.Contains("\"lm_negative_prompt\":\"Schlagzeug\"", req); Assert.Contains("\"audio_duration\":95", req); Assert.Contains("lm_temperature", req);
            Assert.Contains("male vocals", req); Assert.DoesNotContain("Schlagzeug laut", req);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Suno V4: Playlist anlegen (mit gewähltem Lied), Ansicht Playlist/Arbeitsbereich, Filter, Raster, Seitenzahl")]
    public void Library()
    {
        var (w, _, root) = Ui.Open(); _w = w; Directory.CreateDirectory(root);
        try
        {
            var lib = new SongLibrary(w.Core.DataRoot);
            string F(string n) { var f = Path.Combine(root, n + ".mp3"); File.WriteAllBytes(f, [0x49, 0x44, 0x33]); return f; }
            var a = lib.Add(new MusicSong { Title = "Hafenlied", File = F("a"), Seconds = 120 });
            var b = lib.Add(new MusicSong { Title = "Nachtzug", File = F("b"), Seconds = 200, Lyrics = "[Instrumental]" });
            w.MusicStudioSelect(a.Id); w.Show("musik-studio"); w.MsPage("bibliothek"); Pump();
            w.Find<TextBox>("ms-coll-name").Text = "Lieblinge"; w.Find<Button>("ms-coll-new-playlist").Press(); Pump();
            var pl = w.MusicColl.OfKind(MusicCollections.Playlist).Single(); Assert.Equal([a.Id], pl.SongIds);
            Assert.Equal(["Hafenlied"], w.MsQuery().Page.Select(s => s.Title)); // Ansicht springt in die neue Playlist
            w.MsLibraryView(filter: "instrumental"); Assert.Equal(["Nachtzug"], w.MsQuery().Page.Select(s => s.Title));
            var ws = w.MusicColl.Create(MusicCollections.Workspace, "Filmmusik"); w.MusicColl.MoveToWorkspace(b.Id, ws.Id);
            w.MsLibraryView(); Assert.Equal(["Hafenlied"], w.MsQuery().Page.Select(s => s.Title));
            w.MsLibraryView(workspace: ws.Id, grid: true); Pump();
            Assert.NotNull(w.TryFind<Control>("ms-grid-view")); Assert.NotNull(w.TryFind<Button>("ms-tile-0")); Assert.Equal("1 Lied", w.Find<TextBlock>("ms-count").Text);
            w.Find<Button>("ms-coll-del-" + pl.Id).Press(); Pump();
            Assert.Empty(w.MusicColl.OfKind(MusicCollections.Playlist)); Assert.Equal(2, lib.Visible().Count); // Lieder bleiben
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Suno V6: Vollbild-Editor – Etikett an der Marke, Zeile nach Wunsch, 3 Varianten wählen, ganzer Text, Texter-Beispiel fließt in die Anweisung")]
    public async Task Lyrics_editor()
    {
        var (w, _, _) = Ui.Open(); _w = w;
        var prompts = new List<string>();
        w.MusicLyricsOverride = p => { prompts.Add(p); return Task.FromResult(p.Contains("drei Varianten") ? "1. Wir tanzen bis zum Morgenlicht\n2. Wir singen laut und frieren nicht\n3. Wir lachen und vergessen nicht"
            : p.Contains("Überarbeite") ? "[Strophe]\nNeue Strophe hier\n[Refrain]\nNeuer Refrain da" : "wir rollen leise durch die Nacht"); };
        try
        {
            w.Show("musik-studio"); w.MusicStudioProMode(true); w.Show("musik-studio"); Pump();
            w.MsLyricsText = "[Strophe]\nDie Straße glänzt im Abendlicht\nwir fahren los und halten nicht";
            w.Find<Button>("ms-lyrics-full").Press(); Pump();
            Assert.NotNull(w.TryFind<TextBox>("ms-lyrics-big")); Assert.Contains("2 Zeilen", w.Find<TextBlock>("ms-lyf-stats").Text);
            var box = w.Find<TextBox>("ms-lyrics-big"); box.CaretIndex = box.Text!.Length; w.Find<Button>("ms-lyf-tag-Refrain").Press(); Pump();
            Assert.EndsWith("[Refrain]\n", w.MsLyricsText);
            Assert.True(await w.MsLyricsLine(2, "ruhiger")); Assert.Contains("wir rollen leise durch die Nacht", w.MsLyricsText); Assert.Contains("ruhiger", prompts[^1]);
            Assert.Equal(3, await w.MsLyricsVariants(1)); w.Show("musik-studio"); Pump();
            w.Find<Button>("ms-ly-pick-1").Press(); Pump();
            Assert.Contains("Wir singen laut und frieren nicht", w.MsLyricsText); Assert.Empty(w.MsLineVariants);
            Assert.Equal(0, await w.MsLyricsVariants(0)); // Abschnittsmarke ist keine Zeile
            // Texter: Beispiel speichern, fließt in die nächste Anweisung
            w.Show("musik-studio"); Pump();
            w.Find<TextBox>("ms-lyricist-name").Text = "Meine Balladen"; w.Find<TextBox>("ms-lyrics-big").Text = "Mein Hafen schläft\nim grauen Licht\ndie Möwen kreisen\nund schweigen nicht";
            w.Find<Button>("ms-lyricist-save").Press(); Pump();
            Assert.Single(w.LyricistStore.All()); Assert.Contains("Meine Balladen", w.Find<TextBlock>("ms-msg").Text);
            Assert.True(await w.MsLyricsWhole("mehr Bilder")); Assert.Contains("Mein Hafen schläft", prompts[^1]); Assert.StartsWith("[Strophe]\nNeue Strophe", w.MsLyricsText);
            w.Show("musik-studio"); Pump(); w.Find<Button>("ms-lyrics-full-close").Press(); Pump();
            Assert.Null(w.TryFind<TextBox>("ms-lyrics-big")); Assert.Contains("Neue Strophe", w.Find<TextBox>("ms-lyrics").Text); // Text bleibt
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Suno V7/V8: Reiter Lieder/Sounds/Sprache; One-Shot und Loop aus dem Stück der Musik-KI (Rohstück aufgehoben); Sprache mit Musik (echtes FFmpeg, Musik abgesenkt)")]
    public async Task Sounds_and_speech()
    {
        var ff = Cli.Find("ffmpeg");
        var (w, _, root) = Ui.Open(); _w = w; Directory.CreateDirectory(root);
        var fake = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(fake));
        try
        {
            // Stück mit Schlägen bei 100 BPM und einem Einsatz nach 1 s
            const int rate = 22050; var x = new float[rate * 14]; var beat = (int)(rate * 0.6);
            for (var b = 0; b < 21; b++) { var st = rate + b * beat; for (var i = 0; i < rate / 8 && st + i < x.Length; i++) x[st + i] = (float)(0.7 * Math.Sin(2 * Math.PI * 90 * i / rate) * Math.Exp(-i / (rate * 0.03))); }
            var wav = Path.Combine(root, "stueck.wav"); Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(rate, [x, (float[])x.Clone()]), wav); fake.Audio = File.ReadAllBytes(wav);
            w.Show("musik-studio"); Pump();
            w.Find<Button>("ms-kind-sounds").Press(); Pump();
            Assert.NotNull(w.TryFind<TextBox>("ms-sound-desc")); Assert.Null(w.TryFind<Button>("ms-create"));
            var shot = await w.MsMakeSound("Bassdrum, trocken", loop: false);
            Assert.NotNull(shot); Assert.Equal("sound", shot!.Kind); Assert.EndsWith("(One-Shot).wav", shot.File); Assert.InRange(shot.Seconds, 0.05, 1.0);
            Assert.Contains("one shot", fake.Requests[^1]);
            var loop = await w.MsMakeSound("Beat", loop: true, bpm: 100, bars: 2);
            Assert.NotNull(loop); Assert.InRange(Trendo.Core.Media.AudioFile.Read(loop!.File).Seconds, 4.7, 4.9); // 2 Takte bei 100 BPM = 4,8 s
            Assert.Contains("\"bpm\":100", fake.Requests[^1]);
            Assert.True(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(loop.File)!, ".roh")).Length >= 2);
            await Assert.ThrowsAsync<ArgumentException>(() => w.MsMakeSound("Beat", loop: true, bars: 3));
            // Sprache
            w.Find<Button>("ms-kind-sprache").Press(); Pump(); Assert.NotNull(w.TryFind<TextBox>("ms-speech-text"));
            w.MsSpeechTtsOverride = (text, outFile) => { Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(22050, [Enumerable.Range(0, 22050 * 3).Select(i => (float)(0.4 * Math.Sin(i * 0.06))).ToArray()]), outFile); return Task.FromResult(true); };
            if (ff is null) w.MsSpeechMixOverride = a => { File.WriteAllBytes(a[^1], File.ReadAllBytes(wav)); return new Cli.Result(0, "", ""); };
            var sp = await w.MsMakeSpeech("Es war einmal ein kleiner Leuchtturm.", "sanftes Klavier");
            Assert.NotNull(sp); Assert.Equal("sprache", sp!.Kind); Assert.Equal("Es war einmal ein kleiner Leuchtturm.", sp.Lyrics); Assert.Equal(8.0, sp.Seconds, 1);
            Assert.Contains("background music for spoken word", fake.Requests[^1]); Assert.Contains("sanftes Klavier", fake.Requests[^1]);
            if (ff is not null) Assert.InRange(Trendo.Core.Media.AudioFile.Read(sp.File).Seconds, 7.9, 8.1);
            Assert.Null(await w.MsMakeSpeech("  ", ""));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Suno V5: Remix-Knopf je Lied; Beschreibung wiederverwenden; Zuschneiden und Abschnitt entfernen (echtes FFmpeg); Remaster als eigene Fassung")]
    public async Task Remix()
    {
        var ff = Cli.Find("ffmpeg");
        var (w, _, root) = Ui.Open(); _w = w; Directory.CreateDirectory(root);
        var fake = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(fake));
        try
        {
            var lib = new SongLibrary(w.Core.DataRoot);
            var wav = Path.Combine(root, "lied.wav"); Trendo.Core.Media.AudioFile.WriteWav(new Trendo.Core.Media.AudioBuf(16000, [Enumerable.Range(0, 160000).Select(i => (float)(0.3 * Math.Sin(i * 0.05))).ToArray()]), wav);
            var s = lib.Add(new MusicSong { Title = "Hafenlied", Style = "Indie-Pop, Gitarre", Lyrics = "[Strophe]\nDer Hafen schläft", Language = "es", Bpm = 96, Key = "A minor", File = wav, Seconds = 10 });
            w.MusicStudioSelect(s.Id); w.Show("musik-studio"); Pump();
            Assert.NotNull(w.TryFind<Button>("ms-remix-0"));
            Assert.Contains(w.MsRemixActions(s), x => x.Id == "remaster"); Assert.Contains(w.MsRemixActions(s), x => x.Id == "crop");
            w.MsReusePrompt(s.Id); Pump();
            Assert.Equal("erstellen", w.MsCurrentPage); Assert.Equal("Indie-Pop, Gitarre", w.Find<TextBox>("ms-style").Text); Assert.Contains("Der Hafen schläft", w.Find<TextBox>("ms-lyrics").Text); Assert.Equal("96", w.Find<TextBox>("ms-bpm").Text);
            if (ff is not null)
            {
                var keep = await w.MsCrop(s.Id, 2, 5, keep: true); Assert.NotNull(keep);
                var k = Trendo.Core.Media.AudioFile.Read(keep!.File); Assert.InRange(k.Seconds, 2.9, 3.1); Assert.Equal("gekuerzt", keep.Kind); Assert.Equal(s.Id, keep.ParentId);
                var cut = await w.MsCrop(s.Id, 2, 5, keep: false); Assert.InRange(Trendo.Core.Media.AudioFile.Read(cut!.File).Seconds, 6.9, 7.1);
                Assert.True(File.Exists(wav)); // Original bleibt
            }
            await Assert.ThrowsAsync<ArgumentException>(() => w.MsCrop(s.Id, 0, 10, keep: false));
            await Assert.ThrowsAsync<ArgumentException>(() => w.MsCrop(s.Id, 3, 3.2, keep: true));
            var rem = await w.MsRemaster(s.Id);
            Assert.NotNull(rem); Assert.Equal("Hafenlied – Remaster", rem!.Title); Assert.Equal("remaster", rem.Kind);
            Assert.Contains("task_type=cover", fake.Requests[^1]); Assert.Contains("audio_cover_strength=0.85", fake.Requests[^1]);
        }
        finally { w.Close(); }
    }
}
