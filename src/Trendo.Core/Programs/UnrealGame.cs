using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>3D-Spiele in Unreal Engine 5 ohne Fachwissen (Paket 01 S1-01/S1-04/S1-05, Nutzerwunsch 07.11.2026 „Open World, Programme automatisch“):
/// Trendo schreibt ein C++-Spiel (Figur mit Kamera, Steuerung über Enhanced Input, Gegner und Bosse, Münzen, Gegenstände, sprechende Figuren, Fahrzeuge,
/// Regeln und Aufgaben, Spielstand, Menü und Anzeige) und baut die Welt aus dem Trendo-Level – als 3D-Level oder verteilt in der Open World von Unreal.
/// Kompilieren (Build.bat), Unreal-Tests („Automation RunTests Trendo“) und ein Startlauf (-trendocheck, optional -trendoshot) laufen über die offiziellen Kommandozeilen.</summary>
public static class UnrealGame
{
    public static readonly (string Id, string Label, string Map)[] Worlds =
    [
        ("level", "3D-Level aus dem Trendo-Raster", "/Engine/Maps/Templates/Template_Default"),
        ("openworld", "Open World (Gelände, Himmel, World Partition)", "/Engine/Maps/Templates/OpenWorld"),
    ];

    /// <summary>Welt des Spiels: aus dem Dokument („world“), sonst Open World für AAA+ und Level für die anderen Größen.</summary>
    public static string WorldOf(JsonObject d) => d["world3d"]?.ToString() is "level" or "openworld" ? d["world3d"]!.ToString() // Wahl aus dem Spiele-Prompt
        : d["world"]?.ToString() is "level" or "openworld" ? d["world"]!.ToString() : d["scale"]?.ToString() == "aaa" ? "openworld" : "level";

    /// <summary>Modulname aus dem Projektordner (nur Buchstaben und Ziffern, wie das .uproject).</summary>
    public static string ModuleName(string projectDir) => Path.GetFileNameWithoutExtension(Directory.GetFiles(projectDir, "*.uproject").Single());

    /// <summary>C++-Spiel und Welt ins Unreal-Projekt schreiben (überschreibt nur Trendo-eigene Dateien).</summary>
    public static void Write(string projectDir, string level, string world)
    {
        var name = ModuleName(projectDir);
        var up = Path.Combine(projectDir, name + ".uproject");
        var o = JsonNode.Parse(File.ReadAllText(up))!.AsObject();
        o["Modules"] = new JsonArray(new JsonObject { ["Name"] = name, ["Type"] = "Runtime", ["LoadingPhase"] = "Default" });
        // Plugins für das Einrichten der Welt (Python im Editor, Import von glTF über Interchange)
        var plugins = o["Plugins"]?.AsArray().DeepClone().AsArray() ?? [];
        foreach (var pl in Plugins)
            if (!plugins.Any(x => x?["Name"]?.ToString() == pl)) plugins.Add(new JsonObject { ["Name"] = pl, ["Enabled"] = true });
        o["Plugins"] = plugins;
        File.WriteAllText(up, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        void Put(string rel, string text) { var f = Path.Combine(projectDir, rel.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, text.Replace("\r\n", "\n")); }
        string Target(string type, string suffix) => $"using UnrealBuildTool;\n\npublic class {name}{suffix}Target : TargetRules\n{{\n\tpublic {name}{suffix}Target(TargetInfo Target) : base(Target)\n\t{{\n\t\tType = TargetType.{type};\n\t\tDefaultBuildSettings = BuildSettingsVersion.Latest;\n\t\tIncludeOrderVersion = EngineIncludeOrderVersion.Latest;\n\t\tExtraModuleNames.Add(\"{name}\");\n\t}}\n}}\n";
        Put($"Source/{name}.Target.cs", Target("Game", ""));
        Put($"Source/{name}Editor.Target.cs", Target("Editor", "Editor"));
        Put($"Source/{name}/{name}.Build.cs", $"using UnrealBuildTool;\n\npublic class {name} : ModuleRules\n{{\n\tpublic {name}(ReadOnlyTargetRules Target) : base(Target)\n\t{{\n\t\tPCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;\n\t\tPublicDependencyModuleNames.AddRange(new[] {{ \"Core\", \"CoreUObject\", \"Engine\", \"InputCore\", \"EnhancedInput\", \"Json\", \"AIModule\", \"ChaosVehicles\", \"PhysicsCore\" }});\n\t}}\n}}\n");
        Put($"Source/{name}/{name}.cpp", $"// Von Trendo erzeugt.\n#include \"Modules/ModuleManager.h\"\n\nIMPLEMENT_PRIMARY_GAME_MODULE(FDefaultGameModuleImpl, {name}, \"{name}\");\n");
        foreach (var (file, text) in Sources) Put($"Source/{name}/{file}", text + "\n");
        var map = Worlds.FirstOrDefault(w => w.Id == world).Map ?? Worlds[0].Map;
        var ini = Path.Combine(projectDir, "Config", "DefaultEngine.ini");
        var old = File.Exists(ini) ? File.ReadAllText(ini) : "";
        var keep = string.Join("\n", old.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("GameDefaultMap=") && !l.StartsWith("EditorStartupMap=") && !l.StartsWith("GlobalDefaultGameMode=") && l != "[/Script/EngineSettings.GameMapsSettings]"));
        Put("Config/DefaultEngine.ini", $"[/Script/EngineSettings.GameMapsSettings]\nGameDefaultMap={map}\nEditorStartupMap={map}\nGlobalDefaultGameMode=/Script/{name}.TrendoGameMode\n\n" + keep.Trim() + "\n");
        Put("Config/DefaultInput.ini", "[/Script/Engine.InputSettings]\nDefaultPlayerInputClass=/Script/EnhancedInput.EnhancedPlayerInput\nDefaultInputComponentClass=/Script/EnhancedInput.EnhancedInputComponent\n");
        var game = Path.Combine(projectDir, "Config", "DefaultGame.ini");
        var g = File.Exists(game) ? File.ReadAllText(game) : "";
        if (!g.Contains("DirectoriesToAlwaysStageAsNonUFS")) File.WriteAllText(game, g.TrimEnd() + "\n\n[/Script/UnrealEd.ProjectPackagingSettings]\n+DirectoriesToAlwaysStageAsNonUFS=(Path=\"Trendo\")\n");
        // Fahrzeuge (Chaos Modular Vehicle) brauchen die asynchrone Physik-Berechnung
        var eng = File.ReadAllText(ini);
        if (!eng.Contains("bTickPhysicsAsync")) File.WriteAllText(ini, eng.TrimEnd() + "\n\n[/Script/Engine.PhysicsSettings]\nbTickPhysicsAsync=True\n");
        Put("Content/Trendo/level.json", level);
        Put(WeltScriptRel, WeltScript + "\n");
        Put(MenschScriptRel, MenschScript + "\n");
    }

