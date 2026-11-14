using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Z3 im Studio „Webseite bauen“: 3D-Baustein „Modell3D“ für eigene GLB-Modelle (auch aus Blender umgewandelt) und Gaussian Splats (Spark, MIT).
/// Darstellung im Browser über three.js: WebGPU, wenn der Browser es kann, sonst WebGL; ohne 3D-Fähigkeit das Standbild.
/// Hier: Prüfung der Dateien (nur Formate und Erweiterungen, die die Seite wirklich laden kann), Auslieferung als Skript-Datei (läuft auch direkt aus dem Ordner),
/// Bündel im Export und Prüfungen für die Website.</summary>
public static class WebDesignModel
{
    public const long MaxBytes = WebDesignAssets.MaxBytes; // wie bei allen Medien (50 MB)
    public const string Block = "Modell3D", Bundle3D = "drei.js", BundleSplat = "splat.js";
    /// <summary>glTF-Erweiterungen, die der Lader der Seite kann. Draco und KTX2 bräuchten zusätzliche Decoder, die Trendo (noch) nicht mitliefert.</summary>
    public static readonly HashSet<string> SupportedExtensions = ["KHR_materials_clearcoat", "KHR_materials_emissive_strength", "KHR_materials_ior", "KHR_materials_iridescence", "KHR_materials_sheen", "KHR_materials_specular",
        "KHR_materials_transmission", "KHR_materials_unlit", "KHR_materials_volume", "KHR_materials_anisotropy", "KHR_materials_dispersion", "KHR_texture_transform", "KHR_mesh_quantization", "EXT_meshopt_compression", "KHR_lights_punctual", "EXT_texture_webp", "KHR_materials_variants"];

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    /// <summary>Datei eines 3D-Bausteins: nur Medien des Projekts der passenden Art.</summary>
    public static bool IsModelPath(string p, string art) =>
        p.StartsWith($"{WebDesignAssets.Folder}/{art}/", StringComparison.Ordinal) && WebDesignAssets.IsMediaPath(p) && WebDesignAssets.Types[art].Contains(Path.GetExtension(p).ToLowerInvariant());

