using System.Globalization;
using System.Text;

namespace Trendo.Core;

public enum PhotoEngine { Colmap, Meshroom }

public sealed record PhotoRequest(string WorkDir, string? Photos, string? Video, PhotoEngine Engine);

public sealed record PhotoStep(string Title, string File, string Args, string? WorkDir = null, bool NeedsModel = false, string? ImagesAfter = null);

public sealed record PhotoResult(string ObjPath, int Triangles, bool OuterShell, string Note);

/// <summary>SPEC-09: Fotos oder ein Video werden mit COLMAP oder Meshroom zu einem Netz für das 3D-Studio.</summary>
public static class PhotoScan
{
    public const string ModelMark = "{model}";
    public static readonly string[] ImageExt = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".webp"];
    public static readonly string[] VideoExt = [".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v"];

    public static string TooFew(int n) => n == 1
        ? "In dem Ordner liegt erst 1 Foto. Für ein Modell brauchst du mindestens 3, aus verschiedenen Richtungen."
        : $"In dem Ordner liegen erst {n} Fotos. Für ein Modell brauchst du mindestens 3, aus verschiedenen Richtungen.";

    public static IReadOnlyList<string> ListImages(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) throw new IOException("Diesen Foto-Ordner gibt es nicht.");
        return Directory.EnumerateFiles(folder)
            .Where(f => ImageExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string Q(string path) => path.IndexOfAny([' ', '\t', '"']) >= 0 ? "\"" + path.Replace("\"", "") + "\"" : path;

    public static IReadOnlyList<PhotoStep> Steps(PhotoRequest req, string tool, string? ffmpeg)
    {
        if (string.IsNullOrWhiteSpace(req.WorkDir)) throw new ArgumentException("Kein Arbeitsordner.");
        if (string.IsNullOrWhiteSpace(tool)) throw new ArgumentException("Kein Werkzeug.");
        var video = Blank(req.Video);
        var photos = Blank(req.Photos);
        var steps = new List<PhotoStep>();
        string images;
        if (video is not null)
        {
            if (!File.Exists(video)) throw new IOException("Dieses Video gibt es nicht.");
            if (!VideoExt.Contains(Path.GetExtension(video), StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Das Video kenne ich nicht. Nimm mp4, mov, mkv, webm oder avi.");
            if (string.IsNullOrWhiteSpace(ffmpeg)) throw new InvalidOperationException("Für ein Video fehlt FFmpeg.");
            images = Path.Combine(req.WorkDir, "bilder");
            steps.Add(new("Bilder aus dem Video holen", ffmpeg, "-y -i " + Q(video) + " -vf fps=2 " + Q(Path.Combine(images, "bild_%04d.jpg")), null, false, images));
        }
        else
        {
            if (photos is null) throw new InvalidOperationException("Wähle einen Ordner mit mindestens 3 Fotos oder ein Video.");
            var list = ListImages(photos);
            if (list.Count < 3) throw new InvalidOperationException(TooFew(list.Count));
            images = photos;
        }

        if (req.Engine == PhotoEngine.Meshroom)
        {
            var home = Path.GetDirectoryName(tool);
            steps.Add(new("Modell mit Meshroom rechnen", tool, "-i " + Q(images) + " -o " + Q(Path.Combine(req.WorkDir, "meshroom")), string.IsNullOrEmpty(home) ? null : home));
            return steps;
        }

        var db = Path.Combine(req.WorkDir, "datenbank.db");
        var sparse = Path.Combine(req.WorkDir, "sparse");
        var dense = Path.Combine(req.WorkDir, "dicht");
        var match = video is null
            ? "exhaustive_matcher --database_path " + Q(db) + " --SiftMatching.use_gpu 0"
            : "sequential_matcher --database_path " + Q(db) + " --SiftMatching.use_gpu 0 --SequentialMatching.overlap 10";
        steps.Add(new("Merkmale in den Fotos suchen", tool,
            "feature_extractor --database_path " + Q(db) + " --image_path " + Q(images) + " --ImageReader.single_camera 1 --SiftExtraction.use_gpu 0 --SiftExtraction.max_image_size 1600"));
        steps.Add(new("Gleiche Stellen in den Fotos finden", tool, match));
        steps.Add(new("Kameras und die grobe Form rechnen", tool,
            "mapper --database_path " + Q(db) + " --image_path " + Q(images) + " --output_path " + Q(sparse)));
        steps.Add(new("Fotos entzerren", tool,
            "image_undistorter --image_path " + Q(images) + " --input_path " + ModelMark + " --output_path " + Q(dense) + " --output_type COLMAP --max_image_size 1600", null, true));
        steps.Add(new("Oberfläche bauen", tool,
            "delaunay_mesher --input_path " + Q(dense) + " --output_path " + Q(Path.Combine(dense, "meshed-delaunay.ply"))));
        return steps;
    }

    public static async Task<PhotoResult> RunAsync(PhotoRequest req, string tool, string? ffmpeg, IProcessRunner runner, Action<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(req.WorkDir);
        var log = new List<string>();
        foreach (var step in Steps(req, tool, ffmpeg))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Invoke(step.Title);
            var args = step.Args;
            if (step.NeedsModel)
            {
                var model = SparseModel(req.WorkDir) ?? throw new InvalidOperationException("Die Fotos haben kein gemeinsames Modell ergeben. Fotografiere den Gegenstand von allen Seiten, mit Überlappung und gleichbleibendem Licht.");
                args = args.Replace(ModelMark, Q(model));
            }
            if (step.ImagesAfter is { } dir) Directory.CreateDirectory(dir);
            var (exit, output) = await Exec(runner, step.File, args, step.WorkDir, line =>
            {
                var t = line.Trim();
                if (t.Length > 0 && t.Length <= 160) progress?.Invoke(step.Title + ": " + t);
            }, ct);
            foreach (var line in output.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).TakeLast(8)) log.Add(line);
            if (exit != 0)
            {
                var tail = log.Count == 0 ? "" : " " + string.Join(" ", log.TakeLast(3));
                throw new InvalidOperationException($"„{step.Title}“ ist fehlgeschlagen.{tail}");
            }
            if (step.ImagesAfter is { } img)
            {
                var n = ListImages(img).Count;
                if (n < 3) throw new InvalidOperationException($"Aus dem Video kamen nur {n} Bilder. Filme länger und geh um den Gegenstand herum.");
            }
        }

        var found = FindToolMesh(req.WorkDir) ?? throw new InvalidOperationException("Das Werkzeug hat kein Modell geschrieben. Versuch mehr Fotos mit Überlappung.");
        var (mesh, shell) = LoadSurface(found);
        var obj = Path.Combine(req.WorkDir, "modell.obj");
        Mesh3D.WriteObj(mesh, obj);
        var note = shell
            ? $"Fertig. {mesh.Triangles} Dreiecke, nur die äußere Hülle, weil keine Fläche in der Datei stand. Die längste Seite steht auf 20 cm."
            : $"Fertig. {mesh.Triangles} Dreiecke. Die längste Seite steht auf 20 cm, weil Fotos keine echten Maße haben.";
        progress?.Invoke(note);
        return new PhotoResult(obj, mesh.Triangles, shell, note);
    }

    public static string? SparseModel(string work)
    {
        var sparse = Path.Combine(work, "sparse");
        if (!Directory.Exists(sparse)) return null;
        string? best = null;
        long size = -1;
        foreach (var d in Directory.GetDirectories(sparse))
        {
            if (!File.Exists(Path.Combine(d, "cameras.bin")) && !File.Exists(Path.Combine(d, "cameras.txt"))) continue;
            long s = 0;
            foreach (var f in Directory.GetFiles(d)) s += new FileInfo(f).Length;
            if (s > size) { size = s; best = d; }
        }
        return best;
    }

    public static string? FindToolMesh(string workDir)
    {
        if (!Directory.Exists(workDir)) return null;
        foreach (var name in new[] { "meshed-delaunay.ply", "meshed-poisson.ply", "meshed-advancing-front.ply" })
        {
            var hit = Directory.EnumerateFiles(workDir, name, SearchOption.AllDirectories).FirstOrDefault(f => !Path.GetFileName(f).Equals("modell.obj", StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit;
        }
        var obj = Directory.EnumerateFiles(workDir, "*.obj", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("modell.obj", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
        if (obj is not null) return obj;
        return Directory.EnumerateFiles(workDir, "fused.ply", SearchOption.AllDirectories).FirstOrDefault()
            ?? Directory.EnumerateFiles(workDir, "*.ply", SearchOption.AllDirectories).FirstOrDefault();
    }

    /// <summary>Netz aus der Werkzeugdatei. Ohne Flächen wird die äußere Hülle der Punkte gebaut. Längste Seite 20 (Zentimeter im 3D-Studio).</summary>
    public static (Mesh Mesh, bool OuterShell) LoadSurface(string path)
    {
        if (!File.Exists(path)) throw new IOException("Das Modellergebnis gibt es nicht.");
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".ply")
        {
            var (mesh, faces) = ReadPly(path);
            var shell = !faces;
            var fitted = Fit(shell ? Hull(mesh) : mesh);
            return (fitted, shell);
        }
        return (Fit(Mesh3D.ReadMeshFile(path)), false);
    }

    static Task<(int Exit, string Output)> Exec(IProcessRunner run, string file, string args, string? cwd, Action<string>? line, CancellationToken ct)
        => run is RealProcessRunner real ? real.RunInAsync(file, args, cwd, line, ct) : run.RunAsync(file, args, line, ct);

    static string? Blank(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().Trim('"');
        return t.Length == 0 ? null : t;
    }

    public static Mesh Fit(Mesh mesh)
    {
        if (mesh.Triangles == 0) throw new InvalidOperationException("Das Modell hat keine Fläche.");
        mesh = Cluster(mesh, 150_000);
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var v in mesh.V)
        {
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
        }
        var longest = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
        if (longest < 1e-9) throw new InvalidOperationException("Das Modell hat keine Ausdehnung.");
        var s = 20.0 / longest;
        var c = new Vec3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        var o = new Mesh();
        for (var t = 0; t < mesh.Triangles; t++)
        {
            o.CR = mesh.R[t]; o.CG = mesh.G[t]; o.CB = mesh.B[t];
            o.Add((mesh.V[mesh.I[t * 3]] - c) * s, (mesh.V[mesh.I[t * 3 + 1]] - c) * s, (mesh.V[mesh.I[t * 3 + 2]] - c) * s);
        }
        if (o.Triangles == 0) throw new InvalidOperationException("Das Modell hat keine Fläche.");
        return o;
    }

    static Mesh Cluster(Mesh mesh, int maxTris)
    {
        var cur = mesh;
        var cells = 96;
        while (cur.Triangles > maxTris && cells >= 4)
        {
            cur = ClusterOnce(cur, cells);
            if (cur.Triangles == 0) throw new InvalidOperationException("Nach dem Vereinfachen bleibt keine Fläche übrig.");
            cells = cells * 2 / 3;
        }
        if (cur.Triangles > maxTris) throw new InvalidOperationException("Das Modell hat zu viele Flächen für das 3D-Studio.");
        return cur;
    }

    static Mesh ClusterOnce(Mesh src, int cells)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var v in src.V)
        {
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
        }
        var dx = Math.Max(maxX - minX, 1e-9); var dy = Math.Max(maxY - minY, 1e-9); var dz = Math.Max(maxZ - minZ, 1e-9);
        var map = new Dictionary<(int, int, int), int>();
        var sum = new List<Vec3>(); var cnt = new List<int>(); var cr = new List<int>(); var cg = new List<int>(); var cb = new List<int>();
        int Id(Vec3 v, byte r, byte g, byte b)
        {
            var key = (Math.Clamp((int)((v.X - minX) / dx * cells), 0, cells - 1), Math.Clamp((int)((v.Y - minY) / dy * cells), 0, cells - 1), Math.Clamp((int)((v.Z - minZ) / dz * cells), 0, cells - 1));
            if (!map.TryGetValue(key, out var id))
            {
                id = sum.Count; map[key] = id; sum.Add(v); cnt.Add(1); cr.Add(r); cg.Add(g); cb.Add(b); return id;
            }
            sum[id] += v; cnt[id]++; cr[id] += r; cg[id] += g; cb[id] += b; return id;
        }
        var tris = new List<(int A, int B, int C)>();
        for (var t = 0; t < src.Triangles; t++)
        {
            byte r = src.R[t], g = src.G[t], b = src.B[t];
            tris.Add((Id(src.V[src.I[t * 3]], r, g, b), Id(src.V[src.I[t * 3 + 1]], r, g, b), Id(src.V[src.I[t * 3 + 2]], r, g, b)));
        }
        var avg = new Vec3[sum.Count];
        for (var i = 0; i < sum.Count; i++) avg[i] = sum[i] * (1.0 / cnt[i]);
        var m = new Mesh();
        foreach (var (a, b, c) in tris)
        {
            if (a == b || b == c || a == c) continue;
            m.CR = (byte)(cr[a] / cnt[a]); m.CG = (byte)(cg[a] / cnt[a]); m.CB = (byte)(cb[a] / cnt[a]);
            m.Add(avg[a], avg[b], avg[c]);
        }
        return m;
    }

    static (Mesh Mesh, bool HasFaces) ReadPly(string path)
    {
        var b = File.ReadAllBytes(path);
        var headerEnd = IndexOf(b, "end_header");
        if (headerEnd < 0) throw new InvalidOperationException("Die PLY-Datei hat keinen gültigen Kopf.");
        var body = headerEnd + "end_header".Length;
        while (body < b.Length && b[body] is (byte)'\r' or (byte)'\n') { body++; if (b[body - 1] == '\n') break; }
        var header = Encoding.ASCII.GetString(b, 0, headerEnd);
        var format = header.Split('\n').Select(s => s.Trim()).FirstOrDefault(h => h.StartsWith("format ", StringComparison.Ordinal))?.Split(' ').ElementAtOrDefault(1)
            ?? throw new InvalidOperationException("PLY ohne Formatangabe.");
        if (format == "binary_big_endian") throw new InvalidOperationException("PLY im Big-Endian-Format wird nicht unterstützt.");
        var ascii = format == "ascii";
        if (!ascii && format != "binary_little_endian") throw new InvalidOperationException("Dieses PLY-Format kenne ich nicht.");
        var elements = new List<(string Name, int Count, List<PlyProp> Props)>();
        List<PlyProp>? cur = null;
        foreach (var raw in header.Split('\n'))
        {
            var t = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) continue;
            if (t[0] == "element" && t.Length >= 3) { cur = []; elements.Add((t[1], int.Parse(t[2], CultureInfo.InvariantCulture), cur)); }
            else if (t[0] == "property" && cur is not null)
            {
                if (t[1] == "list" && t.Length >= 5) cur.Add(new PlyProp { List = true, CountType = t[2], IndexType = t[3], Name = t[4] });
                else if (t.Length >= 3) cur.Add(new PlyProp { Type = t[1], Name = t[2] });
            }
        }
        var lines = ascii ? Encoding.ASCII.GetString(b, body, b.Length - body).Split('\n') : null;
        var line = 0;
        var pos = body;
        List<Vec3>? verts = null;
        List<(byte R, byte G, byte B)>? colors = null;
        var mesh = new Mesh();
        var hasFaces = false;
        foreach (var (name, count, props) in elements)
        {
            if (count > 2_000_000) throw new InvalidOperationException("Die Datei ist zu groß für das 3D-Studio.");
            if (name == "vertex")
            {
                int ix = props.FindIndex(p => p.Name == "x"), iy = props.FindIndex(p => p.Name == "y"), iz = props.FindIndex(p => p.Name == "z");
                if (ix < 0 || iy < 0 || iz < 0) throw new InvalidOperationException("Die PLY-Datei hat keine x/y/z-Werte.");
                int ir = props.FindIndex(p => p.Name is "red" or "r"), ig = props.FindIndex(p => p.Name is "green" or "g"), ib = props.FindIndex(p => p.Name is "blue" or "b");
                var col = ir >= 0 && ig >= 0 && ib >= 0;
                verts = new List<Vec3>(count);
                if (col) colors = new List<(byte, byte, byte)>(count);
                for (var i = 0; i < count; i++)
                {
                    if (ascii)
                    {
                        var nums = NextLine(lines!, ref line, props.Count);
                        verts.Add(new(D(nums[ix]), D(nums[iy]), D(nums[iz])));
                        if (col) colors!.Add(((byte)Math.Clamp(D(nums[ir]), 0, 255), (byte)Math.Clamp(D(nums[ig]), 0, 255), (byte)Math.Clamp(D(nums[ib]), 0, 255)));
                    }
                    else
                    {
                        double x = 0, y = 0, z = 0, r = 0, g = 0, bl = 0;
                        for (var p = 0; p < props.Count; p++)
                        {
                            if (props[p].List) throw new InvalidOperationException("Unerwartete Liste bei den Punkten.");
                            var val = ReadNum(b, ref pos, props[p].Type);
                            if (p == ix) x = val; else if (p == iy) y = val; else if (p == iz) z = val;
                            else if (col && p == ir) r = val; else if (col && p == ig) g = val; else if (col && p == ib) bl = val;
                        }
                        verts.Add(new(x, y, z));
                        if (col) colors!.Add(((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(bl, 0, 255)));
                    }
                }
            }
            else if (name == "face")
            {
                if (verts is null) throw new InvalidOperationException("Die PLY-Datei nennt Flächen vor den Punkten.");
                var list = props.FindIndex(p => p.List);
                if (list < 0) throw new InvalidOperationException("Die PLY-Datei hat Flächen ohne Punktliste.");
                hasFaces = count > 0;
                for (var i = 0; i < count; i++)
                {
                    int[] ids;
                    if (ascii)
                    {
                        var parts = NextRaw(lines!, ref line);
                        if (parts.Length == 0) continue;
                        var n = int.Parse(parts[0], CultureInfo.InvariantCulture);
                        ids = parts.Skip(1).Take(n).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    }
                    else
                    {
                        ids = [];
                        for (var p = 0; p < props.Count; p++)
                        {
                            if (!props[p].List) { pos += PlySize(props[p].Type); continue; }
                            var n = (int)ReadNum(b, ref pos, props[p].CountType);
                            var id = new int[n];
                            for (var k = 0; k < n; k++) id[k] = (int)ReadNum(b, ref pos, props[p].IndexType);
                            if (p == list) ids = id;
                        }
                    }
                    for (var k = 1; k + 1 < ids.Length; k++)
                    {
                        if ((uint)ids[0] >= (uint)verts.Count || (uint)ids[k] >= (uint)verts.Count || (uint)ids[k + 1] >= (uint)verts.Count) continue;
                        if (colors is not null)
                        {
                            mesh.CR = colors[ids[0]].R; mesh.CG = colors[ids[0]].G; mesh.CB = colors[ids[0]].B;
                        }
                        mesh.Add(verts[ids[0]], verts[ids[k]], verts[ids[k + 1]]);
                    }
                }
            }
            else if (ascii) line += count;
            else SkipElement(b, ref pos, props, count);
        }
        if (verts is null || verts.Count == 0) throw new InvalidOperationException("In der Datei sind keine Punkte.");
        if (!hasFaces)
        {
            var cloud = new Mesh();
            foreach (var v in verts) { cloud.V.Add(v); }
            return (cloud, false);
        }
        if (mesh.Triangles == 0) throw new InvalidOperationException("In der Datei ist kein Dreieck.");
        return (mesh, true);
    }

    static void SkipElement(byte[] b, ref int pos, List<PlyProp> props, int count)
    {
        if (props.Any(p => p.List)) throw new InvalidOperationException("Diese PLY-Datei hat einen Aufbau, den ich nicht überspringen kann.");
        var stride = props.Sum(p => PlySize(p.Type));
        pos += stride * count;
    }

    static string[] NextLine(string[] lines, ref int line, int min)
    {
        while (line < lines.Length && lines[line].Trim().Length == 0) line++;
        if (line >= lines.Length) throw new InvalidOperationException("Die PLY-Datei ist unvollständig.");
        var p = lines[line++].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < min) throw new InvalidOperationException("Die PLY-Datei ist unvollständig.");
        return p;
    }

    static string[] NextRaw(string[] lines, ref int line)
    {
        while (line < lines.Length && lines[line].Trim().Length == 0) line++;
        if (line >= lines.Length) return [];
        return lines[line++].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    static double ReadNum(byte[] b, ref int o, string type)
    {
        if (o + PlySize(type) > b.Length) throw new InvalidOperationException("Die PLY-Datei ist unvollständig.");
        double v = type.ToLowerInvariant() switch
        {
            "float" or "float32" => BitConverter.ToSingle(b, o),
            "double" or "float64" => BitConverter.ToDouble(b, o),
            "uchar" or "uint8" => b[o],
            "char" or "int8" => (sbyte)b[o],
            "short" or "int16" => BitConverter.ToInt16(b, o),
            "ushort" or "uint16" => BitConverter.ToUInt16(b, o),
            "int" or "int32" => BitConverter.ToInt32(b, o),
            "uint" or "uint32" => BitConverter.ToUInt32(b, o),
            _ => throw new InvalidOperationException($"Die PLY-Datei nutzt den Typ {type}, den ich nicht lese."),
        };
        o += PlySize(type);
        return v;
    }

    static int PlySize(string t) => t.ToLowerInvariant() switch
    {
        "char" or "uchar" or "int8" or "uint8" => 1,
        "short" or "ushort" or "int16" or "uint16" => 2,
        "int" or "uint" or "float" or "int32" or "uint32" or "float32" => 4,
        "double" or "float64" => 8,
        _ => throw new InvalidOperationException($"Die PLY-Datei nutzt den Typ {t}, den ich nicht lese."),
    };

    static int IndexOf(byte[] b, string s)
    {
        var p = Encoding.ASCII.GetBytes(s);
        for (var i = 0; i + p.Length <= Math.Min(b.Length, 65536); i++)
            if (b.AsSpan(i, p.Length).SequenceEqual(p)) return i;
        return -1;
    }

    sealed class PlyProp { public string Type = "", Name = "", CountType = "", IndexType = ""; public bool List; }

    /// <summary>Punkte ohne Flächen: äußere Hülle. Die Punktliste steckt in Mesh.V, ohne Dreiecke.</summary>
    public static Mesh Hull(Mesh cloud)
    {
        var pts = Thin(Dedupe(cloud.V), 48);
        if (pts.Count < 3) throw new InvalidOperationException("Zu wenige Punkte für ein Modell.");
        if (FlatEnough(pts, out var normal)) return Flat(pts, normal);
        var scale = Math.Max(1e-6, pts.Max(p => (p - pts[0]).Len));
        var mesh = new Mesh();
        for (var i = 0; i < pts.Count; i++)
            for (var j = i + 1; j < pts.Count; j++)
                for (var k = j + 1; k < pts.Count; k++)
                {
                    var n = Vec3.Cross(pts[j] - pts[i], pts[k] - pts[i]);
                    if (n.Len < 1e-10) continue;
                    var tol = 1e-8 * Math.Max(1, scale) * n.Len;
                    var pos = false; var neg = false;
                    for (var p = 0; p < pts.Count; p++)
                    {
                        if (p == i || p == j || p == k) continue;
                        var vol = Vec3.Dot(n, pts[p] - pts[i]);
                        if (vol > tol) pos = true;
                        else if (vol < -tol) neg = true;
                        if (pos && neg) break;
                    }
                    if (pos && neg) continue;
                    if (pos) mesh.Add(pts[i], pts[k], pts[j]);
                    else mesh.Add(pts[i], pts[j], pts[k]);
                }
        if (mesh.Triangles == 0) throw new InvalidOperationException("Aus den Punkten wird keine Fläche.");
        return mesh;
    }

    static List<Vec3> Dedupe(IReadOnlyList<Vec3> src)
    {
        var r = new List<Vec3>(src.Count);
        foreach (var p in src)
            if (!r.Any(q => (q - p).Len < 1e-7)) r.Add(p);
        return r;
    }

    static List<Vec3> Thin(List<Vec3> pts, int max)
    {
        if (pts.Count <= max) return pts;
        double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y), minZ = pts.Min(p => p.Z);
        double maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y), maxZ = pts.Max(p => p.Z);
        var diag = Math.Max(1e-6, new Vec3(maxX - minX, maxY - minY, maxZ - minZ).Len);
        var voxel = diag / 20;
        var cur = pts;
        while (cur.Count > max && voxel < diag * 2)
        {
            voxel *= 1.6;
            cur = Voxel(pts, voxel);
        }
        return cur;
    }

    static List<Vec3> Voxel(List<Vec3> pts, double voxel)
    {
        var map = new Dictionary<(long, long, long), (double X, double Y, double Z, int N)>();
        foreach (var p in pts)
        {
            var key = ((long)Math.Floor(p.X / voxel), (long)Math.Floor(p.Y / voxel), (long)Math.Floor(p.Z / voxel));
            map[key] = map.TryGetValue(key, out var g) ? (g.X + p.X, g.Y + p.Y, g.Z + p.Z, g.N + 1) : (p.X, p.Y, p.Z, 1);
        }
        return map.Values.Select(g => new Vec3(g.X / g.N, g.Y / g.N, g.Z / g.N)).ToList();
    }

    static bool FlatEnough(List<Vec3> pts, out Vec3 normal)
    {
        normal = new Vec3(0, 0, 1);
        int i1 = -1;
        for (var i = 1; i < pts.Count; i++) if ((pts[i] - pts[0]).Len > 1e-8) { i1 = i; break; }
        if (i1 < 0) return true;
        var best = 0.0;
        var n = new Vec3(0, 0, 1);
        for (var i = 0; i < pts.Count; i++)
        {
            var cr = Vec3.Cross(pts[i1] - pts[0], pts[i] - pts[0]);
            if (cr.Len > best) { best = cr.Len; n = cr; }
        }
        normal = n;
        if (best < 1e-10) return true;
        var scale = Math.Max(1e-6, pts.Max(p => (p - pts[0]).Len));
        foreach (var p in pts)
            if (Math.Abs(Vec3.Dot(p - pts[0], n)) > 1e-6 * scale * n.Len) return false;
        return true;
    }

    static Mesh Flat(List<Vec3> pts, Vec3 normal)
    {
        if (normal.Len < 1e-12) normal = new Vec3(0, 0, 1);
        var nrm = normal.Norm();
        var axis = Math.Abs(nrm.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
        var u = Vec3.Cross(axis, nrm).Norm();
        var v = Vec3.Cross(nrm, u);
        var proj = pts.Select((p, i) => (U: Vec3.Dot(p, u), V: Vec3.Dot(p, v), I: i)).ToList();
        var hull = Chain(proj);
        if (hull.Count < 3) throw new InvalidOperationException("Die Punkte liegen auf einer Linie.");
        var m = new Mesh();
        for (var i = 1; i + 1 < hull.Count; i++) m.Add(pts[hull[0]], pts[hull[i]], pts[hull[i + 1]]);
        if (m.Triangles == 0) throw new InvalidOperationException("Die Punkte liegen auf einer Linie.");
        return m;
    }

    static List<int> Chain(List<(double U, double V, int I)> p)
    {
        var uniq = new List<(double U, double V, int I)>();
        foreach (var pt in p.OrderBy(x => x.U).ThenBy(x => x.V))
            if (uniq.Count == 0 || Math.Abs(pt.U - uniq[^1].U) > 1e-9 || Math.Abs(pt.V - uniq[^1].V) > 1e-9) uniq.Add(pt);
        if (uniq.Count < 3) return uniq.Select(x => x.I).ToList();
        double Cross((double U, double V, int I) o, (double U, double V, int I) a, (double U, double V, int I) b)
            => (a.U - o.U) * (b.V - o.V) - (a.V - o.V) * (b.U - o.U);
        var lower = new List<(double U, double V, int I)>();
        foreach (var pt in uniq) { while (lower.Count >= 2 && Cross(lower[^2], lower[^1], pt) <= 0) lower.RemoveAt(lower.Count - 1); lower.Add(pt); }
        var upper = new List<(double U, double V, int I)>();
        for (var i = uniq.Count - 1; i >= 0; i--) { var pt = uniq[i]; while (upper.Count >= 2 && Cross(upper[^2], upper[^1], pt) <= 0) upper.RemoveAt(upper.Count - 1); upper.Add(pt); }
        lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1); lower.AddRange(upper);
        return lower.Select(x => x.I).ToList();
    }
}
