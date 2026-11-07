using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>3D-Spiele in Unreal ohne Fachwissen (Nutzerwunsch 07.11.2026): C++-Spiel, Welt (3D-Level oder Open World), Kommandozeilen, Auswertung der echten Protokolle.</summary>
public class UnrealGameTests
{
    static JsonObject Doc(string scale, string? world = null)
    {
        var d = new JsonObject { ["title"] = "Waldläufer", ["scale"] = scale, ["weapon"] = "schwert", ["abilities"] = new JsonArray("doppelsprung"),
            ["level"] = new JsonArray("..........", "S..o.G.E.Z", "##########"), ["objects"] = new JsonObject { ["7,1:hp"] = 2 }, ["texts"] = new JsonObject { ["5,1:name"] = "Apfel" },
            ["rules"] = new JsonArray(new JsonObject { ["when"] = "muenzen", ["n"] = 1, ["do"] = "punkte", ["title"] = "Sammler" }) };
        if (world is not null) d["world"] = world;
        return d;
    }

    [Fact(DisplayName = "Unreal-Spiel: Bauen schreibt ein C++-Spiel (Modul im .uproject, Build- und Ziel-Dateien, Figur, Gegner, Spielablauf, Unreal-Tests) mit ASCII-Modulnamen")]
    public void Writes_cpp_game()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ue-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var dir = GameDesign.BuildUnreal(Doc("aaa"), root, "5.8");
            Assert.Equal("Waldlaeufer", Path.GetFileName(dir));
            var up = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "Waldlaeufer.uproject")))!;
            Assert.Equal("Waldlaeufer", up["Modules"]![0]!["Name"]!.GetValue<string>());
            foreach (var f in new[] { "Source/Waldlaeufer.Target.cs", "Source/WaldlaeuferEditor.Target.cs", "Source/Waldlaeufer/Waldlaeufer.Build.cs", "Source/Waldlaeufer/TrendoCharacter.cpp", "Source/Waldlaeufer/TrendoGameMode.cpp", "Source/Waldlaeufer/TrendoActors.cpp", "Source/Waldlaeufer/TrendoTypes.cpp", "Source/Waldlaeufer/TrendoTests.cpp", "Content/Trendo/level.json", "Design/Spielbibel.md" })
                Assert.True(File.Exists(Path.Combine(dir, f)), f);
            Assert.Contains("EnhancedInput", File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "Waldlaeufer.Build.cs")));
            Assert.Contains("IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTrendoRegelnTest", File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "TrendoTests.cpp")));
            var ini = File.ReadAllText(Path.Combine(dir, "Config", "DefaultEngine.ini"));
            Assert.Contains("GameDefaultMap=/Engine/Maps/Templates/OpenWorld", ini); Assert.Contains("GlobalDefaultGameMode=/Script/Waldlaeufer.TrendoGameMode", ini);
            Assert.Contains("r.DynamicGlobalIlluminationMethod=1", ini); // AAA+: Lumen bleibt
            Assert.Single(ini.Split('\n').Where(l => l.StartsWith("GameDefaultMap=")));
            Assert.Contains("DirectoriesToAlwaysStageAsNonUFS", File.ReadAllText(Path.Combine(dir, "Config", "DefaultGame.ini")));
            var lv = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "Content", "Trendo", "level.json")))!;
            Assert.Equal(("openworld", "schwert", "Sammler", "Waldläufer"), (lv["world"]!.GetValue<string>(), lv["weapon"]!.GetValue<string>(), lv["rules"]![0]!["title"]!.GetValue<string>(), lv["name"]!.GetValue<string>()));
            Assert.DoesNotContain("\r", File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "TrendoGameMode.cpp")));
            // Echte Modelle statt Grundformen: Menschfigur mit Animationen, Sportwagen, Fotomodelle je Rolle – Grundform nur als Rückfall
            var actors = File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "TrendoActors.cpp"));
            Assert.Contains("SKM_Manny_Simple", actors); Assert.Contains("BS_MM_WalkRun", actors); Assert.Contains("SKM_SportsCar", actors);
            Assert.Contains("SetBlendSpacePosition", actors); Assert.Contains("Ohne Modell: Grundform", actors);
            var character = File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "TrendoCharacter.cpp"));
            Assert.Contains("TrendoSetupFigure(GetMesh()", character); Assert.Contains("TrendoSetupMetaHuman(this", character); // MetaHuman zuerst, Gliederpuppe als Rückfall
            Assert.True(File.Exists(Path.Combine(dir, "Trendo", "trendo_mensch.py")));
            var gm = File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "TrendoGameMode.cpp"));
            Assert.Contains("\"objects\"", gm);
            Assert.Contains("ThinkOpenWorld", actors); Assert.Contains("LineTraceSingleByChannel", actors); // Gegner-KI: sehen, verfolgen, Gelände
            Assert.Contains("ATrendoCar", actors); Assert.Contains("PC->Possess(Car)", gm); // fahrbarer Wagen mit Chaos-Physik
            var car = File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "TrendoCar.cpp"));
            Assert.Contains("SportsCar_AnimBP", car); Assert.Contains("bUseAutomaticGears = true", car); Assert.Contains("Phys_Wheel_FL", car); // drehende Räder, Automatik
            Assert.Contains("COMNudge", car); Assert.DoesNotContain("FrictionForceMultiplier = 3.f", car); // tiefer Schwerpunkt, normale Haftung: rutscht statt zu kippen
            Assert.Contains("TRENDO-GEGNER", gm); Assert.Contains("TRENDO-WAGEN", gm); // Probe im Prüflauf
            // Gegner als echter Charakter (Kapsel, Schwerkraft, Boden, Kollision), Messung von Bodenkontakt und Kampf; Sturz-Animation beim Tod
            Assert.Contains("class ATrendoEnemy : public ACharacter", File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "TrendoActors.h")));
            Assert.Contains("AIModule", File.ReadAllText(Path.Combine(dir, "Source", "Waldlaeufer", "Waldlaeufer.Build.cs")));
            Assert.Contains("TRENDO-BODEN", gm); Assert.Contains("TRENDO-KAMPF", gm); Assert.Contains("MM_Death_Front_03_ExtractedRM", actors);
            Assert.Contains("TRENDO-GLIED", actors); Assert.Contains("HideBoneByName", actors); Assert.Contains("SetReceivesDecals(false)", gm); // Glieder abtrennbar, Blut nur am Boden
            Assert.Contains("M_TrendoWunde", UnrealGame.MenschScript); Assert.Contains("weld_mesh_edges", UnrealGame.MenschScript); // Schnittfläche geschlossen, Wundmaterial statt Haut
            Assert.Contains("SetScalarParameterValue(TEXT(\"Start\")", gm); Assert.Contains("MaterialExpressionTime", UnrealGame.WeltScript); // Blut trocknet mit der Zeit
            Assert.Contains("TRENDO-FLACKERN", gm); // Messung: Gegner in der Ferne bekommen jedes Bild eine Pose
            Assert.Contains("BS_MM_Bewegung", actors); Assert.Contains("def locomotion", UnrealGame.MenschScript); Assert.Contains("PatrolWait", actors); // Stehen→Gehen→Laufen ohne harten Wechsel, Streife ohne Stop-and-go
            Assert.Contains("trendo-glied.png", gm); Assert.Contains("freie Bahn", gm); // Nahaufnahme des Glieds; Testfahrt in freie Richtung
            Assert.Contains("bEnableUpdateRateOptimizations = false", actors); // kein Ruckeln/Flackern in der Ferne
            Assert.Equal("normal", lv["violence"]!.GetValue<string>()); // Gewalt-Stufe im Level (Standard normal)
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Unreal-Spiel: Welt – AAA+ ist Open World, sonst 3D-Level; ausdrückliche Wahl gewinnt; Level-Welt nutzt die Standardkarte")]
    public void World_choice()
    {
        Assert.Equal("openworld", UnrealGame.WorldOf(Doc("aaa"))); Assert.Equal("level", UnrealGame.WorldOf(Doc("aa"))); Assert.Equal("level", UnrealGame.WorldOf(Doc("indie3d")));
        Assert.Equal("openworld", UnrealGame.WorldOf(Doc("indie3d", "openworld"))); Assert.Equal("level", UnrealGame.WorldOf(Doc("aaa", "level")));
        var root = Path.Combine(Path.GetTempPath(), "trendo-ue-" + Guid.NewGuid().ToString("N")[..6]);
        try { var dir = GameDesign.BuildUnreal(Doc("aa"), root, "5.8"); Assert.Contains("GameDefaultMap=/Engine/Maps/Templates/Template_Default", File.ReadAllText(Path.Combine(dir, "Config", "DefaultEngine.ini"))); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Unreal-Spiel: offizielle Kommandozeilen (Build.bat, Automation RunTests Trendo, -game -trendocheck) und Auswertung echter Protokollzeilen aus UE 5.8")]
    public void Commands_and_logs()
    {
        Assert.Equal(["WaldlaeuferEditor", "Win64", "Development", "-Project=C:/S/Waldlaeufer.uproject", "-WaitMutex", "-NoHotReload"], UnrealGame.CompileArgs("C:/S/Waldlaeufer.uproject"));
        Assert.Contains("-ExecCmds=Automation RunTests Trendo", UnrealGame.TestArgs("p", "l")); Assert.Contains("-nullrhi", UnrealGame.TestArgs("p", "l"));
        Assert.Contains("-trendoshot", UnrealGame.CheckArgs("p", "l", true)); Assert.Contains("-nullrhi", UnrealGame.CheckArgs("p", "l", false));
        Assert.True(UnrealGame.CompileOk("Total time 47.99 seconds\nResult: Succeeded")); Assert.False(UnrealGame.CompileOk("error C2065\nResult: Failed (OtherCompilationError)"));
        const string tests = "[2026.11.07-00.46.14:414][595]LogAutomationController: Display: Test Completed. Result={Success} Name={Daten} Path={Trendo.Daten}\n"
            + "[2026.11.07-00.46.14:430][597]LogAutomationController: Display: Test Completed. Result={Fail} Name={Regeln} Path={Trendo.Regeln}\n"
            + "LogAutomationController: Display: Test Completed. Result={Success} Name={Andere} Path={Engine.Andere}";
        Assert.Equal([("Trendo.Daten", true), ("Trendo.Regeln", false)], UnrealGame.ParseTests(tests));
        const string start = "[2026.11.07-00.47.10:455][  0]LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Figur=ja Boden=ja Z=92\n[2026.11.07-00.47.10:455][  0]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht";
        Assert.Equal((true, "Welt=openworld Level=0 Objekte=7 Gegner=1 Figur=ja Boden=ja Z=92"), UnrealGame.ParseCheck(start));
        Assert.Equal((false, "Spiel nicht spielbar"), UnrealGame.ParseCheck("LogTrendo: Display: TRENDO-FEHLER Spiel nicht spielbar"));
        Assert.False(UnrealGame.ParseCheck("abgestürzt").Ok);
    }
}
