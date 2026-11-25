using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0768 (Sammel 0.9.110): Nachbesserungen aus dem Volltest und der Zweig-Prüfung.</summary>
public class SammelT0768
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0768-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0768: App mit Daten – eingebettetes JSON enthält kein „javascript:“/„data:“-Linkziel; seite:/summe:/https bleiben, Rundreise unverändert")]
    public void Eingebettetes_json_ohne_gefaehrliche_ziele()
    {
        var p = SitePage.Template("einkaufsliste")!;
        p.Pages[0].Blocks.Add(new Block("button", "Böse", "javascript:alert(1)"));
        p.Pages[0].Blocks.Add(new Block("button", "Böse2", " JavaScript:alert(2)"));
        p.Pages[0].Blocks.Add(new Block("button", "Daten", "data:text/html,<b>x</b>"));
        p.Pages[0].Blocks.Add(new Block("button", "Web", "https://example.org/a"));
        var h = p.ToHtml();
        Assert.DoesNotContain("javascript:", h, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text/html", h);
        Assert.Contains("https://example.org/a", h);
        Assert.Contains("seite:", h); // interne Sprünge bleiben im eingebetteten JSON
        Assert.Equal("javascript:alert(1)", p.Pages[0].Blocks[^4].Url); // das Projekt selbst bleibt unverändert
        var d = Tmp();
        try
        {
            var f = Path.Combine(d, "index.html"); File.WriteAllText(f, h);
            var q = SitePage.FromExport(f);
            Assert.Equal(p.Pages.Count, q.Pages.Count);
            Assert.Equal("#", q.Pages[0].Blocks[^4].Url);
            Assert.Equal("https://example.org/a", q.Pages[0].Blocks[^1].Url);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0768 N1: Wiederöffnen einer ZIP – Bild „start.bat.png“ wird nicht geschrieben, normales Bild schon")]
    public void Wiederoeffnen_ohne_ausfuehrbare_teilendung()
    {
        var d = Tmp();
        try
        {
            var page = SitePage.Template("einkaufsliste")!;
            page.Pages[0].Blocks.Add(new Block("img", "Gut", "gut.png"));
            page.Pages[0].Blocks.Add(new Block("img", "Böse", "start.bat.png"));
            var zip = Path.Combine(d, "app.zip");
            using (var a = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                using (var w = new StreamWriter(a.CreateEntry("index.html").Open())) w.Write(page.ToHtml());
                foreach (var n in new[] { "gut.png", "start.bat.png" }) using (var s = a.CreateEntry(n).Open()) s.Write([0x89, 0x50, 0x4E, 0x47]);
            }
            var assets = Path.Combine(d, "ziel"); Directory.CreateDirectory(assets);
            SitePage.FromExport(zip, assets, "importiert/x");
            var files = Directory.GetFiles(assets, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
            Assert.Contains("gut.png", files);
            Assert.DoesNotContain("start.bat.png", files);
        }
        finally { Directory.Delete(d, true); }
    }
}
