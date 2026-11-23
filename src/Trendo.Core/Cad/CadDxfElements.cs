using System.Globalization;

namespace Trendo.Core.Cad;

/// <summary>T-0760: DXF-Elemente, die früher nur gemeldet wurden – Blockverweise (INSERT), Freiformkurven (SPLINE) und Mehrfachlinien (MLINE).
/// REGION bleibt gemeldet: ihr Inhalt ist ACIS (ab AutoCAD 2013 binär im Abschnitt ACDSDATA, davor verschlüsselter Text mit eigener Topologie) –
/// ohne Geometrie-Kern nicht sicher lesbar.</summary>
public static partial class CadDxf
{
    /// <summary>T-0760: höchstens so viel Arbeit (DXF-Gruppen) beim Auflösen aller Blockverweise einer Datei – darüber bleibt ein Verweis gemeldet.</summary>
    public const double MaxInsertWork = 8_000_000;

    /// <summary>T-0760: Lage beim Auflösen eines Blockverweises – Abbildung Block → Zeichnung, Ebene für Elemente auf Ebene 0, unsichtbar, Tiefe, Kette.</summary>
    sealed record InsCtx(Xf M, string Layer, bool Invisible, int Depth, string Block, InsCtx? Up);

    /// <summary>T-0760: affine Abbildung x' = A·x + B·y + Tx, y' = C·x + D·y + Ty.</summary>
    readonly record struct Xf(double A, double B, double C, double D, double Tx, double Ty)
    {
        public static readonly Xf Id = new(1, 0, 0, 1, 0, 0);
        public (double X, double Y) Map(double x, double y) => (A * x + B * y + Tx, C * x + D * y + Ty);
        public (double X, double Y) Dir(double x, double y) => (A * x + B * y, C * x + D * y);
        /// <summary>Erst <paramref name="inner"/>, dann diese Abbildung.</summary>
        public Xf After(Xf inner) => new(A * inner.A + B * inner.C, A * inner.B + B * inner.D, C * inner.A + D * inner.C, C * inner.B + D * inner.D,
            A * inner.Tx + B * inner.Ty + Tx, C * inner.Tx + D * inner.Ty + Ty);
        public double Det => A * D - B * C;
        /// <summary>größter Streckfaktor (Singulärwert).</summary>
        public double SMax
        {
            get
            {
                double p = A * A + C * C, q = B * B + D * D, r = A * B + C * D, h = (p + q) / 2, w = Math.Sqrt(Math.Max(0, (p - q) * (p - q) / 4 + r * r));
                return Math.Sqrt(h + w);
            }
        }
        /// <summary>Ähnlichkeit (Drehung, gleicher Faktor, ggf. gespiegelt)? <paramref name="s"/> = Faktor.</summary>
        public bool Similar(out double s)
        {
            double p = A * A + C * C, q = B * B + D * D, r = A * B + C * D; s = Math.Sqrt(p);
            return Math.Abs(p - q) <= 1e-12 * Math.Max(p, q) && Math.Abs(r) <= 1e-12 * Math.Max(p, q);
        }
        public Box MapBox(Box b)
        {
            if (b.IsEmpty || b.IsAll || b.HasNaN) return b;
            var r = Box.Empty;
            foreach (var x in new[] { b.MinX, b.MaxX }) foreach (var y in new[] { b.MinY, b.MaxY }) r = r.With(Map(x, y));
            return r;
        }
    }

