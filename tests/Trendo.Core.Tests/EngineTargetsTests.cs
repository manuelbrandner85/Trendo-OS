using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S1-01 Universal Game Engine mit Unity: Erkennen ohne Annahmen, Projekte für Unity und Godot, offizielle Kommandozeilen, Ergebnisse lesen.</summary>
public class EngineTargetsTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-engine-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Engines S1-01: Unity-Editoren aus Hub-Ordner und Zusatzpfad, neueste zuerst; ohne Unity.exe kein Fund")]
    public void Find_unity()
    {
        var roots = EngineTargets.UnityRoots("C:/PF", "C:/AD", f => f.EndsWith("secondaryInstallPath.json") ? "\"D:\\\\Unity\"" : null);
        Assert.Equal(["C:/PF" + Path.DirectorySeparatorChar + "Unity" + Path.DirectorySeparatorChar + "Hub" + Path.DirectorySeparatorChar + "Editor", "D:\\Unity"], roots);
        var dirs = new Dictionary<string, string[]> { [roots[0]] = ["h/2022.3.45f1", "h/6000.0.23f1", "h/kaputt"], ["D:\\Unity"] = ["d/6000.1.2f1"] };
        var found = EngineTargets.FindUnity(roots, r => dirs.GetValueOrDefault(r, []), f => !f.Contains("kaputt"));
        Assert.Equal(["6000.1.2f1", "6000.0.23f1", "2022.3.45f1"], found.Select(f => f.Version));
        Assert.Empty(EngineTargets.FindUnity(roots, r => dirs.GetValueOrDefault(r, []), _ => false));
        Assert.Null(EngineTargets.ParseSecondaryInstallPath("{kaputt"));
        Assert.Equal("6000.0.23f1", EngineTargets.ReadProjectVersion("m_EditorVersion: 6000.0.23f1\nm_EditorVersionWithRevision: x"));
    }

    [Fact(DisplayName = "Engines S1-01: Godot 4 am Dateinamen erkennen (nicht Godot 3, nicht die Konsolenfassung); ohne Funde ehrlich „nicht gefunden“, Unity „blockiert“")]
    public void Find_godot_and_summary()
    {
        Assert.Equal("4.4.1-stable", EngineTargets.GodotVersionFromName("Godot_v4.4.1-stable_win64.exe"));
        Assert.Equal("4.3-stable", EngineTargets.GodotVersionFromName("Godot_v4.3-stable_mono_win64.exe"));
        Assert.Null(EngineTargets.GodotVersionFromName("Godot_v3.5-stable_win64.exe"));
        var g = EngineTargets.FindGodot(["a", "b"], d => d == "a" ? ["a/Godot_v4.2-stable_win64.exe", "a/Godot_v4.4-stable_win64_console.exe"] : ["b/Godot_v4.4-stable_win64.exe"]);
        Assert.Equal(["4.4-stable", "4.2-stable"], g.Select(x => x.Version));
        var none = EngineTargets.Summary([], _ => "", [], [], null, _ => false);
        Assert.All(none, e => Assert.False(e.Installed));
        Assert.StartsWith("blockiert", none.Single(e => e.Id == "unity").Status);
        var some = EngineTargets.Summary([("5.8", "C:/UE")], p => p + "/UnrealEditor.exe", [("6000.0.23f1", "C:/U/Unity.exe")], g, "C:/B/blender.exe", f => f == "C:/UE/UnrealEditor.exe");
        Assert.All(some, e => Assert.True(e.Installed));
        Assert.False(EngineTargets.Summary([("5.8", "C:/UE")], p => p + "/x.exe", [], [], null, _ => false).Single(e => e.Id == "unreal").Installed); // Eintrag ohne Programm zählt nicht
    }

    [Fact(DisplayName = "Engines S1-01: Unity-Fähigkeiten ohne Editor blockiert (Projekt anlegen bleibt möglich), mit Editor eingebaut, nach bestandenen Tests geprüft")]
    public void Unity_caps()
    {
        var off = EngineTargets.UnityCapStates(false, false);
        Assert.Equal(EngineCapState.Implemented, off["unity.project.create"]); Assert.Equal(EngineCapState.Blocked, off["unity.build.local"]); Assert.Equal(EngineCapState.Blocked, off["unity.test.playmode"]);
        Assert.Equal(EngineCapState.Implemented, EngineTargets.UnityCapStates(true, false)["unity.test.editmode"]);
        Assert.Equal(EngineCapState.Tested, EngineTargets.UnityCapStates(true, true)["unity.test.editmode"]);
    }

    static GameStudio.GameSettings Game() => new()
    {
        Level = ["..........", "S..o...E.Z", "##########"], MoreLevels = [["S.K.D...Z.", "##########"]], LevelNames = ["Wiese", "Burg"],
        Props = new() { ["7,1:speed"] = 2 }, Rules = [new("muenzen", 1, "leben")], Speed = 7, Lives = 4,
    };

    [Fact(DisplayName = "Engines S1-01: Level-Beschreibung für Engines – alle Level mit Namen, Eigenschaften als Liste (Unity-tauglich), Regeln")]
    public void Level_json()
    {
        var j = JsonNode.Parse(EngineTargets.LevelJson("Mein Spiel", Game()))!;
        Assert.Equal(2, j["levels"]!.AsArray().Count); Assert.Equal("Burg", j["levels"]![1]!["name"]!.GetValue<string>());
        Assert.Equal("7,1:speed", j["props"]![0]!["key"]!.GetValue<string>());
        Assert.Equal("leben", j["rules"]![0]!["act"]!.GetValue<string>());
        Assert.Equal(7, j["speed"]!.GetValue<int>());
        var gp = JsonNode.Parse(EngineTargets.LevelJson("x", Game() with { Abilities = ["sprint"], Weapon = "schwert", Texts = new() { ["2,1:name"] = "Apfel" }, Rules = [new("muenzen", 1, "leben", "Sammler")] }))!;
        Assert.Equal(("sprint", "schwert", "2,1:name", "Sammler"), (gp["abilities"]![0]!.GetValue<string>(), gp["weapon"]!.GetValue<string>(), gp["texts"]![0]!["key"]!.GetValue<string>(), gp["rules"]![0]!["title"]!.GetValue<string>()));
        Assert.Equal(GameStudio.DefaultPlatform.Length, JsonNode.Parse(EngineTargets.LevelJson("x", new()))!["levels"]![0]!["rows"]!.AsArray().Count); // leeres Spiel → Standardlevel
    }

    [Fact(DisplayName = "Engines S1-01 Unity: Projekt mit Spiel (C#), Editor-Build, EditMode- und PlayMode-Tests, Paketliste mit Test Framework; Editor-Version nur wenn bekannt; vorhandener Ordner wird nicht überschrieben")]
    public void Unity_project()
    {
        var root = Tmp();
        try
        {
            var dir = EngineTargets.CreateUnityProject(root, "Mein Spiel!", Game(), "6000.0.23f1");
            Assert.Equal("Mein-Spiel-Unity", Path.GetFileName(dir));
            foreach (var f in new[] { "Assets/Resources/trendo-level.json", "Assets/Scripts/TrendoLevel.cs", "Assets/Scripts/TrendoPlayer.cs", "Assets/Scripts/TrendoLevelData.cs", "Assets/Editor/TrendoBuild.cs", "Assets/Tests/EditMode/TrendoLevelTests.cs", "Assets/Tests/PlayMode/TrendoPlayTests.cs", "Assets/Tests/PlayMode/TrendoGameplayTests.cs", "Packages/manifest.json", "ProjectSettings/ProjectVersion.txt" })
                Assert.True(File.Exists(Path.Combine(dir, f)), f);
            foreach (var a in Directory.EnumerateFiles(dir, "*.asmdef", SearchOption.AllDirectories)) JsonDocument.Parse(File.ReadAllText(a)).Dispose(); // gültiges JSON
            Assert.Contains("com.unity.test-framework", File.ReadAllText(Path.Combine(dir, "Packages", "manifest.json")));
            Assert.Equal("6000.0.23f1", EngineTargets.ReadProjectVersion(File.ReadAllText(Path.Combine(dir, "ProjectSettings", "ProjectVersion.txt"))));
            Assert.Contains("TrendoBuild.BuildWindows", EngineTargets.UnityBuildArgs(dir, "l.txt"));
            Assert.Contains("public static void BuildWindows()", File.ReadAllText(Path.Combine(dir, "Assets", "Editor", "TrendoBuild.cs")));
            Assert.Contains("[UnityTest]", File.ReadAllText(Path.Combine(dir, "Assets", "Tests", "PlayMode", "TrendoPlayTests.cs")));
            Assert.Empty(Directory.EnumerateFiles(dir, "*.unity", SearchOption.AllDirectories)); // Szene legt der Editor selbst an
            Assert.Throws<IOException>(() => EngineTargets.CreateUnityProject(root, "Mein Spiel!", Game()));
            var noVer = EngineTargets.CreateUnityProject(root, "Zweites", Game());
            Assert.False(File.Exists(Path.Combine(noVer, "ProjectSettings", "ProjectVersion.txt")));
            // gleiche Level-Daten wie in Trendo
            Assert.Equal(EngineTargets.LevelJson("Mein Spiel!", Game()).Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(dir, "Assets", "Resources", "trendo-level.json")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Engines S1-01 Godot: Godot-4-Projekt mit Hauptszene, GDScript-Spiel und Prüfskript für die Kommandozeile")]
    public void Godot_project()
    {
        var root = Tmp();
        try
        {
            var dir = EngineTargets.CreateGodotProject(root, "Mein Spiel", Game());
            var proj = File.ReadAllText(Path.Combine(dir, "project.godot"));
            Assert.Contains("config_version=5", proj); Assert.Contains("run/main_scene=\"res://main.tscn\"", proj);
            Assert.Contains("res://trendo_level.gd", File.ReadAllText(Path.Combine(dir, "main.tscn")));
            Assert.Contains("TRENDO-OK", File.ReadAllText(Path.Combine(dir, "tests", "check_level.gd")));
            Assert.Contains("is_on_floor", File.ReadAllText(Path.Combine(dir, "tests", "play_check.gd")));
            Assert.True(File.Exists(Path.Combine(dir, "trendo_shot.gd"))); Assert.Contains("TRENDO-OK Gameplay", File.ReadAllText(Path.Combine(dir, "tests", "gameplay_check.gd")));
            Assert.Equal("res://tests/gameplay_check.gd", EngineTargets.GodotGameplayArgs(dir)[^1]);
            Assert.Contains("remove_child(c)", File.ReadAllText(Path.Combine(dir, "trendo_level.gd"))); // Levelwechsel ohne Umbenennen
            Assert.Equal("res://tests/play_check.gd", EngineTargets.GodotPlayArgs(dir)[^1]);
            Assert.StartsWith("extends Node2D", File.ReadAllText(Path.Combine(dir, "trendo_level.gd")));
            Assert.DoesNotContain("\r", File.ReadAllText(Path.Combine(dir, "trendo_level.gd")));
            Assert.Equal(["--headless", "--path", dir, "--script", "res://tests/check_level.gd"], EngineTargets.GodotCheckArgs(dir));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Engines S1-01: Unity-Tests über -runTests ohne -quit; Ergebnis-XML und Godot-Meldung werden gelesen, fehlende Meldung ist ein Fehler")]
    public void Cli_and_results()
    {
        var a = EngineTargets.UnityTestArgs("P", "PlayMode", "r.xml", "l.txt");
        Assert.Contains("-runTests", a); Assert.DoesNotContain("-quit", a); Assert.Equal("PlayMode", a[a.IndexOf("-testPlatform") + 1]);
        Assert.Equal("EditMode", EngineTargets.UnityTestArgs("P", "irgendwas", "r", "l")[6]);
        Assert.Equal((3, 2, 1), EngineTargets.ParseUnityResults("<?xml version=\"1.0\"?><test-run id=\"2\" testcasecount=\"3\" result=\"Failed\" total=\"3\" passed=\"2\" failed=\"1\">")!.Value);
        Assert.Null(EngineTargets.ParseUnityResults("kein xml"));
        Assert.Equal((true, "2 Level"), EngineTargets.ParseGodotCheck("Godot Engine v4.4\nTRENDO-OK 2 Level\n"));
        Assert.Equal((false, "Burg: Start oder Ziel fehlt"), EngineTargets.ParseGodotCheck("TRENDO-FEHLER Burg: Start oder Ziel fehlt"));
        Assert.False(EngineTargets.ParseGodotCheck("abgestürzt").Ok);
    }
}
