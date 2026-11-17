using System.Globalization;
using System.Text;
using Trendo.Core.Cad;

namespace Trendo.Core;

/// <summary>T-0747: Einstellungen für Fräsbahnen aus einer Zeichnung. Maße in mm, Vorschübe in mm/min.</summary>
public sealed record CamSettings
{
    public double ToolD { get; init; } = 6;
    public double Depth { get; init; } = 3;
    public double StepDown { get; init; } = 1;
    public double Feed { get; init; } = 600;
    public double Plunge { get; init; } = 200;
    public double Safe { get; init; } = 5;
    /// <summary>„auto“ (äußerster Umriss außen, Löcher innen, offene Linien auf der Linie), „aussen“, „innen“ oder „auf“.</summary>
    public string Mode { get; init; } = "auto";
    /// <summary>Haltestege je Außenkontur (0 = keine), Breite (Material, das stehen bleibt) und Höhe über dem Boden.</summary>
    public int Tabs { get; init; }
    public double TabW { get; init; } = 6;
    public double TabH { get; init; } = 1;
    /// <summary>Spindeldrehzahl für M3 (0 = kein M3 im Programm).</summary>
    public double Rpm { get; init; }
    /// <summary>Teil so verschieben, dass seine linke untere Ecke auf dem Nullpunkt liegt.</summary>
    public bool ToOrigin { get; init; } = true;
    /// <summary>Höchste Abweichung der Bahn von Bögen (mm).</summary>
    public double Chord { get; init; } = 0.01;
}

/// <summary>Eine Fräsbahn (Fräsermitte) mit Art und Herkunft.</summary>
public sealed record CamPath(string Kind, List<(double X, double Y)> Pts, bool Closed, int Source, string Layer);

/// <summary>Ergebnis: G-Code, Bahnen, Hinweise, Verschiebung und die (verschobenen) Konturen der Zeichnung.</summary>
public sealed record CamResult(string Gcode, List<CamPath> Paths, List<string> Warnings, double ShiftX, double ShiftY, List<CadContour> Contours);

