namespace Trendo.Core.Cad;

/// <summary>Grundkörper als geschlossene, nach außen zeigende Polygonnetze (Millimeter, Z nach oben).</summary>
public static class CadShapes
{
    static Poly P(params Vec3[] v) => Poly.TryCreate([.. v])!;

    public static Solid Box(Vec3 o, double w, double d, double h)
    {
        Vec3 V(double x, double y, double z) => new(o.X + x * w, o.Y + y * d, o.Z + z * h);
        return new Solid([
            P(V(0, 0, 0), V(0, 1, 0), V(1, 1, 0), V(1, 0, 0)), // unten
            P(V(0, 0, 1), V(1, 0, 1), V(1, 1, 1), V(0, 1, 1)), // oben
            P(V(0, 0, 0), V(1, 0, 0), V(1, 0, 1), V(0, 0, 1)), // vorn (−Y)
            P(V(0, 1, 0), V(0, 1, 1), V(1, 1, 1), V(1, 1, 0)), // hinten
            P(V(0, 0, 0), V(0, 0, 1), V(0, 1, 1), V(0, 1, 0)), // links
            P(V(1, 0, 0), V(1, 1, 0), V(1, 1, 1), V(1, 0, 1)), // rechts
        ]);
    }

    public static Solid Cylinder(Vec3 c, double r, double h, int seg) => Cone(c, r, r, h, seg);

    /// <summary>Kegelstumpf von Radius r1 (unten) zu r2 (oben); r2 = 0 ergibt eine Spitze.</summary>
    public static Solid Cone(Vec3 c, double r1, double r2, double h, int seg)
    {
        var s = new Solid();
        Vec3 At(double r, int i, double z) => new(c.X + r * Math.Cos(2 * Math.PI * i / seg), c.Y + r * Math.Sin(2 * Math.PI * i / seg), c.Z + z);
        s.Polys.Add(Poly.TryCreate(Enumerable.Range(0, seg).Reverse().Select(i => At(r1, i, 0)).ToList())!);
        if (r2 > 1e-9) s.Polys.Add(Poly.TryCreate(Enumerable.Range(0, seg).Select(i => At(r2, i, h)).ToList())!);
        for (var i = 0; i < seg; i++)
        {
            var j = (i + 1) % seg;
            if (r2 > 1e-9) { if (Poly.TryCreate([At(r1, i, 0), At(r1, j, 0), At(r2, j, h), At(r2, i, h)]) is { } q) s.Polys.Add(q); }
            else if (Poly.TryCreate([At(r1, i, 0), At(r1, j, 0), new Vec3(c.X, c.Y, c.Z + h)]) is { } t) s.Polys.Add(t);
        }
        return s;
    }

    public static Solid Sphere(Vec3 c, double r, int seg)
    {
        var s = new Solid(); var stacks = Math.Max(6, seg / 2);
        Vec3 At(int i, int j) { var th = 2 * Math.PI * i / seg; var ph = Math.PI * j / stacks; return new(c.X + r * Math.Cos(th) * Math.Sin(ph), c.Y + r * Math.Sin(th) * Math.Sin(ph), c.Z + r * Math.Cos(ph)); }
        for (var i = 0; i < seg; i++)
            for (var j = 0; j < stacks; j++)
            {
                var v = new List<Vec3> { At(i, j) };
                if (j > 0) v.Add(At(i + 1, j));
                if (j < stacks - 1) v.Add(At(i + 1, j + 1));
                v.Add(At(i, j + 1));
                v.Reverse(); // nach außen
                if (Poly.TryCreate(v) is { } p) s.Polys.Add(p);
            }
        return s;
    }

    /// <summary>Umriss (gegen den Uhrzeigersinn) senkrecht hochziehen. Nicht konvexe Umrisse werden für Deckel/Boden in Dreiecke zerlegt.</summary>
    public static Solid Extrude(IReadOnlyList<(double X, double Y)> pts, double h)
    {
        var s = new Solid(); var n = pts.Count;
        foreach (var (a, b, c) in Triangulate(pts))
        {
            if (Poly.TryCreate([new(a.X, a.Y, h), new(b.X, b.Y, h), new(c.X, c.Y, h)]) is { } top) s.Polys.Add(top);
            if (Poly.TryCreate([new(c.X, c.Y, 0), new(b.X, b.Y, 0), new(a.X, a.Y, 0)]) is { } bot) s.Polys.Add(bot);
        }
        for (var i = 0; i < n; i++)
        {
            var (x1, y1) = pts[i]; var (x2, y2) = pts[(i + 1) % n];
            if (Poly.TryCreate([new(x1, y1, 0), new(x2, y2, 0), new(x2, y2, h), new(x1, y1, h)]) is { } side) s.Polys.Add(side);
        }
        return s;
    }

