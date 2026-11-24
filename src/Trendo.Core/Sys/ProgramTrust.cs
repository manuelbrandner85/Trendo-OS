using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core.Sys;

/// <summary>T-0764 (3. Prüfrunde P3-1): Vertrauen in ein Programm hängt an seiner Herkunft, nicht am Ordnernamen. Trendo schreibt beim Einrichten
/// (Portable-ZIP, Vollpaket, Zusatzprogramm als ZIP, GitHub-Programm) eine Kennungsdatei mit der SHA-256 jeder Programmdatei (.exe) in den
/// Programmordner. Vertrauenswürdig ist eine .exe nur, wenn sie in einem solchen Ordner steht, dort eingetragen ist und ihr Inhalt noch dieselbe
/// Prüfsumme hat. Eine hineinkopierte oder ausgetauschte .exe zählt nicht. Ältere Kennungen ohne Prüfsummen zählen nicht.
/// Das Ergebnis wird je Pfad, Größe und Änderungszeit zwischengespeichert, damit das Starten schnell bleibt.</summary>
public static class ProgramTrust
{
    sealed record Marker(string? Name, string? Sha256, string? Am, Dictionary<string, string>? Dateien);

    /// <summary>Kennungsdatei schreiben: Name, Prüfsumme der Quelle (ZIP/Download) und SHA-256 aller .exe im Ordner (relativer Pfad → Prüfsumme).</summary>
    public static void WriteMarker(string dir, string name, string sourceSha)
    {
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 8 };
        var files = Directory.EnumerateFiles(dir, "*.exe", opt).Take(2000)
            .ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), Sha, StringComparer.OrdinalIgnoreCase);
        File.WriteAllText(Path.Combine(dir, Software.PortableMarker), JsonSerializer.Serialize(new Marker(name, sourceSha, DateTime.Now.ToString("O"), files)));
    }

    static string Sha(string file) { using var s = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(s)); }

    static readonly ConcurrentDictionary<(string Path, long Len, DateTime At), bool> Cache = new();

    /// <summary>Ist diese .exe ein von Trendo eingerichtetes, unverändertes Programm? Gesucht wird der Programmordner direkt unter einem der
    /// <paramref name="roots"/> (Werkzeugordner, Portable-Ordner); Unterordner „.entfernt“ (Sicherung) zählen nie.</summary>
    public static bool Verified(string exe, IEnumerable<string> roots)
    {
        FileInfo fi;
        try { fi = new FileInfo(Path.GetFullPath(exe)); if (!fi.Exists) return false; }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException) { return false; }
        var key = (fi.FullName.ToUpperInvariant(), fi.Length, fi.LastWriteTimeUtc);
        if (Cache.TryGetValue(key, out var hit)) return hit;
        var ok = Check(fi, roots);
        Cache[key] = ok;
        return ok;
    }

    static bool Check(FileInfo fi, IEnumerable<string> roots)
    {
        foreach (var r in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(r)) + Path.DirectorySeparatorChar;
            if (!fi.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            var rel = fi.FullName[root.Length..];
            var top = rel.Split(Path.DirectorySeparatorChar)[0];
            if (top.Length == 0 || top.StartsWith('.') || rel.Length == top.Length) continue; // kein Programmordner (Datei direkt im Wurzelordner, Sicherung)
            var dir = Path.Combine(root, top);
            if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            var marker = Path.Combine(dir, Software.PortableMarker);
            if (!File.Exists(marker)) continue;
            Marker? m;
            try { m = JsonSerializer.Deserialize<Marker>(File.ReadAllText(marker)); }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { continue; }
            var inner = Path.GetRelativePath(dir, fi.FullName).Replace('\\', '/');
            if (m?.Dateien is not { } list || !list.TryGetValue(inner, out var want)) continue;
            try { if (string.Equals(Sha(fi.FullName), want, StringComparison.OrdinalIgnoreCase)) return true; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return false;
    }

    /// <summary>T-0764 (P3-2): Interpreter und Werkzeuge, mit denen sich beliebiger Code ausführen oder etwas installieren lässt. Im Kinderprofil
    /// nie vertrauenswürdig, wenn Person oder Datei sie wählen – nur bei festen Aufrufen von Trendo selbst.</summary>
    public static readonly HashSet<string> Interpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        "python", "pythonw", "py", "pyw", "uv", "uvx", "pip", "pip3", "git", "git-bash", "bash", "sh", "node", "npm", "npx", "deno", "bun", "7z", "7za", "7zfm",
        "code", "codium", "vscodium", "adb", "fastboot", "powershell", "pwsh", "cmd", "wscript", "cscript", "mshta", "rundll32", "regsvr32", "regedit", "reg",
        "msiexec", "winget", "choco", "scoop", "java", "javaw", "ruby", "perl", "php", "lua", "conhost", "wt", "schtasks", "sc", "bitsadmin", "certutil", "curl", "wget", "ollama",
    };

    public static bool IsInterpreter(string path) => Interpreters.Contains(Path.GetFileNameWithoutExtension(path.Trim().Trim('"')));

    /// <summary>T-0764 (P3-4): Feste Installationsorte für Programme, die Trendo selbst auswählt (z. B. Blender aus der Erkennung): „Programme“
    /// (nur mit Administratorrechten beschreibbar), „%LOCALAPPDATA%\Programs“ und die winget-Ablage im Benutzerordner. Der Werkzeugordner zählt hier
    /// nicht – dort gilt nur <see cref="Verified"/>.</summary>
    public static bool InFixedLocation(string exe)
    {
        string full;
        try { full = Path.GetFullPath(exe.Trim().Trim('"')); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var d in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     local.Length > 0 ? Path.Combine(local, "Programs") : "", local.Length > 0 ? Path.Combine(local, "Microsoft", "WinGet", "Packages") : "" }.Where(d => d.Length > 0))
            if (full.StartsWith(Path.TrimEndingDirectorySeparator(d) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>T-0764 (P3-3): Adressen, die ein Kinderprofil öffnen darf. Alle anderen Schemata (ms-appinstaller:, search-ms:, ms-msdt: …) nicht.</summary>
    public static bool ChildSchemeAllowed(string target)
    {
        var t = target.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return true;
        return t.ToLowerInvariant() is "ms-settings:appsfeatures" or "ms-settings:defaultapps" or "ms-settings:easeofaccess-mousepointer" or "shell:recyclebinfolder" or "trash://";
    }
}
