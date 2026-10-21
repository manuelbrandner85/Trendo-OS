using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>X-04: Erweiterungen – nur Daten, kein fremder Programmcode. Installieren aus Ordner oder ZIP, prüfen, ein-/ausschalten, entfernen.</summary>
public sealed record ExtManifest(string Id, string Name, string Version, string Author, string Description, string Kind, string[] Permissions, string Entry, string License);
public sealed record InstalledExt(ExtManifest Manifest, string Dir, bool Enabled, string Sha256);

public static partial class Extensions
{
    public const string ManifestFile = "trendo-extension.json";
    /// <summary>seite = Baukasten-Vorlage (JSON), vorhaben = Schrittliste (Markdown), links = Linkliste (JSON), text = Textbausteine (Markdown).</summary>
    public static readonly string[] Kinds = ["seite", "vorhaben", "links", "text"];
    public static readonly string[] Permissions = ["dateien-lesen", "internet-links"];
    static readonly string[] AllowedExt = [".json", ".md", ".txt", ".png", ".jpg", ".jpeg", ".svg", ".webp", ".css"];

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-\.]{2,63}$")] private static partial Regex IdRx();
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")] private static partial Regex VerRx();

    public static (ExtManifest? M, List<string> Errors) Validate(string dir)
    {
        var err = new List<string>();
        var mf = Path.Combine(dir, ManifestFile);
        if (!File.Exists(mf)) return (null, ["Keine Datei " + ManifestFile + " gefunden."]);
        ExtManifest? m;
        try { m = JsonSerializer.Deserialize<ExtManifest>(File.ReadAllText(mf), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException e) { return (null, ["Beschreibung ist kein gültiges JSON: " + e.Message]); }
        if (m is null) return (null, ["Beschreibung ist leer."]);
        if (m.Id is null || !IdRx().IsMatch(m.Id)) err.Add("Kennung ungültig (nur a–z, 0–9, - und .).");
        if (string.IsNullOrWhiteSpace(m.Name)) err.Add("Name fehlt.");
        if (m.Version is null || !VerRx().IsMatch(m.Version)) err.Add("Version muss wie 1.0.0 aussehen.");
        if (!Kinds.Contains(m.Kind)) err.Add("Unbekannte Art: " + m.Kind);
        foreach (var p in m.Permissions ?? []) if (!Permissions.Contains(p)) err.Add("Nicht erlaubtes Recht: " + p);
        if (m.Entry is null || !FreeAssets.SafeRel(m.Entry) || !File.Exists(Path.Combine(dir, m.Entry))) err.Add("Einstiegsdatei fehlt.");
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (Path.GetFileName(f) is not (ManifestFile or ".aus") && !AllowedExt.Contains(ext)) err.Add($"Datei nicht erlaubt (kein Programmcode): {Path.GetRelativePath(dir, f)}");
            if (new FileInfo(f).LinkTarget is not null) err.Add("Verknüpfungen sind nicht erlaubt: " + Path.GetFileName(f));
        }
        if (err.Count == 0 && m.Kind is "seite" or "links")
            try { JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, m.Entry!))).Dispose(); } catch (JsonException) { err.Add("Einstiegsdatei ist kein gültiges JSON."); }
        return (m, err);
    }

    public static string Hash(string dir)
    {
        using var sha = SHA256.Create();
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != ".aus").OrderBy(f => Path.GetRelativePath(dir, f), StringComparer.Ordinal))
        {
            var name = System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(dir, f).Replace('\\', '/'));
            sha.TransformBlock(name, 0, name.Length, null, 0);
            var b = File.ReadAllBytes(f); sha.TransformBlock(b, 0, b.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}

public sealed class ExtensionStore(string dataRoot, AuditLog? audit = null)
{
    public string Root { get; } = Path.Combine(dataRoot, "erweiterungen");

