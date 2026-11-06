using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Zustand einer Engine-Fähigkeit nach Paket 01 (Unity-Adapter): nie mehr behaupten als geprüft.</summary>
public enum EngineCapState { NotPresent, Planned, Implemented, Tested, Blocked }

/// <summary>Gefundene Engine mit ehrlichem Zustand (nur aus Dateien und echtem Lauf, nie angenommen).</summary>
public sealed record EngineFound(string Id, string Name, string? Version, string? Exe, bool Installed, string Status, string Hint);

/// <summary>Paket 01 / S1-01 „Universal Game Engine“ (Freigabe 05.11.2026, Unity zusätzlich bestätigt): Engines erkennen (Unreal, Unity, Godot, Blender),
/// ein Trendo-Spiel als echtes Engine-Projekt anlegen (Unity: C#-Spiel, Editor-Build, EditMode-/PlayMode-Tests; Godot: GDScript-Spiel und Prüfskript)
/// und die offiziellen Kommandozeilen bauen. Nichts wird heruntergeladen oder installiert; Läufe nur, wenn die Engine wirklich vorhanden ist.</summary>
public static class EngineTargets
{
    public static readonly (string Id, string Name, string License, string Source)[] Engines =
    [
        ("unreal", "Unreal Engine", "Epic EULA – über den Epic Games Launcher", "https://www.unrealengine.com/download"),
        ("unity", "Unity", "Unity-Lizenz (Personal kostenlos bis zur Umsatzgrenze) – über Unity Hub, Anmeldung durch dich", "https://unity.com/download"),
        ("godot", "Godot Engine", "MIT – frei", "https://godotengine.org/download"),
        ("blender", "Blender", "GPL – frei", "https://www.blender.org/download/"),
    ];

    /// <summary>Fähigkeiten des Unity-Adapters (Namen wie im Paket).</summary>
    public static readonly string[] UnityCaps = ["unity.project.create", "unity.project.open", "unity.scene.edit", "unity.import.3d", "unity.audio.import", "unity.build.local", "unity.test.editmode", "unity.test.playmode", "unity.preview.local"];

    /// <summary>Zustand je Unity-Fähigkeit: ohne Editor ist alles, was Unity braucht, blockiert; das Anlegen des Projekts geht immer.</summary>
    public static Dictionary<string, EngineCapState> UnityCapStates(bool installed, bool testsPassed) => UnityCaps.ToDictionary(c => c, c => c switch
    {
        "unity.project.create" => testsPassed ? EngineCapState.Tested : EngineCapState.Implemented,
        "unity.scene.edit" or "unity.audio.import" or "unity.import.3d" => installed ? (testsPassed ? EngineCapState.Tested : EngineCapState.Implemented) : EngineCapState.Implemented,
        _ => installed ? (testsPassed ? EngineCapState.Tested : EngineCapState.Implemented) : EngineCapState.Blocked,
    });

    public static string CapLabel(EngineCapState s) => s switch { EngineCapState.Tested => "geprüft", EngineCapState.Implemented => "eingebaut", EngineCapState.Planned => "geplant", EngineCapState.Blocked => "blockiert", _ => "fehlt" };

    // ───────────── Erkennen ─────────────
    /// <summary>Unity-Hub-Ordner für Editoren: Standard plus der in Unity Hub eingestellte Zusatzpfad (secondaryInstallPath.json).</summary>
    public static List<string> UnityRoots(string programFiles, string appData, Func<string, string?> read)
    {
        var roots = new List<string> { Path.Combine(programFiles, "Unity", "Hub", "Editor") };
        if (read(Path.Combine(appData, "UnityHub", "secondaryInstallPath.json")) is { } j && ParseSecondaryInstallPath(j) is { Length: > 0 } p) roots.Add(p);
        return roots;
    }

    /// <summary>Unity Hub speichert den Zusatzpfad als JSON-Zeichenkette, z. B. "D:\\Unity".</summary>
    public static string? ParseSecondaryInstallPath(string json)
    {
        try { return JsonNode.Parse(json) is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null; } catch (JsonException) { return null; }
    }

    /// <summary>Installierte Unity-Editoren (Hub-Ordner/&lt;Version&gt;/Editor/Unity.exe), neueste zuerst.</summary>
    public static List<(string Version, string Exe)> FindUnity(IEnumerable<string> roots, Func<string, IEnumerable<string>> dirs, Func<string, bool> exists)
    {
        var list = new List<(string, string)>();
        foreach (var r in roots)
            foreach (var d in dirs(r))
            {
                var exe = OperatingSystem.IsMacOS() ? Path.Combine(d, "Unity.app", "Contents", "MacOS", "Unity") : Path.Combine(d, "Editor", OperatingSystem.IsWindows() ? "Unity.exe" : "Unity");
                if (exists(exe)) list.Add((Path.GetFileName(d.TrimEnd('/', '\\')), exe));
            }
        return [.. list.DistinctBy(x => x.Item2).OrderByDescending(x => UnityOrder(x.Item1))];
    }

