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

    /// <summary>Gemeinsame Liste für Downloads, Mail-Anhänge und Dateien vom Handy (T-0767 R3-N1): Dateien, die beim Öffnen etwas ausführen,
    /// installieren, einbinden oder umleiten können. Die frühere Liste (.exe .msi .bat .cmd .ps1 .vbs .js .scr .com) ist vollständig enthalten.</summary>
    static readonly HashSet<string> RiskyExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".scr", ".com",
        ".msix", ".appx", ".lnk", ".url", ".scf", ".psm1", ".vbe", ".jse", ".wsf", ".wsh", ".hta", ".cpl", ".jar", ".reg", ".dll",
        ".pif", ".application", ".appref-ms", ".msp", ".mst", ".xll", ".settingcontent-ms", ".wsc", ".sct", ".inf", ".msc", ".ps1xml", ".psd1",
        ".chm", ".iso", ".img", ".vhd", ".vhdx", ".diagcab", ".gadget", ".docm", ".xlsm", ".pptm",
    };

    public static bool Risky(string file) => RiskyExt.Contains(Path.GetExtension(file)) || Path.GetFileName(file).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
    public static bool Unfinished(string file) => Path.GetExtension(file).ToLowerInvariant() is ".crdownload" or ".part" or ".partial" or ".tmp";
}
