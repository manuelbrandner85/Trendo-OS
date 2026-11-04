using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Videoschnitt X12 (freigegeben 04.11.2026): Takt finden und Schnitte auf den Takt legen – selbst gebaut, ohne Python.
/// Verfahren: Einsatz-Hüllkurve (Anstieg der Lautheit je 11,6 ms), Tempo per Autokorrelation (70–180 BPM), Phase mit der größten Einsatz-Summe.</summary>
public static class BeatDetect
{
    public sealed record Result(double Bpm, IReadOnlyList<double> Beats);

    public static Result Find(AudioBuf a, double maxSeconds = 600)
    {
        if (a.Length == 0) return new(0, []);
        var hop = Math.Max(1, a.Rate / 86); // ≈ 11,6 ms
        var n = (int)Math.Min(a.Length, maxSeconds * a.Rate) / hop;
        if (n < 64) return new(0, []);
        // Lautheit je Fenster, dann positiver Anstieg (Einsätze von Schlägen)
        var e = new double[n];
        for (var k = 0; k < n; k++)
        {
            double sum = 0; var s = k * hop;
            foreach (var c in a.Data) for (var i = s; i < s + hop && i < c.Length; i++) sum += c[i] * c[i];
            e[k] = Math.Log(1e-9 + sum / hop);
        }
        var on = new double[n];
        for (var k = 1; k < n; k++) on[k] = Math.Max(0, e[k] - e[k - 1]);
        var mean = on.Average(); for (var k = 0; k < n; k++) on[k] = Math.Max(0, on[k] - mean);
        var fps = a.Rate / (double)hop;
        // Tempo: Autokorrelation, leicht zu 120 BPM gewichtet; halbes Tempo vermeiden (Klick auf jedem Schlag korreliert auch bei 2 Schlägen)
        int lo = (int)(fps * 60 / 200), hi = (int)(fps * 60 / 60);
        var raw = new double[hi + 2];
        for (var L = lo; L <= hi + 1 && L < n / 2; L++) { double r = 0; for (var k = L; k < n; k++) r += on[k] * on[k - L]; raw[L] = r; }
        double best = -1; var lag = lo;
        for (var L = (int)(fps * 60 / 180); L <= (int)(fps * 60 / 70) && L < n / 2; L++)
        {
            var w = raw[L] * Math.Exp(-0.5 * Math.Pow(Math.Log2(60 * fps / L / 120), 2) / 0.8);
            if (w > best) { best = w; lag = L; }
        }
        var half = (int)Math.Round(lag / 2.0);
        var halfPeak = Enumerable.Range(Math.Max(lo, half - 1), 3).Where(L => L < raw.Length).MaxBy(L => raw[L]);
        if (60 * fps / halfPeak <= 200 && raw[halfPeak] >= 0.6 * raw[lag]) lag = halfPeak;
        // Bruchteil des Abstands per Parabel (sonst wandern die Schläge über längere Stücke weg)
        double frac = lag;
        if (lag > lo && lag + 1 < raw.Length) { double y0 = raw[lag - 1], y1 = raw[lag], y2 = raw[lag + 1], den = y0 - 2 * y1 + y2; if (Math.Abs(den) > 1e-12) frac = lag + Math.Clamp(0.5 * (y0 - y2) / den, -0.5, 0.5); }
        // Phase: Raster mit der größten Einsatz-Summe
        double bestP = -1; var phase = 0.0;
        for (var p0 = 0; p0 < lag; p0++) { double sum = 0; for (var x = (double)p0; x < n; x += frac) sum += on[(int)x]; if (sum > bestP) { bestP = sum; phase = p0; } }
        // Beats leicht an die echte Spitze rücken (±2 Fenster)
        var beats = new List<double>();
        // Mitlaufen: jeder Schlag sucht die echte Spitze in ±3 Fenstern und das Raster folgt ihr (kein Wegdriften)
        var reach = Math.Max(2, (int)(frac / 8));
        for (var x = phase; x < n;)
        {
            var k = (int)Math.Round(x); if (k >= n) break;
            var m = k; for (var j = Math.Max(0, k - reach); j <= Math.Min(n - 1, k + reach); j++) if (on[j] > on[m]) m = j;
            if (on[m] <= 0) m = k; // keine Spitze: im Raster bleiben
            beats.Add(Math.Round(m / fps, 3));
            x = 0.5 * (m + x) + frac; // halb der Spitze folgen, halb dem Raster – ruhig und ohne Drift
        }
        return new(Math.Round(60 * fps / frac, 1), beats);
    }

    /// <summary>Schnitte auf den Takt: jedes Stück der Bildspur endet auf dem nächsten passenden Schlag (jeder <paramref name="every"/>. Schlag).
    /// Beats sind Zeiten der Musikdatei; die Lage der Musik auf der Zeitleiste (Start, In) wird eingerechnet. Liefert die Zahl der verschobenen Schnitte.</summary>
    public static int SnapCuts(JsonObject p, IReadOnlyList<double> beats, int every = 1, int musicIndex = 0)
    {
        var music = VideoEdit.Clips(p, "music");
        if (beats.Count == 0 || musicIndex >= music.Count) return 0;
        var m = music[musicIndex]; every = Math.Max(1, every);
        var grid = beats.Where((_, i) => i % every == 0).Select(b => m.Start + (b - m.In)).Where(t => t > 0).ToList();
        var clips = VideoEdit.Clips(p); var moved = 0; var t0 = 0.0;
        for (var i = 0; i < clips.Count - 1; i++)
        {
            var c = clips[i]; var end = t0 + c.Length - VideoEdit.FadeOf(clips, i);
            var target = grid.Where(g => g - t0 >= VideoEdit.MinLength + 0.3).OrderBy(g => Math.Abs(g - end)).FirstOrDefault(double.NaN);
            if (!double.IsNaN(target))
            {
                var len = target - t0 + VideoEdit.FadeOf(clips, i);
                var maxLen = c.IsImage ? double.MaxValue : c.SourceLength - c.In;
                len = Math.Clamp(len, VideoEdit.MinLength, maxLen);
                if (Math.Abs(len - c.Length) > 0.01) { VideoEdit.Trim(p, i, c.In, c.In + len); moved++; clips = VideoEdit.Clips(p); c = clips[i]; }
            }
            t0 += c.Length - VideoEdit.FadeOf(clips, i);
        }
        return moved;
    }
}
