using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-12 (07.11.2026): Tempo/Tonhöhe mit Messung und Warnung.</summary>
public class TimePitchTests
{
    [Fact(DisplayName = "S4-12-N: starke Änderung → Artefakt-Warnung (gelb), extreme → abgelehnt statt still begrenzt")]
    public void Assess()
    {
        Assert.Equal("gruen", TimePitch.Assess(0.9, 2).Light);
        Assert.Equal("gelb", TimePitch.Assess(0.6, 0).Light); Assert.Contains("Artefakte", TimePitch.Assess(1.0, 7).Text);
        Assert.Equal("rot", TimePitch.Assess(3.0, 0).Light); Assert.Equal("rot", TimePitch.Assess(1.0, 14).Light);
        Assert.Throws<ArgumentException>(() => TimePitch.Args("a.wav", "b.wav", 0.3, 0));
        Assert.Contains("pitchq=quality", string.Join(" ", TimePitch.Args("a.wav", "b.wav", 1, 0.3)));
    }

    [Fact(DisplayName = "S4-12-P echt mit FFmpeg/Rubber Band: Tempo 80 % und +2 Halbtöne sowie Intonation +30 Cent bleiben im Toleranzbereich (Länge ±1 %, Tonhöhe ±10 Cent, Phase stabil)")]
    public void Real_rubberband()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-tp-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var src = Path.Combine(dir, "ton.wav");
            var x = Enumerable.Range(0, 44100 * 4).Select(i => (float)(0.4 * Math.Sin(2 * Math.PI * 220 * i / 44100.0) + 0.15 * Math.Sin(2 * Math.PI * 440 * i / 44100.0))).ToArray();
            AudioFile.WriteWav(new AudioBuf(44100, [x, (float[])x.Clone()]), src);
            var orig = AudioFile.Read(src); var lines = new List<string>();
            foreach (var (tempo, semi, name) in new[] { (0.8, 2.0, "langsam-hoeher.wav"), (1.0, 0.3, "intonation-plus30.wav"), (0.55, 9.0, "extrem.wav") })
            {
                var outp = Path.Combine(dir, name);
                var r = Cli.Run(ff, TimePitch.Args(src, outp, tempo, semi), dir, 120000); Assert.True(File.Exists(outp), r.Err);
                var m = TimePitch.Compare(orig, AudioFile.Read(outp)); var v = TimePitch.Verify(m, tempo, semi);
                lines.Add($"{name}: Tempo {tempo}, {semi:+0.##} Halbtöne → Vorab {TimePitch.Assess(tempo, semi).Light}, {v.Light}: {v.Text}");
                if (name != "extrem.wav") Assert.True(v.Light == "gruen", v.Text);
            }
            Assert.Equal("gelb", TimePitch.Assess(0.55, 9).Light);
            if (Environment.GetEnvironmentVariable("TRENDO_TP_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); foreach (var f in Directory.GetFiles(dir)) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
                File.WriteAllLines(Path.Combine(keep, "messung.txt"), lines);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
