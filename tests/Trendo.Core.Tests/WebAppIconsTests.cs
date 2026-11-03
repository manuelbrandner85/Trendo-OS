using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Wunsch 03.11.2026: WhatsApp, Telegram & Co. mit Original-Symbol in der Leiste – nur von den eigenen Seiten des Dienstes, geprüft und zwischengespeichert.</summary>
public class WebAppIconsTests
{
    [Fact(DisplayName = "App-Symbole: größtes Symbol aus Link-Tags (SVG übersprungen), Manifest erkannt, Manifest-Symbole nach Größe, feste Orte zuletzt")]
    public void Parse_html_and_manifest()
    {
        var page = new Uri("https://web.telegram.org/a/");
        var html = """<link rel="icon" href="./favicon.svg" type="image/svg+xml"><link rel="icon" type="image/png" sizes="32x32" href="./favicon-32x32.png"><link rel="apple-touch-icon" sizes="180x180" href="./apple-touch-icon.png"><link rel='manifest' href='/site.webmanifest'>""";
        var (icons, manifest) = WebAppIcons.FromHtml(html, page);
        Assert.Equal(["https://web.telegram.org/a/apple-touch-icon.png", "https://web.telegram.org/a/favicon-32x32.png"], icons.Select(u => u.ToString()));
        Assert.Equal("https://web.telegram.org/site.webmanifest", manifest!.ToString());
        var m = WebAppIcons.FromManifest("""{"icons":[{"src":"/i/128.png","sizes":"128x128"},{"src":"https://sf16-sg.tiktokcdn.com/pwa/512x512.png","sizes":"512x512"},{"src":"/i/a.svg","sizes":"any"}]}""", new Uri("https://www.tiktok.com/manifest.json"));
        Assert.Equal(["https://sf16-sg.tiktokcdn.com/pwa/512x512.png", "https://www.tiktok.com/i/128.png"], m.Select(u => u.ToString()));
        Assert.Empty(WebAppIcons.FromManifest("kein json", new Uri("https://www.tiktok.com/manifest.json")));
        Assert.Equal(["https://www.facebook.com/apple-touch-icon.png", "https://www.facebook.com/favicon.ico"], WebAppIcons.Fallbacks(new Uri("https://www.facebook.com/")).Select(u => u.ToString()));
    }

    [Fact(DisplayName = "App-Symbole: nur HTTPS von den Domains des Dienstes (auch CDN), nur echte Bilder (PNG/JPEG/WebP/GIF/ICO, nicht zu groß), Speichern ersetzt das alte")]
    public void Allowed_and_valid()
    {
        Assert.True(WebAppIcons.Allowed("tiktok", new Uri("https://sf16-sg.tiktokcdn.com/x.png")));
        Assert.True(WebAppIcons.Allowed("whatsapp", new Uri("https://web.whatsapp.com/apple-touch-icon.png")));
        Assert.False(WebAppIcons.Allowed("whatsapp", new Uri("http://web.whatsapp.com/apple-touch-icon.png")));
        Assert.False(WebAppIcons.Allowed("whatsapp", new Uri("https://whatsapp.com.evil.example/icon.png")));
        Assert.False(WebAppIcons.Allowed("facebook", new Uri("https://static.cdninstagram.com/i.webp")));
        Assert.False(WebAppIcons.Allowed("unbekannt", new Uri("https://example.org/i.png")));
        var png = new byte[400]; png[0] = 0x89; png[1] = 0x50; png[2] = 0x4E; png[3] = 0x47;
        Assert.Equal(".png", WebAppIcons.ImageKind(png));
        Assert.Null(WebAppIcons.ImageKind(System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html>" + new string(' ', 400))));
        Assert.Null(WebAppIcons.ImageKind(png[..100]));
        var root = Path.Combine(Path.GetTempPath(), "trendo-icons-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(WebAppIcons.Cached(root, "telegram"));
            var ico = new byte[400]; ico[2] = 1;
            WebAppIcons.Store(root, "telegram", ico);
            Assert.EndsWith("symbol.ico", WebAppIcons.Cached(root, "telegram"));
            WebAppIcons.Store(root, "telegram", png);
            Assert.EndsWith("symbol.png", WebAppIcons.Cached(root, "telegram"));
            Assert.Single(Directory.GetFiles(WebApps.ProfileDir(root, "telegram"), "symbol.*"));
            Assert.Throws<ArgumentException>(() => WebAppIcons.Store(root, "telegram", new byte[300]));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
