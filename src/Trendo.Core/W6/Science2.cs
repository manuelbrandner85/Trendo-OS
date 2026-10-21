using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>SPEC-05 Ausbau: Deutung, gepaarter t-Test, Quartile, Histogramm, Konfidenzintervall, Chi², Fehlerfortpflanzung, Stichprobe, Streudiagramm, Glätten, z-Werte.</summary>
public static class StatsPlus
{
    /// <summary>F05: Zusammenhang in Worten (nach Cohen).</summary>
    public static string Describe(double r) { var a = Math.Abs(r); var s = a >= 0.8 ? "sehr starker" : a >= 0.5 ? "starker" : a >= 0.3 ? "mittlerer" : a >= 0.1 ? "schwacher" : "kein erkennbarer"; return a < 0.1 ? s + " Zusammenhang" : $"{s} {(r > 0 ? "positiver (je mehr x, desto mehr y)" : "negativer (je mehr x, desto weniger y)")} Zusammenhang"; }

    public static double TwoSidedP(double t, double df) => Stats.BetaReg(df / 2, 0.5, df / (df + t * t));

    /// <summary>t-Quantil (zweiseitig) per Bisektion.</summary>
    public static double TCrit(double df, double alpha = 0.05) { double lo = 0, hi = 1000; for (var i = 0; i < 200; i++) { var m = (lo + hi) / 2; if (TwoSidedP(m, df) > alpha) lo = m; else hi = m; } return (lo + hi) / 2; }

    /// <summary>F07: gepaarter t-Test (vorher/nachher).</summary>
    public static TTest Paired(double[] a, double[] b, double alpha = 0.05)
    {
        if (a.Length != b.Length || a.Length < 2) throw new ArgumentException("Beide Reihen brauchen gleich viele Werte (mindestens 2).");
        var d = a.Zip(b, (x, y) => x - y).ToArray(); var s = Stats.Describe("d", d);
        if (s.StdDev == 0) throw new ArgumentException("Die Unterschiede sind alle gleich – kein Test möglich.");
        var t = s.Mean / (s.StdDev / Math.Sqrt(d.Length)); var p = TwoSidedP(t, d.Length - 1);
        return new(t, d.Length - 1, p, a.Average(), b.Average(), p < alpha);
    }

    /// <summary>F08: Quartile (Typ 7 wie Excel QUARTILE.INKL), IQR, Ausreißer außerhalb 1,5·IQR.</summary>
    public static (double Q1, double Q2, double Q3, double Iqr, IReadOnlyList<double> Outliers) Quartiles(double[] v)
    {
        if (v.Length < 2) throw new ArgumentException("Mindestens 2 Werte.");
        var s = v.OrderBy(x => x).ToArray(); double Q(double p) { var h = (s.Length - 1) * p; var lo = (int)Math.Floor(h); return lo + 1 < s.Length ? s[lo] + (h - lo) * (s[lo + 1] - s[lo]) : s[lo]; }
        double q1 = Q(.25), q3 = Q(.75), iqr = q3 - q1; return (q1, Q(.5), q3, iqr, s.Where(x => x < q1 - 1.5 * iqr || x > q3 + 1.5 * iqr).ToList());
    }

    /// <summary>F09: Histogramm, Klassenzahl nach Sturges (1 + log2 n).</summary>
    public static IReadOnlyList<(double From, double To, int Count)> Histogram(double[] v, int? bins = null)
    {
        if (v.Length == 0) throw new ArgumentException("Keine Werte.");
        var k = bins ?? (int)Math.Ceiling(1 + Math.Log2(v.Length)); double min = v.Min(), max = v.Max(), w = max == min ? 1 : (max - min) / k;
        var c = new int[k]; foreach (var x in v) c[Math.Min(k - 1, (int)((x - min) / w))]++;
        return Enumerable.Range(0, k).Select(i => (min + i * w, min + (i + 1) * w, c[i])).ToList();
    }

