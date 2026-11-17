using System.Globalization;
using System.Text.Json.Nodes;
using Trendo.Core.Kreativ;

namespace Trendo.Core;

/// <summary>T-0745: Punkt eines frei bearbeiteten Schnittteils. Lage und Bézier-Griffe kommen aus dem Vektor-Kern (<see cref="VNode"/>).
/// <paramref name="Edge"/>: An diesem Punkt beginnt die benannte Kante (Saum, Seitennaht …) – daran hängen Nähte, Nahtzugabe und 3D-Anprobe.</summary>
public sealed record PatNode(VNode N, string? Edge = null)
{
    public double X => N.X;
    public double Y => N.Y;
}

/// <summary>T-0745 (SPEC-08): Schnittteile frei bearbeiten wie in Schnitt-Programmen – Punkte verschieben, einfügen und löschen, Kanten als Gerade
/// oder Bézierkurve. Eigene Umsetzung (Clean-Room). Die Teile bleiben <see cref="PatPart"/> mit benannten Kanten: Nahtzugabe, Zuschnitt, PDF/DXF/SVG und
/// die 3D-Anprobe rechnen mit dem bearbeiteten Umriss weiter. Bearbeitungen liegen je Schnittart und Teil im Dokument (<see cref="Field"/>).</summary>
public static class GarmentEdit
{
    /// <summary>Feld im Programm-Dokument: { "tshirt": { "Vorderteil": [Knoten …] } }.</summary>
    public const string Field = "gEdits";
    /// <summary>Höchste Abweichung der Kurvenanpassung von der Vorlage in cm.</summary>
    public const double FitTolerance = 0.05;
    /// <summary>Abtastung je Kurvenstück (wie die Grundschnitte).</summary>
    public const int CurveSteps = 16;

    static readonly Dictionary<string, string> EdgeNames = new()
    {
        ["hem"] = "Saum", ["sideR"] = "Seitennaht rechts", ["sideL"] = "Seitennaht links", ["armR"] = "Armloch rechts", ["armL"] = "Armloch links",
        ["shR"] = "Schulter rechts", ["shL"] = "Schulter links", ["neck"] = "Halsausschnitt", ["capF"] = "Armkugel vorn", ["capB"] = "Armkugel hinten",
        ["underF"] = "Ärmelnaht vorn", ["underB"] = "Ärmelnaht hinten", ["seamF"] = "Ansatz vorn", ["seamB"] = "Ansatz hinten", ["seam"] = "Ansatz",
        ["endR"] = "Ende rechts", ["endL"] = "Ende links", ["free"] = "Außenkante", ["outer"] = "Seitennaht", ["inner"] = "Innere Beinnaht",
        ["crotch"] = "Schrittnaht", ["waist"] = "Bundkante", ["seamFL"] = "Ansatz vorn links", ["seamFR"] = "Ansatz vorn rechts",
        ["seamBR"] = "Ansatz hinten rechts", ["seamBL"] = "Ansatz hinten links",
    };

    /// <summary>Alltagsname einer Kante.</summary>
    public static string EdgeLabel(string id) => EdgeNames.TryGetValue(id, out var n) ? n : id;

    static bool Same(double ax, double ay, double bx, double by) => Math.Abs(ax - bx) < 1e-9 && Math.Abs(ay - by) < 1e-9;

    /// <summary>Das Stück von Punkt <paramref name="seg"/> zum nächsten ist eine Kurve (mindestens ein Griff steht ab).</summary>
    public static bool IsCurve(IReadOnlyList<PatNode> n, int seg)
    {
        var a = n[seg].N; var b = n[(seg + 1) % n.Count].N;
        return !(Same(a.OutX, a.OutY, a.X, a.Y) && Same(b.InX, b.InY, b.X, b.Y));
    }

    // ───────────── Vorlage → Punkte ─────────────
    readonly record struct Seg(PatPt A, PatPt C1, PatPt C2, PatPt B, bool Curve);

