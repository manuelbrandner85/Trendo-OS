namespace Trendo.Core.Cad;

/// <summary>N23: Volumen als Menge ebener, nach außen zeigender Polygone (Millimeter). Boolesche Operationen (Vereinigen, Abziehen, Schneiden)
/// über BSP-Bäume – das bewährte Verfahren aus csg.js (Evan Wallace, MIT-Lizenz), hier eigenständig in C# umgesetzt.</summary>
public sealed class Solid
{
    public List<Poly> Polys { get; } = [];
    public Solid() { }
    public Solid(IEnumerable<Poly> polys) => Polys.AddRange(polys);
    public Solid Clone() => new(Polys.Select(p => p.Clone()));

    public Solid Union(Solid other)
    {
        var a = new Node(Clone().Polys); var b = new Node(other.Clone().Polys);
        a.ClipTo(b); b.ClipTo(a); b.Invert(); b.ClipTo(a); b.Invert(); a.Build(b.AllPolys());
        return new Solid(a.AllPolys());
    }

    public Solid Subtract(Solid other)
    {
        var a = new Node(Clone().Polys); var b = new Node(other.Clone().Polys);
        a.Invert(); a.ClipTo(b); b.ClipTo(a); b.Invert(); b.ClipTo(a); b.Invert(); a.Build(b.AllPolys()); a.Invert();
        return new Solid(a.AllPolys());
    }

    public Solid Intersect(Solid other)
    {
        var a = new Node(Clone().Polys); var b = new Node(other.Clone().Polys);
        a.Invert(); b.ClipTo(a); b.Invert(); a.ClipTo(b); b.ClipTo(a); a.Build(b.AllPolys()); a.Invert();
        return new Solid(a.AllPolys());
    }

    public Solid Transform(Func<Vec3, Vec3> f, bool flip = false)
    {
        var s = new Solid();
        foreach (var p in Polys) { var v = p.Verts.Select(f).ToList(); if (flip) v.Reverse(); if (Poly.TryCreate(v) is { } np) s.Polys.Add(np); }
        return s;
    }

    public (Vec3 Min, Vec3 Max) Bounds()
    {
        if (Polys.Count == 0) return (new Vec3(0, 0, 0), new Vec3(0, 0, 0));
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var v in Polys.SelectMany(p => p.Verts)) { x0 = Math.Min(x0, v.X); y0 = Math.Min(y0, v.Y); z0 = Math.Min(z0, v.Z); x1 = Math.Max(x1, v.X); y1 = Math.Max(y1, v.Y); z1 = Math.Max(z1, v.Z); }
        return (new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));
    }

    /// <summary>Dreiecke (Fächer je Polygon) für Anzeige und Export.</summary>
    public IEnumerable<(Vec3 A, Vec3 B, Vec3 C)> Triangles()
    {
        foreach (var p in Polys) for (var i = 1; i + 1 < p.Verts.Count; i++) yield return (p.Verts[0], p.Verts[i], p.Verts[i + 1]);
    }

    /// <summary>Rauminhalt in mm³ (Divergenzsatz über die Dreiecke).</summary>
    public double Volume() => Triangles().Sum(t => Vec3.Dot(t.A, Vec3.Cross(t.B, t.C))) / 6;

    // ---------- BSP ----------
    sealed class Node
    {
        Plane? plane; Node? front, back; readonly List<Poly> polys = [];
        public Node(List<Poly>? list = null) { if (list is not null) Build(list); }

        public void Invert()
        {
            foreach (var p in polys) p.Flip();
            plane = plane?.Flipped(); front?.Invert(); back?.Invert();
            (front, back) = (back, front);
        }

        public List<Poly> ClipPolys(List<Poly> list)
        {
            if (plane is null) return [.. list];
            var f = new List<Poly>(); var b = new List<Poly>();
            foreach (var p in list) plane.Split(p, f, b, f, b);
            f = front is not null ? front.ClipPolys(f) : f;
            b = back is not null ? back.ClipPolys(b) : [];
            f.AddRange(b);
            return f;
        }

        public void ClipTo(Node bsp)
        {
            var clipped = bsp.ClipPolys(polys); polys.Clear(); polys.AddRange(clipped);
            front?.ClipTo(bsp); back?.ClipTo(bsp);
        }

        public List<Poly> AllPolys()
        {
            var list = new List<Poly>(polys);
            if (front is not null) list.AddRange(front.AllPolys());
            if (back is not null) list.AddRange(back.AllPolys());
            return list;
        }

        public void Build(List<Poly> list)
        {
            if (list.Count == 0) return;
            plane ??= list[0].Plane;
            var f = new List<Poly>(); var b = new List<Poly>();
            foreach (var p in list) plane.Split(p, polys, polys, f, b);
            if (f.Count > 0) (front ??= new Node()).Build(f);
            if (b.Count > 0) (back ??= new Node()).Build(b);
        }
    }
}

