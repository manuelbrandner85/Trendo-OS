using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core.Arch;

namespace Trendo.Core.Messdaten;

/// <summary>T-0261 (SPEC-05): Messdaten-Blatt – Spalten mit Name, Einheit und Werten, dazu die Diagramm-Einstellung.</summary>
public sealed class MeasureSheet
{
    public const int FormatVersion = 1;
    public int Version { get; set; } = FormatVersion;
    public string Name { get; set; } = "Messreihe";
    public List<MColumn> Columns { get; set; } = [];
    public ChartSpec Chart { get; set; } = new();

    static readonly JsonSerializerOptions Opt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public static MeasureSheet From(JsonObject data) => data["sheet"] is JsonObject o ? o.Deserialize<MeasureSheet>(Opt) ?? new() : new();
    public void WriteTo(JsonObject data) => data["sheet"] = JsonSerializer.SerializeToNode(this, Opt);

    public int Rows => Columns.Count == 0 ? 0 : Columns.Max(c => c.Values.Count);
    public double? Get(int col, int row) => col < Columns.Count && row < Columns[col].Values.Count ? Columns[col].Values[row] : null;

    public void Set(int col, int row, double? v)
    {
        if (col < 0 || col >= Columns.Count || row < 0) throw new ArgumentOutOfRangeException(nameof(col), "Diese Zelle gibt es nicht.");
        var l = Columns[col].Values; while (l.Count <= row) l.Add(null); l[row] = v;
        // leere Zeilen am Ende entfernen
        while (Rows > 0 && Columns.All(x => x.Values.Count < Rows || x.Values[Rows - 1] is null)) { var r = Rows - 1; foreach (var x in Columns) if (x.Values.Count > r) x.Values.RemoveAt(r); }
    }

