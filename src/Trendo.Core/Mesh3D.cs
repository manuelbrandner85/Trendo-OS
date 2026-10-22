using System.Globalization;

namespace Trendo.Core;

public readonly struct Vec3(double x, double y, double z)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Z { get; } = z;
    public double Len => Math.Sqrt(X * X + Y * Y + Z * Z);
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public Vec3 Norm() { var l = Len; return l < 1e-12 ? new(0, 0, 1) : new(X / l, Y / l, Z / l); }
}

public sealed class Mesh
{
    public List<Vec3> V { get; } = [];
    public List<int> I { get; } = [];
    public List<byte> R { get; } = [];
    public List<byte> G { get; } = [];
    public List<byte> B { get; } = [];
    public byte CR = 47, CG = 134, CB = 246;
    public int Triangles => I.Count / 3;

    public void Add(Vec3 a, Vec3 b, Vec3 c)
    {
        if (Vec3.Cross(b - a, c - a).Len < 1e-10) return;
        var i = V.Count;
        V.Add(a); V.Add(b); V.Add(c);
        I.Add(i); I.Add(i + 1); I.Add(i + 2);
        R.Add(CR); G.Add(CG); B.Add(CB);
    }

    public void AddOutward(Vec3 a, Vec3 b, Vec3 c)
    {
        var n = Vec3.Cross(b - a, c - a);
        var cen = (a + b + c) * (1.0 / 3.0);
        if (Vec3.Dot(n, cen) < 0) (b, c) = (c, b);
        Add(a, b, c);
    }

    public void AddToward(Vec3 a, Vec3 b, Vec3 c, Vec3 target)
    {
        var n = Vec3.Cross(b - a, c - a);
        var cen = (a + b + c) * (1.0 / 3.0);
        if (Vec3.Dot(n, target - cen) < 0) (b, c) = (c, b);
        Add(a, b, c);
    }

    public void Append(Mesh other)
    {
        var o = V.Count;
        V.AddRange(other.V);
        for (var t = 0; t < other.I.Count; t += 3)
        {
            I.Add(other.I[t] + o); I.Add(other.I[t + 1] + o); I.Add(other.I[t + 2] + o);
            var k = t / 3;
            R.Add(k < other.R.Count ? other.R[k] : other.CR);
            G.Add(k < other.G.Count ? other.G[k] : other.CG);
            B.Add(k < other.B.Count ? other.B[k] : other.CB);
        }
    }
}

/// <summary>Baut aus Teilen ein Netz, bohrt Löcher, rundet Kanten und zeichnet eine Vorschau.</summary>
public static partial class Mesh3D
{
    public static Mesh FromScene(Scene3D scene)
    {
        var all = new Mesh();
        foreach (var p in scene.Parts) all.Append(BuildWorld(p));
        return all;
    }

    public static Mesh BuildWorld(Part3D part)
    {
        var local = BuildLocal(part);
        var world = new Mesh();
        for (var t = 0; t < local.I.Count; t += 3)
        {
            world.CR = local.R[t / 3]; world.CG = local.G[t / 3]; world.CB = local.B[t / 3];
            world.Add(World(local.V[local.I[t]], part), World(local.V[local.I[t + 1]], part), World(local.V[local.I[t + 2]], part));
        }
        return world;
    }

    public static Mesh FromStored(Part3D part)
    {
        var m = new Mesh();
        if (part.Positions is not { Length: >= 9 } || part.Indices is not { Length: >= 3 }) return m;
        var v = new Vec3[part.Positions.Length / 3];
        for (var i = 0; i < v.Length; i++) v[i] = new(part.Positions[i * 3], part.Positions[i * 3 + 1], part.Positions[i * 3 + 2]);
        for (var t = 0; t + 2 < part.Indices.Length; t += 3)
        {
            var a = part.Indices[t]; var b = part.Indices[t + 1]; var c = part.Indices[t + 2];
            if ((uint)a >= (uint)v.Length || (uint)b >= (uint)v.Length || (uint)c >= (uint)v.Length) continue;
            m.Add(v[a], v[b], v[c]);
        }
        return m;
    }

    static Vec3 World(Vec3 v, Part3D part)
    {
        if (part.Shape == "mesh")
        {
            var sx = Math.Abs(part.BaseX) < 1e-8 ? 1 : part.Sx / part.BaseX;
            var sy = Math.Abs(part.BaseY) < 1e-8 ? 1 : part.Sy / part.BaseY;
            var sz = Math.Abs(part.BaseZ) < 1e-8 ? 1 : part.Sz / part.BaseZ;
            v = new(v.X * sx, v.Y * sy, v.Z * sz);
        }
        return Rot(v, part.Rx, part.Ry, part.Rz) + new Vec3(part.X, part.Y, part.Z);
    }

    public static Vec3 Rot(Vec3 p, double rx, double ry, double rz)
    {
        var ax = rx * Math.PI / 180; var ay = ry * Math.PI / 180; var az = rz * Math.PI / 180;
        var y = p.Y * Math.Cos(ax) - p.Z * Math.Sin(ax);
        var z = p.Y * Math.Sin(ax) + p.Z * Math.Cos(ax);
        p = new(p.X, y, z);
        var x = p.X * Math.Cos(ay) + p.Z * Math.Sin(ay);
        z = -p.X * Math.Sin(ay) + p.Z * Math.Cos(ay);
        p = new(x, p.Y, z);
        x = p.X * Math.Cos(az) - p.Y * Math.Sin(az);
        y = p.X * Math.Sin(az) + p.Y * Math.Cos(az);
        return new(x, y, p.Z);
    }

    static Mesh BuildLocal(Part3D part)
    {
        var (r, g, b) = Rgb(part.Color);
        var hole = part.Ops.Where(o => o.Kind == "hole").ToList();
        var bevel = part.Ops.Where(o => o.Kind == "bevel").Select(o => o.Amount).DefaultIfEmpty(0).Max();
        var shell = part.Ops.FirstOrDefault(o => o.Kind == "shell");
        var smooth = part.Ops.Any(o => o.Kind == "smooth");
        Mesh m;
        if (part.Shape == "mesh") m = FromStored(part);
        else if (hole.Count == 1 && part.Shape == "cube" && bevel <= 0 && shell is null && TryBoxHole(part, hole[0]) is { } exact) m = exact;
        else if (hole.Count > 0 || shell is not null || (bevel > 0 && part.Shape != "cube")) m = Voxel(part);
        else if (bevel > 0 && part.Shape == "cube") m = Chamfer(part, bevel);
        else m = Analytic(part, smooth);
        m.CR = r; m.CG = g; m.CB = b;
        for (var i = 0; i < m.R.Count; i++) { m.R[i] = r; m.G[i] = g; m.B[i] = b; }
        if (smooth && part.Shape != "sphere" && part.Shape != "torus") { m = Weld(m); Laplacian(m, 1); }
        foreach (var op in part.Ops.Where(o => o.Kind == "mirror")) Mirror(m, op.Axis);
        return m;
    }

