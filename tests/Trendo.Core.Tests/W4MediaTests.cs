using Trendo.Core;
using Xunit;

public class W4_Core08_Media
{
    public static string? Sample(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "trendo-media-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        if (Cli.Find("ffmpeg") is null) return null;
        var p = Path.Combine(dir, "probe.mkv");
        var r = Cli.Run("ffmpeg", ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=duration=4:size=1920x1080:rate=25", "-f", "lavfi", "-i", "sine=frequency=440:duration=4", "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-shortest", p], dir);
        return r.Ok ? p : null;
    }

    [NeedsFact("ffmpeg", DisplayName = "CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben")]
    public void All_jobs()
    {
        var src = Sample(out var dir); if (src is null) return;
        var before = File.ReadAllBytes(src);
        foreach (var j in MediaStudio.Jobs.Where(j => j.Id != "image"))
        {
            var (r, o) = MediaStudio.Run("ffmpeg", src, j.Id, 1, 2);
            Assert.True(r.Ok, j.Id + ": " + r.Err); Assert.True(new FileInfo(o).Length > 0, j.Id);
        }
        Assert.Equal(before, File.ReadAllBytes(src));
        var trim = Path.Combine(dir, "probe-trim.mp4");
        Assert.InRange(MediaStudio.Duration("ffmpeg", trim) ?? 0, 1.5, 2.6);
        Assert.InRange(MediaStudio.Duration("ffmpeg", src) ?? 0, 3.5, 4.5);
        var (r2, o2) = MediaStudio.Run("ffmpeg", src, "mp4");
        Assert.True(r2.Ok); Assert.EndsWith("probe-mp4-2.mp4", o2);
        var (ri, oi) = MediaStudio.Run("ffmpeg", Path.Combine(dir, "probe-thumb.jpg"), "image");
        Assert.True(ri.Ok, ri.Err); Assert.True(File.Exists(oi));
    }

    [Fact(DisplayName = "CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell")]
    public void Errors()
    {
        Assert.Equal("Datei nicht gefunden.", MediaStudio.Run("ffmpeg", "/nicht/da.mp4", "mp4").Result.Err);
        var a = MediaStudio.Args("a b; rm -rf.mp4", "o.mp4", "trim", 3, 5);
        Assert.Contains("a b; rm -rf.mp4", a); Assert.Equal("-ss", a[4]); Assert.Contains("-n", a);
        Assert.Throws<ArgumentException>(() => MediaStudio.Args("a", "b", "nix"));
    }
}