    /// <summary>Zahl aus einer Zelleingabe (deutsches Komma, leer = keine Messung).</summary>
    public static double? Parse(string s)
    {
        s = s.Trim(); if (s == "") return null;
        if (double.TryParse(s.Replace(" ", ""), NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out var v) || double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
        throw new FormatException($"„{s}“ ist keine Zahl.");
    }

    public MColumn AddColumn(string name, string unit = "") { var c = new MColumn { Name = Unique(name), Unit = unit, Values = Enumerable.Repeat<double?>(null, Rows).ToList() }; Columns.Add(c); return c; }
    string Unique(string n) { var b = string.IsNullOrWhiteSpace(n) ? "Spalte" : n.Trim(); var r = b; for (var i = 2; Columns.Any(c => c.Name == r); i++) r = $"{b} {i}"; return r; }

    public double[] Values(int col) => col >= 0 && col < Columns.Count ? Columns[col].Values.Where(v => v.HasValue).Select(v => v!.Value).ToArray() : [];
    public (double[] X, double[] Y) Pairs(int xc, int yc)
    {
        var n = Math.Min(Columns[xc].Values.Count, Columns[yc].Values.Count); var xs = new List<double>(); var ys = new List<double>();
        for (var i = 0; i < n; i++) if (Columns[xc].Values[i] is { } x && Columns[yc].Values[i] is { } y) { xs.Add(x); ys.Add(y); }
        return (xs.ToArray(), ys.ToArray());
    }

    // ---------- CSV ----------
    public static MeasureSheet FromCsv(string csv, string name = "Messreihe")
    {
        var lines = csv.Replace("\r", "").Split('\n').Where(l => l.Trim() != "").ToList();
        if (lines.Count < 1) throw new FormatException("Die Datei ist leer.");
        var sep = lines[0].Contains('\t') ? '\t' : lines[0].Contains(';') ? ';' : ',';
        var s = new MeasureSheet { Name = name };
        foreach (var h in lines[0].Split(sep))
        {
            var t = h.Trim().Trim('"'); var unit = "";
            var m = System.Text.RegularExpressions.Regex.Match(t, @"^(.*?)\s*[\[(]([^\])]+)[\])]$"); if (m.Success) { t = m.Groups[1].Value; unit = m.Groups[2].Value; }
            s.Columns.Add(new MColumn { Name = t, Unit = unit });
        }
        foreach (var l in lines.Skip(1))
        {
            var c = l.Split(sep);
            for (var i = 0; i < s.Columns.Count; i++) { double? v = null; if (i < c.Length) try { v = Parse(c[i].Trim('"')); } catch (FormatException) { v = null; } s.Columns[i].Values.Add(v); }
        }
        if (s.Columns.Count >= 2) s.Chart = new ChartSpec { X = 0, Ys = [1] };
        return s;
    }

    public string ToCsv()
    {
        var de = CultureInfo.GetCultureInfo("de-DE"); var sb = new StringBuilder(string.Join(';', Columns.Select(c => c.Unit.Length > 0 ? $"{c.Name} [{c.Unit}]" : c.Name))).Append('\n');
        for (var r = 0; r < Rows; r++) sb.Append(string.Join(';', Columns.Select(c => r < c.Values.Count && c.Values[r] is { } v ? v.ToString("0.##########", de) : ""))).Append('\n');
        return sb.ToString();
    }

    // ---------- Auswertung ----------
    public string Report()
    {
        var de = CultureInfo.GetCultureInfo("de-DE"); string F(double v) => v.ToString("0.####", de);
        var sb = new StringBuilder($"# Auswertung – {Name}\n\n| Spalte | n | Mittelwert | Median | Streuung | Min | Max | 95 %-Bereich |\n|---|---:|---:|---:|---:|---:|---:|---|\n");
        foreach (var (c, i) in Columns.Select((c, i) => (c, i)))
        {
            var v = Values(i); if (v.Length == 0) continue; var d = Stats.Describe(c.Name, v);
            var ci = v.Length > 1 ? StatsPlus.Confidence(v) : (d.Mean, d.Mean, d.Mean, 0);
            sb.Append($"| {c.Name}{(c.Unit.Length > 0 ? $" [{c.Unit}]" : "")} | {d.N} | {F(d.Mean)} | {F(d.Median)} | {F(d.StdDev)} | {F(d.Min)} | {F(d.Max)} | {F(ci.Low)} … {F(ci.High)} |\n");
        }
        if (Chart.X >= 0 && Chart.X < Columns.Count && Chart.Ys.Count > 0 && Chart.Fit != "keine")
            foreach (var y in Chart.Ys.Where(y => y < Columns.Count))
                if (Fitting.Fit(this, Chart.X, y, Chart.Fit) is { } f) sb.Append($"\n**{Columns[y].Name} über {Columns[Chart.X].Name}:** {f.Formula} (R² = {F(f.R2)})\n");
        return sb.ToString();
    }

    // ---------- Vorlagen ----------
    public static MeasureSheet Template(string id)
    {
        var s = new MeasureSheet();
        switch (id)
        {
            case "pendel":
                s.Name = "Fadenpendel";
                s.Columns.Add(new MColumn { Name = "Länge", Unit = "m", Values = [0.2, 0.4, 0.6, 0.8, 1.0, 1.2, 1.4] });
                s.Columns.Add(new MColumn { Name = "Periode", Unit = "s", Values = [0.91, 1.26, 1.56, 1.79, 2.01, 2.20, 2.38] });
                s.Chart = new ChartSpec { Kind = "streu", X = 0, Ys = [1], Fit = "potenz" };
                break;
            case "duenger":
                s.Name = "Pflanzenwachstum mit zwei Düngern";
                s.Columns.Add(new MColumn { Name = "Dünger A", Unit = "cm", Values = [12.1, 13.4, 11.8, 12.9, 13.1, 12.4, 12.7, 13.6] });
                s.Columns.Add(new MColumn { Name = "Dünger B", Unit = "cm", Values = [14.2, 13.9, 15.1, 14.6, 13.8, 14.9, 15.3, 14.4] });
                s.Chart = new ChartSpec { Kind = "box", X = -1, Ys = [0, 1], Fit = "keine" };
                break;
            case "abkuehlen":
                s.Name = "Abkühlung einer Tasse Tee";
                s.Columns.Add(new MColumn { Name = "Zeit", Unit = "min", Values = [0, 2, 4, 6, 8, 10, 15, 20, 25, 30] });
                s.Columns.Add(new MColumn { Name = "Temperatur über Raum", Unit = "K", Values = [68, 58.5, 50.2, 43.6, 37.4, 32.2, 22.8, 16.4, 11.6, 8.3] });
                s.Chart = new ChartSpec { Kind = "streu", X = 0, Ys = [1], Fit = "exp" };
                break;
            default:
                s.Columns.Add(new MColumn { Name = "x", Values = [null] }); s.Columns.Add(new MColumn { Name = "y", Values = [null] });
                s.Chart = new ChartSpec { X = 0, Ys = [1] };
                break;
        }
        return s;
    }
}

public sealed class MColumn
{
    public string Name { get; set; } = "Spalte";
    public string Unit { get; set; } = "";
    public List<double?> Values { get; set; } = [];
}

public sealed class ChartSpec
{
    /// <summary>streu, linie, balken, histogramm, box.</summary>
    public string Kind { get; set; } = "streu";
    public int X { get; set; } = 0;
    public List<int> Ys { get; set; } = [1];
    /// <summary>keine, linear, quadratisch, exp, potenz.</summary>
    public string Fit { get; set; } = "linear";
}

public sealed record FitResult(string Kind, Func<double, double> F, double R2, string Formula);

/// <summary>Ausgleichskurven nach der Methode der kleinsten Quadrate.</summary>
public static class Fitting
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static string N(double v) => Math.Abs(v) >= 1e4 || Math.Abs(v) < 1e-3 && v != 0 ? v.ToString("0.###E+0", De) : v.ToString("0.####", De);

