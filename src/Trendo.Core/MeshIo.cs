using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Lesen und Schreiben von 3D-Dateien. Trendo rechnet innen in Zentimetern.</summary>
public static class MeshIo
{
    public sealed class ImportException(string msg) : Exception(msg);

    public sealed record Loaded(string Name, Mesh3D Mesh, int Farbe);

    public static readonly string[] Extensions = [".obj", ".stl", ".glb", ".gltf", ".fbx", ".blend"];

    public static void WriteObj(string path, Mesh3D mesh)
    {
        var sb = new StringBuilder();
        var mtl = Path.ChangeExtension(path, ".mtl");
        sb.AppendLine("# Trendo OS");
        sb.Append("mtllib ").AppendLine(Path.GetFileName(mtl));
        var colors = mesh.Farbe.Count == mesh.T.Count ? mesh.Farbe : Enumerable.Repeat(0xB0B0B0, mesh.T.Count).ToList();
        var groups = colors.Select((c, i) => (c, i)).GroupBy(x => x.c);
        var mtlSb = new StringBuilder();
        foreach (var g in groups)
        {
            var name = "c" + g.Key.ToString("X6");
            var (r, gv, b) = ((g.Key >> 16 & 255) / 255.0, (g.Key >> 8 & 255) / 255.0, (g.Key & 255) / 255.0);
            mtlSb.Append("newmtl ").AppendLine(name);
            mtlSb.AppendLine(FormattableString.Invariant($"Kd {r:0.######} {gv:0.######} {b:0.######}"));
            mtlSb.AppendLine("Ka 0.1 0.1 0.1");
            mtlSb.AppendLine("Ks 0.2 0.2 0.2");
        }
        File.WriteAllText(mtl, mtlSb.ToString(), new UTF8Encoding(false));
        foreach (var v in mesh.V) sb.AppendLine(FormattableString.Invariant($"v {v.X:0.#####} {v.Y:0.#####} {v.Z:0.#####}"));
        string? cur = null;
        foreach (var (c, i) in colors.Select((c, i) => (c, i)))
        {
            var name = "c" + c.ToString("X6");
            if (name != cur) { sb.Append("usemtl ").AppendLine(name); cur = name; }
            var (a, b, d) = mesh.T[i];
            sb.AppendLine($"f {a + 1} {b + 1} {d + 1}");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static string ObjText(Mesh3D mesh)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-3d-obj-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var p = Path.Combine(tmp, "modell.obj");
            WriteObj(p, mesh);
            return File.ReadAllText(p);
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    public static List<Loaded> ReadObj(string path)
    {
        if (!File.Exists(path)) throw new ImportException("Die Datei gibt es nicht.");
        var lines = File.ReadAllLines(path);
        var v = new List<Vec3>();
        var parts = new List<Loaded>();
        Mesh3D? cur = null;
        var name = Path.GetFileNameWithoutExtension(path);
        var farbe = 0xB0B0B0;
        var mtls = ReadMtl(path, lines);
        void Flush() { if (cur is { T.Count: > 0 }) parts.Add(new(name, cur, farbe)); cur = null; }
        foreach (var raw in lines)
        {
            var s = raw.Trim();
            if (s.Length == 0 || s[0] == '#') continue;
            var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p[0] == "o" || p[0] == "g") { Flush(); name = s[2..].Trim(); if (name.Length == 0) name = "Teil"; }
            else if (p[0] == "usemtl" && p.Length > 1 && mtls.TryGetValue(p[1], out var col)) farbe = col;
            else if (p[0] == "v" && p.Length >= 4) v.Add(new(Num(p[1]), Num(p[2]), Num(p[3])));
            else if (p[0] == "f" && p.Length >= 4)
            {
                cur ??= new Mesh3D();
                int Idx(string tok)
                {
                    var cut = tok.Split('/')[0];
                    var i = int.Parse(cut, CultureInfo.InvariantCulture);
                    if (i < 0) i = v.Count + i + 1;
                    return i - 1;
                }
                var ids = p.Skip(1).Select(Idx).ToArray();
                for (var i = 1; i < ids.Length - 1; i++)
                {
                    if (ids[0] < 0 || ids[i] < 0 || ids[i + 1] < 0 || ids[0] >= v.Count) continue;
                    cur.T.Add((ids[0], ids[i], ids[i + 1]));
                    cur.Farbe.Add(farbe);
                }
            }
        }
        Flush();
        if (parts.Count == 0) throw new ImportException("In der OBJ-Datei steckt kein Dreieck.");
        foreach (var part in parts)
        {
            part.Mesh.V.AddRange(v);
            // Faces already index into the shared vertex list. Copy vertices per part so each mesh stands alone.
        }
        var result = new List<Loaded>();
        foreach (var part in parts)
        {
            var m = new Mesh3D();
            m.V.AddRange(v);
            m.T.AddRange(part.Mesh.T);
            m.Farbe.AddRange(part.Mesh.Farbe);
            result.Add(new(part.Name, m.Weld(), part.Farbe));
        }
        return result;
    }

    /// <summary>STL in Millimetern (3D-Druck). cm × 10.</summary>
    public static void WriteStl(string path, Mesh3D mesh)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        var header = Encoding.ASCII.GetBytes("Trendo OS STL mm");
        var buf = new byte[80];
        Array.Copy(header, buf, header.Length);
        bw.Write(buf);
        bw.Write((uint)mesh.T.Count);
        foreach (var (ia, ib, ic) in mesh.T)
        {
            var a = mesh.V[ia] * 10; var b = mesh.V[ib] * 10; var c = mesh.V[ic] * 10;
            var n = Vec3.Cross(b - a, c - a).Norm();
            bw.Write((float)n.X); bw.Write((float)n.Y); bw.Write((float)n.Z);
            void W(Vec3 p) { bw.Write((float)p.X); bw.Write((float)p.Y); bw.Write((float)p.Z); }
            W(a); W(b); W(c);
            bw.Write((ushort)0);
        }
    }

