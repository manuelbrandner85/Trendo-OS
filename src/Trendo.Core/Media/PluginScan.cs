using System.Text;
using System.Text.Json;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-06 (06.11.2026): Plugin-Prüfung ohne Laden – VST3- und CLAP-Plugins in den üblichen Ordnern finden,
/// Name, Hersteller, Version und Architektur aus den Beschreibungsdateien lesen (VST3 „moduleinfo.json“). Es wird kein Plugin-Code ausgeführt.
/// Laden und Ton durchschicken: <see cref="PluginHost"/> (nur freigegeben, nur abgeschottet).</summary>
public static class PluginScan
{
    public sealed record Plugin(string Format, string Name, string Vendor, string Version, string Category, string Path, string Arch, string Status);

    /// <summary>Übliche Ordner unter Windows (nur lesen).</summary>
    public static IEnumerable<string> DefaultRoots()
    {
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (common.Length > 0) { yield return Path.Combine(common, "VST3"); yield return Path.Combine(common, "CLAP"); }
        if (local.Length > 0) { yield return Path.Combine(local, "Programs", "Common", "VST3"); yield return Path.Combine(local, "Programs", "Common", "CLAP"); }
    }

    /// <summary>Ordner durchsuchen (höchstens 4 Ebenen, höchstens 2000 Einträge). Liest nur Dateien, startet nichts.</summary>
    public static List<Plugin> Scan(IEnumerable<string> roots)
    {
        var found = new List<Plugin>(); var opt = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4, IgnoreInaccessible = true };
        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var e in Directory.EnumerateFileSystemEntries(root, "*.vst3", opt).Take(2000))
                if (!InsideBundle(root, e)) found.Add(Vst3(e)); // die Programmdatei im Bündel heißt auch *.vst3
            foreach (var e in Directory.EnumerateFiles(root, "*.clap", opt).Take(2000)) found.Add(Clap(e));
        }
        return [.. found.GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(p => p.Format).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }

    static bool InsideBundle(string root, string entry)
    {
        for (var d = System.IO.Path.GetDirectoryName(entry); d is not null && d.Length > root.Length; d = System.IO.Path.GetDirectoryName(d))
            if (d.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string NameOf(string path) => System.IO.Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/'));

    static Plugin Vst3(string path)
    {
        if (File.Exists(path)) // alte Form: einzelne DLL mit Endung .vst3 – ohne Beschreibung
            return new("VST3", NameOf(path), "", "", "", path, PeArch(path), "ohne Beschreibung (alte Einzeldatei) – Angaben erst beim Laden");
        var contents = System.IO.Path.Combine(path, "Contents");
        var arch = Directory.Exists(System.IO.Path.Combine(contents, "x86_64-win")) ? "x64" : Directory.Exists(System.IO.Path.Combine(contents, "arm64-win")) || Directory.Exists(System.IO.Path.Combine(contents, "arm64ec-win")) ? "ARM64" : Directory.Exists(System.IO.Path.Combine(contents, "x86-win")) ? "x86 (32 Bit)" : "unbekannt";
        var info = System.IO.Path.Combine(contents, "Resources", "moduleinfo.json");
        if (!File.Exists(info)) info = System.IO.Path.Combine(contents, "moduleinfo.json");
        if (!File.Exists(info)) return new("VST3", NameOf(path), "", "", "", path, arch, arch == "unbekannt" ? "defekt: weder Programmdatei noch Beschreibung" : "ohne moduleinfo.json – Angaben erst beim Laden");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(info), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var r = doc.RootElement;
            string S(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var name = S(r, "Name"); var version = S(r, "Version");
            var vendor = r.TryGetProperty("Factory Info", out var fi) ? S(fi, "Vendor") : "";
            var cat = r.TryGetProperty("Classes", out var cl) && cl.ValueKind == JsonValueKind.Array && cl.GetArrayLength() > 0 ? S(cl[0], "Sub Categories") is { Length: > 0 } sc ? sc : (cl[0].TryGetProperty("Sub Categories", out var sca) && sca.ValueKind == JsonValueKind.Array ? string.Join("|", sca.EnumerateArray().Select(x => x.GetString())) : "") : "";
            var status = arch is "x64" or "ARM64" ? "ok" : arch.StartsWith("x86") ? "nur 32 Bit – passt nicht zu Trendo (64 Bit)" : "Architektur unbekannt";
            return new("VST3", name.Length > 0 ? name : NameOf(path), vendor, version, cat, path, arch, status);
        }
        catch (JsonException ex) { return new("VST3", NameOf(path), "", "", "", path, arch, "defekt: moduleinfo.json nicht lesbar (" + ex.Message.Split('.')[0] + ")"); }
    }

    static Plugin Clap(string path)
    {
        var arch = PeArch(path);
        return new("CLAP", NameOf(path), "", "", "", path, arch, arch == "x64" || arch == "ARM64" ? "ok (Angaben erst beim Laden)" : arch.StartsWith("x86") ? "nur 32 Bit – passt nicht zu Trendo (64 Bit)" : "defekt: keine Windows-Programmdatei");
    }

    /// <summary>Architektur aus dem PE-Kopf lesen (nur die ersten Bytes, nichts wird ausgeführt).</summary>
    public static string PeArch(string file)
    {
        try
        {
            using var f = File.OpenRead(file); var b = new byte[4096]; var n = f.Read(b, 0, b.Length);
            if (n < 64 || b[0] != 'M' || b[1] != 'Z') return "unbekannt";
            var pe = BitConverter.ToInt32(b, 0x3C); if (pe < 0 || pe + 6 > n || b[pe] != 'P' || b[pe + 1] != 'E') return "unbekannt";
            return BitConverter.ToUInt16(b, pe + 4) switch { 0x8664 => "x64", 0xAA64 => "ARM64", 0x14C => "x86 (32 Bit)", _ => "unbekannt" };
        }
        catch (IOException) { return "unbekannt"; }
        catch (UnauthorizedAccessException) { return "unbekannt"; }
    }

    public static string Report(IReadOnlyList<Plugin> list, IEnumerable<string> roots)
    {
        var sb = new StringBuilder($"# Plugin-Prüfung\n\nErstellt: {DateTime.Now:dd.MM.yyyy HH:mm} · Durchsuchte Ordner: {string.Join(", ", roots)}\n\n");
        sb.Append("Geprüft wurde ohne Laden: Trendo liest nur Ordner und Beschreibungsdateien. Laden ist erst nach Prüfung und Freigabe je Plugin möglich (Audioeditor → „Plugins“), und nur abgeschottet.\n\n");
        if (list.Count == 0) { sb.Append("Keine VST3- oder CLAP-Plugins gefunden.\n"); return sb.ToString(); }
        sb.Append("| Format | Name | Hersteller | Version | Art | Architektur | Status |\n|---|---|---|---|---|---|---|\n");
        foreach (var p in list) sb.Append($"| {p.Format} | {p.Name} | {p.Vendor} | {p.Version} | {p.Category} | {p.Arch} | {p.Status} |\n");
        return sb.ToString();
    }
}
