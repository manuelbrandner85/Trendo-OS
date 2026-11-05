using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Audio Profi 25 (05.11.2026): Mehrspur wie Audition, spektrales Retuschieren wie iZotope RX (AU-12).</summary>
public class AudioMultiTests
{
    static float[] Sine(int rate, double hz, double sec, float amp = 0.3f) => [.. Enumerable.Range(0, (int)(rate * sec)).Select(i => amp * (float)Math.Sin(2 * Math.PI * hz * i / rate))];
    static double Rms(float[] x, int a, int b) { double s = 0; for (var i = a; i < b; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, b - a)); }
    /// <summary>Stärke einer Frequenz (Goertzel), Amplitude 0–1.</summary>
    static double Tone(float[] x, int rate, double hz, int a, int b)
    {
        double w = 2 * Math.PI * hz / rate, c = 2 * Math.Cos(w), s1 = 0, s2 = 0;
        for (var i = a; i < b; i++) { var s0 = x[i] + c * s1 - s2; s2 = s1; s1 = s0; }
        return 2 * Math.Sqrt(Math.Max(0, s1 * s1 + s2 * s2 - c * s1 * s2)) / (b - a);
    }

    [Fact(DisplayName = "Mehrspur: zwei Spuren mit Startzeit gemischt, Panorama ganz links, Stumm und Solo, Lautstärke in dB; Rendern nimmt die Mischung als Quelle")]
    public void Multitrack()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-multi-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string F(string n, float[] d) { var p = Path.Combine(dir, n); AudioFile.WriteWav(new AudioBuf(44100, [d, (float[])d.Clone()]), p); return p; }
            var voice = F("stimme.wav", Sine(44100, 300, 2)); var music = F("musik.wav", Sine(44100, 1000, 1));
            var doc = AudioMulti.New(); Assert.True(AudioMulti.IsMulti(doc));
            var t1 = AudioMulti.AddTrack(doc, voice); var t2 = AudioMulti.AddTrack(doc, music, 1.5);
            var mix = AudioEdit.Render(doc);
            Assert.Equal(2.5, mix.Seconds, 2);
            var L = mix.Data[0];
            Assert.InRange(Tone(L, 44100, 300, 0, 44100), 0.27, 0.33); Assert.True(Tone(L, 44100, 1000, 0, 44100) < 0.01); // erste Sekunde nur Stimme
            Assert.InRange(Tone(L, 44100, 1000, 70000, 88000), 0.27, 0.33); // ab 1,5 s Musik dazu
            // Panorama ganz links: rechts still
            AudioMulti.SetTrack(doc, t1.Id, pan: -1);
            mix = AudioEdit.Render(doc);
            Assert.True(Rms(mix.Data[1], 0, 44100) < 0.001); Assert.True(Rms(mix.Data[0], 0, 44100) > 0.25);
            // −6 dB halbiert die Spannung
            AudioMulti.SetTrack(doc, t1.Id, pan: 0, gainDb: -6);
            Assert.InRange(Tone(AudioEdit.Render(doc).Data[0], 44100, 300, 0, 44100), 0.13, 0.17);
            // Stumm und Solo
            AudioMulti.SetTrack(doc, t1.Id, mute: true); Assert.True(Tone(AudioEdit.Render(doc).Data[0], 44100, 300, 0, 44100) < 0.001);
            AudioMulti.SetTrack(doc, t1.Id, mute: false); AudioMulti.SetTrack(doc, t2.Id, solo: true);
            mix = AudioEdit.Render(doc); Assert.True(Tone(mix.Data[0], 44100, 300, 0, 44100) < 0.001); Assert.True(Tone(mix.Data[0], 44100, 1000, 70000, 88000) > 0.2);
            // Schritte des Editors wirken auf die Summe
            AudioMulti.SetTrack(doc, t2.Id, solo: false);
            AudioEdit.Add(doc, AudioEdit.Op("cut", 0, 1));
            Assert.Equal(1.5, AudioEdit.Render(doc).Seconds, 2);
            AudioMulti.RemoveTrack(doc, t2.Id); Assert.Single(AudioMulti.Tracks(doc));
            Assert.Throws<FileNotFoundException>(() => AudioMulti.AddTrack(doc, Path.Combine(dir, "fehlt.wav")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Spektral absenken: Pfeifton 3 kHz in der Zeit-Frequenz-Auswahl wird um 30 dB leiser, die Stimme (300 Hz) bleibt, außerhalb der Zeit bleibt alles gleich")]
    public void Spectral()
    {
        const int rate = 44100;
        var a = Sine(rate, 300, 3); var whistle = Sine(rate, 3000, 3, 0.2f);
        var x = a.Zip(whistle, (p, q) => p + q).ToArray(); var orig = (float[])x.Clone();
        AudioDsp.SpectralGain(x, rate, rate, 2 * rate, 2500, 3500, -30);
        int m0 = (int)(rate * 1.2), m1 = (int)(rate * 1.8);
        Assert.True(Tone(x, rate, 3000, m0, m1) < 0.2 * 0.05, $"Pfeifton noch da: {Tone(x, rate, 3000, m0, m1)}");
        Assert.InRange(Tone(x, rate, 300, m0, m1), 0.28, 0.32);
        Assert.Equal(orig[..rate], x[..rate]); Assert.Equal(orig[(2 * rate)..], x[(2 * rate)..]);
        // als Bearbeitungsschritt
        Assert.Throws<InvalidOperationException>(() => AudioEdit.SpectralOp(1, 1, 100, 200, -30));
        Assert.Throws<InvalidOperationException>(() => AudioEdit.SpectralOp(1, 2, 100, 105, -30));
        var o = AudioEdit.SpectralOp(1, 2, 3500, 2500, -90);
        Assert.Equal((2500.0, 3500.0, -80.0), ((double)o["fl"]!, (double)o["fh"]!, (double)o["v"]!));
        Assert.InRange(AudioDsp.RowToHz(159, 160, rate), 39, 41); Assert.InRange(AudioDsp.RowToHz(0, 160, rate), 15999, 16001);
    }
}
