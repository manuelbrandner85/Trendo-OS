using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Trendo Musik – nachgebaute Vorbild-Funktionen in der Oberfläche (04.11.2026).</summary>
public class MusikProWerkzeuge_Ui
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
                case "/release_task": var body = await r.Content!.ReadAsStringAsync(ct); lock (Bodies) Bodies.Add(body); return J("{\"data\":{\"task_id\":\"t1\"},\"code\":200}");
                case "/query_result": return J("{\"data\":[{\"task_id\":\"t1\",\"status\":1,\"result\":\"[{\\\"file\\\":\\\"/v1/audio?path=a.mp3\\\"}]\"}],\"code\":200}");
                case "/v1/audio": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("ID3"u8.ToArray()) };
                default: return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }

    static string Wav(string dir, string name, double hz, double sec)
    {
        const int rate = 22050; var a = AudioBuf.Silence(sec, rate, 2);
        for (var i = 0; i < a.Length; i++) { var v = 0.3f * (float)Math.Sin(2 * Math.PI * hz * i / rate); a.Data[0][i] = v; a.Data[1][i] = v; }
        var f = Path.Combine(dir, name); AudioFile.WriteWav(a, f); return f;
    }

    [AvaloniaFact(DisplayName = "Profi-Reiter: Mastern mit Stil/Ziel/Referenz, Übe-Fassung, MIDI, Loop, Kurzclip – jeweils neue Fassung, Original bleibt; FFmpeg ersetzt")]
    public async Task Pro_tab()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100; Directory.CreateDirectory(root);
        try
        {
            var calls = new List<string[]>();
            w.MasterRunOverride = a => { calls.Add(a); File.Copy(a.First(x => x.EndsWith(".wav") || x.EndsWith(".mp3")), a[^1], true); return Task.FromResult(new Cli.Result(0, "", "")); };
            var src = Wav(root, "lied.wav", 220, 12); var reference = Wav(root, "ref.wav", 6000, 6);
            var song = await w.MsImport(src); var id = song!.Id;
            w.Songs.Update(id, s => s.Analysis = new SongAnalysis(12, 120, "B major", 0.8, "4/4", ["B", "E"], null));
            w.MusicStudioSelect(id); w.MsDetailTab("profi"); w.Show("musik-studio"); Settle();
            Assert.NotNull(w.TryFind<StackPanel>("mp-panel"));
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Kapo im 2. Bund, Griffe wie A")); // H-Dur → Kapo 2, Griffe wie A
            var before = File.ReadAllBytes(w.Songs.Get(id)!.File);
            var m = await w.MsMasterPro(id, "warm", "apple", reference);
            Assert.NotNull(m); Assert.Contains("Master Warm, -16 LUFS nach Referenz", m!.Title);
            Assert.Contains(calls[^1], x => x.Contains("loudnorm=I=-16") && x.Contains("equalizer=f=6000"));
            var p = await w.MsPractice(id, 0.75, -2, true);
            Assert.Contains("Üben 75 %, -2 Halbtöne, mit Klick", p!.Title); Assert.Contains(calls[^1], x => x.Contains("rubberband=tempo=0.75"));
            // Paket 01 S4-12: extreme Werte werden abgelehnt (nicht still begrenzt), es wird nichts gerechnet
            var n = calls.Count;
            Assert.Null(await w.MsPractice(id, 3.0, 0, false)); Assert.Equal(n, calls.Count);
            w.MelodyOverride = _ => Task.FromResult<string?>("{\"hop\":0.05,\"midi\":[60,60,60,62,62,62,null,64,64]}");
            var midi = await w.MsMidi(id);
            Assert.True(File.Exists(midi)); Assert.Equal("MThd", Encoding.ASCII.GetString(File.ReadAllBytes(midi!), 0, 4));
            var loop = await w.MsLoop(id, 4);
            Assert.Equal(8.0, AudioFile.Read(loop!.File).Seconds, 1);
            w.SectionsOverride = _ => Task.FromResult<string?>("{\"bpm\":120,\"duration\":12,\"beats\":[],\"sections\":[{\"start\":0,\"end\":4,\"energy\":0.2},{\"start\":4,\"end\":12,\"energy\":0.9}]}");
            var clip = await w.MsHookClip(id, 5);
            Assert.EndsWith(".mp4", clip); Assert.Contains(calls[^1], x => x == "4");
            Assert.Equal(before, File.ReadAllBytes(w.Songs.Get(id)!.File)); // Original unverändert
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Erstellen: Ablaufplan baut das Textgerüst (Texte bleiben), Klang-Referenz geht als Datei an ACE-Step")]
    public async Task Plan_and_reference()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var ace = new FakeAce(); w.AceClientOverride = new AceStepClient(new HttpClient(ace));
            w.Show("musik-studio"); Settle(); w.MusicStudioProMode(true);
            var t = w.MsApplyPlan("Intro; 4; ruhig; instrumental\nStrophe; 8; erzählend\nRefrain; 8; kraftvoll");
            Assert.Contains("[Intro - ruhig]\n[Instrumental]", t); Assert.Contains("[Refrain - kraftvoll]", t);
            Assert.Equal("Zeile eins", MusicPro.SectionTexts("[Strophe]\nZeile eins\n[Refrain]\nLa")["Strophe 1"]);
            var reference = Wav(root, "klang.wav", 330, 3); w.MusicReference = reference;
            await w.MusicStudioCreate(new AceStepClient.Job("Pop", "[Strophe]\nHallo", 30, "de", 1, ReferenceAudio: reference), "Test");
            Assert.Contains(ace.Bodies, b => b.Contains("reference_audio") && b.Contains("klang.wav"));
        }
        finally { w.Close(); }
    }
}
