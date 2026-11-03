using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Trendo Musik S5/S6 (freigegeben 03.11.2026): Mitsingen mit Treffer-Anzeige und eigene Aufnahme über die Begleitung mischen.
/// Die Tonhöhe des Mikrofons erkennt Trendo live mit dem YIN-Verfahren (de Cheveigné &amp; Kawahara 2002), die Zielmelodie kommt aus der Gesangsspur.</summary>
public static class SingAlong
{
    /// <summary>Grundfrequenz eines Blocks (YIN) in Hz – NaN bei Stille oder ohne klaren Ton.</summary>
    public static double Yin(ReadOnlySpan<float> x, int sampleRate, double fMin = 70, double fMax = 1100, double threshold = 0.15)
    {
        int maxLag = Math.Min(x.Length / 2, (int)(sampleRate / fMin)), minLag = Math.Max(2, (int)(sampleRate / fMax));
        if (maxLag <= minLag) return double.NaN;
        double energy = 0; for (var i = 0; i < x.Length; i++) energy += x[i] * x[i];
        if (energy / x.Length < 1e-5) return double.NaN; // Stille
        var d = new double[maxLag + 1];
        var w = x.Length - maxLag;
        for (var tau = 1; tau <= maxLag; tau++) { double s = 0; for (var i = 0; i < w; i++) { var df = x[i] - x[i + tau]; s += df * df; } d[tau] = s; }
        double run = 0; d[0] = 1;
        for (var tau = 1; tau <= maxLag; tau++) { run += d[tau]; d[tau] = run > 0 ? d[tau] * tau / run : 1; } // kumulierte mittlere Normierung
        for (var tau = minLag; tau < maxLag; tau++)
            if (d[tau] < threshold)
            {
                while (tau + 1 < maxLag && d[tau + 1] < d[tau]) tau++;
                var (a, b, c) = (d[tau - 1], d[tau], d[tau + 1]); var den = a - 2 * b + c;
                var t = den != 0 ? tau + 0.5 * (a - c) / den : tau; // parabolische Verfeinerung
                return sampleRate / t;
            }
        return double.NaN;
    }

    public static double Midi(double hz) => 69 + 12 * Math.Log2(hz / 440.0);
    static readonly string[] Names = ["C", "Cis", "D", "Es", "E", "F", "Fis", "G", "As", "A", "B", "H"];
    public static string NoteName(double midi) { var m = (int)Math.Round(midi); return Names[((m % 12) + 12) % 12] + (m / 12 - 1); }

    /// <summary>Zielmelodie (Analyseskript „--melodie“): Schrittweite in Sekunden und MIDI-Töne (null = kein Gesang).</summary>
    public sealed record Melody(double Hop, double?[] Midi)
    {
        public static Melody? Parse(string json)
        {
            try
            {
                using var d = JsonDocument.Parse(json);
                var hop = d.RootElement.GetProperty("hop").GetDouble();
                var m = d.RootElement.GetProperty("midi").EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null).ToArray();
                return hop > 0 && m.Length > 0 ? new(hop, m) : null;
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
        }

        /// <summary>Zielton um diese Zeit (±<paramref name="window"/> s, gleicht die Verzögerung von Lautsprecher und Mikrofon aus) – der dem gesungenen am nächsten.</summary>
        public double? Near(double t, double sung, double window = 0.25)
        {
            int a = Math.Max(0, (int)((t - window) / Hop)), b = Math.Min(Midi.Length - 1, (int)((t + window) / Hop));
            double? best = null;
            for (var i = a; i <= b; i++) if (Midi[i] is double m && (best is null || Math.Abs(Dist(m, sung)) < Math.Abs(Dist(best.Value, sung)))) best = m;
            return best;
        }
    }

    /// <summary>Abstand in Halbtönen ohne Oktave (Männer singen Frauenlieder oft eine Oktave tiefer – das zählt als richtig).</summary>
    public static double Dist(double target, double sung) { var d = ((sung - target) % 12 + 12) % 12; return d > 6 ? d - 12 : d; }

    public enum Verdict { Still, Treffer, Hoeher, Tiefer }

    /// <summary>Laufende Auswertung eines Durchgangs.</summary>
    public sealed class Take(Melody melody)
    {
        public int Sung { get; private set; }
        public int Hits { get; private set; }
        public double HitPercent => Sung == 0 ? 0 : 100.0 * Hits / Sung;
        public (Verdict Verdict, double? Target, double? SungMidi) Last { get; private set; } = (Verdict.Still, null, null);

        /// <summary>Ein Block: Zeit im Lied und gesungene Frequenz (NaN = nichts gesungen). Halber Halbton Abstand gilt als Treffer.</summary>
        public Verdict Add(double t, double hz)
        {
            if (double.IsNaN(hz)) { Last = (Verdict.Still, null, null); return Verdict.Still; }
            var s = Midi(hz);
            if (melody.Near(t, s) is not double target) { Last = (Verdict.Still, null, s); return Verdict.Still; } // an dieser Stelle wird nicht gesungen
            Sung++;
            var d = Dist(target, s);
            var v = Math.Abs(d) <= 0.5 ? Verdict.Treffer : d < 0 ? Verdict.Hoeher : Verdict.Tiefer;
            if (v == Verdict.Treffer) Hits++;
            Last = (v, target, s); return v;
        }
    }

    /// <summary>S6: Aufnahme über die Begleitung mischen – Stimme säubern (Rumpelfilter, leichter Kompressor), Versatz ausgleichen, Hall, Lautstärke.
    /// Die Begleitung kommt 3 dB leiser hinein (gemessen 03.11.2026: ohne das lag die Spitze bei +0,1 dBTP); danach wird gemastert.</summary>
    public static string[] MixArgs(string accompaniment, string take, string output, int offsetMs, double vocalDb, string reverb)
    {
        var inv = CultureInfo.InvariantCulture;
        var room = reverb switch
        {
            "raum" => ",aecho=0.8:0.7:23|47|71:0.22|0.16|0.11",
            "halle" => ",aecho=0.8:0.75:60|120|190|260:0.25|0.2|0.15|0.1",
            _ => "",
        };
        // positiver Versatz: Stimme kam zu spät (Lautsprecher-/Mikrofon-Verzögerung) – vorn abschneiden; negativer: Stimme später setzen
        var shift = offsetMs > 0 ? $",atrim=start={(offsetMs / 1000.0).ToString("0.000", inv)},asetpts=PTS-STARTPTS" : offsetMs < 0 ? $",adelay={-offsetMs}:all=1" : "";
        var voc = $"[1:a]aformat=channel_layouts=stereo,highpass=f=90,acompressor=threshold=-18dB:ratio=3:attack=8:release=120:makeup=2{shift}{room},volume={vocalDb.ToString("0.0", inv)}dB[v]";
        return ["-hide_banner", "-nostats", "-y", "-i", accompaniment, "-i", take, "-filter_complex",
            $"{voc};[0:a]volume=-3dB[b];[b][v]amix=inputs=2:duration=first:normalize=0[m]", "-map", "[m]", "-c:a", "pcm_s24le", "-ar", "48000", output];
    }
}
