using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik S5/S6: Tonhöhe (YIN), Treffer gegen die Zielmelodie, Mischen.</summary>
public class SingAlongTests
{
    static float[] Tone(double hz, int sr = 16000, int n = 1024, double noise = 0)
    {
        var r = new Random(3); var x = new float[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)sr; double v = 0;
            for (var k = 1; k <= 6; k++) v += Math.Sin(2 * Math.PI * hz * k * t) / k; // stimmähnlich mit Obertönen
            x[i] = (float)(0.3 * v + noise * (r.NextDouble() - 0.5));
        }
        return x;
    }

    [Theory(DisplayName = "YIN: Grundton von Männer- bis Sopranstimme auf ±0,2 Halbtöne, auch mit Obertönen und etwas Rauschen")]
    [InlineData(98.0)] [InlineData(146.8)] [InlineData(220.0)] [InlineData(440.0)] [InlineData(784.0)]
    public void Yin_pitch(double hz)
    {
        var f = SingAlong.Yin(Tone(hz, noise: 0.05), 16000);
        Assert.InRange(SingAlong.Midi(f) - SingAlong.Midi(hz), -0.2, 0.2);
    }

    [Fact(DisplayName = "YIN: Stille und Rauschen ergeben keinen Ton")]
    public void Yin_silence()
    {
        Assert.True(double.IsNaN(SingAlong.Yin(new float[1024], 16000)));
        var r = new Random(1); var n = new float[1024]; for (var i = 0; i < n.Length; i++) n[i] = (float)(r.NextDouble() - 0.5);
        Assert.True(double.IsNaN(SingAlong.Yin(n, 16000)));
    }

    [Fact(DisplayName = "Mitsingen: Treffer bis ½ Halbton, Oktave tiefer zählt, zu tief/zu hoch erkannt, Pausen der Melodie zählen nicht; Verzögerung bis 0,25 s ausgeglichen")]
    public void Take_score()
    {
        var mel = SingAlong.Melody.Parse("""{"hop": 0.1, "midi": [60, 60, 62, null, null, null, null, null, null, 64, 64]}""")!;
        var take = new SingAlong.Take(mel);
        Assert.Equal(SingAlong.Verdict.Treffer, take.Add(0.0, 261.6));           // C4
        Assert.Equal(SingAlong.Verdict.Treffer, take.Add(0.1, 130.8));           // C3 – Oktave tiefer
        Assert.Equal(SingAlong.Verdict.Hoeher, take.Add(0.2, 246.9));            // H3 statt C4/D4 → höher singen
        Assert.Equal(SingAlong.Verdict.Still, take.Add(0.6, 300));               // hier wird nicht gesungen
        Assert.Equal(SingAlong.Verdict.Tiefer, take.Add(1.0, 370.0));            // Fis4 statt E4 → tiefer
        Assert.Equal(SingAlong.Verdict.Treffer, take.Add(0.72, 329.6));          // E4 0,18 s zu früh – noch im Fenster
        Assert.Equal(SingAlong.Verdict.Still, take.Add(1.05, double.NaN));
        Assert.Equal((5, 3), (take.Sung, take.Hits));
        Assert.Equal(60, take.HitPercent, 3);
        Assert.Equal("C4", SingAlong.NoteName(60)); Assert.Equal("Fis4", SingAlong.NoteName(66.2));
        Assert.Null(SingAlong.Melody.Parse("kaputt"));
    }

    [Fact(DisplayName = "Mischen: Stimme gesäubert, Versatz vorn abgeschnitten bzw. verzögert, Hall wählbar, Lautstärke, Begleitung bestimmt die Länge")]
    public void Mix_args()
    {
        var a = string.Join(" ", SingAlong.MixArgs("beg.wav", "take.wav", "out.wav", 120, -2.5, "raum"));
        Assert.Contains("highpass=f=90,acompressor", a); Assert.Contains("atrim=start=0.120", a); Assert.Contains("aecho=0.8:0.7", a);
        Assert.Contains("volume=-2.5dB", a); Assert.Contains("amix=inputs=2:duration=first:normalize=0", a);
        var b = string.Join(" ", SingAlong.MixArgs("beg.wav", "take.wav", "out.wav", -80, 0, "aus"));
        Assert.Contains("adelay=80:all=1", b); Assert.DoesNotContain("aecho", b); Assert.DoesNotContain("atrim", b);
    }
}
