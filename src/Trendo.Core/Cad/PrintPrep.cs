using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Trendo.Core.Cad;

/// <summary>Masterprompt 131: 3D-Druck vorbereiten – Modell in Millimetern laden (STL, OBJ, 3MF, GLB), auf die Druckplatte legen,
/// mit möglichst wenig Überhang aufstellen, Schichten schneiden, Platte/Überhang prüfen, Filament und Zeit grob schätzen.
/// Die genauen Bahnen rechnet weiterhin der Slicer (Bambu Studio); hier geht es um Vorschau und Prüfung.</summary>
public static class PrintPrep
{
    public sealed record Plate(string Name, double W, double D, double H);
    public static readonly Plate BambuA1 = new("Bambu Lab A1", 256, 256, 256);
    public const double LayerHeight = 0.2, PlaDensity = 1.24; // g/cm³

    /// <summary>Netz in Millimetern. STL/OBJ/3MF gelten als Millimeter, GLB/glTF als Meter.</summary>
    public static Mesh Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".3mf") return Read3mf(path);
        var m = Mesh3D.ReadMeshFile(path);
        if (ext is ".glb" or ".gltf") Scale(m, 1000);
        return m;
    }

    static void Scale(Mesh m, double k) { for (var i = 0; i < m.V.Count; i++) m.V[i] = m.V[i] * k; }

    /// <summary>3MF (auch .gcode.3mf): alle Objekte aus 3D/3dmodel.model, Einheit beachten.</summary>
    public static Mesh Read3mf(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("3dmodel.model", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("In der 3MF-Datei ist kein Modell.");
        if (entry.Length > 200_000_000) throw new InvalidOperationException("Das Modell ist zu groß.");
        XDocument x; using (var s = entry.Open()) x = XDocument.Load(s, LoadOptions.None);
        var root = x.Root!; var unit = (string?)root.Attribute("unit") ?? "millimeter";
        var k = unit switch { "micron" => 0.001, "centimeter" => 10, "inch" => 25.4, "foot" => 304.8, "meter" => 1000, _ => 1 };
        var m = new Mesh();
        foreach (var mesh in root.Descendants().Where(e => e.Name.LocalName == "mesh"))
        {
            var v = mesh.Descendants().Where(e => e.Name.LocalName == "vertex").Select(e => new Vec3(D(e, "x") * k, D(e, "y") * k, D(e, "z") * k)).ToList();
            foreach (var t in mesh.Descendants().Where(e => e.Name.LocalName == "triangle"))
            {
                int a = (int)D(t, "v1"), b = (int)D(t, "v2"), c = (int)D(t, "v3");
                if (a < v.Count && b < v.Count && c < v.Count) m.Add(v[a], v[b], v[c]);
            }
        }
        if (m.Triangles == 0) throw new InvalidOperationException("In der 3MF-Datei ist kein Dreieck.");
        return m;
        static double D(XElement e, string a) => double.Parse((string?)e.Attribute(a) ?? "0", CultureInfo.InvariantCulture);
    }

    public static (Vec3 Min, Vec3 Max) Bounds(Mesh m) => m.V.Count == 0 ? (new(0, 0, 0), new(0, 0, 0)) :
        (new(m.V.Min(v => v.X), m.V.Min(v => v.Y), m.V.Min(v => v.Z)), new(m.V.Max(v => v.X), m.V.Max(v => v.Y), m.V.Max(v => v.Z)));

    /// <summary>Auf die Platte: Unterseite auf z = 0, Grundfläche mittig.</summary>
    public static Mesh Place(Mesh m, Plate p)
    {
        var (lo, hi) = Bounds(m); var off = new Vec3(p.W / 2 - (lo.X + hi.X) / 2, p.D / 2 - (lo.Y + hi.Y) / 2, -lo.Z);
        var r = new Mesh(); r.V.AddRange(m.V.Select(v => v + off)); r.I.AddRange(m.I); r.R.AddRange(m.R); r.G.AddRange(m.G); r.B.AddRange(m.B); return r;
    }

    /// <summary>Um X oder Y in 90°-Schritten drehen (zum Aufstellen).</summary>
    public static Mesh Rotate(Mesh m, char axis, int quarter)
    {
        var r = new Mesh(); r.I.AddRange(m.I); r.R.AddRange(m.R); r.G.AddRange(m.G); r.B.AddRange(m.B);
        foreach (var v in m.V)
        {
            var q = v; for (var i = 0; i < ((quarter % 4) + 4) % 4; i++) q = axis == 'x' ? new Vec3(q.X, -q.Z, q.Y) : axis == 'y' ? new Vec3(q.Z, q.Y, -q.X) : new Vec3(-q.Y, q.X, q.Z);
            r.V.Add(q);
        }
        return r;
    }

    /// <summary>Fläche nach unten zeigender Dreiecke, die steiler als 45° überhängen und nicht auf der Platte liegen (mm²).</summary>
    public static double OverhangArea(Mesh m)
    {
        var minZ = m.V.Count == 0 ? 0 : m.V.Min(v => v.Z); double a = 0;
        for (var t = 0; t + 2 < m.I.Count; t += 3)
        {
            Vec3 p0 = m.V[m.I[t]], p1 = m.V[m.I[t + 1]], p2 = m.V[m.I[t + 2]];
            var n = Vec3.Cross(p1 - p0, p2 - p0); var len = n.Len; if (len < 1e-12) continue;
            if (n.Z / len < -0.7071 && Math.Min(p0.Z, Math.Min(p1.Z, p2.Z)) > minZ + 0.3) a += len / 2;
        }
        return a;
    }

    /// <summary>Die beste der sechs Grundlagen: am wenigsten Überhang, bei Gleichstand die niedrigste Höhe.</summary>
    public static (Mesh Mesh, string Label) AutoOrient(Mesh m)
    {
        var options = new (Mesh M, string L)[] { (m, "wie geladen"), (Rotate(m, 'x', 1), "um X gedreht"), (Rotate(m, 'x', 2), "auf den Kopf gestellt"), (Rotate(m, 'x', 3), "um X zurückgedreht"), (Rotate(m, 'y', 1), "um Y gedreht"), (Rotate(m, 'y', 3), "um Y zurückgedreht") };
        return options.Select(o => (o.M, o.L, Over: Math.Round(OverhangArea(o.M), 0), H: Bounds(o.M).Max.Z - Bounds(o.M).Min.Z)).OrderBy(o => o.Over).ThenBy(o => o.H).Select(o => (o.M, o.L)).First();
    }

    /// <summary>Volumen (mm³) über die Divergenz – bei geschlossenen Netzen exakt.</summary>
    public static double Volume(Mesh m)
    {
        double v = 0;
        for (var t = 0; t + 2 < m.I.Count; t += 3) v += Vec3.Dot(m.V[m.I[t]], Vec3.Cross(m.V[m.I[t + 1]], m.V[m.I[t + 2]])) / 6;
        return Math.Abs(v);
    }

    /// <summary>Schnitt in Höhe z: Liniensegmente (x1, y1, x2, y2) in mm.</summary>
    public static List<(double X1, double Y1, double X2, double Y2)> Slice(Mesh m, double z)
    {
        var res = new List<(double, double, double, double)>();
        for (var t = 0; t + 2 < m.I.Count; t += 3)
        {
            Vec3[] p = [m.V[m.I[t]], m.V[m.I[t + 1]], m.V[m.I[t + 2]]];
            var hits = new List<(double X, double Y)>();
            for (var i = 0; i < 3; i++)
            {
                var a = p[i]; var b = p[(i + 1) % 3];
                if ((a.Z - z) * (b.Z - z) < 0) { var k = (z - a.Z) / (b.Z - a.Z); hits.Add((a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k)); }
            }
            if (hits.Count == 2) res.Add((hits[0].X, hits[0].Y, hits[1].X, hits[1].Y));
        }
        return res;
    }

    public sealed record Report(double SizeX, double SizeY, double SizeZ, bool Fits, int Layers, double VolumeCm3, double OverhangCm2, double FilamentG, double FilamentM, TimeSpan Time);

    /// <summary>Kennzahlen. Filament: 2 Wände + 15 % Füllung grob über das Volumen (1,75-mm-Faden, PLA); Zeit: Volumen / 12 mm³/s mal 1,4 für Fahrwege.
    /// Ausdrücklich Schätzungen – die genaue Zeit zeigt der Slicer.</summary>
    public static Report Analyze(Mesh placed, Plate p)
    {
        var (lo, hi) = Bounds(placed); var vol = Volume(placed);
        var area = 0.0; for (var t = 0; t + 2 < placed.I.Count; t += 3) area += Vec3.Cross(placed.V[placed.I[t + 1]] - placed.V[placed.I[t]], placed.V[placed.I[t + 2]] - placed.V[placed.I[t]]).Len / 2;
        var shell = Math.Min(vol, area * 0.84); // zwei Wände à 0,42 mm
        var printed = shell + (vol - shell) * 0.15;
        var grams = printed / 1000 * PlaDensity; var meters = printed / (Math.PI * 0.875 * 0.875) / 1000;
        var seconds = printed / 12.0 * 1.4 + (hi.Z - lo.Z) / LayerHeight * 4;
        return new(hi.X - lo.X, hi.Y - lo.Y, hi.Z - lo.Z, lo.X >= -0.01 && lo.Y >= -0.01 && hi.X <= p.W + 0.01 && hi.Y <= p.D + 0.01 && hi.Z <= p.H + 0.01,
            (int)Math.Ceiling((hi.Z - lo.Z) / LayerHeight), vol / 1000, OverhangArea(placed) / 100, grams, meters, TimeSpan.FromSeconds(seconds));
    }
}
