using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Programme von GitHub auf Trendo OS installieren und wieder entfernen (Vorgabe 2026-10-26).
/// Nur fertige Windows-Programme aus der neuesten Veröffentlichung (Release) eines Projekts: ein ZIP oder eine einzelne Programmdatei,
/// ohne Admin, ohne Installer (kein MSI/Setup), in einen eigenen Ordner unter „TrendoWerkzeuge“. Prüfsumme, wenn GitHub eine angibt.
/// Jede Installation steht in <see cref="ListName"/>; Entfernen löscht genau diesen Ordner. Bei der Deinstallation von Trendo geht alles mit.</summary>
public static partial class GitHubApps
{
    public const string ListName = "github-programme.json";
    public const string Prefix = "gh-";
    /// <summary>Größte erlaubte Datei (2 GB) – schützt vor vollen Platten durch fehlerhafte Angaben.</summary>
    public const long MaxBytes = 2L << 30;

    public sealed record Asset(string Name, string Url, long Size, string? Sha256);
    public sealed record Release(string Tag, string Name, IReadOnlyList<Asset> Assets);

    public sealed record Installed(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("repo")] string Repo,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("exe")] string Exe,
        [property: JsonPropertyName("license")] string License,
        [property: JsonPropertyName("asset")] string Asset,
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("installed")] DateTimeOffset InstalledAt);

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$")] private static partial Regex RepoName();

    public static bool ValidRepo(string fullName) => RepoName().IsMatch(fullName) && !fullName.Contains("..", StringComparison.Ordinal);

    /// <summary>Neueste Veröffentlichung über die offizielle GitHub-API (ohne Konto).</summary>
    public static string LatestReleaseUrl(string fullName) =>
        ValidRepo(fullName) ? $"https://api.github.com/repos/{fullName}/releases/latest" : throw new ArgumentException("Ungültiger Projektname.", nameof(fullName));

    /// <summary>Ordnername im Werkzeugordner, z. B. „gh-owner-repo“ (nur Kleinbuchstaben, Ziffern, Bindestrich).</summary>
    public static string IdFor(string fullName) =>
        Prefix + Regex.Replace(fullName.ToLowerInvariant().Replace('/', '-'), "[^a-z0-9-]", "-");

    public static Release ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var assets = new List<Asset>();
        if (r.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var a in arr.EnumerateArray())
            {
                var digest = a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                var sha = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToLowerInvariant() : null;
                assets.Add(new Asset(a.GetProperty("name").GetString() ?? "", a.GetProperty("browser_download_url").GetString() ?? "",
                    a.TryGetProperty("size", out var s) ? s.GetInt64() : 0, sha));
            }
        return new Release(r.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "", r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "", assets);
    }

    static readonly string[] Skip = ["src", "source", "debug", "symbols", "pdb", "linux", "macos", "darwin", "osx", "android", "ios", "sha256", "checksum", ".sig", ".asc"];

    /// <summary>Wählt das passende Windows-Programm: ZIP oder einzelne .exe für diese Prozessorart; Installer (MSI, Setup) nie, weil sie Admin-Rechte
    /// und systemweite Änderungen brauchen. Null, wenn es kein passendes gibt.</summary>
    public static Asset? PickWindows(IEnumerable<Asset> assets, bool arm64 = false, bool allow7z = false)
    {
        int Score(Asset a)
        {
            var n = a.Name.ToLowerInvariant();
            if (a.Size <= 0 || a.Size > MaxBytes || !a.Url.StartsWith("https://github.com/", StringComparison.Ordinal)) return -1;
            if (Skip.Any(n.Contains) || n.EndsWith(".msi") || n.EndsWith(".msix") || n.Contains("setup") || n.Contains("installer")) return -1;
            var zip = n.EndsWith(".zip"); var exe = n.EndsWith(".exe"); var sz = allow7z && n.EndsWith(".7z");
            if (!zip && !exe && !sz) return -1;
            var score = 0;
            if (n.Contains("win") || n.Contains("windows") || sz && n.Contains(".nsis.")) score += 10; // *.nsis.7z: reines Programmpaket von Electron-Programmen
            else if (zip || sz) return -1; // Archiv ohne Windows-Hinweis: wahrscheinlich etwas anderes
            if (sz) score += 3;
            var isArm = n.Contains("arm64") || n.Contains("aarch64");
            var isX64 = n.Contains("x64") || n.Contains("amd64") || n.Contains("x86_64") || n.Contains("win64");
            if (arm64 ? isArm : isX64) score += 6; else if (arm64 ? isX64 : isArm) score -= 8;
            if (n.Contains("x86") && !n.Contains("x86_64") || n.Contains("win32") || n.Contains("i686")) score -= 3;
            if (n.Contains("portable")) score += 4;
            if (zip) score += 2;
            return score;
        }
        return assets.Select(a => (a, s: Score(a))).Where(x => x.s >= 0).OrderByDescending(x => x.s).ThenBy(x => x.a.Size).Select(x => x.a).FirstOrDefault();
    }

    public static IReadOnlyList<Installed> List(string toolsRoot)
    {
        var f = Path.Combine(toolsRoot, ListName);
        try { return File.Exists(f) ? JsonSerializer.Deserialize<List<Installed>>(File.ReadAllText(f)) ?? [] : []; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return []; }
    }

    static void Save(string toolsRoot, IEnumerable<Installed> list)
    {
        Directory.CreateDirectory(toolsRoot);
        var f = Path.Combine(toolsRoot, ListName);
        File.WriteAllText(f + ".tmp", JsonSerializer.Serialize(list.OrderBy(i => i.Repo, StringComparer.OrdinalIgnoreCase).ToList(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(f + ".tmp", f, true);
    }

    /// <summary>Installiert eine heruntergeladene Datei: Prüfsumme prüfen (wenn GitHub eine nennt), ZIP sicher entpacken oder .exe ablegen,
    /// Hauptprogramm finden, in die Liste eintragen. Ein vorhandener Stand desselben Projekts wird ersetzt.</summary>
    public static Installed Install(string toolsRoot, string repo, Release release, Asset asset, string downloadedFile, string license, string description, AuditLog? audit = null, Action<string, string>? extract7z = null, bool arm64 = false)
    {
        if (!ValidRepo(repo)) throw new ArgumentException("Ungültiger Projektname.", nameof(repo));
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(downloadedFile)));
        if (asset.Sha256 is { } want && !string.Equals(want, sha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Prüfsumme passt nicht zu {asset.Name} – die Datei wurde nicht installiert.");
        var id = IdFor(repo);
        var dir = Path.Combine(toolsRoot, id);
        var tmp = dir + ".neu";
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        Directory.CreateDirectory(tmp);
        try
        {
            if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ExtractSafe(downloadedFile, tmp);
            else if (asset.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                (extract7z ?? throw new InvalidOperationException("7-Zip fehlt – bitte unter „Programme“ aus dem Vollpaket einrichten."))(downloadedFile, tmp);
            else if (DetectInstaller(downloadedFile) is var kind && kind != InstallerKind.None)
            {
                // Installer statt Programm: NSIS mit 7-Zip auspacken (Electron: darin app-64.7z), sonst still installieren lassen (App)
                if (kind != InstallerKind.Nsis || extract7z is null) throw new InstallerOnlyException(kind, downloadedFile);
                var raw = tmp + ".roh"; if (Directory.Exists(raw)) Directory.Delete(raw, true); Directory.CreateDirectory(raw);
                try
                {
                    extract7z(downloadedFile, raw);
                    if (NsisPayload(raw, arm64) is { } payload) extract7z(payload, tmp);
                    else foreach (var f in Directory.EnumerateFiles(raw, "*", SearchOption.AllDirectories).Where(f => !f.Contains("$PLUGINSDIR", StringComparison.Ordinal)))
                    { var dst = Path.Combine(tmp, Path.GetRelativePath(raw, f)); Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.Copy(f, dst, true); }
                }
                finally { if (Directory.Exists(raw)) Directory.Delete(raw, true); }
                if (MainExe(tmp, repo) is null) throw new InstallerOnlyException(kind, downloadedFile);
            }
            else File.Copy(downloadedFile, Path.Combine(tmp, Path.GetFileName(asset.Name)));
            var exe = MainExe(tmp, repo) ?? throw new InvalidOperationException("Im Paket ist kein Windows-Programm (.exe).");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.Move(tmp, dir);
            var entry = new Installed(id, repo, release.Tag, Path.GetRelativePath(tmp, exe).Replace('\\', '/'), license, asset.Name, sha, description, DateTimeOffset.Now);
            Save(toolsRoot, List(toolsRoot).Where(i => i.Id != id).Append(entry));
            audit?.Append("app", "github.install", $"{repo} {release.Tag} {asset.Name} sha256={sha}");
            return entry;
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
    }

    /// <summary>Entpackt ohne „Zip Slip“: kein Eintrag darf außerhalb des Zielordners landen.</summary>
    public static void ExtractSafe(string zip, string target)
    {
        var root = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var z = ZipFile.OpenRead(zip);
        long total = 0;
        foreach (var e in z.Entries)
        {
            var dest = Path.GetFullPath(Path.Combine(root, e.FullName));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsicherer Pfad im ZIP: " + e.FullName);
            total += e.Length;
            if (total > MaxBytes * 2) throw new InvalidOperationException("Das ZIP ist entpackt zu groß.");
            if (e.FullName.EndsWith('/') || e.FullName.EndsWith('\\')) { Directory.CreateDirectory(dest); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            e.ExtractToFile(dest, true);
        }
    }

    /// <summary>Hauptprogramm: .exe, die nach dem Projekt heißt, sonst die größte – ohne Deinstaller, Updater und Hilfsprogramme.</summary>
    public static string? MainExe(string dir, string repo)
    {
        var name = repo.Split('/')[1].ToLowerInvariant();
        string[] helpers = ["unins", "uninstall", "update", "crashpad", "crash", "helper", "elevate", "vc_redist", "setup"];
        // Befund 27.10.2026 („braucht ewig“): nur bis Tiefe 4, ohne Verknüpfungen und gesperrte Ordner – Programme liegen nie tiefer
        var opt = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System };
        var exes = Directory.EnumerateFiles(dir, "*.exe", opt)
            .Where(f => !helpers.Any(h => Path.GetFileName(f).ToLowerInvariant().Contains(h))).Take(400).ToList();
        return exes.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? exes.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(name))
            ?? exes.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
    }

    /// <summary>Ordner unter %LOCALAPPDATA%, die ständig von anderen Programmen beschrieben werden – dort liegt nie ein frisch installiertes Programm.</summary>
    public static readonly string[] NoisyDirs = ["Temp", "Packages", "Microsoft", "Google", "Mozilla", "NVIDIA", "NVIDIA Corporation", "D3DSCache", "CrashDumps", "Comms",
        "ConnectedDevicesPlatform", "Application Data", "History", "Temporary Internet Files", "PlaceholderTileLogoFolder", "Publishers", "IsolatedStorage", "pip", "npm-cache", "Steam", "Discord", "Spotify", "Trendo", "TrendoOS"];

    /// <summary>Welche Ordner hat der Installer angelegt? Neue Ordner immer; geänderte nur, wenn der Name zum Projekt passt; laute Systemordner nie.</summary>
    public static List<string> FreshDirs(IEnumerable<string> now, IReadOnlyDictionary<string, DateTime> before, Func<string, DateTime> lastWrite, DateTime started, string repo, string assetName)
    {
        var name = repo.Split('/')[1].ToLowerInvariant();
        var stem = System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(assetName).ToLowerInvariant(), @"[-_. ]?(setup|installer|x64|win(64)?|\d+(\.\d+)*)", "").Trim('-', '_', ' ');
        bool Fits(string d) { var n = Path.GetFileName(d).ToLowerInvariant().Replace(' ', '-'); return n.Contains(name) || (stem.Length > 2 && n.Contains(stem)); }
        return now.Where(d => !NoisyDirs.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            .Where(d => !before.ContainsKey(d) || (Fits(d) && lastWrite(d) >= started))
            .OrderByDescending(Fits).ThenByDescending(d => !before.ContainsKey(d)).ToList();
    }

    /// <summary>Entfernt ein Programm von GitHub: nur Ordner „gh-…“ direkt im Werkzeugordner, die in der Liste stehen.</summary>
    public static bool Uninstall(string toolsRoot, string id, AuditLog? audit = null)
    {
        var list = List(toolsRoot);
        if (list.FirstOrDefault(i => i.Id == id) is not { } entry || !id.StartsWith(Prefix, StringComparison.Ordinal) || id.IndexOfAny(['/', '\\', '.']) >= 0) return false;
        var dir = Path.Combine(toolsRoot, id);
        if (Directory.Exists(dir))
        {
            var info = new DirectoryInfo(dir);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) info.Delete(); else info.Delete(true);
        }
        Save(toolsRoot, list.Where(i => i.Id != id));
        audit?.Append("app", "github.uninstall", $"{entry.Repo} {entry.Version}");
        return true;
    }
}
