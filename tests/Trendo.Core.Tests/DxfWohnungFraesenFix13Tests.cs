using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix13: Befunde der dreizehnten Prüfung – Linien-Stöße nicht gierig verketten, Punktgruppen streng lesen, gemeinsames
/// Formvergleich-Budget mit Vorabtest, Rahmen-Index ohne flächig eingetragene große Rahmen.</summary>
[Collection("T0747 Zeitmessung")]
public class T0747_Fix13DreizehntePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    const string AusLayer = "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nAUS\n70\n0\n62\n-7\n0\nENDTAB\n0\nENDSEC\n";
    static string Dxf(string entities, string tables = "") => "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n" + tables + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Line(double x0, double y0, double x1, double y1, string layer = "0") => G((0, "LINE"), (8, layer), (10, x0), (20, y0), (30, 0), (11, x1), (21, y1), (31, 0));
    static string Circle(double x, double y, double r) => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r));
    static string Lw(IEnumerable<(double X, double Y)> pts, string layer = "0")
    {
        var p = pts.ToList(); var sb = new StringBuilder(G((0, "LWPOLYLINE"), (8, layer), (90, p.Count), (70, 1)));
        foreach (var q in p) sb.Append(G((10, q.X), (20, q.Y)));
        return sb.ToString();
    }
    static (double X, double Y)[] Sq(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    static string Hatch(string layer, IEnumerable<IEnumerable<(double X, double Y)>> loops)
    {
        var l = loops.Select(x => x.ToList()).ToList();
        var sb = new StringBuilder(G((0, "HATCH"), (8, layer), (10, 0), (20, 0), (30, 0), (210, 0), (220, 0), (230, 1), (2, "SOLID"), (70, 1), (71, 0), (91, l.Count)));
        foreach (var q in l) { sb.Append(G((92, 3), (72, 0), (73, 1), (93, q.Count))); foreach (var v in q) sb.Append(G((10, v.X), (20, v.Y))); sb.Append(G((97, 0))); }
        return sb.Append(G((75, 0), (76, 1), (98, 0))).ToString();
    }
    static CamResult Cam(string dxf) { var rd = CadDxf.Parse(dxf); return CncCam.Generate(CncCam.Contours(rd.Drawing), new CamSettings { ToolD = 1, Unread = rd.Unread }); }
    /// <summary>Fräsbahnen oder – wenn alles ausgelassen wurde – die Meldung (auch sicher: nichts wird gefräst).</summary>
    static (CamResult? R, string Text) CamOrNothing(string dxf)
    {
        try { var r = Cam(dxf); return (r, string.Join(" | ", r.Warnings)); }
        catch (InvalidOperationException e) { return (null, e.Message); }
    }
    static bool MilledOutside(CamResult r, double x, double y, double rr) =>
        r.Paths.Any(p => p.Kind == "aussen" && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr - 0.5) < 0.05));
    static bool MilledInside(CamResult r, double x, double y, double rr) =>
        r.Paths.Any(p => p.Kind == "innen" && p.Pts.All(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - x, 2) + Math.Pow(q.Y - r.ShiftY - y, 2)) - rr + 0.5) < 0.05));
    static long Timed(Action a)
    {
        var sw = Stopwatch.StartNew(); a(); var t = sw.ElapsedMilliseconds;
        if (t > 2000) { sw.Restart(); a(); t = Math.Min(t, sw.ElapsedMilliseconds); } // unter Fremdlast einmal wiederholt
        return t;
    }

    [Fact(DisplayName = "T-0747-fix13 HOCH 1: LINE-Rechteck mit zwei Ausläufern an T-Stößen – in jeder Reihenfolge wird kein Loch außen gefräst; Ausläufer auf eigener Ebene bleibt richtig")]
    public void T_junction_never_hole_outside()
    {
        string s1 = Line(0, 0, -50, 0), l1 = Line(0, 0, 100, 0), l2 = Line(100, 0, 100, 100), s2 = Line(100, 100, 150, 100), l3 = Line(100, 100, 0, 100), l4 = Line(0, 100, 0, 0);
        var orders = new[]
        {
            new[] { s1, l1, l2, s2, l3, l4 }, new[] { l1, l2, l3, l4, s1, s2 }, new[] { s2, s1, l4, l3, l2, l1 },
            new[] { l3, s1, l1, s2, l4, l2 }, new[] { Line(-50, 0, 0, 0), Line(100, 0, 0, 0), l2, Line(150, 100, 100, 100), l3, l4 },
        };
        foreach (var o in orders)
        {
            var (r, text) = CamOrNothing(Dxf(string.Concat(o) + Circle(90, 80, 4) + Circle(20, 20, 4)));
            log.WriteLine(text);
            if (r is not null) { Assert.False(MilledOutside(r, 90, 80, 4)); Assert.False(MilledOutside(r, 20, 20, 4)); }
            Assert.Contains("mehr als zwei Linien zusammenstoßen", text);
        }
        // Kontrolle: Ausläufer auf eigener Ebene → das Rechteck schließt sich, das Loch wird als Loch gefräst
        var z4 = Cam(Dxf(Line(0, 0, -50, 0, "H") + l1 + l2 + Line(100, 100, 150, 100, "H") + l3 + l4 + Circle(90, 80, 4)));
        Assert.True(MilledInside(z4, 90, 80, 4));
        // Kontrolle: Rechteck ohne Ausläufer unverändert
        Assert.True(MilledInside(Cam(Dxf(l1 + l2 + l3 + l4 + Circle(90, 80, 4))), 90, 80, 4));
    }

    [Fact(DisplayName = "T-0747-fix13 MITTEL 2: Punktgruppen streng – SOLID/TRACE/3DFACE/LINE mit fehlender oder doppelter Gruppe gelten überall als unsicher; nur umsortiert wird richtig gelesen")]
    public void Strict_point_groups()
    {
        static string Solid(string kind, params (int C, double V)[] g) => G((0, kind), (8, "0")) + string.Concat(g.Select(x => G((x.C, x.V))));
        // umsortiert (erst alle x, dann alle y) – DXF erlaubt jede Reihenfolge, jeder Code genau einmal: Rahmen 0…100 × 0…60
        var sorted = Solid("SOLID", (10, 0), (11, 100), (12, 0), (13, 100), (20, 0), (21, 0), (22, 60), (23, 60), (30, 0), (31, 0), (32, 0), (33, 0));
        var rd = CadDxf.Parse(Dxf(sorted + Circle(50, 30, 5)));
        var u = Assert.Single(rd.Unread); Assert.Equal((0.0, 0.0, 100.0, 60.0), (u.B.MinX, u.B.MinY, u.B.MaxX, u.B.MaxY));
        // fehlende 20 (erster Punkt), fehlender vierter Punkt, doppelte 11 → überall unsicher
        foreach (var bad in new[]
        {
            Solid("SOLID", (10, -50), (30, 0), (11, 100), (21, 0), (12, 0), (22, 60), (13, 100), (23, 60)),
            Solid("TRACE", (10, 0), (20, 0), (11, 100), (21, 0), (12, 0), (22, 60)),
            Solid("3DFACE", (10, 0), (20, 0), (11, 100), (21, 0), (11, 50), (12, 100), (22, 60), (13, 0), (23, 60)),
        })
        {
            var b = CadDxf.Parse(Dxf(bad + Circle(50, 30, 5)));
            Assert.Contains(b.Unread, x => x.B.IsAll);
            var (r, text) = CamOrNothing(Dxf(bad + Circle(50, 30, 5)));
            log.WriteLine(text);
            if (r is not null) Assert.False(MilledOutside(r, 50, 30, 5));
        }
        // LINE: umsortiert richtig, fehlendes 21 → nicht still mit y = 0 gezeichnet, sondern unsicher
        var ls = CadDxf.Parse(Dxf(G((0, "LINE"), (8, "0"), (10, 0), (11, 100), (20, 5), (21, 7))));
        Assert.Equal(7, ls.Drawing.Entities.Single().Points[1][1]);
        var lm = CadDxf.Parse(Dxf(G((0, "LINE"), (8, "0"), (10, 0), (20, 5), (11, 100))));
        Assert.Empty(lm.Drawing.Entities); Assert.Contains(lm.Unread, x => x.B.IsAll);
    }

    static (double X, double Y)[] Comb(int m, double lastDx)
    {
        var p = new List<(double X, double Y)>(); const double d = 0.05, L = 10;
        for (var t = 0; t < m; t++) { var x = t * d; p.Add((x, 0)); p.Add((x + d / 2 + (t == m - 1 ? lastDx : 0), L)); }
        var xe = m * d + 12; p.Add((xe, L)); p.Add((xe, -1)); p.Add((0, -1));
        return [.. p];
    }

    [Fact(DisplayName = "T-0747-fix13 HOCH 3 / MITTEL 4: Formvergleich Kamm 2000 × 64 gezeichnet × 117 Schraffuren und 1000 riesige Rahmen + 60 000 winzige je unter 10 s, abbrechbar")]
    public void Shape_budget_and_big_frames()
    {
        var sb = new StringBuilder();
        for (var k = 0; k < 64; k++) sb.Append(Lw(Comb(2000, 0.04 * k)));
        for (var k = 0; k < 117; k++) sb.Append(Hatch("0", [Comb(2000, k < 64 ? 0.04 * k + 0.02 : 0.04 * k)]));
        var s1 = Dxf(sb.ToString());
        var t1 = Timed(() => CadDxf.Parse(s1)); var r1 = CadDxf.Parse(s1);
        sb.Clear();
        for (var i = 0; i < 1000; i++) { var s = 1000.0 + i * 0.5; sb.Append(Lw(Sq(-s, -s, s, s))); }
        for (var i = 0; i < 60000; i++) { double x = -900 + i % 245 * (1800.0 / 245), y = -900 + i / 245 * (1800.0 / 245); sb.Append(Lw(Sq(x, y, x + 0.2, y + 0.2))); }
        sb.Append(Hatch("AUS", [Sq(-899.9, -899.9, -899.8, -899.8)]));
        var c1 = Dxf(sb.ToString(), AusLayer);
        var t2 = Timed(() => CadDxf.Parse(c1));
        log.WriteLine($"Kamm-Formvergleich: {t1} ms ({r1.Unread.Count} nicht gelesen); 1000 große + 60 000 winzige: {t2} ms");
        Assert.True(t1 < 10000, $"Kamm {t1} ms"); Assert.True(t2 < 10000, $"große Rahmen {t2} ms");
        Assert.Equal(117, r1.Unread.Count); // nie formgleich: alle bleiben
        // T-0747-fix14: der Abbruch muss wirklich mit OperationCanceledException enden (eigener Thread, startet mit dem Lesen; bis zu fünf Versuche)
        var delay = Math.Max(20, t1 / 2);
        for (var attempt = 0; ; attempt++)
        {
            using var cts = new CancellationTokenSource(); using var go = new ManualResetEventSlim(false); var d = delay;
            var th = new Thread(() => { go.Wait(); Thread.Sleep((int)d); cts.Cancel(); }) { IsBackground = true }; th.Start();
            var sw = Stopwatch.StartNew(); go.Set();
            try { CadDxf.Parse(s1, cts.Token); }
            catch (OperationCanceledException) { th.Join(); Assert.True(sw.ElapsedMilliseconds < d + 3000, $"Abbruch nach {sw.ElapsedMilliseconds} ms"); break; }
            th.Join();
            delay = Math.Max(10, sw.ElapsedMilliseconds / 2);
            if (attempt == 4) throw new Xunit.Sdk.XunitException("Abbruch wirkte nicht");
        }
    }
}
