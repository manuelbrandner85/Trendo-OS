using Trendo.Core.Cad;

namespace Trendo.Core.Arch;

/// <summary>T-0256: 3D-Modell aus dem Grundriss – Wände in voller Höhe mit echten Aussparungen für Türen (bis Sturz) und Fenster (Brüstung + Sturz),
/// Böden je Belag eingefärbt, Möbel als Körper in üblicher Höhe. Für die 3D-Ansicht (CadView) und den Export.</summary>
public static class Plan3D
{
    /// <summary>Alle Geschosse übereinander (jedes auf seiner Höhenlage).</summary>
    public static List<(string Color, Solid Solid, string? Id)> BuildAll(IReadOnlyList<FloorPlan> levels, Func<string, Mesh?>? model = null) => levels.SelectMany(l => Build(l, model)).ToList();

    /// <summary>T-0280: <paramref name="model"/> liefert zum Möbeltyp das echte 3D-Modell (Zentimeter, Z oben, Vorderseite −Y) – dann steht statt
    /// des Körpers das Modell im Raum, auf Stellmaß skaliert, gedreht und gespiegelt wie im Plan. Ohne Modell bleibt der Körper.</summary>
    public static List<(string Color, Solid Solid, string? Id)> Build(FloorPlan p, Func<string, Mesh?>? model = null)
    {
        var z = p.Elevation;
        var res = new List<(string, Solid, string?)>();
        foreach (var r in p.Rooms.Where(r => r.Points.Count >= 3))
            res.Add((r.Floor switch { "parkett" => "#A9774A", "fliesen" => "#9DB3C6", "vinyl" => "#8E9C86", "teppich" => "#8A7BAE", _ => "#C29A63" },
                Slab(r.Points.Select(q => (q.X, q.Y)).ToList(), z - 2, z + 2), r.Id));
        foreach (var w in p.Walls)
        {
            var len = w.Length; if (len < 1) continue;
            var cuts = p.Openings.Where(o => o.WallId == w.Id).Select(o => (S: Math.Max(0, o.At - o.Width / 2), E: Math.Min(len, o.At + o.Width / 2), o.Sill, Top: o.Sill + o.Height)).OrderBy(c => c.S).ToList();
            var pos = 0.0;
            foreach (var c in cuts)
            {
                if (c.S > pos) res.Add(("#E9EDF3", Piece(w, pos, c.S, z, z + w.Height), w.Id));
                if (c.Sill > 0) res.Add(("#E9EDF3", Piece(w, c.S, c.E, z, z + c.Sill), w.Id));
                if (c.Top < w.Height) res.Add(("#E9EDF3", Piece(w, c.S, c.E, z + Math.Min(c.Top, w.Height), z + w.Height), w.Id));
                pos = Math.Max(pos, c.E);
            }
            if (len > pos) res.Add(("#E9EDF3", Piece(w, pos, len, z, z + w.Height), w.Id));
        }
        foreach (var i in p.Items)
        {
            if (model?.Invoke(i.Type) is { Triangles: > 0 } m) { foreach (var (col, sol) in Place(i, m, z)) res.Add((col, sol, i.Id)); continue; }
            var h = FurnitureCatalog.Get(i.Type)?.H ?? 60;
            res.Add((i.Type switch { "teppich" => "#7C6FA8", "pflanze" => "#3FA34D", "sofa" or "sessel" => "#55708F", "bett" or "doppelbett" => "#D9DEE7", _ => "#8C7A66" }, Slab(i.Corners(), z, z + Math.Max(1, h)), i.Id));
        }
        return res;
    }

