using System.Diagnostics;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Pilot 4.0, V1: Vorschau-Bilder aus ffmpeg.</summary>
public class PreviewStreamTests
{
    public sealed class FfmpegFactAttribute : FactAttribute { public FfmpegFactAttribute() { if (Cli.Find("ffmpeg") is null) Skip = "ffmpeg fehlt"; } }

    [Fact(DisplayName = "Vorschau V1: ffmpeg-Aufruf liefert Rohbilder fester Größe ab der Stelle, ohne Ton")]
    public void Args()
    {
        var a = PreviewStream.Args("film.mp4", 12.5, 3, 640, 360, 25);
        Assert.Equal("12.5", a[a.ToList().IndexOf("-ss") + 1]);
        Assert.Contains("-an", a); Assert.Contains("rawvideo", a); Assert.Contains("bgra", a); Assert.Equal("pipe:1", a[^1]);
        Assert.Contains("fps=25,scale=640:360", a[a.ToList().IndexOf("-vf") + 1]);
    }

    [Fact(DisplayName = "Vorschau V1: fehlendes ffmpeg meldet Fehler statt abzustürzen")]
    public void Missing_ffmpeg()
    {
        using var s = new PreviewStream("gibt-es-nicht-ffmpeg", "a.mp4", 0, 1, 16, 16, 10);
        Assert.True(s.Failed);
        Assert.False(s.TryCopy(0, new byte[16 * 16 * 4], 16 * 4));
    }

    static string TestClip(string dir, double seconds)
    {
        var f = Path.Combine(dir, "test.mp4");
        var r = Cli.Run(Cli.Find("ffmpeg")!, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"testsrc2=size=1920x1080:rate=30:duration={seconds}", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", f], dir, 120000);
        Assert.True(r.Ok, r.Err);
        return f;
    }

    [FfmpegFact(DisplayName = "Vorschau V1 echt: 1080p-Stück liefert das zur Uhr passende Bild, verwirft zu alte, kaputte Datei meldet Fehler")]
    public void Real_frames()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-vorschau").FullName;
        try
        {
            var clip = TestClip(dir, 2);
            using (var s = new PreviewStream(Cli.Find("ffmpeg")!, clip, 0.5, 1, 320, 180, 25))
            {
                var buf = new byte[320 * 180 * 4]; var sw = Stopwatch.StartNew(); var got = false;
                while (!got && sw.ElapsedMilliseconds < 15000) { got = s.TryCopy(0, buf, 320 * 4); if (!got) Thread.Sleep(5); }
                Assert.True(got, "kein erstes Bild");
                Assert.Contains(buf, b => b != 0);
                Assert.False(s.TryCopy(0, buf, 320 * 4), "dasselbe Bild darf nicht zweimal kommen");
            }
            var bad = Path.Combine(dir, "kaputt.mp4"); File.WriteAllBytes(bad, [1, 2, 3, 4]);
            using var b2 = new PreviewStream(Cli.Find("ffmpeg")!, bad, 0, 1, 16, 16, 10);
            var sw2 = Stopwatch.StartNew();
            while (!b2.Failed && sw2.ElapsedMilliseconds < 10000) Thread.Sleep(10);
            Assert.True(b2.Failed);
        }
        finally { Directory.Delete(dir, true); }
    }

    [FfmpegFact(DisplayName = "Vorschau V1 Messung: 1080p-Stück wird in Vorschaugröße schneller als Echtzeit (30 Bilder/s) dekodiert")]
    public void Decode_faster_than_realtime()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-vorschau").FullName;
        try
        {
            var clip = TestClip(dir, 4);
            using var s = new PreviewStream(Cli.Find("ffmpeg")!, clip, 0, 4, 640, 360, 30);
            var buf = new byte[640 * 360 * 4]; var sw = Stopwatch.StartNew(); var n = 0;
            for (var i = 0; i < 120 && sw.ElapsedMilliseconds < 30000;)
            {
                if (s.TryCopy(i / 30.0, buf, 640 * 4)) { n++; i++; }
                else Thread.Sleep(1);
            }
            Assert.Equal(120, n);
            var fps = n / sw.Elapsed.TotalSeconds;
            Assert.True(fps >= 30, $"nur {fps:0} Bilder/s");
        }
        finally { Directory.Delete(dir, true); }
    }
}
