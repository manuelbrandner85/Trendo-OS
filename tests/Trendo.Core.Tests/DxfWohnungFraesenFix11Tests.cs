using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix11: Befunde der elften Prüfung – ausgeblendete Schraffur nur in eindeutigen Fällen verwerfen (Inkreis/Raute mit gleichem Rahmen,
/// hartes Arbeitsbudget, Kamm-Kontur), Vollkreise in Eimern, Abtast-Budget für riesige Bögen, wörtliche Lesart der Uhrzeiger-Bögen, echter Bogenrahmen,
/// Formvergleich in mm (Meter-Zeichnungen), unsichtbare Elemente (Gruppe 60).</summary>
public class T0747_Fix11ElftePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    const string AusLayer = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nAUS\n70\n0\n62\n-7\n0\nENDTAB\n0\nENDSEC\n";
    static string Dxf(string entities, string tables = "", int units = 4) => $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n{units}\n0\nENDSEC\n" + tables + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Lw(IEnumerable<(double X, double Y, double B)> pts, string layer = "0")
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, layer), (90, p.Count), (70, 1)));
        foreach (var q in p) { sb.Append(G((10, q.X), (20, q.Y))); if (q.B != 0) sb.Append(G((42, q.B))); }
        return sb.ToString();
    }
    static (double X, double Y, double B)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0, 0), (x1, y0, 0), (x1, y1, 0), (x0, y1, 0)];
    static string Circle(double x, double y, double r, string layer = "0") => G((0, "CIRCLE"), (8, layer), (10, x), (20, y), (30, 0), (40, r));
    static string Arc(double x, double y, double r, double a0, double a1) => G((0, "ARC"), (8, "0"), (10, x), (20, y), (30, 0), (40, r), (50, a0), (51, a1));
    static string Line(double x0, double y0, double x1, double y1) => G((0, "LINE"), (8, "0"), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string ELine(double x0, double y0, double x1, double y1) => G((72, 1), (10, x0), (20, y0), (11, x1), (21, y1));
    static string EArc(double cx, double cy, double r, double a0, double a1, int ccw) => G((72, 2), (10, cx), (20, cy), (40, r), (50, a0), (51, a1), (73, ccw));
    static string Hatch(string layer, IEnumerable<string> loops, string extra = "")
    {
        var l = loops.ToList(); var sb = new StringBuilder(G((0, "HATCH"), (8, layer)) + extra + G((10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, l.Count)));
        foreach (var x in l) sb.Append(x);
        return sb.Append(G((75, 0), (76, 1), (98, 0))).ToString();
    }
    static string PolyLoop(IEnumerable<(double X, double Y)> p, int flags = 3) { var q = p.ToList(); return G((92, flags), (72, 0), (73, 1), (93, q.Count)) + string.Concat(q.Select(v => G((10, v.X), (20, v.Y)))) + G((97, 0)); }
    static string BulgeLoop(IEnumerable<(double X, double Y, double B)> p, int flags = 3) { var q = p.ToList(); return G((92, flags), (72, 1), (73, 1), (93, q.Count)) + string.Concat(q.Select(v => G((10, v.X), (20, v.Y), (42, v.B)))) + G((97, 0)); }
    static string EdgeLoop(params string[] e) => G((92, 1), (93, e.Length)) + string.Concat(e) + G((97, 0));
    static IEnumerable<(double X, double Y)> Sq(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }
    static bool MilledOutside(CamResult r, double x, double y, double rr) =>
        r.Paths.Any(p => p.Kind == "aussen" && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - 0.5) < 0.05));

    static long Timed(Action a)
    {
        var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds;
        if (t > 2000) { sw.Restart(); a(); t = Math.Min(t, sw.ElapsedMilliseconds); } // unter Fremdlast einmal wiederholt
        return t;
    }

    static long CancelTime(string dxf)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var cts = new CancellationTokenSource(); using var go = new ManualResetEventSlim(false); var wait = attempt * 5;
            var th = new Thread(() => { go.Wait(); if (wait > 0) Thread.Sleep(wait); cts.Cancel(); }) { IsBackground = true }; th.Start();
            var sw = Stopwatch.StartNew(); go.Set();
            try { CadDxf.Parse(dxf, cts.Token); }
            catch (OperationCanceledException) { th.Join(); return sw.ElapsedMilliseconds; }
            th.Join();
        }
        throw new Xunit.Sdk.XunitException("Abbruch wirkte nicht");
    }

    [Fact(DisplayName = "T-0747-fix11 HOCH A: ausgeblendete Schraffur über Inkreis (gap 0 / 0,005 / 0,009) bzw. Raute mit gleichem Rahmen – Kreis r 5 darin nie außen gefräst")]
    public void Hidden_fill_inner_same_box()
    {
        foreach (var gap in new[] { 0.0, 0.005, 0.009 })
        {
            var r = Cam(Dxf(Lw(Rect(0, 0, 100, 100)) + Hatch("AUS", [PolyLoop(Sq(20, 20, 80, 80))]) + Circle(50, 50, 30 - gap) + Circle(50, 50, 5), AusLayer));
            log.WriteLine($"gap {gap}: {string.Join(" | ", r.Warnings)}");
            Assert.False(MilledOutside(r, 50, 50, 5), $"gap {gap}");
        }
        var rhombus = Lw([(50, 20, 0), (80, 50, 0), (50, 80, 0), (20, 50, 0)]);
        var rr = Cam(Dxf(Lw(Rect(0, 0, 100, 100)) + Hatch("AUS", [PolyLoop(Sq(20, 20, 80, 80))]) + rhombus + Circle(50, 50, 5), AusLayer));
        Assert.False(MilledOutside(rr, 50, 50, 5));
    }

    [Fact(DisplayName = "T-0747-fix11 HOCH B/MITTEL C: Kamm mit 50 000 Zähnen + 20 ausgeblendete Schraffuren, 500 große ausgeblendete Rechtecke, Platte mit 40 000 Löchern + 2000 ausgeblendete – Sekunden, abbrechbar")]
    public void Hidden_fill_budget()
    {
        // Kamm: Grundleiste 0…10, Zähne bis 100, senkrechte Kanten über die ganze Höhe
        var comb = new List<(double X, double Y, double B)> { (0, 0, 0), (100_000, 0, 0) };
        for (var t = 49_999; t >= 0; t--) { double x = t * 2; comb.Add((x + 2, 100, 0)); comb.Add((x + 1, 100, 0)); comb.Add((x + 1, 10, 0)); comb.Add((x, 10, 0)); }
        var combDxf = Dxf(Lw(comb) + Hatch("AUS", Enumerable.Range(0, 20).Select(q => PolyLoop(Sq(100 + q * 5000, 2, 101 + q * 5000, 3)))), AusLayer);
        var big = Dxf(Lw(Rect(0, 0, 10_000, 10_000)) + Hatch("AUS", Enumerable.Range(0, 500).Select(q => PolyLoop(Sq(10 + q * 19, 10, 20 + q * 19, 9990)))), AusLayer);
        var holes = new StringBuilder(Lw(Rect(0, 0, 4010, 4010)));
        for (var q = 0; q < 40_000; q++) holes.Append(Circle(10 + q % 200 * 20, 10 + q / 200 * 20, 3));
        var many = Dxf(holes + Hatch("AUS", Enumerable.Range(0, 2000).Select(q => PolyLoop(Sq(14 + q % 50 * 80, 14 + q / 50 * 100, 16 + q % 50 * 80, 16 + q / 50 * 100)))), AusLayer);
        var t1 = Timed(() => CadDxf.Parse(combDxf)); var t2 = Timed(() => CadDxf.Parse(big)); var t3 = Timed(() => CadDxf.Parse(many));
        var c1 = CancelTime(combDxf); var c3 = CancelTime(many);
        log.WriteLine($"Kamm 200 004 Punkte + 20: {t1} ms; 500 große: {t2} ms; 40 000 Löcher + 2000: {t3} ms; Abbrechen {c1} / {c3} ms");
        Assert.True(t1 < 10_000 && t2 < 10_000 && t3 < 20_000, $"{t1} / {t2} / {t3}"); // einzeln je unter 2 s; Vorstand > 900 s / 33 s / > 120 s
        Assert.True(c1 < 3000 && c3 < 3000, $"{c1} / {c3}");
    }

    [Fact(DisplayName = "T-0747-fix11 MITTEL D/E: 60 000 gleiche Kreise + 60 000 Schraffur-Kreise daneben in Sekunden, abbrechbar; 1000 Ausbuchtungen r 1e7 ohne Speicherflut")]
    public void Circles_and_huge_bulges()
    {
        var sb = new StringBuilder(); var loops = new List<string>();
        for (var q = 0; q < 60_000; q++) { sb.Append(Circle(5, 5, 1)); loops.Add(EdgeLoop(EArc(5.02, 5, 1, 0, 360, 1))); }
        var circles = Dxf(sb + Hatch("0", loops));
        var bul = Dxf(Hatch("0", Enumerable.Range(0, 1000).Select(q => BulgeLoop([(q * 10, 0, 1), (q * 10 + 2e7, 0, 0.5), (q * 10 + 2e7, 1e5, 0)])))); // Halbkreis r 1e7: je Schleife über 300 000 Punkte
        // T-0760: Zeiten mit Lastfaktor (Grenzen unverändert); Speicher nur des eigenen Fadens – CadDxf.Parse rechnet einfädig, so zählen parallel
        // laufende andere Tests nicht mit (GC.GetTotalAllocatedBytes zählte den ganzen Prozess: im Volltest 1175 MB)
        var t1 = Lastmessung.Best(2, () => CadDxf.Parse(circles), parallel: false); var c1 = Lastmessung.Measure(() => CancelTime(circles), parallel: false);
        long mb = 0;
        var t2 = Lastmessung.Best(2, () => { var a0 = GC.GetAllocatedBytesForCurrentThread(); CadDxf.Parse(bul); mb = (GC.GetAllocatedBytesForCurrentThread() - a0) / 1048576; }, parallel: false);
        log.WriteLine($"1000 Ausbuchtungen r 1e7: alloziert {mb} MB (eigener Faden)");
        Lastmessung.Below(log, "60 000 Kreise", t1, 10_000); Lastmessung.Below(log, "1000 Ausbuchtungen r 1e7", t2, 10_000); Lastmessung.Below(log, "Abbrechen", c1, 3000);
        Assert.True(mb < 1000, $"{mb} MB"); // Vorstand: 100 Ausbuchtungen 2,1 GB, 1000 Speicherüberlauf
        Assert.Equal(1000, CadDxf.Parse(bul).Unread.Count); // Rahmen bleibt, nur keine Form
    }

    [Fact(DisplayName = "T-0747-fix11 MITTEL F: Linse aus zwei Uhrzeiger-Bögen wörtlich geschrieben, gezeichnet die Erdnuss – nicht formgleich, Kreis r 4 nie außen gefräst")]
    public void Cw_literal_lens_vs_peanut()
    {
        double R = 40, D = 48, h = Math.Sqrt(R * R - D / 2 * D / 2), a = Math.Atan2(h, D / 2) * 180 / Math.PI, c1 = 1000, c2 = 1048, cy = 500;
        var peanut = Arc(c1, cy, R, a, 360 - a) + Arc(c2, cy, R, 180 + a, 180 - a);
        var lensLiteral = EdgeLoop(EArc(c1, cy, R, a, 360 - a, 0), EArc(c2, cy, R, 180 + a, 180 - a, 0));
        var dxf = Dxf(peanut + Hatch("0", [lensLiteral]) + Circle(1024, 500, 10) + Circle(1024, 500, 4));
        Assert.NotEmpty(CadDxf.Parse(dxf).Unread);
        try { var r = Cam(dxf); log.WriteLine(string.Join(" | ", r.Warnings)); Assert.False(MilledOutside(r, 1024, 500, 4)); }
        catch (InvalidOperationException e) { log.WriteLine(e.Message); } // alles ausgelassen: ebenfalls sicher
    }

    [Fact(DisplayName = "T-0747-fix11 MITTEL G/H: flache Bogenkante (ARC r 200) formgleich; Platte mit runden Ecken in m, cm und mm formgleich")]
    public void Arc_box_and_units()
    {
        double W = 120, H = 80, R = 200, hw = W / 2, d = Math.Sqrt(R * R - hw * hw), cx = hw, cy = H - d, a0 = Math.Atan2(H - cy, W - cx) * 180 / Math.PI, a1 = 180 - a0;
        var plate = Line(0, 0, W, 0) + Line(W, 0, W, H) + Arc(cx, cy, R, a0, a1) + Line(0, H, 0, 0);
        var hatch = Hatch("0", [EdgeLoop(ELine(0, 0, W, 0), ELine(W, 0, W, H), EArc(cx, cy, R, a0, a1, 1), ELine(0, H, 0, 0))]);
        var rd = CadDxf.Parse(Dxf(plate + hatch + Circle(30, 40, 5)));
        Assert.Empty(rd.Unread); Assert.Equal(1, rd.Redundant["HATCH"]);
        foreach (var (units, s) in new[] { (4, 1.0), (5, 0.1), (6, 0.001) })
        {
            double x0 = 0, y0 = 0, x1 = 120 * s, y1 = 80 * s, r = 10 * s, b = Math.Tan(Math.PI / 8);
            (double X, double Y, double B)[] p = [(x0 + r, y0, 0), (x1 - r, y0, b), (x1, y0 + r, 0), (x1, y1 - r, b), (x1 - r, y1, 0), (x0 + r, y1, b), (x0, y1 - r, 0), (x0, y0 + r, b)];
            var dxf = Dxf(Lw(p) + Hatch("0", [BulgeLoop(p)]) + Circle(30 * s, 40 * s, 5 * s), units: units);
            var u = CadDxf.Parse(dxf); log.WriteLine($"Einheit {units}: {u.Unread.Count} nicht gelesen");
            Assert.Empty(u.Unread); Assert.Equal(1, u.Redundant["HATCH"]);
            Assert.Single(Cam(dxf).Paths, q => q.Kind == "innen");
        }
    }

    [Fact(DisplayName = "T-0747-fix11 niedrig: unsichtbare Elemente (Gruppe 60 = 1) wie ausgeblendet – nicht gefräst, gemeldet; ausgeblendete Schraffur außerhalb gezeichneter Konturen gemeldet")]
    public void Invisible_and_notes()
    {
        var dxf = Dxf(Lw(Rect(0, 0, 100, 60)) + Circle(95, 5, 2) + Line(0, 70, 100, 70).Replace("8\n0\n", "8\n0\n60\n1\n")
            + Hatch("0", [PolyLoop(Sq(10, 10, 90, 50))], G((60, 1))) + Hatch("AUS", [PolyLoop(Sq(200, 0, 300, 60))]) + Circle(250, 30, 5), AusLayer);
        var rd = CadDxf.Parse(dxf);
        Assert.Contains(rd.Drawing.Entities, e => e.Invisible && e.Kind == "linie");
        Assert.DoesNotContain(CncCam.Contours(rd.Drawing), c => !c.Closed && c.Pts.All(p => Math.Abs(p.Y - 70) < 1e-9)); // unsichtbare Linie nicht gefräst
        var f = Path.Combine(Path.GetTempPath(), "t0747fix11-" + Guid.NewGuid().ToString("N")[..8] + ".dxf");
        try
        {
            File.WriteAllText(f, dxf);
            var r = new CncJob { ToolD = 1 }.ImportDrawing(f);
            log.WriteLine(string.Join(" | ", r.Warnings));
            Assert.Contains(r.Warnings, w => w.Contains("(unsichtbar) (2, davon 1× Schraffur)", StringComparison.Ordinal));
            Assert.Contains(r.Warnings, w => w.Contains("AUS (1, davon 1× Schraffur) – 1 Füllfläche(n) nicht sicher zuzuordnen", StringComparison.Ordinal));
            Assert.Single(r.Paths, p => p.Kind == "innen"); // Loch in der Platte (außerhalb der unsichtbaren Schraffur) gefräst; Kreis bei 250/30 (in ausgeblendeter Schraffur) ausgelassen
        }
        finally { File.Delete(f); }
    }
}
