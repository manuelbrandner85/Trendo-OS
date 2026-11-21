using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix14: Befunde der vierzehnten Prüfung – Stoß-Gruppe samt über Spalte angeschlossener Ketten unsicher, offene POLYLINE mit
/// unvollständigem Eckpunkt überall unsicher, Stoß-Schleife beim Fräsen nicht mehr Gruppen × Konturen, exakte Knoten statt Dichteregel.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix14VierzehntePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities) => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1) => G((0, "LINE"), (8, "0"), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }
    static (CamResult? R, string Text) CamOrNothing(string dxf)
    {
        try { var r = Cam(dxf); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool MilledOutside(CamResult r, double x, double y, double rr) =>
        r.Paths.Any(p => p.Kind == "aussen" && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - 0.5) < 0.05));
    static long Timed(Action a)
    {
        var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds;
        if (t > 2000) { sw.Restart(); a(); t = Math.Min(t, sw.ElapsedMilliseconds); }
        return t;
    }

    [Fact(DisplayName = "T-0747-fix14 HOCH 1: Platte aus LINE mit zwei Stößen, Hälften über Spalte 0,00001 / 0,005 / 0,05 mm angeschlossen – kein Loch außen gefräst")]
    public void Junction_with_gap_chains()
    {
        foreach (var g in new[] { 0.00001, 0.005, 0.05 })
        {
            var sb = new StringBuilder();
            sb.Append(Line(45 - g, 60, 0, 60)).Append(Line(0, 60, 0, 0)).Append(Line(0, 0, 45 - g, 0));
            sb.Append(Line(55 + g, 60, 100, 60)).Append(Line(100, 60, 100, 0)).Append(Line(100, 0, 55 + g, 0));
            sb.Append(Line(45, 60, 50, 60)).Append(Line(50, 60, 55, 60)).Append(Line(50, 60, 50, 65));
            sb.Append(Line(45, 0, 50, 0)).Append(Line(50, 0, 55, 0)).Append(Line(50, 0, 50, -5));
            var (r, text) = CamOrNothing(Dxf(sb + Circle(50, 30, 3) + Circle(20, 30, 3) + Circle(80, 30, 3)));
            log.WriteLine($"Spalt {g}: {text}");
            if (r is not null) foreach (var x in new[] { 20.0, 50, 80 }) Assert.False(MilledOutside(r, x, 30, 3), $"Spalt {g}, Loch bei {x}");
        }
    }

    [Fact(DisplayName = "T-0747-fix14 HOCH 2: offene POLYLINE mit Eckpunkt ohne 20 oder mit doppeltem 10 – überall unsicher, Loch nie außen gefräst")]
    public void Open_polyline_incomplete_vertex()
    {
        static string Poly(string v2) => G((0, "POLYLINE"), (8, "0"), (66, 1), (10, 0), (20, 0), (30, 0), (70, 0))
            + G((0, "VERTEX"), (8, "0"), (10, 0), (20, 0), (30, 0)) + G((0, "VERTEX"), (8, "0")) + v2 + G((0, "VERTEX"), (8, "0"), (10, 100), (20, 60), (30, 0))
            + G((0, "VERTEX"), (8, "0"), (10, 0), (20, 60), (30, 0)) + G((0, "SEQEND"), (8, "0"));
        foreach (var v2 in new[] { G((10, 100), (30, 0)), G((10, 100), (20, 0), (10, 100), (20, 0)) })
        {
            var dxf = Dxf(Poly(v2) + Line(0, 60, 0, 0) + Circle(50, 30, 3));
            Assert.Contains(CadDxf.Parse(dxf).Unread, u => u.B.IsAll);
            var (r, text) = CamOrNothing(dxf); log.WriteLine(text);
            if (r is not null) Assert.False(MilledOutside(r, 50, 30, 3));
        }
        // Kontrolle: vollständiger Eckpunkt – Umriss schließt sich, Loch wird als Loch gefräst
        var ok = Cam(Dxf(Poly(G((10, 100), (20, 0), (30, 0))) + Line(0, 60, 0, 0) + Circle(50, 30, 3)));
        Assert.Contains(ok.Paths, p => p.Kind == "innen");
    }

    [Fact(DisplayName = "T-0747-fix14 MITTEL 3: 50 000 Stoßgruppen + 1 Kreis – Fräsbahnen unter 10 s")]
    public void Many_junction_groups_fast()
    {
        var sb = new StringBuilder();
        for (var g = 0; g < 50_000; g++)
        {
            double x = g % 224 * 10.0, y = g / 224 * 10.0;
            sb.Append(Line(x + 2, y + 2, x + 4, y + 2)).Append(Line(x + 2, y + 2, x + 1, y + 4)).Append(Line(x + 2, y + 2, x + 1, y + 0.5));
        }
        var dxf = Dxf(sb + Circle(-1000, 0, 5));
        var t = Timed(() => Cam(dxf));
        var r = Cam(dxf); log.WriteLine($"50 000 Stöße + 1 Kreis: {t} ms; {string.Join(" | ", r.Warnings.Take(3))}");
        Assert.True(t < 10000, $"{t} ms");
        Assert.Single(r.Paths, p => p.Kind == "aussen");
        Assert.True(r.Warnings.Count < 20, $"{r.Warnings.Count} Meldungen"); // Sammelmeldung statt einer Zeile je Linie
    }

    static string Polygon(double cx, double cy, double rr, int n)
    {
        var sb = new StringBuilder();
        for (var k = 0; k < n; k++)
        {
            double a0 = 2 * Math.PI * k / n, a1 = 2 * Math.PI * (k + 1) / n;
            sb.Append(Line(cx + rr * Math.Cos(a0), cy + rr * Math.Sin(a0), cx + rr * Math.Cos(a1), cy + rr * Math.Sin(a1)));
        }
        return sb.ToString();
    }

    [Fact(DisplayName = "T-0747-fix14 MITTEL 4: fein zerlegte Kreise aus LINE (Loch r 1 aus 1500, Kreis r 5 aus 6000) – 2 Bahnen, keine Stoß-Meldung")]
    public void Fine_line_circles()
    {
        foreach (var (rr, n) in new[] { (1.0, 1500), (5.0, 6000) })
        {
            var r = Cam(Dxf(G((0, "LWPOLYLINE"), (8, "0"), (90, 4), (70, 1), (10, -20), (20, -20), (10, 20), (20, -20), (10, 20), (20, 20), (10, -20), (20, 20)) + Polygon(0, 0, rr, n)));
            log.WriteLine($"r {rr}, {n} Linien: {r.Paths.Count} Bahnen; {string.Join(" | ", r.Warnings)}");
            Assert.Equal(2, r.Paths.Count);
            Assert.DoesNotContain(r.Warnings, w => w.Contains("Linienverbund"));
        }
    }
}
