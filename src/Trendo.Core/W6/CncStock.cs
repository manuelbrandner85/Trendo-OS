namespace Trendo.Core;

/// <summary>SPEC-14: Materialabtrag am Werkstück. Ein Höhenfeld wird entlang des Werkzeugwegs mit dem Fräserquerschnitt abgesenkt (Schaftfräser).</summary>
public sealed class CncStock
{
    public const int MaxCells = 170;
    /// <summary>Auflösung für das fertige Foto; in der Vorschau wären so viele Dreiecke zu langsam.</summary>
    public const int PhotoCells = 420;
    /// <summary>Dichte in g/cm³.</summary>
    public static double Density(string material) => material.ToLowerInvariant() switch { var m when m.Contains("alu") => 2.7, var m when m.Contains("stahl") => 7.85, var m when m.Contains("messing") => 8.5, var m when m.Contains("holz") => 0.6, var m when m.Contains("kunst") || m.Contains("acryl") || m.Contains("pom") => 1.2, _ => 2.7 };

    public double X0 { get; }
    public double Y0 { get; }
    public double Cell { get; }
    public int Nx { get; }
    public int Ny { get; }
    public double Thickness { get; }
    readonly double[,] _z; // Oberkante je Zelle in mm; 0 = Rohteil

    CncStock(double x0, double y0, double cell, int nx, int ny, double thickness)
    {
        X0 = x0; Y0 = y0; Cell = cell; Nx = nx; Ny = ny; Thickness = thickness; _z = new double[nx, ny];
    }

    public double HeightAt(int i, int j) => _z[i, j];

    /// <summary>Rohteil um den Werkzeugweg legen und bis zum Schritt <paramref name="upTo"/> abfräsen (alle Schritte, wenn negativ).</summary>
    public static CncStock Cut(IReadOnlyList<CncSeg> segs, double toolD, int upTo = -1, int cells = MaxCells)
    {
        if (toolD <= 0) throw new ArgumentException("Der Werkzeugdurchmesser muss größer als 0 sein.");
        var cutting = segs.Where(s => !s.Rapid && Math.Min(s.Z0, s.Z1) < 0).ToList();
        if (cutting.Count == 0) throw new InvalidOperationException("Im Programm wird nichts abgetragen. Es fehlt ein Vorschub unter Z 0.");
        var margin = toolD + 4;
        double minX = cutting.Min(s => Math.Min(s.X0, s.X1)) - margin, maxX = cutting.Max(s => Math.Max(s.X0, s.X1)) + margin;
        double minY = cutting.Min(s => Math.Min(s.Y0, s.Y1)) - margin, maxY = cutting.Max(s => Math.Max(s.Y0, s.Y1)) + margin;
        var cell = Math.Max((maxX - minX) / cells, (maxY - minY) / cells);
        var thickness = -cutting.Min(s => Math.Min(s.Z0, s.Z1)) + 3;
        var stock = new CncStock(minX, minY, cell, (int)Math.Ceiling((maxX - minX) / cell) + 1, (int)Math.Ceiling((maxY - minY) / cell) + 1, thickness);
        var count = upTo < 0 ? segs.Count : Math.Min(upTo + 1, segs.Count);
        for (var k = 0; k < count; k++) if (!segs[k].Rapid) stock.Sweep(segs[k], toolD / 2);
        return stock;
    }

    void Sweep(CncSeg s, double r)
    {
        var len = Math.Sqrt(Math.Pow(s.X1 - s.X0, 2) + Math.Pow(s.Y1 - s.Y0, 2) + Math.Pow(s.Z1 - s.Z0, 2));
        var steps = Math.Max(1, (int)Math.Ceiling(len / (Cell * 0.5)));
        var reach = (int)Math.Ceiling(r / Cell) + 1;
        for (var k = 0; k <= steps; k++)
        {
            var t = (double)k / steps;
            double x = s.X0 + (s.X1 - s.X0) * t, y = s.Y0 + (s.Y1 - s.Y0) * t, z = s.Z0 + (s.Z1 - s.Z0) * t;
            if (z >= 0) continue;
            int ci = (int)Math.Round((x - X0) / Cell), cj = (int)Math.Round((y - Y0) / Cell);
            for (var i = Math.Max(0, ci - reach); i <= Math.Min(Nx - 1, ci + reach); i++)
                for (var j = Math.Max(0, cj - reach); j <= Math.Min(Ny - 1, cj + reach); j++)
                {
                    double dx = X0 + i * Cell - x, dy = Y0 + j * Cell - y;
                    if (dx * dx + dy * dy <= r * r && z < _z[i, j]) _z[i, j] = z;
                }
        }
    }

    /// <summary>Entferntes Volumen in cm³.</summary>
    public double RemovedVolume { get { double v = 0; foreach (var h in _z) v += -h * Cell * Cell; return v / 1000; } }

    /// <summary>Oberfläche als Dreiecksnetz in Zentimetern, Z nach oben, Unterkante auf 0.</summary>
    public Mesh ToMesh((byte R, byte G, byte B) color)
    {
        var m = new Mesh { CR = color.R, CG = color.G, CB = color.B };
        Vec3 P(int i, int j) => new((X0 + i * Cell) / 10, (Y0 + j * Cell) / 10, (Thickness + _z[i, j]) / 10);
        for (var i = 0; i < Nx - 1; i++)
            for (var j = 0; j < Ny - 1; j++)
            {
                m.Add(P(i, j), P(i + 1, j), P(i + 1, j + 1));
                m.Add(P(i, j), P(i + 1, j + 1), P(i, j + 1));
            }
        Vec3 B(int i, int j) => new((X0 + i * Cell) / 10, (Y0 + j * Cell) / 10, 0);
        void Wall(int i0, int j0, int i1, int j1) { m.Add(B(i0, j0), B(i1, j1), P(i1, j1)); m.Add(B(i0, j0), P(i1, j1), P(i0, j0)); }
        for (var i = 0; i < Nx - 1; i++) { Wall(i, 0, i + 1, 0); Wall(i + 1, Ny - 1, i, Ny - 1); }
        for (var j = 0; j < Ny - 1; j++) { Wall(Nx - 1, j, Nx - 1, j + 1); Wall(0, j + 1, 0, j); }
        m.Add(B(0, 0), B(Nx - 1, Ny - 1), B(Nx - 1, 0));
        m.Add(B(0, 0), B(0, Ny - 1), B(Nx - 1, Ny - 1));
        return m;
    }

    /// <summary>Als Teil für den Blender-Render (Positionen in Zentimetern). Gleiche Punkte werden verschmolzen, damit das Netz in Blender zusammenhängt.</summary>
    public Part3D ToPart(string material, string color = "#B4B8BD")
    {
        var mesh = ToMesh((180, 184, 189));
        var seen = new Dictionary<(float, float, float), int>(); var pos = new List<float>(); var idx = new int[mesh.I.Count];
        for (var k = 0; k < idx.Length; k++)
        {
            var v = mesh.V[mesh.I[k]]; var key = ((float)v.X, (float)v.Y, (float)v.Z);
            if (!seen.TryGetValue(key, out var n)) { n = seen.Count; seen[key] = n; pos.AddRange([key.Item1, key.Item2, key.Item3]); }
            idx[k] = n;
        }
        return new Part3D { Name = "Werkstück", Shape = "mesh", Material = material, Color = color, Positions = [.. pos], Indices = idx };
    }
}
