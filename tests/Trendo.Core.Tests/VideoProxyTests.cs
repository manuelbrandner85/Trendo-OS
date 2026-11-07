using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Proxy-Dateien.</summary>
public class VideoProxyTests
{
    [Fact(DisplayName = "S3-02 echt mit FFmpeg: Proxy 540p gleich lang wie das Original, Vorschau nimmt den Proxy, Export immer das Original; fehlt das Original, wird nicht der Proxy ausgeliefert")]
    public void Real_proxy()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-proxy-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            Cli.Result Run(IReadOnlyList<string> a) => Cli.Run(ff, a, dir, 120000);
            var orig = Path.Combine(dir, "kamera.mp4");
            Assert.True(Run(["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=1920x1080:r=25:d=3", "-f", "lavfi", "-i", "sine=d=3", "-shortest", "-pix_fmt", "yuv420p", orig]).Ok);
            var p = VideoEdit.NewProject(1920, 1080); p["fps"] = 25; VideoEdit.Add(p, VideoEdit.NewClip(orig, 3, true));
            var made = VideoProxy.MakeAll(Run, p, Path.Combine(dir, ".proxy"));
            var px = Assert.Single(made).Proxy!; Assert.Equal("neu", made[0].Note);
            Assert.Contains("1x540", Run(["-hide_banner", "-i", px]).Err.Replace("960x540", "1x540"));
            Assert.True(new FileInfo(px).Length < new FileInfo(orig).Length);
            Assert.Equal("schon da", VideoProxy.MakeAll(Run, p, Path.Combine(dir, ".proxy"))[0].Note); // wiederverwendet
            Assert.Equal(orig, VideoProxy.ForPreview(p, orig)); // aus
            p["useProxy"] = true; Assert.Equal(px, VideoProxy.ForPreview(p, orig));
            var args = string.Join(" ", VideoEdit.ExportArgs(p, Path.Combine(dir, "film.mp4")));
            Assert.Contains("kamera.mp4", args); Assert.DoesNotContain(".proxy.mp4", args); // Export mit Original
            File.Move(orig, orig + ".weg");
            Assert.Throws<FileNotFoundException>(() => VideoEdit.ExportArgs(p, Path.Combine(dir, "film.mp4"))); // nie still den Proxy ausliefern
            Assert.Equal(px, VideoProxy.ForPreview(p, orig)); // die Vorschau darf weiter den Proxy zeigen
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "S3-02-N: Proxy mit anderer Länge wird verworfen; Proxy-Name ändert sich, wenn das Original sich ändert; Bilder bekommen keinen Proxy")]
    public void Rejects()
    {
        Assert.True(VideoProxy.SameLength(3.0, 3.04)); Assert.False(VideoProxy.SameLength(3.0, 2.5));
        Assert.Equal(12.5, VideoProxy.ParseDuration("  Duration: 00:00:12.50, start: 0.000000"));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-proxy-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var v = Path.Combine(dir, "a.mp4"); File.WriteAllBytes(v, [1, 2, 3]); var img = Path.Combine(dir, "b.png"); File.WriteAllBytes(img, [1]);
            var p1 = VideoProxy.PathFor(v, dir); File.WriteAllBytes(v, [1, 2, 3, 4]); Assert.NotEqual(p1, VideoProxy.PathFor(v, dir));
            var p = VideoEdit.NewProject(); VideoEdit.Add(p, VideoEdit.NewClip(v, 3, true)); VideoEdit.Add(p, VideoEdit.NewClip(img, 3, false));
            Cli.Result Fake(IReadOnlyList<string> a)
            {
                if (a.Contains("-c:v")) { File.WriteAllBytes(a[^1], [9]); return new(0, "", ""); } // Kodieren
                return new(1, "", a[^1].EndsWith(".proxy.mp4") ? "Duration: 00:00:02.00," : "Duration: 00:00:03.00,"); // Länge abfragen: Proxy 2 s, Original 3 s
            }
            var r = VideoProxy.MakeAll(Fake, p, Path.Combine(dir, ".proxy"));
            var only = Assert.Single(r); Assert.Null(only.Proxy); Assert.Contains("Länge weicht ab", only.Note);
            Assert.Empty(Directory.GetFiles(Path.Combine(dir, ".proxy")));
            Assert.Empty(VideoProxy.Map(p));
        }
        finally { Directory.Delete(dir, true); }
    }
}
