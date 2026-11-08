using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-06: Retro-Soundeffekte nach sfxr (eigener Code). Geprüft wird am erzeugten Ton selbst:
/// Frequenz über Nulldurchgänge, Tonhöhenverlauf, Hüllkurvenlänge, Pegel.</summary>
public class SfxrTests
{
    static float[] Mono(Sfxr.Params p) => Sfxr.Render(p).Data[0];
    /// <summary>Frequenz aus Nulldurchgängen (steigend) in einem Ausschnitt.</summary>
    static double Hz(float[] s, int from, int to)
    {
        var n = 0; for (var i = Math.Max(1, from); i < to; i++) if (s[i - 1] < 0 && s[i] >= 0) n++;
        return n * (double)Sfxr.Rate / (to - from);
    }

    [Fact(DisplayName = "sfxr +1: alle 7 Vorlagen ergeben hörbare Effekte – 0,02 bis 5 s, Spitze −3 dBFS, keine ungültigen Werte")]
    public void All_presets()
    {
        foreach (var (id, _, ev) in Sfxr.Presets)
            for (var seed = 1; seed <= 5; seed++)
            {
                var a = Sfxr.Render(Sfxr.Preset(id, seed));
                Assert.InRange(a.Seconds, 0.02, 5.0);
                var s = a.Data[0]; Assert.All(s, v => Assert.True(float.IsFinite(v)));
                Assert.InRange(s.Max(v => Math.Abs(v)), 0.70f, 0.71f);
                Assert.StartsWith("Spiel.", ev);
            }
    }

    [Fact(DisplayName = "sfxr +2: gleicher Startwert → gleicher Effekt (Byte für Byte); anderer Startwert → anderer Effekt")]
    public void Deterministic()
    {
        Assert.Equal(Mono(Sfxr.Preset("laser", 7)), Mono(Sfxr.Preset("laser", 7)));
        Assert.NotEqual(Mono(Sfxr.Preset("laser", 7)), Mono(Sfxr.Preset("laser", 8)));
    }

    [Fact(DisplayName = "sfxr +3: Sinus mit Grundton 0,3 klingt mit ≈ 321 Hz (8 × 44 100 / 1098,9) – gemessen ±3 %")]
    public void Sine_frequency()
    {
        var s = Mono(new Sfxr.Params { WaveType = Sfxr.Wave.Sinus });
        Assert.InRange(Hz(s, 2000, 20000), 311, 331);
    }

    [Fact(DisplayName = "sfxr +4: Hüllkurve – ohne Anschlag, Halten 0,3 und Abklingen 0,4 dauert der Ton 25 000 Samples (≈ 0,567 s)")]
    public void Envelope_length()
    {
        var s = Mono(new Sfxr.Params());
        Assert.InRange(s.Length, 24990, 25010);
        Assert.True(Math.Abs(s[100]) > 0.1f); // sofort hörbar
    }

    [Fact(DisplayName = "sfxr +5: Sprung steigt in der Tonhöhe, Laser fällt (gemessen am Anfang und Ende)")]
    public void Pitch_direction()
    {
        for (var seed = 1; seed <= 4; seed++)
        {
            var j = Mono(Sfxr.Preset("sprung", seed) with { HpfFreq = 0, LpfFreq = 1 }); var q = j.Length / 5;
            Assert.True(Hz(j, j.Length - 2 * q, j.Length - q) > Hz(j, 0, q) * 1.2, $"Sprung {seed}");
        }
        var l = Mono(new Sfxr.Params { BaseFreq = 0.7, FreqRamp = -0.3, Sustain = 0.3, Decay = 0.2 }); var k = l.Length / 5;
        Assert.True(Hz(l, l.Length - 2 * k, l.Length - k) < Hz(l, 0, k) * 0.8);
    }

    [Fact(DisplayName = "sfxr +6: Arpeggio – Münze springt nach der Arpeggio-Zeit hörbar nach oben")]
    public void Arpeggio()
    {
        var s = Mono(new Sfxr.Params { WaveType = Sfxr.Wave.Sinus, BaseFreq = 0.4, Sustain = 0.4, Decay = 0.2, ArpSpeed = 0.6, ArpMod = 0.4 }); // Sprung nach (0,4² · 20000 + 32) = 3232 Samples
        Assert.True(Hz(s, 5000, 12000) > Hz(s, 200, 3000) * 1.1);
    }

    [Fact(DisplayName = "sfxr +7: Effekt als WAV speichern und wieder lesen – gleiche Länge, Lizenz „selbst erzeugt“")]
    public void Wav_roundtrip()
    {
        var f = Path.Combine(Path.GetTempPath(), "trendo-sfxr-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            var a = Sfxr.Render(Sfxr.Preset("muenze", 3)); AudioFile.WriteWav(a, f, 24);
            var b = AudioFile.Read(f); Assert.Equal(a.Data[0].Length, b.Data[0].Length); Assert.Equal(Sfxr.Rate, b.Rate);
            Assert.Contains("selbst erzeugt", Sfxr.License);
        }
        finally { File.Delete(f); }
    }

    [Fact(DisplayName = "sfxr −1: unbekannte Vorlage abgelehnt; Frequenzgrenze beendet einen fallenden Ton vorzeitig")]
    public void Limits()
    {
        Assert.Throws<ArgumentException>(() => Sfxr.Preset("gibtsnicht", 1));
        var free = Mono(new Sfxr.Params { BaseFreq = 0.6, FreqRamp = -0.5, Sustain = 0.5, Decay = 0.5 });
        var stop = Mono(new Sfxr.Params { BaseFreq = 0.6, FreqRamp = -0.5, Sustain = 0.5, Decay = 0.5, FreqLimit = 0.4 });
        Assert.True(stop.Length < free.Length / 2);
    }

    [Fact(DisplayName = "sfxr −2: Länge wird gekappt (Wiederholung + langes Halten höchstens 5 s); stilles Ergebnis bleibt still statt Division durch 0")]
    public void Cap_and_silence()
    {
        var a = Sfxr.Render(new Sfxr.Params { Sustain = 1, Decay = 1, Attack = 1 }); Assert.InRange(a.Seconds, 4.99, 5.0);
        var z = Sfxr.Render(new Sfxr.Params { Volume = 0 }); Assert.All(z.Data[0], v => Assert.Equal(0f, v));
    }
}
