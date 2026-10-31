using System.Text.Json.Nodes;
using Trendo.Core.Media;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>CORE-08 / Masterprompt 131: Videoschnitt – Schnittoperationen und Export (mit echtem FFmpeg, wenn installiert).</summary>
public class VideoEditTests
{
    static JsonObject Project()
    {
        var p = VideoEdit.NewProject();
        VideoEdit.Add(p, new EditClip("a", "a.mp4", 0, 10, 10));
        VideoEdit.Add(p, new EditClip("b", "b.png", 0, 5, 3600, false));
        return p;
    }

    [Fact(DisplayName = "Videoschnitt: Länge, Teilen am Abspielkopf, Kürzen innerhalb der Quelle, Verschieben, Entfernen, Stück unter der Zeit")]
    public void Edit_operations()
    {
        var p = Project(); Assert.Equal(15, VideoEdit.Duration(p), 3);
        Assert.True(VideoEdit.Split(p, 4)); var c = VideoEdit.Clips(p);
        Assert.Equal(3, c.Count); Assert.Equal((0.0, 4.0), (c[0].In, c[0].Out)); Assert.Equal((4.0, 10.0), (c[1].In, c[1].Out)); Assert.Equal(15, VideoEdit.Duration(p), 3);
        Assert.False(VideoEdit.Split(p, 4.05)); // zu nah an der Schnittkante
        VideoEdit.Trim(p, 1, 2, 20); c = VideoEdit.Clips(p); Assert.Equal((2.0, 10.0), (c[1].In, c[1].Out)); // Ende bleibt in der Quelle
        VideoEdit.Trim(p, 0, 3.95, 3.96); Assert.Equal(VideoEdit.MinLength, VideoEdit.Clips(p)[0].Length, 3);
        VideoEdit.Move(p, 2, 0); Assert.Equal("b.png", VideoEdit.Clips(p)[0].File);
        Assert.Equal((2, 2.3), VideoEdit.At(p, 5.5)!.Value is var at ? (at.Index, Math.Round(at.SourceTime, 3)) : default);
        VideoEdit.Remove(p, 0); Assert.Equal(2, VideoEdit.Clips(p).Count);
        VideoEdit.Add(p, new EditClip("m", "m.mp3", 0, 30, 30), "music"); VideoEdit.SetMusic(p, 0, 2, 0.5);
        Assert.Equal(32, VideoEdit.Duration(p), 3); Assert.Equal(0.5, VideoEdit.Clips(p, "music")[0].Volume);
    }

    [Fact(DisplayName = "Videoschnitt-Export: Aufruf schneidet jedes Stück, Bild als Standbild, Stille für Stücke ohne Ton, Musik mit Verzögerung und Lautstärke")]
    public void Export_args()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ve-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            foreach (var f in new[] { "a.mp4", "b.png", "m.mp3" }) File.WriteAllText(Path.Combine(dir, f), "x");
            var p = VideoEdit.NewProject();
            VideoEdit.Add(p, new EditClip("a", Path.Combine(dir, "a.mp4"), 2, 6, 10)); VideoEdit.Add(p, new EditClip("b", Path.Combine(dir, "b.png"), 0, 3, 3600, false));
            VideoEdit.Add(p, new EditClip("m", Path.Combine(dir, "m.mp3"), 0, 7, 60), "music"); VideoEdit.SetMusic(p, 0, 1.5, 0.4);
            var a = VideoEdit.ExportArgs(p, Path.Combine(dir, "film.mp4")); var s = string.Join(" ", a);
            Assert.Contains("-ss 2 -t 4 -i", s); Assert.Contains("-loop 1", s); Assert.Contains("anullsrc", s);
            Assert.Contains("concat=n=2:v=1:a=1", s); Assert.Contains("adelay=1500|1500", s); Assert.Contains("volume=0.4", s); Assert.Contains("amix=inputs=2", s);
            Assert.EndsWith("film.mp4", a[^1]);
            Assert.Throws<InvalidOperationException>(() => VideoEdit.ExportArgs(VideoEdit.NewProject(), "x.mp4"));
            File.Delete(Path.Combine(dir, "b.png")); Assert.Throws<FileNotFoundException>(() => VideoEdit.ExportArgs(p, "x.mp4"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Videoschnitt-Export mit echtem FFmpeg: Testbild mit Ton + Standbild + Musik → MP4 mit Bild und Ton in der erwarteten Länge")]
    public void Export_real()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return; // ohne FFmpeg nur der Aufruf-Test oben
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ve-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string P(string n) => Path.Combine(dir, n);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=25:duration=4", "-f", "lavfi", "-i", "sine=frequency=440:duration=4", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", P("a.mp4")], dir, 60000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=orange:size=800x600", "-frames:v", "1", P("b.png")], dir, 60000).Ok);
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "sine=frequency=220:duration=10", P("m.mp3")], dir, 60000).Ok);
            var p = VideoEdit.NewProject(640, 360);
            VideoEdit.Add(p, VideoEdit.NewClip(P("a.mp4"), MediaStudio.Duration(ff, P("a.mp4"))!.Value, VideoEdit.HasAudioStream(ff, P("a.mp4"))));
            VideoEdit.Add(p, VideoEdit.NewClip(P("b.png"), 0, false)); VideoEdit.Trim(p, 1, 0, 2);
            VideoEdit.Split(p, 2); VideoEdit.Remove(p, 1); // 2 s Testbild + 2 s Standbild
            VideoEdit.Add(p, VideoEdit.NewClip(P("m.mp3"), 10, true), "music"); VideoEdit.Trim(p, 0, 0, 3, "music"); VideoEdit.SetMusic(p, 0, 0.5, 0.5);
            Assert.True(VideoEdit.HasAudioStream(ff, P("a.mp4"))); Assert.False(VideoEdit.HasAudioStream(ff, P("b.png")));
            var o = VideoEdit.Export(ff, p, P("film.mp4"));
            Assert.InRange(MediaStudio.Duration(ff, o)!.Value, 3.8, 4.3); Assert.True(VideoEdit.HasAudioStream(ff, o));
        }
        finally { Directory.Delete(dir, true); }
    }
}
