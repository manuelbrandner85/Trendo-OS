using System.Text;
using Trendo.Core;
using Trendo.Core.Bibliothek;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Freie Bilder & Klänge – Profi 29 (05.11.2026): ähnliche Bilder (FM-12), Merklisten (FM-13), Schriftvorschau.</summary>
public class MediaCollectionsTests
{
    sealed class Fetch(Func<string, string> f) : IWebFetch
    {
        public List<string> Urls { get; } = [];
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default) { Urls.Add(url); return Task.FromResult(new WebReply(200, Encoding.UTF8.GetBytes(f(url)))); }
    }

    static MediaHit Hit(string id, string license, string title = "Berg im Nebel") =>
        new(id, "bild", title, "Erika Muster", license, "https://creativecommons.org/licenses/by-sa/4.0/", "https://example.org/" + id, "https://upload.wikimedia.org/" + id + ".jpg", "", "jpg", 1000, 640, 480, 0);

    const string Related = """{"results":[{"id":"r2","title":"Nebel am See","url":"https://live.staticflickr.com/r2.jpg","license":"by","license_version":"2.0","filetype":"jpg","creator":"Max Beispiel"},{"id":"r1","title":"Selbst","url":"https://live.staticflickr.com/r1.jpg","license":"cc0","filetype":"jpg"},{"id":"r3","title":"Gesperrt","url":"https://live.staticflickr.com/r3.jpg","license":"by-nc","filetype":"jpg"}]}""";

    [Fact(DisplayName = "FM-12 Ähnliche Bilder: Openverse „related“ (ohne das Ausgangsbild, ohne NC), Commons über Stichworte aus dem Titel")]
    public async Task Similar()
    {
        Assert.Equal("https://api.openverse.org/v1/images/r1/related/", MediaLibrary.RelatedUrl(Hit("ov-r1", "CC0")));
        Assert.Null(MediaLibrary.RelatedUrl(Hit("123", "CC0")));
        Assert.Equal("Berg Nebel", MediaLibrary.Keywords("File:Berg im Nebel.jpg"));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mc-" + Guid.NewGuid().ToString("N")[..6]);
        var f = new Fetch(u => u.Contains("/related/") ? Related : u.Contains("openverse") ? """{"results":[]}""" : """{"query":{"pages":{}}}""");
        var lib = new MediaLibrary(f, dir);
        var rel = await lib.Related(Hit("ov-r1", "CC0"));
        Assert.Equal(["Nebel am See"], rel.Select(h => h.Title));
        await lib.Related(Hit("123", "CC0"));
        Assert.Contains(f.Urls, u => u.Contains("Berg") && u.Contains("Nebel")); // Commons: Suche mit Stichworten
        await Assert.ThrowsAsync<InvalidOperationException>(() => lib.Related(Hit("124", "CC0", "IMG 01.jpg")));
    }

    [Fact(DisplayName = "FM-13 Merklisten: anlegen beim Merken, doppelt nur einmal, NC/ND abgelehnt, Namensnennungen (gemeinfreie zuletzt) als Text/HTML, umbenennen, entfernen, löschen")]
    public void Collections()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mc-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var c = new MediaCollections(dir);
            Assert.True(c.Add("Projekt Sommerfest", Hit("a", "CC BY-SA 4.0")));
            Assert.False(c.Add("projekt sommerfest", Hit("a", "CC BY-SA 4.0")));
            Assert.True(c.Add("Projekt Sommerfest", Hit("b", "CC0", "See")));
            Assert.Throws<InvalidOperationException>(() => c.Add("Projekt Sommerfest", Hit("x", "CC BY-NC 4.0")));
            Assert.Throws<InvalidOperationException>(() => c.Add("Projekt Sommerfest", Hit("y", "CC BY-ND 4.0")));
            Assert.Throws<InvalidOperationException>(() => c.Add("Projekt Sommerfest", Hit("z", "Alle Rechte vorbehalten")));
            Assert.Throws<ArgumentException>(() => c.Add("  ", Hit("a", "CC0")));
            var all = new MediaCollections(dir).All();
            Assert.Single(all); Assert.Equal(2, all[0].Entries.Count);
            var text = c.Credits("Projekt Sommerfest");
            Assert.StartsWith("„Berg im Nebel“ von Erika Muster, CC BY-SA 4.0", text); Assert.Contains("„See“", text.Split('\n')[1]);
            Assert.StartsWith("<ul>", c.Credits("Projekt Sommerfest", "html"));
            c.Rename("Projekt Sommerfest", "Sommerfest 2026"); Assert.Equal("Sommerfest 2026", c.All()[0].Name);
            c.Remove("Sommerfest 2026", "a"); Assert.Single(c.All()[0].Entries);
            c.Delete("Sommerfest 2026"); Assert.Empty(c.All());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Schriftvorschau: eine Stärke wird zwischengespeichert, keine Schrift-Datei → Fehler, keine offene Lizenz → Fehler")]
    public async Task Font_preview()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mc-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var calls = 0;
            var ok = new FreeFontsPreview(new BytesFetch(() => { calls++; return [0, 1, 0, 0, .. new byte[2000]]; }), dir);
            var f = new FontHit("lora", "Lora", "serif", "OFL-1.1", [400, 700], false);
            var file = await ok.File400(f); Assert.True(File.Exists(file)); await ok.File400(f); Assert.Equal(1, calls);
            await Assert.ThrowsAsync<InvalidDataException>(() => new FreeFontsPreview(new BytesFetch(() => "<html>"u8.ToArray()), dir).File400(f with { Id = "kaputt" }));
            await Assert.ThrowsAsync<InvalidDataException>(() => ok.File400(f with { Id = "zu", License = "Proprietär" }));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    sealed class BytesFetch(Func<byte[]> b) : IWebFetch { public Task<WebReply> GetAsync(string url, CancellationToken ct = default) => Task.FromResult(new WebReply(200, b())); }
}
