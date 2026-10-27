using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public static partial class Mesh3D
{
    public const int MaxTriangles = 200_000;

    public static void WriteObj(Mesh mesh, string path)
    {
        var mtl = Path.ChangeExtension(path, ".mtl");
        var sb = new StringBuilder();
        sb.Append("mtllib ").Append(Path.GetFileName(mtl)).Append('\n');
        foreach (var v in mesh.V)
            sb.Append("v ").Append(F(v.X)).Append(' ').Append(F(v.Y)).Append(' ').Append(F(v.Z)).Append('\n');
        var groups = Groups(mesh);
        var mtlSb = new StringBuilder();
        foreach (var (key, tris) in groups)
        {
            var name = "c" + key.R.ToString("X2") + key.G.ToString("X2") + key.B.ToString("X2");
            mtlSb.Append("newmtl ").Append(name).Append('\n');
            mtlSb.Append("Kd ").Append(F(key.R / 255.0)).Append(' ').Append(F(key.G / 255.0)).Append(' ').Append(F(key.B / 255.0)).Append('\n');
            sb.Append("usemtl ").Append(name).Append('\n');
            foreach (var t in tris)
                sb.Append("f ").Append(mesh.I[t * 3] + 1).Append(' ').Append(mesh.I[t * 3 + 1] + 1).Append(' ').Append(mesh.I[t * 3 + 2] + 1).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        File.WriteAllText(mtl, mtlSb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>STL in Millimetern (3D-Druck). Das Netz liegt in Zentimetern.</summary>
    public static void WriteStl(Mesh mesh, string path)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write(Encoding.ASCII.GetBytes("binary STL, units: mm".PadRight(80, ' ')[..80])); // ohne Programmname (Vorgabe 2026-10-27)
        w.Write(mesh.Triangles);
        for (var t = 0; t < mesh.Triangles; t++)
        {
            var a = mesh.V[mesh.I[t * 3]] * 10; var b = mesh.V[mesh.I[t * 3 + 1]] * 10; var c = mesh.V[mesh.I[t * 3 + 2]] * 10;
            var n = Vec3.Cross(b - a, c - a).Norm();
            W(n); W(a); W(b); W(c); w.Write((ushort)0);
        }
        void W(Vec3 v) { w.Write((float)v.X); w.Write((float)v.Y); w.Write((float)v.Z); }
    }

    /// <summary>glTF speichert Meter. Wir rechnen Zentimeter um. Jede Farbe wird ein Material.</summary>
    public static void WriteGlb(Mesh mesh, string path)
    {
        var bin = new MemoryStream();
        using var bw = new BinaryWriter(bin, Encoding.UTF8, leaveOpen: true);
        var primitives = new List<string>();
        var accessors = new List<string>();
        var views = new List<string>();
        var materials = new List<string>();
        int AddView(byte[] data, int target)
        {
            var pad = (int)((4 - bin.Length % 4) % 4);
            for (var i = 0; i < pad; i++) bw.Write((byte)0);
            var offset = (int)bin.Length;
            bw.Write(data);
            var index = views.Count;
            views.Add("{\"buffer\":0,\"byteOffset\":" + offset + ",\"byteLength\":" + data.Length + ",\"target\":" + target + "}");
            return index;
        }
        foreach (var (key, tris) in Groups(mesh))
        {
            var pos = new List<float>();
            var norm = new List<float>();
            var idx = new List<int>();
            foreach (var ti in tris)
            {
                var a = mesh.I[ti * 3]; var b = mesh.I[ti * 3 + 1]; var c = mesh.I[ti * 3 + 2];
                var n = Vec3.Cross(mesh.V[b] - mesh.V[a], mesh.V[c] - mesh.V[a]).Norm();
                int Map(int i)
                {
                    var slot = pos.Count / 3;
                    var p = mesh.V[i] * 0.01;
                    pos.Add((float)p.X); pos.Add((float)p.Y); pos.Add((float)p.Z);
                    norm.Add((float)n.X); norm.Add((float)n.Y); norm.Add((float)n.Z);
                    return slot;
                }
                idx.Add(Map(a)); idx.Add(Map(b)); idx.Add(Map(c));
            }
            if (idx.Count == 0) continue;
            float Min(int m) { var v = float.MaxValue; for (var i = m; i < pos.Count; i += 3) v = Math.Min(v, pos[i]); return v; }
            float Max(int m) { var v = float.MinValue; for (var i = m; i < pos.Count; i += 3) v = Math.Max(v, pos[i]); return v; }
            var pv = AddView(pos.SelectMany(BitConverter.GetBytes).ToArray(), 34962);
            var nv = AddView(norm.SelectMany(BitConverter.GetBytes).ToArray(), 34962);
            var iv = AddView(idx.SelectMany(BitConverter.GetBytes).ToArray(), 34963);
            int Acc(int view, int count, string type, int comp, string extra = "")
            {
                var i = accessors.Count;
                accessors.Add("{\"bufferView\":" + view + ",\"componentType\":" + comp + ",\"count\":" + count + ",\"type\":\"" + type + "\"" + extra + "}");
                return i;
            }
            var pa = Acc(pv, pos.Count / 3, "VEC3", 5126, ",\"min\":[" + F(Min(0)) + "," + F(Min(1)) + "," + F(Min(2)) + "],\"max\":[" + F(Max(0)) + "," + F(Max(1)) + "," + F(Max(2)) + "]");
            var na = Acc(nv, norm.Count / 3, "VEC3", 5126);
            var ia = Acc(iv, idx.Count, "SCALAR", 5125);
            var mi = materials.Count;
            materials.Add("{\"pbrMetallicRoughness\":{\"baseColorFactor\":[" + F(key.R / 255.0) + "," + F(key.G / 255.0) + "," + F(key.B / 255.0) + ",1],\"metallicFactor\":0.05,\"roughnessFactor\":0.6},\"name\":\"Farbe\"}");
            primitives.Add("{\"attributes\":{\"POSITION\":" + pa + ",\"NORMAL\":" + na + "},\"indices\":" + ia + ",\"material\":" + mi + ",\"mode\":4}");
        }
        while (bin.Length % 4 != 0) bw.Write((byte)0x20);
        var binBytes = bin.ToArray();
        var json = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"Trendo OS\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],\"meshes\":[{\"primitives\":[" + string.Join(",", primitives) + "]}],\"materials\":[" + string.Join(",", materials) + "],\"accessors\":[" + string.Join(",", accessors) + "],\"bufferViews\":[" + string.Join(",", views) + "],\"buffers\":[{\"byteLength\":" + binBytes.Length + "}]}";
        var jb = Encoding.UTF8.GetBytes(json);
        var jpad = (4 - jb.Length % 4) % 4;
        var jsonChunk = new byte[jb.Length + jpad];
        jb.CopyTo(jsonChunk, 0);
        for (var i = 0; i < jpad; i++) jsonChunk[jb.Length + i] = 0x20;
        var total = 12 + 8 + jsonChunk.Length + 8 + binBytes.Length;
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write(0x46546C67); w.Write(2); w.Write(total);
        w.Write(jsonChunk.Length); w.Write(0x4E4F534A); w.Write(jsonChunk);
        w.Write(binBytes.Length); w.Write(0x004E4942); w.Write(binBytes);
    }

    static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Liest ein Netz (OBJ, STL, GLB, glTF) in Originaleinheiten. PLY mit Flächen liest der Foto-Scan.</summary>
    public static Mesh ReadMeshFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mesh = ext switch
        {
            ".obj" => ReadObjPlain(path),
            ".stl" => ReadStl(path, 1),
            ".glb" => ReadGlb(path),
            ".gltf" => ReadGltf(path),
            _ => throw new InvalidOperationException("Diese Datei ist kein Netz, das Trendo kennt."),
        };
        if (mesh.Triangles == 0) throw new InvalidOperationException("In der Datei ist kein Dreieck.");
        return mesh;
    }

    static Mesh ReadObjPlain(string path)
    {
        var v = new List<Vec3>(); var m = new Mesh();
        foreach (var raw in File.ReadLines(path))
        {
            var t = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) continue;
            if (t[0] == "v" && t.Length >= 4)
                v.Add(new Vec3(double.Parse(t[1], CultureInfo.InvariantCulture), double.Parse(t[2], CultureInfo.InvariantCulture), double.Parse(t[3], CultureInfo.InvariantCulture)));
            else if (t[0] == "f" && t.Length >= 4)
            {
                int Ix(string s) { var n = int.Parse(s.Split('/')[0], CultureInfo.InvariantCulture); return n < 0 ? v.Count + n : n - 1; }
                var f = t.Skip(1).Select(Ix).ToArray();
                for (var k = 1; k + 1 < f.Length; k++) m.Add(v[f[0]], v[f[k]], v[f[k + 1]]);
            }
        }
        return m;
    }

    public static Part3D Import(string path) => ImportParts(path, "auto")[0];

    /// <summary>Liest eine Datei als Teile. unit ist auto, mm oder cm. GLB und glTF bleiben Meter.</summary>
    public static List<Part3D> ImportParts(string path, string unit = "auto")
    {
        if (!File.Exists(path)) throw new InvalidOperationException("Die Datei gibt es nicht.");
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name)) name = "Datei";
        var mm = unit == "mm" || (unit != "cm" && ext == ".stl");
        var scale = ext is ".glb" or ".gltf" ? 100.0 : mm ? 0.1 : 1.0;
        List<Part3D> parts = ext switch
        {
            ".obj" => ReadObjParts(path, scale),
            ".stl" => SplitColors(ReadStl(path, scale), name),
            ".glb" => SplitColors(ReadGlb(path), name),
            ".gltf" => SplitColors(ReadGltf(path), name),
            ".fbx" when IsBinaryFbx(path) => throw new InvalidOperationException("Diese FBX-Datei ist binär. Mit Blender kann Trendo sie öffnen, sonst speichere sie als OBJ, STL oder GLB."),
            ".fbx" => SplitColors(ReadFbx(path, scale), name),
            ".blend" => throw new InvalidOperationException("Eine Blender-Datei öffnet Trendo mit Blender."),
            _ => throw new InvalidOperationException("Trendo liest OBJ, STL, GLB, glTF und FBX. Blend geht über Blender."),
        };
        if (parts.Count == 0) throw new InvalidOperationException("In der Datei ist kein Dreieck.");
        return parts;
    }

    public static bool IsBinaryFbx(string path)
    {
        if (!File.Exists(path)) return false;
        var buf = new byte[23];
        using var fs = File.OpenRead(path);
        var n = fs.Read(buf, 0, buf.Length);
        return n >= 18 && Encoding.ASCII.GetString(buf, 0, 18).Contains("Kaydara FBX Binary");
    }

    /// <summary>ASCII-FBX in Zentimetern, ohne Blender. Binäres FBX bleibt Blender vorbehalten.</summary>
    public static void WriteFbx(Mesh mesh, string path)
    {
        var verts = string.Join(",", mesh.V.SelectMany(v => new[] { F(v.X), F(v.Y), F(v.Z) }));
        var idx = new List<string>(mesh.Triangles * 3);
        for (var t = 0; t < mesh.Triangles; t++)
        {
            idx.Add(mesh.I[t * 3].ToString(CultureInfo.InvariantCulture));
            idx.Add(mesh.I[t * 3 + 1].ToString(CultureInfo.InvariantCulture));
            idx.Add((-mesh.I[t * 3 + 2] - 1).ToString(CultureInfo.InvariantCulture));
        }
        var sb = new StringBuilder();
        sb.AppendLine("; FBX 7.4.0 project file");
        sb.AppendLine("; Created by Trendo OS");
        sb.AppendLine("; Einheit: Zentimeter");
        sb.AppendLine("FBXHeaderExtension:  {");
        sb.AppendLine("\tFBXHeaderVersion: 1003");
        sb.AppendLine("\tFBXVersion: 7400");
        sb.AppendLine("\tCreator: \"Trendo OS\"");
        sb.AppendLine("}");
        sb.AppendLine("GlobalSettings:  {");
        sb.AppendLine("\tVersion: 1000");
        sb.AppendLine("\tProperties70:  {");
        sb.AppendLine("\t\tP: \"UpAxis\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"UpAxisSign\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"FrontAxis\", \"int\", \"Integer\", \"\",2");
        sb.AppendLine("\t\tP: \"FrontAxisSign\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"CoordAxis\", \"int\", \"Integer\", \"\",0");
        sb.AppendLine("\t\tP: \"CoordAxisSign\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"UnitScaleFactor\", \"double\", \"Number\", \"\",1");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine("Objects:  {");
        sb.AppendLine("\tGeometry: 1000, \"Geometry::Modell\", \"Mesh\" {");
        sb.AppendLine("\t\tVertices: *" + (mesh.V.Count * 3) + " {");
        sb.AppendLine("\t\t\ta: " + verts);
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t\tPolygonVertexIndex: *" + idx.Count + " {");
        sb.AppendLine("\t\t\ta: " + string.Join(",", idx));
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t\tGeometryVersion: 124");
        sb.AppendLine("\t}");
        sb.AppendLine("\tModel: 1001, \"Model::Modell\", \"Mesh\" {");
        sb.AppendLine("\t\tVersion: 232");
        sb.AppendLine("\t\tProperties70:  {");
        sb.AppendLine("\t\t\tP: \"RotationActive\", \"bool\", \"\", \"\",1");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine("Connections:  {");
        sb.AppendLine("\tC: \"OO\",1001,0");
        sb.AppendLine("\tC: \"OO\",1000,1001");
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static Part3D PartFromMesh(Mesh mesh, string name)
    {
        double minX = 1e9, minY = 1e9, minZ = 1e9, maxX = -1e9, maxY = -1e9, maxZ = -1e9;
        foreach (var v in mesh.V)
        {
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
        }
        var c = new Vec3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        var pos = new float[mesh.V.Count * 3];
        for (var i = 0; i < mesh.V.Count; i++)
        {
            var v = mesh.V[i] - c;
            pos[i * 3] = (float)v.X; pos[i * 3 + 1] = (float)v.Y; pos[i * 3 + 2] = (float)v.Z;
        }
        var sx = Math.Max(0.2, maxX - minX); var sy = Math.Max(0.2, maxY - minY); var sz = Math.Max(0.2, maxZ - minZ);
        var label = string.IsNullOrWhiteSpace(name) ? "Datei" : name.Trim();
        if (label.Length > 40) label = label[..40];
        return new Part3D
        {
            Name = label, Shape = "mesh", Sx = sx, Sy = sy, Sz = sz, BaseX = sx, BaseY = sy, BaseZ = sz,
            X = c.X, Y = c.Y, Z = c.Z,
            Positions = pos, Indices = mesh.I.ToArray(), Color = UniformColor(mesh),
        };
    }

    static List<Part3D> ReadObjParts(string path, double scale)
    {
        var lines = File.ReadAllLines(path);
        var v = new List<Vec3>();
        var mtls = ReadMtl(path, lines);
        var chunks = new List<(string Name, Mesh Mesh)>();
        Mesh? cur = null;
        var name = Path.GetFileNameWithoutExtension(path);
        void Paint(byte r, byte g, byte b) { if (cur is null) return; cur.CR = r; cur.CG = g; cur.CB = b; }
        byte cr = 0xB7, cg = 0xBC, cb = 0xC4;
        void Flush() { if (cur is { Triangles: > 0 }) chunks.Add((name, cur)); cur = null; }
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p[0] is "o" or "g") { Flush(); name = line.Length > 2 ? line[2..].Trim() : "Teil"; if (name.Length == 0) name = "Teil"; }
            else if (p[0] == "usemtl" && p.Length > 1 && mtls.TryGetValue(p[1], out var col)) { cr = col.R; cg = col.G; cb = col.B; Paint(cr, cg, cb); }
            else if (p[0] == "v" && p.Length >= 4) v.Add(new(D(p[1]) * scale, D(p[2]) * scale, D(p[3]) * scale));
            else if (p[0] == "f" && p.Length >= 4)
            {
                cur ??= new Mesh();
                Paint(cr, cg, cb);
                var ids = new List<int>();
                for (var i = 1; i < p.Length; i++)
                {
                    var tok = p[i].Split('/')[0];
                    if (!int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) continue;
                    if (id < 0) id = v.Count + id + 1;
                    ids.Add(id - 1);
                }
                for (var i = 1; i + 1 < ids.Count; i++)
                    if ((uint)ids[0] < (uint)v.Count && (uint)ids[i] < (uint)v.Count && (uint)ids[i + 1] < (uint)v.Count)
                        cur.Add(v[ids[0]], v[ids[i]], v[ids[i + 1]]);
            }
        }
        Flush();
        if (chunks.Count == 0) throw new InvalidOperationException("In der Datei ist kein Dreieck.");
        var parts = new List<Part3D>();
        foreach (var (chunkName, mesh) in chunks)
        {
            Guard(mesh.Triangles);
            parts.AddRange(SplitColors(mesh, chunkName));
        }
        return parts;
    }

    static Mesh ReadStl(string path, double scale)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 84)
        {
            var count = BitConverter.ToUInt32(bytes, 80);
            if (bytes.Length == 84L + count * 50L && count > 0 && !LooksAscii(bytes)) return ReadStlBinary(bytes, (int)count, scale);
        }
        return ReadStlAscii(Encoding.UTF8.GetString(bytes), scale);
    }

    static bool LooksAscii(byte[] bytes)
    {
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 200)).ToLowerInvariant();
        return head.Contains("facet", StringComparison.Ordinal);
    }

    static Mesh ReadStlBinary(byte[] bytes, int count, double scale)
    {
        var m = Gray();
        var o = 84;
        for (var i = 0; i < count; i++)
        {
            o += 12;
            Vec3 P() { var v = new Vec3(BitConverter.ToSingle(bytes, o), BitConverter.ToSingle(bytes, o + 4), BitConverter.ToSingle(bytes, o + 8)); o += 12; return v * scale; }
            var a = P(); var b = P(); var c = P();
            o += 2;
            m.Add(a, b, c);
        }
        return m;
    }

    static Mesh ReadStlAscii(string text, double scale)
    {
        var m = Gray(); var v = new List<Vec3>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4) v.Add(new Vec3(D(p[1]), D(p[2]), D(p[3])) * scale);
                if (v.Count == 3) { m.Add(v[0], v[1], v[2]); v.Clear(); }
            }
        }
        return m;
    }

    static Mesh ReadGlb(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546C67) throw new InvalidOperationException("Das ist keine GLB-Datei.");
        var o = 12; string? json = null; byte[]? bin = null;
        while (o + 8 <= bytes.Length)
        {
            var len = BitConverter.ToInt32(bytes, o); var type = BitConverter.ToUInt32(bytes, o + 4); o += 8;
            if (len < 0 || o + len > bytes.Length) break;
            if (type == 0x4E4F534A) json = Encoding.UTF8.GetString(bytes, o, len);
            else if (type == 0x004E4942) { bin = new byte[len]; Buffer.BlockCopy(bytes, o, bin, 0, len); }
            o += len;
        }
        if (json is null || bin is null) throw new InvalidOperationException("Die GLB-Datei hat keinen Inhalt.");
        return ParseGltf(json, bin, Path.GetDirectoryName(path)!);
    }

    static Mesh ReadGltf(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("buffers", out var buffers) || buffers.GetArrayLength() == 0) throw new InvalidOperationException("Die glTF-Datei hat keinen Speicher.");
        var uri = buffers[0].TryGetProperty("uri", out var u) ? u.GetString() : null;
        byte[] bin;
        if (uri is null) throw new InvalidOperationException("Die glTF-Datei verweist auf keinen Speicher.");
        if (uri.StartsWith("data:", StringComparison.Ordinal))
        {
            var comma = uri.IndexOf(',');
            bin = Convert.FromBase64String(uri[(comma + 1)..]);
        }
        else bin = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path)!, Uri.UnescapeDataString(uri)));
        return ParseGltf(json, bin, Path.GetDirectoryName(path)!);
    }

    static Mesh ParseGltf(string json, byte[] bin, string dir)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("extensionsRequired", out var req))
            foreach (var e in req.EnumerateArray())
                if (e.GetString()?.Contains("draco", StringComparison.OrdinalIgnoreCase) == true)
                    throw new InvalidOperationException("Diese GLB-Datei ist komprimiert. Bitte als OBJ oder STL speichern.");
        var cache = new Dictionary<int, byte[]>();
        byte[] Buf(int index)
        {
            if (cache.TryGetValue(index, out var hit)) return hit;
            if (index == 0 && bin.Length > 0) return cache[index] = bin;
            if (!root.TryGetProperty("buffers", out var buffers) || index < 0 || index >= buffers.GetArrayLength())
                throw new InvalidOperationException("Die glTF-Datei verweist auf keinen Speicher.");
            var b = buffers[index];
            if (!b.TryGetProperty("uri", out var uriEl))
            {
                if (bin.Length > 0) return cache[index] = bin;
                throw new InvalidOperationException("Die glTF-Datei verweist auf keinen Speicher.");
            }
            var uri = uriEl.GetString() ?? "";
            byte[] data;
            if (uri.StartsWith("data:", StringComparison.Ordinal))
            {
                var comma = uri.IndexOf(',');
                data = Convert.FromBase64String(uri[(comma + 1)..]);
            }
            else
            {
                var bp = Path.Combine(dir, Uri.UnescapeDataString(uri));
                if (!File.Exists(bp)) throw new InvalidOperationException("Zur glTF-Datei fehlt die Binärdatei daneben.");
                data = File.ReadAllBytes(bp);
            }
            return cache[index] = data;
        }
        var m = Gray();
        if (!root.TryGetProperty("meshes", out var meshes)) return m;
        var mi = 0;
        foreach (var mesh in meshes.EnumerateArray())
        {
            if (!mesh.TryGetProperty("primitives", out var prims)) continue;
            double[]? matrix = null;
            if (root.TryGetProperty("nodes", out var nodes))
                foreach (var node in nodes.EnumerateArray())
                    if (node.TryGetProperty("mesh", out var nm) && nm.GetInt32() == mi && node.TryGetProperty("matrix", out var mat) && mat.GetArrayLength() == 16)
                        matrix = mat.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            foreach (var prim in prims.EnumerateArray())
            {
                if (prim.TryGetProperty("mode", out var mode) && mode.GetInt32() != 4)
                    throw new InvalidOperationException("Diese glTF-Datei ist kein Dreiecksnetz. Bitte als OBJ oder STL speichern.");
                if (!prim.TryGetProperty("attributes", out var attr) || !attr.TryGetProperty("POSITION", out var posAcc)) continue;
                var (posRaw, posComp, posCount) = ReadPacked(root, Buf, posAcc.GetInt32());
                if (posComp != 5126) throw new InvalidOperationException("Die Punkte in der Datei sind in einem Format, das Trendo nicht liest.");
                var pos = new Vec3[posCount];
                for (var i = 0; i < posCount; i++)
                    pos[i] = Transform(new(BitConverter.ToSingle(posRaw, i * 12), BitConverter.ToSingle(posRaw, i * 12 + 4), BitConverter.ToSingle(posRaw, i * 12 + 8)), matrix) * 100;
                int[] indices;
                if (prim.TryGetProperty("indices", out var ind))
                {
                    var (raw, comp, count) = ReadPacked(root, Buf, ind.GetInt32());
                    indices = new int[count];
                    for (var i = 0; i < count; i++)
                        indices[i] = comp switch
                        {
                            5120 or 5121 => raw[i],
                            5122 or 5123 => BitConverter.ToUInt16(raw, i * 2),
                            _ => (int)BitConverter.ToUInt32(raw, i * 4),
                        };
                }
                else
                {
                    indices = new int[posCount];
                    for (var i = 0; i < posCount; i++) indices[i] = i;
                }
                var (r, g, b) = MaterialRgb(root, prim);
                m.CR = r; m.CG = g; m.CB = b;
                for (var i = 0; i + 2 < indices.Length; i += 3)
                {
                    var a = indices[i]; var b0 = indices[i + 1]; var c = indices[i + 2];
                    if ((uint)a < (uint)pos.Length && (uint)b0 < (uint)pos.Length && (uint)c < (uint)pos.Length) m.Add(pos[a], pos[b0], pos[c]);
                }
            }
            mi++;
        }
        return m;
    }

    static (byte[] Raw, int Comp, int Count) ReadPacked(JsonElement root, Func<int, byte[]> buf, int accessor)
    {
        var a = root.GetProperty("accessors")[accessor];
        if (!a.TryGetProperty("bufferView", out var bv)) throw new InvalidOperationException("Der Datei fehlt ein Speicherbereich.");
        var view = root.GetProperty("bufferViews")[bv.GetInt32()];
        var buffer = buf(view.TryGetProperty("buffer", out var bi) ? bi.GetInt32() : 0);
        var off = (view.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0) + (a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
        var count = a.GetProperty("count").GetInt32();
        var comp = a.GetProperty("componentType").GetInt32();
        var type = a.GetProperty("type").GetString() ?? "SCALAR";
        var comps = type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => throw new InvalidOperationException("Die Datei enthält eine Angabe, die Trendo nicht liest.") };
        var size = comp switch { 5120 or 5121 => 1, 5122 or 5123 => 2, 5125 or 5126 => 4, _ => throw new InvalidOperationException("Die Zahlen in der Datei sind in einem Format, das Trendo nicht liest.") };
        var tight = comps * size;
        var stride = view.TryGetProperty("byteStride", out var st) && st.GetInt32() >= tight ? st.GetInt32() : tight;
        if (count < 0 || off < 0) throw new InvalidOperationException("Die Datei ist abgeschnitten.");
        var raw = new byte[count * tight];
        for (var i = 0; i < count; i++)
        {
            var at = off + i * stride;
            if ((uint)at > (uint)buffer.Length || at + tight > buffer.Length) throw new InvalidOperationException("Die Datei ist abgeschnitten.");
            Buffer.BlockCopy(buffer, at, raw, i * tight, tight);
        }
        return (raw, comp, count);
    }

    static (byte R, byte G, byte B) MaterialRgb(JsonElement root, JsonElement prim)
    {
        if (!prim.TryGetProperty("material", out var mat) || !root.TryGetProperty("materials", out var materials)) return (0xB7, 0xBC, 0xC4);
        try
        {
            var f = materials[mat.GetInt32()].GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor");
            byte C(int i) => (byte)Math.Clamp((int)Math.Round(f[i].GetDouble() * 255), 0, 255);
            return (C(0), C(1), C(2));
        }
        catch { return (0xB7, 0xBC, 0xC4); }
    }

    static Vec3 Transform(Vec3 v, double[]? matrix)
    {
        if (matrix is null) return v;
        return new(
            matrix[0] * v.X + matrix[4] * v.Y + matrix[8] * v.Z + matrix[12],
            matrix[1] * v.X + matrix[5] * v.Y + matrix[9] * v.Z + matrix[13],
            matrix[2] * v.X + matrix[6] * v.Y + matrix[10] * v.Z + matrix[14]);
    }

    static double D(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    static Mesh Gray() => new() { CR = 0xB7, CG = 0xBC, CB = 0xC4 };

    static void Guard(int tris)
    {
        if (tris > MaxTriangles) throw new InvalidOperationException($"Die Datei hat zu viele Dreiecke (über {MaxTriangles:N0}).");
    }

    static List<(ColorKey Key, List<int> Tris)> Groups(Mesh mesh)
    {
        var map = new Dictionary<ColorKey, List<int>>();
        var order = new List<ColorKey>();
        for (var t = 0; t < mesh.Triangles; t++)
        {
            var key = new ColorKey(mesh.R.Count > t ? mesh.R[t] : mesh.CR, mesh.G.Count > t ? mesh.G[t] : mesh.CG, mesh.B.Count > t ? mesh.B[t] : mesh.CB);
            if (!map.TryGetValue(key, out var list)) { list = []; map[key] = list; order.Add(key); }
            list.Add(t);
        }
        return order.Select(k => (k, map[k])).ToList();
    }

    static List<Part3D> SplitColors(Mesh mesh, string name)
    {
        Guard(mesh.Triangles);
        if (mesh.Triangles == 0) throw new InvalidOperationException("In der Datei ist kein Dreieck.");
        var groups = Groups(mesh);
        var parts = new List<Part3D>();
        for (var i = 0; i < groups.Count; i++)
        {
            var (key, tris) = groups[i];
            var g = new Mesh { CR = key.R, CG = key.G, CB = key.B };
            foreach (var t in tris)
                g.Add(mesh.V[mesh.I[t * 3]], mesh.V[mesh.I[t * 3 + 1]], mesh.V[mesh.I[t * 3 + 2]]);
            var label = groups.Count == 1 ? name : name + (i == 0 ? "" : " " + (i + 1));
            parts.Add(PartFromMesh(g, label));
        }
        return parts;
    }

    static string UniformColor(Mesh mesh)
    {
        if (mesh.R.Count == 0) return "#B7BCC4";
        var r = mesh.R[0]; var g = mesh.G[0]; var b = mesh.B[0];
        for (var i = 1; i < mesh.R.Count; i++)
            if (mesh.R[i] != r || mesh.G[i] != g || mesh.B[i] != b) return "#B7BCC4";
        return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    readonly record struct ColorKey(byte R, byte G, byte B);

    static Dictionary<string, (byte R, byte G, byte B)> ReadMtl(string objPath, string[] objLines)
    {
        var map = new Dictionary<string, (byte R, byte G, byte B)>(StringComparer.OrdinalIgnoreCase);
        var mtllib = objLines.Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase));
        if (mtllib is null) return map;
        var file = Path.Combine(Path.GetDirectoryName(objPath) ?? "", mtllib[7..].Trim());
        if (!File.Exists(file)) return map;
        string? name = null;
        foreach (var line in File.ReadAllLines(file))
        {
            var s = line.Trim();
            if (s.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase)) name = s[7..].Trim();
            else if (name is not null && s.StartsWith("Kd ", StringComparison.OrdinalIgnoreCase))
            {
                var n = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (n.Length >= 4)
                {
                    byte C(string t) => (byte)Math.Clamp((int)Math.Round(D(t) * 255), 0, 255);
                    map[name] = (C(n[1]), C(n[2]), C(n[3]));
                }
            }
        }
        return map;
    }

    static Mesh ReadFbx(string path, double scale)
    {
        var text = File.ReadAllText(path);
        var verts = GrabArray(text, "Vertices");
        var polys = GrabArray(text, "PolygonVertexIndex");
        if (verts.Count < 9 || polys.Count < 3) throw new InvalidOperationException("In der FBX-Datei steckt kein Netz, das Trendo lesen kann.");
        var mesh = Gray();
        var v = new List<Vec3>();
        for (var i = 0; i + 2 < verts.Count; i += 3) v.Add(new(verts[i] * scale, verts[i + 1] * scale, verts[i + 2] * scale));
        var face = new List<int>();
        foreach (var id in polys)
        {
            if (id < 0)
            {
                face.Add((int)(-id - 1));
                for (var k = 1; k + 1 < face.Count; k++)
                    if ((uint)face[0] < (uint)v.Count && (uint)face[k] < (uint)v.Count && (uint)face[k + 1] < (uint)v.Count)
                        mesh.Add(v[face[0]], v[face[k]], v[face[k + 1]]);
                face.Clear();
            }
            else face.Add((int)id);
        }
        if (mesh.Triangles == 0) throw new InvalidOperationException("In der FBX-Datei steckt kein Dreieck.");
        Guard(mesh.Triangles);
        return mesh;
    }

    static List<double> GrabArray(string text, string key)
    {
        var i = text.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return [];
        var a = text.IndexOf("a:", i, StringComparison.Ordinal);
        if (a < 0 || a > i + 200) return [];
        var start = a + 2;
        var end = text.IndexOf('}', start);
        if (end < 0) end = Math.Min(text.Length, start + 2_000_000);
        var list = new List<double>();
        foreach (var tok in text[start..end].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) list.Add(d);
        return list;
    }
}