    /// <summary>Installiert aus Ordner oder .zip. ZIP-Einträge, die aus dem Ordner ausbrechen, werden abgewiesen.</summary>
    public (InstalledExt? Ext, List<string> Errors) Install(string source)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-ext-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(tmp);
                using var z = ZipFile.OpenRead(source);
                long total = 0;
                foreach (var e in z.Entries)
                {
                    if (e.FullName.EndsWith('/')) continue;
                    if (!FreeAssets.SafeRel(e.FullName)) return (null, ["Unsicherer Pfad im ZIP: " + e.FullName]);
                    if ((total += e.Length) > 50L << 20) return (null, ["Erweiterung ist größer als 50 MB."]);
                    var t = Path.Combine(tmp, e.FullName); Directory.CreateDirectory(Path.GetDirectoryName(t)!); e.ExtractToFile(t);
                }
                var inner = Directory.GetDirectories(tmp); if (!File.Exists(Path.Combine(tmp, Extensions.ManifestFile)) && inner.Length == 1) source = inner[0]; else source = tmp;
            }
            if (!Directory.Exists(source)) return (null, ["Ordner oder ZIP nicht gefunden."]);
            var (m, err) = Extensions.Validate(source);
            if (m is null || err.Count > 0) return (null, err);
            var dst = Path.Combine(Root, m.Id);
            if (Directory.Exists(dst)) Directory.Delete(dst, true);
            CopyDir(source, dst);
            audit?.Append("app", "ext.install", $"{m.Id} {m.Version}");
            return (Get(m.Id), []);
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
    }

    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)));
        foreach (var sub in Directory.GetDirectories(s)) CopyDir(sub, Path.Combine(d, Path.GetFileName(sub)));
    }

    public InstalledExt? Get(string id)
    {
        var d = Path.Combine(Root, id); if (!Directory.Exists(d)) return null;
        var (m, err) = Extensions.Validate(d); if (m is null || err.Count > 0) return null;
        return new(m, d, !File.Exists(Path.Combine(d, ".aus")), Extensions.Hash(d));
    }

    public IReadOnlyList<InstalledExt> All() => Directory.Exists(Root) ? Directory.GetDirectories(Root).Select(d => Get(Path.GetFileName(d))).OfType<InstalledExt>().OrderBy(e => e.Manifest.Name).ToList() : [];
    public void SetEnabled(string id, bool on) { var f = Path.Combine(Root, id, ".aus"); if (on) File.Delete(f); else File.WriteAllText(f, ""); audit?.Append("app", "ext." + (on ? "on" : "off"), id); }
    public void Remove(string id) { var d = Path.Combine(Root, id); if (Directory.Exists(d)) Directory.Delete(d, true); audit?.Append("app", "ext.remove", id); }
    public string EntryText(InstalledExt e) => File.ReadAllText(Path.Combine(e.Dir, e.Manifest.Entry));
}

public static class ExtensionSamples
{
    /// <summary>Legt eine Beispiel-Erweiterung (Baukasten-Vorlage „Visitenkarte“) an – zum Ausprobieren und als Muster für eigene.</summary>
    public static string WriteSample(string parent)
    {
        var d = Path.Combine(parent, "visitenkarte"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, Extensions.ManifestFile), JsonSerializer.Serialize(new ExtManifest("trendo.visitenkarte", "Visitenkarte", "1.0.0", "Trendo", "Eine Seite mit Name, kurzer Vorstellung und Kontakt.", "seite", [], "seite.json", "CC0-1.0"), new JsonSerializerOptions { WriteIndented = true }));
        new SitePage { Title = "Visitenkarte", Blocks = [new("h1", "Dein Name"), new("p", "Was du machst – in zwei Sätzen."), new("list", "Fähigkeit 1\nFähigkeit 2\nFähigkeit 3"), new("contact", "Schreib mir", "du@example.org")] }.Save(Path.Combine(d, "seite.json"));
        return d;
    }
}