    /// <summary>T-0760: Element aus einem Block abbilden. Bei Ähnlichkeit bleiben Kreise, Bögen und Ausbuchtungen (Spiegelung kehrt die Richtung um);
    /// sonst werden sie mit Sehnenfehler ≤ <paramref name="tolLocal"/> (Blockeinheiten) abgetastet und als Zug abgebildet. False: Zahlen unbrauchbar.</summary>
    static bool MapEntity(CadEntity e, Xf m, double tolLocal, double kUnits)
    {
        var sim = m.Similar(out var s); var mirror = m.Det < 0;
        static double Ang((double X, double Y) v) { var a = Math.Atan2(v.Y, v.X) * 180 / Math.PI; return a < 0 ? a + 360 : a; }
        switch (e.Kind)
        {
            case "kreis" when sim: { (e.Cx, e.Cy) = m.Map(e.Cx, e.Cy); e.R *= s; break; }
            case "kreis":
            {
                var pts = ArcPts(e.Cx, e.Cy, e.R, 0, 2 * Math.PI, tolLocal, false);
                e.Kind = "polylinie"; e.Closed = true; e.Source = "CIRCLE"; e.Points = pts.Select(q => { var (x, y) = m.Map(q.X, q.Y); return new[] { x, y, 0 }; }).ToList();
                e.Cx = e.Cy = e.R = 0;
                break;
            }
            case "bogen" when sim:
            {
                double a0 = e.A0 * Math.PI / 180, a1 = e.A1 * Math.PI / 180;
                var n0 = Ang(m.Dir(Math.Cos(a0), Math.Sin(a0))); var n1 = Ang(m.Dir(Math.Cos(a1), Math.Sin(a1)));
                (e.Cx, e.Cy) = m.Map(e.Cx, e.Cy); e.R *= s;
                if (mirror) { e.A0 = n1; e.A1 = n0; } else { e.A0 = n0; e.A1 = n1; }
                break;
            }
            case "bogen":
            {
                var sw = (e.A1 - e.A0) % 360; if (sw <= 0) sw += 360;
                var pts = ArcPts(e.Cx, e.Cy, e.R, e.A0 * Math.PI / 180, sw * Math.PI / 180, tolLocal, true);
                e.Kind = "polylinie"; e.Closed = false; e.Source = "ARC"; e.Points = pts.Select(q => { var (x, y) = m.Map(q.X, q.Y); return new[] { x, y, 0 }; }).ToList();
                e.Cx = e.Cy = e.R = 0;
                break;
            }
            case "linie" or "polylinie":
            {
                var bulged = e.Points.Any(q => q.Length > 2 && Math.Abs(q[2]) > 1e-12);
                if (!sim && bulged)
                {
                    // Ausbuchtungen abtasten (Stützpunkte bleiben), dann abbilden
                    var res = new List<double[]>();
                    for (var i = 0; i < e.Points.Count; i++)
                    {
                        var p = e.Points[i]; res.Add([p[0], p[1], 0]);
                        if (i == e.Points.Count - 1 && !e.Closed) break;
                        var q = e.Points[(i + 1) % e.Points.Count]; var bl = p.Length > 2 ? p[2] : 0;
                        if (Math.Abs(bl) <= 1e-12) continue;
                        double dx = q[0] - p[0], dy = q[1] - p[1], c = Math.Sqrt(dx * dx + dy * dy); if (!(c > 0)) continue;
                        var th = 4 * Math.Atan(bl); var r = c / (2 * Math.Sin(Math.Abs(th) / 2)); var hh = c / 2 / Math.Tan(th / 2);
                        double mx = (p[0] + q[0]) / 2 - dy / c * hh, my = (p[1] + q[1]) / 2 + dx / c * hh;
                        var arc = ArcPts(mx, my, r, Math.Atan2(p[1] - my, p[0] - mx), th, tolLocal, false);
                        for (var k = 1; k < arc.Count; k++) res.Add([arc[k].X, arc[k].Y, 0]);
                        if (res.Count > MaxEllipsePoints) return false;
                    }
                    e.Points = res;
                }
                foreach (var p in e.Points) { (p[0], p[1]) = m.Map(p[0], p[1]); if (p.Length > 2 && mirror) p[2] = -p[2]; }
                break;
            }
            case "text":
            {
                foreach (var p in e.Points) (p[0], p[1]) = m.Map(p[0], p[1]);
                var r = e.Rotation * Math.PI / 180; e.Rotation = Ang(m.Dir(Math.Cos(r), Math.Sin(r))); e.Height *= Math.Sqrt(Math.Abs(m.Det));
                break;
            }
        }
        if (!double.IsFinite(e.Cx) || !double.IsFinite(e.Cy) || !double.IsFinite(e.R) || Math.Max(Math.Abs(e.Cx), Math.Abs(e.Cy)) * kUnits > 1e9 || e.R * kUnits > 1e9) return false;
        foreach (var p in e.Points) if (!double.IsFinite(p[0]) || !double.IsFinite(p[1]) || Math.Max(Math.Abs(p[0]), Math.Abs(p[1])) * kUnits > 1e9) return false;
        return e.Points.Count <= MaxEllipsePoints;
    }

