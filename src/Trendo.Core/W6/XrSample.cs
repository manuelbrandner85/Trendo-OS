namespace Trendo.Core;

/// <summary>T-0269/T-0282: Beispielmodell zum Ausprobieren des Modell-Prüfers – Leseecke aus den in Blender gebauten Katalogmöbeln
/// (Sessel 80 × 85 × 90 cm, Zimmerpflanze 40 × 40 × 120 cm, Teppich 200 × 140 cm) in echten Maßen statt Kisten.
/// Das Netz rechnet wie alle Trendo-Netze in Zentimetern mit Z oben (GLB speichert wieder Meter, Y oben).</summary>
public static class XrSample
{
    static Mesh Model(string type)
    {
        using var s = typeof(XrSample).Assembly.GetManifestResourceStream($"Trendo.Core.Moebel.{type}.glb")
            ?? throw new InvalidOperationException($"Beispielmodell „{type}“ fehlt im Programm.");
        var tmp = Path.Combine(Path.GetTempPath(), $"trendo-xr-{Guid.NewGuid():N}.glb");
        try
        {
            using (var o = File.Create(tmp)) s.CopyTo(o);
            return Mesh3D.ReadMeshFile(tmp);
        }
        finally { try { File.Delete(tmp); } catch (IOException) { } }
    }

    /// <summary>Modell mit Grundflächenmitte auf (x, y) und Unterkante auf dem Boden einsetzen, um <paramref name="deg"/> um Z gedreht.</summary>
    static void Put(Mesh into, Mesh m, double x, double y, double deg = 0)
    {
        var (cx, cy, z0) = ((m.V.Min(v => v.X) + m.V.Max(v => v.X)) / 2, (m.V.Min(v => v.Y) + m.V.Max(v => v.Y)) / 2, m.V.Min(v => v.Z));
        var (c, s) = (Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180));
        var moved = new Mesh();
        moved.V.AddRange(m.V.Select(v => { var (dx, dy) = (v.X - cx, v.Y - cy); return new Vec3(x + dx * c - dy * s, y + dx * s + dy * c, v.Z - z0); }));
        moved.I.AddRange(m.I); moved.R.AddRange(m.R); moved.G.AddRange(m.G); moved.B.AddRange(m.B);
        into.Append(moved);
    }

    public static Mesh Build()
    {
        var m = new Mesh();
        Put(m, Model("teppich"), 0, 0);
        Put(m, Model("sessel"), -25, 5, 20); // leicht zur Raummitte gedreht, Front zeigt nach vorn (−Y)
        Put(m, Model("pflanze"), 62, 38);
        return m;
    }

    public static string Write(string path) { Mesh3D.WriteGlb(Build(), path); return path; }
}