    /// <summary>Schnittteil in bearbeitbare Punkte zerlegen: jede Kante wird zu Geraden und wenigen Bézierkurven (Abweichung höchstens <see cref="FitTolerance"/>).</summary>
    public static List<PatNode> ToNodes(PatPart p)
    {
        var segs = new List<(Seg S, string? Edge)>();
        foreach (var e in p.Edges)
        {
            var pts = Dedupe(e.Points);
            if (pts.Count < 2) continue;
            var fitted = FitEdge(pts);
            for (var i = 0; i < fitted.Count; i++) segs.Add((fitted[i], i == 0 ? e.Name : null));
        }
        var res = new List<PatNode>(segs.Count);
        for (var i = 0; i < segs.Count; i++)
        {
            var (s, edge) = segs[i]; var prev = segs[(i + segs.Count - 1) % segs.Count].S;
            var inH = prev.Curve ? prev.C2 : s.A; var outH = s.Curve ? s.C1 : s.A;
            res.Add(new(new VNode(s.A.X, s.A.Y, inH.X, inH.Y, outH.X, outH.Y), edge));
        }
        return res;
    }

    static List<PatPt> Dedupe(IReadOnlyList<PatPt> pts)
    {
        var r = new List<PatPt>();
        foreach (var p in pts) if (r.Count == 0 || Dist(r[^1], p) > 1e-6) r.Add(p);
        return r;
    }

