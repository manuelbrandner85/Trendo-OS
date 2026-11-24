using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0759: Sonderfälle beim Fräsen aus DXF – Brücke zwischen zwei Platten, U mit sich kreuzenden Überständen, Sehne auf anderer Ebene,
/// Bemaßungs-/Mittellinien „zu nah“, Zeichnungsrahmen mit Schriftfeld – und Gegenfälle, die die neuen Regeln angreifen.
/// Grundregel in jedem Test: kein Loch und kein Kreis in einem unklaren Bereich wird als Außenkontur gefräst.</summary>
[Collection("T0747 Zeitmessung")]
public class T0759_FraesenSonderfaelle(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities) => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1, string layer = "0") => G((0, "LINE"), (8, layer), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Circle(double x, double y, double r, string layer = "0") => G((0, "CIRCLE"), (8, layer), (10, x), (20, y), (30, 0), (40, r));
    static string Lw(IEnumerable<(double X, double Y)> pts, string layer = "0")
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, layer), (90, p.Count), (70, 1)));
        foreach (var q in p) sb.Append(G((10, q.X), (20, q.Y)));
        return sb.ToString();
    }
    static (double X, double Y)[] Sq(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static readonly (double X, double Y)[] Notch = [(0, 0), (100, 0), (100, 20), (70, 20), (70, 80), (100, 80), (100, 100), (0, 100)];
    static (CamResult? R, string Text) Cam(string dxf, double tool = 1)
    {
        try { var rd = CadDxf.Parse(dxf); var r = CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = tool, Unread = rd.Unread, Texts = CncCam.TextPoints(rd.Drawing) }); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool Around(CamResult? r, string kind, double x, double y, double rr, double tool = 1) => r is not null &&
        r.Paths.Any(p => p.Kind == kind && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - (kind == "aussen" ? tool / 2 : -tool / 2)) < 0.05));
    static bool PlateOut(CamResult? r, double x0, double y0, double x1, double y1) => r is not null &&
        r.Paths.Any(p => p.Kind == "aussen" && p.Pts.Min(q => q.X - r.ShiftX) < x0 && p.Pts.Max(q => q.X - r.ShiftX) > x1 && p.Pts.Min(q => q.Y - r.ShiftY) < y0 && p.Pts.Max(q => q.Y - r.ShiftY) > y1
            && p.Pts.Min(q => q.X - r.ShiftX) > x0 - 2 && p.Pts.Max(q => q.X - r.ShiftX) < x1 + 2);
    (CamResult? R, string Text) Run(string name, string dxf, double tool = 1) { var res = Cam(dxf, tool); log.WriteLine($"{name}: {(res.R is null ? "kein G-Code" : res.R.Paths.Count + " Bahnen")} | {res.Text[..Math.Min(500, res.Text.Length)]}"); return res; }

    [Fact(DisplayName = "T-0759 Fall 1: Brücke aus zwei Linien zwischen zwei Platten – Kreis im eingeschlossenen Bereich nie außen, Löcher der Platten innen")]
    public void Bridge_between_two_plates()
    {
        var (r, text) = Run("b1", Dxf(Lw(Sq(0, 0, 50, 100)) + Lw(Sq(100, 0, 150, 100)) + Line(50, 30, 100, 30) + Line(50, 70, 100, 70) + Circle(75, 50, 5) + Circle(25, 50, 5) + Circle(125, 50, 5)));
        Assert.False(Around(r, "aussen", 75, 50, 5), "Kreis in der Brücke außen gefräst");
        Assert.True(Around(r, "innen", 25, 50, 5)); Assert.True(Around(r, "innen", 125, 50, 5));
        Assert.Contains("einen Bereich ein", text);
        // Kontrolle: eine einzelne Linie zwischen zwei Platten schließt nichts ein – die neue Regel greift nicht
        var (r1, t1) = Run("eine Linie", Dxf(Lw(Sq(0, 0, 50, 100)) + Lw(Sq(100, 0, 150, 100)) + Line(50, 30, 100, 30) + Circle(25, 50, 5)));
        Assert.DoesNotContain("einen Bereich ein", t1); Assert.True(Around(r1, "innen", 25, 50, 5));
        // Gegenfall: Brücke als U über drei Linien und eine dritte Platte – Kreis darin nie außen
        var (r2, _) = Run("drei Platten", Dxf(Lw(Sq(0, 0, 50, 100)) + Lw(Sq(100, 0, 150, 100)) + Lw(Sq(200, 0, 250, 100)) + Line(50, 30, 100, 30) + Line(150, 30, 200, 30) + Line(50, 90, 200, 90) + Circle(125, 60, 5) + Circle(25, 50, 5)));
        Assert.False(Around(r2, "aussen", 125, 60, 5), "Kreis zwischen den Brücken außen gefräst");
    }

    [Fact(DisplayName = "T-0759 Fall 2: U mit sich kreuzenden Überständen vor einer Kerbe – Kreis darin nie außen; Gegenfall „#“ ohne Platte")]
    public void Crossing_u_and_hash()
    {
        var (r, text) = Run("x1", Dxf(Lw(Notch) + Line(100, 20, 150, 20) + Line(148, 18, 148, 82) + Line(150, 80, 100, 80) + Circle(120, 50, 5) + Circle(30, 50, 5)));
        Assert.False(Around(r, "aussen", 120, 50, 5), "Kreis im U außen gefräst");
        Assert.True(Around(r, "innen", 30, 50, 5));
        // Gegenfall: vier sich kreuzende Linien („#“) schließen einen Kreis ein – nie außen, die Platte daneben bleibt
        var (h, ht) = Run("#", Dxf(Line(0, 10, 60, 10) + Line(0, 50, 60, 50) + Line(10, 0, 10, 60) + Line(50, 0, 50, 60) + Circle(30, 30, 5) + Lw(Sq(200, 0, 260, 60)) + Circle(230, 30, 5)));
        Assert.False(Around(h, "aussen", 30, 30, 5), "Kreis im # außen gefräst");
        Assert.True(Around(h, "innen", 230, 30, 5)); Assert.True(PlateOut(h, 200, 0, 260, 60));
        // Kontrolle: Mittelkreuz aus zwei sich kreuzenden Linien (kein Kreis darin) über einer Bohrung, gleiche Ebene → Bohrung bleibt gefräst, wenn weit genug
        var (k, _) = Run("Kreuz", Dxf(Lw(Sq(0, 0, 100, 60)) + Circle(30, 30, 5) + Line(70, 20, 90, 40) + Line(70, 40, 90, 20)));
        Assert.True(Around(k, "innen", 30, 30, 5));
    }

    [Fact(DisplayName = "T-0759 Fall 3: Sehne bzw. U auf anderer Ebene vor einer Kerbe – zählt wie auf derselben Ebene (Kreis in der Kerbe nie außen)")]
    public void Chord_on_other_layer()
    {
        foreach (var layer in new[] { "BEM", "KONTUR2", "Bemaßung" })
        {
            var (r, text) = Run($"Sehne {layer}", Dxf(Lw(Notch) + Line(100, 20, 100, 80, layer) + Circle(85, 50, 5) + Circle(30, 50, 2)), 6);
            Assert.False(Around(r, "aussen", 85, 50, 5, 6), $"{layer}: Kreis in der Kerbe außen gefräst");
            Assert.True(Around(r, "innen", 30, 50, 2, 6) || r is null, $"{layer}: Loch in der Platte");
            var (u, _) = Run($"U {layer}", Dxf(Lw(Notch) + Line(100, 20, 150, 20, layer) + Line(150, 20, 150, 80, layer) + Line(150, 80, 100, 80, layer) + Circle(120, 50, 5) + Circle(30, 50, 5)));
            Assert.False(Around(u, "aussen", 120, 50, 5), $"{layer}: Kreis im U außen gefräst");
            Assert.True(Around(u, "innen", 30, 50, 5));
        }
    }

    static string Dim(double x1, double x2, double yBase, double yDim, string layer)
    {
        string Arrow(double x, double y, int dir) => Line(x, y, x - dir * 2.5, y + 0.8, layer) + Line(x, y, x - dir * 2.5, y - 0.8, layer);
        return Line(x1, yBase, x1, yDim + 1.25, layer) + Line(x2, yBase, x2, yDim + 1.25, layer) + Line(x1, yDim, x2, yDim, layer) + Arrow(x1, yDim, -1) + Arrow(x2, yDim, 1);
    }

    [Fact(DisplayName = "T-0759 Fall 4: Bemaßung und Mittellinien auf eigener Ebene machen Bohrungen und Platte nicht mehr „zu nah“")]
    public void Dimension_and_center_lines()
    {
        var plate = Lw(Sq(0, 0, 200, 100));
        var (d1, _) = Run("d1", Dxf(plate + Circle(50, 50, 5) + Circle(150, 50, 5) + Dim(50, 150, 50, 80, "BEM")));
        Assert.True(Around(d1, "innen", 50, 50, 5) && Around(d1, "innen", 150, 50, 5), "Bohrungen mit Bemaßung fehlen");
        var (m1, _) = Run("m1", Dxf(plate + Circle(50, 50, 5) + Line(50, 50, 57, 50, "MITTE") + Line(50, 50, 43, 50, "MITTE") + Line(50, 50, 50, 57, "MITTE") + Line(50, 50, 50, 43, "MITTE")));
        Assert.True(Around(m1, "innen", 50, 50, 5), "Bohrung mit Mittelkreuz fehlt");
        var (m2, _) = Run("m2", Dxf(plate + Circle(50, 50, 5) + Circle(150, 20, 5) + Line(-5, 50, 100, 50, "Mittellinie") + Line(100, 50, 205, 50, "Mittellinie") + Line(100, 50, 100, 55, "Mittellinie")));
        Assert.True(Around(m2, "innen", 50, 50, 5) && Around(m2, "innen", 150, 20, 5) && PlateOut(m2, 0, 0, 200, 100), "Mittellinie über die Platte");
        var (d2, _) = Run("d2 mit Abstand", Dxf(plate + Circle(50, 50, 5) + Dim(0, 200, 100.625, 115, "A-ANNO-DIMS")));
        Assert.True(PlateOut(d2, 0, 0, 200, 100), "Platte mit Bemaßung im Abstand fehlt");
        // Kontrolle: dieselbe Bemaßung auf Ebene 0 ist kein sicher erkannter Hilfslinien-Layer – Platte bleibt „zu nah“
        var (d0, t0) = Run("d2 Ebene 0", Dxf(plate + Circle(50, 50, 5) + Dim(0, 200, 100.625, 115, "0")));
        Assert.False(PlateOut(d0, 0, 0, 200, 100)); Assert.Contains("zu nah", t0);
        // Gegenfall: Kerbe, die nur durch eine Bemaßung (Hilfslinien ab den Kerbenecken) geschlossen wird – Kreis darin nie außen
        var (k, _) = Run("Kerbe nur durch Bemaßung", Dxf(Lw(Notch) + Line(100, 20, 112, 20, "BEM") + Line(100, 80, 112, 80, "BEM") + Line(110, 18.75, 110, 81.25, "BEM") + Circle(90, 50, 5) + Circle(30, 50, 5)));
        Assert.False(Around(k, "aussen", 90, 50, 5), "Kreis in der durch Bemaßung geschlossenen Kerbe außen gefräst");
        Assert.True(Around(k, "innen", 30, 50, 5));
        // Gegenfall: Lochkreis (geschlossen) auf Mittellinien-Ebene – wird nicht gefräst, die Bohrung darin nie außen
        var (lk, lt) = Run("Lochkreis", Dxf(plate + Circle(100, 50, 30, "MITTE") + Circle(100, 50, 8) + Circle(130, 50, 3)));
        Assert.False(Around(lk, "innen", 100, 50, 30) || Around(lk, "aussen", 100, 50, 30), "Lochkreis gefräst");
        Assert.False(Around(lk, "aussen", 100, 50, 8), "Bohrung im Lochkreis außen gefräst");
        Assert.True(PlateOut(lk, 0, 0, 200, 100)); Assert.Contains("Lochkreis", lt);
    }

    static string Text(double x, double y, string t, string layer = "0") => G((0, "TEXT"), (8, layer), (10, x), (20, y), (30, 0), (40, 3), (1, t));
    /// <summary>Rahmen wie <see cref="FrameLines"/> mit Text im Schriftfeld (T-0759-fix1: Beleg für den Rahmen).</summary>
    static string Frame(string layer, double gap = 0) => FrameLines(layer, gap) + Text(240, 20, "Maßstab 1:1", layer) + Text(330, 20, "Blatt 1", layer);
    static string FrameLines(string layer, double gap = 0)
    {
        // Blatt A3 quer 420 × 297, Schriftfeld 180 × 50 rechts unten mit Teilung
        return Line(-10, -10, 230, -10, layer) + Line(230, -10, 410, -10, layer) + Line(410, -10, 410, 40, layer) + Line(410, 40, 410, 287, layer) + Line(410, 287, -10, 287, layer)
            + Line(-10, 287, -10, -10 + gap, layer) + Line(230, -10, 230, 40, layer) + Line(230, 40, 410, 40, layer) + Line(320, -10, 320, 40, layer);
    }

    [Fact(DisplayName = "T-0759 Fall 5: Zeichnungsrahmen mit Schriftfeld wird erkannt und nicht gefräst, die Teile darin werden gefräst; Gegenfälle bleiben vorsichtig")]
    public void Drawing_frame()
    {
        var parts = Lw(Sq(0, 50, 100, 110)) + Circle(50, 80, 5) + Lw(Sq(150, 50, 250, 110)) + Circle(200, 80, 5);
        foreach (var (name, frame) in new[] { ("RAHMEN", Frame("RAHMEN")), ("Ebene 0, A3", Frame("0")), ("Spalt 0,05", Frame("RAHMEN", 0.05)) })
        {
            var (r, text) = Run(name, Dxf(frame + parts));
            Assert.True(Around(r, "innen", 50, 80, 5) && Around(r, "innen", 200, 80, 5), $"{name}: Löcher fehlen");
            Assert.True(PlateOut(r, 0, 50, 100, 110) && PlateOut(r, 150, 50, 250, 110), $"{name}: Platten fehlen");
            Assert.Contains("Zeichnungsrahmen", text);
            Assert.True(r!.ShiftX > -1 && r.ShiftX < 1 && r.ShiftY > -51 && r.ShiftY < -49, "Nullpunkt an den Teilen, nicht am Rahmen");
        }
        // geschlossener Rahmen (Polylinie) mit Schriftfeld-Linien: vorher wurden die Platten als Löcher und die Kreise als Teile (außen) gefräst
        var (c, ctext) = Run("geschlossen", Dxf(Lw(Sq(-10, -10, 410, 287), "Rahmen") + Line(230, -10, 230, 40, "Rahmen") + Line(230, 40, 410, 40, "Rahmen") + Line(320, -10, 320, 40, "Rahmen") + Text(240, 20, "Werkstoff") + parts));
        Assert.False(Around(c, "aussen", 50, 80, 5) || Around(c, "aussen", 200, 80, 5), "Loch außen gefräst");
        Assert.True(Around(c, "innen", 50, 80, 5)); Assert.Contains("Zeichnungsrahmen", ctext);
        // Gegenfall: „Rahmen“ ist eigentlich eine Platte 300 × 200 aus Linien mit Teilung (Stoß) und Löchern – keine Normgröße, Ebene 0: nicht als Rahmen
        var plateLike = Line(0, 0, 200, 0) + Line(200, 0, 300, 0) + Line(300, 0, 300, 200) + Line(300, 200, 0, 200) + Line(0, 200, 0, 0) + Line(200, 0, 200, 40) + Line(200, 40, 300, 40);
        var (p, ptext) = Run("Platte aus Linien", Dxf(plateLike + Circle(50, 100, 5) + Circle(150, 100, 5)));
        Assert.False(Around(p, "aussen", 50, 100, 5) || Around(p, "aussen", 150, 100, 5), "Loch der Platte außen gefräst");
        Assert.DoesNotContain("Zeichnungsrahmen", ptext);
        // Gegenfall: Rahmen-Ebene, aber ein Teil berührt den Rahmen – nicht eindeutig, nichts außen
        var (t, ttext) = Run("Teil am Rahmen", Dxf(Frame("RAHMEN") + Lw(Sq(-10, 50, 100, 110)) + Circle(50, 80, 5)));
        Assert.DoesNotContain("Zeichnungsrahmen", ttext); Assert.False(Around(t, "aussen", 50, 80, 5));
        // Gegenfall: Rahmen ohne Schriftfeld (geschlossenes Rechteck) – nicht von einer Platte zu unterscheiden, bleibt Platte (Kreise darin Löcher)
        var (n, ntext) = Run("ohne Schriftfeld", Dxf(Lw(Sq(-10, -10, 410, 287), "RAHMEN") + Circle(50, 80, 5)));
        Assert.DoesNotContain("Zeichnungsrahmen", ntext); Assert.True(Around(n, "innen", 50, 80, 5));
    }

    [Fact(DisplayName = "T-0759: Mittellinien-Stern (Baum) lässt die Bohrung in seiner Hülle nicht mehr aus – auf Ebene 0, mit Kreis im Verbund oder als „[ ]“ mit Spalt weiter ausgelassen")]
    public void Junction_tree_vs_cycle()
    {
        // Stern aus drei Linien auf der Mittellinien-Ebene in einer Platte, Bohrung teilweise in seiner Hülle, weit genug von den Linien – vorher ausgelassen, jetzt gefräst
        string Star(string layer) => Line(50, 30, 90, 30, layer) + Line(50, 30, 20, 55, layer) + Line(50, 30, 20, 5, layer);
        var (t, _) = Run("Baum MITTE", Dxf(Lw(Sq(0, 0, 100, 60)) + Circle(18, 30, 3) + Star("MITTE")));
        Assert.True(Around(t, "innen", 18, 30, 3), "Bohrung am Rand der Hülle eines Mittellinien-Sterns fehlt");
        // derselbe Stern auf Ebene 0: könnte mit einer Schließsehne ein Umriss sein – Bohrung bleibt ausgelassen (wie bisher), nie außen
        var (t0, _) = Run("Baum 0", Dxf(Lw(Sq(0, 0, 100, 60)) + Circle(18, 30, 3) + Star("0")));
        Assert.False(Around(t0, "aussen", 18, 30, 3)); Assert.False(Around(t0, "innen", 18, 30, 3));
        // Bohrung ganz in der Hülle des Mittellinien-Sterns: eine Schließsehne könnte sie einschließen – weiter ausgelassen, nie außen
        var (ti, _) = Run("Baum MITTE innen", Dxf(Lw(Sq(0, 0, 100, 60)) + Circle(32, 30, 3) + Star("MITTE")));
        Assert.False(Around(ti, "aussen", 32, 30, 3)); Assert.False(Around(ti, "innen", 32, 30, 3));
        // Gegenfall (Prüfer 15 gg): „[“ und „]“ aus Linien mit Stummel (je ein Baum), 0,2 mm Spalt – auch auf der Bemaßungs-Ebene liegt der Kreis ganz in der
        // Hülle: nie außen
        foreach (var layer in new[] { "0", "BEM" })
        {
            var g = 0.1;
            var gg = Line(50 - g, 60, 0, 60, layer) + Line(0, 60, 0, 30, layer) + Line(0, 30, 0, 0, layer) + Line(0, 0, 50 - g, 0, layer) + Line(0, 30, 10, 30, layer)
                + Line(50 + g, 0, 100, 0, layer) + Line(100, 0, 100, 30, layer) + Line(100, 30, 100, 60, layer) + Line(100, 60, 50 + g, 60, layer) + Line(100, 30, 90, 30, layer);
            var (r, _) = Run($"[ ] {layer}", Dxf(gg + Circle(50, 30, 5) + Circle(25, 15, 2)));
            Assert.False(Around(r, "aussen", 50, 30, 5) || Around(r, "aussen", 25, 15, 2), $"{layer}: Loch in „[ ]“ außen gefräst");
        }
        // Rechteck aus Linien mit Stichlinie an einer Ecke (Stoß, Kreis im Graphen) – Kreis darin nie außen
        var (c, ct) = Run("Kreis", Dxf(Line(0, 0, 100, 0) + Line(100, 0, 100, 60) + Line(100, 60, 0, 60) + Line(0, 60, 0, 0) + Line(100, 0, 110, -10) + Circle(50, 30, 5)));
        Assert.False(Around(c, "aussen", 50, 30, 5), "Loch im Linienverbund außen gefräst");
    }

    [Fact(DisplayName = "T-0759: dichte Gravurlinien (Enden ≤ 0,1 mm, Verbund mit Kreis) – Bohrungen außerhalb ihrer Hülle bleiben Löcher, in der Hülle nie außen")]
    public void Dense_engraving_keeps_holes_outside_hull()
    {
        // Prüfer 19 r2_diag_3000: Diagonalen von Rand zu Rand im Abstand 0,05 mm (Enden verkettet: Verbund mit Kreis), Bohrungen im Rahmen, aber außerhalb der Hülle der Linien
        var sb = new StringBuilder(Lw(Sq(0, 0, 200, 200)));
        for (var k = 0; k < 400; k++) { var c = 120 + 0.05 * k; sb.Append(Line(0, c, c, 0)); }
        var (r, text) = Run("dicht", Dxf(sb + Circle(100, 100, 5) + Circle(115, 90, 4)));
        Assert.True(Around(r, "innen", 100, 100, 5), "Bohrung neben dichten Gravurlinien ausgelassen");
        Assert.True(Around(r, "innen", 115, 90, 4), "Bohrung neben dichten Gravurlinien ausgelassen");
        // Gegenprobe: Bohrung in der Hülle der dichten Linien (zwischen ihnen) – ausgelassen oder innen, nie außen
        var (r2, _) = Run("dicht innen", Dxf(sb + Circle(65, 65, 2)));
        Assert.False(Around(r2, "aussen", 65, 65, 2), "Bohrung zwischen dichten Linien außen gefräst");
        // Gegenprobe „#“ um die Bohrung bleibt ausgelassen (die Hülle schließt sie ein)
        var (h, _) = Run("# in Platte", Dxf(Lw(Sq(-50, -50, 110, 110)) + Line(0, 10, 60, 10) + Line(0, 50, 60, 50) + Line(10, 0, 10, 60) + Line(50, 0, 50, 60) + Circle(30, 30, 5)));
        Assert.False(Around(h, "innen", 30, 30, 5) || Around(h, "aussen", 30, 30, 5), "Bohrung im „#“ gefräst");
    }

    [Fact(DisplayName = "T-0759: Meldungen werden für die Anzeige zusammengefasst – höchstens 20 je Art, dazu „… und N weitere“")]
    public void Warnings_summarized()
    {
        var w = new List<string>();
        for (var i = 1; i <= 3000; i++) w.Add($"Kontur {i} ausgelassen: offene Kontur (Enden {i % 7},5 mm auseinander) – nur als Gravur.");
        for (var i = 1; i <= 6000; i++) w.Add($"Mehrdeutiger Spalt bei X {i},2 Y 0: mehrere Enden liegen etwa gleich nah – nicht verbunden.");
        w.Add("Eine einzelne Meldung.");
        var s = CncCam.SummarizeWarnings(w);
        Assert.Equal(20 + 1 + 20 + 1 + 1, s.Count);
        Assert.Contains(s, x => x.StartsWith("… und 2.980 weitere", StringComparison.Ordinal));
        Assert.Contains(s, x => x.StartsWith("… und 5.980 weitere", StringComparison.Ordinal));
        Assert.Equal("Eine einzelne Meldung.", s[^1]);
        Assert.Equal(w.Take(20), s.Take(20));
        var few = new List<string> { "a", "b" }; Assert.Equal(few, CncCam.SummarizeWarnings(few));
        // im Fehlertext „keine fräsbare Kontur“ ebenfalls zusammengefasst
        var sb = new StringBuilder();
        for (var k = 0; k < 60; k++) sb.Append(Line(k * 10, 0, k * 10 + 5, 3));
        var (r, text) = Run("nur offen", Dxf(sb.ToString()));
        Assert.Null(r); Assert.Contains("weitere Meldungen der Art „offene Kontur ausgelassen“", text);
        // T-0759-fix1: Meldungen zu Löchern/Platten werden nie gekürzt, auch nicht bei vielen gleichartigen
        var holes = Enumerable.Range(1, 50).Select(i => $"Kontur {i} ausgelassen: liegt in einem Bereich, den die offenen Linien ab Kontur 99 einschließen – Loch oder Teil unklar.").ToList();
        holes.AddRange(Enumerable.Range(1, 30).Select(i => $"Kontur {i + 100} ausgelassen: offene Kontur (Enden {i} mm auseinander) – nur als Gravur."));
        var sh = CncCam.SummarizeWarnings(holes);
        Assert.Equal(50, sh.Count(x => x.Contains("einschließen"))); Assert.Contains(sh, x => x.Contains("10 weitere Meldungen der Art „offene Kontur ausgelassen“ (offene Linien"));
    }

    [Fact(DisplayName = "T-0759: Linien, die Löcher ganz durchqueren (Gravur von Rand zu Rand), schließen nichts ein – Löcher bleiben innen; Langloch aus Linien bis an die Kreise und Brücke durch die Platten weiter vorsichtig")]
    public void Lines_through_holes()
    {
        // zwei parallele Gravurlinien von Rand zu Rand durch zwei Bohrungen (wie T-0758 diagonal_60_loecher_50) – vorher „schließen einen Bereich ein“
        var (r, text) = Run("durch Löcher", Dxf(Lw(Sq(0, 0, 200, 100)) + Circle(60, 50, 8) + Circle(140, 50, 8) + Line(0, 48, 200, 48) + Line(0, 52, 200, 52)));
        // wie 0.9.108/T-0758: die Bohrungen sind wegen der Linien darin „zu nah“ (Hindernis), aber nicht mehr wegen eines eingeschlossenen Bereichs; nie außen
        Assert.DoesNotContain("einen Bereich ein", text); Assert.Contains("zu nah", text);
        Assert.False(Around(r, "aussen", 60, 50, 8) || Around(r, "aussen", 140, 50, 8));
        // Bohrung neben den Linien (nicht berührt) bleibt innen
        var (r3, t3) = Run("durch Löcher + frei", Dxf(Lw(Sq(0, 0, 200, 100)) + Circle(60, 50, 8) + Circle(140, 50, 8) + Line(0, 48, 200, 48) + Line(0, 52, 200, 52) + Circle(100, 80, 5)));
        Assert.True(Around(r3, "innen", 100, 80, 5), "freie Bohrung fehlt: " + t3);
        // Gegenfall Langloch: zwei Linien, die an den Kreisen enden (nicht durchqueren) – Bereich zwischen den Kreisen bleibt unklar, kein Kreis außen
        var (s1, t1) = Run("Langloch", Dxf(Lw(Sq(0, 0, 200, 100)) + Circle(60, 50, 8) + Circle(140, 50, 8) + Line(67.75, 48, 132.25, 48) + Line(67.75, 52, 132.25, 52) + Circle(100, 50, 1)));
        Assert.False(Around(s1, "aussen", 100, 50, 1) || Around(s1, "aussen", 60, 50, 8) || Around(s1, "aussen", 140, 50, 8), "Langloch: Kreis außen gefräst");
        Assert.Contains("einen Bereich ein", t1);
        // Gegenfall Brücke, deren Linien die Platten ganz durchqueren (Material, keine Luft) – Kreis in der Brücke nie außen
        var (b, tb) = Run("Brücke durch Platten", Dxf(Lw(Sq(0, 0, 50, 100)) + Lw(Sq(100, 0, 150, 100)) + Line(-10, 30, 160, 30) + Line(-10, 70, 160, 70) + Circle(75, 50, 5)));
        Assert.False(Around(b, "aussen", 75, 50, 5), "Kreis in der Brücke außen gefräst");
    }

    // ------------------------------------------------------------ T-0759-fix1: Gegenprüfung

    static readonly string Holes = Circle(60, 100, 8) + Circle(150, 100, 10);
    static void NoHoleOutside(string name, CamResult? r, string text)
    {
        Assert.False(Around(r, "aussen", 60, 100, 8) || Around(r, "aussen", 150, 100, 10), $"{name}: Loch außen gefräst – {text[..Math.Min(300, text.Length)]}");
        Assert.DoesNotContain("Zeichnungsrahmen mit Schriftfeld erkannt", text);
    }
    static string Corner(double x1, double y0, double cx, double cy, string l) => Line(cx, y0, cx, cy, l) + Line(cx, cy, x1, cy, l);

    [Fact(DisplayName = "T-0759-fix1 HOCH: Platte mit abgeteiltem Eckfeld wird nie als Zeichnungsrahmen gedeutet – kein Loch außen (Ebene Rahmen, Rahmen-Teil 1, FRAME aus Linien, A4 mit 180-mm-Feld, A3 mit Heftrand)")]
    public void Plate_is_not_a_frame()
    {
        var variants = new (string Name, string Dxf)[]
        {
            ("Ebene Rahmen 300×200", Lw(Sq(0, 0, 300, 200), "Rahmen") + Corner(300, 0, 200, 50, "Rahmen")),
            ("Ebene Rahmen-Teil 1", Lw(Sq(0, 0, 300, 200), "Rahmen-Teil 1") + Corner(300, 0, 200, 50, "Rahmen-Teil 1")),
            ("FRAME aus 4 Linien", Line(0, 0, 300, 0, "FRAME") + Line(300, 0, 300, 200, "FRAME") + Line(300, 200, 0, 200, "FRAME") + Line(0, 200, 0, 0, "FRAME") + Corner(300, 0, 200, 50, "FRAME")),
            ("A4 quer, Ebene 0, Eckfeld 180", Lw(Sq(0, 0, 297, 210)) + Corner(297, 0, 117, 40, "0")),
            ("A3 mit Heftrand, Ebene 0", Lw(Sq(0, 0, 400, 277)) + Corner(400, 0, 220, 60, "0")),
        };
        foreach (var (name, plate) in variants)
        {
            var (r, text) = Run(name, Dxf(plate + Holes));
            NoHoleOutside(name, r, text);
            // Löcher innen (keine Rahmen-Deutung) oder ausgelassen mit Meldung – nie außen
            Assert.True((Around(r, "innen", 60, 100, 8) && Around(r, "innen", 150, 100, 10)) || text.Contains("Rahmen oder Platte nicht eindeutig"), $"{name}: weder Löcher noch Meldung");
        }
        // mit Text im (nicht unterteilten) Eckfeld: nicht eindeutig
        var (t1, tt1) = Run("Text, nicht unterteilt", Dxf(Lw(Sq(0, 0, 300, 200), "Rahmen") + Corner(300, 0, 200, 50, "Rahmen") + Text(220, 20, "Teil 4711") + Holes));
        NoHoleOutside("Text, nicht unterteilt", t1, tt1); Assert.Contains("nicht eindeutig", tt1);
        // unterteilt, aber ohne Text: nicht eindeutig
        var (t2, tt2) = Run("unterteilt, ohne Text", Dxf(Lw(Sq(0, 0, 300, 200), "Rahmen") + Corner(300, 0, 200, 50, "Rahmen") + Line(250, 0, 250, 50, "Rahmen") + Holes));
        NoHoleOutside("unterteilt, ohne Text", t2, tt2); Assert.Contains("nicht eindeutig", tt2);
        // Kontrolle: echter Rahmen (Ebene Rahmen, Text im unterteilten Schriftfeld) – Teil außen, Loch darin innen
        var (f, ft) = Run("echter Rahmen", Dxf(Lw(Sq(-10, -10, 410, 287), "Rahmen") + Line(230, -10, 230, 40, "Rahmen") + Line(230, 40, 410, 40, "Rahmen") + Line(320, -10, 320, 40, "Rahmen")
            + Text(240, 20, "Werkstoff") + Lw(Sq(0, 50, 100, 110)) + Circle(50, 80, 5)));
        Assert.Contains("Zeichnungsrahmen mit Schriftfeld erkannt", ft); Assert.True(Around(f, "innen", 50, 80, 5)); Assert.True(PlateOut(f, 0, 50, 100, 110));
    }

    [Fact(DisplayName = "T-0759-fix1: Platte auf Ebene ACHSE/CENTER/Mitte/BEM wird gefräst (enthält Konturen); „Achse 1“, „Dimension 3mm“, Wortteile sind keine Hilfslinien-Ebenen")]
    public void Plate_on_annotation_like_layer()
    {
        foreach (var l in new[] { "ACHSE", "CENTER", "Mitte", "BEM", "Achse 1", "Dimension 3mm", "Mittelteil", "DIMMER", "Teil-Mitte", "Achsenteil" })
        {
            var (r, text) = Run(l, Dxf(Lw(Sq(0, 0, 200, 120), l) + Circle(60, 60, 10) + Circle(140, 60, 8)));
            Assert.True(PlateOut(r, 0, 0, 200, 120), $"{l}: Platte fehlt – {text[..Math.Min(300, text.Length)]}");
            Assert.True(Around(r, "innen", 60, 60, 10) && Around(r, "innen", 140, 60, 8), $"{l}: Löcher fehlen");
        }
        Assert.False(CncCam.IsAnnotationLayer("Achse 1")); Assert.False(CncCam.IsAnnotationLayer("Dimension 3mm")); Assert.False(CncCam.IsAnnotationLayer("Teil-Mitte"));
        Assert.True(CncCam.IsAnnotationLayer("BEM")); Assert.True(CncCam.IsAnnotationLayer("Bemaßung_2")); Assert.True(CncCam.IsAnnotationLayer("A-ANNO-DIMS")); Assert.True(CncCam.IsAnnotationLayer("Dimension"));
        // Lochkreis auf Mittellinien-Ebene (enthält nichts) wird weiter nicht gefräst
        var (k, kt) = Run("Lochkreis", Dxf(Lw(Sq(0, 0, 200, 200)) + Circle(100, 100, 50, "MITTE") + Circle(150, 100, 5) + Circle(50, 100, 5)));
        Assert.Contains("nicht gefräst", kt); Assert.False(Around(k, "aussen", 150, 100, 5) || Around(k, "aussen", 50, 100, 5));
    }

    [Fact(DisplayName = "T-0759-fix1: Umriss aus Hilfslinien mit 0,3-mm-Spalten (Prüferfall a5) und Linien quer über ein Loch mit Insel (t2, t6) – Kreis nie außen")]
    public void Gaps_and_web_over_hole()
    {
        // a5: Quadrat auf BEM mit Stoß an einer Ecke und zwei 0,3-mm-Spalten, Kreis halb in der Hülle
        var a5 = Line(0, 0, 100, 0, "BEM") + Line(0, 0, 0, 100, "BEM") + Line(0, 0, -10, -10, "BEM") + Line(0, 100, 99.7, 100, "BEM") + Line(100, 0.3, 100, 100, "BEM") + Circle(100, 50, 10);
        var (r, t) = Run("a5", Dxf(a5), 6);
        Assert.False(Around(r, "aussen", 100, 50, 10, 6), "a5: Kreis außen gefräst – " + t);
        // t2: Platte, Loch r 30, zwei Linien quer über das Loch (Enden im Material), Insel r 6 zwischen ihnen
        var plate = Lw(Sq(0, 0, 300, 200)) + Circle(100, 100, 30);
        var (r2, t2) = Run("t2", Dxf(plate + Line(85, 140, 85, 60) + Line(115, 140, 115, 60) + Circle(100, 100, 6)), 6);
        Assert.False(Around(r2, "aussen", 100, 100, 6, 6), "t2: Insel im Steg außen gefräst – " + t2);
        // t6: freie Platte mit Loch, zwei Linien durch Platte und Loch hindurch, Insel im Loch
        var (r6, t6) = Run("t6", Dxf(Lw(Sq(0, 0, 100, 100)) + Circle(50, 50, 30) + Line(35, 120, 35, -20) + Line(65, 120, 65, -20) + Circle(50, 50, 6)), 6);
        Assert.False(Around(r6, "aussen", 50, 50, 6, 6), "t6: Insel außen gefräst – " + t6);
        // Kontrolle: leeres Loch mit zwei Linien quer darüber (wie T-0758 diagonal) – keine Bereichsmeldung
        var (r3, t3) = Run("leeres Loch", Dxf(plate + Line(0, 95, 300, 95) + Line(0, 105, 300, 105)), 6);
        Assert.DoesNotContain("einen Bereich ein", t3);
    }
}
