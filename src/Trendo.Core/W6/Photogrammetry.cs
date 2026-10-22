using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>SPEC-09: Fotos oder ein Video zu einem Dreiecksnetz. Startet selbst kein Programm.</summary>
public static class PhotoScan
{
    public const int MinPhotos = 3;
    static readonly string[] ImageExt = [".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp"];

    public static IReadOnlyList<string> ListPhotos(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            throw new ArgumentException("Diesen Ordner gibt es nicht.");
        var files = Directory.EnumerateFiles(folder)
            .Where(f => ImageExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count < MinPhotos)
            throw new ArgumentException($"Ich brauche mindestens {MinPhotos} Fotos von dem Gegenstand. Im Ordner sind {files.Count}.");
        return files;
    }

    public static string[] FrameArgs(string video, string outDir, int everySeconds)
    {
        if (string.IsNullOrWhiteSpace(video)) throw new ArgumentException("Bitte ein Video angeben.");
        if (everySeconds < 1 || everySeconds > 30) throw new ArgumentException("Bitte eine ganze Zahl von 1 bis 30 Sekunden.");
        return ["-hide_banner", "-loglevel", "error", "-y", "-i", video, "-vf", "fps=1/" + everySeconds.ToString(CultureInfo.InvariantCulture), Path.Combine(outDir, "bild-%04d.jpg")];
    }

    public static string[] MeshroomArgs(string inputDir, string outputDir) => ["--input", inputDir, "--output", outputDir];

    public static IReadOnlyList<string[]> ColmapSteps(string imageDir, string workDir)
    {
        var db = Path.Combine(workDir, "datenbank.db");
        var sparse = Path.Combine(workDir, "sparse");
        var ply = Path.Combine(workDir, "punkte.ply");
        var mesh = Path.Combine(workDir, "netz.ply");
        return
        [
            ["feature_extractor", "--database_path", db, "--image_path", imageDir],
            ["exhaustive_matcher", "--database_path", db],
            ["mapper", "--database_path", db, "--image_path", imageDir, "--output_path", sparse],
            ["model_converter", "--input_path", Path.Combine(sparse, "0"), "--output_path", ply, "--output_type", "PLY"],
            ["poisson_mesher", "--input_path", ply, "--output_path", mesh],
        ];
    }