/// <summary>T-0747 (SPEC-14): CAM aus Zeichnungen – Konturen aus DXF (<see cref="CadDxf"/>) oder SVG (<see cref="CadSvg"/>) werden zu Fräsbahnen.
/// Außenkontur: Fräsermitte um den Radius nach außen versetzt, Gleichlauf (im Uhrzeigersinn); Innenkontur: nach innen versetzt, gegen den Uhrzeigersinn;
/// offene Linien: auf der Linie (Gravur). Die Werkzeugradius-Korrektur rechnet Trendo selbst (Versatz mit runden Außenecken, Schleifen an engen Stellen
/// werden entfernt) – im G-Code stehen nur G0/G1, kein G41/G42. Zustellung in Stufen bis zur Tiefe, Haltestege auf Außenkonturen, Löcher vor dem Umriss.</summary>
public static class CncCam
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string I(double v) => Math.Round(v, 4).ToString("0.####", Inv);

    /// <summary>Konturen einer Zeichnung (sichtbare Ebenen, Texte bleiben weg).</summary>
    public static List<CadContour> Contours(CadDrawing d, double chord = 0.01)
    {
        var hidden = d.Layers.Where(l => !l.Visible).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CadContours.Build(d.Entities.Where(e => e.Kind != "text" && !hidden.Contains(e.Layer)), 0.01, chord);
    }

    public static CamResult Generate(IReadOnlyList<CadContour> source, CamSettings s)
    {
        if (source.Count == 0) throw new InvalidOperationException("Die Zeichnung hat keine Konturen (Linien, Kreise, Bögen, Pfade).");
        if (s.ToolD <= 0 || s.Depth <= 0 || s.StepDown <= 0 || s.Feed <= 0 || s.Plunge <= 0) throw new ArgumentException("Fräser-Ø, Tiefe, Zustellung und Vorschübe müssen größer als 0 sein.");
        if (s.Tabs > 0 && (s.TabH <= 0 || s.TabH >= s.Depth)) throw new ArgumentException("Haltestege: Höhe muss größer als 0 und kleiner als die Tiefe sein.");
        var r = s.ToolD / 2; var warn = new List<string>();
        // verschieben: linke untere Ecke aufs Nullpunkt
        double sx = 0, sy = 0;
        if (s.ToOrigin) { sx = -source.Min(c => c.Pts.Min(p => p.X)); sy = -source.Min(c => c.Pts.Min(p => p.Y)); }
        var cs = source.Select(c => c with { Pts = c.Pts.Select(p => (p.X + sx, p.Y + sy)).ToList() }).ToList();
        // Art je Kontur
        var kinds = new string[cs.Count]; var depth = new int[cs.Count];
        for (var i = 0; i < cs.Count; i++)
        {
            if (!cs[i].Closed) { kinds[i] = "auf"; continue; }
            var probe = cs[i].Pts[0];
            depth[i] = cs.Where((c, j) => j != i && c.Closed && Math.Abs(c.SignedArea) > Math.Abs(cs[i].SignedArea) && c.Contains(probe.X, probe.Y)).Count();
            kinds[i] = s.Mode switch { "aussen" => "aussen", "innen" => "innen", "auf" => "auf", _ => depth[i] % 2 == 0 ? "aussen" : "innen" };
        }
        // Bahnen
        var paths = new List<CamPath>();
        for (var i = 0; i < cs.Count; i++)
        {
            var c = cs[i]; var name = $"Kontur {i + 1}" + (c.Layer is { Length: > 0 } and not "0" ? $" (Ebene {c.Layer})" : "");
            switch (kinds[i])
            {
                case "aussen":
                {
                    var off = Offset(c.Pts, r, s.Chord); off.Reverse(); // im Uhrzeigersinn = Gleichlauf außen
                    if (off.Count < 3 || Gouges(off, c, r, name, warn)) continue;
                    paths.Add(new CamPath("aussen", off, true, i, c.Layer)); break;
                }
                case "innen":
                {
                    var off = Offset(c.Pts, -r, s.Chord);
                    if (off.Count < 3 || SignedArea(off) <= 1e-6) { warn.Add($"{name}: Innenkontur ist zu klein für den Fräser Ø {I(s.ToolD)} mm – ausgelassen."); continue; }
                    if (Gouges(off, c, r, name, warn)) continue;
                    paths.Add(new CamPath("innen", off, true, i, c.Layer)); break;
                }
                default: paths.Add(new CamPath("auf", [.. c.Pts], c.Closed, i, c.Layer)); break;
            }
        }
        // Reihenfolge: Löcher (tiefste zuerst), Gravuren, dann Umrisse – jeweils der nächste zuerst
        var order = new List<CamPath>(); (double X, double Y) at = (0, 0);
        foreach (var group in new[] { paths.Where(p => p.Kind == "innen").GroupBy(p => -depth[p.Source]).OrderBy(g => g.Key).SelectMany(g => g).ToList(), paths.Where(p => p.Kind == "auf").ToList(), paths.Where(p => p.Kind == "aussen").ToList() })
        {
            var left = group.ToList();
            while (left.Count > 0)
            {
                var next = left.OrderBy(p => p.Closed ? p.Pts.Min(q => D2(q, at)) : Math.Min(D2(p.Pts[0], at), D2(p.Pts[^1], at))).First(); left.Remove(next);
                var pts = next.Pts;
                if (next.Closed) { var k = Enumerable.Range(0, pts.Count).MinBy(j => D2(pts[j], at)); pts = [.. pts.Skip(k), .. pts.Take(k)]; }
                else if (D2(pts[^1], at) < D2(pts[0], at)) pts = Enumerable.Reverse(pts).ToList();
                order.Add(next with { Pts = pts }); at = pts[0];
            }
        }
        // G-Code
        var g = new StringBuilder();
        g.Append("(Konturen aus Zeichnung – Trendo OS)\n");
        g.Append($"(Fräser Ø {I(s.ToolD)} mm, Tiefe {I(s.Depth)} mm, Zustellung {I(s.StepDown)} mm – Radiuskorrektur ist eingerechnet, Bahn = Fräsermitte)\n");
        g.Append("G21 G90 G17\n").Append($"G0 Z{I(s.Safe)}\n");
        if (s.Rpm > 0) g.Append($"M3 S{I(Math.Round(s.Rpm))}\n");
        var passes = (int)Math.Ceiling(s.Depth / s.StepDown - 1e-9);
        var zTab = -s.Depth + s.TabH; var n = 0;
        foreach (var p in order)
        {
            n++;
            var label = p.Kind switch { "aussen" => "außen", "innen" => "innen", _ => "auf der Linie" };
            g.Append($"(Kontur {p.Source + 1}: {label}{(p.Layer is { Length: > 0 } and not "0" ? ", Ebene " + p.Layer : "")})\n");
            g.Append($"G0 X{I(p.Pts[0].X)} Y{I(p.Pts[0].Y)}\n");
            var tabs = p.Kind == "aussen" && s.Tabs > 0 ? TabRanges(p.Pts, s, name: $"Kontur {p.Source + 1}", warn) : [];
            var forward = true;
            for (var k = 1; k <= passes; k++)
            {
                var z = -Math.Min(s.Depth, k * s.StepDown);
                var pts = p.Closed || forward ? p.Pts : Enumerable.Reverse(p.Pts).ToList();
                g.Append($"G1 Z{I(z)} F{I(s.Plunge)}\n");
                Loop(g, pts, p.Closed, z, tabs.Count > 0 && z < zTab - 1e-9 ? tabs : [], zTab, s);
                if (!p.Closed) forward = !forward;
            }
            g.Append($"G0 Z{I(s.Safe)}\n");
        }
        g.Append("M5\nM30\n");
        if (n == 0) throw new InvalidOperationException("Es blieb keine fräsbare Kontur übrig: " + string.Join(" ", warn));
        return new CamResult(g.ToString(), order, warn, sx, sy, cs);
    }

    /// <summary>Eine Runde fahren; in den Stegbereichen auf Steghöhe anheben.</summary>
    static void Loop(StringBuilder g, List<(double X, double Y)> pts, bool closed, double z, List<(double S, double E)> tabs, double zTab, CamSettings s)
    {
        var seq = closed ? [.. pts, pts[0]] : pts;
        var pos = 0.0; var up = false; var first = true;
        string F() { if (!first) return ""; first = false; return $" F{I(s.Feed)}"; }
        for (var i = 1; i < seq.Count; i++)
        {
            var a = seq[i - 1]; var b = seq[i]; var len = Math.Sqrt(D2(a, b)); if (len < 1e-9) continue;
            // Grenzen der Stege auf diesem Stück
            var cuts = tabs.SelectMany(t => new[] { t.S, t.E }).Where(t => t > pos + 1e-9 && t < pos + len - 1e-9).OrderBy(t => t).ToList();
            foreach (var c in cuts)
            {
                var f = (c - pos) / len; var (x, y) = (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
                g.Append($"G1 X{I(x)} Y{I(y)}{F()}\n");
                var inTab = tabs.Any(t => c + 1e-9 >= t.S && c + 1e-9 < t.E);
                if (inTab && !up) { g.Append($"G1 Z{I(zTab)}\n"); up = true; }
                else if (!inTab && up) { g.Append($"G1 Z{I(z)} F{I(s.Plunge)}\n"); up = false; first = true; }
            }
            g.Append($"G1 X{I(b.X)} Y{I(b.Y)}{F()}\n");
            pos += len;
        }
        if (up) g.Append($"G1 Z{I(z)} F{I(s.Plunge)}\n");
    }

    /// <summary>Stegbereiche entlang der Bahn (Bogenlänge): Stegbreite plus Fräser-Ø, gleichmäßig verteilt, nicht am Startpunkt.</summary>
    static List<(double S, double E)> TabRanges(List<(double X, double Y)> pts, CamSettings s, string name, List<string> warn)
    {
        var total = 0.0; for (var i = 0; i < pts.Count; i++) total += Math.Sqrt(D2(pts[i], pts[(i + 1) % pts.Count]));
        var half = (s.TabW + s.ToolD) / 2;
        if (2 * half * s.Tabs > total * 0.6) { warn.Add($"{name}: zu kurz für {s.Tabs} Haltestege – ohne Stege."); return []; }
        return Enumerable.Range(0, s.Tabs).Select(k => { var c = total * (k + 0.5) / s.Tabs; return (c - half, c + half); }).ToList();
    }

    /// <summary>Versatz eines geschlossenen Zugs um <paramref name="dist"/> (positiv = nach außen). Außenecken rund (Fräser fährt um die Ecke),
    /// Innenecken als Schnittpunkt; Schleifen an Stellen, die enger als der Fräser sind, werden herausgeschnitten. Ergebnis gegen den Uhrzeigersinn.</summary>
    public static List<(double X, double Y)> Offset(IReadOnlyList<(double X, double Y)> poly, double dist, double chord = 0.01)
    {
        var pts = Clean(poly);
        if (pts.Count < 3) return [];
        if (SignedArea(pts) < 0) pts.Reverse();
        var n = pts.Count; var res = new List<(double X, double Y)>(n * 2);
        for (var i = 0; i < n; i++)
        {
            var prev = pts[(i - 1 + n) % n]; var cur = pts[i]; var next = pts[(i + 1) % n];
            var d0 = Unit(cur.X - prev.X, cur.Y - prev.Y); var d1 = Unit(next.X - cur.X, next.Y - cur.Y);
            (double X, double Y) n0 = (d0.Y, -d0.X), n1 = (d1.Y, -d1.X); // rechts = außen bei Gegenuhrzeigersinn
            var cross = d0.X * d1.Y - d0.Y * d1.X; var dot = d0.X * d1.X + d0.Y * d1.Y; var turn = Math.Atan2(cross, dot);
            if (Math.Abs(turn) < 1e-9) { res.Add((cur.X + n0.X * dist, cur.Y + n0.Y * dist)); continue; }
            if (turn * dist > 0)
            {
                // Bogen um die Ecke: Sehnen berühren den Kreis mit Radius dist von außen – die Fräsermitte kommt nie näher als dist an die Ecke
                var m = Math.Max(1, (int)Math.Ceiling(CadContours.Segments(Math.Abs(dist), 2 * Math.PI, chord) * Math.Abs(turn) / (2 * Math.PI)));
                var a0 = Math.Atan2(n0.Y, n0.X); var rr = dist / Math.Cos(turn / (2 * m));
                res.Add((cur.X + n0.X * dist, cur.Y + n0.Y * dist));
                for (var k = 1; k <= m; k++) { var a = a0 + turn * (k - 0.5) / m; res.Add((cur.X + Math.Cos(a) * rr, cur.Y + Math.Sin(a) * rr)); }
                res.Add((cur.X + n1.X * dist, cur.Y + n1.Y * dist));
            }
            else
            {
                var den = 1 + n0.X * n1.X + n0.Y * n1.Y;
                if (den < 1e-6) { res.Add((cur.X + n0.X * dist, cur.Y + n0.Y * dist)); res.Add((cur.X + n1.X * dist, cur.Y + n1.Y * dist)); }
                else res.Add((cur.X + (n0.X + n1.X) / den * dist, cur.Y + (n0.Y + n1.Y) / den * dist));
            }
        }
        res = RemoveLoops(Clean(res));
        if (res.Count < 3 || SignedArea(res) <= 0) return [];
        // nach innen zusammengefallen (Loch enger als der Fräser): die Bahn käme der Kontur näher als dist
        return dist < 0 && MinDistance(res, pts, true) < -dist - Math.Max(chord, 1e-6) ? [] : res;
    }

    /// <summary>Selbstschnitte entfernen: an jedem Schnittpunkt fällt die Teilschleife weg, die falsch herum läuft (bzw. die kleinere).</summary>
    static List<(double X, double Y)> RemoveLoops(List<(double X, double Y)> p)
    {
        for (var guard = 0; guard < 2000 && p.Count >= 4; guard++)
        {
            var hit = false; var n = p.Count;
            for (var i = 0; i < n && !hit; i++)
            {
                var a = p[i]; var b = p[(i + 1) % n];
                double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X), minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
                for (var j = i + 2; j < n && !hit; j++)
                {
                    if (i == 0 && j == n - 1) continue; // Nachbarn über den Anfang
                    var c = p[j]; var d = p[(j + 1) % n];
                    if (Math.Max(c.X, d.X) < minX || Math.Min(c.X, d.X) > maxX || Math.Max(c.Y, d.Y) < minY || Math.Min(c.Y, d.Y) > maxY) continue;
                    if (Intersect(a, b, c, d) is not { } x) continue;
                    var inner = new List<(double X, double Y)> { x }; inner.AddRange(p.Skip(i + 1).Take(j - i));
                    var outer = new List<(double X, double Y)> { x }; outer.AddRange(p.Skip(j + 1)); outer.AddRange(p.Take(i + 1));
                    double ai = SignedArea(inner), ao = SignedArea(outer);
                    p = ai > 0 && ao > 0 ? (ai >= ao ? inner : outer) : ai > ao ? inner : outer;
                    hit = true;
                }
            }
            if (!hit) break;
        }
        return p;
    }

    static (double X, double Y)? Intersect((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y, den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-15) return null;
        double t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / den, u = ((c.X - a.X) * ry - (c.Y - a.Y) * rx) / den;
        const double e = 1e-9;
        return t > e && t < 1 - e && u > e && u < 1 - e ? (a.X + rx * t, a.Y + ry * t) : null;
    }

    /// <summary>Kommt die Fräsermitte der Kontur irgendwo näher als der Radius, würde das Teil verletzt: Kontur auslassen und melden.</summary>
    static bool Gouges(List<(double X, double Y)> path, CadContour c, double r, string name, List<string> warn)
    {
        var min = MinDistance(path, c.Pts, true);
        if (min >= r - 0.001) return false;
        warn.Add($"{name}: hat Stellen enger als der Fräser (die Bahn käme {I(r - min)} mm zu nah) – ausgelassen. Kleineren Fräser wählen.");
        return true;
    }

    /// <summary>Kleinster Abstand der Punkte von <paramref name="path"/> (auch Stückmitten) zum Zug <paramref name="poly"/>.</summary>
    public static double MinDistance(IReadOnlyList<(double X, double Y)> path, IReadOnlyList<(double X, double Y)> poly, bool closed)
    {
        var min = double.MaxValue; var m = poly.Count - (closed ? 0 : 1);
        for (var i = 0; i < path.Count; i++)
        {
            var q = path[i]; var q2 = path[(i + 1) % path.Count]; var mid = ((q.X + q2.X) / 2, (q.Y + q2.Y) / 2);
            for (var j = 0; j < m; j++) { var a = poly[j]; var b = poly[(j + 1) % poly.Count]; min = Math.Min(min, Math.Min(SegDist(q, a, b), SegDist(mid, a, b))); }
        }
        return min;
    }

    public static double SegDist((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        var t = l2 < 1e-18 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        double x = a.X + dx * t - p.X, y = a.Y + dy * t - p.Y; return Math.Sqrt(x * x + y * y);
    }

    public static double SignedArea(IReadOnlyList<(double X, double Y)> p)
    {
        var a = 0.0; for (var i = 0; i < p.Count; i++) { var q = p[i]; var w = p[(i + 1) % p.Count]; a += q.X * w.Y - w.X * q.Y; }
        return a / 2;
    }

    static List<(double X, double Y)> Clean(IReadOnlyList<(double X, double Y)> p)
    {
        var res = new List<(double X, double Y)>(p.Count);
        foreach (var q in p) if (res.Count == 0 || D2(res[^1], q) > 1e-12) res.Add(q);
        while (res.Count > 1 && D2(res[0], res[^1]) <= 1e-12) res.RemoveAt(res.Count - 1);
        return res;
    }

    static (double X, double Y) Unit(double x, double y) { var l = Math.Sqrt(x * x + y * y); return l < 1e-15 ? (1, 0) : (x / l, y / l); }
    static double D2((double X, double Y) a, (double X, double Y) b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
