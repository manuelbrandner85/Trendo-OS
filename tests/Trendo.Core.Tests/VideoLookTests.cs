using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Media;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0695: Vorschau = Export – das Vorschaubild am Abspielkopf entsteht aus demselben Filtergraphen wie der Export, nur auf den Ausschnitt begrenzt.</summary>
public class VideoLookTests
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Fact(DisplayName = "Vorschau = Export: ohne Ausschnitt bleibt der Export-Aufruf wie bisher; der Ausschnitt nimmt nur die sichtbaren Stücke und rechnet Zeit und Überblendung um")]
    public void Window_args()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-look-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            foreach (var n in new[] { "a.mp4", "b.mp4", "c.png", "logo.png" }) File.WriteAllText(Path.Combine(dir, n), "x");
            var p = VideoEdit.NewProject(1920, 1080);
            VideoEdit.Add(p, new EditClip("a", Path.Combine(dir, "a.mp4"), 1, 5, 10, Fade: 1, Title: "Hallo"));
            VideoEdit.Add(p, new EditClip("b", Path.Combine(dir, "b.mp4"), 0, 4, 10));
            VideoEdit.Add(p, new EditClip("c", Path.Combine(dir, "c.png"), 0, 3, 3600, false));
            VideoAnim.SetKey(p, "b", "scale", 0, 1); VideoAnim.SetKey(p, "b", "scale", 4, 0.5);
            var logo = VideoPro.AddOverlay(p, Path.Combine(dir, "logo.png"), 5, 4);
            var export = string.Join(" ", VideoEdit.ExportArgs(p, Path.Combine(dir, "f.mp4")));
            Assert.Contains("-ss 1 -t 4 -i", export); Assert.Contains("xfade=transition=fade:duration=1:offset=3[", export); Assert.DoesNotContain("setpts=PTS+", export);

            // Stelle 1,0 s: nur Stück a, beginnt eine Sekunde später, Zeit bleibt die des Stücks
            var s1 = string.Join(" ", VideoLook.FrameArgs(p, 1.0, Path.Combine(dir, "v.png")));
            Assert.Contains("-ss 2 -t 3 -i", s1); Assert.Contains("[0:v]setpts=PTS+1/TB,scale=1920:1080", s1); Assert.DoesNotContain("b.mp4", s1); Assert.DoesNotContain("logo.png", s1);
            Assert.Contains("drawtext=", s1); Assert.Contains("-frames:v 1", s1);
            // Stelle 3,5 s: in der Überblendung a→b – a beginnt mit der Blende, Versatz 0
            var s2 = string.Join(" ", VideoLook.FrameArgs(p, 3.5, Path.Combine(dir, "v.png")));
            Assert.Contains("-ss 4 -t 1 -i", s2); Assert.Contains("b.mp4", s2); Assert.Contains("xfade=transition=fade:duration=1:offset=0[", s2); Assert.Contains(@"fps=30,select='eq(n\,0)+gte(t\,0.491667)'", s2); // Standbild: Bilder vor der Stelle früh verwerfen
            // Stelle 6,0 s: Stück b (Start 3 s) mit Keyframes, Hintergrund und Einblendung beginnen an der Stelle
            var s3 = string.Join(" ", VideoLook.FrameArgs(p, 6.0, Path.Combine(dir, "v.png")));
            Assert.DoesNotContain("a.mp4", s3); Assert.Contains("-ss 3 -t 1 -i", s3); Assert.Contains(":d=1,setpts=PTS+3/TB", s3);
            Assert.Contains("[vbase]setpts=PTS+3/TB[vtl]", s3); Assert.Contains("logo.png", s3); Assert.Contains($"-t 3 -i {Path.Combine(dir, "logo.png")}", s3);
            Assert.Contains("format=yuv420p,trim=start=5.991667", s3);
            // Wiedergabe ab 2 s bis zum Ende: alle späteren Stücke, Ausgabe als Rohbilder
            var st = VideoLook.StreamArgs(p, 2, 640, 360);
            Assert.Contains("-nostdin", st); Assert.Equal("pipe:1", st[^1]); Assert.Contains("c.png", string.Join(" ", st)); Assert.Contains("format=bgra", string.Join(" ", st));
            // Proxy für die Vorschau: Datei ersetzt, Export unberührt
            Assert.Contains("a.proxy.mp4", string.Join(" ", VideoLook.FrameArgs(p, 1, "v.png", 960, source: f => f.Replace("a.mp4", "a.proxy.mp4"))));
            Assert.Contains("scale=960:-2", string.Join(" ", VideoLook.FrameArgs(p, 1, "v.png", 960)));
            Assert.Equal(logo.Id, VideoPro.Overlays(p)[0].Id);
        }
        finally { Directory.Delete(dir, true); }
    }

    static byte[] Rgb(string ff, string png, string dir, int w, int h)
    {
        var raw = Path.Combine(dir, Guid.NewGuid().ToString("N")[..8] + ".rgb");
        var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-i", png, "-vf", $"scale={w}:{h}", "-f", "rawvideo", "-pix_fmt", "rgb24", raw], dir, 60000);
        Assert.True(r.Ok, r.Err);
        var b = File.ReadAllBytes(raw); File.Delete(raw); return b;
    }
    static double Mad(byte[] a, byte[] b) { Assert.Equal(a.Length, b.Length); long s = 0; for (var i = 0; i < a.Length; i++) s += Math.Abs(a[i] - b[i]); return s / (double)a.Length; }

    [Fact(DisplayName = "Vorschau = Export mit echtem FFmpeg (1080p): Farbe, Titel, Überblendung, Keyframes, Effekt und Logo – Vorschaubild an 5 Stellen gleich dem Bild im MP4")]
    public async Task Preview_equals_export_real()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) { Ueberspringen.Weil("FFmpeg fehlt auf diesem PC."); return; }
        var dir = Path.Combine(Path.GetTempPath(), "trendo-look-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var log = new StringBuilder(); void Log(string s) => log.AppendLine(s);
        try
        {
            string P(string n) => Path.Combine(dir, n);
            Log("FFmpeg: " + Cli.Run(ff, ["-version"], dir, 30000).Out.Split('\n')[0].Trim());
            // Echte 1080p-Quellen mit Bewegung (jedes Einzelbild anders) – ein falsches Bild fällt sofort auf
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=1920x1080:rate=30:duration=6", "-f", "lavfi", "-i", "sine=frequency=440:duration=6", "-shortest", "-c:v", "libx264", "-g", "60", "-pix_fmt", "yuv420p", "-c:a", "aac", P("a.mp4")], dir, 120000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "mandelbrot=size=1920x1080:rate=30", "-t", "6", "-c:v", "libx264", "-g", "60", "-pix_fmt", "yuv420p", P("b.mp4")], dir, 120000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "smptehdbars=size=1920x1080", "-frames:v", "1", P("c.png")], dir, 60000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=gold:size=400x200,drawbox=x=40:y=40:w=320:h=120:color=black@0.8:t=fill", "-frames:v", "1", P("logo.png")], dir, 60000).Ok);

            var p = VideoEdit.NewProject(1920, 1080);
            VideoEdit.Add(p, new EditClip("a", P("a.mp4"), 0.5, 4.5, 6, true, Fade: 1, Title: "Sommer 2026"));
            VideoEdit.Add(p, new EditClip("b", P("b.mp4"), 1, 5, 6, false));
            VideoEdit.Add(p, new EditClip("c", P("c.png"), 0, 3, 3600, false));
            VideoPro.SetFx(p, "a", VideoPro.Looks.First(l => l.Id == "sw").Fx with { Transition = "slideleft" }); // Schwarzweiß-Look + Übergangsart
            VideoAnim.SetKey(p, "b", "scale", 0, 1); VideoAnim.SetKey(p, "b", "scale", 4, 0.5); // Keyframes: Größe
            VideoAnim.SetKey(p, "b", "x", 0, 0); VideoAnim.SetKey(p, "b", "x", 4, 0.2); // Keyframes: Lage
            VideoAnim.AddEffect(p, "b", "vignette");
            var logo = VideoPro.AddOverlay(p, P("logo.png"), 5, 4);
            VideoAnim.SetKey(p, logo.Id, "x", 0, 0.05); VideoAnim.SetKey(p, logo.Id, "x", 4, 0.6); // bewegte Einblendung
            File.WriteAllText(P("projekt.json"), p.ToJsonString(new() { WriteIndented = true }));
            Log("Projekt: 1920×1080, 30 Bilder/s – a (Testbild, Look „Schwarzweiß“, Titel, Schieben 1 s) → b (Fraktal, Keyframes Größe+Lage, Vignette) | harter Schnitt → c (Standbild); Logo 5–9 s mit bewegter Lage");
            Log("Länge Zeitleiste: " + VideoEdit.VideoLength(VideoEdit.Clips(p)).ToString("0.###", Inv) + " s");

            var sw = Stopwatch.StartNew();
            var film = VideoEdit.Export(ff, p, P("film.mp4"));
            Log($"Export: {sw.ElapsedMilliseconds} ms → film.mp4, SHA-256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(film))).ToLowerInvariant()} ({new FileInfo(film).Length} Bytes)");

            // Stellen: Look+Titel · mitten in der Überblendung · Keyframes+Vignette+Logo · Standbild nach hartem Schnitt mit Logo · kurz vor Ende
            var times = new[] { 1.0, 3.5, 5.2, 6.6, 8.0 };
            const double limit = 2.5; var fails = new List<string>();
            var cache = P("cache");
            var beleg = Environment.GetEnvironmentVariable("TRENDO_T0695_BELEG");
            foreach (var t in times)
            {
                var tq = VideoLook.Snap(p, t);
                sw.Restart();
                var prev = await VideoLook.RenderFrameAsync(ff, p, t, cache);
                var ms = sw.ElapsedMilliseconds;
                Assert.True(prev is not null, "Vorschau ohne Bild: " + VideoLook.LastError);
                sw.Restart(); var again = await VideoLook.RenderFrameAsync(ff, p, t, cache); var hit = sw.ElapsedMilliseconds; Assert.Equal(prev, again);
                var exp = P($"export-{t.ToString("0.0", Inv)}.png");
                Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", tq.ToString("0.######", Inv), "-i", film, "-frames:v", "1", exp], dir, 60000).Ok);
                var a = Rgb(ff, prev!, dir, 1920, 1080); var b = Rgb(ff, exp, dir, 1920, 1080);
                var mad = Mad(a, b);
                // Gegenproben: Nachbarbild im Export (ein Bild daneben) und das rohe Quellbild ohne Effekte
                var next = P($"export-next-{t.ToString("0.0", Inv)}.png");
                Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", (tq + 1.0 / 30).ToString("0.######", Inv), "-i", film, "-frames:v", "1", next], dir, 60000).Ok);
                var madNext = Mad(Rgb(ff, next, dir, 1920, 1080), b);
                if (Environment.GetEnvironmentVariable("T0695_DEBUG") is { Length: > 0 })
                {
                    for (var k = -3; k <= 3; k++) { var x = P($"dbg{k}.png"); Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", (tq + k / 30.0).ToString("0.######", Inv), "-i", film, "-frames:v", "1", x], dir, 60000); Log($"  dbg k={k}: {Mad(Rgb(ff, x, dir, 1920, 1080), a):0.000}"); }
                    Log("  args " + string.Join(" ", VideoLook.FrameArgs(p, t, "x.png")));
                }
                var at = VideoEdit.At(p, tq)!.Value; var clip = VideoEdit.Clips(p)[at.Index];
                var raw = P($"roh-{t.ToString("0.0", Inv)}.png");
                Assert.True(Cli.Run(ff, clip.IsImage ? ["-hide_banner", "-loglevel", "error", "-y", "-i", clip.File, "-frames:v", "1", raw] : ["-hide_banner", "-loglevel", "error", "-y", "-ss", at.SourceTime.ToString("0.######", Inv), "-i", clip.File, "-frames:v", "1", raw], dir, 60000).Ok);
                var madRaw = Mad(Rgb(ff, raw, dir, 1920, 1080), b);
                Log($"t={tq.ToString("0.000", Inv)} s: Vorschau {ms} ms (Zwischenspeicher {hit} ms) · mittlere Abweichung Vorschau↔Export {mad.ToString("0.000", Inv)} · Gegenprobe Nachbarbild {madNext.ToString("0.000", Inv)} · Gegenprobe rohes Stück (alte Vorschau) {madRaw.ToString("0.000", Inv)} · SHA-256 Vorschau {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(prev!))).ToLowerInvariant()[..16]}…");
                if (beleg is { Length: > 0 })
                {
                    Directory.CreateDirectory(beleg);
                    Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-i", prev!, "-i", exp, "-i", raw, "-filter_complex", "[0]scale=640:360[a];[1]scale=640:360[b];[2]scale=640:360[c];[c][a][b]hstack=inputs=3", "-frames:v", "1", Path.Combine(beleg, $"vergleich-{t.ToString("0.0", Inv)}s.png")], dir, 60000);
                }
                if (mad >= limit) fails.Add($"t={tq}: Vorschau weicht im Mittel {mad:0.000} vom Export ab");
                if (madRaw < 3 * mad) fails.Add($"t={tq}: Gegenprobe rohes Stück liegt zu nah am Export ({madRaw:0.000}) – der Vergleich würde fehlende Effekte nicht erkennen");
            }

            if (beleg is { Length: > 0 })
            {
                // Wartezeit wie in der Oberfläche (960 px breit, kalter Zwischenspeicher): je Stelle 5 Läufe, Median und Höchstwert
                foreach (var t in times)
                {
                    var ms = new List<long>();
                    for (var r = 0; r < 5; r++) { var c = P($"cache-{t}-{r}"); sw.Restart(); Assert.NotNull(await VideoLook.RenderFrameAsync(ff, p, t, c, 960)); ms.Add(sw.ElapsedMilliseconds); }
                    ms.Sort(); Log($"Wartezeit Standbild 960 px bei {t.ToString("0.0", Inv)} s: Median {ms[2]} ms, Höchstwert {ms[^1]} ms ({string.Join("/", ms)})");
                }
            }
            // Abbrechen (Scrubben): der eigene FFmpeg-Prozess endet, es bleibt keine halbe Datei liegen
            var cancelDir = P("abbruch");
            using (var cts = new CancellationTokenSource(40))
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VideoLook.RenderFrameAsync(ff, p, 3.5, cancelDir, 0, null, null, cts.Token));
            Assert.Empty(Directory.GetFiles(cancelDir));
            Log("Abbrechen nach 40 ms: Ausnahme, kein Bild und keine halbe Datei im Zwischenspeicher");
            // Wiedergabe mit Effekten: fertige Bilder 640×360 aus demselben Graphen, Durchsatz messen
            if (Environment.GetEnvironmentVariable("T0695_DEBUG") is { Length: > 0 }) Log("  stream " + string.Join(" ", VideoLook.StreamArgs(p, 0, 640, 360).Select(x => x.Contains(' ') || x.Contains(';') ? "\"" + x + "\"" : x)));
            sw.Restart();
            using (var stream = new PreviewStream(ff, VideoLook.StreamArgs(p, 0, 640, 360), 640, 360, 30))
            {
                var buf = new byte[640 * 360 * 4]; var frames = 0; double firstMs = -1;
                var until = Stopwatch.StartNew();
                while (until.ElapsedMilliseconds < 30000 && frames < 300)
                {
                    if (stream.TryCopy((frames + 0.5) / 30.0, buf, 640 * 4)) { if (frames == 0) firstMs = sw.ElapsedMilliseconds; frames++; }
                    else if (stream.Failed) break; else Thread.Sleep(1);
                }
                var total = sw.ElapsedMilliseconds;
                Log($"Wiedergabe mit Effekten 640×360 aus 1080p: {frames} Bilder in {total} ms = {(frames * 1000.0 / Math.Max(1, total)).ToString("0.0", Inv)} Bilder/s (Ziel 30), erstes Bild nach {firstMs.ToString("0", Inv)} ms");
                if (frames < 290) fails.Add($"Wiedergabe: nur {frames} Bilder");
            }
            Log("Schwelle: mittlere Abweichung je Farbkanal < " + limit.ToString("0.0", Inv) + " von 255 (H.264-Verlust im Export; ein falsches Bild oder fehlender Effekt liegt deutlich darüber, siehe Gegenproben)");
            if (beleg is { Length: > 0 }) { File.WriteAllText(Path.Combine(beleg, "kern-rohlog.txt"), log.ToString()); File.Copy(P("projekt.json"), Path.Combine(beleg, "projekt.json"), true); }
            Console.WriteLine(log.ToString());
            Assert.True(fails.Count == 0, string.Join(Environment.NewLine, fails) + Environment.NewLine + log);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
