using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik M1–M9 (freigegeben 03.11.2026) in der Oberfläche – ACE-Step, Analyse und Netz sind nachgebildet.</summary>
[Collection("Claude")]
public class MusikPlusUiTests
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

    [AvaloniaFact(DisplayName = "M1/M8/M6: Genre-Auswahl (Gruppe, Suche, höchstens drei), Stimme, Tonlage und Instrumente landen als ACE-Step-Begriffe im Auftrag; Tempo-Vorschlag übernehmbar")]
    public async Task Genres_voice_instruments()
    {
        var (w, _, _) = Ui.Open();
        var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace));
        w.MusicLyricsOverride = _ => Task.FromResult("[Strophe]\nAm Meer im Licht\n[Refrain]\nSommer bleib");
        try
        {
            w.Show("musik-studio"); await Pump();
            w.Find<Button>("ms-mode-profi").Press(); await Pump();
            w.Find<Button>("ms-genre-open").Press(); await Pump();
            w.Find<Button>("ms-genre-group-jazz-blues-soul").Press(); await Pump();
            Assert.NotNull(w.TryFind<Button>("ms-genre-bossa-nova")); Assert.Null(w.TryFind<Button>("ms-genre-techno"));
            w.Find<Button>("ms-genre-jazz-ballade").Press(); await Pump();
            Assert.Contains("55–80 BPM", w.Find<TextBlock>("ms-genre-hint").Text);
            w.Find<Button>("ms-genre-bpm").Press(); await Pump();
            Assert.Equal("67", w.Find<TextBox>("ms-bpm").Text);
            w.Find<ComboBox>("ms-voice").SelectedIndex = 3; await Pump(); // Duett
            w.Find<ComboBox>("ms-range").SelectedIndex = 3; await Pump(); // tief
            w.Find<Button>("ms-instr-saxophon").Press(); await Pump();
            w.Find<TextBox>("ms-style").Text = "ruhig"; await Pump();
            w.Find<Button>("ms-create").Press();
            for (var i = 0; i < 100 && ace.Bodies.Count == 0; i++) await Pump();
            var body = System.Text.Json.JsonDocument.Parse(ace.Bodies.Single()).RootElement;
            Assert.Equal("ruhig, jazz ballad, male and female duet vocals, deep low vocals, saxophone", body.GetProperty("prompt").GetString());
            Assert.Equal(67, body.GetProperty("bpm").GetInt32());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M2 „Im Stil von“: Stilmerkmale statt Namen – der Name wird entfernt, auch wenn die KI ihn nennt; ohne Text-KI ehrlicher Hinweis")]
    public async Task Style_like()
    {
        var (w, _, _) = Ui.Open();
        string? asked = null;
        w.MusicLyricsOverride = p => { asked = p; return Task.FromResult("1970s British rock, Queen-like, operatic layered vocals, piano, electric guitar, dramatic"); };
        try
        {
            w.Show("musik-studio"); await Pump();
            var traits = await w.MsStyleLike("Queen");
            Assert.Contains("KEINE Namen", asked);
            Assert.DoesNotContain("Queen", traits, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("operatic layered vocals", traits);
            w.MusicLyricsOverride = _ => Task.FromResult("");
            Assert.Null(await w.MsStyleLike("Queen"));
            Assert.Contains("Text-KI", w.Find<TextBlock>("ms-msg").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M3/M5: eigene Aufnahme hinzufügen → analysiert (Tonart, BPM, Takt, Tonumfang), Hinweis „nur privat“, „Als Vorlage“ setzt Tempo, Tonart und Takt im Profi-Modus")]
    public async Task Import_and_analyse()
    {
        var (w, _, root) = Ui.Open();
        w.SongAnalyseOverride = _ => Task.FromResult<string?>("""{"duration": 200.0, "bpm": 92.3, "key": "A minor", "key_confidence": 0.7, "meter": "3/4", "chords": ["Am","G","C"], "range": {"low": "E3", "high": "A4", "semitones": 17, "voice": "Tenor"}}""");
        try
        {
            var src = Path.Combine(root, "meine-aufnahme.mp3"); File.WriteAllBytes(src, "ID3"u8.ToArray());
            w.Show("musik-studio"); await Pump();
            var s = await w.MsImport(src); await Pump();
            for (var i = 0; i < 50 && new SongLibrary(root).Get(s!.Id)!.Analysis is null; i++) await Pump();
            var song = new SongLibrary(root).Get(s!.Id)!;
            Assert.Equal("eigen", song.Kind); Assert.Equal("A minor", song.Analysis!.Key);
            Assert.Contains("A-Moll · 92 BPM · 3/4-Takt", w.Find<TextBlock>("ms-analyse-result").Text);
            Assert.Contains("GEMA", string.Join("|", w.GetLogicalTexts()));
            w.Find<Button>("ms-analyse-take").Press(); await Pump();
            Assert.Equal("92", w.Find<TextBox>("ms-bpm").Text);
            Assert.Equal("A-Moll", w.Find<ComboBox>("ms-key").SelectedItem);
            Assert.Equal("3/4", w.Find<ComboBox>("ms-time").SelectedItem);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M7: Tonart und Tempo eines bekannten Songs (MusicBrainz + AcousticBrainz, Quelle sichtbar), übernehmbar; Archiv-Dienst ohne Messung = ehrlicher Hinweis")]
    public async Task Song_facts()
    {
        var (w, _, _) = Ui.Open();
        var ab = """{"tonal":{"key_key":"A#","key_scale":"major"},"rhythm":{"bpm":71.8}}""";
        w.SongFactsFetchOverride = u => Task.FromResult<string?>(u.Host == "musicbrainz.org"
            ? """{"recordings":[{"id":"mbid-1","title":"Bohemian Rhapsody","artist-credit":[{"name":"Queen"}],"first-release-date":"1975"}]}"""
            : ab);
        try
        {
            w.Show("musik-studio"); await Pump(); w.Find<Button>("ms-mode-profi").Press(); await Pump();
            var f = await w.MsSongFacts("Queen – Bohemian Rhapsody"); await Pump();
            Assert.Equal("Bb major", f!.Key);
            Assert.Contains("B-Dur, 72 BPM · Quelle: AcousticBrainz (CC0)", w.Find<TextBlock>("ms-facts-result").Text);
            w.Find<Button>("ms-facts-take").Press(); await Pump();
            Assert.Equal("72", w.Find<TextBox>("ms-bpm").Text);
            ab = "<html>500</html>";
            Assert.Null(await w.MsSongFacts("Queen – Bohemian Rhapsody")); await Pump();
            Assert.Contains("Archiv", w.Find<TextBlock>("ms-facts-result").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M4/M9: eingefügten Text gliedern, Refrain wiederholen, Silben zählen, Refrain eingängiger (KI)")]
    public async Task Lyrics_tools()
    {
        var (w, _, _) = Ui.Open();
        w.MusicLyricsOverride = _ => Task.FromResult("[Strophe]\nDie Straße glänzt\n[Refrain]\nSommer, Sommer, nimm uns mit");
        try
        {
            w.Show("musik-studio"); await Pump(); w.Find<Button>("ms-mode-profi").Press(); await Pump();
            w.Find<TextBox>("ms-lyrics").Text = "Die Straße glänzt\nwir fahren los\n\nSommer nimm uns mit\n\nDer Wind ist warm\n\nSommer nimm uns mit"; await Pump();
            w.Find<Button>("ms-lyrics-structure").Press(); await Pump();
            Assert.StartsWith("[Strophe]", w.Find<TextBox>("ms-lyrics").Text);
            Assert.Contains("[Refrain]\nSommer nimm uns mit", w.Find<TextBox>("ms-lyrics").Text);
            w.Find<Button>("ms-lyrics-repeat").Press(); await Pump();
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(w.Find<TextBox>("ms-lyrics").Text!, @"\[Refrain\]").Count);
            w.Find<Button>("ms-lyrics-syllables").Press(); await Pump();
            Assert.NotNull(w.TryFind<StackPanel>("ms-syllables"));
            w.Find<Button>("ms-lyrics-catchy").Press();
            for (var i = 0; i < 50 && !(w.TryFind<TextBox>("ms-lyrics")?.Text ?? "").Contains("Sommer, Sommer"); i++) await Pump();
            Assert.Contains("Sommer, Sommer, nimm uns mit", w.Find<TextBox>("ms-lyrics").Text);
        }
        finally { w.Close(); }
    }
}