public sealed class Plane(Vec3 normal, double w)
{
    public const double Eps = 1e-5;
    public Vec3 Normal { get; } = normal;
    public double W { get; } = w;
    public Plane Flipped() => new(Normal * -1, -W);

    public static Plane? From(Vec3 a, Vec3 b, Vec3 c)
    {
        var n = Vec3.Cross(b - a, c - a); if (n.Len < 1e-12) return null;
        n = n.Norm(); return new Plane(n, Vec3.Dot(n, a));
    }

    /// <summary>Polygon an der Ebene teilen: koplanar (nach Ausrichtung vorn/hinten), vorn, hinten oder durchschnitten.</summary>
    public void Split(Poly p, List<Poly> coFront, List<Poly> coBack, List<Poly> front, List<Poly> back)
    {
        const int Co = 0, Fr = 1, Ba = 2, Sp = 3;
        var type = 0; var types = new int[p.Verts.Count];
        for (var i = 0; i < p.Verts.Count; i++)
        {
            var t = Vec3.Dot(Normal, p.Verts[i]) - W;
            var ty = t < -Eps ? Ba : t > Eps ? Fr : Co;
            type |= ty; types[i] = ty;
        }
        switch (type)
        {
            case Co: (Vec3.Dot(Normal, p.Plane.Normal) > 0 ? coFront : coBack).Add(p); break;
            case Fr: front.Add(p); break;
            case Ba: back.Add(p); break;
            default:
                var f = new List<Vec3>(); var b = new List<Vec3>();
                for (var i = 0; i < p.Verts.Count; i++)
                {
                    var j = (i + 1) % p.Verts.Count; int ti = types[i], tj = types[j]; var vi = p.Verts[i]; var vj = p.Verts[j];
                    if (ti != Ba) f.Add(vi);
                    if (ti != Fr) b.Add(vi);
                    if ((ti | tj) == Sp)
                    {
                        var t = (W - Vec3.Dot(Normal, vi)) / Vec3.Dot(Normal, vj - vi);
                        var v = vi + (vj - vi) * t; f.Add(v); b.Add(v);
                    }
                }
                if (f.Count >= 3 && Poly.TryCreate(f, p.Plane) is { } pf) front.Add(pf);
                if (b.Count >= 3 && Poly.TryCreate(b, p.Plane) is { } pb) back.Add(pb);
                break;
        }
    }
}

public sealed class Poly
{
    public List<Vec3> Verts { get; private set; }
    public Plane Plane { get; private set; }
    Poly(List<Vec3> verts, Plane plane) { Verts = verts; Plane = plane; }

    public static Poly? TryCreate(List<Vec3> verts, Plane? plane = null)
    {
        if (verts.Count < 3) return null;
        plane ??= Plane.From(verts[0], verts[1], verts[2]);
        for (var i = 3; plane is null && i < verts.Count; i++) plane = Plane.From(verts[0], verts[i - 1], verts[i]);
        return plane is null ? null : new Poly(verts, plane);
    }

    public Poly Clone() => new([.. Verts], Plane);
    public void Flip() { Verts.Reverse(); Plane = Plane.Flipped(); }
}
