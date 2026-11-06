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
            rules = s.Rules.Select(r => new { when = r.When, n = r.N, act = r.Do }),
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
        Put(dir, ".gitignore", ".godot/\nexport/\n");
        Put(dir, "LIESMICH.txt", $"{name} – von Trendo als Godot-4-Projekt angelegt.\nÖffnen: Godot → Importieren → project.godot. Start mit F5.\nPrüfen ohne Fenster: godot --headless --path . --script res://tests/check_level.gd\n");
        return dir;
    }

    // ───────────── Kommandozeilen (offiziell) ─────────────
    /// <summary>Spielstand-Prüfung in Godot: speichern, fortsetzen, neuere Fassung und kaputte Datei ablehnen.</summary>
    public static List<string> GodotSaveArgs(string project) => ["--headless", "--path", project, "--script", "res://tests/save_check.gd"];
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
[Serializable] public class TrendoRule { public string when; public int n; public string act; }

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

/// <summary>Baut das Trendo-Level als Spielszene: Boden mit Kollision, Münzen, Gegner, Stacheln, Sprungfedern, Schlüssel und Türen, Ziel.</summary>
public class TrendoLevel : MonoBehaviour
{
    public TrendoLevelData Data; public int Index; public int Coins, Keys, Score, Lives; public TrendoPlayer Player;
    static Sprite _square;
    readonly List<GameObject> _doors = new List<GameObject>();

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
    public TrendoSave Snapshot() => new TrendoSave { level = Index, score = Score, lives = Lives, coins = Coins, checkX = Player != null ? Mathf.RoundToInt(Player.Checkpoint.x) : -1, checkY = Player != null ? Mathf.RoundToInt(-Player.Checkpoint.y) : -1 };
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
        foreach (Transform c in transform) Destroy(c.gameObject);
        var cam = Camera.main; if (cam == null) { cam = new GameObject("Kamera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
        cam.orthographic = true; cam.orthographicSize = 7; cam.backgroundColor = TrendoLevelData.Hex(d.background, Color.black); cam.clearFlags = CameraClearFlags.SolidColor;
        var rows = d.levels[index].rows;
        for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++)
            {
                var p = new Vector3(x, -y, 0);
                switch (rows[y][x])
                {
                    case '#': Tile("Boden", p, new Color(0.35f, 0.4f, 0.5f), true, false); break;
                    case 'o': Tile("Münze", p, TrendoLevelData.Hex(d.accent, Color.yellow), false, true, 0.5f); break;
                    case '^': Tile("Stacheln", p, new Color(0.9f, 0.3f, 0.3f), false, true, 0.8f); break;
                    case 'E': Tile("Gegner", p, new Color(0.8f, 0.2f, 0.6f), false, true, 0.9f).AddComponent<TrendoEnemy>(); break;
                    case 'Z': Tile("Ziel", p, Color.green, false, true); break;
                    case 'F': Tile("Sprungfeder", p, new Color(1f, 0.6f, 0.1f), false, true, 0.6f); break;
                    case 'C': Tile("Speicherpunkt", p, Color.cyan, false, true, 0.6f); break;
                    case 'K': Tile("Schlüssel", p, new Color(1f, 0.85f, 0.2f), false, true, 0.5f); break;
                    case 'D': _doors.Add(Tile("Tür", p, new Color(0.55f, 0.35f, 0.2f), true, false)); break;
                    case 'S':
                        Player = new GameObject("Spielfigur").AddComponent<TrendoPlayer>(); Player.transform.SetParent(transform); Player.Setup(this, p, TrendoLevelData.Hex(d.player, Color.blue)); break;
                }
            }
    }

    GameObject Tile(string name, Vector3 p, Color c, bool solid, bool trigger, float size = 1f)
    {
        var g = new GameObject(name); g.transform.SetParent(transform); g.transform.position = p; g.transform.localScale = Vector3.one * size;
        var r = g.AddComponent<SpriteRenderer>(); r.sprite = Square(); r.color = c;
        var col = g.AddComponent<BoxCollider2D>(); col.isTrigger = trigger && !solid;
        return g;
    }

    public void Touch(GameObject g)
    {
        switch (g.name)
        {
            case "Münze": Coins++; Score += 10; Destroy(g); break;
            case "Schlüssel": Keys++; Destroy(g); if (Keys >= Data.Count(Index, 'K')) foreach (var d in _doors) Destroy(d); break;
            case "Stacheln": case "Gegner": Hit(); break;
            case "Sprungfeder": Player.Bounce(); break;
            case "Speicherpunkt": if (Player.Checkpoint != g.transform.position) { Player.Checkpoint = g.transform.position; Save(); } break;
            case "Ziel": Score += 10; if (Index + 1 < Data.levels.Length) { Begin(Data, Index + 1); Save(); } else { Won = true; Drop(); } break;
        }
    }
    public bool Won, Over;
    public void Hit() { Lives--; if (Lives <= 0) { Over = true; Drop(); } else Player.Respawn(); }
    void Drop() { if (System.IO.File.Exists(FilePath)) System.IO.File.Delete(FilePath); }
}

/// <summary>Gegner läuft hin und her.</summary>
public class TrendoEnemy : MonoBehaviour
{
    Vector3 _start; float _t;
    void Start() => _start = transform.position;
    void Update() { _t += Time.deltaTime; transform.position = _start + Vector3.right * Mathf.Sin(_t * 1.5f) * 1.5f; }
}
""";

    const string UnityPlayer = """
using UnityEngine;

/// <summary>Spielfigur: Pfeiltasten/A-D laufen, Leertaste springt.</summary>
public class TrendoPlayer : MonoBehaviour
{
    public Vector3 Checkpoint; TrendoLevel _level; Rigidbody2D _rb; float _speed;

    public void Setup(TrendoLevel level, Vector3 at, Color color)
    {
        _level = level; transform.position = Checkpoint = at;
        var r = gameObject.AddComponent<SpriteRenderer>(); r.sprite = TrendoLevel.Square(); r.color = color; transform.localScale = new Vector3(0.8f, 0.9f, 1);
        gameObject.AddComponent<BoxCollider2D>();
        _rb = gameObject.AddComponent<Rigidbody2D>(); _rb.freezeRotation = true; _rb.gravityScale = 3;
        _speed = 2f + level.Data.speed;
    }

    public void Respawn() { transform.position = Checkpoint; _rb.linearVelocity = Vector2.zero; }
    public void Bounce() => _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 16f);

    void Update()
    {
        if (_rb == null) return;
        var h = Input.GetAxisRaw("Horizontal");
        _rb.linearVelocity = new Vector2(h * _speed, _rb.linearVelocity.y);
        if (Input.GetKeyDown(KeyCode.Space) && Mathf.Abs(_rb.linearVelocity.y) < 0.01f) _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 11f);
        if (transform.position.y < -40) _level.Hit();
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
				"E": area("Gegner", p, Color(0.8, 0.2, 0.6))
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
			for d in doors:
				d.queue_free()
	elif n.begins_with("Stacheln") or n.begins_with("Gegner"):
		hit()
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

func hit() -> void:
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
	velocity.y += GRAVITY * delta
	velocity.x = Input.get_axis("ui_left", "ui_right") * speed
	if is_on_floor() and Input.is_action_just_pressed("ui_accept"):
		velocity.y = -560.0
	move_and_slide()
	if position.y > 3000:
		level.hit()
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
}