    /// <summary>GLB prüfen: Kopf „glTF“ Version 2, JSON-Abschnitt lesbar, keine Erweiterung, die die Seite nicht laden kann. Ohne passenden Decoder zeigte sie sonst still nichts an.</summary>
    public static string? CheckGlb(string file)
    {
        if (!File.Exists(file)) return "Die Modelldatei fehlt.";
        var len = new FileInfo(file).Length;
        if (len > MaxBytes) return "Das Modell ist zu groß für eine Website (über 50 MB). In Blender Texturen verkleinern oder Polygone reduzieren.";
        using var fs = File.OpenRead(file);
        Span<byte> head = stackalloc byte[20];
        if (fs.Read(head) != 20 || Encoding.ASCII.GetString(head[..4]) != "glTF") return "Keine GLB-Datei (glTF binär).";
        if (BinaryPrimitives.ReadUInt32LittleEndian(head[4..8]) != 2) return "Nur glTF 2.0 wird unterstützt.";
        var jsonLen = BinaryPrimitives.ReadUInt32LittleEndian(head[12..16]);
        if (Encoding.ASCII.GetString(head[16..20]) != "JSON" || jsonLen > 50_000_000 || jsonLen > len) return "Die GLB-Datei ist beschädigt.";
        var buf = new byte[jsonLen]; fs.ReadExactly(buf);
        try
        {
            var o = JsonNode.Parse(buf) as JsonObject;
            var required = (o?["extensionsRequired"] as JsonArray ?? []).Select(x => Str(x)).ToList();
            if (required.FirstOrDefault(r => !SupportedExtensions.Contains(r)) is { } bad)
                return bad switch
                {
                    "KHR_draco_mesh_compression" => "Das Modell ist mit Draco komprimiert. Bitte in Blender ohne Draco (oder mit meshopt) exportieren; sonst zeigt die Seite nichts an.",
                    "KHR_texture_basisu" => "Das Modell nutzt KTX2-Texturen. Bitte mit PNG/JPEG/WebP-Texturen exportieren; sonst zeigt die Seite nichts an.",
                    _ => $"Das Modell braucht die Erweiterung „{bad}“, die die Seite nicht laden kann.",
                };
            if ((o?["buffers"] as JsonArray ?? []).OfType<JsonObject>().Any(b => Str(b["uri"]).Length > 0) || (o?["images"] as JsonArray ?? []).OfType<JsonObject>().Any(i => Str(i["uri"]) is { Length: > 0 } u && !u.StartsWith("data:", StringComparison.Ordinal)))
                return "Das Modell verweist auf Dateien neben sich. Bitte als ein einziges GLB mit eingebetteten Texturen exportieren.";
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { return "Der Aufbau der GLB-Datei ist nicht lesbar."; }
        return null;
    }

    /// <summary>Gaussian Splat prüfen: bekannte Endung, Größe, PLY mit Kopf.</summary>
    public static string? CheckSplat(string file)
    {
        if (!File.Exists(file)) return "Die Splat-Datei fehlt.";
        if (new FileInfo(file).Length > MaxBytes) return "Der Splat ist zu groß für eine Website (über 50 MB). In .spz umwandeln verkleinert ihn stark.";
        if (Path.GetExtension(file).Equals(".ply", StringComparison.OrdinalIgnoreCase))
        {
            using var fs = File.OpenRead(file); var b = new byte[3]; fs.ReadExactly(b);
            if (Encoding.ASCII.GetString(b) != "ply") return "Keine PLY-Datei.";
        }
        return null;
    }

    /// <summary>Alle 3D-Bausteine einer Seite.</summary>
    public static IEnumerable<JsonObject> Blocks(JsonNode? n)
    {
        if (n is JsonArray a) { foreach (var x in a) foreach (var b in Blocks(x)) yield return b; yield break; }
        if (n is not JsonObject o) yield break;
        if (Str(o["type"]) == Block && o["props"] is JsonObject p) yield return p;
        foreach (var kv in o) foreach (var b in Blocks(kv.Value)) yield return b;
    }

    /// <summary>Dateien, die ein 3D-Baustein braucht (Modell oder Splat, Umgebungsbild).</summary>
    public static IEnumerable<string> Files(JsonObject p)
    {
        var src = Str(p["datei"]);
        if (src.Length > 0 && (IsModelPath(src, "modell") || IsModelPath(src, "splat"))) yield return src;
        if (Str(p["umgebung"]) == "hdri" && Str(p["hdri"]) is { Length: > 0 } h && IsModelPath(h, "umgebung")) yield return h;
    }

    /// <summary>Datei als Skript (Base64), damit die Seite sie auch direkt aus dem Ordner (file://) ohne Abruf laden kann.</summary>
    public static string FileScript(string rel, byte[] bytes) =>
        "(window.TRENDO_DATEIEN=window.TRENDO_DATEIEN||{})[" + JsonSerializer.Serialize(rel) + "]=\"" + Convert.ToBase64String(bytes) + "\";window.dispatchEvent(new Event(\"trendo-datei\"));\n";

    /// <summary>Skripte für eine Seite mit 3D-Bausteinen: Bündel (WebGPU/WebGL oder Splats) und die Dateien. Alles liegt nur einmal im Export.</summary>
    public static string Scripts(JsonObject doc, string projectDir, string outDir, string editorDir, List<string>? notes)
    {
        var blocks = Blocks(doc).ToList();
        if (blocks.Count == 0) return "";
        var sb = new StringBuilder();
        foreach (var (bundle, needed) in new[] { (Bundle3D, blocks.Any(b => Str(b["quelle"]) != "splat")), (BundleSplat, blocks.Any(b => Str(b["quelle"]) == "splat")) })
        {
            if (!needed) continue;
            var src = Path.Combine(editorDir, "assets", bundle);
            if (!File.Exists(src)) { notes?.Add($"Das 3D-Bündel {bundle} fehlt im Programm; der 3D-Baustein zeigt nur sein Standbild."); continue; }
            var dest = Path.Combine(outDir, "assets", bundle);
            if (!File.Exists(dest)) File.Copy(src, dest, true);
            sb.Append($"<script defer src=\"assets/{bundle}\"></script>\n");
        }
        foreach (var rel in blocks.SelectMany(Files).Distinct())
        {
            var file = Path.Combine(projectDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file) || new FileInfo(file).Length > MaxBytes) { notes?.Add($"3D-Datei {rel} fehlt oder ist zu groß."); continue; }
            var bytes = File.ReadAllBytes(file);
            var name = "daten/dateien/" + Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant() + ".js";
            var dest = Path.Combine(outDir, name.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(dest)) { Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.WriteAllText(dest, FileScript(rel, bytes)); }
            sb.Append($"<script defer src=\"{name}\"></script>\n");
        }
        return sb.ToString();
    }

    /// <summary>Prüfung je Seite: Datei vorhanden und ladbar, Standbild für Geräte ohne 3D.</summary>
    public static IEnumerable<(string Text, string Ebene)> Check(string projectDir, string name, JsonObject doc)
    {
        foreach (var p in Blocks(doc))
        {
            var src = Str(p["datei"]); var quelle = Str(p["quelle"]) is { Length: > 0 } q ? q : "modell";
            if (src.Length == 0) { yield return ($"{name}: 3D-Baustein ohne Datei. Wähle ein Modell oder einen Splat aus den Medien.", "Fehler"); continue; }
            var file = Path.Combine(projectDir, src.Replace('/', Path.DirectorySeparatorChar));
            var err = !IsModelPath(src, quelle == "splat" ? "splat" : "modell") ? "Die Datei passt nicht zur Art des Bausteins." : quelle == "splat" ? CheckSplat(file) : CheckGlb(file);
            if (err is not null) yield return ($"{name}: {err}", "Fehler");
            if (Str(p["standbild"]).Length == 0) yield return ($"{name}: 3D-Baustein ohne Standbild. Geräte ohne 3D zeigen dann nur einen Hinweis. Im Editor „Standbild aus der Ansicht“ wählen.", "Hinweis");
            if (Str(p["umgebung"]) == "hdri" && !(Str(p["hdri"]) is { Length: > 0 } h && IsModelPath(h, "umgebung") && File.Exists(Path.Combine(projectDir, h.Replace('/', Path.DirectorySeparatorChar)))))
                yield return ($"{name}: Umgebungsbild (HDR) fehlt; die Szene nutzt dann das Studio-Licht.", "Hinweis");
        }
    }
}
