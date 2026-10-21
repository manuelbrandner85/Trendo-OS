using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public sealed record XrModelInfo(string File, long Bytes, int Meshes, int Primitives, long Triangles, long Vertices, int Materials, int Textures, int Nodes, int Animations, string Generator);
public sealed record XrBudget(string Device, long MaxTriangles, int MaxDrawCalls, int MaxTextures, long MaxBytes, int Fps);
public sealed record XrCheck(XrBudget Budget, bool Ok, IReadOnlyList<string> Problems);

/// <summary>SPEC-04: VR/AR/MR – 3D-Modelle (glTF/GLB) prüfen, ob sie auf einer Brille flüssig laufen.</summary>
public static class XrInspector
{
    public static readonly IReadOnlyList<XrBudget> Budgets =
    [
        new("Meta Quest 3 / Pico 4 (eigenständig)", 750_000, 150, 40, 100L << 20, 72),
        new("Meta Quest 2 (eigenständig)", 400_000, 100, 30, 60L << 20, 72),
        new("Handy-AR (WebXR / Quick Look)", 300_000, 60, 20, 25L << 20, 60),
        new("PC-VR (Steam VR, starke Grafikkarte)", 3_000_000, 1000, 200, 1L << 30, 90),
    ];

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

    public static XrCheck Check(XrModelInfo m, XrBudget b)
    {
        var p = new List<string>();
        if (m.Triangles > b.MaxTriangles) p.Add($"Zu viele Dreiecke: {Fach.F(m.Triangles, 0)} (Grenze {Fach.F(b.MaxTriangles, 0)}). Modell vereinfachen (Decimate).");
        if (m.Primitives > b.MaxDrawCalls) p.Add($"Zu viele Einzelteile: {m.Primitives} Zeichenaufrufe (Grenze {b.MaxDrawCalls}). Teile mit gleichem Material zusammenfügen.");
        if (m.Textures > b.MaxTextures) p.Add($"Zu viele Texturen: {m.Textures} (Grenze {b.MaxTextures}). Texturen zu einem Atlas bündeln.");
        if (m.Bytes > b.MaxBytes) p.Add($"Datei zu groß: {m.Bytes / 1048576.0:0.#} MB (Grenze {b.MaxBytes >> 20} MB). Texturen verkleinern oder komprimieren.");
        if (m.Primitives > 0 && m.Materials == 0) p.Add("Kein Material – das Modell erscheint grau.");
        return new(b, p.Count == 0, p);
    }

    /// <summary>Zeitbudget pro Bild: bei 72 Hz 13,9 ms. Liegt die Messung darüber, ruckelt es – das macht in VR schnell übel.</summary>
    public static (double BudgetMs, bool Smooth, double Headroom) FrameBudget(int fps, double measuredMs) { var b = 1000.0 / fps; return (Math.Round(b, 2), measuredMs <= b, Math.Round((b - measuredMs) / b * 100, 1)); }
}
