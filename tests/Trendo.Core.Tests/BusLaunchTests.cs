using Trendo.Core;
using Xunit;

public class W5_Bus_Launch
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-ln-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static BusinessIdea Idea() => new() { Name = "Sitterly", Problem = "Hundesitter kurzfristig finden ist schwer.", Solution = "Geprüfte Sitter online buchen.", Audience = "Hundebesitzer", ModelId = "marktplatz", Price = 20, VariableCost = 4, FixedCostMonth = 300, CustomersMonth = 60, Competitors = 3, Skill = 4, HoursWeek = 15, Capital = 2500 };
    static readonly LegalInfo L = new("Max Muster", "Hauptstr. 1", "10115 Berlin", "max@sitterly.de");

    [Fact(DisplayName = "BUS-016/026/027: Webseite mit 5 Seiten, Pflichtangaben erfüllt, Qualität fehlerfrei; Mängel werden erkannt")]
    public void Site()
    {
        var site = BusinessLaunch.BuildSite(Tmp(), Idea(), L, [new("Basis", 9.9)]);
        Assert.Equal(5, Directory.GetFiles(site, "*.html").Length);
        Assert.Contains("9,90 €", File.ReadAllText(Path.Combine(site, "preise.html")));
        var comp = BusinessLaunch.Compliance(site, L, false); Assert.All(comp, c => Assert.True(c.Ok, c.Label));
        Assert.All(BusinessLaunch.Quality(site), c => Assert.True(c.Ok, c.Label + c.Fix));
        Assert.Contains(BusinessLaunch.Compliance(site, L, true), c => !c.Ok && c.Label.Contains("Widerruf"));
        Assert.Contains(BusinessLaunch.Compliance(site, L with { Street = "Hauptstraße" }, false), c => !c.Ok && c.Label.Contains("Anschrift"));
        File.AppendAllText(Path.Combine(site, "index.html"), "<img src=\"fehlt.png\"><a href=\"http://x.de\">x</a><script src=\"https://tracker.example/t.js\"></script>");
        var q = BusinessLaunch.Quality(site).Where(c => !c.Ok).Select(c => c.Label).ToList();
        Assert.Contains("index.html: keine toten Links", q); Assert.Contains("index.html: Bilder haben eine Beschreibung", q); Assert.Contains("index.html: nur sichere Links (https)", q);
        Assert.Contains(BusinessLaunch.Compliance(site, L, false), c => !c.Ok && c.Label.Contains("fremden Dienste"));
    }

    [Fact(DisplayName = "BUS-028/029: Paket mit Anleitung und Pages-Ablauf, Domain geprüft; Sicherung behält 10, Wiederherstellen")]
    public void Package_Backup()
    {
        var root = Tmp(); var site = BusinessLaunch.BuildSite(root, Idea(), L, []);
        var zip = BusinessLaunch.Package(site, "sitterly.de");
        using (var z = System.IO.Compression.ZipFile.OpenRead(zip)) { var names = z.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList(); Assert.Contains("index.html", names); Assert.Contains(".github/workflows/pages.yml", names); Assert.Contains("CNAME", names); }
        Assert.Throws<ArgumentException>(() => BusinessLaunch.Package(site, "kein domain"));
        var data = Tmp(); var i = Idea(); new IdeaStore(data).Save(i); new IdeaList<Competitor>(data, i.Id, "mitbewerber").Add(new("A", "", 1, "", ""));
        var bdir = Path.Combine(data, "bk");
        for (var k = 0; k < 12; k++) BusinessLaunch.Backup(data, i.Id, bdir);
        Assert.Equal(10, Directory.GetFiles(bdir).Length);
        new IdeaStore(data).Delete(i.Id); Assert.Empty(new IdeaStore(data).All());
        Assert.True(BusinessLaunch.Restore(Directory.GetFiles(bdir).OrderByDescending(File.GetLastWriteTimeUtc).First(), data) >= 2);
        Assert.Single(new IdeaStore(data).All()); Assert.Single(new IdeaList<Competitor>(data, i.Id, "mitbewerber").All());
    }

    [Fact(DisplayName = "BUS-030: Startklar erst, wenn Bewertung, Markttest, Webseite, Recht, Qualität, Paket und Sicherung passen")]
    public void Handover()
    {
        var data = Tmp(); var i = Idea(); var bdir = Path.Combine(data, "bk");
        var (r0, c0) = BusinessLaunch.Handover(i, [], null, L, false, bdir);
        Assert.False(r0); Assert.Contains(c0, c => !c.Ok && c.Area == "Webseite");
        var site = BusinessLaunch.BuildSite(data, i, L, []); BusinessLaunch.Package(site); BusinessLaunch.Backup(data, i.Id, bdir);
        var (r1, c1) = BusinessLaunch.Handover(i, [new("a", "x", 1, 2)], site, L, false, bdir);
        Assert.True(r1, string.Join("; ", c1.Where(x => !x.Ok).Select(x => x.Label)));
    }
}
