using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-13 (07.11.2026): KI-Video – Prüfung, Aufruf, Herkunftsnachweis.</summary>
public class GenVideoTests
{
    static (GenVideo.Model M, string Dir) FakeModel()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-gen-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        string F(string n) { var p = Path.Combine(dir, n); File.WriteAllBytes(p, new byte[100]); return p; }
        return (new GenVideo.Model("wan2.2-szenen", "Wan 2.2 – Filmszenen (5B)", "Apache-2.0", F("ti2v.gguf"), F("umt5.gguf"), F("vae.safetensors")), dir);
    }

    [Fact(DisplayName = "S3-13-N: ohne Modell, ohne Beschreibung, falscher Zufallswert, Länge oder fehlende Modelldatei → erklärter Fehler vor dem Start")]
    public void Problems()
    {
        var (m, dir) = FakeModel();
        try
        {
            var ok = new GenVideo.Request("Kaffee wird eingegossen", 42, 2, GenVideo.Preview);
            Assert.Null(GenVideo.Problem(ok, m));
            Assert.Contains("Kein Video-Modell", GenVideo.Problem(ok, null));
            Assert.Contains("beschreiben", GenVideo.Problem(ok with { Prompt = "  " }, m));
            Assert.Contains("Zufallswert", GenVideo.Problem(ok with { Seed = -1 }, m));
            Assert.Contains("Zufallswert", GenVideo.Problem(ok with { Seed = 1L << 33 }, m));
            Assert.Contains("0,5 bis 5", GenVideo.Problem(ok with { Seconds = 9 }, m));
            Assert.Contains("Startbild fehlt", GenVideo.Problem(ok with { StartImage = Path.Combine(dir, "nichtda.png") }, m));
            File.Delete(m.Vae); Assert.Contains("Modelldatei fehlt: vae.safetensors", GenVideo.Problem(ok, m));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S3-13-P: Aufruf mit Zufallswert, 4·k+1 Bildern und Kacheln; Herkunftsnachweis mit Beschreibung, Seed, Modell, Lizenz, Prüfsumme; Veränderung am Clip wird erkannt")]
    public void Args_and_provenance()
    {
        var (m, dir) = FakeModel();
        try
        {
            var r = new GenVideo.Request("Ein Leuchtturm im Sturm", 1234567, 2, GenVideo.Preview);
            var a = GenVideo.Args(r, m, Path.Combine(dir, "roh.webm"));
            Assert.Equal("1234567", a[a.IndexOf("-s") + 1]); Assert.Equal("49", a[a.IndexOf("--video-frames") + 1]); Assert.Equal(5, GenVideo.Frames(0.1, 12)); Assert.Equal(("1280", "704"), (a[a.IndexOf("-W") + 1], a[a.IndexOf("-H") + 1]));
            Assert.Contains("--temporal-tiling", a); Assert.DoesNotContain("-i", a);
            Assert.Contains("-i", GenVideo.Args(r with { StartImage = m.Vae }, m, "x.webm"));
            var clip = Path.Combine(dir, "KI.mp4"); File.WriteAllBytes(clip, [1, 2, 3]); var raw = Path.Combine(dir, "roh.webm"); File.WriteAllBytes(raw, [9]);
            var p = GenVideo.Provenance(r, m, "stable-diffusion.cpp", "master-1015-e41fbcf", clip, raw, new DateTime(2026, 11, 7, 6, 0, 0, DateTimeKind.Utc));
            GenVideo.WriteProvenance(clip, p);
            var txt = File.ReadAllText(GenVideo.ProvenancePath(clip));
            Assert.Contains("\"zufallswert\": 1234567", txt); Assert.Contains("Ein Leuchtturm im Sturm", txt); Assert.Contains("Apache-2.0", txt); Assert.Contains("KI-erzeugt", txt);
            Assert.DoesNotContain(dir.Replace("\\", "\\\\"), txt); // nur Dateinamen
            Assert.Null(GenVideo.VerifyProvenance(clip));
            File.WriteAllBytes(clip, [1, 2, 4]); Assert.Contains("verändert", GenVideo.VerifyProvenance(clip));
            Assert.Contains("Kein Herkunftsnachweis", GenVideo.VerifyProvenance(raw));
            Assert.InRange(GenVideo.NewSeed(), 0, uint.MaxValue);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>Echter Lauf nur mit TRENDO_GENVIDEO_ECHT = Werkzeugordner mit eingerichtetem Wan 2.2 (dauert Minuten auf der Grafikkarte).</summary>
    [Fact(DisplayName = "S3-13 echt: Wan 2.2 erzeugt einen Clip (1280 × 704); gleicher Zufallswert ergibt praktisch dieselben Bilder, anderer Zufallswert andere; Nachweis passt")]
    public void Real_generation()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_GENVIDEO_ECHT") is not { Length: > 0 } tools) return;
        var md = Path.Combine(tools, "ki", "modelle", "wan2.2-szenen"); var sd = Path.Combine(tools, "ki", "laufzeit", "stablediffusioncpp", "sd-cli.exe");
        var ff = Cli.Find("ffmpeg"); Assert.NotNull(ff); Assert.True(File.Exists(sd), sd);
        var m = new GenVideo.Model("wan2.2-szenen", "Wan 2.2 – Filmszenen (5B)", "Apache-2.0", Path.Combine(md, "Wan2.2-TI2V-5B-Q6_K.gguf"), Path.Combine(md, "umt5-xxl-encoder-Q8_0.gguf"), Path.Combine(md, "wan2.2_vae.safetensors"));
        var keep = Environment.GetEnvironmentVariable("TRENDO_GENVIDEO_BELEG");
        var dir = keep ?? Path.Combine(Path.GetTempPath(), "trendo-genecht-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        var log = new List<string>();
        string Make(string name, long seed)
        {
            var r = new GenVideo.Request("a steaming cup of coffee on a wooden table, morning sunlight through a window, slow camera push in, photorealistic", seed, 0.5, GenVideo.Preview);
            Assert.Null(GenVideo.Problem(r, m));
            var raw = Path.Combine(dir, name + ".roh.webm"); var sw = System.Diagnostics.Stopwatch.StartNew();
            var res = Cli.Run(sd, GenVideo.Args(r, m, raw), dir, 3_600_000);
            log.Add($"{name}: Zufallswert {seed}, Exitcode {res.Exit}, {sw.Elapsed.TotalSeconds:0} s");
            Assert.True(res.Ok && File.Exists(raw), res.Err[^Math.Min(800, res.Err.Length)..]);
            var clip = Path.Combine(dir, name + ".mp4");
            Assert.True(Cli.Run(ff!, GenVideo.NormalizeArgs(raw, 0.5, 1280, 704, 24, clip), dir, 120000).Ok);
            GenVideo.WriteProvenance(clip, GenVideo.Provenance(r, m, "stable-diffusion.cpp", "master-1015-e41fbcf", clip, raw, DateTime.UtcNow));
            Assert.Null(GenVideo.VerifyProvenance(clip));
            return raw;
        }
        // Bildinhalt vergleichen (dekodierte Bilder, unabhängig vom Container)
        string Frames(string raw) { var o = Path.ChangeExtension(raw, ".rgb"); Cli.Run(ff!, ["-hide_banner", "-loglevel", "error", "-y", "-i", raw, "-f", "rawvideo", "-pix_fmt", "rgb24", o], dir, 120000); return o; }
        static double Mad(string a, string b) { var x = File.ReadAllBytes(a); var y = File.ReadAllBytes(b); var n = Math.Min(x.Length, y.Length); if (n == 0) return 999; long s = 0; for (var i = 0; i < n; i++) s += Math.Abs(x[i] - y[i]); return s / (double)n; }
        var a = Frames(Make("clip-a-seed42", 42)); var b = Frames(Make("clip-b-seed42", 42)); var c = Frames(Make("clip-c-seed4711", 4711));
        double same = Mad(a, b), diff = Mad(a, c);
        log.Add($"Mittlere Bildabweichung gleicher Zufallswert: {same:0.000} (0–255), anderer Zufallswert: {diff:0.000}");
        File.WriteAllLines(Path.Combine(dir, "messung.txt"), log);
        foreach (var f in new[] { a, b, c }) File.Delete(f);
        // Gemessen 07.11.2026: die Grafikkarte (Vulkan) rechnet nicht bitgenau gleich – gleicher Zufallswert weicht im Mittel ~1 von 255 ab (unsichtbar), anderer ~80
        Assert.True(same < 3.0, string.Join("\n", log)); Assert.True(diff > 10 * Math.Max(0.2, same), string.Join("\n", log));
        if (keep is null) Directory.Delete(dir, true);
    }
}
