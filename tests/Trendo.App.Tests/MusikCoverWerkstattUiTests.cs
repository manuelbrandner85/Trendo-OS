using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Cover-Werkstatt M1–M8 (04.11.2026): Suche, eigenes Original, Bauplan, Cover mit Tempo/Tonart/Takt, Ampel mit Selbstverbesserung, Epochen-Klang, Karaoke.</summary>
public class MusikCoverWerkstatt_Ui
{
    sealed class FakeAce : HttpMessageHandler
    {
        public readonly List<string> Bodies = []; int _n;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            static HttpResponseMessage J(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
            switch (r.RequestUri!.AbsolutePath)
            {
                case "/health": return J("{\"status\":\"ok\"}");
                case "/release_task": var b = await r.Content!.ReadAsStringAsync(ct); lock (Bodies) Bodies.Add(b); _n++; return J("{\"data\":{\"task_id\":\"t" + _n + "\"},\"code\":200}");
                case "/query_result": return J("{\"data\":[{\"task_id\":\"t" + _n + "\",\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=c" + _n + ".mp3\\\"}]\"}],\"code\":200}");
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3"u8.ToArray()) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    const string Mb = """{"recordings":[{"id":"r1","title":"Mein Lied","length":184000,"first-release-date":"1961-05-02","artist-credit":[{"name":"Erika Muster"}],"releases":[{"title":"Erste Platte"}]}]}""";
    static string An(double bpm, string key, string meter, double dur) => "{\"duration\":" + dur + ",\"bpm\":" + bpm + ",\"key\":\"" + key + "\",\"key_confidence\":0.8,\"meter\":\"" + meter + "\",\"chords\":[\"Am\",\"F\",\"C\",\"G\"],\"range\":{\"low\":\"A3\",\"high\":\"E5\",\"semitones\":19,\"voice\":\"Alt\"}}";
    const string Sec = """{"bpm":120,"duration":60,"beats":[],"sections":[{"start":0,"end":20,"energy":0.4},{"start":20,"end":40,"energy":0.9},{"start":40,"end":60,"energy":0.5}]}""";
    static string Mel(Func<int, double?> f) => "{\"hop\":0.05,\"midi\":[" + string.Join(",", Enumerable.Range(0, 1200).Select(i => f(i) is { } m ? m.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "null")) + "]}";
    static double? Tune(int i) => i % 40 < 30 ? 62 + 5 * Math.Sin(i / 15.0) : null;

    [AvaloniaFact(DisplayName = "Cover-Werkstatt UI: Lied suchen und wählen, Original untersuchen (Bauplan sichtbar), ohne Text freundlicher Hinweis, Cover mit Tempo/Tonart/Takt, rote Ampel → neuer Versuch näher am Original → grün, Klang 1956 als neue Fassung")]
    public async Task Cover_flow()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100; Directory.CreateDirectory(root);
        var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace));
        w.SongFactsFetchOverride = _ => Task.FromResult<string?>(Mb);
        var coverRuns = 0;
        w.SongAnalyseOverride = f => Task.FromResult<string?>(Path.GetFileName(f).Contains("original") ? An(120, "A minor", "4/4", 60) : ++coverRuns == 1 ? An(92, "F# major", "3/4", 40) : An(121, "A minor", "4/4", 61));
        w.SectionsOverride = _ => Task.FromResult<string?>(Sec);
        w.MelodyOverride = f => Task.FromResult<string?>(Path.GetFileName(f).Contains("original") || coverRuns >= 2 ? Mel(Tune) : Mel(i => 60 + (i * 7 % 13)));
        string? soundIn = null; w.EraSoundOverride = (i, o) => { soundIn = i; File.WriteAllBytes(o, "ID3"u8.ToArray()); return Task.FromResult(true); };
        var orig = Path.Combine(root, "original.mp3"); File.WriteAllBytes(orig, "ID3"u8.ToArray());
        try
        {
            w.Show("musik-studio"); Settle();
            // ① Suche
            Assert.Single(await w.MsCoverSearch("Erika Muster – Mein Lied"));
            w.MsCoverPick(0); Assert.Contains("1961", w.CoverMessage);
            // ② Original
            var plan = await w.MsCoverOriginal(orig);
            Assert.True(plan is not null, w.CoverMessage); Assert.Equal(120, plan!.Bpm); Assert.Equal(3, plan.Sections.Count); Assert.Equal("A-Moll", plan.KeyDe);
            w.Show("musik-studio"); Settle();
            Assert.True(w.TryFind<Expander>("cw-box") is not null, "Werkstatt fehlt; Seite: " + w.CurrentView + " / " + w.TryFind<Control>("ms-create-panel"));
            Assert.True(w.TryFind<Expander>("cw-box")!.IsExpanded, "zu"); w.TryFind<Expander>("cw-box")!.BringIntoView(); Settle(); Settle();
            Assert.NotNull(w.TryFind<Border>("cw-plan")); Assert.NotNull(w.TryFind<Border>("cw-melody"));
            // ohne Text: Hinweis statt Auftrag
            Assert.Null(await w.MsCoverMake()); Assert.Contains("Liedtext", w.CoverMessage); Assert.Empty(ace.Bodies);
            // ③–⑤
            w.CoverLyrics = "[Strophe]\nIch singe mein Lied\n[Refrain]\nLa la la"; w.CoverVoice = "rocknroll56"; w.CoverGenre = "Rockabilly"; w.CoverCloseness = 0.5;
            var made = await w.MsCoverMake();
            Assert.NotNull(made);
            Assert.Equal(2, ace.Bodies.Count); // rot → zweiter Versuch
            Assert.Contains("120 BPM", ace.Bodies[0]); Assert.Contains("key of A minor", ace.Bodies[0]); Assert.Contains("1956 style", ace.Bodies[0]); Assert.Contains("cover", ace.Bodies[0]);
            Assert.Contains("Ich singe mein Lied", ace.Bodies[0]);
            Assert.Equal(2, w.CoverResults.Count);
            Assert.Equal("rot", w.CoverResults[0].Report.Light); Assert.Equal("gruen", w.CoverResults[1].Report.Light);
            Assert.True(w.CoverResults[1].Strength > w.CoverResults[0].Strength);
            // M8: Stimme 1956 bringt ihren Klang mit – neue Fassung, Cover bleibt
            Assert.NotNull(soundIn); Assert.Contains("Klang 1956", made!.Title);
            Assert.Contains("grün", w.CoverMessage);
            w.Show("musik-studio"); Settle();
            Assert.Equal(2, Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w).OfType<Border>().Count(b => b.Name == "cw-report"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Cover-Werkstatt UI: Karaoke ohne Original meldet freundlich; mit Original trennt es die Spuren und startet das Mitsingen")]
    public async Task Karaoke()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show("musik-studio"); Settle();
            Assert.False(await w.MsCoverKaraoke()); Assert.Contains("Original", w.CoverMessage);
            w.SongAnalyseOverride = _ => Task.FromResult<string?>(An(100, "C major", "4/4", 30));
            w.SectionsOverride = _ => Task.FromResult<string?>(Sec); w.MelodyOverride = _ => Task.FromResult<string?>(Mel(Tune));
            var split = false; w.StemSplitOverride = (inFile, dir) => { split = true; Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, "gesang.wav"), [1]); File.WriteAllBytes(Path.Combine(dir, "begleitung.wav"), [1]); return Task.FromResult(true); };
            var orig = Path.Combine(root, "original.mp3"); File.WriteAllBytes(orig, "ID3"u8.ToArray());
            await w.MsCoverOriginal(orig);
            w.CoverLyrics = "[Strophe]\nZeile";
            await w.MsCoverKaraoke();
            Assert.True(split);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M6 Eigene Stimme: ohne Zustimmung keine Aufnahme, lernen erst ab 6 Aufnahmen, Cover in der eigenen Stimme (Spuren → umwandeln → mischen), Löschen entfernt alles")]
    public async Task Own_voice()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            w.Show("musik-studio"); Settle();
            w.MicRecordOverride = sec => { var f = Path.Combine(root, Guid.NewGuid().ToString("N") + ".wav"); File.WriteAllBytes(f, [1]); return Task.FromResult<string?>(f); };
            var calls = new List<string[]>();
            w.OwnVoiceRunOverride = a => { calls.Add(a); return Task.FromResult(new Trendo.Core.Cli.Result(0, a[0] == "lernen" ? "{\"ok\": true}" : "{\"ok\": true}", "")); };
            Assert.False(await w.MsOwnVoiceRecord()); Assert.Contains("eigene Stimme", w.OwnMessage);
            w.OwnConsent = true;
            for (var i = 0; i < 5; i++) Assert.True(await w.MsOwnVoiceRecord());
            Assert.False(await w.MsOwnVoiceLearn()); Assert.Contains("mindestens 6", w.OwnMessage); Assert.Empty(calls);
            Assert.True(await w.MsOwnVoiceRecord()); Assert.Equal(6, w.OwnTakeCount);
            Assert.True(await w.MsOwnVoiceLearn()); Assert.True(w.OwnVoiceReady);
            Assert.Equal("lernen", calls[0][0]); Assert.EndsWith("aufnahmen", calls[0][2]);
            // Cover in eigener Stimme
            w.StemSplitOverride = (inFile, dir) => { Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, "gesang.wav"), [1]); File.WriteAllBytes(Path.Combine(dir, "begleitung.wav"), [1]); return Task.FromResult(true); };
            string? mixed = null; w.OwnVoiceMixOverride = (v, b, o) => { mixed = o; File.WriteAllBytes(o, "ID3"u8.ToArray()); return Task.FromResult(true); };
            var f = Path.Combine(root, "cover.mp3"); File.WriteAllBytes(f, "ID3"u8.ToArray());
            var song = await w.MsImport(f);
            var mine = await w.MsOwnVoiceSing(song!);
            Assert.NotNull(mine); Assert.Contains("deine Stimme", mine!.Title); Assert.NotNull(mixed);
            Assert.Equal("singen", calls[^1][0]); Assert.EndsWith("gesang.wav", calls[^1][2]);
            w.Show("musik-studio"); Settle();
            // Löschen
            w.MsOwnVoiceDelete();
            Assert.False(w.OwnVoiceReady); Assert.Equal(0, w.OwnTakeCount); Assert.False(w.OwnConsent);
        }
        finally { w.Close(); }
    }
}
