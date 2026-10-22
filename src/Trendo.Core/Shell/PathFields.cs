using System.Text.RegularExpressions;

namespace Trendo.Core.Shell;

/// <summary>T-0201: erkennt Datei- und Ordnerfelder und leitet den passenden Dateityp-Filter ab.</summary>
public static class PathFields
{
    public enum Kind { None, File, Folder, FileOrFolder }

    private static readonly Regex NotPath = new(@"(url|pattern|find|replace|args|goal|^bm-folder$)", RegexOptions.IgnoreCase);
    private static readonly Regex Suffix = new(@"-(file\d?|files|folder|path|csv|zip|exe|src|import)$", RegexOptions.IgnoreCase);

    /// <summary>Feldart aus Name und Platzhalter. Adressen (https://), Muster und Parameter sind keine Pfadfelder.</summary>
    public static Kind Classify(string? name, string? hint)
    {
        name ??= ""; hint ??= "";
        if (name is "txt-search" or "startmenu-search" or "ki-input" || NotPath.IsMatch(name) || hint.Contains("http", StringComparison.OrdinalIgnoreCase) || hint.StartsWith("Adresse", StringComparison.OrdinalIgnoreCase)) return Kind.None;
        var h = hint.ToLowerInvariant();
        bool file = h.Contains("datei") || h.Contains("pfad") || Regex.IsMatch(name, @"-(file\d?|files|csv|zip|exe|import|path)$", RegexOptions.IgnoreCase);
        bool folder = h.Contains("ordner") || h.Contains("verzeichnis") || Regex.IsMatch(name, @"-folder$", RegexOptions.IgnoreCase);
        if (!file && !folder && !Suffix.IsMatch(name)) return Kind.None;
        if (folder && file) return h.Contains("mit den dateien") || h.Contains("mit installationsdateien") || h.Contains("mit fotos") ? Kind.Folder : Kind.FileOrFolder;
        if (Regex.IsMatch(name, @"-src$", RegexOptions.IgnoreCase)) return Kind.FileOrFolder;
        return folder ? Kind.Folder : Kind.File;
    }

    /// <summary>Mehrere Dateien (eine pro Zeile)?</summary>
    public static bool Multiple(string? name, string? hint) => (name ?? "").EndsWith("-files") || (hint ?? "").Contains("eine pro Zeile");

    private static readonly Dictionary<string, string[]> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bilddatei"] = ["png", "jpg", "jpeg", "webp", "bmp", "gif"], ["Bild"] = ["png", "jpg", "jpeg", "webp", "bmp", "gif"],
        ["Audiodatei"] = ["mp3", "wav", "ogg", "flac", "m4a"], ["Videodatei"] = ["mp4", "mkv", "mov", "webm", "avi"],
        ["Programmdatei"] = ["exe", "bat", "cmd", "lnk", "sh", "AppImage"], ["Lesezeichen"] = ["html", "htm"],
        ["Wiederherstellungsliste"] = ["json"], ["Bank-CSV"] = ["csv"],
    };
    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    { "csv", "xlsx", "gpx", "srt", "zip", "ics", "pdf", "json", "html", "htm", "glb", "gltf", "obj", "stl", "fbx", "blend", "ply", "xyz", "pcd", "vcf", "eml", "txt", "md", "png", "jpg", "jpeg", "gcode", "nc", "mp3", "mp4", "wav", "docx", "odt" };

    /// <summary>Dateiendungen (ohne Punkt) aus dem Platzhalter, z. B. „CSV- oder XLSX-Datei“ → csv, xlsx. Leer = alle Dateien.</summary>
    public static IReadOnlyList<string> Extensions(string? hint)
    {
        var r = new List<string>();
        foreach (Match m in Regex.Matches(hint ?? "", @"[\p{L}0-9.-]+"))
        {
            var t = m.Value.Trim('.', '-', ',');
            if (Words.TryGetValue(t, out var ws)) r.AddRange(ws);
            foreach (var part in t.Split('-', '.', '/'))
                if (Known.Contains(part) && (part.Length > 2 || part.Equals("md", StringComparison.OrdinalIgnoreCase) || part.Equals("nc", StringComparison.OrdinalIgnoreCase))) r.Add(part.ToLowerInvariant());
        }
        return r.Distinct().ToList();
    }

    /// <summary>Zuletzt benutzte Pfade: neuester zuerst, ohne Doppelte, höchstens <paramref name="max"/>.</summary>
    public static List<string> Remember(IEnumerable<string> list, string path, int max = 8) =>
        new[] { path }.Concat(list.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase))).Take(max).ToList();
}
