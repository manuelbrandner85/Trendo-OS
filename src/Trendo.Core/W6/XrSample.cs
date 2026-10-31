namespace Trendo.Core;

/// <summary>T-0269: Beispielmodell (erfunden) zum Ausprobieren des Modell-Prüfers – Sessel mit Beistelltisch und Kugelleuchte. Maße unten in Metern,
/// das Netz selbst rechnet wie alle Trendo-Netze in Zentimetern (GLB speichert wieder Meter); Y ist oben.</summary>
public static class XrSample
{
    public static Mesh Build(int sphereSegments = 48)
    {
        var m = new Mesh();
        void Color(byte r, byte g, byte b) { m.CR = r; m.CG = g; m.CB = b; }
        void Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            Vec3 V(double x, double y, double z) => new(x * 100, y * 100, z * 100);
            var p = new[] { V(x0, y0, z0), V(x1, y0, z0), V(x1, y1, z0), V(x0, y1, z0), V(x0, y0, z1), V(x1, y0, z1), V(x1, y1, z1), V(x0, y1, z1) };
            foreach (var (a, b, c, d) in new[] { (0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5) }) { m.Add(p[a], p[b], p[c]); m.Add(p[a], p[c], p[d]); }
        }
        void Sphere(double cx, double cy, double cz, double r, int seg)
        {
            for (var i = 0; i < seg / 2; i++)
                for (var j = 0; j < seg; j++)
                {
                    Vec3 S(int a, int b) { var th = Math.PI * a / (seg / 2); var ph = 2 * Math.PI * b / seg; return new((cx + r * Math.Sin(th) * Math.Cos(ph)) * 100, (cy + r * Math.Cos(th)) * 100, (cz + r * Math.Sin(th) * Math.Sin(ph)) * 100); }
                    var (p00, p01, p10, p11) = (S(i, j), S(i, j + 1), S(i + 1, j), S(i + 1, j + 1));
                    m.Add(p00, p01, p11); m.Add(p00, p11, p10);
                }
        }
        Color(70, 96, 140); Box(0, 0.38, 0, 0.8, 0.52, 0.8); Box(0, 0.52, 0.65, 0.8, 1.0, 0.8); Box(-0.12, 0.38, 0, 0, 0.68, 0.8); Box(0.8, 0.38, 0, 0.92, 0.68, 0.8); // Sessel
        Color(60, 44, 32); foreach (var (x, z) in new[] { (-0.1, 0.02), (0.88, 0.02), (-0.1, 0.76), (0.88, 0.76) }) Box(x, 0, z, x + 0.05, 0.38, z + 0.05);
        Color(150, 112, 74); Box(1.15, 0.5, 0.2, 1.65, 0.54, 0.7); Box(1.38, 0, 0.43, 1.42, 0.5, 0.47); Box(1.25, 0, 0.33, 1.55, 0.02, 0.57); // Beistelltisch
        Color(245, 214, 140); Sphere(1.4, 0.72, 0.45, 0.15, sphereSegments); // Leuchte
        return m;
    }

    public static string Write(string path) { Mesh3D.WriteGlb(Build(), path); return path; }
}
