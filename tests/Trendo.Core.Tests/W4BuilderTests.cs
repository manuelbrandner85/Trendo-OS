using Trendo.Core;
using Xunit;

public class W4_X03_Builder
{
    [Fact(DisplayName = "X-03: Bausteine werden zu sicherem HTML, Reihenfolge änderbar, Speichern und Laden")]
    public void Html()
    {
        var p = new SitePage { Title = "<T>", Blocks = [new("h1", "Hallo <script>"), new("button", "Los", "javascript:alert(1)"), new("img", "Bild", "../../x.png"), new("list", "a\nb\n"), new("contact", "", "a@b.de"), new("button", "Web", "https://trendo.de/x?a=1&b=2")] };
        var h = p.ToHtml();
        Assert.DoesNotContain("<script>", h); Assert.Contains("&lt;script&gt;", h);
        Assert.DoesNotContain("javascript:", h); Assert.Contains("href=\"#\"", h);
        Assert.Contains("src=\"#\"", h); Assert.Contains("<li>b</li>", h); Assert.Contains("mailto:a@b.de", h);
        Assert.Contains("https://trendo.de/x?a=1&amp;b=2", h);
        p.Move(0, 1); Assert.Equal("button", p.Blocks[0].Kind); p.Move(0, -1); Assert.Equal("button", p.Blocks[0].Kind);
        var f = Path.GetTempFileName(); p.Save(f);
        var q = SitePage.Load(f); Assert.Equal(6, q.Blocks.Count); Assert.Equal("<T>", q.Title);
        p.Accent = "red;}</style><script>"; Assert.Contains("#2F86F6", p.ToHtml());
    }

    [Fact(DisplayName = "X-03: Veröffentlichen schreibt index.html und kopiert lokale Bilder mit")]
    public void Publish()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-b-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(d, "bilder"));
        File.WriteAllText(Path.Combine(d, "bilder", "a.png"), "x");
        var p = new SitePage { Blocks = [new("img", "A", "bilder/a.png")] };
        var html = p.Publish(Path.Combine(d, "out"), d);
        Assert.Contains("src=\"bilder/a.png\"", File.ReadAllText(html));
        Assert.True(File.Exists(Path.Combine(d, "out", "bilder", "a.png")));
    }
}
