using System.Globalization;
using System.Text.RegularExpressions;
using Trendo.Core.Cad;

namespace Trendo.Core.Arch;

/// <summary>T-0747: Einstellungen für den DXF-Import in den Grundriss.</summary>
public sealed record PlanDxfOptions
{
    /// <summary>Stärke für Wände, die in der DXF nur als eine Linie (Achse) gezeichnet sind.</summary>
    public double SingleWallCm { get; init; } = 24;
    public double WallHeightCm { get; init; } = 250;
    /// <summary>Zwei parallele Linien mit diesem Abstand gelten als die beiden Seiten einer Wand.</summary>
    public double MinPairCm { get; init; } = 5;
    public double MaxPairCm { get; init; } = 60;
    /// <summary>Ebenen, die Wände werden; null = automatisch (Ebenen mit „Wand“, „Wall“, „Mauer“ im Namen, sonst alle außer Möbel, Maße, Texte, Räume).</summary>
    public IReadOnlyCollection<string>? WallLayers { get; init; }
}

/// <summary>T-0747: Was der DXF-Import in den Plan gebracht hat.</summary>
public sealed record PlanDxfReport(int Walls, int DoubleLineWalls, int SingleLineWalls, int ArcWalls, int Openings, int Rooms, int Labels,
    IReadOnlyList<string> WallLayers, IReadOnlyDictionary<string, int> SkippedLayers, IReadOnlyDictionary<string, int> SkippedKinds, string Units, double OffsetX, double OffsetY)
{
    public string Text
    {
        get
        {
            var parts = new List<string> { $"{Walls} Wände" };
            if (Openings > 0) parts.Add($"{Openings} Türen/Fenster");
            if (Rooms > 0) parts.Add($"{Rooms} Räume");
            if (Labels > 0) parts.Add($"{Labels} Texte");
            var s = string.Join(", ", parts) + $" (Einheit {Units}; Wandebenen: {(WallLayers.Count == 0 ? "–" : string.Join(", ", WallLayers))})";
            if (SkippedLayers.Count > 0) s += ". Nicht als Wand übernommen: " + string.Join(", ", SkippedLayers.Select(k => $"Ebene {k.Key} ({k.Value})"));
            if (SkippedKinds.Count > 0) s += ". Nicht übernommen: " + string.Join(", ", SkippedKinds.Select(k => $"{k.Value}× {CadDxf.KindLabel(k.Key)}")); // T-0747-fix8
            return s + ".";
        }
    }
}

