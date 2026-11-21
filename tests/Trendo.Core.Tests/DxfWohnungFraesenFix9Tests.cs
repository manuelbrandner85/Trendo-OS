using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix9: Befunde der neunten Prüfung – HATCH als strenge Grammatik (nichts hinter dem Randabschnitt, Kennungen als Text),
/// ausgeblendete Schraffur nur bei echter Enthaltenseinsprüfung, Formvergleich schnell und abbrechbar, Toleranz mit Zerlegungsfehler, fallende Knoten.</summary>
public class T0747_Fix9NeuntePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities, string tables = "") => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + tables + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    const string AusLayer = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nAUS\n70\n0\n62\n-7\n0\nENDTAB\n0\nENDSEC\n";
    static (double X, double Y)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static string Lw((double X, double Y)[] pts) => G((0, "LWPOLYLINE"), (8, "0"), (90, pts.Length), (70, 1)) + string.Concat(pts.Select(p => G((10, p.X), (20, p.Y))));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Line(double x0, double y0, double x1, double y1) => G((72, 1), (10, x0), (20, y0), (11, x1), (21, y1));
    static string RectLoop(double x0, double y0, double x1, double y1, int flags = 1, string src = "97\n0\n") =>
        G((92, flags), (93, 4)) + Line(x0, y0, x1, y0) + Line(x1, y0, x1, y1) + Line(x1, y1, x0, y1) + Line(x0, y1, x0, y0) + src;
    static string Head(string layer = "0") => G((0, "HATCH"), (8, layer), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0));
    static string Tail => G((75, 0), (76, 1), (98, 1), (10, 1), (20, 1));
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }

    [Fact(DisplayName = "T-0747-fix9 H-1/H-3: HATCH als strenge Grammatik – Randdaten nach 75/76, zweites 91, Randgruppe im Vorspann → überall unsicher; 330-Kennungen hexadezimal sind Text")]
    public void Hatch_grammar()
    {
        var big = RectLoop(0, 0, 100, 60);
        var bad = new (string Name, string Hatch)[]
        {
            ("91 = 0, Schleife nach 75/76", Head() + G((91, 0), (75, 0), (76, 1)) + big + G((98, 0))),
            ("91 = 1, zweite Schleife nach 75", Head() + G((91, 1)) + RectLoop(300, 300, 310, 310) + G((75, 0), (76, 1)) + big + G((98, 0))),
            ("zweites 91 nach den Saatpunkten", Head() + G((91, 1)) + RectLoop(300, 300, 310, 310) + Tail + G((91, 1)) + big),
            ("Randgruppe 93 vor 91", Head() + G((93, 4), (91, 1)) + big + Tail),
            ("330 ohne 97", Head() + G((91, 1)) + RectLoop(0, 0, 100, 60, 1, "") + G((330, "2A5")) + Tail),
            ("97 = 2, nur eine 330", Head() + G((91, 1)) + RectLoop(0, 0, 100, 60, 1, G((97, 2), (330, "2A5"))) + Tail),
        };
        foreach (var (name, h) in bad)
        {
            var u = CadDxf.Parse(Dxf(h + Circle(50, 30, 5))).Unread;
            log.WriteLine($"{name}: {string.Join(" | ", u.Select(x => $"{x.What} {x.B}"))}");
            Assert.True(u.Any(x => x.B.IsAll), name);
            Assert.Throws<InvalidOperationException>(() => Cam(Dxf(h + Circle(50, 30, 5)))); // Kreis nicht außen gefräst
        }
        // assoziative AutoCAD-Schraffur: 97 1 + 330 mit hexadezimaler Kennung → gelesen und formgleich mit der Platte
        foreach (var hex in new[] { "2A5", "FF", "1B0" })
        {
            var assoc = Head() + G((91, 1)) + RectLoop(0, 0, 100, 60, 5, G((97, 1), (330, hex))) + Tail + G((1001, "ACAD"), (1000, "x"));
            var rd = CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Circle(30, 30, 5) + assoc));
            Assert.Empty(rd.Unread); Assert.Equal(1, rd.Redundant["HATCH"]);
        }
    }

    [Fact(DisplayName = "T-0747-fix9 H-2: ausgeblendete Schraffur über einer Aussparung (U-Platte) macht den Kreis darin unsicher; ganz in der Platte über den Löchern stört sie nicht und wird gemeldet")]
    public void Hidden_fill_real_containment()
    {
        (double X, double Y)[] u = [(0, 0), (100, 0), (100, 60), (60, 60), (60, 20), (40, 20), (40, 60), (0, 60)];
        var hatch = Head("AUS") + G((91, 1)) + RectLoop(10, 10, 90, 50) + Tail;
        var ru = Cam(Dxf(Lw(u) + hatch + Circle(50, 40, 5), AusLayer));
        log.WriteLine(string.Join(" | ", ru.Warnings));
        Assert.Contains(ru.Warnings, w => w.StartsWith("Kontur 2 ausgelassen: liegt in nicht gelesenem Element Schraffur", StringComparison.Ordinal));
        Assert.Single(ru.Paths); Assert.Equal("aussen", ru.Paths[0].Kind); // nur die U-Platte, der Kreis in der Aussparung nicht als Teil
        var inside = Dxf(Lw(Rect(0, 0, 100, 60)) + hatch + Circle(30, 30, 5) + Circle(70, 30, 5), AusLayer);
        var rd = CadDxf.Parse(inside);
        Assert.Empty(rd.Unread); Assert.Empty(rd.Redundant); // nur enthalten, nicht formgleich
        Assert.Equal(1, rd.HiddenFills["Schraffur auf Ebene AUS"]);
        Assert.Equal(2, Cam(inside).Paths.Count(p => p.Kind == "innen"));
    }

    [Fact(DisplayName = "T-0747-fix9 H-4: 8000 Schraffur-Schleifen und 8000 Kreise lesen in Sekunden, Riesenkreis r 300 000 mit gleicher Schraffur, Abbrechen beim Lesen wirkt")]
    public void Fill_matching_fast_and_cancellable()
    {
        static string Many(int n)
        {
            var side = 6 * ((int)Math.Sqrt(n) + 1); var sb = new StringBuilder(Lw(Rect(0, 0, side, side)));
            for (var k = 0; k < n; k++) sb.Append(Circle(3 + 6 * (k / ((int)Math.Sqrt(n) + 1)), 3 + 6 * (k % ((int)Math.Sqrt(n) + 1)), 1.5));
            sb.Append(Head()).Append(G((91, n)));
            for (var q = 0; q < n; q++) { double x0 = side + 100 + q % 1000 * 3, y0 = q / 1000 * 3; sb.Append(RectLoop(x0, y0, x0 + 1, y0 + 1)); }
            // und je Kreis eine formgleiche Inselschleife (8000 echte Paare)
            return sb.Append(G((75, 0), (76, 1), (98, 0))).ToString();
        }
        static string Pairs(int n)
        {
            var sb = new StringBuilder(); var m = (int)Math.Sqrt(n) + 1;
            for (var k = 0; k < n; k++) sb.Append(Lw(Rect(10 * (k / m), 10 * (k % m), 10 * (k / m) + 5, 10 * (k % m) + 5)));
            sb.Append(Head()).Append(G((91, n)));
            for (var k = 0; k < n; k++) sb.Append(RectLoop(10 * (k / m), 10 * (k % m), 10 * (k / m) + 5, 10 * (k % m) + 5));
            return sb.Append(G((75, 0), (76, 1), (98, 0))).ToString();
        }
        var many = Dxf(Many(8000)); var pairs = Dxf(Pairs(8000));
        var giant = Dxf(Circle(0, 0, 3e5) + Head() + G((91, 1), (92, 3), (72, 1), (73, 1), (93, 2), (10, -3e5), (20, 0), (42, 1), (10, 3e5), (20, 0), (42, 1), (97, 0), (75, 0), (76, 1), (98, 0)));
        Assert.Equal(8000, CadDxf.Parse(many).Unread.Count);
        var rp = CadDxf.Parse(pairs); Assert.Empty(rp.Unread); Assert.Equal(1, rp.Redundant["HATCH"]);
        Assert.Empty(CadDxf.Parse(giant).Unread);
        // T-0747-fix10: Grenze 10 s fängt einen Rückschritt (einzeln je unter 1 s); unter Fremdlast einmal wiederholt, die schnellere Messung zählt
        var t1 = Timed(() => CadDxf.Parse(many)); var t2 = Timed(() => CadDxf.Parse(pairs)); var t3 = Timed(() => CadDxf.Parse(giant));
        log.WriteLine($"8000 Schleifen + 8000 Kreise: {t1} ms; 8000 formgleiche Paare: {t2} ms; Riesenkreis r 300 000: {t3} ms");
        Assert.True(t1 < 10_000 && t2 < 10_000 && t3 < 10_000, $"{t1} / {t2} / {t3}");
        var tc = CancelTime(pairs);
        log.WriteLine($"Abbrechen: {tc} ms bis zur Ausnahme");
        Assert.True(tc < 10_000, $"{tc}");
    }

    /// <summary>T-0747-fix10: Laufzeit in ms; liegt sie über 2 s (Fremdlast?), wird einmal wiederholt und die schnellere Messung genommen.</summary>
    static long Timed(Action a)
    {
        var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds;
        if (t > 2000) { sw.Restart(); a(); t = Math.Min(t, sw.ElapsedMilliseconds); }
        return t;
    }

    /// <summary>T-0747-fix10: Abbrechen beim Lesen lastrobust – ein schon abgebrochener Token wirft sofort; dann Abbruch nach einem Drittel der gemessenen
    /// Lesezeit (mindestens 1 ms): Zeit bis zur Abbruch-Ausnahme. Ist das Lesen schneller fertig als der Abbruch, wird mit 1 ms wiederholt; über 3 s einmal wiederholt.</summary>
    static long CancelTime(string dxf)
    {
        using (var pre = new CancellationTokenSource()) { pre.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => CadDxf.Parse(dxf, pre.Token)); }
        var normal = Stopwatch.StartNew(); CadDxf.Parse(dxf); var tn = normal.ElapsedMilliseconds;
        long? Once(int delay)
        {
            using var cts = new CancellationTokenSource(); cts.CancelAfter(delay); var sw = Stopwatch.StartNew();
            try { CadDxf.Parse(dxf, cts.Token); return null; } catch (OperationCanceledException) { return sw.ElapsedMilliseconds; }
        }
        var t = Once((int)Math.Max(1, tn / 3)) ?? Once(1) ?? throw new Xunit.Sdk.XunitException("Abbruch wirkte nicht");
        return t > 3000 ? Math.Min(t, Once(1) ?? t) : t;
    }

    [Fact(DisplayName = "T-0747-fix9 N-2/N-3/N-4: Toleranz je Achse 0,01 mm inkl. Zerlegungsfehler (Kreis r + 0,0099 gleich, r + 0,011 nicht); fallende Knoten keine Form; 2-Punkt-Rand mit gleichen Punkten unbekannt")]
    public void Tolerance_knots_degenerate()
    {
        static string Ring(double r) => Head() + G((91, 1), (92, 3), (72, 1), (73, 1), (93, 2), (10, 50 - r), (20, 50), (42, 1), (10, 50 + r), (20, 50), (42, 1), (97, 0)) + Tail;
        Assert.Empty(CadDxf.Parse(Dxf(Circle(50, 50, 30) + Ring(30.0099))).Unread);
        Assert.Single(CadDxf.Parse(Dxf(Circle(50, 50, 30) + Ring(30.011))).Unread);
        // Grad-1-Spline als rechte Kante, Knoten fallend → keine Form → nicht formgleich
        string Edges(params double[] knots) => Head() + G((91, 1), (92, 1), (93, 4)) + Line(0, 0, 100, 0)
            + G((72, 4), (94, 1), (73, 0), (74, 0), (95, knots.Length), (96, 3)) + string.Concat(knots.Select(k => G((40, k)))) + G((10, 100), (20, 0), (10, 100), (20, 30), (10, 100), (20, 60), (97, 0))
            + Line(100, 60, 0, 60) + Line(0, 60, 0, 0) + G((97, 0)) + Tail;
        Assert.Empty(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Edges(0, 0, 1, 2, 2))).Unread);
        Assert.Single(CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Edges(0, 0, 2, 1, 1))).Unread);
        // 2 gleiche Punkte mit Ausbuchtung: Form nicht bestimmbar → überall unsicher (nicht ein Punkt-Rahmen)
        var same = Head() + G((91, 1), (92, 3), (72, 1), (73, 1), (93, 2), (10, 50), (20, 30), (42, 1), (10, 50), (20, 30), (42, 1), (97, 0)) + Tail;
        Assert.True(Assert.Single(CadDxf.Parse(Dxf(same)).Unread).B.IsAll);
    }
    [Fact(DisplayName = "T-0747-fix9 H-4 (Ergänzung): Kreis r 100 000 + Schraffur-Bogen 0…360, 100 fast gleiche Schraffuren gegen 100 gleiche Züge mit 1 000 Punkten – Sekunden, abbrechbar")]
    public void Fill_matching_worst_cases()
    {
        var circ = Circle(0, 0, 1e5) + Head() + G((91, 1), (92, 1), (93, 1), (72, 2), (10, 0), (20, 0), (40, 1e5), (50, 0), (51, 360), (73, 1), (97, 0)) + Tail;
        var sw = Stopwatch.StartNew(); Assert.Empty(CadDxf.Parse(Dxf(circ)).Unread); var t1 = sw.ElapsedMilliseconds;
        var ring = Enumerable.Range(0, 1000).Select(k => (X: 50 * Math.Cos(2 * Math.PI * k / 1000), Y: 50 * Math.Sin(2 * Math.PI * k / 1000))).ToArray();
        var sb = new StringBuilder();
        for (var k = 0; k < 100; k++) sb.Append(Lw(ring));
        sb.Append(Head()).Append(G((91, 100)));
        for (var k = 0; k < 100; k++)
        {
            sb.Append(G((92, 3), (72, 0), (73, 1), (93, 1000)));
            for (var q = 0; q < 1000; q++) { var f = q == 999 ? 0.98 : 1; sb.Append(G((10, ring[q].X * f), (20, ring[q].Y * f))); }
            sb.Append(G((97, 0)));
        }
        var h4 = Dxf(sb.Append(Tail).ToString());
        Assert.Equal(100, CadDxf.Parse(h4).Unread.Count); var t2 = Timed(() => CadDxf.Parse(h4));
        var t3 = CancelTime(h4);
        log.WriteLine($"Kreis r 100 000 + Bogen: {t1} ms; 100 × 100 fast gleich (1 000 Punkte): {t2} ms; Abbrechen: {t3} ms bis zur Ausnahme");
        Assert.True(t1 < 10_000 && t2 < 20_000, $"{t1} / {t2}"); Assert.True(t3 < 10_000, $"{t3}"); // einmal wiederholt; einzeln 0,2 s bzw. 3–4 s (Fix9) und jetzt darunter
    }
}
