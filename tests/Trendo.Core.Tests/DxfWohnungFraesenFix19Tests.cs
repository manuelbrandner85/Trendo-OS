using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix19: Befunde der neunzehnten Prüfung – Teilstück der Sehne in Schritten abgetastet (Finger/Zunge knapp davor), zu viele Schnitte
/// → sichere Folge, Gesamtgrenze geprüft und testbar.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix19NeunzehntePruefung(ITestOutputHelper log)
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
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }
    static (CamResult? R, string Text) CamOrNothing(string dxf)
    {
        try { var r = Cam(dxf); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool Around(CamResult r, string kind, double x, double y, double rr) =>
        r.Paths.Any(p => p.Kind == kind && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - (kind == "aussen" ? 0.5 : -0.5)) < 0.05));

    [Fact(DisplayName = "T-0747-fix19 HOCH: Zunge 0,05 / 0,09 knapp vor der Sehne, Kammfinger 0,02 unter der Abschlusslinie – Kreise in der Lücke nie außen; Zunge 0,11 und 0 weiter ausgelöst")]
    public void Tongue_and_finger_near_chord()
    {
        foreach (var g in new[] { 0.05, 0.09, 0.11, 0.0 })
        {
            var gx = 100 - g;
            var plate = Lw([(0, 0), (100, 0), (100, 20), (70, 20), (70, 49.5), (gx, 49.5), (gx, 50.5), (70, 50.5), (70, 80), (100, 80), (100, 100), (0, 100)]);
            var (r, text) = CamOrNothing(Dxf(plate + Line(100, 20, 100, 80) + Circle(85, 35, 5) + Circle(85, 65, 5) + Circle(30, 50, 5)));
            log.WriteLine($"Zunge {g}: {text[..Math.Min(200, text.Length)]}");
            Assert.NotNull(r);
            Assert.False(Around(r!, "aussen", 85, 35, 5) || Around(r!, "aussen", 85, 65, 5), $"Zunge {g}: Kreis außen");
            Assert.True(Around(r!, "innen", 30, 50, 5), $"Zunge {g}: Loch in der Platte fehlt");
            Assert.Contains("stößt mit", text);
        }
        var comb = Lw([(0, 0), (50, 0), (50, 100), (40, 100), (40, 60), (30, 60), (30, 99.98), (20, 99.98), (20, 60), (10, 60), (10, 100), (0, 100)]);
        var (k, kt) = CamOrNothing(Dxf(comb + Line(0, 100, 50, 100) + Circle(15, 80, 3) + Circle(35, 80, 3) + Circle(25, 30, 5)));
        log.WriteLine($"Kamm: {kt[..Math.Min(200, kt.Length)]}");
        Assert.NotNull(k);
        Assert.False(Around(k!, "aussen", 15, 80, 3) || Around(k!, "aussen", 35, 80, 3));
        Assert.True(Around(k!, "innen", 25, 30, 5));
        // keine neue Auslösung: Linie 0,05 mm entlang der Kante, Mittellinie von Rand zu Rand
        var plate2 = Lw([(0, 0), (100, 0), (100, 100), (0, 100)]);
        foreach (var (name, l) in new[] { ("Kante 0,05", Line(0, 0.05, 100, 0.05)), ("Mittellinie", Line(0, 50, 100, 50)) })
        {
            var (r2, t2) = CamOrNothing(Dxf(plate2 + l + Circle(25, 25, 4)));
            Assert.DoesNotContain("stößt mit", t2);
            Assert.True(r2 is not null && Around(r2, "innen", 25, 25, 4), name);
        }
    }

    [Fact(DisplayName = "T-0747-fix19 MITTEL: Sehne mit mehr als 4096 Schnitten (Zickzack am Rand) – sichere Folge, Kreis in der Kerbe nie außen")]
    public void Many_crossings_safe()
    {
        var zz = new List<(double X, double Y)> { (0, 0), (100, 0) };
        for (var k = 0; k < 2500; k++) { var y = 10.0 * k / 2500; zz.Add((100.01, y + 0.001)); zz.Add((99.99, y + 0.003)); }
        zz.AddRange([(100, 10), (100, 70), (70, 70), (70, 95), (100, 95), (100, 100), (0, 100)]);
        var (r, text) = CamOrNothing(Dxf(Lw(zz) + Line(100, 0, 100, 100) + Circle(85, 82, 5) + Circle(30, 50, 5)));
        log.WriteLine(text[..Math.Min(200, text.Length)]);
        Assert.True(r is null || !Around(r, "aussen", 85, 82, 5));
        Assert.Contains("stößt mit", text);
    }

    [Fact(DisplayName = "T-0747-fix19 niedrig: Gesamtgrenze klein gestellt – Grund je Kandidat, bei Erschöpfung sichere Folge mit Meldung „Prüfaufwand zu groß“")]
    public void Small_file_budget_safe()
    {
        var plate = Lw([(0, 0), (100, 0), (100, 20), (70, 20), (70, 80), (100, 80), (100, 100), (0, 100)]);
        var dxf = Dxf(plate + Line(100, 20, 100, 80) + Circle(85, 50, 5) + Circle(30, 50, 5) + Lw([(200, 0), (300, 0), (300, 100), (200, 100)]) + Circle(250, 50, 5));
        try
        {
            CncCam.ProtectFileBudget = 50; // sehr klein: die Linienzug-Prüfung kommt nicht weit
            var (r, text) = CamOrNothing(dxf);
            log.WriteLine(text[..Math.Min(400, text.Length)]);
            Assert.True(r is null || !Around(r, "aussen", 85, 50, 5), "Kreis in der Kerbe außen gefräst");
            Assert.True(r is not null && Around(r, "innen", 250, 50, 5), "Platte daneben samt Loch fehlt"); // nicht im Rahmen – bleibt
            Assert.Contains("Prüfaufwand", text); // T-0747-fix20: bei erschöpfter Grenze die zutreffende Meldung (nicht „schließt einen Bereich ein“)
        }
        finally { CncCam.ProtectFileBudget = null; }
    }
}
