using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Multicam wie im Profi-Schnitt – mehrere Kameras derselben Szene über den Ton synchronisieren
/// (Kreuzkorrelation der Lautstärke-Hüllkurve, 1 ms genau gerechnet) und zwischen den Blickwinkeln schneiden. Ohne gemeinsamen Ton wird nicht geraten.</summary>
public static class Multicam
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const int EnvRate = 1000; // Hüllkurve: 1 Wert je ms

    /// <summary>FFmpeg: nur die Tonspur als Mono-WAV (8 kHz reicht für die Synchronisation).</summary>
    public static List<string> AudioArgs(string video, string wav) => ["-hide_banner", "-loglevel", "error", "-y", "-i", video, "-vn", "-ac", "1", "-ar", "8000", "-c:a", "pcm_s16le", wav];

    /// <summary>Lautstärke-Hüllkurve (Betrag, 1 ms), ohne Gleichanteil – sie ist unabhängig von Mikrofon-Klangfarbe.</summary>
    public static float[] Envelope(AudioBuf a)
    {
        var step = Math.Max(1, a.Rate / EnvRate); var n = a.Length / step; var e = new float[n];
        for (var i = 0; i < n; i++) { double s = 0; for (var k = 0; k < step; k++) { var v = a.Data[0][i * step + k]; s += Math.Abs(v); } e[i] = (float)(s / step); }
        var mean = e.Length == 0 ? 0 : e.Average(); for (var i = 0; i < n; i++) e[i] -= mean;
        return e;
    }

    public sealed record SyncResult(double Offset, double Confidence);

    /// <summary>Wie viel später beginnt <paramref name="other"/> als <paramref name="reference"/>? (positiv = Kamera B startete später.) Suche ±<paramref name="maxSeconds"/>.</summary>
    public static SyncResult Offset(float[] reference, float[] other, double maxSeconds = 30)
    {
        double nr = Math.Sqrt(reference.Sum(x => (double)x * x)), no = Math.Sqrt(other.Sum(x => (double)x * x));
        if (nr < 1e-6 || no < 1e-6) return new(0, 0);
        static double Corr(float[] a, float[] b, int lag) // b[t] gehört zu a[t + lag]
        {
            double s = 0; var from = Math.Max(0, -lag); var to = Math.Min(b.Length, a.Length - lag);
            for (var t = from; t < to; t++) s += b[t] * (double)a[t + lag];
            return s;
        }
        // grob: 10-ms-Raster (Hüllkurven gemittelt), dann fein: 1 ms um den besten Treffer
        static float[] Down(float[] x) { var r = new float[x.Length / 10]; for (var i = 0; i < r.Length; i++) { float s = 0; for (var k = 0; k < 10; k++) s += x[i * 10 + k]; r[i] = s / 10; } return r; }
        var (ra, oa) = (Down(reference), Down(other)); var max = (int)(maxSeconds * EnvRate / 10);
        var coarse = 0; var bestC = double.MinValue;
        for (var lag = -max; lag <= max; lag++) { var s = Corr(ra, oa, lag); if (s > bestC) { bestC = s; coarse = lag; } }
        var bestLag = coarse * 10; var best = double.MinValue;
        for (var lag = coarse * 10 - 20; lag <= coarse * 10 + 20; lag++) { var s = Corr(reference, other, lag); if (s > best) { best = s; bestLag = lag; } }
        return new(Math.Round(bestLag / (double)EnvRate, 3), Math.Round(Math.Clamp(best / (nr * no), 0, 1), 3));
    }

    /// <summary>Ab Sekunde <paramref name="At"/> (Projektzeit = Zeit der Referenzkamera) läuft Kamera <paramref name="Angle"/>.</summary>
    public sealed record Switch(double At, int Angle);

    /// <summary>„0:1, 4:2, 9:1“ (Sekunde:Kamera, Kameras ab 1) lesen.</summary>
    public static List<Switch> ParseSwitches(string text)
    {
        var r = new List<Switch>();
        foreach (var part in (text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split(':');
            if (kv.Length != 2 || !double.TryParse(kv[0].Replace(',', '.'), NumberStyles.Float, Inv, out var t) || !int.TryParse(kv[1], out var cam) || cam < 1)
                throw new ArgumentException($"„{part}“ verstehe ich nicht – Schreibweise Sekunde:Kamera, z. B. 4:2.");
            r.Add(new Switch(t, cam - 1));
        }
        return r;
    }

    /// <summary>Schnitt aus den Wechseln: je Abschnitt ein Stück der gewählten Kamera mit passendem Einstiegspunkt (Versatz verrechnet). Ton bleibt durchgehend von Kamera 1.</summary>
    public static List<EditClip> Cut(IReadOnlyList<(string File, double Offset, double Length)> angles, IReadOnlyList<Switch> switches, double end)
    {
        if (angles.Count < 2) throw new ArgumentException("Multicam braucht mindestens zwei Kameras.");
        if (switches.Count == 0 || switches[0].At > 1e-6) throw new ArgumentException("Der erste Wechsel muss bei 0 s beginnen.");
        var clips = new List<EditClip>();
        for (var i = 0; i < switches.Count; i++)
        {
            var sw = switches[i]; var stop = i + 1 < switches.Count ? switches[i + 1].At : end;
            if (sw.Angle < 0 || sw.Angle >= angles.Count) throw new ArgumentException($"Kamera {sw.Angle + 1} gibt es nicht.");
            if (stop <= sw.At) throw new ArgumentException("Wechsel müssen zeitlich aufsteigen.");
            var (file, off, len) = angles[sw.Angle];
            var inPt = sw.At - off; // Zeit in der Datei dieser Kamera
            if (inPt < -1e-6 || inPt + (stop - sw.At) > len + 1e-3) throw new InvalidOperationException($"Kamera {sw.Angle + 1} hat bei {sw.At:0.##}–{stop:0.##} s keine Aufnahme (Versatz {off:0.###} s).");
            clips.Add(new EditClip(Guid.NewGuid().ToString("N")[..8], file, Math.Max(0, inPt), Math.Max(0, inPt) + (stop - sw.At), len, false)); // Bild ohne Ton – der Ton läuft durchgehend von Kamera 1 auf der Musikspur
        }
        return clips;
    }

    /// <summary>Synchronisieren mit FFmpeg (Ton ziehen) – Kamera 1 ist die Referenz. Ohne erkennbaren gemeinsamen Ton: Fehler statt Raten.</summary>
    public static List<SyncResult> Sync(Func<IReadOnlyList<string>, Cli.Result> run, IReadOnlyList<string> videos, string workDir, double minConfidence = 0.5)
    {
        Directory.CreateDirectory(workDir);
        var env = new List<float[]>();
        foreach (var v in videos)
        {
            var wav = Path.Combine(workDir, $"sync-{Guid.NewGuid():N}.wav");
            var r = run(AudioArgs(v, wav));
            if (!r.Ok || !File.Exists(wav)) throw new InvalidOperationException($"„{Path.GetFileName(v)}“ hat keinen Ton – ohne Ton keine automatische Synchronisation.");
            env.Add(Envelope(AudioFile.Read(wav))); File.Delete(wav);
        }
        var res = new List<SyncResult> { new(0, 1) };
        for (var i = 1; i < env.Count; i++)
        {
            var s = Offset(env[0], env[i]);
            if (s.Confidence < minConfidence) throw new InvalidOperationException($"Kamera {i + 1} („{Path.GetFileName(videos[i])}“) passt im Ton nicht zu Kamera 1 (Übereinstimmung {s.Confidence:0.00}) – ohne gemeinsamen Ton wird nicht geraten.");
            res.Add(s);
        }
        return res;
    }
}
