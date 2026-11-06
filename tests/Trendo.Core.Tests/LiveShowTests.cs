using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Live streamen wie OBS (Profi 27, 05.11.2026): Szenen mit Einblendungen, Tonmischer mit Pegeln, Ton-Wache, Probelauf, Sendeplan.</summary>
public class LiveShowTests
{
    static CaptureChoice Cam(LiveScene? scene, LiveMix? mix, bool mic = true, string? meter = "trendo-live-pegel") => new()
    {
        Os = CaptureOs.Windows, Video = VideoKind.Screen, Fps = 30, Width = 1280, Height = 720, VideoKbps = 2500, AudioKbps = 160,
        Microphone = mic, Mic = "Tischmikrofon", Scene = scene, Mix = mix, MeterDir = meter,
    };

    [Fact(DisplayName = "Szenen: Startbesetzung, neue Szene, Einblendungen (Bauchbinde, Logo, zweite Kamera) mit Grenzen; Speichern und Laden")]
    public void Scenes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-live-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var show = LiveShow.Load(dir);
            Assert.Equal(4, show.Scenes.Count); Assert.Equal("Bildschirm", show.Current!.Name);
            var s = show.Add("Interview", VideoKind.Camera);
            var logo = Path.Combine(Path.GetTempPath(), "trendo-logo-" + Guid.NewGuid().ToString("N")[..6] + ".png"); File.WriteAllBytes(logo, [1]);
            try
            {
                show.AddOverlay(s.Id, "text", "Erika Muster – Gast"); show.AddOverlay(s.Id, "bild", logo); show.AddOverlay(s.Id, "kamera", "USB-Kamera");
                Assert.Throws<FileNotFoundException>(() => show.AddOverlay(s.Id, "bild", logo + ".fehlt"));
                Assert.Throws<ArgumentException>(() => show.AddOverlay(s.Id, "video", "x"));
                Assert.Throws<ArgumentException>(() => show.Add(" ", VideoKind.Screen));
                show.Active = s.Id; show.Plan = [new LiveCue(TimeSpan.FromMinutes(2), s.Id, "Gast vorstellen")]; show.Mix = new LiveMix(MicDb: 3, MusicDb: -18);
                show.Save(dir);
                var back = LiveShow.Load(dir);
                Assert.Equal("Interview", back.Current!.Name); Assert.Equal(3, back.Current.Overlays.Count); Assert.Equal(3, back.Mix.MicDb); Assert.Single(back.Plan);
            }
            finally { File.Delete(logo); }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "FFmpeg-Graph: Einblendungen über dem Hauptbild, Mikrofon mit Lautstärke und Pegel-Datei, Musik in Schleife gemischt; Vorschau ohne Mikrofon; ohne Szene bleibt alles wie bisher")]
    public void Graph()
    {
        var logo = Path.Combine(Path.GetTempPath(), "trendo-logo-" + Guid.NewGuid().ToString("N")[..6] + ".png"); File.WriteAllBytes(logo, [1]);
        var music = Path.Combine(Path.GetTempPath(), "trendo-musik-" + Guid.NewGuid().ToString("N")[..6] + ".mp3"); File.WriteAllBytes(music, [1]);
        try
        {
            var sc = new LiveScene("s", "Interview", VideoKind.Screen, [new("o1", "bild", logo, 0.78, 0.04, 0.18), new("o2", "kamera", "USB-Kamera", 0.68, 0.64, 0.28), new("o3", "text", "Hallo", 0.05, 0.8, 0.9, Visible: false)]);
            var a = LiveFfmpeg.Args(LiveOutput.Record, Cam(sc, new LiveMix(MicDb: -6, MusicFile: music, MusicDb: -18)), "v.jpg", "a.mp4", null);
            var j = string.Join(" ", a);
            var fc = a[a.IndexOf("-filter_complex") + 1];
            Assert.Contains("audio=Tischmikrofon", j); Assert.Contains("video=USB-Kamera", j); Assert.Contains("-stream_loop -1 -i " + music, j);
            Assert.Contains("overlay=x=998:y=29", fc); Assert.Contains("[3:v]scale=358", fc); // Kamera ist Eingang 3 (0 Bild, 1 Mikro, 2 Logo)
            Assert.DoesNotContain("drawtext", fc); // ausgeblendete Bauchbinde
            Assert.Contains("[1:a]aresample=44100,volume=0.501", fc); Assert.Contains("file=trendo-live-pegel/mikrofon.txt", fc); Assert.Contains("[4:a]", fc);
            Assert.Contains("amix=inputs=2", fc); Assert.Contains("[aout]", a);
            var prev = LiveFfmpeg.Args(LiveOutput.Preview, Cam(sc, new LiveMix(MusicFile: music)), "v.jpg", null, null);
            Assert.DoesNotContain("audio=Tischmikrofon", string.Join(" ", prev)); Assert.DoesNotContain(prev, x => x.Contains("stream_loop"));
            var old = LiveFfmpeg.Args(LiveOutput.Record, Cam(null, null, meter: null), "v.jpg", "a.mp4", null);
            Assert.Contains("[0:v]scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2,split=2[vout][p];[p]fps=2,scale=640:-2[prev]", old);
            var muted = LiveFfmpeg.Args(LiveOutput.Record, Cam(sc, new LiveMix(MicMute: true)), "v.jpg", "a.mp4", null);
            Assert.Contains("volume=0,", muted[muted.IndexOf("-filter_complex") + 1]);
        }
        finally { File.Delete(logo); File.Delete(music); }
    }

    [Fact(DisplayName = "Echt mit FFmpeg: Szene mit Logo und Bauchbinde, Mikrofon und Musik gemischt – Datei entsteht, Pegel-Datei hat Messwerte, Logo ist im Bild")]
    public void Real_ffmpeg()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null || Media.VideoEdit.FontFile() is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-livereal-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var logo = Path.Combine(dir, "logo.png"); Cli.Run(ff, ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=white:s=64x64", "-frames:v", "1", logo], dir, 60000);
            var music = Path.Combine(dir, "musik.wav"); Cli.Run(ff, ["-y", "-loglevel", "error", "-f", "lavfi", "-i", "sine=f=330:d=2", music], dir, 60000);
            var sc = new LiveScene("s", "Test", VideoKind.Screen, [new("o1", "bild", logo, 0.8, 0.05, 0.1), new("o2", "text", "Erika: Hallo 100%", 0.05, 0.8, 0.9)]);
            var c = Cam(sc, new LiveMix(MicDb: -3, MusicFile: music, MusicDb: -12), meter: "pegel") with { Width = 320, Height = 180, Fps = 10 };
            var args = LiveFfmpeg.Args(LiveOutput.Record, c, Path.Combine(dir, "v.jpg"), Path.Combine(dir, "a.mp4"), null);
            // Bildschirm und Mikrofon durch Testquellen ersetzen (gleiche Eingangs-Nummern)
            var i0 = args.IndexOf("gdigrab"); args.RemoveRange(i0 - 3, 8); args.InsertRange(i0 - 3, ["-f", "lavfi", "-i", "color=c=blue:s=640x360:r=10"]);
            var i1 = args.IndexOf("audio=Tischmikrofon"); args.RemoveRange(i1 - 5, 6); args.InsertRange(i1 - 5, ["-f", "lavfi", "-i", "sine=f=440:sample_rate=44100"]);
            args.InsertRange(0, ["-t", "3"]); // Dauer begrenzen
            var outIdx = args.LastIndexOf(Path.Combine(dir, "a.mp4")); args.InsertRange(outIdx, ["-t", "3"]);
            var prevIdx = args.IndexOf(Path.Combine(dir, "v.jpg")); args.InsertRange(prevIdx, ["-t", "3"]);
            Directory.CreateDirectory(Path.Combine(dir, "pegel"));
            var r = Cli.Run(ff, args, dir, 120000);
            Assert.True(File.Exists(Path.Combine(dir, "a.mp4")) && new FileInfo(Path.Combine(dir, "a.mp4")).Length > 1000, r.Err[^Math.Min(r.Err.Length, 1500)..]);
            var meter = LiveMeter.Read(File.ReadAllText(Path.Combine(dir, "pegel", "mikrofon.txt")));
            Assert.True(meter.Count > 5, "keine Pegel"); Assert.InRange(meter[^1].Lufs, -40, 0);
            var raw = Path.Combine(dir, "f.rgb");
            Cli.Run(ff, ["-y", "-loglevel", "error", "-ss", "1", "-i", Path.Combine(dir, "a.mp4"), "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", raw], dir, 60000);
            var px = File.ReadAllBytes(raw); var at = ((int)(180 * 0.05 + 8) * 320 + (int)(320 * 0.8 + 8)) * 3;
            Assert.True(px[at] > 200 && px[at + 1] > 200 && px[at + 2] > 200, "Logo fehlt");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Ton-Wache: über 5 s still bei offenem Mikrofon → Warnung, stumm geschaltet → keine; zu laut → Warnung; Pegel lesen")]
    public void Meter()
    {
        var text = string.Concat(Enumerable.Range(0, 70).Select(i => $"frame:{i} pts:{i} pts_time:{i * 0.1:0.0}\nlavfi.r128.M={(i < 10 ? "-20.5" : "-inf")}\n".Replace(',', '.')));
        var v = LiveMeter.Read(text);
        Assert.Equal(70, v.Count); Assert.Equal(-20.5, v[0].Lufs); Assert.Equal(-120, v[^1].Lufs);
        Assert.Contains("kein Ton", LiveMeter.Warn(v, "Mikrofon", false)); Assert.Null(LiveMeter.Warn(v, "Mikrofon", true));
        var loud = Enumerable.Range(0, 20).Select(i => (i * 0.1, -3.0)).ToList();
        Assert.Contains("zu laut", LiveMeter.Warn(loud, "Mikrofon", false));
        Assert.Null(LiveMeter.Warn([.. Enumerable.Range(0, 80).Select(i => (i * 0.1, -18.0))], "Mikrofon", false));
        Assert.Equal(0.7, LiveMeter.Bar(-18), 2);
    }

    [Fact(DisplayName = "Probelauf: schwarzes Bild rot, kein Ton rot, übersteuert gelb, alles gut grün; Datenrate mit Upload-Hinweis")]
    public void Probe()
    {
        var ok = LiveProbe.Evaluate("mean_volume: -22.0 dB\nmax_volume: -4.1 dB", 8, 2_700_000, 2660, true);
        Assert.All(ok, c => Assert.Equal("gruen", c.Light)); Assert.Contains(ok, c => c.Text.Contains("Upload"));
        var black = LiveProbe.Evaluate("black_start:0 black_end:8 black_duration:8\nmean_volume: -22.0 dB\nmax_volume: -4 dB", 8, 900_000, 2660, true);
        Assert.Equal("rot", black[0].Light);
        Assert.Contains(LiveProbe.Evaluate("mean_volume: -91.0 dB\nmax_volume: -80 dB", 8, 900_000, 2660, true), c => c.Light == "rot" && c.Text.Contains("Kein Ton"));
        Assert.Contains(LiveProbe.Evaluate("mean_volume: -12.0 dB\nmax_volume: -0.1 dB", 8, 900_000, 2660, true), c => c.Light == "gelb" && c.Text.Contains("übersteuert"));
        Assert.Equal("rot", LiveProbe.Evaluate("", 8, 10, 2660, true).Single().Light);
        Assert.Contains("ffmpeg", "ffmpeg " + string.Join(" ", LiveProbe.AnalyzeArgs("p.mp4"))); Assert.Contains("volumedetect", LiveProbe.AnalyzeArgs("p.mp4"));
    }

    [Fact(DisplayName = "Sendeplan: „mm:ss Szene – Notiz“ (Zeilen oder „ / “), sortiert; unbekannte Szene und falsche Form abgelehnt; laufender und nächster Punkt")]
    public void Plan()
    {
        var sc = LiveShow.Default().Scenes;
        var (p, e) = LiveProbe.ParsePlan("02:00 Kamera – Thema vorstellen / 00:00 Gleich geht's los – Begrüßung\n10:30 Bildschirm – Demo", sc);
        Assert.Null(e); Assert.Equal(3, p!.Count); Assert.Equal("start", p[0].Scene); Assert.Equal("Thema vorstellen", p[1].Note); Assert.Equal(TimeSpan.FromSeconds(630), p[2].At);
        Assert.Contains("Talkshow", LiveProbe.ParsePlan("00:00 Talkshow", sc).Error); Assert.NotNull(LiveProbe.ParsePlan("gleich Kamera", sc).Error);
        var (now, next) = LiveProbe.At(p, TimeSpan.FromMinutes(3));
        Assert.Equal("kamera", now!.Scene); Assert.Equal("bildschirm", next!.Scene);
        Assert.Contains("00:00 Gleich geht's los – Begrüßung / 02:00 Kamera", LiveProbe.PlanText(p, sc));
    }
}