    public static Loaded ReadStl(string path, bool millimeters)
    {
        var bytes = File.ReadAllBytes(path);
        var scale = millimeters ? 0.1 : 1.0; // mm → cm
        Mesh3D mesh;
        if (bytes.Length >= 84)
        {
            var count = BitConverter.ToUInt32(bytes, 80);
            if (bytes.Length == 84 + count * 50)
            {
                mesh = new Mesh3D();
                var o = 84;
                for (uint i = 0; i < count; i++)
                {
                    o += 12; // normal
                    Vec3 R() { var p = new Vec3(BitConverter.ToSingle(bytes, o), BitConverter.ToSingle(bytes, o + 4), BitConverter.ToSingle(bytes, o + 8)); o += 12; return p * scale; }
                    var a = mesh.V.Count; mesh.V.Add(R()); mesh.V.Add(R()); mesh.V.Add(R());
                    mesh.T.Add((a, a + 1, a + 2));
                    mesh.Farbe.Add(0xB0B0B0);
                    o += 2;
                }
                if (mesh.T.Count == 0) throw new ImportException("Die STL-Datei enthält kein Dreieck.");
                return new(Path.GetFileNameWithoutExtension(path), mesh.Weld(), 0xB0B0B0);
            }
        }
        var text = Encoding.ASCII.GetString(bytes);
        if (!text.Contains("facet", StringComparison.OrdinalIgnoreCase))
            throw new ImportException("Diese STL-Datei kann Trendo nicht lesen.");
        mesh = new Mesh3D();
        var lines = text.Split('\n');
        var tri = new List<Vec3>();
        foreach (var line in lines)
        {
            var s = line.Trim();
            if (!s.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;
            var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 4) continue;
            tri.Add(new Vec3(Num(p[1]), Num(p[2]), Num(p[3])) * scale);
            if (tri.Count == 3)
            {
                var a = mesh.V.Count;
                mesh.V.AddRange(tri);
                mesh.T.Add((a, a + 1, a + 2));
                mesh.Farbe.Add(0xB0B0B0);
                tri.Clear();
            }
        }
        if (mesh.T.Count == 0) throw new ImportException("Die STL-Datei enthält kein Dreieck.");
        return new(Path.GetFileNameWithoutExtension(path), mesh.Weld(), 0xB0B0B0);
    }

