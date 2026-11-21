using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix2: Befunde der zweiten unabhängigen Prüfung – Konturen in abgelehnten Achtern, offene Konturen, nicht lesbare Elemente,
/// große Radien und Bézierkurven, Laufzeit/Abbrechen, kurze Engstellen. Abstände werden gegen die WAHRE Geometrie (Kreis, Kurvenformel) gemessen,
/// mit eigenem G-Code-Leser – nicht mit Trendos Konturen.</summary>
public class T0747_Fix2ZweitePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static List<((double X, double Y) A, (double X, double Y) B)> CutMoves(string gcode)
    {
        var res = new List<((double, double), (double, double))>(); double x = 0, y = 0, z = 0;
        foreach (var raw in gcode.Split('\n'))
        {
            var line = System.Text.RegularExpressions.Regex.Replace(raw, @"\(.*?\)", "").Trim(); if (line.Length == 0) continue;
            double nx = x, ny = y, nz = z; var g1 = false;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line, @"([GXYZ])\s*(-?[\d.]+)"))
            {
                var v = double.Parse(m.Groups[2].Value, Inv);
                switch (m.Groups[1].Value) { case "G": g1 = v == 1; break; case "X": nx = v; break; case "Y": ny = v; break; case "Z": nz = v; break; }
            }
            if (g1 && z < 0 && nz < 0 && (Math.Abs(nx - x) > 1e-9 || Math.Abs(ny - y) > 1e-9)) res.Add(((x, y), (nx, ny)));
            x = nx; y = ny; z = nz;
        }
        return res.Distinct().ToList();
    }

    static double PtSeg((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l = dx * dx + dy * dy, t = l == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l, 0, 1);
        return Math.Sqrt(Math.Pow(a.X + t * dx - p.X, 2) + Math.Pow(a.Y + t * dy - p.Y, 2));
    }

    static List<(double X, double Y)> Circle(double cx, double cy, double r, int n = 400) => Enumerable.Range(0, n).Select(k => (cx + r * Math.Cos(2 * Math.PI * k / n), cy + r * Math.Sin(2 * Math.PI * k / n))).ToList();
    static CadContour Rect(double x0, double y0, double x1, double y1) => new([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], true, "0");

    static string Dxf(int units, string body, string tables = "") =>
        $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n{units}\n0\nENDSEC\n{tables}0\nSECTION\n2\nENTITIES\n{body}0\nENDSEC\n0\nEOF\n";
    static string Line(string layer, double x1, double y1, double x2, double y2) => string.Create(Inv, $"0\nLINE\n8\n{layer}\n10\n{x1}\n20\n{y1}\n11\n{x2}\n21\n{y2}\n");

    static string TempFile(string ext, string text) { var f = Path.Combine(Path.GetTempPath(), "t0747fix2-" + Guid.NewGuid().ToString("N")[..8] + ext); File.WriteAllText(f, text); return f; }

    // ---------- 1: Konturen in oder an einer abgelehnten Acht ----------

    [Fact(DisplayName = "T-0747-fix2 1: x1/x2/x3 – Insel im Achter-Loch, Loch im Achter-Umriss werden ausgelassen statt auf der falschen Seite gefräst")]
    public void Contours_inside_rejected_eight()
    {
        List<(double X, double Y)> eight = [(20, 20), (50, 50), (50, 20), (20, 50)];
        // x1: Platte, Achter-Loch, Insel darin (Fräser Ø 1)
        var x1 = CncCam.Generate([Rect(0, 0, 100, 100), new CadContour(eight, true, "0"), new CadContour(Circle(25, 35, 2), true, "0")], new CamSettings { ToolD = 1 });
        Assert.Equal([0], x1.Paths.Select(p => p.Source).Distinct());
        Assert.Contains(x1.Warnings, w => w.StartsWith("Kontur 3 ausgelassen: liegt in oder an Kontur 2", StringComparison.Ordinal));
        // x2: Achter-Umriss mit Loch Ø 16 – früher wurde das Loch als „außen“ gefräst und dadurch Ø 22
        List<(double X, double Y)> big8 = [(0, 0), (100, 100), (100, 0), (0, 100)];
        var e = Assert.Throws<InvalidOperationException>(() => CncCam.Generate([new CadContour(big8, true, "0"), new CadContour(Circle(20, 50, 8), true, "0")], new CamSettings { ToolD = 3 }));
        Assert.Contains("Kontur 2 ausgelassen: liegt in oder an Kontur 1", e.Message);
        // x3: dazu ein freies Teil daneben – nur dieses wird gefräst
        var x3 = CncCam.Generate([new CadContour(big8, true, "0"), new CadContour(Circle(20, 50, 8), true, "0"), Rect(130, 0, 180, 50)], new CamSettings { ToolD = 3 });
        Assert.Equal([2], x3.Paths.Select(p => p.Source).Distinct());
        Assert.All(CutMoves(x3.Gcode), m => Assert.True(m.A.X >= 125 && m.B.X >= 125, $"{m}"));
    }

    // ---------- 2: offene Konturen ----------

    [Fact(DisplayName = "T-0747-fix2 2: Rechteck aus 4 LINE mit 0,05 mm Spalt wird geschlossen; 1 mm Spalt → offen, ausgelassen und gemeldet, nur als Gravur gefräst")]
    public void Open_contours()
    {
        static string Box(double gap) => Line("0", 0, 0, 40, 0) + Line("0", 40, 0, 40, 30) + Line("0", 40, 30, 0, 30) + Line("0", 0, 30, 0, gap);
        var closed = CncCam.Contours(CadDxf.Parse(Dxf(4, Box(0.05))).Drawing);
        var c = Assert.Single(closed); Assert.True(c.Closed);
        var r = CncCam.Generate(closed, new CamSettings { ToolD = 6 });
        Assert.Equal(["aussen"], r.Paths.Select(p => p.Kind)); Assert.Empty(r.Warnings);
        // unabhängig: jede Schnittbewegung gegen die vier wahren Linien (Strecke zu Strecke, Spalt 0,05 mm eingeschlossen)
        (double X, double Y)[][] truth = [[(0, 0), (40, 0)], [(40, 0), (40, 30)], [(40, 30), (0, 30)], [(0, 30), (0, 0.05)], [(0, 0.05), (0, 0)]];
        var worst = CutMoves(r.Gcode).Min(m => truth.Min(t => Math.Min(Math.Min(PtSeg(m.A, t[0], t[1]), PtSeg(m.B, t[0], t[1])), Math.Min(PtSeg(t[0], m.A, m.B), PtSeg(t[1], m.A, m.B)))));
        Assert.True(worst >= 3 - 0.002, $"{worst}");
        var open = CncCam.Contours(CadDxf.Parse(Dxf(4, Box(1))).Drawing);
        Assert.False(Assert.Single(open).Closed);
        var e = Assert.Throws<InvalidOperationException>(() => CncCam.Generate(open, new CamSettings { ToolD = 6 }));
        Assert.Contains("offene Kontur", e.Message); Assert.Contains("um den Fräserradius kleiner", e.Message);
        var g = CncCam.Generate(open, new CamSettings { ToolD = 6, Mode = "auf" });
        Assert.Equal(["auf"], g.Paths.Select(p => p.Kind));
    }

    // ---------- 3: nicht lesbare Elemente ----------

    [Fact(DisplayName = "T-0747-fix2 3: SPLINE, ELLIPSE, Blockverweis, Texte, ausgeblendete Ebene und Zoll werden beim CNC-Import gemeldet")]
    public void Skipped_elements_reported()
    {
        var tables = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nAUS\n70\n0\n62\n-7\n0\nENDTAB\n0\nENDSEC\n";
        var body = Line("0", 0, 0, 2, 0) + Line("0", 2, 0, 2, 1) + Line("0", 2, 1, 0, 1) + Line("0", 0, 1, 0, 0) + Line("AUS", 5, 5, 6, 6)
            + "0\nSPLINE\n8\n0\n0\nSPLINE\n8\n0\n0\nELLIPSE\n8\n0\n0\nINSERT\n8\n0\n2\nBLOCK1\n0\nTEXT\n8\n0\n10\n0\n20\n0\n40\n0.1\n1\nTeil A\n";
        var f = TempFile(".dxf", Dxf(1, body, tables));
        try
        {
            var r = new CncJob { ToolD = 3 }.ImportDrawing(f);
            var all = string.Join(" | ", r.Warnings); log.WriteLine(all);
            Assert.Contains("Maße in mm umgerechnet (Zeichnung in Zoll)", all);
            Assert.Contains("Nicht gelesen: 2× SPLINE", all); Assert.Contains("1× ELLIPSE", all); Assert.Contains("1× Blockverweis (INSERT)", all);
            Assert.Contains("1 Text(e) werden nicht gefräst", all);
            Assert.Contains("Ausgeblendete Ebenen nicht gefräst: AUS (1)", all);
        }
        finally { File.Delete(f); }
    }

    // ---------- 4: große Radien und lange Bézierkurven ----------

    [Theory(DisplayName = "T-0747-fix2 4: Kreis R 1000 / 8000 / 20000 – Bahn nie näher als Radius − 0,002 mm am WAHREN Kreis")]
    [InlineData(1000.0)]
    [InlineData(8000.0)]
    [InlineData(20000.0)]
    public void Big_radius_true_circle(double R)
    {
        var body = string.Create(Inv, $"0\nCIRCLE\n8\n0\n10\n0\n20\n0\n40\n{R}\n");
        var cs = CncCam.Contours(CadDxf.Parse(Dxf(4, body)).Drawing);
        var sw = Stopwatch.StartNew(); var r = CncCam.Generate(cs, new CamSettings { ToolD = 6 }); var ms = sw.ElapsedMilliseconds;
        var c = (r.ShiftX, r.ShiftY); var worst = double.MaxValue;
        foreach (var (a, b) in CutMoves(r.Gcode)) worst = Math.Min(worst, Math.Min(Math.Sqrt(Math.Pow(a.X - c.ShiftX, 2) + Math.Pow(a.Y - c.ShiftY, 2)), PtSeg(c, a, b)) - R);
        log.WriteLine($"R {R}: {cs[0].Pts.Count} Konturpunkte, kleinster Abstand zum wahren Kreis {worst:0.00000} mm (Radius 3), {ms} ms");
        Assert.True(worst >= 3 - 0.002, $"{worst}");
    }

    [Fact(DisplayName = "T-0747-fix2 4: lange SVG-Bézierkurve wird nach Pfeilhöhe abgetastet – Bahn nie näher als Radius − 0,002 mm an der wahren Kurve")]
    public void Long_bezier_true_curve()
    {
        // geschlossene Form: Bézierbogen über 3000 mm, unten gerade
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"3000mm\" height=\"1600mm\" viewBox=\"0 0 3000 1600\"><path d=\"M0 1500 C 0 -500 3000 -500 3000 1500 Z\"/></svg>";
        var cs = CncCam.Contours(CadSvg.Parse(svg).Drawing);
        var r = CncCam.Generate(cs, new CamSettings { ToolD = 6 });
        // wahre Kurve (y nach oben gespiegelt wie im Leser), dicht abgetastet
        (double X, double Y) B(double t) { var u = 1 - t; double x = 3 * u * u * t * 0 + 3 * u * t * t * 3000 + t * t * t * 3000, y = u * u * u * 1500 + 3 * u * u * t * -500 + 3 * u * t * t * -500 + t * t * t * 1500; return (x + r.ShiftX, -y + r.ShiftY); }
        var truth = Enumerable.Range(0, 200_001).Select(k => B(k / 200_000.0)).ToList();
        var moves = CutMoves(r.Gcode);
        // Raster der Bahnstrecken (10 mm)
        var grid = new Dictionary<(int, int), List<int>>();
        for (var i = 0; i < moves.Count; i++)
        {
            var (a, b) = moves[i];
            for (var gx = (int)Math.Floor(Math.Min(a.X, b.X) / 10); gx <= (int)Math.Floor(Math.Max(a.X, b.X) / 10); gx++)
                for (var gy = (int)Math.Floor(Math.Min(a.Y, b.Y) / 10); gy <= (int)Math.Floor(Math.Max(a.Y, b.Y) / 10); gy++)
                { if (!grid.TryGetValue((gx, gy), out var l)) grid[(gx, gy)] = l = []; l.Add(i); }
        }
        var worst = double.MaxValue;
        foreach (var p in truth)
            for (var gx = (int)Math.Floor((p.X - 4) / 10); gx <= (int)Math.Floor((p.X + 4) / 10); gx++)
                for (var gy = (int)Math.Floor((p.Y - 4) / 10); gy <= (int)Math.Floor((p.Y + 4) / 10); gy++)
                    if (grid.TryGetValue((gx, gy), out var l)) foreach (var i in l) worst = Math.Min(worst, PtSeg(p, moves[i].A, moves[i].B));
        log.WriteLine($"Bézier 3000 mm: {cs[0].Pts.Count} Konturpunkte, kleinster Abstand Bahn–wahre Kurve {worst:0.00000} mm (Radius 3)");
        Assert.True(worst >= 3 - 0.002, $"{worst}");
    }

    // ---------- 5: Laufzeit und Abbrechen ----------

    [Fact(DisplayName = "T-0747-fix2 5: Zickzack mit 5000 Punkten – schneller als vorher, Abbrechen wirkt auch mitten im Versatz")]
    public void Zigzag_speed_and_cancel()
    {
        var pts = new List<(double X, double Y)>();
        for (var k = 0; k < 2500; k++) { pts.Add((k * 0.2, 0)); pts.Add((k * 0.2 + 0.1, 10)); }
        pts.Add((500, -20)); pts.Add((0, -20));
        var cs = new List<CadContour> { new(pts, true, "0") };
        var sw = Stopwatch.StartNew(); CncCam.Generate(cs, new CamSettings { ToolD = 6 }); var ms = sw.ElapsedMilliseconds;
        if (ms > 2000) { sw.Restart(); CncCam.Generate(cs, new CamSettings { ToolD = 6 }); ms = Math.Min(ms, sw.ElapsedMilliseconds); } // T-0747-fix12: unter Fremdlast einmal wiederholt
        long cancelMs = long.MaxValue;
        for (var attempt = 0; attempt < 2 && cancelMs >= 1500; attempt++)
        {
            using var cts = new CancellationTokenSource(); cts.CancelAfter(100); sw.Restart();
            Assert.ThrowsAny<OperationCanceledException>(() => CncCam.Generate(cs, new CamSettings { ToolD = 6 }, cts.Token));
            cancelMs = Math.Min(cancelMs, sw.ElapsedMilliseconds);
        }
        log.WriteLine($"Zickzack 5000 Punkte: {ms} ms; Abbrechen nach 100 ms wirkte nach {cancelMs} ms");
        Assert.True(cancelMs < 1500, $"{cancelMs}"); Assert.True(ms < 8000, $"{ms}");
    }

    // ---------- 6: kurze Engstellen ----------

    [Fact(DisplayName = "T-0747-fix2 6: kurze Engstelle (spitze Kerbe 0,2 mm breit) wird gemeldet, auch wenn nur ein Messpunkt sie trifft")]
    public void Short_narrow_spot_reported()
    {
        List<(double X, double Y)> hole = [(20, 20), (80, 20), (80, 60), (50.2, 60), (50.1, 61.8), (50, 60), (20, 60)];
        var r = CncCam.Generate([Rect(0, 0, 100, 80), new CadContour(hole, true, "0")], new CamSettings { ToolD = 6 });
        log.WriteLine(string.Join(" | ", r.Warnings));
        Assert.Contains(r.Warnings, w => w.StartsWith("Kontur 2: Bereich enger als der Fräser", StringComparison.Ordinal) && w.Contains("X 50", StringComparison.Ordinal));
    }
}
