using System.Text;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-19 (07.11.2026): Melodie aus einer Aufnahme als Noten schätzen – Grundton je Abschnitt (YIN), Sicherheit je Note,
/// Erkennung von Mehrstimmigkeit. Mehrstimmiges oder Unsicheres wird nie als „exakt“ ausgegeben, sondern im Unsicherheitsbericht benannt. Läuft ohne KI-Modell.</summary>
public static class AudioToMidi
{
    public sealed record Frame(double Time, double Hz, double Confidence, bool Voiced, bool Polyphonic, bool Audible = true);
    public sealed record Note(int Pitch, double Start, double Length, double Confidence);
    public sealed record Result(IReadOnlyList<Note> Notes, double PolyphonicShare, double UnsureShare, bool Exact, string Verdict);

    /// <summary>Grundton je Abschnitt (Fenster 2048, Schritt 512) mit YIN; dazu Mehrstimmigkeits-Prüfung über das Spektrum.</summary>
    public static List<Frame> Frames(AudioBuf a, double minHz = 60, double maxHz = 1500)
    {
        var x = new float[a.Length];
        for (var c = 0; c < a.Channels; c++) for (var i = 0; i < a.Length; i++) x[i] += a.Data[c][i] / a.Channels;
        const int size = 2048, hop = 512; var rate = a.Rate; var frames = new List<Frame>();
        int tauMin = Math.Max(2, (int)(rate / maxHz)), tauMax = Math.Min(size / 2 - 1, (int)(rate / minHz));
        var d = new double[tauMax + 1];
        for (var s = 0; s + size <= x.Length; s += hop)
        {
            double e = 0; for (var i = 0; i < size; i++) e += x[s + i] * (double)x[s + i];
            var rms = Math.Sqrt(e / size); var t = (s + size / 2.0) / rate;
            if (rms < 0.01) { frames.Add(new(t, 0, 0, false, false, false)); continue; } // −40 dBFS: Stille
            const int w = size / 2;
            for (var tau = 1; tau <= tauMax; tau++) { double sum = 0; for (var i = 0; i < w; i++) { var df = x[s + i] - x[s + i + tau]; sum += df * df; } d[tau] = sum; }
            double run = 0; var best = -1; var bestVal = 1.0;
            var cm = new double[tauMax + 1]; cm[0] = 1;
            for (var tau = 1; tau <= tauMax; tau++) { run += d[tau]; cm[tau] = run <= 0 ? 1 : d[tau] * tau / run; }
            for (var tau = tauMin; tau < tauMax; tau++)
                if (cm[tau] < 0.15) { while (tau + 1 < tauMax && cm[tau + 1] < cm[tau]) tau++; best = tau; bestVal = cm[tau]; break; }
            if (best < 0) { for (var tau = tauMin; tau < tauMax; tau++) if (cm[tau] < bestVal) { bestVal = cm[tau]; best = tau; } }
            if (best < 0) { frames.Add(new(t, 0, 0, false, false)); continue; }
            double tb = best;
            if (best > 1 && best < tauMax) { var (a1, b1, c1) = (cm[best - 1], cm[best], cm[best + 1]); var den = a1 - 2 * b1 + c1; if (Math.Abs(den) > 1e-12) tb = best + 0.5 * (a1 - c1) / den; }
            var hz = rate / tb; var conf = Math.Clamp(1 - bestVal, 0, 1);
            frames.Add(new(t, hz, conf, conf > 0.5, Polyphonic(x, s, size, rate, hz)));
        }
        return frames;
    }

