using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-12 (06.11.2026): Bild verbessern mit messbaren Vorher/Nachher-Werten.</summary>
public class VideoEnhanceTests
{
    const string Log = "[Parsed_ssim_4 @ 0x1] SSIM Y:0.991 (20.5) U:0.995 V:0.996 All:0.993102 (21.6)\n[Parsed_psnr_5 @ 0x2] PSNR y:41.2 u:44.0 v:45.1 average:42.31 min:40.1 max:44.9";

    [Fact(DisplayName = "S3-12: Filterkette in fester Reihenfolge, Ton bleibt, ohne Wahl kein Aufruf; SSIM/PSNR aus der FFmpeg-Ausgabe; Urteil nur mit Messung")]
    public void Filter_parse_judge()
    {
        var o = new VideoEnhance.Options(Stabilize: true, Denoise: 0.5, Sharpen: 0.5, Upscale: 2);
        Assert.Equal("deshake,hqdn3d=4:3:6:4.5,unsharp=5:5:0.75:5:5:0,scale=iw*2:ih*2:flags=lanczos,format=yuv420p", VideoEnhance.Filter(o));
        var a = VideoEnhance.Args("in.mp4", "out.mp4", o);
        Assert.Equal("out.mp4", a[^1]); Assert.Contains("0:a?", a); Assert.Equal("copy", a[a.IndexOf("-c:a") + 1]);
        Assert.Throws<ArgumentException>(() => VideoEnhance.Args("in.mp4", "out.mp4", new VideoEnhance.Options()));
        Assert.Equal(new VideoEnhance.Quality(0.993102, 42.31), VideoEnhance.ParseQuality(Log));
        Assert.Equal(100, VideoEnhance.ParseQuality(Log.Replace("average:42.31", "average:inf"))!.Psnr);
        Assert.Null(VideoEnhance.ParseQuality("nichts gemessen"));
        Assert.Equal("gruen", VideoEnhance.Judge(new(0.80, 25), new(0.90, 28)).Light);
        Assert.False(VideoEnhance.Judge(new(0.90, 30), new(0.85, 27)).MayApply);
        Assert.Equal("gelb", VideoEnhance.Judge(new(0.90, 30), new(0.901, 30)).Light);
        Assert.Equal("gelb", VideoEnhance.JudgeChange(new(0.6, 22), stabilized: true).Light); // Stabilisieren verschiebt gewollt
        Assert.False(VideoEnhance.JudgeChange(new(0.6, 22)).MayApply);
    }

    [Fact(DisplayName = "S3-12-N: Hochskalieren mit Artefakten gilt nicht als Verbesserung – rot, nicht übernommen, Datei gelöscht; fehlende Quelle wird gemeldet")]
    public void Upscale_artifacts_rejected()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-enh-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var src = Path.Combine(dir, "a.mp4"); File.WriteAllBytes(src, [1]); var outp = Path.Combine(dir, "a-besser.mp4");
            Cli.Result Fake(IReadOnlyList<string> args)
            {
                if (args[^1] == outp) { File.WriteAllBytes(outp, [1, 2]); return new(0, "", ""); }
                return new(0, "", Log.Replace("All:0.993102", "All:0.90").Replace("average:42.31", "average:24.0"));
            }
            var r = VideoEnhance.Enhance(Fake, src, outp, new VideoEnhance.Options(Upscale: 2), 320, 180);
            Assert.Equal("rot", r.Verdict.Light); Assert.False(r.Verdict.MayApply); Assert.Contains("Artefakte", r.Verdict.Text);
            Assert.False(File.Exists(outp));
            Assert.True(VideoEnhance.JudgeUpscale(new(0.99, 40)).MayApply); Assert.DoesNotContain("besser", VideoEnhance.JudgeUpscale(new(0.99, 40)).Text);
            Assert.Throws<FileNotFoundException>(() => VideoEnhance.Enhance(Fake, Path.Combine(dir, "fehlt.mp4"), outp, new(Denoise: 0.5), 320, 180));
            Assert.Throws<InvalidOperationException>(() => VideoEnhance.Enhance(_ => new(1, "", "Conversion failed!"), src, outp, new(Denoise: 0.5), 320, 180));
            Assert.Equal((1920, 1080), VideoEnhance.ParseSize("  Stream #0:0[0x1](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p(tv, bt709, progressive), 1920x1080 [SAR 1:1 DAR 16:9], 25 fps"));
            Assert.Throws<InvalidOperationException>(() => VideoEnhance.Enhance(_ => new(1, "", "Stream #0:0: Audio: mp3, 44100 Hz"), src, outp, new(Denoise: 0.5))); // Ton statt Video: klar gemeldet
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S3-12-P echt mit FFmpeg: verrauschter Clip entrauscht – gegen das saubere Original messbar besser (SSIM, PSNR); 2× hochskaliert ohne Artefakte, absichtlich kaputtes Hochskalieren rot")]
    public void Real_denoise_measured()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-enh-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            Cli.Result Run(IReadOnlyList<string> a) => Cli.Run(ff, a, dir, 120000);
            string Make(string name, params string[] args) { var f = Path.Combine(dir, name); var r = Run(["-hide_banner", "-loglevel", "error", "-y", .. args, f]); Assert.True(File.Exists(f), r.Err); return f; }
            var clean = Make("sauber.mp4", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=25:d=2", "-c:v", "libx264", "-crf", "12", "-pix_fmt", "yuv420p");
            var noisy = Make("verrauscht.mp4", "-i", clean, "-vf", "noise=alls=28:allf=t", "-c:v", "libx264", "-crf", "12", "-pix_fmt", "yuv420p");
            VideoEnhance.Quality Q(string test, string reference) => VideoEnhance.ParseQuality(Run(VideoEnhance.CompareArgs(test, reference, 320, 180)).Err) ?? throw new Xunit.Sdk.XunitException("keine Messung");
            var r = VideoEnhance.Enhance(Run, noisy, Path.Combine(dir, "entrauscht.mp4"), new(Denoise: 0.8)); // Größe selbst erkannt
            Assert.True(r.Verdict.MayApply, r.Verdict.Text); Assert.True(File.Exists(r.Output));
            var before = Q(noisy, clean); var after = Q(r.Output, clean);
            var v = VideoEnhance.Judge(before, after);
            Assert.True(v.Light == "gruen", v.Text);
            var up = VideoEnhance.Enhance(Run, clean, Path.Combine(dir, "gross.mp4"), new(Upscale: 2), 320, 180);
            Assert.True(up.Verdict.MayApply, up.Verdict.Text);
            Assert.Contains("640x360", Run(["-hide_banner", "-i", up.Output]).Err);
            var bad = Make("kaputt-gross.mp4", "-i", clean, "-vf", "scale=iw*2:ih*2:flags=neighbor,noise=alls=60:allf=t,unsharp=7:7:5", "-c:v", "libx264", "-crf", "30", "-pix_fmt", "yuv420p");
            Assert.False(VideoEnhance.JudgeUpscale(Q(bad, clean)).MayApply);
            if (Environment.GetEnvironmentVariable("TRENDO_ENH_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                foreach (var f in Directory.GetFiles(dir)) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
                File.WriteAllText(Path.Combine(keep, "messung.txt"), $"vorher (verrauscht gegen sauber): {before}\nnachher (entrauscht gegen sauber): {after}\nUrteil: {v}\nHochskaliert: {up.Verdict}\nKaputt hochskaliert: {VideoEnhance.JudgeUpscale(Q(bad, clean))}\n");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
