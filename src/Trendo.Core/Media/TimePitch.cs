using System.Globalization;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-12 (07.11.2026): Tempo und Tonhöhe ändern (Rubber Band, Formanten erhalten), auch Feinstimmung in Cent für die Intonation.
/// Das Ergebnis wird gemessen (Länge, Tonhöhe, Phase L/R). Starke Änderungen bekommen eine Artefakt-Warnung, extreme werden abgelehnt – nie still begrenzt.</summary>
public static class TimePitch
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.#####", Inv);

    public sealed record Check(string Light, string Text);

    /// <summary>Grün: 0,75–1,33 und ±4 Halbtöne. Gelb (Artefakte wahrscheinlich): bis 0,5–2,0 und ±12. Rot (abgelehnt): darüber.</summary>
    public static Check Assess(double tempo, double semitones)
    {
        if (!double.IsFinite(tempo) || !double.IsFinite(semitones)) return new("rot", "Ungültige Werte.");
        if (tempo is < 0.5 or > 2.0 || Math.Abs(semitones) > 12) return new("rot", $"Zu extrem (Tempo {tempo * 100:0} %, {semitones:+0.##;-0.##;0} Halbtöne) – erlaubt sind 50–200 % und ±12 Halbtöne.");
        if (tempo is < 0.75 or > 1.33 || Math.Abs(semitones) > 4)
            return new("gelb", $"Starke Änderung (Tempo {tempo * 100:0} %, {semitones:+0.##;-0.##;0} Halbtöne) – hörbare Artefakte wahrscheinlich (verwaschene Anschläge, „Blechklang“). Bitte anhören.");
        return new("gruen", "Im sauberen Bereich.");
    }

    /// <summary>FFmpeg-Aufruf mit Rubber Band (höchste Tonhöhen-Qualität, Formanten erhalten). Bei Rot wird nicht gerechnet.</summary>
    public static string[] Args(string input, string output, double tempo, double semitones)
    {
        if (Assess(tempo, semitones) is { Light: "rot" } c) throw new ArgumentException(c.Text);
        var pitch = Math.Pow(2, semitones / 12.0);
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-af", $"rubberband=tempo={F(tempo)}:pitch={F(pitch)}:formant=preserved:pitchq=quality", output];
    }

    public sealed record Measure(double LengthRatio, double PitchCents, double Correlation);

    /// <summary>Ergebnis gegen das Original messen: Längenverhältnis, Tonhöhenverschiebung in Cent (Median des Grundtons), Phase L/R.</summary>
    public static Measure Compare(AudioBuf original, AudioBuf result)
    {
        static double MedianHz(AudioBuf a) { var hz = AudioToMidi.Frames(a).Where(f => f.Voiced).Select(f => f.Hz).OrderBy(x => x).ToList(); return hz.Count == 0 ? double.NaN : hz[hz.Count / 2]; }
        double ho = MedianHz(original), hr = MedianHz(result);
        var cents = double.IsNaN(ho) || double.IsNaN(hr) ? double.NaN : 1200 * Math.Log2(hr / ho);
        double corr = 1;
        if (result.Channels >= 2)
        {
            double sl = 0, sr = 0, slr = 0;
            for (var i = 0; i < result.Length; i++) { double l = result.Data[0][i], r = result.Data[1][i]; sl += l * l; sr += r * r; slr += l * r; }
            corr = sl < 1e-9 || sr < 1e-9 ? 1 : slr / Math.Sqrt(sl * sr);
        }
        return new(Math.Round(result.Seconds / Math.Max(1e-9, original.Seconds), 4), Math.Round(cents, 1), Math.Round(corr, 3));
    }

    /// <summary>Im Toleranzbereich? Länge ±1 %, Tonhöhe ±10 Cent, Phase L/R nicht schlechter als 0,9 (bei Mono-ähnlichem Material).</summary>
    public static Check Verify(Measure m, double tempo, double semitones, double originalCorrelation = 1)
    {
        var p = new List<string>();
        var wantLen = 1 / tempo;
        if (Math.Abs(m.LengthRatio / wantLen - 1) > 0.01) p.Add($"Länge {m.LengthRatio:0.000}× statt {wantLen:0.000}×");
        if (!double.IsNaN(m.PitchCents) && Math.Abs(m.PitchCents - semitones * 100) > 10) p.Add($"Tonhöhe {m.PitchCents:+0;-0} Cent statt {semitones * 100:+0;-0}");
        if (originalCorrelation > 0.95 && m.Correlation < 0.9) p.Add($"Phase L/R {m.Correlation:0.00} (Stereo zerfällt)");
        return p.Count == 0 ? new("gruen", $"Im Toleranzbereich: Länge {m.LengthRatio:0.000}×, Tonhöhe {m.PitchCents:+0;-0} Cent, Phase {m.Correlation:0.00}.")
                            : new("gelb", "Außerhalb der Toleranz: " + string.Join(", ", p) + ".");
    }
}