    /// <summary>Level-Beschreibung für Unreal: gleiche Daten wie für Unity/Godot plus Welt-Art.</summary>
    public static string LevelJson(string name, GameStudio.GameSettings s, string world)
    {
        var o = JsonNode.Parse(EngineTargets.LevelJson(name, s))!.AsObject();
        o["world"] = world;
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>Plugins, die Trendo im Projekt einschaltet: Python/Interchange zum Einrichten der Welt, dazu die Engine-Beispielinhalte
    /// mit Menschfigur samt Geh-/Lauf-/Sprung-Animationen (MoverExamples) und Sportwagen (ChaosModularVehicleExamples) – Teil jeder Unreal-Installation –
    /// und der MetaHuman Creator für den echten Menschen als Spielfigur.</summary>
    public static readonly string[] Plugins = ["PythonScriptPlugin", "EditorScriptingUtilities", "Interchange", "InterchangeEditor", "MoverExamples", "ChaosModularVehicleExamples", "ChaosModularVehicle", "ChaosVehiclesPlugin", "MetaHumanCharacter", "GeometryScripting"];
    /// <summary>Eigene Karte, die das Welt-Skript aus der Open-World-Vorlage anlegt.</summary>
    public const string WeltMap = "/Game/Trendo/Welt";
    public const string WeltScriptRel = "Trendo/trendo_welt.py";
    public const string MenschScriptRel = "Trendo/trendo_mensch.py";

    /// <summary>Nach gelungenem Einrichten: Spiel und Editor starten in der eingerichteten Welt.</summary>
    public static void UseWeltMap(string projectDir)
    {
        var ini = Path.Combine(projectDir, "Config", "DefaultEngine.ini");
        var lines = File.ReadAllText(ini).Replace("\r\n", "\n").Split('\n')
            .Select(l => l.StartsWith("GameDefaultMap=") ? "GameDefaultMap=" + WeltMap : l.StartsWith("EditorStartupMap=") ? "EditorStartupMap=" + WeltMap : l);
        File.WriteAllText(ini, string.Join("\n", lines));
    }

    /// <summary>Welt einrichten: Ergebnis aus dem Protokoll („TRENDO-WELT-OK n“, Zeilen „TRENDO-WELT …“, letzter Python-Fehler).</summary>
    public static (bool Ok, int Models, List<string> Lines) ParseWelt(string log)
    {
        var ls = log.Split('\n').Select(l => l.Trim()).ToList();
        var m = System.Text.RegularExpressions.Regex.Match(ls.LastOrDefault(l => l.Contains("TRENDO-WELT-OK")) ?? "", @"TRENDO-WELT-OK (\d+)");
        var info = ls.Where(l => l.Contains("TRENDO-WELT ")).Select(l => l[(l.IndexOf("TRENDO-WELT ") + 12)..]).ToList();
        var err = ls.Where(l => l.Contains("LogPython: Error:")).Select(l => l[(l.IndexOf("LogPython: Error:") + 17)..].Trim()).LastOrDefault();
        if (!m.Success && err is not null) info.Add("Fehler: " + err);
        return (m.Success, m.Success ? int.Parse(m.Groups[1].Value) : 0, info);
    }

    /// <summary>Ordner für freie MetaHuman-Outfits (.mhpkg, z. B. kostenlos von Fab – Download macht der Nutzer mit seinem Konto).</summary>
    public static string ClothesDir(string toolsRoot) => Path.Combine(toolsRoot, "assets", "kleidung");

    /// <summary>Outfits ins Projekt legen (Trendo/Kleidung); das Mensch-Skript importiert sie und kleidet die Figur damit ein. Gibt die Anzahl zurück.</summary>
    public static int CopyClothes(string clothesDir, string projectDir)
    {
        if (!Directory.Exists(clothesDir)) return 0;
        var dest = Path.Combine(projectDir, "Trendo", "Kleidung"); var n = 0;
        foreach (var f in Directory.EnumerateFiles(clothesDir, "*.mhpkg"))
        {
            Directory.CreateDirectory(dest);
            var t = Path.Combine(dest, Path.GetFileName(f));
            if (!File.Exists(t) || new FileInfo(t).Length != new FileInfo(f).Length) File.Copy(f, t, true);
            n++;
        }
        return n;
    }

    /// <summary>Echter Mensch: Ergebnis aus dem Protokoll („TRENDO-MENSCH-OK“, Zeilen „TRENDO-MENSCH …“, Grund bei „TRENDO-MENSCH-FEHLER“).</summary>
    public static (bool Ok, List<string> Lines, string Error) ParseMensch(string log)
    {
        var ls = log.Split('\n').Select(l => l.Trim()).ToList();
        var ok = ls.Any(l => l.Contains("TRENDO-MENSCH-OK"));
        var info = ls.Where(l => l.Contains("TRENDO-MENSCH ")).Select(l => l[(l.IndexOf("TRENDO-MENSCH ") + 14)..]).ToList();
        var err = ls.LastOrDefault(l => l.Contains("TRENDO-MENSCH-FEHLER")) is { } e ? e[(e.IndexOf("TRENDO-MENSCH-FEHLER") + 20)..].Trim() : ok ? "" : "Kein Ergebnis – Protokoll mensch.txt";
        return (ok, info, err);
    }

    // ───────────── Kommandozeilen (offiziell) ─────────────
    public static string BuildBat(string engineDir) => Path.Combine(engineDir, "Engine", "Build", "BatchFiles", "Build.bat");
    public static string EditorCmd(string engineDir) => Path.Combine(engineDir, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe");
    public static string Editor(string engineDir) => Path.Combine(engineDir, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
    public static List<string> CompileArgs(string uproject) => [Path.GetFileNameWithoutExtension(uproject) + "Editor", "Win64", "Development", "-Project=" + uproject, "-WaitMutex", "-NoHotReload"];
    public static List<string> TestArgs(string uproject, string log) => [uproject, "-ExecCmds=Automation RunTests Trendo", "-TestExit=Automation Test Queue Empty", "-unattended", "-nullrhi", "-nosplash", "-nopause", "-log", "-abslog=" + log];
    public static List<string> CheckArgs(string uproject, string log, bool screenshot) =>
        screenshot ? [uproject, "-game", "-trendocheck", "-trendoshot", "-windowed", "-ResX=1600", "-ResY=900", "-unattended", "-nosplash", "-log", "-abslog=" + log]
                   : [uproject, "-game", "-trendocheck", "-nullrhi", "-unattended", "-nosplash", "-nopause", "-log", "-abslog=" + log];
    public static List<string> WeltArgs(string uproject, string log) =>
        [uproject, "-ExecutePythonScript=" + Path.Combine(Path.GetDirectoryName(uproject)!, WeltScriptRel.Replace('/', Path.DirectorySeparatorChar)), "-unattended", "-nullrhi", "-nosplash", "-nopause", "-log", "-abslog=" + log];
    /// <summary>Mensch bauen: mit Grafik ohne Fenster (Hauttexturen entstehen auf der Grafikkarte).</summary>
    public static List<string> MenschArgs(string uproject, string log) =>
        [uproject, "-ExecutePythonScript=" + Path.Combine(Path.GetDirectoryName(uproject)!, MenschScriptRel.Replace('/', Path.DirectorySeparatorChar)), "-RenderOffscreen", "-unattended", "-nosplash", "-nopause", "-log", "-abslog=" + log];
    public static List<string> PlayArgs(string uproject) => [uproject, "-game", "-windowed", "-ResX=1600", "-ResY=900"];

    /// <summary>Ergebnis des Kompilierens aus dem Build-Protokoll.</summary>
    public static bool CompileOk(string log) => log.Contains("Result: Succeeded");

    /// <summary>Unreal-Testergebnisse (Name → bestanden) aus dem Protokoll.</summary>
    public static List<(string Test, bool Ok)> ParseTests(string log) =>
        [.. System.Text.RegularExpressions.Regex.Matches(log, @"Test Completed\. Result=\{(\w+)\} Name=\{[^}]*\} Path=\{(Trendo\.[^}]+)\}").Select(m => (m.Groups[2].Value, m.Groups[1].Value == "Success"))];

    /// <summary>Startlauf: „TRENDO-OK …“ bzw. „TRENDO-FEHLER …“ und die Kennzahlen.</summary>
    public static (bool Ok, string Text) ParseCheck(string log)
    {
        var line = log.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Contains("TRENDO-OK") || l.Contains("TRENDO-FEHLER")) ?? "";
        var info = log.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Contains("TRENDO-SPIEL")) ?? "";
        var details = info.Length > 0 ? info[(info.IndexOf("TRENDO-SPIEL") + 12)..].Trim() : "";
        return line.Contains("TRENDO-OK") ? (true, details) : (false, line.Length > 0 ? line[(line.IndexOf("TRENDO-FEHLER") + 13)..].Trim() : "Kein Startbericht – das Spiel lief nicht bis zum Ende.");
    }

    /// <summary>Python-Skript für Unreal: Fotomaterialien und Modelle importieren, Boden-, Blatt- und Oberflächenmaterial bauen, Nanite einstellen, Karte anlegen.</summary>
    public const string WeltScript = """
# Von Trendo erzeugt: richtet die Welt in Unreal ein (läuft ohne Fenster über -ExecutePythonScript).
# Importiert freie Fotomaterialien und Modelle (Poly Haven, CC0) aus Content/Trendo/Quellen, baut das Bodenmaterial,
# legt die eigene Open-World-Karte /Game/Trendo/Welt aus der Engine-Vorlage an und schreibt Content/Trendo/natur.json.
import json, os, unreal

ROOT = unreal.Paths.project_content_dir()
SRC = os.path.join(ROOT, "Trendo", "Quellen")
DEST = "/Game/Trendo/Natur"
KINDS = {"grass_medium_01": "gras", "fern_02": "farn", "shrub_02": "strauch", "shrub_04": "strauch", "pine_sapling_small": "baum", "tree_small_02": "baum",
         "tree_stump_01": "holz", "dead_tree_trunk": "holz", "rock_moss_set_01": "stein", "boulder_01": "stein"}
# Spielobjekte (Rolle → Modell, echte Größe): Fundstück o (Messingvase statt Münze – im Wald sichtbar), Gegenstand G, Ziel Z, Schlüssel K, Speicherpunkt C, Tür/Hindernis D, Feder F, Waffe, Wurfwaffe
PROPS = {"o": "brass_vase_02", "G": "food_apple_01", "Z": "treasure_chest", "K": "brass_diya_lantern", "C": "wooden_lantern_01", "D": "wooden_crate_02",
         "F": "old_tyre", "waffe": "wooden_handle_saber", "wurf": "hatchet"}
SOLID = {"D"}
tools = unreal.AssetToolsHelpers.get_asset_tools()
eal = unreal.EditorAssetLibrary
log = []

def task(path, dest):
    t = unreal.AssetImportTask()
    t.set_editor_property("filename", path); t.set_editor_property("destination_path", dest)
    t.set_editor_property("automated", True); t.set_editor_property("save", True); t.set_editor_property("replace_existing", True)
    return t

# 1) Bodentexturen: Gras-Boden und Waldboden (je Farbe, Normalen, Rauheit, AO)
GROUNDS = ["leafy_grass", "forest_ground_04"]
ground = {}
for g in GROUNDS:
    gdir = os.path.join(SRC, g)
    if not os.path.isdir(gdir): continue
    ts = {k: task(os.path.join(gdir, f"{g}_{k}_2k.jpg"), DEST + "/Boden") for k in ["Diffuse", "nor_dx", "Rough", "AO"]}
    tools.import_asset_tasks(list(ts.values()))
    for k, t in ts.items():
        paths = t.get_editor_property("imported_object_paths")
        if not paths: continue
        tex = unreal.load_asset(paths[0]); ground[(g, k)] = tex
        if k != "Diffuse":
            tex.set_editor_property("srgb", False)
        if k == "nor_dx":
            tex.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_NORMALMAP)
        eal.save_loaded_asset(tex)
log.append("Boden-Texturen: %d" % len(ground))

# 2) Bodenmaterial: Landschafts-Koordinaten (Kachel 4 m), zwei Böden großflächig über Rauschen gemischt (keine sichtbare Wiederholung)
mat = None
have = [g for g in GROUNDS if all((g, k) in ground for k in ["Diffuse", "nor_dx", "Rough", "AO"])]
if have:
    mpath = DEST + "/M_TrendoBoden"
    if eal.does_asset_exist(mpath): eal.delete_asset(mpath)
    mat = tools.create_asset("M_TrendoBoden", DEST, unreal.Material, unreal.MaterialFactoryNew())
    mel = unreal.MaterialEditingLibrary
    uv = mel.create_material_expression(mat, unreal.MaterialExpressionLandscapeLayerCoords, -1400, 0)
    uv.set_editor_property("mapping_scale", 4.0)
    # Ferne: dieselben Farbkarten zwölfmal größer, ab 25 m allmählich eingeblendet (keine Kachelflecken auf Hügeln)
    uvfar = mel.create_material_expression(mat, unreal.MaterialExpressionLandscapeLayerCoords, -1400, -900)
    uvfar.set_editor_property("mapping_scale", 48.0)
    depth = mel.create_material_expression(mat, unreal.MaterialExpressionPixelDepth, -900, -1100)
    dsub = mel.create_material_expression(mat, unreal.MaterialExpressionSubtract, -750, -1100); dsub.set_editor_property("const_b", 2500.0)
    ddiv = mel.create_material_expression(mat, unreal.MaterialExpressionDivide, -650, -1100); ddiv.set_editor_property("const_b", 15000.0)
    dsat = mel.create_material_expression(mat, unreal.MaterialExpressionSaturate, -550, -1100)
    mel.connect_material_expressions(depth, "", dsub, "A"); mel.connect_material_expressions(dsub, "", ddiv, "A"); mel.connect_material_expressions(ddiv, "", dsat, "")
    def sample(tex, x, y, sampler, coords=None):
        s = mel.create_material_expression(mat, unreal.MaterialExpressionTextureSample, x, y)
        s.set_editor_property("texture", tex); s.set_editor_property("sampler_type", sampler)
        s.set_editor_property("sampler_source", unreal.SamplerSourceMode.SSM_WRAP_WORLD_GROUP_SETTINGS)
        mel.connect_material_expressions(coords or uv, "", s, "UVs"); return s
    kinds = [("Diffuse", unreal.MaterialSamplerType.SAMPLERTYPE_COLOR, "RGB", unreal.MaterialProperty.MP_BASE_COLOR),
             ("nor_dx", unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL, "RGB", unreal.MaterialProperty.MP_NORMAL),
             ("Rough", unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR, "R", unreal.MaterialProperty.MP_ROUGHNESS),
             ("AO", unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR, "R", unreal.MaterialProperty.MP_AMBIENT_OCCLUSION)]
    noise = None
    if len(have) > 1:
        noise = mel.create_material_expression(mat, unreal.MaterialExpressionNoise, -700, -700)
        noise.set_editor_property("scale", 0.0004); noise.set_editor_property("levels", 4)
        noise.set_editor_property("output_min", -1.2); noise.set_editor_property("output_max", 1.4)
        sat = mel.create_material_expression(mat, unreal.MaterialExpressionSaturate, -500, -700)
        mel.connect_material_expressions(noise, "", sat, ""); noise = sat
    for i, (k, sampler, out, prop) in enumerate(kinds):
        a = sample(ground[(have[0], k)], -1000, i * 500, sampler)
        if noise is None:
            mel.connect_material_property(a, out, prop); continue
        b = sample(ground[(have[1], k)], -1000, i * 500 + 250, sampler)
        l = mel.create_material_expression(mat, unreal.MaterialExpressionLinearInterpolate, -300, i * 500)
        mel.connect_material_expressions(a, out, l, "A"); mel.connect_material_expressions(b, out, l, "B"); mel.connect_material_expressions(noise, "", l, "Alpha")
        if k == "Diffuse":
            fa = sample(ground[(have[0], k)], -1000, -1500, sampler, uvfar); fb = sample(ground[(have[1], k)], -1000, -1250, sampler, uvfar)
            fl = mel.create_material_expression(mat, unreal.MaterialExpressionLinearInterpolate, -300, -1300)
            mel.connect_material_expressions(fa, out, fl, "A"); mel.connect_material_expressions(fb, out, fl, "B"); mel.connect_material_expressions(noise, "", fl, "Alpha")
            mix = mel.create_material_expression(mat, unreal.MaterialExpressionLinearInterpolate, -150, -600)
            mel.connect_material_expressions(l, "", mix, "A"); mel.connect_material_expressions(fl, "", mix, "B"); mel.connect_material_expressions(dsat, "", mix, "Alpha")
            l = mix
        mel.connect_material_property(l, "", prop)
    mel.recompile_material(mat); eal.save_loaded_asset(mat)
    log.append("Bodenmaterial: " + mat.get_path_name() + " aus " + ", ".join(have))

# 3) Eigene Hauptmaterialien (Projekt statt Engine-Plugin, mit Nutzung für Instanzen und Nanite):
#    M_TrendoPBR für Rinde, Holz, Fels; M_TrendoBlatt maskiert, zweiseitig, Two-Sided-Foliage (Licht scheint durch Blätter)
_masters = {}
def master(leaf, col, nor, arm, alpha, ao=True):
    # ao=False: Karte ist nur Metall/Rauheit (Rot leer) – dann ohne Umgebungsverdeckung, sonst würde das Objekt schwarz
    key = "M_TrendoBlatt" if leaf else "M_TrendoPBR" if ao else "M_TrendoPBR_MR"
    if key in _masters: return _masters[key]
    if eal.does_asset_exist(DEST + "/" + key): eal.delete_asset(DEST + "/" + key)
    m = tools.create_asset(key, DEST, unreal.Material, unreal.MaterialFactoryNew())
    mel = unreal.MaterialEditingLibrary
    if leaf:
        m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_MASKED)
        m.set_editor_property("two_sided", True)
        m.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_TWO_SIDED_FOLIAGE)
        m.set_editor_property("opacity_mask_clip_value", 0.4)
    for u in (unreal.MaterialUsage.MATUSAGE_INSTANCED_STATIC_MESHES, unreal.MaterialUsage.MATUSAGE_NANITE, unreal.MaterialUsage.MATUSAGE_STATIC_LIGHTING):
        mel.set_material_usage(m, u)
    def param(name, tex, y, sampler):
        e = mel.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -800, y)
        e.set_editor_property("parameter_name", name); e.set_editor_property("texture", tex); e.set_editor_property("sampler_type", sampler); return e
    c = param("Farbe", col, -400, unreal.MaterialSamplerType.SAMPLERTYPE_COLOR)
    mel.connect_material_property(c, "RGB", unreal.MaterialProperty.MP_BASE_COLOR)
    if leaf:
        a = param("Maske", alpha, -100, unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR)
        mel.connect_material_property(a, "R", unreal.MaterialProperty.MP_OPACITY_MASK)
        sss = mel.create_material_expression(m, unreal.MaterialExpressionMultiply, -300, -250)
        mel.connect_material_expressions(c, "RGB", sss, "A"); sss.set_editor_property("const_b", 0.6)
        mel.connect_material_property(sss, "", unreal.MaterialProperty.MP_SUBSURFACE_COLOR)
    n = param("Normal", nor, 200, unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL)
    mel.connect_material_property(n, "RGB", unreal.MaterialProperty.MP_NORMAL)
    r = param("ARM", arm, 500, unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR)
    if ao: mel.connect_material_property(r, "R", unreal.MaterialProperty.MP_AMBIENT_OCCLUSION)
    mel.connect_material_property(r, "G", unreal.MaterialProperty.MP_ROUGHNESS)
    mel.connect_material_property(r, "B", unreal.MaterialProperty.MP_METALLIC)
    mel.recompile_material(m); eal.save_loaded_asset(m); _masters[key] = m; return m

def imp(path, dest):
    t = task(path, dest); tools.import_asset_tasks([t]); p = t.get_editor_property("imported_object_paths")
    return unreal.load_asset(p[0]) if p else None

# 4) Modelle (glTF) importieren und auf die eigenen Materialien umstellen
_combine = None
def combined_pipeline():
    # Standard-glTF-Einstellungen von Unreal, aber alle Teile zu einem Modell zusammengefasst (Truhe mit Deckel, Uhr mit Zeigern …)
    global _combine
    if _combine: return _combine
    pp = "/Game/Trendo/Modelle/P_TrendoZusammen"
    if eal.does_asset_exist(pp): eal.delete_asset(pp)
    p = eal.duplicate_asset("/Interchange/Pipelines/DefaultGLTFAssetsPipeline", pp)
    p.get_editor_property("mesh_pipeline").set_editor_property("combine_static_meshes_behavior", unreal.InterchangeCombineStaticMeshesBehavior.ALL)
    eal.save_loaded_asset(p); _combine = pp; return pp

def bring(asset, dest, kind, combine=False):
    folder = os.path.join(SRC, asset)
    gltf = [f for f in os.listdir(folder) if f.endswith(".gltf")] if os.path.isdir(folder) else []
    if not gltf: return []
    if combine:
        sd = unreal.InterchangeManager.create_source_data(os.path.join(folder, gltf[0]))
        ip = unreal.ImportAssetParameters(); ip.is_automated = True; ip.replace_existing = True
        for x in (combined_pipeline(), "/Interchange/Pipelines/DefaultGLTFPipeline"): ip.override_pipelines.append(unreal.SoftObjectPath(x))
        unreal.InterchangeManager.get_interchange_manager_scripted().import_asset(dest, sd, ip)
    else:
        tools.import_asset_tasks([task(os.path.join(folder, gltf[0]), dest)])
    meshes = [p for p in eal.list_assets(dest, recursive=True, include_folder=False) if eal.find_asset_data(p).get_editor_property("asset_class_path").get_editor_property("asset_name") == "StaticMesh"]
    tdir = os.path.join(folder, "textures")
    alphas = [f[:-len("alpha_2k.png")] for f in (sorted(os.listdir(tdir)) if os.path.isdir(tdir) else []) if f.endswith("_alpha_2k.png")]
    leaves = opaque = 0
    for p in eal.list_assets(dest, recursive=True, include_folder=False):
        old = unreal.load_asset(p)
        if not isinstance(old, unreal.MaterialInstanceConstant) or old.get_name().startswith("MI_Trendo_"): continue
        tex = {str(t.get_editor_property("parameter_info").get_editor_property("name")): t.get_editor_property("parameter_value") for t in old.get_editor_property("texture_parameter_values")}
        col, nor, arm = tex.get("BaseColorTexture"), tex.get("NormalTexture"), tex.get("MetallicRoughnessTexture")
        if not (col and nor and arm): continue
        prefix = next((a for a in alphas if col.get_name().startswith(a + "diff")), None)
        alpha = None
        if prefix:
            # Blatt: Alpha-Karte und Farbkarte selbst importieren (die vom glTF-Import zusammengesetzte Farbkarte ist ohne Alpha-Bild leer)
            alpha = imp(os.path.join(tdir, prefix + "alpha_2k.png"), dest + "/Blatt")
            if not alpha: continue
            alpha.set_editor_property("srgb", False); eal.save_loaded_asset(alpha)
            dfile = os.path.join(tdir, prefix + "diff_2k.jpg")
            c2 = imp(dfile, dest + "/Blatt") if os.path.isfile(dfile) else None
            if c2: col = c2
            # Blatt-Texturen nie nachladen (sonst fehlen Blätter, solange nur grobe Stufen geladen sind: Maske < Schwelle)
            for tx in (alpha, col):
                tx.set_editor_property("never_stream", True); eal.save_loaded_asset(tx)
        # glTF-Normalen sind OpenGL-Normalen (Grün nach oben); Unreal erwartet DirectX
        if not nor.get_editor_property("flip_green_channel"):
            nor.set_editor_property("flip_green_channel", True); eal.save_loaded_asset(nor)
        par = master(prefix is not None, col, nor, arm, alpha, "arm" in arm.get_name().lower())
        name = "MI_Trendo_" + old.get_name()
        if eal.does_asset_exist(f"{dest}/{name}"): eal.delete_asset(f"{dest}/{name}")
        mic = tools.create_asset(name, dest, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        mic.set_editor_property("parent", par)
        mel = unreal.MaterialEditingLibrary
        for k, v in (("Farbe", col), ("Normal", nor), ("ARM", arm), ("Maske", alpha)):
            if v: mel.set_material_instance_texture_parameter_value(mic, k, v)
        eal.save_loaded_asset(mic)
        for m in meshes:
            sm = unreal.load_asset(m); mats = sm.get_editor_property("static_materials"); hit = False
            for idx, sl in enumerate(mats):
                if sl.get_editor_property("material_interface") == old: sm.set_material(idx, mic); hit = True
            if hit: eal.save_loaded_asset(sm)
        if prefix: leaves += 1
        else: opaque += 1
    # Alle Modelle mit Nanite (zehntausende Instanzen ohne Detailverlust); Pflanzen mit Flächenerhalt, sonst verschwinden dünne Blätter
    for m in meshes:
        sm = unreal.load_asset(m); ns = sm.get_editor_property("nanite_settings")
        ns.set_editor_property("enabled", True)
        if kind in ("gras", "farn", "strauch", "baum"):
            ns.set_editor_property("shape_preservation", unreal.NaniteShapePreservation.PRESERVE_AREA)
        sm.set_editor_property("nanite_settings", ns)
        if kind == "fest":
            # Hindernis: einfache Kollisionsbox, damit die Figur nicht hindurchläuft
            try: unreal.get_editor_subsystem(unreal.StaticMeshEditorSubsystem).add_simple_collisions(sm, unreal.ScriptCollisionShapeType.BOX)
            except Exception as e: log.append(f"{asset}: Kollision nicht gesetzt ({e})")
        eal.save_loaded_asset(sm)
    log.append(f"{asset}: {len(meshes)} Modell(e), {leaves} Blatt- und {opaque} Oberflächen-Material(ien)")
    return meshes

def ref(m): return m.split(".")[0] + "." + m.split("/")[-1].split(".")[0]

nature = []
for asset, kind in KINDS.items():
    for m in bring(asset, f"{DEST}/{asset}", kind):
        nature.append({"mesh": ref(m), "kind": kind, "asset": asset})
objects = {}
for role, asset in PROPS.items():
    ms = bring(asset, f"/Game/Trendo/Modelle/{asset}", "fest" if role in SOLID else "objekt", combine=True)
    if ms: objects[role] = ref(ms[0])

# 4b) Blut als Boden-Decal (nur genutzt, wenn das Spiel Gewalt darstellt): dunkles, nasses Rot, unregelmäßiger Rand aus Rauschen statt runder Klecks
try:
    bp = DEST + "/M_TrendoBlut"
    if eal.does_asset_exist(bp): eal.delete_asset(bp)
    bm = tools.create_asset("M_TrendoBlut", DEST, unreal.Material, unreal.MaterialFactoryNew())
    mel = unreal.MaterialEditingLibrary
    bm.set_editor_property("material_domain", unreal.MaterialDomain.MD_DEFERRED_DECAL)
    bm.set_editor_property("blend_mode", unreal.BlendMode.BLEND_TRANSLUCENT)
    col = mel.create_material_expression(bm, unreal.MaterialExpressionConstant3Vector, -600, -200); col.set_editor_property("constant", unreal.LinearColor(0.045, 0.002, 0.0015, 1))
    mel.connect_material_property(col, "", unreal.MaterialProperty.MP_BASE_COLOR)
    rough = mel.create_material_expression(bm, unreal.MaterialExpressionConstant, -600, 0); rough.set_editor_property("r", 0.62)
    mel.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    spec = mel.create_material_expression(bm, unreal.MaterialExpressionConstant, -600, 100); spec.set_editor_property("r", 0.25)
    mel.connect_material_property(spec, "", unreal.MaterialProperty.MP_SPECULAR)
    uv = mel.create_material_expression(bm, unreal.MaterialExpressionTextureCoordinate, -1200, 200)
    mid = mel.create_material_expression(bm, unreal.MaterialExpressionConstant2Vector, -1200, 320); mid.set_editor_property("r", 0.5); mid.set_editor_property("g", 0.5)
    dist = mel.create_material_expression(bm, unreal.MaterialExpressionDistance, -1000, 250)
    mel.connect_material_expressions(uv, "", dist, "A"); mel.connect_material_expressions(mid, "", dist, "B")
    inv = mel.create_material_expression(bm, unreal.MaterialExpressionOneMinus, -850, 250); mel.connect_material_expressions(dist, "", inv, "")
    noise = mel.create_material_expression(bm, unreal.MaterialExpressionNoise, -1000, 450)
    noise.set_editor_property("scale", 0.012); noise.set_editor_property("levels", 5); noise.set_editor_property("output_min", -0.55); noise.set_editor_property("output_max", 0.35)
    add = mel.create_material_expression(bm, unreal.MaterialExpressionAdd, -700, 300); mel.connect_material_expressions(inv, "", add, "A"); mel.connect_material_expressions(noise, "", add, "B")
    sub_ = mel.create_material_expression(bm, unreal.MaterialExpressionSubtract, -600, 300); sub_.set_editor_property("const_b", 0.72); mel.connect_material_expressions(add, "", sub_, "A")
    mul = mel.create_material_expression(bm, unreal.MaterialExpressionMultiply, -500, 300); mul.set_editor_property("const_b", 6.0); mel.connect_material_expressions(sub_, "", mul, "A")
    sat = mel.create_material_expression(bm, unreal.MaterialExpressionSaturate, -400, 300); mel.connect_material_expressions(mul, "", sat, "")
    mel.connect_material_property(sat, "", unreal.MaterialProperty.MP_OPACITY)
    mel.recompile_material(bm); eal.save_loaded_asset(bm)
    log.append("Blut-Decal: " + bm.get_path_name())
except Exception as e:
    log.append("Blut-Decal nicht angelegt: " + str(e)[:200])

# 5) Eigene Open-World-Karte mit Bodenmaterial
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
world = "/Game/Trendo/Welt"
if not eal.does_asset_exist(world):
    les.new_level_from_template(world, "/Engine/Maps/Templates/OpenWorld")
else:
    les.load_level(world)
count = 0
if mat:
    for a in unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors():
        if isinstance(a, unreal.LandscapeProxy):
            a.set_editor_property("landscape_material", mat); count += 1
les.save_current_level()
unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
log.append(f"Karte {world}: Bodenmaterial auf {count} Gelände-Teil(e)")

json.dump({"map": world, "ground": mat.get_path_name() if mat else "", "nature": nature, "objects": objects}, open(os.path.join(ROOT, "Trendo", "natur.json"), "w", encoding="utf-8"), indent=2)
for l in log: unreal.log("TRENDO-WELT " + l)
unreal.log("TRENDO-WELT-OK %d Pflanzen/Felsen-Modelle, %d Spielobjekt-Modelle" % (len(nature), len(objects)))
""";

    /// <summary>Python-Skript für Unreal: echter Mensch (MetaHuman aus Epic-Vorlage, Rig und Texturen aus dem MetaHuman-Dienst, Zusammenbau) und Bewegungen per IK-Retargeting.</summary>
    public const string MenschScript = """
# Von Trendo erzeugt: baut die Menschen des Spiels als MetaHumans (Spielfigur, Gegner, Figur) und überträgt die Bewegungen auf ihre Skelette.
# Läuft mit Grafik ohne Fenster (-RenderOffscreen): Hauttexturen entstehen auf der Grafikkarte.
# Braucht die MetaHuman Creator-Kerndaten (Epic Games Launcher → Bibliothek → 5.8 → Optionen) und die Epic-Anmeldung aus dem Launcher
# (Gesichts-Rig und hochaufgelöste Texturen kommen aus Epics MetaHuman-Dienst). Fehlt etwas, bleibt das Spiel bei der Gliederpuppe.
import os, traceback, unreal

ROOT = "/Game/Trendo/Mensch"
# (Name im Spiel, Epic-Vorlage, Geschlecht für die Kleidung) – Vorlagen bringen Haut, Haare, Augenbrauen, Wimpern mit; jede Figur sieht anders aus
CAST = [("Waldlaeuferin", "Ada", "F"), ("Gegner", "Victor", "M"), ("Figur", "Walter", "M")]
MOVES = ["MM_Idle", "MM_Jump", "MM_Fall_Loop", "MM_Walk_Fwd", "MM_Run_Fwd", "MM_Walk_InPlace", "BS_MM_WalkRun", "MM_Death_Front_03_ExtractedRM"]
SRC = "/MoverExamples/Characters/Mannequins/"
PRESETS = "/MetaHumanCharacter/Optional/Presets/"
eal = unreal.EditorAssetLibrary
tools = unreal.AssetToolsHelpers.get_asset_tools()
def log(m): unreal.log("TRENDO-MENSCH " + str(m).replace("\n", " | "))
def fail(m): unreal.log("TRENDO-MENSCH-FEHLER " + m)
def kind(p): return str(eal.find_asset_data(p).get_editor_property("asset_class_path").get_editor_property("asset_name"))
def again(fn):
    # Nach dem Zusammenbau meldet Unreal beim nächsten Dateizugriff einmal verzögert einen ARKit-Hinweis als Fehler – dann einmal wiederholen
    try: return fn()
    except Exception as e:
        if "ARKit" not in str(e): raise
        log("Hinweis (ARKit-Zuordnung) – Schritt wiederholt"); return fn()
def save_all():
    # Unreal meldet beim Speichern mancher Gesichts-Hilfsdateien (ARKit-Zuordnung) Hinweise als Fehler – gespeichert wird trotzdem
    try: unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
    except Exception as e: log("Hinweis beim Speichern: " + str(e)[:200])

def import_clothes():
    # Freie MetaHuman-Outfits (.mhpkg, z. B. von Fab) aus Trendo/Kleidung importieren; gibt die Kleidungsstücke (Wardrobe-Items) zurück
    folder = os.path.join(unreal.Paths.project_dir(), "Trendo", "Kleidung")
    for f in sorted(os.listdir(folder)) if os.path.isdir(folder) else []:
        if f.lower().endswith(".mhpkg"):
            t = unreal.AssetImportTask(); t.filename = os.path.join(folder, f); t.destination_path = "/Game/Trendo/Kleidung"; t.automated = True; t.save = True; t.replace_existing = True
            try: tools.import_asset_tasks([t])
            except Exception as e: log("Hinweis Kleidung %s: %s" % (f, str(e)[:200]))
    if not eal.does_directory_exist("/Game/Trendo/Kleidung"): return []
    return [p for p in eal.list_assets("/Game/Trendo/Kleidung", recursive=True) if kind(p) == "MetaHumanWardrobeItem"]

def dress(ch, gender, items):
    # Outfit passend zum Geschlecht (Dateiname „_F_“/„_M_“), sonst das erste; ersetzt die Grundkleidung der Vorlage
    pick = next((p for p in items if f"_{gender}_" in p.split("/")[-1].upper()), items[0] if items else None)
    if not pick: return "Grundkleidung von Epic"
    wi = unreal.load_asset(pick); col = ch.internal_collection
    key = col.try_add_item_from_wardrobe_item("Outfits", wi)
    col.default_instance.set_single_slot_selection("Outfits", key)
    eal.save_loaded_asset(ch, False)
    return wi.get_name()

# Abtrennbare Glieder (nur für Spiele mit Gewalt-Stufe „brutal“ genutzt): Unreals Geometrie-Skript schneidet Unterarme und Unterschenkel
# aus Körper und Kleidung heraus (Punkte nach ihrem stärksten Knochen), schließt die Schnittkante, legt den Drehpunkt ans Gelenk
# und speichert jedes Stück als eigenes Modell mit Materialien und Physik-Kollision.
LIMBS = ["lowerarm_l", "lowerarm_r", "calf_l", "calf_r"]
def cut_limbs(name):
    G = unreal; AU, BW, ME, MQ = G.GeometryScript_AssetUtils, G.GeometryScript_BoneWeights, G.GeometryScript_MeshEdits, G.GeometryScript_MeshQueries
    parts = [(k, p) for k, p in (("Koerper", f"{ROOT}/{name}/Bau/{name}/Body/SKM_{name}_BodyMesh"), ("Kleidung", f"{ROOT}/{name}/Bau/{name}/Clothing/{name}_Outfits")) if eal.does_asset_exist(p)]
    made = 0
    for part, path in parts:
        skm = unreal.load_asset(path); mats = [m.get_editor_property("material_interface") for m in skm.get_editor_property("materials")]
        base = unreal.DynamicMesh()
        base, _ = AU.copy_mesh_from_skeletal_mesh(skm, base, G.GeometryScriptCopyMeshFromAssetOptions(), G.GeometryScriptMeshReadLOD())
        n = MQ.get_vertex_count(base); bones = []
        for v in range(n):
            r = BW.get_largest_vertex_bone_weight(base, v); bones.append(r[1].bone_index if r[2] else -1)
        for limb in LIMBS:
            r = BW.get_bone_index(base, limb)
            if not r[1]: continue
            keep = {r[2]} | {k.get_editor_property("index") for k in BW.get_bone_children(base, limb, True)[2]}
            drop = [v for v, b in enumerate(bones) if b not in keep]
            if n - len(drop) < 50: continue   # z. B. kurzer Ärmel: an diesem Glied keine Kleidung
            dm = unreal.DynamicMesh()
            dm, _ = AU.copy_mesh_from_skeletal_mesh(skm, dm, G.GeometryScriptCopyMeshFromAssetOptions(), G.GeometryScriptMeshReadLOD())
            ME.delete_vertices_from_mesh(dm, G.GeometryScript_List.convert_array_to_index_list(drop, G.GeometryScriptIndexType.VERTEX))
            G.GeometryScript_MeshRepair.fill_all_mesh_holes(dm, G.GeometryScriptFillHolesOptions())
            # Drehpunkt ans Gelenk: Stück in den Knochenraum (Grundhaltung) legen – im Spiel sitzt es dann genau am Knochen
            wt = BW.get_bone_info(dm, limb)[2].get_editor_property("world_transform")
            G.GeometryScript_MeshTransforms.transform_mesh(dm, wt.inverse())
            dest = f"{ROOT}/{name}/Glieder/Glied_{limb}_{part}"
            if eal.does_asset_exist(dest): eal.delete_asset(dest)
            sm, _ = G.GeometryScript_NewAssetUtils.create_new_static_mesh_asset_from_mesh(dm, dest, G.GeometryScriptCreateNewStaticMeshAssetOptions())
            if not sm: continue
            for i, m in enumerate(mats):
                try: sm.set_material(i, m)
                except Exception: break
            try: G.GeometryScript_Collision.set_static_mesh_collision_from_mesh(dm, sm, G.GeometryScriptCollisionFromMeshOptions())
            except Exception as e: log("Hinweis Kollision %s: %s" % (limb, str(e)[:120]))
            eal.save_loaded_asset(sm); made += 1
    return made

def build_one(sub, name, preset, gender, clothes):
    ch = eal.duplicate_asset(PRESETS + preset, f"{ROOT}/{name}/{name}")
    def edit(fn):
        if not sub.try_add_object_to_edit(ch): raise RuntimeError("MetaHuman lässt sich nicht bearbeiten")
        try: fn()
        except Exception as e: log("Hinweis: " + str(e)[:300])   # Control-Rig-Hinweise beim Zusammenbau sind harmlos
        finally:
            if sub.is_object_added_for_editing(ch): sub.remove_object_to_edit(ch)
    ready = []
    def cloud():
        r = unreal.MetaHumanCharacterAutoRiggingRequestParams(); r.blocking = True; r.report_progress = False; r.rig_type = unreal.MetaHumanRigType.JOINTS_ONLY
        sub.request_auto_rigging(ch, r)
        t = unreal.MetaHumanCharacterTextureRequestParams(); t.blocking = True; t.report_progress = False
        sub.request_texture_sources(ch, t)
        ready.append(bool(ch.has_high_resolution_textures) and bool(sub.can_build_meta_human(ch)))   # nur während der Bearbeitung lesbar
    edit(cloud)
    if not (ready and ready[0]): return "MetaHuman-Dienst nicht erreicht – bitte im Epic Games Launcher angemeldet sein und Internet prüfen"
    eal.save_loaded_asset(ch, False)
    try: cloth = dress(ch, gender, clothes)
    except Exception: cloth = "Grundkleidung (Outfit ging nicht: " + traceback.format_exc().strip().splitlines()[-1][:150] + ")"
    def build():
        b = unreal.MetaHumanCharacterEditorBuildParameters()
        b.pipeline_type = unreal.MetaHumanDefaultPipelineType.OPTIMIZED
        b.pipeline_quality = unreal.MetaHumanQualityLevel.HIGH
        b.absolute_build_path = f"{ROOT}/{name}/Bau"; b.common_folder_path = f"{ROOT}/Gemeinsam"
        b.enable_wardrobe_item_validation = False
        sub.build_meta_human(character=ch, params=b)
    edit(build)
    save_all()
    body = None; parts = {}
    for p in again(lambda: eal.list_assets(f"{ROOT}/{name}/Bau", recursive=True)):
        c = kind(p); parts[c] = parts.get(c, 0) + 1
        if c == "SkeletalMesh" and "BodyMesh" in p: body = unreal.load_asset(p)
    if body is None or not eal.does_asset_exist(f"{ROOT}/{name}/Bau/{name}/BP_{name}"):
        return "Zusammenbau fehlgeschlagen – Protokoll mensch.txt"
    # Bewegungen der Engine-Figur (Mover-Beispiel) per IK-Retargeting auf das eigene Skelett übertragen – eigene Proportionen bleiben
    dest = f"{ROOT}/{name}/Bewegung"
    for a in MOVES:
        to = f"{dest}/Quelle/{a}"
        again(lambda: eal.does_asset_exist(to) or eal.duplicate_asset(SRC + "Animations/Manny/" + a, to))
    bs = unreal.load_asset(f"{dest}/Quelle/BS_MM_WalkRun"); samples = bs.get_editor_property("sample_data")
    for s in samples: s.set_editor_property("animation", unreal.load_asset(f"{dest}/Quelle/" + s.get_editor_property("animation").get_name()))
    bs.set_editor_property("sample_data", samples); again(lambda: eal.save_loaded_asset(bs))
    rig = tools.create_asset(f"IK_{name}", dest, unreal.IKRigDefinition, unreal.IKRigDefinitionFactory())
    rc = unreal.IKRigController.get_controller(rig); rc.set_skeletal_mesh(body); rc.apply_auto_generated_retarget_definition(); eal.save_loaded_asset(rig)
    rtg = tools.create_asset(f"RTG_Figur_{name}", dest, unreal.IKRetargeter, unreal.IKRetargetFactory())
    c = unreal.IKRetargeterController.get_controller(rtg)
    src_rig = unreal.load_asset(SRC + "Rigs/IK_Mannequin"); manny = unreal.load_asset(SRC + "Meshes/SKM_Manny_Simple")
    c.set_ik_rig(unreal.RetargetSourceOrTarget.SOURCE, src_rig); c.set_ik_rig(unreal.RetargetSourceOrTarget.TARGET, rig)
    c.remove_all_ops(); c.add_default_ops()
    c.assign_ik_rig_to_all_ops(unreal.RetargetSourceOrTarget.SOURCE, src_rig); c.assign_ik_rig_to_all_ops(unreal.RetargetSourceOrTarget.TARGET, rig)
    c.auto_map_chains(unreal.AutoMapChainType.FUZZY, True); eal.save_loaded_asset(rtg)
    data = [eal.find_asset_data(f"{dest}/Quelle/{a}") for a in MOVES]
    unreal.IKRetargetBatchOperation.duplicate_and_retarget(assets_to_retarget=data, source_mesh=manny, target_mesh=body, ik_retarget_asset=rtg,
                                                           prefix="MH_", target_path=dest, include_referenced_assets=True)
    save_all()
    have = [a for a in ("MM_Idle", "MM_Jump", "MM_Fall_Loop", "BS_MM_WalkRun") if eal.does_asset_exist(f"{dest}/MH_{a}")]
    if len(have) < 4: return "Bewegungen nicht vollständig übertragen: " + ", ".join(have)
    if name == "Gegner":
        try: log("Glieder: %d abtrennbare Stücke" % cut_limbs(name))
        except Exception: log("Hinweis Glieder: " + traceback.format_exc().strip().splitlines()[-1][:200])
    log(f"{name} ({preset}) gebaut: {parts.get('SkeletalMesh', 0)} Körperteile, {parts.get('GroomAsset', 0)} Haar-Arten, Kleidung {cloth}, Bewegungen übertragen")
    return ""

def run():
    if unreal.load_asset(PRESETS + CAST[0][1]) is None:
        return fail("MetaHuman Creator-Kerndaten fehlen – Epic Games Launcher → Bibliothek → Unreal 5.8 → Optionen → „MetaHuman Creator-Kerndaten“ anhaken → Übernehmen")
    sub = unreal.get_editor_subsystem(unreal.MetaHumanCharacterEditorSubsystem)
    if eal.does_directory_exist(ROOT): eal.delete_directory(ROOT)
    clothes = import_clothes()
    log("Kleidung: %d Outfit(s) – %s" % (len(clothes), ", ".join(p.split("/")[-1].split(".")[0] for p in clothes) or "Grundkleidung von Epic"))
    built = []
    for name, preset, gender in CAST:
        try: err = build_one(sub, name, preset, gender, clothes)
        except Exception: err = traceback.format_exc().strip().splitlines()[-1]
        if err:
            if name == CAST[0][0]: return fail(err)   # ohne Spielfigur kein Mensch im Spiel
            log(f"{name}: nicht gebaut ({err}) – bleibt Gliederpuppe")
        else: built.append(name)
    unreal.log("TRENDO-MENSCH-OK %d %s" % (len(built), ",".join(built)))

try: run()
except Exception: fail(traceback.format_exc().strip().splitlines()[-1])
""";

    static readonly Dictionary<string, string> Sources = new()
    {
        ["TrendoTypes.h"] = """
// Von Trendo erzeugt: Spieldaten und Regeln (ohne Welt testbar).
#pragma once

#include "CoreMinimal.h"

/** Regel bzw. Aufgabe: Wenn When ≥ N, dann Act (einmal). Mit Titel = Aufgabe mit Fortschritt. */
struct FTrendoRule { FString When; int32 N = 0; FString Act; FString Title; };

/** Ein Level als Raster wie im Trendo-Editor. */
struct FTrendoLevelRows { FString Name; TArray<FString> Rows; };

/** Das ganze Spiel aus Trendo (level.json). */
struct FTrendoData
{
	FString Name = TEXT("Spiel"); FString World = TEXT("level");
	int32 Speed = 5; int32 Lives = 3; FString Weapon; bool bMenu = true;
	/** Gewalt-Darstellung: keine (kein Blut, Gegner verschwinden), normal (Blut, Ragdoll), brutal (zusätzlich stärkere Treffer). */
	FString Violence = TEXT("normal");
	TArray<FString> Abilities; TArray<FTrendoLevelRows> Levels; TArray<FTrendoRule> Rules;
	TMap<FString, FString> Texts; TMap<FString, float> Props;

	bool Has(const FString& Ability) const { return Abilities.Contains(Ability); }
	FString Key(int32 Level, int32 X, int32 Y, const FString& K) const;
	FString Text(int32 Level, int32 X, int32 Y, const FString& K, const FString& Default) const;
	float Prop(int32 Level, int32 X, int32 Y, const FString& K, float Default) const;
	/** level.json lesen; false mit Grund bei kaputter Datei. */
	static bool Parse(const FString& Json, FTrendoData& Out, FString& Error);
};

/** Spielstand, Fortschritt und Regeln. */
struct FTrendoState
{
	int32 Level = 0, Score = 0, Lives = 3, Coins = 0, Defeated = 0, Items = 0; float Seconds = 0; float Boost = 0;
	bool bWon = false, bOver = false, bDoorsOpen = false;
	TArray<FString> Inventory; TArray<bool> Fired; TSet<FString> Given; FString Note;

	void Reset(const FTrendoData& D) { *this = FTrendoState(); Lives = D.Lives; Fired.Init(false, D.Rules.Num()); }
	double RuleValue(const FTrendoRule& R) const;
	/** Wirkung (Regel oder sprechende Figur): leben, tueren, schneller, punkte, sieg. */
	void Apply(const FString& What);
	/** Regeln prüfen; gibt die Zahl der neu erfüllten zurück. */
	int32 CheckRules(const FTrendoData& D);
	/** Aufgaben mit Fortschritt, eine je Zeile. */
	FString Quests(const FTrendoData& D) const;
	/** Sprechende Figur: nimmt den gewünschten Gegenstand und belohnt; sonst ihr Text. */
	void TalkTo(const FTrendoData& D, int32 X, int32 Y);
};
""",
        ["TrendoTypes.cpp"] = """
// Von Trendo erzeugt: Spieldaten und Regeln.
#include "TrendoTypes.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

FString FTrendoData::Key(int32 Level, int32 X, int32 Y, const FString& K) const
{
	return (Level == 0 ? FString() : FString::Printf(TEXT("%d:"), Level + 1)) + FString::Printf(TEXT("%d,%d:"), X, Y) + K;
}

FString FTrendoData::Text(int32 Level, int32 X, int32 Y, const FString& K, const FString& Default) const
{
	const FString* V = Texts.Find(Key(Level, X, Y, K));
	return V && !V->IsEmpty() ? *V : Default;
}

float FTrendoData::Prop(int32 Level, int32 X, int32 Y, const FString& K, float Default) const
{
	const float* V = Props.Find(Key(Level, X, Y, K));
	return V ? *V : Default;
}

bool FTrendoData::Parse(const FString& Json, FTrendoData& Out, FString& Error)
{
	TSharedPtr<FJsonObject> O;
	if (!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json), O) || !O.IsValid()) { Error = TEXT("level.json ist beschädigt."); return false; }
	Out = FTrendoData();
	O->TryGetStringField(TEXT("name"), Out.Name);
	O->TryGetStringField(TEXT("world"), Out.World);
	O->TryGetNumberField(TEXT("speed"), Out.Speed);
	O->TryGetNumberField(TEXT("lives"), Out.Lives);
	O->TryGetStringField(TEXT("weapon"), Out.Weapon);
	O->TryGetBoolField(TEXT("menu"), Out.bMenu);
	O->TryGetStringField(TEXT("violence"), Out.Violence); if (Out.Violence != TEXT("keine") && Out.Violence != TEXT("brutal")) Out.Violence = TEXT("normal");
	const TArray<TSharedPtr<FJsonValue>>* A;
	if (O->TryGetArrayField(TEXT("abilities"), A)) for (const auto& V : *A) Out.Abilities.Add(V->AsString());
	if (O->TryGetArrayField(TEXT("levels"), A))
		for (const auto& V : *A)
		{
			FTrendoLevelRows L; const TSharedPtr<FJsonObject>& LO = V->AsObject(); if (!LO.IsValid()) continue;
			LO->TryGetStringField(TEXT("name"), L.Name);
			const TArray<TSharedPtr<FJsonValue>>* R; if (LO->TryGetArrayField(TEXT("rows"), R)) for (const auto& RV : *R) L.Rows.Add(RV->AsString());
			if (L.Rows.Num() > 0) Out.Levels.Add(L);
		}
	if (O->TryGetArrayField(TEXT("rules"), A))
		for (const auto& V : *A)
		{
			const TSharedPtr<FJsonObject>& RO = V->AsObject(); if (!RO.IsValid()) continue;
			FTrendoRule R; RO->TryGetStringField(TEXT("when"), R.When); RO->TryGetNumberField(TEXT("n"), R.N); RO->TryGetStringField(TEXT("act"), R.Act); RO->TryGetStringField(TEXT("title"), R.Title);
			Out.Rules.Add(R);
		}
	if (O->TryGetArrayField(TEXT("texts"), A))
		for (const auto& V : *A) { const TSharedPtr<FJsonObject>& T = V->AsObject(); if (T.IsValid()) Out.Texts.Add(T->GetStringField(TEXT("key")), T->GetStringField(TEXT("value"))); }
	if (O->TryGetArrayField(TEXT("props"), A))
		for (const auto& V : *A) { const TSharedPtr<FJsonObject>& P = V->AsObject(); if (P.IsValid()) Out.Props.Add(P->GetStringField(TEXT("key")), (float)P->GetNumberField(TEXT("value"))); }
	if (Out.Levels.Num() == 0) { Error = TEXT("level.json enthält kein Level."); return false; }
	return true;
}

double FTrendoState::RuleValue(const FTrendoRule& R) const
{
	if (R.When == TEXT("muenzen")) return Coins;
	if (R.When == TEXT("punkte")) return Score;
	if (R.When == TEXT("gegner")) return Defeated;
	if (R.When == TEXT("sekunden")) return Seconds;
	if (R.When == TEXT("gegenstaende")) return Items;
	return 0;
}

void FTrendoState::Apply(const FString& What)
{
	if (What == TEXT("leben")) Lives++;
	else if (What == TEXT("tueren")) bDoorsOpen = true;
	else if (What == TEXT("schneller")) Boost = 2;
	else if (What == TEXT("punkte")) Score += 10;
	else if (What == TEXT("sieg")) bWon = true;
}

int32 FTrendoState::CheckRules(const FTrendoData& D)
{
	int32 New = 0;
	if (Fired.Num() != D.Rules.Num()) Fired.Init(false, D.Rules.Num());
	for (int32 i = 0; i < D.Rules.Num(); i++)
	{
		if (Fired[i] || RuleValue(D.Rules[i]) < D.Rules[i].N) continue;
		Fired[i] = true; Apply(D.Rules[i].Act); New++;
		if (!D.Rules[i].Title.IsEmpty()) Note = TEXT("✓ ") + D.Rules[i].Title;
	}
	return New;
}

FString FTrendoState::Quests(const FTrendoData& D) const
{
	FString S;
	for (int32 i = 0; i < D.Rules.Num(); i++)
	{
		const FTrendoRule& R = D.Rules[i]; if (R.Title.IsEmpty()) continue;
		S += Fired.IsValidIndex(i) && Fired[i] ? TEXT("✓ ") + R.Title : FString::Printf(TEXT("%s: %d/%d"), *R.Title, FMath::Min((int32)RuleValue(R), R.N), R.N);
		S += TEXT("\n");
	}
	return S;
}

void FTrendoState::TalkTo(const FTrendoData& D, int32 X, int32 Y)
{
	const FString K = FString::Printf(TEXT("%d:%d,%d"), Level, X, Y);
	const FString Need = D.Text(Level, X, Y, TEXT("need"), FString());
	if (!Need.IsEmpty() && !Given.Contains(K) && Inventory.RemoveSingle(Need) > 0)
	{
		Given.Add(K); Apply(D.Text(Level, X, Y, TEXT("give"), TEXT("punkte"))); Note = D.Text(Level, X, Y, TEXT("thanks"), TEXT("Danke!"));
	}
	else Note = Given.Contains(K) ? D.Text(Level, X, Y, TEXT("thanks"), TEXT("Danke!")) : D.Text(Level, X, Y, TEXT("say"), TEXT("Hallo!"));
}
""",
        ["TrendoActors.h"] = """
// Von Trendo erzeugt: Spielobjekte (Kacheln, Gegner, Geschoss) – mit echten Modellen, sonst Grundformen.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "GameFramework/Character.h"
#include "TrendoActors.generated.h"

class UStaticMeshComponent; class USkeletalMeshComponent; class USphereComponent; class UStaticMesh; class USkeletalMesh;

/** Ein Objekt aus dem Trendo-Raster: Boden, Münze, Stacheln, Ziel, Feder, Speicherpunkt, Schlüssel, Tür, Gegenstand, Figur, Fahrzeug. */
UCLASS()
class ATrendoThing : public AActor
{
	GENERATED_BODY()
public:
	ATrendoThing();
	/** Kachel-Zeichen wie in Trendo (#, o, ^, Z, F, C, K, D, G, N, V). */
	UPROPERTY(VisibleAnywhere) FString Tile;
	UPROPERTY(VisibleAnywhere) int32 TileX = 0;
	UPROPERTY(VisibleAnywhere) int32 TileY = 0;
	UPROPERTY(VisibleAnywhere) FString Kind;
	/** Fangbereich (Berühren): Größe passt zum Objekt. */
	UPROPERTY(VisibleAnywhere) USphereComponent* Catch;
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Mesh;
	UPROPERTY(VisibleAnywhere) USkeletalMeshComponent* Figure;
	/** Animierter Körper: MetaHuman, sonst Gliederpuppe (Figure). */
	UPROPERTY() USkeletalMeshComponent* Body3D = nullptr;
	void Setup(TCHAR InTile, int32 X, int32 Y, const FLinearColor& Color, float Size, bool bSolid);
	/** true, wenn ein echtes Modell statt einer Grundform verwendet wird. */
	bool bModel = false;
	/** Fahrbarer Wagen (Chaos-Fahrphysik aus dem Engine-Beispiel), wenn vorhanden. */
	UPROPERTY() APawn* Car = nullptr;
};

/** Gegner als echter Charakter (Nutzerwunsch 07.11.2026: nie durch Boden oder Objekte, echtes Laufen, kein Flackern): Kollisionskapsel, Schwerkraft,
 *  Bodenkontakt und Stufen über die Charakter-Bewegung von Unreal, gesteuert von einem KI-Controller; Trefferpunkte wie in Trendo, ab 2 ein Boss. */
UCLASS()
class ATrendoEnemy : public ACharacter
{
	GENERATED_BODY()
public:
	ATrendoEnemy();
	/** Grundform (ohne Menschmodell) und Berührungsfühler für die Spielfigur. */
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Shape;
	UPROPERTY(VisibleAnywhere) USkeletalMeshComponent* Figure;
	/** Animierter Körper: MetaHuman, sonst Gliederpuppe (Figure). */
	UPROPERTY() USkeletalMeshComponent* Body3D = nullptr;
	UPROPERTY(VisibleAnywhere) int32 Hp = 1;
	UPROPERTY(VisibleAnywhere) int32 MaxHp = 1;
	/** Im Menü still (von GameMode gesetzt). */
	bool bFrozen = false;
	bool bModel = false;
	/** Trefferpunkte setzen und Aussehen wählen (Boss größer); Ground = Bodenhöhe unter dem Gegner. */
	void Init(int32 InHp, float Ground);
	virtual void Tick(float DeltaSeconds) override;
	virtual void BeginPlay() override;
	/** Open World: Streife, Sehen (Blickfeld, freie Sicht), Verfolgen, Angreifen, Suchen am letzten Ort, Rückzug des angeschlagenen Bosses, Hindernissen ausweichen. */
	void ThinkOpenWorld(float Dt, class ATrendoGameMode* GM);
	/** Treffer: physikalisch zurückgestoßen, kurz benommen. */
	void Knock(const FVector& Push);
	/** Tod: Körper fällt als Ragdoll (Physik, Gewicht) mit Stoß; false, wenn kein Körper mit Physik da ist. */
	bool Ragdoll(const FVector& Impulse);
	/** Brutale Spiele: ein Glied (Unterarm/Unterschenkel) abtrennen – am Körper ausblenden, ausgeschnittenes Stück fliegt als Physikteil; false ohne Glieder. */
	bool Sever(const FVector& Push);
	/** Abgetrennter Knochen (für die Blutlache am Stumpf), sonst NAME_None. */
	FName SeveredBone = NAME_None;
	float Stun = 0; bool bDead = false;
	/** Sekunden, die der Gegner nach dem letzten Sichtkontakt noch sucht (> 0 = alarmiert). */
	float Alert = 0;
private:
	FVector Start; float T = 0; float Attack = 0; FVector LastSeen = FVector::ZeroVector;
	float Stuck = 0, Detour = 0, DetourSign = 1;
	/** In Richtung Goal gehen (Tempo in cm/s) – die Charakter-Bewegung sorgt für Boden, Stufen und Kollision. */
	void WalkTo(const FVector& Goal, float Speed, float Dt);
};

/** Geschoss der Waffe „Werfen“ (mit Modell: geworfenes Beil, das sich dreht). */
UCLASS()
class ATrendoShot : public AActor
{
	GENERATED_BODY()
public:
	ATrendoShot();
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Mesh;
	FVector Dir = FVector::ForwardVector; float Life = 1.5f;
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
};

/** Farbe für eine Form setzen (Engine-Grundmaterial mit Farbparameter). */
void TrendoPaint(UStaticMeshComponent* Mesh, const FLinearColor& Color);
/** Echtes Modell zu einer Rolle (o, G, Z, waffe, wurf …) aus Content/Trendo/natur.json, sonst nullptr. */
UStaticMesh* TrendoModel(const UObject* Context, const FString& Role);
/** Menschmodell mit Skelett (Epic-Mannequin aus dem Engine-Plugin MoverExamples), sonst nullptr. */
USkeletalMesh* TrendoHuman();
/** Sportwagen (Engine-Plugin ChaosModularVehicleExamples), sonst nullptr. */
USkeletalMesh* TrendoCar();
/** Menschfigur bewegen: Gehen/Laufen fließend nach Tempo (Blendspace), in der Luft Sprung bzw. Fallen, im Stand Atmen. */
void TrendoAnimate(USkeletalMeshComponent* Figure, const FVector& Velocity, bool bInAir);
/** MetaHuman (echter Mensch mit Gesicht, Haaren, Kleidung) als Teil von Owner unter Parent; gibt den Körper zum Animieren zurück, sonst nullptr. */
USkeletalMeshComponent* TrendoSetupMetaHuman(AActor* Owner, USceneComponent* Parent, float FootZ, const TCHAR* Who = TEXT("Waldlaeuferin"), float Scale = 1.f);
/** Figur einrichten: Modell, Blickrichtung (Mannequin schaut entlang Y), Füße auf FootZ, matte Farbe (Tint) statt Chrom. */
bool TrendoSetupFigure(USkeletalMeshComponent* Figure, float FootZ, const FLinearColor& Tint = FLinearColor(0.32f, 0.36f, 0.27f));
""",
        ["TrendoActors.cpp"] = """
// Von Trendo erzeugt: Spielobjekte – echte Modelle (Poly Haven, CC0; Epic-Mannequin und Sportwagen aus Engine-Plugins), sonst Grundformen.
#include "TrendoActors.h"
#include "TrendoGameMode.h"
#include "TrendoCar.h"
#include "Misc/CommandLine.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Animation/AnimSingleNodeInstance.h"
#include "Animation/AnimationAsset.h"
#include "Engine/StaticMesh.h"
#include "Engine/SkeletalMesh.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Kismet/GameplayStatics.h"
#include "Components/ChildActorComponent.h"
#include "Animation/Skeleton.h"
#include "PhysicsEngine/PhysicsAsset.h"
#include "AIController.h"
#include "Engine/StaticMeshActor.h"
#include "Components/DecalComponent.h"
#include "Components/CapsuleComponent.h"
#include "GameFramework/CharacterMovementComponent.h"

static UStaticMesh* TrendoShape(const TCHAR* Path) { return LoadObject<UStaticMesh>(nullptr, Path); }
static const TCHAR* HumanPath = TEXT("/MoverExamples/Characters/Mannequins/Meshes/SKM_Manny_Simple.SKM_Manny_Simple");
static const TCHAR* AnimDir = TEXT("/MoverExamples/Characters/Mannequins/Animations/Manny/");
static const TCHAR* CarPath = TEXT("/ChaosModularVehicleExamples/Models/SportsCar/SKM_SportsCar.SKM_SportsCar");

template <class T> static T* Cached(const FString& Path)
{
	static TMap<FString, TWeakObjectPtr<UObject>> Cache; static TSet<FString> Missing;
	if (Missing.Contains(Path)) return nullptr;
	TWeakObjectPtr<UObject>& W = Cache.FindOrAdd(Path);
	if (!W.IsValid()) { W = LoadObject<T>(nullptr, *Path); if (!W.IsValid()) { Missing.Add(Path); return nullptr; } }
	return Cast<T>(W.Get());
}
// MetaHuman (von Trendo im Projekt gebaut: Körper, Gesicht, Kleidung, Haare) und seine auf das MetaHuman-Skelett übertragenen Bewegungen
// Jede Figur (Waldlaeuferin, Gegner, Figur) liegt unter /Game/Trendo/Mensch/<Name>/ mit Bau/ und Bewegung/
static FString MetaHumanDir(const TCHAR* Who) { return FString::Printf(TEXT("/Game/Trendo/Mensch/%s/"), Who); }
static UAnimationAsset* Anim(const TCHAR* Name, const FString& MetaDir = FString())
{
	return MetaDir.Len() ? Cached<UAnimationAsset>(FString::Printf(TEXT("%sBewegung/MH_%s.MH_%s"), *MetaDir, Name, Name)) : Cached<UAnimationAsset>(FString::Printf(TEXT("%s%s.%s"), AnimDir, Name, Name));
}
/** Bewegungs-Ordner eines MetaHuman-Körpers (als Kennzeichen am Körper gespeichert), sonst leer = Gliederpuppe. */
static FString MetaDirOf(const USkeletalMeshComponent* F)
{
	if (F) for (const FName& T : F->ComponentTags) { const FString S = T.ToString(); if (S.StartsWith(TEXT("TrendoMensch:"))) return S.Mid(13); }
	return FString();
}
static bool IsMeta(const USkeletalMeshComponent* F)
{
	const USkeletalMesh* M = F ? F->GetSkeletalMeshAsset() : nullptr;
	return M && M->GetSkeleton() && M->GetSkeleton()->GetName().StartsWith(TEXT("metahuman"));
}

USkeletalMeshComponent* TrendoSetupMetaHuman(AActor* Owner, USceneComponent* Parent, float FootZ, const TCHAR* Who, float Scale)
{
	const FString Dir = MetaHumanDir(Who);
	UClass* C = Owner ? LoadClass<AActor>(nullptr, *FString::Printf(TEXT("%sBau/%s/BP_%s.BP_%s_C"), *Dir, Who, Who, Who)) : nullptr; if (!C || !Parent) return nullptr;
	UChildActorComponent* CA = NewObject<UChildActorComponent>(Owner, TEXT("MetaHuman"));
	CA->SetChildActorClass(C); CA->SetupAttachment(Parent);
	CA->SetUsingAbsoluteScale(true); CA->SetWorldScale3D(FVector(Scale)); // eigene Größe, auch wenn der Trefferkörper gestreckt ist
	CA->SetRelativeLocationAndRotation(FVector(0, 0, FootZ), FRotator(0, -90, 0)); // MetaHuman schaut entlang Y
	CA->RegisterComponent();
	AActor* A = CA->GetChildActor(); if (!A) { CA->DestroyComponent(); return nullptr; }
	A->SetActorEnableCollision(false);
	TArray<USkeletalMeshComponent*> Parts; A->GetComponents(Parts);
	USkeletalMeshComponent* Body = nullptr;
	for (USkeletalMeshComponent* P : Parts) if (IsMeta(P) && (!Body || P->GetName().Contains(TEXT("Body")))) Body = P;
	if (!Body || !Anim(TEXT("MM_Idle"), Dir)) { CA->DestroyComponent(); return nullptr; }
	Body->ComponentTags.Add(FName(*(TEXT("TrendoMensch:") + Dir)));
	for (USkeletalMeshComponent* P : Parts) { P->bEnableUpdateRateOptimizations = false; P->VisibilityBasedAnimTickOption = EVisibilityBasedAnimTickOption::AlwaysTickPoseAndRefreshBones; }
	Body->SetAnimationMode(EAnimationMode::AnimationSingleNode);
	Body->PlayAnimation(Anim(TEXT("MM_Idle"), Dir), true);
	return Body;
}

USkeletalMesh* TrendoHuman() { return Cached<USkeletalMesh>(HumanPath); }
USkeletalMesh* TrendoCar() { return Cached<USkeletalMesh>(CarPath); }

UStaticMesh* TrendoModel(const UObject* Context, const FString& Role)
{
	const ATrendoGameMode* GM = Context ? Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(Context)) : nullptr;
	if (!GM) return nullptr;
	UStaticMesh* const* M = GM->Models.Find(Role); return M ? *M : nullptr;
}

bool TrendoSetupFigure(USkeletalMeshComponent* Figure, float FootZ, const FLinearColor& Tint)
{
	USkeletalMesh* Human = TrendoHuman(); if (!Figure || !Human) return false;
	Figure->SetSkeletalMesh(Human);
	Figure->SetRelativeLocationAndRotation(FVector(0, 0, FootZ), FRotator(0, -90, 0)); // Mannequin schaut entlang Y
	Figure->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Figure->SetAnimationMode(EAnimationMode::AnimationSingleNode);
	Figure->SetVisibility(true); Figure->SetCastShadow(true);
	for (int32 i = 0; i < Figure->GetNumMaterials(); i++)
		if (UMaterialInstanceDynamic* M = Figure->CreateDynamicMaterialInstance(i)) { M->SetVectorParameterValue(TEXT("Tint"), Tint); M->SetScalarParameterValue(TEXT("Metal_Brightness"), 0.35f); }
	if (UAnimationAsset* Idle = Anim(TEXT("MM_Idle"))) Figure->PlayAnimation(Idle, true);
	return true;
}

void TrendoAnimate(USkeletalMeshComponent* Figure, const FVector& Velocity, bool bInAir)
{
	if (!Figure || !Figure->GetSkeletalMeshAsset() || !Figure->IsVisible()) return;
	UAnimSingleNodeInstance* I = Figure->GetSingleNodeInstance(); if (!I) return;
	const float Speed = Velocity.Size2D(); const FString M = MetaDirOf(Figure);
	UAnimationAsset* Want = bInAir ? Anim(Velocity.Z > 200.f ? TEXT("MM_Jump") : TEXT("MM_Fall_Loop"), M) : Speed < 15.f ? Anim(TEXT("MM_Idle"), M) : Anim(TEXT("BS_MM_WalkRun"), M);
	if (!Want) return;
	if (I->GetCurrentAsset() != Want) I->SetAnimationAsset(Want, Want != Anim(TEXT("MM_Jump"), M), 1.f);
	if (Want == Anim(TEXT("BS_MM_WalkRun"), M))
	{
		// Blendspace: 0–231 cm/s Gehen, bis 500 cm/s Laufen; schneller (Sprint, Wagen) läuft die Lauf-Animation schneller, damit die Füße nicht rutschen
		I->SetBlendSpacePosition(FVector(FMath::Min(Speed, 500.f), 0, 0));
		I->SetPlayRate(Speed > 500.f ? Speed / 500.f : 1.f);
	}
	else I->SetPlayRate(1.f);
}

void TrendoPaint(UStaticMeshComponent* Mesh, const FLinearColor& Color)
{
	if (!Mesh) return;
	if (UMaterialInterface* Base = LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial")))
	{
		UMaterialInstanceDynamic* M = UMaterialInstanceDynamic::Create(Base, Mesh);
		M->SetVectorParameterValue(TEXT("Color"), Color);
		Mesh->SetMaterial(0, M);
	}
}

ATrendoThing::ATrendoThing()
{
	Catch = CreateDefaultSubobject<USphereComponent>(TEXT("Fang"));
	RootComponent = Catch; Catch->SetSphereRadius(50.f); Catch->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Form"));
	Mesh->SetupAttachment(Catch);
	Figure = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("Figur"));
	Figure->SetupAttachment(Catch); Figure->SetVisibility(false); Figure->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void ATrendoThing::Setup(TCHAR InTile, int32 X, int32 Y, const FLinearColor& Color, float Size, bool bSolid)
{
	Tile = FString::Chr(InTile); TileX = X; TileY = Y;
	const ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	const float Foot = GM && GM->Data.World == TEXT("openworld") ? -60.f : -50.f * Size; // Abstand Mitte → Boden
	// 1) Figur (N) als Mensch, Fahrzeug (V „wagen“) als Sportwagen
	if (InTile == 'N') { Body3D = TrendoSetupMetaHuman(this, Catch, Foot, TEXT("Figur")); if (Body3D) bModel = true; else if (TrendoSetupFigure(Figure, Foot, FLinearColor(0.42f, 0.33f, 0.22f))) { bModel = true; Body3D = Figure; } }
	if (InTile == 'V' && Kind != TEXT("ufo") && GM && GM->Data.World == TEXT("openworld"))
		{
			// echter Wagen (Chaos Vehicles): Motor, Automatik, Federung, Reifen, Lenkung, drehende Räder; Einsteigen mit E (oder Berühren), Aussteigen mit E
			FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
			ATrendoCar* NewCar = GetWorld()->SpawnActor<ATrendoCar>(GetActorLocation() + FVector(0, 0, 80), FRotator(0, GM->PathYaw(), 0), SP); // in Wegrichtung: dort ist frei
			if (NewCar && NewCar->bReady) { Car = NewCar; bModel = true; } else if (NewCar) NewCar->Destroy();
		}
	if (InTile == 'V' && Kind != TEXT("ufo") && !Car)
		if (USkeletalMesh* CarModel = TrendoCar()) { Figure->SetSkeletalMesh(CarModel); Figure->SetRelativeLocation(FVector(0, 0, Foot)); Figure->SetVisibility(true); Figure->SetCastShadow(true); bModel = true; }
	// 2) Gegenstände, Ziel, Speicherpunkt, Kiste … als Fotomodell (echte Größe, zufällig gedreht)
	if (!bModel)
		if (UStaticMesh* M = TrendoModel(this, Tile))
		{
			Mesh->SetStaticMesh(M); Mesh->SetRelativeLocationAndRotation(FVector(0, 0, Foot), FRotator(0, (X * 47 + Y * 13) % 360, 0));
			Mesh->SetCollisionProfileName(bSolid ? TEXT("BlockAll") : TEXT("NoCollision"));
			bModel = true;
		}
	if (bModel)
	{
		if (!bSolid)
		{
			Catch->SetSphereRadius(InTile == 'V' ? (Car ? 1.f : 260.f) : InTile == 'Z' ? 110.f : InTile == 'N' ? 90.f : 70.f);
			Catch->SetCollisionProfileName(TEXT("OverlapAllDynamic")); Catch->SetGenerateOverlapEvents(true);
		}
		if (InTile != 'N' && InTile != 'V' && !Mesh->GetStaticMesh()) Mesh->SetVisibility(false);
		if (InTile == 'N' || InTile == 'V') Mesh->SetVisibility(false);
		if (Car) { Figure->SetVisibility(false); Catch->SetCollisionEnabled(ECollisionEnabled::NoCollision); }
		return;
	}
	// 3) Ohne Modell: Grundform wie bisher
	const bool Round = InTile == 'o' || InTile == 'G' || InTile == 'K';
	Mesh->SetStaticMesh(TrendoShape(Round ? TEXT("/Engine/BasicShapes/Sphere.Sphere") : InTile == 'N' ? TEXT("/Engine/BasicShapes/Cylinder.Cylinder") : TEXT("/Engine/BasicShapes/Cube.Cube")));
	Mesh->SetWorldScale3D(FVector(Size));
	Mesh->SetCollisionProfileName(bSolid ? TEXT("BlockAll") : TEXT("OverlapAllDynamic"));
	Mesh->SetGenerateOverlapEvents(!bSolid);
	TrendoPaint(Mesh, Color);
}

ATrendoEnemy::ATrendoEnemy()
{
	PrimaryActorTick.bCanEverTick = true;
	GetCapsuleComponent()->InitCapsuleSize(38.f, 90.f);
	bUseControllerRotationYaw = false;
	UCharacterMovementComponent* M = GetCharacterMovement();
	M->bOrientRotationToMovement = true; M->RotationRate = FRotator(0, 320, 0); M->MaxWalkSpeed = 380.f; M->MaxStepHeight = 45.f;
	M->SetWalkableFloorAngle(50.f); M->bUseRVOAvoidance = true; M->AvoidanceConsiderationRadius = 300.f; M->BrakingDecelerationWalking = 900.f;
	AutoPossessAI = EAutoPossessAI::PlacedInWorldOrSpawned; AIControllerClass = AAIController::StaticClass();
	// Grundform (Rückfall ohne Menschmodell) und Fühler: Berührt die Spielfigur den Gegner, merkt das Spiel es (Kapsel blockt, Fühler überlappt)
	Shape = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Form"));
	Shape->SetupAttachment(GetCapsuleComponent());
	Shape->SetStaticMesh(TrendoShape(TEXT("/Engine/BasicShapes/Cube.Cube")));
	Shape->SetRelativeScale3D(FVector(0.9f, 0.9f, 1.9f));
	Shape->SetCollisionProfileName(TEXT("OverlapAllDynamic")); Shape->SetGenerateOverlapEvents(true);
	Figure = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("Figur"));
	Figure->SetupAttachment(GetCapsuleComponent()); Figure->SetVisibility(false); Figure->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	GetMesh()->SetVisibility(false);
}

void ATrendoEnemy::BeginPlay() { Super::BeginPlay(); Start = GetActorLocation(); }

void ATrendoEnemy::Init(int32 InHp, float Ground)
{
	MaxHp = Hp = InHp;
	const float K = MaxHp > 1 ? 1.25f : 1.f; // Boss größer – Kapsel, Körper und Fühler wachsen mit
	SetActorScale3D(FVector(K));
	Body3D = TrendoSetupMetaHuman(this, GetCapsuleComponent(), -90.f, TEXT("Gegner"), K);
	if (!Body3D && TrendoSetupFigure(Figure, -90.f, MaxHp > 1 ? FLinearColor(0.16f, 0.05f, 0.04f) : FLinearColor(0.38f, 0.14f, 0.1f))) Body3D = Figure;
	bModel = Body3D != nullptr;
	if (bModel) { Shape->SetHiddenInGame(true); Shape->SetCastShadow(false); }
	else TrendoPaint(Shape, MaxHp > 1 ? FLinearColor(0.6f, 0.05f, 0.3f) : FLinearColor(0.8f, 0.2f, 0.6f));
	// Füße auf den Boden: Kapselmitte 90 cm (mal Größe) darüber, dann übernimmt die Schwerkraft
	Start = FVector(GetActorLocation().X, GetActorLocation().Y, Ground + 92.f * K);
	SetActorLocation(Start, false, nullptr, ETeleportType::TeleportPhysics);
}

void ATrendoEnemy::WalkTo(const FVector& Goal, float Speed, float Dt)
{
	FVector Dir = Goal - GetActorLocation(); Dir.Z = 0;
	if (Dir.Size() < 40.f || Speed <= 0.f) return;
	Dir.Normalize();
	// Hindernis (Baum, Fels, Kiste): kommt er trotz Gehen nicht voran, weicht er eine Weile seitlich aus
	if (Detour > 0) { Detour -= Dt; Dir = Dir.RotateAngleAxis(65.f * DetourSign, FVector::UpVector); }
	else if (GetVelocity().Size2D() < Speed * 0.25f) { if ((Stuck += Dt) > 0.6f) { Stuck = 0; Detour = 1.2f; DetourSign = FMath::RandBool() ? 1.f : -1.f; } }
	else Stuck = 0;
	GetCharacterMovement()->MaxWalkSpeed = Speed;
	AddMovementInput(Dir, 1.f);
}

void ATrendoEnemy::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (bDead) return;
	TrendoAnimate(Body3D, GetVelocity(), GetCharacterMovement()->IsFalling()); // Bewegung aus der echten Geschwindigkeit: Füße passen zum Boden
	if (bFrozen) return;
	T += DeltaSeconds;
	ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	if (GM && GM->Data.World == TEXT("openworld")) { ThinkOpenWorld(DeltaSeconds, GM); return; }
	// Level-Spiel: hin und her gehen
	WalkTo(Start + FVector(FMath::Sin(T * 0.8f) > 0 ? 160.f : -160.f, 0, 0), 200.f, DeltaSeconds);
}

void ATrendoEnemy::Knock(const FVector& Push)
{
	LaunchCharacter(Push * 3.f + FVector(0, 0, 160), true, true); Stun = 0.6f; Alert = 4.f; // Treffer stößt ihn physikalisch zurück
}

bool ATrendoEnemy::Ragdoll(const FVector& Impulse)
{
	if (!Body3D) return false;
	bDead = true; GetCharacterMovement()->DisableMovement();
	// Echte Sturz-Animation (auf das eigene Skelett übertragen): fällt glaubwürdig zu Boden und bleibt liegen
	// Standard: echter Physik-Sturz (Ragdoll mit der Physik-Hülle des Engine-Mannequins); fehlt sie, fällt er mit echter Sturz-Animation
	UPhysicsAsset* PA = LoadObject<UPhysicsAsset>(nullptr, TEXT("/MoverExamples/Characters/Mannequins/Rigs/PA_Mannequin.PA_Mannequin"));
	if (UAnimationAsset* Death = PA && !FParse::Param(FCommandLine::Get(), TEXT("trendosturzanimation")) ? nullptr : Anim(TEXT("MM_Death_Front_03_ExtractedRM"), MetaDirOf(Body3D)))
	{
		SetActorRotation(FRotator(0, Impulse.Rotation().Yaw + 180.f, 0)); // fällt in Schlagrichtung nach vorn weg
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision); Shape->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Body3D->PlayAnimation(Death, false); return true;
	}
	GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision); Shape->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	if (AActor* Part = Body3D->GetOwner(); Part && Part != this) { Part->DetachFromActor(FDetachmentTransformRules::KeepWorldTransform); Part->SetActorEnableCollision(true); } // MetaHuman fällt frei – Kollision des Teils wieder an (war für die Kapsel aus)
	Body3D->Stop(); Body3D->SetAnimationMode(EAnimationMode::AnimationCustomMode);
	// Physik-Hülle des Engine-Mannequins (gleiche Körperknochen wie MetaHuman, Kollision mit Gelände und Objekten erprobt)
	if (PA) Body3D->SetPhysicsAsset(PA, true); else if (!Body3D->GetPhysicsAsset()) return false;
	Body3D->SetCollisionObjectType(ECC_PhysicsBody); Body3D->SetCollisionResponseToAllChannels(ECR_Block); Body3D->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore); Body3D->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	Body3D->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
	Body3D->SetAllUseCCD(true); // schnelle Körperteile rutschen nicht durchs Gelände
	Body3D->SetAllBodiesSimulatePhysics(true); Body3D->SetSimulatePhysics(true); Body3D->WakeAllRigidBodies(); Body3D->bBlendPhysics = true;
	Body3D->AddImpulse((Impulse.GetSafeNormal() + FVector(0, 0, 0.15f)) * 260.f, TEXT("spine_03"), true); // Stoß in Schlagrichtung (Geschwindigkeit), Körper kippt
	return true;
}

bool ATrendoEnemy::Sever(const FVector& Push)
{
	const FString Dir = MetaDirOf(Body3D); if (Dir.IsEmpty() || !Body3D->GetOwner()) return false;
	static const TCHAR* Limbs[] = { TEXT("lowerarm_l"), TEXT("lowerarm_r"), TEXT("calf_l"), TEXT("calf_r") };
	const FString Bone = Limbs[FMath::RandRange(0, 3)];
	auto Piece = [&](const TCHAR* Part) { const FString N = FString::Printf(TEXT("Glied_%s_%s"), *Bone, Part); return LoadObject<UStaticMesh>(nullptr, *FString::Printf(TEXT("%sGlieder/%s.%s"), *Dir, *N, *N)); };
	UStaticMesh* Flesh = Piece(TEXT("Koerper")); if (!Flesh) return false;
	UStaticMesh* Cloth = Piece(TEXT("Kleidung"));
	const FTransform At = Body3D->GetSocketTransform(FName(*Bone), RTS_World);
	// am Körper (und an Kleidung, Gesicht) ausblenden: das Glied ist ab hier weg
	TArray<USkeletalMeshComponent*> Parts; Body3D->GetOwner()->GetComponents(Parts);
	for (USkeletalMeshComponent* P : Parts) if (P->GetBoneIndex(FName(*Bone)) != INDEX_NONE) P->HideBoneByName(FName(*Bone), EPhysBodyOp::PBO_Term);
	// ausgeschnittenes Stück genau an der Stelle des Knochens, fliegt in Schlagrichtung weg
	FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	AStaticMeshActor* A = GetWorld()->SpawnActor<AStaticMeshActor>(At.GetLocation(), At.Rotator(), SP); if (!A) return false;
	UStaticMeshComponent* M = A->GetStaticMeshComponent(); M->SetMobility(EComponentMobility::Movable); M->SetStaticMesh(Flesh); A->SetActorScale3D(At.GetScale3D());
	if (Cloth) { UStaticMeshComponent* C = NewObject<UStaticMeshComponent>(A, TEXT("Kleidung")); C->SetStaticMesh(Cloth); C->SetCollisionEnabled(ECollisionEnabled::NoCollision); C->SetupAttachment(M); C->RegisterComponent(); }
	M->SetCollisionProfileName(TEXT("PhysicsActor")); M->SetUseCCD(true); M->SetSimulatePhysics(true);
	M->AddImpulse(Push.GetSafeNormal() * 240.f + FVector(0, 0, 190.f), NAME_None, true); // fliegt kurz und landet in der Nähe
	M->AddAngularImpulseInDegrees(FVector(FMath::FRandRange(-600.f, 600.f), FMath::FRandRange(-600.f, 600.f), FMath::FRandRange(-300.f, 300.f)), NAME_None, true);
	A->SetLifeSpan(90.f);
	M->SetReceivesDecals(false); if (Cloth) for (UActorComponent* C : A->GetComponents()) if (UPrimitiveComponent* Pc = Cast<UPrimitiveComponent>(C)) Pc->SetReceivesDecals(false);
	SeveredBone = Body3D->GetParentBone(FName(*Bone)); // Blut fließt am Stumpf auf den Boden (Spielmodus legt die Lache)
	UE_LOG(LogTemp, Display, TEXT("TRENDO-GLIED abgetrennt=%s Kleidung=%s"), *Bone, Cloth ? TEXT("ja") : TEXT("nein"));
	return true;
}

/** Gegner-KI in der Open World: Streife um den Startpunkt, sieht die Spielfigur (Sichtweite, Blickfeld, freie Sicht), verfolgt sie, greift aus der Nähe an,
 *  sucht sie nach Sichtverlust noch eine Weile am letzten Ort; ein angeschlagener Boss weicht zurück. Laufen, Boden und Kollision über die Charakter-Bewegung. */
void ATrendoEnemy::ThinkOpenWorld(float Dt, ATrendoGameMode* GM)
{
	const FVector Me = GetActorLocation();
	const float K = MaxHp > 1 ? 1.25f : 1.f;
	if (Stun > 0) { Stun -= Dt; return; } // benommen nach einem Treffer
	FVector Goal = Start + FVector(FMath::Sin(T * 0.35f) * 450.f, FMath::Sin(T * 0.23f + 1.3f) * 300.f, 0); float Speed = 150.f; // Streife, gemächlich
	Attack = FMath::Max(0.f, Attack - Dt);
	if (APawn* H = UGameplayStatics::GetPlayerPawn(this, 0); H && GM->Playing() && !H->IsHidden())
	{
		const FVector To = H->GetActorLocation() - Me; const float Dist = To.Size2D();
		const bool bInView = Dist < 2600.f && (Dist < 600.f || FVector::DotProduct(GetActorForwardVector(), To.GetSafeNormal2D()) > 0.3f); // ~145° Blickfeld, aus der Nähe hört er
		bool bSees = false;
		if (bInView)
		{
			FHitResult Hit; FCollisionQueryParams Q(TEXT("TrendoSicht"), false, this);
			TArray<AActor*> Kids; GetAllChildActors(Kids); Q.AddIgnoredActors(Kids); // eigener Körper versperrt nicht die Sicht
			bSees = !GetWorld()->LineTraceSingleByChannel(Hit, Me + FVector(0, 0, 70.f * K), H->GetActorLocation() + FVector(0, 0, 60), ECC_Visibility, Q) || Hit.GetActor() == H;
		}
		if (bSees) { Alert = 4.f; LastSeen = H->GetActorLocation(); } else Alert = FMath::Max(0.f, Alert - Dt);
		if (Alert > 0)
		{
			const bool bFlee = MaxHp > 1 && Hp == 1; // angeschlagener Boss weicht zurück
			if (bFlee) { Goal = Me - To.GetSafeNormal2D() * 800.f; Speed = 420.f; }
			else
			{
				Goal = bSees ? H->GetActorLocation() : LastSeen; Speed = Dist < 170.f ? 0.f : 380.f * (MaxHp > 1 ? 0.85f : 1.f);
				if (Dist < 170.f) SetActorRotation(FMath::RInterpTo(GetActorRotation(), FRotator(0, To.Rotation().Yaw, 0), Dt, 8.f)); // dem Gegner zuwenden
				if (bSees && Dist < 190.f && Attack <= 0.f) { Attack = 1.4f; GM->Hit(); } // Schlag aus der Nähe, dann kurz Pause
			}
		}
	}
	WalkTo(Goal, Speed, Dt);
}

ATrendoShot::ATrendoShot()
{
	PrimaryActorTick.bCanEverTick = true;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Form"));
	RootComponent = Mesh;
	Mesh->SetStaticMesh(TrendoShape(TEXT("/Engine/BasicShapes/Sphere.Sphere")));
	Mesh->SetWorldScale3D(FVector(0.2f));
	Mesh->SetCollisionProfileName(TEXT("NoCollision"));
}

void ATrendoShot::BeginPlay()
{
	Super::BeginPlay();
	if (UStaticMesh* Axe = TrendoModel(this, TEXT("wurf"))) { Mesh->SetStaticMesh(Axe); Mesh->SetWorldScale3D(FVector(1.f)); Mesh->SetCastShadow(true); }
}

void ATrendoShot::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Life -= DeltaSeconds;
	const FVector From = GetActorLocation(), To = From + Dir * 1500.f * DeltaSeconds;
	FHitResult Hit; FCollisionQueryParams Q; Q.AddIgnoredActor(this);
	if (APawn* P = UGameplayStatics::GetPlayerPawn(this, 0)) Q.AddIgnoredActor(P);
	ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	if (GM)
		for (ATrendoEnemy* E : GM->Enemies)
			if (IsValid(E) && E->Hp > 0 && FMath::PointDistToSegment(E->GetActorLocation(), From, To) < 70.f) { GM->Damage(E); Destroy(); return; }
	if (GetWorld()->LineTraceSingleByChannel(Hit, From, To, ECC_Visibility, Q) || Life <= 0) { Destroy(); return; }
	SetActorLocation(To);
	SetActorRotation(FRotator(-Life * 900.f, Dir.Rotation().Yaw, 0)); // geworfenes Beil überschlägt sich
}
""",
        ["TrendoCar.h"] = """
// Von Trendo erzeugt: fahrbarer Wagen mit Chaos-Fahrphysik (Motor, Automatik, Federung, Reifenreibung, Lenkung) – Modell und Rad-Animation aus Epics Fahrzeug-Beispiel.
#pragma once

#include "CoreMinimal.h"
#include "WheeledVehiclePawn.h"
#include "ChaosVehicleWheel.h"
#include "InputActionValue.h"
#include "TrendoCar.generated.h"

class USpringArmComponent; class UCameraComponent; class UInputAction; class UInputMappingContext;

/** Vorderrad: lenkt, bremst. */
UCLASS()
class UTrendoWheelFront : public UChaosVehicleWheel
{
	GENERATED_BODY()
public:
	UTrendoWheelFront();
};

/** Hinterrad: angetrieben, Handbremse. */
UCLASS()
class UTrendoWheelRear : public UChaosVehicleWheel
{
	GENERATED_BODY()
public:
	UTrendoWheelRear();
};

/** Sportwagen: W/S bzw. rechter/linker Trigger Gas und Bremse (Bremse im Stand = Rückwärts), A/D bzw. linker Stick lenken, Leertaste/A Handbremse, E/B aussteigen. */
UCLASS()
class ATrendoCar : public AWheeledVehiclePawn
{
	GENERATED_BODY()
public:
	ATrendoCar();
	UPROPERTY(VisibleAnywhere) USpringArmComponent* Arm;
	UPROPERTY(VisibleAnywhere) UCameraComponent* Camera;
	/** true, wenn Modell, Physik-Hülle und Rad-Animation gefunden wurden (sonst nicht fahrbar). */
	bool bReady = false;
	/** Gas/Bremse/Lenkung direkt setzen (für den Prüflauf und die KI). */
	void Drive(float Throttle, float Steer, float Brake);
	/** Größte Raddrehung (Grad) aller Räder seit Fahrtbeginn. */
	float WheelSpin() const;
	virtual void BeginPlay() override;
	virtual void SetupPlayerInputComponent(UInputComponent* Input) override;
private:
	UPROPERTY() UInputMappingContext* Mapping;
	UPROPERTY() UInputAction* ThrottleAction;
	UPROPERTY() UInputAction* BrakeAction;
	UPROPERTY() UInputAction* SteerAction;
	UPROPERTY() UInputAction* HandbrakeAction;
	UPROPERTY() UInputAction* ExitAction;
	void BuildInput();
	void Throttle(const FInputActionValue& V);
	void Brake(const FInputActionValue& V);
	void Steer(const FInputActionValue& V);
	void Handbrake(const FInputActionValue& V);
	void Exit();
};
""",
        ["TrendoCar.cpp"] = """
// Von Trendo erzeugt: fahrbarer Wagen (Chaos Vehicles) – Federung, Reifen, Motor mit Drehmomentkurve, Automatikgetriebe, sich drehende Räder.
#include "TrendoCar.h"
#include "TrendoGameMode.h"
#include "ChaosWheeledVehicleMovementComponent.h"
#include "Camera/CameraComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/SkeletalMesh.h"
#include "PhysicsEngine/PhysicsAsset.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "Kismet/GameplayStatics.h"

static const TCHAR* CarDir = TEXT("/ChaosModularVehicleExamples/Models/SportsCar/");

UTrendoWheelFront::UTrendoWheelFront()
{
	WheelRadius = 36.f; WheelWidth = 24.f; bAffectedBySteering = true; MaxSteerAngle = 38.f;
	bAffectedByHandbrake = false; bAffectedByEngine = false; FrictionForceMultiplier = 3.f; CorneringStiffness = 1000.f;
	SuspensionMaxRaise = 8.f; SuspensionMaxDrop = 12.f; SpringRate = 250.f; SpringPreload = 50.f; SuspensionDampingRatio = 0.5f;
	MaxBrakeTorque = 3000.f;
}

UTrendoWheelRear::UTrendoWheelRear()
{
	WheelRadius = 36.f; WheelWidth = 28.f; bAffectedBySteering = false; bAffectedByHandbrake = true; bAffectedByEngine = true;
	FrictionForceMultiplier = 3.f; CorneringStiffness = 1000.f; MaxHandBrakeTorque = 6000.f; MaxBrakeTorque = 3000.f;
	SuspensionMaxRaise = 8.f; SuspensionMaxDrop = 12.f; SpringRate = 250.f; SpringPreload = 50.f; SuspensionDampingRatio = 0.5f;
}

ATrendoCar::ATrendoCar()
{
	PrimaryActorTick.bCanEverTick = true;
	UChaosWheeledVehicleMovementComponent* M = CastChecked<UChaosWheeledVehicleMovementComponent>(GetVehicleMovementComponent());
	M->ChassisHeight = 140.f; M->DragCoefficient = 0.31f; M->DownforceCoefficient = 0.3f; M->Mass = 1450.f; // Sportwagen um 1,45 t
	M->WheelSetups.SetNum(4);
	const TCHAR* Bones[4] = { TEXT("Phys_Wheel_FL"), TEXT("Phys_Wheel_FR"), TEXT("Phys_Wheel_BL"), TEXT("Phys_Wheel_BR") };
	for (int32 i = 0; i < 4; i++) { M->WheelSetups[i].BoneName = Bones[i]; M->WheelSetups[i].WheelClass = i < 2 ? UTrendoWheelFront::StaticClass() : UTrendoWheelRear::StaticClass(); }
	// Motor: Drehmomentkurve über die Drehzahl, Automatik mit sechs Gängen
	M->EngineSetup.MaxTorque = 750.f; M->EngineSetup.MaxRPM = 7000.f; M->EngineSetup.EngineIdleRPM = 900.f; M->EngineSetup.EngineBrakeEffect = 0.2f;
	M->EngineSetup.TorqueCurve.GetRichCurve()->Reset();
	M->EngineSetup.TorqueCurve.GetRichCurve()->AddKey(0.f, 400.f); M->EngineSetup.TorqueCurve.GetRichCurve()->AddKey(1890.f, 500.f);
	M->EngineSetup.TorqueCurve.GetRichCurve()->AddKey(5730.f, 750.f); M->EngineSetup.TorqueCurve.GetRichCurve()->AddKey(7000.f, 600.f);
	M->TransmissionSetup.bUseAutomaticGears = true; M->TransmissionSetup.bUseAutoReverse = true; M->TransmissionSetup.FinalRatio = 3.7f;
	M->TransmissionSetup.ForwardGearRatios = { 4.25f, 2.52f, 1.66f, 1.22f, 1.0f, 0.86f };
	M->TransmissionSetup.ReverseGearRatios = { 4.04f };
	M->TransmissionSetup.ChangeUpRPM = 6000.f; M->TransmissionSetup.ChangeDownRPM = 2000.f; M->TransmissionSetup.GearChangeTime = 0.2f;
	M->DifferentialSetup.DifferentialType = EVehicleDifferential::RearWheelDrive;
	M->SteeringSetup.SteeringType = ESteeringType::AngleRatio; M->SteeringSetup.AngleRatio = 0.7f;
	GetMesh()->SetCollisionProfileName(TEXT("Vehicle")); GetMesh()->BodyInstance.bSimulatePhysics = true;
	Arm = CreateDefaultSubobject<USpringArmComponent>(TEXT("Kameraarm"));
	Arm->SetupAttachment(GetMesh()); Arm->TargetArmLength = 650.f; Arm->SocketOffset = FVector(0, 0, 180); Arm->bEnableCameraLag = true; Arm->CameraLagSpeed = 6.f;
	Arm->bEnableCameraRotationLag = true; Arm->CameraRotationLagSpeed = 4.f; Arm->bInheritPitch = false; Arm->bInheritRoll = false;
	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Kamera")); Camera->SetupAttachment(Arm); Camera->SetRelativeRotation(FRotator(-10, 0, 0));
}

void ATrendoCar::BeginPlay()
{
	// Modell, Physik-Hülle (Räder als Kugeln) und Rad-Animation (dreht und federt die Räder) aus Epics Fahrzeug-Beispiel
	USkeletalMesh* CarModel = LoadObject<USkeletalMesh>(nullptr, *FString::Printf(TEXT("%sSKM_SportsCar.SKM_SportsCar"), CarDir));
	UPhysicsAsset* PA = LoadObject<UPhysicsAsset>(nullptr, *FString::Printf(TEXT("%sSportsCar_PhysicsAsset.SportsCar_PhysicsAsset"), CarDir));
	UClass* Anim = LoadClass<UAnimInstance>(nullptr, *FString::Printf(TEXT("%sSportsCar_AnimBP.SportsCar_AnimBP_C"), CarDir));
	if (CarModel && PA)
	{
		GetMesh()->SetSkeletalMesh(CarModel); GetMesh()->SetPhysicsAsset(PA, true);
		if (Anim) GetMesh()->SetAnimInstanceClass(Anim);
		GetMesh()->SetSimulatePhysics(true);
		GetVehicleMovementComponent()->RecreatePhysicsState();
		bReady = true;
	}
	Super::BeginPlay();
}

void ATrendoCar::Drive(float Throttle, float Steer, float Brake)
{
	UChaosVehicleMovementComponent* M = GetVehicleMovementComponent(); if (!M) return;
	M->SetThrottleInput(Throttle); M->SetSteeringInput(Steer); M->SetBrakeInput(Brake); M->SetHandbrakeInput(false);
}

float ATrendoCar::WheelSpin() const
{
	float Max = 0.f;
	if (const UChaosWheeledVehicleMovementComponent* M = Cast<UChaosWheeledVehicleMovementComponent>(GetVehicleMovementComponent()))
		for (const UChaosVehicleWheel* W : M->Wheels) if (W) Max = FMath::Max(Max, FMath::Abs(W->GetRotationAngle()));
	return Max;
}

static UInputAction* CarAction(UObject* Outer, const TCHAR* Name, EInputActionValueType Type) { UInputAction* A = NewObject<UInputAction>(Outer, Name); A->ValueType = Type; return A; }

void ATrendoCar::BuildInput()
{
	if (Mapping) return;
	Mapping = NewObject<UInputMappingContext>(this, TEXT("TrendoWagen"));
	ThrottleAction = CarAction(this, TEXT("Gas"), EInputActionValueType::Axis1D); BrakeAction = CarAction(this, TEXT("Bremse"), EInputActionValueType::Axis1D);
	SteerAction = CarAction(this, TEXT("Lenken"), EInputActionValueType::Axis1D); HandbrakeAction = CarAction(this, TEXT("Handbremse"), EInputActionValueType::Boolean);
	ExitAction = CarAction(this, TEXT("Aussteigen"), EInputActionValueType::Boolean);
	for (const FKey& K : { EKeys::W, EKeys::Up, EKeys::Gamepad_RightTriggerAxis }) Mapping->MapKey(ThrottleAction, K);
	for (const FKey& K : { EKeys::S, EKeys::Down, EKeys::Gamepad_LeftTriggerAxis }) Mapping->MapKey(BrakeAction, K);
	for (const FKey& K : { EKeys::D, EKeys::Right, EKeys::Gamepad_LeftX }) Mapping->MapKey(SteerAction, K);
	for (const FKey& K : { EKeys::A, EKeys::Left }) Mapping->MapKey(SteerAction, K).Modifiers.Add(NewObject<UInputModifierNegate>(Mapping));
	for (const FKey& K : { EKeys::SpaceBar, EKeys::Gamepad_FaceButton_Bottom }) Mapping->MapKey(HandbrakeAction, K);
	for (const FKey& K : { EKeys::E, EKeys::Gamepad_FaceButton_Right }) Mapping->MapKey(ExitAction, K);
}

void ATrendoCar::SetupPlayerInputComponent(UInputComponent* Input)
{
	Super::SetupPlayerInputComponent(Input);
	BuildInput();
	if (APlayerController* PC = Cast<APlayerController>(GetController()))
		if (auto* Sub = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(PC->GetLocalPlayer())) Sub->AddMappingContext(Mapping, 1);
	if (UEnhancedInputComponent* EI = Cast<UEnhancedInputComponent>(Input))
	{
		EI->BindAction(ThrottleAction, ETriggerEvent::Triggered, this, &ATrendoCar::Throttle); EI->BindAction(ThrottleAction, ETriggerEvent::Completed, this, &ATrendoCar::Throttle);
		EI->BindAction(BrakeAction, ETriggerEvent::Triggered, this, &ATrendoCar::Brake); EI->BindAction(BrakeAction, ETriggerEvent::Completed, this, &ATrendoCar::Brake);
		EI->BindAction(SteerAction, ETriggerEvent::Triggered, this, &ATrendoCar::Steer); EI->BindAction(SteerAction, ETriggerEvent::Completed, this, &ATrendoCar::Steer);
		EI->BindAction(HandbrakeAction, ETriggerEvent::Started, this, &ATrendoCar::Handbrake); EI->BindAction(HandbrakeAction, ETriggerEvent::Completed, this, &ATrendoCar::Handbrake);
		EI->BindAction(ExitAction, ETriggerEvent::Started, this, &ATrendoCar::Exit);
	}
}

void ATrendoCar::Throttle(const FInputActionValue& V) { GetVehicleMovementComponent()->SetThrottleInput(V.Get<float>()); }
void ATrendoCar::Brake(const FInputActionValue& V) { GetVehicleMovementComponent()->SetBrakeInput(V.Get<float>()); }
void ATrendoCar::Steer(const FInputActionValue& V) { GetVehicleMovementComponent()->SetSteeringInput(V.Get<float>()); }
void ATrendoCar::Handbrake(const FInputActionValue& V) { GetVehicleMovementComponent()->SetHandbrakeInput(V.Get<bool>()); }
void ATrendoCar::Exit() { if (ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this))) GM->ExitCar(); }
""",
        ["TrendoCharacter.h"] = """
// Von Trendo erzeugt: Spielfigur mit Kamera (dritte Person).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "InputActionValue.h"
#include "TrendoCharacter.generated.h"

class USpringArmComponent; class UCameraComponent; class UStaticMeshComponent; class USkeletalMeshComponent; class UInputAction; class UInputMappingContext;

/** Spielfigur: WASD/Pfeile laufen, Leertaste springt (Doppelsprung), Umschalt sprintet, X/J/linke Maustaste greift an, E steigt aus, Esc pausiert. Controller ebenso. */
UCLASS()
class ATrendoCharacter : public ACharacter
{
	GENERATED_BODY()
public:
	ATrendoCharacter();
	UPROPERTY(VisibleAnywhere) USpringArmComponent* Arm;
	UPROPERTY(VisibleAnywhere) UCameraComponent* Camera;
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Body;
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* VehicleMesh;
	/** Sportwagen (echtes Modell), wenn vorhanden; sonst die Form VehicleMesh. */
	UPROPERTY(VisibleAnywhere) USkeletalMeshComponent* CarMesh;
	/** Waffe in der rechten Hand (Säbel bzw. Beil). */
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Weapon;
	/** true, wenn die Figur das Menschmodell mit Skelett und Animationen nutzt. */
	bool bHuman = false;
	/** Animierter Körper: MetaHuman, sonst die Gliederpuppe (GetMesh). */
	UPROPERTY() USkeletalMeshComponent* Figure = nullptr;
	/** Waffe einmalig in der Faust ausgerichtet (nach der ersten Ruhepose). */
	bool bWeaponSet = false;
	FVector Checkpoint; FString Vehicle; float Fuel = 0, Safe = 0, NoEnter = 0, Cool = 0; bool bSprint = false;

	/** Tempo mit Sprint (Fähigkeit) und Fahrzeug, in cm/s. */
	float WalkSpeed() const;
	void ApplyAbilities();
	void Respawn();
	void SetVehicle(const FString& Kind);

	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
	virtual void SetupPlayerInputComponent(UInputComponent* Input) override;
	virtual void NotifyActorBeginOverlap(AActor* Other) override;

private:
	UPROPERTY() UInputMappingContext* Mapping;
	UPROPERTY() UInputAction* MoveAction;
	UPROPERTY() UInputAction* LookAction;
	UPROPERTY() UInputAction* JumpAction;
	UPROPERTY() UInputAction* SprintAction;
	UPROPERTY() UInputAction* AttackAction;
	UPROPERTY() UInputAction* UseAction;
	UPROPERTY() UInputAction* PauseAction;
	UPROPERTY() UInputAction* UpAction;
	UPROPERTY() UInputAction* DownAction;
	UPROPERTY() UInputAction* EnterAction;
	void BuildInput();
	void Move(const FInputActionValue& V);
	void Look(const FInputActionValue& V);
	void JumpPressed();
	void JumpReleased();
	void SprintOn() { bSprint = true; ApplyAbilities(); }
	void SprintOff() { bSprint = false; ApplyAbilities(); }
	void Attack();
	void Use();
	void PausePressed();
	void MenuUp();
	void MenuDown();
	void MenuEnter();
	bool bThrust = false;
};
""",
        ["TrendoCharacter.cpp"] = """
// Von Trendo erzeugt: Spielfigur mit Kamera, Steuerung über Enhanced Input (im Code angelegt, keine Assets nötig).
#include "TrendoCharacter.h"
#include "TrendoGameMode.h"
#include "TrendoActors.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/SkeletalMesh.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "Kismet/GameplayStatics.h"

ATrendoCharacter::ATrendoCharacter()
{
	PrimaryActorTick.bCanEverTick = true;
	GetCapsuleComponent()->InitCapsuleSize(40.f, 90.f);
	bUseControllerRotationYaw = false;
	GetCharacterMovement()->bOrientRotationToMovement = true;
	GetCharacterMovement()->RotationRate = FRotator(0, 720, 0);
	GetCharacterMovement()->JumpZVelocity = 650.f;
	GetCharacterMovement()->AirControl = 0.6f;
	Arm = CreateDefaultSubobject<USpringArmComponent>(TEXT("Kameraarm"));
	Arm->SetupAttachment(RootComponent); Arm->TargetArmLength = 520.f; Arm->bUsePawnControlRotation = true; Arm->SocketOffset = FVector(0, 0, 80);
	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Kamera"));
	Camera->SetupAttachment(Arm);
	Body = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Koerper"));
	Body->SetupAttachment(RootComponent); Body->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Body->SetStaticMesh(LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cylinder.Cylinder")));
	Body->SetRelativeScale3D(FVector(0.7f, 0.7f, 1.8f));
	VehicleMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Fahrzeug"));
	VehicleMesh->SetupAttachment(RootComponent); VehicleMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	VehicleMesh->SetStaticMesh(LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cube.Cube")));
	VehicleMesh->SetRelativeLocation(FVector(0, 0, -70)); VehicleMesh->SetRelativeScale3D(FVector(1.6f, 1.0f, 0.4f)); VehicleMesh->SetVisibility(false);
	CarMesh = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("Wagen"));
	CarMesh->SetupAttachment(RootComponent); CarMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision); CarMesh->SetVisibility(false); CarMesh->SetRelativeLocation(FVector(0, 0, -90));
	Weapon = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Waffe"));
	Weapon->SetupAttachment(GetMesh()); Weapon->SetCollisionEnabled(ECollisionEnabled::NoCollision); Weapon->SetVisibility(false);
}

void ATrendoCharacter::BeginPlay()
{
	Super::BeginPlay();
	Checkpoint = GetActorLocation();
	// Echte Menschfigur (Skelett, Geh-/Lauf-/Sprung-Animationen), sonst Form
	// Bevorzugt ein echter Mensch (MetaHuman), sonst die Gliederpuppe
	Figure = TrendoSetupMetaHuman(this, RootComponent, -90.f);
	if (Figure) { bHuman = true; GetMesh()->SetVisibility(false); }
	else { bHuman = TrendoSetupFigure(GetMesh(), -90.f); Figure = GetMesh(); }
	UE_LOG(LogTemp, Display, TEXT("TRENDO-FIGUR MetaHuman=%s (%s) Mensch=%s"), Figure && Figure != GetMesh() ? TEXT("ja") : TEXT("nein"), Figure && Figure->GetOwner() ? *Figure->GetOwner()->GetClass()->GetName() : TEXT("-"), bHuman ? TEXT("ja") : TEXT("nein"));
	if (bHuman) Body->SetVisibility(false); else TrendoPaint(Body, FLinearColor(0.31f, 0.7f, 1.f));
	if (USkeletalMesh* Car = TrendoCar()) CarMesh->SetSkeletalMesh(Car);
	if (const ATrendoGameMode* G = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this)); bHuman && G)
		if (UStaticMesh* W = TrendoModel(this, G->Data.Weapon == TEXT("schwert") ? TEXT("waffe") : TEXT("wurf"))) { Weapon->SetStaticMesh(W); Weapon->AttachToComponent(Figure, FAttachmentTransformRules::SnapToTargetNotIncludingScale, TEXT("hand_r")); Weapon->SetVisibility(true); }
	TrendoPaint(VehicleMesh, FLinearColor(0.98f, 0.45f, 0.09f));
	ApplyAbilities();
	if (APlayerController* PC = Cast<APlayerController>(GetController()))
		if (auto* Sub = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(PC->GetLocalPlayer())) { BuildInput(); Sub->AddMappingContext(Mapping, 0); }
}

float ATrendoCharacter::WalkSpeed() const
{
	const ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	const float Base = 300.f + 40.f * (GM ? GM->Data.Speed : 5) + (GM ? GM->State.Boost * 100.f : 0);
	return Base * (bSprint && GM && GM->Data.Has(TEXT("sprint")) ? 1.6f : 1.f) * (Vehicle == TEXT("wagen") ? 1.8f : Vehicle == TEXT("ufo") ? 1.3f : 1.f);
}

void ATrendoCharacter::ApplyAbilities()
{
	const ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	JumpMaxCount = GM && GM->Data.Has(TEXT("doppelsprung")) ? 2 : 1;
	GetCharacterMovement()->MaxWalkSpeed = WalkSpeed();
}

void ATrendoCharacter::SetVehicle(const FString& Kind)
{
	Vehicle = Kind; Fuel = Kind == TEXT("ufo") ? 10.f : 0.f;
	const bool bCar = Kind == TEXT("wagen") && CarMesh->GetSkeletalMeshAsset();
	CarMesh->SetVisibility(bCar); VehicleMesh->SetVisibility(!Kind.IsEmpty() && !bCar);
	// Im Wagen sitzt die Figur unsichtbar am Steuer; der Wagen dreht träger als ein Mensch
	if (bHuman) { Figure->GetOwner()->SetActorHiddenInGame(!Kind.IsEmpty()); Figure->SetVisibility(Kind.IsEmpty(), true); Weapon->SetVisibility(Kind.IsEmpty() && Weapon->GetStaticMesh() != nullptr); }
	GetCharacterMovement()->RotationRate = FRotator(0, bCar ? 160 : 720, 0);
	ApplyAbilities();
}

void ATrendoCharacter::Respawn() { SetActorLocation(Checkpoint); GetCharacterMovement()->Velocity = FVector::ZeroVector; }

void ATrendoCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Safe = FMath::Max(0.f, Safe - DeltaSeconds); NoEnter = FMath::Max(0.f, NoEnter - DeltaSeconds); Cool = FMath::Max(0.f, Cool - DeltaSeconds);
	if (Vehicle == TEXT("ufo") && bThrust && Fuel > 0)
	{
		FVector V = GetCharacterMovement()->Velocity; V.Z = FMath::Min(V.Z + 2400.f * DeltaSeconds, 500.f); GetCharacterMovement()->Velocity = V;
		if (GetCharacterMovement()->IsMovingOnGround()) GetCharacterMovement()->SetMovementMode(MOVE_Falling);
		Fuel -= DeltaSeconds;
		if (Fuel <= 0) { SetVehicle(FString()); if (ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this))) GM->State.Note = TEXT("Treibstoff leer"); }
	}
	if (bHuman) TrendoAnimate(Figure, GetVelocity(), GetCharacterMovement()->IsFalling());
	if (bHuman && !bWeaponSet && Weapon->GetStaticMesh() && GetGameTimeSinceCreation() > 0.3f)
	{
		// Klinge (Modellachse Z) schräg nach vorn unten, Griff in der Faust; danach folgt sie der Hand bei jeder Bewegung
		bWeaponSet = true;
		const FTransform Hand = Figure->GetSocketTransform(TEXT("hand_r"));
		const FVector Dir = (GetActorForwardVector() * 0.8f - FVector::UpVector * 0.6f).GetSafeNormal();
		Weapon->SetWorldLocationAndRotation(Hand.GetLocation() - FVector::UpVector * 8.f + GetActorForwardVector() * 2.f, FRotationMatrix::MakeFromZX(Dir, GetActorRightVector()).Rotator());
	}
	if (GetActorLocation().Z < -5000.f) if (ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this))) GM->Hit(true);
}

static UInputAction* MakeAction(UObject* Outer, const TCHAR* Name, EInputActionValueType Type, bool bWhenPaused = false)
{
	UInputAction* A = NewObject<UInputAction>(Outer, Name); A->ValueType = Type; A->bTriggerWhenPaused = bWhenPaused; return A;
}

void ATrendoCharacter::BuildInput()
{
	if (Mapping) return;
	Mapping = NewObject<UInputMappingContext>(this, TEXT("TrendoSteuerung"));
	MoveAction = MakeAction(this, TEXT("Laufen"), EInputActionValueType::Axis2D);
	LookAction = MakeAction(this, TEXT("Umsehen"), EInputActionValueType::Axis2D);
	JumpAction = MakeAction(this, TEXT("Springen"), EInputActionValueType::Boolean);
	SprintAction = MakeAction(this, TEXT("Sprinten"), EInputActionValueType::Boolean);
	AttackAction = MakeAction(this, TEXT("Angreifen"), EInputActionValueType::Boolean);
	UseAction = MakeAction(this, TEXT("Aussteigen"), EInputActionValueType::Boolean);
	PauseAction = MakeAction(this, TEXT("Pause"), EInputActionValueType::Boolean, true);
	UpAction = MakeAction(this, TEXT("MenueHoch"), EInputActionValueType::Boolean, true);
	DownAction = MakeAction(this, TEXT("MenueRunter"), EInputActionValueType::Boolean, true);
	EnterAction = MakeAction(this, TEXT("MenueWaehlen"), EInputActionValueType::Boolean, true);
	auto Map2D = [this](const FKey& Key, bool bSwizzle, bool bNegate)
	{
		FEnhancedActionKeyMapping& M = Mapping->MapKey(MoveAction, Key);
		if (bSwizzle) M.Modifiers.Add(NewObject<UInputModifierSwizzleAxis>(Mapping));
		if (bNegate) M.Modifiers.Add(NewObject<UInputModifierNegate>(Mapping));
	};
	Map2D(EKeys::W, true, false); Map2D(EKeys::S, true, true); Map2D(EKeys::D, false, false); Map2D(EKeys::A, false, true);
	Map2D(EKeys::Up, true, false); Map2D(EKeys::Down, true, true); Map2D(EKeys::Right, false, false); Map2D(EKeys::Left, false, true);
	// Controller-Sticks mit Totzone: ein leicht driftender Stick bewegt weder Figur noch Kamera
	auto DeadZone = [this]() { UInputModifierDeadZone* Z = NewObject<UInputModifierDeadZone>(Mapping); Z->Type = EDeadZoneType::Radial; Z->LowerThreshold = 0.25f; Z->UpperThreshold = 1.f; return Z; };
	Mapping->MapKey(MoveAction, EKeys::Gamepad_Left2D).Modifiers.Add(DeadZone());
	Mapping->MapKey(LookAction, EKeys::Mouse2D); Mapping->MapKey(LookAction, EKeys::Gamepad_Right2D).Modifiers.Add(DeadZone());
	for (const FKey& K : { EKeys::SpaceBar, EKeys::Gamepad_FaceButton_Bottom }) Mapping->MapKey(JumpAction, K);
	for (const FKey& K : { EKeys::LeftShift, EKeys::RightShift, EKeys::Gamepad_LeftShoulder }) Mapping->MapKey(SprintAction, K);
	for (const FKey& K : { EKeys::X, EKeys::J, EKeys::LeftMouseButton, EKeys::Gamepad_FaceButton_Left }) Mapping->MapKey(AttackAction, K);
	for (const FKey& K : { EKeys::E, EKeys::Gamepad_FaceButton_Right }) Mapping->MapKey(UseAction, K);
	for (const FKey& K : { EKeys::Escape, EKeys::P, EKeys::Gamepad_Special_Right }) Mapping->MapKey(PauseAction, K);
	for (const FKey& K : { EKeys::Up, EKeys::W, EKeys::Gamepad_DPad_Up }) Mapping->MapKey(UpAction, K);
	for (const FKey& K : { EKeys::Down, EKeys::S, EKeys::Gamepad_DPad_Down }) Mapping->MapKey(DownAction, K);
	for (const FKey& K : { EKeys::Enter, EKeys::SpaceBar, EKeys::Gamepad_FaceButton_Bottom }) Mapping->MapKey(EnterAction, K);
}

void ATrendoCharacter::SetupPlayerInputComponent(UInputComponent* Input)
{
	Super::SetupPlayerInputComponent(Input);
	BuildInput();
	if (UEnhancedInputComponent* EI = Cast<UEnhancedInputComponent>(Input))
	{
		EI->BindAction(MoveAction, ETriggerEvent::Triggered, this, &ATrendoCharacter::Move);
		EI->BindAction(LookAction, ETriggerEvent::Triggered, this, &ATrendoCharacter::Look);
		EI->BindAction(JumpAction, ETriggerEvent::Started, this, &ATrendoCharacter::JumpPressed);
		EI->BindAction(JumpAction, ETriggerEvent::Completed, this, &ATrendoCharacter::JumpReleased);
		EI->BindAction(SprintAction, ETriggerEvent::Started, this, &ATrendoCharacter::SprintOn);
		EI->BindAction(SprintAction, ETriggerEvent::Completed, this, &ATrendoCharacter::SprintOff);
		EI->BindAction(AttackAction, ETriggerEvent::Started, this, &ATrendoCharacter::Attack);
		EI->BindAction(UseAction, ETriggerEvent::Started, this, &ATrendoCharacter::Use);
		EI->BindAction(PauseAction, ETriggerEvent::Started, this, &ATrendoCharacter::PausePressed);
		EI->BindAction(UpAction, ETriggerEvent::Started, this, &ATrendoCharacter::MenuUp);
		EI->BindAction(DownAction, ETriggerEvent::Started, this, &ATrendoCharacter::MenuDown);
		EI->BindAction(EnterAction, ETriggerEvent::Started, this, &ATrendoCharacter::MenuEnter);
	}
}

static ATrendoGameMode* TrendoGM(const UObject* O) { return Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(O)); }

void ATrendoCharacter::Move(const FInputActionValue& V)
{
	const ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing() || !Controller) return;
	const FVector2D In = V.Get<FVector2D>(); const FRotator Yaw(0, Controller->GetControlRotation().Yaw, 0);
	AddMovementInput(FRotationMatrix(Yaw).GetUnitAxis(EAxis::X), In.Y);
	AddMovementInput(FRotationMatrix(Yaw).GetUnitAxis(EAxis::Y), In.X);
}

void ATrendoCharacter::Look(const FInputActionValue& V)
{
	const ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing()) return;
	const FVector2D In = V.Get<FVector2D>(); AddControllerYawInput(In.X); AddControllerPitchInput(-In.Y);
}

void ATrendoCharacter::JumpPressed() { const ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing()) return; bThrust = true; Jump(); }
void ATrendoCharacter::JumpReleased() { bThrust = false; StopJumping(); }
void ATrendoCharacter::Attack() { ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing() || Cool > 0) return; Cool = 0.33f; GM->Attack(); }
void ATrendoCharacter::Use() { ATrendoGameMode* GM = TrendoGM(this); if (GM && GM->Playing()) GM->Dismount(); }
void ATrendoCharacter::PausePressed() { if (ATrendoGameMode* GM = TrendoGM(this)) GM->MenuKey(TEXT("escape")); }
void ATrendoCharacter::MenuUp() { if (ATrendoGameMode* GM = TrendoGM(this); GM && !GM->Playing()) GM->MenuKey(TEXT("up")); }
void ATrendoCharacter::MenuDown() { if (ATrendoGameMode* GM = TrendoGM(this); GM && !GM->Playing()) GM->MenuKey(TEXT("down")); }
void ATrendoCharacter::MenuEnter() { if (ATrendoGameMode* GM = TrendoGM(this); GM && !GM->Playing()) GM->MenuKey(TEXT("enter")); }

void ATrendoCharacter::NotifyActorBeginOverlap(AActor* Other)
{
	Super::NotifyActorBeginOverlap(Other);
	if (ATrendoGameMode* GM = TrendoGM(this)) GM->Touch(Other);
}
""",
        ["TrendoGameMode.h"] = """
// Von Trendo erzeugt: Spielablauf – Welt aus Trendo bauen, Regeln, Kampf, Fahrzeuge, Spielstand, Menü und Anzeige.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/HUD.h"
#include "GameFramework/SaveGame.h"
#include "TrendoTypes.h"
#include "TrendoGameMode.generated.h"

class ATrendoThing; class ATrendoEnemy; class ATrendoCharacter; class UStaticMesh;

/** Spielstand – gleiche Inhalte wie in Trendo (Format 1). */
UCLASS()
class UTrendoSaveGame : public USaveGame
{
	GENERATED_BODY()
public:
	UPROPERTY() int32 Version = 1;
	UPROPERTY() int32 Level = 0;
	UPROPERTY() int32 Score = 0;
	UPROPERTY() int32 Lives = 3;
	UPROPERTY() TArray<FString> Inventory;
};

/** Anzeige: Punkte, Leben, Aufgaben, Inventar, Treibstoff, Meldung; Menü und Pause. */
UCLASS()
class ATrendoHUD : public AHUD
{
	GENERATED_BODY()
public:
	virtual void DrawHUD() override;
};

UCLASS()
class ATrendoGameMode : public AGameModeBase
{
	GENERATED_BODY()
public:
	ATrendoGameMode();
	FTrendoData Data; FTrendoState State;
	UPROPERTY() TArray<ATrendoEnemy*> Enemies;
	UPROPERTY() TArray<AActor*> Spawned;
	/** Echte Modelle je Rolle (Kachel-Zeichen o, G, Z … sowie waffe, wurf) aus Content/Trendo/natur.json. */
	UPROPERTY() TMap<FString, UStaticMesh*> Models;
	/** Menü: menu, play, pause, settings, help. */
	FString Mode = TEXT("menu"); int32 Sel = 0; FString Back = TEXT("menu"); bool bHasSave = false;
	bool bContrast = false, bBig = false, bMap = true; int32 Volume = 100;
	static constexpr float Tile = 100.f;

	bool Playing() const { return Mode == TEXT("play"); }
	/** level.json aus Content/Trendo lesen (oder übergebene Daten für Tests). */
	bool LoadData(FString& Error);
	/** Welt bauen: Level-Raster als 3D-Blöcke oder Spielobjekte in der Open World verteilen. */
	void BuildWorld(int32 Level);
	void Touch(AActor* Other);
	void Damage(ATrendoEnemy* E);
	/** Blutfleck auf dem Boden unter Pos (Material M_TrendoBlut aus der Welt-Einrichtung), Größe 1 = normal. */
	void SpawnBlood(const FVector& Pos, float Size);
	void Attack();
	void Dismount();
	/** In den fahrbaren Wagen steigen bzw. aussteigen (E oder Controller-B). */
	bool EnterCar(APawn* Car);
	void ExitCar();
	UPROPERTY() APawn* DrivenCar = nullptr;
	UPROPERTY() ATrendoCharacter* DrivenHero = nullptr;
	void Hit(bool bFall = false);
	void NewGame();
	void SaveGame();
	TArray<FString> MenuItems() const;
	void Choose(const FString& Item);
	void MenuKey(const FString& Key);
	ATrendoCharacter* Hero() const;
	/** Richtung des freien Spielwegs (Startpunkt) – Fahrzeuge stehen in Fahrtrichtung. */
	float PathYaw() const { return StartRot.Yaw; }

	virtual void InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage) override;
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
private:
	float CheckSeconds = -1; bool bShotTaken = false; bool bCloseShot = false; bool bCloseDone = false; bool bProbe = false; bool bProbeDone = false; float ProbeStart = 0; int32 ProbeLives = 0; bool bCarProbe = false; bool bCarDone = false; int32 BloodCount = 0; int32 Severed = 0; float FightShotDelay = 0; bool bFight = false; bool bFightDone = false; UPROPERTY() ATrendoEnemy* FightEnemy = nullptr; FVector CarStart = FVector::ZeroVector; float WheelTurn = 0; TMap<FString, FQuat> WheelLast;
	static float WheelAngle(AActor* Car, TMap<FString, FQuat>& Last);
	static void VehicleAxis(AActor* Car, FName Input, float Value);
	static void VehicleCall(AActor* Car, const TCHAR* Func, FName Input, int32 Value);
	static int32 VehicleGear(AActor* Car);
	float GearCool = 0;
	FVector Origin = FVector::ZeroVector; FRotator StartRot = FRotator::ZeroRotator;
	FVector Place(int32 X, int32 Y) const;
	void Freeze();
	void Report();
	void SpawnNature();
	void LoadModels();
	int32 NatureCount = 0;
};
""",
        ["TrendoGameMode.cpp"] = """
// Von Trendo erzeugt: Spielablauf.
#include "TrendoGameMode.h"
#include "TrendoActors.h"
#include "TrendoCharacter.h"
#include "TrendoCar.h"
#include "ChaosVehicleMovementComponent.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/Font.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerStart.h"
#include "Kismet/GameplayStatics.h"
#include "EngineUtils.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Misc/CommandLine.h"
#include "UnrealClient.h"
#include "Components/HierarchicalInstancedStaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Components/DecalComponent.h"
#include "Engine/DecalActor.h"
#include "Components/CapsuleComponent.h"
#include "EnhancedInputSubsystems.h"
#include "InputAction.h"
#include "Engine/SkeletalMesh.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/DirectionalLight.h"
#include "Engine/ExponentialHeightFog.h"
#include "Components/ExponentialHeightFogComponent.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

DEFINE_LOG_CATEGORY_STATIC(LogTrendo, Log, All);

ATrendoGameMode::ATrendoGameMode()
{
	PrimaryActorTick.bCanEverTick = true;
	DefaultPawnClass = ATrendoCharacter::StaticClass();
	HUDClass = ATrendoHUD::StaticClass();
}

bool ATrendoGameMode::LoadData(FString& Error)
{
	FString Json; const FString File = FPaths::ProjectContentDir() / TEXT("Trendo/level.json");
	if (!FFileHelper::LoadFileToString(Json, *File)) { Error = TEXT("level.json fehlt: ") + File; return false; }
	return FTrendoData::Parse(Json, Data, Error);
}

void ATrendoGameMode::InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage)
{
	Super::InitGame(MapName, Options, ErrorMessage);
	FString Error; if (!LoadData(Error)) UE_LOG(LogTrendo, Error, TEXT("TRENDO-FEHLER %s"), *Error);
}

ATrendoCharacter* ATrendoGameMode::Hero() const { return DrivenHero ? DrivenHero : Cast<ATrendoCharacter>(UGameplayStatics::GetPlayerPawn(this, 0)); }

bool ATrendoGameMode::EnterCar(APawn* Car)
{
	ATrendoCharacter* H = Hero(); APlayerController* PC = GetWorld()->GetFirstPlayerController();
	if (!H || !PC || !IsValid(Car) || DrivenCar) return false;
	DrivenHero = H; DrivenCar = Car;
	H->SetActorHiddenInGame(true); H->SetActorEnableCollision(false); H->GetCharacterMovement()->DisableMovement();
	H->AttachToActor(Car, FAttachmentTransformRules::KeepWorldTransform);
	PC->Possess(Car);
	State.Note = TEXT("Wagen: W/S oder Trigger Gas und Bremse, A/D lenken – E steigt aus");
	UE_LOG(LogTrendo, Display, TEXT("TRENDO-WAGEN eingestiegen"));
	return true;
}

void ATrendoGameMode::ExitCar()
{
	APlayerController* PC = GetWorld()->GetFirstPlayerController(); ATrendoCharacter* H = DrivenHero;
	if (!PC || !H || !IsValid(DrivenCar)) { DrivenCar = nullptr; DrivenHero = nullptr; return; }
	H->DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
	H->SetActorLocation(DrivenCar->GetActorLocation() + DrivenCar->GetActorRightVector() * -260.f + FVector(0, 0, 120), false, nullptr, ETeleportType::TeleportPhysics);
	H->SetActorRotation(FRotator(0, DrivenCar->GetActorRotation().Yaw, 0));
	H->SetActorHiddenInGame(false); H->SetActorEnableCollision(true); H->GetCharacterMovement()->SetMovementMode(MOVE_Walking);
	DrivenCar = nullptr; DrivenHero = nullptr; PC->Possess(H); H->NoEnter = 0.5f; State.Note = TEXT("Ausgestiegen");
}

void ATrendoGameMode::BeginPlay()
{
	Super::BeginPlay();
	State.Reset(Data);
	bHasSave = UGameplayStatics::DoesSaveGameExist(TEXT("Trendo"), 0);
	if (bHasSave)
		if (UTrendoSaveGame* S = Cast<UTrendoSaveGame>(UGameplayStatics::LoadGameFromSlot(TEXT("Trendo"), 0)); S && S->Version <= 1 && Data.Levels.IsValidIndex(S->Level))
		{ State.Level = S->Level; State.Score = S->Score; State.Lives = FMath::Max(1, S->Lives); State.Inventory = S->Inventory; }
	Mode = Data.bMenu ? TEXT("menu") : TEXT("play");
	LoadModels();
	if (Data.Levels.Num() > 0) BuildWorld(State.Level);
	SpawnNature();
	if (FParse::Param(FCommandLine::Get(), TEXT("trendocheck"))) { CheckSeconds = FParse::Param(FCommandLine::Get(), TEXT("trendoshot")) ? 12.f : 4.f; Mode = TEXT("play"); } // Bild erst, wenn Texturen geladen sind
}

FVector ATrendoGameMode::Place(int32 X, int32 Y) const
{
	const TArray<FString>& Rows = Data.Levels[State.Level].Rows;
	if (Data.World == TEXT("openworld"))
	{
		// Open World: Objekte vor dem Startpunkt auf dem Gelände verteilen (Höhe per Strahl nach unten)
		FVector P = Origin + StartRot.RotateVector(FVector(400.f + X * 300.f, (Y - Rows.Num() / 2) * 300.f, 0));
		FHitResult Hit; FCollisionQueryParams Q; if (APawn* H = UGameplayStatics::GetPlayerPawn(this, 0)) Q.AddIgnoredActor(H);
		for (AActor* A : Spawned) Q.AddIgnoredActor(A);
		if (GetWorld()->LineTraceSingleByChannel(Hit, P + FVector(0, 0, 50000), P - FVector(0, 0, 50000), ECC_WorldStatic, Q)) P.Z = Hit.Location.Z + 60.f;
		return P;
	}
	return Origin + StartRot.RotateVector(FVector(X * Tile, 0, 0)) + FVector(0, 0, (Rows.Num() - 1 - Y) * Tile);
}

void ATrendoGameMode::BuildWorld(int32 Level)
{
	for (AActor* A : Spawned) if (IsValid(A)) A->Destroy();
	Spawned.Empty(); Enemies.Empty(); State.Level = Level; State.bDoorsOpen = false;
	if (AActor* Start = FindPlayerStart(nullptr)) { Origin = Start->GetActorLocation() - FVector(0, 0, 90); StartRot = FRotator(0, Start->GetActorRotation().Yaw, 0); }
	const bool bOpen = Data.World == TEXT("openworld");
	const TArray<FString>& Rows = Data.Levels[Level].Rows;
	FVector StartAt = Origin + FVector(0, 0, 200);
	for (int32 Y = 0; Y < Rows.Num(); Y++)
		for (int32 X = 0; X < Rows[Y].Len(); X++)
		{
			const TCHAR C = Rows[Y][X];
			if (C == '.' || (bOpen && C == '#')) continue;
			const FVector P = Place(X, Y);
			if (C == 'S') { StartAt = P + FVector(0, 0, 120); continue; }
			if (C == 'E')
			{
				FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
				ATrendoEnemy* E = GetWorld()->SpawnActor<ATrendoEnemy>(P + FVector(0, 0, 10), FRotator::ZeroRotator, SP);
				E->Init(FMath::Clamp(FMath::RoundToInt(Data.Prop(Level, X, Y, TEXT("hp"), 1)), 1, 9), P.Z - (bOpen ? 60.f : 50.f));
				Enemies.Add(E); Spawned.Add(E); continue;
			}
			FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
			ATrendoThing* T = GetWorld()->SpawnActor<ATrendoThing>(P, FRotator::ZeroRotator, SP);
			FLinearColor Col(0.35f, 0.4f, 0.5f); float Size = 1.f; bool bSolid = false;
			switch (C)
			{
			case '#': bSolid = true; break;
			case 'o': Col = FLinearColor(1.f, 0.8f, 0.1f); Size = 0.4f; break;
			case '^': Col = FLinearColor(0.9f, 0.3f, 0.3f); Size = 0.6f; break;
			case 'Z': Col = FLinearColor(0.2f, 0.9f, 0.3f); Size = 1.2f; break;
			case 'F': Col = FLinearColor(1.f, 0.6f, 0.1f); Size = 0.5f; break;
			case 'C': Col = FLinearColor(0.2f, 0.9f, 0.9f); Size = 0.5f; break;
			case 'K': Col = FLinearColor(1.f, 0.85f, 0.2f); Size = 0.4f; break;
			case 'D': Col = FLinearColor(0.55f, 0.35f, 0.2f); bSolid = true; break;
			case 'G': Col = FLinearColor(0.66f, 0.33f, 0.97f); Size = 0.4f; break;
			case 'N': Col = FLinearColor(0.08f, 0.72f, 0.65f); Size = 0.8f; break;
			case 'V': Col = FLinearColor(0.98f, 0.45f, 0.09f); Size = 0.9f; break;
			default: break;
			}
			if (C == 'V') T->Kind = Data.Text(Level, X, Y, TEXT("kind"), TEXT("wagen"));
			T->Setup(C, X, Y, Col, Size, bSolid);
			Spawned.Add(T); if (IsValid(T->Car)) Spawned.Add(T->Car);
		}
	if (ATrendoCharacter* H = Hero())
	{
		H->SetActorLocation(StartAt); H->Checkpoint = StartAt; H->ApplyAbilities();
		if (bOpen) H->SetActorLocation(Origin + FVector(0, 0, 120)); // Open World: am Startpunkt, Spielobjekte davor
		H->SetActorRotation(StartRot); if (AController* C = H->GetController()) C->SetControlRotation(StartRot + FRotator(-12, 0, 0));
		H->Checkpoint = H->GetActorLocation();
	}
}

void ATrendoGameMode::Touch(AActor* Other)
{
	ATrendoCharacter* H = Hero(); if (!H || !Playing()) return;
	if (ATrendoEnemy* E = Cast<ATrendoEnemy>(Other))
	{
		if (H->GetVelocity().Z < -100.f && H->GetActorLocation().Z > E->GetActorLocation().Z + 50.f) { Damage(E); H->LaunchCharacter(FVector(0, 0, 500), false, true); }
		else Hit();
		return;
	}
	ATrendoThing* T = Cast<ATrendoThing>(Other); if (!T) return;
	const TCHAR C = T->Tile.Len() > 0 ? T->Tile[0] : '.';
	switch (C)
	{
	case 'o': State.Coins++; State.Score += 10; T->Destroy(); break;
	case 'K': T->Destroy(); if (State.Inventory.Num() >= 0) { int32 Left = 0; for (AActor* A : Spawned) if (IsValid(A) && A != T) if (ATrendoThing* K = Cast<ATrendoThing>(A); K && K->Tile == TEXT("K")) Left++; if (Left == 0) State.bDoorsOpen = true; } break;
	case '^': Hit(); break;
	case 'F': H->LaunchCharacter(FVector(0, 0, 1100), false, true); break;
	case 'C': if (!H->Checkpoint.Equals(T->GetActorLocation() + FVector(0, 0, 100), 1.f)) { H->Checkpoint = T->GetActorLocation() + FVector(0, 0, 100); SaveGame(); } break;
	case 'G': { const FString N = Data.Text(State.Level, T->TileX, T->TileY, TEXT("name"), TEXT("Kristall")); State.Inventory.Add(N); State.Items++; State.Note = N + TEXT(" eingesammelt"); T->Destroy(); break; }
	case 'N': State.TalkTo(Data, T->TileX, T->TileY); break;
	case 'V': if (IsValid(T->Car)) break; // fahrbarer Wagen: Einsteigen mit E
		if (H->Vehicle.IsEmpty() && H->NoEnter <= 0) { H->SetVehicle(T->Kind == TEXT("ufo") ? TEXT("ufo") : TEXT("wagen")); State.Note = H->Vehicle == TEXT("ufo") ? TEXT("Ufo: Leertaste halten – E steigt aus") : TEXT("Wagen: schnell, fängt einen Treffer ab – E steigt aus"); T->Destroy(); } break;
	case 'Z':
		State.Score += 10;
		if (State.Level + 1 < Data.Levels.Num()) { BuildWorld(State.Level + 1); SaveGame(); }
		else { State.bWon = true; UGameplayStatics::DeleteGameInSlot(TEXT("Trendo"), 0); State.Note = TEXT("Gewonnen!"); }
		break;
	default: break;
	}
}

void ATrendoGameMode::Damage(ATrendoEnemy* E)
{
	if (!IsValid(E) || E->Hp <= 0) return;
	const bool bBrutal = Data.Violence == TEXT("brutal"), bNone = Data.Violence == TEXT("keine");
	FVector From = Hero() ? (E->GetActorLocation() - Hero()->GetActorLocation()).GetSafeNormal2D() : -E->GetActorForwardVector();
	E->Hp--;
	if (E->Hp > 0) { E->Knock(From * (bBrutal ? 260.f : 180.f)); return; } // Treffer: taumelt zurück, kurz benommen
	State.Defeated++; State.Score += E->MaxHp > 1 ? 10 : 2; if (E->MaxHp > 1) State.Note = TEXT("Boss besiegt!");
	Enemies.Remove(E);
	if (bBrutal && E->Sever(From)) Severed++; // brutale Spiele: ein Glied wird abgetrennt
	if (bNone || !E->Ragdoll(From * (bBrutal ? 32000.f : 18000.f))) { E->Destroy(); return; } // ohne Gewalt verschwindet er, sonst fällt er als Körper
	// Bodenflecken nur auf dem Boden: der liegende Körper nimmt keine Boden-Decals an
	{ TArray<UPrimitiveComponent*> Prims; E->GetComponents(Prims); if (E->Body3D && E->Body3D->GetOwner()) { TArray<UPrimitiveComponent*> More; E->Body3D->GetOwner()->GetComponents(More); Prims.Append(More); }
	  for (UPrimitiveComponent* P : Prims) P->SetReceivesDecals(false); }
	if (!bNone) SpawnBlood(E->GetActorLocation(), bBrutal ? 1.2f : 0.8f); // Spritzer am Trefferort
	// Blutlache dort, wo der Körper nach dem Sturz liegt (nach 1,2 s)
	if (!bNone && E->Body3D)
	{
		TWeakObjectPtr<USkeletalMeshComponent> Body = E->Body3D; const float Size = bBrutal ? 2.4f : 1.5f; const FName Stump = E->SeveredBone; FTimerHandle Th;
		GetWorldTimerManager().SetTimer(Th, FTimerDelegate::CreateWeakLambda(this, [this, Body, Size, Stump]() { if (!Body.IsValid()) return; SpawnBlood(Body->GetBoneLocation(TEXT("pelvis")), Size);
			if (!Stump.IsNone()) SpawnBlood(Body->GetBoneLocation(Stump), Size * 0.55f); }), 1.2f, false);
	}
	E->SetLifeSpan(30.f);
}

void ATrendoGameMode::Attack()
{
	ATrendoCharacter* H = Hero(); if (!H || Data.Weapon.IsEmpty()) return;
	const FVector Fwd = H->GetActorForwardVector(), At = H->GetActorLocation();
	if (Data.Weapon == TEXT("schwert"))
	{
		for (ATrendoEnemy* E : TArray<ATrendoEnemy*>(Enemies))
		{
			const FVector D = E->GetActorLocation() - At;
			if (FVector(D.X, D.Y, 0).Size() < 180.f && FMath::Abs(D.Z) < 120.f && FVector::DotProduct(D.GetSafeNormal2D(), Fwd) > 0.2f) Damage(E);
		}
	}
	else
	{
		FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		if (ATrendoShot* S = GetWorld()->SpawnActor<ATrendoShot>(At + Fwd * 70.f, FRotator::ZeroRotator, SP)) S->Dir = Fwd;
	}
}

void ATrendoGameMode::Dismount()
{
	ATrendoCharacter* H = Hero(); if (!H) return;
	if (H->Vehicle.IsEmpty())
	{
		for (AActor* A : Spawned) if (ATrendoThing* T = Cast<ATrendoThing>(A); IsValid(T) && IsValid(T->Car) && FVector::Dist(T->Car->GetActorLocation(), H->GetActorLocation()) < 450.f) { EnterCar(T->Car); return; }
		return;
	}
	FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	ATrendoThing* T = GetWorld()->SpawnActor<ATrendoThing>(H->GetActorLocation() - H->GetActorForwardVector() * 150.f, FRotator::ZeroRotator, SP);
	T->Kind = H->Vehicle; T->Setup('V', -1, -1, FLinearColor(0.98f, 0.45f, 0.09f), 0.9f, false); Spawned.Add(T);
	H->SetVehicle(FString()); H->NoEnter = 0.5f; State.Note = TEXT("Ausgestiegen");
}

void ATrendoGameMode::Hit(bool bFall)
{
	ATrendoCharacter* H = Hero(); if (!H) return;
	if (!H->Vehicle.IsEmpty() && !bFall) { H->SetVehicle(FString()); H->Safe = 0.75f; State.Note = TEXT("Fahrzeug kaputt – zu Fuß weiter"); return; }
	if (H->Safe > 0 && !bFall) return;
	H->SetVehicle(FString()); State.Lives--;
	if (State.Lives <= 0) { State.bOver = true; UGameplayStatics::DeleteGameInSlot(TEXT("Trendo"), 0); State.Note = TEXT("Verloren – Esc, Neu beginnen"); }
	else H->Respawn();
}

void ATrendoGameMode::SaveGame()
{
	UTrendoSaveGame* S = Cast<UTrendoSaveGame>(UGameplayStatics::CreateSaveGameObject(UTrendoSaveGame::StaticClass()));
	S->Level = State.Level; S->Score = State.Score; S->Lives = State.Lives; S->Inventory = State.Inventory;
	UGameplayStatics::SaveGameToSlot(S, TEXT("Trendo"), 0);
}

void ATrendoGameMode::NewGame()
{
	UGameplayStatics::DeleteGameInSlot(TEXT("Trendo"), 0); bHasSave = false;
	State.Reset(Data); BuildWorld(0);
}

TArray<FString> ATrendoGameMode::MenuItems() const
{
	if (Mode == TEXT("menu")) { TArray<FString> L; if (bHasSave) L.Add(TEXT("Fortsetzen")); L.Append({ TEXT("Spielen"), TEXT("Einstellungen"), TEXT("Steuerung") }); return L; }
	if (Mode == TEXT("pause")) return { TEXT("Weiter"), TEXT("Neu beginnen"), TEXT("Einstellungen"), TEXT("Zum Menü") };
	if (Mode == TEXT("settings")) return { FString::Printf(TEXT("Lautstärke: %d %%"), Volume), FString(TEXT("Hoher Kontrast: ")) + (bContrast ? TEXT("an") : TEXT("aus")), FString(TEXT("Große Schrift: ")) + (bBig ? TEXT("an") : TEXT("aus")), FString(TEXT("Karte: ")) + (bMap ? TEXT("an") : TEXT("aus")), TEXT("Zurück") };
	return { TEXT("Zurück") };
}

void ATrendoGameMode::Choose(const FString& Item)
{
	FString W = Item; Item.Split(TEXT(":"), &W, nullptr);
	if (W == TEXT("Fortsetzen") || W == TEXT("Weiter")) Mode = TEXT("play");
	else if (W == TEXT("Spielen") || W == TEXT("Neu beginnen")) { NewGame(); Mode = TEXT("play"); }
	else if (W == TEXT("Einstellungen") || W == TEXT("Steuerung")) { Back = Mode; Mode = W == TEXT("Einstellungen") ? TEXT("settings") : TEXT("help"); Sel = 0; }
	else if (W == TEXT("Zum Menü")) { Mode = TEXT("menu"); Sel = 0; }
	else if (W == TEXT("Lautstärke")) Volume = (Volume + 25) % 125;
	else if (W == TEXT("Hoher Kontrast")) bContrast = !bContrast;
	else if (W == TEXT("Große Schrift")) bBig = !bBig;
	else if (W == TEXT("Karte")) bMap = !bMap;
	else if (W == TEXT("Zurück")) { Mode = Back; Sel = 0; }
}

void ATrendoGameMode::MenuKey(const FString& Key)
{
	if (Mode == TEXT("play")) { if (Key == TEXT("escape")) { Mode = TEXT("pause"); Sel = 0; } return; }
	const TArray<FString> It = MenuItems();
	if (Key == TEXT("up")) Sel = (Sel + It.Num() - 1) % It.Num();
	else if (Key == TEXT("down")) Sel = (Sel + 1) % It.Num();
	else if (Key == TEXT("enter")) Choose(It[FMath::Min(Sel, It.Num() - 1)]);
	else if (Key == TEXT("escape")) { if (Mode == TEXT("pause")) Mode = TEXT("play"); else if (Mode == TEXT("settings") || Mode == TEXT("help")) { Mode = Back; Sel = 0; } }
}

void ATrendoGameMode::Freeze() { for (ATrendoEnemy* E : Enemies) if (IsValid(E)) E->bFrozen = !Playing(); }

void ATrendoGameMode::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Freeze();
	// im Wagen: E (Tastatur) bzw. B (Controller) steigt aus – der Wagen hat eigene Steuerung, die Figur ist dann nicht aktiv
	if (DrivenCar) if (APlayerController* PC = GetWorld()->GetFirstPlayerController(); PC && (PC->WasInputKeyJustPressed(EKeys::E) || PC->WasInputKeyJustPressed(EKeys::Gamepad_FaceButton_Right))) ExitCar();
	if (DrivenCar && bCarProbe && !bCarDone)
		if (ATrendoCar* TC = Cast<ATrendoCar>(DrivenCar)) { TC->Drive(1.f, 0.f, 0.f); WheelTurn = TC->WheelSpin(); } // Vollgas geradeaus wie ein Spieler
	if (Playing() && !State.bWon && !State.bOver)
	{
		State.Seconds += DeltaSeconds; State.CheckRules(Data);
		if (State.bDoorsOpen) for (AActor* A : Spawned) if (ATrendoThing* T = Cast<ATrendoThing>(A); IsValid(T) && T->Tile == TEXT("D")) T->Destroy();
	}
	// Prüflauf: keine echten Eingaben (Maus, Tastatur, Controller des Nutzers) – Figur und Kamera bleiben am Start
	if (CheckSeconds > 0) if (APlayerController* PC = GetWorld()->GetFirstPlayerController()) { PC->SetIgnoreMoveInput(true); PC->SetIgnoreLookInput(true); if (PC->GetPawn()) PC->GetPawn()->DisableInput(PC); }
	if (CheckSeconds > 0 && (CheckSeconds -= DeltaSeconds) <= 0)
	{
		// Startlauf (-trendocheck): mit -trendoshot erst ein Bildschirmfoto nach Saved/Screenshots, dann Bericht und Ende
		const bool bShot = FParse::Param(FCommandLine::Get(), TEXT("trendoshot"));
		if (!bShotTaken && bShot) { bShotTaken = true; FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir() / TEXT("Screenshots/trendo-start.png"), false, false); CheckSeconds = 1.5f; }
		else if (bShot && !bCloseShot && Data.World == TEXT("openworld"))
		{
			// Zweites Bild aus der Nähe: Figur seitlich vor die ersten Spielobjekte stellen (Modelle prüfen), Bericht folgt danach
			bCloseShot = true; CheckSeconds = 4.f;
			ATrendoThing* First = nullptr; for (AActor* A : Spawned) if (ATrendoThing* T = Cast<ATrendoThing>(A); IsValid(T) && T->bModel) { First = T; break; }
			if (ATrendoCharacter* H = Hero(); H && First)
			{
				const FVector Fwd = StartRot.Vector(), Right = FRotationMatrix(StartRot).GetUnitAxis(EAxis::Y);
				H->SetActorLocation(First->GetActorLocation() - Fwd * 260.f + Right * 120.f + FVector(0, 0, 60), false, nullptr, ETeleportType::TeleportPhysics);
				H->SetActorRotation(StartRot); if (AController* C = H->GetController()) C->SetControlRotation(StartRot + FRotator(-14, -12, 0));
			}
		}
		else if (bCloseShot && !bCloseDone) { bCloseDone = true; FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir() / TEXT("Screenshots/trendo-objekte.png"), false, false); CheckSeconds = 1.5f; }
		else if (bCloseDone && !bProbe && Enemies.Num() > 0 && IsValid(Enemies[0]) && Hero())
		{
			// Gegner-Probe: Figur 11 m vor den ersten Gegner stellen (in sein Blickfeld) – nach 3 s muss er sie bemerkt haben und näher gekommen sein
			bProbe = true; CheckSeconds = 3.f;
			ATrendoEnemy* E = Enemies[0]; ATrendoCharacter* H = Hero();
			const FVector At = E->GetActorLocation() + E->GetActorForwardVector() * 1100.f;
			H->SetActorLocation(FVector(At.X, At.Y, E->GetActorLocation().Z + 150.f), false, nullptr, ETeleportType::TeleportPhysics);
			const FRotator Look = (E->GetActorLocation() - H->GetActorLocation()).Rotation();
			H->SetActorRotation(FRotator(0, Look.Yaw, 0)); if (AController* C = H->GetController()) C->SetControlRotation(FRotator(-10, Look.Yaw, 0));
			ProbeStart = FVector::Dist2D(E->GetActorLocation(), H->GetActorLocation()); ProbeLives = State.Lives;
		}
		else if (bProbe && !bProbeDone && Enemies.Num() > 0 && IsValid(Enemies[0]) && Hero())
		{
			bProbeDone = true; CheckSeconds = 1.5f;
			const float Now = FVector::Dist2D(Enemies[0]->GetActorLocation(), Hero()->GetActorLocation());
			const bool bHit = State.Lives < ProbeLives; // Treffer setzt die Figur an den Speicherpunkt zurück – dann zählt der Angriff
			{
				ATrendoEnemy* E = Enemies[0]; FHitResult G; FCollisionQueryParams Q(TEXT("TrendoProbe"), false, E); TArray<AActor*> Kids; E->GetAllChildActors(Kids); Q.AddIgnoredActors(Kids);
				const float Bottom = E->GetActorLocation().Z - E->GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
				const bool bG = GetWorld()->LineTraceSingleByChannel(G, E->GetActorLocation(), E->GetActorLocation() - FVector(0, 0, 2000), ECC_WorldStatic, Q);
				UE_LOG(LogTrendo, Display, TEXT("TRENDO-BODEN Gegner geht=%s Abstand Füße–Boden=%.1f cm"), E->GetCharacterMovement()->IsMovingOnGround() ? TEXT("ja") : TEXT("nein"), bG ? Bottom - G.Location.Z : -1.f);
			}
			UE_LOG(LogTrendo, Display, TEXT("TRENDO-GEGNER alarmiert=%s Abstand vorher=%.0f nachher=%.0f Leben %d→%d %s"), Enemies[0]->Alert > 0 ? TEXT("ja") : TEXT("nein"), ProbeStart, Now, ProbeLives, State.Lives,
				bHit ? TEXT("verfolgt und angegriffen") : Enemies[0]->Alert > 0 && Now < ProbeStart - 300.f ? TEXT("verfolgt") : TEXT("reagiert nicht"));
			FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir() / TEXT("Screenshots/trendo-gegner.png"), false, false);
		}
		else if (bProbeDone && !bFight && Enemies.Num() > 0 && IsValid(Enemies[0]) && Hero())
		{
			bFight = true; CheckSeconds = 3.5f; ATrendoEnemy* E = Enemies[0]; ATrendoCharacter* H = Hero(); E->bFrozen = true;
			const FVector At = E->GetActorLocation() - E->GetActorForwardVector() * 120.f;
			H->SetActorLocation(FVector(At.X, At.Y, E->GetActorLocation().Z + 10.f), false, nullptr, ETeleportType::TeleportPhysics);
			const FRotator Look = (E->GetActorLocation() - H->GetActorLocation()).Rotation(); H->SetActorRotation(FRotator(0, Look.Yaw, 0));
			if (AController* Co = H->GetController()) Co->SetControlRotation(FRotator(-15, Look.Yaw + 25.f, 0));
			const FString Old = Data.Weapon; Data.Weapon = TEXT("schwert"); const int32 N = E->MaxHp; for (int32 i = 0; i < N; i++) Attack(); Data.Weapon = Old;
			FightEnemy = E;
		}
		else if (bFight && !bFightDone)
		{
			bFightDone = true; CheckSeconds = 1.5f; // 2 s nach dem letzten Hieb: Körper muss liegen
			float Lie = 9999.f;
			if (IsValid(FightEnemy) && FightEnemy->Body3D)
			{
				const FVector Pelvis = FightEnemy->Body3D->GetBoneLocation(TEXT("pelvis")); FHitResult G; FCollisionQueryParams Q(TEXT("TrendoLiegt"), false, FightEnemy);
				TArray<AActor*> Kids; FightEnemy->GetAllChildActors(Kids); Q.AddIgnoredActors(Kids); if (AActor* Part = FightEnemy->Body3D->GetOwner()) Q.AddIgnoredActor(Part);
				FCollisionObjectQueryParams Land(ECC_WorldStatic); // nur Gelände/Statik, von weit oben: auch ein Körper unter dem Boden wird erkannt
				if (GetWorld()->LineTraceSingleByObjectType(G, FVector(Pelvis.X, Pelvis.Y, Pelvis.Z + 3000), FVector(Pelvis.X, Pelvis.Y, Pelvis.Z - 3000), Land, Q)) Lie = Pelvis.Z - G.Location.Z;
				UE_LOG(LogTrendo, Display, TEXT("TRENDO-KAMPF Körper: Becken Z=%.0f, Gegner Z=%.0f, Simulation=%s, Physik-Körper=%d"), Pelvis.Z, FightEnemy->GetActorLocation().Z, FightEnemy->Body3D->IsSimulatingPhysics() ? TEXT("ja") : TEXT("nein"), FightEnemy->Body3D->Bodies.Num());
			}
			UE_LOG(LogTrendo, Display, TEXT("TRENDO-KAMPF Gegner besiegt=%s Ragdoll=%s Becken über Boden=%.0f cm (%s) Blut=%d abgetrennt=%d Gewalt=%s"), Enemies.Contains(FightEnemy) ? TEXT("nein") : TEXT("ja"),
				IsValid(FightEnemy) && FightEnemy->bDead ? TEXT("ja") : TEXT("nein"), Lie, Lie < -20.f ? TEXT("unter dem Boden") : Lie < 45.f ? TEXT("liegt") : TEXT("steht"), BloodCount, Severed, *Data.Violence);
			// Blick auf den Kampfort: Figur seitlich daneben, Kamera schräg von oben auf den liegenden Gegner
			if (IsValid(FightEnemy) && FightEnemy->Body3D && Hero())
			{
				const FVector Pel = FightEnemy->Body3D->GetBoneLocation(TEXT("pelvis")); const FVector Side = FVector(260.f, -330.f, 0);
				Hero()->SetActorLocation(FVector(Pel.X + Side.X, Pel.Y + Side.Y, Hero()->GetActorLocation().Z + 40.f), false, nullptr, ETeleportType::TeleportPhysics);
				const FRotator Look = (Pel - Hero()->GetActorLocation()).Rotation();
				if (AController* Co = Hero()->GetController()) Co->SetControlRotation(FRotator(-32.f, Look.Yaw + 18.f, 0));
			}
			FightShotDelay = 0.6f;

		}
		else if (bFightDone && FightShotDelay > 0.f) { FightShotDelay = 0.f; CheckSeconds = 1.f; FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir() / TEXT("Screenshots/trendo-kampf.png"), false, false); }
		else if (bFightDone && !bCarProbe)
		{
			// Wagen-Probe: einsteigen (der Wagen wird gesteuert), Bild aus der Fahrzeugkamera, danach aussteigen
			bCarProbe = true; CheckSeconds = 5.f;
			for (AActor* A : Spawned) if (ATrendoThing* T = Cast<ATrendoThing>(A); IsValid(T) && IsValid(T->Car)) { for (ATrendoEnemy* E : Enemies) if (IsValid(E)) E->bFrozen = true; EnterCar(T->Car); CarStart = T->Car->GetActorLocation(); WheelTurn = 0; WheelLast.Empty(); break; }
		}
		else if (bCarProbe && !bCarDone)
		{
			bCarDone = true; CheckSeconds = 1.5f;
			if (DrivenCar)
			{
				UE_LOG(LogTrendo, Display, TEXT("TRENDO-WAGEN gesteuert=%s Klasse=%s Tempo=%.0f km/h Strecke=%.1f m Raddrehung=%.0f° (%s) Gang=%d"), GetWorld()->GetFirstPlayerController()->GetPawn() == DrivenCar ? TEXT("ja") : TEXT("nein"), *DrivenCar->GetClass()->GetName(),
					DrivenCar->GetVelocity().Size() * 0.036f, FVector::Dist2D(DrivenCar->GetActorLocation(), CarStart) / 100.f, WheelTurn, WheelTurn > 90.f ? TEXT("Reifen drehen sich") : TEXT("Reifen stehen"), Cast<ATrendoCar>(DrivenCar) ? Cast<ATrendoCar>(DrivenCar)->GetVehicleMovementComponent()->GetCurrentGear() : -99);
				FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir() / TEXT("Screenshots/trendo-wagen.png"), false, false);
			}
			else UE_LOG(LogTrendo, Display, TEXT("TRENDO-WAGEN kein fahrbarer Wagen"));
		}
		else if (bCarDone && DrivenCar) { ExitCar(); CheckSeconds = 1.5f; UE_LOG(LogTrendo, Display, TEXT("TRENDO-WAGEN ausgestiegen, Figur=%s"), Hero() && !Hero()->IsHidden() ? TEXT("sichtbar") : TEXT("fehlt")); }
		else Report();
	}
}

/** Eingabe an Epics Fahrzeug-Simulation (ModularVehicleBaseComponent.SetInputAxis1D) – über Reflexion, ohne Abhängigkeit vom Plugin-Code. */
void ATrendoGameMode::VehicleAxis(AActor* Car, FName Input, float Value)
{
	TArray<UActorComponent*> Comps; Car->GetComponents(Comps);
	for (UActorComponent* C : Comps)
	{
		UFunction* F = C ? C->FindFunction(TEXT("SetInputAxis1D")) : nullptr; if (!F) continue;
		uint8* Buf = (uint8*)FMemory_Alloca(F->ParmsSize); FMemory::Memzero(Buf, F->ParmsSize);
		for (TFieldIterator<FProperty> It(F); It && It->HasAnyPropertyFlags(CPF_Parm); ++It)
			if (FNameProperty* N = CastField<FNameProperty>(*It)) N->SetPropertyValue_InContainer(Buf, Input);
			else if (FDoubleProperty* D = CastField<FDoubleProperty>(*It)) D->SetPropertyValue_InContainer(Buf, Value);
			else if (FFloatProperty* Fl = CastField<FFloatProperty>(*It)) Fl->SetPropertyValue_InContainer(Buf, Value);
		C->ProcessEvent(F, Buf); return;
	}
}

/** Weitere Fahrzeug-Eingaben über Reflexion (Bool bzw. Gang): Name (optional) und ganzzahliger Wert. */
void ATrendoGameMode::VehicleCall(AActor* Car, const TCHAR* Func, FName Input, int32 Value)
{
	TArray<UActorComponent*> Comps; Car->GetComponents(Comps);
	for (UActorComponent* C : Comps)
	{
		UFunction* F = C ? C->FindFunction(Func) : nullptr; if (!F) continue;
		uint8* Buf = (uint8*)FMemory_Alloca(F->ParmsSize); FMemory::Memzero(Buf, F->ParmsSize);
		for (TFieldIterator<FProperty> It(F); It && It->HasAnyPropertyFlags(CPF_Parm); ++It)
			if (FNameProperty* N = CastField<FNameProperty>(*It)) N->SetPropertyValue_InContainer(Buf, Input);
			else if (FBoolProperty* B = CastField<FBoolProperty>(*It)) B->SetPropertyValue_InContainer(Buf, Value != 0);
			else if (FIntProperty* I = CastField<FIntProperty>(*It)) I->SetPropertyValue_InContainer(Buf, Value);
		C->ProcessEvent(F, Buf); return;
	}
}

/** Aktueller Gang der Fahrzeug-Simulation (GetCurrentGear über Reflexion), −99 wenn unbekannt. */
int32 ATrendoGameMode::VehicleGear(AActor* Car)
{
	TArray<UActorComponent*> Comps; Car->GetComponents(Comps);
	for (UActorComponent* C : Comps)
	{
		UFunction* F = C ? C->FindFunction(TEXT("GetCurrentGear")) : nullptr; if (!F) continue;
		uint8* Buf = (uint8*)FMemory_Alloca(F->ParmsSize); FMemory::Memzero(Buf, F->ParmsSize); C->ProcessEvent(F, Buf);
		if (FIntProperty* R = CastField<FIntProperty>(F->GetReturnProperty())) return R->GetPropertyValue_InContainer(Buf);
	}
	return -99;
}

/** Drehung eines Rads seit dem letzten Bild (Grad): erster Knochen mit „wheel“ im Namen am Fahrzeug-Skelett. */
float ATrendoGameMode::WheelAngle(AActor* Car, TMap<FString, FQuat>& Last)
{
	// größte Drehung seit dem letzten Bild über alle Rad-Knochen aller Skelette des Fahrzeugs (Grad)
	float Max = 0.f; TArray<USkeletalMeshComponent*> Meshes; Car->GetComponents(Meshes);
	for (USkeletalMeshComponent* M : Meshes)
		for (int32 i = 0; i < M->GetNumBones(); i++)
		{
			const FName B = M->GetBoneName(i); if (!B.ToString().Contains(TEXT("wheel"), ESearchCase::IgnoreCase)) continue;
			const FString Key = M->GetName() + TEXT("/") + B.ToString();
			const FQuat Q = M->GetBoneQuaternion(B, EBoneSpaces::ComponentSpace);
			if (const FQuat* P = Last.Find(Key)) Max = FMath::Max(Max, FMath::RadiansToDegrees(P->AngularDistance(Q)));
			Last.Add(Key, Q);
		}
	return Max;
}

void ATrendoGameMode::SpawnBlood(const FVector& Pos, float Size)
{
	UMaterialInterface* M = LoadObject<UMaterialInterface>(nullptr, TEXT("/Game/Trendo/Natur/M_TrendoBlut.M_TrendoBlut")); if (!M) return;
	FHitResult Hit; FCollisionQueryParams Q; for (ATrendoEnemy* E : Enemies) Q.AddIgnoredActor(E);
	if (!GetWorld()->LineTraceSingleByObjectType(Hit, Pos + FVector(0, 0, 50), Pos - FVector(0, 0, 400), FCollisionObjectQueryParams(ECC_WorldStatic), Q)) return; // nur Gelände, nie der liegende Körper
	FRandomStream R(static_cast<int32>(Pos.X * 7 + Pos.Y));
	for (int32 i = 0; i < 3; i++) // ein großer Fleck und zwei Spritzer daneben
	{
		const float S = (i == 0 ? 60.f : 22.f) * Size * R.FRandRange(0.8f, 1.2f);
		const FVector At = Hit.Location + (i == 0 ? FVector::ZeroVector : FVector(R.FRandRange(-90, 90), R.FRandRange(-90, 90), 0) * Size);
		if (UGameplayStatics::SpawnDecalAtLocation(this, M, FVector(40, S, S), At, FRotator(-90, R.FRandRange(0, 360), 0), 120.f)) BloodCount++;
	}
}

/** Echte Modelle für Spielobjekte aus Content/Trendo/natur.json („objects“: Rolle → Modellpfad). */
void ATrendoGameMode::LoadModels()
{
	Models.Empty();
	FString Json; if (!FFileHelper::LoadFileToString(Json, *(FPaths::ProjectContentDir() / TEXT("Trendo/natur.json")))) return;
	TSharedPtr<FJsonObject> O; if (!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json), O) || !O.IsValid()) return;
	const TSharedPtr<FJsonObject>* Obj; if (!O->TryGetObjectField(TEXT("objects"), Obj)) return;
	for (const auto& KV : (*Obj)->Values)
		if (UStaticMesh* M = LoadObject<UStaticMesh>(nullptr, *KV.Value->AsString())) Models.Add(FString(KV.Key), M);
	UE_LOG(LogTrendo, Display, TEXT("TRENDO-MODELLE %d Spielobjekt-Modelle, Mensch=%s, Wagen=%s"), Models.Num(), TrendoHuman() ? TEXT("ja") : TEXT("nein"), TrendoCar() ? TEXT("ja") : TEXT("nein"));
}

/** Pflanzen, Bäume, Felsen und Holz aus Content/Trendo/natur.json (von Trendo eingerichtet) auf dem Gelände verteilen – feste Zufallsfolge, Spielweg bleibt frei. */
void ATrendoGameMode::SpawnNature()
{
	FString Json; if (!FFileHelper::LoadFileToString(Json, *(FPaths::ProjectContentDir() / TEXT("Trendo/natur.json")))) return;
	TSharedPtr<FJsonObject> O; if (!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json), O) || !O.IsValid()) return;
	TMap<FString, TArray<UStaticMesh*>> Kinds;
	const TArray<TSharedPtr<FJsonValue>>* Arr; if (!O->TryGetArrayField(TEXT("nature"), Arr)) return;
	for (const auto& V : *Arr)
		if (const TSharedPtr<FJsonObject>& E = V->AsObject(); E.IsValid())
			if (UStaticMesh* M = LoadObject<UStaticMesh>(nullptr, *E->GetStringField(TEXT("mesh")))) Kinds.FindOrAdd(E->GetStringField(TEXT("kind"))).Add(M);
	if (Kinds.Num() == 0) return;
	AActor* Holder = GetWorld()->SpawnActor<AActor>(); Holder->SetRootComponent(NewObject<USceneComponent>(Holder, TEXT("Natur"))); Holder->GetRootComponent()->RegisterComponent();
	TMap<UStaticMesh*, UHierarchicalInstancedStaticMeshComponent*> Comps;
	FRandomStream R(4711); NatureCount = 0;
	struct FKindRule { const TCHAR* Kind; int32 Count; float Radius; float MinScale, MaxScale; bool bSolid; float Clear; };
	const FKindRule Rules[] = { { TEXT("gras"), 120000, 4500, 1.1f, 2.0f, false, 0 }, { TEXT("farn"), 2500, 6500, 0.8f, 1.5f, false, 0 }, { TEXT("strauch"), 1200, 9000, 0.9f, 1.8f, false, 250 },
		{ TEXT("baum"), 2000, 25000, 1.0f, 2.2f, true, 450 }, { TEXT("stein"), 260, 10000, 0.6f, 2.2f, true, 350 }, { TEXT("holz"), 60, 9000, 0.8f, 1.3f, true, 350 } };
	const FVector Fwd = StartRot.Vector(); const float PathLen = 400.f + (Data.Levels.Num() > 0 ? Data.Levels[0].Rows[0].Len() * 300.f : 3000.f);
	FCollisionQueryParams Q; if (APawn* H = UGameplayStatics::GetPlayerPawn(this, 0)) Q.AddIgnoredActor(H);
	for (const FKindRule& Rule : Rules)
	{
		const TArray<UStaticMesh*>* Meshes = Kinds.Find(Rule.Kind); if (!Meshes || Meshes->Num() == 0) continue;
		for (int32 i = 0; i < Rule.Count; i++)
		{
			const float A = R.FRandRange(0, 2 * PI), D = Rule.Radius * FMath::Sqrt(R.FRand());
			FVector P = Origin + FVector(FMath::Cos(A) * D, FMath::Sin(A) * D, 0);
			const FVector Rel = P - Origin; const float Along = FVector::DotProduct(Rel, Fwd), Side = FVector::CrossProduct(Fwd, Rel).Size();
			if (Rule.Clear > 0 && Along > -1200.f && Along < PathLen && Side < Rule.Clear + 300.f) continue; // Spielweg frei
			FHitResult Hit; if (!GetWorld()->LineTraceSingleByChannel(Hit, P + FVector(0, 0, 30000), P - FVector(0, 0, 30000), ECC_WorldStatic, Q)) continue;
			UStaticMesh* M = (*Meshes)[R.RandHelper(Meshes->Num())];
			UHierarchicalInstancedStaticMeshComponent*& C = Comps.FindOrAdd(M);
			if (!C)
			{
				C = NewObject<UHierarchicalInstancedStaticMeshComponent>(Holder); C->SetStaticMesh(M); C->SetupAttachment(Holder->GetRootComponent());
				C->SetCollisionEnabled(Rule.bSolid ? ECollisionEnabled::QueryAndPhysics : ECollisionEnabled::NoCollision);
				C->SetCastShadow(true); if (!Rule.bSolid) C->SetCullDistances(0, 9000); C->RegisterComponent();
			}
			const float S = R.FRandRange(Rule.MinScale, Rule.MaxScale);
			C->AddInstance(FTransform(FRotator(0, R.FRandRange(0, 360), 0), Hit.Location - FVector(0, 0, 5), FVector(S)), true);
			NatureCount++;
		}
	}
	UE_LOG(LogTrendo, Display, TEXT("TRENDO-NATUR %d Pflanzen/Felsen in %d Arten"), NatureCount, Comps.Num());
	// Licht wie draußen am Vormittag: Sonne schräg hinter der Kamera, leichter Dunst in der Ferne
	for (TActorIterator<ADirectionalLight> L(GetWorld()); L; ++L) { L->GetRootComponent()->SetMobility(EComponentMobility::Movable); L->SetActorRotation(FRotator(-38.f, StartRot.Yaw + 35.f, 0)); }
	for (TActorIterator<AExponentialHeightFog> F(GetWorld()); F; ++F) if (UExponentialHeightFogComponent* C = F->GetComponent()) { C->SetFogDensity(0.035f); C->SetFogHeightFalloff(0.15f); }
}

void ATrendoGameMode::Report()
{
	ATrendoCharacter* H = Hero();
	const bool bGround = H && H->GetCharacterMovement()->IsMovingOnGround();
	int32 Live = 0; for (AActor* A : Spawned) if (IsValid(A)) Live++;
	UE_LOG(LogTrendo, Display, TEXT("TRENDO-SPIEL Welt=%s Level=%d Objekte=%d Gegner=%d Natur=%d Figur=%s Boden=%s Z=%.0f"), *Data.World, State.Level, Live, Enemies.Num(), NatureCount, H ? TEXT("ja") : TEXT("nein"), bGround ? TEXT("ja") : TEXT("nein"), H ? H->GetActorLocation().Z : 0.f);
	for (AActor* A : Spawned) if (ATrendoThing* T = Cast<ATrendoThing>(A); IsValid(T))
		UE_LOG(LogTrendo, Display, TEXT("TRENDO-OBJEKT %s Modell=%s %s"), *T->Tile, T->bModel ? TEXT("ja") : TEXT("nein"), T->Mesh->GetStaticMesh() ? *T->Mesh->GetStaticMesh()->GetName() : T->Figure->GetSkeletalMeshAsset() ? *T->Figure->GetSkeletalMeshAsset()->GetName() : TEXT("-"));
	UE_LOG(LogTrendo, Display, TEXT("%s"), H && bGround && Live > 0 ? TEXT("TRENDO-OK Spiel läuft, Figur steht") : TEXT("TRENDO-FEHLER Spiel nicht spielbar"));
	FGenericPlatformMisc::RequestExit(false);
}

void ATrendoHUD::DrawHUD()
{
	Super::DrawHUD();
	ATrendoGameMode* GM = GetWorld() ? GetWorld()->GetAuthGameMode<ATrendoGameMode>() : nullptr; if (!GM || !Canvas) return;
	UFont* Font = GEngine->GetMediumFont(); const float S = GM->bBig ? 1.6f : 1.15f;
	const FLinearColor Accent = GM->bContrast ? FLinearColor::Yellow : FLinearColor(1.f, 0.8f, 0.1f);
	if (GM->Playing())
	{
		const ATrendoCharacter* H = GM->Hero();
		FString T = FString::Printf(TEXT("Punkte %d   Leben %d"), GM->State.Score, GM->State.Lives);
		if (GM->Data.Levels.Num() > 1) T += FString::Printf(TEXT("   Level %d/%d"), GM->State.Level + 1, GM->Data.Levels.Num());
		if (H && H->Vehicle == TEXT("ufo")) T += FString::Printf(TEXT("   Treibstoff %d s"), FMath::CeilToInt(H->Fuel));
		if (GM->State.Inventory.Num() > 0) T += TEXT("\nInventar: ") + FString::Join(GM->State.Inventory, TEXT(", "));
		T += TEXT("\n") + GM->State.Quests(GM->Data) + GM->State.Note;
		if (GM->bContrast) DrawRect(FLinearColor(0, 0, 0, 0.8f), 0, 0, 640 * S, 140 * S);
		DrawText(T, FLinearColor::White, 20, 20, Font, S);
		return;
	}
	DrawRect(FLinearColor(0, 0, 0, GM->bContrast ? 0.94f : 0.8f), 0, 0, Canvas->SizeX, Canvas->SizeY);
	const FString Title = GM->Mode == TEXT("menu") ? GM->Data.Name : GM->Mode == TEXT("pause") ? FString(TEXT("Pause")) : GM->Mode == TEXT("settings") ? FString(TEXT("Einstellungen")) : FString(TEXT("Steuerung"));
	float W, Hh; GetTextSize(Title, W, Hh, Font, S * 2.f); DrawText(Title, FLinearColor::White, (Canvas->SizeX - W) / 2, 80, Font, S * 2.f);
	float Y = 180;
	if (GM->Mode == TEXT("help"))
		for (const TCHAR* L : { TEXT("WASD oder Pfeile laufen · Maus umsehen · Leertaste springen · Umschalt sprinten"), TEXT("X, J oder linke Maustaste angreifen · E aussteigen · Esc Pause"), TEXT("Controller: Stick laufen · A springen · X angreifen · B aussteigen · Start Pause") })
		{ GetTextSize(L, W, Hh, Font, S); DrawText(L, FLinearColor::White, (Canvas->SizeX - W) / 2, Y, Font, S); Y += 34 * S; }
	const TArray<FString> It = GM->MenuItems();
	for (int32 i = 0; i < It.Num(); i++)
	{
		GetTextSize(It[i], W, Hh, Font, S * 1.3f);
		if (i == GM->Sel) DrawRect(Accent, Canvas->SizeX / 2 - 200, Y - 6, 400, Hh + 12);
		DrawText(It[i], i == GM->Sel ? FLinearColor::Black : FLinearColor::White, (Canvas->SizeX - W) / 2, Y, Font, S * 1.3f);
		Y += Hh + 22;
	}
}
""",
        ["TrendoTests.cpp"] = """
// Von Trendo erzeugt: Unreal-Automatisierungstests (Session Frontend → Automation, oder Kommandozeile „Automation RunTests Trendo“).
#include "Misc/AutomationTest.h"
#include "TrendoTypes.h"
#include "TrendoGameMode.h"
#include "Kismet/GameplayStatics.h"

#if WITH_DEV_AUTOMATION_TESTS

static const TCHAR* TrendoTestJson = TEXT("{\"name\":\"Test\",\"world\":\"openworld\",\"speed\":5,\"lives\":3,\"weapon\":\"schwert\",\"menu\":true,\"abilities\":[\"doppelsprung\"],")
	TEXT("\"levels\":[{\"name\":\"L\",\"rows\":[\"..........\",\"SGNE.V...Z\",\"##########\"]}],\"props\":[{\"key\":\"3,1:hp\",\"value\":2}],")
	TEXT("\"texts\":[{\"key\":\"1,1:name\",\"value\":\"Apfel\"},{\"key\":\"2,1:need\",\"value\":\"Apfel\"},{\"key\":\"2,1:thanks\",\"value\":\"Lecker!\"},{\"key\":\"2,1:give\",\"value\":\"leben\"}],")
	TEXT("\"rules\":[{\"when\":\"gegenstaende\",\"n\":1,\"act\":\"punkte\",\"title\":\"Finder\"}]}");

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTrendoDatenTest, "Trendo.Daten", EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FTrendoDatenTest::RunTest(const FString& Parameters)
{
	FTrendoData D; FString Error;
	TestTrue(TEXT("level.json wird gelesen"), FTrendoData::Parse(TrendoTestJson, D, Error));
	TestEqual(TEXT("Welt"), D.World, FString(TEXT("openworld")));
	TestTrue(TEXT("Doppelsprung"), D.Has(TEXT("doppelsprung")));
	TestEqual(TEXT("Waffe"), D.Weapon, FString(TEXT("schwert")));
	TestEqual(TEXT("Boss-Trefferpunkte"), D.Prop(0, 3, 1, TEXT("hp"), 1), 2.f);
	TestEqual(TEXT("Gegenstand"), D.Text(0, 1, 1, TEXT("name"), TEXT("Kristall")), FString(TEXT("Apfel")));
	FTrendoData Bad;
	TestFalse(TEXT("kaputte Datei abgelehnt"), FTrendoData::Parse(TEXT("{kaputt"), Bad, Error));
	TestFalse(TEXT("ohne Level abgelehnt"), FTrendoData::Parse(TEXT("{\"levels\":[]}"), Bad, Error));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTrendoRegelnTest, "Trendo.Regeln", EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FTrendoRegelnTest::RunTest(const FString& Parameters)
{
	FTrendoData D; FString Error; FTrendoData::Parse(TrendoTestJson, D, Error);
	FTrendoState S; S.Reset(D);
	TestEqual(TEXT("Aufgabe offen"), S.Quests(D), FString(TEXT("Finder: 0/1\n")));
	S.TalkTo(D, 2, 1); TestEqual(TEXT("Figur ohne Apfel"), S.Lives, 3); TestEqual(TEXT("sagt Hallo"), S.Note, FString(TEXT("Hallo!")));
	S.Inventory.Add(TEXT("Apfel")); S.Items++;
	TestEqual(TEXT("Regel erfüllt"), S.CheckRules(D), 1); TestEqual(TEXT("Belohnung"), S.Score, 10);
	TestEqual(TEXT("Aufgabe erledigt"), S.Quests(D), FString(TEXT("✓ Finder\n")));
	S.TalkTo(D, 2, 1); TestEqual(TEXT("Figur belohnt"), S.Lives, 4); TestEqual(TEXT("Dank"), S.Note, FString(TEXT("Lecker!"))); TestEqual(TEXT("Apfel abgegeben"), S.Inventory.Num(), 0);
	TestEqual(TEXT("Regel feuert nur einmal"), S.CheckRules(D), 0);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTrendoSpielstandTest, "Trendo.Spielstand", EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FTrendoSpielstandTest::RunTest(const FString& Parameters)
{
	UTrendoSaveGame* S = Cast<UTrendoSaveGame>(UGameplayStatics::CreateSaveGameObject(UTrendoSaveGame::StaticClass()));
	S->Level = 1; S->Score = 77; S->Lives = 2; S->Inventory.Add(TEXT("Apfel"));
	TestTrue(TEXT("gespeichert"), UGameplayStatics::SaveGameToSlot(S, TEXT("TrendoTest"), 0));
	UTrendoSaveGame* L = Cast<UTrendoSaveGame>(UGameplayStatics::LoadGameFromSlot(TEXT("TrendoTest"), 0));
	TestNotNull(TEXT("geladen"), L);
	if (L) { TestEqual(TEXT("Level"), L->Level, 1); TestEqual(TEXT("Punkte"), L->Score, 77); TestEqual(TEXT("Inventar"), L->Inventory.Num(), 1); TestEqual(TEXT("Format"), L->Version, 1); }
	UGameplayStatics::DeleteGameInSlot(TEXT("TrendoTest"), 0);
	return true;
}

#endif
""",
    };
}
