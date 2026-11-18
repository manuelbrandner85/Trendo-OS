namespace Trendo.Core.Cad;

/// <summary>T-0747: Ein Linienzug aus einer Zeichnung – geschlossen (Umriss, Loch) oder offen (Gravur, Hilfslinie). Maße wie in der Zeichnung.</summary>
public sealed record CadContour(List<(double X, double Y)> Pts, bool Closed, string Layer)
{
    /// <summary>Fläche mit Vorzeichen (positiv = gegen den Uhrzeigersinn, y nach oben).</summary>
    public double SignedArea
    {
        get
        {
            var a = 0.0;
            for (var i = 0; i < Pts.Count; i++) { var p = Pts[i]; var q = Pts[(i + 1) % Pts.Count]; a += p.X * q.Y - q.X * p.Y; }
            return a / 2;
        }
    }

    public double Length => Enumerable.Range(0, Closed ? Pts.Count : Math.Max(0, Pts.Count - 1)).Sum(i => { var p = Pts[i]; var q = Pts[(i + 1) % Pts.Count]; return Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y)); });

    public (double MinX, double MinY, double MaxX, double MaxY) Box => (Pts.Min(p => p.X), Pts.Min(p => p.Y), Pts.Max(p => p.X), Pts.Max(p => p.Y));

    /// <summary>Liegt der Punkt innerhalb (gerade/ungerade-Regel)? Nur für geschlossene Züge sinnvoll.</summary>
    public bool Contains(double x, double y)
    {
        var inside = false;
        for (int i = 0, j = Pts.Count - 1; i < Pts.Count; j = i++)
        {
            var (xi, yi) = Pts[i]; var (xj, yj) = Pts[j];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }
}

/// <summary>T-0747: Aus den Elementen einer Zeichnung (DXF über <see cref="CadDxf"/>, SVG über <see cref="CadSvg"/>) werden Linienzüge.
/// Lose Linien und Bögen, deren Enden sich treffen, werden zu einem Zug verkettet – so wird aus vier LINE-Elementen ein geschlossenes Rechteck.
/// Gemeinsam genutzt vom Wohnungsplaner (Räume) und vom Fräsen (Konturen).</summary>
public static class CadContours
{
    /// <summary>Linienzüge bilden. <paramref name="tol"/>: Enden, die näher als das beieinanderliegen, gelten als verbunden;
    /// <paramref name="chord"/>: höchste Abweichung beim Zerlegen von Kreisen und Bögen in gerade Stücke (gleiche Einheit wie die Zeichnung).</summary>
    public static List<CadContour> Build(IEnumerable<CadEntity> entities, double tol = 0.01, double chord = 0.01, CancellationToken ct = default)
    {
        var closed = new List<CadContour>(); var open = new List<CadContour>();
        foreach (var e in entities)
        {
            ct.ThrowIfCancellationRequested();
            switch (e.Kind)
            {
                case "kreis" when e.R > 0:
                    closed.Add(new CadContour(e.Outline(Segments(e.R, 2 * Math.PI, chord)), true, e.Layer)); break;
                case "bogen" when e.R > 0:
                    open.Add(new CadContour(e.Outline(Segments(e.R, 2 * Math.PI, chord)), false, e.Layer)); break;
                case "linie" when e.Points.Count >= 2:
                    open.Add(new CadContour([(e.Points[0][0], e.Points[0][1]), (e.Points[1][0], e.Points[1][1])], false, e.Layer)); break;
                case "polylinie" when e.Points.Count >= 2:
                {
                    var r = e.Points.Select(p => p.Length > 2 && Math.Abs(p[2]) > 1e-9 ? BulgeRadius(p, e) : 0).DefaultIfEmpty(0).Max();
                    var pts = Dedupe(e.Outline(r > 0 ? Segments(r, 2 * Math.PI, chord) : 48), tol);
                    var isClosed = e.Closed || pts.Count > 2 && Near(pts[0], pts[^1], tol);
                    if (isClosed && pts.Count > 2 && Near(pts[0], pts[^1], tol)) pts.RemoveAt(pts.Count - 1);
                    if (isClosed && pts.Count >= 3) closed.Add(new CadContour(pts, true, e.Layer)); else if (pts.Count >= 2) open.Add(new CadContour(pts, false, e.Layer));
                    break;
                }
            }
        }
        closed.AddRange(Chain(open, tol, ct));
        return closed.Where(c => c.Closed || c.Pts.Count >= 2).ToList();
    }

