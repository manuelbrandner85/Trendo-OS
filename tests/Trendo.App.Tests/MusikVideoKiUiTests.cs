using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>KI-Musikvideo C/G/D/F: Figurenbild, Szenen und singende Figur rendern, prüfen, Fehler automatisch neu erzeugen, rot markieren,
/// Vorschau zuerst, im Videoschnitt auf den Takt setzen. Alle KI- und FFmpeg-Aufrufe sind ersetzt – Tests starten nie echte Programme.</summary>
[Collection("Claude")]
public class MusikVideoKiUiTests
{
    static async Task Pump() { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    static string OutOf(IReadOnlyList<string> a) { var i = a.ToList().IndexOf("-o"); return i >= 0 ? a[i + 1] : a[^1]; }

    [AvaloniaFact(DisplayName = "Musikvideo: Vorschau → Prüfung findet „3 Arme“ → automatisch neu (strengere Beschreibung) → grün; dauerhaft fehlerhaft = rot; Zusammensetzen im Videoschnitt")]
    public async Task Render_check_assemble()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var f = Path.Combine(root, "duett.mp3"); File.WriteAllBytes(f, "ID3"u8.ToArray());
            var s = new SongLibrary(root).Add(new MusicSong { Title = "Duett", Style = "pop", Lyrics = "[Strophe – Sie]\nIch warte am Meer\n[Refrain – beide]\nWir zwei", Seconds = 20, File = f });
            w.StemSplitOverride = (src, dir) => { Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, "begleitung.wav"), [1]); File.WriteAllBytes(Path.Combine(dir, "gesang.wav"), [1]); return Task.FromResult(true); };
            w.SectionsOverride = _ => Task.FromResult<string?>("""{"bpm":120,"duration":20,"sections":[{"start":0,"end":10,"energy":0.8},{"start":10,"end":20,"energy":1.3}]}""");
            w.SingersOverride = _ => Task.FromResult<string?>("""{"singers":[{"id":0,"gender":"w","voice":"Alt"},{"id":1,"gender":"m","voice":"Tenor"}],"phrases":[{"start":1,"end":5,"singer":0},{"start":11,"end":15,"singer":1,"both":true}]}""");
            w.WordsOverride = _ => Task.FromResult<string?>(null);
            w.ThemeOverride = _ => Task.FromResult<string?>(null);
            var calls = new List<(string Exe, List<string> Args)>();
            w.VideoKiOverride = (exe, args) => { lock (calls) calls.Add((exe, args.ToList())); var o = OutOf(args); Directory.CreateDirectory(Path.GetDirectoryName(o)!); File.WriteAllText(o, "x"); return Task.FromResult(true); };
            var checks = new Dictionary<string, int>();
            w.VideoCheckOverride = (clip, attempt) =>
            {
                var name = Path.GetFileName(clip);
                if (name == "einstellung000.mp4" && attempt == 1) return Task.FromResult("""{"ok":false,"issues":[{"code":"haende","text":"Zu viele Hände oder Arme (3 bei 1 Person(en))","at":1.2}]}""");
                if (name == "einstellung002.mp4") return Task.FromResult("""{"ok":false,"issues":[{"code":"gesichter","text":"Zusätzliches Gesicht","at":0.5}]}""");
                return Task.FromResult("""{"ok":true,"issues":[]}""");
            };
            var vision = new Dictionary<string, int>();
            w.VisionCheckOverride = clip => { var n = Path.GetFileName(clip); lock (vision) vision[n] = vision.GetValueOrDefault(n) + 1; return Task.FromResult<string?>(n == "einstellung001.mp4" && vision[n] == 1 ? "ERROR: sechs Finger an der Hand" : "OK"); };
            w.MusicStudioSelect(s.Id); w.MsDetailTab("video"); w.Show("musik-studio"); await Pump();
            await w.MsScriptPlan(s.Id);
            var sc = w.Script(s)!;
            await Pump();
            Assert.NotNull(w.TryFind<Button>("ms-vk-preview"));
            Assert.False(w.Find<Button>("ms-vk-final").IsEnabled); // F1: erst die Vorschau
            var ok = await w.MsRenderVideo(s.Id, preview: true); await Pump();
            var clips = w.ShotClips(s, "vorschau");
            Assert.Equal(sc.Shots.Count, clips.Count);
            Assert.Equal(sc.Shots.Count - 1, ok);
            Assert.Contains("✓ in Ordnung nach 2 Versuchen", clips.Single(c => c.Index == 0).Report);
            Assert.Contains("Zu viele Hände", clips.Single(c => c.Index == 0).Report);
            Assert.Contains("sechs Finger", clips.Single(c => c.Index == 1).Report); // G2: zweiter Blick der Bild-KI, danach behoben
            Assert.True(clips.Single(c => c.Index == 1).Ok);
            var red = clips.Single(c => c.Index == 2);
            Assert.False(red.Ok); Assert.Contains("nach 3 Versuchen noch fehlerhaft", red.Report);
            // Versuch 2 von Einstellung 0: strengere Beschreibung, anderer Zufall, ruhige Kamera
            var sdRuns = calls.Where(c => c.Exe == "sd-cli" && c.Args.Contains("vid_gen")).ToList();
            var shot0 = sdRuns.Where(c => OutOf(c.Args).Contains("roh000")).ToList();
            Assert.Equal(2, shot0.Count);
            Assert.Contains("two arms and two hands per person", shot0[1].Args[shot0[1].Args.IndexOf("-p") + 1]);
            Assert.NotEqual(shot0[0].Args[shot0[0].Args.IndexOf("-s") + 1], shot0[1].Args[shot0[1].Args.IndexOf("-s") + 1]);
            Assert.All(sdRuns, c => Assert.Contains("extra arms", c.Args[c.Args.IndexOf("-n") + 1])); // G7 immer dabei
            // singende Figur: Gesangsausschnitt + Figurenbild + S2V (nur für eine Person; „beide“ als Szene)
            Assert.Contains(sdRuns, c => c.Args.Contains("--audio") && c.Args.Contains("-i"));
            Assert.Contains(calls, c => c.Exe == "sd-cli" && !c.Args.Contains("vid_gen") && OutOf(c.Args).EndsWith(".figur-fig0.png"));
            Assert.Equal(16, int.Parse(sdRuns.First(c => c.Args.Contains("--audio")).Args[sdRuns.First(c => c.Args.Contains("--audio")).Args.IndexOf("--fps") + 1]));
            await Pump();
            Assert.True(w.Find<Button>("ms-vk-final").IsEnabled);
            Assert.NotNull(w.TryFind<StackPanel>("ms-vk-report"));
            // Zusammensetzen: jede Einstellung auf ihrem Takt, die rote wird von der vorigen überbrückt
            var placed = w.MsAssembleVideo(s.Id, preview: true); await Pump();
            Assert.Equal(sc.Shots.Count - 1, placed);
            var d = w.ProgramActive("schnitt")!;
            Assert.Equal(sc.Shots.Count - 1, VideoEdit.Clips(d.Data).Count);
            Assert.Equal(20, VideoEdit.VideoLength(VideoEdit.Clips(d.Data)), 1);
            Assert.Equal(s.Id, VideoEdit.Song(d.Data)!.Value.Id);
            Assert.Contains("1 fehlen oder sind rot", w.ProgramMessage("schnitt"));
            // Nur eine Einstellung neu (nach dem Neu-Würfeln): die übrigen bleiben
            calls.Clear();
            await w.MsRenderVideo(s.Id, preview: true, only: 2);
            Assert.All(calls.Where(c => c.Args.Contains("vid_gen")), c => Assert.Contains("roh002", OutOf(c.Args)));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Musikvideo ohne Video-KI: ehrlicher Hinweis und Weg in die KI-Bibliothek; Abbrechen behält Fertiges")]
    public async Task Not_ready()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var f = Path.Combine(root, "a.mp3"); File.WriteAllBytes(f, "ID3"u8.ToArray());
            var s = new SongLibrary(root).Add(new MusicSong { Title = "A", Style = "pop", Lyrics = "[Instrumental]", Seconds = 10, File = f });
            w.SectionsOverride = _ => Task.FromResult<string?>("""{"bpm":120,"duration":10,"sections":[{"start":0,"end":10,"energy":1}]}""");
            w.MusicStudioSelect(s.Id); w.MsDetailTab("video"); w.Show("musik-studio"); await Pump();
            await w.MsScriptPlan(s.Id); await Pump();
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Wan 2.2 – Filmszenen"));
            Assert.Equal(0, await w.MsRenderVideo(s.Id, true));
            Assert.Contains("KI-Bibliothek", w.Find<TextBlock>("ms-msg").Text);
        }
        finally { w.Close(); }
    }
}
