using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>X-04 Katalog: Liste von Erweiterungen (nur Daten) mit Prüfsumme. Quellen: mitgelieferter lokaler Katalog oder ein GitHub-Katalog.
/// Core lädt nichts aus dem Netz (QA-019) – das Herunterladen macht die App, Core prüft nur.</summary>
public sealed record CatalogEntry(string Id, string Name, string Version, string Kind, string Description, string Url, string Sha256);

public static class ExtensionCatalog
{
    public const string FileName = "katalog.json";
    static readonly string[] GitHubHosts = ["github.com", "raw.githubusercontent.com", "objects.githubusercontent.com"];
    static readonly JsonSerializerOptions Opt = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    /// <summary>Erlaubt: https auf GitHub-Adressen oder ein relativer Pfad neben dem Katalog (lokal). Alles andere wird abgewiesen.</summary>
    public static bool IsRemote(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && GitHubHosts.Contains(u.Host);
    public static bool UrlAllowed(string url) => IsRemote(url) || (!Uri.TryCreate(url, UriKind.Absolute, out _) && FreeAssets.SafeRel(url) && url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    public static (List<CatalogEntry> Entries, List<string> Errors) Parse(string json)
    {
        var err = new List<string>(); List<CatalogEntry>? list;
        try { list = JsonSerializer.Deserialize<List<CatalogEntry>>(json, Opt); }
        catch (JsonException e) { return ([], ["Katalog ist kein gültiges JSON: " + e.Message]); }
        var ok = new List<CatalogEntry>();
        foreach (var e in list ?? [])
        {
            var why = e.Id is null || e.Name is null || e.Version is null ? "Angaben fehlen"
                : !Extensions.Kinds.Contains(e.Kind) ? "unbekannte Art " + e.Kind
                : e.Url is null || !UrlAllowed(e.Url) ? "Adresse nicht erlaubt (nur GitHub über https oder lokale ZIP)"
                : e.Sha256 is null || e.Sha256.Length != 64 || !e.Sha256.All(Uri.IsHexDigit) ? "Prüfsumme fehlt oder ist ungültig" : null;
            if (why is null) ok.Add(e); else err.Add($"{e.Id ?? "?"}: {why}");
        }
        return (ok, err);
    }

    public static string FileSha256(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

    /// <summary>Lokale Datei eines Eintrags (für relative Adressen), sonst null.</summary>
    public static string? LocalFile(string catalogDir, CatalogEntry e) => IsRemote(e.Url) ? null : Path.GetFullPath(Path.Combine(catalogDir, e.Url));

    /// <summary>Installiert nur, wenn die Prüfsumme der ZIP stimmt und Kennung/Version zum Katalogeintrag passen.</summary>
    public static (InstalledExt? Ext, List<string> Errors) InstallVerified(ExtensionStore store, CatalogEntry e, string zip)
    {
        if (!File.Exists(zip)) return (null, ["Datei nicht gefunden."]);
        var h = FileSha256(zip);
        if (!string.Equals(h, e.Sha256, StringComparison.OrdinalIgnoreCase)) return (null, [$"Prüfsumme stimmt nicht (erwartet {e.Sha256[..12]}…, erhalten {h[..12]}…). Nicht installiert."]);
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-cat-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            using (var z = ZipFile.OpenRead(zip))
            {
                var mf = z.Entries.FirstOrDefault(x => x.FullName == Extensions.ManifestFile || x.FullName.EndsWith("/" + Extensions.ManifestFile));
                if (mf is null) return (null, ["Keine Beschreibung in der ZIP."]);
                var m = JsonSerializer.Deserialize<ExtManifest>(new StreamReader(mf.Open()).ReadToEnd(), Opt);
                if (m?.Id != e.Id || m.Version != e.Version) return (null, ["Inhalt passt nicht zum Katalogeintrag (Kennung/Version)."]);
            }
            return store.Install(zip);
        }
        catch (JsonException) { return (null, ["Beschreibung in der ZIP ist ungültig."]); }
        finally { Directory.Delete(tmp, true); }
    }

    /// <summary>Baut den mitgelieferten lokalen Katalog (Beispiel-Erweiterungen als ZIP + katalog.json mit Prüfsummen). Nichts wird veröffentlicht.</summary>
    public static string BuildLocal(string dir)
    {
        Directory.CreateDirectory(dir);
        var src = Path.Combine(dir, "quellen"); if (Directory.Exists(src)) Directory.Delete(src, true);
        var dirs = new List<string> { ExtensionSamples.WriteSample(src), WriteTextSample(src), WriteLinksSample(src) };
        var list = new List<CatalogEntry>();
        foreach (var d in dirs)
        {
            var (m, err) = Extensions.Validate(d); if (m is null || err.Count > 0) continue;
            var zip = Path.Combine(dir, m.Id + "-" + m.Version + ".zip"); File.Delete(zip);
            ZipFile.CreateFromDirectory(d, zip);
            list.Add(new(m.Id, m.Name, m.Version, m.Kind, m.Description, Path.GetFileName(zip), FileSha256(zip)));
        }
        Directory.Delete(src, true);
        var f = Path.Combine(dir, FileName); File.WriteAllText(f, JsonSerializer.Serialize(list, Opt)); return f;
    }

    static string WriteTextSample(string parent)
    {
        var d = Path.Combine(parent, "angebot"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, Extensions.ManifestFile), JsonSerializer.Serialize(new ExtManifest("trendo.angebotstexte", "Angebotstexte", "1.0.0", "Trendo", "Textbausteine für Angebot, Rechnungshinweis und Absage.", "text", [], "texte.md", "CC0-1.0"), Opt));
        File.WriteAllText(Path.Combine(d, "texte.md"), "# Angebot\nVielen Dank für Ihre Anfrage. Gern biete ich Ihnen an: …\n\n# Freundliche Absage\nDanke für Ihr Interesse – diesmal passt es leider nicht.\n");
        return d;
    }

    static string WriteLinksSample(string parent)
    {
        var d = Path.Combine(parent, "lernen"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, Extensions.ManifestFile), JsonSerializer.Serialize(new ExtManifest("trendo.lernlinks", "Kostenlos lernen", "1.0.0", "Trendo", "Freie Lernseiten für Web, Grafik und 3D.", "links", ["internet-links"], "links.json", "CC0-1.0"), Opt));
        File.WriteAllText(Path.Combine(d, "links.json"), JsonSerializer.Serialize(new Dictionary<string, string> { ["MDN Web Docs"] = "https://developer.mozilla.org/de/", ["Blender Handbuch"] = "https://docs.blender.org/manual/de/latest/" }));
        return d;
    }
}