    public static string HistogramSvg(IReadOnlyList<(double From, double To, int Count)> h, string title)
    {
        const int W = 600, H = 300; var max = Math.Max(1, h.Max(x => x.Count)); var bw = (W - 40.0) / h.Count; var sb = new StringBuilder();
        for (var i = 0; i < h.Count; i++) { var bh = (H - 60.0) * h[i].Count / max; sb.Append(FormattableString.Invariant($"<rect x=\"{20 + i * bw:0.#}\" y=\"{H - 30 - bh:0.#}\" width=\"{bw - 2:0.#}\" height=\"{bh:0.#}\" fill=\"#3b82f6\"/><text x=\"{20 + i * bw + bw / 2:0.#}\" y=\"{H - 12}\" font-size=\"10\" text-anchor=\"middle\">{h[i].From.ToString("0.##", Fach.De)}</text><text x=\"{20 + i * bw + bw / 2:0.#}\" y=\"{H - 34 - bh:0.#}\" font-size=\"10\" text-anchor=\"middle\">{h[i].Count}</text>")); }
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{W}\" height=\"{H}\" font-family=\"sans-serif\"><text x=\"20\" y=\"18\" font-size=\"14\">{System.Security.SecurityElement.Escape(title)}</text>{sb}</svg>";
    }

    /// <summary>F10: Konfidenzintervall des Mittelwerts (t-Verteilung).</summary>
    public static (double Mean, double Low, double High, double Half) Confidence(double[] v, double level = 0.95)
    {
        if (v.Length < 2) throw new ArgumentException("Mindestens 2 Werte.");
        var s = Stats.Describe("x", v); var h = TCrit(v.Length - 1, 1 - level) * s.StdDev / Math.Sqrt(v.Length); return (s.Mean, s.Mean - h, s.Mean + h, h);
    }

    /// <summary>Regularisierte obere Gammafunktion Q(a,x).</summary>
    public static double GammaQ(double a, double x)
    {
        if (x <= 0) return 1;
        var lg = Stats.LogGamma(a);
        if (x < a + 1) { double sum = 1 / a, del = sum, ap = a; for (var n = 0; n < 500; n++) { ap++; del *= x / ap; sum += del; if (Math.Abs(del) < Math.Abs(sum) * 1e-14) break; } return 1 - sum * Math.Exp(-x + a * Math.Log(x) - lg); }
        double b = x + 1 - a, c = 1e300, d = 1 / b, h = d;
        for (var i = 1; i < 500; i++) { var an = -i * (i - a); b += 2; d = an * d + b; if (Math.Abs(d) < 1e-300) d = 1e-300; c = b + an / c; if (Math.Abs(c) < 1e-300) c = 1e-300; d = 1 / d; var del = d * c; h *= del; if (Math.Abs(del - 1) < 1e-14) break; }
        return Math.Exp(-x + a * Math.Log(x) - lg) * h;
    }

    /// <summary>F11: Chi²-Unabhängigkeitstest für eine r×c-Tafel (ohne Korrektur).</summary>
    public static (double Chi2, int Df, double P, bool Significant, bool LowExpected) ChiSquare(double[][] t, double alpha = 0.05)
    {
        int r = t.Length, c = t[0].Length; if (r < 2 || c < 2 || t.Any(x => x.Length != c) || t.SelectMany(x => x).Any(x => x < 0)) throw new ArgumentException("Mindestens 2×2 Felder mit nicht-negativen Zahlen.");
        var n = t.SelectMany(x => x).Sum(); double chi = 0; var low = false;
        for (var i = 0; i < r; i++) for (var j = 0; j < c; j++) { var e = t[i].Sum() * t.Sum(x => x[j]) / n; if (e < 5) low = true; chi += (t[i][j] - e) * (t[i][j] - e) / e; }
        var df = (r - 1) * (c - 1); var p = GammaQ(df / 2.0, chi / 2); return (chi, df, p, p < alpha, low);
    }

    /// <summary>F12: Fehlerfortpflanzung (Gauß): Summe/Differenz addiert absolute, Produkt/Quotient relative Unsicherheiten quadratisch.</summary>
    public static (double Value, double Uncertainty) Propagate(char op, (double V, double U) a, (double V, double U) b)
    {
        switch (op)
        {
            case '+': return (a.V + b.V, Math.Sqrt(a.U * a.U + b.U * b.U));
            case '-': return (a.V - b.V, Math.Sqrt(a.U * a.U + b.U * b.U));
            case '*': case '/':
                if (a.V == 0 || b.V == 0) throw new ArgumentException("Bei Produkt/Quotient dürfen die Werte nicht 0 sein.");
                var v = op == '*' ? a.V * b.V : a.V / b.V; return (v, Math.Abs(v) * Math.Sqrt(Math.Pow(a.U / a.V, 2) + Math.Pow(b.U / b.V, 2)));
            default: throw new ArgumentException("Rechenart: + − * /");
        }
    }

    /// <summary>F13: Stichprobengröße für Mittelwert: n = (z·σ/E)²; Anteil: n = z²p(1−p)/E².</summary>
    public static int SampleSize(double sigma, double margin, double confidence = 0.95) { if (margin <= 0 || sigma <= 0) throw new ArgumentException("Streuung und Genauigkeit müssen größer als 0 sein."); return (int)Math.Ceiling(Math.Pow(Z(confidence) * sigma / margin, 2)); }
    public static int SampleSizeShare(double margin, double p = 0.5, double confidence = 0.95) => (int)Math.Ceiling(Z(confidence) * Z(confidence) * p * (1 - p) / (margin * margin));
    public static double Z(double c) => c switch { 0.9 => 1.6449, 0.95 => 1.96, 0.99 => 2.5758, _ => throw new ArgumentException("Sicherheit 90, 95 oder 99 %.") };

    /// <summary>F14: Streudiagramm mit Ausgleichsgerade.</summary>
    public static string ScatterSvg(double[] x, double[] y, string title)
    {
        var r = Stats.Linear(x, y); const int W = 600, H = 400, P = 40; double x0 = x.Min(), x1 = x.Max(), y0 = Math.Min(y.Min(), r.Intercept + r.Slope * x0), y1 = Math.Max(y.Max(), r.Intercept + r.Slope * x1);
        double X(double v) => P + (v - x0) / Math.Max(1e-12, x1 - x0) * (W - 2 * P); double Y(double v) => H - P - (v - y0) / Math.Max(1e-12, y1 - y0) * (H - 2 * P);
        var sb = new StringBuilder(); for (var i = 0; i < x.Length; i++) sb.Append(FormattableString.Invariant($"<circle cx=\"{X(x[i]):0.#}\" cy=\"{Y(y[i]):0.#}\" r=\"4\" fill=\"#3b82f6\"/>"));
        sb.Append(FormattableString.Invariant($"<line x1=\"{X(x0):0.#}\" y1=\"{Y(r.Intercept + r.Slope * x0):0.#}\" x2=\"{X(x1):0.#}\" y2=\"{Y(r.Intercept + r.Slope * x1):0.#}\" stroke=\"#ef4444\" stroke-width=\"2\"/>"));
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{W}\" height=\"{H}\" font-family=\"sans-serif\"><text x=\"{P}\" y=\"20\" font-size=\"14\">{System.Security.SecurityElement.Escape(title)} (R² = {r.R2.ToString("0.000", Fach.De)})</text>{sb}</svg>";
    }

    /// <summary>F15: gleitender Mittelwert (zentriert, Fenster ungerade).</summary>
    public static double?[] MovingAverage(double[] v, int window)
    {
        if (window < 1 || window % 2 == 0) throw new ArgumentException("Fenster bitte ungerade (3, 5, 7 …).");
        var h = window / 2; return v.Select((_, i) => i < h || i + h >= v.Length ? (double?)null : v.Skip(i - h).Take(window).Average()).ToArray();
    }

    /// <summary>F16: z-Werte (x − Mittel)/Streuung.</summary>
    public static double[] ZScores(double[] v) { var s = Stats.Describe("x", v); if (s.StdDev == 0) throw new ArgumentException("Alle Werte gleich – keine z-Werte."); return v.Select(x => (x - s.Mean) / s.StdDev).ToArray(); }

    /// <summary>Neue Spalte an die CSV anhängen (für F15/F16).</summary>
    public static string AppendColumn(string csv, string name, IReadOnlyList<double?> values, int sourceColumn)
    {
        var lines = csv.Replace("\r", "").Split('\n').Where(l => l.Trim() != "").ToList(); var sep = lines[0].Contains('\t') ? '\t' : lines[0].Contains(';') ? ';' : ',';
        var sb = new StringBuilder(lines[0] + sep + name + "\n"); var k = 0;
        foreach (var l in lines.Skip(1))
        {
            var c = l.Split(sep); var has = sourceColumn < c.Length && c[sourceColumn].Trim() != "" && double.TryParse(c[sourceColumn].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            var val = has && k < values.Count ? values[k++] : null;
            sb.Append(l + sep + (val is { } x ? x.ToString("0.####", sep == ';' ? Fach.De : CultureInfo.InvariantCulture) : "") + "\n");
        }
        return sb.ToString();
    }
}
