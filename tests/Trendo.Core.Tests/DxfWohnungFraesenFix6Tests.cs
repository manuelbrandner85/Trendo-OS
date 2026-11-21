using System.Diagnostics;
using System.Globalization;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix6: Befunde der sechsten Prüfung – vorsichtige Ausdehnung nicht gelesener Elemente (Ausbuchtungen, Normale im Block, Fit-Punkt-Spline,
/// Blöcke nach ENTITIES, Xref), alle verworfenen Umrisse in derselben Auslass-Logik, ELLIPSE ohne Endlosschleife, begrenzte Punktzahl, Verhältnis > 1,
/// kulturunabhängige Prüfsumme, Ellipsen im Grundriss gemeldet.</summary>
public class T0747_Fix6SechstePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities, string blocks = "", bool blocksAfter = false)
    {
        var head = "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n";
        var b = blocks.Length > 0 ? $"0\nSECTION\n2\nBLOCKS\n{blocks}0\nENDSEC\n" : "";
        var e = $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n";
        return head + (blocksAfter ? e + b : b + e) + "0\nEOF\n";
    }
    static string Lw((double X, double Y, double B)[] pts, bool closed = true, string extra = "", string layer = "0") =>
        G((0, "LWPOLYLINE"), (8, layer), (90, pts.Length), (70, closed ? 1 : 0)) + string.Concat(pts.Select(p => G((10, p.X), (20, p.Y)) + (p.B != 0 ? G((42, p.B)) : ""))) + extra;
    static (double X, double Y, double B)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0, 0), (x1, y0, 0), (x1, y1, 0), (x0, y1, 0)];
    static string Circle(double x, double y, double r, string extra = "") => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r)) + extra;
    static string Block(string name, string content, int flags = 0) => G((0, "BLOCK"), (8, "0"), (2, name), (70, flags), (10, 0), (20, 0), (30, 0)) + content + G((0, "ENDBLK"), (8, "0"));
    static string Insert(string name, double x = 0, double y = 0) => G((0, "INSERT"), (8, "0"), (2, name), (10, x), (20, y), (30, 0));
    const string MinusZ = "210\n0.0\n220\n0.0\n230\n-1.0\n";
    static CamResult Cam(string dxf, double tool = 1) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = tool, Unread = rd.Unread }); }
    static CadDxf.Box One(string dxf) => Assert.Single(CadDxf.Parse(dxf).Unread).B;

    /// <summary>Wahre Ausdehnung eines Zugs mit Ausbuchtungen (fein abgetastet).</summary>
    static (double MinX, double MinY, double MaxX, double MaxY) TrueBox((double X, double Y, double B)[] v, bool closed)
    {
        var e = new CadEntity { Kind = "polylinie", Closed = closed, Points = v.Select(p => new[] { p.X, p.Y, p.B }).ToList() };
        var o = e.Outline(4096);
        return (o.Min(p => p.X), o.Min(p => p.Y), o.Max(p => p.X), o.Max(p => p.Y));
    }

    static void Covers(CadDxf.Box b, (double MinX, double MinY, double MaxX, double MaxY) t)
    {
        Assert.True(b.MinX <= t.MinX + 1e-6 && b.MinY <= t.MinY + 1e-6 && b.MaxX >= t.MaxX - 1e-6 && b.MaxY >= t.MaxY - 1e-6, $"Ausdehnung {b} deckt {t} nicht");
    }

    // ---------- M-1: vorsichtige Ausdehnung ----------

    [Fact(DisplayName = "T-0747-fix6 M-1: Ausdehnung im Block – alte POLYLINE mit Ausbuchtung (i9) und Normale −Z (i10), LWPOLYLINE mit Ausbuchtung 1,5 und 3 (i7/i8)")]
    public void Block_extent_bulges_and_normal()
    {
        // i9: Vollkreis aus zwei Halbkreisen (Ausbuchtung 1) als alte POLYLINE → y −50…50
        var poly = G((0, "POLYLINE"), (8, "0"), (66, 1), (10, 0), (20, 0), (30, 0), (70, 1))
            + G((0, "VERTEX"), (8, "0"), (10, 0), (20, 0), (30, 0), (42, 1.0), (70, 0)) + G((0, "VERTEX"), (8, "0"), (10, 100), (20, 0), (30, 0), (42, 1.0), (70, 0)) + G((0, "SEQEND"), (8, "0"));
        var b9 = One(Dxf(Insert("PB"), Block("PB", poly)));
        Covers(b9, TrueBox([(0, 0, 1), (100, 0, 1)], true));
        // i10: alte POLYLINE mit Normale −Z im Block → x −120…−20
        var polyN = G((0, "POLYLINE"), (8, "0"), (66, 1), (10, 0), (20, 0), (30, 0), (70, 1)) + MinusZ
            + string.Concat(Rect(20, 0, 120, 60).Select(p => G((0, "VERTEX"), (8, "0"), (10, p.X), (20, p.Y), (30, 0), (70, 0)))) + G((0, "SEQEND"), (8, "0"));
        var b10 = One(Dxf(Insert("PN"), Block("PN", polyN)));
        Assert.Equal(-120, b10.MinX, 6); Assert.Equal(-20, b10.MaxX, 6);
        // i7/i8: Ausbuchtung 3 (286°) und 1,5 – Bogen weit über die Sehne hinaus
        foreach (var bulge in new[] { 0.3, 1.0, 1.5, 3.0, -3.0, 12.0 })
        {
            (double X, double Y, double B)[] v = [(0, 0, bulge), (100, 0, 0)];
            var b = One(Dxf(Insert("B"), Block("B", Lw(v))));
            log.WriteLine($"Ausbuchtung {bulge}: {b} wahr {TrueBox(v, true)}");
            Covers(b, TrueBox(v, true));
        }
        // CAM: Loch innerhalb des wahren Kreises von i9 (bei y 40, außerhalb des Punkt-Rahmens) wird ausgelassen statt außen gefräst
        var r = Assert.Throws<InvalidOperationException>(() => Cam(Dxf(Insert("PB") + Circle(50, 40, 0.4), Block("PB", poly))));
        Assert.Contains("liegt in nicht gelesenem Element Blockverweis „PB“", r.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "T-0747-fix6 M-1/N-1: SPLINE nur mit Fit-Punkten (i16) und Xref-Block (i15) → unbekannt, überall; Blöcke nach ENTITIES (i14) werden gefunden")]
    public void Unknown_extents()
    {
        var fit = G((0, "SPLINE"), (8, "0"), (70, 11), (71, 3), (72, 0), (73, 0), (74, 4), (44, 1e-10))
            + string.Concat(Rect(0, 0, 100, 100).Select(p => G((11, p.X), (21, p.Y), (31, 0))));
        Assert.True(One(Dxf(fit)).IsAll);
        var ctrl = G((0, "SPLINE"), (8, "0"), (70, 8), (71, 3), (72, 8), (73, 4)) + string.Concat(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }.Select(k => G((40, k))))
            + G((10, 0), (20, 0), (30, 0), (10, 30), (20, 80), (30, 0), (10, 70), (20, -20), (30, 0), (10, 100), (20, 0), (30, 0));
        var c = One(Dxf(ctrl)); Assert.Equal((0.0, -20.0, 100.0, 80.0), (c.MinX, c.MinY, c.MaxX, c.MaxY)); // konvexe Hülle der Kontrollpunkte
        var ex = Assert.Throws<InvalidOperationException>(() => Cam(Dxf(fit + Circle(50, -10, 0.4) + Circle(50, 50, 0.4))));
        Assert.Contains("keine bestimmbare Ausdehnung", ex.Message, StringComparison.Ordinal);
        // Xref: Inhalt in anderer Datei
        Assert.True(One(Dxf(Insert("XR"), Block("XR", "", 4))).IsAll);
        // Blöcke nach den ENTITIES
        var late = One(Dxf(Insert("P"), Block("P", Lw(Rect(0, 0, 100, 60))), blocksAfter: true));
        Assert.Equal((0.0, 0.0, 100.0, 60.0), (late.MinX, late.MinY, late.MaxX, late.MaxY));
    }

    // ---------- M-2: alle verworfenen Umrisse ----------

    [Fact(DisplayName = "T-0747-fix6 M-2: Umriss als Kreis r < 0, LWPOLYLINE mit NaN, MLINE, REGION, nur HATCH – lose Löcher werden ausgelassen statt außen gefräst; HATCH auf gezeichneter Platte ändert nichts")]
    public void Unreadable_outlines_make_inside_unsure()
    {
        var mline = G((0, "MLINE"), (8, "0"), (2, "STANDARD"), (40, 1.0), (70, 0), (71, 3), (72, 4), (73, 2), (10, 0), (20, 0), (30, 0))
            + string.Concat(Rect(0, 0, 100, 60).Select(p => G((11, p.X), (21, p.Y), (31, 0), (12, 1), (22, 0), (32, 0), (13, 0), (23, 1), (33, 0), (74, 2), (41, 0), (41, 0), (75, 0), (74, 2), (41, -1), (41, 0), (75, 0))));
        var hatch = G((0, "HATCH"), (8, "0"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, 1), (92, 2), (72, 0), (73, 1), (93, 4))
            + string.Concat(Rect(0, 0, 100, 60).Select(p => G((10, p.X), (20, p.Y)))) + G((97, 0), (75, 0), (76, 1), (98, 0));
        var outlines = new (string Name, string Dxf, string Expect)[]
        {
            ("Kreis r −50", Circle(50, 30, -50), "CIRCLE mit Radius ≤ 0"),
            ("LWPOLYLINE mit NaN", G((0, "LWPOLYLINE"), (8, "0"), (90, 4), (70, 1), (10, 0), (20, 0), (10, 100), (20, 0), (10, 100), (20, "NaN"), (10, 0), (20, 60)), "LWPOLYLINE mit ungültiger Koordinate"),
            ("MLINE", mline, "MLINE"),
            ("REGION", G((0, "REGION"), (8, "0"), (70, 1), (1, "ACIS")), "REGION"),
            ("HATCH", hatch, "Füllfläche HATCH"),
        };
        foreach (var (name, outline, expect) in outlines)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Cam(Dxf(outline + Circle(30, 30, 5) + Circle(70, 20, 5))));
            log.WriteLine($"{name}: {ex.Message[..Math.Min(200, ex.Message.Length)]}");
            Assert.Contains($"Kontur 1 ausgelassen", ex.Message, StringComparison.Ordinal); Assert.Contains($"Kontur 2 ausgelassen", ex.Message, StringComparison.Ordinal);
            Assert.Contains(expect, ex.Message, StringComparison.Ordinal);
        }
        // HATCH mit gezeichnetem Rand (h1): Platte und Loch werden gefräst
        var r = Cam(Dxf(Lw(Rect(0, 0, 100, 60)) + hatch + Circle(50, 30, 10)), 3);
        Assert.Single(r.Paths, p => p.Kind == "innen"); Assert.Single(r.Paths, p => p.Kind == "aussen");
        // Kreis mit Radius 0 ist kein Umriss: Teil daneben bleibt
        var r0 = Cam(Dxf(Lw(Rect(0, 0, 100, 60)) + Circle(50, 30, 10) + Circle(85, 50, 0)), 3);
        Assert.Equal(2, r0.Paths.Count);
    }

    // ---------- M-3 / N-3 / N-4: ELLIPSE ----------

    [Fact(DisplayName = "T-0747-fix6 M-3: ELLIPSE mit Parameter 1e17 bzw. −1e300 hängt nicht, wird verworfen und gemeldet; große Parameter werden per Rest normalisiert")]
    public void Ellipse_extreme_parameters()
    {
        static string Ell(object t0, object t1) => G((0, "ELLIPSE"), (8, "0"), (10, 50), (20, 30), (30, 0), (11, 30), (21, 0), (31, 0), (40, 0.5), (41, t0), (42, t1));
        var sw = Stopwatch.StartNew();
        var rd = CadDxf.Parse(Dxf(Ell(1e17, 1) + Ell(0, -1e300)));
        log.WriteLine($"{sw.ElapsedMilliseconds} ms: {string.Join(", ", rd.Skipped.Select(k => $"{k.Value}× {k.Key}"))}");
        Assert.True(sw.ElapsedMilliseconds < 2000);
        Assert.Equal(2, rd.Skipped["ELLIPSE mit unsinnigem Parameter"]); Assert.Empty(rd.Drawing.Entities);
        Assert.All(rd.Unread, u => { Assert.Equal(20, u.B.MinX, 6); Assert.Equal(80, u.B.MaxX, 6); Assert.Equal(15, u.B.MinY, 6); Assert.Equal(45, u.B.MaxY, 6); });
        // 4π + 0,3 … 4π + 2,5 ist dieselbe Teilellipse wie 0,3 … 2,5
        var a = Assert.Single(CadDxf.Parse(Dxf(Ell(0.3, 2.5))).Drawing.Entities); var b = Assert.Single(CadDxf.Parse(Dxf(Ell(4 * Math.PI + 0.3, 4 * Math.PI + 2.5))).Drawing.Entities);
        Assert.Equal(a.Points[0][0], b.Points[0][0], 6); Assert.Equal(a.Points[^1][1], b.Points[^1][1], 6); Assert.False(b.Closed);
    }

    [Fact(DisplayName = "T-0747-fix6 N-3/N-4: flache Ellipse wenige Punkte und Pfeilhöhe ≤ 0,001 mm auch nach dem Verketten; Verhältnis 1,5 richtig; zu große Ellipse gemeldet")]
    public void Ellipse_points_ratio_limit()
    {
        static string Ell(double ax, double ay, double ratio) => G((0, "ELLIPSE"), (8, "0"), (10, 0), (20, 0), (30, 0), (11, ax), (21, ay), (31, 0), (40, ratio), (41, 0), (42, 2 * Math.PI));
        foreach (var (ax, ay, ratio) in new[] { (100.0, 0.0, 0.001), (1500.0, 800.0, 0.02), (40.0, 0.0, 1.5), (30.0, 40.0, 0.5) })
        {
            var d = CadDxf.Parse(Dxf(Ell(ax, ay, ratio))).Drawing; var e = Assert.Single(d.Entities);
            var c = Assert.Single(CncCam.Contours(d));
            double a = Math.Sqrt(ax * ax + ay * ay); double ux = ax / a, uy = ay / a; var b = a * ratio;
            double Par((double X, double Y) p) => Math.Atan2((-p.X * uy + p.Y * ux) / b, (p.X * ux + p.Y * uy) / a);
            (double X, double Y) On(double t) => (a * Math.Cos(t) * ux - b * Math.Sin(t) * uy, a * Math.Cos(t) * uy + b * Math.Sin(t) * ux);
            var worst = 0.0;
            for (var i = 0; i < c.Pts.Count; i++)
            {
                var p = c.Pts[i]; var q = c.Pts[(i + 1) % c.Pts.Count]; var t0 = Par(p); var dt = Math.IEEERemainder(Par(q) - t0, 2 * Math.PI);
                for (var k = 1; k < 16; k++) worst = Math.Max(worst, CncCam.SegDist(On(t0 + dt * k / 16), p, q));
            }
            log.WriteLine($"Ellipse {ax}/{ay} Verhältnis {ratio}: {e.Points.Count} Punkte, nach Verketten {c.Pts.Count}, Pfeilhöhe {worst:0.000000} mm");
            Assert.True(worst <= 0.001 + 1e-9, $"{worst}");
            Assert.True(e.Points.Count < 5000, $"{e.Points.Count}");
        }
        // Halbachse 5e8 mm bräuchte über 200 000 Punkte → nicht abgetastet, gemeldet, Ausdehnung bekannt
        var big = CadDxf.Parse(Dxf(Ell(5e8, 0, 0.5)));
        Assert.Equal(1, big.Skipped["ELLIPSE zu groß zum Abtasten"]); Assert.Empty(big.Drawing.Entities); Assert.Equal(-5e8, Assert.Single(big.Unread).B.MinX, 0);
    }

    // ---------- N-4: Prüfsumme kulturunabhängig ----------

    [Fact(DisplayName = "T-0747-fix6 N-4: Prüfsumme der Konturen mit nicht gelesenen Elementen ist in jeder Sprache gleich")]
    public void Contours_hash_invariant()
    {
        var job = new CncJob { Unread = [("SPLINE", new CadDxf.Box(1.5, 2.25, 3.125, 4.75))] };
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); var de = job.ContoursHash();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); var en = job.ContoursHash();
            Assert.Equal(de, en);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    // ---------- N-2: Grundriss ----------

    [Fact(DisplayName = "T-0747-fix6 N-2: elliptische Wand im Grundriss wird als nicht gelesen gemeldet statt still zu verschwinden")]
    public void Floor_plan_reports_ellipse()
    {
        var ell = G((0, "ELLIPSE"), (8, "WAND"), (10, 2000), (20, 1500), (30, 0), (11, 800), (21, 0), (31, 0), (40, 0.5), (41, 0), (42, 2 * Math.PI));
        var d = CadDxf.Parse(Dxf(Lw([(0, 0, 0), (4000, 0, 0)], false, layer: "WAND") + Lw([(0, 200, 0), (4000, 200, 0)], false, layer: "WAND") + ell));
        var rep = PlanDxfImport.Import(new FloorPlan(), d);
        Assert.Equal(1, rep.SkippedKinds["ELLIPSE (nicht als Wand übernommen)"]);
    }
}