    /// <summary>T-0760: Kreisbogen als Punkte mit Sehnenfehler ≤ <paramref name="tol"/> (Anfang immer, Ende wenn <paramref name="withEnd"/>).</summary>
    static List<(double X, double Y)> ArcPts(double cx, double cy, double r, double a0, double sweep, double tol, bool withEnd)
    {
        var step = r > tol ? 2 * Math.Acos(1 - tol / r) : Math.PI / 2;
        var n = (int)Math.Clamp(Math.Ceiling(Math.Abs(sweep) / Math.Max(step, 1e-9)), Math.Abs(sweep) >= 2 * Math.PI - 1e-12 ? 8 : 1, MaxEllipsePoints + 1);
        var res = new List<(double X, double Y)>(n + 1);
        for (var k = 0; k < n + (withEnd ? 1 : 0); k++) { var a = a0 + sweep * k / n; res.Add((cx + r * Math.Cos(a), cy + r * Math.Sin(a))); }
        return res;
    }

    /// <summary>T-0760: SPLINE lesen. Kurve aus Kontrollpunkten (10/20), Knoten (40) und Gewichten (41): NURBS nach de Boor im Bereich
    /// [Knoten p, Knoten n] abgetastet – je Knotenstück erst gleichmäßig in max(4, 2p) Teile, dann halbiert, bis Mitte und Viertelpunkte höchstens
    /// <paramref name="tolU"/> (Zeichnungseinheiten, 0,001 mm) von der Sehne liegen. Punkte in Weltkoordinaten wie bei LINE (z wird nicht verwendet).
    /// Nicht geraten wird: geschlossen (Flag 1) ohne dass Anfang und Ende sich treffen (wie schließt das Programm? periodisch?), nur Fitpunkte
    /// bei Grad ≥ 2 (Verlauf hängt vom Rechenweg ab), Fitpunkte neben der Kurve, widersprüchliche Zahlen, Gewicht ≤ 0 → gemeldet.</summary>
    internal static (List<double[]>? Pts, bool Closed, string? Why, Box Box) ReadSpline(List<(int Code, string Value)> b, double tolU, long budget, double kUnits = 1)
    {
        var ctrl = SeqPts(b, 10); var fit = SeqPts(b, 11);
        if (ctrl is null || fit is null) return (null, false, "unvollständige Punkte", Box.All); // T-0747-fix13: x ohne y oder y ohne x
        var hull = Box.Empty; foreach (var q in ctrl) hull = hull.With(q); // B-Spline liegt in der konvexen Hülle der Kontrollpunkte (Gewichte > 0)
        static double? Dn(string v) => double.TryParse(v.Trim(), NumberStyles.Float, Inv, out var x) && double.IsFinite(x) ? x : null;
        var knots = new List<double>(); var weights = new List<double>();
        foreach (var (c, v) in b)
        {
            if (c is not (40 or 41)) continue;
            if (Dn(v) is not { } x) return (null, false, "Knoten oder Gewicht ist keine Zahl", Box.All);
            (c == 40 ? knots : weights).Add(x);
        }
        if (weights.Any(w => !(w > 0))) return (null, false, "Gewicht ≤ 0", Box.All); // Hülle gilt dann nicht
        var flags = IntSafe(b, 70); var deg = Has(b, 71) ? IntSafe(b, 71) : 3;
        if (ctrl.Count == 0)
        {
            if (fit.Count == 0) return (null, false, null, Box.Empty); // gar keine Punkte: nichts gezeichnet
            if (deg == 1 && fit.Count >= 2 && !Has(b, 12) && !Has(b, 13)) return (fit.Select(q => new[] { q.X, q.Y, 0 }).ToList(), (flags & 1) != 0 && fit.Count >= 3, null, FitBox(fit));
            return (null, false, "nur Fitpunkte (Verlauf hängt vom Ursprungsprogramm ab)", Box.All);
        }
        var n = ctrl.Count;
        if (deg < 1 || deg > 25 || n < deg + 1) return (null, false, "Grad passt nicht zu den Kontrollpunkten", hull);
        if (Has(b, 72) && IntSafe(b, 72) != knots.Count || Has(b, 73) && IntSafe(b, 73) != n || Has(b, 74) && IntSafe(b, 74) != fit.Count)
            return (null, false, "Anzahl der Knoten oder Punkte widersprüchlich", hull);
        if (knots.Count != n + deg + 1) return (null, false, "Knotenzahl passt nicht", hull);
        for (var i = 1; i < knots.Count; i++) if (knots[i] < knots[i - 1]) return (null, false, "Knoten fallen", hull);
        if (weights.Count != 0 && weights.Count != n) return (null, false, "Gewichte passen nicht zu den Kontrollpunkten", hull);
        double t0 = knots[deg], t1 = knots[n];
        if (!(t1 > t0)) return (null, false, "leerer Kurvenbereich", hull);
        var w = weights.Count == n ? weights : null;
        var work = new double[deg + 1, 3];
        // de Boor im Knotenstück k (knots[k] ≤ t ≤ knots[k+1]), homogen (x·w, y·w, w)
        (double X, double Y) Eval(int k, double t)
        {
            for (var j = 0; j <= deg; j++) { var cp = ctrl[k - deg + j]; var wj = w?[k - deg + j] ?? 1; work[j, 0] = cp.X * wj; work[j, 1] = cp.Y * wj; work[j, 2] = wj; }
            for (var r = 1; r <= deg; r++)
                for (var j = deg; j >= r; j--)
                {
                    var i = k - deg + j; var den = knots[i + deg - r + 1] - knots[i]; var a = den > 0 ? (t - knots[i]) / den : 0;
                    for (var q = 0; q < 3; q++) work[j, q] = (1 - a) * work[j - 1, q] + a * work[j, q];
                }
            return (work[deg, 0] / work[deg, 2], work[deg, 1] / work[deg, 2]);
        }
        static double Dev((double X, double Y) p, (double X, double Y) a, (double X, double Y) c)
        {
            double dx = c.X - a.X, dy = c.Y - a.Y, l2 = dx * dx + dy * dy; var t = l2 > 0 ? Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1) : 0;
            return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
        }
        var pts = new List<(double X, double Y)>(); var over = false;
        void Seg(int k, double ta, (double X, double Y) pa, double tb, (double X, double Y) pb, int depth)
        {
            if (over) return;
            double tm = (ta + tb) / 2; var pm = Eval(k, tm);
            if (depth < 30 && (Dev(pm, pa, pb) > tolU || Dev(Eval(k, ta + (tb - ta) / 4), pa, pb) > tolU || Dev(Eval(k, ta + 3 * (tb - ta) / 4), pa, pb) > tolU))
            { Seg(k, ta, pa, tm, pm, depth + 1); Seg(k, tm, pm, tb, pb, depth + 1); return; }
            pts.Add(pb); if (pts.Count > budget) over = true;
        }
        var started = false;
        for (var k = deg; k < n; k++)
        {
            double ka = knots[k], kb = knots[k + 1]; if (!(kb > ka)) continue;
            var m = Math.Max(4, 2 * deg); // ein Knotenstück ist ein Polynom vom Grad p – so viele Vorteile fangen Wendepunkte und Schleifen
            var prev = Eval(k, ka); if (!started) { pts.Add(prev); started = true; }
            var prevT = ka;
            for (var j = 1; j <= m; j++)
            {
                var tb = j == m ? kb : ka + (kb - ka) * j / m; var pb = Eval(k, tb);
                Seg(k, prevT, prev, tb, pb, 0); prevT = tb; prev = pb;
                if (over) return (null, false, "zu groß zum Abtasten", hull);
            }
        }
        if (pts.Any(q => !double.IsFinite(q.X) || !double.IsFinite(q.Y))) return (null, false, "unbrauchbare Zahlen", Box.All);
        // Fitpunkte (falls angegeben) müssen auf der Kurve liegen – sonst passen Kontroll- und Fitpunkte nicht zusammen
        if (fit.Count > 0)
        {
            var ftol = Math.Max(Math.Abs(b.FirstOrDefault(x => x.Code == 44) is { Value: { } fv } ? Dn(fv) ?? 0 : 0), 0.01 / kUnits) + 2 * tolU;
            var grid = new Dictionary<(long, long), List<int>>(); var cell = Math.Max(ftol * 4, 1e-12);
            var tooFine = false;
            for (var i = 0; i + 1 < pts.Count && !tooFine; i++)
            {
                var (a, c) = (pts[i], pts[i + 1]);
                long x0 = (long)Math.Floor(Math.Min(a.X, c.X) / cell), x1 = (long)Math.Floor(Math.Max(a.X, c.X) / cell), y0 = (long)Math.Floor(Math.Min(a.Y, c.Y) / cell), y1 = (long)Math.Floor(Math.Max(a.Y, c.Y) / cell);
                if ((x1 - x0 + 1) * (y1 - y0 + 1) > 64) { tooFine = true; break; }
                for (var gx = x0; gx <= x1; gx++) for (var gy = y0; gy <= y1; gy++) { if (!grid.TryGetValue((gx, gy), out var l)) grid[(gx, gy)] = l = []; l.Add(i); }
            }
            foreach (var f in fit)
            {
                var best = double.MaxValue;
                if (tooFine) { for (var i = 0; i + 1 < pts.Count && best > ftol; i++) best = Math.Min(best, Dev(f, pts[i], pts[i + 1])); }
                else
                {
                    long fx = (long)Math.Floor(f.X / cell), fy = (long)Math.Floor(f.Y / cell);
                    for (var gx = fx - 1; gx <= fx + 1; gx++) for (var gy = fy - 1; gy <= fy + 1; gy++)
                            if (grid.TryGetValue((gx, gy), out var l)) foreach (var i in l) best = Math.Min(best, Dev(f, pts[i], pts[i + 1]));
                }
                if (best > ftol) return (null, false, "Fitpunkte liegen nicht auf der Kurve", Box.All);
            }
        }
        var gap = Math.Max(Math.Abs(pts[0].X - pts[^1].X), Math.Abs(pts[0].Y - pts[^1].Y));
        var closed = false;
        if ((flags & 1) != 0)
        {
            if (gap > 2 * tolU) return (null, false, "geschlossen, aber Anfang und Ende treffen sich nicht", hull);
            closed = pts.Count >= 4; if (closed) pts.RemoveAt(pts.Count - 1);
        }
        return (pts.Select(q => new[] { q.X, q.Y, 0 }).ToList(), closed, null, hull);

