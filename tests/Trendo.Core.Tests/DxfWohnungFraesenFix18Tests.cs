using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix18: Befunde der achtzehnten Prüfung – „teilweise außerhalb“ je Strecke (einzelne Linie über die Kerbe), alle Linienzüge an
/// derselben Kontur gemeinsam, Gesamtgrenze je Datei.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix18AchtzehntePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities) => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1) => G((0, "LINE"), (8, "0"), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Lw(IEnumerable<(double X, double Y)> pts, bool closed = true)
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, "0"), (90, p.Count), (70, closed ? 1 : 0)));
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

    [Fact(DisplayName = "T-0747-fix18 HOCH: einzelne Linie / offene 2-Punkt-Polylinie / 0,05 mm vor der Kante / Sehne bei x 95 / 3-Punkt-Polylinie auf dem Rand schließen die Kerbe – Kreis darin nie außen; Gravur ganz in der Platte löst nicht aus")]
    public void Chord_closing_notch()
    {
        var plate = Lw([(0, 0), (100, 0), (100, 20), (70, 20), (70, 80), (100, 80), (100, 100), (0, 100)]);
        var cases = new (string Name, string Chord, double Cx)[]
        {
            ("s1 Linie auf den Ecken", Line(100, 20, 100, 80), 85),
            ("s2 offene 2-Punkt-Polylinie", Lw([(100, 20), (100, 80)], false), 85),
            ("s4 0,05 mm vor der Kante", Line(100, 20.05, 100, 79.95), 85),
            ("s9 Sehne bei x 95", Line(95, 20, 95, 80), 82),
            ("s10 3-Punkt-Polylinie auf dem Rand", Lw([(100, 20), (100, 50), (100, 80)], false), 85),
        };
        foreach (var (name, chord, cx) in cases)
        {
            var (r, text) = CamOrNothing(Dxf(plate + chord + Circle(cx, 50, 5) + Circle(30, 50, 5)));
            log.WriteLine($"{name}: {text[..Math.Min(300, text.Length)]}");
            Assert.NotNull(r);
            Assert.False(Around(r!, "aussen", cx, 50, 5), $"{name}: Kreis in der Kerbe außen gefräst");
            Assert.True(Around(r!, "innen", 30, 50, 5), $"{name}: Loch in der Platte fehlt");
        }
        // Kontrolle: Gravurlinie ganz in der Platte von Rand zu Rand – Platte und Loch werden gefräst, keine neue Meldung
        var (g, gt) = CamOrNothing(Dxf(Lw(Sq(0, 0, 100, 60)) + Line(0, 40, 100, 40) + Circle(20, 15, 4)));
        Assert.NotNull(g); Assert.DoesNotContain("stößt mit", gt);
        Assert.Single(g!.Paths, p => p.Kind == "aussen"); Assert.True(Around(g, "innen", 20, 15, 4));
    }

    [Fact(DisplayName = "T-0747-fix18 MITTEL: Platte mit 5000 Löchern und 5000 U am Rand unter 10 s; Streifen mit vielen Verbünden unter Gesamtgrenze")]
    public void Many_u_on_one_plate_fast()
    {
        const double W = 2000;
        var sb = new StringBuilder(Lw(Sq(0, 0, W, W)));
        var m = 71; var st = W / (m + 1);
        for (var k = 0; k < 5000; k++)
        {
            double cx = st * (k % m + 1), cy = st * (k / m + 1), rr = st * 0.3;
            sb.Append(Lw(Enumerable.Range(0, 16).Select(t => (cx + rr * Math.Cos(2 * Math.PI * t / 16), cy + rr * Math.Sin(2 * Math.PI * t / 16)))));
        }
        var du = W / 5001;
        for (var k = 0; k < 5000; k++) { var x = du * (k + 1); var w = du * 0.3; sb.Append(Lw([(x - w / 2, 0), (x - w / 2, -5), (x + w / 2, -5), (x + w / 2, 0)], false)); }
        var dxf = Dxf(sb.ToString());
        var t = Min2(() => CamOrNothing(dxf, 6));
        var (r, text) = CamOrNothing(dxf, 6);
        log.WriteLine($"5000 Löcher + 5000 U: {t} ms; {text[..Math.Min(200, text.Length)]}");
        Assert.True(t < 10000, $"{t} ms");
        // T-0747-fix19: Ergebnis genau prüfen – die Platte selbst wird ausgelassen (U am Rand), die 5000 Löcher ganz in ihr werden weiter gefräst
        Assert.NotNull(r);
        Assert.Equal(5000, r!.Paths.Count(p => p.Kind == "innen"));
        Assert.DoesNotContain(r.Paths, p => p.Kind == "aussen");
        Assert.Contains("Kontur 1 ausgelassen", text); // die Platte (schon über die Regel für offene Umrisse am Rand)
    }
}
