namespace Trendo.Core;

/// <summary>Punkt im Raum, in Zentimetern.</summary>
public readonly struct Vec3(double x, double y, double z)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Z { get; } = z;
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator /(Vec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
    public double Len2 => X * X + Y * Y + Z * Z;
    public double Len => Math.Sqrt(Len2);
    public Vec3 Norm() { var l = Len; return l < 1e-12 ? new(0, 0, 0) : this / l; }
    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public override string ToString() => FormattableString.Invariant($"{X:0.###},{Y:0.###},{Z:0.###}");
}

/// <summary>Flächennetz aus Vielecken. Flächen sind gegen den Uhrzeigersinn, von außen gesehen.</summary>
public sealed class Poly3D
{
    public List<Vec3> V { get; } = [];
    public List<int[]> F { get; } = [];

    public Poly3D Add(params Vec3[] vs)
    {
        var idx = new int[vs.Length];
        for (var i = 0; i < vs.Length; i++) { idx[i] = V.Count; V.Add(vs[i]); }
        if (idx.Length >= 3) F.Add(idx);
        return this;
    }

    public Mesh3D ToMesh()
    {
        var m = new Mesh3D();
        foreach (var p in V) m.V.Add(p);
        foreach (var f in F)
            for (var i = 1; i < f.Length - 1; i++) m.T.Add((f[0], f[i], f[i + 1]));
        return m;
    }

    public static Poly3D FromMesh(Mesh3D m)
    {
        var p = new Poly3D();
        p.V.AddRange(m.V);
        foreach (var (a, b, c) in m.T) p.F.Add([a, b, c]);
        return p;
    }

    public Poly3D Clone()
    {
        var p = new Poly3D();
        p.V.AddRange(V);
        foreach (var f in F) p.F.Add((int[])f.Clone());
        return p;
    }

    public static Vec3 Normal(IReadOnlyList<Vec3> v, IReadOnlyList<int> f)
    {
        var n = new Vec3(0, 0, 0);
        for (var i = 0; i < f.Count; i++) n += Vec3.Cross(v[f[i]], v[f[(i + 1) % f.Count]]);
        return n.Norm();
    }

    public Vec3 FaceNormal(int fi) => Normal(V, F[fi]);

