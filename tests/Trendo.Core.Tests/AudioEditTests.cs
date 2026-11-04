using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Selbst nachgebaut statt Audacity (04.11.2026): WAV lesen/schreiben, Schnitt, Effekte, Wellenform.</summary>
public class AudioEditTests
{
    static string Tone(string dir, string name, double seconds, double hz = 440, float amp = 0.5f, int rate = 8000, int ch = 2)
    {
        var n = (int)(seconds * rate);
        var d = Enumerable.Range(0, ch).Select(_ => Enumerable.Range(0, n).Select(i => amp * (float)Math.Sin(2 * Math.PI * hz * i / rate)).ToArray()).ToArray();
        var p = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, d), p); return p;
    }

    static string Dir() { var d = Path.Combine(Path.GetTempPath(), "trendo-audiotest-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "WAV: 16 und 24 Bit schreiben und wieder lesen – gleiche Länge, Rate, Kanäle, Werte")]
    public void Wav_roundtrip()
    {
        var dir = Dir();
        try
        {
            var p = Tone(dir, "a.wav", 1.5);
            var a = AudioFile.Read(p);
            Assert.Equal(8000, a.Rate); Assert.Equal(2, a.Channels); Assert.Equal(12000, a.Length);
            Assert.InRange(AudioEdit.PeakDb(a), -6.1, -5.9);
            var p24 = Path.Combine(dir, "b.wav"); AudioFile.WriteWav(a, p24, 24);
            var b = AudioFile.Read(p24);
            Assert.Equal(a.Length, b.Length); Assert.True(Math.Abs(a.Data[1][777] - b.Data[1][777]) < 1e-4);
            Assert.Throws<InvalidOperationException>(() => AudioFile.ReadWav("RIFFxxxxJUNK"u8.ToArray()));
            File.WriteAllText(Path.Combine(dir, "c.flac"), "x");
            var ex = Assert.Throws<InvalidOperationException>(() => AudioFile.Read(Path.Combine(dir, "c.flac"))); Assert.Contains("WAV oder MP3", ex.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Schnitt: Löschen, Zuschneiden, Duplizieren, Stille, Einfügen – Längen stimmen, Original bleibt unverändert")]
    public void Cuts()
    {
        var dir = Dir();
        try
        {
            var src = Tone(dir, "s.wav", 4); var before = File.ReadAllBytes(src);
            var doc = AudioEdit.New(src);
            AudioEdit.Add(doc, AudioEdit.Op("cut", 1, 2));
            Assert.Equal(3.0, AudioEdit.Render(doc).Seconds, 3);
            AudioEdit.Add(doc, AudioEdit.Op("duplicate", 0, 0.5));
            Assert.Equal(3.5, AudioEdit.Render(doc).Seconds, 3);
            AudioEdit.Add(doc, AudioEdit.Op("insert-silence", 0, 0, 1));
            var r = AudioEdit.Render(doc); Assert.Equal(4.5, r.Seconds, 3);
            Assert.Equal(double.NegativeInfinity, AudioEdit.PeakDb(r, 0, 1));
            AudioEdit.Add(doc, AudioEdit.Op("insert", 4.5, 0, 0, Tone(dir, "m.wav", 1, 220, 0.3f, 16000, 1))); // andere Rate, mono
            r = AudioEdit.Render(doc); Assert.Equal(5.5, r.Seconds, 2); Assert.Equal(2, r.Channels);
            AudioEdit.Add(doc, AudioEdit.Op("keep", 1, 3));
            Assert.Equal(2.0, AudioEdit.Render(doc).Seconds, 3);
            AudioEdit.Add(doc, AudioEdit.Op("silence", 0, 1));
            r = AudioEdit.Render(doc); Assert.Equal(double.NegativeInfinity, AudioEdit.PeakDb(r, 0.01, 0.99)); Assert.True(AudioEdit.PeakDb(r, 1.1, 1.9) > -10);
            Assert.Equal(before, File.ReadAllBytes(src));
            Assert.Throws<ArgumentException>(() => AudioEdit.Op("zaubern"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Effekte: Lautstärke, Normalisieren, Blenden, Umkehren, Rauschtor, Filter, Mono, Tempo")]
    public void Effects()
    {
        var dir = Dir();
        try
        {
            var src = Tone(dir, "s.wav", 2, 440, 0.25f);
            var d1 = AudioEdit.New(src); AudioEdit.Add(d1, AudioEdit.Op("gain", 0, 0, 6));
            Assert.InRange(AudioEdit.PeakDb(AudioEdit.Render(d1)), -6.2, -5.8);
            var d2 = AudioEdit.New(src); AudioEdit.Add(d2, AudioEdit.Op("normalize", 0, 0, -1));
            Assert.InRange(AudioEdit.PeakDb(AudioEdit.Render(d2)), -1.05, -0.95);
            var d3 = AudioEdit.New(src); AudioEdit.Add(d3, AudioEdit.Op("fade-in", 0, 1)); AudioEdit.Add(d3, AudioEdit.Op("fade-out", 1, 2));
            var f = AudioEdit.Render(d3); Assert.True(AudioEdit.PeakDb(f, 0, 0.05) < -25); Assert.True(AudioEdit.PeakDb(f, 0.95, 1.05) > -13); Assert.True(AudioEdit.PeakDb(f, 1.96, 2) < -25);
            var d4 = AudioEdit.New(src); AudioEdit.Add(d4, AudioEdit.Op("reverse"));
            var orig = AudioFile.Read(src); var rev = AudioEdit.Render(d4); Assert.Equal(orig.Data[0][0], rev.Data[0][^1]);
            // Rauschtor: leises Rauschen weg, lauter Ton bleibt
            var quiet = Tone(dir, "q.wav", 1, 3000, 0.002f); var loud = Tone(dir, "l.wav", 1, 440, 0.5f);
            var d5 = AudioEdit.New(quiet); AudioEdit.Add(d5, AudioEdit.Op("insert", 1, 0, 0, loud)); AudioEdit.Add(d5, AudioEdit.Op("gate", 0, 0, -45));
            var g = AudioEdit.Render(d5); Assert.True(AudioEdit.PeakDb(g, 0.3, 0.9) < -70); Assert.True(AudioEdit.PeakDb(g, 1.3, 1.9) > -7);
            // Filter: 100-Hz-Brummen wird durch Tiefen-weg (300 Hz) deutlich leiser; 3-kHz-Ton durch Höhen-weg (500 Hz)
            var hum = Tone(dir, "h.wav", 1, 100, 0.5f); var d6 = AudioEdit.New(hum); AudioEdit.Add(d6, AudioEdit.Op("highpass", 0, 0, 300));
            Assert.True(AudioEdit.PeakDb(AudioEdit.Render(d6), 0.2, 1) < -22);
            var hiss = Tone(dir, "z.wav", 1, 3000, 0.5f); var d7 = AudioEdit.New(hiss); AudioEdit.Add(d7, AudioEdit.Op("lowpass", 0, 0, 500));
            Assert.True(AudioEdit.PeakDb(AudioEdit.Render(d7), 0.2, 1) < -35);
            var d8 = AudioEdit.New(src); AudioEdit.Add(d8, AudioEdit.Op("mono")); AudioEdit.Add(d8, AudioEdit.Op("speed", 0, 0, 2));
            var m = AudioEdit.Render(d8); Assert.Equal(1.0, m.Seconds, 2); Assert.Equal(2, m.Channels);
            Assert.Equal(m.Data[0][100], m.Data[1][100]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Wellenform: Spaltenwerte folgen der Lautstärke; leere Spur geht; Zeitanzeige")]
    public void Peaks()
    {
        var a = AudioBuf.Silence(2, 8000, 1);
        for (var i = 8000; i < 16000; i++) a.Data[0][i] = i % 2 == 0 ? 0.8f : -0.8f;
        var p = AudioEdit.Peaks(a, 100);
        Assert.Equal(100, p.Length); Assert.Equal(0f, p[10].Max); Assert.Equal(0.8f, p[80].Max); Assert.Equal(-0.8f, p[80].Min);
        Assert.Equal(5, AudioEdit.Peaks(AudioBuf.Silence(0), 5).Length);
        Assert.Equal("1:05,50", AudioEdit.Time(65.5));
        var doc = AudioEdit.New(null, 3); Assert.Equal(3, AudioEdit.Render(doc).Seconds, 3);
    }
}
