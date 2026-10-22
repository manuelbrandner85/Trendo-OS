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
        sb.Append("usemtl farbe\n");
        foreach (var v in mesh.V)
            sb.Append("v ").Append(F(v.X)).Append(' ').Append(F(v.Y)).Append(' ').Append(F(v.Z)).Append('\n');
        for (var t = 0; t < mesh.I.Count; t += 3)
            sb.Append("f ").Append(mesh.I[t] + 1).Append(' ').Append(mesh.I[t + 1] + 1).Append(' ').Append(mesh.I[t + 2] + 1).Append('\n');
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        var (r, g, b) = mesh.R.Count > 0 ? (mesh.R[0] / 255.0, mesh.G[0] / 255.0, mesh.B[0] / 255.0) : (0.18, 0.53, 0.96);
        File.WriteAllText(mtl, $"newmtl farbe\nKd {F(r)} {F(g)} {F(b)}\n", new UTF8Encoding(false));
    }

    /// <summary>STL in Millimetern (3D-Druck). Das Netz liegt in Zentimetern.</summary>
    public static void WriteStl(Mesh mesh, string path)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write(Encoding.ASCII.GetBytes("Trendo OS STL Millimeter".PadRight(80, ' ')[..80]));
        w.Write(mesh.Triangles);
        for (var t = 0; t < mesh.Triangles; t++)
        {
            var a = mesh.V[mesh.I[t * 3]] * 10; var b = mesh.V[mesh.I[t * 3 + 1]] * 10; var c = mesh.V[mesh.I[t * 3 + 2]] * 10;
            var n = Vec3.Cross(b - a, c - a).Norm();
            W(n); W(a); W(b); W(c); w.Write((ushort)0);
        }
        void W(Vec3 v) { w.Write((float)v.X); w.Write((float)v.Y); w.Write((float)v.Z); }
    }

    /// <summary>glTF speichert Meter. Wir rechnen Zentimeter um.</summary>
    public static void WriteGlb(Mesh mesh, string path)
    {
        var n = mesh.V.Count; var tris = mesh.Triangles;
        var pos = new byte[n * 12]; var col = new byte[n * 12]; var idx = new byte[tris * 12];
        double minX = 1e9, minY = 1e9, minZ = 1e9, maxX = -1e9, maxY = -1e9, maxZ = -1e9;
        for (var i = 0; i < n; i++)
        {
            var v = mesh.V[i] * 0.01;
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
            BitConverter.GetBytes((float)v.X).CopyTo(pos, i * 12);
            BitConverter.GetBytes((float)v.Y).CopyTo(pos, i * 12 + 4);
            BitConverter.GetBytes((float)v.Z).CopyTo(pos, i * 12 + 8);
        }
        for (var t = 0; t < tris; t++)
        {
            byte r = mesh.R[t], g = mesh.G[t], b = mesh.B[t];
            for (var k = 0; k < 3; k++)
            {
                var vi = mesh.I[t * 3 + k];
                BitConverter.GetBytes(r / 255f).CopyTo(col, vi * 12);
                BitConverter.GetBytes(g / 255f).CopyTo(col, vi * 12 + 4);
                BitConverter.GetBytes(b / 255f).CopyTo(col, vi * 12 + 8);
                BitConverter.GetBytes(mesh.I[t * 3 + k]).CopyTo(idx, (t * 3 + k) * 4);
            }
        }
        var bin = new byte[Align4(pos.Length) + Align4(col.Length) + Align4(idx.Length)];
        pos.CopyTo(bin, 0);
        var colOff = Align4(pos.Length); col.CopyTo(bin, colOff);
        var idxOff = colOff + Align4(col.Length); idx.CopyTo(bin, idxOff);
        var json = "{" +
            "\"asset\":{\"version\":\"2.0\",\"generator\":\"Trendo OS\"}," +
            "\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}]," +
            "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"COLOR_0\":1},\"indices\":2,\"mode\":4}]}]," +
            "\"accessors\":[" +
            Acc(0, 5126, n, "VEC3", $"[{F(minX)},{F(minY)},{F(minZ)}]", $"[{F(maxX)},{F(maxY)},{F(maxZ)}]") + "," +
            Acc(1, 5126, n, "VEC3", null, null) + "," +
            Acc(2, 5125, tris * 3, "SCALAR", null, null) +
            "],\"bufferViews\":[" +
            View(0, pos.Length) + "," + View(colOff, col.Length) + "," + View(idxOff, idx.Length) +
            "],\"buffers\":[{\"byteLength\":" + bin.Length + "}]}";
        var jb = Encoding.UTF8.GetBytes(json);
        var jpad = (4 - jb.Length % 4) % 4;
        var jsonChunk = new byte[jb.Length + jpad];
        jb.CopyTo(jsonChunk, 0);
        for (var i = 0; i < jpad; i++) jsonChunk[jb.Length + i] = 0x20;
        var total = 12 + 8 + jsonChunk.Length + 8 + bin.Length;
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write(0x46546C67); w.Write(2); w.Write(total);
        w.Write(jsonChunk.Length); w.Write(0x4E4F534A); w.Write(jsonChunk);
        w.Write(bin.Length); w.Write(0x004E4942); w.Write(bin);
    }

    static string Acc(int view, int type, int count, string kind, string? min, string? max)
    {
        var s = "{\"bufferView\":" + view + ",\"componentType\":" + type + ",\"count\":" + count + ",\"type\":\"" + kind + "\"";
        if (min is not null) s += ",\"min\":" + min + ",\"max\":" + max;
        return s + "}";
    }

    static string View(int offset, int length) => "{\"buffer\":0,\"byteOffset\":" + offset + ",\"byteLength\":" + length + "}";

    static int Align4(int n) => (n + 3) & ~3;

    static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    public static Part3D Import(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mesh = ext switch
        {
            ".obj" => ReadObj(path, 1),
            ".stl" => ReadStl(path),
            ".glb" => ReadGlb(path),
            ".gltf" => ReadGltf(path),
            _ => throw new InvalidOperationException("Trendo liest OBJ, STL und GLB. FBX und Blend gehen über Blender."),
        };
        if (mesh.Triangles == 0) throw new InvalidOperationException("In der Datei ist kein Dreieck.");
        if (mesh.Triangles > MaxTriangles) throw new InvalidOperationException($"Die Datei hat zu viele Dreiecke (über {MaxTriangles:N0}).");
        return PartFromMesh(mesh, Path.GetFileNameWithoutExtension(path));
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
            Positions = pos, Indices = mesh.I.ToArray(), Color = "#B7BCC4",
        };
    }

    static Mesh ReadObj(string path, double scale)
    {
        var v = new List<Vec3>(); var m = new Mesh();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 4) continue;
                v.Add(new(D(p[1]) * scale, D(p[2]) * scale, D(p[3]) * scale));
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
                        m.Add(v[ids[0]], v[ids[i]], v[ids[i + 1]]);
            }
        }
        return m;
    }

    static Mesh ReadStl(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 84)
        {
            var count = BitConverter.ToUInt32(bytes, 80);
            if (bytes.Length == 84L + count * 50L && count > 0 && !LooksAscii(bytes)) return ReadStlBinary(bytes, (int)count);
        }
        return ReadStlAscii(Encoding.UTF8.GetString(bytes));
    }

    static bool LooksAscii(byte[] bytes)
    {
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 200)).ToLowerInvariant();
        return head.Contains("facet", StringComparison.Ordinal);
    }

    static Mesh ReadStlBinary(byte[] bytes, int count)
    {
        var m = new Mesh();
        var o = 84;
        for (var i = 0; i < count; i++)
        {
            o += 12;
            Vec3 P() { var v = new Vec3(BitConverter.ToSingle(bytes, o), BitConverter.ToSingle(bytes, o + 4), BitConverter.ToSingle(bytes, o + 8)); o += 12; return v * 0.1; }
            var a = P(); var b = P(); var c = P();
            o += 2;
            m.Add(a, b, c);
        }
        return m;
    }

    static Mesh ReadStlAscii(string text)
    {
        var m = new Mesh(); var v = new List<Vec3>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4) v.Add(new Vec3(D(p[1]), D(p[2]), D(p[3])) * 0.1);
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
        var m = new Mesh();
        if (!root.TryGetProperty("meshes", out var meshes)) return m;
        var mi = 0;
        foreach (var mesh in meshes.EnumerateArray())
        {
            if (!mesh.TryGetProperty("primitives", out var prims)) continue;
            foreach (var prim in prims.EnumerateArray())
            {
                if (!prim.TryGetProperty("attributes", out var attr) || !attr.TryGetProperty("POSITION", out var posAcc)) continue;
                var pos = ReadVec3(root, bin, posAcc.GetInt32());
                int[]? indices = null;
                if (prim.TryGetProperty("indices", out var ind)) indices = ReadScalar(root, bin, ind.GetInt32());
                double[]? matrix = null;
                if (root.TryGetProperty("nodes", out var nodes))
                    foreach (var node in nodes.EnumerateArray())
                        if (node.TryGetProperty("mesh", out var nm) && nm.GetInt32() == mi && node.TryGetProperty("matrix", out var mat) && mat.GetArrayLength() == 16)
                            matrix = mat.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                for (var i = 0; i < pos.Length; i++) pos[i] = Transform(pos[i], matrix) * 100;
                if (indices is null)
                    for (var i = 0; i + 2 < pos.Length; i += 3) m.Add(pos[i], pos[i + 1], pos[i + 2]);
                else
                    for (var i = 0; i + 2 < indices.Length; i += 3)
                    {
                        var a = indices[i]; var b = indices[i + 1]; var c = indices[i + 2];
                        if ((uint)a < (uint)pos.Length && (uint)b < (uint)pos.Length && (uint)c < (uint)pos.Length) m.Add(pos[a], pos[b], pos[c]);
                    }
            }
            mi++;
        }
        _ = dir;
        return m;
    }

    static Vec3 Transform(Vec3 v, double[]? matrix)
    {
        if (matrix is null) return v;
        return new(
            matrix[0] * v.X + matrix[4] * v.Y + matrix[8] * v.Z + matrix[12],
            matrix[1] * v.X + matrix[5] * v.Y + matrix[9] * v.Z + matrix[13],
            matrix[2] * v.X + matrix[6] * v.Y + matrix[10] * v.Z + matrix[14]);
    }

    static Vec3[] ReadVec3(JsonElement root, byte[] bin, int accessor)
    {
        var (view, comp, count, _) = Accessor(root, accessor);
        var (off, _) = View(root, view);
        var stride = comp == 5126 ? 12 : throw new InvalidOperationException("Die Punkte in der Datei sind in einem Format, das Trendo nicht liest.");
        var list = new Vec3[count];
        for (var i = 0; i < count; i++)
        {
            var o = off + i * stride;
            list[i] = new(BitConverter.ToSingle(bin, o), BitConverter.ToSingle(bin, o + 4), BitConverter.ToSingle(bin, o + 8));
        }
        return list;
    }

    static int[] ReadScalar(JsonElement root, byte[] bin, int accessor)
    {
        var (view, comp, count, _) = Accessor(root, accessor);
        var (off, _) = View(root, view);
        var list = new int[count];
        for (var i = 0; i < count; i++)
        {
            var o = off + i * (comp == 5123 ? 2 : comp == 5121 ? 1 : 4);
            list[i] = comp switch
            {
                5121 => bin[o],
                5123 => BitConverter.ToUInt16(bin, o),
                _ => (int)BitConverter.ToUInt32(bin, o),
            };
        }
        return list;
    }

    static (int View, int Comp, int Count, string Type) Accessor(JsonElement root, int index)
    {
        var a = root.GetProperty("accessors")[index];
        return (a.GetProperty("bufferView").GetInt32(), a.GetProperty("componentType").GetInt32(), a.GetProperty("count").GetInt32(), a.GetProperty("type").GetString() ?? "");
    }

    static (int Offset, int Length) View(JsonElement root, int index)
    {
        var v = root.GetProperty("bufferViews")[index];
        var off = v.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
        return (off, v.GetProperty("byteLength").GetInt32());
    }

    static double D(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}