    /// <summary>glTF rechnet in Metern. Wir teilen Zentimeter durch 100.</summary>
    public static void WriteGlb(string path, Mesh3D mesh)
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
            views.Add(FormattableString.Invariant($"{{\"buffer\":0,\"byteOffset\":{offset},\"byteLength\":{data.Length},\"target\":{target}}}"));
            return index;
        }
        var groups = new List<(int Farbe, List<int> Tris)>();
        var map = new Dictionary<int, int>();
        for (var i = 0; i < mesh.T.Count; i++)
        {
            var c = i < mesh.Farbe.Count ? mesh.Farbe[i] : 0xB0B0B0;
            if (!map.TryGetValue(c, out var g)) { g = groups.Count; map[c] = g; groups.Add((c, [])); }
            groups[g].Tris.Add(i);
        }
        if (groups.Count == 0) groups.Add((0xB0B0B0, []));
        foreach (var (farbe, tris) in groups)
        {
            var remap = new Dictionary<int, int>();
            var pos = new List<float>();
            var norm = new List<float>();
            var idx = new List<int>();
            int MapV(int i)
            {
                if (remap.TryGetValue(i, out var n)) return n;
                n = pos.Count / 3;
                var p = mesh.V[i];
                pos.Add((float)(p.X / 100)); pos.Add((float)(p.Y / 100)); pos.Add((float)(p.Z / 100));
                norm.Add(0); norm.Add(0); norm.Add(1);
                remap[i] = n;
                return n;
            }
            foreach (var ti in tris)
            {
                var (a, b, c) = mesh.T[ti];
                var va = MapV(a); var vb = MapV(b); var vc = MapV(c);
                idx.Add(va); idx.Add(vb); idx.Add(vc);
                var n = Vec3.Cross(mesh.V[b] - mesh.V[a], mesh.V[c] - mesh.V[a]).Norm();
                void Put(int slot) { norm[slot * 3] = (float)n.X; norm[slot * 3 + 1] = (float)n.Y; norm[slot * 3 + 2] = (float)n.Z; }
                Put(va); Put(vb); Put(vc);
            }
            if (idx.Count == 0) continue;
            float Min(List<float> s, int m) => s.Where((_, i) => i % 3 == m).Min();
            float Max(List<float> s, int m) => s.Where((_, i) => i % 3 == m).Max();
            var posBytes = pos.SelectMany(BitConverter.GetBytes).ToArray();
            var nrmBytes = norm.SelectMany(BitConverter.GetBytes).ToArray();
            var shortIdx = pos.Count / 3 <= 65535;
            var idxBytes = shortIdx ? idx.SelectMany(i => BitConverter.GetBytes((ushort)i)).ToArray() : idx.SelectMany(BitConverter.GetBytes).ToArray();
            var pv = AddView(posBytes, 34962);
            var nv = AddView(nrmBytes, 34962);
            var iv = AddView(idxBytes, 34963);
            int Acc(int view, int count, string type, int comp, string extra = "")
            {
                var i = accessors.Count;
                accessors.Add(FormattableString.Invariant($"{{\"bufferView\":{view},\"componentType\":{comp},\"count\":{count},\"type\":\"{type}\"{extra}}}"));
                return i;
            }
            var pa = Acc(pv, pos.Count / 3, "VEC3", 5126, FormattableString.Invariant($",\"min\":[{Min(pos, 0):R},{Min(pos, 1):R},{Min(pos, 2):R}],\"max\":[{Max(pos, 0):R},{Max(pos, 1):R},{Max(pos, 2):R}]"));
            var na = Acc(nv, norm.Count / 3, "VEC3", 5126);
            var ia = Acc(iv, idx.Count, "SCALAR", shortIdx ? 5123 : 5125);
            var mi = materials.Count;
            var rf = ((farbe >> 16) & 255) / 255.0;
            var gf = ((farbe >> 8) & 255) / 255.0;
            var bf = (farbe & 255) / 255.0;
            materials.Add(FormattableString.Invariant($"{{\"pbrMetallicRoughness\":{{\"baseColorFactor\":[{rf:0.###},{gf:0.###},{bf:0.###},1],\"metallicFactor\":0.05,\"roughnessFactor\":0.6}},\"name\":\"Farbe\"}}"));
            primitives.Add(FormattableString.Invariant($"{{\"attributes\":{{\"POSITION\":{pa},\"NORMAL\":{na}}},\"indices\":{ia},\"material\":{mi}}}"));
        }
        while (bin.Length % 4 != 0) bw.Write((byte)0x20);
        var binBytes = bin.ToArray();
        var json = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"Trendo OS\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],\"meshes\":[{\"primitives\":[" + string.Join(",", primitives) + "]}],\"materials\":[" + string.Join(",", materials) + "],\"accessors\":[" + string.Join(",", accessors) + "],\"bufferViews\":[" + string.Join(",", views) + "],\"buffers\":[{\"byteLength\":" + binBytes.Length + "}]}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        while (jsonBytes.Length % 4 != 0) jsonBytes = jsonBytes.Append((byte)0x20).ToArray();
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        var total = 12 + 8 + jsonBytes.Length + 8 + binBytes.Length;
        w.Write(0x46546C67); w.Write(2); w.Write(total);
        w.Write(jsonBytes.Length); w.Write(0x4E4F534A); w.Write(jsonBytes);
        w.Write(binBytes.Length); w.Write(0x004E4942); w.Write(binBytes);
    }

    public static List<Loaded> ReadGlb(string path) => ReadGltf(path, File.ReadAllBytes(path), Path.GetDirectoryName(path)!);

    public static List<Loaded> ReadGltfFile(string path)
    {
        var text = File.ReadAllText(path);
        return ReadGltf(path, Encoding.UTF8.GetBytes(text), Path.GetDirectoryName(path)!, text);
    }

    private static List<Loaded> ReadGltf(string path, byte[] file, string dir, string? jsonText = null)
    {
        byte[] bin;
        string json;
        if (jsonText is null)
        {
            if (file.Length < 20 || BitConverter.ToUInt32(file, 0) != 0x46546C67)
                throw new ImportException("Das ist keine glTF-Datei.");
            var o = 12; json = ""; bin = [];
            while (o + 8 <= file.Length)
            {
                var len = BitConverter.ToInt32(file, o); var kind = BitConverter.ToUInt32(file, o + 4); o += 8;
                if (o + len > file.Length) break;
                if (kind == 0x4E4F534A) json = Encoding.UTF8.GetString(file, o, len).TrimEnd();
                else if (kind == 0x004E4942) bin = file[o..(o + len)];
                o += len;
            }
        }
        else
        {
            json = jsonText;
            bin = [];
        }
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("extensionsRequired", out var req) && req.EnumerateArray().Any(e => e.GetString()?.Contains("draco", StringComparison.OrdinalIgnoreCase) == true))
            throw new ImportException("Diese Datei ist mit Draco gepackt. Speichere sie als gewöhnliches GLB, OBJ oder STL.");
        byte[] BufferBytes(int index)
        {
            if (index == 0 && bin.Length > 0 && jsonText is null) return bin;
            var b = root.GetProperty("buffers")[index];
            if (!b.TryGetProperty("uri", out var uriEl)) return bin;
            var uri = uriEl.GetString() ?? "";
            if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = uri.IndexOf(',');
                return Convert.FromBase64String(uri[(comma + 1)..]);
            }
            var bp = Path.Combine(dir, Uri.UnescapeDataString(uri));
            if (!File.Exists(bp)) throw new ImportException("Zur glTF-Datei fehlt die Binärdatei daneben.");
            return File.ReadAllBytes(bp);
        }
        if (!root.TryGetProperty("meshes", out var meshes)) throw new ImportException("In der Datei steckt kein Netz.");
        var buffers = new Dictionary<int, byte[]>();
        byte[] Buf(int i) => buffers.TryGetValue(i, out var b) ? b : buffers[i] = BufferBytes(i);
        int Farbe(int mat)
        {
            try
            {
                var f = root.GetProperty("materials")[mat].GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor");
                int C(int i) => (int)Math.Clamp(Math.Round(f[i].GetDouble() * 255), 0, 255);
                return (C(0) << 16) | (C(1) << 8) | C(2);
            }
            catch { return 0xB0B0B0; }
        }
        var parts = new List<Loaded>();
        var mi = 0;
        foreach (var mesh in meshes.EnumerateArray())
        {
            foreach (var prim in mesh.GetProperty("primitives").EnumerateArray())
            {
                if (!prim.GetProperty("attributes").TryGetProperty("POSITION", out var posAcc))
                    throw new ImportException("Der Datei fehlen die Punkte (POSITION).");
                var (pos, _) = ReadAccessor(root, Buf, posAcc.GetInt32());
                var indices = new List<int>();
                if (prim.TryGetProperty("indices", out var ind))
                {
                    var (raw, comp) = ReadAccessor(root, Buf, ind.GetInt32());
                    if (comp == 5123) { for (var i = 0; i + 1 < raw.Length; i += 2) indices.Add(BitConverter.ToUInt16(raw, i)); }
                    else if (comp == 5125) { for (var i = 0; i + 3 < raw.Length; i += 4) indices.Add(BitConverter.ToInt32(raw, i)); }
                    else if (comp == 5121) { foreach (var by in raw) indices.Add(by); }
                    else throw new ImportException("Die Dreieckszeiger in der Datei sind in einem Format, das Trendo nicht liest.");
                }
                else for (var i = 0; i < pos.Length / 12; i++) indices.Add(i);
                var m = new Mesh3D();
                for (var i = 0; i + 11 < pos.Length; i += 12)
                    m.V.Add(new(BitConverter.ToSingle(pos, i) * 100, BitConverter.ToSingle(pos, i + 4) * 100, BitConverter.ToSingle(pos, i + 8) * 100));
                var col = prim.TryGetProperty("material", out var mat) ? Farbe(mat.GetInt32()) : 0xB0B0B0;
                for (var i = 0; i + 2 < indices.Count; i += 3)
                {
                    m.T.Add((indices[i], indices[i + 1], indices[i + 2]));
                    m.Farbe.Add(col);
                }
                if (m.T.Count > 0) parts.Add(new(Path.GetFileNameWithoutExtension(path) + (mi == 0 ? "" : " " + (mi + 1)), m.Weld(), col));
                mi++;
            }
        }
        if (parts.Count == 0) throw new ImportException("In der Datei steckt kein Dreieck.");
        return parts;
    }

    private static (byte[] Raw, int Component) ReadAccessor(JsonElement root, Func<int, byte[]> buf, int index)
    {
        var acc = root.GetProperty("accessors")[index];
        var view = root.GetProperty("bufferViews")[acc.GetProperty("bufferView").GetInt32()];
        var buffer = buf(view.GetProperty("buffer").GetInt32());
        var off = (view.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0) + (acc.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
        var count = acc.GetProperty("count").GetInt32();
        var comp = acc.GetProperty("componentType").GetInt32();
        var type = acc.GetProperty("type").GetString();
        var comps = type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => throw new ImportException("Unbekannte glTF-Angabe: " + type) };
        var size = comp switch { 5120 or 5121 => 1, 5122 or 5123 => 2, 5125 or 5126 => 4, _ => throw new ImportException("Unbekannter Zahlentyp in der glTF-Datei.") };
        var stride = view.TryGetProperty("byteStride", out var st) ? st.GetInt32() : comps * size;
        var raw = new byte[count * comps * size];
        for (var i = 0; i < count; i++) Buffer.BlockCopy(buffer, off + i * stride, raw, i * comps * size, comps * size);
        return (raw, comp);
    }

    public static void WriteFbx(string path, Mesh3D mesh)
    {
        static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        var verts = string.Join(",", mesh.V.SelectMany(v => new[] { F(v.X), F(v.Y), F(v.Z) }));
        var idx = new List<string>();
        foreach (var (a, b, c) in mesh.T) { idx.Add(a.ToString()); idx.Add(b.ToString()); idx.Add((-c - 1).ToString()); }
        var sb = new StringBuilder();
        sb.AppendLine("; FBX 7.4.0 project file");
        sb.AppendLine("; Created by Trendo OS");
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
        sb.AppendLine($"\t\tVertices: *{mesh.V.Count * 3} {{");
        sb.AppendLine("\t\t\ta: " + verts);
        sb.AppendLine("\t\t}");
        sb.AppendLine($"\t\tPolygonVertexIndex: *{idx.Count} {{");
        sb.AppendLine("\t\t\ta: " + string.Join(",", idx));
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t\tGeometryVersion: 124");
        sb.AppendLine("\t}");
        sb.AppendLine("\tModel: 1001, \"Model::Modell\", \"Mesh\" {");
        sb.AppendLine("\t\tVersion: 232");
        sb.AppendLine("\t\tProperties70:  {");
        sb.AppendLine("\t\t\tP: \"RotationActive\", \"bool\", \"\", \"\",1");
        sb.AppendLine("\t\t\tP: \"InheritType\", \"enum\", \"\", \"\",1");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t\tShading: Y");
        sb.AppendLine("\t\tCulling: \"CullingOff\"");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine("Connections:  {");
        sb.AppendLine("\tC: \"OO\",1001,0");
        sb.AppendLine("\tC: \"OO\",1000,1001");
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static Loaded ReadFbx(string path)
    {
        using var fs = File.OpenRead(path);
        var magic = new byte[23];
        var n = fs.Read(magic, 0, magic.Length);
        if (n >= 20 && Encoding.ASCII.GetString(magic, 0, 18).Contains("Kaydara FBX Binary"))
            throw new ImportException("Diese FBX-Datei ist binär. Mit Blender kann Trendo sie öffnen, sonst speichere sie als OBJ, STL oder GLB.");
        fs.Position = 0;
        using var r = new StreamReader(fs);
        var text = r.ReadToEnd();
        var verts = GrabArray(text, "Vertices");
        var polys = GrabArray(text, "PolygonVertexIndex");
        if (verts.Count < 9 || polys.Count < 3) throw new ImportException("In der FBX-Datei steckt kein Netz, das Trendo lesen kann.");
        var mesh = new Mesh3D();
        for (var i = 0; i + 2 < verts.Count; i += 3) mesh.V.Add(new(verts[i], verts[i + 1], verts[i + 2]));
        var face = new List<int>();
        foreach (var id in polys)
        {
            if (id < 0) { face.Add((int)(-id - 1)); for (var k = 1; k < face.Count - 1; k++) { mesh.T.Add((face[0], face[k], face[k + 1])); mesh.Farbe.Add(0xB0B0B0); } face.Clear(); }
            else face.Add((int)id);
        }
        if (mesh.T.Count == 0) throw new ImportException("In der FBX-Datei steckt kein Dreieck.");
        return new(Path.GetFileNameWithoutExtension(path), mesh.Weld(), 0xB0B0B0);
    }

    public static byte[] RenderPng(Mesh3D mesh, int view, int width = 480, int height = 360)
    {
        var rgb = new byte[width * height * 3];
        for (var i = 0; i < rgb.Length; i += 3) { rgb[i] = 0x0E; rgb[i + 1] = 0x1A; rgb[i + 2] = 0x33; }
        if (mesh.T.Count == 0 || mesh.V.Count == 0) return EncodePng(rgb, width, height);
        var (min, max) = mesh.Bounds();
        var center = (min + max) / 2;
        var ext = (max - min).Len;
        if (ext < 1e-4) ext = 1;
        var light = new Vec3(0.35, -0.55, 0.75).Norm();
        bool persp = view == 5;
        Vec3 Cam(Vec3 p)
        {
            var d = p - center;
            return view switch
            {
                1 => new(-d.X, d.Z, -d.Y), // hinten
                2 => new(d.Y, d.Z, d.X),   // links
                3 => new(-d.Y, d.Z, -d.X), // rechts
                4 => new(d.X, -d.Y, -d.Z), // oben
                5 => ProjectPersp(d, ext),
                _ => new(d.X, d.Z, d.Y),   // vorn
            };
        }
        var proj = mesh.V.Select(Cam).ToList();
        var minX = proj.Min(p => p.X); var maxX = proj.Max(p => p.X);
        var minY = proj.Min(p => p.Y); var maxY = proj.Max(p => p.Y);
        var span = Math.Max(maxX - minX, maxY - minY);
        if (span < 1e-6) span = 1;
        var scale = 0.78 * Math.Min(width, height) / span;
        var zbuf = new float[width * height];
        Array.Fill(zbuf, float.PositiveInfinity);
        float Sx(double x) => (float)(width / 2.0 + (x - (minX + maxX) / 2) * scale);
        float Sy(double y) => (float)(height / 2.0 - (y - (minY + maxY) / 2) * scale);
        for (var ti = 0; ti < mesh.T.Count; ti++)
        {
            var (ia, ib, ic) = mesh.T[ti];
            var a = proj[ia]; var b = proj[ib]; var c = proj[ic];
            var n = Vec3.Cross(mesh.V[ib] - mesh.V[ia], mesh.V[ic] - mesh.V[ia]).Norm();
            var shade = 0.38 + 0.62 * Math.Abs(Vec3.Dot(n, light));
            var col = ti < mesh.Farbe.Count ? mesh.Farbe[ti] : 0x2F86F6;
            var cr = (byte)Math.Clamp(((col >> 16) & 255) * shade, 0, 255);
            var cg = (byte)Math.Clamp(((col >> 8) & 255) * shade, 0, 255);
            var cb = (byte)Math.Clamp((col & 255) * shade, 0, 255);
            var ax = Sx(a.X); var ay = Sy(a.Y); var az = (float)a.Z;
            var bx = Sx(b.X); var by = Sy(b.Y); var bz = (float)b.Z;
            var cx = Sx(c.X); var cy = Sy(c.Y); var cz = (float)c.Z;
            var minPx = (int)Math.Max(0, Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
            var maxPx = (int)Math.Min(width - 1, Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            var minPy = (int)Math.Max(0, Math.Floor(Math.Min(ay, Math.Min(by, cy))));
            var maxPy = (int)Math.Min(height - 1, Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            var area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 0.5f) continue;
            for (var y = minPy; y <= maxPy; y++)
                for (var x = minPx; x <= maxPx; x++)
                {
                    var px = x + 0.5f; var py = y + 0.5f;
                    var w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) / area;
                    var w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) / area;
                    var w2 = 1 - w0 - w1;
                    if (w0 < -0.001f || w1 < -0.001f || w2 < -0.001f) continue;
                    var z = (float)(w0 * az + w1 * bz + w2 * cz);
                    var pi = y * width + x;
                    if (z >= zbuf[pi]) continue;
                    zbuf[pi] = z;
                    var o = pi * 3;
                    rgb[o] = cr; rgb[o + 1] = cg; rgb[o + 2] = cb;
                }
        }
        _ = persp;
        return EncodePng(rgb, width, height);
    }

    private static Vec3 ProjectPersp(Vec3 d, double ext)
    {
        var eye = new Vec3(1, -1.15, 0.72).Norm() * ext * 2.4;
        var forward = (eye * -1).Norm();
        var right = Vec3.Cross(forward, new Vec3(0, 0, 1)).Norm();
        var up = Vec3.Cross(right, forward).Norm();
        var rel = d - eye;
        var z = Vec3.Dot(rel, forward);
        if (z < 0.05) z = 0.05;
        return new(Vec3.Dot(rel, right) / z * ext, Vec3.Dot(rel, up) / z * ext, z);
    }

    public static byte[] EncodePng(byte[] rgb, int w, int h)
    {
        var raw = new byte[(w * 3 + 1) * h];
        for (var y = 0; y < h; y++)
        {
            var row = y * (w * 3 + 1);
            raw[row] = 0;
            Buffer.BlockCopy(rgb, y * w * 3, raw, row + 1, w * 3);
        }
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string tag, byte[] data)
        {
            var len = BitConverter.GetBytes(data.Length); if (BitConverter.IsLittleEndian) Array.Reverse(len);
            ms.Write(len);
            var t = Encoding.ASCII.GetBytes(tag);
            ms.Write(t);
            ms.Write(data);
            var crc = Crc(t.Concat(data).ToArray());
            var cb = BitConverter.GetBytes(crc); if (BitConverter.IsLittleEndian) Array.Reverse(cb);
            ms.Write(cb);
        }
        var ihdr = new byte[13];
        void Be(int o, int v) { ihdr[o] = (byte)(v >> 24); ihdr[o + 1] = (byte)(v >> 16); ihdr[o + 2] = (byte)(v >> 8); ihdr[o + 3] = (byte)v; }
        Be(0, w); Be(4, h); ihdr[8] = 8; ihdr[9] = 2;
        Chunk("IHDR", ihdr);
        using var comp = new MemoryStream();
        comp.WriteByte(0x78); comp.WriteByte(0x9C);
        using (var def = new DeflateStream(comp, CompressionLevel.Fastest, true)) def.Write(raw);
        var adler = Adler(raw);
        var ab = BitConverter.GetBytes(adler); if (BitConverter.IsLittleEndian) Array.Reverse(ab);
        comp.Write(ab);
        Chunk("IDAT", comp.ToArray());
        Chunk("IEND", []);
        return ms.ToArray();
    }

    private static uint Adler(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var by in data) { a = (a + by) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }

    private static uint Crc(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var by in data)
        {
            c ^= by;
            for (var i = 0; i < 8; i++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFFu;
    }

    private static Dictionary<string, int> ReadMtl(string objPath, string[] objLines)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var mtllib = objLines.Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase));
        if (mtllib is null) return map;
        var p = Path.Combine(Path.GetDirectoryName(objPath)!, mtllib[7..].Trim());
        if (!File.Exists(p)) return map;
        string? name = null;
        foreach (var line in File.ReadAllLines(p))
        {
            var s = line.Trim();
            if (s.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase)) name = s[7..].Trim();
            else if (name is not null && s.StartsWith("Kd ", StringComparison.OrdinalIgnoreCase))
            {
                var n = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (n.Length >= 4)
                {
                    int C(string t) => (int)Math.Clamp(Math.Round(Num(t) * 255), 0, 255);
                    map[name] = (C(n[1]) << 16) | (C(n[2]) << 8) | C(n[3]);
                }
            }
        }
        return map;
    }

    private static List<double> GrabArray(string text, string key)
    {
        var i = text.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return [];
        var a = text.IndexOf("a:", i, StringComparison.Ordinal);
        if (a < 0 || a > i + 200) return [];
        var start = a + 2;
        var end = text.IndexOf('}', start);
        if (end < 0) end = Math.Min(text.Length, start + 2_000_000);
        var slice = text[start..end];
        var list = new List<double>();
        foreach (var tok in slice.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) list.Add(d);
        return list;
    }

    private static double Num(string s) => double.Parse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
}
