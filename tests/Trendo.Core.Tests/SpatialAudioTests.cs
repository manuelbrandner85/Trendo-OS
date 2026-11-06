using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-16 (06.11.2026): Raumklang 5.1, Kanalplan, Kopfhörer-Vorschau.</summary>
public class SpatialAudioTests
{
    static string Tone(string dir, string name, double hz, double seconds = 2)
    {
        const int rate = 48000; var n = (int)(seconds * rate);
        var d = Enumerable.Range(0, 2).Select(_ => Enumerable.Range(0, n).Select(i => (float)(0.4 * Math.Sin(2 * Math.PI * hz * i / rate))).ToArray()).ToArray();
        var f = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, d), f, 24); return f;
    }

    static double Rms(float[] x) { double s = 0; foreach (var v in x) s += v * (double)v; return Math.Sqrt(s / Math.Max(1, x.Length)); }

    [Fact(DisplayName = "S4-16: Panorama im Kreis – 30° nur rechts, 0° Mitte, −110° links hinten, 70° zwischen rechts und rechts hinten; gleiche Leistung an jedem Winkel")]
    public void Gains()
    {
        Assert.Equal(1, SpatialAudio.Gains(30)[1], 6); Assert.Equal(1, SpatialAudio.Gains(0)[2], 6); Assert.Equal(1, SpatialAudio.Gains(-110)[4], 6);
        var g70 = SpatialAudio.Gains(70); Assert.Equal(g70[1], g70[5], 6); Assert.Equal(0.7071, g70[1], 3);
        var g180 = SpatialAudio.Gains(180); Assert.Equal(g180[4], g180[5], 6);
        for (var a = -180; a <= 180; a += 7) Assert.Equal(1, SpatialAudio.Gains(a).Sum(x => x * x), 6);
        Assert.Equal(0, SpatialAudio.Gains(45)[3]); // LFE nie im Panorama
    }

    [Fact(DisplayName = "S4-16-P/N: zwei Spuren platziert → richtige Kanäle, Tiefton nur Tiefen, WAV mit Kanalmaske 5.1(side) und Kanalplan; Stereo wird nicht still als 5.1 geschrieben; Kopfhörer-Vorschau links lauter und rechts später")]
    public void Mix_write_binaural()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-51-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var doc = AudioMulti.New();
            var hall = AudioMulti.AddTrack(doc, Tone(dir, "hall.wav", 1000), 0, "Hall hinten links");
            var voice = AudioMulti.AddTrack(doc, Tone(dir, "stimme.wav", 300), 0, "Stimme rechts");
            SpatialAudio.SetAzimuth(doc, hall.Id, -110); SpatialAudio.SetAzimuth(doc, voice.Id, 30);
            var a = SpatialAudio.Mix51(doc);
            Assert.Equal(6, a.Channels);
            var rms = a.Data.Select(Rms).ToArray();
            Assert.True(rms[4] > 0.1 && rms[1] > 0.1, string.Join(" ", rms));
            Assert.True(rms[0] < 1e-4 && rms[2] < 1e-4 && rms[5] < 1e-4, string.Join(" ", rms)); // L, C, Rs still
            Assert.True(rms[3] < rms[1] * 0.5, "LFE zu laut"); // nur Tiefen
            var f = Path.Combine(dir, "mix-51.wav"); SpatialAudio.WriteWav51(a, f);
            var bytes = File.ReadAllBytes(f);
            Assert.Equal(SpatialAudio.Mask51Side, SpatialAudio.WavChannelMask(bytes));
            var back = AudioFile.ReadWav(bytes); Assert.Equal(6, back.Channels); Assert.Equal(24, AudioQa.WavBits(bytes));
            Assert.Contains("5 Ls | Links Raum | -110°", SpatialAudio.ChannelPlan(a));
            Assert.Contains("1 L | Links vorn | -30° | stumm", SpatialAudio.ChannelPlan(a));
            Assert.Contains("Kein 5.1", Assert.Throws<ArgumentException>(() => SpatialAudio.WriteWav51(AudioFile.Read(Tone(dir, "stereo.wav", 440)), Path.Combine(dir, "falsch.wav"))).Message);
            Assert.False(File.Exists(Path.Combine(dir, "falsch.wav")));
            // Kopfhörer: nur links hinten
            var onlyLs = new AudioBuf(48000, [.. Enumerable.Range(0, 6).Select(c => c == 4 ? a.Data[4] : new float[a.Length])]);
            var bin = SpatialAudio.Binaural(onlyLs);
            Assert.True(Rms(bin.Data[0]) > 1.3 * Rms(bin.Data[1]), $"links {Rms(bin.Data[0])}, rechts {Rms(bin.Data[1])}");
            int First(float[] x) { for (var i = 0; i < x.Length; i++) if (Math.Abs(x[i]) > 0.01) return i; return -1; }
            Assert.InRange(First(bin.Data[1]) - First(bin.Data[0]), 15, 40); // 0,3–0,8 ms später am rechten Ohr
            if (Cli.Find("ffprobe") is { } fp)
            {
                var info = Cli.Run(fp, ["-v", "error", "-show_entries", "stream=codec_name,channels,channel_layout,sample_rate:format=duration", "-of", "compact=p=0:nk=1", f], dir, 30000);
                Assert.True(info.Ok, info.Err); Assert.Contains("pcm_s24le|48000|6|5.1(side)", info.Out);
                if (Environment.GetEnvironmentVariable("TRENDO_51_BELEG") is { Length: > 0 } keep)
                {
                    Directory.CreateDirectory(keep); File.Copy(f, Path.Combine(keep, "mix-51.wav"), true);
                    var hp = Path.Combine(keep, "kopfhoerer-vorschau.wav"); AudioFile.WriteWav(SpatialAudio.Binaural(a), hp, 24);
                    File.WriteAllText(Path.Combine(keep, "kanalplan.md"), $"ffprobe: {info.Out.Trim()}\n\n" + SpatialAudio.ChannelPlan(a) + $"\nKopfhörer-Vorschau nur „Links Raum“: links {20 * Math.Log10(Rms(bin.Data[0])):0.0} dBFS, rechts {20 * Math.Log10(Rms(bin.Data[1])):0.0} dBFS, rechts {First(bin.Data[1]) - First(bin.Data[0])} Abtastwerte später ({(First(bin.Data[1]) - First(bin.Data[0])) / 48.0:0.00} ms)\n");
                }
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
