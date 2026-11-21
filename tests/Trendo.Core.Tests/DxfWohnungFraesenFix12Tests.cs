using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix12: Befunde der zwölften Prüfung – gleiche Schleifen nur bei gleicher Punktfolge zusammenfassen, ausgeblendete Schraffur nur
/// leer in einem Teil verwerfen, Formvergleich mit Kandidaten- und Schrittbudget, Abbruch mitten im Abgleich, unsichtbar beim Schreiben erhalten.</summary>
/// <summary>T-0747-fix13: Zeittests nicht parallel zu anderen Testklassen (unter Last waren 3 s zu knapp).</summary>
[CollectionDefinition("T0747 Zeitmessung", DisableParallelization = true)]
public class T0747ZeitmessungCollection { }

[Collection("T0747 Zeitmessung")]
public class T0747_Fix12ZwoelftePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    const string AusLayer = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nAUS\n70\n0\n62\n-7\n0\nENDTAB\n0\nENDSEC\n";
    static string Dxf(string entities, string tables = "", int units = 4) => $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n{units}\n0\nENDSEC\n" + tables + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Lw(IEnumerable<(double X, double Y)> pts, string layer = "0")
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, layer), (90, p.Count), (70, 1)));
        foreach (var q in p) sb.Append(G((10, q.X), (20, q.Y)));
        return sb.ToString();
    }
    static string Circle(double x, double y, double r, string layer = "0") => G((0, "CIRCLE"), (8, layer), (10, x), (20, y), (30, 0), (40, r));
    static string Hatch(string layer, IEnumerable<string> loops)
    {
        var l = loops.ToList(); var sb = new StringBuilder(G((0, "HATCH"), (8, layer), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, l.Count)));
        foreach (var x in l) sb.Append(x);
        return sb.Append(G((75, 0), (76, 1), (98, 0))).ToString();
    }
    static string PolyLoop(IEnumerable<(double X, double Y)> p) { var q = p.ToList(); return G((92, 3), (72, 0), (73, 1), (93, q.Count)) + string.Concat(q.Select(v => G((10, v.X), (20, v.Y)))) + G((97, 0)); }
    static (double X, double Y)[] Sq(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];

    static long Timed(Action a)
    {
        var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds;
        if (t > 2000) { sw.Restart(); a(); t = Math.Min(t, sw.ElapsedMilliseconds); } // unter Fremdlast einmal wiederholt
        return t;
    }

    [Fact(DisplayName = "T-0747-fix12 HOCH 1: zwei Schraffuren mit gleichem Rahmen, gleicher Punktzahl und gleichem ersten Punkt – nur die formgleiche fällt weg")]
    public void Same_first_point_not_same_shape()
    {
        (double, double)[] t1 = [(0, 0), (10, 0), (10, 10), (0, 10)];
        (double, double)[] t2 = [(0, 0), (10, 0), (10, 10), (3, 10)]; // gleicher Rahmen (MinX 0 über (0,0)), andere Form
        var rd = CadDxf.Parse(Dxf(Lw(t1) + Hatch("0", [PolyLoop(t1)]) + Hatch("0", [PolyLoop(t2)])));
        log.WriteLine($"{rd.Unread.Count} nicht gelesen, Redundant {string.Join(",", rd.Redundant)}");
        Assert.Single(rd.Unread); Assert.Equal(1, rd.Redundant["HATCH"]);
        // umgekehrte Reihenfolge: die andere Form zuerst
        var rd2 = CadDxf.Parse(Dxf(Lw(t1) + Hatch("0", [PolyLoop(t2)]) + Hatch("0", [PolyLoop(t1)])));
        Assert.Single(rd2.Unread); Assert.Equal(1, rd2.Redundant["HATCH"]);
    }

    [Fact(DisplayName = "T-0747-fix12 MITTEL: ausgeblendete Schraffuren mit gleichem Rahmen/Punktzahl/ersten Punkt einzeln entschieden (L-Platte: eine liegt drin, eine ragt hinaus)")]
    public void Hidden_decided_by_full_shape()
    {
        (double, double)[] plate = [(0, 0), (100, 0), (100, 50), (50, 50), (50, 100), (0, 100)];
        (double, double)[] a = [(30, 30), (70, 30), (50, 48), (30, 70)];
        (double, double)[] b = [(30, 30), (70, 30), (70, 70), (30, 70)];
        foreach (var order in new[] { new[] { a, b }, new[] { b, a } })
        {
            var rd = CadDxf.Parse(Dxf(Lw(plate) + Hatch("AUS", [PolyLoop(order[0])]) + Hatch("AUS", [PolyLoop(order[1])]), AusLayer));
            log.WriteLine($"{rd.Unread.Count} nicht gelesen");
            Assert.Single(rd.Unread); // nur die leere Schraffur im Teil fällt weg
        }
    }

    [Fact(DisplayName = "T-0747-fix12 HOCH 2: ausgeblendete Schraffur bleibt, sobald eine andere Kontur sie berührt, schneidet oder darin liegt – auch ohne Inhalt")]
    public void Hidden_fill_kept_when_touched()
    {
        var plate = Lw(Sq(0, 0, 200, 200)); var hatch = Hatch("AUS", [PolyLoop(Sq(20, 20, 80, 80))]);
        var cases = new (string Name, string Extra)[]
        {
            ("Raute schneidet, mit Loch", Lw([(80, 50), (110, 80), (80, 110), (50, 80)]) + Circle(80, 80, 3)),
            ("Raute schneidet, leer", Lw([(80, 50), (110, 80), (80, 110), (50, 80)])),
            ("Kreis darin, leer", Circle(50, 50, 5)),
            ("Kreis berührt den Rand", Circle(85, 50, 5)),
        };
        foreach (var (name, extra) in cases)
        {
            var rd = CadDxf.Parse(Dxf(plate + hatch + extra, AusLayer));
            log.WriteLine($"{name}: {rd.Unread.Count} nicht gelesen");
            Assert.Single(rd.Unread);
        }
        // Gegenprobe: leer im Teil, Löcher weit weg → fällt weg
        Assert.Empty(CadDxf.Parse(Dxf(plate + hatch + Circle(150, 150, 5), AusLayer)).Unread);
    }

    [Fact(DisplayName = "T-0747-fix12 HOCH 2 Tiefe: umschließende Kontur ist ein Loch (ungerade Tiefe) – ausgeblendete Schraffur bleibt stehen")]
    public void Hidden_fill_in_hole_kept()
    {
        var dxf = Dxf(Lw(Sq(0, 0, 300, 300)) + Lw(Sq(50, 50, 250, 250)) + Hatch("AUS", [PolyLoop(Sq(100, 100, 200, 200))]), AusLayer);
        Assert.Single(CadDxf.Parse(dxf).Unread);
        // drei Ebenen: Teil im Loch (Tiefe 2) → wieder ein Teil, Schraffur darin leer → fällt weg
        var dxf2 = Dxf(Lw(Sq(0, 0, 300, 300)) + Lw(Sq(50, 50, 250, 250)) + Lw(Sq(80, 80, 220, 220)) + Hatch("AUS", [PolyLoop(Sq(100, 100, 200, 200))]), AusLayer);
        Assert.Empty(CadDxf.Parse(dxf2).Unread);
    }

    static (double X, double Y)[] Spiral(int n)
    {
        var pts = new List<(double X, double Y)>();
        for (var k = 0; k < n - 3; k++) { var t = k / 400.0; var r = 1.0 + 0.05 * t; var a = 2 * Math.PI * t; pts.Add((r * Math.Cos(a), r * Math.Sin(a))); }
        var (lx, ly) = pts[^1]; pts.Add((lx + 1e8, ly)); pts.Add((lx + 1e8, ly - 0.2)); pts.Add((lx, ly - 0.2));
        return [.. pts];
    }

    static string Notched(int m, int n, double shift)
    {
        var sb = new StringBuilder();
        (double, double)[] Sh(double x) => [(0, 0), (10, 0), (10, 10), (x + 0.5, 10), (x + 0.5, 9), (x, 9), (x, 10), (0, 10)];
        for (var i = 0; i < m; i++) sb.Append(Lw(Sh(1 + 8.0 * i / m)));
        for (var i = 0; i < n; i++) sb.Append(Hatch("0", [PolyLoop(Sh(1 + 8.0 * i / n + shift))]));
        return Dxf(sb.ToString());
    }

    [Fact(DisplayName = "T-0747-fix12 MITTEL Leistung: Riesenzelle 100 000 Punkte und 20 000 Konturen mit gleichem Rahmen in Sekunden; Abbruch mitten im Abgleich wirkt")]
    public void Big_cell_and_same_box_fast()
    {
        var sp = Spiral(100_000); var d1 = Dxf(Lw(sp) + Hatch("0", [PolyLoop(sp)]));
        var t1 = Timed(() => CadDxf.Parse(d1));
        var b1 = Notched(20_000, 2000, 0); var rb = CadDxf.Parse(b1);
        var t2 = Timed(() => CadDxf.Parse(b1));
        log.WriteLine($"Riesenzelle 100 000: {t1} ms; 20 000 gleiche Rahmen + 2000 Schraffuren: {t2} ms, {rb.Unread.Count} nicht gelesen");
        Assert.True(t1 < 10000, $"Riesenzelle {t1} ms"); // T-0747-fix13: Grenze 10 s (ohne Last ≈ 1,3 s)
        Assert.True(t2 < 10000, $"gleiche Rahmen {t2} ms");
        Assert.True(rb.Redundant.GetValueOrDefault("HATCH") + rb.Unread.Count == 2000);

        // Abbruch nach der halben gemessenen Laufzeit: muss mit Ausnahme enden (Abgleich-Phase), bis zu fünf Versuche
        var t = Math.Max(t2, 40);
        for (var attempt = 0; ; attempt++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(t / 2)); var sw = Stopwatch.StartNew();
            try { CadDxf.Parse(b1, cts.Token); log.WriteLine($"Versuch {attempt}: fertig nach {sw.ElapsedMilliseconds} ms (Abbruch nach {t / 2} ms)"); }
            catch (OperationCanceledException) { log.WriteLine($"Versuch {attempt}: abgebrochen nach {sw.ElapsedMilliseconds} ms (Abbruch nach {t / 2} ms)"); break; }
            t = Math.Max(sw.ElapsedMilliseconds, 40); // ohne Fremdlast schneller fertig: halbe Zeit dieses Laufs als neuer Abbruchzeitpunkt
            if (attempt == 4) throw new Xunit.Sdk.XunitException("Abbruch mitten im Abgleich wirkte nicht");
        }
    }

    [Fact(DisplayName = "T-0747-fix12 niedrig: unsichtbare Elemente (60 = 1) bleiben beim Schreiben unsichtbar – Rundreise")]
    public void Write_keeps_invisible()
    {
        var dxf = Dxf(Lw(Sq(0, 0, 100, 100)) + G((0, "LINE"), (8, "0"), (60, 1), (10, 1), (20, 1), (30, 0), (11, 9), (21, 9), (31, 0))
            + G((0, "CIRCLE"), (8, "0"), (60, 1), (10, 50), (20, 50), (30, 0), (40, 5)));
        var d = CadDxf.Parse(dxf).Drawing;
        var back = CadDxf.Parse(CadDxf.Write(d)).Drawing;
        Assert.Equal(d.Entities.Count(e => e.Invisible), back.Entities.Count(e => e.Invisible));
        Assert.Contains(back.Entities, e => e.Invisible && e.Kind == "linie");
        Assert.Contains(back.Entities, e => e.Invisible && e.Kind == "kreis");
        Assert.Contains(back.Entities, e => !e.Invisible);
    }
}
