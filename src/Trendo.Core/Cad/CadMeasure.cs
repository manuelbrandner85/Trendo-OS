namespace Trendo.Core.Cad;

/// <summary>Messen wie in Fusion 360 / Onshape „Eigenschaften“ und FreeCAD (nur Funktionen, Clean-Room, 04.11.2026):
/// Volumen, Oberfläche, Masse je Werkstoff, Schwerpunkt, Querschnittsfläche in einer Höhe, Normbohrungen (ISO 273, DIN 74, Kernloch).</summary>
public static class CadMeasure
{
    public sealed record Werkstoff(string Id, string Name, double Density);
    public static readonly Werkstoff[] Werkstoffe =
    [
        new("pla", "PLA (3D-Druck)", 1.24), new("petg", "PETG (3D-Druck)", 1.27), new("stahl", "Stahl", 7.85), new("edelstahl", "Edelstahl", 7.9),
        new("alu", "Aluminium", 2.70), new("messing", "Messing", 8.5), new("buche", "Holz (Buche)", 0.72), new("fichte", "Holz (Fichte)", 0.47), new("beton", "Beton", 2.4),
    ];

    public sealed record Props(double VolumeCm3, double AreaCm2, double MassG, Vec3 Center, Vec3 Size);

    static IEnumerable<(Vec3 A, Vec3 B, Vec3 C)> Tris(IEnumerable<Solid> solids) => solids.Where(s => s.Polys.Count > 0).SelectMany(CadExport.PrintTriangles);

    /// <summary>Eigenschaften aller Körper (Maße in mm): Volumen und Schwerpunkt über Tetraeder zum Ursprung, Oberfläche über die Dreiecke.</summary>
    public static Props Properties(IEnumerable<Solid> solids, string werkstoff = "pla")
    {
        var list = Tris(solids).ToList();
        double vol = 0, area = 0; var c = new Vec3(0, 0, 0);
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var (a, b, d) in list)
        {
            var v = Vec3.Dot(a, Vec3.Cross(b, d)) / 6; vol += v; c += (a + b + d) * (v / 4);
            area += Vec3.Cross(b - a, d - a).Len / 2;
            foreach (var p in new[] { a, b, d }) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z); }
        }
        if (list.Count == 0) throw new CadError("Es gibt noch keinen Körper zum Messen.");
        var center = Math.Abs(vol) < 1e-9 ? new Vec3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2) : c * (1 / vol);
        var rho = (Werkstoffe.FirstOrDefault(w => w.Id == werkstoff) ?? Werkstoffe[0]).Density;
        var cm3 = Math.Abs(vol) / 1000;
        return new(Math.Round(cm3, 3), Math.Round(area / 100, 3), Math.Round(cm3 * rho, 2), new(Math.Round(center.X, 2), Math.Round(center.Y, 2), Math.Round(center.Z, 2)), new(x1 - x0, y1 - y0, z1 - z0));
    }

    /// <summary>Querschnittsfläche (mm²) in Höhe z – Löcher werden abgezogen (Verschachtelungstiefe der Umrisse).</summary>
    public static double SectionArea(IEnumerable<Solid> solids, double z)
    {
        var m = new Mesh(); foreach (var (a, b, c) in Tris(solids)) m.Add(a, b, c);
        var loops = Slicer.Loops(PrintPrep.Slice(m, z + 1e-5)); // knapp neben der Höhe: CSG-Netze haben oft Ecken genau dort
        double sum = 0;
        foreach (var l in loops)
        {
            var p = l[0]; var depth = loops.Count(o => !ReferenceEquals(o, l) && Inside(o, p));
            sum += (depth % 2 == 0 ? 1 : -1) * Slicer.Area(l);
        }
        return Math.Round(Math.Max(0, sum), 2);
    }

    static bool Inside(IReadOnlyList<(double X, double Y)> poly, (double X, double Y) p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        return inside;
    }

    /// <summary>Normbohrungen: Durchgangsloch mittel (ISO 273), Kernloch fürs Gewinde, Senkung für Senkschrauben (DIN 74).</summary>
    public sealed record Hole(string Id, string Label, double Diameter, double Countersink);
    public static readonly Hole[] Holes = BuildHoles();

    static Hole[] BuildHoles()
    {
        (string M, double Through, double Core, double Sink)[] t =
        [
            ("M2", 2.4, 1.6, 4.4), ("M2,5", 2.9, 2.05, 5.5), ("M3", 3.4, 2.5, 6.5), ("M4", 4.5, 3.3, 8.6), ("M5", 5.5, 4.2, 10.4),
            ("M6", 6.6, 5.0, 12.4), ("M8", 9.0, 6.8, 16.4), ("M10", 11.0, 8.5, 20.4), ("M12", 13.5, 10.2, 24.4),
        ];
        var res = new List<Hole> { new("frei", "Eigenes Maß", 0, 0) };
        foreach (var (m, th, core, sink) in t)
        {
            res.Add(new($"{m}-durch", $"{m} Durchgang ({th:0.##} mm)", th, 0));
            res.Add(new($"{m}-kern", $"{m} Gewinde-Kernloch ({core:0.##} mm)", core, 0));
            res.Add(new($"{m}-senk", $"{m} Senkschraube ({th:0.##} / {sink:0.#} mm)", th, sink));
        }
        return [.. res];
    }
}
