using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix17: Befunde der siebzehnten Prüfung – Budget je Verbund statt global (Nachbarplatte bleibt), Eckpunkt-Raster statt
/// Kandidatenlisten, offener Linienzug mit zwei Enden an einer geschlossenen Kontur (U vor einer Kerbe) macht den eingeschlossenen Bereich unsicher.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix17SiebzehntePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities) => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1, string layer = "0") => G((0, "LINE"), (8, layer), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Lw(IEnumerable<(double X, double Y)> pts)
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, "0"), (90, p.Count), (70, 1)));
        foreach (var q in p) sb.Append(G((10, q.X), (20, q.Y)));
        return sb.ToString();
    }
    static (double X, double Y)[] Sq(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static CamResult Cam(string dxf, double tool = 1) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = tool, Unread = rd.Unread }); }
    static (CamResult? R, string Text) CamOrNothing(string dxf, double tool = 1)
    {
        try { var r = Cam(dxf, tool); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool Around(CamResult r, string kind, double x, double y, double rr, double tool = 1) =>
        r.Paths.Any(p => p.Kind == kind && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - (kind == "aussen" ? tool / 2 : -tool / 2)) < 0.05));
    static long Min2(Action a) { var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds; sw.Restart(); a(); return Math.Min(t, sw.ElapsedMilliseconds); }

    [Fact(DisplayName = "T-0747-fix17 HOCH: U aus Linien vor einer Kerbe (mit/ohne Stummel, Ende auf Kante statt Ecke, 0,05 mm Spalt) – Loch in der Kerbe nie außen, Loch in der Platte bleibt")]
    public void U_in_front_of_notch()
    {
        var plate = Lw([(0, 0), (100, 0), (100, 20), (70, 20), (70, 80), (100, 80), (100, 100), (0, 100)]);
        var cases = new (string Name, string U)[]
        {
            ("ohne Stummel", Line(100, 20, 150, 20) + Line(150, 20, 150, 80) + Line(150, 80, 100, 80)),
            ("mit Stummel", Line(100, 20, 150, 20) + Line(150, 20, 150, 80) + Line(150, 80, 100, 80) + Line(150, 20, 160, 10)),
            ("Enden auf der Kante", Line(85, 20, 150, 20) + Line(150, 20, 150, 80) + Line(150, 80, 85, 80)),
            ("Spalt 0,05", Line(100.05, 20, 150, 20) + Line(150, 20, 150, 80) + Line(150, 80, 100.05, 80)),
        };
        foreach (var (name, u) in cases)
        {
            var (r, text) = CamOrNothing(Dxf(plate + u + Circle(85, 50, 5) + Circle(30, 50, 5)));
            log.WriteLine($"{name}: {text[..Math.Min(400, text.Length)]}");
            if (r is null) continue;
            Assert.False(Around(r, "aussen", 85, 50, 5), $"{name}: Loch in der Kerbe außen gefräst");
            Assert.True(Around(r, "innen", 30, 50, 5), $"{name}: Loch in der Platte fehlt");
        }
        // Kontrolle: Gravur ganz in der Platte von Rand zu Rand – nichts ändert sich (Platte und Loch werden gefräst)
        var g = Cam(Dxf(Lw(Sq(0, 0, 100, 60)) + Line(0, 40, 100, 40) + Circle(20, 15, 4)));
        Assert.Single(g.Paths, p => p.Kind == "aussen"); Assert.True(Around(g, "innen", 20, 15, 4));
        // Kontrolle: Linien auf eigener Ebene (z. B. Bemaßung) bilden keinen Anschluss – die neue Regel greift nicht (die ältere Regel für offene Umrisse
        // lässt die berührte Platte weiterhin aus, das Loch darin wird gefräst)
        var (d, dt) = CamOrNothing(Dxf(plate + Line(100, 20, 150, 20, "BEM") + Line(150, 20, 150, 80, "BEM") + Line(150, 80, 100, 80, "BEM") + Circle(30, 50, 5)));
        Assert.DoesNotContain("stößt mit", dt); Assert.True(d is not null && Around(d, "innen", 30, 50, 5));
    }

    [Fact(DisplayName = "T-0747-fix17 MITTEL 1/2: 5000 dünne T-Stöße über 10 000 Quadraten, Nachbarplatte mit L-Stoß außerhalb der Hülle – Platte und Loch gefräst, unter 15 s")]
    public void Neighbour_plate_survives_many_groups()
    {
        var sb = new StringBuilder();
        for (var g = 0; g < 5000; g++) { var y = 2000 + g * 0.7; sb.Append(Line(0, y, 500, y)).Append(Line(500, y, 1000, y)).Append(Line(500, y, 500, y + 0.5)); }
        var st = 10.0;
        for (var i = 0; i < 100; i++) for (var j = 0; j < 100; j++) sb.Append(Lw(Sq(i * st, j * st, i * st + 5, j * st + 5)));
        sb.Append(Lw(Sq(3000, 0, 3100, 100))).Append(Circle(3050, 50, 10));
        sb.Append(Line(3050, 150, 3250, 150)).Append(Line(3250, 150, 3250, 50)).Append(Line(3250, 150, 3260, 160));
        var dxf = Dxf(sb.ToString());
        var t = Min2(() => CamOrNothing(dxf));
        var (r, text) = CamOrNothing(dxf);
        log.WriteLine($"{t} ms; {text[..Math.Min(300, text.Length)]}");
        Assert.NotNull(r);
        Assert.True(Around(r!, "innen", 3050, 50, 10), "Loch der Nachbarplatte fehlt");
        Assert.Contains(r!.Paths, p => p.Kind == "aussen" && p.Pts.All(q => q.X - r.ShiftX > 2990 && q.X - r.ShiftX < 3110));
        Assert.DoesNotContain("Prüfaufwand zu groß", text);
        Assert.True(t < 15000, $"{t} ms");
    }
}
