using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Videoschnitt X12 (04.11.2026): Takt erkennen und Schnitte auf den Takt legen.</summary>
public class BeatDetectTests
{
    static AudioBuf Clicks(double bpm, double phase, double seconds, int rate = 22050)
    {
        var a = AudioBuf.Silence(seconds, rate, 1); var step = 60 / bpm; var rnd = new Random(7);
        for (var i = 0; i < a.Length; i++) a.Data[0][i] = (float)(rnd.NextDouble() - 0.5) * 0.01f; // leises Rauschen
        for (var t = phase; t < seconds; t += step)
        {
            var s = (int)(t * rate);
            for (var k = 0; k < rate / 50 && s + k < a.Length; k++) a.Data[0][s + k] += (float)(Math.Sin(2 * Math.PI * 1000 * k / rate) * Math.Exp(-k / (rate * 0.005)) * 0.9);
        }
        return a;
    }

    [Theory(DisplayName = "Takt: Klickspur mit 90, 120 und 150 BPM wird erkannt, Schläge liegen auf den Klicks")]
    [InlineData(90.0)] [InlineData(120.0)] [InlineData(150.0)]
    public void Finds_tempo(double bpm)
    {
        var r = BeatDetect.Find(Clicks(bpm, 0.25, 20));
        Assert.InRange(r.Bpm, bpm * 0.97, bpm * 1.03);
        var step = 60 / bpm;
        var off = r.Beats.Skip(2).Take(20).Select(b => Math.Abs(((b - 0.25) / step) - Math.Round((b - 0.25) / step)) * step).Max();
        Assert.True(off < 0.04, $"Schläge {off:0.000} s neben den Klicks");
        Assert.Empty(BeatDetect.Find(AudioBuf.Silence(0.2)).Beats);
    }

    [Fact(DisplayName = "Schnitte auf den Takt: Stückenden landen auf Schlägen (Musik-Start eingerechnet), jeder 2. Schlag, Quelle wird nicht überschritten")]
    public void Snap_cuts()
    {
        var p = VideoEdit.NewProject();
        VideoEdit.Add(p, new EditClip("a", "a.mp4", 0, 1.3, 10)); VideoEdit.Add(p, new EditClip("b", "b.mp4", 0, 1.7, 10)); VideoEdit.Add(p, new EditClip("c", "c.mp4", 0, 1.2, 1.2));
        VideoEdit.Add(p, new EditClip("m", "m.mp3", 0, 10, 60), "music"); VideoEdit.SetMusic(p, 0, 0.1, 1);
        var beats = Enumerable.Range(0, 20).Select(i => i * 0.5).ToList(); // Musikdatei: alle 0,5 s; auf der Zeitleiste 0,1 s später
        var n = BeatDetect.SnapCuts(p, beats);
        Assert.Equal(2, n);
        var c = VideoEdit.Clips(p); var starts = VideoEdit.Starts(c);
        Assert.Equal(1.1, starts[1], 2); Assert.Equal(2.6, starts[2], 2); // 2,8 → nächster Schlag 2,6
        // jeder 2. Schlag: Raster 1 s
        var q = VideoEdit.NewProject();
        VideoEdit.Add(q, new EditClip("a", "a.mp4", 0, 1.3, 10)); VideoEdit.Add(q, new EditClip("b", "b.mp4", 0, 0.5, 0.6));
        VideoEdit.Add(q, new EditClip("m", "m.mp3", 0, 10, 60), "music");
        BeatDetect.SnapCuts(q, beats, every: 2);
        Assert.Equal(1.0, VideoEdit.Clips(q)[0].Length, 2);
        Assert.Equal(0, BeatDetect.SnapCuts(VideoEdit.NewProject(), beats));
    }
}
