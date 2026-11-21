using System.Globalization;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix8: Befunde der achten Prüfung (Schraffur) – Schleifen- und Kantenzahlen streng geprüft, NaN/∞ nie in einem Rahmen,
/// Spline-Kante mit „97 0“, WIPEOUT-Ausschnitt über den Rahmen hinaus, Ellipsen-/Uhrzeiger-/Spline-Kanten im Formvergleich, eine Bezeichnungstabelle.</summary>
public class T0747_Fix8AchtePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities, string blocks = "") =>
        "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + (blocks.Length > 0 ? $"0\nSECTION\n2\nBLOCKS\n{blocks}0\nENDSEC\n" : "") + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static (double X, double Y)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static string Lw((double X, double Y)[] pts) => G((0, "LWPOLYLINE"), (8, "0"), (90, pts.Length), (70, 1)) + string.Concat(pts.Select(p => G((10, p.X), (20, p.Y))));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Line(double x0, double y0, double x1, double y1) => G((72, 1), (10, x0), (20, y0), (11, x1), (21, y1));
    static string RectEdges(double x0, double y0, double x1, double y1) => Line(x0, y0, x1, y0) + Line(x1, y0, x1, y1) + Line(x1, y1, x0, y1) + Line(x0, y1, x0, y0);
    const string Head = "0\nHATCH\n8\n0\n10\n0.0\n20\n0.0\n30\n0.0\n210\n0.0\n220\n0.0\n230\n1.0\n2\nSOLID\n70\n1\n71\n0\n";
    const string Tail = "75\n0\n76\n1\n98\n1\n10\n50.5\n20\n30.5\n";
    static string EdgeLoop(int n, string edges, int flags = 1) => G((92, flags), (93, n)) + edges + G((97, 0));
    static string Hatch(object n91, string loops) => Head + (n91 is null ? "" : G((91, n91))) + loops + Tail;
    static string Big => EdgeLoop(4, RectEdges(0, 0, 100, 60));
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }

    /// <summary>Loch r 5 bei 50/30 allein mit der (kaputten) Schraffur als Umriss: darf nie außen gefräst werden.</summary>
    static void NeverOutside(string name, string hatch)
    {
        var dxf = Dxf(hatch + Circle(50, 30, 5));
        var u = CadDxf.Parse(dxf).Unread;
        Assert.True(u.Any(x => x.B.IsAll || x.B.MinX <= 45 && x.B.MinY <= 25 && x.B.MaxX >= 55 && x.B.MaxY >= 35), $"{name}: {string.Join(" | ", u.Select(x => $"{x.What} {x.B}"))}");
        Assert.Throws<InvalidOperationException>(() => Cam(dxf)); // einzige Kontur ausgelassen statt außen gefräst
    }

    [Fact(DisplayName = "T-0747-fix8 H-1/H-2: Schleifenzahl 91 fehlt/0 mit Daten/−1/NaN/zu klein, Kantenzahl 93 zu klein/−1/0/1 – Schraffur nie still übergangen")]
    public void Hatch_counts_strict()
    {
        NeverOutside("b01 91 fehlt", Hatch(null!, Big));
        NeverOutside("b02 91 = 0 mit Daten", Hatch(0, Big));
        NeverOutside("b03 91 zu klein", Hatch(1, EdgeLoop(4, RectEdges(300, 300, 310, 310)) + Big));
        NeverOutside("b04 91 = −1", Hatch(-1, Big));
        NeverOutside("b05 91 = NaN", Hatch("NaN", Big));
        NeverOutside("b06 91 = Text", Hatch("zwei", Big));
        NeverOutside("Polylinienrand 93 zu klein", Hatch(1, G((92, 3), (72, 0), (73, 1), (93, 2), (10, 0), (20, 0), (10, 100), (20, 0), (10, 100), (20, 60), (10, 0), (20, 60), (97, 0))));
        NeverOutside("b08 93 = −1", Hatch(1, EdgeLoop(-1, RectEdges(0, 0, 100, 60))));
        NeverOutside("b09 93 = 0, Kanten folgen", Hatch(1, EdgeLoop(0, RectEdges(0, 0, 100, 60))));
        NeverOutside("b19 93 = 1 bei 4 Linien", Hatch(1, EdgeLoop(1, RectEdges(0, 0, 100, 60))));
        NeverOutside("Polylinienrand mit 2 Punkten ohne Ausbuchtung", Hatch(1, G((92, 3), (72, 0), (73, 1), (93, 2), (10, 0), (20, 0), (10, 100), (20, 60), (97, 0))));
        // f02: dieselbe kaputte Schraffur im Block
        var blk = G((0, "BLOCK"), (8, "0"), (2, "HB"), (70, 0), (10, 0), (20, 0), (30, 0)) + Hatch(0, Big) + G((0, "ENDBLK"), (8, "0"));
        Assert.True(Assert.Single(CadDxf.Parse(Dxf(G((0, "INSERT"), (8, "0"), (2, "HB"), (10, 0), (20, 0), (30, 0)), blk)).Unread).B.IsAll);
        // 91 = 0 ohne Randdaten: leere Schraffur, kein Eintrag
        Assert.Empty(CadDxf.Parse(Dxf(Hatch(0, ""))).Unread);
        // Kreis aus zwei Halbkreisen (2 Punkte mit Ausbuchtung) bleibt gültig
        var ring = Hatch(1, G((92, 18), (72, 1), (73, 1), (93, 2), (10, 45), (20, 30), (42, 1), (10, 55), (20, 30), (42, 1), (97, 0)));
        var rb = Assert.Single(CadDxf.Parse(Dxf(ring)).Unread).B; Assert.Equal((45.0, 25.0, 55.0, 35.0), (Math.Round(rb.MinX, 9), Math.Round(rb.MinY, 9), Math.Round(rb.MaxX, 9), Math.Round(rb.MaxY, 9)));
    }

    [Fact(DisplayName = "T-0747-fix8 H-3: Bogenkante mit Winkel NaN/−NaN/∞/1e400/1e300 – Rahmen „überall“; NaN gelangt nie in einen Rahmen")]
    public void Nan_never_in_box()
    {
        foreach (var a in new[] { "NaN", "-NaN", "Infinity", "1e400", "1e300" })
        {
            var arc = G((72, 2), (10, 100), (20, 30), (40, 30), (50, a), (51, 90), (73, 1));
            NeverOutside($"Winkel {a}", Hatch(1, EdgeLoop(4, Line(0, 0, 100, 0) + arc + Line(100, 60, 0, 60) + Line(0, 60, 0, 0))));
        }
        NeverOutside("Ellipsenkante Achse NaN", Hatch(1, EdgeLoop(1, G((72, 3), (10, 50), (20, 30), (11, "NaN"), (21, 0), (40, 0.6), (50, 0), (51, 6.283), (73, 1)))));
        Assert.True(CadDxf.Box.Empty.With((double.NaN, 1)).IsAll);
        Assert.True(CadDxf.Box.Empty.With((1, double.PositiveInfinity)).IsAll);
        Assert.True(new CadDxf.Box(0, 0, 1, 1).Union(new CadDxf.Box(double.NaN, 0, 1, 1)).IsAll);
        Assert.True(new CadDxf.Box(double.NaN, 0, 1, 1).ToSafe().IsAll);
    }

    [Fact(DisplayName = "T-0747-fix8 M-1/niedrig: Spline-Kante mit „97 0“ mitten im Rand (c16/g04) und als letzte Kante (g03); Ellipsen- und Uhrzeiger-Kanten im Formvergleich")]
    public void Spline_fit_zero_and_shapes()
    {
        // Spline Grad 2, Kontrollpunkte auf einer Geraden = rechte Kante der Platte; danach weitere Kanten und eine Insel (Kreis aus zwei Halbkreisen)
        static string Spline(double x0, double y0, double x1, double y1) => G((72, 4), (94, 2), (73, 0), (74, 0), (95, 6), (96, 3), (40, 0), (40, 0), (40, 0), (40, 1), (40, 1), (40, 1),
            (10, x0), (20, y0), (10, (x0 + x1) / 2), (20, (y0 + y1) / 2), (10, x1), (20, y1), (97, 0));
        var island = G((92, 18), (72, 1), (73, 1), (93, 2), (10, 25), (20, 30), (42, 1), (10, 35), (20, 30), (42, 1), (97, 0));
        var c16 = Hatch(2, EdgeLoop(4, Line(0, 0, 100, 0) + Spline(100, 0, 100, 60) + Line(100, 60, 0, 60) + Line(0, 60, 0, 0)) + island);
        var r = Cam(Dxf(Lw(Rect(0, 0, 100, 60)) + c16 + Circle(30, 30, 5) + Circle(70, 30, 5)));
        log.WriteLine(string.Join(" | ", r.Warnings));
        Assert.Equal(2, r.Paths.Count(p => p.Kind == "innen")); Assert.Single(r.Paths, p => p.Kind == "aussen");
        var g03 = Hatch(1, EdgeLoop(4, Line(0, 0, 100, 0) + Line(100, 0, 100, 60) + Line(100, 60, 0, 60) + Spline(0, 60, 0, 0)));
        Assert.Empty(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + g03)).Unread); // formgleich, fällt weg
        // Platte als ELLIPSE gezeichnet, Schraffur mit Ellipsenkante gleicher Form → formgleich
        var ell = G((0, "ELLIPSE"), (8, "0"), (10, 50), (20, 30), (30, 0), (11, 40), (21, 0), (31, 0), (40, 0.5), (41, 0), (42, 2 * Math.PI));
        var hEll = Hatch(1, EdgeLoop(1, G((72, 3), (10, 50), (20, 30), (11, 40), (21, 0), (40, 0.5), (50, 0), (51, 360), (73, 1))));
        Assert.Empty(CadDxf.Parse(Dxf(ell + hEll)).Unread);
        // Insel als Vollkreis im Uhrzeigersinn (73 = 0) neben gezeichnetem Kreis → formgleich
        var cw = Hatch(2, Big + EdgeLoop(1, G((72, 2), (10, 30), (20, 30), (40, 5), (50, 0), (51, 360), (73, 0)), 16));
        Assert.Empty(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Circle(30, 30, 5) + cw)).Unread);
        // Toleranz je Achse ± 0,01 mm: 0,009 rundum noch gleich, 0,011 nicht
        Assert.Empty(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Hatch(1, EdgeLoop(4, RectEdges(-0.009, -0.009, 100.009, 60.009))))).Unread);
        Assert.Single(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Hatch(1, EdgeLoop(4, RectEdges(-0.011, -0.011, 100.011, 60.011))))).Unread);
    }

    [Fact(DisplayName = "T-0747-fix8 M-2: WIPEOUT-Ausschnitt außerhalb des Bildrahmens (e02) gehört zur Ausdehnung")]
    public void Wipeout_clip_outside_frame()
    {
        var wipe = G((0, "WIPEOUT"), (8, "0"), (90, 0), (10, 0), (20, 0), (30, 0), (11, 100), (21, 0), (31, 0), (12, 0), (22, 60), (32, 0), (13, 1), (23, 1), (70, 7), (71, 2), (91, 5),
            (14, -0.5), (24, -0.5), (14, 3.5), (24, -0.5), (14, 3.5), (24, 0.5), (14, -0.5), (24, 0.5), (14, -0.5), (24, -0.5));
        var b = Assert.Single(CadDxf.Parse(Dxf(wipe)).Unread).B;
        Assert.True(b.MaxX >= 400 && b.MinX <= 0 && b.MaxY >= 60, $"{b}");
        Assert.Throws<InvalidOperationException>(() => Cam(Dxf(wipe + Circle(250, 30, 5))));
    }

    [Fact(DisplayName = "T-0747-fix8 niedrig: eine Bezeichnungstabelle (Schraffur, Abdeckfläche, Bemaßung); Grundriss „nicht übernommen“; formgleiche Schraffur fehlt nicht in den Fräsbahnen")]
    public void Labels_and_redundant_fill()
    {
        Assert.Equal("Schraffur", CadDxf.KindLabel("HATCH")); Assert.Equal("Abdeckfläche", CadDxf.KindLabel("WIPEOUT")); Assert.Equal("Bemaßung", CadDxf.KindLabel("DIMENSION"));
        var dxf = Dxf(Lw(Rect(0, 0, 100, 60)) + Hatch(1, Big) + Circle(50, 30, 5));
        var rd = CadDxf.Parse(dxf); Assert.Equal(1, rd.Redundant["HATCH"]); Assert.Empty(rd.Unread);
        var f = Path.Combine(Path.GetTempPath(), "t0747fix8-" + Guid.NewGuid().ToString("N")[..8] + ".dxf");
        try
        {
            File.WriteAllText(f, dxf);
            var r = new CncJob { ToolD = 1 }.ImportDrawing(f);
            log.WriteLine(string.Join(" | ", r.Warnings));
            Assert.DoesNotContain(r.Warnings, w => w.Contains("Schraffur", StringComparison.Ordinal) || w.Contains("HATCH", StringComparison.Ordinal));
        }
        finally { File.Delete(f); }
        var wall = G((0, "LWPOLYLINE"), (8, "WAND"), (90, 2), (70, 0), (10, 0), (20, 0), (10, 4000), (20, 0)) + G((0, "LWPOLYLINE"), (8, "WAND"), (90, 2), (70, 0), (10, 0), (20, 200), (10, 4000), (20, 200));
        var plan = PlanDxfImport.Import(new FloorPlan(), CadDxf.Parse(Dxf(wall + Hatch(1, Big).Replace("8\n0\n", "8\nSCHRAFFUR\n"))));
        log.WriteLine(plan.Text);
        Assert.Contains("Nicht übernommen: 1× Schraffur", plan.Text, StringComparison.Ordinal); Assert.DoesNotContain("Nicht lesbar", plan.Text, StringComparison.Ordinal);
    }
}
