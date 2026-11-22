using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix4: Befunde der vierten unabhängigen Prüfung – DXF-Normale (0,0,−1), doppelt gezeichnete Konturen, Löcher in einem ausgelassenen
/// offenen Umriss, gefrorene Ebenen, winzige Bögen/Linien. Lage und Seite werden gegen die WAHRE Geometrie geprüft.</summary>
public class T0747_Fix4ViertePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const string Flip = "210\n0.0\n220\n0.0\n230\n-1.0\n";
    static string Dxf(string entities, string tables = "") => $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n{tables}0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Lw(IEnumerable<(double X, double Y)> pts, bool closed, string extra = "", string layer = "0") =>
        $"0\nLWPOLYLINE\n8\n{layer}\n90\n{pts.Count()}\n70\n{(closed ? 1 : 0)}\n" + string.Concat(pts.Select(p => string.Create(Inv, $"10\n{p.X}\n20\n{p.Y}\n"))) + extra;
    static string Circle(double x, double y, double r, string extra = "", string layer = "0") => string.Create(Inv, $"0\nCIRCLE\n8\n{layer}\n10\n{x}\n20\n{y}\n40\n{r}\n") + extra;
    static string Line(double x1, double y1, double x2, double y2) => string.Create(Inv, $"0\nLINE\n8\n0\n10\n{x1}\n20\n{y1}\n11\n{x2}\n21\n{y2}\n");
    static string Arc(double x, double y, double r, double a0, double a1, string extra = "") => string.Create(Inv, $"0\nARC\n8\n0\n10\n{x}\n20\n{y}\n40\n{r}\n50\n{a0}\n51\n{a1}\n") + extra;
    static (double X, double Y)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];

    /// <summary>Mittelpunkt der Innenbahn einer Kontur (Fräsermitte, um die Verschiebung des Teils zurückgerechnet).</summary>
    static (double X, double Y) Center(CamResult r, CamPath p) => (p.Pts.Average(q => q.X) - r.ShiftX, p.Pts.Average(q => q.Y) - r.ShiftY);

    // ---------- H1: Normale (0,0,−1) ----------

    [Fact(DisplayName = "T-0747-fix4 H1: CIRCLE, ARC und LWPOLYLINE mit Normale (0,0,−1) liegen gespiegelt richtig; schräge Normale wird gemeldet")]
    public void Ocs_normal_minus_z()
    {
        // v14: Kreis (50/20) mit Normale −Z liegt in Wahrheit bei (−50/20) – in der Platte, nicht daneben
        var d = CadDxf.Parse(Dxf(Lw(Rect(-100, -50, 100, 50), true) + Circle(50, 20, 10, Flip))).Drawing;
        var c = d.Entities.Single(e => e.Kind == "kreis"); Assert.Equal(-50, c.Cx, 9); Assert.Equal(20, c.Cy, 9);
        var r = CncCam.Generate(CncCam.Contours(d), new CamSettings { ToolD = 3 });
        var hole = Assert.Single(r.Paths, p => p.Kind == "innen"); var (hx, hy) = Center(r, hole);
        Assert.Equal(-50, hx, 2); Assert.Equal(20, hy, 2);
        // gespiegelte LWPOLYLINE als Loch (wahr bei x −60 … −30)
        var d2 = CadDxf.Parse(Dxf(Lw(Rect(-120, 0, 0, 60), true) + Lw([(30, 10), (60, 10), (60, 40), (30, 40)], true, Flip))).Drawing;
        var poly = d2.Entities.Last(); Assert.Equal([-30.0, -60.0, -60.0, -30.0], poly.Points.Select(p => p[0]));
        var r2 = CncCam.Generate(CncCam.Contours(d2), new CamSettings { ToolD = 3 });
        Assert.Contains(r2.Paths, p => p.Kind == "innen");
        // Rundrechteck: Linien in Weltkoordinaten, Eckbögen mit Normale −Z → geschlossen, außen gefräst
        var sb = new StringBuilder(); double x0 = 0, y0 = 0, x1 = 100, y1 = 60, rr = 10;
        sb.Append(Line(x0 + rr, y0, x1 - rr, y0)).Append(Line(x1, y0 + rr, x1, y1 - rr)).Append(Line(x1 - rr, y1, x0 + rr, y1)).Append(Line(x0, y1 - rr, x0, y0 + rr));
        foreach (var (cx, cy, a0) in new[] { (x1 - rr, y0 + rr, -90.0), (x1 - rr, y1 - rr, 0.0), (x0 + rr, y1 - rr, 90.0), (x0 + rr, y0 + rr, 180.0) })
            sb.Append(Arc(-cx, cy, rr, ((180 - (a0 + 90)) % 360 + 360) % 360, ((180 - a0) % 360 + 360) % 360, Flip));
        var cs3 = CncCam.Contours(CadDxf.Parse(Dxf(sb.ToString())).Drawing);
        var rect = Assert.Single(cs3); Assert.True(rect.Closed);
        Assert.Equal(100 * 60 - (4 - Math.PI) * 100, Math.Abs(rect.SignedArea), 0);
        // schräge Normale → nicht gelesen, gemeldet
        var skew = CadDxf.Parse(Dxf(Lw(Rect(0, 0, 10, 10), true) + Circle(5, 5, 2, "210\n0.6\n220\n0.0\n230\n0.8\n")));
        Assert.Equal(1, skew.Skipped["Kreis nicht in der Zeichenebene"]);
        Assert.DoesNotContain(skew.Drawing.Entities, e => e.Kind == "kreis");
        // Normale (0,0,1) ausdrücklich: unverändert
        Assert.Equal(50, CadDxf.Parse(Dxf(Circle(50, 20, 10, "210\n0\n220\n0\n230\n1\n"))).Drawing.Entities[0].Cx, 9);
    }

    [Fact(DisplayName = "T-0747-fix4 H1: gemeinsamer Leser – Grundriss übernimmt eine gespiegelte Polylinie an der richtigen Stelle")]
    public void Ocs_in_floor_plan()
    {
        var d = CadDxf.Parse(Dxf(Lw([(0, 0), (4000, 0)], false, layer: "WAND") + Lw([(0, 3000), (4000, 3000)], false, Flip, layer: "WAND")));
        Assert.Equal([0.0, -4000.0], d.Drawing.Entities[1].Points.Select(p => p[0]));
        var p = new FloorPlan(); PlanDxfImport.Import(p, d);
        Assert.Equal(2, p.Walls.Count);
        var starts = p.Walls.Select(w => Math.Min(w.X1, w.X2)).Order().ToList();
        Assert.InRange(starts[1] - starts[0], 390, 410); // gespiegelte Wand liegt 4 m links der anderen (vorher deckungsgleich darüber)
    }

    // ---------- H2: doppelte Konturen ----------

    [Fact(DisplayName = "T-0747-fix4 H2: doppelt gezeichnete Platte (LWPOLYLINE, 8 LINE, umgedreht, andere Startecke, 0,0005 mm versetzt) – Loch bleibt Loch, gemeldet")]
    public void Duplicate_outlines()
    {
        var plate = Rect(0, 0, 100, 60); var lines = string.Concat(Enumerable.Range(0, 4).Select(k => Line(plate[k].X, plate[k].Y, plate[(k + 1) % 4].X, plate[(k + 1) % 4].Y)));
        var variants = new Dictionary<string, string>
        {
            ["LWPOLYLINE doppelt"] = Lw(plate, true) + Lw(plate, true),
            ["8 LINE"] = lines + lines,
            ["umgedreht"] = Lw(plate, true) + Lw(plate.Reverse(), true),
            ["andere Startecke"] = Lw(plate, true) + Lw(plate.Skip(2).Concat(plate.Take(2)), true),
            ["0,0005 versetzt"] = Lw(plate, true) + Lw(plate.Select(q => (q.X + 0.0005, q.Y + 0.0005)), true),
        };
        foreach (var (name, ents) in variants)
        {
            var r = CncCam.Generate(CncCam.Contours(CadDxf.Parse(Dxf(ents + Circle(50, 30, 10))).Drawing), new CamSettings { ToolD = 3 });
            log.WriteLine($"{name}: {string.Join(", ", r.Paths.Select(p => p.Kind))} | {string.Join(" | ", r.Warnings)}");
            var hole = Assert.Single(r.Paths, p => p.Kind == "innen");
            var rad = hole.Pts.Average(q => Math.Sqrt(Math.Pow(q.X - r.ShiftX - 50, 2) + Math.Pow(q.Y - r.ShiftY - 30, 2)));
            Assert.InRange(rad, 8.5 - 0.01, 8.5 + 0.01); // Fräsermitte 1,5 mm innerhalb des Lochs – nicht außen um den Fräser-Ø zu groß
            Assert.Single(r.Paths, p => p.Kind == "aussen");
            Assert.Contains(r.Warnings, w => w.Contains("doppelt gezeichnet", StringComparison.Ordinal));
        }
        // doppeltes Loch: einmal gefräst
        var r2 = CncCam.Generate(CncCam.Contours(CadDxf.Parse(Dxf(Lw(plate, true) + Circle(50, 30, 10) + Circle(50, 30, 10))).Drawing), new CamSettings { ToolD = 3 });
        Assert.Single(r2.Paths, p => p.Kind == "innen");
        // teilweise überlappend, nicht deckungsgleich → abgelehnt und gemeldet
        var r3 = CncCam.Generate(CncCam.Contours(CadDxf.Parse(Dxf(Lw(plate, true) + Lw(Rect(50, 0, 150, 60), true) + Lw(Rect(200, 0, 220, 20), true))).Drawing), new CamSettings { ToolD = 3 });
        Assert.All(r3.Paths, p => Assert.Equal(2, p.Source));
        Assert.Contains(r3.Warnings, w => w.Contains("zu nah an Kontur", StringComparison.Ordinal));
    }

    // ---------- M1: Loch in einem ausgelassenen offenen Umriss ----------

    [Fact(DisplayName = "T-0747-fix4 M1: Umriss mit Spalt knapp über 0,1 mm (v5b) – Kreis darin wird ausgelassen und gemeldet statt außen gefräst")]
    public void Hole_inside_open_outline()
    {
        var ents = Line(0, 0, 99.95, 0) + Line(100, 0.08, 100, 59.97) + Line(100, 60, 0.001, 60) + Line(0, 59.9, 0, 0.03) + Circle(50, 30, 20) + Lw(Rect(200, 0, 230, 20), true);
        var r = CncCam.Generate(CncCam.Contours(CadDxf.Parse(Dxf(ents)).Drawing), new CamSettings { ToolD = 3 });
        log.WriteLine(string.Join(" | ", r.Warnings));
        Assert.Contains(r.Warnings, w => w.Contains("liegt in oder an der offenen Kontur", StringComparison.Ordinal));
        Assert.All(r.Paths, p => Assert.True(Center(r, p).X > 150, "nur das freie Teil wird gefräst"));
    }

    // ---------- N1 / N2 ----------

    [Fact(DisplayName = "T-0747-fix4 N1/N2: gefrorene Ebene wird nicht gefräst, aber gemeldet; Bogen < 0,00001° und Linie der Länge 0 sind keine Konturen")]
    public void Frozen_layer_and_tiny_pieces()
    {
        var tables = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nHILFE\n70\n1\n62\n7\n0\nENDTAB\n0\nENDSEC\n";
        var f = Path.Combine(Path.GetTempPath(), "t0747fix4-" + Guid.NewGuid().ToString("N")[..8] + ".dxf");
        try
        {
            File.WriteAllText(f, Dxf(Lw(Rect(0, 0, 100, 60), true) + Circle(50, 30, 15, layer: "HILFE"), tables));
            var r = new CncJob { ToolD = 3 }.ImportDrawing(f);
            Assert.DoesNotContain(r.Paths, p => p.Kind == "innen");
            Assert.Contains(r.Warnings, w => w.StartsWith("Ausgeblendete Ebenen nicht gefräst: HILFE (1)", StringComparison.Ordinal));
        }
        finally { File.Delete(f); }
        var cs = CncCam.Contours(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60), true) + Arc(50, 30, 10, 0, 180) + Arc(50, 30, 10, 180, 360 - 2e-6) + Arc(50, 30, 10, 360 - 2e-6, 360) + Line(10, 10, 10, 10))).Drawing);
        Assert.Equal(2, cs.Count); Assert.All(cs, c => Assert.True(c.Closed));
    }
}