    /// <summary>Würfel, Mitte im Ursprung. bx, by, bz sind die Kantenlängen in cm.</summary>
    public static Poly3D Box(double bx, double by, double bz)
    {
        var (hx, hy, hz) = (Math.Abs(bx) / 2, Math.Abs(by) / 2, Math.Abs(bz) / 2);
        var p = new Poly3D();
        // 0–3 unten, 4–7 oben
        Vec3[] c =
        [
            new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, hy, -hz), new(-hx, hy, -hz),
            new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz),
        ];
        p.V.AddRange(c);
        p.F.Add([4, 5, 6, 7]); // oben +Z
        p.F.Add([1, 0, 3, 2]); // unten -Z
        p.F.Add([0, 1, 5, 4]); // vorn -Y
        p.F.Add([2, 3, 7, 6]); // hinten +Y
        p.F.Add([1, 2, 6, 5]); // rechts +X
        p.F.Add([3, 0, 4, 7]); // links -X
        return p;
    }

    public static Poly3D Plane(double bx, double by)
    {
        var (hx, hy) = (Math.Abs(bx) / 2, Math.Abs(by) / 2);
        var p = new Poly3D();
        p.V.Add(new(-hx, -hy, 0)); p.V.Add(new(hx, -hy, 0)); p.V.Add(new(hx, hy, 0)); p.V.Add(new(-hx, hy, 0));
        p.F.Add([0, 1, 2, 3]);
        return p;
    }

    public static Poly3D Sphere(double bx, double by, double bz, int seg = 16, int rings = 10)
    {
        var p = new Poly3D();
        var (rx, ry, rz) = (Math.Abs(bx) / 2, Math.Abs(by) / 2, Math.Abs(bz) / 2);
        for (var i = 0; i <= rings; i++)
        {
            var v = Math.PI * i / rings;
            var sv = Math.Sin(v); var cv = Math.Cos(v);
            for (var j = 0; j < seg; j++)
            {
                var u = 2 * Math.PI * j / seg;
                p.V.Add(new(rx * sv * Math.Cos(u), ry * sv * Math.Sin(u), rz * cv));
            }
        }
        int Id(int i, int j) => i * seg + (j % seg);
        for (var i = 0; i < rings; i++)
            for (var j = 0; j < seg; j++)
            {
                if (i == 0) p.F.Add([Id(0, 0), Id(1, j), Id(1, j + 1)]);
                else if (i == rings - 1) p.F.Add([Id(i, j + 1), Id(i, j), Id(rings, 0)]);
                else p.F.Add([Id(i, j), Id(i + 1, j), Id(i + 1, j + 1), Id(i, j + 1)]);
            }
        // Pole vertices are duplicated per segment index 0 only; Id(0, j) all map through j%seg but the pole ring stored seg copies.
        // Rebuild poles as single vertices so the mesh is cleaner: acceptable as-is (duplicates). Weld later if needed.
        return p;
    }

    public static Poly3D Cylinder(double bx, double by, double bz, int seg = 24)
    {
        var p = new Poly3D();
        var (rx, ry, hz) = (Math.Abs(bx) / 2, Math.Abs(by) / 2, Math.Abs(bz) / 2);
        for (var j = 0; j < seg; j++)
        {
            var u = 2 * Math.PI * j / seg;
            var (x, y) = (rx * Math.Cos(u), ry * Math.Sin(u));
            p.V.Add(new(x, y, -hz)); p.V.Add(new(x, y, hz));
        }
        var bot = new int[seg]; var top = new int[seg];
        for (var j = 0; j < seg; j++) { bot[seg - 1 - j] = j * 2; top[j] = j * 2 + 1; }
        p.F.Add(bot); p.F.Add(top);
        for (var j = 0; j < seg; j++)
        {
            var a = j * 2; var b = ((j + 1) % seg) * 2;
            p.F.Add([a, b, b + 1, a + 1]);
        }
        return p;
    }

    public static Poly3D Cone(double bx, double by, double bz, int seg = 24)
    {
        var p = new Poly3D();
        var (rx, ry, hz) = (Math.Abs(bx) / 2, Math.Abs(by) / 2, Math.Abs(bz) / 2);
        var tip = p.V.Count; p.V.Add(new(0, 0, hz));
        var start = p.V.Count;
        for (var j = 0; j < seg; j++)
        {
            var u = 2 * Math.PI * j / seg;
            p.V.Add(new(rx * Math.Cos(u), ry * Math.Sin(u), -hz));
        }
        var bot = new int[seg];
        for (var j = 0; j < seg; j++) bot[j] = start + seg - 1 - j;
        p.F.Add(bot);
        for (var j = 0; j < seg; j++) p.F.Add([start + j, start + (j + 1) % seg, tip]);
        return p;
    }

    public static Poly3D Torus(double bx, double by, double bz, int major = 24, int minor = 12)
    {
        var tube = Math.Max(0.05, Math.Abs(bz) / 2);
        var rx = Math.Max(tube + 0.05, Math.Abs(bx) / 2 - tube);
        var ry = Math.Max(tube + 0.05, Math.Abs(by) / 2 - tube);
        var p = new Poly3D();
        for (var i = 0; i < major; i++)
        {
            var u = 2 * Math.PI * i / major;
            var (cu, su) = (Math.Cos(u), Math.Sin(u));
            for (var j = 0; j < minor; j++)
            {
                var v = 2 * Math.PI * j / minor;
                var cv = Math.Cos(v); var sv = Math.Sin(v);
                p.V.Add(new((rx + tube * cv) * cu, (ry + tube * cv) * su, tube * sv));
            }
        }
        int Id(int i, int j) => (i % major) * minor + (j % minor);
        for (var i = 0; i < major; i++)
            for (var j = 0; j < minor; j++)
                p.F.Add([Id(i, j), Id(i, j + 1), Id(i + 1, j + 1), Id(i + 1, j)]);
        return p;
    }

    /// <summary>Einfache Blockschrift. Jeder gesetzte Punkt wird ein kleiner Klotz.</summary>
    public static Poly3D Text(string text, double bx, double by, double bz)
    {
        text = string.IsNullOrWhiteSpace(text) ? "Text" : text.Trim();
        if (text.Length > 24) text = text[..24];
        var glyphs = text.Select(BlockFont.Rows).ToList();
        var cols = glyphs.Sum(g => g is null ? 3 : 6); // 5 Pixel + 1 Lücke, Leerzeichen 3
        var cellX = Math.Abs(bx) / Math.Max(1, cols);
        var cellY = Math.Abs(by) / 7.0;
        var depth = Math.Max(0.05, Math.Abs(bz));
        var p = new Poly3D();
        double x0 = -Math.Abs(bx) / 2;
        foreach (var rows in glyphs)
        {
            if (rows is null) { x0 += cellX * 3; continue; }
            for (var row = 0; row < 7; row++)
            {
                var bits = rows[row];
                for (var col = 0; col < 5; col++)
                {
                    if ((bits & (1 << (4 - col))) == 0) continue;
                    var x = x0 + col * cellX; var y = Math.Abs(by) / 2 - (row + 1) * cellY;
                    var box = Box(cellX * 0.92, cellY * 0.92, depth);
                    foreach (var v in box.V) p.V.Add(new(v.X + x + cellX / 2, v.Y + y + cellY / 2, v.Z));
                    var b = p.V.Count - 8;
                    foreach (var f in box.F) p.F.Add(f.Select(i => i + b).ToArray());
                }
            }
            x0 += cellX * 6;
        }
        if (p.F.Count == 0) return Box(Math.Abs(bx), Math.Abs(by), depth);
        return p;
    }

    /// <summary>Kanten abrunden: jede Ecke wird angeschnitten, jede Kante wird eine Fase.</summary>
    public static Poly3D Bevel(Poly3D src, double width)
    {
        if (width <= 0) throw new ArgumentException("Die Abrundung muss größer als 0 sein.");
        if (src.F.Count > 8000) throw new InvalidOperationException("Das Teil hat zu viele Flächen zum Abrunden. Glätten geht dann nicht mehr zurück.");
        var map = new Dictionary<(int f, int c), int>();
        var dst = new Poly3D();
        for (var fi = 0; fi < src.F.Count; fi++)
        {
            var f = src.F[fi];
            var n = src.FaceNormal(fi);
            if (n.Len < 1e-8) continue;
            var lines = new (Vec3 P, Vec3 D, double W)[f.Length];
            for (var i = 0; i < f.Length; i++)
            {
                var a = src.V[f[i]]; var b = src.V[f[(i + 1) % f.Length]];
                var dir = (b - a).Norm();
                var inward = Vec3.Cross(n, dir).Norm();
                var w = Math.Min(width, (b - a).Len * 0.45);
                lines[i] = (a + inward * w, dir, w);
            }
            for (var c = 0; c < f.Length; c++)
            {
                var prev = lines[(c - 1 + f.Length) % f.Length];
                var next = lines[c];
                Vec3 hit;
                if (!LineHit(prev.P, prev.D, next.P, next.D, out hit))
                {
                    var v = src.V[f[c]];
                    var cen = Centroid(src.V, f);
                    var to = (cen - v).Norm();
                    hit = v + to * Math.Min(width, (cen - v).Len * 0.45);
                }
                map[(fi, c)] = dst.V.Count;
                dst.V.Add(hit);
            }
            var face = new int[f.Length];
            for (var c = 0; c < f.Length; c++) face[c] = map[(fi, c)];
            dst.F.Add(face);
        }
        var edges = new Dictionary<(int, int), List<(int f, int a, int b)>>();
        for (var fi = 0; fi < src.F.Count; fi++)
        {
            var f = src.F[fi];
            for (var i = 0; i < f.Length; i++)
            {
                var a = f[i]; var b = f[(i + 1) % f.Length];
                var key = a < b ? (a, b) : (b, a);
                if (!edges.TryGetValue(key, out var list)) edges[key] = list = [];
                list.Add((fi, i, (i + 1) % f.Length));
            }
        }
        var center = Center(src.V);
        foreach (var list in edges.Values)
        {
            if (list.Count != 2) continue;
            var (f0, a0, b0) = list[0]; var (f1, a1, b1) = list[1];
            if (!map.ContainsKey((f0, a0)) || !map.ContainsKey((f1, a1))) continue;
            var q = new[] { map[(f0, a0)], map[(f0, b0)], map[(f1, a1)], map[(f1, b1)] };
            // Die Kante liegt in beiden Flächen, aber die Reihenfolge ist dort gegenläufig.
            // Wir probieren die Verbindung und drehen sie nach außen.
            // Benachbarte Flächen laufen über die Kante in entgegengesetzter Richtung.
            var poly = new[] { q[0], q[1], q[2], q[3] };
            dst.F.Add(FacingOut(dst.V, poly, center));
        }
        var around = new Dictionary<int, List<(int f, int c)>>();
        for (var fi = 0; fi < src.F.Count; fi++)
        {
            var f = src.F[fi];
            for (var c = 0; c < f.Length; c++)
            {
                if (!around.TryGetValue(f[c], out var list)) around[f[c]] = list = [];
                list.Add((fi, c));
            }
        }
        foreach (var (vi, list) in around)
        {
            if (list.Count < 3 || list.Any(x => !map.ContainsKey((x.f, x.c)))) continue;
            var vn = new Vec3(0, 0, 0);
            foreach (var (f, _) in list) vn += src.FaceNormal(f);
            vn = vn.Norm();
            var origin = src.V[vi];
            var ordered = list.OrderBy(x => AngleAround(dst.V[map[(x.f, x.c)]] - origin, vn)).ToList();
            var poly = ordered.Select(x => map[(x.f, x.c)]).ToArray();
            dst.F.Add(FacingOut(dst.V, poly, center));
        }
        if (dst.F.Count == 0) throw new InvalidOperationException("Abrunden hat keine Fläche ergeben.");
        return dst;
    }

    /// <summary>Macht das Netz feiner und zieht Ecken etwas glatt.</summary>
    public static Poly3D Smooth(Poly3D src)
    {
        var mesh = src.ToMesh();
        if (mesh.T.Count * 4 > 12000) throw new InvalidOperationException("Glätten würde das Teil zu schwer machen. Einmal reicht oft.");
        var mids = new Dictionary<(int, int), int>();
        int Mid(int a, int b)
        {
            var k = a < b ? (a, b) : (b, a);
            if (mids.TryGetValue(k, out var id)) return id;
            id = mesh.V.Count;
            mesh.V.Add((mesh.V[a] + mesh.V[b]) / 2);
            mids[k] = id;
            return id;
        }
        var tris = new List<(int, int, int)>();
        var neighbors = new List<int>[src.V.Count];
        for (var i = 0; i < neighbors.Length; i++) neighbors[i] = [];
        foreach (var (a, b, c) in mesh.T.ToList())
        {
            // mesh.T still the original triangles; midpoints append vertices
        }
        var original = src.ToMesh();
        mesh = new Mesh3D();
        mesh.V.AddRange(original.V);
        mids.Clear();
        foreach (var (a, b, c) in original.T)
        {
            void Link(int u, int v) { if (!neighbors[u].Contains(v)) neighbors[u].Add(v); }
            Link(a, b); Link(b, a); Link(b, c); Link(c, b); Link(c, a); Link(a, c);
            var ab = Mid(a, b); var bc = Mid(b, c); var ca = Mid(c, a);
            tris.Add((a, ab, ca)); tris.Add((b, bc, ab)); tris.Add((c, ca, bc)); tris.Add((ab, bc, ca));
        }
        for (var i = 0; i < original.V.Count; i++)
        {
            if (neighbors[i].Count == 0) continue;
            var avg = new Vec3(0, 0, 0);
            foreach (var n in neighbors[i]) avg += original.V[n];
            avg /= neighbors[i].Count;
            mesh.V[i] = original.V[i] * 0.5 + avg * 0.5;
        }
        mesh.T.AddRange(tris);
        return FromMesh(mesh);
    }

    /// <summary>Zweite Hülle nach innen bzw. eine Dicke für eine Fläche. thickness in cm.</summary>
    public static Poly3D Solidify(Poly3D src, double thickness)
    {
        if (thickness <= 0) throw new ArgumentException("Die Wandstärke muss größer als 0 sein.");
        var vn = new Vec3[src.V.Count];
        foreach (var f in src.F)
        {
            var n = Normal(src.V, f);
            var area = 0.0;
            for (var i = 0; i < f.Length; i++) area += Vec3.Cross(src.V[f[i]], src.V[f[(i + 1) % f.Length]]).Len;
            foreach (var i in f) vn[i] += n * area;
        }
        for (var i = 0; i < vn.Length; i++) vn[i] = vn[i].Norm();
        var dst = new Poly3D();
        dst.V.AddRange(src.V);
        foreach (var v in src.V.Select((v, i) => v - vn[i] * thickness)) dst.V.Add(v);
        var o = src.V.Count;
        foreach (var f in src.F) dst.F.Add((int[])f.Clone());
        foreach (var f in src.F) dst.F.Add(f.Reverse().Select(i => i + o).ToArray());
        var count = new Dictionary<(int, int), int>();
        foreach (var f in src.F)
            for (var i = 0; i < f.Length; i++)
            {
                var a = f[i]; var b = f[(i + 1) % f.Length];
                var k = a < b ? (a, b) : (b, a);
                count[k] = count.GetValueOrDefault(k) + 1;
            }
        foreach (var f in src.F)
            for (var i = 0; i < f.Length; i++)
            {
                var a = f[i]; var b = f[(i + 1) % f.Length];
                var k = a < b ? (a, b) : (b, a);
                if (count[k] != 1) continue;
                dst.F.Add(FacingOut(dst.V, [a, b, b + o, a + o], Center(src.V)));
            }
        return dst;
    }

    /// <summary>Bohrt ein durchgehendes Loch. Achse x, y oder z, Radius in cm. u und v verschieben den Bohrer in der Ebene.</summary>
    public static Poly3D Drill(Poly3D src, char axis, double radius, double u, double v)
    {
        if (radius <= 0) throw new ArgumentException("Der Bohrer braucht einen Radius größer als 0.");
        axis = char.ToLowerInvariant(axis);
        if (axis is not ('x' or 'y' or 'z')) throw new ArgumentException("Die Bohr-Achse ist X, Y oder Z.");
        var mesh = Refine(src.ToMesh(), Math.Max(0.25, radius * 0.45));
        var kept = new Mesh3D();
        kept.V.AddRange(mesh.V);
        for (var i = 0; i < mesh.T.Count; i++)
        {
            var (a, b, c) = mesh.T[i];
            if (InCylinder(mesh, a, b, c, axis, radius, u, v)) continue;
            kept.T.Add((a, b, c));
            if (mesh.Farbe.Count == mesh.T.Count) kept.Farbe.Add(mesh.Farbe[i]);
        }
        kept = kept.Weld();
        if (kept.T.Count == 0) throw new InvalidOperationException("Nach dem Bohren ist nichts mehr übrig. Nimm einen kleineren Bohrer.");
        var loops = kept.BoundaryLoops();
        if (loops.Count == 0) throw new InvalidOperationException("Der Bohrer hat das Teil verfehlt. Schieb ihn auf die Fläche oder nimm einen größeren Radius.");
        foreach (var loop in loops)
            foreach (var i in loop)
                kept.V[i] = OnRadius(kept.V[i], axis, radius, u, v);
        var ranked = loops.Select(loop => (loop, h: loop.Average(i => AxisValue(kept.V[i], axis)))).OrderBy(x => x.h).ToList();
        if (ranked.Count == 1)
            throw new InvalidOperationException("Das Loch hat das Teil nur angeschnitten, nicht durchbohrt. Nimm einen kleineren Bohrer oder eine dickere Stelle.");
        for (var i = 0; i + 1 < ranked.Count; i += 2)
            Zip(kept, ranked[i].loop, ranked[i + 1].loop, axis, u, v);
        return FromMesh(kept.Weld());
    }

    private static Mesh3D Refine(Mesh3D src, double maxEdge)
    {
        var m = src;
        for (var pass = 0; pass < 6 && m.T.Count < 6000; pass++)
        {
            var longEdge = false;
            foreach (var (a, b, c) in m.T)
                if ((m.V[a] - m.V[b]).Len > maxEdge || (m.V[b] - m.V[c]).Len > maxEdge || (m.V[c] - m.V[a]).Len > maxEdge) { longEdge = true; break; }
            if (!longEdge) break;
            var next = new Mesh3D();
            next.V.AddRange(m.V);
            var mids = new Dictionary<(int, int), int>();
            int Mid(int a, int b)
            {
                if ((m.V[a] - m.V[b]).Len <= maxEdge) return -1;
                var k = a < b ? (a, b) : (b, a);
                if (mids.TryGetValue(k, out var id)) return id;
                id = next.V.Count;
                next.V.Add((m.V[a] + m.V[b]) / 2);
                mids[k] = id;
                return id;
            }
            foreach (var (a, b, c) in m.T)
            {
                var ab = Mid(a, b); var bc = Mid(b, c); var ca = Mid(c, a);
                if (ab < 0 && bc < 0 && ca < 0) { next.T.Add((a, b, c)); continue; }
                // Fehlende Mittelpunkte liegen auf der Ecke, damit jede Fläche Dreiecke bleibt.
                int P(int mid, int v) => mid >= 0 ? mid : v;
                if (ab >= 0 && bc >= 0 && ca >= 0)
                {
                    next.T.Add((a, ab, ca)); next.T.Add((b, bc, ab)); next.T.Add((c, ca, bc)); next.T.Add((ab, bc, ca));
                }
                else if (ab >= 0 && bc < 0 && ca < 0) { next.T.Add((a, ab, c)); next.T.Add((ab, b, c)); }
                else if (bc >= 0 && ab < 0 && ca < 0) { next.T.Add((b, bc, a)); next.T.Add((bc, c, a)); }
                else if (ca >= 0 && ab < 0 && bc < 0) { next.T.Add((c, ca, b)); next.T.Add((ca, a, b)); }
                else if (ab >= 0 && bc >= 0) { next.T.Add((a, ab, c)); next.T.Add((ab, bc, c)); next.T.Add((ab, b, bc)); }
                else if (bc >= 0 && ca >= 0) { next.T.Add((b, bc, a)); next.T.Add((bc, ca, a)); next.T.Add((bc, c, ca)); }
                else { next.T.Add((c, ca, b)); next.T.Add((ca, ab, b)); next.T.Add((ca, a, ab)); }
                _ = P(0, 0);
            }
            m = next;
        }
        return m;
    }

    private static void Zip(Mesh3D mesh, List<int> loopA, List<int> loopB, char axis, double u, double v)
    {
        var A = loopA.Select(i => (i, a: NormAng(Angle(axis, Shift(mesh.V[i], axis, u, v))))).OrderBy(t => t.a).ToList();
        var B = loopB.Select(i => (i, a: NormAng(Angle(axis, Shift(mesh.V[i], axis, u, v))))).OrderBy(t => t.a).ToList();
        if (A.Count < 3 || B.Count < 3) return;
        var best = 0; var bestD = double.MaxValue;
        for (var i = 0; i < B.Count; i++)
        {
            var gap = Math.Min(Forward(A[0].a, B[i].a), Forward(B[i].a, A[0].a));
            if (gap < bestD) { bestD = gap; best = i; }
        }
        if (best != 0) B = B.Skip(best).Concat(B.Take(best)).ToList();
        var cen = new Vec3(0, 0, 0);
        foreach (var p in A) cen += mesh.V[p.i];
        cen /= A.Count;
        int ia = 0, ib = 0, doneA = 0, doneB = 0;
        while (doneA < A.Count || doneB < B.Count)
        {
            var na = (ia + 1) % A.Count;
            var nb = (ib + 1) % B.Count;
            var stepA = doneA < A.Count && (doneB >= B.Count || Sweep(A, ia + 1) <= Sweep(B, ib + 1));
            if (stepA)
            {
                mesh.T.Add(FaceOut(mesh.V, A[ia].i, A[na].i, B[ib].i, TowardAxis(mesh.V[A[ia].i], cen, axis)));
                ia = na; doneA++;
            }
            else
            {
                mesh.T.Add(FaceOut(mesh.V, B[ib].i, A[ia].i, B[nb].i, TowardAxis(mesh.V[B[ib].i], cen, axis)));
                ib = nb; doneB++;
            }
        }
    }

    private static double Sweep(List<(int i, double a)> loop, int steps)
    {
        var n = loop.Count;
        var idx = steps % n;
        var a = loop[idx].a;
        if (idx == 0 && steps > 0) return loop[0].a + 2 * Math.PI;
        if (a < loop[0].a) a += 2 * Math.PI;
        return a;
    }

    private static double Forward(double from, double to)
    {
        var d = to - from;
        if (d < 0) d += 2 * Math.PI;
        return d;
    }

    private static double NormAng(double a) => a < 0 ? a + 2 * Math.PI : a;

    private static Vec3 Shift(Vec3 p, char axis, double u, double v) => p - AxisOffset(axis, u, v);

    private static double Radial(Vec3 p, char axis, double u, double v)
    {
        var (x, y) = AxisPerp(Shift(p, axis, u, v), axis);
        return Math.Sqrt(x * x + y * y);
    }

    private static bool InCylinder(Mesh3D mesh, int a, int b, int c, char axis, double radius, double u, double v)
    {
        var pa = mesh.V[a]; var pb = mesh.V[b]; var pc = mesh.V[c];
        if (Radial(pa, axis, u, v) < radius || Radial(pb, axis, u, v) < radius || Radial(pc, axis, u, v) < radius) return true;
        if (Radial((pa + pb + pc) / 3, axis, u, v) < radius) return true;
        if (Radial((pa + pb) / 2, axis, u, v) < radius) return true;
        if (Radial((pb + pc) / 2, axis, u, v) < radius) return true;
        return Radial((pc + pa) / 2, axis, u, v) < radius;
    }

    private static Vec3 OnRadius(Vec3 p, char axis, double radius, double u, double v)
    {
        var local = Shift(p, axis, u, v);
        var (x, y) = AxisPerp(local, axis);
        var len = Math.Sqrt(x * x + y * y);
        if (len < 1e-8) { x = 1; y = 0; len = 1; }
        var s = radius / len;
        var snapped = axis switch
        {
            'x' => new Vec3(local.X, x * s, y * s),
            'y' => new Vec3(x * s, local.Y, y * s),
            _ => new Vec3(x * s, y * s, local.Z),
        };
        return snapped + AxisOffset(axis, u, v);
    }

    private static Vec3 TowardAxis(Vec3 p, Vec3 cen, char axis)
    {
        var c = axis switch
        {
            'x' => new Vec3(p.X, cen.Y, cen.Z),
            'y' => new Vec3(cen.X, p.Y, cen.Z),
            _ => new Vec3(cen.X, cen.Y, p.Z),
        };
        var d = c - p;
        return d.Len2 < 1e-8 ? new Vec3(0, 0, 1) : d;
    }

    public Poly3D Transform(double rx, double ry, double rz, double x, double y, double z)
    {
        var dst = new Poly3D();
        foreach (var v in V) dst.V.Add(Rotate(v, rx, ry, rz) + new Vec3(x, y, z));
        foreach (var f in F) dst.F.Add((int[])f.Clone());
        return dst;
    }

    public static Vec3 Rotate(Vec3 v, double rxDeg, double ryDeg, double rzDeg)
    {
        var (x, y, z) = (v.X, v.Y, v.Z);
        var rx = rxDeg * Math.PI / 180; var ry = ryDeg * Math.PI / 180; var rz = rzDeg * Math.PI / 180;
        var cx = Math.Cos(rx); var sx = Math.Sin(rx);
        var y1 = y * cx - z * sx; var z1 = y * sx + z * cx;
        var cy = Math.Cos(ry); var sy = Math.Sin(ry);
        var x2 = x * cy + z1 * sy; var z2 = -x * sy + z1 * cy;
        var cz = Math.Cos(rz); var sz = Math.Sin(rz);
        return new(x2 * cz - y1 * sz, x2 * sz + y1 * cz, z2);
    }

    public (Vec3 Min, Vec3 Max) Bounds()
    {
        if (V.Count == 0) return (new(0, 0, 0), new(0, 0, 0));
        var min = V[0]; var max = V[0];
        foreach (var v in V)
        {
            min = new(Math.Min(min.X, v.X), Math.Min(min.Y, v.Y), Math.Min(min.Z, v.Z));
            max = new(Math.Max(max.X, v.X), Math.Max(max.Y, v.Y), Math.Max(max.Z, v.Z));
        }
        return (min, max);
    }

    private static (int, int, int) FaceOut(List<Vec3> v, int a, int b, int c, Vec3 outward)
    {
        var n = Vec3.Cross(v[b] - v[a], v[c] - v[a]);
        return Vec3.Dot(n, outward) >= 0 ? (a, b, c) : (a, c, b);
    }

    private static int[] FacingOut(List<Vec3> v, int[] poly, Vec3 center)
    {
        var n = Normal(v, poly);
        var mid = new Vec3(0, 0, 0);
        foreach (var i in poly) mid += v[i];
        mid /= poly.Length;
        if (Vec3.Dot(n, mid - center) < 0) Array.Reverse(poly);
        return poly;
    }

    private static bool LineHit(Vec3 p1, Vec3 d1, Vec3 p2, Vec3 d2, out Vec3 hit)
    {
        var c = Vec3.Cross(d1, d2);
        var denom = c.Len2;
        if (denom < 1e-10) { hit = default; return false; }
        var t = Vec3.Dot(Vec3.Cross(p2 - p1, d2), c) / denom;
        hit = p1 + d1 * t;
        return hit.Len < 1e6;
    }

    private static double AngleAround(Vec3 v, Vec3 n)
    {
        var u = Math.Abs(n.X) < 0.9 ? Vec3.Cross(n, new Vec3(1, 0, 0)).Norm() : Vec3.Cross(n, new Vec3(0, 1, 0)).Norm();
        var w = Vec3.Cross(n, u);
        return Math.Atan2(Vec3.Dot(v, w), Vec3.Dot(v, u));
    }

    private static Vec3 Centroid(List<Vec3> v, int[] f)
    {
        var s = new Vec3(0, 0, 0);
        foreach (var i in f) s += v[i];
        return s / f.Length;
    }

    private static Vec3 Center(List<Vec3> v)
    {
        var s = new Vec3(0, 0, 0);
        foreach (var p in v) s += p;
        return v.Count == 0 ? s : s / v.Count;
    }

    private static List<Vec3> Clean(IReadOnlyList<Vec3> poly)
    {
        var r = new List<Vec3>();
        foreach (var p in poly)
            if (r.Count == 0 || (r[^1] - p).Len2 > 1e-12) r.Add(p);
        if (r.Count > 1 && (r[0] - r[^1]).Len2 <= 1e-12) r.RemoveAt(r.Count - 1);
        return r;
    }

    private static Vec3 AxisDir(char axis, double c, double s) => axis switch
    {
        'x' => new(0, c, s),
        'y' => new(c, 0, s),
        _ => new(c, s, 0),
    };

    private static Vec3 AxisOffset(char axis, double u, double v) => axis switch
    {
        'x' => new(0, u, v),
        'y' => new(u, 0, v),
        _ => new(u, v, 0),
    };

    private static double AxisValue(Vec3 p, char axis) => axis switch { 'x' => p.X, 'y' => p.Y, _ => p.Z };

    private static (double u, double v) AxisPerp(Vec3 p, char axis) => axis switch
    {
        'x' => (p.Y, p.Z),
        'y' => (p.X, p.Z),
        _ => (p.X, p.Y),
    };

    private static double Angle(char axis, Vec3 p)
    {
        var (u, v) = AxisPerp(p, axis);
        return Math.Atan2(v, u);
    }
}