    /// <summary>Mehrstimmig, wenn ein starker Spektralgipfel (−20 dB zum lautesten) nicht auf einem Vielfachen des Grundtons liegt
    /// oder der Grundton selbst keine Energie hat (nur „gedachter“ Grundton eines Akkords).</summary>
    static bool Polyphonic(float[] x, int s, int size, int rate, double f0)
    {
        var re = new double[size]; var im = new double[size];
        for (var i = 0; i < size; i++) re[i] = x[s + i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / size));
        AudioDsp.Fft(re, im);
        var mag = new double[size / 2]; var max = 1e-12;
        for (var k = 1; k < size / 2; k++) { mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); max = Math.Max(max, mag[k]); }
        var binHz = rate / (double)size; var strong = new List<double>();
        for (var k = 2; k < size / 2 - 1; k++)
            if (mag[k] > max * 0.1 && mag[k] >= mag[k - 1] && mag[k] >= mag[k + 1]) strong.Add(k * binHz);
        var f0Bin = (int)Math.Round(f0 / binHz);
        var f0Energy = Enumerable.Range(Math.Max(1, f0Bin - 1), 3).Where(k => k < mag.Length).Max(k => mag[k]);
        if (f0Energy < max * 0.05 && strong.Count > 1) return true;
        foreach (var p in strong)
        {
            var h = p / f0; var dist = Math.Abs(h - Math.Round(h)) * f0;
            if (Math.Round(h) < 1 || dist > Math.Max(binHz * 1.5, p * 0.03)) return true;
        }
        return false;
    }

    static int Midi(double hz) => (int)Math.Round(69 + 12 * Math.Log2(hz / 440));

    /// <summary>Abschnitte zu Noten zusammenfassen (gleiche Tonhöhe hintereinander, mindestens 60 ms); Sicherheit = Mittel der Abschnitte.</summary>
    public static Result Transcribe(AudioBuf a)
    {
        var f = Frames(a); var notes = new List<Note>(); var hop = 512.0 / a.Rate;
        int i = 0;
        while (i < f.Count)
        {
            if (!f[i].Voiced) { i++; continue; }
            var p = Midi(f[i].Hz); var j = i; var conf = 0.0; var poly = 0;
            while (j < f.Count && f[j].Voiced && Midi(f[j].Hz) == p) { conf += f[j].Confidence; if (f[j].Polyphonic) poly++; j++; }
            var n = j - i; var len = n * hop;
            if (len >= 0.06) notes.Add(new Note(p, Math.Round(f[i].Time - hop / 2, 3), Math.Round(len, 3), Math.Round(conf / n * (poly > n / 2 ? 0.5 : 1), 3)));
            i = j;
        }
        var audible = f.Where(x => x.Audible && x.Hz > 0).ToList(); // alles Hörbare zählt – auch Abschnitte ohne sicheren Grundton
        var polyShare = audible.Count == 0 ? 0 : audible.Count(x => x.Polyphonic) / (double)audible.Count;
        var unsure = notes.Count == 0 ? 1 : notes.Count(n => n.Confidence < 0.8) / (double)notes.Count;
        var exact = notes.Count > 0 && polyShare < 0.1 && unsure < 0.1;
        var verdict = polyShare >= 0.1 && notes.Count == 0 ? $"Mehrstimmig ({polyShare:P0} der Abschnitte), keine einzelne Melodie – NICHT exakt. Akkorde bitte von Hand in der Piano-Roll setzen."
            : notes.Count == 0 ? "Keine Melodie erkannt (Stille oder nur Geräusch)."
            : polyShare >= 0.1 ? $"Mehrstimmig ({polyShare:P0} der Abschnitte) – nur eine Stimme geschätzt, NICHT exakt. Bitte in der Piano-Roll nachprüfen."
            : unsure >= 0.1 ? $"{unsure:P0} der Noten unsicher – Schätzung, nicht exakt."
            : "Einstimmig und sicher erkannt.";
        return new Result(notes, Math.Round(polyShare, 3), Math.Round(unsure, 3), exact, verdict);
    }

    /// <summary>Noten als Piano-Roll-Lied (Ticks bei gegebenem Tempo).</summary>
    public static PianoRoll.Song ToSong(Result r, double bpm, string name) =>
        new(bpm, 4, [.. r.Notes.Select(n => new PianoRoll.Note(Math.Clamp(n.Pitch, 0, 127), (int)Math.Round(n.Start * bpm / 60 * PianoRoll.Ppq), Math.Max(1, (int)Math.Round(n.Length * bpm / 60 * PianoRoll.Ppq)), (int)Math.Clamp(40 + n.Confidence * 80, 1, 127)))], name);

    /// <summary>Unsicherheitsbericht (Markdown).</summary>
    public static string Report(string title, Result r)
    {
        var sb = new StringBuilder($"# Melodie → MIDI „{title}“\n\n**{r.Verdict}**\n\nMehrstimmige Abschnitte: {r.PolyphonicShare:P0} · unsichere Noten: {r.UnsureShare:P0} · Ergebnis {(r.Exact ? "exakt (einstimmig, sicher)" : "geschätzt")}\n\n");
        sb.Append("| Note | Beginn (s) | Länge (s) | Sicherheit |\n|---|---|---|---|\n");
        foreach (var n in r.Notes) sb.Append($"| {PianoRoll.NoteName(n.Pitch)} | {n.Start:0.000} | {n.Length:0.000} | {n.Confidence:P0}{(n.Confidence < 0.8 ? " ⚠" : "")} |\n");
        return sb.ToString();
    }
}
