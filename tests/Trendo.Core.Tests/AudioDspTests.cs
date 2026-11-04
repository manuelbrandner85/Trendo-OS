using System.Globalization;
using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Audio-Editor – Vorbild-Funktionen (Audacity, Audition, RX; 04.11.2026): Rauschprofil, Brummen, Stille kürzen, LUFS, Kompressor, Spektrogramm.</summary>
public class AudioDspTests
{
    const int Rate = 22050;
    static AudioBuf Sig(double sec, Func<int, double> f, int rate = Rate) { var a = AudioBuf.Silence(sec, rate, 2); for (var i = 0; i < a.Length; i++) { var v = (float)f(i); a.Data[0][i] = v; a.Data[1][i] = v; } return a; }
    static double Rms(float[] x, int a, int b) { double s = 0; for (var i = a; i < b; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, b - a)); }

    [Fact(DisplayName = "Rauschen nach Profil: Rauschen im Teil ohne Ton sinkt um > 12 dB, der Ton bleibt (±1,5 dB)")]
    public void Denoise()
    {
        var rnd = new Random(3);
        var a = Sig(4, i => (rnd.NextDouble() - 0.5) * 0.04 + (i > Rate * 2 ? 0.3 * Math.Sin(2 * Math.PI * 440 * i / Rate) : 0));
        var noiseBefore = Rms(a.Data[0], 0, Rate * 2); var toneBefore = Rms(a.Data[0], Rate * 2 + 4000, Rate * 4 - 4000);
        var prof = AudioDsp.NoiseProfile(a.Data[0], 0, Rate);
        AudioDsp.Denoise(a.Data[0], 0, a.Length, prof);
        var noiseAfter = Rms(a.Data[0], 2000, Rate * 2 - 2000); var toneAfter = Rms(a.Data[0], Rate * 2 + 4000, Rate * 4 - 4000);
        Assert.True(20 * Math.Log10(noiseBefore / noiseAfter) > 12, $"Rauschen nur {20 * Math.Log10(noiseBefore / noiseAfter):0.0} dB leiser");
        Assert.InRange(20 * Math.Log10(toneAfter / toneBefore), -1.5, 1.5);
        Assert.Throws<InvalidOperationException>(() => AudioDsp.NoiseProfile(a.Data[0], 0, 100));
    }

    [Fact(DisplayName = "Brummen: 50 Hz und 150 Hz verschwinden (> 20 dB), 1 kHz bleibt")]
    public void Dehum()
    {
        var a = Sig(3, i => 0.2 * Math.Sin(2 * Math.PI * 50 * i / Rate) + 0.1 * Math.Sin(2 * Math.PI * 150 * i / Rate) + 0.2 * Math.Sin(2 * Math.PI * 1000 * i / Rate));
        var lv0 = MusicPro.BandLevels(a);
        AudioDsp.Dehum(a, 0, a.Length);
        var b = Sig(3, i => 0.2 * Math.Sin(2 * Math.PI * 1000 * i / Rate));
        // gemessen ab 0,5 s: der schmale Kerbfilter braucht ~0,3 s zum Einschwingen (echtes Brummen läuft durchgehend)
        var lv1 = MusicPro.BandLevels(a.Slice(Rate / 2, a.Length)); var lvRef = MusicPro.BandLevels(b.Slice(Rate / 2, b.Length));
        Assert.True(lv0[0] - lvRef[0] > 20, "Prüfsignal hat kein Brummen");
        Assert.True(lv1[0] - lvRef[0] < 3, $"Brummen nicht entfernt: {lv1[0] - lvRef[0]:0.0} dB über dem reinen 1-kHz-Ton"); // im 60-Hz-Band bleibt nur der Rest des 1-kHz-Tons
        Assert.InRange(lv1[3] - lvRef[3], -1, 1);
    }

    [Fact(DisplayName = "Stille kürzen: zwei lange Pausen werden kurz, kurze Pause bleibt; Kompressor senkt Spitzen")]
    public void Truncate_and_compress()
    {
        var a = Sig(7, i => { var t = i / (double)Rate; return t is < 1 or (> 3 and < 3.3) or (> 3.5 and < 4) or > 6 ? 0.3 * Math.Sin(2 * Math.PI * 300 * t) : 0; });
        var (d, cut) = AudioDsp.TruncateSilence(a, -45, 0.6, 0.3);
        Assert.Equal(2, cut); Assert.InRange(d[0].Length / (double)Rate, 3.4, 3.8); // 2,8 s Ton + 0,2 s kurze Pause + 2 × 0,3 s
        var loud = Sig(2, i => (i < Rate ? 0.1 : 0.9) * Math.Sin(2 * Math.PI * 200 * i / Rate));
        AudioDsp.Compress(loud, 0, loud.Length, -20, 4);
        var quiet = Rms(loud.Data[0], 2000, Rate - 2000); var hi = Rms(loud.Data[0], Rate + 4000, 2 * Rate - 2000);
        Assert.True(hi / quiet < 0.9 / 0.1 * 0.6, "Abstand laut/leise wurde nicht kleiner");
    }

    [Fact(DisplayName = "LUFS nach BS.1770 stimmt mit FFmpeg ebur128 überein (±0,5 LU) – Musiksignal und Sinus")]
    public void Lufs_matches_ffmpeg()
    {
        var rnd = new Random(1);
        var tone = Sig(6, i => 0.1 * Math.Sin(2 * Math.PI * 997 * i / 48000), 48000);
        var music = Sig(8, i => 0.25 * Math.Sin(2 * Math.PI * 110 * i / 48000) + 0.1 * Math.Sin(2 * Math.PI * 2500 * i / 48000) + (rnd.NextDouble() - 0.5) * 0.05 * (i % 48000 < 24000 ? 1 : 0.2), 48000);
        var ff = Cli.Find("ffmpeg");
        foreach (var (a, name) in new[] { (tone, "sinus"), (music, "musik") })
        {
            var mine = AudioDsp.Lufs(a);
            Assert.InRange(mine, -40, 0);
            if (ff is null) continue;
            var dir = Path.Combine(Path.GetTempPath(), "trendo-lufs-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
            try
            {
                var f = Path.Combine(dir, name + ".wav"); AudioFile.WriteWav(a, f, 24);
                var r = Cli.Run(ff, ["-hide_banner", "-nostats", "-i", f, "-af", "ebur128", "-f", "null", "-"], dir, 60000);
                var m = Regex.Matches(r.Err, @"I:\s+(-?\d+(?:\.\d+)?) LUFS"); Assert.True(m.Count > 0, r.Err[^Math.Min(400, r.Err.Length)..]);
                var theirs = double.Parse(m[^1].Groups[1].Value, CultureInfo.InvariantCulture);
                Assert.True(Math.Abs(mine - theirs) <= 0.5, $"{name}: Trendo {mine} LUFS, FFmpeg {theirs} LUFS");
            }
            finally { Directory.Delete(dir, true); }
        }
    }

    [Fact(DisplayName = "Spektrogramm: ein 1-kHz-Ton ergibt eine helle Zeile, Stille bleibt dunkel; Schritt „Lautheit“ trifft −16 LUFS")]
    public void Spectrogram_and_loudness_step()
    {
        var a = Sig(2, i => 0.3 * Math.Sin(2 * Math.PI * 1000 * i / Rate));
        var img = AudioDsp.Spectrogram(a, 20, 64);
        var rowMax = Enumerable.Range(0, 64).MaxBy(r => img[10, r]);
        Assert.True(img[10, rowMax] > 0.6); Assert.True(img[10, 0] < 0.3 && img[10, 63] < 0.3);
        var dir = Path.Combine(Path.GetTempPath(), "trendo-loud-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "a.wav"); AudioFile.WriteWav(Sig(5, i => 0.05 * Math.Sin(2 * Math.PI * 500 * i / 48000), 48000), f, 24);
            var doc = AudioEdit.New(f); AudioEdit.Add(doc, AudioEdit.Op("loudness", 0, 0, -16));
            Assert.InRange(AudioDsp.Lufs(AudioEdit.Render(doc)), -16.3, -15.7);
            var d2 = AudioEdit.New(f); AudioEdit.Add(d2, AudioEdit.Op("denoise"));
            Assert.Throws<InvalidOperationException>(() => AudioEdit.Render(d2)); // ohne Profil: freundliche Meldung
        }
        finally { Directory.Delete(dir, true); }
    }
}