/// <summary>T-0747 (SPEC-01): Ein DXF-Grundriss wird zu bearbeitbaren Wänden, Türen, Fenstern, Räumen und Texten. Gelesen wird über den gemeinsamen
/// Leser <see cref="CadDxf"/> (LINE, LWPOLYLINE, POLYLINE, ARC, CIRCLE, TEXT/MTEXT, Ebenen, $INSUNITS – alles schon in mm).
/// Wände: zwei parallele Linien im Abstand <see cref="PlanDxfOptions.MinPairCm"/>…<see cref="PlanDxfOptions.MaxPairCm"/> werden eine Wand dieser Stärke
/// (Achse in der Mitte), einzelne Linien eine Wand mit <see cref="PlanDxfOptions.SingleWallCm"/>, Bögen und Kreise Bogenwände. Kurze Querstriche an
/// Wandenden fallen weg, Ecken werden auf den Achsschnittpunkt gezogen, Lücken zwischen zwei Wandstücken auf einer Linie werden Tür (mit Schwenkbogen) oder
/// Fenster. Geschlossene Umrisse auf Raum-Ebenen werden Räume (Name aus dem Text darin). Maßstab aus $INSUNITS, y wird gespiegelt (Plan: y nach unten).</summary>
public static class PlanDxfImport
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly Regex WallRx = new(@"wand|wände|waende|wall|mauer|tragend", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex RoomRx = new(@"raum|räume|raeume|room|fläche|flaeche|area|boden", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex TextRx = new(@"text|beschrift|label|anno|schrift", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex OtherRx = new(@"möbel|moebel|furn|mass|maß|bema|dim|schraff|hatch|nord|north|einricht|achse|axis|raster|grid|oeffnung|öffnung|tuer|tür|fenster|door|window|opening|defpoints", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex AreaRx = new(@"^\s*[\d.,]+\s*(m²|m2|qm)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    sealed class Seg(double x1, double y1, double x2, double y2)
    {
        public double X1 = x1, Y1 = y1, X2 = x2, Y2 = y2;
        public double Len => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
        public (double X, double Y) U { get { var l = Math.Max(Len, 1e-12); return ((X2 - X1) / l, (Y2 - Y1) / l); } }
    }

    /// <summary>T-0747-fix: höchstens so viele Elemente liest der Grundriss-Import (sonst Meldung statt minutenlanger Rechnung).</summary>
    public const int MaxEntities = 50_000;

    public static PlanDxfReport Import(FloorPlan plan, CadDxf.ReadResult dxf, PlanDxfOptions? options = null, CancellationToken ct = default)
    {
        var o = options ?? new PlanDxfOptions();
        var d = dxf.Drawing;
        var visible = d.Layers.Where(l => l.Visible).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ents = d.Entities.Where(e => visible.Contains(e.Layer) || d.Layers.All(l => !l.Name.Equals(e.Layer, StringComparison.OrdinalIgnoreCase))).ToList();
        // T-0747-fix6: Ellipsen (vom Leser abgetastet) übernimmt der Grundriss nicht als Wand – sie werden gemeldet statt still in kurze Stücke zu zerfallen
        var skippedKinds = new Dictionary<string, int>(dxf.Skipped);
        var nEll = ents.Count(e => e.Source == "ELLIPSE");
        if (nEll > 0) { ents = ents.Where(e => e.Source != "ELLIPSE").ToList(); skippedKinds["ELLIPSE (nicht als Wand übernommen)"] = nEll; }
        if (ents.Count > MaxEntities) throw new CadError($"Die DXF-Datei hat {ents.Count.ToString("N0", De)} Elemente – zu viele für einen Grundriss (höchstens {MaxEntities.ToString("N0", De)}). Bitte nur die Ebenen mit Wänden, Türen und Räumen exportieren.");
        var layers = ents.Select(e => e.Layer).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Ebenen zuordnen
        List<string> wallLayers;
        if (o.WallLayers is { Count: > 0 } wl) wallLayers = layers.Where(l => wl.Contains(l, StringComparer.OrdinalIgnoreCase)).ToList();
        else
        {
            wallLayers = layers.Where(l => WallRx.IsMatch(l)).ToList();
            if (wallLayers.Count == 0) wallLayers = layers.Where(l => !RoomRx.IsMatch(l) && !TextRx.IsMatch(l) && !OtherRx.IsMatch(l)).ToList();
            if (wallLayers.Count == 0) wallLayers = layers.Where(l => ents.Any(e => e.Layer == l && e.Kind != "text")).ToList();
        }
        bool IsWall(string l) => wallLayers.Contains(l, StringComparer.OrdinalIgnoreCase);
        bool IsRoom(string l) => !IsWall(l) && RoomRx.IsMatch(l);
        bool IsText(string l) => !IsWall(l) && !OtherRx.IsMatch(l) && (TextRx.IsMatch(l) || RoomRx.IsMatch(l));

        // mm → cm, y spiegeln; Lage: leerer Plan → links oben bei 0/0, sonst 1 m rechts neben den vorhandenen Plan
        var used = ents.Where(e => IsWall(e.Layer) || IsRoom(e.Layer) || e.Kind == "text" && IsText(e.Layer)).ToList();
        var pts = used.SelectMany(Extent).ToList();
        if (pts.Count == 0) throw new CadError("In der DXF-Datei sind keine Linien auf Wand-Ebenen. Gefundene Ebenen: " + string.Join(", ", layers) + ".");
        double minX = pts.Min(p => p.X) / 10, maxY = pts.Max(p => p.Y) / 10;
        var b = plan.Bounds();
        double ox = b is { } bb ? bb.MaxX + 100 - minX : -minX, oy = b is { } bb2 ? bb2.MinY + maxY : maxY;
        (double X, double Y) P(double x, double y) => (x / 10 + ox, -y / 10 + oy);

        // Wandlinien sammeln
        var straight = new List<Seg>(); var arcs = new List<(double Cx, double Cy, double R, double A0, double Sweep)>(); // Bögen im Plan: Winkel im Uhrzeigersinn
        foreach (var e in ents.Where(e => IsWall(e.Layer)))
            switch (e.Kind)
            {
                case "linie" when e.Points.Count >= 2: Add(e.Points[0], e.Points[1]); break;
                case "polylinie" when e.Points.Count >= 2:
                    for (var i = 0; i < e.Points.Count - (e.Closed ? 0 : 1); i++)
                    {
                        var p = e.Points[i]; var q = e.Points[(i + 1) % e.Points.Count]; var bulge = p.Length > 2 ? p[2] : 0;
                        if (Math.Abs(bulge) < 1e-9) Add(p, q);
                        else
                        {
                            var th = 4 * Math.Atan(bulge); var (a, c) = (P(p[0], p[1]), P(q[0], q[1])); var ch = Dist(a, c); if (ch < 1e-6) continue;
                            var r = ch / (2 * Math.Sin(Math.Abs(th) / 2));
                            // Mittelpunkt in DXF-Koordinaten, dann gespiegelt
                            double mx = (p[0] + q[0]) / 2, my = (p[1] + q[1]) / 2, dx = q[0] - p[0], dy = q[1] - p[1], chm = Math.Sqrt(dx * dx + dy * dy);
                            var h = r * 10 * Math.Cos(th / 2) * Math.Sign(bulge);
                            var (pcx, pcy) = P(mx - dy / chm * h, my + dx / chm * h);
                            var a0 = Math.Atan2(a.Y - pcy, a.X - pcx); arcs.Add((pcx, pcy, r, a0, -th)); // Spiegeln kehrt die Drehrichtung um
                        }
                    }
                    break;
                case "bogen" when e.R > 0:
                {
                    var (cx, cy) = P(e.Cx, e.Cy); var sw = e.A1 - e.A0; while (sw <= 0) sw += 360;
                    arcs.Add((cx, cy, e.R / 10, -e.A0 * Math.PI / 180, -sw * Math.PI / 180)); break;
                }
                case "kreis" when e.R > 0: { var (cx, cy) = P(e.Cx, e.Cy); arcs.Add((cx, cy, e.R / 10, 0, -2 * Math.PI)); break; }
            }
        void Add(double[] a, double[] c) { var (x1, y1) = P(a[0], a[1]); var (x2, y2) = P(c[0], c[1]); if (Math.Abs(x2 - x1) + Math.Abs(y2 - y1) > 0.05) straight.Add(new Seg(x1, y1, x2, y2)); }

        // 1. Doppellinien zu Wänden: längste zuerst, Partner mit dem kleinsten Abstand.
        // T-0747-fix: Kandidaten aus einem Raster (Zellgröße = größter Wandabstand) statt aus allen Linien; Reihenfolge wie bisher (kleinster Index gewinnt bei Gleichstand).
        var segIndex = new SegGrid(Math.Max(o.MaxPairCm, 1)); for (var i = 0; i < straight.Count; i++) segIndex.Add((straight[i].X1, straight[i].Y1), (straight[i].X2, straight[i].Y2), i);
        var walls = new List<(Seg Axis, double T, bool Pair)>(); var paired = new HashSet<Seg>(); var pad = o.MaxPairCm + 0.02; var nDone = 0;
        foreach (var s in straight.OrderByDescending(s => s.Len))
        {
            if ((++nDone & 255) == 0) ct.ThrowIfCancellationRequested();
            if (paired.Contains(s)) continue;
            var (ux, uy) = s.U; Seg? best = null; double bestD = double.MaxValue, bs = 0, be = 0;
            var near = segIndex.Near(Math.Min(s.X1, s.X2) - pad, Math.Min(s.Y1, s.Y2) - pad, Math.Max(s.X1, s.X2) + pad, Math.Max(s.Y1, s.Y2) + pad); near.Sort();
            foreach (var t in near.Select(k => straight[k]))
            {
                if (t == s || paired.Contains(t)) continue;
                var (vx, vy) = t.U; if (Math.Abs(ux * vy - uy * vx) > 0.01) continue; // nicht parallel
                var dist = Math.Abs((t.X1 - s.X1) * -uy + (t.Y1 - s.Y1) * ux);
                if (dist < o.MinPairCm - 0.01 || dist > o.MaxPairCm + 0.01) continue;
                double t1 = (t.X1 - s.X1) * ux + (t.Y1 - s.Y1) * uy, t2 = (t.X2 - s.X1) * ux + (t.Y2 - s.Y1) * uy;
                double lo = Math.Max(0, Math.Min(t1, t2)), hi = Math.Min(s.Len, Math.Max(t1, t2));
                if (hi - lo < Math.Max(dist, 0.5 * Math.Min(s.Len, t.Len))) continue; // zu wenig Überdeckung
                if (dist < bestD) { bestD = dist; best = t; bs = lo; be = hi; }
            }
            if (best is null) continue;
            paired.Add(s); paired.Add(best);
            var sgn = (best.X1 - s.X1) * -uy + (best.Y1 - s.Y1) * ux >= 0 ? 1 : -1; var (nx, ny) = (-uy * sgn * bestD / 2, ux * sgn * bestD / 2);
            walls.Add((new Seg(s.X1 + ux * bs + nx, s.Y1 + uy * bs + ny, s.X1 + ux * be + nx, s.Y1 + uy * be + ny), Math.Round(bestD, 2), true));
        }
        ct.ThrowIfCancellationRequested();
        // 2. Querstriche (Wandabschluss, Leibung) an gepaarten Linien fallen weg; der Rest sind Achsen-Wände (Enden im 1-cm-Raster nachgeschlagen)
        var endPts = straight.Where(paired.Contains).SelectMany(s => new[] { (s.X1, s.Y1), (s.X2, s.Y2) }).GroupBy(e => ((long)Math.Floor(e.Item1), (long)Math.Floor(e.Item2))).ToDictionary(g => g.Key, g => g.ToList());
        bool AtEnd(double x, double y)
        {
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (endPts.TryGetValue(((long)Math.Floor(x) + dx, (long)Math.Floor(y) + dy), out var l) && l.Any(e => Math.Abs(e.Item1 - x) < 1 && Math.Abs(e.Item2 - y) < 1)) return true;
            return false;
        }
        var caps = 0;
        foreach (var s in straight.Where(s => !paired.Contains(s)))
        {
            if (paired.Count > 0 && s.Len <= o.MaxPairCm + 1 && AtEnd(s.X1, s.Y1) && AtEnd(s.X2, s.Y2)) { caps++; continue; }
            if (s.Len >= 5) walls.Add((s, o.SingleWallCm, false));
        }
        // 3. Wandstücke auf einer Linie mit Lücke → eine Wand mit Öffnung (Tür, wenn ein Schwenkbogen dazu passt, sonst Fenster).
        // T-0747-fix: gleiche Reihenfolge wie das frühere „immer wieder von vorn suchen“ (kleinstes Paar zuerst), aber ohne Neustart: nach einer
        // Zusammenführung können sich nur Paare mit der veränderten Wand ändern; Kandidaten kommen aus einem Raster mit Versionsnummern.
        var doorArcs = ents.Where(e => e.Kind == "bogen" && !IsWall(e.Layer)).Select(e => (C: P(e.Cx, e.Cy), R: e.R / 10)).ToList();
        var openings = new List<(double X, double Y, double W, string Kind, (double X, double Y) Hinge, (double X, double Y) Face)>();
        var alive = Enumerable.Repeat(true, walls.Count).ToList(); var ver = new int[walls.Count];
        var wallIndex = new SegGrid(100); var wallVer = new List<int>();
        void IndexWall(int i) { ver[i]++; var a = walls[i].Axis; wallIndex.Add((a.X1, a.Y1), (a.X2, a.Y2), i); wallVer.Add(ver[i]); }
        for (var i = 0; i < walls.Count; i++) { ver[i] = -1; IndexWall(i); }
        List<int> Candidates(int i, double margin)
        {
            var a = walls[i].Axis; var res = new List<int>();
            foreach (var e in wallIndex.Near(Math.Min(a.X1, a.X2) - margin, Math.Min(a.Y1, a.Y2) - margin, Math.Max(a.X1, a.X2) + margin, Math.Max(a.Y1, a.Y2) + margin))
            {
                var j = wallIndex.Segs[e].Tag; if (alive[j] && wallVer[e] == ver[j] && j != i) res.Add(j);
            }
            res = res.Distinct().ToList(); res.Sort(); return res;
        }
        bool TryMerge(int i, int j)
        {
            var (a, ta, pa) = walls[i]; var (c, tc, pc) = walls[j];
            if (Math.Abs(ta - tc) > 1) return false;
            var (ux, uy) = a.U; var (vx, vy) = c.U; if (Math.Abs(ux * vy - uy * vx) > 0.01) return false;
            if (Math.Abs((c.X1 - a.X1) * -uy + (c.Y1 - a.Y1) * ux) > 1 || Math.Abs((c.X2 - a.X1) * -uy + (c.Y2 - a.Y1) * ux) > 1) return false; // nicht auf einer Linie
            double c1 = (c.X1 - a.X1) * ux + (c.Y1 - a.Y1) * uy, c2 = (c.X2 - a.X1) * ux + (c.Y2 - a.Y1) * uy;
            double cLo = Math.Min(c1, c2), cHi = Math.Max(c1, c2);
            double gapS, gapE;
            if (cLo >= a.Len) { gapS = a.Len; gapE = cLo; } else if (cHi <= 0) { gapS = cHi; gapE = 0; } else return false;
            var gap = gapE - gapS; if (gap < 40 || gap > 400) return false;
            double lo = Math.Min(0, cLo), hi = Math.Max(a.Len, cHi);
            var axis = new Seg(a.X1 + ux * lo, a.Y1 + uy * lo, a.X1 + ux * hi, a.Y1 + uy * hi);
            (double X, double Y) At(double t) => (a.X1 + ux * t, a.Y1 + uy * t);
            var (gs, ge) = (At(gapS), At(gapE)); var mid = At((gapS + gapE) / 2);
            var arc = doorArcs.Where(r => Math.Abs(r.R - gap) <= Math.Max(5, gap * 0.15) && Math.Min(Dist(r.C, gs), Dist(r.C, ge)) <= ta / 2 + 3).OrderBy(r => Math.Min(Dist(r.C, gs), Dist(r.C, ge))).FirstOrDefault();
            var isDoor = arc.R > 0;
            var hinge = isDoor ? (Dist(arc.C, gs) <= Dist(arc.C, ge) ? gs : ge) : gs;
            openings.Add((mid.X, mid.Y, Math.Round(gap, 1), isDoor ? "tuer" : "fenster", hinge, isDoor ? arc.C : hinge));
            walls[i] = (axis, ta, pa || pc); alive[j] = false; IndexWall(i);
            return true;
        }
        for (var i = 0; i < walls.Count; i++)
        {
            if (!alive[i]) continue;
            if ((i & 63) == 0) ct.ThrowIfCancellationRequested();
            var cur = i;
            while (true)
            {
                // zuerst: geht eine frühere Wand jetzt mit der veränderten Wand zusammen? (kleinstes Paar zuerst, wie beim früheren Neustart)
                var back = Candidates(cur, 402).Where(x => x < cur).FirstOrDefault(x => TryMerge(x, cur), -1);
                if (back >= 0) { cur = back; continue; }
                var fwd = Candidates(cur, 402).Where(x => x > cur).FirstOrDefault(x => TryMerge(cur, x), -1);
                if (fwd < 0) break;
            }
        }
        walls = walls.Where((_, k) => alive[k]).ToList();
        ct.ThrowIfCancellationRequested();
        // 4. Ecken: jedes Wandende zum nächstgelegenen Schnitt mit der Achse einer anderen Wand ziehen (höchstens halbe Wandstärke + 2 cm); Kandidaten aus dem Raster
        var cornerIndex = new SegGrid(100); for (var i = 0; i < walls.Count; i++) cornerIndex.Add((walls[i].Axis.X1, walls[i].Axis.Y1), (walls[i].Axis.X2, walls[i].Axis.Y2), i);
        var maxT = walls.Count == 0 ? 0 : walls.Max(w => w.T);
        foreach (var (a, ta, _) in walls)
            for (var end = 0; end < 2; end++)
            {
                var (ux, uy) = a.U; double best = double.MaxValue, move = 0;
                var (ex, ey) = end == 0 ? (a.X1, a.Y1) : (a.X2, a.Y2); var pad2 = 2 * (Math.Max(ta, maxT) / 2 + 2) + 120; // Wandenden verschieben sich höchstens um die Reichweite
                var near = cornerIndex.Near(ex - pad2, ey - pad2, ex + pad2, ey + pad2).Select(e => cornerIndex.Segs[e].Tag).Distinct().ToList(); near.Sort();
                foreach (var (c, tc, _) in near.Select(k => walls[k]))
                {
                    if (c == a) continue;
                    var (vx, vy) = c.U; var den = ux * vy - uy * vx; if (Math.Abs(den) < 0.05) continue;
                    var s = ((c.X1 - a.X1) * vy - (c.Y1 - a.Y1) * vx) / den; // Lage des Schnittpunkts auf a
                    var t = ((c.X1 - a.X1) * uy - (c.Y1 - a.Y1) * ux) / den;  // Lage auf c
                    var reach = Math.Max(ta, tc) / 2 + 2;
                    if (t < -reach || t > c.Len + reach) continue;
                    var k = end == 0 ? s : s - a.Len;
                    if (Math.Abs(k) <= reach && Math.Abs(k) < best) { best = Math.Abs(k); move = k; }
                }
                if (best == double.MaxValue || Math.Abs(move) < 1e-6) continue;
                if (end == 0) { a.X1 += ux * move; a.Y1 += uy * move; } else { a.X2 += ux * move; a.Y2 += uy * move; }
            }

        // In den Plan übernehmen
        int nWalls = 0, nPair = 0, nSingle = 0, nArc = 0, nOpen = 0, nRooms = 0, nLabels = 0;
        foreach (var (s, t, pair) in walls)
        {
            if (s.Len < 5) continue;
            plan.AddWall(R(s.X1), R(s.Y1), R(s.X2), R(s.Y2), Math.Clamp(t, 5, 100), o.WallHeightCm); nWalls++;
            if (pair) nPair++; else nSingle++;
        }
        foreach (var (cx, cy, r, a0, sweep) in arcs)
        {
            var pieces = Math.Abs(sweep) > Math.PI + 1e-6 ? 2 : 1; // mehr als ein Halbkreis (auch Vollkreis): in zwei Bogenwände teilen
            for (var k = 0; k < pieces; k++)
            {
                double s0 = a0 + sweep * k / pieces, s1 = a0 + sweep * (k + 1) / pieces, sm = (s0 + s1) / 2;
                (double X, double Y) Pt(double an) => (cx + r * Math.Cos(an), cy + r * Math.Sin(an));
                var (ax, ay) = Pt(s0); var (bx, by) = Pt(s1); var (mx, my) = Pt(sm);
                var ch = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); if (ch < 20) continue;
                double ux = (bx - ax) / ch, uy = (by - ay) / ch, nx = uy, ny = -ux; // „links“ wie in FloorPlan.AddArcWall
                var bulge = (mx - (ax + bx) / 2) * nx + (my - (ay + by) / 2) * ny;
                bulge = Math.Clamp(bulge, -ch / 2, ch / 2);
                plan.AddArcWall(R(ax), R(ay), R(bx), R(by), bulge, o.SingleWallCm, o.WallHeightCm); nArc++;
            }
        }
        foreach (var (x, y, w, kind, hinge, face) in openings)
        {
            var op = plan.AddOpening(kind, x, y, w, 30); if (op is null) continue;
            if (kind == "tuer" && plan.WallOf(op.WallId) is { } ow)
            {
                var (ux, uy) = ow.Dir; var (nx, ny) = (-uy, ux);
                op.HingeLeft = (hinge.X - x) * ux + (hinge.Y - y) * uy < 0;
                op.Inward = (face.X - hinge.X) * nx + (face.Y - hinge.Y) * ny >= 0;
            }
            nOpen++;
        }
        // Räume aus geschlossenen Umrissen auf Raum-Ebenen, Name aus dem Text darin
        var texts = ents.Where(e => e.Kind == "text" && e.Points.Count > 0 && !string.IsNullOrWhiteSpace(e.Text) && (IsText(e.Layer) || IsWall(e.Layer) || IsRoom(e.Layer)))
            .Select(e => (P: P(e.Points[0][0], e.Points[0][1]), e.Text, H: e.Height / 10, e.Rotation)).ToList();
        var usedText = new HashSet<int>();
        foreach (var c in CadContours.Build(ents.Where(e => IsRoom(e.Layer)), 5, 1, ct).Where(c => c.Closed))
        {
            var poly = c.Pts.Select(p => P(p.X, p.Y)).ToList();
            var ti = Enumerable.Range(0, texts.Count).Where(k => !usedText.Contains(k) && !AreaRx.IsMatch(texts[k].Text) && Geo.Inside(poly, texts[k].P.X, texts[k].P.Y)).DefaultIfEmpty(-1).First();
            var name = ti >= 0 ? texts[ti].Text.Trim() : $"Raum {nRooms + 1}";
            try { plan.AddPolygonRoom(poly, name, KindOf(name)); nRooms++; if (ti >= 0) usedText.Add(ti); }
            catch (ArgumentException) { }
            for (var k = 0; k < texts.Count; k++) if (AreaRx.IsMatch(texts[k].Text) && Geo.Inside(poly, texts[k].P.X, texts[k].P.Y)) usedText.Add(k); // Flächenangabe rechnet Trendo selbst
        }
        for (var k = 0; k < texts.Count; k++)
        {
            if (usedText.Contains(k) || AreaRx.IsMatch(texts[k].Text)) continue;
            plan.Labels.Add(new PlanLabel { X = R(texts[k].P.X), Y = R(texts[k].P.Y), Text = texts[k].Text.Trim(), Size = Math.Clamp(texts[k].H, 5, 200), Rot = -texts[k].Rotation }); nLabels++;
        }
        // Bericht: nicht verwendete Ebenen
        var skippedLayers = ents.Where(e => !IsWall(e.Layer) && !IsRoom(e.Layer) && !(e.Kind == "text" && IsText(e.Layer)))
            .GroupBy(e => e.Layer, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count());
        return new PlanDxfReport(nWalls + nArc, nPair, nSingle, nArc, nOpen, nRooms, nLabels, wallLayers, skippedLayers, skippedKinds, dxf.UnitName, Math.Round(ox, 1), Math.Round(oy, 1));

        static double R(double v) => Math.Round(v, 2);
        static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    /// <summary>Raumart aus dem Namen (Bad, Küche, Schlafen …).</summary>
    public static string KindOf(string name)
    {
        var n = name.ToLower(CultureInfo.GetCultureInfo("de-DE"));
        return n switch
        {
            _ when n.Contains("bad") || n.Contains("wc") || n.Contains("dusche") => "bad",
            _ when n.Contains("küche") || n.Contains("kueche") || n.Contains("kochen") => "kueche",
            _ when n.Contains("schlaf") => "schlafen",
            _ when n.Contains("kind") => "kind",
            _ when n.Contains("flur") || n.Contains("diele") || n.Contains("gang") || n.Contains("eingang") => "flur",
            _ when n.Contains("arbeit") || n.Contains("büro") || n.Contains("buero") => "arbeiten",
            _ when n.Contains("essen") => "essen",
            _ when n.Contains("abstell") || n.Contains("lager") || n.Contains("speis") => "abstell",
            _ when n.Contains("balkon") || n.Contains("terrasse") || n.Contains("loggia") => "balkon",
            _ when n.Contains("keller") => "keller",
            _ => "wohnen",
        };
    }

    /// <summary>Punkte, die ein Element belegt (für die Lage im Plan).</summary>
    static IEnumerable<(double X, double Y)> Extent(CadEntity e) => e.Kind switch
    {
        "kreis" or "bogen" => [(e.Cx - e.R, e.Cy - e.R), (e.Cx + e.R, e.Cy + e.R)],
        _ => e.Points.Select(p => (p[0], p[1])),
    };
}