    public static FitResult? Fit(MeasureSheet s, int xc, int yc, string kind)
    {
        if (xc < 0 || yc < 0 || xc >= s.Columns.Count || yc >= s.Columns.Count) return null;
        var (x, y) = s.Pairs(xc, yc); if (x.Length < 3) return null; // unter 3 Wertepaaren ist eine Ausgleichskurve nicht aussagekräftig
        try { return FitCore(x, y, kind); }
        catch (ArgumentException) { return null; } // z. B. alle x gleich
    }

    static FitResult? FitCore(double[] x, double[] y, string kind)
    {
        switch (kind)
        {
            case "linear":
                { var r = Stats.Linear(x, y); return new("linear", v => r.Slope * v + r.Intercept, r.R2, $"y = {N(r.Slope)}·x {(r.Intercept >= 0 ? "+" : "−")} {N(Math.Abs(r.Intercept))}"); }
            case "quadratisch" when x.Length >= 3:
                {
                    // Normalgleichungen 3×3
                    double S(int p) => x.Sum(v => Math.Pow(v, p)); double T(int p) => x.Zip(y, (a, b) => Math.Pow(a, p) * b).Sum();
                    var A = new[,] { { S(4), S(3), S(2) }, { S(3), S(2), S(1) }, { S(2), S(1), x.Length } }; var B = new[] { T(2), T(1), T(0) };
                    var c = Solve3(A, B); if (c is null) return null;
                    Func<double, double> f = v => c[0] * v * v + c[1] * v + c[2];
                    return new("quadratisch", f, R2(x, y, f), $"y = {N(c[0])}·x² {(c[1] >= 0 ? "+" : "−")} {N(Math.Abs(c[1]))}·x {(c[2] >= 0 ? "+" : "−")} {N(Math.Abs(c[2]))}");
                }
            case "exp" when y.All(v => v > 0):
                { var r = Stats.Linear(x, y.Select(v => Math.Log(v)).ToArray()); var a = Math.Exp(r.Intercept); var b = r.Slope; Func<double, double> f = v => a * Math.Exp(b * v); return new("exp", f, R2(x, y, f), $"y = {N(a)}·e^({N(b)}·x)"); }
            case "potenz" when x.All(v => v > 0) && y.All(v => v > 0):
                { var r = Stats.Linear(x.Select(v => Math.Log(v)).ToArray(), y.Select(v => Math.Log(v)).ToArray()); var a = Math.Exp(r.Intercept); var b = r.Slope; Func<double, double> f = v => a * Math.Pow(v, b); return new("potenz", f, R2(x, y, f), $"y = {N(a)}·x^{N(b)}"); }
        }
        return null;
    }

    static double R2(double[] x, double[] y, Func<double, double> f)
    {
        var m = y.Average(); var ssTot = y.Sum(v => (v - m) * (v - m)); var ssRes = x.Zip(y, (a, b) => (b - f(a)) * (b - f(a))).Sum();
        return ssTot <= 0 ? 1 : 1 - ssRes / ssTot;
    }

    static double[]? Solve3(double[,] a, double[] b)
    {
        double D(double[,] m) => m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
        var d = D(a); if (Math.Abs(d) < 1e-12) return null;
        var r = new double[3];
        for (var k = 0; k < 3; k++) { var m = (double[,])a.Clone(); for (var i = 0; i < 3; i++) m[i, k] = b[i]; r[k] = D(m) / d; }
        return r;
    }
}

