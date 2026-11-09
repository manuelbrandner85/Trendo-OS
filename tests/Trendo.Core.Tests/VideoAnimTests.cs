using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Pilot 4.0: V5 Keyframes, V6 Effektstapel.</summary>
public class VideoAnimTests
{
    public sealed class FfmpegFactAttribute : FactAttribute { public FfmpegFactAttribute() { if (Cli.Find("ffmpeg") is null) Skip = "ffmpeg fehlt"; } }

    static JsonObject P() => VideoEdit.NewProject();

    [Fact(DisplayName = "V5: Keyframes bleiben sortiert, gleiche Stelle ersetzt, Werte werden begrenzt, leere Daten verschwinden")]
    public void Keys()
    {
        var p = P();
        VideoAnim.SetKey(p, "c", "opacity", 2, 0.5); VideoAnim.SetKey(p, "c", "opacity", 0, 1); VideoAnim.SetKey(p, "c", "opacity", 2, 9);
        var k = VideoAnim.Keys(p, "c", "opacity");
        Assert.Equal([0.0, 2.0], k.Select(x => x.Time)); Assert.Equal(1, k[1].Value);
        Assert.Throws<ArgumentException>(() => VideoAnim.SetKey(p, "c", "wackeln", 0, 1));
        Assert.Throws<ArgumentException>(() => VideoAnim.SetKey(p, "c", "x", double.NaN, 1));
        VideoAnim.RemoveKey(p, "c", "opacity", 0); VideoAnim.RemoveKey(p, "c", "opacity", 0);
        Assert.False(p.ContainsKey("anim"));
    }

    [Fact(DisplayName = "V5: Zwischenwerte linear, davor und danach der Randwert")]
    public void Interpolation()
    {
        VideoAnim.Key[] k = [new(1, 0), new(3, 10)];
        Assert.Equal(0, VideoAnim.ValueAt(k, 0, 5)); Assert.Equal(5, VideoAnim.ValueAt(k, 2, 5)); Assert.Equal(10, VideoAnim.ValueAt(k, 9, 5)); Assert.Equal(5, VideoAnim.ValueAt([], 2, 5));
        Assert.Equal("7", VideoAnim.Expr([], 7));
        Assert.Contains("lte(t\\,1)", VideoAnim.Expr(k, 0));
    }

    [Fact(DisplayName = "V6: Effekte hinzufügen, Reihenfolge, An und Aus, Entfernen; unbekannte Art wird abgelehnt")]
    public void Stack()
    {
        var p = P();
        VideoAnim.AddEffect(p, "c", "blur"); VideoAnim.AddEffect(p, "c", "gray");
        var l = VideoAnim.Stack(p, "c");
        Assert.StartsWith(",gblur=", VideoAnim.StackFilter(p, "c")); Assert.EndsWith(",hue=s=0", VideoAnim.StackFilter(p, "c"));
        VideoAnim.MoveEffect(p, "c", l[1].Id, -1);
        Assert.StartsWith(",hue=s=0", VideoAnim.StackFilter(p, "c"));
        VideoAnim.MoveEffect(p, "c", l[1].Id, -1); // schon oben: bleibt
        VideoAnim.ToggleEffect(p, "c", l[0].Id);
        Assert.Equal(",hue=s=0", VideoAnim.StackFilter(p, "c"));
        VideoAnim.SetAmount(p, "c", l[0].Id, 9); Assert.Equal(1, VideoAnim.Stack(p, "c").First(i => i.Id == l[0].Id).Amount);
        VideoAnim.RemoveEffect(p, "c", l[0].Id); VideoAnim.RemoveEffect(p, "c", l[1].Id);
        Assert.False(p.ContainsKey("fxstack"));
        Assert.Throws<ArgumentException>(() => VideoAnim.AddEffect(p, "c", "boese"));
    }

    [FfmpegFact(DisplayName = "V5 und V6 echt: Stück mit Bewegung, Deckkraft, Tonverlauf, Effekten und bewegter Einblendung wird von ffmpeg wirklich gerendert")]
    public void Real_render()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-anim").FullName;
        try
        {
            var ff = Cli.Find("ffmpeg")!;
            string Gen(string name, params string[] a) { var f = Path.Combine(dir, name); var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", .. a, f], dir, 120000); Assert.True(r.Ok, r.Err); return f; }
            var clip = Gen("a.mp4", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=2", "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest");
            var logo = Gen("logo.png", "-f", "lavfi", "-i", "color=c=red:s=64x64:d=0.1", "-frames:v", "1");
            var p = VideoEdit.NewProject(640, 360); p["fps"] = 25;
            VideoEdit.Store(p, [new EditClip("c1", clip, 0, 2, 2)]);
            VideoAnim.SetKey(p, "c1", "scale", 0, 1); VideoAnim.SetKey(p, "c1", "scale", 2, 1.5);
            VideoAnim.SetKey(p, "c1", "x", 0, -0.1); VideoAnim.SetKey(p, "c1", "x", 2, 0.1);
            VideoAnim.SetKey(p, "c1", "opacity", 0, 0.2); VideoAnim.SetKey(p, "c1", "opacity", 1, 1);
            VideoAnim.SetKey(p, "c1", "vol", 0, 0); VideoAnim.SetKey(p, "c1", "vol", 1, 1);
            VideoAnim.AddEffect(p, "c1", "vignette"); VideoAnim.AddEffect(p, "c1", "gray");
            var o = VideoPro.AddOverlay(p, logo, 0.2, 1.5);
            VideoAnim.SetKey(p, o.Id, "x", 0, 0); VideoAnim.SetKey(p, o.Id, "x", 1.5, 0.8);
            VideoAnim.SetKey(p, o.Id, "scale", 0, 1); VideoAnim.SetKey(p, o.Id, "scale", 1.5, 2);
            VideoAnim.SetKey(p, o.Id, "opacity", 0, 0.3); VideoAnim.SetKey(p, o.Id, "opacity", 1.5, 1);
            var output = Path.Combine(dir, "aus.mp4");
            var run = Cli.Run(ff, VideoEdit.ExportArgs(p, output), dir, 180000);
            Assert.True(run.Ok, run.Err);
            Assert.True(new FileInfo(output).Length > 2000);
        }
        finally { Directory.Delete(dir, true); }
    }
}
