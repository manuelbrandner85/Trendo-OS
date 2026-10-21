using System.Globalization;
using System.Text;

namespace Trendo.Core;

public readonly record struct P3(double X, double Y, double Z);
public sealed record CloudInfo(int Points, P3 Min, P3 Max, P3 Center, double SizeX, double SizeY, double SizeZ, bool HasColor);

/// <summary>SPEC-09: 3D-Scan – Punktwolken (PLY ASCII/binär, XYZ, OBJ) lesen, ausdünnen, Ausreißer entfernen, als PLY speichern.</summary>
public sealed class PointCloud
{
    public List<P3> Points { get; } = [];
    public List<(byte R, byte G, byte B)>? Colors { get; internal set; }

    public static PointCloud Load(string path)
    {
        if (!File.Exists(path)) throw new IOException("Diese Datei gibt es nicht.");
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var pc = ext switch { ".ply" => ReadPly(File.ReadAllBytes(path)), ".obj" => ReadText(path, obj: true), ".xyz" or ".txt" or ".pts" or ".csv" => ReadText(path, obj: false), _ => throw new InvalidOperationException("Unterstützt: .ply, .xyz, .txt, .pts, .csv, .obj") };
        if (pc.Points.Count == 0) throw new InvalidOperationException("In der Datei sind keine Punkte.");
        return pc;
    }

