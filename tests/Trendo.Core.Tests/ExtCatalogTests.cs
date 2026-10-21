using System.IO.Compression;
using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

public class W5_X04_Catalog
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-cat-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "X-04 Katalog: lokaler Katalog mit Prüfsummen, Installation nur bei passender Prüfsumme")]
    public void LocalCatalog()
    {
        var root = Tmp(); var dir = Path.Combine(root, "kat");
        var f = ExtensionCatalog.BuildLocal(dir);
        var (list, err) = ExtensionCatalog.Parse(File.ReadAllText(f));
        Assert.Empty(err); Assert.Equal(3, list.Count);
        var store = new ExtensionStore(root);
        foreach (var e in list) { var (x, er) = ExtensionCatalog.InstallVerified(store, e, ExtensionCatalog.LocalFile(dir, e)!); Assert.True(x is not null, string.Join(" ", er)); }
        Assert.Equal(3, store.All().Count);
        var bad = list[0] with { Sha256 = new string('0', 64) };
        var (n, e2) = ExtensionCatalog.InstallVerified(new ExtensionStore(Tmp()), bad, ExtensionCatalog.LocalFile(dir, bad)!);
        Assert.Null(n); Assert.Contains("Prüfsumme", e2[0]);
    }

    [Fact(DisplayName = "X-04 Katalog: nur GitHub-https oder lokale ZIP, Prüfsumme Pflicht, Kennung muss passen")]
    public void Rules()
    {
        var h = new string('a', 64);
        var json = $$"""
        [{"id":"a.ok","name":"A","version":"1.0.0","kind":"text","description":"","url":"https://raw.githubusercontent.com/x/y/main/a.zip","sha256":"{{h}}"},
         {"id":"b.http","name":"B","version":"1.0.0","kind":"text","description":"","url":"http://github.com/b.zip","sha256":"{{h}}"},
         {"id":"c.fremd","name":"C","version":"1.0.0","kind":"text","description":"","url":"https://example.com/c.zip","sha256":"{{h}}"},
         {"id":"d.nohash","name":"D","version":"1.0.0","kind":"text","description":"","url":"d.zip","sha256":""},
         {"id":"e.escape","name":"E","version":"1.0.0","kind":"text","description":"","url":"../e.zip","sha256":"{{h}}"},
         {"id":"f.exe","name":"F","version":"1.0.0","kind":"programm","description":"","url":"f.zip","sha256":"{{h}}"}]
        """;
        var (ok, err) = ExtensionCatalog.Parse(json);
        Assert.Equal(["a.ok"], ok.Select(e => e.Id)); Assert.Equal(5, err.Count);
        Assert.Single(ExtensionCatalog.Parse("kaputt").Errors);
        // Kennung passt nicht zum Eintrag
        var root = Tmp(); var dir = Path.Combine(root, "kat"); ExtensionCatalog.BuildLocal(dir);
        var e0 = ExtensionCatalog.Parse(File.ReadAllText(Path.Combine(dir, ExtensionCatalog.FileName))).Entries[0];
        var (n, er) = ExtensionCatalog.InstallVerified(new ExtensionStore(root), e0 with { Id = "anderer.name" }, ExtensionCatalog.LocalFile(dir, e0)!);
        Assert.Null(n); Assert.Contains("Katalogeintrag", er[0]);
    }
}
