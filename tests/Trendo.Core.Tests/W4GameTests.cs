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
        var plugins = d.RootElement.GetProperty("Plugins").EnumerateArray().Select(p => p.GetProperty("Name").GetString()).ToList();
        Assert.Contains("PythonScriptPlugin", plugins);
    }

    [Fact(DisplayName = "CORE-06: Unreal unter Programme\\Epic Games und in der Launcher-Liste, Prüfbefehl für den Editor")]
    public void Unreal_pc_paths_and_editor_command()
    {
        var root = Tmp();
        var epic = Path.Combine(root, "Epic Games");
        foreach (var ver in new[] { "5.3", "5.5" })
        {
            var bin = Path.Combine(epic, "UE_" + ver, "Engine", "Binaries", "Win64");
            Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(bin, "UnrealEditor.exe"), "");
            if (ver == "5.5") File.WriteAllText(Path.Combine(bin, "UnrealEditor-Cmd.exe"), "");
        }
        var scanned = GameStudio.ScanEpicFolder(epic);
        Assert.Equal(["5.5", "5.3"], scanned.Select(x => x.Version));
        var custom = Path.Combine(root, "woanders");
        Directory.CreateDirectory(custom);
        var dat = Path.Combine(root, "LauncherInstalled.dat");
        File.WriteAllText(dat, JsonSerializer.Serialize(new { InstallationList = new[] { new { InstallLocation = custom, AppName = "UE_5.4" }, new { InstallLocation = custom, AppName = "Fortnite" } } }));
        var merged = GameStudio.FindEngines(dat, [epic]);
        Assert.Equal(["5.5", "5.4", "5.3"], merged.Select(x => x.Version));
        Assert.EndsWith("UnrealEditor-Cmd.exe", GameStudio.EditorCmdExe(scanned[0]));
        var args = GameStudio.CheckArguments(Path.Combine(root, "Spiel.uproject"), Path.Combine(root, "check.py"));
        Assert.Contains("-run=pythonscript", args);
        Assert.Contains("-unattended", args);
        Assert.Contains("TRENDO_UE ok", GameStudio.ProjectCheckScript);
        Assert.True(GameStudio.CheckSucceeded("Log\nTRENDO_UE ok file=/x/Spiel.uproject\n"));
        Assert.False(GameStudio.CheckSucceeded("Editor gestartet"));
        var missing = GameStudio.CheckProject(Path.Combine(root, "fehlt.exe"), Path.Combine(root, "Spiel.uproject"));
        Assert.False(missing.Ran);
        Assert.False(missing.Ok);
    }

    [Fact(DisplayName = "CORE-06: Editor-Python läuft, wenn UnrealEditor-Cmd.exe auf diesem Rechner liegt")]
    public void Unreal_editor_python_when_present()
    {
        var cmd = GameStudio.FindEngines().Select(GameStudio.EditorCmdExe).FirstOrDefault(File.Exists);
        if (cmd is null) return;
        var engine = GameStudio.FindEngines().First(e => File.Exists(GameStudio.EditorCmdExe(e)));
        var dir = GameStudio.CreateUnrealProject(Tmp(), "Pruefung", engine.Version);
        var up = Directory.GetFiles(dir, "*.uproject").Single();
        var c = GameStudio.CheckProject(cmd, up);
        Assert.True(c.Ran);
        Assert.True(c.Ok, c.Message + "\n" + c.Output);
    }
}
