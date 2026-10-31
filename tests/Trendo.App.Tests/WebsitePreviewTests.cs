using System.Net;
using Trendo.Core;
using Xunit;

public class WebsitePreviewTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-web-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Webseite: lokale Vorschau bleibt im Projektordner")]
    public async Task Preview_host_stays_inside()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Tmp();
        var built = WebsiteStudio.Build(root, WebsiteStudio.Example("mikro"));
        using var host = WebsiteHost.Start(built.Directory);
        using var http = new HttpClient();
        var ok = await http.GetAsync(host.Origin + "/index.html", ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("Praxis Nord", await ok.Content.ReadAsStringAsync(ct));
        var blocked = await http.GetAsync(host.Origin + "/%2e%2e/%2e%2e/etc/passwd", ct);
        Assert.NotEqual(HttpStatusCode.OK, blocked.StatusCode);
        var css = await http.GetAsync(host.Origin + "/stil.css", ct);
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
    }

    [Fact(DisplayName = "Webseite: echte Browser-Vorschau je Technik (übersprungen ohne Chrome)")]
    public async Task Preview_screenshots()
    {
        if (WebsitePreview.FindBrowser() is null) { Assert.Skip("Übersprungen – kein Chrome/Edge auf diesem PC."); return; }
        var ct = TestContext.Current.CancellationToken;
        var root = Tmp();
        var shotDir = Path.Combine(root, "shots");
        Directory.CreateDirectory(shotDir);
        foreach (var (id, _, _) in WebsiteTechniques.All)
        {
            string? media = null;
            if (id == "video" && Cli.Find("ffmpeg") is { } ff)
            {
                media = Path.Combine(root, "clip.mp4");
                var made = Cli.Run(ff, ["-y", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=10:duration=1", "-pix_fmt", "yuv420p", media], root, 20000);
                if (!made.Ok) media = null;
            }
            var built = WebsiteStudio.Build(root, WebsiteStudio.Example(id, media));
            var png = Path.Combine(shotDir, id + "-desktop.png");
            var shot = await WebsitePreview.CaptureAsync(built.Directory, 1280, 800, png, ct: ct);
            Assert.True(shot.Ok, shot.Note);
            Assert.True(new FileInfo(png).Length > 8000, id);
            AssertPng(png, 1280, 800);
        }
        var handyDir = Directory.GetDirectories(root).First(d => File.Exists(Path.Combine(d, "index.html")) && File.ReadAllText(Path.Combine(d, "index.html")).Contains("data-technik=\"mikro\""));
        var phone = Path.Combine(shotDir, "mikro-handy.png");
        var narrow = await WebsitePreview.CaptureAsync(handyDir, 390, 844, phone, ct: ct);
        Assert.True(narrow.Ok, narrow.Note);
        AssertPng(phone, 390, 844);
        var story = Directory.GetDirectories(root).First(d => File.Exists(Path.Combine(d, "index.html")) && File.ReadAllText(Path.Combine(d, "index.html")).Contains("data-technik=\"geschichte\""));
        var chapter = Path.Combine(shotDir, "geschichte-kapitel2.png");
        var second = await WebsitePreview.CaptureAsync(story, 1280, 800, chapter, "kapitel=2", ct);
        Assert.True(second.Ok, second.Note);
        File.WriteAllText(Path.Combine(shotDir, "wo.txt"), shotDir);
    }

    static void AssertPng(string path, int width, int height)
    {
        var b = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, b[..4]);
        int U(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        Assert.Equal(width, U(16));
        Assert.Equal(height, U(20));
    }
}