    /// <summary>Modell auf das Möbel im Plan setzen: Grundfläche auf B × T skaliert (Höhe im Mittel mit), Boden auf die Geschosshöhe,
    /// Drehung und Spiegelung wie im Plan; je Farbe ein Teil (für Anzeige und OBJ-Export).</summary>
    public static List<(string Color, Solid Solid)> Place(Item i, Mesh m, double z)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var v in m.V) { x0 = Math.Min(x0, v.X); x1 = Math.Max(x1, v.X); y0 = Math.Min(y0, v.Y); y1 = Math.Max(y1, v.Y); z0 = Math.Min(z0, v.Z); }
        double mw = Math.Max(1e-6, x1 - x0), md = Math.Max(1e-6, y1 - y0), cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
        double sx = i.W / mw, sy = i.D / md, sz = (sx + sy) / 2;
        var a = i.Rot * Math.PI / 180; double c = Math.Cos(a), s = Math.Sin(a);
        Vec3 T(Vec3 v)
        {
            var lx = (v.X - cx) * sx * (i.Mirror ? -1 : 1); var ly = -(v.Y - cy) * sy; // Modell: Vorderseite −Y → im Plan (y nach unten) Vorderseite +y
            var px = i.X + lx * c - ly * s; var py = i.Y + lx * s + ly * c;
            return new Vec3(px, -py, z + (v.Z - z0) * sz);
        }
        var groups = new Dictionary<string, Solid>();
        for (var t = 0; t < m.Triangles; t++)
        {
            Vec3 A = T(m.V[m.I[3 * t]]), B = T(m.V[m.I[3 * t + 1]]), C = T(m.V[m.I[3 * t + 2]]);
            List<Vec3> vs = i.Mirror ? [A, C, B] : [A, B, C];
            if (Poly.TryCreate(vs) is not { } poly) continue;
            var key = $"#{m.R[t]:X2}{m.G[t]:X2}{m.B[t]:X2}";
            if (!groups.TryGetValue(key, out var sol)) groups[key] = sol = new Solid { Smooth = true };
            sol.Polys.Add(poly);
        }
        return groups.Select(g => (g.Key, g.Value)).ToList();
    }

    /// <summary>Netz für die Vorschau vereinfachen (Punkte auf ein Raster von <paramref name="cell"/> cm ziehen, entartete Dreiecke weg) –
    /// die Silhouette bleibt, die Foto-Ansicht rechnet ohnehin mit dem vollen Modell.</summary>
    public static Mesh Simplify(Mesh m, double cell)
    {
        var res = new Mesh(); var seen = new HashSet<(long, long, long, long, long, long, long, long, long)>();
        Vec3 Q(Vec3 v) => new(Math.Round(v.X / cell) * cell, Math.Round(v.Y / cell) * cell, Math.Round(v.Z / cell) * cell);
        for (var t = 0; t < m.Triangles; t++)
        {
            Vec3 a = Q(m.V[m.I[3 * t]]), b = Q(m.V[m.I[3 * t + 1]]), c = Q(m.V[m.I[3 * t + 2]]);
            if (Vec3.Cross(b - a, c - a).Len < 1e-9) continue;
            long K(double d) => (long)Math.Round(d / cell);
            if (!seen.Add((K(a.X), K(a.Y), K(a.Z), K(b.X), K(b.Y), K(b.Z), K(c.X), K(c.Y), K(c.Z)))) continue;
            res.CR = m.R[t]; res.CG = m.G[t]; res.CB = m.B[t]; res.Add(a, b, c);
        }
        return res;
    }

    /// <summary>Abschnitt [s, e] einer Wand von Höhe z0 bis z1 als Quader.</summary>
    static Solid Piece(Wall w, double s, double e, double z0, double z1)
    {
        var (ux, uy) = w.Dir; var (nx, ny) = (-uy * w.Thickness / 2, ux * w.Thickness / 2);
        var (ax, ay) = w.PointAt(s); var (bx, by) = w.PointAt(e);
        return Slab([(ax + nx, ay + ny), (bx + nx, by + ny), (bx - nx, by - ny), (ax - nx, ay - ny)], z0, z1);
    }

    /// <summary>Umriss (Grundriss-Koordinaten, y nach unten) zwischen z0 und z1 hochziehen – im Modell zeigt y nach oben.</summary>
    static Solid Slab(IReadOnlyList<(double X, double Y)> pts, double z0, double z1)
    {
        var m = pts.Select(q => (q.X, -q.Y)).ToList();
        if (Geo.SignedArea(m) < 0) m.Reverse();
        return CadShapes.Extrude(m, z1 - z0).Transform(v => new Vec3(v.X, v.Y, v.Z + z0));
    }
}
