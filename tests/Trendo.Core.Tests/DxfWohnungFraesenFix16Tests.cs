using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix16: Befunde der sechzehnten Prüfung – lineares Verschmelzen der Stoß-Komponenten, schneller Schutzbereich, Platte am
/// Hüllenrand wieder gefräst, enge Zeitgrenzen.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix16SechzehntePruefung(ITestOutputHelper log)
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
    static string Hatch(IEnumerable<(double X, double Y)> loop)
    {
        var q = loop.ToList(); var sb = new StringBuilder(G((0, "HATCH"), (8, "0"), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, 1), (92, 3), (72, 0), (73, 1), (93, q.Count)));
        foreach (var v in q) sb.Append(G((10, v.X), (20, v.Y)));
        return sb.Append(G((97, 0), (75, 0), (76, 1), (98, 0))).ToString();
    }
    static CamResult Cam(string dxf, double tool = 1) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = tool, Unread = rd.Unread }); }
    static (CamResult? R, string Text) CamOrNothing(string dxf, double tool = 1)
    {
        try { var r = Cam(dxf, tool); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool Around(CamResult r, string kind, double x, double y, double rr, double tool = 1) =>
        r.Paths.Any(p => p.Kind == kind && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - (kind == "aussen" ? tool / 2 : -tool / 2)) < 0.05));
    /// <summary>Kleinste von zwei Messungen (lastrobust).</summary>
    static long Min2(Action a) { var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds; sw.Restart(); a(); return Math.Min(t, sw.ElapsedMilliseconds); }
    /// <summary>T-0747-fix18: unter Fremdlast lastrobuster – bis zu fünf Messungen mit Pause, die kleinste zählt (Grenze unverändert).</summary>
    static long Min3(Action a)
    {
        var best = long.MaxValue;
        for (var k = 0; k < 5; k++) { if (k > 0) { if (best < 10000) break; GC.Collect(); Thread.Sleep(1000); } var sw = Stopwatch.StartNew(); a(); best = Math.Min(best, sw.ElapsedMilliseconds); }
        return best;
    }

    [Fact(DisplayName = "T-0747-fix16 MITTEL 3: Gravur-T mit Enden auf dem Plattenrand (auch eigene Ebene) – Platte und Loch werden gefräst; teilweise in der Hülle bleibt ausgelassen")]
    public void Plate_touching_hull_is_milled()
    {
        var plate = Lw(Sq(0, 0, 100, 60));
        foreach (var layer in new[] { "0", "GRAV" })
        {
            var r = Cam(Dxf(plate + Line(0, 30, 50, 30, layer) + Line(50, 30, 100, 30, layer) + Line(50, 30, 50, 60, layer) + Circle(20, 15, 4)));
            log.WriteLine($"Ebene {layer}: {string.Join(" | ", r.Warnings)}");
            Assert.Single(r.Paths, p => p.Kind == "aussen"); // die Platte
            Assert.True(Around(r, "innen", 20, 15, 4));
        }
        // Gegenbeispiel: ein Quadrat kreuzt den Hüllenrand und liegt teilweise darin → ausgelassen, nie außen gefräst
        var (r2, text) = CamOrNothing(Dxf(plate + Line(10, 30, 50, 30) + Line(50, 30, 90, 30) + Line(50, 30, 50, 55) + Lw(Sq(45, 50, 55, 58)) + Circle(20, 15, 4)));
        log.WriteLine(text);
        Assert.NotNull(r2);
        Assert.Contains("teilweise darin", text);
        Assert.DoesNotContain(r2!.Paths, p => p.Pts.All(q => q.X - r2.ShiftX > 44 && q.X - r2.ShiftX < 56 && q.Y - r2.ShiftY > 49 && q.Y - r2.ShiftY < 59)); // das Quadrat
        Assert.True(Around(r2, "innen", 20, 15, 4));
    }

    static string Star(int n) => string.Concat(Enumerable.Range(0, n).Select(k => Line(0, 0, 100 * Math.Cos(2 * Math.PI * k / n), 100 * Math.Sin(2 * Math.PI * k / n))));

    [Fact(DisplayName = "T-0747-fix16 MITTEL 1/2: Stern 50 000, Bürste 50 000, Rechteck 8000-fach deckungsgleich, 2000 X-Stöße über 20 000 Quadraten – je unter 10 s; Stoßkette 20 000 + Schraffur unter 4 s")]
    public void Junction_cases_fast()
    {
        var plate = Lw(Sq(300, 0, 400, 100)) + Circle(350, 50, 10);
        var star = Dxf(Star(50_000) + plate);
        var sb = new StringBuilder(Line(0, 0, 4, 0) + Line(0, 0, 0, 5) + Line(0, 0, 0, -5) + Line(4.05, 0, 10, 0));
        for (var k = 0; k < 50_000; k++) { var a = 2 * Math.PI * k / 50_000; double x0 = 10.02 + 0.05 * (k * 7919 % 1000) / 1000.0, y0 = 0.05 * (k * 104729 % 1000) / 1000.0; sb.Append(Line(x0, y0, x0 + 50 * Math.Cos(a), y0 + 50 * Math.Sin(a))); }
        var brush = Dxf(sb + plate);
        var rect = Line(0, 0, 100, 0) + Line(100, 0, 100, 60) + Line(100, 60, 0, 60) + Line(0, 60, 0, 0);
        var dup = Dxf(string.Concat(Enumerable.Repeat(rect, 8000)) + Circle(50, 30, 5));
        var rnd = new Random(16); sb.Clear();
        for (var g = 0; g < 2000; g++) { double cx = rnd.NextDouble() * 1000, cy = rnd.NextDouble() * 1000; sb.Append(Line(cx, cy, cx + 700, cy)).Append(Line(cx, cy, cx - 700, cy)).Append(Line(cx, cy, cx, cy + 700)).Append(Line(cx, cy, cx, cy - 700)); }
        var st = 1000.0 / 142;
        for (var i = 0; i < 142; i++) for (var j = 0; j < 142; j++) sb.Append(Lw(Sq(i * st, j * st, i * st + st / 2, j * st + st / 2)));
        var x4 = Dxf(sb + Lw(Sq(3000, 0, 3100, 100)) + Circle(3050, 50, 10));
        foreach (var (name, dxf) in new[] { ("Stern 50 000", star), ("Bürste 50 000", brush), ("Rechteck 8000-fach", dup), ("2000 X-Stöße", x4) })
        {
            // Fräser Ø 6 wie in der Prüfung (mit Ø 1 dominiert das vorbestehende Hindernis-Raster der Fräsbahn-Prüfung – nicht Teil dieser Runde)
            // T-0760: Grenze bleibt 10 s – unter Fremdlast um den gemessenen Lastfaktor verlängert (Lastmessung)
            var t = Lastmessung.Best(3, () => CamOrNothing(dxf, 6), parallel: true);
            var (r, _) = CamOrNothing(dxf, 6);
            log.WriteLine($"{name}: {r?.Paths.Count ?? 0} Bahnen");
            Lastmessung.Below(log, name, t, 10000);
            if (name != "Rechteck 8000-fach") Assert.True(r is not null && Around(r, "innen", name == "2000 X-Stöße" ? 3050 : 350, 50, 10, 6), name); // Platte daneben samt Loch bleibt
        }
        // Stoßkette 20 000 mit einer Schraffur: Lesen unter 4 s (kleinste von zwei Messungen)
        var kette = new StringBuilder();
        for (var k = 0; k < 20_000; k++) { var x = 10.0 * k; kette.Append(Line(x, 0, x + 4, 0)).Append(Line(x, 0, x, 5)).Append(Line(x, 0, x, -5)).Append(Line(x + 4.05, 0, x + 9.95, 0)); }
        (double, double)[] sq = [(0, -100), (20, -100), (20, -80), (0, -80)];
        var u6 = Dxf(kette + Lw(sq) + Hatch(sq));
        var t6 = Lastmessung.Best(2, () => CadDxf.Parse(u6), parallel: false);
        Lastmessung.Below(log, "Stoßkette 20 000 + Schraffur", t6, 4000);
    }
}
