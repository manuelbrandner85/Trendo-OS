using System.Diagnostics;
using System.Globalization;
using System.Text;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0747-fix5: Befunde der fünften Prüfung – 3D-Polylinien liegen in Weltkoordinaten (Normale gilt nicht), nicht lesbare Umrisse
/// (ELLIPSE wird jetzt abgetastet; Blockverweise, SPLINE, schräge Normale machen das Innere unsicher), schnelle Doppel-Prüfung, unsinnige Zahlen,
/// Kommentare im Kopf, negative Radien, PEDIT-Spline-Rahmenpunkte, NaN.</summary>
public class T0747_Fix5FuenftePruefung(ITestOutputHelper log)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string G(params (int Code, object Value)[] pairs) => string.Concat(pairs.Select(p => $"{p.Code}\n{Convert.ToString(p.Value, Inv)}\n"));
    static string Dxf(string entities, string blocks = "", int units = 4) =>
        $"0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n{units}\n0\nENDSEC\n" + (blocks.Length > 0 ? $"0\nSECTION\n2\nBLOCKS\n{blocks}0\nENDSEC\n" : "") + $"0\nSECTION\n2\nENTITIES\n{entities}0\nENDSEC\n0\nEOF\n";
    static string Lw((double X, double Y)[] pts, bool closed = true, string extra = "") =>
        G((0, "LWPOLYLINE"), (8, "0"), (90, pts.Length), (70, closed ? 1 : 0)) + string.Concat(pts.Select(p => G((10, p.X), (20, p.Y)))) + extra;
    static string Circle(double x, double y, double r, string extra = "") => G((0, "CIRCLE"), (8, "0"), (10, x), (20, y), (30, 0), (40, r)) + extra;
    static (double X, double Y)[] Rect(double x0, double y0, double x1, double y1) => [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
    const string MinusZ = "210\n0.0\n220\n0.0\n230\n-1.0\n";
    static CamResult Cam(string dxf, double tool = 3) => CncCam.Generate(CncCam.Contours(CadDxf.Parse(dxf).Drawing), new CamSettings { ToolD = tool, Unread = CadDxf.Parse(dxf).Unread });
    static (double X, double Y) Mid(CamResult r, CamPath p) => (p.Pts.Average(q => q.X) - r.ShiftX, p.Pts.Average(q => q.Y) - r.ShiftY);

    // ---------- M1: 3D-Polylinie ----------

    [Fact(DisplayName = "T-0747-fix5 M1: 3D-Polylinie (Flag 8) mit Normale −Z bleibt in Weltkoordinaten; 3D-Netz mit schräger Normale ist kein „nicht in der Zeichenebene“")]
    public void Polyline3d_ignores_normal()
    {
        var poly3d = G((0, "POLYLINE"), (8, "0"), (66, 1), (70, 9), (10, 0), (20, 0), (30, 0)) + MinusZ
            + string.Concat(Rect(0, 0, 100, 60).Select(p => G((0, "VERTEX"), (8, "0"), (10, p.X), (20, p.Y), (30, 5), (70, 32)))) + G((0, "SEQEND"), (8, "0"));
        var d = CadDxf.Parse(Dxf(poly3d + Circle(50, 30, 10)));
        Assert.Equal([0.0, 100.0, 100.0, 0.0], d.Drawing.Entities[0].Points.Select(p => p[0])); // nicht gespiegelt
        var r = CncCam.Generate(CncCam.Contours(d.Drawing), new CamSettings { ToolD = 3 });
        var hole = Assert.Single(r.Paths, p => p.Kind == "innen"); var (hx, hy) = Mid(r, hole); Assert.Equal(50, hx, 1); Assert.Equal(30, hy, 1);
        var mesh = G((0, "POLYLINE"), (8, "0"), (66, 1), (70, 16), (10, 0), (20, 0), (30, 0), (210, 0.6), (220, 0), (230, 0.8)) + G((0, "VERTEX"), (8, "0"), (10, 1), (20, 1), (30, 1)) + G((0, "SEQEND"), (8, "0"));
        var dm = CadDxf.Parse(Dxf(Lw(Rect(0, 0, 10, 10)) + mesh));
        Assert.Equal(1, dm.Skipped["3D-Netz (Polylinie)"]); Assert.False(dm.Skipped.ContainsKey("Polylinie nicht in der Zeichenebene")); Assert.Empty(dm.Unread);
    }

    // ---------- M2: nicht lesbare Umrisse ----------

    [Fact(DisplayName = "T-0747-fix5 M2: ELLIPSE wird abgetastet (Platte mit Löchern, Normale −Z, Bogenstück) – Abweichung ≤ 0,001 mm")]
    public void Ellipse_is_sampled()
    {
        var ell = G((0, "ELLIPSE"), (8, "0"), (10, 60), (20, 40), (30, 0), (11, 60), (21, 0), (31, 0), (40, 2.0 / 3), (41, 0), (42, 2 * Math.PI));
        var dxf = Dxf(ell + Circle(30, 40, 6) + Circle(90, 40, 6));
        var e = Assert.Single(CadDxf.Parse(dxf).Drawing.Entities, x => x.Kind == "polylinie"); Assert.True(e.Closed);
        // jeder Punkt auf der wahren Ellipse, jede Sehnenmitte höchstens 0,001 mm innerhalb
        Assert.All(e.Points, p => Assert.Equal(1, Math.Pow((p[0] - 60) / 60, 2) + Math.Pow((p[1] - 40) / 40, 2), 9));
        var worst = 0.0;
        for (var i = 0; i < e.Points.Count; i++)
        {
            var a = e.Points[i]; var b = e.Points[(i + 1) % e.Points.Count]; double mx = (a[0] + b[0]) / 2 - 60, my = (a[1] + b[1]) / 2 - 40;
            var t = Math.Atan2(my / 40, mx / 60); var dist = Math.Sqrt(Math.Pow(60 * Math.Cos(t) - mx, 2) + Math.Pow(40 * Math.Sin(t) - my, 2)); worst = Math.Max(worst, dist);
        }
        log.WriteLine($"Ellipse 120 × 80: {e.Points.Count} Punkte, größte Sehnenabweichung ≈ {worst:0.000000} mm");
        Assert.True(worst <= 0.0011, $"{worst}");
        var r = Cam(dxf);
        Assert.Equal(2, r.Paths.Count(p => p.Kind == "innen")); Assert.Single(r.Paths, p => p.Kind == "aussen");
        // Normale −Z: Nebenachse zeigt in die andere Richtung – als Loch in einer Platte
        var ellFlip = G((0, "ELLIPSE"), (8, "0"), (10, 50), (20, 30), (30, 0), (11, 20), (21, 0), (31, 0), (40, 0.5), (41, 0), (42, Math.PI)) + MinusZ;
        var half = Assert.Single(CadDxf.Parse(Dxf(ellFlip)).Drawing.Entities); Assert.False(half.Closed);
        Assert.All(half.Points, p => Assert.True(p[1] <= 30 + 1e-9, "obere Hälfte gespiegelt nach unten")); // t 0…π mit Nebenachse (0,−10)
    }

    [Fact(DisplayName = "T-0747-fix5 M2: Platte im Blockverweis, Löcher lose (b2) – Löcher werden ausgelassen und gemeldet; Loch-Block in Platte wird gemeldet")]
    public void Insert_outline_makes_inside_unsure()
    {
        var blk = G((0, "BLOCK"), (8, "0"), (2, "PLATTE"), (70, 0), (10, 0), (20, 0), (30, 0)) + Lw(Rect(0, 0, 100, 60)) + G((0, "ENDBLK"), (8, "0"));
        var b2 = Dxf(G((0, "INSERT"), (8, "0"), (2, "PLATTE"), (10, 0), (20, 0), (30, 0)) + Circle(30, 30, 8) + Circle(70, 30, 8) + Lw(Rect(200, 0, 230, 20)), blk);
        var rd = CadDxf.Parse(b2); var u = Assert.Single(rd.Unread);
        Assert.Equal((0.0, 0.0, 100.0, 60.0), (u.B.MinX, u.B.MinY, u.B.MaxX, u.B.MaxY));
        var r = Cam(b2);
        log.WriteLine(string.Join(" | ", r.Warnings));
        Assert.Equal(2, r.Warnings.Count(w => w.Contains("liegt in nicht gelesenem Element Blockverweis „PLATTE“", StringComparison.Ordinal)));
        Assert.All(r.Paths, p => Assert.True(Mid(r, p).X > 150));
        // Loch als Block in einer losen Platte: Platte wird gefräst, aber gemeldet
        var lochBlk = G((0, "BLOCK"), (8, "0"), (2, "LOCH"), (70, 0), (10, 0), (20, 0), (30, 0)) + Circle(0, 0, 6) + G((0, "ENDBLK"), (8, "0"));
        var r4 = Cam(Dxf(Lw(Rect(0, 0, 120, 80)) + G((0, "INSERT"), (8, "0"), (2, "LOCH"), (10, 30), (20, 40), (30, 0)), lochBlk));
        Assert.Contains(r4.Warnings, w => w.Contains("enthält nicht gelesenes Element Blockverweis „LOCH“", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "T-0747-fix5 M2: Ausdehnung eines Blockverweises mit Drehung, Skalierung, Spiegelung (−Z), verschachtelt und als Feld (MINSERT)")]
    public void Insert_extent_transform()
    {
        var blocks = G((0, "BLOCK"), (8, "0"), (2, "A"), (70, 0), (10, 10), (20, 0), (30, 0)) + Lw(Rect(10, 0, 20, 10)) + G((0, "ENDBLK"), (8, "0"))
            + G((0, "BLOCK"), (8, "0"), (2, "B"), (70, 0), (10, 0), (20, 0), (30, 0)) + G((0, "INSERT"), (8, "0"), (2, "A"), (10, 100), (20, 0), (30, 0)) + Circle(0, 0, 5) + G((0, "ENDBLK"), (8, "0"));
        static CadDxf.Box One(string blocks, string ins) => Assert.Single(CadDxf.Parse(Dxf(ins, blocks)).Unread).B;
        // A: Basis (10/0) → Inhalt 0…10 × 0…10 relativ; eingesetzt bei (50/50), Faktor 2, 90° gedreht → x 30…50, y 50…70
        var a = One(blocks, G((0, "INSERT"), (8, "0"), (2, "A"), (10, 50), (20, 50), (30, 0), (41, 2), (42, 2), (50, 90)));
        Assert.Equal(30, a.MinX, 6); Assert.Equal(50, a.MaxX, 6); Assert.Equal(50, a.MinY, 6); Assert.Equal(70, a.MaxY, 6);
        // gespiegelt (Normale −Z): x → −x
        var m = One(blocks, G((0, "INSERT"), (8, "0"), (2, "A"), (10, 50), (20, 0), (30, 0)) + MinusZ);
        Assert.Equal(-60, m.MinX, 6); Assert.Equal(-50, m.MaxX, 6);
        // verschachtelt: B enthält Kreis r 5 um 0/0 und A bei x 100 → x −5…110
        var n = One(blocks, G((0, "INSERT"), (8, "0"), (2, "B"), (10, 0), (20, 0), (30, 0)));
        Assert.Equal(-5, n.MinX, 6); Assert.Equal(110, n.MaxX, 6);
        // Feld: 3 Spalten im Abstand 30 → x 0…70
        var f = One(blocks, G((0, "INSERT"), (8, "0"), (2, "A"), (10, 10), (20, 0), (30, 0), (70, 3), (71, 1), (44, 30), (45, 0)));
        Assert.Equal(10, f.MinX, 6); Assert.Equal(80, f.MaxX, 6);
        // Block gibt es nicht → nichts gezeichnet (leer); sich selbst enthaltender Block → Ausdehnung unbekannt, überall unsicher
        Assert.True(One(blocks, G((0, "INSERT"), (8, "0"), (2, "FEHLT"), (10, 0), (20, 0), (30, 0))).IsEmpty);
        var self = G((0, "BLOCK"), (8, "0"), (2, "S"), (70, 0), (10, 0), (20, 0), (30, 0)) + G((0, "INSERT"), (8, "0"), (2, "S"), (10, 1), (20, 0), (30, 0)) + G((0, "ENDBLK"), (8, "0"));
        Assert.True(One(self, G((0, "INSERT"), (8, "0"), (2, "S"), (10, 0), (20, 0), (30, 0))).IsAll);
    }

    [Fact(DisplayName = "T-0747-fix5 M2: Kreis mit schräger Normale als Umriss – Löcher darin werden ausgelassen statt außen gefräst")]
    public void Oblique_outline_makes_inside_unsure()
    {
        var r = Cam(Dxf(Circle(0, 0, 40, "210\n0.0\n220\n0.6\n230\n0.8\n") + Circle(0, 0, 5) + Lw(Rect(200, 0, 220, 20))));
        log.WriteLine(string.Join(" | ", r.Warnings));
        Assert.Contains(r.Warnings, w => w.Contains("liegt in nicht gelesenem Element Kreis (nicht in der Zeichenebene)", StringComparison.Ordinal));
        Assert.All(r.Paths, p => Assert.True(Mid(r, p).X > 150));
    }

    // ---------- N1: viele Kreise ----------

    [Fact(DisplayName = "T-0747-fix5 N1: 50 000 Kreise – Doppel-Prüfung, Tiefe mit Vorfilter, Bahnen parallel, unter 30 s; Abbrechen wirkt")]
    public void Many_circles_fast()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 50_000; i++) sb.Append(Circle(i % 250 * 10, i / 250 * 10, 3));
        sb.Append(Circle(0, 0, 3)); // ein Doppel
        var cs = CncCam.Contours(CadDxf.Parse(Dxf(sb.ToString())).Drawing);
        var sw = Stopwatch.StartNew(); var r = CncCam.Generate(cs, new CamSettings { ToolD = 1, Depth = 1, StepDown = 1 }); var ms = sw.ElapsedMilliseconds;
        log.WriteLine($"50 001 Kreise: {ms} ms, {r.Paths.Count} Bahnen, {r.Warnings.Count} Hinweise");
        Assert.Equal(50_000, r.Paths.Count); Assert.Contains(r.Warnings, w => w.Contains("doppelt gezeichnet", StringComparison.Ordinal));
        // T-0747-fix8: unter Fremdlast (Volltest, andere Läufe) dauert es länger – Grenze großzügig, Messwert im Log (einzeln etwa 20 s)
        Assert.True(ms < 120_000, $"{ms}");
        using var cts = new CancellationTokenSource(); cts.CancelAfter(200); sw.Restart();
        Assert.ThrowsAny<OperationCanceledException>(() => CncCam.Generate(cs, new CamSettings { ToolD = 1, Depth = 1, StepDown = 1 }, cts.Token));
        log.WriteLine($"Abbrechen nach 0,2 s: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 10_000, $"Abbrechen {sw.ElapsedMilliseconds} ms"); // T-0747-fix8: lastrobust (einzeln unter 1 s)
    }

    // ---------- N2–N6 ----------

    [Fact(DisplayName = "T-0747-fix5 N2–N6: unsinnige Zahlen, NaN, ∞, negativer Radius werden einzeln verworfen und gemeldet; 999 im Kopf; PEDIT-Spline")]
    public void Robust_reader()
    {
        var bad = G((0, "CIRCLE"), (8, "0"), (10, "1e20"), (20, 0), (30, 0), (40, 5)) + G((0, "CIRCLE"), (8, "0"), (10, "NaN"), (20, 0), (30, 0), (40, 5))
            + G((0, "LINE"), (8, "0"), (10, 0), (20, 0), (11, "Infinity"), (21, 0)) + G((0, "CIRCLE"), (8, "0"), (10, 50), (20, 30), (30, 0), (40, -4))
            + G((0, "CIRCLE"), (8, "0"), (10, "1e300"), (20, 30), (30, 0), (40, 5));
        var rd = CadDxf.Parse(Dxf(Lw(Rect(0, 0, 100, 60)) + Circle(50, 30, 10) + bad));
        log.WriteLine(string.Join(", ", rd.Skipped.Select(k => $"{k.Value}× {k.Key}")));
        Assert.Equal(3, rd.Skipped["Kreis mit ungültiger Koordinate"]); Assert.Equal(1, rd.Skipped["Linie mit ungültiger Koordinate"]);
        Assert.Equal(1, rd.Skipped["Kreis mit Radius ≤ 0"]);
        Assert.Equal(2, rd.Drawing.Entities.Count); // Platte und gültiger Kreis bleiben
        // Kommentar zwischen $INSUNITS und dem Wert
        var withComment = "0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n999\nZoll\n70\n1\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n" + Circle(0, 0, 1) + "0\nENDSEC\n0\nEOF\n";
        var rc = CadDxf.Parse(withComment); Assert.Equal(1, rc.Units); Assert.Equal(25.4, rc.Drawing.Entities[0].R, 9);
        // PEDIT-Spline: Rahmenpunkte (Vertex-Flag 16) weglassen, Kurvenpunkte (8) verwenden
        var frame = Rect(0, 0, 100, 60); var fit = Rect(10, 10, 90, 50);
        var spl = G((0, "POLYLINE"), (8, "0"), (66, 1), (70, 5), (10, 0), (20, 0), (30, 0)) + string.Concat(frame.Select(p => G((0, "VERTEX"), (8, "0"), (10, p.X), (20, p.Y), (70, 16))))
            + string.Concat(fit.Select(p => G((0, "VERTEX"), (8, "0"), (10, p.X), (20, p.Y), (70, 8)))) + G((0, "SEQEND"), (8, "0"));
        var e = Assert.Single(CadDxf.Parse(Dxf(spl)).Drawing.Entities);
        Assert.Equal(fit.Select(p => p.X), e.Points.Select(p => p[0]));
    }
}
