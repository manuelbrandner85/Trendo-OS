using System.Globalization;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix7: Befunde der siebten Prüfung – Rahmen einer Schraffur nur aus dem Randpfad (nicht Erhebungs- und Saatpunkt), Füllflächen-Ausnahme
/// nur bei formgleicher Randschleife (Inseln einzeln), WIPEOUT/OLE2FRAME/Proxy mit bestimmbarer Ausdehnung.</summary>
public class T0747_Fix7SiebtePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities, string blocks = "", string tables = "") =>
        "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + tables + (blocks.Length > 0 ? $"0\nSECTION\n2\nBLOCKS\n{blocks}0\nENDSEC\n" : "") + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static (double X, double Y)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static string Lw((double X, double Y)[] pts, string layer = "0") => G((0, "LWPOLYLINE"), (8, layer), (90, pts.Length), (70, 1)) + string.Concat(pts.Select(p => G((10, p.X), (20, p.Y))));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));

    /// <summary>HATCH wie AutoCAD: Erhebungspunkt (0/0), Polylinien-Randschleifen, ein Saatpunkt.</summary>
    static string Hatch(string layer, params (double X, double Y)[][] loops)
    {
        var s = G((0, "HATCH"), (8, layer), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, loops.Length));
        for (var k = 0; k < loops.Length; k++)
        {
            s += G((92, k == 0 ? 3 : 2), (72, 0), (73, 1), (93, loops[k].Length));
            foreach (var (x, y) in loops[k]) s += G((10, x), (20, y));
            s += G((97, 0));
        }
        return s + G((75, 0), (76, 1), (98, 1), (10, loops[0][0].X + 0.5), (20, loops[0][0].Y + 0.5));
    }
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }
    static (double X, double Y) Mid(CamResult r, CamPath p) => (p.Pts.Average(q => q.X) - r.ShiftX, p.Pts.Average(q => q.Y) - r.ShiftY);

    [Fact(DisplayName = "T-0747-fix7 M-1: Schraffur-Rahmen nur aus dem Randpfad – deckungsgleiche Schraffur weit vom Nullpunkt (c1) und kleine Schraffur in der Platte (c2) lassen die Löcher gefräst")]
    public void Hatch_box_from_boundary_only()
    {
        var plate = Lw(Rect(500, 400, 600, 460)) + Circle(530, 430, 5) + Circle(570, 430, 5);
        var c1 = Cam(Dxf(plate + Hatch("0", Rect(500, 400, 600, 460))));
        Assert.Equal(2, c1.Paths.Count(p => p.Kind == "innen")); Assert.Single(c1.Paths, p => p.Kind == "aussen");
        var rd = CadDxf.Parse(Dxf(plate + Hatch("0", Rect(540, 405, 560, 415))));
        var u = Assert.Single(rd.Unread); Assert.Equal((540.0, 405.0, 560.0, 415.0), (u.B.MinX, u.B.MinY, u.B.MaxX, u.B.MaxY)); // nicht bis 0/0
        var c2 = Cam(Dxf(plate + Hatch("0", Rect(540, 405, 560, 415))));
        log.WriteLine(string.Join(" | ", c2.Warnings));
        Assert.Equal(2, c2.Paths.Count(p => p.Kind == "innen"));
        Assert.Contains(c2.Warnings, w => w.Contains("enthält nicht gelesenes Element Schraffur", StringComparison.Ordinal));
        // Randpfad aus Kanten: Linie + Bogen (Halbkreis nach oben) → Rahmen bis y 60, nicht zum Nullpunkt
        var edges = G((0, "HATCH"), (8, "0"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, 1), (92, 1), (93, 2),
            (72, 1), (10, 200), (20, 50), (11, 220), (21, 50), (72, 2), (10, 210), (20, 50), (40, 10), (50, 0), (51, 180), (73, 1), (97, 0), (75, 0), (76, 1), (98, 0));
        var e = Assert.Single(CadDxf.Parse(Dxf(edges)).Unread).B;
        Assert.Equal(200, e.MinX, 6); Assert.Equal(220, e.MaxX, 6); Assert.Equal(50, e.MinY, 6); Assert.Equal(60, e.MaxY, 6);
    }

    [Fact(DisplayName = "T-0747-fix7 N-1: Füllflächen-Ausnahme nur bei formgleicher Randschleife – L-Platte mit Rechteck-Schraffur (b1), Insel ohne Umriss (b2), Raute (b3) bleiben sicher")]
    public void Fill_exception_needs_same_shape()
    {
        (double X, double Y)[] l = [(0, 0), (100, 0), (100, 30), (40, 30), (40, 60), (0, 60)];
        var b1 = Assert.Throws<InvalidOperationException>(() => Cam(Dxf(Lw(l) + Hatch("0", Rect(0, 0, 100, 60)) + Circle(70, 45, 4))));
        Assert.Contains("liegt in nicht gelesenem Element Schraffur", b1.Message, StringComparison.Ordinal);
        // Insel ohne gezeichneten Umriss: äußere Schleife deckt die Platte, Insel nicht → Loch in der Insel wird ausgelassen, die Platte gefräst
        var b2 = Cam(Dxf(Lw(Rect(0, 0, 100, 60)) + Hatch("0", Rect(0, 0, 100, 60), Rect(20, 10, 80, 50)) + Circle(50, 30, 4)));
        log.WriteLine(string.Join(" | ", b2.Warnings));
        Assert.DoesNotContain(b2.Paths, p => p.Kind == "innen"); Assert.Single(b2.Paths, p => p.Kind == "aussen");
        Assert.Contains(b2.Warnings, w => w.StartsWith("Kontur 2 ausgelassen: liegt in nicht gelesenem Element Schraffur", StringComparison.Ordinal));
        var b3 = Assert.Throws<InvalidOperationException>(() => Cam(Dxf(Lw(Rect(0, 0, 100, 60)) + Hatch("0", [(50, 0), (100, 30), (50, 60), (0, 30)]) + Circle(8, 8, 3))));
        Assert.Contains("Schraffur", b3.Message, StringComparison.Ordinal);
        // gleiche Form, andere Startecke und Richtung, Rand aus vier LINE gezeichnet → ändert nichts
        var lines = string.Concat(new[] { ((0.0, 0.0), (100.0, 0.0)), ((100.0, 0.0), (100.0, 60.0)), ((100.0, 60.0), (0.0, 60.0)), ((0.0, 60.0), (0.0, 0.0)) }
            .Select(s => G((0, "LINE"), (8, "0"), (10, s.Item1.Item1), (20, s.Item1.Item2), (30, 0), (11, s.Item2.Item1), (21, s.Item2.Item2), (31, 0))));
        var ok = Cam(Dxf(lines + Hatch("0", [(100, 60), (100, 0), (0, 0), (0, 60)]) + Circle(50, 30, 4)));
        Assert.Single(ok.Paths, p => p.Kind == "innen");
        // Schraffur auf ausgeblendeter Ebene zählt nicht
        var tables = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nSCHRAFFUR\n70\n0\n62\n-7\n0\nENDTAB\n0\nENDSEC\n";
        Assert.Empty(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Hatch("SCHRAFFUR", Rect(0, 0, 100, 60)), tables: tables)).Unread);
    }

    [Fact(DisplayName = "T-0747-fix7 N-2: WIPEOUT im Türblock, OLE2FRAME, Proxy ohne Ersatzgrafik und SHAPE neben der Platte – Platte und Löcher bleiben gefräst")]
    public void Wipeout_ole_proxy_shape()
    {
        var wipe = G((0, "WIPEOUT"), (8, "0"), (90, 0), (10, 0), (20, 0), (30, 0), (11, 9), (21, 0), (31, 0), (12, 0), (22, 9), (32, 0), (13, 1), (23, 1), (70, 7), (71, 1), (91, 2), (14, -0.5), (24, -0.5), (14, 0.5), (24, 0.5));
        var blk = G((0, "BLOCK"), (8, "0"), (2, "TUER"), (70, 0), (10, 0), (20, 0), (30, 0)) + wipe + G((0, "ENDBLK"), (8, "0"));
        var rd = CadDxf.Parse(Dxf(G((0, "INSERT"), (8, "0"), (2, "TUER"), (10, 200), (20, 0), (30, 0)), blk));
        var u = Assert.Single(rd.Unread).B; Assert.False(u.IsAll); Assert.InRange(u.MinX, 190, 200); Assert.InRange(u.MaxX, 209, 215);
        var ole = G((0, "OLE2FRAME"), (8, "0"), (70, 2), (10, 150), (20, 50), (30, 0), (11, 180), (21, 30), (31, 0), (71, 2), (72, 0), (90, 0));
        var proxy = G((0, "ACAD_PROXY_ENTITY"), (8, "0"), (90, 498), (91, 0), (92, 0));
        var shape = G((0, "SHAPE"), (8, "0"), (10, 200), (20, 0), (30, 0), (40, 1), (2, "X"));
        var all = Dxf(Lw(Rect(0, 0, 100, 60)) + Circle(30, 30, 5) + Circle(70, 30, 5) + ole + proxy + shape + G((0, "INSERT"), (8, "0"), (2, "TUER"), (10, 200), (20, 0), (30, 0)), blk);
        var o = CadDxf.Parse(all).Unread; log.WriteLine(string.Join(" | ", o.Select(x => $"{x.What} {x.B}")));
        Assert.DoesNotContain(o, x => x.B.IsAll);
        var r = Cam(all);
        Assert.Equal(2, r.Paths.Count(p => p.Kind == "innen")); Assert.All(r.Paths, p => Assert.True(Mid(r, p).X < 120));
        // Proxy mit Ersatzgrafik: Ausdehnung unbekannt
        Assert.True(Assert.Single(CadDxf.Parse(Dxf(G((0, "ACAD_PROXY_ENTITY"), (8, "0"), (90, 498), (92, 16), (310, "00FF")))).Unread).B.IsAll);
    }
}