    static double Dist(PatPt a, PatPt b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    static PatPt Unit(PatPt a, PatPt b) { var d = Math.Max(1e-12, Dist(a, b)); return new((b.X - a.X) / d, (b.Y - a.Y) / d); }

    static List<Seg> FitEdge(List<PatPt> p)
    {
        var res = new List<Seg>();
        var straight = p.Count == 2 || p.All(q => SegDist(q, p[0], p[^1]) <= FitTolerance);
        if (straight) { res.Add(new(p[0], p[0], p[^1], p[^1], false)); return res; }
        FitRange(p, 0, p.Count - 1, Unit(p[0], p[1]), Unit(p[^1], p[^2]), res, 0);
        return res;
    }

    static double SegDist(PatPt q, PatPt a, PatPt b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        var t = l2 < 1e-12 ? 0 : Math.Clamp(((q.X - a.X) * dx + (q.Y - a.Y) * dy) / l2, 0, 1);
        return Dist(q, new(a.X + t * dx, a.Y + t * dy));
    }

    /// <summary>Kubische Bézierkurve durch die Punkte i0…i1 mit festen Endrichtungen (kleinste Quadrate, Neuparametrierung nach Newton); bei zu großer
    /// Abweichung an der schlechtesten Stelle teilen. Eigene Umsetzung des bekannten Verfahrens.</summary>
    static void FitRange(List<PatPt> p, int i0, int i1, PatPt tA, PatPt tB, List<Seg> res, int depth)
    {
        PatPt a = p[i0], b = p[i1];
        if (i1 - i0 == 1) { res.Add(new(a, a, b, b, false)); return; }
        var u = new double[i1 - i0 + 1];
        for (var k = 1; k < u.Length; k++) u[k] = u[k - 1] + Dist(p[i0 + k - 1], p[i0 + k]);
        var total = u[^1];
        for (var k = 0; k < u.Length; k++) u[k] /= total;
        Seg seg = default; double worst = 0; var at = (i0 + i1) / 2;
        for (var iter = 0; iter < 4; iter++)
        {
            seg = Solve(p, i0, u, tA, tB, a, b, total);
            (worst, at) = MaxError(p, i0, u, seg);
            if (worst <= FitTolerance) break;
            for (var k = 1; k < u.Length - 1; k++) u[k] = Newton(seg, p[i0 + k], u[k]);
        }
        if (worst <= FitTolerance || depth > 8 || i1 - i0 < 3) { res.Add(seg); return; }
        at = Math.Clamp(at, i0 + 1, i1 - 1);
        var tc = Unit(p[at - 1], p[at + 1]);
        FitRange(p, i0, at, tA, new(-tc.X, -tc.Y), res, depth + 1);
        FitRange(p, at, i1, tc, tB, res, depth + 1);
    }

    static Seg Solve(List<PatPt> p, int i0, double[] u, PatPt tA, PatPt tB, PatPt a, PatPt b, double chord)
    {
        double c11 = 0, c12 = 0, c22 = 0, x1 = 0, x2 = 0;
        for (var k = 0; k < u.Length; k++)
        {
            double t = u[k], s = 1 - t, b0 = s * s * s, b1 = 3 * s * s * t, b2 = 3 * s * t * t, b3 = t * t * t;
            double a1x = tA.X * b1, a1y = tA.Y * b1, a2x = tB.X * b2, a2y = tB.Y * b2;
            c11 += a1x * a1x + a1y * a1y; c12 += a1x * a2x + a1y * a2y; c22 += a2x * a2x + a2y * a2y;
            double rx = p[i0 + k].X - (a.X * (b0 + b1) + b.X * (b2 + b3)), ry = p[i0 + k].Y - (a.Y * (b0 + b1) + b.Y * (b2 + b3));
            x1 += a1x * rx + a1y * ry; x2 += a2x * rx + a2y * ry;
        }
        var det = c11 * c22 - c12 * c12;
        double al = det == 0 ? 0 : (x1 * c22 - x2 * c12) / det, ar = det == 0 ? 0 : (c11 * x2 - c12 * x1) / det;
        var d = Dist(a, b);
        if (al < 1e-6 * d || ar < 1e-6 * d || al > 3 * d || ar > 3 * d) al = ar = d / 3;
        return new(a, new(a.X + tA.X * al, a.Y + tA.Y * al), new(b.X + tB.X * ar, b.Y + tB.Y * ar), b, true);
    }

    static PatPt Eval(Seg s, double t)
    {
        var v = 1 - t;
        return new(v * v * v * s.A.X + 3 * v * v * t * s.C1.X + 3 * v * t * t * s.C2.X + t * t * t * s.B.X, v * v * v * s.A.Y + 3 * v * v * t * s.C1.Y + 3 * v * t * t * s.C2.Y + t * t * t * s.B.Y);
    }

    static (double Worst, int At) MaxError(List<PatPt> p, int i0, double[] u, Seg s)
    {
        double worst = 0; var at = i0 + u.Length / 2;
        for (var k = 1; k < u.Length - 1; k++) { var e = Dist(Eval(s, u[k]), p[i0 + k]); if (e > worst) { worst = e; at = i0 + k; } }
        return (worst, at);
    }

    static double Newton(Seg s, PatPt q, double t)
    {
        var v = 1 - t;
        var pt = Eval(s, t);
        double d1x = 3 * (v * v * (s.C1.X - s.A.X) + 2 * v * t * (s.C2.X - s.C1.X) + t * t * (s.B.X - s.C2.X)), d1y = 3 * (v * v * (s.C1.Y - s.A.Y) + 2 * v * t * (s.C2.Y - s.C1.Y) + t * t * (s.B.Y - s.C2.Y));
        double d2x = 6 * (v * (s.C2.X - 2 * s.C1.X + s.A.X) + t * (s.B.X - 2 * s.C2.X + s.C1.X)), d2y = 6 * (v * (s.C2.Y - 2 * s.C1.Y + s.A.Y) + t * (s.B.Y - 2 * s.C2.Y + s.C1.Y));
        double num = (pt.X - q.X) * d1x + (pt.Y - q.Y) * d1y, den = d1x * d1x + d1y * d1y + (pt.X - q.X) * d2x + (pt.Y - q.Y) * d2y;
        return Math.Abs(den) < 1e-12 ? t : Math.Clamp(t - num / den, 0, 1);
    }

    // ───────────── Punkte → Schnittteil ─────────────
    /// <summary>Punkte eines Stücks (Start, abgetastete Kurve, Ende).</summary>
    public static List<PatPt> SegmentPoints(IReadOnlyList<PatNode> n, int seg, int steps = CurveSteps)
    {
        var a = n[seg].N; var b = n[(seg + 1) % n.Count].N;
        if (!IsCurve(n, seg)) return [new(a.X, a.Y), new(b.X, b.Y)];
        return VectorKit.Sample([a, b], false, steps).Select(q => new PatPt(q.X, q.Y)).ToList();
    }

    /// <summary>Bearbeitete Punkte zurück in ein Schnittteil mit denselben benannten Kanten (Name, Rolle, Anzahl bleiben).</summary>
    public static PatPart FromNodes(PatPart template, IReadOnlyList<PatNode> n)
    {
        Validate(template, n);
        var starts = Enumerable.Range(0, n.Count).Where(i => n[i].Edge is not null).ToList();
        var edges = new List<PatEdge>();
        for (var k = 0; k < starts.Count; k++)
        {
            var s = starts[k]; var e = starts[(k + 1) % starts.Count]; if (e <= s) e += n.Count;
            var pts = new List<PatPt> { new(n[s].X, n[s].Y) };
            for (var j = s; j < e; j++) pts.AddRange(SegmentPoints(n, j % n.Count).Skip(1));
            edges.Add(new PatEdge(n[s].Edge!, pts));
        }
        // in der Reihenfolge der Vorlage (die erste Kante zuerst – der Saum bleibt vorne)
        var order = template.Edges.Select(x => x.Name).ToList();
        edges.Sort((x, y) => order.IndexOf(x.Name).CompareTo(order.IndexOf(y.Name)));
        return template with { Edges = edges };
    }

    /// <summary>Prüft, dass jede Kante der Vorlage genau einmal und in derselben Reihenfolge vorkommt (sonst passen Nähte und Anprobe nicht mehr).</summary>
    public static void Validate(PatPart template, IReadOnlyList<PatNode> n)
    {
        if (n.Count < 3) throw new ArgumentException("Ein Schnittteil braucht mindestens drei Punkte.");
        if (n.Any(q => !double.IsFinite(q.X) || !double.IsFinite(q.Y) || !double.IsFinite(q.N.InX) || !double.IsFinite(q.N.OutX) || !double.IsFinite(q.N.InY) || !double.IsFinite(q.N.OutY)))
            throw new ArgumentException("Ein Punkt hat keine gültige Lage.");
        var names = n.Where(q => q.Edge is not null).Select(q => q.Edge!).ToList();
        var want = template.Edges.Select(e => e.Name).ToList();
        if (names.Count != want.Count || names.Distinct().Count() != names.Count || !want.All(names.Contains))
            throw new ArgumentException("Die bearbeiteten Punkte passen nicht mehr zu den Kanten des Teils „" + template.Name + "“.");
        // gleiche zyklische Reihenfolge
        var off = names.IndexOf(want[0]);
        for (var i = 0; i < want.Count; i++) if (names[(off + i) % names.Count] != want[i]) throw new ArgumentException("Die Kanten des Teils „" + template.Name + "“ sind durcheinander geraten.");
    }

    // ───────────── Bearbeiten (jede Funktion liefert eine neue Liste) ─────────────
    /// <summary>Punkt mitsamt seinen Griffen an eine neue Lage setzen.</summary>
    public static List<PatNode> MoveNode(IReadOnlyList<PatNode> n, int i, double x, double y)
    {
        var r = n.ToList(); var p = r[i]; r[i] = p with { N = p.N.Offset(x - p.X, y - p.Y) }; return r;
    }

    /// <summary>Griff ziehen. Ist der Punkt glatt (Griffe in einer Linie), dreht der Gegengriff mit und behält seine Länge – wie in Schnitt- und Zeichenprogrammen.</summary>
    public static List<PatNode> MoveHandle(IReadOnlyList<PatNode> n, int i, bool outHandle, double x, double y)
    {
        var r = n.ToList(); var p = r[i].N;
        var smooth = IsSmooth(p);
        double nx = outHandle ? x : p.OutX, ny = outHandle ? y : p.OutY, ix = outHandle ? p.InX : x, iy = outHandle ? p.InY : y;
        if (smooth)
        {
            double mx = x - p.X, my = y - p.Y, ml = Math.Sqrt(mx * mx + my * my);
            if (ml > 1e-9)
            {
                var other = outHandle ? Math.Sqrt(Math.Pow(p.InX - p.X, 2) + Math.Pow(p.InY - p.Y, 2)) : Math.Sqrt(Math.Pow(p.OutX - p.X, 2) + Math.Pow(p.OutY - p.Y, 2));
                if (outHandle) { ix = p.X - mx / ml * other; iy = p.Y - my / ml * other; } else { nx = p.X - mx / ml * other; ny = p.Y - my / ml * other; }
            }
        }
        r[i] = r[i] with { N = new VNode(p.X, p.Y, ix, iy, nx, ny) };
        return r;
    }

    /// <summary>Beide Griffe stehen ab und liegen auf einer Linie durch den Punkt.</summary>
    public static bool IsSmooth(VNode p)
    {
        double ax = p.X - p.InX, ay = p.Y - p.InY, bx = p.OutX - p.X, by = p.OutY - p.Y;
        double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
        if (la < 1e-6 || lb < 1e-6) return false;
        return (ax * bx + ay * by) / (la * lb) > 0.9994; // unter etwa 2°
    }

    /// <summary>Neuen Punkt auf dem Stück <paramref name="seg"/> bei <paramref name="t"/> (0…1) einfügen; die Form bleibt genau gleich (de Casteljau). Rückgabe: Liste und Nummer des neuen Punkts.</summary>
    public static (List<PatNode> Nodes, int Index) Insert(IReadOnlyList<PatNode> n, int seg, double t)
    {
        t = Math.Clamp(t, 0.02, 0.98);
        var r = n.ToList(); var j = (seg + 1) % r.Count; var a = r[seg].N; var b = r[j].N;
        PatNode mid;
        if (!IsCurve(n, seg))
        {
            double x = a.X + (b.X - a.X) * t, y = a.Y + (b.Y - a.Y) * t;
            mid = new(VNode.Corner(x, y));
        }
        else
        {
            static (double, double) L((double X, double Y) p, (double X, double Y) q, double t) => (p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t);
            var p0 = (a.X, a.Y); var p1 = (a.OutX, a.OutY); var p2 = (b.InX, b.InY); var p3 = (b.X, b.Y);
            var q0 = L(p0, p1, t); var q1 = L(p1, p2, t); var q2 = L(p2, p3, t); var r0 = L(q0, q1, t); var r1 = L(q1, q2, t); var s = L(r0, r1, t);
            r[seg] = r[seg] with { N = a with { OutX = q0.Item1, OutY = q0.Item2 } };
            r[j] = r[j] with { N = r[j].N with { InX = q2.Item1, InY = q2.Item2 } };
            mid = new(new VNode(s.Item1, s.Item2, r0.Item1, r0.Item2, r1.Item1, r1.Item2));
        }
        var at = seg + 1;
        r.Insert(at, mid);
        return (r, at);
    }

    /// <summary>Punkt löschen. Beginnt hier eine Kante (Nahtkante), übernimmt der nächste Punkt derselben Kante den Anfang; gibt es keinen, bleibt die Ecke stehen.
    /// Rückgabe: neue Liste oder ein Hinweis, warum es nicht geht.</summary>
    public static (List<PatNode>? Nodes, string? Problem) Delete(IReadOnlyList<PatNode> n, int i)
    {
        if (n.Count <= 3) return (null, "Ein Schnittteil braucht mindestens drei Punkte.");
        var r = n.ToList(); var j = (i + 1) % r.Count;
        if (r[i].Edge is { } edge)
        {
            if (r[j].Edge is not null) return (null, $"An diesem Eckpunkt beginnt die Kante „{EdgeLabel(edge)}“, an der eine Naht hängt. Füge erst einen Punkt in diese Kante ein oder verschiebe die Ecke.");
            r[j] = r[j] with { Edge = edge };
        }
        r.RemoveAt(i);
        return (r, null);
    }

    /// <summary>Stück als Kurve (Griffe auf einem Drittel, die Form bleibt zunächst gerade) oder als Gerade (Griffe weg).</summary>
    public static List<PatNode> SetCurve(IReadOnlyList<PatNode> n, int seg, bool curve)
    {
        var r = n.ToList(); var j = (seg + 1) % r.Count; var a = r[seg].N; var b = r[j].N;
        if (curve == IsCurve(n, seg)) return r;
        if (curve)
        {
            double dx = (b.X - a.X) / 3, dy = (b.Y - a.Y) / 3;
            // leicht nach außen gewölbt, damit die Griffe sichtbar sind und die Kante gleich als Kurve erkennbar ist
            double len = Math.Sqrt(dx * dx + dy * dy), bow = Math.Min(1.0, len * 0.25), nx = len < 1e-9 ? 0 : dy / len * bow, ny = len < 1e-9 ? 0 : -dx / len * bow;
            r[seg] = r[seg] with { N = a with { OutX = a.X + dx + nx, OutY = a.Y + dy + ny } };
            r[j] = r[j] with { N = r[j].N with { InX = b.X - dx + nx, InY = b.Y - dy + ny } };
        }
        else
        {
            r[seg] = r[seg] with { N = a with { OutX = a.X, OutY = a.Y } };
            r[j] = r[j] with { N = r[j].N with { InX = b.X, InY = b.Y } };
        }
        return r;
    }

    /// <summary>Nächstes Stück zu einem Punkt (cm): Stück, Lage 0…1 auf dem Stück, Abstand.</summary>
    public static (int Seg, double T, double Dist) Nearest(IReadOnlyList<PatNode> n, double x, double y)
    {
        (int, double, double) best = (-1, 0, double.MaxValue);
        for (var s = 0; s < n.Count; s++)
        {
            var pts = SegmentPoints(n, s, 48);
            for (var k = 1; k < pts.Count; k++)
            {
                PatPt a = pts[k - 1], b = pts[k];
                double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                var u = l2 < 1e-12 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0, 1);
                var d = Dist(new(x, y), new(a.X + u * dx, a.Y + u * dy));
                if (d < best.Item3) best = (s, (k - 1 + u) / (pts.Count - 1), d);
            }
        }
        // T auf dem Kurvenparameter: die Abtastung ist gleichmäßig im Parameter, also passt der Anteil
        return best;
    }

