using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>X-07: Freie Assets finden – Quellen mit klarer Lizenz, Poly-Haven-Katalog (CC0) lesen, Download-Plan mit Prüfsumme und Lizenzdatei.</summary>
public sealed record AssetSource(string Name, string Url, string License, string What);
public sealed record AssetHit(string Id, string Name, string Type, string[] Categories, int Downloads);
public sealed record AssetFile(string RelPath, string Url, string Md5, long Size);

public static class FreeAssets
{
    public static readonly IReadOnlyList<AssetSource> Sources =
    [
        new("Poly Haven", "https://polyhaven.com", "CC0", "3D-Modelle, Texturen, Himmel (HDRI) – direkt in Trendo"),
        new("ambientCG", "https://ambientcg.com", "CC0", "Texturen und Materialien"),
        new("Kenney", "https://kenney.nl/assets", "CC0", "Spielgrafiken, Töne, Schriften"),
        new("OpenGameArt", "https://opengameart.org", "je Datei verschieden (CC0, CC-BY, GPL)", "Spielgrafiken und Musik – Lizenz je Datei prüfen"),
        new("Freesound", "https://freesound.org", "je Datei verschieden (CC0, CC-BY)", "Geräusche – kostenloses Konto nötig"),
        new("Google Fonts", "https://fonts.google.com", "OFL / Apache", "Schriften"),
        new("Openverse", "https://openverse.org", "CC-Lizenzen", "Fotos und Bilder"),
    ];

    public static readonly (int Code, string Api, string Label)[] Types = [(0, "hdris", "Himmel (HDRI)"), (1, "textures", "Texturen"), (2, "models", "3D-Modelle")];

    public static string ListUrl(string api) => "https://api.polyhaven.com/assets?t=" + Uri.EscapeDataString(api);
    public static string FilesUrl(string id) => "https://api.polyhaven.com/files/" + Uri.EscapeDataString(id);

    public static IReadOnlyList<AssetHit> ParseList(string json, string query, int max = 30)
    {
        using var d = JsonDocument.Parse(json);
        var q = GlobalSearch.Norm(query);
        var hits = new List<AssetHit>();
        foreach (var p in d.RootElement.EnumerateObject())
        {
            var e = p.Value;
            var name = e.TryGetProperty("name", out var n) ? n.GetString() ?? p.Name : p.Name;
            var cats = e.TryGetProperty("categories", out var c) ? c.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [];
            var tags = e.TryGetProperty("tags", out var t) ? t.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [];
            var hay = GlobalSearch.Norm(p.Name + " " + name + " " + string.Join(' ', cats) + " " + string.Join(' ', tags));
            if (q.Length > 0 && !q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(hay.Contains)) continue;
            var type = e.TryGetProperty("type", out var ty) ? Types.FirstOrDefault(x => x.Code == ty.GetInt32()).Api ?? "?" : "?";
            hits.Add(new(p.Name, name, type, cats, e.TryGetProperty("download_count", out var dc) ? dc.GetInt32() : 0));
        }
        return hits.OrderByDescending(h => h.Downloads).Take(max).ToList();
    }

    /// <summary>Welche Dateien geladen werden (1k-Auflösung): HDRI als .hdr, Modelle/Texturen als glTF samt Texturen.</summary>
    public static IReadOnlyList<AssetFile> Plan(string filesJson, string type, string res = "1k")
    {
        using var d = JsonDocument.Parse(filesJson);
        var r = d.RootElement; var list = new List<AssetFile>();
        AssetFile F(string rel, JsonElement e) => new(rel, e.GetProperty("url").GetString()!, e.TryGetProperty("md5", out var m) ? m.GetString() ?? "" : "", e.TryGetProperty("size", out var s) ? s.GetInt64() : 0);
        if (type == "hdris")
        {
            var h = r.GetProperty("hdri").GetProperty(res).GetProperty("hdr");
            list.Add(F(Path.GetFileName(new Uri(h.GetProperty("url").GetString()!).AbsolutePath), h));
        }
        else
        {
            var g = r.GetProperty("gltf").GetProperty(res).GetProperty("gltf");
            list.Add(F(Path.GetFileName(new Uri(g.GetProperty("url").GetString()!).AbsolutePath), g));
            if (g.TryGetProperty("include", out var inc)) foreach (var i in inc.EnumerateObject()) list.Add(F(i.Name, i.Value));
        }
        foreach (var f in list)
            if (!SafeRel(f.RelPath) || !f.Url.StartsWith("https://", StringComparison.Ordinal)) throw new InvalidDataException("Unsicherer Dateipfad im Katalog: " + f.RelPath);
        return list;
    }

