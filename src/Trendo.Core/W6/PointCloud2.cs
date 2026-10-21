using System.Globalization;
using System.Text;

namespace Trendo.Core;

public sealed record Plane(double A, double B, double C, double D, int Inliers, double TiltDeg)
{
    public double Dist(P3 p) => Math.Abs(A * p.X + B * p.Y + C * p.Z + D);
}
public sealed record CloudDiff(double Mean, double Max, double Rms, int Points);

/// <summary>SPEC-09 Ausbau: Punktwolken umformen, Boden finden, vergleichen, messen.</summary>
public static class ScanPlus
{
    private static PointCloud Map(PointCloud pc, Func<P3, P3> f, Func<int, bool>? keep = null)
    {
        var r = new PointCloud(); var col = pc.Colors is null ? null : new List<(byte, byte, byte)>();
        for (var i = 0; i < pc.Points.Count; i++) { if (keep is not null && !keep(i)) continue; r.Points.Add(f(pc.Points[i])); col?.Add(pc.Colors![i]); }
        SetColors(r, col); return r;
    }
    private static void SetColors(PointCloud pc, List<(byte R, byte G, byte B)>? c) => pc.Colors = c;

    public static PointCloud Scale(PointCloud pc, double f) { if (f <= 0 || double.IsNaN(f)) throw new ArgumentException("Der Faktor muss größer als 0 sein."); return Map(pc, p => new(p.X * f, p.Y * f, p.Z * f)); }
    public static double UnitFactor(string from, string to) { static double M(string u) => u.Trim().ToLowerInvariant() switch { "mm" => 0.001, "cm" => 0.01, "m" => 1, "in" or "zoll" => 0.0254, "ft" => 0.3048, _ => throw new ArgumentException($"Unbekannte Einheit „{u}“ (mm, cm, m, in, ft).") }; return M(from) / M(to); }

    public static PointCloud Center(PointCloud pc) { var i = pc.Info(); return Map(pc, p => new(p.X - (i.Min.X + i.Max.X) / 2, p.Y - (i.Min.Y + i.Max.Y) / 2, p.Z - i.Min.Z)); }

    public static PointCloud Crop(PointCloud pc, P3 min, P3 max) => Map(pc, p => p, i => { var p = pc.Points[i]; return p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y && p.Z >= min.Z && p.Z <= max.Z; });

