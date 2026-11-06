using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Freie Spiele-Assets (G1–G3): Godot Asset Library, Asset-Liste mit Lizenzen, Abspann, Lizenzprüfung, Einbau ins gebaute Spiel.</summary>
public class GameAssetsTests
{
    sealed class Fake(Dictionary<string, byte[]> map) : IWebFetch
    {
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default)
        {
            var hit = map.FirstOrDefault(kv => url.StartsWith(kv.Key, StringComparison.Ordinal));
            return Task.FromResult(hit.Key is null ? new WebReply(404, []) : new WebReply(200, hit.Value));
        }
    }
    static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact(DisplayName = "CORE-06 G1: Godot Asset Library – Suche mit Titel, Autor, Lizenz; Laden nur von GitHub/GitLab, ZIP sicher entpackt; fremde Download-Adresse abgelehnt")]
    public async Task Godot()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ga-" + Guid.NewGuid().ToString("N")[..6]);
        var zipBytes = new MemoryStream();
        using (var z = new ZipArchive(zipBytes, ZipArchiveMode.Create, true)) { using (var e = z.CreateEntry("paket/figur.png").Open()) e.Write([1, 2]); using (var e = z.CreateEntry("../boese.png").Open()) e.Write([1]); }
        var fake = new Fake(new()
        {
            ["https://godotengine.org/asset-library/api/asset?"] = B("""{"result":[{"asset_id":"77","title":"Pixel Figuren","author":"Erika Muster","cost":"CC0","category":"2D Tools","icon_url":"https://raw.githubusercontent.com/x/i.png"}]}"""),
            ["https://godotengine.org/asset-library/api/asset/77"] = B("""{"download_url":"https://github.com/erika/pixel/archive/abc.zip"}"""),
            ["https://godotengine.org/asset-library/api/asset/88"] = B("""{"download_url":"https://boese.example/x.zip"}"""),
            ["https://github.com/erika/pixel/archive/abc.zip"] = zipBytes.ToArray(),
        });
        try
        {
            var h = Assert.Single(await GameAssets.SearchGodot(fake, "pixel"));
            Assert.Equal(("77", "Pixel Figuren", "Erika Muster", "CC0"), (h.Id, h.Title, h.Author, h.License));
            var dir = await GameAssets.DownloadGodot(fake, h, root);
            Assert.True(File.Exists(Path.Combine(dir, "paket", "figur.png"))); Assert.False(File.Exists(Path.Combine(root, "boese.png")));
            await Assert.ThrowsAsync<InvalidDataException>(() => GameAssets.DownloadGodot(fake, h with { Id = "88" }, root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "CORE-06 G2/G3: Asset-Liste – Kopie im Projekt, Verwendung passt zur Datei, Bildstreifen-Bilder aus dem Seitenverhältnis, Abspann nach Quellen, Prüfung meldet NC-Lizenz, fehlenden Urheber und fehlende Lizenz")]
    public void List_credits_check()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ga-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(root);
        try
        {
            var png = Path.Combine(root, "lauf.png"); File.WriteAllBytes(png, [1]); var wav = Path.Combine(root, "sprung.wav"); File.WriteAllBytes(wav, [1]);
            var d = new JsonObject { ["title"] = "Münzjäger" };
            var a = GameAssets.Add(d, Path.Combine(root, "assets"), png, "Kenney", "CC0 1.0", "Kenney", "https://kenney.nl", "anim-laufen");
            Assert.True(File.Exists(a["file"]!.GetValue<string>()));
            Assert.Throws<ArgumentException>(() => GameAssets.Add(d, Path.Combine(root, "assets"), wav, "Kenney", "CC0 1.0", "", "", "figur")); // Ton als Figur passt nicht
            GameAssets.Add(d, Path.Combine(root, "assets"), wav, "OpenGameArt", "CC BY-NC 4.0", "", "https://opengameart.org/x", "ton-springen");
            GameAssets.Add(d, Path.Combine(root, "assets"), png, "itch.io", "", "Erika Muster", "", "hintergrund");
            Assert.Equal(4, GameAssets.FramesOf(128, 32)); Assert.Equal(1, GameAssets.FramesOf(32, 32));
            var head = new byte[26]; new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 128, 0, 0, 0, 32 }.CopyTo(head, 0);
            var pngHead = Path.Combine(root, "streifen.png"); File.WriteAllBytes(pngHead, head);
            Assert.Equal((128, 32), GameAssets.ImageSize(pngHead)); Assert.Null(GameAssets.ImageSize(wav));
            var credits = GameAssets.Credits(d);
            Assert.StartsWith("Münzjäger – Mitwirkende", credits); Assert.Contains("Kenney\n  „lauf“ von Kenney – CC0 1.0", credits); Assert.Contains("OpenGameArt", credits);
            var w = GameAssets.Check(d, commercial: true);
            Assert.Contains(w, x => x.Contains("verkauften Spiel")); Assert.Contains(w, x => x.Contains("Urheber fehlt")); Assert.Contains(w, x => x.Contains("Lizenz fehlt"));
            Assert.DoesNotContain(GameAssets.Check(d, commercial: false), x => x.Contains("verkauften Spiel"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "CORE-06 G2/G3: Gebautes Spiel bekommt Figur- und Hintergrundbild aus der Asset-Liste und den Abspann als CREDITS.txt")]
    public void Build_with_assets()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ga-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(root);
        try
        {
            var png = Path.Combine(root, "held.png"); File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
            var d = new JsonObject { ["browser"] = "plattform", ["title"] = "Asset Probe", ["level"] = new JsonArray("S....Z", "######"), ["playerImage"] = png, ["backgroundImage"] = png };
            GameAssets.Add(d, Path.Combine(root, "assets"), png, "Kenney", "CC0 1.0", "Kenney", "https://kenney.nl", "figur");
            var game = GameDesign.BuildBrowser(d, root);
            Assert.True(File.Exists(Path.Combine(game, "spielfigur.png"))); Assert.True(File.Exists(Path.Combine(game, "hintergrund.png")));
            Assert.Contains("\"playerImage\":\"spielfigur.png\"", File.ReadAllText(Path.Combine(game, "config.js")));
            Assert.Contains("Kenney", File.ReadAllText(Path.Combine(game, "CREDITS.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
}
