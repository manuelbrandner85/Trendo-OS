using System.Xml.Linq;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Videoschnitt X1/X2/X11/X16 (04.11.2026): Vorlagen, Encoder-Wahl, Bereich, Lautheit, nur Ton, EDL und FCPXML.</summary>
public class VideoExportTests
{
    static (string Dir, Func<string, string> P) Dir() { var d = Path.Combine(Path.GetTempPath(), "trendo-vx-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return (d, n => Path.Combine(d, n)); }

    [Fact(DisplayName = "Encoder: NVENC nur wenn gemessen vorhanden, sonst x264/x265/SVT-AV1; WhatsApp rechnet die Bitrate aus der Zielgröße")]
    public void Encoders()
    {
        var yt = VideoExport.Get("yt1080");
        Assert.Equal("libx264", VideoExport.Encoder(yt, null, 60).Encoder);
        Assert.Equal("h264_nvenc", VideoExport.Encoder(yt, new HashSet<string> { "h264_nvenc" }, 60).Encoder);
        Assert.Equal("libx265", VideoExport.Encoder(VideoExport.Get("yt4k"), new HashSet<string> { "h264_nvenc" }, 60).Encoder); // H.265 fehlt auf der Karte
        Assert.Equal("libsvtav1", VideoExport.Encoder(VideoExport.Get("av1"), null, 60).Encoder);
        var wa = VideoExport.Encoder(VideoExport.Get("whatsapp"), null, 60).Args;
        var kbps = int.Parse(wa[wa.IndexOf("-b:v") + 1].TrimEnd('k'));
        Assert.InRange(kbps * 60 / 8 + 96 * 60 / 8, 13_000, 15_400); // ≈ 15 MB in kB
        Assert.Equal("projekt", VideoExport.Get("gibtsnicht").Id);
    }

    [Fact(DisplayName = "Filter: Bereich (trim), Hochformat aus 16:9 mit weichem Hintergrund, gleiches Format nur skaliert, Lautheit −14 LUFS")]
    public void Tail()
    {
        var f = new List<string>();
        var (v, a) = VideoExport.Tail(f, "vout", "aout", 1920, 1080, new VideoExport.Options(VideoExport.Get("hoch"), From: 2, To: 5, Loudness: true));
        var s = string.Join(";", f);
        Assert.Contains("trim=start=2:end=5", s); Assert.Contains("atrim=start=2:end=5", s); Assert.Contains("boxblur", s); Assert.Contains("loudnorm=I=-14", s);
        Assert.Equal(("vsize", "aloud"), (v, a));
        f.Clear(); VideoExport.Tail(f, "vout", "aout", 1280, 720, new VideoExport.Options(VideoExport.Get("yt1080")));
        Assert.Single(f); Assert.Contains("scale=1920:1080", f[0]);
        f.Clear(); Assert.Equal(("vout", "aout"), VideoExport.Tail(f, "vout", "aout", 1280, 720, new VideoExport.Options(VideoExport.Get("projekt")))); Assert.Empty(f);
        Assert.Equal(3, VideoExport.Length(10, new VideoExport.Options(VideoExport.Presets[0], From: 2, To: 5)));
    }

    [Fact(DisplayName = "EDL und FCPXML: Schnittpunkte, Überblendung, Quelle, Musikspur – FCPXML ist gültiges XML")]
    public void Interchange()
    {
        var p = VideoEdit.NewProject(1920, 1080);
        VideoEdit.Add(p, new EditClip("a", @"C:\Filme\strand.mp4", 2, 6, 10)); VideoEdit.Add(p, new EditClip("b", @"C:\Filme\stadt.mp4", 0, 3, 8));
        VideoEdit.SetClip(p, 0, 1, "Hallo");
        VideoEdit.Add(p, new EditClip("m", @"C:\Musik\lied.mp3", 0, 7, 60), "music");
        var edl = VideoExport.Edl(p, "Mein Film");
        Assert.Contains("TITLE: Mein Film", edl); Assert.Contains("001  AX       AA/V  C         00:00:02:00 00:00:06:00 00:00:00:00 00:00:04:00", edl);
        Assert.Contains("002  AX       AA/V  D    030", edl); Assert.Contains("FROM CLIP NAME: stadt.mp4", edl); Assert.Contains("Titel „Hallo“", edl);
        var x = XDocument.Parse(VideoExport.Fcpxml(p, "Mein Film"));
        Assert.Equal("1.10", x.Root!.Attribute("version")!.Value);
        Assert.Equal(3, x.Descendants("asset").Count());
        var clips = x.Descendants("spine").Single().Elements("asset-clip").ToList();
        Assert.Equal(2, clips.Count); Assert.Equal("6000/3000s", clips[0].Attribute("start")!.Value); Assert.Equal("9000/3000s", clips[1].Attribute("offset")!.Value);
        Assert.Equal("-1", clips[0].Element("asset-clip")!.Attribute("lane")!.Value);
        Assert.StartsWith("file:///", x.Descendants("media-rep").First().Attribute("src")!.Value);
    }

    [Fact(DisplayName = "Echter Export mit FFmpeg (Software-Encoder): Bereich 1–3 s als Hochformat 1080 × 1920 mit Lautheit; nur Ton als M4A")]
    public void Export_real()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var (dir, P) = Dir();
        try
        {
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=25:duration=4", "-f", "lavfi", "-i", "sine=frequency=440:duration=4", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", P("a.mp4")], dir, 60000).Ok);
            var p = VideoEdit.NewProject(640, 360);
            VideoEdit.Add(p, VideoEdit.NewClip(P("a.mp4"), 4, true));
            var o = VideoEdit.Export(ff, p, P("hoch.mp4"), o: new VideoExport.Options(VideoExport.Get("hoch"), From: 1, To: 3, Loudness: true));
            Assert.InRange(MediaStudio.Duration(ff, o)!.Value, 1.8, 2.3);
            var probe = Cli.Run(Path.Combine(Path.GetDirectoryName(ff)!, Path.GetFileName(ff).Replace("ffmpeg", "ffprobe")), ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0", o], dir, 30000);
            Assert.Equal("1080,1920", probe.Out.Trim());
            var m = VideoEdit.Export(ff, p, P("ton.m4a"), o: new VideoExport.Options(VideoExport.Get("ton")));
            Assert.True(VideoEdit.HasAudioStream(ff, m)); Assert.InRange(MediaStudio.Duration(ff, m)!.Value, 3.8, 4.3);
        }
        finally { Directory.Delete(dir, true); }
    }
}