    /// <summary>RANSAC-Ebene; bevorzugt waagerechte Ebenen (Normale nahe z).</summary>
    public static Plane FindFloor(PointCloud pc, double tol, int iterations = 300, int seed = 1, double maxTiltDeg = 30)
    {
        var pts = pc.Points; if (pts.Count < 3) throw new InvalidOperationException("Mindestens 3 Punkte nötig.");
        var rnd = new Random(seed); Plane? best = null;
        for (var it = 0; it < iterations; it++)
        {
            P3 a = pts[rnd.Next(pts.Count)], b = pts[rnd.Next(pts.Count)], c = pts[rnd.Next(pts.Count)];
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-12) continue; nx /= len; ny /= len; nz /= len; if (nz < 0) { nx = -nx; ny = -ny; nz = -nz; }
            var tilt = Math.Acos(Math.Min(1, nz)) * 180 / Math.PI; if (tilt > maxTiltDeg) continue;
            var d = -(nx * a.X + ny * a.Y + nz * a.Z); var n = 0;
            foreach (var p in pts) if (Math.Abs(nx * p.X + ny * p.Y + nz * p.Z + d) <= tol) n++;
            if (best is null || n > best.Inliers) best = new(nx, ny, nz, d, n, Math.Round(tilt, 2));
        }
        return best ?? throw new InvalidOperationException("Keine waagerechte Fläche gefunden.");
    }

    public static PointCloud RemovePlane(PointCloud pc, Plane pl, double tol) => Map(pc, p => p, i => pl.Dist(pc.Points[i]) > tol);

    /// <summary>Abstand jedes Punkts von <paramref name="a"/> zum nächsten Punkt in <paramref name="b"/> (Gitter-Suche).</summary>
    public static CloudDiff Compare(PointCloud a, PointCloud b)
    {
        var ib = b.Info(); var cell = Math.Max(1e-9, Math.Max(ib.SizeX, Math.Max(ib.SizeY, ib.SizeZ)) / Math.Max(1, Math.Cbrt(b.Points.Count)));
        var grid = b.Points.GroupBy(p => K(p, cell)).ToDictionary(g => g.Key, g => g.ToList());
        double sum = 0, sq = 0, max = 0;
        foreach (var p in a.Points)
        {
            var best = double.MaxValue; var (x, y, z) = K(p, cell);
            for (var r = 1; ; r++)
            {
                for (var dx = -r; dx <= r; dx++) for (var dy = -r; dy <= r; dy++) for (var dz = -r; dz <= r; dz++)
                    if (grid.TryGetValue((x + dx, y + dy, z + dz), out var l)) foreach (var q in l) best = Math.Min(best, D2(p, q));
                if (best <= (r * cell) * (r * cell) || r > 1000) break;
            }
            var d = Math.Sqrt(best); sum += d; sq += d * d; max = Math.Max(max, d);
        }
        var n = a.Points.Count; return new(sum / n, max, Math.Sqrt(sq / n), n);
    }
    private static (long, long, long) K(P3 p, double s) => ((long)Math.Floor(p.X / s), (long)Math.Floor(p.Y / s), (long)Math.Floor(p.Z / s));
    private static double D2(P3 p, P3 q) => (p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y) + (p.Z - q.Z) * (p.Z - q.Z);

    /// <summary>Volumen über z = <paramref name="floorZ"/>: Höhenfeld mit Rasterzellen (höchster Punkt je Zelle).</summary>
    public static double Volume(PointCloud pc, double cell, double floorZ = 0)
    {
        if (cell <= 0) throw new ArgumentException("Die Rastergröße muss größer als 0 sein.");
        return pc.Points.GroupBy(p => ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell))).Sum(g => Math.Max(0, g.Max(p => p.Z) - floorZ)) * cell * cell;
    }

    public static string ToXyz(PointCloud pc)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < pc.Points.Count; i++) { var p = pc.Points[i]; sb.Append(FormattableString.Invariant($"{p.X:0.######} {p.Y:0.######} {p.Z:0.######}")); if (pc.Colors is { } c) sb.Append($" {c[i].R} {c[i].G} {c[i].B}"); sb.Append('\n'); }
        return sb.ToString();
    }

    /// <summary>Mittlerer Abstand zum nächsten Nachbarn.</summary>
    public static double Density(PointCloud pc)
    {
        if (pc.Points.Count < 2) throw new InvalidOperationException("Mindestens 2 Punkte nötig.");
        var total = 0.0;
        var i0 = pc.Info(); var cell = Math.Max(1e-9, Math.Max(i0.SizeX, Math.Max(i0.SizeY, i0.SizeZ)) / Math.Max(1, Math.Cbrt(pc.Points.Count)));
        var grid = pc.Points.Select((p, i) => (p, i)).GroupBy(x => K(x.p, cell)).ToDictionary(g => g.Key, g => g.ToList());
        for (var i = 0; i < pc.Points.Count; i++)
        {
            var p = pc.Points[i]; var best = double.MaxValue; var (x, y, z) = K(p, cell);
            for (var r = 1; ; r++)
            {
                for (var dx = -r; dx <= r; dx++) for (var dy = -r; dy <= r; dy++) for (var dz = -r; dz <= r; dz++)
                    if (grid.TryGetValue((x + dx, y + dy, z + dz), out var l)) foreach (var (q, j) in l) if (j != i) best = Math.Min(best, D2(p, q));
                if (best <= (r * cell) * (r * cell) || r > 1000) break;
            }
            total += Math.Sqrt(best);
        }
        return total / pc.Points.Count;
    }

    public static PointCloud RotateZ(PointCloud pc, double deg) { var a = deg * Math.PI / 180; double c = Math.Cos(a), s = Math.Sin(a); return Map(pc, p => new(Math.Round(p.X * c - p.Y * s, 12), Math.Round(p.X * s + p.Y * c, 12), p.Z)); }

    public static PointCloud Merge(PointCloud a, PointCloud b)
    {
        var r = new PointCloud(); r.Points.AddRange(a.Points); r.Points.AddRange(b.Points);
        SetColors(r, a.Colors is not null && b.Colors is not null ? [.. a.Colors, .. b.Colors] : null); return r;
    }
}