    static Mesh Analytic(Part3D part, bool smooth)
    {
        var m = new Mesh();
        var sx = Math.Max(0.2, part.Sx); var sy = Math.Max(0.2, part.Sy); var sz = Math.Max(0.2, part.Sz);
        switch (part.Shape)
        {
            case "sphere": Sphere(m, sx / 2, sy / 2, sz / 2, smooth ? 28 : 16, smooth ? 14 : 10); break;
            case "cylinder": Cylinder(m, sx / 2, sy / 2, sz / 2, smooth ? 32 : 24); break;
            case "cone": Cone(m, sx / 2, sy / 2, sz / 2, smooth ? 32 : 24); break;
            case "torus": Torus(m, sx, sy, smooth ? 28 : 20, smooth ? 14 : 10); break;
            case "plane": Box(m, sx / 2, sy / 2, Math.Max(sz, 0.2) / 2); break;
            case "text": Text(m, part.Text, sx, sy, sz); break;
            default: Box(m, sx / 2, sy / 2, sz / 2); break;
        }
        return m;
    }

    static void Box(Mesh m, double hx, double hy, double hz) => AddBox(m, -hx, hx, -hy, hy, -hz, hz);

    public static void AddBox(Mesh m, double x0, double x1, double y0, double y1, double z0, double z1)
    {
        void Q(Vec3 a, Vec3 b, Vec3 c, Vec3 d) { m.Add(a, b, c); m.Add(a, c, d); }
        Q(new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), new(x1, y0, z1));
        Q(new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0));
        Q(new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0));
        Q(new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1));
        Q(new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1));
        Q(new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), new(x1, y0, z0));
    }

    static void Sphere(Mesh m, double rx, double ry, double rz, int seg, int rings)
    {
        var v = new Vec3[(rings + 1) * (seg + 1)];
        for (var lat = 0; lat <= rings; lat++)
        {
            var tv = lat * Math.PI / rings;
            for (var lon = 0; lon <= seg; lon++)
            {
                var u = lon * 2 * Math.PI / seg;
                v[lat * (seg + 1) + lon] = new(Math.Sin(tv) * Math.Cos(u) * rx, Math.Sin(tv) * Math.Sin(u) * ry, Math.Cos(tv) * rz);
            }
        }
        for (var lat = 0; lat < rings; lat++)
            for (var lon = 0; lon < seg; lon++)
            {
                var a = lat * (seg + 1) + lon; var b = a + seg + 1;
                m.AddOutward(v[a], v[a + 1], v[b + 1]);
                m.AddOutward(v[a], v[b + 1], v[b]);
            }
    }

    static void Cylinder(Mesh m, double rx, double ry, double hz, int seg)
    {
        var b = new Vec3[seg]; var t = new Vec3[seg];
        for (var i = 0; i < seg; i++)
        {
            var a = i * 2 * Math.PI / seg;
            b[i] = new(Math.Cos(a) * rx, Math.Sin(a) * ry, -hz);
            t[i] = new(Math.Cos(a) * rx, Math.Sin(a) * ry, hz);
        }
        for (var i = 0; i < seg; i++)
        {
            var j = (i + 1) % seg;
            m.AddOutward(b[i], b[j], t[j]);
            m.AddOutward(b[i], t[j], t[i]);
            m.AddOutward(new(0, 0, hz), t[i], t[j]);
            m.AddOutward(new(0, 0, -hz), b[j], b[i]);
        }
    }

    static void Cone(Mesh m, double rx, double ry, double hz, int seg)
    {
        var apex = new Vec3(0, 0, hz);
        var b = new Vec3[seg];
        for (var i = 0; i < seg; i++)
        {
            var a = i * 2 * Math.PI / seg;
            b[i] = new(Math.Cos(a) * rx, Math.Sin(a) * ry, -hz);
        }
        for (var i = 0; i < seg; i++)
        {
            var j = (i + 1) % seg;
            m.AddOutward(b[i], b[j], apex);
            m.AddOutward(new(0, 0, -hz), b[j], b[i]);
        }
    }

    static void Torus(Mesh m, double outer, double tubeDiam, int maj, int minr)
    {
        var tube = Math.Max(0.1, tubeDiam / 2);
        var R = Math.Max(tube + 0.1, outer / 2 - tube);
        Vec3 P(int i, int j)
        {
            var u = i * 2 * Math.PI / maj; var v = j * 2 * Math.PI / minr;
            var c = Math.Cos(v); var s = Math.Sin(v);
            return new((R + tube * c) * Math.Cos(u), (R + tube * c) * Math.Sin(u), tube * s);
        }
        Vec3 Major(Vec3 p)
        {
            var len = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            return len < 1e-8 ? new(R, 0, 0) : new(p.X / len * R, p.Y / len * R, 0);
        }
        void T(Vec3 a, Vec3 b, Vec3 c)
        {
            var cen = (a + b + c) * (1.0 / 3.0);
            var n = Vec3.Cross(b - a, c - a);
            if (Vec3.Dot(n, cen - Major(cen)) < 0) (b, c) = (c, b);
            m.Add(a, b, c);
        }
        for (var i = 0; i < maj; i++)
            for (var j = 0; j < minr; j++)
            {
                var a = P(i, j); var b = P(i + 1, j); var c = P(i + 1, j + 1); var d = P(i, j + 1);
                T(a, b, c); T(a, c, d);
            }
    }

    static Mesh Chamfer(Part3D part, double bevel)
    {
        var m = new Mesh();
        var hx = Math.Max(0.2, part.Sx) / 2; var hy = Math.Max(0.2, part.Sy) / 2; var hz = Math.Max(0.2, part.Sz) / 2;
        var c = Math.Clamp(bevel, 0, Math.Min(hx, Math.Min(hy, hz)) * 0.45);
        if (c < 0.02) { Box(m, hx, hy, hz); return m; }
        foreach (var sx in new[] { -1, 1 })
            foreach (var sy in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                {
                    var A = new Vec3(sx * hx, sy * (hy - c), sz * (hz - c));
                    var B = new Vec3(sx * (hx - c), sy * hy, sz * (hz - c));
                    var C = new Vec3(sx * (hx - c), sy * (hy - c), sz * hz);
                    m.AddOutward(A, B, C);
                }
        foreach (var sy in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
            {
                Vec3 B(int sx) => new(sx * (hx - c), sy * hy, sz * (hz - c));
                Vec3 C(int sx) => new(sx * (hx - c), sy * (hy - c), sz * hz);
                m.AddOutward(B(-1), C(-1), C(1)); m.AddOutward(B(-1), C(1), B(1));
            }
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
            {
                Vec3 A(int sy) => new(sx * hx, sy * (hy - c), sz * (hz - c));
                Vec3 C(int sy) => new(sx * (hx - c), sy * (hy - c), sz * hz);
                m.AddOutward(A(-1), C(-1), C(1)); m.AddOutward(A(-1), C(1), A(1));
            }
        foreach (var sx in new[] { -1, 1 })
            foreach (var sy in new[] { -1, 1 })
            {
                Vec3 A(int sz) => new(sx * hx, sy * (hy - c), sz * (hz - c));
                Vec3 B(int sz) => new(sx * (hx - c), sy * hy, sz * (hz - c));
                m.AddOutward(A(-1), B(-1), B(1)); m.AddOutward(A(-1), B(1), A(1));
            }
        void FaceX(int s)
        {
            var x = s * hx; var y = hy - c; var z = hz - c;
            m.AddOutward(new(x, -y, -z), new(x, y, -z), new(x, y, z));
            m.AddOutward(new(x, -y, -z), new(x, y, z), new(x, -y, z));
        }
        FaceX(1); FaceX(-1);
        void FaceY(int s)
        {
            var y = s * hy; var x = hx - c; var z = hz - c;
            m.AddOutward(new(-x, y, -z), new(-x, y, z), new(x, y, z));
            m.AddOutward(new(-x, y, -z), new(x, y, z), new(x, y, -z));
        }
        FaceY(1); FaceY(-1);
        void FaceZ(int s)
        {
            var z = s * hz; var x = hx - c; var y = hy - c;
            m.AddOutward(new(-x, -y, z), new(x, -y, z), new(x, y, z));
            m.AddOutward(new(-x, -y, z), new(x, y, z), new(-x, y, z));
        }
        FaceZ(1); FaceZ(-1);
        return m;
    }

    static Mesh? TryBoxHole(Part3D part, PartOp hole)
    {
        var axis = hole.Axis is "x" or "y" ? hole.Axis : "z";
        var hx = Math.Max(0.2, part.Sx) / 2; var hy = Math.Max(0.2, part.Sy) / 2; var hz = Math.Max(0.2, part.Sz) / 2;
        var r = hole.Diameter / 2;
        if (r < 0.05) return null;
        bool Fit(double a, double limA, double b, double limB) => Math.Abs(a) + r < limA - 0.05 && Math.Abs(b) + r < limB - 0.05;
        if (axis == "z" && !Fit(hole.AtX, hx, hole.AtY, hy)) return null;
        if (axis == "x" && !Fit(hole.AtY, hy, hole.AtZ, hz)) return null;
        if (axis == "y" && !Fit(hole.AtX, hx, hole.AtZ, hz)) return null;
        return BoxHole(hx, hy, hz, axis, hole.AtX, hole.AtY, hole.AtZ, r, 28);
    }

    static Mesh BoxHole(double hx, double hy, double hz, string axis, double cx, double cy, double cz, double r, int n)
    {
        var m = new Mesh();
        if (axis == "z")
        {
            AddBoxSides(m, hx, hy, hz, skipZ: true);
            Cap(m, hx, hy, hz, cx, cy, true, n, r);
            Cap(m, hx, hy, -hz, cx, cy, false, n, r);
            WallZ(m, cx, cy, -hz, hz, r, n);
        }
        else if (axis == "x")
        {
            AddBoxSides(m, hx, hy, hz, skipX: true);
            CapX(m, hy, hz, hx, cy, cz, true, n, r);
            CapX(m, hy, hz, -hx, cy, cz, false, n, r);
            WallX(m, cy, cz, -hx, hx, r, n);
        }
        else
        {
            AddBoxSides(m, hx, hy, hz, skipY: true);
            CapY(m, hx, hz, hy, cx, cz, true, n, r);
            CapY(m, hx, hz, -hy, cx, cz, false, n, r);
            WallY(m, cx, cz, -hy, hy, r, n);
        }
        return m;
    }

    static void AddBoxSides(Mesh m, double hx, double hy, double hz, bool skipX = false, bool skipY = false, bool skipZ = false)
    {
        void Q(Vec3 a, Vec3 b, Vec3 c, Vec3 d) { m.Add(a, b, c); m.Add(a, c, d); }
        if (!skipX)
        {
            Q(new(hx, -hy, -hz), new(hx, hy, -hz), new(hx, hy, hz), new(hx, -hy, hz));
            Q(new(-hx, -hy, -hz), new(-hx, -hy, hz), new(-hx, hy, hz), new(-hx, hy, -hz));
        }
        if (!skipY)
        {
            Q(new(-hx, hy, -hz), new(-hx, hy, hz), new(hx, hy, hz), new(hx, hy, -hz));
            Q(new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, -hy, hz), new(-hx, -hy, hz));
        }
        if (!skipZ)
        {
            Q(new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz));
            Q(new(-hx, -hy, -hz), new(-hx, hy, -hz), new(hx, hy, -hz), new(hx, -hy, -hz));
        }
    }

    static void Cap(Mesh m, double hx, double hy, double z, double cx, double cy, bool top, int n, double r)
    {
        var outer = new List<Vec3> { new(-hx, -hy, z), new(hx, -hy, z), new(hx, hy, z), new(-hx, hy, z) };
        if (!top) outer.Reverse();
        var inner = Circle(n, i =>
        {
            var a = (top ? -1 : 1) * i * 2 * Math.PI / n;
            return new(cx + r * Math.Cos(a), cy + r * Math.Sin(a), z);
        });
        Ear(m, Bridge(outer, inner));
    }

    static void CapX(Mesh m, double hy, double hz, double x, double cy, double cz, bool pos, int n, double r)
    {
        var outer = new List<Vec3> { new(x, -hy, -hz), new(x, -hy, hz), new(x, hy, hz), new(x, hy, -hz) };
        if (!pos) outer.Reverse();
        var inner = Circle(n, i =>
        {
            var a = (pos ? -1 : 1) * i * 2 * Math.PI / n;
            return new(x, cy + r * Math.Cos(a), cz + r * Math.Sin(a));
        });
        Ear(m, Bridge(outer, inner));
    }

    static void CapY(Mesh m, double hx, double hz, double y, double cx, double cz, bool pos, int n, double r)
    {
        var outer = new List<Vec3> { new(-hx, y, -hz), new(-hx, y, hz), new(hx, y, hz), new(hx, y, -hz) };
        if (!pos) outer.Reverse();
        var inner = Circle(n, i =>
        {
            var a = (pos ? -1 : 1) * i * 2 * Math.PI / n;
            return new(cx + r * Math.Cos(a), y, cz + r * Math.Sin(a));
        });
        Ear(m, Bridge(outer, inner));
    }

    static List<Vec3> Circle(int n, Func<int, Vec3> at)
    {
        var l = new List<Vec3>(n);
        for (var i = 0; i < n; i++) l.Add(at(i));
        return l;
    }

    static void WallZ(Mesh m, double cx, double cy, double z0, double z1, double r, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var a0 = i * 2 * Math.PI / n; var a1 = (i + 1) * 2 * Math.PI / n;
            var b0 = new Vec3(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0), z0);
            var b1 = new Vec3(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1), z0);
            var t0 = new Vec3(b0.X, b0.Y, z1); var t1 = new Vec3(b1.X, b1.Y, z1);
            m.AddToward(b0, t0, t1, new(cx, cy, (z0 + z1) / 2));
            m.AddToward(b0, t1, b1, new(cx, cy, (z0 + z1) / 2));
        }
    }

    static void WallX(Mesh m, double cy, double cz, double x0, double x1, double r, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var a0 = i * 2 * Math.PI / n; var a1 = (i + 1) * 2 * Math.PI / n;
            var b0 = new Vec3(x0, cy + r * Math.Cos(a0), cz + r * Math.Sin(a0));
            var b1 = new Vec3(x0, cy + r * Math.Cos(a1), cz + r * Math.Sin(a1));
            var t0 = new Vec3(x1, b0.Y, b0.Z); var t1 = new Vec3(x1, b1.Y, b1.Z);
            m.AddToward(b0, t0, t1, new((x0 + x1) / 2, cy, cz));
            m.AddToward(b0, t1, b1, new((x0 + x1) / 2, cy, cz));
        }
    }

    static void WallY(Mesh m, double cx, double cz, double y0, double y1, double r, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var a0 = i * 2 * Math.PI / n; var a1 = (i + 1) * 2 * Math.PI / n;
            var b0 = new Vec3(cx + r * Math.Cos(a0), y0, cz + r * Math.Sin(a0));
            var b1 = new Vec3(cx + r * Math.Cos(a1), y0, cz + r * Math.Sin(a1));
            var t0 = new Vec3(b0.X, y1, b0.Z); var t1 = new Vec3(b1.X, y1, b1.Z);
            m.AddToward(b0, t0, t1, new(cx, (y0 + y1) / 2, cz));
            m.AddToward(b0, t1, b1, new(cx, (y0 + y1) / 2, cz));
        }
    }

    static List<Vec3> Bridge(List<Vec3> outer, List<Vec3> inner)
    {
        var best = double.MaxValue; var oi = 0; var ii = 0;
        for (var o = 0; o < outer.Count; o++)
            for (var i = 0; i < inner.Count; i++)
            {
                var d = (outer[o] - inner[i]).Len;
                if (d < best) { best = d; oi = o; ii = i; }
            }
        var loop = new List<Vec3>();
        for (var k = 0; k <= outer.Count; k++) loop.Add(outer[(oi + k) % outer.Count]);
        for (var k = 0; k <= inner.Count; k++) loop.Add(inner[(ii + k) % inner.Count]);
        return loop;
    }

    static void Ear(Mesh m, List<Vec3> poly)
    {
        if (poly.Count < 3) return;
        var nrm = Newell(poly);
        var drop = Math.Abs(nrm.X) >= Math.Abs(nrm.Y) && Math.Abs(nrm.X) >= Math.Abs(nrm.Z) ? 0 : Math.Abs(nrm.Y) >= Math.Abs(nrm.Z) ? 1 : 2;
        var idx = Enumerable.Range(0, poly.Count).ToList();
        var guard = idx.Count * idx.Count + 8;
        while (idx.Count > 3 && guard-- > 0)
        {
            var clipped = false;
            for (var i = 0; i < idx.Count; i++)
            {
                var ia = idx[(i + idx.Count - 1) % idx.Count]; var ib = idx[i]; var ic = idx[(i + 1) % idx.Count];
                var e = Vec3.Cross(poly[ib] - poly[ia], poly[ic] - poly[ib]);
                if (Vec3.Dot(e, nrm) <= 1e-8) continue;
                var hit = false;
                foreach (var id in idx)
                {
                    if (id == ia || id == ib || id == ic) continue;
                    if (InTri(poly[id], poly[ia], poly[ib], poly[ic], drop)) { hit = true; break; }
                }
                if (hit) continue;
                m.Add(poly[ia], poly[ib], poly[ic]);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
        if (idx.Count == 3) m.Add(poly[idx[0]], poly[idx[1]], poly[idx[2]]);
        else if (idx.Count > 3)
            for (var i = 1; i < idx.Count - 1; i++) m.Add(poly[idx[0]], poly[idx[i]], poly[idx[i + 1]]);
    }

    static Vec3 Newell(List<Vec3> p)
    {
        double x = 0, y = 0, z = 0;
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            x += (a.Y - b.Y) * (a.Z + b.Z); y += (a.Z - b.Z) * (a.X + b.X); z += (a.X - b.X) * (a.Y + b.Y);
        }
        return new Vec3(x, y, z).Norm();
    }

    static bool InTri(Vec3 p, Vec3 a, Vec3 b, Vec3 c, int drop)
    {
        static (double X, double Y) D(Vec3 v, int drop) => drop switch { 0 => (v.Y, v.Z), 1 => (v.X, v.Z), _ => (v.X, v.Y) };
        var pp = D(p, drop); var aa = D(a, drop); var bb = D(b, drop); var cc = D(c, drop);
        double Area(double ax, double ay, double bx, double by, double cx, double cy) => (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        var ab = Area(aa.X, aa.Y, bb.X, bb.Y, pp.X, pp.Y);
        var bc = Area(bb.X, bb.Y, cc.X, cc.Y, pp.X, pp.Y);
        var ca = Area(cc.X, cc.Y, aa.X, aa.Y, pp.X, pp.Y);
        var neg = ab < -1e-8 || bc < -1e-8 || ca < -1e-8;
        var pos = ab > 1e-8 || bc > 1e-8 || ca > 1e-8;
        return !(neg && pos);
    }

    static Mesh Voxel(Part3D part)
    {
        var m = new Mesh();
        var hx = Math.Max(0.2, part.Sx) / 2.0; var hy = Math.Max(0.2, part.Sy) / 2.0; var hz = Math.Max(0.2, part.Sz) / 2.0;
        var bevel = part.Ops.Where(o => o.Kind == "bevel").Select(o => Math.Max(0, o.Amount)).DefaultIfEmpty(0).Max();
        var shell = part.Ops.Where(o => o.Kind == "shell").Select(o => Math.Max(0, o.Amount)).DefaultIfEmpty(0).Max();
        var margin = Math.Max(bevel, shell) + 0.15;
        var min = new Vec3(-hx - margin, -hy - margin, -hz - margin);
        var max = new Vec3(hx + margin, hy + margin, hz + margin);
        var longest = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
        var cell = longest / 46.0;
        foreach (var h in part.Ops.Where(o => o.Kind == "hole" && o.Diameter > 0)) cell = Math.Min(cell, h.Diameter / 9.0);
        cell = Math.Max(cell, longest / 64.0);
        int N(double span) => Math.Clamp((int)Math.Ceiling(span / cell), 8, 64);
        var nx = N(max.X - min.X); var ny = N(max.Y - min.Y); var nz = N(max.Z - min.Z);
        var dx = (max.X - min.X) / nx; var dy = (max.Y - min.Y) / ny; var dz = (max.Z - min.Z) / nz;
        var solid = new bool[nx * ny * nz];
        int Id(int i, int j, int k) => i + nx * (j + ny * k);
        for (var k = 0; k < nz; k++)
            for (var j = 0; j < ny; j++)
                for (var i = 0; i < nx; i++)
                {
                    var p = new Vec3(min.X + (i + 0.5) * dx, min.Y + (j + 0.5) * dy, min.Z + (k + 0.5) * dz);
                    solid[Id(i, j, k)] = Sd(part, p) < 0;
                }
        void Face(Vec3 a, Vec3 b, Vec3 c, Vec3 d) { m.Add(a, b, c); m.Add(a, c, d); }
        for (var k = 0; k < nz; k++)
            for (var j = 0; j < ny; j++)
                for (var i = 0; i < nx; i++)
                {
                    if (!solid[Id(i, j, k)]) continue;
                    var x0 = min.X + i * dx; var y0 = min.Y + j * dy; var z0 = min.Z + k * dz;
                    var x1 = x0 + dx; var y1 = y0 + dy; var z1 = z0 + dz;
                    if (i + 1 >= nx || !solid[Id(i + 1, j, k)]) Face(new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), new(x1, y0, z1));
                    if (i == 0 || !solid[Id(i - 1, j, k)]) Face(new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0));
                    if (j + 1 >= ny || !solid[Id(i, j + 1, k)]) Face(new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0));
                    if (j == 0 || !solid[Id(i, j - 1, k)]) Face(new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1));
                    if (k + 1 >= nz || !solid[Id(i, j, k + 1)]) Face(new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1));
                    if (k == 0 || !solid[Id(i, j, k - 1)]) Face(new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), new(x1, y0, z0));
                }
        return m;
    }

    static double Sd(Part3D part, Vec3 p)
    {
        var bevel = part.Ops.Where(o => o.Kind == "bevel").Select(o => Math.Max(0, o.Amount)).DefaultIfEmpty(0).Max();
        var d = SdShape(part, p, bevel);
        foreach (var h in part.Ops.Where(o => o.Kind == "hole")) d = Math.Max(d, -SdHole(p, h, part));
        var shell = part.Ops.Where(o => o.Kind == "shell").Select(o => o.Amount).DefaultIfEmpty(0).Max();
        if (shell > 0)
        {
            var t = Math.Max(0.05, shell);
            d = Math.Max(d, -d - t);
        }
        return d;
    }

    static double SdShape(Part3D part, Vec3 p, double r)
    {
        var sx = Math.Max(0.2, part.Sx); var sy = Math.Max(0.2, part.Sy); var sz = Math.Max(0.2, part.Sz);
        r = Math.Max(0, r);
        switch (part.Shape)
        {
            case "sphere":
                return SdEllipsoid(p, Math.Max(0.05, sx / 2 - r), Math.Max(0.05, sy / 2 - r), Math.Max(0.05, sz / 2 - r)) - Math.Min(r, Math.Min(sx, Math.Min(sy, sz)) / 4);
            case "cylinder":
                return SdCyl(new(p.X, p.Y, p.Z), Math.Max(0.05, (sx + sy) / 4 - r), Math.Max(0.05, sz / 2 - r)) - Math.Min(r, Math.Min((sx + sy) / 8, sz / 4));
            case "cone":
                return SdCone(p, Math.Max(0.05, (sx + sy) / 4 - r), Math.Max(0.05, sz / 2 - r)) - Math.Min(r, sz / 6);
            case "torus":
                var tube = Math.Max(0.08, sy / 2 - r);
                var R = Math.Max(tube + 0.05, sx / 2 - tube - r);
                return SdTorus(p, R, tube) - Math.Min(r, tube * 0.4);
            case "plane":
                return SdBox(p, Math.Max(0.05, sx / 2 - r), Math.Max(0.05, sy / 2 - r), Math.Max(0.05, Math.Max(sz, 0.2) / 2 - r)) - Math.Min(r, 0.1);
            case "text":
                return SdText(p, part.Text, sx, sy, sz);
            default:
                var c = Math.Min(r, Math.Min(sx, Math.Min(sy, sz)) * 0.2);
                return SdBox(p, Math.Max(0.05, sx / 2 - c), Math.Max(0.05, sy / 2 - c), Math.Max(0.05, sz / 2 - c)) - c;
        }
    }

    static double SdBox(Vec3 p, double hx, double hy, double hz)
    {
        var qx = Math.Abs(p.X) - hx; var qy = Math.Abs(p.Y) - hy; var qz = Math.Abs(p.Z) - hz;
        var ox = Math.Max(qx, 0); var oy = Math.Max(qy, 0); var oz = Math.Max(qz, 0);
        return Math.Sqrt(ox * ox + oy * oy + oz * oz) + Math.Min(Math.Max(qx, Math.Max(qy, qz)), 0);
    }

    static double SdEllipsoid(Vec3 p, double rx, double ry, double rz)
    {
        var k0 = Math.Sqrt(p.X * p.X / (rx * rx) + p.Y * p.Y / (ry * ry) + p.Z * p.Z / (rz * rz));
        var k1 = Math.Sqrt(p.X * p.X / (rx * rx * rx * rx) + p.Y * p.Y / (ry * ry * ry * ry) + p.Z * p.Z / (rz * rz * rz * rz));
        return k1 < 1e-8 ? k0 - 1 : k0 * (k0 - 1) / k1;
    }

    static double SdCyl(Vec3 p, double rad, double hz)
    {
        var dx = Math.Sqrt(p.X * p.X + p.Y * p.Y) - rad; var dy = Math.Abs(p.Z) - hz;
        var ox = Math.Max(dx, 0); var oy = Math.Max(dy, 0);
        return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(dx, dy), 0);
    }

    static double SdCone(Vec3 p, double rad, double hz)
    {
        // Kegel: Spitze bei +hz, Boden bei -hz. Annäherung über eine Kugelkappe ist unnötig; Ebene + Radius.
        var qz = (p.Z + hz) / (2 * hz);
        var rr = rad * (1 - Math.Clamp(qz, 0, 1));
        var side = Math.Sqrt(p.X * p.X + p.Y * p.Y) - rr;
        var cap = -hz - p.Z;
        var top = p.Z - hz;
        return Math.Max(Math.Max(side, cap), top);
    }

    static double SdTorus(Vec3 p, double R, double tube)
    {
        var q = Math.Sqrt(p.X * p.X + p.Y * p.Y) - R;
        return Math.Sqrt(q * q + p.Z * p.Z) - tube;
    }

    static double SdHole(Vec3 p, PartOp h, Part3D part)
    {
        var q = new Vec3(p.X - h.AtX, p.Y - h.AtY, p.Z - h.AtZ);
        if (h.Axis == "x") q = new(q.Y, q.Z, q.X);
        else if (h.Axis == "y") q = new(q.X, q.Z, q.Y);
        var half = Math.Max(part.Sx, Math.Max(part.Sy, part.Sz)) + 20;
        return SdCyl(q, Math.Max(0.05, h.Diameter / 2), half);
    }

    static double SdText(Vec3 p, string text, double sx, double sy, double sz)
    {
        var glyphs = GlyphsOf(text);
        if (glyphs.Count == 0) return SdBox(p, sx / 2, Math.Max(sy, 0.2) / 2, sz / 2);
        var cols = glyphs.Count * 6 - 1;
        var pw = sx / cols; var ph = sz / 7.0; var depth = Math.Max(sy, 0.2);
        double best = 1e9;
        for (var i = 0; i < glyphs.Count; i++)
        {
            var rows = FontRows(glyphs[i]);
            for (var row = 0; row < 7; row++)
                for (var col = 0; col < 5; col++)
                {
                    if ((rows[row] & (1 << (4 - col))) == 0) continue;
                    var cx = -sx / 2 + (i * 6 + col + 0.5) * pw;
                    var cz = sz / 2 - (row + 0.5) * ph;
                    best = Math.Min(best, SdBox(new(p.X - cx, p.Y, p.Z - cz), pw * 0.5, depth / 2, ph * 0.5));
                }
        }
        return best;
    }

    static void Text(Mesh m, string text, double sx, double sy, double sz)
    {
        var glyphs = GlyphsOf(text);
        if (glyphs.Count == 0) { Box(m, sx / 2, Math.Max(sy, 0.2) / 2, sz / 2); return; }
        var cols = glyphs.Count * 6 - 1;
        var pw = sx / cols; var ph = sz / 7.0; var depth = Math.Max(sy, 0.2);
        for (var i = 0; i < glyphs.Count; i++)
        {
            var rows = FontRows(glyphs[i]);
            for (var row = 0; row < 7; row++)
                for (var col = 0; col < 5; col++)
                {
                    if ((rows[row] & (1 << (4 - col))) == 0) continue;
                    var cx = -sx / 2 + (i * 6 + col + 0.5) * pw;
                    var cz = sz / 2 - (row + 0.5) * ph;
                    AddBox(m, cx - pw * 0.48, cx + pw * 0.48, -depth / 2, depth / 2, cz - ph * 0.48, cz + ph * 0.48);
                }
        }
    }

    static List<char> GlyphsOf(string text)
    {
        var s = (string.IsNullOrWhiteSpace(text) ? "Text" : text).Trim();
        s = s.Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace("Ä", "A").Replace("Ö", "O").Replace("Ü", "U").Replace("ß", "ss");
        var list = new List<char>();
        foreach (var ch in s.ToUpperInvariant())
        {
            if (ch == ' ' || Font.ContainsKey(ch)) list.Add(ch);
            if (list.Count >= 16) break;
        }
        return list;
    }

    static int[] FontRows(char ch) => Font.TryGetValue(ch, out var rows) ? rows : Font[' '];

    static readonly Dictionary<char, int[]> Font = BuildFont();

    static Dictionary<char, int[]> BuildFont()
    {
        var raw = new Dictionary<char, string>
        {
            ['A'] = "01110,10001,10001,11111,10001,10001,10001", ['B'] = "11110,10001,10001,11110,10001,10001,11110",
            ['C'] = "01111,10000,10000,10000,10000,10000,01111", ['D'] = "11110,10001,10001,10001,10001,10001,11110",
            ['E'] = "11111,10000,10000,11110,10000,10000,11111", ['F'] = "11111,10000,10000,11110,10000,10000,10000",
            ['G'] = "01110,10001,10000,10111,10001,10001,01110", ['H'] = "10001,10001,10001,11111,10001,10001,10001",
            ['I'] = "01110,00100,00100,00100,00100,00100,01110", ['J'] = "00111,00010,00010,00010,10010,10010,01100",
            ['K'] = "10001,10010,10100,11000,10100,10010,10001", ['L'] = "10000,10000,10000,10000,10000,10000,11111",
            ['M'] = "10001,11011,10101,10101,10001,10001,10001", ['N'] = "10001,11001,10101,10011,10001,10001,10001",
            ['O'] = "01110,10001,10001,10001,10001,10001,01110", ['P'] = "11110,10001,10001,11110,10000,10000,10000",
            ['Q'] = "01110,10001,10001,10001,10101,10010,01101", ['R'] = "11110,10001,10001,11110,10100,10010,10001",
            ['S'] = "01111,10000,10000,01110,00001,00001,11110", ['T'] = "11111,00100,00100,00100,00100,00100,00100",
            ['U'] = "10001,10001,10001,10001,10001,10001,01110", ['V'] = "10001,10001,10001,10001,01010,01010,00100",
            ['W'] = "10001,10001,10001,10101,10101,10101,01010", ['X'] = "10001,10001,01010,00100,01010,10001,10001",
            ['Y'] = "10001,10001,01010,00100,00100,00100,00100", ['Z'] = "11111,00001,00010,00100,01000,10000,11111",
            ['0'] = "01110,10001,10011,10101,11001,10001,01110", ['1'] = "00100,01100,00100,00100,00100,00100,01110",
            ['2'] = "01110,10001,00001,00110,01000,10000,11111", ['3'] = "11110,00001,00001,01110,00001,00001,11110",
            ['4'] = "00010,00110,01010,10010,11111,00010,00010", ['5'] = "11111,10000,10000,11110,00001,00001,11110",
            ['6'] = "01110,10000,10000,11110,10001,10001,01110", ['7'] = "11111,00001,00010,00100,01000,01000,01000",
            ['8'] = "01110,10001,10001,01110,10001,10001,01110", ['9'] = "01110,10001,10001,01111,00001,00001,01110",
            [' '] = "00000,00000,00000,00000,00000,00000,00000", ['-'] = "00000,00000,00000,11111,00000,00000,00000",
            ['.'] = "00000,00000,00000,00000,00000,01100,01100",
        };
        var map = new Dictionary<char, int[]>();
        foreach (var (ch, rows) in raw)
            map[ch] = rows.Split(',').Select(s => Convert.ToInt32(s, 2)).ToArray();
        return map;
    }

    static Mesh Weld(Mesh src)
    {
        var map = new Dictionary<(long, long, long), int>();
        var m = new Mesh();
        int Id(Vec3 v)
        {
            var key = ((long)Math.Round(v.X * 1000), (long)Math.Round(v.Y * 1000), (long)Math.Round(v.Z * 1000));
            if (map.TryGetValue(key, out var i)) return i;
            m.V.Add(v); return map[key] = m.V.Count - 1;
        }
        for (var t = 0; t < src.Triangles; t++)
        {
            var ia = Id(src.V[src.I[t * 3]]); var ib = Id(src.V[src.I[t * 3 + 1]]); var ic = Id(src.V[src.I[t * 3 + 2]]);
            if (ia == ib || ib == ic || ia == ic) continue;
            m.I.Add(ia); m.I.Add(ib); m.I.Add(ic);
            m.R.Add(src.R[t]); m.G.Add(src.G[t]); m.B.Add(src.B[t]);
        }
        return m;
    }

    static void Laplacian(Mesh m, int times)
    {
        var n = m.V.Count;
        if (n == 0) return;
        var nbr = new List<int>[n];
        for (var i = 0; i < n; i++) nbr[i] = [];
        for (var t = 0; t < m.I.Count; t += 3)
        {
            void Link(int a, int b) { if (!nbr[a].Contains(b)) nbr[a].Add(b); }
            Link(m.I[t], m.I[t + 1]); Link(m.I[t + 1], m.I[t]);
            Link(m.I[t + 1], m.I[t + 2]); Link(m.I[t + 2], m.I[t + 1]);
            Link(m.I[t + 2], m.I[t]); Link(m.I[t], m.I[t + 2]);
        }
        for (var k = 0; k < times; k++)
        {
            var next = new Vec3[n];
            for (var i = 0; i < n; i++)
            {
                if (nbr[i].Count == 0) { next[i] = m.V[i]; continue; }
                double x = 0, y = 0, z = 0;
                foreach (var j in nbr[i]) { x += m.V[j].X; y += m.V[j].Y; z += m.V[j].Z; }
                var c = nbr[i].Count;
                next[i] = m.V[i] * 0.45 + new Vec3(x / c, y / c, z / c) * 0.55;
            }
            for (var i = 0; i < n; i++) m.V[i] = next[i];
        }
    }

    static void Mirror(Mesh m, string axis)
    {
        if (m.V.Count == 0) return;
        var min = axis == "y" ? m.V.Min(v => v.Y) : axis == "z" ? m.V.Min(v => v.Z) : m.V.Min(v => v.X);
        var n = m.V.Count;
        for (var i = 0; i < n; i++)
        {
            var v = m.V[i];
            m.V.Add(axis == "y" ? new(v.X, 2 * min - v.Y, v.Z) : axis == "z" ? new(v.X, v.Y, 2 * min - v.Z) : new(2 * min - v.X, v.Y, v.Z));
        }
        var tcount = m.I.Count;
        for (var i = 0; i < tcount; i += 3)
        {
            m.I.Add(m.I[i] + n); m.I.Add(m.I[i + 2] + n); m.I.Add(m.I[i + 1] + n);
            m.R.Add(m.R[i / 3]); m.G.Add(m.G[i / 3]); m.B.Add(m.B[i / 3]);
        }
    }

    public static (byte R, byte G, byte B) Rgb(string? hex)
    {
        if (!Scene3D.ValidColor(hex)) return (47, 134, 246);
        return (Convert.ToByte(hex![1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));
    }

    public static bool Contains(Mesh m, Vec3 p)
    {
        var dir = new Vec3(1, 0.013, 0.017);
        var hits = 0;
        for (var t = 0; t < m.I.Count; t += 3)
            if (Ray(p, dir, m.V[m.I[t]], m.V[m.I[t + 1]], m.V[m.I[t + 2]])) hits++;
        return hits % 2 == 1;
    }

    static bool Ray(Vec3 orig, Vec3 dir, Vec3 v0, Vec3 v1, Vec3 v2)
    {
        const double E = 1e-8;
        var e1 = v1 - v0; var e2 = v2 - v0;
        var p = Vec3.Cross(dir, e2);
        var det = Vec3.Dot(e1, p);
        if (Math.Abs(det) < E) return false;
        var inv = 1 / det;
        var s = orig - v0;
        var u = inv * Vec3.Dot(s, p);
        if (u < 0 || u > 1) return false;
        var q = Vec3.Cross(s, e1);
        var v = inv * Vec3.Dot(dir, q);
        if (v < 0 || u + v > 1) return false;
        var dist = inv * Vec3.Dot(e2, q);
        return dist > E;
    }

    public static byte[]? RenderPng(Scene3D scene, string view, int w, int h)
    {
        if (scene.Parts.Count == 0) return null;
        var mesh = FromScene(scene);
        return mesh.Triangles == 0 ? null : RenderPng(mesh, view, w, h);
    }

    public static byte[] RenderPng(Mesh mesh, string view, int w, int h)
    {
        var proj = new (float X, float Y, float Z)[mesh.V.Count];
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (var i = 0; i < mesh.V.Count; i++)
        {
            var s = Project(mesh.V[i], view);
            proj[i] = s;
            minX = Math.Min(minX, s.X); maxX = Math.Max(maxX, s.X);
            minY = Math.Min(minY, s.Y); maxY = Math.Max(maxY, s.Y);
        }
        var spanX = Math.Max(0.001f, maxX - minX); var spanY = Math.Max(0.001f, maxY - minY);
        var scale = Math.Min(w * 0.86f / spanX, h * 0.86f / spanY);
        var cx = (minX + maxX) / 2; var cy = (minY + maxY) / 2;
        (float X, float Y, float Z) Screen(int i)
        {
            var p = proj[i];
            return ((p.X - cx) * scale + w / 2f, h / 2f - (p.Y - cy) * scale, p.Z);
        }
        var zbuf = new float[w * h];
        Array.Fill(zbuf, float.NegativeInfinity);
        var rgb = new byte[w * h * 3];
        for (var i = 0; i < w * h; i++) { rgb[i * 3] = 11; rgb[i * 3 + 1] = 23; rgb[i * 3 + 2] = 48; }
        var light = new Vec3(0.32, -0.52, 0.79).Norm();
        for (var t = 0; t < mesh.Triangles; t++)
        {
            var ia = mesh.I[t * 3]; var ib = mesh.I[t * 3 + 1]; var ic = mesh.I[t * 3 + 2];
            var a = Screen(ia); var b = Screen(ib); var c = Screen(ic);
            var n = Vec3.Cross(mesh.V[ib] - mesh.V[ia], mesh.V[ic] - mesh.V[ia]).Norm();
            var shade = 0.30 + 0.70 * Math.Abs(Vec3.Dot(n, light));
            var cr = (byte)Math.Clamp(mesh.R[t] * shade, 0, 255);
            var cg = (byte)Math.Clamp(mesh.G[t] * shade, 0, 255);
            var cb = (byte)Math.Clamp(mesh.B[t] * shade, 0, 255);
            Fill(rgb, zbuf, w, h, a, b, c, cr, cg, cb);
        }
        return Png(w, h, rgb);
    }

    static (float X, float Y, float Z) Project(Vec3 p, string view)
    {
        if (view == "right") return ((float)-p.Y, (float)p.Z, (float)p.X);
        if (view == "top") return ((float)p.X, (float)-p.Y, (float)p.Z);
        if (view == "angle")
        {
            var yaw = 38 * Math.PI / 180; var pitch = 28 * Math.PI / 180;
            var x = p.X * Math.Cos(yaw) - p.Y * Math.Sin(yaw);
            var y = p.X * Math.Sin(yaw) + p.Y * Math.Cos(yaw);
            var y2 = y * Math.Cos(pitch) - p.Z * Math.Sin(pitch);
            var z = y * Math.Sin(pitch) + p.Z * Math.Cos(pitch);
            return ((float)x, (float)z, (float)y2);
        }
        return ((float)p.X, (float)p.Z, (float)p.Y);
    }

    static void Fill(byte[] rgb, float[] zbuf, int w, int h, (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c, byte r, byte g, byte bl)
    {
        static float Edge(float ax, float ay, float bx, float by, float px, float py) => (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        var area = Edge(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (Math.Abs(area) < 0.5f) return;
        var minX = (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)));
        var maxX = (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X)));
        var minY = (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)));
        var maxY = (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y)));
        minX = Math.Clamp(minX, 0, w - 1); maxX = Math.Clamp(maxX, 0, w - 1);
        minY = Math.Clamp(minY, 0, h - 1); maxY = Math.Clamp(maxY, 0, h - 1);
        var inv = 1f / area;
        for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var px = x + 0.5f; var py = y + 0.5f;
                var w0 = Edge(b.X, b.Y, c.X, c.Y, px, py);
                var w1 = Edge(c.X, c.Y, a.X, a.Y, px, py);
                var w2 = Edge(a.X, a.Y, b.X, b.Y, px, py);
                if (area > 0) { if (w0 < 0 || w1 < 0 || w2 < 0) continue; }
                else if (w0 > 0 || w1 > 0 || w2 > 0) continue;
                var z = (w0 * a.Z + w1 * b.Z + w2 * c.Z) * inv;
                var id = y * w + x;
                if (z <= zbuf[id]) continue;
                zbuf[id] = z;
                rgb[id * 3] = r; rgb[id * 3 + 1] = g; rgb[id * 3 + 2] = bl;
            }
    }

    public static byte[] Png(int w, int h, byte[] rgb)
    {
        var raw = new byte[h * (1 + w * 3)];
        for (var y = 0; y < h; y++)
        {
            var row = y * (1 + w * 3);
            raw[row] = 0;
            Buffer.BlockCopy(rgb, y * w * 3, raw, row + 1, w * 3);
        }
        using var ms = new MemoryStream();
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Chunk(ms, "IHDR", Ih(w, h));
        Chunk(ms, "IDAT", Zlib(raw));
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    static byte[] Ih(int w, int h)
    {
        var b = new byte[13];
        WriteBe(b, 0, w); WriteBe(b, 4, h);
        b[8] = 8; b[9] = 2;
        return b;
    }

    static void WriteBe(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBe(len, 0, data.Length); s.Write(len);
        var td = new byte[4 + data.Length];
        var tb = System.Text.Encoding.ASCII.GetBytes(type);
        Buffer.BlockCopy(tb, 0, td, 0, 4); Buffer.BlockCopy(data, 0, td, 4, data.Length);
        s.Write(td);
        var crc = new byte[4]; WriteBe(crc, 0, (int)Crc(td)); s.Write(crc);
    }

    static uint Crc(byte[] buf)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in buf) { c ^= b; for (var i = 0; i < 8; i++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; }
        return c ^ 0xFFFFFFFFu;
    }

    static byte[] Zlib(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); ms.WriteByte(0x9C);
        using (var d = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true)) d.Write(data);
        var a = Adler(data);
        ms.WriteByte((byte)(a >> 24)); ms.WriteByte((byte)(a >> 16)); ms.WriteByte((byte)(a >> 8)); ms.WriteByte((byte)a);
        return ms.ToArray();
    }

    static uint Adler(byte[] data)
    {
        uint s1 = 1, s2 = 0;
        foreach (var b in data) { s1 = (s1 + b) % 65521; s2 = (s2 + s1) % 65521; }
        return (s2 << 16) | s1;
    }

    public static string Unique(string path)
    {
        var dir = Path.GetDirectoryName(path)!; var baseName = Path.GetFileNameWithoutExtension(path); var ext = Path.GetExtension(path);
        var i = 2;
        while (File.Exists(path)) { path = Path.Combine(dir, $"{baseName}-{i}{ext}"); i++; }
        return path;
    }
}
