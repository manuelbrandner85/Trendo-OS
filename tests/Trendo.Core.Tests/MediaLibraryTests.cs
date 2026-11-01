using System.Text;
using Trendo.Core;
using Trendo.Core.Bibliothek;
using Xunit;

namespace Trendo.Core.Tests;

/// Medienbrowser: Wikimedia Commons lesen (Lizenz, Urheber ohne HTML, Format), filtern, Vorschau und Laden nur von Commons-Adressen, Herkunft immer dabei.
public class MediaLibraryTests
{
    public sealed class FakeFetch(Func<string, WebReply> f) : IWebFetch
    {
        public List<string> Urls { get; } = [];
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default) { Urls.Add(url); return Task.FromResult(f(url)); }
    }

    static string Page(int id, int index, string title, string lic, string artist, string mime, long size, string? thumb = null, double dur = 0)
    {
        var t = Uri.EscapeDataString(title);
        return "\"" + id + "\":{\"pageid\":" + id + ",\"ns\":6,\"title\":\"File:" + title + "\",\"index\":" + index + ",\"imageinfo\":[{\"size\":" + size + ",\"width\":800,\"height\":600,\"duration\":" + dur.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ",\"thumburl\":\"" + (thumb ?? "https://commons.wikimedia.org/w/resources/assets/file-type-icons/fileicon-ogg.png") + "\""
            + ",\"url\":\"https://upload.wikimedia.org/wikipedia/commons/a/ab/" + t + "?utm_source=commons\",\"descriptionurl\":\"https://commons.wikimedia.org/wiki/File:" + t + "\",\"mime\":\"" + mime + "\""
            + ",\"extmetadata\":{\"LicenseShortName\":{\"value\":\"" + lic + "\"},\"Artist\":{\"value\":\"" + artist + "\"},\"LicenseUrl\":{\"value\":\"https://creativecommons.org/licenses/by-sa/4.0\"}}}]}";
    }

    public static string Images => "{\"query\":{\"pages\":{" + string.Join(",",
        Page(11, 2, "Berg im Nebel.jpg", "CC BY-SA 4.0", "<a href=\\\"//commons.wikimedia.org/wiki/User:Erika\\\">Erika Muster</a>", "image/jpeg", 2_000_000, "https://upload.wikimedia.org/thumb/berg.jpg/320px-berg.jpg"),
        Page(12, 1, "See.png", "CC0", "Max Beispiel", "image/png", 500_000, "https://upload.wikimedia.org/thumb/see.png/320px-see.png"),
        Page(13, 3, "Gesperrt.jpg", "CC BY-NC 2.0", "X", "image/jpeg", 100, "https://upload.wikimedia.org/x.jpg"),
        Page(14, 4, "Alt.jpg", "Public domain", "Unbekannt &amp; Co.", "image/jpeg", 100, "https://evil.example/x.jpg"),
        Page(15, 5, "Text.pdf", "CC0", "X", "application/pdf", 100)) + "}}}";

    [Fact(DisplayName = "Medienbrowser: Commons-Antwort lesen – Reihenfolge der Suche, Urheber ohne HTML, Titel ohne Endung, nicht-kommerzielle Lizenz und fremde Formate fallen heraus; Filter nach Lizenz und Format")]
    public void Parse_and_filter()
    {
        var all = MediaLibrary.Parse(Images, "bild");
        Assert.Equal(["See", "Berg im Nebel", "Alt"], all.Select(h => h.Title));
        var berg = all[1];
        Assert.Equal("Erika Muster", berg.Creator); Assert.Equal("jpg", berg.Format); Assert.Equal(2_000_000, berg.Bytes);
        Assert.Equal("https://upload.wikimedia.org/wikipedia/commons/a/ab/Berg%20im%20Nebel.jpg", berg.FileUrl); // ohne Nachverfolgung
        Assert.False(berg.Free); Assert.Contains("von Erika Muster, CC BY-SA 4.0", berg.Credit);
        Assert.True(all[0].Free); Assert.True(all[2].Free); Assert.Equal("Unbekannt & Co.", all[2].Creator);
        Assert.Equal(["See", "Alt"], MediaLibrary.Parse(Images, "bild", "frei").Select(h => h.Title));
        Assert.Equal(["Berg im Nebel"], MediaLibrary.Parse(Images, "bild", "nennung").Select(h => h.Title));
        Assert.Equal(["See"], MediaLibrary.Parse(Images, "bild", "alle", "png").Select(h => h.Title));
        Assert.Empty(MediaLibrary.Parse("{\"batchcomplete\":\"\"}", "bild"));
        Assert.Contains("filetype%3Aaudio", MediaLibrary.SearchUrl("klang", "Regen"));
    }

    [Fact(DisplayName = "Medienbrowser: Vorschau und Laden nur von Commons, Größengrenze, Datei mit Herkunftsdatei in „Bilder“, Verzeichnis und Namensnennungen; zweites Laden lädt nicht erneut")]
    public async Task Download()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-medien-" + Guid.NewGuid().ToString("N"));
        var fetch = new FakeFetch(url => new WebReply(200, url.Contains("api.php") ? Encoding.UTF8.GetBytes(Images) : Encoding.UTF8.GetBytes("BILD")));
        var lib = new MediaLibrary(fetch, root);
        await Assert.ThrowsAsync<ArgumentException>(() => lib.Search("bild", " "));
        var hits = await lib.Search("bild", "Berg");
        Assert.NotNull(await lib.Thumb(hits[0]));
        Assert.Null(await lib.Thumb(hits[2])); // fremde Adresse
        var n = fetch.Urls.Count; await lib.Thumb(hits[0]); Assert.Equal(n, fetch.Urls.Count); // zwischengespeichert
        var item = await lib.Download(hits[1]);
        Assert.Equal(Path.Combine(root, "Bilder", "Berg_im_Nebel.jpg"), item.File); Assert.Equal("BILD", File.ReadAllText(item.File));
        Assert.Contains("Urheber: Erika Muster", File.ReadAllText(item.File + ".herkunft.txt"));
        Assert.Contains("Berg im Nebel", File.ReadAllText(Path.Combine(root, "HERKUNFT.txt")));
        n = fetch.Urls.Count; Assert.Equal(item.File, (await lib.Download(hits[1])).File); Assert.Equal(n, fetch.Urls.Count);
        Assert.Single(lib.Items());
        var big = hits[0] with { Bytes = MediaLibrary.MaxBytes + 1 };
        await Assert.ThrowsAsync<InvalidDataException>(() => lib.Download(big));
        await Assert.ThrowsAsync<InvalidDataException>(() => lib.Download(hits[0] with { Id = "x", FileUrl = "https://evil.example/a.jpg" }));
        await Assert.ThrowsAsync<InvalidDataException>(() => lib.Preview(hits[0] with { Bytes = MediaLibrary.MaxPreview + 1 }));
    }
}
