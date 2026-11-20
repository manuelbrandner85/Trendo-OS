using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix3: Befunde der dritten unabhängigen Prüfung – Verketten (Endpunkte bleiben, nächster statt kleinster Index, echter Abstand),
/// Haltestege mit Grenze auf einem Bahnpunkt, offene Konturen als Hindernis, Limaçon, fehlende Einheit. Abstände gegen die WAHRE Geometrie.</summary>
public class T0747_Fix3DrittePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    record struct L((double X, double Y) A, (double X, double Y) B);

    static string Dxf(IEnumerable<L> lines, int? units = 4)
    {
        var sb = new StringBuilder(units is { } u ? $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n{u}\n0\nENDSEC\n" : "");
        sb.Append("0\nSECTION\n2\nENTITIES\n");
        foreach (var l in lines) sb.Append(string.Create(Inv, $"0\nLINE\n8\n0\n10\n{l.A.X:R}\n20\n{l.A.Y:R}\n11\n{l.B.X:R}\n21\n{l.B.Y:R}\n"));
        return sb.Append("0\nENDSEC\n0\nEOF\n").ToString();
    }

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
    static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
    static double SegSeg((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        if ((d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0) && d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0) return 0;
        return Math.Min(Math.Min(PtSeg(a, c, d), PtSeg(b, c, d)), Math.Min(PtSeg(c, a, b), PtSeg(d, a, b)));
    }
    /// <summary>Kleinster Abstand aller Schnittbewegungen zu den wahren Linien (um die Verschiebung des Teils versetzt).</summary>
    static double Worst(CamResult r, IEnumerable<L> truth) => CutMoves(r.Gcode).Min(m => truth.Min(t => SegSeg(m.A, m.B, (t.A.X + r.ShiftX, t.A.Y + r.ShiftY), (t.B.X + r.ShiftX, t.B.Y + r.ShiftY))));

    static List<L> Box(double x0, double y0, double x1, double y1) => [new((x0, y0), (x1, y0)), new((x1, y0), (x1, y1)), new((x1, y1), (x0, y1)), new((x0, y1), (x0, y0))];

    // ---------- M1: Spalt mitten in der Kette ----------

    [Theory(DisplayName = "T-0747-fix3 M1: Spalt mitten in der Kette (o6) – beide Endpunkte bleiben, Bahn ≥ Radius − 0,002 zur wahren Geometrie")]
    [InlineData(0.05)]
    [InlineData(0.09)]
    public void Gap_in_the_middle_keeps_both_ends(double gap)
    {
        List<L> truth = [new((0, 0), (50, 0)), new((50 + gap, 0), (50 + gap, 30)), new((50 + gap, 30), (0, 30)), new((0, 30), (0, 0))];
        var c = Assert.Single(CncCam.Contours(CadDxf.Parse(Dxf(truth)).Drawing)); Assert.True(c.Closed);
        Assert.Contains(c.Pts, p => p == (50, 0)); Assert.Contains(c.Pts, p => p == (50 + gap, 0));
        var r = CncCam.Generate([c], new CamSettings { ToolD = 6 });
        var w = Worst(r, truth); log.WriteLine($"Spalt {gap}: kleinster Abstand {w:0.0000} (Radius 3)");
        Assert.True(w >= 3 - 0.002, $"{w}");
    }

    // ---------- M2 + N1: nächster Kandidat, exakt zuerst, nie Geschlossenes verbinden, echter Abstand ----------

    [Fact(DisplayName = "T-0747-fix3 M2/N1: zwei Teile 0,06–0,08 mm auseinander (o4, o7), U vor Rechteck (o5), Diagonalspalt 0,113 mm, Mehrdeutigkeit")]
    public void Chaining_nearest_exact_first()
    {
        // o4: Linien zweier Rechtecke gemischt – jedes bleibt für sich geschlossen
        var a = Box(0, 0, 50, 30); var b = Box(50.08, 0, 100, 30);
        var mixed = a.Zip(b).SelectMany(t => new[] { t.Second, t.First }).ToList();
        var cs = CncCam.Contours(CadDxf.Parse(Dxf(mixed)).Drawing);
        Assert.Equal(2, cs.Count); Assert.All(cs, c => Assert.True(c.Closed)); Assert.All(cs, c => Assert.Equal(4, c.Pts.Count));
        var r = CncCam.Generate(cs, new CamSettings { ToolD = 0.05 });
        Assert.True(Worst(r, mixed) >= 0.025 - 0.002);
        // o7: Ecke an Ecke diagonal 0,06/0,06 – zwei getrennte Teile
        var o7 = Box(0, 0, 50, 30).Concat(Box(50.06, 30.06, 100, 60)).ToList();
        var c7 = CncCam.Contours(CadDxf.Parse(Dxf(o7)).Drawing);
        Assert.Equal(2, c7.Count); Assert.All(c7, c => Assert.Equal(4, c.Pts.Count));
        // o5: offene U-Form 0,06 mm vor einem Rechteck mit 0,05-mm-Spalt – das Rechteck schließt sich selbst, das U bleibt offen
        List<L> u = [new((-0.06, 0.02), (-40, 0.02)), new((-40, 0.02), (-40, 29.98)), new((-40, 29.98), (-0.06, 29.98))];
        List<L> rect = [new((0, 0), (50, 0)), new((50, 0), (50, 30)), new((50, 30), (0, 30)), new((0, 30), (0, 0.05))];
        var c5 = CncCam.Contours(CadDxf.Parse(Dxf(u.Concat(rect))).Drawing);
        Assert.Equal(2, c5.Count);
        Assert.Contains(c5, c => c.Closed && c.Pts.Count == 5 && c.Pts.All(p => p.X >= 0)); // 4 Ecken + Spaltende (0 / 0,05) – nichts verschoben
        Assert.Contains(c5, c => !c.Closed && c.Pts.All(p => p.X <= -0.06));
        // N1: diagonaler Spalt 0,08/0,08 = 0,113 mm > 0,1 → offen (früher je Achse gemessen → geschlossen)
        List<L> diag = [new((0, 0), (50, 0)), new((50, 0), (50, 30)), new((50, 30), (0, 30)), new((0, 30), (-0.08, 0.08))];
        Assert.False(Assert.Single(CncCam.Contours(CadDxf.Parse(Dxf(diag)).Drawing)).Closed);
        // Spalt genau 0,1 bei krummen Koordinaten (50,1 − 50,0 = 0,10000000000000142) → geschlossen
        List<L> krumm = [new((50.0, 10.0), (100, 10)), new((100, 10), (100, 40)), new((100, 40), (50.0, 40)), new((50.0, 40), (50.0, 10.1))];
        Assert.True(Assert.Single(CncCam.Contours(CadDxf.Parse(Dxf(krumm)).Drawing)).Closed);
        // Mehrdeutig: zwei Enden etwa gleich nah an einem dritten → nicht verbinden, melden
        List<L> amb = [new((0, 0), (10, 0)), new((10.05, 0.02), (20, 5)), new((10.05, -0.02), (20, -5))];
        var notes = new List<string>();
        var ca = CncCam.Contours(CadDxf.Parse(Dxf(amb)).Drawing, notes);
        log.WriteLine(string.Join(" | ", notes));
        Assert.Equal(3, ca.Count); Assert.Contains(notes, n => n.StartsWith("Mehrdeutiger Spalt", StringComparison.Ordinal));
    }

    // ---------- M3: Haltestege mit Grenze auf einem Bahnpunkt ----------

    static int ArcPieces(double r, double chord = 0.01) => Math.Max(1, (int)Math.Ceiling(CadContours.Segments(r, 2 * Math.PI, chord) * (Math.PI / 2) / (2 * Math.PI)));

    [Theory(DisplayName = "T-0747-fix3 M3: Haltestege – Quadrat 20 mm mit 2 Stegen und Seite = Bogenlänge: genau so viele Anhebungen wie Stege")]
    [InlineData(3.0, 20.0)]
    [InlineData(6.0, 20.0)]
    [InlineData(2.0, 20.0)]
    [InlineData(3.0, 0.0)]
    [InlineData(6.0, 0.0)]
    [InlineData(2.0, 0.0)]
    public void Tabs_on_path_points(double tool, double side)
    {
        var r = tool / 2; var m = ArcPieces(r); var arc = m * 2 * r * Math.Tan(Math.PI / (4 * m)); // Länge der Sehnen um eine Ecke
        var a = side > 0 ? side : arc; var tw = 2 * arc - tool; // so fallen Steggrenzen genau auf Bahnpunkte
        foreach (var tabs in new[] { 2, 4 })
        {
            var s = new CamSettings { ToolD = tool, Depth = 3, StepDown = 1, Tabs = tabs, TabW = Math.Max(0.1, tw), TabH = 1 };
            CamResult res;
            try { res = CncCam.Generate([new CadContour([(0, 0), (a, 0), (a, a), (0, a)], true, "0")], s); }
            catch (InvalidOperationException e) { Assert.Contains("Haltestege", e.Message); continue; } // lieber Meldung als still falsch
            if (res.Warnings.Any(w => w.Contains("zu kurz", StringComparison.Ordinal))) continue;
            var lastPass = res.Gcode[res.Gcode.IndexOf("G1 Z-3 F", StringComparison.Ordinal)..]; // ab dem ersten Eintauchen auf volle Tiefe
            var lifts = System.Text.RegularExpressions.Regex.Matches(lastPass, @"\nG1 Z-2\n").Count;
            var downs = System.Text.RegularExpressions.Regex.Matches(lastPass, @"\nG1 Z-3 F").Count;
            log.WriteLine($"Fräser {tool}, Seite {a:0.###}, {tabs} Stege: {lifts} Anhebungen, {downs} wieder hinunter");
            Assert.Equal(tabs, lifts); Assert.Equal(tabs, downs);
        }
    }

    // ---------- M5: offene Kontur ist Hindernis ----------

    [Fact(DisplayName = "T-0747-fix3 M5: offener Umriss 2 mm neben einem Teil (o3) – die Bahn des Nachbarn fährt nicht hindurch")]
    public void Open_contour_is_obstacle()
    {
        List<L> open = [new((0, 0), (50, 0)), new((50, 0), (50, 30)), new((50, 30), (0, 30)), new((0, 30), (0, 0.3))];
        var cs = CncCam.Contours(CadDxf.Parse(Dxf(open.Concat(Box(52, 0, 100, 30)))).Drawing);
        var e = Assert.Throws<InvalidOperationException>(() => CncCam.Generate(cs, new CamSettings { ToolD = 6 }));
        Assert.Contains("offene Kontur", e.Message); Assert.Contains("zu nah an Kontur", e.Message);
        // weiter weg: das Teil wird gefräst, ohne den offenen Umriss zu berühren
        var far = CncCam.Contours(CadDxf.Parse(Dxf(open.Concat(Box(60, 0, 100, 30)))).Drawing);
        var r = CncCam.Generate(far, new CamSettings { ToolD = 6 });
        Assert.True(Worst(r, open.Concat(Box(60, 0, 100, 30))) >= 3 - 0.002);
    }

    // ---------- N2 + N4 ----------

    [Fact(DisplayName = "T-0747-fix3 N2/N4: Kreis im doppelt umlaufenen Bereich eines Limaçon wird ausgelassen; DXF ohne Einheit wird gemeldet")]
    public void Limacon_and_missing_units()
    {
        var lim = Enumerable.Range(0, 720).Select(k => 2 * Math.PI * k / 720).Select(t => (15 * (1 + 2 * Math.Cos(t)) * Math.Cos(t), 15 * (1 + 2 * Math.Cos(t)) * Math.Sin(t))).ToList();
        var circle = Enumerable.Range(0, 200).Select(k => (7 + 2 * Math.Cos(2 * Math.PI * k / 200), 2 * Math.Sin(2 * Math.PI * k / 200))).ToList();
        var r = CncCam.Generate([new CadContour([(-60, -60), (80, -60), (80, 60), (-60, 60)], true, "0"), new CadContour(lim, true, "0"), new CadContour(circle, true, "0")], new CamSettings { ToolD = 1 });
        Assert.DoesNotContain(r.Paths, p => p.Source == 2);
        Assert.Contains(r.Warnings, w => w.StartsWith("Kontur 3 ausgelassen: liegt in oder an Kontur 2", StringComparison.Ordinal));
        var f = Path.Combine(Path.GetTempPath(), "t0747fix3-" + Guid.NewGuid().ToString("N")[..8] + ".dxf");
        try
        {
            File.WriteAllText(f, Dxf(Box(0, 0, 40, 30), units: null));
            var res = new CncJob { ToolD = 3 }.ImportDrawing(f);
            Assert.Contains(res.Warnings, w => w.StartsWith("Einheit nicht angegeben – als mm gelesen", StringComparison.Ordinal));
        }
        finally { File.Delete(f); }
    }
}