    private static PointCloud ReadText(string path, bool obj)
    {
        var pc = new PointCloud(); var col = new List<(byte, byte, byte)>(); var allCol = true;
        foreach (var raw in File.ReadLines(path))
        {
            var l = raw.Trim(); if (l == "" || l[0] is '#' or '/') continue;
            if (obj) { if (!l.StartsWith("v ")) continue; l = l[2..]; }
            var t = l.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 3 || !D(t[0], out var x) || !D(t[1], out var y) || !D(t[2], out var z)) continue;
            pc.Points.Add(new(x, y, z));
            if (!obj && t.Length >= 6 && byte.TryParse(t[3], out var r) && byte.TryParse(t[4], out var g) && byte.TryParse(t[5], out var b)) col.Add((r, g, b)); else allCol = false;
        }
        if (allCol && col.Count == pc.Points.Count && col.Count > 0) pc.Colors = col;
        return pc;
    }

    private static bool D(string s, out double v) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static PointCloud ReadPly(byte[] b)
    {
        var headerEnd = IndexOf(b, "end_header"); if (headerEnd < 0) throw new InvalidOperationException("Die PLY-Datei hat keinen gültigen Kopf.");
        var bodyStart = headerEnd + "end_header".Length; while (bodyStart < b.Length && b[bodyStart] is (byte)'\r' or (byte)'\n') { bodyStart++; if (b[bodyStart - 1] == '\n') break; }
        var header = Encoding.ASCII.GetString(b, 0, headerEnd).Split('\n').Select(s => s.Trim()).ToList();
        var format = header.FirstOrDefault(h => h.StartsWith("format "))?.Split(' ')[1] ?? throw new InvalidOperationException("PLY ohne Formatangabe.");
        if (format == "binary_big_endian") throw new InvalidOperationException("PLY im Big-Endian-Format wird nicht unterstützt.");
        int count = 0; var props = new List<(string Type, string Name)>(); var inVertex = false; long skipAfter = 0;
        foreach (var h in header)
        {
            var t = h.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (t.Length == 0) continue;
            if (t[0] == "element") { inVertex = t[1] == "vertex"; if (inVertex) count = int.Parse(t[2], CultureInfo.InvariantCulture); else skipAfter++; }
            else if (t[0] == "property" && inVertex) { if (t[1] == "list") throw new InvalidOperationException("Unerwartete Liste bei den Punkten."); props.Add((t[1], t[2])); }
        }
        int ix = props.FindIndex(p => p.Name == "x"), iy = props.FindIndex(p => p.Name == "y"), iz = props.FindIndex(p => p.Name == "z");
        int ir = props.FindIndex(p => p.Name is "red" or "r"), ig = props.FindIndex(p => p.Name is "green" or "g"), ibl = props.FindIndex(p => p.Name is "blue" or "b");
        if (ix < 0 || iy < 0 || iz < 0) throw new InvalidOperationException("Die PLY-Datei hat keine x/y/z-Werte.");
        var pc = new PointCloud(); var hasCol = ir >= 0 && ig >= 0 && ibl >= 0; var col = new List<(byte, byte, byte)>();
        if (format == "ascii")
        {
            var lines = Encoding.ASCII.GetString(b, bodyStart, b.Length - bodyStart).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines.Take(count))
            {
                var t = l.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (t.Length < props.Count) continue;
                pc.Points.Add(new(double.Parse(t[ix], CultureInfo.InvariantCulture), double.Parse(t[iy], CultureInfo.InvariantCulture), double.Parse(t[iz], CultureInfo.InvariantCulture)));
                if (hasCol) col.Add((Clamp(t[ir]), Clamp(t[ig]), Clamp(t[ibl])));
            }
        }
        else
        {
            var sizes = props.Select(p => Size(p.Type)).ToArray(); var stride = sizes.Sum(); var off = new int[sizes.Length]; for (var i = 1; i < off.Length; i++) off[i] = off[i - 1] + sizes[i - 1];
            if (bodyStart + (long)stride * count > b.Length) throw new InvalidOperationException("Die PLY-Datei ist unvollständig.");
            double Val(int row, int p) { var o = bodyStart + row * stride + off[p]; return props[p].Type switch { "float" or "float32" => BitConverter.ToSingle(b, o), "double" or "float64" => BitConverter.ToDouble(b, o), "uchar" or "uint8" => b[o], "char" or "int8" => (sbyte)b[o], "short" or "int16" => BitConverter.ToInt16(b, o), "ushort" or "uint16" => BitConverter.ToUInt16(b, o), "int" or "int32" => BitConverter.ToInt32(b, o), _ => BitConverter.ToUInt32(b, o) }; }
            for (var i = 0; i < count; i++) { pc.Points.Add(new(Val(i, ix), Val(i, iy), Val(i, iz))); if (hasCol) col.Add(((byte)Val(i, ir), (byte)Val(i, ig), (byte)Val(i, ibl))); }
        }
        if (hasCol) pc.Colors = col;
        return pc;
    }

    private static byte Clamp(string s) => (byte)Math.Clamp(double.Parse(s, CultureInfo.InvariantCulture), 0, 255);
    private static int Size(string t) => t switch { "char" or "uchar" or "int8" or "uint8" => 1, "short" or "ushort" or "int16" or "uint16" => 2, "int" or "uint" or "float" or "int32" or "uint32" or "float32" => 4, "double" or "float64" => 8, _ => throw new InvalidOperationException($"Unbekannter PLY-Typ {t}.") };
    private static int IndexOf(byte[] b, string s) { var p = Encoding.ASCII.GetBytes(s); for (var i = 0; i + p.Length <= Math.Min(b.Length, 65536); i++) if (b.AsSpan(i, p.Length).SequenceEqual(p)) return i; return -1; }

    public CloudInfo Info()
    {
        var min = new P3(Points.Min(p => p.X), Points.Min(p => p.Y), Points.Min(p => p.Z)); var max = new P3(Points.Max(p => p.X), Points.Max(p => p.Y), Points.Max(p => p.Z));
        return new(Points.Count, min, max, new(Points.Average(p => p.X), Points.Average(p => p.Y), Points.Average(p => p.Z)), max.X - min.X, max.Y - min.Y, max.Z - min.Z, Colors is not null);
    }

    private static (long, long, long) Key(P3 p, double s) => ((long)Math.Floor(p.X / s), (long)Math.Floor(p.Y / s), (long)Math.Floor(p.Z / s));

    /// <summary>Ausdünnen: ein gemittelter Punkt pro Würfel mit Kantenlänge <paramref name="voxel"/>.</summary>
    public PointCloud Downsample(double voxel)
    {
        if (voxel <= 0) throw new ArgumentException("Die Würfelgröße muss größer als 0 sein.");
        var res = new PointCloud(); var col = Colors is null ? null : new List<(byte, byte, byte)>();
        foreach (var g in Points.Select((p, i) => (p, i)).GroupBy(x => Key(x.p, voxel)))
        {
            res.Points.Add(new(g.Average(x => x.p.X), g.Average(x => x.p.Y), g.Average(x => x.p.Z)));
            if (col is not null) col.Add(((byte)g.Average(x => Colors![x.i].R), (byte)g.Average(x => Colors![x.i].G), (byte)g.Average(x => Colors![x.i].B)));
        }
        res.Colors = col; return res;
    }

    /// <summary>Ausreißer entfernen: Punkte mit weniger als <paramref name="minNeighbors"/> Nachbarn im Umkreis <paramref name="radius"/>.</summary>
    public PointCloud RemoveOutliers(double radius, int minNeighbors = 3)
    {
        if (radius <= 0) throw new ArgumentException("Der Umkreis muss größer als 0 sein.");
        var grid = Points.Select((p, i) => (p, i)).GroupBy(x => Key(x.p, radius)).ToDictionary(g => g.Key, g => g.ToList());
        var res = new PointCloud(); var col = Colors is null ? null : new List<(byte, byte, byte)>(); var r2 = radius * radius;
        for (var i = 0; i < Points.Count; i++)
        {
            var p = Points[i]; var (kx, ky, kz) = Key(p, radius); var n = 0;
            for (var dx = -1; dx <= 1 && n < minNeighbors; dx++) for (var dy = -1; dy <= 1 && n < minNeighbors; dy++) for (var dz = -1; dz <= 1 && n < minNeighbors; dz++)
                if (grid.TryGetValue((kx + dx, ky + dy, kz + dz), out var cell)) foreach (var (q, j) in cell) if (j != i && (q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y) + (q.Z - p.Z) * (q.Z - p.Z) <= r2 && ++n >= minNeighbors) break;
            if (n >= minNeighbors) { res.Points.Add(p); col?.Add(Colors![i]); }
        }
        res.Colors = col; return res;
    }

    public void SavePly(string path)
    {
        var sb = new StringBuilder($"ply\nformat ascii 1.0\ncomment Trendo OS\nelement vertex {Points.Count}\nproperty float x\nproperty float y\nproperty float z\n");
        if (Colors is not null) sb.Append("property uchar red\nproperty uchar green\nproperty uchar blue\n");
        sb.Append("end_header\n");
        for (var i = 0; i < Points.Count; i++)
        {
            var p = Points[i]; sb.Append(FormattableString.Invariant($"{p.X:0.######} {p.Y:0.######} {p.Z:0.######}"));
            if (Colors is not null) sb.Append($" {Colors[i].R} {Colors[i].G} {Colors[i].B}");
            sb.Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }
}
