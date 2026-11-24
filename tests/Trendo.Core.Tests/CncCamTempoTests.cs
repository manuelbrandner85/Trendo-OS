using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0758: Laufzeit beim Fräsen aus DXF. Das Streckenraster liefert dieselben Treffer in derselben Reihenfolge wie vorher (Vergleich mit der
/// alten Umsetzung), ohne lange Strecken in jede Zelle ihres Rahmens einzutragen; dazu Zeit- und Abbruchgrenzen für die bekannten langsamen Fälle.</summary>
[Collection("T0747 Zeitmessung")]
public class T0758_CncCamTempo(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities) => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1) => G((0, "LINE"), (8, "0"), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Lw(IEnumerable<(double X, double Y)> pts)
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, "0"), (90, p.Count), (70, 1)));
        foreach (var q in p) sb.Append(G((10, Math.Round(q.X, 6)), (20, Math.Round(q.Y, 6))));
        return sb.ToString();
    }

    /// <summary>Die bisherige Umsetzung (bis 0.9.108): jede Strecke in jeder Zelle ihres Rahmens.</summary>
    sealed class OldGrid(double cell)
    {
        readonly double _cell = Math.Max(cell, 1e-9);
        readonly Dictionary<long, List<int>> _map = [];
        readonly List<int> _stamp = []; int _query;
        public int Count => _stamp.Count;
        static long Key(long x, long y) => (x << 32) ^ (y & 0xFFFFFFFFL);
        long C(double v) => (long)Math.Floor(v / _cell);
        public void Add((double X, double Y) a, (double X, double Y) b)
        {
            var i = _stamp.Count; _stamp.Add(0);
            for (var x = C(Math.Min(a.X, b.X)); x <= C(Math.Max(a.X, b.X)); x++)
                for (var y = C(Math.Min(a.Y, b.Y)); y <= C(Math.Max(a.Y, b.Y)); y++)
                {
                    var k = Key(x, y); if (!_map.TryGetValue(k, out var l)) _map[k] = l = []; l.Add(i);
                }
        }
        public List<int> Near(double minX, double minY, double maxX, double maxY)
        {
            _query++; var res = new List<int>();
            for (var x = C(minX); x <= C(maxX); x++)
                for (var y = C(minY); y <= C(maxY); y++)
                    if (_map.TryGetValue(Key(x, y), out var l))
                        foreach (var i in l) if (_stamp[i] != _query) { _stamp[i] = _query; res.Add(i); }
            return res;
        }
    }

    [Fact(DisplayName = "T-0758: Streckenraster – gleiche Treffer in gleicher Reihenfolge wie die alte Umsetzung (kurze, lange, schräge, waagrechte, senkrechte Strecken, negative Lage), Any bricht an derselben Stelle ab")]
    public void Grid_same_hits_same_order()
    {
        var rnd = new Random(758);
        foreach (var cell in new[] { 0.7, 3.0, 25.0 })
        {
            var neu = new SegGrid(cell); var alt = new OldGrid(cell);
            for (var i = 0; i < 1500; i++)
            {
                (double X, double Y) a = (rnd.NextDouble() * 400 - 150, rnd.NextDouble() * 400 - 150);
                var len = (i % 5) switch { 0 => rnd.NextDouble() * 2, 1 => rnd.NextDouble() * 30 * cell / 3, 2 => rnd.NextDouble() * 80 * cell, _ => rnd.NextDouble() * 40 * cell };
                var ang = (i % 7) switch { 0 => 0, 1 => Math.PI / 2, 2 => Math.PI / 4, _ => rnd.NextDouble() * 2 * Math.PI };
                (double X, double Y) b = (a.X + Math.Cos(ang) * len, a.Y + Math.Sin(ang) * len);
                neu.Add(a, b, i); alt.Add(a, b);
            }
            Assert.True(neu.BigCount > 100, $"zu wenige lange Strecken ({neu.BigCount})");
            for (var q = 0; q < 2000; q++)
            {
                double x = rnd.NextDouble() * 500 - 200, y = rnd.NextDouble() * 500 - 200, w = q % 4 == 0 ? rnd.NextDouble() * 300 : rnd.NextDouble() * 8, h = q % 3 == 0 ? rnd.NextDouble() * 300 : rnd.NextDouble() * 8;
                var e = alt.Near(x, y, x + w, y + h); var n = neu.Near(x, y, x + w, y + h);
                Assert.Equal(e, n);
                // Any: dieselbe Folge bis zum Treffer
                var stop = e.Count == 0 ? -1 : e[rnd.Next(e.Count)]; var seen = new List<int>();
                var hit = neu.Any(x, y, x + w, y + h, i => { seen.Add(i); return i == stop; });
                Assert.Equal(stop >= 0, hit);
                Assert.Equal(stop >= 0 ? e.Take(e.IndexOf(stop) + 1) : e, seen);
            }
        }
    }

    [Fact(DisplayName = "T-0758: lange schräge Strecke belegt nur wenige Rasterzellen (vorher rund 445 000 je 2000-mm-Linie bei 3 mm Zellen)")]
    public void Long_segment_few_cells()
    {
        var g = new SegGrid(3);
        g.Add((0, 0), (2000, 2000), 0);
        Assert.Equal(1, g.BigCount); Assert.Equal(0, g.FineEntries);
        Assert.Equal([0], g.Near(1000, 1000, 1001, 1001));
        Assert.Empty(g.Near(2010, 1000, 2020, 1001));
    }

    /// <summary>Bisheriges Douglas-Peucker (bis 0.9.108).</summary>
    static (List<(double X, double Y)> Pts, double Dev) OldSimplify(List<(double X, double Y)> p, double eps)
    {
        var n = p.Count;
        var far = Enumerable.Range(1, n - 1).MaxBy(k => D2(p[0], p[k]));
        var keep = new bool[n]; keep[0] = keep[far] = true; var dev = 0.0;
        var stack = new Stack<(int A, int B)>(); stack.Push((0, far)); stack.Push((far, n));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop(); if (b - a < 2) continue;
            var pa = p[a]; var pb = p[b % n]; double max = -1; var at = -1;
            for (var k = a + 1; k < b; k++) { var dk = CncCam.SegDist(p[k], pa, pb); if (dk > max) { max = dk; at = k; } }
            if (max > eps) { keep[at] = true; stack.Push((a, at)); stack.Push((at, b)); }
            else dev = Math.Max(dev, max);
        }
        return (p.Where((_, k) => keep[k]).ToList(), dev);
    }
    static double D2((double X, double Y) a, (double X, double Y) b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    static List<(double X, double Y)> Comb(int teeth, double h = 999)
    {
        var p = new List<(double X, double Y)> { (-1000, 0), (-1000, h + 1) };
        for (var t = 0; t < teeth; t++) { double x = 2.0 * t; p.AddRange([(x, h + 1), (x, 1), (x + 1, 1), (x + 1, h + 1)]); }
        p.AddRange([(2.0 * teeth, h + 1), (2.0 * teeth, 0)]);
        return p;
    }

    [Fact(DisplayName = "T-0758: Glätten (Douglas-Peucker) – gleiche Punkte und gleiche Abweichung wie bisher (Rauschen, Kreis, Kamm mit 20 000 Zähnen, Gleichstände)")]
    public void Simplify_same_as_before()
    {
        var rnd = new Random(7580);
        var cases = new List<List<(double X, double Y)>>
        {
            Enumerable.Range(0, 3000).Select(k => (Math.Cos(2 * Math.PI * k / 3000) * 500, Math.Sin(2 * Math.PI * k / 3000) * 500)).ToList(),
            Enumerable.Range(0, 5000).Select(k => ((500 + rnd.NextDouble() * 0.004) * Math.Cos(2 * Math.PI * k / 5000), (500 + rnd.NextDouble() * 0.004) * Math.Sin(2 * Math.PI * k / 5000))).ToList(),
            Comb(2000), Comb(5000, 50),
            Enumerable.Range(0, 150_000).Select(k => (Math.Cos(2 * Math.PI * k / 150_000) * 800, Math.Sin(2 * Math.PI * k / 150_000) * 800)).ToList(), // lange Bereiche (parallel)
            Enumerable.Range(0, 4000).Select(k => (k * 0.5, k % 2 == 0 ? 0.0 : 0.0005)).Concat([(2000.0, 300.0), (0.0, 300.0)]).ToList(),
        };
        foreach (var c in cases)
            foreach (var eps in new[] { 0.001, 0.0002, 0.05 })
            {
                var (pa, da) = OldSimplify(c, eps); var (pn, dn) = CncCam.Simplify(c, eps);
                if (pa.Count < 3) continue;
                Assert.Equal(pa, pn); Assert.Equal(da, dn);
            }
    }

    static bool OldSelfIntersects(List<(double X, double Y)> poly)
    {
        var p = new List<(double X, double Y)>(); // wie CncCam.Clean
        foreach (var q in poly) if (p.Count == 0 || D2(p[^1], q) > 1e-12) p.Add(q);
        while (p.Count > 1 && D2(p[0], p[^1]) <= 1e-12) p.RemoveAt(p.Count - 1);
        var n = p.Count; if (n < 4) return false;
        double x0 = p.Min(q => q.X), y0 = p.Min(q => q.Y), x1 = p.Max(q => q.X), y1 = p.Max(q => q.Y);
        var len = 0.0; for (var i = 0; i < n; i++) len += Math.Sqrt(D2(p[i], p[(i + 1) % n]));
        var cell = Math.Max(Math.Max(1e-6, len / n) * 2, Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) / 2048);
        var g = new OldGrid(cell); for (var k = 0; k < n; k++) g.Add(p[k], p[(k + 1) % n]);
        for (var k = 0; k < n; k++)
        {
            var a = p[k]; var b = p[(k + 1) % n];
            foreach (var j in g.Near(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)))
            {
                if (j <= k || j == k + 1 || k == 0 && j == n - 1) continue;
                if (CncCam.SegSeg(a, b, p[j], p[(j + 1) % n]) < 1e-7) return true;
            }
        }
        return false;
    }

    [Fact(DisplayName = "T-0758: Selbstschnitt-Prüfung – gleiches Ergebnis wie bisher (Kamm, berührende Zähne, Schnitt knapp unter/über 1e-7 an Zellgrenzen) und schnell beim Kamm mit 50 000 Zähnen")]
    public void SelfIntersects_same_as_before()
    {
        var rnd = new Random(758);
        for (var t = 0; t < 300; t++)
        {
            var c = Comb(20 + rnd.Next(60), 5 + rnd.NextDouble() * 50);
            var k = 2 + 4 * rnd.Next(10) + rnd.Next(4); var gap = (t % 3) switch { 0 => -1e-6, 1 => 5e-8, _ => 2e-7 };
            if (t % 2 == 0) c[k] = (c[k].X + (k % 4 < 2 ? 1 : -1) * (1 + gap), c[k].Y); // Zahn an den Nachbarn heranrücken
            var p = c.Distinct().ToList();
            Assert.Equal(OldSelfIntersects(p), CncCam.SelfIntersects(p));
        }
        var big = Comb(50000); var sw = Stopwatch.StartNew();
        Assert.False(CncCam.SelfIntersects(big)); log.WriteLine($"Kamm 50 000: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 4000, $"{sw.ElapsedMilliseconds} ms");
    }

    static string Diag(int lines, int holes)
    {
        const double W = 2000; var sb = new StringBuilder(Lw([(0, 0), (W, 0), (W, W), (0, W)]));
        for (var k = 0; k < lines; k++)
        {
            var h = lines / 2;
            if (k < h) sb.Append(Line(0, W * (k + 0.5) / h, W - W * (k + 0.5) / h, W));
            else sb.Append(Line(W * (k - h + 0.5) / h, 0, W, W - W * (k - h + 0.5) / h));
        }
        var m = (int)Math.Ceiling(Math.Sqrt(holes)); var st = W / (m + 1);
        for (var k = 0; k < holes; k++)
        {
            double cx = st * (k % m + 1), cy = st * (k / m + 1), rr = st * 0.2;
            sb.Append(Lw(Enumerable.Range(0, 16).Select(t => (cx + rr * Math.Cos(2 * Math.PI * t / 16), cy + rr * Math.Sin(2 * Math.PI * t / 16)))));
        }
        return Dxf(sb.ToString());
    }

    [Fact(DisplayName = "T-0758: Platte 2000 × 2000 mit 200 schrägen Linien von Rand zu Rand – Fräsbahn unter 3 s (0.9.108: 44 s)")]
    public void Diagonal_lines_fast()
    {
        var rd = CadDxf.Parse(Diag(200, 0)); var cs = CncCam.Contours(rd.Drawing);
        var sw = Stopwatch.StartNew(); var r = CncCam.Generate(cs, new CamSettings { ToolD = 6, Depth = 1, StepDown = 1, Unread = rd.Unread }); var t = sw.ElapsedMilliseconds;
        log.WriteLine($"200 Linien: {t} ms, {r.Paths.Count} Bahnen");
        Assert.Single(r.Paths, p => p.Kind == "aussen");
        Assert.True(t < 3000, $"Fräsbahn brauchte {t} ms");
    }

    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Sq(double x0, double y0, double w) => Lw([(x0, y0), (x0 + w, y0), (x0 + w, y0 + w), (x0, y0 + w)]);
    static string H(string s) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];

    /// <summary>Fälle für den Vergleich mit 0.9.108 (Zeichnung, Einstellungen, erwarteter Prüfwert von G-Code und Meldungen aus 0.9.108).</summary>
    static IEnumerable<(string Name, string Dxf, CamSettings S, string Expect)> Cases()
    {
        yield return ("diagonal_60_loecher_50", Diag(60, 50), new CamSettings { ToolD = 6, Depth = 1, StepDown = 1 }, "F3FA464007ADBCFF/60DFE8BEE53513F9");
        var grav = new StringBuilder(); for (var k = 0; k < 120; k++) grav.Append(Line(k * 15.0, 0, k * 15.0 + 1500, 1500)).Append(Line(k * 15.0 + 3, 0, k * 15.0 + 3 + 750, 750 + (k % 2) * 0.001));
        yield return ("gravur_schraeg_240", Dxf(grav.ToString()), new CamSettings { ToolD = 2, Depth = 1, StepDown = 0.5, Mode = "auf" }, "4F41F7023E8BB8CE/E3B0C44298FC1C14"); // Gravur: Linien 3 mm auseinander, Fräser 2 mm; teils zu nah
        var small = new StringBuilder(Sq(0, 0, 300));
        for (var i = 0; i < 30; i++) for (var j = 0; j < 30; j++) small.Append(Circle(5 + i * 10, 5 + j * 10, (i + j) % 7 == 0 ? 4.5 : 0.5));
        yield return ("kleine_loecher_900", Dxf(small.ToString()), new CamSettings { ToolD = 6, Depth = 3, StepDown = 1, Tabs = 3, TabW = 5, TabH = 1 }, "9A591491A566466F/BF1BB72117774CE9");
        yield return ("kamm_3000", Dxf(Lw(Comb(3000))), new CamSettings { ToolD = 6, Depth = 1, StepDown = 1 }, "34EA06CCFE6AC37D/46FBB822B5DE7B63");
        var kol = new List<(double X, double Y)>(); for (var k = 0; k <= 1000; k++) kol.Add((k, k)); kol.Add((0, 1000));
        yield return ("kollinear_1000", Dxf(Lw(kol) + Line(0, 0, 1000, 1000)), new CamSettings { ToolD = 1, Depth = 1, StepDown = 1 }, "48369B293ACFF2F9/48CF96F34F65338F");
        var many = new StringBuilder(Sq(100_000, 100_000, 50_000));
        for (var i = 0; i < 3000; i++) many.Append(Sq(-(i % 60) * 3.0, (i / 60) * 3.0, 1));
        yield return ("riesig_neben_3000", Dxf(many.ToString()), new CamSettings { ToolD = 1, Depth = 1, StepDown = 1 }, "ED1E6F3FEFAB6276/E3B0C44298FC1C14");
        var near = new StringBuilder();
        for (var i = 0; i < 40; i++) near.Append(Sq(i * 25, 0, 20)).Append(Sq(i * 25 + 20.5 + (i % 3) * 0.25, 2, 3));
        for (var i = 0; i < 150; i++) near.Append(Circle(500 + i % 15 * 30, 100 + i / 15 * 30, 10)).Append(Circle(500 + i % 15 * 30, 100 + i / 15 * 30, 10));
        yield return ("zu_nah_und_doppelt", Dxf(near.ToString()), new CamSettings { ToolD = 4, Depth = 2, StepDown = 1 }, "2C1963644457C817/9B82409687993D64");
    }

    static string Run(string dxf, CamSettings s)
    {
        var rd = CadDxf.Parse(dxf); var cs = CncCam.Contours(rd.Drawing);
        try { var r = CncCam.Generate(cs, s with { Unread = rd.Unread }); return H(r.Gcode) + "/" + H(string.Join("\n", r.Warnings)); }
        catch (InvalidOperationException e) { return "Fehler/" + H(e.Message); }
    }

    [Fact(DisplayName = "T-0758: G-Code und Meldungen byte-gleich wie 0.9.108, zusammen unter 20 s statt über 10 min (schräge Linien, Gravur, zu kleine Löcher mit Stegen, Kamm, kollineare Kante, riesig neben 3000 winzigen, zu nah, doppelt)")]
    public void Same_gcode_as_before()
    {
        var print = Environment.GetEnvironmentVariable("T0758_PRINT") == "1"; var all = Stopwatch.StartNew();
        foreach (var (name, dxf, s, expect) in Cases())
        {
            var sw = Stopwatch.StartNew(); var got = Run(dxf, s);
            log.WriteLine($"{name}: {got} ({sw.ElapsedMilliseconds} ms)");
            if (print) { File.AppendAllText(Path.Combine(Path.GetTempPath(), "t0758-hashes.txt"), $"{name} {got}\n"); continue; }
            Assert.Equal(expect, got);
        }
        // 0.9.108 brauchte für diese Fälle zusammen über 10 Minuten (Gravur 438 s, kollineare Kante 152 s, schräge Linien 19 s, kleine Löcher 12 s)
        if (!print) Assert.True(all.ElapsedMilliseconds < 20000, $"zusammen {all.ElapsedMilliseconds} ms");
    }

    [Fact(DisplayName = "T-0758: 2500 zu kleine Löcher (Ø 1 mm, Fräser 6 mm) unter 3 s (0.9.108: quadratisch viele Schnittpunkte je Loch)")]
    public void Small_holes_fast()
    {
        var sb = new StringBuilder(Sq(0, 0, 1000));
        for (var i = 0; i < 50; i++) for (var j = 0; j < 50; j++) sb.Append(Circle(10 + i * 19, 10 + j * 19, 0.5));
        var rd = CadDxf.Parse(Dxf(sb.ToString())); var cs = CncCam.Contours(rd.Drawing);
        var sw = Stopwatch.StartNew(); var r = CncCam.Generate(cs, new CamSettings { ToolD = 6, Unread = rd.Unread }); var t = sw.ElapsedMilliseconds;
        log.WriteLine($"2500 Löcher: {t} ms");
        Assert.Equal(2500, r.Warnings.Count(w => w.Contains("zu klein für den Fräser")));
        Assert.True(t < 3000, $"{t} ms");
    }

    [Fact(DisplayName = "T-0758: Abbrechen der Fräsbahn-Erzeugung wirkt in unter 2 s (Kamm 20 000 Zähne, 20 000 Kreise, schräge Linien mit Löchern)")]
    public void Cancel_quickly()
    {
        var circles = new StringBuilder(Sq(0, 0, 1500)); for (var i = 0; i < 140; i++) for (var j = 0; j < 140; j++) circles.Append(Circle(10 + i * 10, 10 + j * 10, 1.2));
        foreach (var (name, dxf, tool) in new[] { ("kamm", Dxf(Lw(Comb(20000))), 6.0), ("kreise", Dxf(circles.ToString()), 1.0), ("diagonal", Diag(400, 1500), 6.0) })
        {
            var rd = CadDxf.Parse(dxf); var cs = CncCam.Contours(rd.Drawing);
            foreach (var delay in new[] { 50, 400, 1500 })
            {
                using var cts = new CancellationTokenSource(); var t = Task.Run(() => CncCam.Generate(cs, new CamSettings { ToolD = tool, Depth = 1, StepDown = 1, Unread = rd.Unread }, cts.Token));
                if (t.Wait(delay)) { log.WriteLine($"{name}: fertig vor {delay} ms"); break; }
                var sw = Stopwatch.StartNew(); cts.Cancel();
                try { t.Wait(); } catch (AggregateException e) when (e.InnerException is OperationCanceledException) { }
                log.WriteLine($"{name}: Abbruch nach {delay} ms, Nachlauf {sw.ElapsedMilliseconds} ms");
                Assert.True(sw.ElapsedMilliseconds < 2000, $"{name}: Nachlauf {sw.ElapsedMilliseconds} ms");
            }
        }
    }
}