    public static bool SafeRel(string rel) =>
        rel.Length > 0 && !Path.IsPathRooted(rel) && !rel.Contains(':') && rel.Replace('\\', '/').Split('/').All(s => s is not ("" or "." or ".."));

    public static bool Md5Ok(string file, string md5) =>
        md5.Length == 0 || Convert.ToHexString(MD5.HashData(File.ReadAllBytes(file))).Equals(md5, StringComparison.OrdinalIgnoreCase);

    /// <summary>T-0735: Art aus der Dateiliste von Poly Haven – HDRI haben „hdri“, Modelle und Texturen „gltf“.</summary>
    public static string TypeOf(string filesJson)
    {
        using var d = JsonDocument.Parse(filesJson);
        return d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("hdri", out _) ? "hdris" : "models";
    }

    /// <summary>Gültige Poly-Haven-Kennung (z. B. „rock_09“): nur Kleinbuchstaben, Ziffern, Unterstrich und Bindestrich.</summary>
    public static bool ValidId(string? id) => id is { Length: > 0 and <= 80 } && id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');

    /// <summary>Ein geladenes Stück mit Beleg: Adresse, Größe, SHA-256 und (falls der Katalog sie nennt) MD5.</summary>
    public sealed record FetchedFile(string RelPath, string Url, long Size, string Sha256, string Md5);

    /// <summary>T-0735: Lizenz- und Quellenbeleg je Datei – lesbarer Text (HERKUNFT.txt) und maschinenlesbar (herkunft.json).</summary>
    public static (string Text, string Json) Provenance(string id, string name, IReadOnlyList<FetchedFile> files, DateTimeOffset at, string source = "Poly Haven", string license = "CC0 1.0")
    {
        var page = "https://polyhaven.com/a/" + id;
        var sb = new System.Text.StringBuilder($"Herkunft: {name} ({id})\nQuelle: {source} – {page}\nLizenz: {license} – frei nutzbar, auch kommerziell, ohne Namensnennung (https://creativecommons.org/publicdomain/zero/1.0/deed.de)\nGeladen: {at:yyyy-MM-dd HH:mm zzz}\n\nDateien:\n");
        foreach (var f in files) sb.Append($"- {f.RelPath}\n  Adresse: {f.Url}\n  Größe: {f.Size} Bytes\n  SHA-256: {f.Sha256}\n  Lizenz: {license} · Quelle: {page}\n" + (f.Md5.Length > 0 ? $"  MD5 laut Katalog: {f.Md5} (geprüft)\n" : ""));
        var json = JsonSerializer.Serialize(new
        {
            asset = id, name, source, page, license, licenseUrl = "https://creativecommons.org/publicdomain/zero/1.0/", loaded = at.ToString("O"),
            files = files.Select(f => new { path = f.RelPath, url = f.Url, size = f.Size, sha256 = f.Sha256, md5 = f.Md5, license, source = page }),
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return (sb.ToString(), json);
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string Sha256File(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

    public static bool Md5Ok(byte[] bytes, string md5) =>
        md5.Length == 0 || Convert.ToHexString(MD5.HashData(bytes)).Equals(md5, StringComparison.OrdinalIgnoreCase);

    public static void WriteLicense(string dir, string id, string name, string source = "Poly Haven", string license = "CC0 1.0")
        => AtomicFile.WriteAllText(Path.Combine(dir, "LIZENZ.txt"), $"{name} ({id})\nQuelle: {source} – https://polyhaven.com/a/{id}\nLizenz: {license} – frei nutzbar, auch kommerziell, ohne Namensnennung.\nGeladen: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}\n");
}
