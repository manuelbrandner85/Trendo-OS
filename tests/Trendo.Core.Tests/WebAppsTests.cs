using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>27.10.2026: WhatsApp, Telegram, TikTok, Instagram, Facebook als Apps (offizielle Webversion im eigenen App-Fenster).</summary>
public class WebAppsTests
{
    [Fact(DisplayName = "Apps: Katalog mit offiziellen Adressen, Installieren/Entfernen mit eigenem Speicher, App-Fenster nur mit Chromium-Browser")]
    public void Catalog_install_launch()
    {
        Assert.Equal(["whatsapp", "telegram", "tiktok", "instagram", "facebook"], WebApps.Catalog.Select(a => a.Id));
        Assert.All(WebApps.Catalog, a => Assert.StartsWith("https://", a.Url));
        var root = Path.Combine(Path.GetTempPath(), "trendo-apps-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
            WebApps.Install(root, "telegram", audit, "test");
            WebApps.Install(root, "telegram", audit, "test");
            Assert.Equal(["telegram"], WebApps.Installed(root));
            File.WriteAllText(Path.Combine(WebApps.ProfileDir(root, "telegram"), "Cookies"), "x");
            var args = WebApps.LaunchArgs(root, WebApps.Get("telegram")!);
            Assert.Equal("--app=https://web.telegram.org/a/", args[0]);
            Assert.Contains("--user-data-dir=" + WebApps.ProfileDir(root, "telegram"), args);
            WebApps.Uninstall(root, "telegram", audit, "test");
            Assert.Empty(WebApps.Installed(root));
            Assert.False(Directory.Exists(WebApps.ProfileDir(root, "telegram")));
            Assert.Contains(audit.ReadAll(), e => e.Action == "app.uninstall");
            Assert.Throws<ArgumentException>(() => WebApps.Install(root, "unbekannt", audit, "test"));
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
        Assert.Equal("chrome", WebApps.Browser([new("firefox", "Firefox", "f.exe"), new("chrome", "Chrome", "c.exe")])!.Id);
        Assert.Null(WebApps.Browser([new("firefox", "Firefox", "f.exe")]));
        Assert.EndsWith(@"Telegram Desktop\Telegram.exe", WebApps.TelegramExe(s => s.Replace("%APPDATA%", @"C:\Daten")));
    }
}
