using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record XrModelInfo(string File, long Bytes, int Meshes, int Primitives, long Triangles, long Vertices, int Materials, int Textures, int Nodes, int Animations, string Generator);

/// <summary>„3D-Modelle ansehen“ (Kennung SPEC-04, T-0722): 3D-Modelle (glTF/GLB) lesen und auf Fehler prüfen – für 3D-Druck und Architektur.
/// Die VR-Teile (Zielbrillen mit Richtwerten, Bildzeit) sind mit T-0722 entfernt (Nutzerentscheidung 16.11.2026).</summary>
public static class XrInspector
{
    public static XrModelInfo Inspect(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string json;
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "glTF")
        {
            if (BitConverter.ToUInt32(bytes, 4) != 2) throw new InvalidOperationException("Nur glTF 2.0 wird unterstützt.");
            if (bytes.Length < 20) throw new InvalidOperationException("Die GLB-Datei ist beschädigt.");
            var len = (int)BitConverter.ToUInt32(bytes, 12);
            if (BitConverter.ToUInt32(bytes, 16) != 0x4E4F534A || 20 + len > bytes.Length) throw new InvalidOperationException("Die GLB-Datei ist beschädigt (kein JSON-Teil).");
            json = Encoding.UTF8.GetString(bytes, 20, len);
        }
        else json = Encoding.UTF8.GetString(bytes);
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); } catch (JsonException) { throw new InvalidOperationException("Das ist keine glTF- oder GLB-Datei."); }
        using (doc)
        {
            var r = doc.RootElement;
            int Count(string k) => r.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array ? a.GetArrayLength() : 0;
            var acc = r.TryGetProperty("accessors", out var aa) ? aa.EnumerateArray().Select(a => a.TryGetProperty("count", out var c) ? c.GetInt64() : 0).ToList() : [];
            long Acc(JsonElement e) => e.ValueKind == JsonValueKind.Number && e.GetInt32() is var i && i >= 0 && i < acc.Count ? acc[i] : 0;
            int prims = 0; long tris = 0, verts = 0;
            if (r.TryGetProperty("meshes", out var ms))
                foreach (var m in ms.EnumerateArray())
                    if (m.TryGetProperty("primitives", out var ps))
                        foreach (var p in ps.EnumerateArray())
                        {
                            prims++;
                            var mode = p.TryGetProperty("mode", out var mo) ? mo.GetInt32() : 4;
                            var v = p.TryGetProperty("attributes", out var at) && at.TryGetProperty("POSITION", out var pos) ? Acc(pos) : 0;
                            var idx = p.TryGetProperty("indices", out var ix) ? Acc(ix) : v;
                            verts += v;
                            tris += mode switch { 4 => idx / 3, 5 or 6 => Math.Max(0, idx - 2), _ => 0 };
                        }
            var gen = r.TryGetProperty("asset", out var asset) && asset.TryGetProperty("generator", out var g) ? g.GetString() ?? "" : "";
            return new(Path.GetFileName(path), bytes.LongLength, Count("meshes"), prims, tris, verts, Count("materials"), Count("textures"), Count("nodes"), Count("animations"), gen);
        }
    }

    /// <summary>Fehler im Modell, die man ohne Zielgerät erkennt (für jedes Ziel gleich): fehlendes Material, keine Geometrie.</summary>
    public static IReadOnlyList<string> Problems(XrModelInfo m)
    {
        var p = new List<string>();
        if (m.Triangles == 0) p.Add("Keine Dreiecke – die Datei enthält keine sichtbare Form.");
        if (m.Primitives > 0 && m.Materials == 0) p.Add("Kein Material – das Modell erscheint grau.");
        return p;
    }
}
