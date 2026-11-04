using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo OS (Linux) statt nur Windows-Registrierung (04.11.2026): .desktop-Dateien, Autostart, Standardprogramme, Ordner zeigen.</summary>
public class DesktopPlatformTests
{
    [Fact(DisplayName = "Linux-Wege: .desktop lesen (nur [Desktop Entry]), Programm aus Exec, Browsernamen, Ordner zeigen je System")]
    public void Desktop_files()
    {
        var d = DesktopPlatform.ReadDesktopFile("[Desktop Entry]\nName=Notizen\nExec=\"/opt/notes/notes app\" %U\n# Kommentar\n[Desktop Action neu]\nName=Falsch\n");
        Assert.Equal("Notizen", d["Name"]); Assert.Equal("/opt/notes/notes app", DesktopPlatform.ExecProgram(d["Exec"]));
        Assert.Equal("krita", DesktopPlatform.ExecProgram("krita %F"));
        Assert.Equal("Mozilla Firefox", DesktopPlatform.AppName("org.mozilla.firefox.desktop")); Assert.Equal("nicht festgelegt", DesktopPlatform.AppName(null));
        var (exe, args) = DesktopPlatform.RevealCommand(Path.Combine(Path.GetTempPath(), "a", "b.mp4"));
        if (OperatingSystem.IsWindows()) { Assert.Equal("explorer.exe", exe); Assert.StartsWith("/select,", args); }
        else { Assert.Equal("xdg-open", exe); Assert.EndsWith("a", args); }
    }

    [Fact(DisplayName = "Linux-Wege: Autostart aus ~/.config/autostart (aus/an) und neue Programme aus ~/.local/share/applications (nur unter Linux echt geprüft)")]
    public void Autostart_and_new_apps()
    {
        if (!OperatingSystem.IsLinux()) return; // unter Windows liest Trendo die Registrierung (nur lesen) – hier nicht nachbildbar
        var home = Path.Combine(Path.GetTempPath(), "trendo-home-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null); Environment.SetEnvironmentVariable("XDG_DATA_HOME", null);
            var auto = Path.Combine(home, ".config", "autostart"); Directory.CreateDirectory(auto);
            File.WriteAllText(Path.Combine(auto, "sync.desktop"), "[Desktop Entry]\nName=Sync\nExec=sync-tool\n");
            File.WriteAllText(Path.Combine(auto, "aus.desktop"), "[Desktop Entry]\nName=Aus\nExec=x\nHidden=true\n");
            var items = DesktopPlatform.AutostartItems(home);
            Assert.Equal(2, items.Count); Assert.False(items.First(i => i.Name == "Aus").Enabled); Assert.True(items.First(i => i.Name == "Sync").Enabled);
            var apps = Path.Combine(home, ".local", "share", "applications"); Directory.CreateDirectory(apps);
            var before = DesktopPlatform.InstalledAppIds(home);
            File.WriteAllText(Path.Combine(apps, "neu.desktop"), "[Desktop Entry]\nName=Neu\nExec=neu-app %f\n");
            Assert.Equal("neu-app", DesktopPlatform.NewAppExe(before, _ => null, home));
        }
        finally { Directory.Delete(home, true); }
    }

    [Fact(DisplayName = "Standardbrowser unter Linux über xdg-settings (Befehl ersetzt)")]
    public void Default_browser()
    {
        if (OperatingSystem.IsWindows()) return;
        DesktopPlatform.XdgOverride = (exe, a) => exe == "xdg-settings" ? "firefox.desktop" : null;
        try { Assert.Equal("Mozilla Firefox", DesktopPlatform.DefaultBrowser()); }
        finally { DesktopPlatform.XdgOverride = null; }
    }
}
