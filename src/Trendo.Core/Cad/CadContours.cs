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
    public static List<CadContour> Build(IEnumerable<CadEntity> entities, double tol = 0.01, double chord = 0.01, CancellationToken ct = default) => Build(entities, tol, chord, null, ct);

    /// <summary>Wie oben; <paramref name="notes"/> bekommt Hinweise beim Verketten (mehrdeutige Spalten).</summary>
    public static List<CadContour> Build(IEnumerable<CadEntity> entities, double tol, double chord, List<string>? notes, CancellationToken ct = default)
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
                    if (isClosed && pts.Count > 2 && Near(pts[0], pts[^1], DupTol)) pts.RemoveAt(pts.Count - 1); // T-0747-fix2: nur doppelte Punkte weg – ein echter Endpunkt in Spaltnähe bleibt (sonst fehlt ein Bogenstück)
                    if (isClosed && pts.Count >= 3) closed.Add(new CadContour(pts, true, e.Layer)); else if (pts.Count >= 2) open.Add(new CadContour(pts, false, e.Layer));
                    break;
                }
            }
        }
        closed.AddRange(Chain(open, tol, notes, ct));
        return closed.Where(c => c.Closed || c.Pts.Count >= 2).ToList();
    }

    /// <summary>Offene Züge mit gemeinsamen Enden zusammenhängen (auch umgedreht); schließt sich eine Kette, wird sie geschlossen.</summary>
    public static List<CadContour> Chain(List<CadContour> pieces, double tol, CancellationToken ct = default) => Chain(pieces, tol, null, ct);

    /// <summary>T-0747-fix3 (dritte Prüfung): Verketten in drei Schritten, die wahre Geometrie bleibt dabei unverändert –
    /// 1. nur exakt passende Enden (≤ 1 µm) verbinden; 2. eine Kette, deren eigene Enden höchstens <paramref name="tol"/> auseinanderliegen, schließen
    /// (der Spalt wird eine eigene kurze Strecke, beide Endpunkte bleiben); 3. offene Ketten untereinander über einen Spalt ≤ <paramref name="tol"/>
    /// verbinden, kürzester Spalt zuerst (echter Abstand). Liegt ein zweites Ende etwa gleich nah, wird nicht überbrückt und das in
    /// <paramref name="notes"/> gemeldet. Geschlossene Ketten werden nie mit anderen verbunden.</summary>
    public static List<CadContour> Chain(List<CadContour> pieces, double tol, List<string>? notes, CancellationToken ct = default)
    {
        var lim = tol + 1e-9;
        // 1. exakt verbinden (Raster der Enden)
        var used = new bool[pieces.Count];
        var cell = Math.Max(tol, 1e-6) * 2;
        (long, long) C((double X, double Y) p) => ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell));
        var map = new Dictionary<(long, long), List<int>>();
        for (var k = 0; k < pieces.Count; k++)
            foreach (var c in new[] { C(pieces[k].Pts[0]), C(pieces[k].Pts[^1]) }.Distinct())
            {
                if (!map.TryGetValue(c, out var l)) map[c] = l = [];
                l.Add(k);
            }
        int Exact((double X, double Y) at, string layer)
        {
            var (cx, cy) = C(at); var best = int.MaxValue;
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (map.TryGetValue((cx + dx, cy + dy), out var l))
                        foreach (var k in l)
                            if (k < best && !used[k] && pieces[k].Layer == layer && (Dist(at, pieces[k].Pts[0]) <= DupTol || Dist(at, pieces[k].Pts[^1]) <= DupTol)) best = k;
            return best == int.MaxValue ? -1 : best;
        }
        var chains = new List<(List<(double X, double Y)> Pts, string Layer, bool Closed)>();
        for (var s = 0; s < pieces.Count; s++)
        {
            if (used[s]) continue; used[s] = true;
            if ((s & 1023) == 0) ct.ThrowIfCancellationRequested();
            var chain = new List<(double X, double Y)>(pieces[s].Pts); var layer = pieces[s].Layer;
            while (!(chain.Count > 2 && Dist(chain[0], chain[^1]) <= DupTol))
            {
                var k = Exact(chain[^1], layer);
                if (k >= 0)
                {
                    var p = pieces[k].Pts; used[k] = true;
                    if (Dist(chain[^1], p[0]) <= DupTol) chain.AddRange(p.Skip(1)); else chain.AddRange(Enumerable.Reverse(p).Skip(1));
                    continue;
                }
                k = Exact(chain[0], layer);
                if (k >= 0)
                {
                    var p = pieces[k].Pts; used[k] = true;
                    if (Dist(chain[0], p[^1]) <= DupTol) chain.InsertRange(0, p.Take(p.Count - 1)); else chain.InsertRange(0, Enumerable.Reverse(p).Take(p.Count - 1));
                    continue;
                }
                break;
            }
            if (chain.Count > 3 && Dist(chain[0], chain[^1]) <= DupTol) { chain.RemoveAt(chain.Count - 1); chains.Add((chain, layer, true)); }
            else chains.Add((chain, layer, false));
        }
        // 2. + 3. Spalten überbrücken, bis sich nichts mehr ändert
        var open = chains.Where(c => !c.Closed).Select(c => (Pts: c.Pts, c.Layer)).ToList();
        var done = chains.Where(c => c.Closed).Select(c => new CadContour(c.Pts, true, c.Layer)).ToList();
        var reported = new HashSet<(long, long)>();
        for (var round = 0; round < 100_000; round++)
        {
            ct.ThrowIfCancellationRequested();
            // 2. eigene Enden nah beieinander → schließen (Spalt wird Strecke)
            for (var i = open.Count - 1; i >= 0; i--)
                if (open[i].Pts.Count >= 3 && Dist(open[i].Pts[0], open[i].Pts[^1]) <= lim) { done.Add(new CadContour(open[i].Pts, true, open[i].Layer)); open.RemoveAt(i); }
            // 3. kürzester eindeutiger Spalt zwischen zwei offenen Ketten
            var ends = new List<(int Chain, bool Last, (double X, double Y) P)>();
            for (var i = 0; i < open.Count; i++) { ends.Add((i, false, open[i].Pts[0])); ends.Add((i, true, open[i].Pts[^1])); }
            var emap = new Dictionary<(long, long), List<int>>();
            for (var e = 0; e < ends.Count; e++) { var c = C(ends[e].P); if (!emap.TryGetValue(c, out var l)) emap[c] = l = []; l.Add(e); }
            IEnumerable<int> Around((double X, double Y) p)
            {
                var (cx, cy) = C(p);
                for (var dx = -1L; dx <= 1; dx++) for (var dy = -1L; dy <= 1; dy++) if (emap.TryGetValue((cx + dx, cy + dy), out var l)) foreach (var e in l) yield return e;
            }
            var pairs = new List<(double D, int A, int B)>();
            for (var a = 0; a < ends.Count; a++)
                foreach (var b in Around(ends[a].P))
                    if (b > a && ends[b].Chain != ends[a].Chain && open[ends[a].Chain].Layer == open[ends[b].Chain].Layer && Dist(ends[a].P, ends[b].P) is var d && d <= lim) pairs.Add((d, a, b));
            if (pairs.Count == 0) break;
            pairs.Sort((x, y) => x.D.CompareTo(y.D));
            var merged = false;
            foreach (var (d, a, b) in pairs)
            {
                // etwa gleich nah: ein anderes Ende (andere Kette) liegt an a oder b höchstens d + tol/2 entfernt → nicht raten
                var amb = Around(ends[a].P).Concat(Around(ends[b].P)).Any(e => e != a && e != b && ends[e].Chain != ends[a].Chain && ends[e].Chain != ends[b].Chain
                    && open[ends[e].Chain].Layer == open[ends[a].Chain].Layer && Math.Min(Dist(ends[e].P, ends[a].P), Dist(ends[e].P, ends[b].P)) <= d + tol / 2);
                if (amb)
                {
                    var key = ((long)Math.Round(ends[a].P.X * 1000), (long)Math.Round(ends[a].P.Y * 1000));
                    if (reported.Add(key)) notes?.Add($"Mehrdeutiger Spalt bei X {ends[a].P.X.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} Y {ends[a].P.Y.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))}: mehrere Enden liegen etwa gleich nah – nicht verbunden.");
                    continue;
                }
                var (ca, cb) = (open[ends[a].Chain], open[ends[b].Chain]);
                var pa = ends[a].Last ? ca.Pts : Enumerable.Reverse(ca.Pts).ToList(); // endet am Ende a
                var pb = ends[b].Last ? Enumerable.Reverse(cb.Pts).ToList() : cb.Pts; // beginnt am Ende b
                var joined = new List<(double X, double Y)>(pa.Count + pb.Count); joined.AddRange(pa); joined.AddRange(pb); // beide Endpunkte bleiben, der Spalt ist eine Strecke
                var (hi, lo) = ends[a].Chain > ends[b].Chain ? (ends[a].Chain, ends[b].Chain) : (ends[b].Chain, ends[a].Chain);
                open.RemoveAt(hi); open.RemoveAt(lo); open.Add((joined, ca.Layer));
                merged = true; break;
            }
            if (!merged) break;
        }
        done.AddRange(open.Select(o => new CadContour(o.Pts, false, o.Layer)));
        return done;
    }

    static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Teilstücke je Vollkreis, damit die Sehne höchstens <paramref name="chord"/> vom Bogen abweicht (mindestens 24).
    /// T-0747-fix2: keine feste Obergrenze mehr (vorher 1440 – dann galt die Toleranz nur bis R ≈ 420 mm bei 0,001 mm); Sicherheitsgrenze nur über die
    /// Sehnenlänge (nicht kürzer als 0,05 mm) und höchstens 4 Mio. Stücke.</summary>
    public static int Segments(double r, double sweepRad, double chord)
    {
        if (r <= chord) return 24;
        var step = 4 * Math.Asin(Math.Sqrt(chord / (2 * r))); // = 2·acos(1 − chord/r), genauer bei kleinen Werten
        var n = Math.Ceiling(2 * Math.PI / step);
        var byLength = Math.Ceiling(2 * Math.PI * r / 0.05);
        return (int)Math.Clamp(Math.Min(n, byLength), 24, 4_000_000);
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

    /// <summary>T-0747-fix2: Punkte, die näher als das beieinanderliegen, gelten als doppelt.</summary>
    const double DupTol = 1e-6;

    /// <summary>T-0747-fix3: echter Abstand (vorher je Achse) mit kleiner Reserve gegen Rundung (0,1 + 1e-9).</summary>
    public static bool Near((double X, double Y) a, (double X, double Y) b, double tol) => Dist(a, b) <= tol + 1e-9;
}
