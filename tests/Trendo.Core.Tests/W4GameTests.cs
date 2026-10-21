using System.Text.Json;
using Trendo.Core;
using Xunit;

public class W4_Core06_Game
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-game-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "CORE-06: Alle Browser-Spiele werden angelegt, JavaScript ist gültig (node --check)")]
    public void Browser_games()
    {
        var root = Tmp();
        foreach (var t in GameStudio.Templates)
        {
            var dir = GameStudio.CreateBrowserGame(root, t.Id, "Spiel " + t.Id);
            Assert.Contains("game.js", File.ReadAllText(Path.Combine(dir, "index.html")));
            if (Cli.Find("node") is not null) { var r = Cli.Run("node", ["--check", Path.Combine(dir, "game.js")]); Assert.True(r.Ok, t.Id + r.Err); }
        }
        Assert.Throws<IOException>(() => GameStudio.CreateBrowserGame(root, "snake", "Spiel snake"));
        Assert.Contains("&lt;x&gt;", File.ReadAllText(Path.Combine(GameStudio.CreateBrowserGame(root, "jump", "<x>"), "index.html")));
    }

    [Fact(DisplayName = "CORE-06: Unreal-Engines aus Launcher-Liste erkannt, neueste zuerst; .uproject ist gültig")]
    public void Unreal()
    {
        var json = """{"InstallationList":[{"InstallLocation":"C:\\UE_5.3","AppName":"UE_5.3"},{"InstallLocation":"C:\\Fortnite","AppName":"Fortnite"},{"InstallLocation":"C:\\UE_5.5","AppName":"UE_5.5"}]}""";
        var e = GameStudio.ParseLauncherInstalled(json);
        Assert.Equal(["5.5", "5.3"], e.Select(x => x.Version));
        Assert.Empty(GameStudio.ParseLauncherInstalled("kaputt"));
        var dir = GameStudio.CreateUnrealProject(Tmp(), "3 Mein Spiel!", "5.5");
        var up = Directory.GetFiles(dir, "*.uproject").Single();
        Assert.Equal("Spiel3MeinSpiel.uproject", Path.GetFileName(up));
        using var d = JsonDocument.Parse(File.ReadAllText(up));
        Assert.Equal("5.5", d.RootElement.GetProperty("EngineAssociation").GetString());
        Assert.EndsWith(Path.Combine("Binaries", "Win64", "UnrealEditor.exe"), GameStudio.EditorExe(e[0]));
    }
}
