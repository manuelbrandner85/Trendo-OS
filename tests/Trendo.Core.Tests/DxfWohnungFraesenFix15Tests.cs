using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix15: Befunde der fünfzehnten Prüfung – Stoß-Gruppen verschmelzen über Spalte, Schutzbereich (Hülle) je Stoß-Komponente,
/// Union-Find mit Pfadkürzung, Stoß-Erkennung unabhängig von der Reihenfolge.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix15FuenfzehntePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities) => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1) => G((0, "LINE"), (8, "0"), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Lw(IEnumerable<(double X, double Y)> pts)
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, "0"), (90, p.Count), (70, 1)));
        foreach (var q in p) sb.Append(G((10, q.X), (20, q.Y)));
        return sb.ToString();
    }
    static string Hatch(IEnumerable<(double X, double Y)> loop)
    {
        var q = loop.ToList(); var sb = new StringBuilder(G((0, "HATCH"), (8, "0"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, 1), (92, 3), (72, 0), (73, 1), (93, q.Count)));
        foreach (var v in q) sb.Append(G((10, v.X), (20, v.Y)));
        return sb.Append(G((97, 0), (75, 0), (76, 1), (98, 0))).ToString();
    }
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }
    static (CamResult? R, string Text) CamOrNothing(string dxf)
    {
        try { var r = Cam(dxf); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool MilledOutside(CamResult r, double x, double y, double rr) =>
        r.Paths.Any(p => p.Kind == "aussen" && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - 0.5) < 0.05));
    static long Timed(Action a)
    {
        var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds;
        if (t > 2000) { sw.Restart(); a(); t = Math.Min(t, sw.ElapsedMilliseconds); }
        return t;
    }

    static string TwoHalves(double g) =>
        Line(50 - g, 60, 0, 60) + Line(0, 60, 0, 30) + Line(0, 30, 0, 0) + Line(0, 0, 50 - g, 0) + Line(0, 30, 10, 30)
        + Line(50 + g, 0, 100, 0) + Line(100, 0, 100, 30) + Line(100, 30, 100, 60) + Line(100, 60, 50 + g, 60) + Line(100, 30, 90, 30);

    [Fact(DisplayName = "T-0747-fix15 HOCH 1/2: zwei Stoß-Gruppen direkt über Spalt 0 … 0,11 mm (auch über der Verkettungstoleranz) – kein Loch außen gefräst")]
    public void Junction_groups_touch_over_gap()
    {
        foreach (var g in new[] { 0.0, 0.00001, 0.001, 0.005, 0.05, 0.0999, 0.11 })
        {
            var (r, text) = CamOrNothing(Dxf(TwoHalves(g) + Circle(50, 30, 5) + Circle(50, 50, 3) + Circle(25, 15, 2)));
            log.WriteLine($"Spalt {g}: {text[..Math.Min(300, text.Length)]}");
            if (r is not null) foreach (var (x, y, rr) in new[] { (50.0, 30.0, 5.0), (50, 50, 3), (25, 15, 2) }) Assert.False(MilledOutside(r, x, y, rr), $"Spalt {g}, Loch {x}/{y}");
        }
        // Kontrolle: Platte als Polylinie um alles – ein Loch weit weg wird weiter gefräst, die Platte bleibt
        var ok = Cam(Dxf(Lw([(-50, -50), (300, -50), (300, 150), (-50, 150)]) + TwoHalves(0.11) + Circle(200, 50, 4)));
        Assert.Contains(ok.Paths, p => p.Kind == "innen");
    }

    static IEnumerable<int[]> Perms(int[] a)
    {
        if (a.Length <= 1) { yield return a; yield break; }
        for (var i = 0; i < a.Length; i++)
            foreach (var rest in Perms([.. a.Take(i), .. a.Skip(i + 1)])) yield return [a[i], .. rest];
    }

    [Fact(DisplayName = "T-0747-fix15 MITTEL 4: drei Enden um 0,9 × Verbindungsgrenze versetzt – in allen 120 Reihenfolgen gleiches Ergebnis, kein Loch außen")]
    public void Near_junction_order_independent()
    {
        const double U = 1e-6, X = 1000, Y = 500; // Versatz 0,9 × Verbindungsgrenze (1e-6), wie in der Prüfung an versetzten Koordinaten
        double[] offs = [-0.9 * U, 0, 0.9 * U];
        string? reference = null;
        foreach (var perm in Perms([0, 1, 2]))
        {
            double oa = offs[perm[0]], od = offs[perm[1]], oe = offs[perm[2]];
            string[] pcs = [Line(X + oa, Y, X + 100, Y), Line(X + 100, Y, X + 100, Y + 60), Line(X + 100, Y + 60, X + oa, Y + 60), Line(X + od, Y + 60, X + od, Y), Line(X + oe, Y, X + 30, Y + 30) + Line(X + 30, Y + 30, X + oe, Y + 60)];
            foreach (var order in Perms([0, 1, 2, 3, 4]))
            {
                var (r, text) = CamOrNothing(Dxf(string.Concat(order.Select(k => pcs[k])) + Circle(X + 5, Y + 30, 2) + Circle(X + 70, Y + 30, 4)));
                if (r is not null) { Assert.False(MilledOutside(r, X + 5, Y + 30, 2), string.Join("", order)); Assert.False(MilledOutside(r, X + 70, Y + 30, 4), string.Join("", order)); }
                var key = r is null ? "nichts" : $"{r.Paths.Count}:{string.Join(",", r.Paths.Select(p => p.Kind).OrderBy(k => k))}";
                reference ??= key;
                Assert.True(reference == key, $"Reihenfolge {string.Join("", order)} (Versatz {string.Join("", perm)}): {key} statt {reference}");
            }
        }
        log.WriteLine($"Ergebnis in allen Reihenfolgen: {reference}");
    }

    static string Kette(int n)
    {
        var sb = new StringBuilder();
        for (var k = 0; k < n; k++) { var x = 10.0 * k; sb.Append(Line(x, 0, x + 4, 0)).Append(Line(x, 0, x, 5)).Append(Line(x, 0, x, -5)).Append(Line(x + 4.05, 0, x + 9.95, 0)); }
        return sb.ToString();
    }

    [Fact(DisplayName = "T-0747-fix15 MITTEL 3: Stoßkette 50 000 und 20 000 mit einer Schraffur – Lesen und Konturen unter 10 s, Abbruch schnell")]
    public void Junction_chain_fast()
    {
        var u1 = Dxf(Kette(50_000));
        var t1 = Timed(() => CncCam.Contours(CadDxf.Parse(u1).Drawing));
        (double, double)[] sq = [(0, -100), (20, -100), (20, -80), (0, -80)];
        var u6 = Dxf(Kette(20_000) + Lw(sq) + Hatch(sq));
        var f6 = WriteTemp(u6); long t2;
        try { t2 = Timed(() => CadDxf.Read(f6)); } finally { File.Delete(f6); }
        log.WriteLine($"u1 50 000: {t1} ms; u6 20 000: {t2} ms");
        Assert.True(t1 < 10000, $"u1 {t1} ms"); Assert.True(t2 < 10000, $"u6 {t2} ms");
        // Abbruch: muss mit OperationCanceledException enden, Nachlauf unter 1 s (mehrere Versuche gegen Fremdlast)
        long lag = long.MaxValue;
        for (var attempt = 0; attempt < 5 && lag >= 1000; attempt++)
        {
            using var cts = new CancellationTokenSource(); using var go = new ManualResetEventSlim(false); var sw = new Stopwatch(); long at = 0;
            var th = new Thread(() => { go.Wait(); Thread.Sleep((int)Math.Max(10, t1 / 3)); at = sw.ElapsedMilliseconds; cts.Cancel(); }) { IsBackground = true }; th.Start();
            sw.Start(); go.Set();
            try { CncCam.Contours(CadDxf.Parse(u1, cts.Token).Drawing, ct: cts.Token); th.Join(); continue; }
            catch (OperationCanceledException) { th.Join(); lag = Math.Min(lag, sw.ElapsedMilliseconds - at); }
        }
        log.WriteLine($"Abbruch-Nachlauf {lag} ms");
        Assert.True(lag < 1000, $"Nachlauf {lag} ms");
    }

    static string WriteTemp(string dxf)
    {
        var f = Path.Combine(Path.GetTempPath(), $"t0747fix15-{Guid.NewGuid():N}.dxf"); File.WriteAllText(f, dxf);
        return f;
    }
}