        static Box FitBox(List<(double X, double Y)> f) { var bx = Box.Empty; foreach (var q in f) bx = bx.With(q); return bx; }
    }

    /// <summary>T-0760: Mehrfachlinien-Stil: Kennung, Name, Flags (Enden, Gehrungen), Abstände der Linien (49), Winkel der Enden (51/52).</summary>
    internal sealed record MlStyle(string Handle, string Name, int Flags, List<double> Offsets, double StartAngle, double EndAngle);

    /// <summary>T-0760: alle MLINESTYLE-Objekte (Abschnitt OBJECTS) vorab einsammeln – nach Kennung („#“ + Gruppe 5) und Name.</summary>
    static Dictionary<string, MlStyle> MlineStyles(List<(int Code, string Value)> pairs)
    {
        var res = new Dictionary<string, MlStyle>(StringComparer.OrdinalIgnoreCase); var section = "";
        for (var i = 0; i < pairs.Count; i++)
        {
            var (c, v) = pairs[i];
            if (c != 0) continue;
            if (v == "SECTION") { section = i + 1 < pairs.Count && pairs[i + 1].Code == 2 ? pairs[i + 1].Value.Trim() : ""; continue; }
            if (v == "ENDSEC") { section = ""; continue; }
            if (section != "OBJECTS" || v != "MLINESTYLE") continue;
            var body = new List<(int Code, string Value)>(); var j = i + 1;
            while (j < pairs.Count && pairs[j].Code != 0) body.Add(pairs[j++]);
            var offs = new List<double>(); var okNum = true;
            foreach (var (bc, bv) in body) if (bc == 49) { if (double.TryParse(bv.Trim(), NumberStyles.Float, Inv, out var o) && double.IsFinite(o)) offs.Add(o); else okNum = false; }
            if (!okNum || Has(body, 71) && IntSafe(body, 71) != offs.Count) continue; // widersprüchlich: wie nicht vorhanden – dann gelten nur die Daten der MLINE (ohne Enden)
            double Ang(int code) => body.FirstOrDefault(x => x.Code == code) is { Value: { } s } && double.TryParse(s.Trim(), NumberStyles.Float, Inv, out var a) ? a : 90;
            var st = new MlStyle(Str(body, 5) ?? "", Str(body, 2) ?? "", IntSafe(body, 70), offs, Ang(51), Ang(52));
            if (st.Handle.Length > 0) res["#" + st.Handle] = st;
            if (st.Name.Length > 0) res.TryAdd(st.Name, st);
        }
        return res;
    }

    /// <summary>T-0760: MLINE lesen. Die Lage jeder Linie steht in der MLINE selbst: an jedem Scheitel (11) beginnt Linie j bei
    /// Scheitel + Gehrung (13) · erster Parameter (41) – so zeichnet auch AutoCAD. Streng geprüft: Gruppenfolge, Richtung (12) = Richtung zum nächsten
    /// Scheitel, Vektoren normiert, Gehrung nicht parallel, Abstände untereinander = Stil-Abstände (49) × Maßstab (40), keine Unterbrechungen.
    /// Enden (Linie, äußerer/innerer Bogen) und Gehrungslinien nach den Stil-Flags (ohne Stil: nur die Linien – offene Enden meldet das Fräsen).</summary>
    internal static (List<CadEntity>? Ents, string? Why, Box Box) ReadMline(List<(int Code, string Value)> b, Dictionary<string, MlStyle> styles, string layer, double kUnits = 1)
    {
        var i = b.FindIndex(x => x.Code == 11);
        try
        {
            var nv = IntSafe(b, 72); var ne = IntSafe(b, 73); var flags = IntSafe(b, 71); var scale = Num(b, 40, 1); var closed = (flags & 2) != 0;
            if (i < 0 || nv < 2 || ne < 1 || ne > 16 || nv > 1_000_000 || closed && nv < 3) return (null, "ohne lesbare Scheitel", Box.All);
            for (var j = 0; j < i; j++) if (b[j].Code is 74 or 75 or 41 or 42 or 12 or 13) return (null, "Gruppen in falscher Reihenfolge", Box.All);
            double N(int code) { if (i >= b.Count || b[i].Code != code) throw new CadError("MLINE"); var v = D(b[i].Value); i++; if (!double.IsFinite(v)) throw new CadError("MLINE"); return v; }
            (double X, double Y, double Z) V3(int code) { var x = N(code); var y = N(code + 10); var z = i < b.Count && b[i].Code == code + 20 ? N(code + 20) : 0; return (x, y, z); }
            int Count(int code) { var v = N(code); if (v != Math.Floor(v) || v < 0 || v > 1000) throw new CadError("MLINE"); return (int)v; }
            var vx = new List<(double X, double Y, double Z)>(); var dir = new List<(double X, double Y, double Z)>(); var mit = new List<(double X, double Y, double Z)>();
            var prm = new List<double[]>();
            for (var k = 0; k < nv; k++)
            {
                vx.Add(V3(11)); dir.Add(V3(12)); mit.Add(V3(13)); var row = new double[ne];
                for (var e = 0; e < ne; e++)
                {
                    var np = Count(74); var ps = new double[np]; for (var q = 0; q < np; q++) ps[q] = N(41);
                    if (np == 0) return (null, "Linie ohne Lage", Box.All);
                    if (np > 2 || np == 2 && Math.Abs(ps[1]) > 1e-12) return (null, "mit Unterbrechungen", Box.All); // Strich-Unterbrechungen liest Trendo nicht
                    row[e] = ps[0];
                    var nf = Count(75); for (var q = 0; q < nf; q++) N(42);
                }
                prm.Add(row);
            }
            for (; i < b.Count; i++) if (b[i].Code < 1000 && b[i].Code is not (102 or 330 or 360)) return (null, "unerwartete Gruppe nach den Scheiteln", Box.All);
            static double Len((double X, double Y, double Z) a) => Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
            static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) c) => (a.Y * c.Z - a.Z * c.Y, a.Z * c.X - a.X * c.Z, a.X * c.Y - a.Y * c.X);
            for (var k = 0; k < nv; k++)
            {
                if (Math.Abs(Len(dir[k]) - 1) > 1e-6 || Math.Abs(Len(mit[k]) - 1) > 1e-6) return (null, "Richtung oder Gehrung nicht normiert", Box.All);
                if (Len(Cross(dir[k], mit[k])) < 1e-6) return (null, "Gehrung parallel zur Richtung", Box.All);
                if (k == nv - 1 && !closed) continue;
                var nx = vx[(k + 1) % nv]; (double X, double Y, double Z) seg = (nx.X - vx[k].X, nx.Y - vx[k].Y, nx.Z - vx[k].Z); var l = Len(seg);
                if (!(l > 0)) return (null, "zwei Scheitel fallen zusammen", Box.All);
                if (Len(Cross((seg.X / l, seg.Y / l, seg.Z / l), dir[k])) > 1e-6 || seg.X * dir[k].X + seg.Y * dir[k].Y + seg.Z * dir[k].Z < 0) return (null, "Richtung passt nicht zu den Scheiteln", Box.All);
            }
            // Stil (Kennung 340, sonst Name 2): gleiche Linienzahl, Abstände untereinander senkrecht zur Richtung = (Stil-Abstand − erster) × Maßstab
            MlStyle? st = null;
            if (Str(b, 340) is { Length: > 0 } h && styles.TryGetValue("#" + h, out var s1)) st = s1;
            else if (Str(b, 2) is { Length: > 0 } nm && styles.TryGetValue(nm, out var s2)) st = s2;
            if (st is not null)
            {
                if (st.Offsets.Count != ne) return (null, "Linienzahl passt nicht zum Stil", Box.All);
                var tol = 0.001 / kUnits + 1e-9 * Math.Abs(scale) * st.Offsets.Max(o => Math.Abs(o));
                for (var k = 0; k < nv; k++)
                {
                    var sn = Cross(dir[k], mit[k]); var sinv = Len(sn) * (sn.Z >= 0 ? 1 : -1); // senkrechter Abstand = Parameter · sin(Winkel Richtung → Gehrung)
                    for (var e = 1; e < ne; e++)
                        if (Math.Abs((prm[k][e] - prm[k][0]) * sinv - (st.Offsets[e] - st.Offsets[0]) * scale) > tol) return (null, "Abstände passen nicht zum Stil", Box.All);
                }
            }
            (double X, double Y) P(int k, int e) => (vx[k].X + mit[k].X * prm[k][e], vx[k].Y + mit[k].Y * prm[k][e]);
            var res = new List<CadEntity>();
            for (var e = 0; e < ne; e++)
                res.Add(new CadEntity { Kind = "polylinie", Layer = layer, Closed = closed, Source = "MLINE", Points = Enumerable.Range(0, nv).Select(k => { var (x, y) = P(k, e); return new[] { x, y, 0 }; }).ToList() });
            if (st is not null && ne >= 2)
            {
                var f = st.Flags;
                void Lin((double X, double Y) a, (double X, double Y) c) { if (a != c) res.Add(new CadEntity { Kind = "linie", Layer = layer, Source = "MLINE", Points = [[a.X, a.Y, 0], [c.X, c.Y, 0]] }); }
                // Halbkreis von Linie e0 nach e1 am Scheitel k, nach außen (am Anfang gegen die Laufrichtung, am Ende mit ihr)
                void Semi(int k, int e0, int e1, double outward)
                {
                    var (a, c) = (P(k, e0), P(k, e1)); double dx = c.X - a.X, dy = c.Y - a.Y; if (dx == 0 && dy == 0) return;
                    var right = (dy * dir[k].X - dx * dir[k].Y) * outward; // Ausbuchtung > 0: Bogen rechts von a→c
                    res.Add(new CadEntity { Kind = "polylinie", Layer = layer, Source = "MLINE", Points = [[a.X, a.Y, right > 0 ? 1 : -1], [c.X, c.Y, 0]] });
                }
                void Cap(int k, int lineBit, int outerBit, int innerBit, double outward)
                {
                    if ((f & lineBit) != 0) Lin(P(k, 0), P(k, ne - 1));
                    if ((f & outerBit) != 0) Semi(k, 0, ne - 1, outward);
                    if ((f & innerBit) != 0) for (var e = 1; e < ne - 1 - e; e++) Semi(k, e, ne - 1 - e, outward);
                }
                if (!closed)
                {
                    if ((f & (32 | 64 | 512 | 1024)) != 0 && (Math.Abs(st.StartAngle - 90) > 1e-9 || Math.Abs(st.EndAngle - 90) > 1e-9)) return (null, "schräge Bogenenden", Box.All);
                    if ((flags & 4) == 0) Cap(0, 16, 64, 32, -1); // Anfang (nicht unterdrückt)
                    if ((flags & 8) == 0) Cap(nv - 1, 256, 1024, 512, 1); // Ende
                }
                if ((f & 2) != 0) for (var k = closed ? 0 : 1; k < (closed ? nv : nv - 1); k++) Lin(P(k, 0), P(k, ne - 1)); // Gehrungen anzeigen
            }
            return (res, null, Box.All);
        }
        catch (CadError) { return (null, "Gruppen unvollständig", Box.All); }
    }
}
