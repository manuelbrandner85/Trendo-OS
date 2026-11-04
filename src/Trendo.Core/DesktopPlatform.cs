using System.Diagnostics;
using Trendo.Core.Sys;

namespace Trendo.Core;

/// <summary>Systemfragen, die Windows und Linux verschieden beantworten (Umbau 04.11.2026: vorher nur Windows-Registrierung, unter Trendo OS „eingeschränkt“).
/// Windows: Registrierung (nur lesen). Linux (Trendo OS): freedesktop-Standards – .desktop-Dateien, ~/.config/autostart, xdg-settings, xdg-mime, xdg-open.</summary>
public static class DesktopPlatform
{
    public static bool IsLinux() => OperatingSystem.IsLinux();

    /// <summary>Ordner mit .desktop-Dateien (Linux): Benutzer zuerst, dann System.</summary>
    public static IReadOnlyList<string> ApplicationDirs(string? home = null)
    {
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".local", "share");
        return [Path.Combine(data, "applications"), "/usr/local/share/applications", "/usr/share/applications"];
    }

    /// <summary>Werte einer .desktop-Datei aus dem Abschnitt [Desktop Entry] (Name, Exec, Hidden, Icon …).</summary>
    public static Dictionary<string, string> ReadDesktopFile(string text)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal); var inEntry = false;
        foreach (var raw in text.Split('\n'))
        {
            var l = raw.Trim();
            if (l.StartsWith('[')) { inEntry = l == "[Desktop Entry]"; continue; }
            if (!inEntry || l.Length == 0 || l.StartsWith('#')) continue;
            var i = l.IndexOf('='); if (i <= 0) continue;
            d.TryAdd(l[..i].Trim(), l[(i + 1)..].Trim());
        }
        return d;
    }

    /// <summary>Programm aus einer Exec-Zeile (ohne %f/%U-Platzhalter und Anführungszeichen).</summary>
    public static string ExecProgram(string exec)
    {
        exec = exec.Trim();
        if (exec.StartsWith('"')) { var e = exec.IndexOf('"', 1); return e > 0 ? exec[1..e] : exec.Trim('"'); }
        return exec.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    }

    /// <summary>Bekannte Programme (Kennungen), um nach einer Installation das neue zu finden: Windows „Apps“-Einträge, Linux .desktop-Dateien des Benutzers.</summary>
    public static HashSet<string> InstalledAppIds(string? home = null)
    {
        if (OperatingSystem.IsWindows())
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            return k is null ? [] : [.. k.GetSubKeyNames()];
        }
        var dir = ApplicationDirs(home)[0];
        return Directory.Exists(dir) ? [.. Directory.GetFiles(dir, "*.desktop").Select(Path.GetFileName).OfType<string>()] : [];
    }

    /// <summary>Neu dazugekommenes Programm seit <paramref name="before"/> → ausführbare Datei (oder null). <paramref name="mainExe"/> sucht im Installationsordner.</summary>
    public static string? NewAppExe(HashSet<string> before, Func<string, string?> mainExe, string? home = null)
    {
        if (OperatingSystem.IsWindows())
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (k is null) return null;
            foreach (var n in k.GetSubKeyNames().Where(n => !before.Contains(n)))
            {
                using var e = k.OpenSubKey(n);
                if (e?.GetValue("DisplayIcon") is string icon && icon.Split(',')[0].Trim('"') is var ico && ico.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(ico)
                    && !Path.GetFileName(ico).StartsWith("unins", StringComparison.OrdinalIgnoreCase)) return ico;
                if (e?.GetValue("InstallLocation") is string dir && Directory.Exists(dir.Trim('"')) && mainExe(dir.Trim('"')) is { } exe) return exe;
            }
            return null;
        }
        var apps = ApplicationDirs(home)[0];
        if (!Directory.Exists(apps)) return null;
        foreach (var f in Directory.GetFiles(apps, "*.desktop").Where(f => !before.Contains(Path.GetFileName(f))))
            if (ReadDesktopFile(File.ReadAllText(f)).GetValueOrDefault("Exec") is { Length: > 0 } ex && ExecProgram(ex) is { Length: > 0 } prog && (File.Exists(prog) || !prog.Contains('/'))) return prog;
        return null;
    }

    /// <summary>Autostart-Einträge des Benutzers: Windows Run-Schlüssel, Linux ~/.config/autostart/*.desktop.</summary>
    public static List<Software.AutostartItem> AutostartItems(string? home = null)
    {
        var l = new List<Software.AutostartItem>();
        if (OperatingSystem.IsWindows())
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (k is not null) foreach (var n in k.GetValueNames()) l.Add(new(n, k.GetValue(n)?.ToString() ?? "", "Registrierung", true));
            return l;
        }
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cfg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".config");
        var dir = Path.Combine(cfg, "autostart");
        if (!Directory.Exists(dir)) return l;
        foreach (var f in Directory.GetFiles(dir, "*.desktop").Order(StringComparer.Ordinal))
        {
            var d = ReadDesktopFile(File.ReadAllText(f));
            var off = d.GetValueOrDefault("Hidden") == "true" || d.GetValueOrDefault("X-GNOME-Autostart-enabled") == "false";
            l.Add(new(d.GetValueOrDefault("Name") ?? Path.GetFileNameWithoutExtension(f), d.GetValueOrDefault("Exec") ?? "", "Autostart-Ordner", !off));
        }
        return l;
    }

    /// <summary>Für Tests: Ersatz für xdg-Befehle (Programm, Argumente → Ausgabe).</summary>
    public static Func<string, string[], string?>? XdgOverride { get; set; }

    static string? Xdg(string exe, params string[] args)
    {
        if (XdgOverride is { } o) return o(exe, args);
        try
        {
            var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi); if (p is null) return null;
            var o2 = p.StandardOutput.ReadToEnd(); p.WaitForExit(5000);
            return p.ExitCode == 0 ? o2.Trim() : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { return null; }
    }

    /// <summary>Lesbarer Name aus „firefox.desktop“ / „org.mozilla.firefox.desktop“.</summary>
    public static string AppName(string? desktopId) => desktopId switch
    {
        null or "" => "nicht festgelegt",
        var d when d.Contains("firefox", StringComparison.OrdinalIgnoreCase) => "Mozilla Firefox",
        var d when d.Contains("chromium", StringComparison.OrdinalIgnoreCase) => "Chromium",
        var d when d.Contains("chrome", StringComparison.OrdinalIgnoreCase) => "Google Chrome",
        var d when d.Contains("brave", StringComparison.OrdinalIgnoreCase) => "Brave",
        var d when d.Contains("vivaldi", StringComparison.OrdinalIgnoreCase) => "Vivaldi",
        var d when d.Contains("edge", StringComparison.OrdinalIgnoreCase) => "Microsoft Edge",
        var d => d.Replace(".desktop", ""),
    };

    /// <summary>Standardbrowser: Windows über die Registrierung, Linux über xdg-settings.</summary>
    public static string DefaultBrowser()
    {
        if (OperatingSystem.IsWindows())
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            return Web.BrowserForProgId(k?.GetValue("ProgId") as string);
        }
        return AppName(Xdg("xdg-settings", "get", "default-web-browser"));
    }

    static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".txt"] = "text/plain", [".html"] = "text/html", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        [".mp3"] = "audio/mpeg", [".mp4"] = "video/mp4", [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".zip"] = "application/zip", [".odt"] = "application/vnd.oasis.opendocument.text",
    };

    /// <summary>Standardprogramm für eine Endung: Windows ProgId, Linux .desktop-Kennung über xdg-mime (null = nicht festgelegt).</summary>
    public static string? DefaultAppFor(string ext)
    {
        if (OperatingSystem.IsWindows())
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice");
            return k?.GetValue("ProgId")?.ToString();
        }
        return Mime.TryGetValue(ext, out var m) ? Xdg("xdg-mime", "query", "default", m) : null;
    }

    /// <summary>Proxy aus den Windows-Interneteinstellungen; unter Linux null (dort gelten HTTPS_PROXY/HTTP_PROXY).</summary>
    public static (bool? On, string? Server, string? Pac)? WindowsProxy()
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
        return (k?.GetValue("ProxyEnable") is int i ? i == 1 : null, k?.GetValue("ProxyServer") as string, k?.GetValue("AutoConfigURL") as string);
    }

    /// <summary>Datei im Ordner zeigen: Windows markiert sie im Explorer, Linux öffnet den Ordner (xdg-open). Gibt Programm und Argumente zurück.</summary>
    public static (string Exe, string Args) RevealCommand(string file) =>
        OperatingSystem.IsWindows() ? ("explorer.exe", "/select,\"" + file + "\"") : ("xdg-open", Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".");
}
