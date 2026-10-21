using System.Text;

namespace Trendo.Core;

public sealed record ColumnStats(string Name, int N, double Mean, double Median, double StdDev, double Min, double Max);
public sealed record Regression(double Slope, double Intercept, double R, double R2, int N);
public sealed record TTest(double T, double Df, double P, double MeanA, double MeanB, bool Significant);

/// <summary>SPEC-05: Wissenschaft & Forschung – Messdaten (CSV) auswerten: Kennzahlen, Zusammenhang, Gruppenvergleich.</summary>
public sealed class DataTable
{
    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<double?[]> Rows { get; }
    private DataTable(List<string> c, List<double?[]> r) { Columns = c; Rows = r; }

    public static DataTable Parse(string csv)
    {
        var lines = csv.Replace("\r", "").Split('\n').Where(l => l.Trim() != "").ToList();
        if (lines.Count < 2) throw new FormatException("Die Tabelle braucht eine Kopfzeile und mindestens eine Datenzeile.");
        var sep = lines[0].Contains('\t') ? '\t' : lines[0].Contains(';') ? ';' : ',';
        var cols = lines[0].Split(sep).Select(s => s.Trim().Trim('"')).ToList();
        var rows = new List<double?[]>();
        foreach (var l in lines.Skip(1))
        {
            var c = l.Split(sep); var r = new double?[cols.Count];
            for (var i = 0; i < cols.Count && i < c.Length; i++) { var t = c[i].Trim().Trim('"'); if (t != "") try { r[i] = Fach.Num(t); } catch (FormatException) { r[i] = null; } }
            rows.Add(r);
        }
        return new(cols, rows);
    }

    public static DataTable Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : throw new IOException("Diese Datei gibt es nicht.");

    public int Index(string col)
    {
        var i = Columns.ToList().FindIndex(c => c.Equals(col.Trim(), StringComparison.OrdinalIgnoreCase));
        if (i < 0 && int.TryParse(col, out var n) && n >= 1 && n <= Columns.Count) i = n - 1;
        return i >= 0 ? i : throw new ArgumentException($"Spalte „{col}“ nicht gefunden. Vorhanden: {string.Join(", ", Columns)}");
    }

    public double[] Values(string col) { var i = Index(col); return Rows.Where(r => r[i].HasValue).Select(r => r[i]!.Value).ToArray(); }

    public IReadOnlyList<ColumnStats> Describe() => Columns.Select((c, i) => (c, v: Rows.Where(r => r[i].HasValue).Select(r => r[i]!.Value).ToArray())).Where(x => x.v.Length > 0).Select(x => Stats.Describe(x.c, x.v)).ToList();

    public Regression Fit(string xCol, string yCol)
    {
        int xi = Index(xCol), yi = Index(yCol);
        var pts = Rows.Where(r => r[xi].HasValue && r[yi].HasValue).Select(r => (r[xi]!.Value, r[yi]!.Value)).ToArray();
        return Stats.Linear(pts.Select(p => p.Item1).ToArray(), pts.Select(p => p.Item2).ToArray());
    }
}

public static class Stats
{
    public static ColumnStats Describe(string name, double[] v)
    {
        var s = v.OrderBy(x => x).ToArray(); var n = s.Length; var mean = s.Average();
        var sd = n > 1 ? Math.Sqrt(s.Sum(x => (x - mean) * (x - mean)) / (n - 1)) : 0;
        var med = n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
        return new(name, n, mean, med, sd, s[0], s[^1]);
    }

    public static Regression Linear(double[] x, double[] y)
    {
        var n = x.Length; if (n < 3) throw new ArgumentException("Für einen Zusammenhang braucht es mindestens 3 Wertepaare.");
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < n; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); syy += (y[i] - my) * (y[i] - my); }
        if (sxx == 0) throw new ArgumentException("Die x-Werte sind alle gleich.");
        var b = sxy / sxx; var r = syy == 0 ? 0 : sxy / Math.Sqrt(sxx * syy);
        return new(b, my - b * mx, r, r * r, n);
    }

    /// <summary>Welch-t-Test (zweiseitig) für zwei Gruppen mit ungleichen Varianzen.</summary>
    public static TTest Welch(double[] a, double[] b, double alpha = 0.05)
    {
        if (a.Length < 2 || b.Length < 2) throw new ArgumentException("Jede Gruppe braucht mindestens 2 Werte.");
        var da = Describe("a", a); var db = Describe("b", b);
        double va = da.StdDev * da.StdDev / a.Length, vb = db.StdDev * db.StdDev / b.Length;
        if (va + vb == 0) throw new ArgumentException("Beide Gruppen haben keine Streuung.");
        var t = (da.Mean - db.Mean) / Math.Sqrt(va + vb);
        var df = (va + vb) * (va + vb) / (va * va / (a.Length - 1) + vb * vb / (b.Length - 1));
        var p = BetaReg(df / 2, 0.5, df / (df + t * t));
        return new(t, df, p, da.Mean, db.Mean, p < alpha);
    }

    /// <summary>Regularisierte unvollständige Betafunktion I_x(a,b) (Kettenbruch nach Lentz).</summary>
    public static double BetaReg(double a, double b, double x)
    {
        if (x <= 0) return 0; if (x >= 1) return 1;
        var lbeta = LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x);
        if (x > (a + 1) / (a + b + 2)) return 1 - BetaReg(b, a, 1 - x);
        double f = 1, c = 1, d = 0;
        for (var i = 0; i <= 300; i++)
        {
            var m = i / 2; double num;
            if (i == 0) num = 1; else if (i % 2 == 0) num = m * (b - m) * x / ((a + 2 * m - 1) * (a + 2 * m)); else num = -((a + m) * (a + b + m) * x) / ((a + 2 * m) * (a + 2 * m + 1));
            d = 1 + num * d; if (Math.Abs(d) < 1e-30) d = 1e-30; d = 1 / d;
            c = 1 + num / c; if (Math.Abs(c) < 1e-30) c = 1e-30;
            var cd = c * d; f *= cd; if (Math.Abs(1 - cd) < 1e-12) break;
        }
        return Math.Exp(lbeta) * (f - 1) / a;
    }

    public static double LogGamma(double z)
    {
        double[] g = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
        double x = z, y = z, tmp = x + 5.5; tmp -= (x + 0.5) * Math.Log(tmp); var ser = 1.000000000190015;
        foreach (var c in g) ser += c / ++y;
        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }

    public static string Report(DataTable t, string? title = null)
    {
        var sb = new StringBuilder($"# {title ?? "Auswertung"}\n\nErstellt: {DateTime.Now:dd.MM.yyyy HH:mm}\n\n| Spalte | n | Mittelwert | Median | Std.-Abw. | Min | Max |\n|---|---|---|---|---|---|---|\n");
        foreach (var s in t.Describe()) sb.Append($"| {s.Name} | {s.N} | {Fach.F(s.Mean, 3)} | {Fach.F(s.Median, 3)} | {Fach.F(s.StdDev, 3)} | {Fach.F(s.Min, 3)} | {Fach.F(s.Max, 3)} |\n");
        return sb.ToString();
    }
}
