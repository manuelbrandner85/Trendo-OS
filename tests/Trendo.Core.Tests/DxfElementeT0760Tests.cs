using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0760: DXF-Elemente lesen statt nur melden – INSERT (Blockverweise), SPLINE (NURBS), MLINE (Mehrfachlinien). REGION und die
/// „ezdxf-Linse“ bleiben ehrlich gemeldet. Die Sicherheitsregeln aus T-0747 gelten nach dem Auflösen der Blöcke.</summary>
public class T0760_DxfElemente(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities, string blocks = "", string tables = "", string objects = "", int units = 4) =>
        $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n{units}\n0\nENDSEC\n" + tables + (blocks.Length > 0 ? $"0\nSECTION\n2\nBLOCKS\n{blocks}0\nENDSEC\n" : "")
        + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n" + (objects.Length > 0 ? $"0\nSECTION\n2\nOBJECTS\n{objects}0\nENDSEC\n" : "") + "0\nEOF\n";
    static string Layers(params (string Name, int Color, int Flags)[] l) =>
        "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n" + string.Concat(l.Select(x => G((0, "LAYER"), (2, x.Name), (70, x.Flags), (62, x.Color)))) + "0\nENDTAB\n0\nENDSEC\n";
    static string Lw((double X, double Y)[] p, string layer = "0") => G((0, "LWPOLYLINE"), (8, layer), (90, p.Length), (70, 1)) + string.Concat(p.Select(q => G((10, q.X), (20, q.Y))));
    static string Circle(double x, double y, double r, string layer = "0") => G((0, "CIRCLE"), (8, layer), (10, x), (20, y), (30, 0), (40, r));
    static (double X, double Y)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static string Block(string name, string content, double bx = 0, double by = 0, int flags = 0) => G((0, "BLOCK"), (8, "0"), (2, name), (70, flags), (10, bx), (20, by), (30, 0)) + content + G((0, "ENDBLK"), (8, "0"));
    static string Insert(string name, double x, double y, string extra = "", string layer = "0") => G((0, "INSERT"), (8, layer), (2, name), (10, x), (20, y), (30, 0)) + extra;
    static (CamResult? R, string Text) Cam(string dxf, double tool = 3)
    {
        try { var rd = CadDxf.Parse(dxf); var r = CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = tool, Unread = rd.Unread }); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static (double X, double Y) Mid(CamResult r, CamPath p) => (p.Pts.Average(q => q.X) - r.ShiftX, p.Pts.Average(q => q.Y) - r.ShiftY);
    static int Near(CamResult r, string kind, double x, double y, double d = 1) => r.Paths.Count(p => p.Kind == kind && Math.Abs(Mid(r, p).X - x) < d && Math.Abs(Mid(r, p).Y - y) < d);
    static CadDxf.Box Ext(IEnumerable<CadEntity> es) { var b = CadDxf.Box.Empty; foreach (var e in es) foreach (var q in e.Outline(4096)) b = b.With(q); return b; }

    // ---------- INSERT ----------

    // Platte 100 × 60 mit Loch r 8 bei (30/30); Basispunkt (10/10)
    static readonly string Plate = Block("PL", Lw(Rect(10, 10, 110, 70)) + Circle(40, 40, 8), 10, 10);

    [Fact(DisplayName = "T-0760 INSERT: Einfügepunkt, Drehung, Skalierung, Spiegelung (−42 und Normale −Z) und Feld – Platte und Loch landen genau dort und werden gefräst")]
    public void Insert_transforms()
    {
        // gedreht um 90° bei (500/0): Loch bei (500 − 30, 30) = (470/30)
        var rd = CadDxf.Parse(Dxf(Insert("PL", 500, 0, G((50, 90))), Plate));
        Assert.Empty(rd.Unread); Assert.Empty(rd.Skipped);
        var hole = Assert.Single(rd.Drawing.Entities, e => e.Kind == "kreis"); Assert.Equal(470, hole.Cx, 9); Assert.Equal(30, hole.Cy, 9); Assert.Equal(8, hole.R, 9);
        // Faktor 2: Loch r 16 bei (60/60)
        var s2 = Assert.Single(CadDxf.Parse(Dxf(Insert("PL", 0, 0, G((41, 2), (42, 2))), Plate)).Drawing.Entities, e => e.Kind == "kreis");
        Assert.Equal((60.0, 60.0, 16.0), (s2.Cx, s2.Cy, s2.R));
        // gespiegelt über x (Faktor −1) und über die Normale −Z: Loch bei (−30/30)
        foreach (var mir in new[] { G((41, -1)), "210\n0.0\n220\n0.0\n230\n-1.0\n" })
        {
            var m = Assert.Single(CadDxf.Parse(Dxf(Insert("PL", 0, 0, mir), Plate)).Drawing.Entities, e => e.Kind == "kreis");
            Assert.Equal(-30, m.Cx, 9); Assert.Equal(30, m.Cy, 9);
        }
        // Feld 3 × 2 im Abstand 150/100 → 6 Platten mit je einem Loch
        var arr = Dxf(Insert("PL", 0, 0, G((70, 3), (71, 2), (44, 150), (45, 100))), Plate);
        var (r, text) = Cam(arr); log.WriteLine(text);
        Assert.NotNull(r);
        Assert.Equal(6, r!.Paths.Count(p => p.Kind == "aussen")); Assert.Equal(6, r.Paths.Count(p => p.Kind == "innen"));
        foreach (var c in new[] { 0, 1, 2 }) foreach (var w in new[] { 0, 1 }) Assert.Equal(1, Near(r, "innen", 30 + 150 * c, 30 + 100 * w));
    }

    [Fact(DisplayName = "T-0760 INSERT: ungleiche Skalierung macht aus Kreis und Bogen eine Ellipse (fein abgetastet, ≤ 0,001 mm), Ausbuchtungen ebenso")]
    public void Insert_nonuniform_scale()
    {
        var blk = Block("K", Circle(0, 0, 10) + G((0, "ARC"), (8, "0"), (10, 0), (20, 0), (30, 0), (40, 5), (50, 0), (51, 90))
            + G((0, "LWPOLYLINE"), (8, "0"), (90, 2), (70, 1), (10, -20), (20, 0), (42, 1), (10, 20), (20, 0), (42, 1)));
        var rd = CadDxf.Parse(Dxf(Insert("K", 100, 50, G((41, 3), (42, 1))), blk));
        Assert.Empty(rd.Unread);
        Assert.All(rd.Drawing.Entities, e => Assert.Equal("polylinie", e.Kind));
        foreach (var (e, a, b) in new[] { (rd.Drawing.Entities[0], 30.0, 10.0), (rd.Drawing.Entities[1], 15.0, 5.0), (rd.Drawing.Entities[2], 60.0, 20.0) })
        {
            var worst = 0.0;
            for (var i = 0; i < e.Points.Count - (e.Closed ? 0 : 1); i++)
            {
                var p = e.Points[i]; var q = e.Points[(i + 1) % e.Points.Count];
                double U(double x, double y) => Math.Sqrt(Math.Pow((x - 100) / a, 2) + Math.Pow((y - 50) / b, 2));
                Assert.Equal(1, U(p[0], p[1]), 9); // Stützpunkte genau auf der Ellipse
                worst = Math.Max(worst, (1 - U((p[0] + q[0]) / 2, (p[1] + q[1]) / 2)) * b); // Sehnenmitte: Abstand nach innen (≤ Pfeilhöhe)
            }
            log.WriteLine($"{e.Source}: {e.Points.Count} Punkte, Pfeilhöhe ≤ {worst:0.000000} mm");
            Assert.True(worst <= 0.0011, $"{worst}");
        }
    }

    [Fact(DisplayName = "T-0760 INSERT: verschachtelt, Ebene 0 erbt die Ebene des Verweises, gefrorene/ausgeschaltete Ebenen und unsichtbare Verweise werden nicht gefräst, sondern gemeldet")]
    public void Insert_nested_layers_visibility()
    {
        var blocks = Block("LOCH", Circle(0, 0, 5)) + Block("PL2", Lw(Rect(0, 0, 100, 60)) + Insert("LOCH", 30, 30) + Insert("LOCH", 70, 30, layer: "ROT"));
        var tables = Layers(("0", 7, 0), ("TEIL", 3, 0), ("ROT", 1, 0), ("FROST", 5, 1), ("AUS", -7, 0));
        var rd = CadDxf.Parse(Dxf(Insert("PL2", 0, 0, layer: "TEIL") + Insert("PL2", 200, 0, layer: "FROST") + Insert("PL2", 400, 0, layer: "AUS") + Insert("PL2", 600, 0, G((60, 1))), blocks, tables));
        Assert.Empty(rd.Unread);
        var first = rd.Drawing.Entities.Take(3).ToList();
        Assert.Equal(["TEIL", "TEIL", "ROT"], first.Select(e => e.Layer)); // verschachtelt: LOCH auf 0 erbt TEIL, auf ROT bleibt ROT
        Assert.All(rd.Drawing.Entities.Skip(3).Take(3), e => Assert.True(e.Invisible)); // gefroren: ganzer Inhalt unsichtbar
        var aus = rd.Drawing.Entities.Skip(6).Take(3).ToList(); Assert.Equal(["AUS", "AUS", "ROT"], aus.Select(e => e.Layer)); // aus: nur Inhalt auf Ebene 0
        Assert.All(rd.Drawing.Entities.Skip(9), e => Assert.True(e.Invisible)); // 60 = 1
        var f = Path.Combine(Path.GetTempPath(), $"t0760-{Guid.NewGuid():N}.dxf");
        File.WriteAllText(f, Dxf(Insert("PL2", 0, 0, layer: "TEIL") + Insert("PL2", 200, 0, layer: "FROST") + Insert("PL2", 400, 0, layer: "AUS") + Insert("PL2", 600, 0, G((60, 1))), blocks, tables));
        try
        {
            var r = new CncJob { ToolD = 3 }.ImportDrawing(f); var all = string.Join(" | ", r.Warnings); log.WriteLine(all);
            Assert.Contains("Ausgeblendete Ebenen nicht gefräst", all);
            Assert.Equal(1, Near(r, "aussen", 50, 30, 2)); Assert.Equal(1, Near(r, "innen", 30, 30)); Assert.Equal(1, Near(r, "innen", 70, 30));
            Assert.Equal(1, r.Paths.Count(p => Math.Abs(Mid(r, p).X - 470) < 1 && Math.Abs(Mid(r, p).Y - 30) < 1)); // ROT-Kreis im ausgeschalteten Verweis ist sichtbar …
            Assert.Equal(0, r.Paths.Count(p => Mid(r, p).X is > 150 and < 350 || Mid(r, p).X > 550)); // … gefroren und unsichtbar: nichts
            Assert.Equal(0, Near(r, "aussen", 250, 30, 2) + Near(r, "aussen", 450, 30, 2) + Near(r, "aussen", 650, 30, 2)); // ausgeblendete Platten werden nie gefräst
        }
        finally { File.Delete(f); }
    }

    [Fact(DisplayName = "T-0760 INSERT: Zyklus, Tiefe > 16, Xref, Block fehlt, unsinnige Faktoren und riesige Felder bleiben gemeldet – schnell, ohne Speicherflut")]
    public void Insert_limits()
    {
        var self = Block("S", Circle(0, 0, 1) + Insert("S", 5, 0));
        var rd = CadDxf.Parse(Dxf(Insert("S", 0, 0), self));
        Assert.Single(rd.Drawing.Entities); Assert.True(Assert.Single(rd.Unread).B.IsAll); Assert.Contains("im Kreis", rd.Unread[0].What);
        var chain = string.Concat(Enumerable.Range(0, 20).Select(k => Block("T" + k, Circle(0, 0, 1) + (k < 19 ? Insert("T" + (k + 1), 1, 0) : ""))));
        var deep = CadDxf.Parse(Dxf(Insert("T0", 0, 0), chain));
        Assert.Equal(16, deep.Drawing.Entities.Count); Assert.Contains(deep.Unread, u => u.What.Contains("zu tief") && u.B.IsAll);
        Assert.True(Assert.Single(CadDxf.Parse(Dxf(Insert("X", 0, 0), Block("X", Circle(0, 0, 3), flags: 4))).Unread).B.IsAll);
        var miss = CadDxf.Parse(Dxf(Insert("FEHLT", 0, 0) + Circle(0, 0, 1)));
        Assert.True(Assert.Single(miss.Unread).B.IsEmpty); Assert.Equal(1, miss.Skipped["INSERT"]);
        Assert.Contains("unsinnige", Assert.Single(CadDxf.Parse(Dxf(Insert("PL", 0, 0, G((41, 0))), Plate)).Unread).What);
        // 32767 × 32767 Platten (10^9) – Arbeitsgrenze: gemeldet mit der Ausdehnung des Felds, in Sekunden
        var sw = Stopwatch.StartNew();
        var big = CadDxf.Parse(Dxf(Insert("PL", 0, 0, G((70, 32767), (71, 32767), (44, 200), (45, 100))), Plate));
        log.WriteLine($"Riesenfeld: {sw.ElapsedMilliseconds} ms, {big.Unread[0].What} {big.Unread[0].B}");
        var u = Assert.Single(big.Unread); Assert.Contains("zu groß", u.What); Assert.Equal(200 * 32766 + 100, u.B.MaxX, 6);
        Assert.Empty(big.Drawing.Entities);
        Assert.True(sw.ElapsedMilliseconds < 5000);
        // Verschachtelte Explosion 10 × 10 × … (10^16) ebenfalls abgefangen
        var bomb = string.Concat(Enumerable.Range(0, 16).Select(k => Block("B" + k, k < 15 ? string.Concat(Enumerable.Range(0, 10).Select(j => Insert("B" + (k + 1), j, 0))) : Circle(0, 0, 1))));
        sw.Restart(); var rb = CadDxf.Parse(Dxf(Insert("B0", 0, 0), bomb)); Assert.Contains("zu groß", Assert.Single(rb.Unread).What); Assert.True(sw.ElapsedMilliseconds < 5000);
    }

    [Fact(DisplayName = "T-0760 INSERT: Sicherheitsregeln nach dem Auflösen – nicht lesbares im Block (REGION) macht die Teile darin unsicher; Schraffur im Block formgleich zum Rand fällt weg")]
    public void Insert_safety_after_expansion()
    {
        // Block: Platte + REGION darin; eingefügt um 90° gedreht → REGION-Ausdehnung „überall“ → alle Konturen ausgelassen und gemeldet
        var blk = Block("R", Lw(Rect(0, 0, 100, 60)) + Circle(30, 30, 5) + G((0, "REGION"), (8, "0"), (70, 1), (1, "ACIS")));
        var (r, text) = Cam(Dxf(Insert("R", 0, 0, G((50, 90))), blk));
        Assert.Null(r); Assert.Contains("Region", text);
        // Schraffur im Block, Rand formgleich zur Platte: fällt beim Fräsen nicht weg und erzeugt keine Unsicherheit
        var hatch = G((0, "HATCH"), (8, "0"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, 1), (92, 3), (72, 0), (73, 1), (93, 4))
            + string.Concat(Rect(0, 0, 100, 60).Select(p => G((10, p.X), (20, p.Y)))) + G((97, 0), (75, 0), (76, 1), (98, 0));
        var rd = CadDxf.Parse(Dxf(Insert("H", 200, 0, G((50, 30))), Block("H", Lw(Rect(0, 0, 100, 60)) + hatch + Circle(30, 30, 5))));
        Assert.Empty(rd.Unread); Assert.Equal(1, rd.Redundant["HATCH"]);
        // Linien-Stöße und U vor Kerbe gelten auch für Konturen aus Blöcken: Platte aus vier LINE im Block wird verkettet und gefräst
        var lines = string.Concat(new[] { (0, 0, 100, 0), (100, 0, 100, 60), (100, 60, 0, 60), (0, 60, 0, 0) }.Select(l => G((0, "LINE"), (8, "0"), (10, l.Item1), (20, l.Item2), (30, 0), (11, l.Item3), (21, l.Item4), (31, 0))));
        var (r2, t2) = Cam(Dxf(Insert("LN", 0, 0) + Insert("LN", 150, 0), Block("LN", lines + Circle(50, 30, 10))));
        log.WriteLine(t2);
        Assert.NotNull(r2); Assert.Equal(2, r2!.Paths.Count(p => p.Kind == "aussen")); Assert.Equal(2, r2.Paths.Count(p => p.Kind == "innen"));
    }

    // ---------- SPLINE ----------

    static string Spline(int deg, (double X, double Y)[] ctrl, double[] knots, double[]? weights = null, int flags = 8, (double X, double Y)[]? fit = null, string extra = "") =>
        G((0, "SPLINE"), (8, "0"), (70, flags | (weights is null ? 0 : 4)), (71, deg), (72, knots.Length), (73, ctrl.Length), (74, fit?.Length ?? 0)) + extra
        + string.Concat(knots.Select(k => G((40, k)))) + string.Concat((weights ?? []).Select(w => G((41, w))))
        + string.Concat(ctrl.Select(p => G((10, p.X), (20, p.Y), (30, 0)))) + string.Concat((fit ?? []).Select(p => G((11, p.X), (21, p.Y), (31, 0))));
    static readonly double W = Math.Sqrt(0.5);
    static string NurbsCircle(double cx, double cy, double r) =>
        Spline(2, [(cx + r, cy), (cx + r, cy + r), (cx, cy + r), (cx - r, cy + r), (cx - r, cy), (cx - r, cy - r), (cx, cy - r), (cx + r, cy - r), (cx + r, cy)],
            [0, 0, 0, 0.25, 0.25, 0.5, 0.5, 0.75, 0.75, 1, 1, 1], [1, W, 1, W, 1, W, 1, W, 1], 1 | 8);

    [Fact(DisplayName = "T-0760 SPLINE: rationaler NURBS-Kreis (Gewichte) liegt genau auf dem Kreis, Sehnenfehler ≤ 0,001 mm auch in Zoll; als Loch in einer Platte gefräst")]
    public void Spline_nurbs_circle()
    {
        foreach (var (units, k) in new[] { (4, 1.0), (1, 25.4) })
        {
            var r0 = 40 / k;
            var rd = CadDxf.Parse(Dxf(NurbsCircle(50 / k, 50 / k, r0), units: units));
            var e = Assert.Single(rd.Drawing.Entities); Assert.Empty(rd.Unread); Assert.True(e.Closed); Assert.Equal("SPLINE", e.Source);
            var worst = 0.0;
            for (var i = 0; i < e.Points.Count; i++)
            {
                var p = e.Points[i]; var q = e.Points[(i + 1) % e.Points.Count];
                Assert.Equal(40, Math.Sqrt(Math.Pow(p[0] - 50, 2) + Math.Pow(p[1] - 50, 2)), 9);
                worst = Math.Max(worst, 40 - Math.Sqrt(Math.Pow((p[0] + q[0]) / 2 - 50, 2) + Math.Pow((p[1] + q[1]) / 2 - 50, 2)));
            }
            log.WriteLine($"Einheit {units}: {e.Points.Count} Punkte, Pfeilhöhe {worst:0.0000000} mm");
            Assert.True(worst <= 0.001 + 1e-9, $"{worst}");
        }
        var (r, text) = Cam(Dxf(Lw(Rect(0, 0, 100, 100)) + NurbsCircle(50, 50, 20)));
        log.WriteLine(text);
        Assert.NotNull(r); Assert.Equal(1, Near(r!, "innen", 50, 50, 0.5)); Assert.Equal(1, r.Paths.Count(p => p.Kind == "aussen"));
        Assert.DoesNotContain("SPLINE", text);
        // Wohnungsplaner: Freiformkurve wird wie die Ellipse nicht in Hunderte Wandstücke zerlegt, sondern gemeldet
        var plan = PlanDxfImport.Import(new FloorPlan(), CadDxf.Parse(Dxf(Lw(Rect(0, 0, 5000, 4000)) + NurbsCircle(2000, 2000, 500))));
        log.WriteLine(plan.Text); Assert.Equal(1, plan.SkippedKinds["Freiformkurve (SPLINE, nicht als Wand übernommen)"]); Assert.Equal(4, plan.Walls);
    }

    [Fact(DisplayName = "T-0760 SPLINE: nicht geraten – geschlossen ohne passende Enden, nur Fitpunkte, Fitpunkte neben der Kurve, fallende Knoten, Gewicht ≤ 0 bleiben gemeldet")]
    public void Spline_not_guessed()
    {
        var open = Spline(3, [(0, 0), (30, 80), (70, -20), (100, 0)], [0, 0, 0, 0, 1, 1, 1, 1], flags: 1 | 8);
        var cases = new (string Name, string Dxf, string Why, bool All)[]
        {
            ("geschlossen, Enden offen", open, "treffen sich nicht", false),
            ("nur Fitpunkte", G((0, "SPLINE"), (8, "0"), (70, 8), (71, 3), (72, 0), (73, 0), (74, 3), (11, 0), (21, 0), (31, 0), (11, 50), (21, 50), (31, 0), (11, 100), (21, 0), (31, 0)), "nur Fitpunkte", true),
            ("Fitpunkt neben der Kurve", Spline(3, [(0, 0), (30, 80), (70, -20), (100, 0)], [0, 0, 0, 0, 1, 1, 1, 1], fit: [(0, 0), (50, 40), (100, 0)]), "Fitpunkte liegen nicht", true),
            ("fallende Knoten", Spline(3, [(0, 0), (30, 80), (70, -20), (100, 0)], [0, 0, 0, 1, 0, 1, 1, 1]), "Knoten fallen", false),
            ("Gewicht 0", Spline(2, [(0, 0), (50, 50), (100, 0)], [0, 0, 0, 1, 1, 1], [1, 0, 1]), "Gewicht", true),
        };
        foreach (var (name, dxf, why, all) in cases)
        {
            var rd = CadDxf.Parse(Dxf(dxf)); var u = Assert.Single(rd.Unread); log.WriteLine($"{name}: {u.What} {u.B}");
            Assert.Contains(why, u.What); Assert.Equal(all, u.B.IsAll); Assert.Equal(1, rd.Skipped["SPLINE"]); Assert.Empty(rd.Drawing.Entities);
        }
        // Fitpunkte auf der Kurve (wie AutoCAD sie mitschreibt): gelesen
        (double X, double Y) Bez(double t) { var v = 1 - t; return (3 * v * v * t * 30 + 3 * v * t * t * 70 + t * t * t * 100, 3 * v * v * t * 80 + 3 * v * t * t * -20); }
        var good = CadDxf.Parse(Dxf(Spline(3, [(0, 0), (30, 80), (70, -20), (100, 0)], [0, 0, 0, 0, 1, 1, 1, 1], fit: [Bez(0), Bez(0.3), Bez(0.5), Bez(1)])));
        Assert.Empty(good.Unread); Assert.Single(good.Drawing.Entities);
    }

    // ---------- MLINE ----------

    static string Style(string handle, string name, int flags, params double[] offs) =>
        G((0, "MLINESTYLE"), (5, handle), (2, name), (70, flags), (3, ""), (62, 256), (51, 90), (52, 90), (71, offs.Length)) + string.Concat(offs.Select(o => G((49, o), (62, 256), (6, "BYLAYER"))));

    /// <summary>MLINE wie AutoCAD: Richtung je Scheitel, Gehrung = Winkelhalbierende der linken Normalen, Parameter = Abstand / (Gehrung · Normale).</summary>
    static string Mline((double X, double Y)[] v, bool closed, double scale, double[] offs, string styleHandle = "A1", string layer = "0", int extraFlags = 0, double breakParam = 0)
    {
        var n = v.Length; var sb = new StringBuilder(G((0, "MLINE"), (8, layer), (2, "STANDARD"), (340, styleHandle), (40, scale), (70, 1), (71, 1 | (closed ? 2 : 0) | extraFlags), (72, n), (73, offs.Length), (10, v[0].X), (20, v[0].Y), (30, 0)));
        (double X, double Y) Dir(int i) { var a = v[i]; var b = v[(i + 1) % n]; var l = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2)); return ((b.X - a.X) / l, (b.Y - a.Y) / l); }
        for (var k = 0; k < n; k++)
        {
            var dOut = !closed && k == n - 1 ? Dir(k - 1) : Dir(k);
            var dIn = !closed && k == 0 ? dOut : Dir((k - 1 + n) % n);
            (double X, double Y) nOut = (-dOut.Y, dOut.X), nIn = (-dIn.Y, dIn.X);
            var mx = nOut.X + nIn.X; var my = nOut.Y + nIn.Y; var ml = Math.Sqrt(mx * mx + my * my); mx /= ml; my /= ml;
            var dot = mx * nOut.X + my * nOut.Y;
            sb.Append(G((11, v[k].X), (21, v[k].Y), (31, 0), (12, dOut.X), (22, dOut.Y), (32, 0), (13, mx), (23, my), (33, 0)));
            foreach (var o in offs) sb.Append(breakParam == 0 ? G((74, 2), (41, o * scale / dot), (41, 0), (75, 0)) : G((74, 3), (41, o * scale / dot), (41, 0), (41, breakParam), (75, 0)));
        }
        return sb.ToString();
    }

    [Fact(DisplayName = "T-0760 MLINE: Wand als geschlossene Mehrfachlinie (Stil ±0,5 × 20) → zwei Rechtecke im Abstand 20; offene Mehrfachlinie mit Linien-Enden wird als ein Teil gefräst")]
    public void Mline_walls()
    {
        var style = Style("A1", "STANDARD", 16 | 256, 0.5, -0.5);
        var rd = CadDxf.Parse(Dxf(Mline(Rect(0, 0, 300, 200), true, 20, [0.5, -0.5]), objects: style));
        Assert.Empty(rd.Unread); Assert.Empty(rd.Skipped);
        Assert.Equal(2, rd.Drawing.Entities.Count); Assert.All(rd.Drawing.Entities, e => Assert.True(e.Closed));
        var inner = Ext([rd.Drawing.Entities[0]]); var outer = Ext([rd.Drawing.Entities[1]]); // +10 liegt links = innen beim Rechteck gegen den Uhrzeigersinn
        Assert.Equal((10.0, 10.0, 290.0, 190.0), (Math.Round(inner.MinX, 9), Math.Round(inner.MinY, 9), Math.Round(inner.MaxX, 9), Math.Round(inner.MaxY, 9)));
        Assert.Equal((-10.0, -10.0, 310.0, 210.0), (Math.Round(outer.MinX, 9), Math.Round(outer.MinY, 9), Math.Round(outer.MaxX, 9), Math.Round(outer.MaxY, 9)));
        // offene Mehrfachlinie (L-Form) mit Linien-Enden: ein geschlossener Umriss → eine Außenbahn
        var (r, text) = Cam(Dxf(Mline([(0, 0), (200, 0), (200, 100)], false, 20, [0.5, -0.5]), objects: style));
        log.WriteLine(text);
        Assert.NotNull(r); Assert.Single(r!.Paths, p => p.Kind == "aussen"); Assert.DoesNotContain("Mehrfachlinie", text);
        // runde Enden (äußerer Bogen): Umriss geschlossen, Bogen nach außen
        var round = CadDxf.Parse(Dxf(Mline([(0, 0), (200, 0)], false, 20, [0.5, -0.5], "A2"), objects: Style("A2", "RUND", 64 | 1024, 0.5, -0.5)));
        var ext = Ext(round.Drawing.Entities); Assert.Equal(-10, ext.MinX, 6); Assert.Equal(210, ext.MaxX, 6);
        // Wohnungsplaner: Mehrfachlinie auf Ebene WAND wird als Wand übernommen
        var plan = PlanDxfImport.Import(new FloorPlan(), CadDxf.Parse(Dxf(Mline(Rect(0, 0, 5000, 4000), true, 240, [0.5, -0.5], layer: "WAND"), objects: style)));
        log.WriteLine(plan.Text); Assert.True(plan.Walls >= 4, plan.Text);
    }

    [Fact(DisplayName = "T-0760 MLINE: widersprüchliche Daten (Richtung, Stil-Abstände, Unterbrechungen) bleiben gemeldet; ohne Stil nur die Linien (offene Enden meldet das Fräsen)")]
    public void Mline_not_guessed()
    {
        var style = Style("A1", "STANDARD", 16 | 256, 0.5, -0.5);
        var cases = new (string Name, string Dxf, string Why)[]
        {
            ("Stil 0,5/−0,25", Dxf(Mline(Rect(0, 0, 300, 200), true, 20, [0.5, -0.5]), objects: Style("A1", "STANDARD", 0, 0.5, -0.25)), "Abstände passen nicht"),
            ("Unterbrechung", Dxf(Mline(Rect(0, 0, 300, 200), true, 20, [0.5, -0.5], breakParam: 50), objects: style), "Unterbrechungen"),
            ("Richtung falsch", Dxf(Mline(Rect(0, 0, 300, 200), true, 20, [0.5, -0.5]).Replace("\n12\n1\n22\n0\n", "\n12\n0\n22\n1\n"), objects: style), "Richtung"),
        };
        foreach (var (name, dxf, why) in cases)
        {
            var rd = CadDxf.Parse(dxf); var u = Assert.Single(rd.Unread); log.WriteLine($"{name}: {u.What}");
            Assert.Contains(why, u.What); Assert.True(u.B.IsAll); Assert.Empty(rd.Drawing.Entities);
        }
        var noStyle = CadDxf.Parse(Dxf(Mline([(0, 0), (200, 0)], false, 20, [0.5, -0.5])));
        Assert.Equal(2, noStyle.Drawing.Entities.Count); Assert.Empty(noStyle.Unread);
    }

    // ---------- REGION, Linse ----------

    [Fact(DisplayName = "T-0760 REGION bleibt gemeldet (ACIS ohne Geometrie-Kern nicht sicher lesbar); Uhrzeiger-Linse: gleiche Bytes passen zu zwei Formen → weiter keine Form")]
    public void Region_and_lens_stay_reported()
    {
        var rd = CadDxf.Parse(Dxf(G((0, "REGION"), (8, "0"), (70, 1), (1, "ACIS-Daten")) + Circle(0, 0, 3)));
        Assert.Equal(1, rd.Skipped["REGION"]); Assert.True(Assert.Single(rd.Unread).B.IsAll);
        // Linse aus zwei Uhrzeiger-Bögen (73 = 0). AutoCAD-/ezdxf-Lesart (Komplementwinkel) ergibt die Linse, die wörtliche Lesart die „Erdnuss“ –
        // beide schließen sich. Gruppe 73 ist in beiden Fällen 0, die Winkel liegen in beiden Lesarten in [0°, 360°): die Datei entscheidet es nicht.
        double R = 40, D = 48, h = Math.Sqrt(R * R - D / 2 * D / 2), a = Math.Atan2(h, D / 2) * 180 / Math.PI;
        static string EArc(double cx, double cy, double r, double a0, double a1) => G((72, 2), (10, cx), (20, cy), (40, r), (50, a0), (51, a1), (73, 0));
        var hatch = G((0, "HATCH"), (8, "0"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, 1), (92, 0), (93, 2))
            + EArc(1000, 500, R, a, 360 - a) + EArc(1048, 500, R, 180 + a, 180 - a) + G((97, 0), (75, 0), (76, 1), (98, 0));
        // beide Lesarten von Hand: Endpunkte jeder Kante treffen den Anfang der nächsten
        (double, double) P(double cx, double deg) => (cx + R * Math.Cos(deg * Math.PI / 180), 500 + R * Math.Sin(deg * Math.PI / 180));
        // AutoCAD: Start 360 − a0, im Uhrzeigersinn um (a1 − a0) → Ende 360 − a1;  wörtlich: Start a0, Ende a1
        Assert.Equal(P(1000, 360 - (360 - a)).Item1, P(1048, 360 - (180 + a)).Item1, 6); // Linse: Ende Kante 1 = Anfang Kante 2
        Assert.Equal(P(1000, 360 - a).Item1, P(1048, 180 + a).Item1, 6); // Erdnuss: ebenfalls zusammenhängend
        var rl = CadDxf.Parse(Dxf(hatch + Circle(1024, 500, 4)));
        Assert.Single(rl.Unread); Assert.Empty(rl.Redundant); // ohne gezeichneten Rand keine Form → bleibt gemeldet
    }

    [Fact(DisplayName = "T-0760: 10 000 Blockverweise mit je Platte und Loch – lesen unter 10 s, abbrechbar")]
    public void Many_inserts_fast()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 10_000; i++) sb.Append(Insert("PL", i % 100 * 150, i / 100 * 100));
        var dxf = Dxf(sb.ToString(), Plate);
        var sw = Stopwatch.StartNew(); var rd = CadDxf.Parse(dxf); var ms = sw.ElapsedMilliseconds;
        log.WriteLine($"10 000 Verweise: {ms} ms, {rd.Drawing.Entities.Count} Elemente");
        Assert.Equal(20_000, rd.Drawing.Entities.Count); Assert.True(ms < 10_000, $"{ms}");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => CadDxf.Parse(dxf, cts.Token));
    }
}
