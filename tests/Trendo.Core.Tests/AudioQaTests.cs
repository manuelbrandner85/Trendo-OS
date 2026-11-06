using System.Globalization;
using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-20 (06.11.2026): Audio-Prüfung und Freigabe-Sperre.</summary>
public class AudioQaTests
{
    static AudioBuf Sine(double hz, double amp, double seconds, int ch = 2, int rate = 48000, double phase = 0, bool clamp = false)
    {
        var n = (int)(seconds * rate); var d = Enumerable.Range(0, ch).Select(_ => new float[n]).ToArray();
        for (var i = 0; i < n; i++) { var v = amp * Math.Sin(2 * Math.PI * hz * i / rate + phase); if (clamp) v = Math.Clamp(v, -1, 1); foreach (var c in d) c[i] = (float)v; }
        return new AudioBuf(rate, d);
    }

    static readonly AudioQa.Source[] Ok = [new("Eigene Aufnahme", "eigene Rechte")];

    [Fact(DisplayName = "S4-20-P: Messwerte (Peak, True Peak zwischen den Abtastwerten, Übersteuerung, Format) und Bericht mit SHA-256; Freigabe möglich")]
    public void Measure_and_report()
    {
        var m = AudioQa.Analyze(Sine(1000, 0.1, 3), 24);
        Assert.Equal(-20, m.SamplePeakDb, 1); Assert.Equal(0, m.ClipEvents); Assert.Equal(1, m.Correlation, 2);
        // Ton bei fs/4 mit 45° Phase: Abtastwerte nur bei 70,7 %, der echte Gipfel liegt dazwischen
        var isp = AudioQa.Analyze(Sine(12000, 1.0, 1, phase: Math.PI / 4), 24);
        Assert.Equal(-3.01, isp.SamplePeakDb, 1); Assert.InRange(isp.TruePeakDb, -0.4, 0.3);
        var r = AudioQa.Run(Sine(1000, 0.1, 3), 24, AudioQa.TargetOf("streaming"), Ok, 48000, 2);
        Assert.False(r.Blocked, r.Reason);
        var dir = Path.Combine(Path.GetTempPath(), "trendo-aqa-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "mix.wav"); AudioFile.WriteWav(Sine(1000, 0.1, 3), f, 24);
            var rf = AudioQa.Run(f, AudioQa.TargetOf("streaming"), Ok);
            Assert.Equal(24, rf.Measure!.Bits); Assert.Equal(48000, rf.Measure.Rate);
            var rep = AudioQa.Report("Mix", AudioQa.TargetOf("streaming"), rf, [f]);
            Assert.Contains("Freigabe möglich", rep); Assert.Contains(FilmDelivery.Sha256(f), rep); Assert.Contains("| True Peak |", rep);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S4-20-N: Übersteuerung, fehlende Lizenz, KI-Stimme ohne Einwilligung, falsche Kanalzahl und defekte WAV sperren die Freigabe mit Grund")]
    public void Blocks_with_reason()
    {
        var clip = AudioQa.Run(Sine(440, 1.6, 2, clamp: true), 16, AudioQa.TargetOf("streaming"), Ok);
        Assert.True(clip.Blocked); Assert.Contains("Übersteuert", clip.Reason);
        Assert.Contains("Lizenz fehlen", AudioQa.Run(Sine(440, 0.1, 1), 16, AudioQa.Targets[0], null).Reason);
        Assert.Contains("Lizenz unbekannt", AudioQa.Run(Sine(440, 0.1, 1), 16, AudioQa.Targets[0], [new("Sample X", "unbekannt")]).Reason);
        var voice = AudioQa.Run(Sine(440, 0.1, 1), 16, AudioQa.Targets[0], [new("Gesang", "eigene Rechte", VoiceModel: true)]);
        Assert.True(voice.Blocked); Assert.Contains("Einwilligung", voice.Reason);
        Assert.False(AudioQa.Run(Sine(440, 0.1, 1), 16, AudioQa.Targets[0], [new("Gesang", "eigene Rechte", VoiceModel: true, Consent: true)]).Blocked);
        Assert.Contains("1 Kanäle", AudioQa.Run(Sine(440, 0.1, 1, ch: 1), 16, AudioQa.Targets[0], Ok, expectChannels: 2).Reason);
        var dir = Path.Combine(Path.GetTempPath(), "trendo-aqa-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var bad = Path.Combine(dir, "kaputt.wav"); File.WriteAllBytes(bad, "RIFF\0\0\0\0WAVEjunkjunk"u8.ToArray());
            var r = AudioQa.Run(bad, AudioQa.Targets[0], Ok);
            Assert.True(r.Blocked); Assert.Null(r.Measure); Assert.Contains("defekt", r.Reason);
            Assert.Contains("Freigabe gesperrt", AudioQa.Report("Kaputt", AudioQa.Targets[0], r, [bad]));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "S4-20 echt gegen FFmpeg ebur128: Lautheit und True Peak von Trendo stimmen mit FFmpeg überein (±0,5)")]
    public void Real_crosscheck_ffmpeg()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-aqa-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "musik.wav");
            var mk = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "sine=f=220:d=6,volume=0.3", "-f", "lavfi", "-i", "anoisesrc=d=6:a=0.05:c=pink", "-filter_complex", "[0][1]amix=inputs=2:normalize=0,aformat=channel_layouts=stereo,aresample=48000", "-c:a", "pcm_s24le", f], dir, 60000);
            Assert.True(File.Exists(f), mk.Err);
            var r = AudioQa.Run(f, AudioQa.TargetOf("streaming"), Ok, 48000, 2);
            var log = Cli.Run(ff, ["-hide_banner", "-nostats", "-i", f, "-af", "ebur128=peak=true", "-f", "null", "-"], dir, 60000).Err;
            var lufs = double.Parse(Regex.Matches(log, @"I:\s+(-?[0-9.]+) LUFS").Last().Groups[1].Value, CultureInfo.InvariantCulture);
            var tp = double.Parse(Regex.Matches(log, @"Peak:\s+(-?[0-9.]+) dBFS").Last().Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.InRange(r.Measure!.Lufs - lufs, -0.5, 0.5); Assert.InRange(r.Measure.TruePeakDb - tp, -0.5, 0.5);
            Assert.Equal(24, r.Measure.Bits);
            if (Environment.GetEnvironmentVariable("TRENDO_AQA_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); File.Copy(f, Path.Combine(keep, "musik.wav"), true);
                var clipped = Path.Combine(keep, "uebersteuert.wav"); AudioFile.WriteWav(Sine(440, 1.6, 2, clamp: true), clipped, 16);
                var rc = AudioQa.Run(clipped, AudioQa.TargetOf("streaming"), Ok);
                File.WriteAllText(Path.Combine(keep, "Prüfbericht musik.md"), AudioQa.Report("musik", AudioQa.TargetOf("streaming"), r, [f]));
                File.WriteAllText(Path.Combine(keep, "Prüfbericht uebersteuert.md"), AudioQa.Report("uebersteuert", AudioQa.TargetOf("streaming"), rc, [clipped]));
                File.WriteAllText(Path.Combine(keep, "vergleich.txt"), $"Trendo: {r.Measure.Lufs:0.00} LUFS, True Peak {r.Measure.TruePeakDb:0.00} dBTP\nFFmpeg ebur128: {lufs:0.00} LUFS, True Peak {tp:0.00} dBFS\n");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
