namespace Trendo.Core.Media;

/// <summary>Audio-Editor – Vorbild-Funktionen (Audacity, Adobe Audition, iZotope RX; nur Funktionen, Clean-Room, 04.11.2026):
/// Rauschen nach Rauschprofil entfernen (Spektral-Subtraktion), Brummen entfernen, Stille kürzen, Lautheit nach ITU-R BS.1770 messen und angleichen,
/// Kompressor, Spektrogramm.</summary>
public static class AudioDsp
{
    // ───────────── FFT (Radix 2, an Ort und Stelle) ─────────────
    public static void Fft(double[] re, double[] im, bool inverse = false)
    {
        var n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (var len = 2; len <= n; len <<= 1)
        {
            var ang = 2 * Math.PI / len * (inverse ? 1 : -1); double wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (var i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (var k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = a + len / 2;
                    double tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                    var nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                }
            }
        }
        if (inverse) for (var i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
    }

    const int N = 2048, Hop = 512;
    static readonly double[] Hann = [.. Enumerable.Range(0, N).Select(i => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N))];

    /// <summary>Rauschprofil: mittlere Stärke je Frequenz in einem Stück, das nur Rauschen enthält (wie „Rauschprofil ermitteln“ in Audacity).</summary>
    public static double[] NoiseProfile(float[] x, int from, int to)
    {
        var prof = new double[N / 2 + 1]; var frames = 0;
        for (var s = Math.Max(0, from); s + N <= Math.Min(x.Length, to); s += Hop)
        {
            var re = new double[N]; var im = new double[N];
            for (var i = 0; i < N; i++) re[i] = x[s + i] * Hann[i];
            Fft(re, im);
            for (var k = 0; k <= N / 2; k++) prof[k] += Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
            frames++;
        }
        if (frames == 0) throw new InvalidOperationException("Für das Rauschprofil bitte mindestens 0,1 Sekunden reines Rauschen wählen.");
        for (var k = 0; k <= N / 2; k++) prof[k] /= frames;
        return prof;
    }

