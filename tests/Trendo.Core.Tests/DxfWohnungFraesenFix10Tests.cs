using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix10: Befunde der zehnten Prüfung – Bogen-/Ellipsenkanten im Uhrzeigersinn mit Komplementwinkeln (wie AutoCAD/ezdxf),
/// Zusammenhang der Randschleife, ungültige Radien/Verhältnisse, schräge Normale, ausgeblendete Schraffur mit verschachteltem Inhalt,
/// Raster und Punkt-im-Zug-Index je Kontur einmal (große Konturen mit vielen ausgeblendeten Schraffuren), Meldung ausgeblendeter Füllflächen bei ihrer Ebene.</summary>
public class T0747_Fix10ZehntePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    const string AusLayer = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nAUS\n70\n0\n62\n-7\n0\nENDTAB\n0\nENDSEC\n";
    static string Dxf(string entities, string tables = "") => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + tables + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Lw((double X, double Y, double B)[] pts, string layer = "0") =>
        G((0, "LWPOLYLINE"), (8, layer), (90, pts.Length), (70, 1)) + string.Concat(pts.Select(p => G((10, p.X), (20, p.Y)) + (p.B != 0 ? G((42, p.B)) : "")));
    static (double X, double Y, double B)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0, 0), (x1, y0, 0), (x1, y1, 0), (x0, y1, 0)];
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Line(double x0, double y0, double x1, double y1) => G((72, 1), (10, x0), (20, y0), (11, x1), (21, y1));
    /// <summary>Bogenkante wie AutoCAD/ezdxf: im Uhrzeigersinn mit Komplementwinkeln (360 − a, vertauscht) und 73 = 0.</summary>
    static string CwArc(double cx, double cy, double r, double realStart, double realEnd)
    {
        double s = ((realEnd % 360) + 360) % 360, e = ((realStart % 360) + 360) % 360; if (e <= s) e += 360;
        return G((72, 2), (10, cx), (20, cy), (40, r), (50, 360 - e), (51, 360 - s), (73, 0));
    }
    static string CcwArc(double cx, double cy, double r, double a0, double a1) => G((72, 2), (10, cx), (20, cy), (40, r), (50, a0), (51, a1), (73, 1));
    static string Hatch(string layer, params string[] loops) =>
        G((0, "HATCH"), (8, layer), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, loops.Length)) + string.Concat(loops) + G((75, 0), (76, 1), (98, 0));
    static string EdgeLoop(int flags, params string[] edges) => G((92, flags), (93, edges.Length)) + string.Concat(edges) + G((97, 0));
    static string PolyLoop((double X, double Y)[] p, int flags = 3) => G((92, flags), (72, 0), (73, 1), (93, p.Length)) + string.Concat(p.Select(q => G((10, q.X), (20, q.Y)))) + G((97, 0));
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }

    /// <summary>T-0747-fix11: Abbrechen beim Lesen lastrobust – ein eigener Thread bricht ab, sobald das Lesen startet (ManualResetEvent); bis zu drei
    /// Versuche. Liefert die Zeit bis zur Abbruch-Ausnahme; wirft, wenn das Lesen jedes Mal ohne Abbruch durchlief.</summary>
    static long CancelTime(string dxf)
    {
        using (var pre = new CancellationTokenSource()) { pre.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => CadDxf.Parse(dxf, pre.Token)); }
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

    static long Timed(Action a)
    {
        var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds;
        if (t > 2000) { sw.Restart(); a(); t = Math.Min(t, sw.ElapsedMilliseconds); } // unter Fremdlast einmal wiederholt
        return t;
    }

    // Platte mit runden Ecken (r 10) als LWPOLYLINE mit Ausbuchtungen
    static readonly double B90 = Math.Tan(Math.PI / 8);
    static (double X, double Y, double B)[] Rounded(double x0, double y0, double x1, double y1, double rr) =>
        [(x0 + rr, y0, 0), (x1 - rr, y0, B90), (x1, y0 + rr, 0), (x1, y1 - rr, B90), (x1 - rr, y1, 0), (x0 + rr, y1, B90), (x0, y1 - rr, 0), (x0, y0 + rr, B90)];

    [Fact(DisplayName = "T-0747-fix10 HOCH 1: Bogenkanten im Uhrzeigersinn mit Komplementwinkeln – runde Ecken formgleich (G-Code mit Löchern innen), gegen den Uhrzeigersinn ebenso")]
    public void Cw_arc_edges_complement()
    {
        double x0 = 500, y0 = 400, x1 = 600, y1 = 460, rr = 10;
        // Rand im Uhrzeigersinn: links hoch, oben nach rechts, rechts runter, unten nach links
        var cw = EdgeLoop(5, Line(x0, y0 + rr, x0, y1 - rr), CwArc(x0 + rr, y1 - rr, rr, 180, 90), Line(x0 + rr, y1, x1 - rr, y1), CwArc(x1 - rr, y1 - rr, rr, 90, 0),
            Line(x1, y1 - rr, x1, y0 + rr), CwArc(x1 - rr, y0 + rr, rr, 0, -90), Line(x1 - rr, y0, x0 + rr, y0), CwArc(x0 + rr, y0 + rr, rr, 270, 180));
        var ccw = EdgeLoop(1, Line(x0 + rr, y0, x1 - rr, y0), CcwArc(x1 - rr, y0 + rr, rr, 270, 360), Line(x1, y0 + rr, x1, y1 - rr), CcwArc(x1 - rr, y1 - rr, rr, 0, 90),
            Line(x1 - rr, y1, x0 + rr, y1), CcwArc(x0 + rr, y1 - rr, rr, 90, 180), Line(x0, y1 - rr, x0, y0 + rr), CcwArc(x0 + rr, y0 + rr, rr, 180, 270));
        foreach (var (name, loop) in new[] { ("im Uhrzeigersinn", cw), ("gegen den Uhrzeigersinn", ccw) })
        {
            var dxf = Dxf(Lw(Rounded(x0, y0, x1, y1, rr)) + Circle(530, 430, 5) + Circle(570, 430, 5) + Hatch("0", loop));
            var rd = CadDxf.Parse(dxf);
            log.WriteLine($"{name}: {rd.Unread.Count} nicht gelesen, Redundant {string.Join(",", rd.Redundant)}");
            Assert.Empty(rd.Unread); Assert.Equal(1, rd.Redundant["HATCH"]);
            Assert.Equal(2, Cam(dxf).Paths.Count(p => p.Kind == "innen"));
        }
    }

    [Fact(DisplayName = "T-0747-fix10 HOCH 1: ausgeblendete und sichtbare Schraffur mit echtem Uhrzeiger-Bogen in die Kerbe – Kreis in der Kerbe nie außen gefräst")]
    public void Cw_arc_into_notch()
    {
        (double X, double Y, double B)[] notch = [(0, 0, 0), (200, 0, 0), (200, 100, 0), (110, 100, 0), (110, 60, 0), (90, 60, 0), (90, 100, 0), (0, 100, 0)];
        double kx = 100, ky = 50, kr = 25; (double, double) A(double d) => (kx + kr * Math.Cos(d * Math.PI / 180), ky + kr * Math.Sin(d * Math.PI / 180));
        var (x6, y6) = A(60); var (x3, y3) = A(300);
        // echter Bogen im Uhrzeigersinn von 300° über 180° nach 60° (gespeichert 50 = 60, 51 = 300, 73 = 0), Sehne 60° → 300°
        var loop = EdgeLoop(1, G((72, 2), (10, kx), (20, ky), (40, kr), (50, 60), (51, 300), (73, 0)), Line(x6, y6, x3, y3));
        foreach (var layer in new[] { "AUS", "0" })
        {
            var r = Cam(Dxf(Lw(notch) + Hatch(layer, loop) + Circle(100, 70, 2) + Circle(30, 30, 5), AusLayer));
            log.WriteLine($"{layer}: {string.Join(" | ", r.Warnings)}");
            Assert.DoesNotContain(r.Paths, p => p.Kind == "aussen" && p.Pts.All(q => Math.Abs(q.X - r.ShiftX - 100) < 4 && Math.Abs(q.Y - r.ShiftY - 70) < 4));
            Assert.Contains(r.Warnings, w => w.Contains("ausgelassen: liegt in nicht gelesenem Element Schraffur", StringComparison.Ordinal));
        }
    }

    [Fact(DisplayName = "T-0747-fix10: Randschleife ohne Zusammenhang hat keine Form; Bogen r ≤ 0, Ellipse mit Verhältnis ≤ 0 oder > 1 und schräge Normale → überall unsicher")]
    public void Connectivity_and_invalid()
    {
        // Rechteck aus vier Linien mit 0,5 mm Lücke: Rahmen gleich, aber nicht zusammenhängend → nicht formgleich
        var gap = EdgeLoop(1, Line(0, 0, 100, 0), Line(100, 0, 100, 60), Line(100, 60, 0.5, 60), Line(0, 60, 0, 0));
        Assert.Single(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Hatch("0", gap))).Unread);
        foreach (var bad in new[] { CcwArc(50, 30, 0, 0, 360), CcwArc(50, 30, -30, 0, 360),
                     G((72, 3), (10, 50), (20, 30), (11, 50), (21, 0), (40, 0), (50, 0), (51, 360), (73, 1)), G((72, 3), (10, 50), (20, 30), (11, 50), (21, 0), (40, -0.5), (50, 0), (51, 360), (73, 1)),
                     G((72, 3), (10, 50), (20, 30), (11, 50), (21, 0), (40, 1.5), (50, 0), (51, 360), (73, 1)) })
            Assert.True(Assert.Single(CadDxf.Parse(Dxf(Hatch("0", EdgeLoop(1, bad)) + Circle(50, 30, 5))).Unread).B.IsAll);
        var oblique = Hatch("0", PolyLoop([(0, 0), (100, 0), (100, 60), (0, 60)])).Replace("210\n0\n220\n0\n230\n1\n", "210\n0.3\n220\n0\n230\n0.95\n");
        Assert.Contains("0.95", oblique);
        Assert.True(Assert.Single(CadDxf.Parse(Dxf(oblique + Circle(50, 30, 5))).Unread).B.IsAll);
    }

    [Fact(DisplayName = "T-0747-fix10: ausgeblendete Schraffur über einem Loch mit Teil darin bleibt (Teil nie außen gefräst); über einfachen Löchern und mit Spitze bis 0,01 mm am Rand fällt sie weg")]
    public void Hidden_fill_nested_and_tolerance()
    {
        var nested = Cam(Dxf(Lw(Rect(0, 0, 200, 100)) + Lw(Rect(50, 20, 150, 80)) + Circle(100, 50, 5) + Hatch("AUS", PolyLoop([(10, 10), (190, 10), (190, 90), (10, 90)])), AusLayer));
        log.WriteLine(string.Join(" | ", nested.Warnings));
        Assert.DoesNotContain(nested.Paths, p => p.Kind == "aussen" && p.Pts.All(q => Math.Abs(q.X - nested.ShiftX - 100) < 7 && Math.Abs(q.Y - nested.ShiftY - 50) < 7));
        foreach (var d in new[] { 0.0019, 0.0021, -0.001 })
        {
            // T-0747-fix12: Löcher außerhalb des Schraffurrahmens (eine Kontur im Rahmen ließe die Schraffur stehen)
            var r = Cam(Dxf(Lw(Rect(0, 0, 200, 60)) + Circle(130, 30, 5) + Circle(170, 30, 5) + Hatch("AUS", PolyLoop([(5, 5), (50, d), (95, 5), (95, 55), (5, 55)])), AusLayer));
            Assert.Equal(2, r.Paths.Count(p => p.Kind == "innen"));
        }
    }

    [Fact(DisplayName = "T-0747-fix10 HOCH 2: Kontur mit 300 000 Punkten und 2000 ausgeblendeten Schraffuren, Sägezahn mit 362 000 Punkten und 50 ausgeblendeten – Sekunden, abbrechbar")]
    public void Hidden_fills_in_huge_contour()
    {
        static string Big(int n, int hidden)
        {
            var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, "0"), (90, n), (70, 1)));
            for (var k = 0; k < n; k++) sb.Append(G((10, 1000 * Math.Cos(2 * Math.PI * k / n)), (20, 1000 * Math.Sin(2 * Math.PI * k / n))));
            sb.Append(G((0, "HATCH"), (8, "AUS"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, hidden)));
            for (var q = 0; q < hidden; q++) { double x = -300 + q % 100 * 6, y = -300 + q / 100 * 6; sb.Append(PolyLoop([(x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1)])); }
            return sb.Append(G((75, 0), (76, 1), (98, 0))).ToString();
        }
        static string Saw(int n, int hidden)
        {
            var teeth = (n - 2) / 2; var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, "0"), (90, 2 + 2 * teeth), (70, 1)));
            sb.Append(G((10, 0), (20, 0), (10, 1000), (20, 0)));
            for (var k = teeth - 1; k >= 0; k--) { var x = 1000.0 * k / teeth; sb.Append(G((10, x + 1000.0 / teeth), (20, 100), (10, x + 500.0 / teeth), (20, 200))); }
            sb.Append(G((0, "HATCH"), (8, "AUS"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, hidden)));
            for (var q = 0; q < hidden; q++) { double x = 10 + q * 19, y = 20; sb.Append(PolyLoop([(x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1)])); }
            return sb.Append(G((75, 0), (76, 1), (98, 0))).ToString();
        }
        var q2 = Dxf(Big(300_000, 2000), AusLayer); var q7 = Dxf(Saw(362_054, 50), AusLayer);
        var r2 = CadDxf.Parse(q2); Assert.Empty(r2.Unread); Assert.Equal(1, r2.HiddenFillKinds["HATCH"]);
        var r7 = CadDxf.Parse(q7); Assert.Empty(r7.Unread);
        var t2 = Timed(() => CadDxf.Parse(q2)); var t7 = Timed(() => CadDxf.Parse(q7));
        var tc = CancelTime(q7); // T-0747-fix11: verlangt die Abbruch-Ausnahme wirklich
        log.WriteLine($"Kontur 300 000 + 2000 ausgeblendet: {t2} ms; Sägezahn 362 054 + 50 ausgeblendet: {t7} ms; Abbrechen: Ausnahme nach {tc} ms");
        Assert.True(tc < 3000, $"{tc}");
        Assert.True(t2 < 10_000 && t7 < 10_000, $"{t2} / {t7}"); // einzeln je etwa 1 s; Vorstand 16 s bzw. > 120 s
    }

    [Fact(DisplayName = "T-0747-fix10: ausgeblendete Schraffur einmal bei ihrer Ebene gemeldet („AUS (2, davon 1× Schraffur)“), nicht zusätzlich unter „Nicht gelesen“")]
    public void Hidden_fill_note_once()
    {
        var dxf = Dxf(Lw(Rect(0, 0, 100, 60)) + Circle(30, 30, 5) + G((0, "LINE"), (8, "AUS"), (10, 0), (20, 70), (30, 0), (11, 100), (21, 70), (31, 0))
            + Hatch("AUS", PolyLoop([(10, 10), (90, 10), (90, 50), (10, 50)])), AusLayer);
        var f = Path.Combine(Path.GetTempPath(), "t0747fix10-" + Guid.NewGuid().ToString("N")[..8] + ".dxf");
        try
        {
            File.WriteAllText(f, dxf);
            var r = new CncJob { ToolD = 1 }.ImportDrawing(f);
            log.WriteLine(string.Join(" | ", r.Warnings));
            Assert.Contains(r.Warnings, w => w.Contains("Ausgeblendete Ebenen nicht gefräst: AUS (2, davon 1× Schraffur)", StringComparison.Ordinal));
            Assert.DoesNotContain(r.Warnings, w => w.StartsWith("Nicht gelesen", StringComparison.Ordinal));
        }
        finally { File.Delete(f); }
    }
}