/// <summary>Dreiecksnetz, fertig für Bild, Datei und Strahltests.</summary>
public sealed class Mesh3D
{
    public List<Vec3> V { get; } = [];
    public List<(int A, int B, int C)> T { get; } = [];
    public List<int> Farbe { get; } = [];

    public void Add(Mesh3D o, int farbe)
    {
        var b = V.Count;
        V.AddRange(o.V);
        foreach (var (a, c, d) in o.T)
        {
            T.Add((a + b, c + b, d + b));
            Farbe.Add(farbe);
        }
    }

    public Mesh3D Weld(double tol = 1e-4)
    {
        var dst = new Mesh3D();
        var map = new Dictionary<(long, long, long), int>();
        var remap = new int[V.Count];
        long Q(double v) => (long)Math.Round(v / tol);
        for (var i = 0; i < V.Count; i++)
        {
            var k = (Q(V[i].X), Q(V[i].Y), Q(V[i].Z));
            if (!map.TryGetValue(k, out var id)) { id = dst.V.Count; dst.V.Add(V[i]); map[k] = id; }
            remap[i] = id;
        }
        for (var i = 0; i < T.Count; i++)
        {
            var (a, b, c) = T[i];
            a = remap[a]; b = remap[b]; c = remap[c];
            if (a == b || b == c || c == a) continue;
            dst.T.Add((a, b, c));
            dst.Farbe.Add(i < Farbe.Count ? Farbe[i] : 0xB0B0B0);
        }
        return dst;
    }