    /// <summary>Sucht ein OBJ mit Flächen, sonst ein PLY-Netz, sonst die äußere Hülle der Punkte.</summary>
    public static (string Path, bool Hull) BuildObj(string workDir)
    {
        if (!Directory.Exists(workDir)) throw new InvalidOperationException("Es ist kein Modell entstanden.");
        var obj = Directory.EnumerateFiles(workDir, "*.obj", SearchOption.AllDirectories)
            .OrderByDescending(p => p.Contains("texturedMesh", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(HasFaces);
        if (obj is not null) return (obj, false);
        foreach (var ply in Directory.EnumerateFiles(workDir, "*.ply", SearchOption.AllDirectories))
        {
            var (v, f) = ReadPly(ply);
            if (f.Count == 0) continue;
            var mesh = Path.Combine(workDir, "modell.obj");
            SaveObj(mesh, v, f);
            return (mesh, false);
        }
        var points = new List<Vec3>();
        foreach (var ply in Directory.EnumerateFiles(workDir, "*.ply", SearchOption.AllDirectories))
            points.AddRange(ReadPly(ply).V);
        var bare = Directory.EnumerateFiles(workDir, "*.obj", SearchOption.AllDirectories).FirstOrDefault();
        if (bare is not null) points.AddRange(ReadObjPoints(bare));
        if (points.Count < 4) throw new InvalidOperationException("Es ist kein Netz entstanden, und für eine Hülle sind zu wenige Punkte da.");
        var hull = Hull(points);
        var hp = Path.Combine(workDir, "huelle.obj");
        SaveObj(hp, hull.V, hull.F);
        return (hp, true);
    }

    public static bool HasFaces(string path)
    {
        foreach (var line in File.ReadLines(path))
            if (line.TrimStart().StartsWith("f ", StringComparison.Ordinal)) return true;
        return false;
    }

    public static (List<Vec3> V, List<(int A, int B, int C)> F) ReadPly(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var end = Encoding.ASCII.GetString(bytes).IndexOf("end_header", StringComparison.Ordinal);
        if (end < 0) throw new InvalidOperationException("Die PLY-Datei hat keinen Kopf.");
        var nl = bytes[end..].AsSpan().IndexOf((byte)'\n');
        if (nl < 0) throw new InvalidOperationException("Die PLY-Datei hat keinen Kopf.");
        var body = end + nl + 1;
        var header = Encoding.ASCII.GetString(bytes, 0, body);
        var lines = header.Split('\n').Select(l => l.Trim('\r').Trim()).Where(l => l.Length > 0).ToList();
        var binary = lines.Any(l => l.StartsWith("format binary_little_endian", StringComparison.OrdinalIgnoreCase));
        var ascii = lines.Any(l => l.StartsWith("format ascii", StringComparison.OrdinalIgnoreCase));
        if (!binary && !ascii) throw new InvalidOperationException("Dieses PLY-Format lese ich nicht.");
        var verts = 0; var faces = 0; var vProps = new List<(string Name, string Type)>(); var faceList = false; var section = "";
        foreach (var line in lines)
        {
            var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 3 && p[0] == "element" && p[1] == "vertex" && int.TryParse(p[2], out var n)) { section = "v"; verts = n; }
            else if (p.Length >= 3 && p[0] == "element" && p[1] == "face" && int.TryParse(p[2], out var m)) { section = "f"; faces = m; }
            else if (p.Length >= 3 && p[0] == "element") section = "";
            else if (section == "v" && p.Length >= 3 && p[0] == "property" && p[1] != "list") vProps.Add((p[^1], p[1]));
            else if (section == "f" && p.Length >= 5 && p[0] == "property" && p[1] == "list") faceList = true;
        }
        var v = new List<Vec3>(verts);
        var f = new List<(int, int, int)>();
        if (ascii)
        {
            var data = Encoding.ASCII.GetString(bytes, body, bytes.Length - body).Split('\n').Select(l => l.Trim('\r').Trim()).Where(l => l.Length > 0).ToList();
            var row = 0;
            for (var i = 0; i < verts && row < data.Count; i++, row++)
            {
                var c = data[row].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                double x = 0, y = 0, z = 0; var k = 0;
                foreach (var prop in vProps)
                {
                    if (k >= c.Length) break;
                    var num = D(c[k++]);
                    if (prop.Name == "x") x = num; else if (prop.Name == "y") y = num; else if (prop.Name == "z") z = num;
                }
                v.Add(new(x, y, z));
            }
            if (faceList)
                for (var i = 0; i < faces && row < data.Count; i++, row++)
                    AddFace(f, data[row].Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray());
        }
        else
        {
            var o = body;
            for (var i = 0; i < verts; i++)
            {
                double x = 0, y = 0, z = 0;
                foreach (var prop in vProps)
                {
                    var (num, n) = ReadNum(bytes, o, prop.Type); o += n;
                    if (prop.Name == "x") x = num; else if (prop.Name == "y") y = num; else if (prop.Name == "z") z = num;
                }
                v.Add(new(x, y, z));
            }
            if (faceList)
                for (var i = 0; i < faces; i++)
                {
                    if (o >= bytes.Length) break;
                    var count = bytes[o++];
                    var idx = new int[count];
                    for (var k = 0; k < count; k++) { idx[k] = BitConverter.ToInt32(bytes, o); o += 4; }
                    AddFace(f, idx);
                }
        }
        return (v, f);
    }

    public static void SaveObj(string path, IReadOnlyList<Vec3> v, IReadOnlyList<(int A, int B, int C)> f)
    {
        var sb = new StringBuilder();
        foreach (var p in v) sb.Append("v ").Append(F(p.X)).Append(' ').Append(F(p.Y)).Append(' ').Append(F(p.Z)).Append('\n');
        foreach (var t in f) sb.Append("f ").Append(t.A + 1).Append(' ').Append(t.B + 1).Append(' ').Append(t.C + 1).Append('\n');
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Äußere Hülle. Liegen alle Punkte in einer Ebene, wird daraus ein Fächer.</summary>
    public static (List<Vec3> V, List<(int A, int B, int C)> F) Hull(IReadOnlyList<Vec3> src)
    {
        var pts = Unique(src);
        if (pts.Count < 3) throw new ArgumentException("Zu wenige Punkte für eine Hülle.");
        var scale = 1d;
        foreach (var p in pts) scale = Math.Max(scale, Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z))));
        var eps = Math.Max(1e-8, scale * 1e-8);
        if (TryFan(pts, eps, out var fan)) return fan;
        return Wrap(pts, eps);
    }

    static void AddFace(List<(int, int, int)> f, int[] idx)
    {
        if (idx.Length < 3) return;
        for (var i = 1; i < idx.Length - 1; i++) f.Add((idx[0], idx[i], idx[i + 1]));
    }

    static List<Vec3> ReadObjPoints(string path)
    {
        var v = new List<Vec3>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (!line.StartsWith("v ", StringComparison.Ordinal)) continue;
            var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 4) v.Add(new(D(p[1]), D(p[2]), D(p[3])));
        }
        return v;
    }

    static (double Num, int Bytes) ReadNum(byte[] b, int o, string type) => type switch
    {
        "double" or "float64" => (BitConverter.ToDouble(b, o), 8),
        "float" or "float32" => (BitConverter.ToSingle(b, o), 4),
        "int" or "int32" => (BitConverter.ToInt32(b, o), 4),
        "uint" or "uint32" => (BitConverter.ToUInt32(b, o), 4),
        "short" or "int16" => (BitConverter.ToInt16(b, o), 2),
        "ushort" or "uint16" => (BitConverter.ToUInt16(b, o), 2),
        "char" or "uchar" or "uint8" or "int8" => (b[o], 1),
        _ => throw new InvalidOperationException("PLY-Eigenschaft „" + type + "“ kenne ich nicht."),
    };

    static List<Vec3> Unique(IReadOnlyList<Vec3> src)
    {
        var list = new List<Vec3>();
        foreach (var p in src)
            if (!list.Any(q => (p - q).Len < 1e-7)) list.Add(p);
        return list;
    }

    static bool TryFan(List<Vec3> pts, double eps, out (List<Vec3> V, List<(int A, int B, int C)> F) fan)
    {
        fan = ([], []);
        var i0 = 0; var i1 = 0; var best = -1d;
        for (var i = 0; i < pts.Count; i++)
            for (var j = i + 1; j < pts.Count; j++)
            {
                var d = (pts[j] - pts[i]).Len;
                if (d > best) { best = d; i0 = i; i1 = j; }
            }
        if (best < eps) return false;
        var line = (pts[i1] - pts[i0]).Norm();
        var i2 = 0; var off = -1d;
        for (var i = 0; i < pts.Count; i++)
        {
            var d = Vec3.Cross(pts[i] - pts[i0], line).Len;
            if (d > off) { off = d; i2 = i; }
        }
        if (off < eps) throw new ArgumentException("Die Punkte liegen auf einer Linie.");
        var normal = Vec3.Cross(pts[i1] - pts[i0], pts[i2] - pts[i0]).Norm();
        foreach (var p in pts)
            if (Math.Abs(Vec3.Dot(p - pts[i0], normal)) > eps * 10) return false;
        var u = (pts[i1] - pts[i0]).Norm();
        var v = Vec3.Cross(normal, u);
        var flat = pts.Select(p => (U: Vec3.Dot(p - pts[i0], u), V: Vec3.Dot(p - pts[i0], v), P: p)).ToList();
        flat.Sort((a, b) => a.U == b.U ? a.V.CompareTo(b.V) : a.U.CompareTo(b.U));
        double Turn((double U, double V, Vec3 P) o, (double U, double V, Vec3 P) a, (double U, double V, Vec3 P) b) =>
            (a.U - o.U) * (b.V - o.V) - (a.V - o.V) * (b.U - o.U);
        var lower = new List<(double U, double V, Vec3 P)>();
        foreach (var p in flat)
        {
            while (lower.Count >= 2 && Turn(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }
        var upper = new List<(double U, double V, Vec3 P)>();
        for (var i = flat.Count - 1; i >= 0; i--)
        {
            var p = flat[i];
            while (upper.Count >= 2 && Turn(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }
        lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1);
        var ring = lower.Concat(upper).Select(p => p.P).ToList();
        if (ring.Count < 3) throw new ArgumentException("Die Punkte ergeben keine Fläche.");
        var faces = new List<(int, int, int)>();
        for (var i = 1; i < ring.Count - 1; i++) faces.Add((0, i, i + 1));
        fan = (ring, faces);
        return true;
    }

    static double Vol(IReadOnlyList<Vec3> p, int a, int b, int c, int d) =>
        Vec3.Dot(Vec3.Cross(p[b] - p[a], p[c] - p[a]), p[d] - p[a]);

    /// <summary>Jede Hüllenkante bekommt die ganze Fläche, auch wenn mehr als drei Punkte in einer Ebene liegen.</summary>
    static (List<Vec3> V, List<(int A, int B, int C)> F) Wrap(List<Vec3> pts, double eps)
    {
        var n = pts.Count;
        var a0 = 0;
        for (var i = 1; i < n; i++)
            if (pts[i].X < pts[a0].X - eps || (Math.Abs(pts[i].X - pts[a0].X) <= eps && (pts[i].Y < pts[a0].Y || (Math.Abs(pts[i].Y - pts[a0].Y) <= eps && pts[i].Z < pts[a0].Z)))) a0 = i;
        List<int>? start = null;
        for (var i = 0; i < n && start is null; i++)
            if (i != a0) start = Polygon(pts, a0, i, eps);
        if (start is null || start.Count < 3) throw new InvalidOperationException("Die Hülle hat keine Fläche.");
        var faces = new List<(int A, int B, int C)>();
        var used = new HashSet<(int, int)>();
        var stack = new Stack<(int A, int B)>();
        void AddPolygon(List<int> ring)
        {
            for (var i = 1; i < ring.Count - 1; i++) faces.Add((ring[0], ring[i], ring[i + 1]));
            for (var i = 0; i < ring.Count; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Count];
                used.Add((a, b));
                if (!used.Contains((b, a))) stack.Push((b, a));
            }
            for (var i = 2; i < ring.Count - 1; i++)
            {
                used.Add((ring[0], ring[i]));
                used.Add((ring[i], ring[0]));
            }
        }
        AddPolygon(start);
        var guard = 0;
        while (stack.Count > 0 && guard++ < n * n)
        {
            var (a, b) = stack.Pop();
            if (used.Contains((a, b))) continue;
            var ring = Polygon(pts, a, b, eps);
            if (ring is null) { used.Add((a, b)); continue; }
            AddPolygon(ring);
        }
        if (faces.Count == 0) throw new InvalidOperationException("Die Hülle hat keine Fläche.");
        var keep = faces.SelectMany(f => new[] { f.A, f.B, f.C }).Distinct().OrderBy(i => i).ToList();
        var map = keep.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return (keep.Select(i => pts[i]).ToList(), faces.Select(f => (map[f.A], map[f.B], map[f.C])).ToList());
    }

    /// <summary>Fläche durch die Kante a→b: alle Punkte in dieser Ebene, gegen den Uhrzeigersinn von außen.</summary>
    static List<int>? Polygon(List<Vec3> pts, int a, int b, double eps)
    {
        var c = -1;
        var n = pts.Count;
        for (var guard = 0; guard < n; guard++)
        {
            var moved = false;
            for (var j = 0; j < n; j++)
            {
                if (j == a || j == b) continue;
                if (c < 0) { c = j; moved = true; continue; }
                if (j == c) continue;
                if (Vol(pts, a, b, c, j) > eps) { c = j; moved = true; }
            }
            if (!moved) break;
        }
        if (c < 0) return null;
        var normal = Vec3.Cross(pts[b] - pts[a], pts[c] - pts[a]);
        if (normal.Len < eps) return null;
        normal = normal.Norm();
        var pos = 0; var neg = 0;
        foreach (var p in pts)
        {
            var s = Vec3.Dot(normal, p - pts[a]);
            if (s > eps) pos++; else if (s < -eps) neg++;
        }
        if (pos > 0 && neg > 0) return null;
        if (pos > neg) normal = new Vec3(-normal.X, -normal.Y, -normal.Z);
        var on = new List<int>();
        for (var i = 0; i < n; i++)
            if (Math.Abs(Vec3.Dot(normal, pts[i] - pts[a])) <= eps * 10) on.Add(i);
        if (on.Count < 3) return null;
        var tangent = Math.Abs(normal.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
        var x = Vec3.Cross(normal, tangent).Norm();
        var y = Vec3.Cross(normal, x);
        var origin = pts[a];
        var flat = on.Select(i => (U: Vec3.Dot(pts[i] - origin, x), V: Vec3.Dot(pts[i] - origin, y), I: i)).ToList();
        flat.Sort((p, q) => p.U == q.U ? p.V.CompareTo(q.V) : p.U.CompareTo(q.U));
        double Turn((double U, double V, int I) o, (double U, double V, int I) p, (double U, double V, int I) q) =>
            (p.U - o.U) * (q.V - o.V) - (p.V - o.V) * (q.U - o.U);
        var lower = new List<(double U, double V, int I)>();
        foreach (var p in flat)
        {
            while (lower.Count >= 2 && Turn(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }
        var upper = new List<(double U, double V, int I)>();
        for (var i = flat.Count - 1; i >= 0; i--)
        {
            while (upper.Count >= 2 && Turn(upper[^2], upper[^1], flat[i]) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(flat[i]);
        }
        lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1);
        var ring = lower.Concat(upper).Select(p => p.I).ToList();
        return ring.Count >= 3 ? ring : null;
    }

    static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}
