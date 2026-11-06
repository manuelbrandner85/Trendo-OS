using System.Text;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-11 (06.11.2026): LUT je Stück, Scopes und Farbvergleich.</summary>
public class ColorScopesTests
{
    /// <summary>3D-LUT der Größe 2: Rot läuft am schnellsten (wie im .cube-Format).</summary>
    static string Cube(Func<double, double, double, (double, double, double)> map, string title = "Test")
    {
        var sb = new StringBuilder($"TITLE \"{title}\"\nLUT_3D_SIZE 2\n");
        for (var b = 0; b <= 1; b++) for (var g = 0; g <= 1; g++) for (var r = 0; r <= 1; r++) { var (x, y, z) = map(r, g, b); sb.Append(FormattableString.Invariant($"{x:0.0} {y:0.0} {z:0.0}\n")); }
        return sb.ToString();
    }

    [Fact(DisplayName = "S3-11-N: LUT-Prüfung – 1D-LUT, unvollständige, beschädigte LUT und ICC-Profil werden abgelehnt; Stück behält seine Farbe; Messwerte und Vergleich")]
    public void Cube_and_compare()
    {
        Assert.Equal(new ColorScopes.Cube(2, "Neutral"), ColorScopes.ParseCube(Cube((r, g, b) => (r, g, b), "Neutral")));
        Assert.Throws<ArgumentException>(() => ColorScopes.ParseCube("LUT_1D_SIZE 2\n0 0 0\n1 1 1\n"));
        Assert.Throws<ArgumentException>(() => ColorScopes.ParseCube("LUT_3D_SIZE 2\n0 0 0\n1 1 1\n"));
        Assert.Throws<ArgumentException>(() => ColorScopes.ParseCube(Cube((r, g, b) => (r, g, b)).Replace("1.0 1.0 1.0", "1.0 abc 1.0")));
        Assert.Throws<ArgumentException>(() => ColorScopes.ParseCube(Cube((r, g, b) => (r * 9, g, b))));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-lut-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var icc = Path.Combine(dir, "profil.icc"); File.WriteAllBytes(icc, [1, 2]);
            Assert.Throws<ArgumentException>(() => ColorScopes.ValidateCubeFile(icc));
            var a = Path.Combine(dir, "a.mp4"); File.WriteAllBytes(a, [0]);
            var p = VideoEdit.NewProject(); VideoEdit.Add(p, VideoEdit.NewClip(a, 2, true)); var id = VideoEdit.Clips(p)[0].Id;
            var broken = Path.Combine(dir, "kaputt.cube"); File.WriteAllText(broken, "LUT_3D_SIZE 2\n0 0 0\n");
            Assert.Throws<ArgumentException>(() => VideoPro.SetFx(p, id, new VideoPro.ClipFx(Lut: broken)));
            Assert.Equal("", VideoPro.Fx(p, id).Lut); // nichts gesetzt
            var good = Path.Combine(dir, "warm.cube"); File.WriteAllText(good, Cube((r, g, b) => (r, g, b)));
            VideoPro.SetFx(p, id, new VideoPro.ClipFx(Lut: good));
            Assert.Equal(good, VideoPro.Fx(p, id).Lut); Assert.Contains("lut3d=file='", VideoPro.ColorFilter(VideoPro.Fx(p, id)));
        }
        finally { Directory.Delete(dir, true); }
        var log = "[Parsed_metadata_2 @ 0x1] frame:0 pts:0\nlavfi.signalstats.YMIN=16\nlavfi.signalstats.YAVG=120.5\nlavfi.signalstats.YMAX=235\nlavfi.signalstats.SATAVG=30.25\nlavfi.signalstats.HUEAVG=200.1";
        Assert.Equal(new ColorScopes.Stats(120.5, 16, 235, 30.25, 200.1), ColorScopes.ParseStats(log));
        Assert.Null(ColorScopes.ParseStats("nichts"));
        Assert.True(ColorScopes.Compare(new(120, 16, 235, 30, 200), new(122, 16, 235, 28, 205)).Equivalent);
        var bad = ColorScopes.Compare(new(120, 16, 235, 30, 20), new(120, 16, 235, 30, 200));
        Assert.False(bad.Equivalent); Assert.Contains("Farbton", bad.Text);
        Assert.True(ColorScopes.Compare(new(120, 16, 235, 2, 20), new(121, 16, 235, 2, 200)).Equivalent); // Grautöne: Farbton zählt nicht
    }

    [Fact(DisplayName = "S3-11-P echt mit FFmpeg: Scopes-Bild (Bild · Waveform · Vektorskop), neutrale LUT gilt als gleichwertig, falsche LUT (Farben vertauscht) nicht; Export mit LUT läuft")]
    public void Real_scopes_and_lut()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-lut-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            Cli.Result Run(IReadOnlyList<string> a) => Cli.Run(ff, a, dir, 120000);
            var clip = Path.Combine(dir, "szene.mp4");
            Assert.True(Run(["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=25:d=2", "-pix_fmt", "yuv420p", clip]).Ok);
            var neutral = Path.Combine(dir, "neutral.cube"); File.WriteAllText(neutral, Cube((r, g, b) => (r, g, b), "Neutral"));
            var wrong = Path.Combine(dir, "falsch.cube"); File.WriteAllText(wrong, Cube((r, g, b) => (b, r, g), "Falsch"));
            ColorScopes.Stats S(string extra) => ColorScopes.ParseStats(Run(ColorScopes.StatsArgs(clip, 1, extra)).Err) ?? throw new Xunit.Sdk.XunitException("keine Messung");
            var plain = S(""); var withNeutral = S(ColorScopes.LutFilter(neutral)); var withWrong = S(ColorScopes.LutFilter(wrong));
            var mNeutral = ColorScopes.Compare(withNeutral, plain); var mWrong = ColorScopes.Compare(withWrong, plain);
            Assert.True(mNeutral.Equivalent, mNeutral.Text); // Mittelwerte allein reichen bei vertauschten Kanälen nicht – darum bildgenau:
            ColorScopes.Match Eq(string lut) => ColorScopes.Equivalence(VideoEnhance.ParseQuality(Run(ColorScopes.EquivalenceArgs(clip, 1, ColorScopes.LutFilter(lut))).Err) ?? throw new Xunit.Sdk.XunitException("keine Messung"));
            var eNeutral = Eq(neutral); var eWrong = Eq(wrong);
            Assert.True(eNeutral.Equivalent, eNeutral.Text); Assert.False(eWrong.Equivalent, eWrong.Text);
            var png = Path.Combine(dir, "scopes.png");
            var rs = Run(ColorScopes.ScopeArgs(clip, 1, png, ColorScopes.LutFilter(wrong)));
            Assert.True(File.Exists(png), rs.Err);
            Assert.Contains("1440x270", Run(["-hide_banner", "-i", png]).Err);
            var p = VideoEdit.NewProject(320, 180); VideoEdit.Add(p, VideoEdit.NewClip(clip, 2, false)); VideoPro.SetFx(p, VideoEdit.Clips(p)[0].Id, new VideoPro.ClipFx(Lut: wrong));
            var outp = Path.Combine(dir, "export.mp4"); var re = Run(VideoEdit.ExportArgs(p, outp)); Assert.True(File.Exists(outp), re.Err);
            var exported = VideoEnhance.ParseQuality(Run(VideoEnhance.CompareArgs(outp, clip, 320, 180)).Err)!;
            Assert.False(ColorScopes.Equivalence(exported).Equivalent); // LUT wirkt im Export
            if (Environment.GetEnvironmentVariable("TRENDO_LUT_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                foreach (var f in Directory.GetFiles(dir)) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
                File.WriteAllText(Path.Combine(keep, "messung.txt"), $"ohne LUT: {plain}\nneutrale LUT: {withNeutral}\n  → {mNeutral.Text}\nfalsche LUT: {withWrong}\n  → Mittelwerte: {mWrong.Text}\n  → bildgenau neutral: {eNeutral.Text}\n  → bildgenau falsch: {eWrong.Text}\nExport mit falscher LUT: {exported}\n");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
