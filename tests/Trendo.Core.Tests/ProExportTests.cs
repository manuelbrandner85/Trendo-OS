using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-18 (06.11.2026): ProRes 422 HQ, PNG- und EXR-Bildsequenz mit WAV; fehlender Encoder → klare Alternative.</summary>
public class ProExportTests
{
    const string EncList = " V....D libx264              libx264 H.264 (codec h264)\n VF...D png                  PNG image\n A....D aac                  AAC (Advanced Audio Coding)\n VFS..D dnxhd                VC3/DNxHD\n";

    [Fact(DisplayName = "S3-18-N: fehlender Encoder (ProRes, EXR, H.265) → klare Meldung mit Alternative, die wirklich geht; ohne Alternative der Hinweis zum Einrichten")]
    public void Missing_codec_alternative()
    {
        var enc = VideoExport.ParseEncoders(EncList);
        Assert.Equal(new HashSet<string> { "libx264", "png", "aac", "dnxhd" }, enc);
        var pr = VideoExport.Check(VideoExport.Get("prores"), enc);
        Assert.False(pr.Ok); Assert.Equal("yt1080", pr.Alternative!.Id); Assert.Contains("prores_ks", pr.Text); Assert.Contains("Alternative", pr.Text);
        Assert.Equal("png", VideoExport.Check(VideoExport.Get("exr"), enc).Alternative!.Id);
        Assert.Equal("projekt", VideoExport.Check(VideoExport.Get("yt4k"), enc).Alternative!.Id);
        Assert.True(VideoExport.Check(VideoExport.Get("png"), enc).Ok);
        var none = VideoExport.Check(VideoExport.Get("exr"), new HashSet<string> { "aac" });
        Assert.False(none.Ok); Assert.Null(none.Alternative); Assert.Contains("einrichten", none.Text);
    }

    [Fact(DisplayName = "S3-18: ProRes-Aufruf mit Profil HQ, 10 Bit 4:2:2 und PCM-Ton; Sequenz schreibt Bilder und WAV in einen Ordner")]
    public void Args()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pro-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.mp4"); File.WriteAllBytes(a, [0]);
            var p = VideoEdit.NewProject(320, 180); VideoEdit.Add(p, VideoEdit.NewClip(a, 1, true));
            var pr = VideoEdit.ExportArgs(p, Path.Combine(dir, "film.mov"), new(VideoExport.Get("prores")));
            Assert.Contains("prores_ks", pr); Assert.Contains("yuv422p10le", pr); Assert.Contains("pcm_s24le", pr); Assert.DoesNotContain("yuv420p", pr); Assert.DoesNotContain("aac", pr);
            var folder = Path.Combine(dir, "Film-EXR");
            var ex = VideoEdit.ExportArgs(p, folder, new(VideoExport.Get("exr")));
            Assert.Contains(Path.Combine(folder, "Film-EXR_%05d.exr"), ex); Assert.Contains(Path.Combine(folder, "Film-EXR.wav"), ex); Assert.Contains("gbrpf32le", ex);
            Assert.False(VideoExport.Produced(folder, VideoExport.Get("exr")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S3-18-P echt mit FFmpeg: ProRes 422 HQ (.mov), EXR- und PNG-Sequenz mit WAV – Codec, Profil, Pixelformat, Bildzahl und Tonlänge per ffprobe geprüft")]
    public void Real_pro_exports()
    {
        var ff = Cli.Find("ffmpeg"); var fp = Cli.Find("ffprobe"); if (ff is null || fp is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pro-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var src = Path.Combine(dir, "szene.mp4");
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=25:d=1", "-f", "lavfi", "-i", "sine=f=440:d=1", "-shortest", "-pix_fmt", "yuv420p", src], dir, 60000).Ok);
            var p = VideoEdit.NewProject(320, 180); p["fps"] = 25; VideoEdit.Add(p, VideoEdit.NewClip(src, 1, true));
            string Probe(string f, string entries, string sel = "v:0") => Cli.Run(fp, ["-v", "error", "-select_streams", sel, "-show_entries", entries, "-of", "compact=p=0:nk=1", f], dir, 30000).Out.Trim();
            var mov = VideoEdit.Export(ff, p, Path.Combine(dir, "film.mov"), 120000, new(VideoExport.Get("prores")));
            Assert.Equal("prores|HQ|yuv422p10le", Probe(mov, "stream=codec_name,profile,pix_fmt"));
            Assert.Equal("pcm_s24le", Probe(mov, "stream=codec_name", "a:0"));
            var exr = VideoEdit.Export(ff, p, Path.Combine(dir, "Film-EXR"), 120000, new(VideoExport.Get("exr")));
            var frames = Directory.GetFiles(exr, "*.exr").Order(StringComparer.Ordinal).ToList();
            Assert.Equal(25, frames.Count); Assert.EndsWith("Film-EXR_00001.exr", frames[0]);
            Assert.StartsWith("exr|gbrpf16le", Probe(frames[0], "stream=codec_name,pix_fmt"));
            var wav = Path.Combine(exr, "Film-EXR.wav");
            Assert.Equal("pcm_s24le", Probe(wav, "stream=codec_name", "a:0"));
            Assert.InRange(double.Parse(Probe(wav, "format=duration", "a:0"), System.Globalization.CultureInfo.InvariantCulture), 0.95, 1.05);
            var png = VideoEdit.Export(ff, p, Path.Combine(dir, "Film-PNG"), 120000, new(VideoExport.Get("png")));
            Assert.Equal(25, Directory.GetFiles(png, "*.png").Length);
            Assert.True(VideoExport.Check(VideoExport.Get("prores"), VideoExport.Encoders(ff)).Ok); // dieses FFmpeg kann ProRes
            if (Environment.GetEnvironmentVariable("TRENDO_PRO_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                File.Copy(mov, Path.Combine(keep, "film.mov"), true); File.Copy(frames[0], Path.Combine(keep, Path.GetFileName(frames[0])), true); File.Copy(frames[^1], Path.Combine(keep, Path.GetFileName(frames[^1])), true);
                File.Copy(wav, Path.Combine(keep, "Film-EXR.wav"), true); File.Copy(Directory.GetFiles(png, "*.png").Order(StringComparer.Ordinal).First(), Path.Combine(keep, "Film-PNG_00001.png"), true);
                File.WriteAllText(Path.Combine(keep, "probe.txt"), $"film.mov video: {Probe(mov, "stream=codec_name,profile,pix_fmt,width,height,r_frame_rate")}\nfilm.mov audio: {Probe(mov, "stream=codec_name,sample_rate,channels", "a:0")}\nEXR: {frames.Count} Bilder, erstes: {Probe(frames[0], "stream=codec_name,pix_fmt,width,height")}\nEXR-WAV: {Probe(wav, "stream=codec_name,sample_rate", "a:0")} · {Probe(wav, "format=duration", "a:0")} s\nPNG: {Directory.GetFiles(png, "*.png").Length} Bilder\n");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
