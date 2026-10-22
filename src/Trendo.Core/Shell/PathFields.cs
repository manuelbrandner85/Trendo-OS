using System.Text.RegularExpressions;

namespace Trendo.Core.Shell;

/// <summary>T-0201: erkennt Datei- und Ordnerfelder und leitet den passenden Dateityp-Filter ab.</summary>
public static class PathFields
{
    public enum Kind { None, File, Folder, FileOrFolder }

    private static readonly Regex NotPath = new(@"(url|pattern|find|replace|args|goal|^bm-folder$)", RegexOptions.IgnoreCase);
    private static readonly Regex Suffix = new(@"-(file\d?|files|folder|path|csv|zip|exe|src|import|doc|img)$", RegexOptions.IgnoreCase);
    private static readonly Regex FileName = new(@"-(file\d?|files|csv|zip|exe|import|path|doc|img)$", RegexOptions.IgnoreCase);

    /// <summary>Feldart aus Name und Platzhalter. Adressen (https://), Muster und Parameter sind keine Pfadfelder. Bilddateien bleiben Pfadfelder, auch wenn der Platzhalter eine https-Adresse erlaubt.</summary>
    public static Kind Classify(string? name, string? hint)
    {
        name ??= ""; hint ??= "";
        var n = name.Trim();
        var h = hint.Trim().ToLowerInvariant();
        var nl = n.ToLowerInvariant();
        if (n.Length == 0 && h.Length == 0) return Kind.None;
        if (nl is "txt-search" or "startmenu-search" or "ki-input" or "txt-studio-search") return Kind.None;
        if (nl is "bm-folder" or "sc-args" or "wfe-pattern" or "ren-pattern" or "attachments" or "letter-attachments" or "wfe-text") return Kind.None;
        if (nl.Contains("attachment")) return Kind.None;
        if (h.Contains("prüfsumme") || h.Contains("pruefsumme") || h.Contains("muster") || h.Contains("parameter") || h.Contains("ordner (optional)")) return Kind.None;
        var image = h.Contains("bilddatei") || h.Contains("bild für") || h.Contains("bild der") || h.Contains("bild zum") || h.Contains("oder bild") || h.Contains("vorhandenes bild");
        if (!image && (NotPath.IsMatch(n) || h.Contains("http") || h.StartsWith("adresse", StringComparison.Ordinal))) return Kind.None;
        if (image && (h.Contains("http") || nl.Contains("url"))) return Kind.File;
        bool file = h.Contains("datei") || h.Contains("pfad") || h.Contains("hauptdokument") || Word(h, "dokument") || Word(h, "fassung") || h.Contains("anlagen")
            || (h.Contains("vorlage") && (h.Contains("word") || h.Contains("platzhalter") || h.Contains(".docx")))
            || FileName.IsMatch(n) || nl.Contains("-file-");
        bool folder = !image && (h.Contains("ordner") || h.Contains("verzeichnis") || Regex.IsMatch(n, @"-folder$", RegexOptions.IgnoreCase) || nl.Contains("-folder-"));
        if (!file && !folder && !Suffix.IsMatch(n)) return Kind.None;
        if (folder && file) return h.Contains("mit den dateien") || h.Contains("mit installationsdateien") || h.Contains("mit fotos") ? Kind.Folder : Kind.FileOrFolder;
        if (Regex.IsMatch(n, @"-src$", RegexOptions.IgnoreCase)) return Kind.FileOrFolder;
        return folder ? Kind.Folder : Kind.File;
    }

    private static bool Word(string text, string wort) => Regex.IsMatch(text, $@"\b{Regex.Escape(wort)}\b", RegexOptions.CultureInvariant);

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
