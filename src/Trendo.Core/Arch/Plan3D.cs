using Trendo.Core.Cad;

namespace Trendo.Core.Arch;

/// <summary>T-0256: 3D-Modell aus dem Grundriss – Wände in voller Höhe mit echten Aussparungen für Türen (bis Sturz) und Fenster (Brüstung + Sturz),
/// Böden je Belag eingefärbt, Möbel als Körper in üblicher Höhe. Für die 3D-Ansicht (CadView) und den Export.</summary>
public static class Plan3D
{
    /// <summary>Alle Geschosse übereinander (jedes auf seiner Höhenlage).</summary>
    public static List<(string Color, Solid Solid, string? Id)> BuildAll(IReadOnlyList<FloorPlan> levels) => levels.SelectMany(Build).ToList();

    public static List<(string Color, Solid Solid, string? Id)> Build(FloorPlan p)
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
            var h = FurnitureCatalog.Get(i.Type)?.H ?? 60;
            res.Add((i.Type switch { "teppich" => "#7C6FA8", "pflanze" => "#3FA34D", "sofa" or "sessel" => "#55708F", "bett" or "doppelbett" => "#D9DEE7", _ => "#8C7A66" }, Slab(i.Corners(), z, z + Math.Max(1, h)), i.Id));
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
