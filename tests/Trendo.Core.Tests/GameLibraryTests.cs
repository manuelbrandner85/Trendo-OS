using System.Security.Cryptography;
using Trendo.Core;
using Trendo.Core.ConsolePort;
using Xunit;

namespace Trendo.Core.Tests;

public class GameLibraryTests
{
    sealed class Fake(int code, byte[] body) : IWebFetch
    {
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default) => Task.FromResult(new WebReply(code, body));
    }

    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-lib-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Bibliothek: IDs eindeutig, jeder Eintrag beschrieben und lizenziert")]
    public void Catalog_complete()
    {
        Assert.Equal(GameLibrary.Entries.Count, GameLibrary.Entries.Select(e => e.Id).Distinct().Count());
        Assert.All(GameLibrary.Entries, e => { Assert.NotEmpty(e.Title); Assert.NotEmpty(e.Description); Assert.NotEmpty(e.License); });
        Assert.Contains(GameLibrary.Entries, e => e.Kind == GameLibrary.Kind.Eigen && e.Genre.Contains("Open World"));
    }

    [Fact(DisplayName = "Bibliothek: Keine bekannten Kaufspiele und keine Konsolen-Marken im Katalog")]
    public void No_known_titles()
    {
        string[] banned = ["gta", "grand theft", "red dead", "zelda", "mario", "sonic", "god of war", "last of us", "uncharted", "halo", "final fantasy", "metal gear", "dark souls", "elden ring", "gran turismo", "resident evil", "playstation", "xbox", "nintendo"];
        Assert.All(GameLibrary.Entries, e => Assert.DoesNotContain(banned, b => (e.Title + " " + e.Description + " " + e.Genre + " " + e.Style).Contains(b, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact(DisplayName = "Bibliothek: Downloads haben feste Prüfsummen, freie Spiele einen festen Stand")]
    public void Pinned()
    {
        Assert.Equal(40, GameLibrary.DemoCommit.Length);
        Assert.Equal(64, GameLibrary.MgbaSha256.Length);
        Assert.All(GameLibrary.Entries.Where(e => e.Kind == GameLibrary.Kind.Homebrew), e => { Assert.Equal(64, e.Sha256!.Length); Assert.StartsWith("https://github.com/", e.Url); });
        var steps = GameLibrary.FetchSteps(GameLibrary.Find("demo-truck")!).Select(a => string.Join(' ', a)).ToList();
        Assert.Contains(steps, s => s.Contains(GameLibrary.DemoCommit));
        Assert.Contains(steps, s => s.EndsWith("3d/truck_town"));
    }

    [Fact(DisplayName = "Bibliothek: Jeder Auftrag verbietet den Nachbau bestehender Spiele")]
    public void Prompts_carry_rule()
    {
        var own = GameLibrary.Find("morvane")!;
        var free = GameLibrary.Find("demo-truck")!;
        var wish = GameLibrary.BuildPrompt(GameLibrary.Wish("so etwas wie GTA"), "C:/p");
        Assert.Contains("so etwas wie GTA", wish);
        foreach (var p in new[] { wish, GameLibrary.BuildPrompt(own, "C:/p"), GameLibrary.ChangePrompt(own, "mehr Autos", "C:/p"), GameLibrary.ChangePrompt(free, "Regen", "C:/p"), GameLibrary.ExportPrompt(own, "android", "C:/p") })
            Assert.Contains("Baue kein bestehendes Spiel nach, auch nicht umbenannt", p);
        Assert.Contains("Lizenz- und Urheberhinweise", GameLibrary.ChangePrompt(free, "Regen", "C:/p"));
        Assert.Throws<ArgumentException>(() => GameLibrary.ExportPrompt(own, "dreamcast", "C:/p"));
        Assert.Throws<ArgumentException>(() => GameLibrary.BuildPrompt(free, "C:/p"));
    }

    [Fact(DisplayName = "Bibliothek: Leerer oder zu langer Wunsch wird abgewiesen")]
    public void Wish_checked()
    {
        Assert.NotNull(GameLibrary.CheckWish("  "));
        Assert.NotNull(GameLibrary.CheckWish(new string('x', GameLibrary.MaxWish + 1)));
        Assert.Null(GameLibrary.CheckWish("Rennspiel an der Küste"));
    }

    [Fact(DisplayName = "Bibliothek: Projektordner liegt beim Eintrag, freie Spiele im Unterordner")]
    public void Project_dirs()
    {
        Assert.Equal(Path.Combine("b", "morvane"), GameLibrary.ProjectDir("b", GameLibrary.Find("morvane")!));
        Assert.Equal(Path.Combine("b", "demo-truck", "3d", "truck_town"), GameLibrary.ProjectDir("b", GameLibrary.Find("demo-truck")!));
    }

    [Fact(DisplayName = "Bibliothek: Download behält die Datei nur mit richtiger Prüfsumme")]
    public async Task Download_checks_hash()
    {
        var d = Temp();
        try
        {
            byte[] body = [1, 2, 3, 4];
            var sha = Convert.ToHexString(SHA256.HashData(body));
            var dest = Path.Combine(d, "sub", "spiel.gb");
            var ok = new Fake(200, body);
            Assert.Null(await GameLibrary.DownloadAsync(ok, "https://x.test/a", sha, dest));
            Assert.Equal(body, File.ReadAllBytes(dest));

            var bad = Path.Combine(d, "falsch.gb");
            Assert.Contains("Prüfsumme", await GameLibrary.DownloadAsync(ok, "https://x.test/a", new string('0', 64), bad));
            Assert.False(File.Exists(bad));
            Assert.False(File.Exists(bad + ".part"));

            var nf = new Fake(404, []);
            Assert.Contains("404", await GameLibrary.DownloadAsync(nf, "https://x.test/a", sha, bad));
        }
        finally { Directory.Delete(d, true); }
    }
}