    public List<List<int>> BoundaryLoops()
    {
        var undirected = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in T)
        {
            void Inc(int u, int v) { var k = u < v ? (u, v) : (v, u); undirected[k] = undirected.GetValueOrDefault(k) + 1; }
            Inc(a, b); Inc(b, c); Inc(c, a);
        }
        var next = new Dictionary<int, int>();
        foreach (var (a, b, c) in T)
        {
            void One(int u, int v)
            {
                var k = u < v ? (u, v) : (v, u);
                if (undirected[k] == 1) next[u] = v;
            }
            One(a, b); One(b, c); One(c, a);
        }
        var unused = new HashSet<int>(next.Keys);
        var loops = new List<List<int>>();
        while (unused.Count > 0)
        {
            var start = unused.First();
            var loop = new List<int>();
            var cur = start;
            for (var guard = 0; guard < next.Count + 2; guard++)
            {
                loop.Add(cur); unused.Remove(cur);
                if (!next.TryGetValue(cur, out var n) || n == start) break;
                if (!unused.Contains(n)) break;
                cur = n;
            }
            if (loop.Count >= 3) loops.Add(loop);
        }
        return loops;
    }

    public (Vec3 Min, Vec3 Max) Bounds()
    {
        if (V.Count == 0) return (new(0, 0, 0), new(0, 0, 0));
        var min = V[0]; var max = V[0];
        foreach (var p in V)
        {
            min = new(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        return (min, max);
    }

    public double Volume()
    {
        double s = 0;
        foreach (var (a, b, c) in T) s += Vec3.Dot(V[a], Vec3.Cross(V[b], V[c]));
        return Math.Abs(s) / 6.0;
    }

    public int RayHits(Vec3 origin, Vec3 dir)
    {
        dir = dir.Norm();
        var n = 0;
        foreach (var (ia, ib, ic) in T)
        {
            var a = V[ia]; var b = V[ib]; var c = V[ic];
            var e1 = b - a; var e2 = c - a;
            var p = Vec3.Cross(dir, e2);
            var det = Vec3.Dot(e1, p);
            if (Math.Abs(det) < 1e-9) continue;
            var inv = 1 / det;
            var tvec = origin - a;
            var u = Vec3.Dot(tvec, p) * inv;
            if (u < -1e-6 || u > 1 + 1e-6) continue;
            var q = Vec3.Cross(tvec, e1);
            var v = Vec3.Dot(dir, q) * inv;
            if (v < -1e-6 || u + v > 1 + 1e-6) continue;
            var t = Vec3.Dot(e2, q) * inv;
            if (t > 1e-4) n++;
        }
        return n;
    }

    public static int ParseColor(string? hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#') return 0x2F86F6;
        try { return Convert.ToInt32(hex[1..], 16); } catch { return 0x2F86F6; }
    }

    public static string Hex(int c) => "#" + (c & 0xFFFFFF).ToString("X6");
}

/// <summary>5×7-Punktmuster für Buchstaben, Zahlen und ein paar Umlaute.</summary>
public static class BlockFont
{
    private static readonly Dictionary<char, byte[]> Map = Build();

    public static byte[]? Rows(char ch)
    {
        ch = char.ToUpperInvariant(ch);
        if (ch == ' ') return null;
        return Map.TryGetValue(ch, out var rows) ? rows : Map['?'];
    }

    private static Dictionary<char, byte[]> Build()
    {
        byte[] G(params string[] rows)
        {
            var b = new byte[7];
            for (var i = 0; i < 7 && i < rows.Length; i++)
            {
                byte v = 0;
                var row = rows[i];
                for (var c = 0; c < 5 && c < row.Length; c++) if (row[c] == '#') v |= (byte)(1 << (4 - c));
                b[i] = v;
            }
            return b;
        }
        return new Dictionary<char, byte[]>
        {
            ['A'] = G(".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"),
            ['B'] = G("####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."),
            ['C'] = G(".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."),
            ['D'] = G("####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."),
            ['E'] = G("#####", "#....", "#....", "####.", "#....", "#....", "#####"),
            ['F'] = G("#####", "#....", "#....", "####.", "#....", "#....", "#...."),
            ['G'] = G(".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."),
            ['H'] = G("#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"),
            ['I'] = G(".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###."),
            ['J'] = G("..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.."),
            ['K'] = G("#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"),
            ['L'] = G("#....", "#....", "#....", "#....", "#....", "#....", "#####"),
            ['M'] = G("#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#"),
            ['N'] = G("#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"),
            ['O'] = G(".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
            ['P'] = G("####.", "#...#", "#...#", "####.", "#....", "#....", "#...."),
            ['Q'] = G(".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"),
            ['R'] = G("####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"),
            ['S'] = G(".####", "#....", "#....", ".###.", "....#", "....#", "####."),
            ['T'] = G("#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."),
            ['U'] = G("#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
            ['V'] = G("#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."),
            ['W'] = G("#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"),
            ['X'] = G("#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"),
            ['Y'] = G("#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."),
            ['Z'] = G("#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"),
            ['0'] = G(".###.", "#..##", "#.#.#", "##..#", "#...#", "#...#", ".###."),
            ['1'] = G(".#...", "##...", ".#...", ".#...", ".#...", ".#...", "###.."),
            ['2'] = G(".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"),
            ['3'] = G("####.", "....#", "....#", ".###.", "....#", "....#", "####."),
            ['4'] = G("#...#", "#...#", "#...#", "#####", "....#", "....#", "....#"),
            ['5'] = G("#####", "#....", "#....", "####.", "....#", "....#", "####."),
            ['6'] = G(".###.", "#....", "#....", "####.", "#...#", "#...#", ".###."),
            ['7'] = G("#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."),
            ['8'] = G(".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."),
            ['9'] = G(".###.", "#...#", "#...#", ".####", "....#", "....#", ".###."),
            ['Ä'] = G("#.#.#", ".....", ".###.", "#...#", "#####", "#...#", "#...#"),
            ['Ö'] = G("#.#.#", ".....", ".###.", "#...#", "#...#", "#...#", ".###."),
            ['Ü'] = G("#.#.#", ".....", "#...#", "#...#", "#...#", "#...#", ".###."),
            ['?'] = G(".###.", "#...#", "...#.", "..#..", "..#..", ".....", "..#.."),
            ['-'] = G(".....", ".....", ".....", "#####", ".....", ".....", "....."),
            ['.'] = G(".....", ".....", ".....", ".....", ".....", ".##..", ".##.."),
        };
    }
}