    /// <summary>Umriss in der XZ-Ebene (X = Abstand zur Achse, Y der Skizze = Höhe) um die Z-Achse drehen.</summary>
    public static Solid Revolve(IReadOnlyList<(double X, double Y)> prof, double angleDeg, int seg)
    {
        var s = new Solid(); var full = angleDeg >= 359.999;
        var steps = Math.Max(3, (int)Math.Ceiling(seg * angleDeg / 360));
        var a = angleDeg * Math.PI / 180;
        Vec3 At((double X, double Y) p, int k) { var t = a * k / steps; return new(p.X * Math.Cos(t), p.X * Math.Sin(t), p.Y); }
        for (var k = 0; k < steps; k++)
            for (var i = 0; i < prof.Count; i++)
            {
                var p = prof[i]; var q = prof[(i + 1) % prof.Count];
                var v = new List<Vec3> { At(p, k), At(q, k), At(q, k + 1), At(p, k + 1) };
                // Punkte auf der Achse zusammenfassen
                var clean = new List<Vec3>(); foreach (var x in v) if (clean.Count == 0 || (clean[^1] - x).Len > 1e-9) clean.Add(x);
                if (clean.Count > 1 && (clean[0] - clean[^1]).Len < 1e-9) clean.RemoveAt(clean.Count - 1);
                clean.Reverse();
                if (Poly.TryCreate(clean) is { } poly) s.Polys.Add(poly);
            }
        if (!full)
        {
            foreach (var (x, y, z) in Triangulate(prof).Select(t => (t.A, t.B, t.C)))
            {
                if (Poly.TryCreate([At(x, 0), At(y, 0), At(z, 0)]) is { } start) s.Polys.Add(start);       // Normale zeigt nach −Y (außen)
                if (Poly.TryCreate([At(z, steps), At(y, steps), At(x, steps)]) is { } end) s.Polys.Add(end);
            }
        }
        return s;
    }

    /// <summary>Werkzeug für Rundung/Fase: Quader mit gerundeten bzw. gefasten senkrechten Kanten (und auf Wunsch Oberkanten). Schnitt mit dem Körper ergibt die Kanten.</summary>
    public static Solid EdgeCutter(Vec3 min, Vec3 max, double r, bool round, bool top, int seg)
    {
        var w = max.X - min.X; var d = max.Y - min.Y; r = Math.Min(r, Math.Min(w, d) / 2 - 1e-3);
        if (r <= 0) throw new CadError("Der Körper ist zu schmal für diese Rundung.");
        var pts = new List<(double, double)>();
        void Corner(double cx, double cy, double a0)
        {
            if (round) for (var k = 0; k <= seg / 4; k++) { var a = a0 + Math.PI / 2 * k / (seg / 4); pts.Add((cx + r * Math.Cos(a), cy + r * Math.Sin(a))); }
            else { pts.Add((cx + r * Math.Cos(a0), cy + r * Math.Sin(a0))); pts.Add((cx + r * Math.Cos(a0 + Math.PI / 2), cy + r * Math.Sin(a0 + Math.PI / 2))); }
        }
        Corner(max.X - r, min.Y + r, -Math.PI / 2); Corner(max.X - r, max.Y - r, 0); Corner(min.X + r, max.Y - r, Math.PI / 2); Corner(min.X + r, min.Y + r, Math.PI);
        var h = max.Z - min.Z;
        var body = Extrude(pts, h + 2).Transform(v => v + new Vec3(0, 0, min.Z - 1));
        if (!top) return body;
        // Oberkanten: von oben nach innen geneigte Umrandung (Fase) bzw. Viertelkreis-Stufen (Rundung)
        var steps = round ? Math.Max(3, seg / 4) : 1;
        var shape = new Solid(Extrude(pts, h - r + 1).Transform(v => v + new Vec3(0, 0, min.Z - 1)).Polys);
        for (var k = 1; k <= steps; k++)
        {
            var t0 = (double)(k - 1) / steps; var t1 = (double)k / steps;
            double Inset(double t) => round ? r - r * Math.Cos(Math.Asin(Math.Min(1, t))) : r * t;
            var z0 = max.Z - r + r * t0; var z1 = max.Z - r + r * t1;
            var ins = Inset(t1);
            var ring = pts.Select(p => (Clamp(p.Item1, min.X + ins, max.X - ins), Clamp(p.Item2, min.Y + ins, max.Y - ins))).ToList();
            shape = shape.Union(Extrude(ring, z1 - z0 + 1e-3).Transform(v => v + new Vec3(0, 0, z0)));
        }
        return shape;
        static double Clamp(double v, double a, double b) => a > b ? (a + b) / 2 : Math.Clamp(v, a, b);
    }

    /// <summary>Ohrenschnitt-Zerlegung eines einfachen Polygons (gegen den Uhrzeigersinn) in Dreiecke.</summary>
    public static IEnumerable<((double X, double Y) A, (double X, double Y) B, (double X, double Y) C)> Triangulate(IReadOnlyList<(double X, double Y)> pts)
    {
        var idx = Enumerable.Range(0, pts.Count).ToList(); var guard = 0;
        static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        static bool Inside((double X, double Y) p, (double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0;
        while (idx.Count > 3 && guard++ < 10_000)
        {
            var found = false;
            for (var i = 0; i < idx.Count; i++)
            {
                var a = pts[idx[(i + idx.Count - 1) % idx.Count]]; var b = pts[idx[i]]; var c = pts[idx[(i + 1) % idx.Count]];
                if (Cross(a, b, c) <= 1e-12) continue;
                var ear = true;
                foreach (var j in idx) { var p = pts[j]; if (p == a || p == b || p == c) continue; if (Inside(p, a, b, c)) { ear = false; break; } }
                if (!ear) continue;
                yield return (a, b, c); idx.RemoveAt(i); found = true; break;
            }
            if (!found) break;
        }
        if (idx.Count == 3) yield return (pts[idx[0]], pts[idx[1]], pts[idx[2]]);
    }
}
