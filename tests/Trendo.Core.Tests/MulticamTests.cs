using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Multicam – Ton-Sync und Schnitt.</summary>
public class MulticamTests
{
    /// <summary>Gemeinsamer Szenenton: unregelmäßige Klopfer (wie Sprache/Klappe), damit es genau eine passende Verschiebung gibt.</summary>
    static float[] SceneAudio(int rate, double seconds, int seed)
    {
        var rng = new Random(seed); var x = new float[(int)(seconds * rate)]; var t = 0.3;
        while (t < seconds - 0.3) { var at = (int)(t * rate); for (var i = 0; i < rate / 20 && at + i < x.Length; i++) x[at + i] = (float)(0.6 * Math.Sin(2 * Math.PI * 300 * i / rate) * Math.Exp(-i / (rate * 0.015))); t += 0.15 + rng.NextDouble() * 0.6; }
        return x;
    }

    [Fact(DisplayName = "S3-02 Multicam: Versatz aus dem Ton auf 1 ms genau, Schnitt zwischen Kameras mit richtigen Einstiegspunkten; Kamera ohne Aufnahme im Abschnitt und falsche Wechsel werden abgelehnt")]
    public void Offset_and_cut()
    {
        const int rate = 8000; var scene = SceneAudio(rate, 20, 3);
        var a = scene[..(int)(18 * rate)]; var b = scene[(int)(1.5 * rate)..]; // Kamera B startete 1,5 s später
        var r = Multicam.Offset(Multicam.Envelope(new AudioBuf(rate, [a])), Multicam.Envelope(new AudioBuf(rate, [b])));
        Assert.Equal(1.5, r.Offset, 3); Assert.True(r.Confidence > 0.8, $"{r.Confidence}");
        var clips = Multicam.Cut([("a.mp4", 0, 18), ("b.mp4", 1.5, 18.5)], [new(0, 0), new(4, 1), new(9, 0)], 12);
        Assert.Equal([("a.mp4", 0.0, 4.0), ("b.mp4", 2.5, 7.5), ("a.mp4", 9.0, 12.0)], clips.Select(c => (c.File, c.In, c.Out)));
        Assert.Throws<InvalidOperationException>(() => Multicam.Cut([("a.mp4", 0, 18), ("b.mp4", 1.5, 18.5)], [new(0, 1)], 4)); // B läuft bei 0 s noch nicht
        Assert.Throws<ArgumentException>(() => Multicam.Cut([("a.mp4", 0, 18), ("b.mp4", 1.5, 18.5)], [new(2, 0)], 4));
        Assert.Throws<ArgumentException>(() => Multicam.Cut([("a.mp4", 0, 18)], [new(0, 0)], 4));
        Assert.Equal(0, Multicam.Offset(new float[100], new float[100]).Confidence); // Stille: keine Aussage
    }

    [Fact(DisplayName = "S3-02 Multicam echt mit FFmpeg: zwei Kameradateien mit 2,25 s Versatz werden über den Ton gefunden; Kamera ohne gemeinsamen Ton wird abgewiesen")]
    public void Real_sync()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mc-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            const int rate = 48000; var scene = SceneAudio(rate, 16, 7);
            string Cam(string name, float[] audio, string color)
            {
                var wav = Path.Combine(dir, name + ".wav"); AudioFile.WriteWav(new AudioBuf(rate, [audio, (float[])audio.Clone()]), wav);
                var mp4 = Path.Combine(dir, name + ".mp4");
                var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c={color}:s=320x180:r=25", "-i", wav, "-shortest", "-pix_fmt", "yuv420p", "-c:a", "aac", mp4], dir, 60000);
                Assert.True(File.Exists(mp4), r.Err); return mp4;
            }
            var a = Cam("kamera-a", scene[..(int)(14 * rate)], "red");
            var b = Cam("kamera-b", scene[(int)(2.25 * rate)..], "blue");
            var c = Cam("fremd", SceneAudio(rate, 10, 99), "green");
            Cli.Result Run(IReadOnlyList<string> args) => Cli.Run(ff, args, dir, 60000);
            var res = Multicam.Sync(Run, [a, b], Path.Combine(dir, "sync"));
            Assert.InRange(res[1].Offset, 2.23, 2.27); // AAC-Vorlauf: wenige ms
            Assert.Contains("passt im Ton nicht", Assert.Throws<InvalidOperationException>(() => Multicam.Sync(Run, [a, c], Path.Combine(dir, "sync"))).Message);
            if (Environment.GetEnvironmentVariable("TRENDO_MC_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); foreach (var f in new[] { a, b, c }) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
                File.WriteAllText(Path.Combine(keep, "messung.txt"), $"Kamera B gefunden: {res[1].Offset:0.000} s später (Soll 2,250), Übereinstimmung {res[1].Confidence:0.00}\n");
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
