namespace Trendo.Core;

/// <summary>SYS-03: Browser finden, Adressen sicher prüfen, Download-Ordner lesen.</summary>
public sealed record BrowserInfo(string Id, string Name, string Path);

public static class Browsers
{
    public static readonly (string Id, string Name, string[] Paths)[] Known =
    [
        ("edge", "Microsoft Edge", [@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe", @"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"]),
        ("chrome", "Google Chrome", [@"%ProgramFiles%\Google\Chrome\Application\chrome.exe", @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe", @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"]),
        ("firefox", "Mozilla Firefox", [@"%ProgramFiles%\Mozilla Firefox\firefox.exe", @"%ProgramFiles(x86)%\Mozilla Firefox\firefox.exe"]),
        ("brave", "Brave", [@"%ProgramFiles%\BraveSoftware\Brave-Browser\Application\brave.exe", @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\Application\brave.exe"]),
    ];

    public static IReadOnlyList<BrowserInfo> Find(Func<string, bool>? exists = null, Func<string, string>? expand = null)
    {
        exists ??= File.Exists; expand ??= Environment.ExpandEnvironmentVariables;
        var l = new List<BrowserInfo>();
        foreach (var (id, name, paths) in Known)
            if (paths.Select(expand).FirstOrDefault(p => !p.Contains('%') && exists(p)) is { } hit) l.Add(new(id, name, hit));
        return l;
    }

    /// <summary>Nur http/https. „trendo.de“ wird zu https://trendo.de; Suchbegriffe werden zur Websuche.</summary>
    public static Uri? Normalize(string input, string searchUrl = "https://duckduckgo.com/?q=")
    {
        var s = input.Trim();
        if (s.Length == 0) return null;
        if (Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "http" or "https") return u;
        if (s.Contains("://") || s.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || s.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return null;
        if (!s.Contains(' ') && s.Contains('.') && Uri.TryCreate("https://" + s, UriKind.Absolute, out var h) && h.Host.Contains('.')) return h;
        return new Uri(searchUrl + Uri.EscapeDataString(s));
    }

    public static string DownloadsFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public static bool Risky(string file) => Path.GetExtension(file).ToLowerInvariant() is ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" or ".vbs" or ".js" or ".scr" or ".com";
    public static bool Unfinished(string file) => Path.GetExtension(file).ToLowerInvariant() is ".crdownload" or ".part" or ".partial" or ".tmp";
}