    /// <summary>Sortierschlüssel für Unity-Versionen wie „6000.0.23f1“ oder „2022.3.45f1“.</summary>
    public static Version UnityOrder(string v) { var m = Regex.Match(v, @"^(\d+)\.(\d+)\.(\d+)"); return m.Success ? new(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)) : new(0, 0); }

    /// <summary>Editor-Version eines Unity-Projekts aus ProjectSettings/ProjectVersion.txt.</summary>
    public static string? ReadProjectVersion(string text) => Regex.Match(text, @"m_EditorVersion:\s*(\S+)") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Godot-Programme (Godot_v4…exe) in den üblichen Ordnern und im eigenen Werkzeugordner; neueste zuerst.</summary>
    public static List<(string Version, string Exe)> FindGodot(IEnumerable<string> dirs, Func<string, IEnumerable<string>> files)
    {
        var list = new List<(string, string)>();
        foreach (var d in dirs)
            foreach (var f in files(d))
                if (GodotVersionFromName(Path.GetFileName(f)) is { } v && !f.Contains("_console", StringComparison.OrdinalIgnoreCase)) list.Add((v, f));
        return [.. list.DistinctBy(x => x.Item2).OrderByDescending(x => Version.TryParse(Regex.Match(x.Item1, @"^\d+(\.\d+){1,2}").Value, out var v) ? v : new Version(0, 0))];
    }

    /// <summary>„Godot_v4.4.1-stable_win64.exe“ → „4.4.1-stable“; nur Godot 4 (ältere Projekte sind anders aufgebaut).</summary>
    public static string? GodotVersionFromName(string file) => Regex.Match(file, @"^Godot_v(4\.[\w.\-]+?)(_mono)?_(win64|win32|linux\.x86_64|linux_x86_64|macos\.universal)(\.exe)?$", RegexOptions.IgnoreCase) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Gesamtbild aller Engines aus den Funden (ohne Prozess-Start).</summary>
    public static List<EngineFound> Summary(IReadOnlyList<(string Version, string Path)> unreal, Func<string, string> unrealExe, IReadOnlyList<(string Version, string Exe)> unity,
        IReadOnlyList<(string Version, string Exe)> godot, string? blenderExe, Func<string, bool> exists)
    {
        var r = new List<EngineFound>();
        var ue = unreal.FirstOrDefault(e => exists(unrealExe(e.Path)));
        r.Add(ue.Path is null ? new("unreal", "Unreal Engine", null, null, false, "nicht installiert", "Kostenlos über den Epic Games Launcher (eigenes Konto)")
            : new("unreal", "Unreal Engine", ue.Version, unrealExe(ue.Path), true, "bereit", "Projekte mit Spielbibel; Python nur zur Editor-Automatisierung"));
        r.Add(unity.Count == 0 ? new("unity", "Unity", null, null, false, "blockiert: nicht installiert", "Unity Hub und Editor installierst du selbst (Anmeldung bei Unity). Trendo legt das Projekt trotzdem an.")
            : new("unity", "Unity", unity[0].Version, unity[0].Exe, true, "bereit", "C#-Spiel, Editor-Build, EditMode-/PlayMode-Tests über die Kommandozeile"));
        r.Add(godot.Count == 0 ? new("godot", "Godot Engine", null, null, false, "nicht gefunden", "Frei (MIT). Godot_v4…exe in den Werkzeugordner legen oder unter Programme einrichten.")
            : new("godot", "Godot Engine", godot[0].Version, godot[0].Exe, true, "bereit", "GDScript-Spiel und Prüfung ohne Fenster"));
        r.Add(blenderExe is null ? new("blender", "Blender", null, null, false, "nicht installiert", "Frei (GPL) – für 3D-Figuren und Kulissen")
            : new("blender", "Blender", null, blenderExe, true, "bereit", "3D-Assets als GLB/FBX für alle Engines"));
        return r;
    }

    // ───────────── Spiel als Engine-Projekt ─────────────
    /// <summary>Engine-neutrale Beschreibung des Trendo-Spiels (Unity JsonUtility-tauglich: keine Wörterbücher, keine verschachtelten Felder).</summary>
    public static string LevelJson(string name, GameStudio.GameSettings s)
    {
        var levels = new List<object>();
        var all = new List<string[]> { s.Level.Length > 0 ? s.Level : GameStudio.DefaultPlatform }; all.AddRange(s.MoreLevels);
        for (var i = 0; i < all.Count; i++)
            levels.Add(new { name = i < s.LevelNames.Count ? s.LevelNames[i] : $"Level {i + 1}", rows = all[i], decor = i < s.Decor.Count ? s.Decor[i] : Array.Empty<string>() });
        var o = new
        {
            name, speed = s.Speed, lives = s.Lives, winScore = s.WinScore, timeLimit = s.TimeLimit,
            player = s.Player, accent = s.Accent, background = s.Background, levels,
            props = s.Props.Select(kv => new { key = kv.Key, value = kv.Value }),
            rules = s.Rules.Select(r => new { when = r.When, n = r.N, act = r.Do, title = r.Title ?? "" }),
            abilities = s.Abilities, weapon = s.Weapon,
            texts = s.Texts.Select(kv => new { key = kv.Key, value = kv.Value }),
        };
        return JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true });
    }

    static string NewProjectDir(string parent, string name, string suffix)
    {
        var safe = new string(DevStudio.SafeName(name).Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        if (safe.Length == 0) safe = "Spiel";
        var dir = Path.Combine(parent, safe + suffix);
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{Path.GetFileName(dir)}“ gibt es schon – erst umbenennen oder löschen.");
        return dir;
    }

    static void Put(string root, string rel, string text) { var f = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, text.Replace("\r\n", "\n")); }

    /// <summary>Unity-Projekt: Spiel in C# (lädt das Trendo-Level zur Laufzeit), Editor-Build-Skript, EditMode- und PlayMode-Tests (Unity Test Framework).
    /// Die Szene legt der Editor selbst an (TrendoBuild.EnsureScene), damit keine handgeschriebene Szenen-Datei kaputt sein kann.</summary>
    public static string CreateUnityProject(string parent, string name, GameStudio.GameSettings s, string? editorVersion = null)
    {
        var dir = NewProjectDir(parent, name, "-Unity");
        Put(dir, "Assets/Resources/trendo-level.json", LevelJson(name, s));
        Put(dir, "Assets/Scripts/TrendoGame.asmdef", """{ "name": "TrendoGame", "references": [], "autoReferenced": true }""");
        Put(dir, "Assets/Scripts/TrendoLevelData.cs", UnityLevelData);
        Put(dir, "Assets/Scripts/TrendoLevel.cs", UnityLevel);
        Put(dir, "Assets/Scripts/TrendoPlayer.cs", UnityPlayer);
        Put(dir, "Assets/Editor/TrendoBuild.cs", UnityBuild);
        Put(dir, "Assets/Tests/EditMode/TrendoTests.EditMode.asmdef", """{ "name": "TrendoTests.EditMode", "references": ["TrendoGame", "UnityEngine.TestRunner", "UnityEditor.TestRunner"], "includePlatforms": ["Editor"], "overrideReferences": true, "precompiledReferences": ["nunit.framework.dll"], "autoReferenced": false, "defineConstraints": ["UNITY_INCLUDE_TESTS"] }""");
        Put(dir, "Assets/Tests/EditMode/TrendoLevelTests.cs", UnityEditTests);
        Put(dir, "Assets/Tests/PlayMode/TrendoTests.PlayMode.asmdef", """{ "name": "TrendoTests.PlayMode", "references": ["TrendoGame", "UnityEngine.TestRunner"], "includePlatforms": [], "overrideReferences": true, "precompiledReferences": ["nunit.framework.dll"], "autoReferenced": false, "defineConstraints": ["UNITY_INCLUDE_TESTS"] }""");
        Put(dir, "Assets/Tests/PlayMode/TrendoPlayTests.cs", UnityPlayTests);
        Put(dir, "Assets/Tests/PlayMode/TrendoGameplayTests.cs", UnityGameplayTests);
        Put(dir, "Packages/manifest.json", """{ "dependencies": { "com.unity.test-framework": "1.4.5", "com.unity.modules.physics2d": "1.0.0", "com.unity.modules.jsonserialize": "1.0.0", "com.unity.modules.audio": "1.0.0", "com.unity.modules.imgui": "1.0.0" }, "testables": [] }""");
        if (editorVersion is { Length: > 0 }) Put(dir, "ProjectSettings/ProjectVersion.txt", $"m_EditorVersion: {editorVersion}\n");
        Put(dir, ".gitignore", "Library/\nTemp/\nLogs/\nobj/\nBuild/\nUserSettings/\n");
        Put(dir, "LIESMICH.txt", $"{name} – von Trendo als Unity-Projekt angelegt.\nÖffnen: Unity Hub → Hinzufügen → diesen Ordner wählen. Das Spiel startet in jeder Szene (TrendoLevel.Boot).\nBauen: Menü Trendo → Windows-Build. Tests: Window → General → Test Runner.\nDas Level steht in Assets/Resources/trendo-level.json. Braucht Unity 6 (6000.x).\n");
        return dir;
    }

    /// <summary>Godot-4-Projekt: Hauptszene mit GDScript-Spiel (lädt das Trendo-Level) und ein Prüfskript für die Kommandozeile.</summary>
    public static string CreateGodotProject(string parent, string name, GameStudio.GameSettings s)
    {
        var dir = NewProjectDir(parent, name, "-Godot");
        Put(dir, "level.json", LevelJson(name, s));
        Put(dir, "project.godot", $"; Von Trendo angelegt\nconfig_version=5\n\n[application]\n\nconfig/name=\"{name.Replace("\"", "'")}\"\nrun/main_scene=\"res://main.tscn\"\nconfig/features=PackedStringArray(\"4.2\")\n\n[display]\n\nwindow/size/viewport_width=1280\nwindow/size/viewport_height=720\n\n[physics]\n\ncommon/physics_ticks_per_second=60\n");
        Put(dir, "main.tscn", "[gd_scene load_steps=2 format=3]\n\n[ext_resource type=\"Script\" path=\"res://trendo_level.gd\" id=\"1\"]\n\n[node name=\"Main\" type=\"Node2D\"]\nscript = ExtResource(\"1\")\n");
        Put(dir, "trendo_level.gd", GodotLevel);
        Put(dir, "trendo_player.gd", GodotPlayer);
        Put(dir, "tests/check_level.gd", GodotCheck);
        Put(dir, "tests/play_check.gd", GodotPlayCheck);
        Put(dir, "tests/save_check.gd", GodotSaveCheck);
        Put(dir, "trendo_shot.gd", GodotShot);
        Put(dir, "tests/gameplay_check.gd", GodotGameplayCheck);
        Put(dir, ".gitignore", ".godot/\nexport/\n");
        Put(dir, "LIESMICH.txt", $"{name} – von Trendo als Godot-4-Projekt angelegt.\nÖffnen: Godot → Importieren → project.godot. Start mit F5.\nPrüfen ohne Fenster: godot --headless --path . --script res://tests/check_level.gd\n");
        return dir;
    }

    // ───────────── Kommandozeilen (offiziell) ─────────────
    /// <summary>Spielstand-Prüfung in Godot: speichern, fortsetzen, neuere Fassung und kaputte Datei ablehnen.</summary>
    public static List<string> GodotSaveArgs(string project) => ["--headless", "--path", project, "--script", "res://tests/save_check.gd"];
    /// <summary>Gameplay-Prüfung in Godot: Inventar, Figur, Aufgabe, Schwert, Boss, Fahrzeug, Doppelsprung.</summary>
    public static List<string> GodotGameplayArgs(string project) => ["--headless", "--path", project, "--script", "res://tests/gameplay_check.gd"];
    /// <summary>Unity-Tests: -runTests beendet den Editor selbst (deshalb ohne -quit).</summary>
    public static List<string> UnityTestArgs(string project, string platform, string results, string log) =>
        ["-batchmode", "-nographics", "-projectPath", project, "-runTests", "-testPlatform", platform == "PlayMode" ? "PlayMode" : "EditMode", "-testResults", results, "-logFile", log];
    public static List<string> UnityBuildArgs(string project, string log) => ["-batchmode", "-quit", "-projectPath", project, "-executeMethod", "TrendoBuild.BuildWindows", "-logFile", log];
    public static List<string> GodotCheckArgs(string project) => ["--headless", "--path", project, "--script", "res://tests/check_level.gd"];
    public static List<string> GodotImportArgs(string project) => ["--headless", "--path", project, "--import"];
    /// <summary>Spielprüfung wie ein PlayMode-Test: Hauptszene 2 Sekunden ohne Fenster, Figur muss stehen.</summary>
    public static List<string> GodotPlayArgs(string project) => ["--headless", "--path", project, "--script", "res://tests/play_check.gd"];

    /// <summary>Ergebnis eines Unity-Testlaufs (NUnit-3-XML: &lt;test-run total passed failed&gt;).</summary>
    public static (int Total, int Passed, int Failed)? ParseUnityResults(string xml)
    {
        var m = Regex.Match(xml, "<test-run\\b[^>]*>");
        if (!m.Success) return null;
        int A(string n) => Regex.Match(m.Value, n + "=\"(\\d+)\"") is { Success: true } a ? int.Parse(a.Groups[1].Value) : 0;
        return (A("total"), A("passed"), A("failed"));
    }

    /// <summary>Godot-Prüfskript meldet „TRENDO-OK n Level“ oder „TRENDO-FEHLER …“.</summary>
    public static (bool Ok, string Text) ParseGodotCheck(string output)
    {
        var line = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("TRENDO-")) ?? "";
        return line.StartsWith("TRENDO-OK") ? (true, line["TRENDO-OK".Length..].Trim()) : line.StartsWith("TRENDO-FEHLER") ? (false, line["TRENDO-FEHLER".Length..].Trim()) : (false, "Keine Prüfmeldung von Godot.");
    }

    // ───────────── Unity-Quelltexte (eigene, Clean-Room) ─────────────
    const string UnityLevelData = """
using System;
using UnityEngine;

/// <summary>Trendo-Level wie es Trendo exportiert (JsonUtility-tauglich).</summary>
[Serializable] public class TrendoLevelRow { public string name; public string[] rows; public string[] decor; }
[Serializable] public class TrendoProp { public string key; public float value; }
[Serializable] public class TrendoRule { public string when; public int n; public string act; public string title; }
[Serializable] public class TrendoText { public string key; public string value; }

/// <summary>Spielstand – gleiches Format wie in Trendo und im Browser-Spiel (Format 1).</summary>
[Serializable] public class TrendoSave
{
    public int version = 1; public int level; public int score; public int lives = 3; public int checkX = -1; public int checkY = -1; public int coins; public int defeated; public int playSeconds; public string savedUtc;
    public static string DefaultPath => System.IO.Path.Combine(Application.persistentDataPath, "trendo-spielstand.json");
    public static void Write(string path, TrendoSave s) { s.version = 1; s.savedUtc = DateTime.UtcNow.ToString("o"); System.IO.File.WriteAllText(path, JsonUtility.ToJson(s, true)); }
    /// <summary>Lesen und prüfen; null bei fehlender, kaputter, neuerer oder unpassender Datei.</summary>
    public static TrendoSave Read(string path, int levelCount)
    {
        if (!System.IO.File.Exists(path)) return null;
        TrendoSave s; try { s = JsonUtility.FromJson<TrendoSave>(System.IO.File.ReadAllText(path)); } catch (ArgumentException) { return null; }
        if (s == null || s.version > 1 || s.level < 0 || s.level >= levelCount) return null;
        s.lives = Mathf.Clamp(s.lives, 1, 99); s.score = Mathf.Max(0, s.score); return s;
    }
}
[Serializable] public class TrendoLevelData
{
    public string name; public int speed = 5; public int lives = 3; public int winScore; public int timeLimit;
    public string player = "#4FB3FF"; public string accent = "#FACC15"; public string background = "#10203F";
    public TrendoLevelRow[] levels; public TrendoProp[] props; public TrendoRule[] rules;
    public string[] abilities; public string weapon; public TrendoText[] texts;

    static string Key(int level, int x, int y, string k) => (level == 0 ? "" : (level + 1) + ":") + x + "," + y + ":" + k;
    /// <summary>Objekt-Text (Gegenstand, Figur, Fahrzeug) wie in Trendo, sonst Vorgabe.</summary>
    public string Text(int level, int x, int y, string k, string def) { var key = Key(level, x, y, k); if (texts != null) foreach (var t in texts) if (t.key == key && !string.IsNullOrEmpty(t.value)) return t.value; return def; }
    public float Prop(int level, int x, int y, string k, float def) { var key = Key(level, x, y, k); if (props != null) foreach (var p in props) if (p.key == key) return p.value; return def; }
    public bool Has(string ability) => abilities != null && Array.IndexOf(abilities, ability) >= 0;

    public static TrendoLevelData Parse(string json) => JsonUtility.FromJson<TrendoLevelData>(json);
    public static TrendoLevelData Load() { var t = Resources.Load<TextAsset>("trendo-level"); return t == null ? null : Parse(t.text); }

    /// <summary>Start (S) eines Levels in Kachel-Koordinaten (x, y von oben), sonst (-1, -1).</summary>
    public Vector2Int Start(int level)
    {
        var rows = levels[level].rows;
        for (var y = 0; y < rows.Length; y++) { var x = rows[y].IndexOf('S'); if (x >= 0) return new Vector2Int(x, y); }
        return new Vector2Int(-1, -1);
    }
    public int Count(int level, char tile) { var n = 0; foreach (var r in levels[level].rows) foreach (var c in r) if (c == tile) n++; return n; }
    public static Color Hex(string h, Color fallback) => ColorUtility.TryParseHtmlString(h, out var c) ? c : fallback;
}
""";

    const string UnityLevel = """
using System.Collections.Generic;
using UnityEngine;

/// <summary>Baut das Trendo-Level als Spielszene: Boden, Münzen, Gegner (mit Trefferpunkten), Stacheln, Sprungfedern, Schlüssel und Türen,
/// Gegenstände, sprechende Figuren, Fahrzeuge, Ziel – mit Regeln und Aufgaben wie im Trendo-Spiel.</summary>
public class TrendoLevel : MonoBehaviour
{
    public TrendoLevelData Data; public int Index; public int Coins, Keys, Score, Lives, Defeated, ItemsCollected; public TrendoPlayer Player;
    public readonly List<string> Inventory = new List<string>();
    public string Note = ""; public float Boost; public bool Won, Over;
    static Sprite _square;
    readonly List<GameObject> _doors = new List<GameObject>();
    readonly HashSet<string> _given = new HashSet<string>();
    bool[] _fired; float _time;

    /// <summary>Startet das Spiel in jeder Szene – auch in der leeren Standardszene.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<TrendoLevel>() != null) return; var d = TrendoLevelData.Load(); if (d == null) return;
        var game = new GameObject("Trendo-Spiel").AddComponent<TrendoLevel>();
        var save = TrendoSave.Read(TrendoSave.DefaultPath, d.levels.Length);
        if (save != null) game.Resume(d, save); else game.Begin(d, 0);
    }

    public string SavePath;
    string FilePath => string.IsNullOrEmpty(SavePath) ? TrendoSave.DefaultPath : SavePath;
    public TrendoSave Snapshot() => new TrendoSave { level = Index, score = Score, lives = Lives, coins = Coins, defeated = Defeated, checkX = Player != null ? Mathf.RoundToInt(Player.Checkpoint.x) : -1, checkY = Player != null ? Mathf.RoundToInt(-Player.Checkpoint.y) : -1 };
    public void Save() => TrendoSave.Write(FilePath, Snapshot());
    public void Resume(TrendoLevelData d, TrendoSave s)
    {
        Lives = s.lives; Begin(d, s.level); Score = s.score;
        var rows = d.levels[s.level].rows;
        if (Player != null && s.checkY >= 0 && s.checkY < rows.Length && s.checkX >= 0 && s.checkX < rows[s.checkY].Length && "CS".IndexOf(rows[s.checkY][s.checkX]) >= 0)
        { Player.Checkpoint = new Vector3(s.checkX, -s.checkY, 0); Player.Respawn(); }
    }

    public static Sprite Square()
    {
        if (_square != null) return _square;
        var t = new Texture2D(4, 4); var px = new Color[16]; for (var i = 0; i < 16; i++) px[i] = Color.white; t.SetPixels(px); t.Apply();
        return _square = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    public void Begin(TrendoLevelData d, int index)
    {
        Data = d; Index = index; Lives = Lives == 0 ? d.lives : Lives; Coins = 0; Keys = 0; _doors.Clear();
        if (_fired == null) _fired = new bool[d.rules != null ? d.rules.Length : 0];
        foreach (Transform c in transform) Destroy(c.gameObject);
        Player = null;
        var cam = Camera.main; if (cam == null) { cam = new GameObject("Kamera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
        cam.orthographic = true; cam.orthographicSize = 7; cam.backgroundColor = TrendoLevelData.Hex(d.background, Color.black); cam.clearFlags = CameraClearFlags.SolidColor;
        var rows = d.levels[index].rows;
        for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++)
            {
                var p = new Vector3(x, -y, 0);
                switch (rows[y][x])
                {
                    case '#': Tile("Boden", p, new Color(0.35f, 0.4f, 0.5f), true, false, 1f, x, y); break;
                    case 'o': Tile("Münze", p, TrendoLevelData.Hex(d.accent, Color.yellow), false, true, 0.5f, x, y); break;
                    case '^': Tile("Stacheln", p, new Color(0.9f, 0.3f, 0.3f), false, true, 0.8f, x, y); break;
                    case 'E':
                        var e = Tile("Gegner", p, new Color(0.8f, 0.2f, 0.6f), false, true, 0.9f, x, y).AddComponent<TrendoEnemy>();
                        e.MaxHp = e.Hp = Mathf.Clamp(Mathf.RoundToInt(d.Prop(index, x, y, "hp", 1)), 1, 9); break;
                    case 'Z': Tile("Ziel", p, Color.green, false, true, 1f, x, y); break;
                    case 'F': Tile("Sprungfeder", p, new Color(1f, 0.6f, 0.1f), false, true, 0.6f, x, y); break;
                    case 'C': Tile("Speicherpunkt", p, Color.cyan, false, true, 0.6f, x, y); break;
                    case 'K': Tile("Schlüssel", p, new Color(1f, 0.85f, 0.2f), false, true, 0.5f, x, y); break;
                    case 'D': _doors.Add(Tile("Tür", p, new Color(0.55f, 0.35f, 0.2f), true, false, 1f, x, y)); break;
                    case 'G': Tile("Gegenstand", p, new Color(0.66f, 0.33f, 0.97f), false, true, 0.5f, x, y); break;
                    case 'N': Tile("Figur", p, new Color(0.08f, 0.72f, 0.65f), false, true, 0.9f, x, y); break;
                    case 'V': Tile("Fahrzeug", p, new Color(0.98f, 0.45f, 0.09f), false, true, 0.9f, x, y).GetComponent<TrendoTile>().Kind = d.Text(index, x, y, "kind", "wagen"); break;
                    case 'S':
                        Player = new GameObject("Spielfigur").AddComponent<TrendoPlayer>(); Player.transform.SetParent(transform); Player.Setup(this, p, TrendoLevelData.Hex(d.player, Color.blue)); break;
                }
            }
    }

    GameObject Tile(string name, Vector3 p, Color c, bool solid, bool trigger, float size, int x, int y)
    {
        var g = new GameObject(name); g.transform.SetParent(transform); g.transform.position = p; g.transform.localScale = Vector3.one * size;
        var r = g.AddComponent<SpriteRenderer>(); r.sprite = Square(); r.color = c;
        var col = g.AddComponent<BoxCollider2D>(); col.isTrigger = trigger && !solid;
        var t = g.AddComponent<TrendoTile>(); t.X = x; t.Y = y;
        return g;
    }

    /// <summary>Teil des Levels nach Name (für Tests und Werkzeuge).</summary>
    public GameObject Find(string name) { foreach (Transform c in transform) if (c.name == name) return c.gameObject; return null; }

    public void Touch(GameObject g)
    {
        var t = g.GetComponent<TrendoTile>();
        switch (g.name)
        {
            case "Münze": Coins++; Score += 10; Destroy(g); break;
            case "Schlüssel": Keys++; Destroy(g); if (Keys >= Data.Count(Index, 'K')) OpenDoors(); break;
            case "Stacheln": Hit(); break;
            case "Gegner":
                var e = g.GetComponent<TrendoEnemy>();
                if (Player != null && Player.Falling && Player.transform.position.y > g.transform.position.y + 0.4f) { Damage(e); Player.Bounce(8f); } else Hit();
                break;
            case "Sprungfeder": Player.Bounce(16f); break;
            case "Speicherpunkt": if (Player.Checkpoint != g.transform.position) { Player.Checkpoint = g.transform.position; Save(); } break;
            case "Gegenstand":
                var n = Data.Text(Index, t.X, t.Y, "name", "Kristall"); Inventory.Add(n); ItemsCollected++; Note = n + " eingesammelt"; Destroy(g); break;
            case "Figur":
                var key = Index + ":" + t.X + "," + t.Y; var need = Data.Text(Index, t.X, t.Y, "need", "");
                if (need.Length > 0 && !_given.Contains(key) && Inventory.Remove(need)) { _given.Add(key); Apply(Data.Text(Index, t.X, t.Y, "give", "punkte")); Note = Data.Text(Index, t.X, t.Y, "thanks", "Danke!"); }
                else Note = _given.Contains(key) ? Data.Text(Index, t.X, t.Y, "thanks", "Danke!") : Data.Text(Index, t.X, t.Y, "say", "Hallo!");
                break;
            case "Fahrzeug":
                if (Player != null && Player.Vehicle == null && Player.NoEnter <= 0)
                {
                    Player.Vehicle = t.Kind == "ufo" ? "ufo" : "wagen"; Player.Fuel = Player.Vehicle == "ufo" ? 10f : 0f; Destroy(g);
                    Note = Player.Vehicle == "ufo" ? "Ufo: Leertaste halten – E steigt aus" : "Wagen: schnell, fängt einen Treffer ab – E steigt aus";
                }
                break;
            case "Ziel": Score += 10; if (Index + 1 < Data.levels.Length) { Begin(Data, Index + 1); Save(); } else { Won = true; Drop(); } break;
        }
    }

    void OpenDoors() { foreach (var d in _doors) if (d != null) Destroy(d); _doors.Clear(); }

    /// <summary>Belohnung bzw. Wirkung (Regel oder sprechende Figur).</summary>
    public void Apply(string what)
    {
        switch (what)
        {
            case "leben": Lives++; break;
            case "tueren": OpenDoors(); break;
            case "schneller": Boost = 2; break;
            case "punkte": Score += 10; break;
            case "sieg": Won = true; break;
        }
    }

    /// <summary>Treffer: im Fahrzeug geht das Fahrzeug kaputt (kurz unverwundbar), sonst ein Leben weniger. Absturz trifft immer.</summary>
    public void Hit(bool fall = false)
    {
        if (Player != null && Player.Vehicle != null && !fall) { Player.Vehicle = null; Player.Safe = 0.75f; Note = "Fahrzeug kaputt – zu Fuß weiter"; return; }
        if (Player != null && Player.Safe > 0 && !fall) return;
        if (Player != null) Player.Vehicle = null;
        Lives--; if (Lives <= 0) { Over = true; Drop(); } else if (Player != null) Player.Respawn();
    }

    /// <summary>Gegner trifft es: ein Trefferpunkt weniger; besiegt bringt 2 Punkte (Boss 10).</summary>
    public void Damage(TrendoEnemy e)
    {
        if (e == null || e.Hp <= 0) return;
        e.Hp--; if (e.Hp > 0) return;
        Defeated++; Score += e.MaxHp > 1 ? 10 : 2; if (e.MaxHp > 1) Note = "Boss besiegt!";
        Destroy(e.gameObject);
    }

    /// <summary>Angriff mit der Waffe aus Trendo: Schwert trifft vor der Figur, Werfen schickt ein Geschoss.</summary>
    public void Attack()
    {
        if (Player == null || string.IsNullOrEmpty(Data.weapon)) return;
        var pos = Player.transform.position;
        if (Data.weapon == "schwert")
        {
            foreach (var e in GetComponentsInChildren<TrendoEnemy>())
            {
                var dx = e.transform.position.x - pos.x;
                if ((Mathf.Sign(dx) == Player.Facing || Mathf.Abs(dx) < 0.2f) && Mathf.Abs(dx) < 1.4f && Mathf.Abs(e.transform.position.y - pos.y) < 0.9f) Damage(e);
            }
        }
        else
        {
            var s = new GameObject("Geschoss"); s.transform.SetParent(transform); s.transform.position = pos + Vector3.right * Player.Facing * 0.5f; s.transform.localScale = Vector3.one * 0.25f;
            s.AddComponent<SpriteRenderer>().sprite = Square(); s.GetComponent<SpriteRenderer>().color = TrendoLevelData.Hex(Data.accent, Color.yellow);
            s.AddComponent<TrendoShot>().Setup(this, Player.Facing);
        }
    }

    /// <summary>Aussteigen: das Fahrzeug bleibt an der Stelle stehen und lässt sich wieder nehmen.</summary>
    public void Dismount()
    {
        if (Player == null || Player.Vehicle == null) return;
        var p = Player.transform.position; var v = Tile("Fahrzeug", new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0), new Color(0.98f, 0.45f, 0.09f), false, true, 0.9f, -1, -1);
        v.GetComponent<TrendoTile>().Kind = Player.Vehicle; Player.Vehicle = null; Player.NoEnter = 0.5f; Note = "Ausgestiegen";
    }

    double RuleValue(TrendoRule r) => r.when == "muenzen" ? Coins : r.when == "punkte" ? Score : r.when == "gegner" ? Defeated : r.when == "sekunden" ? _time : r.when == "gegenstaende" ? ItemsCollected : 0;

    /// <summary>Aufgaben (Regeln mit Titel) mit Fortschritt.</summary>
    public string Quests()
    {
        var sb = new System.Text.StringBuilder();
        if (Data == null || Data.rules == null) return "";
        for (var i = 0; i < Data.rules.Length; i++)
        {
            var r = Data.rules[i]; if (string.IsNullOrEmpty(r.title)) continue;
            sb.AppendLine(_fired[i] ? "✓ " + r.title : r.title + ": " + Mathf.Min((int)RuleValue(r), r.n) + "/" + r.n);
        }
        return sb.ToString();
    }

    void Update()
    {
        if (Data == null || Over || Won) return;
        _time += Time.deltaTime;
        if (Data.rules == null) return;
        for (var i = 0; i < Data.rules.Length; i++)
        {
            if (_fired[i]) continue; var r = Data.rules[i];
            if (RuleValue(r) < r.n) continue;
            _fired[i] = true; Apply(r.act); if (!string.IsNullOrEmpty(r.title)) Note = "✓ " + r.title;
        }
    }

    void OnGUI()
    {
        if (Data == null || Player == null) return;
        var text = "Punkte " + Score + "   Leben " + Lives + (Data.levels.Length > 1 ? "   Level " + (Index + 1) + "/" + Data.levels.Length : "")
            + (Player.Vehicle == "ufo" ? "   Treibstoff " + Mathf.CeilToInt(Player.Fuel) + " s" : "")
            + (Inventory.Count > 0 ? "\nInventar: " + string.Join(", ", Inventory) : "") + "\n" + Quests() + Note;
        GUI.Label(new Rect(10, 10, 600, 200), text);
    }

    void Drop() { if (System.IO.File.Exists(FilePath)) System.IO.File.Delete(FilePath); }
}

/// <summary>Merkt sich die Kachel-Stelle (für Texte) und die Fahrzeug-Art.</summary>
public class TrendoTile : MonoBehaviour { public int X, Y; public string Kind = "wagen"; }

/// <summary>Gegner läuft hin und her; Trefferpunkte wie in Trendo.</summary>
public class TrendoEnemy : MonoBehaviour
{
    public int Hp = 1, MaxHp = 1;
    Vector3 _start; float _t;
    void Start() => _start = transform.position;
    void Update() { _t += Time.deltaTime; transform.position = _start + Vector3.right * Mathf.Sin(_t * 1.5f) * 1.5f; }
}

/// <summary>Geschoss der Waffe „Werfen“: fliegt bis zur Wand oder zum Gegner.</summary>
public class TrendoShot : MonoBehaviour
{
    TrendoLevel _level; float _dir; float _life = 1.5f;
    public void Setup(TrendoLevel level, float dir) { _level = level; _dir = dir; var c = gameObject.AddComponent<BoxCollider2D>(); c.isTrigger = true; var rb = gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; }
    void Update() { transform.position += Vector3.right * _dir * 12f * Time.deltaTime; _life -= Time.deltaTime; if (_life <= 0) Destroy(gameObject); }
    void OnTriggerEnter2D(Collider2D other)
    {
        var e = other.GetComponent<TrendoEnemy>();
        if (e != null) { _level.Damage(e); Destroy(gameObject); }
        else if (other.name == "Boden" || other.name == "Tür") Destroy(gameObject);
    }
}
""";

    const string UnityPlayer = """
using UnityEngine;

/// <summary>Spielfigur: Pfeiltasten/A-D laufen, Leertaste springt (Doppelsprung, Ufo fliegen), Umschalt sprintet, X/J greift an, E steigt aus.</summary>
public class TrendoPlayer : MonoBehaviour
{
    public Vector3 Checkpoint; public float Facing = 1;
    public string Vehicle; public float Fuel, Safe, NoEnter;
    TrendoLevel _level; Rigidbody2D _rb; float _speed, _cool; int _air;

    public void Setup(TrendoLevel level, Vector3 at, Color color)
    {
        _level = level; transform.position = Checkpoint = at;
        var r = gameObject.AddComponent<SpriteRenderer>(); r.sprite = TrendoLevel.Square(); r.color = color; transform.localScale = new Vector3(0.8f, 0.9f, 1);
        gameObject.AddComponent<BoxCollider2D>();
        _rb = gameObject.AddComponent<Rigidbody2D>(); _rb.freezeRotation = true; _rb.gravityScale = 3;
        _speed = 2f + level.Data.speed;
    }

    public bool Grounded => _rb != null && Mathf.Abs(_rb.linearVelocity.y) < 0.01f;
    public bool Falling => _rb != null && _rb.linearVelocity.y < -0.5f;
    public void Respawn() { transform.position = Checkpoint; _rb.linearVelocity = Vector2.zero; }
    public void Bounce(float v) => _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, v);

    /// <summary>Sprung: am Boden normal, in der Luft einmal mit Fähigkeit „doppelsprung“. Gibt zurück, ob gesprungen wurde.</summary>
    public bool Jump()
    {
        if (Grounded) { Bounce(11f); return true; }
        if (_air > 0) { _air--; Bounce(10f); return true; }
        return false;
    }

    /// <summary>Tempo mit Sprint (Fähigkeit, Umschalt) und Fahrzeug.</summary>
    public float Speed(bool sprint) => (_speed + _level.Boost) * (sprint && _level.Data.Has("sprint") ? 1.6f : 1f) * (Vehicle == "wagen" ? 1.8f : Vehicle == "ufo" ? 1.3f : 1f);

    /// <summary>Ufo-Antrieb für eine Zeitspanne (Leertaste gehalten).</summary>
    public void Thrust(float dt)
    {
        if (Vehicle != "ufo" || Fuel <= 0) return;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, Mathf.Min(_rb.linearVelocity.y + 40f * dt, 6f));
        Fuel -= dt; if (Fuel <= 0) { Vehicle = null; _level.Note = "Treibstoff leer"; }
    }

    void Update()
    {
        if (_rb == null) return;
        var dt = Time.deltaTime;
        if (Safe > 0) Safe -= dt; if (NoEnter > 0) NoEnter -= dt; if (_cool > 0) _cool -= dt;
        if (Grounded) _air = _level.Data.Has("doppelsprung") ? 1 : 0;
        var h = Input.GetAxisRaw("Horizontal"); if (h != 0) Facing = Mathf.Sign(h);
        _rb.linearVelocity = new Vector2(h * Speed(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)), _rb.linearVelocity.y);
        if (Input.GetKeyDown(KeyCode.Space)) Jump();
        if (Input.GetKey(KeyCode.Space)) Thrust(dt);
        if ((Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.J)) && _cool <= 0) { _cool = 0.33f; _level.Attack(); }
        if (Input.GetKeyDown(KeyCode.E)) _level.Dismount();
        if (transform.position.y < -40) _level.Hit(true);
        var cam = Camera.main; if (cam != null) cam.transform.position = new Vector3(transform.position.x, transform.position.y, -10);
    }

    void OnTriggerEnter2D(Collider2D other) => _level.Touch(other.gameObject);
}
""";

    const string UnityBuild = """
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Trendo-Bauskript: legt die Szene an (falls nötig) und baut ein Windows-Spiel nach Build/Windows. Für -executeMethod TrendoBuild.BuildWindows.</summary>
public static class TrendoBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Trendo/Szene anlegen")]
    public static void EnsureScene()
    {
        if (File.Exists(ScenePath)) return;
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    [MenuItem("Trendo/Windows-Build")]
    public static void BuildWindows()
    {
        EnsureScene();
        var name = PlayerSettings.productName.Length > 0 ? PlayerSettings.productName : "Spiel";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Build/Windows/" + name + ".exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        Debug.Log("TRENDO-BUILD " + report.summary.result + " " + report.summary.totalSize);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
""";

    const string UnityEditTests = """
using NUnit.Framework;
using UnityEngine;

/// <summary>EditMode-Tests: das von Trendo exportierte Level ist vollständig.</summary>
public class TrendoLevelTests
{
    [Test] public void Level_wird_geladen() { var d = TrendoLevelData.Load(); Assert.IsNotNull(d); Assert.Greater(d.levels.Length, 0); }
    [Test] public void Jedes_Level_hat_Start_und_Ziel()
    {
        var d = TrendoLevelData.Load();
        for (var i = 0; i < d.levels.Length; i++) { Assert.GreaterOrEqual(d.Start(i).x, 0, "Start fehlt in " + d.levels[i].name); Assert.Greater(d.Count(i, 'Z'), 0, "Ziel fehlt in " + d.levels[i].name); }
    }
    [Test] public void Kaputtes_Level_wird_erkannt() { Assert.Throws<System.ArgumentException>(() => TrendoLevelData.Parse("{kaputt")); }
    [Test] public void Gameplay_Daten_werden_gelesen()
    {
        var d = TrendoLevelData.Parse("{\"levels\":[{\"rows\":[\"S.G\"]}],\"weapon\":\"werfen\",\"abilities\":[\"sprint\"],\"texts\":[{\"key\":\"2,0:name\",\"value\":\"Apfel\"},{\"key\":\"2:1,0:name\",\"value\":\"Birne\"}],\"props\":[{\"key\":\"1,0:hp\",\"value\":3}]}");
        Assert.AreEqual("werfen", d.weapon); Assert.IsTrue(d.Has("sprint")); Assert.IsFalse(d.Has("doppelsprung"));
        Assert.AreEqual("Apfel", d.Text(0, 2, 0, "name", "Kristall")); Assert.AreEqual("Birne", d.Text(1, 1, 0, "name", "Kristall")); Assert.AreEqual("Kristall", d.Text(0, 5, 0, "name", "Kristall"));
        Assert.AreEqual(3f, d.Prop(0, 1, 0, "hp", 1));
    }
    [Test] public void Spielstand_speichern_und_laden()
    {
        var f = System.IO.Path.Combine(Application.temporaryCachePath, "test-spielstand.json");
        TrendoSave.Write(f, new TrendoSave { level = 1, score = 77, lives = 2, checkX = 3, checkY = 0 });
        var s = TrendoSave.Read(f, 2); Assert.IsNotNull(s); Assert.AreEqual(1, s.level); Assert.AreEqual(77, s.score); Assert.AreEqual(1, s.version);
        Assert.IsNull(TrendoSave.Read(f, 1), "Level 2 gibt es nicht");
        System.IO.File.WriteAllText(f, "{\"version\":9,\"level\":0}"); Assert.IsNull(TrendoSave.Read(f, 2), "neuere Fassung");
        System.IO.File.WriteAllText(f, "{kaputt"); Assert.IsNull(TrendoSave.Read(f, 2), "kaputte Datei");
        System.IO.File.Delete(f);
    }
}
""";

    const string UnityPlayTests = """
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>PlayMode-Test: das Spiel startet, die Figur steht und fällt nicht durch den Boden.</summary>
public class TrendoPlayTests
{
    [UnityTest] public IEnumerator Spiel_startet_und_Figur_steht()
    {
        var level = new GameObject("Test-Spiel").AddComponent<TrendoLevel>(); level.Begin(TrendoLevelData.Load(), 0);
        for (var i = 0; i < 60; i++) yield return null;
        Assert.IsNotNull(level.Player); Assert.Greater(level.Player.transform.position.y, -20f); Assert.IsFalse(level.Over);
        Object.Destroy(level.gameObject);
    }

    [UnityTest] public IEnumerator Spielstand_setzt_im_richtigen_Level_fort()
    {
        var d = TrendoLevelData.Load(); var last = d.levels.Length - 1;
        var level = new GameObject("Test-Fortsetzen").AddComponent<TrendoLevel>(); level.SavePath = System.IO.Path.Combine(Application.temporaryCachePath, "play-spielstand.json");
        level.Resume(d, new TrendoSave { level = last, score = 42, lives = 2 });
        yield return null;
        Assert.AreEqual(last, level.Index); Assert.AreEqual(42, level.Score); Assert.AreEqual(2, level.Lives);
        level.Save(); Assert.AreEqual(last, TrendoSave.Read(level.SavePath, d.levels.Length).level);
        System.IO.File.Delete(level.SavePath); Object.Destroy(level.gameObject);
    }
}
""";

    const string UnityGameplayTests = """
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>PlayMode-Tests: Gameplay aus Trendo – Gegenstand, sprechende Figur, Schwert, Boss, Fahrzeug, Aufgabe, Doppelsprung.</summary>
public class TrendoGameplayTests
{
    const string Json = "{\"name\":\"Test\",\"speed\":5,\"lives\":3,\"weapon\":\"schwert\",\"abilities\":[\"doppelsprung\",\"sprint\"],"
        + "\"levels\":[{\"name\":\"L\",\"rows\":[\"..........\",\"SGNE.V...Z\",\"##########\"],\"decor\":[]}],"
        + "\"props\":[{\"key\":\"3,1:hp\",\"value\":2}],"
        + "\"texts\":[{\"key\":\"1,1:name\",\"value\":\"Apfel\"},{\"key\":\"2,1:need\",\"value\":\"Apfel\"},{\"key\":\"2,1:thanks\",\"value\":\"Lecker!\"},{\"key\":\"2,1:give\",\"value\":\"leben\"},{\"key\":\"5,1:kind\",\"value\":\"wagen\"}],"
        + "\"rules\":[{\"when\":\"gegenstaende\",\"n\":1,\"act\":\"punkte\",\"title\":\"Finder\"}]}";

    TrendoLevel Make() { var level = new GameObject("Test-Gameplay").AddComponent<TrendoLevel>(); level.Begin(TrendoLevelData.Parse(Json), 0); return level; }

    [UnityTest] public IEnumerator Gegenstand_Figur_und_Aufgabe()
    {
        var level = Make(); yield return null;
        level.Touch(level.Find("Gegenstand")); yield return null;
        CollectionAssert.AreEqual(new[] { "Apfel" }, level.Inventory); Assert.AreEqual(1, level.ItemsCollected);
        StringAssert.Contains("✓ Finder", level.Quests());
        level.Touch(level.Find("Figur"));
        Assert.AreEqual(4, level.Lives); Assert.AreEqual("Lecker!", level.Note); Assert.IsEmpty(level.Inventory);
        Object.Destroy(level.gameObject);
    }

    [UnityTest] public IEnumerator Schwert_und_Boss()
    {
        var level = Make(); yield return null;
        var boss = level.Find("Gegner").GetComponent<TrendoEnemy>(); Assert.AreEqual(2, boss.MaxHp);
        level.Player.transform.position = boss.transform.position + Vector3.left * 0.8f; level.Player.Facing = 1;
        level.Attack(); Assert.AreEqual(1, boss.Hp);
        level.Player.transform.position = boss.transform.position + Vector3.left * 0.8f;
        level.Attack(); yield return null;
        Assert.AreEqual(1, level.Defeated); Assert.AreEqual("Boss besiegt!", level.Note);
        Object.Destroy(level.gameObject);
    }

    [UnityTest] public IEnumerator Wagen_faengt_Treffer_ab_und_Aussteigen()
    {
        var level = Make(); yield return null;
        var foot = level.Player.Speed(false);
        level.Touch(level.Find("Fahrzeug")); yield return null;
        Assert.AreEqual("wagen", level.Player.Vehicle);
        Assert.AreEqual(foot * 1.8f, level.Player.Speed(false), 0.001f);
        level.Hit(); Assert.IsNull(level.Player.Vehicle); Assert.AreEqual(3, level.Lives);
        level.Hit(); Assert.AreEqual(3, level.Lives, "kurz unverwundbar");
        Object.Destroy(level.gameObject);
    }

    [UnityTest] public IEnumerator Doppelsprung_nur_einmal_in_der_Luft()
    {
        var level = Make();
        for (var i = 0; i < 60 && !level.Player.Grounded; i++) yield return new WaitForFixedUpdate();
        for (var i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        Assert.IsTrue(level.Player.Jump(), "Sprung vom Boden");
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Assert.IsTrue(level.Player.Jump(), "Doppelsprung");
        Assert.IsFalse(level.Player.Jump(), "kein dritter Sprung");
        Object.Destroy(level.gameObject);
    }
}
""";

    // ───────────── Godot-Quelltexte (eigene, Clean-Room) ─────────────
    const string GodotLevel = """
extends Node2D
## Von Trendo: baut das Trendo-Level als Spielszene (Boden, Münzen, Gegner, Stacheln, Sprungfedern, Schlüssel, Türen, Ziel).

const T := 32.0
var data := {}
var index := 0
var coins := 0
var keys := 0
var lives := 3
var won := false
var player: CharacterBody2D
var doors: Array = []
var defeated := 0
var items := 0
var inventory: Array = []
var given := {}
var note := ""
var boost := 0.0
var fired: Array = []
var time := 0.0
var hud: Label

func key(x: int, y: int, k: String) -> String:
	return ("" if index == 0 else str(index + 1) + ":") + str(x) + "," + str(y) + ":" + k

## Objekt-Text (Gegenstand, Figur, Fahrzeug) wie in Trendo, sonst Vorgabe
func text(x: int, y: int, k: String, d: String) -> String:
	var want := key(x, y, k)
	for t in data.get("texts", []):
		if t.get("key", "") == want and String(t.get("value", "")) != "":
			return String(t["value"])
	return d

func prop(x: int, y: int, k: String, d: float) -> float:
	var want := key(x, y, k)
	for p in data.get("props", []):
		if p.get("key", "") == want:
			return float(p["value"])
	return d

func has(ability: String) -> bool:
	return ability in data.get("abilities", [])

func tagged(a: Area2D, x: int, y: int) -> Area2D:
	a.set_meta("x", x)
	a.set_meta("y", y)
	return a

static func load_level(path: String) -> Dictionary:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return {}
	var d = JSON.parse_string(f.get_as_text())
	return d if d is Dictionary else {}

func _ready() -> void:
	data = load_level("res://level.json")
	if data.is_empty():
		push_error("level.json fehlt oder ist kaputt")
		return
	lives = int(data.get("lives", 3))
	fired.resize(data.get("rules", []).size())
	fired.fill(false)
	var layer := CanvasLayer.new()
	hud = Label.new()
	hud.position = Vector2(10, 10)
	layer.add_child(hud)
	add_sibling.call_deferred(layer)
	RenderingServer.set_default_clear_color(Color(data.get("background", "#10203F")))
	var s := read_save(SAVE, data["levels"].size())
	if s.is_empty():
		build(0)
	else:
		resume(s)

const SAVE := "user://trendo-spielstand.json"
var score := 0

## Spielstand im Trendo-Format 1 schreiben
func save_game(path := SAVE) -> void:
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		return
	var cp: Vector2 = player.checkpoint if player else Vector2(-T, -T)
	f.store_string(JSON.stringify({"version": 1, "level": index, "score": score, "lives": lives, "checkX": int(cp.x / T), "checkY": int(cp.y / T), "coins": coins, "savedUtc": Time.get_datetime_string_from_system(true)}))

## Lesen und prüfen: leer bei fehlender, kaputter, neuerer oder unpassender Datei
func read_save(path: String, level_count: int) -> Dictionary:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return {}
	var j := JSON.new()
	if j.parse(f.get_as_text()) != OK or not (j.data is Dictionary):
		return {}
	var s: Dictionary = j.data
	if int(s.get("version", 0)) > 1:
		return {}
	var lv := int(s.get("level", -1))
	if lv < 0 or lv >= level_count:
		return {}
	s["lives"] = clampi(int(s.get("lives", 1)), 1, 99)
	return s

func resume(s: Dictionary) -> void:
	lives = int(s["lives"])
	score = int(s.get("score", 0))
	build(int(s["level"]))
	var rows: Array = data["levels"][index]["rows"]
	var cx := int(s.get("checkX", -1))
	var cy := int(s.get("checkY", -1))
	if cy >= 0 and cy < rows.size() and cx >= 0 and cx < String(rows[cy]).length() and "CS".contains(String(rows[cy])[cx]):
		player.checkpoint = Vector2(cx * T, cy * T)
		player.respawn()

func drop_save() -> void:
	if FileAccess.file_exists(SAVE):
		DirAccess.remove_absolute(SAVE)

func build(i: int) -> void:
	index = i
	keys = 0
	doors.clear()
	for c in get_children():
		remove_child(c) # sofort weg: sonst benennt Godot die neuen Kacheln um
		c.queue_free()
	var rows: Array = data["levels"][i]["rows"]
	for y in rows.size():
		var row: String = rows[y]
		for x in row.length():
			var p := Vector2(x * T, y * T)
			match row[x]:
				"#": solid("Boden", p, Color(0.35, 0.4, 0.5))
				"D": doors.append(solid("Tuer", p, Color(0.55, 0.35, 0.2)))
				"o": area("Muenze", p, Color(data.get("accent", "#FACC15")))
				"^": area("Stacheln", p, Color(0.9, 0.3, 0.3))
				"E":
					var e := tagged(area("Gegner", p, Color(0.8, 0.2, 0.6)), x, y)
					var hp := clampi(int(round(prop(x, y, "hp", 1))), 1, 9)
					e.set_meta("hp", hp)
					e.set_meta("max", hp)
				"G": tagged(area("Gegenstand", p, Color(0.66, 0.33, 0.97)), x, y)
				"N": tagged(area("Figur", p, Color(0.08, 0.72, 0.65)), x, y)
				"V": tagged(area("Fahrzeug", p, Color(0.98, 0.45, 0.09)), x, y).set_meta("kind", text(x, y, "kind", "wagen"))
				"Z": area("Ziel", p, Color(0.2, 0.9, 0.3))
				"F": area("Sprungfeder", p, Color(1, 0.6, 0.1))
				"C": area("Speicherpunkt", p, Color(0.2, 0.9, 0.9))
				"K": area("Schluessel", p, Color(1, 0.85, 0.2))
				"S":
					player = preload("res://trendo_player.gd").new()
					player.setup(self, p, Color(data.get("player", "#4FB3FF")), 120.0 + 20.0 * float(data.get("speed", 5)))
					add_child(player)

func rect(color: Color) -> ColorRect:
	var r := ColorRect.new()
	r.size = Vector2(T, T)
	r.color = color
	return r

func solid(n: String, p: Vector2, c: Color) -> StaticBody2D:
	var b := StaticBody2D.new()
	b.name = n
	b.position = p
	b.add_child(rect(c))
	var s := CollisionShape2D.new()
	var sh := RectangleShape2D.new()
	sh.size = Vector2(T, T)
	s.shape = sh
	s.position = Vector2(T / 2, T / 2)
	b.add_child(s)
	add_child(b)
	return b

func area(n: String, p: Vector2, c: Color) -> Area2D:
	var a := Area2D.new()
	a.name = n
	a.position = p
	a.add_child(rect(c))
	var s := CollisionShape2D.new()
	var sh := RectangleShape2D.new()
	sh.size = Vector2(T * 0.8, T * 0.8)
	s.shape = sh
	s.position = Vector2(T / 2, T / 2)
	a.add_child(s)
	a.body_entered.connect(_on_enter.bind(a))
	add_child(a)
	return a

func _on_enter(body, a: Area2D) -> void:
	if body == player:
		touch(a)

func count(tile: String) -> int:
	var n := 0
	for row in data["levels"][index]["rows"]:
		n += String(row).count(tile)
	return n

func touch(a: Area2D) -> void:
	var n := String(a.name)
	if n.begins_with("Muenze"):
		coins += 1
		score += 1
		a.queue_free()
	elif n.begins_with("Schluessel"):
		keys += 1
		a.queue_free()
		if keys >= count("K"):
			open_doors()
	elif n.begins_with("Stacheln"):
		hit()
	elif n.begins_with("Gegner"):
		if player.velocity.y > 0 and player.position.y < a.position.y - 8:
			damage(a)
			player.velocity.y = -400.0
		else:
			hit()
	elif n.begins_with("Gegenstand"):
		var nm := text(a.get_meta("x"), a.get_meta("y"), "name", "Kristall")
		inventory.append(nm)
		items += 1
		note = nm + " eingesammelt"
		a.queue_free()
	elif n.begins_with("Figur"):
		var ax: int = a.get_meta("x")
		var ay: int = a.get_meta("y")
		var k := str(index) + ":" + str(ax) + "," + str(ay)
		var need := text(ax, ay, "need", "")
		if need != "" and not given.has(k) and need in inventory:
			inventory.erase(need)
			given[k] = true
			apply(text(ax, ay, "give", "punkte"))
			note = text(ax, ay, "thanks", "Danke!")
		else:
			note = text(ax, ay, "thanks", "Danke!") if given.has(k) else text(ax, ay, "say", "Hallo!")
	elif n.begins_with("Fahrzeug"):
		if player.vehicle == "" and player.no_enter <= 0:
			player.vehicle = "ufo" if a.get_meta("kind", "wagen") == "ufo" else "wagen"
			player.fuel = 10.0 if player.vehicle == "ufo" else 0.0
			note = "Ufo: Leertaste halten – E steigt aus" if player.vehicle == "ufo" else "Wagen: schnell, fängt einen Treffer ab – E steigt aus"
			a.queue_free()
	elif n.begins_with("Sprungfeder"):
		player.velocity.y = -720.0
	elif n.begins_with("Speicherpunkt"):
		if player.checkpoint != a.position:
			player.checkpoint = a.position
			save_game()
	elif n.begins_with("Ziel"):
		score += 10
		if index + 1 < data["levels"].size():
			build.call_deferred(index + 1)
			save_game.call_deferred()
		else:
			won = true
			drop_save()

func open_doors() -> void:
	for d in doors:
		if is_instance_valid(d):
			d.queue_free()
	doors.clear()

## Belohnung bzw. Wirkung (Regel oder sprechende Figur)
func apply(what: String) -> void:
	match what:
		"leben": lives += 1
		"tueren": open_doors()
		"schneller": boost = 80.0
		"punkte": score += 10
		"sieg": won = true

## Gegner trifft es: ein Trefferpunkt weniger; besiegt bringt 2 Punkte (Boss 10)
func damage(a: Area2D) -> void:
	var hp: int = a.get_meta("hp", 1) - 1
	a.set_meta("hp", hp)
	if hp > 0:
		return
	defeated += 1
	var boss: bool = a.get_meta("max", 1) > 1
	score += 10 if boss else 2
	if boss:
		note = "Boss besiegt!"
	a.queue_free()

func enemies() -> Array:
	return get_children().filter(func(c): return c is Area2D and String(c.name).begins_with("Gegner") and not c.is_queued_for_deletion() and c.get_meta("hp", 1) > 0)

## Angriff mit der Waffe aus Trendo: Schwert trifft vor der Figur, Werfen schickt ein Geschoss
func attack() -> void:
	var w: String = data.get("weapon", "")
	if w == "" or player == null:
		return
	if w == "schwert":
		for e in enemies():
			var dx: float = e.position.x - player.position.x
			if (signf(dx) == player.facing or absf(dx) < 6) and absf(dx) < 44 and absf(e.position.y - player.position.y) < 28:
				damage(e)
	else:
		var s := preload("res://trendo_shot.gd").new()
		s.setup(self, player.position + Vector2(16 + player.facing * 12, 12), player.facing)
		add_child(s)

## Aussteigen: das Fahrzeug bleibt an der Stelle stehen und lässt sich wieder nehmen
func dismount() -> void:
	if player == null or player.vehicle == "":
		return
	var v := tagged(area("Fahrzeug", Vector2(roundf(player.position.x / T) * T, roundf(player.position.y / T) * T), Color(0.98, 0.45, 0.09)), -1, -1)
	v.set_meta("kind", player.vehicle)
	player.vehicle = ""
	player.no_enter = 0.5
	note = "Ausgestiegen"

func rule_value(r: Dictionary) -> float:
	match String(r.get("when", "")):
		"muenzen": return coins
		"punkte": return score
		"gegner": return defeated
		"sekunden": return time
		"gegenstaende": return items
	return 0.0

## Aufgaben (Regeln mit Titel) mit Fortschritt
func quests() -> String:
	var out := ""
	var rules: Array = data.get("rules", [])
	for i in rules.size():
		var r: Dictionary = rules[i]
		var t := String(r.get("title", ""))
		if t == "":
			continue
		out += ("✓ " + t) if fired[i] else (t + ": " + str(mini(int(rule_value(r)), int(r["n"]))) + "/" + str(int(r["n"])))
		out += "\n"
	return out

func _process(delta: float) -> void:
	if data.is_empty() or won:
		return
	time += delta
	var rules: Array = data.get("rules", [])
	for i in rules.size():
		if fired[i] or rule_value(rules[i]) < float(rules[i]["n"]):
			continue
		fired[i] = true
		apply(String(rules[i]["act"]))
		if String(rules[i].get("title", "")) != "":
			note = "✓ " + String(rules[i]["title"])
	if hud:
		hud.text = "Punkte %d   Leben %d" % [score, lives] + ("   Treibstoff %d s" % ceili(player.fuel) if player and player.vehicle == "ufo" else "") + ("\nInventar: " + ", ".join(PackedStringArray(inventory)) if inventory.size() > 0 else "") + "\n" + quests() + note

## Treffer: im Fahrzeug geht das Fahrzeug kaputt (kurz unverwundbar), sonst ein Leben weniger. Absturz trifft immer.
func hit(fall := false) -> void:
	if player and player.vehicle != "" and not fall:
		player.vehicle = ""
		player.safe = 0.75
		note = "Fahrzeug kaputt – zu Fuß weiter"
		return
	if player and player.safe > 0 and not fall:
		return
	if player:
		player.vehicle = ""
	lives -= 1
	if lives <= 0:
		drop_save()
		get_tree().paused = true
	else:
		player.respawn()
""";

    const string GodotPlayer = """
extends CharacterBody2D
## Von Trendo: Spielfigur – Pfeiltasten laufen, Leertaste springt.

var level
var checkpoint := Vector2.ZERO
var speed := 220.0
const GRAVITY := 1400.0
var facing := 1.0
var vehicle := ""
var fuel := 0.0
var safe := 0.0
var no_enter := 0.0
var air := 0
var cool := 0.0

## Tempo mit Sprint (Fähigkeit, Umschalt) und Fahrzeug
func cur_speed(sprint: bool) -> float:
	return (speed + level.boost) * (1.6 if sprint and level.has("sprint") else 1.0) * (1.8 if vehicle == "wagen" else 1.3 if vehicle == "ufo" else 1.0)

## Sprung: am Boden normal, in der Luft einmal mit Fähigkeit „doppelsprung“
func jump() -> bool:
	if is_on_floor():
		velocity.y = -560.0
		return true
	if air > 0:
		air -= 1
		velocity.y = -520.0
		return true
	return false

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if (event.keycode == KEY_X or event.keycode == KEY_J) and cool <= 0:
			cool = 0.33
			level.attack()
		elif event.keycode == KEY_E:
			level.dismount()

func setup(l, at: Vector2, color: Color, s: float) -> void:
	level = l
	position = at
	checkpoint = at
	speed = s
	var r := ColorRect.new()
	r.size = Vector2(26, 30)
	r.position = Vector2(3, 2)
	r.color = color
	add_child(r)
	var c := CollisionShape2D.new()
	var sh := RectangleShape2D.new()
	sh.size = Vector2(26, 30)
	c.shape = sh
	c.position = Vector2(16, 17)
	add_child(c)
	var cam := Camera2D.new()
	add_child(cam)

func respawn() -> void:
	position = checkpoint
	velocity = Vector2.ZERO

func _physics_process(delta: float) -> void:
	safe = maxf(0.0, safe - delta)
	no_enter = maxf(0.0, no_enter - delta)
	cool = maxf(0.0, cool - delta)
	if is_on_floor():
		air = 1 if level.has("doppelsprung") else 0
	velocity.y += GRAVITY * delta
	var h := Input.get_axis("ui_left", "ui_right")
	if h != 0:
		facing = signf(h)
	velocity.x = h * cur_speed(Input.is_key_pressed(KEY_SHIFT))
	if Input.is_action_just_pressed("ui_accept"):
		jump()
	if vehicle == "ufo" and Input.is_action_pressed("ui_accept") and fuel > 0:
		velocity.y = maxf(velocity.y - 2400.0 * delta, -360.0)
		fuel -= delta
		if fuel <= 0:
			vehicle = ""
			level.note = "Treibstoff leer"
	move_and_slide()
	if position.y > 3000:
		level.hit(true)
""";

    const string GodotCheck = """
extends SceneTree
## Von Trendo: Prüfung ohne Fenster – jedes Level braucht Start (S) und Ziel (Z).

func _init() -> void:
	var f := FileAccess.open("res://level.json", FileAccess.READ)
	if f == null:
		print("TRENDO-FEHLER level.json fehlt")
		quit(1)
		return
	var d = JSON.parse_string(f.get_as_text())
	if not (d is Dictionary) or not d.has("levels"):
		print("TRENDO-FEHLER level.json ist kaputt")
		quit(1)
		return
	for l in d["levels"]:
		var all := "".join(PackedStringArray(l["rows"]))
		if all.count("S") != 1 or all.count("Z") < 1:
			print("TRENDO-FEHLER " + String(l["name"]) + ": Start oder Ziel fehlt")
			quit(1)
			return
	print("TRENDO-OK %d Level" % d["levels"].size())
	quit(0)
""";

    const string GodotPlayCheck = """
extends SceneTree
## Von Trendo: Spielprüfung ohne Fenster – Hauptszene 2 Sekunden laufen lassen, die Figur muss stehen und darf nicht durchfallen.

var frames := 0
var main: Node

func _initialize() -> void:
	main = load("res://main.tscn").instantiate()
	root.add_child(main)

func _process(_delta: float) -> bool:
	frames += 1
	if frames < 120:
		return false
	var p = main.get("player")
	if p == null:
		print("TRENDO-FEHLER keine Spielfigur (Start S fehlt?)")
		quit(1)
	elif not p.is_on_floor() or p.position.y > 2000:
		print("TRENDO-FEHLER Figur steht nicht auf dem Boden")
		quit(1)
	else:
		print("TRENDO-OK Spiel läuft, Figur steht")
		quit(0)
	return true
""";

    const string GodotSaveCheck = """
extends SceneTree
## Von Trendo: Spielstand-Prüfung ohne Fenster – speichern, im richtigen Level fortsetzen, Fremdes ablehnen.

var frames := 0
var main: Node
const P := "user://trendo-test-spielstand.json"

func _initialize() -> void:
	main = load("res://main.tscn").instantiate()
	root.add_child(main)

func fail(t: String) -> void:
	print("TRENDO-FEHLER " + t)
	quit(1)

func _process(_delta: float) -> bool:
	frames += 1
	if frames < 10:
		return false
	var n: int = main.data["levels"].size()
	main.resume({"level": n - 1, "score": 42, "lives": 2, "checkX": -1, "checkY": -1})
	main.save_game(P)
	var s: Dictionary = main.read_save(P, n)
	if s.is_empty() or int(s["level"]) != n - 1 or int(s["score"]) != 42 or int(s["version"]) != 1:
		fail("Spielstand stimmt nach dem Speichern nicht")
		return true
	if n > 1 and not main.read_save(P, n - 1).is_empty():
		fail("Spielstand mit fehlendem Level wurde angenommen")
		return true
	var f := FileAccess.open(P, FileAccess.WRITE)
	f.store_string('{"version":9,"level":0}')
	f.close()
	if not main.read_save(P, n).is_empty():
		fail("neuere Fassung wurde angenommen")
		return true
	f = FileAccess.open(P, FileAccess.WRITE)
	f.store_string("{kaputt")
	f.close()
	if not main.read_save(P, n).is_empty():
		fail("kaputte Datei wurde angenommen")
		return true
	DirAccess.remove_absolute(P)
	print("TRENDO-OK Spielstand speichern, fortsetzen und prüfen")
	quit(0)
	return true
""";

    const string GodotShot = """
extends Area2D
## Von Trendo: Geschoss der Waffe „Werfen“ – fliegt bis zur Wand oder zum Gegner.

var level
var dir := 1.0
var life := 1.5

func setup(l, at: Vector2, d: float) -> void:
	level = l
	position = at
	dir = d
	var r := ColorRect.new()
	r.size = Vector2(8, 8)
	r.position = Vector2(-4, -4)
	r.color = Color(1, 0.85, 0.2)
	add_child(r)
	var c := CollisionShape2D.new()
	var sh := RectangleShape2D.new()
	sh.size = Vector2(8, 8)
	c.shape = sh
	add_child(c)
	area_entered.connect(_hit_area)
	body_entered.connect(_hit_body)

func _physics_process(delta: float) -> void:
	position.x += dir * 420.0 * delta
	life -= delta
	if life <= 0:
		queue_free()

func _hit_area(a: Area2D) -> void:
	if String(a.name).begins_with("Gegner") and not is_queued_for_deletion():
		level.damage(a)
		queue_free()

func _hit_body(b: Node) -> void:
	if b is StaticBody2D:
		queue_free()
""";

    const string GodotGameplayCheck = """
extends SceneTree
## Von Trendo: Gameplay-Prüfung ohne Fenster – Gegenstand, Figur, Aufgabe, Schwert, Boss, Fahrzeug, Doppelsprung.

var frames := 0
var main: Node
var jumped := false
var at := 0
const DATA := '{"name":"Test","speed":5,"lives":3,"weapon":"schwert","abilities":["doppelsprung"],"levels":[{"name":"L","rows":["..........","SGNE.V...Z","##########"],"decor":[]}],"props":[{"key":"3,1:hp","value":2}],"texts":[{"key":"1,1:name","value":"Apfel"},{"key":"2,1:need","value":"Apfel"},{"key":"2,1:thanks","value":"Lecker!"},{"key":"2,1:give","value":"leben"},{"key":"5,1:kind","value":"wagen"}],"rules":[{"when":"gegenstaende","n":1,"act":"punkte","title":"Finder"}]}'

func _initialize() -> void:
	main = load("res://main.tscn").instantiate()
	root.add_child(main)

func part(prefix: String) -> Node:
	for c in main.get_children():
		if String(c.name).begins_with(prefix) and not c.is_queued_for_deletion():
			return c
	return null

func fail(t: String) -> bool:
	print("TRENDO-FEHLER " + t)
	quit(1)
	return true

func _process(_delta: float) -> bool:
	frames += 1
	if frames == 5:
		main.data = JSON.parse_string(DATA)
		main.lives = 3
		main.score = 0
		main.fired = [false]
		main.build(0)
		return false
	if frames < 40:
		return false
	if frames == 40:
		main.touch(part("Gegenstand"))
		if main.inventory != ["Apfel"]:
			return fail("Gegenstand nicht im Inventar")
		main.touch(part("Figur"))
		if main.lives != 4 or main.note != "Lecker!" or main.inventory.size() != 0:
			return fail("Figur hat nicht belohnt")
		var boss = part("Gegner")
		if boss.get_meta("max") != 2:
			return fail("Boss-Trefferpunkte fehlen")
		main.player.position = boss.position - Vector2(30, 0)
		main.player.facing = 1.0
		main.attack()
		if boss.get_meta("hp") != 1:
			return fail("Schwert hat nicht getroffen")
		main.attack()
		if main.defeated != 1 or main.note != "Boss besiegt!":
			return fail("Boss nicht besiegt")
		var foot: float = main.player.cur_speed(false)
		main.touch(part("Fahrzeug"))
		if main.player.vehicle != "wagen" or absf(main.player.cur_speed(false) - foot * 1.8) > 0.01:
			return fail("Wagen nicht genommen")
		main.hit()
		main.hit()
		if main.player.vehicle != "" or main.lives != 4:
			return fail("Wagen hat den Treffer nicht abgefangen")
		return false
	if frames == 41:
		if not main.quests().contains("✓ Finder"):
			return fail("Aufgabe nicht erfüllt: " + main.quests())
		return false
	if not jumped:
		if main.player.is_on_floor() and main.player.air == 1:
			jumped = main.player.jump()
			at = frames
		return false
	if frames == at + 5:
		if not main.player.jump() or main.player.jump():
			return fail("Doppelsprung stimmt nicht")
		print("TRENDO-OK Gameplay: Inventar, Figur, Aufgabe, Schwert, Boss, Fahrzeug, Doppelsprung")
		quit(0)
		return true
	if frames > 400:
		return fail("Figur kam nicht auf den Boden")
	return false
""";
}