    /// <summary>Punkt unter dem Zeiger (cm), sonst -1.</summary>
    public static int HitNode(IReadOnlyList<PatNode> n, double x, double y, double tol)
    {
        var best = -1; var bd = tol;
        for (var i = 0; i < n.Count; i++) { var d = Dist(new(x, y), new(n[i].X, n[i].Y)); if (d <= bd) { bd = d; best = i; } }
        return best;
    }

    /// <summary>Kante, zu der ein Stück gehört.</summary>
    public static string EdgeOf(IReadOnlyList<PatNode> n, int seg)
    {
        for (var k = 0; k < n.Count; k++) { var i = (seg - k + n.Count) % n.Count; if (n[i].Edge is { } e) return e; }
        return "";
    }

    /// <summary>Der Umriss schneidet sich selbst (dann stimmen Fläche und Nahtzugabe nicht).</summary>
    public static bool SelfIntersects(PatPart p)
    {
        var o = p.Outline; var c = o.Count;
        for (var i = 0; i < c; i++)
            for (var j = i + 2; j < c; j++)
            {
                if (i == 0 && j == c - 1) continue;
                if (Cross(o[i], o[(i + 1) % c], o[j], o[(j + 1) % c])) return true;
            }
        return false;
    }

    static bool Cross(PatPt a, PatPt b, PatPt c, PatPt d)
    {
        static double O(PatPt p, PatPt q, PatPt r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        double d1 = O(c, d, a), d2 = O(c, d, b), d3 = O(a, b, c), d4 = O(a, b, d);
        return (d1 > 1e-9 && d2 < -1e-9 || d1 < -1e-9 && d2 > 1e-9) && (d3 > 1e-9 && d4 < -1e-9 || d3 < -1e-9 && d4 > 1e-9);
    }

    // ───────────── Speichern im Dokument ─────────────
    static double R(double v) => Math.Round(v, 4);

    public static JsonArray ToJson(IReadOnlyList<PatNode> n) => new([.. n.Select(q =>
    {
        var o = new JsonObject { ["x"] = R(q.X), ["y"] = R(q.Y), ["ix"] = R(q.N.InX), ["iy"] = R(q.N.InY), ["ox"] = R(q.N.OutX), ["oy"] = R(q.N.OutY) };
        if (q.Edge is not null) o["e"] = q.Edge;
        return (JsonNode)o;
    })]);

    public static List<PatNode>? FromJson(JsonNode? node)
    {
        if (node is not JsonArray a) return null;
        var r = new List<PatNode>();
        foreach (var x in a)
        {
            if (x is not JsonObject o) return null;
            double V(string k) => Programs.ProgramDocument.ToNumber(o[k], double.NaN);
            var e = o["e"] is JsonValue ev && ev.TryGetValue<string>(out var s) ? s : null;
            r.Add(new(new VNode(V("x"), V("y"), V("ix"), V("iy"), V("ox"), V("oy")), e));
        }
        return r;
    }

    /// <summary>Bearbeitete Teile einsetzen. Ungültige Einträge (alte oder kaputte Daten) bleiben unbeachtet – dann gilt die Vorlage.</summary>
    public static Garment Apply(Garment g, JsonObject? edits)
    {
        if (edits?[g.Kind] is not JsonObject kind || kind.Count == 0) return g;
        var parts = g.Parts.Select(p =>
        {
            if (FromJson(kind[p.Name]) is not { } n) return p;
            try { return FromNodes(p, n); } catch (ArgumentException) { return p; }
        }).ToList();
        return g with { Parts = parts };
    }

    /// <summary>Namen der bearbeiteten Teile dieser Schnittart.</summary>
    public static IReadOnlyList<string> Edited(JsonObject? edits, string kind) => edits?[kind] is JsonObject k ? [.. k.Select(x => x.Key)] : [];

    /// <summary>Neues Bearbeitungsfeld mit dem Teil <paramref name="part"/> (null = zurücksetzen).</summary>
    public static JsonObject With(JsonObject? edits, string kind, string part, IReadOnlyList<PatNode>? nodes)
    {
        var all = edits?.DeepClone() as JsonObject ?? [];
        if (all[kind] is not JsonObject k) { k = []; all[kind] = k; }
        if (nodes is null) k.Remove(part); else k[part] = ToJson(nodes);
        if (k.Count == 0) all.Remove(kind);
        return all;
    }

    /// <summary>Nahtlängen vergleichen: Teil A, Teil B und Unterschied in cm (Armkugeln sind gewollt etwas länger – Mehrweite).</summary>
    public static IReadOnlyList<(PatSeam Seam, double A, double B)> SeamLengths(Garment g)
    {
        var res = new List<(PatSeam, double, double)>();
        foreach (var s in g.Seams)
        {
            var pa = g.Parts.FirstOrDefault(p => p.Name == s.PartA); var pb = g.Parts.FirstOrDefault(p => p.Name == s.PartB);
            var ea = pa?.Edges.FirstOrDefault(e => e.Name == s.EdgeA); var eb = pb?.Edges.FirstOrDefault(e => e.Name == s.EdgeB);
            if (ea is null || eb is null) continue;
            res.Add((s, ea.Length, eb.Length));
        }
        return res;
    }

    /// <summary>Zahl mit Komma (deutsch).</summary>
    public static string Cm(double v) => v.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));
}
