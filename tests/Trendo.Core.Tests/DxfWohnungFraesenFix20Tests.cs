using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix20: Befunde der zwanzigsten Prüfung – genaue Abstandsprüfung der Sehne (Kapseln um die nahen Randstrecken statt Abtastung),
/// genau tol, Rauschen am Rand löst nicht aus, schnell bei Linien auf der Kante.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix20ZwanzigstePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities) => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1) => G((0, "LINE"), (8, "0"), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Lw(IEnumerable<(double X, double Y)> pts)
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, "0"), (90, p.Count), (70, 1)));
        foreach (var q in p) sb.Append(G((10, q.X), (20, q.Y)));
        return sb.ToString();
    }
    static (double X, double Y)[] Sq(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static (CamResult? R, string Text) CamOrNothing(string dxf)
    {
        try { var rd = CadDxf.Parse(dxf); var r = CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool Around(CamResult r, string kind, double x, double y, double rr) =>
        r.Paths.Any(p => p.Kind == kind && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - (kind == "aussen" ? 0.5 : -0.5)) < 0.05));
    static long Min2(Func<object?> a) { var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds; sw.Restart(); a(); return Math.Min(t, sw.ElapsedMilliseconds); }

    [Fact(DisplayName = "T-0747-fix20: Linie 0,095 mm über der Oberkante (genau tol, kein Band 0,0875–0,1) und Rauschen ±1e-7 an der Unterkante – Platte und Loch werden gefräst")]
    public void Exact_tolerance_and_noise()
    {
        var (r, text) = CamOrNothing(Dxf(Lw(Sq(0, 0, 100, 100)) + Line(0, 100.095, 100, 100.095) + Circle(30, 50, 5)));
        log.WriteLine(text[..Math.Min(200, text.Length)]);
        Assert.DoesNotContain("stößt mit", text);
        Assert.True(r is not null && Around(r, "innen", 30, 50, 5));
        var pts = new List<(double X, double Y)> { (0, 300) };
        for (var k = 0; k <= 5000; k++) pts.Add((1000.0 * k / 5000, k % 2 == 0 ? 1e-7 : -1e-7));
        pts.Add((1000, 300));
        var (n, nt) = CamOrNothing(Dxf(Lw(pts) + Line(0, 0, 1000, 0) + Circle(100, 150, 10)));
        Assert.DoesNotContain("stößt mit", nt); Assert.DoesNotContain("Prüfaufwand", nt);
        Assert.True(n is not null && Around(n, "innen", 100, 150, 10));
    }

    [Fact(DisplayName = "T-0747-fix20: 3000 Platten mit Linie auf der Oberkante (unter 12 s; 70d4c6c74 ≈ 9 s, 137191e37 ≈ 17 s), 1000 mit Linie auf der Rechtskante und fein geteilte Kante (200 000 Punkte, große Ausdehnung) – Platten gefräst")]
    public void Edge_lines_fast()
    {
        foreach (var (kind, count, limit) in new[] { ("oben", 3000, 12000L), ("rechts", 1000, 6000L) })
        {
            var sb = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                double x = i % 40 * 320, y = i / 40 * 320;
                sb.Append(Lw(Sq(x, y, x + 300, y + 300))).Append(kind == "oben" ? Line(x, y + 300, x + 300, y + 300) : Line(x + 300, y, x + 300, y + 300)).Append(Circle(x + 150, y + 150, 10));
            }
            var dxf = Dxf(sb.ToString());
            var t = Lastmessung.Best(2, () => CamOrNothing(dxf), parallel: true); // T-0760: Grenze unverändert, unter Fremdlast × Lastfaktor
            var (r, text) = CamOrNothing(dxf);
            Lastmessung.Below(log, kind, t, limit);
            Assert.NotNull(r); Assert.Equal(count, r!.Paths.Count(p => p.Kind == "innen")); Assert.Equal(count, r.Paths.Count(p => p.Kind == "aussen"));
            Assert.DoesNotContain("stößt mit", text);
        }
        var edge = new List<(double X, double Y)> { (0, 0), (20000, 0) };
        for (var k = 200000; k >= 0; k--) edge.Add((20000.0 * k / 200000, 1000));
        var fine = Dxf(Lw(edge) + Line(0, 1000, 20000, 1000) + Circle(5000, 500, 20) + Lw(Sq(24000, 0, 24010, 10)));
        var tf = Lastmessung.Best(2, () => CamOrNothing(fine), parallel: true);
        var (f, ft) = CamOrNothing(fine);
        Lastmessung.Below(log, "feine Kante", tf, 6000);
        Assert.DoesNotContain("stößt mit", ft);
        Assert.True(f is not null && Around(f, "innen", 5000, 500, 20));
    }
}
