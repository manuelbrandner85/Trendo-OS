using System.IO.Compression;
using System.Net;
using System.Text;
using Trendo.Core.Bibliothek;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0279: 3D-Bibliothek aus freien Quellen – ohne Netz, mit nachgestellten Antworten von Poly Haven und ambientCG.</summary>
public class T0279_AssetLibrary
{
    sealed class Fake(Func<Uri, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public readonly List<string> Calls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Calls.Add(r.RequestUri!.ToString()); return Task.FromResult(answer(r.RequestUri!)); }
    }
    static HttpResponseMessage Json(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
    static HttpResponseMessage Bytes(byte[] b) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(b) };
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0279-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    const string Models = """{"wooden_chair":{"name":"Wooden Chair","tags":["chair","seat"],"categories":["furniture","seating"],"authors":{"Erika Muster":"All"},"dimensions":[450,520,900],"polycount":4200},"rock_01":{"name":"Rock 01","tags":["stone"],"categories":["nature"],"authors":{"Max Muster":"All"}}}""";

    static string Files(string include) => """{"gltf":{"1k":{"gltf":{"url":"https://dl.polyhaven.org/file/ph-assets/Models/gltf/1k/wooden_chair/wooden_chair_1k.gltf","include":""" + include + "}}}}";

    [Fact(DisplayName = "T-0279: Deutsche Suche findet englisch verschlagwortete Modelle; Maße mm→cm, Lizenz, Urheber; Liste 1 Tag zwischengespeichert")]
    public async Task Search_german_and_cached()
    {
        var fake = new Fake(u => Json(Models)); var lib = new AssetLibrary(new HttpClient(fake), Temp());
        var hits = await lib.Search("Stuhl");
        var h = Assert.Single(hits); Assert.Equal("wooden_chair", h.Id); Assert.Equal("CC0 1.0", h.License); Assert.Equal("Erika Muster", h.Author);
        Assert.Equal([45.0, 52.0, 90.0], h.SizeCm); Assert.Equal(4200, h.Polycount); Assert.StartsWith("https://cdn.polyhaven.com/", h.ThumbUrl);
        Assert.Equal(2, (await lib.Search("")).Count);
        Assert.Single(fake.Calls); // zweite Suche aus dem Zwischenspeicher
    }

    [Fact(DisplayName = "T-0279: Download holt glTF samt Texturen, schreibt LIZENZ.txt und Index; Entfernen räumt auf")]
    public async Task Download_and_remove()
    {
        var root = Temp();
        var fake = new Fake(u => u.Host == "api.polyhaven.com" ? Json(u.AbsolutePath.StartsWith("/files") ? Files("""{"textures/chair_diff_1k.jpg":{"url":"https://dl.polyhaven.org/file/ph-assets/Models/jpg/1k/wooden_chair/chair_diff_1k.jpg"}}""") : Models) : Bytes([1, 2, 3]));
        var lib = new AssetLibrary(new HttpClient(fake), root);
        var item = await lib.Download((await lib.Search("chair"))[0]);
        Assert.True(File.Exists(item.File)); Assert.EndsWith("wooden_chair_1k.gltf", item.File);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(item.File)!, "textures", "chair_diff_1k.jpg")));
        Assert.Contains("CC0 1.0", File.ReadAllText(Path.Combine(Path.GetDirectoryName(item.File)!, "LIZENZ.txt")));
        Assert.Single(lib.Items()); Assert.Contains("Wooden Chair – Erika Muster", lib.Credits());
        lib.Remove(item); Assert.Empty(lib.Items()); Assert.False(File.Exists(item.File));
    }

    [Fact(DisplayName = "T-0279: Paketliste mit ../-Pfad oder fremder Adresse wird abgewiesen")]
    public async Task Rejects_traversal_and_foreign_hosts()
    {
        foreach (var inc in new[] { """{"../../boese.jpg":{"url":"https://dl.polyhaven.org/x.jpg"}}""", """{"t.jpg":{"url":"https://example.com/t.jpg"}}""" })
        {
            var fake = new Fake(u => u.Host == "api.polyhaven.com" ? Json(u.AbsolutePath.StartsWith("/files") ? Files(inc) : Models) : Bytes([1]));
            var lib = new AssetLibrary(new HttpClient(fake), Temp());
            var hit = (await lib.Search("chair"))[0];
            await Assert.ThrowsAsync<InvalidDataException>(() => lib.Download(hit));
            Assert.DoesNotContain(fake.Calls, c => c.Contains("example.com"));
        }
    }

    [Fact(DisplayName = "T-0279: Materialien von ambientCG – Suche übersetzt, 1K-Paket wird entpackt")]
    public async Task AmbientCg_material()
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true)) foreach (var n in new[] { "Wood049_1K-JPG_Color.jpg", "Wood049_1K-JPG_NormalGL.jpg" }) using (var s = z.CreateEntry(n).Open()) s.Write([1, 2]);
        var fake = new Fake(u => u.Host == "ambientcg.com" && u.AbsolutePath == "/get" ? Bytes(ms.ToArray())
            : u.Query.Contains("include=downloadData") ? Json("""{"foundAssets":[{"assetId":"Wood049","downloadFolders":{"default":{"downloadFiletypeCategories":{"zip":{"downloads":[{"attribute":"1K-JPG","fullDownloadPath":"https://ambientcg.com/get?file=Wood049_1K-JPG.zip"}]}}}}}]}""")
            : Json("""{"foundAssets":[{"assetId":"Wood049","displayName":"Wood 049","displayCategory":"Wood","previewImage":{"256-PNG":"https://acg-media.struffelproductions.com/x.png"}}]}"""));
        var lib = new AssetLibrary(new HttpClient(fake), Temp());
        var hit = Assert.Single(await lib.Search("holz", "material")); Assert.Contains(fake.Calls, c => c.Contains("q=wood"));
        var item = await lib.Download(hit);
        Assert.True(File.Exists(Path.Combine(item.File, "Wood049_1K-JPG_Color.jpg"))); Assert.False(Directory.EnumerateFiles(item.File, "*.zip").Any());
    }

    [Fact(DisplayName = "T-0279: Umwandlung aus anderen Programmen – passendes Blender-Importskript je Format, fremde Formate abgewiesen")]
    public void Converter_script()
    {
        Assert.Contains("import_scene.fbx", ModelConverter.Script(@"C:\m\sessel.fbx", @"C:\o\sessel.glb"));
        Assert.Contains("usd_import", ModelConverter.Script("/m/a.usdz", "/o/a.glb"));
        var blend = ModelConverter.Script("/m/haus.blend", "/o/haus.glb"); Assert.Contains("open_mainfile", blend); Assert.DoesNotContain("read_homefile", blend);
        Assert.Contains("export_format='GLB'", blend);
        Assert.DoesNotContain("'); import os", ModelConverter.Script("/m/x'); import os;('.obj", "/o/x.glb")); // Anführungszeichen im Namen brechen das Skript nicht auf
        Assert.Throws<ArgumentException>(() => ModelConverter.Script("/m/a.exe", "/o/a.glb"));
    }
}