/// <summary>Diagramm als Zeichenelemente in Bildpunkten (für Bildschirm und SVG gleich).</summary>
public static class ChartDrawing
{
    public static readonly string[] Palette = ["#2F86F6", "#F59E0B", "#10B981", "#EF4444", "#8B5CF6", "#06B6D4"];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    static double Nice(double span, int ticks = 6)
    {
        var raw = span / ticks; var mag = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(raw, 1e-12)))); var f = raw / mag;
        return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * mag;
    }
    static string Tick(double v, double step) => v.ToString(step >= 1 ? "0.##" : step >= 0.01 ? "0.###" : "0.#####", De);

    public static List<Shape> Build(MeasureSheet s, double w, double h, string text = "#1A1A1A", string grid = "#22000000")
    {
        var sh = new List<Shape>(); var c = s.Chart;
        double L = 64, R = 18, T = 36, B = 52;
        var ys = c.Ys.Where(i => i >= 0 && i < s.Columns.Count).ToList();
        if (ys.Count == 0) { sh.Add(new DText(w / 2, h / 2, "Keine Spalte für das Diagramm gewählt", 13, text, "TEXT")); return sh; }
        var title = c.Kind is "histogramm" or "box" ? string.Join(", ", ys.Select(i => s.Columns[i].Name)) : $"{string.Join(", ", ys.Select(i => s.Columns[i].Name))} über {(c.X >= 0 && c.X < s.Columns.Count ? s.Columns[c.X].Name : "Nr.")}";
        sh.Add(new DText(w / 2, 16, title, 14, text, "TEXT", true));
        // Wertebereiche
        List<(double X, double Y, int S)> pts = [];
        double x0, x1, y0, y1; string xLabel, yLabel;
        if (c.Kind == "histogramm")
        {
            var v = s.Values(ys[0]); if (v.Length == 0) return sh;
            var bins = StatsPlus.Histogram(v); x0 = bins[0].From; x1 = bins[^1].To; y0 = 0; y1 = bins.Max(b => b.Count) * 1.1;
            xLabel = Axis(s.Columns[ys[0]]); yLabel = "Anzahl";
            Frame(); foreach (var b in bins) { var (ax, ay) = P(b.From, b.Count); var (bx, by) = P(b.To, 0); sh.Add(new DPoly([(ax + 1, ay), (bx - 1, ay), (bx - 1, by), (ax + 1, by)], Palette[0] + "CC", Palette[0], 1, "DATEN")); }
            return sh;
        }
        if (c.Kind == "box")
        {
            var groups = ys.Select(i => (i, v: s.Values(i))).Where(g => g.v.Length > 0).ToList(); if (groups.Count == 0) return sh;
            x0 = 0; x1 = groups.Count; y0 = groups.Min(g => g.v.Min()); y1 = groups.Max(g => g.v.Max()); var pad = (y1 - y0) * 0.1 + 1e-9; y0 -= pad; y1 += pad;
            xLabel = ""; yLabel = Axis(s.Columns[groups[0].i]);
            Frame(xTicks: false);
            for (var k = 0; k < groups.Count; k++)
            {
                var (q1, q2, q3, iqr, outl) = StatsPlus.Quartiles(groups[k].v); var lo = groups[k].v.Where(v => v >= q1 - 1.5 * iqr).Min(); var hi = groups[k].v.Where(v => v <= q3 + 1.5 * iqr).Max();
                var col = Palette[k % Palette.Length]; var (cx, _) = P(k + 0.5, 0); var bw = (w - L - R) / groups.Count * 0.35;
                var (_, yq1) = P(0, q1); var (_, yq3) = P(0, q3); var (_, yq2) = P(0, q2); var (_, ylo) = P(0, lo); var (_, yhi) = P(0, hi);
                sh.Add(new DLine(cx, ylo, cx, yq1, col, 1.5, "DATEN")); sh.Add(new DLine(cx, yq3, cx, yhi, col, 1.5, "DATEN"));
                sh.Add(new DLine(cx - bw / 2, ylo, cx + bw / 2, ylo, col, 1.5, "DATEN")); sh.Add(new DLine(cx - bw / 2, yhi, cx + bw / 2, yhi, col, 1.5, "DATEN"));
                sh.Add(new DPoly([(cx - bw, yq3), (cx + bw, yq3), (cx + bw, yq1), (cx - bw, yq1)], col + "55", col, 1.8, "DATEN"));
                sh.Add(new DLine(cx - bw, yq2, cx + bw, yq2, col, 3, "DATEN"));
                foreach (var o in outl) { var (_, oy) = P(0, o); sh.Add(new DPoly(Circle(cx, oy, 3.5), null, col, 1.5, "DATEN")); }
                sh.Add(new DText(cx, h - B + 18, s.Columns[groups[k].i].Name, 12, text, "TEXT", true));
            }
            return sh;
        }
        // Streu/Linie/Balken
        var hasX = c.X >= 0 && c.X < s.Columns.Count;
        foreach (var (yi, k) in ys.Select((v, k) => (v, k)))
        {
            if (hasX) { var (x, y) = s.Pairs(c.X, yi); for (var i = 0; i < x.Length; i++) pts.Add((x[i], y[i], k)); }
            else { var v = s.Columns[yi].Values; for (var i = 0; i < v.Count; i++) if (v[i] is { } y) pts.Add((i + 1, y, k)); }
        }
        if (pts.Count == 0) { sh.Add(new DText(w / 2, h / 2, "Noch keine Werte – links in die Tabelle tippen", 13, text, "TEXT")); return sh; }
        x0 = pts.Min(p => p.X); x1 = pts.Max(p => p.X); y0 = Math.Min(c.Kind == "balken" ? 0 : double.MaxValue, pts.Min(p => p.Y)); y1 = pts.Max(p => p.Y);
        if (x1 - x0 < 1e-12) { x0 -= 1; x1 += 1; } if (y1 - y0 < 1e-12) { y0 -= 1; y1 += 1; }
        var px = (x1 - x0) * 0.05; var py = (y1 - y0) * 0.08; x0 -= px; x1 += px; if (c.Kind != "balken" || y0 < 0) y0 -= py; y1 += py;
        xLabel = hasX ? Axis(s.Columns[c.X]) : "Nr."; yLabel = ys.Count == 1 ? Axis(s.Columns[ys[0]]) : string.Join(" / ", ys.Select(i => s.Columns[i].Name));
        Frame();
        foreach (var (yi, k) in ys.Select((v, k) => (v, k)))
        {
            var col = Palette[k % Palette.Length]; var my = pts.Where(p => p.S == k).OrderBy(p => p.X).ToList();
            if (c.Kind == "linie" && my.Count > 1) sh.Add(new DPoly(my.Select(p => P(p.X, p.Y)).ToList(), null, col, 2.2, "DATEN", null, false));
            if (c.Kind == "balken")
            {
                var bw = (w - L - R) / Math.Max(1, my.Count) / (ys.Count + 1) * 0.9;
                foreach (var p in my) { var (bx, by) = P(p.X, p.Y); var (_, b0) = P(0, Math.Max(y0, 0)); var off = (k - (ys.Count - 1) / 2.0) * bw; sh.Add(new DPoly([(bx + off - bw / 2, by), (bx + off + bw / 2, by), (bx + off + bw / 2, b0), (bx + off - bw / 2, b0)], col + "CC", col, 1, "DATEN")); }
            }
            else foreach (var p in my) { var (qx, qy) = P(p.X, p.Y); sh.Add(new DPoly(Circle(qx, qy, 4), col, "#FFFFFF", 1.2, "DATEN")); }
            if (hasX && c.Fit != "keine" && Fitting.Fit(s, c.X, yi, c.Fit) is { } fit)
            {
                var curve = Enumerable.Range(0, 120).Select(i => x0 + (x1 - x0) * i / 119).Select(xv => (xv, yv: fit.F(xv))).Where(q => double.IsFinite(q.yv) && q.yv >= y0 - (y1 - y0) && q.yv <= y1 + (y1 - y0)).Select(q => P(q.xv, Math.Clamp(q.yv, y0, y1))).ToList();
                if (curve.Count > 1) sh.Add(new DPoly(curve, null, col, 1.6, "FIT", null, false));
                sh.Add(new DText(w - R - 6, T + 14 + k * 18, $"{fit.Formula}   R² = {fit.R2.ToString("0.0000", De)}", 11.5, col, "FIT", true, 0, 0) with { X = w - R - 6 - (fit.Formula.Length + 14) * 3.1 });
            }
        }
        if (ys.Count > 1) foreach (var (yi, k) in ys.Select((v, k) => (v, k))) sh.Add(new DText(L + 60 + k * 120, h - 10, "■ " + s.Columns[yi].Name, 11, Palette[k % Palette.Length], "TEXT", true));
        return sh;

        (double, double) P(double x, double y) => (L + (x - x0) / (x1 - x0) * (w - L - R), T + (1 - (y - y0) / (y1 - y0)) * (h - T - B));
        void Frame(bool xTicks = true)
        {
            var xs = Nice(x1 - x0); var yss = Nice(y1 - y0);
            for (var v = Math.Ceiling(y0 / yss) * yss; v <= y1 + 1e-9; v += yss) { var (_, yy) = P(x0, v); sh.Add(new DLine(L, yy, w - R, yy, grid, 1, "RASTER")); sh.Add(new DText(L - 30, yy, Tick(v, yss), 11, text, "ACHSE")); }
            if (xTicks) for (var v = Math.Ceiling(x0 / xs) * xs; v <= x1 + 1e-9; v += xs) { var (xx, _) = P(v, y0); sh.Add(new DLine(xx, T, xx, h - B, grid, 1, "RASTER")); sh.Add(new DText(xx, h - B + 14, Tick(v, xs), 11, text, "ACHSE")); }
            sh.Add(new DLine(L, h - B, w - R, h - B, text, 1.2, "ACHSE")); sh.Add(new DLine(L, T, L, h - B, text, 1.2, "ACHSE"));
            if (xLabel.Length > 0) sh.Add(new DText((L + w - R) / 2, h - B + 32, xLabel, 12, text, "ACHSE", true));
            sh.Add(new DText(16, (T + h - B) / 2, yLabel, 12, text, "ACHSE", true, -90));
        }
    }

    static string Axis(MColumn c) => c.Unit.Length > 0 ? $"{c.Name} [{c.Unit}]" : c.Name;
    static List<(double X, double Y)> Circle(double x, double y, double r) => Enumerable.Range(0, 12).Select(k => (x + Math.Cos(k * Math.PI / 6) * r, y + Math.Sin(k * Math.PI / 6) * r)).ToList();

    public static string Svg(MeasureSheet s, double w = 900, double h = 560)
    {
        var I = CultureInfo.InvariantCulture; string N(double d) => d.ToString("0.##", I);
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(w)}\" height=\"{N(h)}\" viewBox=\"0 0 {N(w)} {N(h)}\" font-family=\"Segoe UI, Arial, sans-serif\"><rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>\n");
        static (string C, string A) Col(string c) => c.Length == 9 ? (c[..7], (Convert.ToInt32(c[7..], 16) / 255.0).ToString("0.##", CultureInfo.InvariantCulture)) : (c, "1");
        foreach (var x in Build(s, w, h))
            switch (x)
            {
                case DPoly p: { var f = p.Fill is { } ff ? Col(ff) : ("none", "1"); var st = p.Stroke is { } ss ? Col(ss) : ("none", "1"); sb.Append($"<{(p.Closed ? "polygon" : "polyline")} points=\"{string.Join(' ', p.Pts.Select(q => N(q.X) + "," + N(q.Y)))}\" fill=\"{f.Item1}\" fill-opacity=\"{f.Item2}\" stroke=\"{st.Item1}\" stroke-opacity=\"{st.Item2}\" stroke-width=\"{N(p.Width)}\"/>\n"); break; }
                case DLine l: { var st = Col(l.Stroke); sb.Append($"<line x1=\"{N(l.X1)}\" y1=\"{N(l.Y1)}\" x2=\"{N(l.X2)}\" y2=\"{N(l.Y2)}\" stroke=\"{st.C}\" stroke-opacity=\"{st.A}\" stroke-width=\"{N(l.Width)}\"/>\n"); break; }
                case DText t: sb.Append($"<text x=\"{N(t.X)}\" y=\"{N(t.Y)}\" font-size=\"{N(t.Size)}\" fill=\"{Col(t.Color).C}\" text-anchor=\"middle\" dominant-baseline=\"middle\"{(t.Bold ? " font-weight=\"600\"" : "")}{(t.AngleDeg != 0 ? $" transform=\"rotate({N(t.AngleDeg)} {N(t.X)} {N(t.Y)})\"" : "")}>{System.Security.SecurityElement.Escape(t.Text)}</text>\n"); break;
            }
        return sb.Append("</svg>\n").ToString();
    }
}