    /// <summary>Offene Züge mit gemeinsamen Enden zusammenhängen (auch umgedreht); schließt sich eine Kette, wird sie geschlossen.
    /// T-0747-fix: Enden liegen in einem Raster (Zellgröße 2 × Toleranz) – gesucht wird nur in den Nachbarzellen statt in allen Stücken
    /// (10 000 Linien: Millisekunden statt quadratischer Laufzeit). Angehängt wird jeweils das Stück mit dem kleinsten Index, erst am Ende, dann am Anfang.</summary>
    public static List<CadContour> Chain(List<CadContour> pieces, double tol, CancellationToken ct = default)
    {
        var res = new List<CadContour>(); var used = new bool[pieces.Count];
        var cell = Math.Max(tol, 1e-9) * 2;
        (long, long) C((double X, double Y) p) => ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell));
        var map = new Dictionary<(long, long), List<int>>();
        for (var k = 0; k < pieces.Count; k++)
            foreach (var c in new[] { C(pieces[k].Pts[0]), C(pieces[k].Pts[^1]) }.Distinct())
            {
                if (!map.TryGetValue(c, out var l)) map[c] = l = [];
                l.Add(k);
            }
        int Find((double X, double Y) at, string layer)
        {
            var (cx, cy) = C(at); var best = int.MaxValue;
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (map.TryGetValue((cx + dx, cy + dy), out var l))
                        foreach (var k in l)
                            if (k < best && !used[k] && pieces[k].Layer == layer && (Near(at, pieces[k].Pts[0], tol) || Near(at, pieces[k].Pts[^1], tol))) best = k;
            return best == int.MaxValue ? -1 : best;
        }
        for (var s = 0; s < pieces.Count; s++)
        {
            if (used[s]) continue; used[s] = true;
            if ((s & 1023) == 0) ct.ThrowIfCancellationRequested();
            var chain = new List<(double X, double Y)>(pieces[s].Pts); var layer = pieces[s].Layer;
            while (!(chain.Count > 2 && Near(chain[0], chain[^1], tol)))
            {
                var k = Find(chain[^1], layer);
                if (k >= 0)
                {
                    var p = pieces[k].Pts; used[k] = true;
                    if (Near(chain[^1], p[0], tol)) chain.AddRange(p.Skip(1)); else chain.AddRange(Enumerable.Reverse(p).Skip(1));
                    continue;
                }
                k = Find(chain[0], layer);
                if (k >= 0)
                {
                    var p = pieces[k].Pts; used[k] = true;
                    if (Near(chain[0], p[^1], tol)) chain.InsertRange(0, p.Take(p.Count - 1)); else chain.InsertRange(0, Enumerable.Reverse(p).Take(p.Count - 1));
                    continue;
                }
                break;
            }
            if (chain.Count > 3 && Near(chain[0], chain[^1], tol)) { chain.RemoveAt(chain.Count - 1); res.Add(new CadContour(chain, true, layer)); }
            else res.Add(new CadContour(chain, false, layer));
        }
        return res;
    }

    /// <summary>Teilstücke je Vollkreis, damit die Sehne höchstens <paramref name="chord"/> vom Bogen abweicht (24 bis 1440).</summary>
    public static int Segments(double r, double sweepRad, double chord)
    {
        if (r <= chord) return 24;
        var step = 2 * Math.Acos(1 - chord / r);
        return Math.Clamp((int)Math.Ceiling(2 * Math.PI / step), 24, 1440);
    }

    static double BulgeRadius(double[] p, CadEntity e)
    {
        var i = e.Points.IndexOf(p); var q = e.Points[(i + 1) % e.Points.Count];
        var c = Math.Sqrt((q[0] - p[0]) * (q[0] - p[0]) + (q[1] - p[1]) * (q[1] - p[1])); var th = 4 * Math.Atan(Math.Abs(p[2]));
        return th < 1e-9 ? 0 : c / (2 * Math.Sin(th / 2));
    }

    static List<(double X, double Y)> Dedupe(List<(double X, double Y)> pts, double tol)
    {
        var res = new List<(double X, double Y)>(pts.Count);
        foreach (var p in pts) if (res.Count == 0 || !Near(res[^1], p, tol * 0.01)) res.Add(p);
        return res;
    }

    public static bool Near((double X, double Y) a, (double X, double Y) b, double tol) => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;
}
