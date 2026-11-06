using System.IO.Compression;
using System.Text;
using Trendo.Core;
using Trendo.Core.Bibliothek;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Freie 3D-Modelle aus weiteren Quellen (Q1–Q4): Thingiverse, Smithsonian, NASA, Sketchfab, Download-Übernahme, Lizenzen.</summary>
public class FreeModelsTests
{
    sealed class Fake(Dictionary<string, byte[]> map) : IWebFetchAuth
    {
        public List<string> Urls = []; public List<IReadOnlyDictionary<string, string>> Headers = [];
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default)
        {
            Urls.Add(url);
            var hit = map.FirstOrDefault(kv => url.StartsWith(kv.Key, StringComparison.Ordinal));
            return Task.FromResult(hit.Key is null ? new WebReply(404, []) : new WebReply(200, hit.Value));
        }
        public Task<WebReply> GetAsync(string url, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default) { Headers.Add(headers); return GetAsync(url, ct); }
    }
    static byte[] B(string s) => Encoding.UTF8.GetBytes(s);
    static string Root() => Path.Combine(Path.GetTempPath(), "trendo-freie-" + Guid.NewGuid().ToString("N")[..6]);

    [Fact(DisplayName = "CORE-05 Q3: Lizenzen – CC0, CC BY, CC BY-SA und NASA sind kommerziell nutzbar, NC nicht; Namensnennung bei allem außer CC0/gemeinfrei")]
    public void Licenses()
    {
        Assert.True(FreeModels.Commercial("CC0 1.0")); Assert.True(FreeModels.Commercial("Creative Commons - Attribution")); Assert.True(FreeModels.Commercial("CC BY-SA 4.0"));
        Assert.True(FreeModels.Commercial("Gemeinfrei (NASA-Nutzungsregeln)"));
        Assert.False(FreeModels.Commercial("Creative Commons - Attribution - Non-Commercial")); Assert.False(FreeModels.Commercial("CC BY-NC 4.0")); Assert.False(FreeModels.Commercial("Standard-Lizenz der Seite (nur privat)"));
        Assert.False(FreeModels.NeedsCredit("CC0 1.0")); Assert.True(FreeModels.NeedsCredit("CC BY 4.0"));
    }

    [Fact(DisplayName = "CORE-05 Q1: Thingiverse über die offizielle Schnittstelle – ohne Schlüssel klare Meldung, Suche mit Schlüssel, Laden holt Lizenz und die STL, Lizenzdatei mit Namensnennung")]
    public async Task Thingiverse()
    {
        var root = Root();
        var fake = new Fake(new()
        {
            ["https://api.thingiverse.com/search/"] = B("""{"total":1,"hits":[{"id":42,"name":"Kabelhalter","thumbnail":"https://cdn.thingiverse.com/a.jpg","public_url":"https://www.thingiverse.com/thing:42","creator":{"name":"Erika Muster"}}]}"""),
            ["https://api.thingiverse.com/things/42/files"] = B("""[{"name":"bild.png","download_url":"https://api.thingiverse.com/files/1/download"},{"name":"halter.stl","download_url":"https://api.thingiverse.com/files/2/download"}]"""),
            ["https://api.thingiverse.com/things/42?"] = B("""{"license":"Creative Commons - Attribution","creator":{"name":"Erika Muster"}}"""),
            ["https://api.thingiverse.com/files/2/download"] = B("solid halter\nendsolid halter\n"),
        });
        try
        {
            var fm = new FreeModels(fake, root);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fm.Search(FreeModels.Thingiverse, "halter", null));
            var hits = await fm.Search(FreeModels.Thingiverse, "Kabel", "tok123");
            var h = Assert.Single(hits); Assert.Equal(("42", "Kabelhalter", "Erika Muster"), (h.Id, h.Name, h.Author));
            var item = await fm.Download(new AssetLibrary(fake, root), h, "tok123");
            Assert.EndsWith("halter.stl", item.File); Assert.Equal("Creative Commons - Attribution", item.License);
            Assert.Contains("access_token=tok123", fake.Urls.Last());
            var lic = File.ReadAllText(Path.Combine(Path.GetDirectoryName(item.File)!, "LIZENZ.txt"));
            Assert.Contains("Namensnennung", lic); Assert.Contains("Kommerzielle Nutzung: erlaubt", lic);
            Assert.Single(new AssetLibrary(fake, root).Items());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "CORE-05 Q1: Smithsonian (nur CC0 mit 3D-Datei, GLB bevorzugt), NASA über GitHub (Suche im Namen, LFS-Dateien über den Medien-Server), Sketchfab (Suche frei, Laden nur mit eigenem Schlüssel im Kopf der Anfrage)")]
    public async Task Smithsonian_Nasa_Sketchfab()
    {
        var root = Root();
        var media = """{"descriptiveNonRepeating":{"record_link":"https://3d.si.edu/x","metadata_usage":{"access":"CC0"},"online_media":{"media":[{"thumbnail":"https://ids.si.edu/t.jpg","resources":[{"url":"https://3d-api.si.edu/f/mammut.obj"},{"url":"https://3d-api.si.edu/f/mammut.glb"}]}]}}}""";
        var fake = new Fake(new()
        {
            ["https://api.si.edu/openaccess/api/v1.0/search"] = B("""{"response":{"rows":[{"id":"ld1-1","title":"Mammut","unitCode":"NMNH","content":""" + media + """},{"id":"ld1-2","title":"Gemälde","content":{"descriptiveNonRepeating":{"metadata_usage":{"access":"Usage conditions apply"}}}}]}}"""),
            ["https://api.si.edu/openaccess/api/v1.0/content/ld1-1"] = B("""{"response":{"content":""" + media + """}}"""),
            ["https://3d-api.si.edu/f/mammut.glb"] = B("glTF"),
            ["https://api.github.com/repos/nasa/"] = B("""{"tree":[{"path":"3D Models/Mars Rover/rover.glb"},{"path":"3D Models/Mars Rover/rover.stl"},{"path":"3D Models/Hubble/hubble.glb"},{"path":"Images/a.png"}]}"""),
            ["https://raw.githubusercontent.com/nasa/NASA-3D-Resources/master/3D%20Models/Mars%20Rover/rover.glb"] = B("version https://git-lfs.github.com/spec/v1\noid sha256:abc\n"),
            ["https://media.githubusercontent.com/media/nasa/"] = B("glTF-echt"),
            ["https://api.sketchfab.com/v3/search"] = B("""{"results":[{"uid":"abc1","name":"Laterne","license":{"label":"CC Attribution"},"user":{"displayName":"Erika Muster"},"viewerUrl":"https://sketchfab.com/3d-models/abc1","faceCount":1200,"thumbnails":{"images":[{"width":1024,"url":"https://media.sketchfab.com/big.jpg"},{"width":256,"url":"https://media.sketchfab.com/small.jpg"}]}}]}"""),
            ["https://api.sketchfab.com/v3/models/abc1/download"] = B("""{"glb":{"url":"https://sketchfab-prod-media.s3.amazonaws.com/archives/abc1.glb"}}"""),
            ["https://sketchfab-prod-media.s3.amazonaws.com/archives/abc1.glb"] = B("glTF-laterne"),
        });
        try
        {
            var fm = new FreeModels(fake, root); var lib = new AssetLibrary(fake, root);
            var si = Assert.Single(await fm.Search(FreeModels.Smithsonian, "Mammut", "key1")); // das Gemälde ohne CC0 fällt weg
            Assert.Equal("CC0 1.0", si.License);
            var siItem = await fm.Download(lib, si, "key1"); Assert.EndsWith("mammut.glb", siItem.File);
            var nasa = await fm.Search(FreeModels.Nasa, "rover", null);
            Assert.Equal("Mars Rover", Assert.Single(nasa).Name);
            var nasaItem = await fm.Download(lib, nasa[0], null);
            Assert.Equal("glTF-echt", File.ReadAllText(nasaItem.File)); // LFS-Zeiger erkannt, echte Datei geholt
            Assert.Equal(2, (await fm.Search(FreeModels.Nasa, "", null)).Count);
            var sk = Assert.Single(await fm.Search(FreeModels.Sketchfab, "Laterne", null));
            Assert.Equal(("CC Attribution", 1200, "https://media.sketchfab.com/small.jpg"), (sk.License, sk.Polycount, sk.ThumbUrl));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fm.Download(lib, sk, null));
            var skItem = await fm.Download(lib, sk, "sk-geheim");
            Assert.Equal("glTF-laterne", File.ReadAllText(skItem.File));
            Assert.Equal("Token sk-geheim", fake.Headers.Single()["Authorization"]);
            Assert.DoesNotContain(fake.Urls, u => u.Contains("sk-geheim")); // Schlüssel nie in der Adresse
            Assert.Equal(3, lib.Items().Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "CORE-05 Q2: Download-Übernahme – nur neue, fertig geladene Modelldateien; ZIP wird entpackt (ohne Pfad-Ausbruch), Lizenz und Seite kommen in die Bibliothek")]
    public void Pickup()
    {
        var root = Root(); var dl = Path.Combine(root, "Downloads"); Directory.CreateDirectory(dl);
        try
        {
            var old = Path.Combine(dl, "alt.stl"); File.WriteAllText(old, "x"); File.SetLastWriteTime(old, DateTime.Now.AddDays(-2));
            File.WriteAllText(Path.Combine(dl, "halb.stl.crdownload"), "x"); File.WriteAllText(Path.Combine(dl, "notiz.txt"), "x");
            var zip = Path.Combine(dl, "vase.zip");
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) { using (var e = z.CreateEntry("files/vase.3mf").Open()) e.Write(B("3mf")); using (var e = z.CreateEntry("../../boese.stl").Open()) e.Write(B("x")); }
            var files = DownloadPickup.NewFiles(dl, DateTime.Now.AddHours(-1));
            Assert.Equal(["vase.zip"], files.Select(Path.GetFileName));
            var fm = new FreeModels(new Fake([]), root); var lib = new AssetLibrary(new Fake([]), root);
            var item = fm.Pickup(lib, files[0], "Printables", "CC BY 4.0", "Erika Muster", "https://www.printables.com");
            Assert.EndsWith("vase.3mf", item.File); Assert.Equal("modell", item.Kind);
            Assert.False(File.Exists(Path.Combine(root, "boese.stl"))); Assert.False(File.Exists(Path.Combine(root, "..", "boese.stl")));
            Assert.Contains("Erika Muster", File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(item.File)!)!, "LIZENZ.txt")));
            Assert.Equal("https://www.printables.com/model?q=Vase%20blau", DownloadPickup.SearchUrl(DownloadPickup.Sites[0], " Vase blau "));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