    /// <summary>Rauschen abziehen: je Frequenz wird abgesenkt, was nicht deutlich über dem Profil liegt (<paramref name="reductionDb"/>, Glättung über die Zeit gegen „Zwitschern“).</summary>
    public static void Denoise(float[] x, int from, int to, double[] profile, double reductionDb = 18, double sensitivity = 3.0)
    {
        from = Math.Clamp(from, 0, x.Length); to = Math.Clamp(to, from, x.Length);
        var floor = Math.Pow(10, -reductionDb / 20); var outp = new double[to - from + N]; var norm = new double[to - from + N];
        var prevGain = Enumerable.Repeat(1.0, N / 2 + 1).ToArray();
        for (var s = from - N / 2; s < to; s += Hop)
        {
            var re = new double[N]; var im = new double[N];
            for (var i = 0; i < N; i++) { var p = s + i; re[i] = p >= 0 && p < x.Length ? x[p] * Hann[i] : 0; }
            Fft(re, im);
            for (var k = 0; k <= N / 2; k++)
            {
                var mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]) + 1e-12;
                var g = mag > profile[k] * sensitivity ? 1.0 : floor;
                g = 0.6 * g + 0.4 * prevGain[k]; prevGain[k] = g; // zeitlich glätten
                re[k] *= g; im[k] *= g; if (k > 0 && k < N / 2) { re[N - k] *= g; im[N - k] *= g; }
            }
            Fft(re, im, inverse: true);
            for (var i = 0; i < N; i++) { var p = s + i - from; if (p < 0 || p >= outp.Length) continue; outp[p] += re[i] * Hann[i]; norm[p] += Hann[i] * Hann[i]; }
        }
        for (var i = from; i < to; i++) { var p = i - from; if (norm[p] > 1e-6) x[i] = (float)(outp[p] / norm[p]); }
    }

    /// <summary>Brummen entfernen: schmale Kerbfilter bei 50 Hz (oder 60 Hz) und den ersten Oberwellen.</summary>
    public static void Dehum(AudioBuf a, int from, int to, double baseHz = 50, int harmonics = 4)
    {
        for (var h = 1; h <= harmonics; h++)
        {
            var f = baseHz * h; if (f > a.Rate / 2.0 - 100) break;
            var w = 2 * Math.PI * f / a.Rate; var q = 30.0; var al = Math.Sin(w) / (2 * q); var cw = Math.Cos(w);
            double b0 = 1, b1 = -2 * cw, b2 = 1, a0 = 1 + al, a1 = -2 * cw, a2 = 1 - al; b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            foreach (var c in a.Data)
            {
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                for (var i = from; i < to; i++) { double x0 = c[i], y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = x0; y2 = y1; y1 = y0; c[i] = (float)y0; }
            }
        }
    }

    /// <summary>Stille kürzen (wie Audacity „Stille kürzen“): Pausen leiser als <paramref name="thresholdDb"/> und länger als <paramref name="minSec"/> werden auf <paramref name="keepSec"/> verkürzt.
    /// Liefert die neue Spur und wie viele Pausen gekürzt wurden.</summary>
    public static (float[][] Data, int Cut) TruncateSilence(AudioBuf a, double thresholdDb = -45, double minSec = 0.6, double keepSec = 0.3)
    {
        var th = Math.Pow(10, thresholdDb / 20); var win = Math.Max(1, a.Rate / 100);
        var quiet = new bool[(a.Length + win - 1) / win];
        for (var b = 0; b < quiet.Length; b++)
        {
            double peak = 0; foreach (var c in a.Data) for (var i = b * win; i < Math.Min(a.Length, (b + 1) * win); i++) peak = Math.Max(peak, Math.Abs(c[i]));
            quiet[b] = peak < th;
        }
        var keep = new List<(int, int)>(); var cut = 0; int start = 0, bi = 0;
        while (bi < quiet.Length)
        {
            if (!quiet[bi]) { bi++; continue; }
            var e = bi; while (e < quiet.Length && quiet[e]) e++;
            var len = (e - bi) * win;
            if (len >= minSec * a.Rate)
            {
                var half = (int)(keepSec * a.Rate / 2);
                keep.Add((start, Math.Min(a.Length, bi * win + half)));
                start = Math.Max(0, e * win - half); cut++;
            }
            bi = e;
        }
        keep.Add((start, a.Length));
        var data = a.Data.Select(c => keep.SelectMany(k => c[k.Item1..Math.Max(k.Item1, k.Item2)]).ToArray()).ToArray();
        return (data, cut);
    }

    /// <summary>Integrierte Lautheit nach ITU-R BS.1770-4 (K-Filter, Blöcke 400 ms mit 75 % Überlappung, absolutes Gate −70 LUFS, relatives Gate −10 LU).</summary>
    public static double Lufs(AudioBuf a)
    {
        // K-Filter: Kopfanhebung (Hochregal) und RLB-Hochpass – Koeffizienten nach BS.1770 für die tatsächliche Abtastrate berechnet
        double[] Shelf() { double f0 = 1681.974450955533, g = 3.999843853973347, q = 0.7071752369554196, k = Math.Tan(Math.PI * f0 / a.Rate), vh = Math.Pow(10, g / 20), vb = Math.Pow(vh, 0.4996667741545416), a0 = 1 + k / q + k * k; return [(vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0]; }
        double[] Hp() { double f0 = 38.13547087602444, q = 0.5003270373238773, k = Math.Tan(Math.PI * f0 / a.Rate), a0 = 1 + k / q + k * k; return [1, -2, 1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0]; }
        static double[] Run(float[] x, double[] c)
        {
            var y = new double[x.Length]; double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (var i = 0; i < x.Length; i++) { double x0 = x[i], y0 = c[0] * x0 + c[1] * x1 + c[2] * x2 - c[3] * y1 - c[4] * y2; x2 = x1; x1 = x0; y2 = y1; y1 = y0; y[i] = y0; }
            return y;
        }
        static double[] Run2(double[] x, double[] c)
        {
            var y = new double[x.Length]; double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (var i = 0; i < x.Length; i++) { double x0 = x[i], y0 = c[0] * x0 + c[1] * x1 + c[2] * x2 - c[3] * y1 - c[4] * y2; x2 = x1; x1 = x0; y2 = y1; y1 = y0; y[i] = y0; }
            return y;
        }
        var sh = Shelf(); var hp = Hp(); // Hochpass mit Zähler 1, −2, 1 wie in der Norm
        var filtered = a.Data.Select(c => Run2(Run(c, sh), hp)).ToArray();
        var block = (int)(0.4 * a.Rate); var step = block / 4; var z = new List<double>();
        for (var s = 0; s + block <= a.Length; s += step)
        {
            double sum = 0; foreach (var c in filtered) { double e = 0; for (var i = s; i < s + block; i++) e += c[i] * c[i]; sum += e / block; }
            z.Add(sum);
        }
        if (z.Count == 0) return double.NegativeInfinity;
        double L(double v) => -0.691 + 10 * Math.Log10(v + 1e-20);
        var abs = z.Where(v => L(v) > -70).ToList(); if (abs.Count == 0) return double.NegativeInfinity;
        var rel = L(abs.Average()) - 10;
        var gated = abs.Where(v => L(v) > rel).ToList();
        return Math.Round(L(gated.Average()), 2);
    }

    /// <summary>Kompressor (RMS, weiche Kennlinie): über <paramref name="thresholdDb"/> wird mit <paramref name="ratio"/> abgesenkt, danach Ausgleich (makeup).</summary>
    public static void Compress(AudioBuf a, int from, int to, double thresholdDb = -20, double ratio = 3, double attackMs = 10, double releaseMs = 150)
    {
        double at = Math.Exp(-1.0 / (a.Rate * attackMs / 1000)), rt = Math.Exp(-1.0 / (a.Rate * releaseMs / 1000)), env = 0;
        var makeup = Math.Pow(10, -(thresholdDb * (1 - 1 / ratio)) / 2 / 20);
        for (var i = from; i < to; i++)
        {
            double p = 0; foreach (var c in a.Data) p = Math.Max(p, Math.Abs(c[i]));
            env = p > env ? at * env + (1 - at) * p : rt * env + (1 - rt) * p;
            var db = 20 * Math.Log10(env + 1e-9); var over = db - thresholdDb;
            var gainDb = over > 0 ? -over * (1 - 1 / ratio) : 0;
            var g = (float)(Math.Pow(10, gainDb / 20) * makeup);
            foreach (var c in a.Data) c[i] = Math.Clamp(c[i] * g, -1f, 1f);
        }
    }

    /// <summary>Spektrogramm (wie Audition „Spektral-Ansicht“): Spalten × Zeilen, Werte 0–1 (dB, −90 bis 0), tiefe Frequenzen unten, logarithmische Frequenzachse.</summary>
    public static float[,] Spectrogram(AudioBuf a, int columns, int rows)
    {
        var img = new float[columns, rows];
        if (a.Length < N) return img;
        var mono = new float[a.Length]; for (var i = 0; i < a.Length; i++) { float s = 0; foreach (var c in a.Data) s += c[i]; mono[i] = s / a.Channels; }
        double fMin = 40, fMax = Math.Min(16000, a.Rate / 2.0);
        for (var col = 0; col < columns; col++)
        {
            var s = (int)((long)col * (a.Length - N) / Math.Max(1, columns - 1));
            var re = new double[N]; var im = new double[N];
            for (var i = 0; i < N; i++) re[i] = mono[s + i] * Hann[i];
            Fft(re, im);
            for (var r = 0; r < rows; r++)
            {
                var f = fMin * Math.Pow(fMax / fMin, (double)r / Math.Max(1, rows - 1)); var k = (int)Math.Clamp(Math.Round(f * N / a.Rate), 1, N / 2);
                var mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]) / (N / 4.0);
                var db = 20 * Math.Log10(mag + 1e-9);
                img[col, rows - 1 - r] = (float)Math.Clamp((db + 90) / 90, 0, 1);
            }
        }
        return img;
    }
}
