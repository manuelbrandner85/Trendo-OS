using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Trendo.Core.Media;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix: Befunde der Gegenprüfung – Nachbarkonturen, Engstellen, verrauschte Konturen, große Dateien, veraltete Bahnen, Kodierung.
/// Der Abstand Fräsermitte–Kontur wird hier unabhängig vom Trendo-Code geprüft: eigener G-Code-Leser, eigener Strecke-zu-Strecke-Abstand.</summary>
public class T0747_FixGegenpruefung(ITestOutputHelper log)
{
    const double Tol = 0.0011; // Toleranz der Bahn (0,001 mm) + Rundung im G-Code (4 Stellen)

    // ---------- unabhängiger Prüfer ----------

    /// <summary>Alle Schnittbewegungen (G1 mit Z &lt; 0 an Anfang und Ende, Lage ändert sich) aus dem G-Code – eigener, kleiner Leser.</summary>
    static List<((double X, double Y) A, (double X, double Y) B)> CutMoves(string gcode)
    {
        var res = new List<((double, double), (double, double))>(); double x = 0, y = 0, z = 0;
        foreach (var raw in gcode.Split('\n'))
        {
            var line = System.Text.RegularExpressions.Regex.Replace(raw, @"\(.*?\)", "").Trim().ToUpperInvariant(); if (line.Length == 0) continue;
            double nx = x, ny = y, nz = z; var g1 = false;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line, @"([GXYZ])\s*(-?[\d.]+)"))
            {
                var v = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                switch (m.Groups[1].Value) { case "G": g1 = v == 1; break; case "X": nx = v; break; case "Y": ny = v; break; case "Z": nz = v; break; }
            }
            if (g1 && z < 0 && nz < 0 && (Math.Abs(nx - x) > 1e-9 || Math.Abs(ny - y) > 1e-9)) res.Add(((x, y), (nx, ny)));
            x = nx; y = ny; z = nz;
        }
        return res;
    }

    static double PtSeg((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l = dx * dx + dy * dy, t = l == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l, 0, 1);
        return Math.Sqrt(Math.Pow(a.X + t * dx - p.X, 2) + Math.Pow(a.Y + t * dy - p.Y, 2));
    }

    static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    static double SegSeg((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        if ((d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0) && d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0) return 0;
        return Math.Min(Math.Min(PtSeg(a, c, d), PtSeg(b, c, d)), Math.Min(PtSeg(c, a, b), PtSeg(d, a, b)));
    }

    /// <summary>Kleinster Abstand der Schnittbewegungen zu jeder geschlossenen Kontur (Strecke zu Strecke).</summary>
    static double MinClearance(string gcode, IEnumerable<CadContour> contours)
    {
        var moves = CutMoves(gcode).Distinct().ToList(); var min = double.MaxValue;
        foreach (var c in contours.Where(c => c.Closed))
            for (var i = 0; i < c.Pts.Count; i++)
            {
                var a = c.Pts[i]; var b = c.Pts[(i + 1) % c.Pts.Count];
                foreach (var (p, q) in moves)
                {
                    if (Math.Max(p.X, q.X) < Math.Min(a.X, b.X) - min || Math.Min(p.X, q.X) > Math.Max(a.X, b.X) + min) continue;
                    if (Math.Max(p.Y, q.Y) < Math.Min(a.Y, b.Y) - min || Math.Min(p.Y, q.Y) > Math.Max(a.Y, b.Y) + min) continue;
                    min = Math.Min(min, SegSeg(p, q, a, b));
                }
            }
        return min;
    }

    static CadContour Rect(double x0, double y0, double x1, double y1, string layer = "0") => new([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], true, layer);
    static readonly CamSettings S6 = new() { ToolD = 6, Depth = 3, StepDown = 1.5 };

    // ---------- Befund 1: Nachbarkonturen ----------

    [Fact(DisplayName = "T-0747-fix 1: zwei Teile 3 mm auseinander, Fräser Ø 6 – keine Bahn fährt durchs Nachbarteil, beide werden gemeldet")]
    public void Neighbours_too_close()
    {
        List<CadContour> cs = [Rect(0, 0, 40, 30), Rect(43, 0, 83, 30), Rect(0, 60, 40, 90)]; // das dritte Teil steht frei
        var r = CncCam.Generate(cs, S6);
        Assert.Equal([2], r.Paths.Select(p => p.Source).Distinct());
        Assert.Contains(r.Warnings, w => w.Contains("Kontur 1 ausgelassen: zu nah an Kontur 2", StringComparison.Ordinal));
        Assert.Contains(r.Warnings, w => w.Contains("Kontur 2 ausgelassen: zu nah an Kontur 1", StringComparison.Ordinal));
        var min = MinClearance(r.Gcode, r.Contours); log.WriteLine($"kleinster Abstand {min:0.0000} mm");
        Assert.True(min >= 3 - Tol, $"{min}");
    }

    [Fact(DisplayName = "T-0747-fix 1: Insel 2 mm neben dem Lochrand – die Innenbahn fährt nicht durch die Insel")]
    public void Island_near_hole_edge()
    {
        List<CadContour> cs = [Rect(0, 0, 100, 80), Rect(20, 20, 60, 60), Rect(22, 30, 32, 40)];
        var r = CncCam.Generate(cs, S6);
        Assert.Equal(["aussen"], r.Paths.Select(p => p.Kind)); // nur der Umriss bleibt
        Assert.Contains(r.Warnings, w => w.Contains("Kontur 2 ausgelassen: zu nah an Kontur 3", StringComparison.Ordinal));
        Assert.Contains(r.Warnings, w => w.Contains("Kontur 3 ausgelassen: zu nah an Kontur 2", StringComparison.Ordinal));
        Assert.True(MinClearance(r.Gcode, r.Contours) >= 3 - Tol);
        // Insel weit genug weg: Loch und Insel werden gefräst, ohne sich zu berühren
        var ok = CncCam.Generate([Rect(0, 0, 100, 80), Rect(20, 20, 60, 60), Rect(35, 35, 45, 45)], S6);
        Assert.Equal(3, ok.Paths.Count); Assert.True(MinClearance(ok.Gcode, ok.Contours) >= 3 - Tol);
    }

    [Fact(DisplayName = "T-0747-fix 1: Achterform (Kontur kreuzt sich selbst) wird abgelehnt und gemeldet")]
    public void Figure_eight_rejected()
    {
        List<(double X, double Y)> eight = [(0, 0), (20, 20), (20, 0), (0, 20)];
        var e = Assert.Throws<InvalidOperationException>(() => CncCam.Generate([new CadContour(eight, true, "0")], S6));
        Assert.Contains("kreuzt sich selbst", e.Message);
        var r = CncCam.Generate([new CadContour(eight, true, "0"), Rect(50, 0, 80, 30)], S6);
        Assert.DoesNotContain(r.Paths, p => p.Source == 0);
        Assert.Contains(r.Warnings, w => w.StartsWith("Kontur 1 ausgelassen", StringComparison.Ordinal) && w.Contains("kreuzt sich selbst", StringComparison.Ordinal));
        // Acht mit dem Kreuzungspunkt als Eckpunkt
        var r2 = CncCam.Generate([new CadContour([(0, 0), (10, 10), (20, 0), (20, 20), (10, 10), (0, 20)], true, "0"), Rect(50, 0, 80, 30)], S6);
        Assert.DoesNotContain(r2.Paths, p => p.Source == 0);
        Assert.True(MinClearance(r2.Gcode, r2.Contours) >= 3 - Tol);
    }

    // ---------- Befund 2: Engstellen ----------

    /// <summary>Hantel-Loch: zwei Kreise R 10 (Mitten 30/30 und 90/30), verbunden durch einen 3 mm breiten Steg.</summary>
    static CadContour Dumbbell()
    {
        var pts = new List<(double X, double Y)>(); var a = Math.Asin(1.5 / 10);
        for (var k = 0; k <= 180; k++) { var t = a + (2 * Math.PI - 2 * a) * k / 180; pts.Add((30 + 10 * Math.Cos(t), 30 + 10 * Math.Sin(t))); }
        for (var k = 0; k <= 180; k++) { var t = Math.PI + a + (2 * Math.PI - 2 * a) * k / 180; pts.Add((90 + 10 * Math.Cos(t), 30 + 10 * Math.Sin(t))); }
        return new CadContour(pts, true, "0");
    }

    [Fact(DisplayName = "T-0747-fix 2: Hantel-Loch mit 3-mm-Steg – beide Kammern werden gefräst, der Steg wird mit Lage gemeldet")]
    public void Dumbbell_hole()
    {
        var r = CncCam.Generate([Rect(0, 0, 120, 60), Dumbbell()], S6);
        Assert.Equal(2, r.Paths.Count(p => p.Kind == "innen"));
        var w = Assert.Single(r.Warnings, x => x.Contains("enger als der Fräser", StringComparison.Ordinal));
        Assert.Contains("Kontur 2", w); Assert.Contains("Material stehen", w); Assert.Matches(@"X (5\d|6\d)", w); log.WriteLine(w); // Lage: im Steg zwischen X 40 und 80
        Assert.True(MinClearance(r.Gcode, r.Contours) >= 3 - Tol);
    }

    [Fact(DisplayName = "T-0747-fix 2: 3-mm-Schlitz außen – Umriss wird gefräst, der Schlitz wird gemeldet; leerer Versatz nennt den Grund")]
    public void Slot_outside_and_empty_reason()
    {
        List<(double X, double Y)> part = [(0, 0), (60, 0), (60, 40), (31.5, 40), (31.5, 25), (28.5, 25), (28.5, 40), (0, 40)];
        var r = CncCam.Generate([new CadContour(part, true, "0")], S6);
        Assert.Single(r.Paths);
        var w = Assert.Single(r.Warnings); Assert.Contains("enger als der Fräser", w); Assert.Contains("Material stehen", w); Assert.Contains("X 30", w); // Lage: Schlitz bei X 30
        Assert.True(MinClearance(r.Gcode, r.Contours) >= 3 - Tol);
        // Loch kleiner als der Fräser: Grund wird genannt
        var small = CncCam.Generate([Rect(0, 0, 40, 30), Rect(10, 10, 14, 14)], S6);
        Assert.Contains(small.Warnings, x => x.StartsWith("Kontur 2", StringComparison.Ordinal) && x.Contains("zu klein", StringComparison.Ordinal));
        // ohne Engstelle: keine Meldung (rechte Innenecken sind normal)
        Assert.Empty(CncCam.Generate([Rect(0, 0, 60, 40), Rect(10, 10, 30, 30)], S6).Warnings);
    }

    // ---------- Befund 3: verrauschte Konturen ----------

    [Theory(DisplayName = "T-0747-fix 3: Kreis R 50 mit Rauschen wird gefräst, Abstand ≥ Radius")]
    [InlineData(0.05, 500)]
    [InlineData(0.05, 2000)]
    [InlineData(0.5, 500)]
    [InlineData(0.5, 2000)]
    public void Noisy_circle(double noise, int n)
    {
        var rnd = new Random(4711);
        var pts = Enumerable.Range(0, n).Select(k => { var t = 2 * Math.PI * k / n; var rr = 50 + noise * (2 * rnd.NextDouble() - 1); return (rr * Math.Cos(t), rr * Math.Sin(t)); }).ToList();
        foreach (var mode in new[] { "auto", "innen" })
        {
            var sw = Stopwatch.StartNew();
            var r = CncCam.Generate([new CadContour(pts, true, "0")], S6 with { Mode = mode });
            var p = Assert.Single(r.Paths);
            var min = MinClearance(r.Gcode, r.Contours);
            log.WriteLine($"Rauschen {noise} mm, {n} Punkte, {mode}: {p.Pts.Count} Bahnpunkte, kleinster Abstand {min:0.0000} mm, {sw.ElapsedMilliseconds} ms, Hinweise: {string.Join(" | ", r.Warnings)}");
            Assert.True(min >= 3 - Tol, $"{min}");
            // die Bahn liegt dicht an der Kontur (Fräser erreicht sie): mittlerer Abstand höchstens Radius + Rauschen
            var c = r.Contours[0].Pts; var far = p.Pts.Average(q => c.Min(z => Math.Sqrt((z.X - q.X) * (z.X - q.X) + (z.Y - q.Y) * (z.Y - q.Y))));
            Assert.True(far <= 3 + 2 * noise + 0.2, $"{far}");
        }
    }

    [Fact(DisplayName = "T-0747-fix 1–3: Zufallsformen (Sterne, Zacken, Rauschen), Fräser 1–10 mm – Außenbahn immer da, nie näher als der Radius")]
    public void Random_shapes_never_gouge()
    {
        var rnd = new Random(20261118); var worst = double.MaxValue; var innen = 0;
        for (var run = 0; run < 40; run++)
        {
            var n = rnd.Next(40, 600); var spikes = rnd.Next(3, 12); var amp = rnd.NextDouble() * 15; var noise = rnd.NextDouble() * 0.6;
            var pts = Enumerable.Range(0, n).Select(k => { var t = 2 * Math.PI * k / n; var rr = 35 + amp * Math.Sin(spikes * t) + noise * (2 * rnd.NextDouble() - 1); return (rr * Math.Cos(t), rr * Math.Sin(t)); }).ToList();
            var tool = 1 + rnd.NextDouble() * 9; var s = new CamSettings { ToolD = tool, Depth = 1, StepDown = 1 };
            var outer = CncCam.Generate([new CadContour(pts, true, "0")], s);
            Assert.Contains(outer.Paths, p => p.Kind == "aussen");
            var min = MinClearance(outer.Gcode, outer.Contours); worst = Math.Min(worst, min - tool / 2);
            Assert.True(min >= tool / 2 - Tol, $"Lauf {run}: außen {min} < {tool / 2}");
            try
            {
                var inner = CncCam.Generate([new CadContour(pts, true, "0")], s with { Mode = "innen" }); innen++;
                var mi = MinClearance(inner.Gcode, inner.Contours); worst = Math.Min(worst, mi - tool / 2);
                Assert.True(mi >= tool / 2 - Tol, $"Lauf {run}: innen {mi} < {tool / 2}");
            }
            catch (InvalidOperationException e) { Assert.Contains("zu klein", e.Message); }
        }
        log.WriteLine($"40 Zufallsformen: {innen} mit Innenbahn, kleinster Abstand minus Radius {worst:0.000000} mm");
        Assert.True(innen >= 30);
    }

    [Fact(DisplayName = "T-0747-fix: Belegdateien – alle Gegenprüfungs-Fälle in einer DXF (LINE-Ketten) → G-Code + Hinweise (Ordner über T0747_BELEG)")]
    public void Beleg_fix_cases()
    {
        var dir = Environment.GetEnvironmentVariable("T0747_BELEG") is { Length: > 0 } e ? e : Path.Combine(Path.GetTempPath(), "t0747fix-beleg-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        var polys = new List<(string Layer, List<(double X, double Y)> Pts)>
        {
            ("F1", Rect(0, 0, 40, 30).Pts), ("F2", Rect(43, 0, 83, 30).Pts), // zwei Teile 3 mm auseinander
            ("F3", Rect(100, 0, 200, 80).Pts), ("F4", Rect(120, 20, 160, 60).Pts), ("F5", Rect(122, 30, 132, 40).Pts), // Platte, Loch, Insel 2 mm neben dem Rand
            ("F6", [(250, 0), (270, 20), (270, 0), (250, 20)]), // Acht
            ("F7", Rect(300, 0, 420, 60).Pts), ("F8", Dumbbell().Pts.Select(p => (p.X + 300, p.Y)).ToList()), // Platte mit Hantel-Loch
            ("F9", [(450, 0), (510, 0), (510, 40), (481.5, 40), (481.5, 25), (478.5, 25), (478.5, 40), (450, 40)]), // Schlitz außen
        };
        var rnd = new Random(4711);
        polys.Add(("F10", Enumerable.Range(0, 2000).Select(k => { var t = 2 * Math.PI * k / 2000; var rr = 50 + 0.5 * (2 * rnd.NextDouble() - 1); return (600 + rr * Math.Cos(t), 50 + rr * Math.Sin(t)); }).ToList()));
        var sb = new StringBuilder("0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n");
        foreach (var (layer, pts) in polys)
            for (var i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; sb.Append(FormattableString.Invariant($"0\nLINE\n8\n{layer}\n10\n{a.X:R}\n20\n{a.Y:R}\n11\n{b.X:R}\n21\n{b.Y:R}\n")); }
        sb.Append("0\nENDSEC\n0\nEOF\n");
        var dxf = Path.Combine(dir, "fix-faelle.dxf"); File.WriteAllText(dxf, sb.ToString());
        var cs = CncCam.Contours(CadDxf.Read(dxf).Drawing);
        Assert.Equal(10, cs.Count); Assert.All(cs, c => Assert.True(c.Closed));
        var r = CncCam.Generate(cs, S6 with { Feed = 800, Plunge = 200 });
        File.WriteAllText(Path.Combine(dir, "fix-faelle.nc"), r.Gcode);
        File.WriteAllText(Path.Combine(dir, "fix-faelle-hinweise.txt"), string.Join("\n", r.Warnings) + "\n");
        var milled = r.Paths.Select(p => cs[p.Source].Layer).Distinct().Order().ToList();
        log.WriteLine("gefräst: " + string.Join(", ", milled)); foreach (var w in r.Warnings) log.WriteLine("Hinweis: " + w);
        Assert.Equal(["F10", "F3", "F7", "F8", "F9"], milled);
        Assert.True(MinClearance(r.Gcode, r.Contours) >= 3 - Tol);
    }

    // ---------- Befund 4: große Dateien ----------

    /// <summary>DXF mit vielen Linien: Wände als Doppellinien im Raster mit Türlücken (so viele, bis <paramref name="lines"/> erreicht sind).</summary>
    static string BigDxf(int lines)
    {
        var sb = new StringBuilder("0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n"); var n = 0;
        void L(double x1, double y1, double x2, double y2) { if (n++ < lines) sb.Append(FormattableString.Invariant($"0\nLINE\n8\nWAND\n10\n{x1}\n20\n{y1}\n11\n{x2}\n21\n{y2}\n")); }
        for (var row = 0; n < lines; row++)
            for (var col = 0; col < 50 && n < lines; col++)
            {
                double x = col * 4000, y = row * 3000;
                L(x, y, x + 1500, y); L(x + 2400, y, x + 4000, y); L(x, y + 240, x + 1500, y + 240); L(x + 2400, y + 240, x + 4000, y + 240); // Wand mit Tür 90 cm
                L(x, y, x, y + 3000); L(x + 240, y + 240, x + 240, y + 3000);
            }
        return sb.Append("0\nENDSEC\n0\nEOF\n").ToString();
    }

    [Fact(DisplayName = "T-0747-fix 4: 10 000 Linien – Grundriss-Import und Konturen in wenigen Sekunden; Obergrenze mit Meldung; Abbrechen")]
    public void Big_dxf_fast()
    {
        var dxf = CadDxf.Parse(BigDxf(10_000));
        var sw = Stopwatch.StartNew();
        var rep = PlanDxfImport.Import(new FloorPlan(), dxf);
        var tPlan = sw.ElapsedMilliseconds; sw.Restart();
        var cs = CncCam.Contours(dxf.Drawing);
        var tChain = sw.ElapsedMilliseconds;
        log.WriteLine($"10 000 Linien: Grundriss-Import {tPlan} ms ({rep.Walls} Wände, {rep.Openings} Öffnungen), Konturen {tChain} ms ({cs.Count} Züge)");
        Assert.True(rep.Walls >= 100 && rep.Openings >= 1000, rep.Text); // gleiches Ergebnis wie vor der Beschleunigung: 168 Wände, 4832 Öffnungen
        Assert.Equal((168, 4832), (rep.Walls, rep.Openings));
        Assert.True(tPlan < 8000, $"Grundriss-Import {tPlan} ms"); Assert.True(tChain < 3000, $"Konturen {tChain} ms");
        // Obergrenze
        var many = CadDxf.Parse(BigDxf(PlanDxfImport.MaxEntities + 1));
        var e = Assert.Throws<CadError>(() => PlanDxfImport.Import(new FloorPlan(), many));
        Assert.Contains("zu viele", e.Message);
        Assert.Throws<CadError>(() => CncCam.Contours(CadDxf.Parse(BigDxf(CncCam.MaxEntities + 1)).Drawing)); // T-0747-fix5: Fräsen hat eine höhere Grenze (50 000 Kreise sollen gehen)
        // Abbrechen
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PlanDxfImport.Import(new FloorPlan(), dxf, null, cts.Token));
    }

    // ---------- Befund 5: veraltete Bahnen ----------

    [Fact(DisplayName = "T-0747-fix 5: Fräser-Ø/Tiefe/Zustellung geändert → G-Code aus der Zeichnung gilt als veraltet, bis neu gerechnet")]
    public void Stale_cam()
    {
        var j = new CncJob { ToolD = 4, Depth = 6, StepDown = 2 };
        j.ImportDrawing(Path.Combine(FixtureDir(), "teil-fremd.svg"));
        Assert.False(j.CamStale);
        j.ToolD = 6; Assert.True(j.CamStale);
        j.ToolD = 4; Assert.False(j.CamStale);
        j.Depth = 5; Assert.True(j.CamStale);
        // im Dokument gespeichert und wieder gelesen
        var o = new System.Text.Json.Nodes.JsonObject(); j.WriteTo(o); Assert.True(CncJob.From(o).CamStale);
        j.SetCamGcode(j.Cam().Gcode); Assert.False(j.CamStale);
        // G-Code von Hand geändert: kein CAM-Code mehr, also auch nicht veraltet
        j.Gcode += "(Hand)\n"; j.StepDown = 1; Assert.False(j.CamStale);
        // alte Dokumente ohne Stempel gelten nicht als veraltet
        Assert.False(new CncJob { Gcode = "G0 X0\n" }.CamStale);
    }

    static string FixtureDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return Path.Combine(d!.FullName, "tests", "fixtures", "t0747");
    }

    // ---------- Befund 6: Kodierung, Sonderzeichen, kaputte SVG ----------

    [Fact(DisplayName = "T-0747-fix 6: Windows-1252 (€), Sonderzeichen nur in fremden DXF, kaputter SVG-Pfad mit verständlicher Meldung")]
    public void Encoding_escapes_broken_svg()
    {
        var f = Path.Combine(Path.GetTempPath(), "t0747fix-enc-" + Guid.NewGuid().ToString("N")[..6] + ".dxf");
        try
        {
            var body = "0\nSECTION\n2\nENTITIES\n0\nTEXT\n8\n0\n10\n0\n20\n0\n40\n2.5\n1\nPreis 12 € – Küche\n0\nENDSEC\n0\nEOF\n";
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            File.WriteAllBytes(f, System.Text.Encoding.GetEncoding(1252).GetBytes(body));
            Assert.Equal("Preis 12 € – Küche", CadDxf.Read(f).Drawing.Entities[0].Text);
        }
        finally { File.Delete(f); }
        // Trendo-eigene Datei: Text bleibt wörtlich, fremde Datei: %%c → Ø
        var d = new CadDrawing(); d.Entities.Add(new CadEntity { Kind = "text", Layer = "0", Points = [[0, 0, 0]], Text = "Maß %%c10 und \\U+00B2", Height = 2.5 });
        Assert.Equal("Maß %%c10 und \\U+00B2", CadDxf.Parse(CadDxf.Write(d)).Drawing.Entities[0].Text);
        Assert.Equal("Maß Ø10 und ²", CadDxf.Parse("0\nSECTION\n2\nENTITIES\n0\nTEXT\n8\n0\n10\n0\n20\n0\n40\n2.5\n1\nMaß %%c10 und \\U+00B2\n0\nENDSEC\n0\nEOF\n").Drawing.Entities[0].Text);
        var p = new FloorPlan(); p.AddWall(0, 0, 400, 0); p.Labels.Add(new PlanLabel { X = 50, Y = 50, Text = "Rohr %%c50", Size = 20 });
        var back = new FloorPlan(); PlanDxfImport.Import(back, CadDxf.Parse(PlanDrawing.Dxf(p)));
        Assert.Contains(back.Labels, l => l.Text == "Rohr %%c50");
        // kaputte SVG-Pfade
        foreach (var bad in new[] { "M0 0 L10", "M0 0 C1 2 3", "M5", "M0 0 A5 5 0 0 1 10" })
        {
            var e = Assert.Throws<CadError>(() => CadSvg.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100mm\" height=\"100mm\" viewBox=\"0 0 100 100\"><path id=\"teil\" d=\"{bad}\"/></svg>"));
            Assert.Contains("Pfad", e.Message); Assert.Contains("unvollständig", e.Message);
        }
        Assert.Throws<FormatException>(() => ImageVector.ParseSubpaths("M0 0 L10"));
    }
}
