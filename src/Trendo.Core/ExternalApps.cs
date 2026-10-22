namespace Trendo.Core;

/// <summary>T-0203: externe Programme, die ein Studio mit der aktuellen Datei bzw. dem Projekt öffnet.</summary>
public sealed record ExternalApp(string Id, string Name, string ComponentId, string[] Exe, string[] Extensions, bool Folders, string Use);

public static class ExternalApps
{
    public static readonly IReadOnlyList<ExternalApp> All =
    [
        new("blender", "Blender", "blender", ["blender"], [".blend", ".glb", ".gltf", ".obj", ".stl", ".fbx", ".ply"], false, "3D-Modelle"),
        new("unreal", "Unreal Engine", "unreal", ["UnrealEditor"], [".uproject"], false, "Spiele"),
        new("vscode", "VS Code", "vscode", ["code"], [".html", ".css", ".js", ".ts", ".json", ".md", ".txt", ".py", ".cs", ".xml", ".yaml", ".yml"], true, "Code und Webseiten"),
        new("libreoffice", "LibreOffice", "libreoffice", ["soffice", "libreoffice"], [".docx", ".doc", ".odt", ".rtf", ".xlsx", ".xls", ".ods", ".csv", ".pptx", ".odp", ".txt"], false, "Texte, Tabellen, Präsentationen"),
        new("obs", "OBS Studio", "obs", ["obs64", "obs"], [".json"], false, "Aufnehmen und Streamen"),
    ];

    public static ExternalApp Get(string id) => All.First(a => a.Id == id);

    /// <summary>Kann das Programm diese Datei bzw. diesen Ordner öffnen?</summary>
    public static bool Accepts(ExternalApp app, string path) =>
        Directory.Exists(path) ? app.Folders : app.Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Argumente für „mit dieser Datei öffnen“. Blender importiert Austauschformate in eine leere Szene; OBS lädt die Szenensammlung.</summary>
    public static IReadOnlyList<string> Plan(ExternalApp app, string path, string? obsScenesDir = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Bitte zuerst eine Datei wählen.");
        if (!File.Exists(path) && !Directory.Exists(path)) throw new IOException($"„{Path.GetFileName(path)}“ gibt es nicht (mehr).");
        if (!Accepts(app, path)) throw new ArgumentException($"{app.Name} kann {(Directory.Exists(path) ? "keinen Ordner" : Path.GetExtension(path) + "-Dateien")} nicht öffnen.");
        var full = Path.GetFullPath(path);
        switch (app.Id)
        {
            case "blender" when !full.EndsWith(".blend", StringComparison.OrdinalIgnoreCase):
                var ext = Path.GetExtension(full).ToLowerInvariant();
                var op = ext switch { ".glb" or ".gltf" => "bpy.ops.import_scene.gltf", ".obj" => "bpy.ops.wm.obj_import", ".stl" => "bpy.ops.wm.stl_import", ".fbx" => "bpy.ops.import_scene.fbx", _ => "bpy.ops.wm.ply_import" };
                var lit = full.Replace("\\", "\\\\").Replace("'", "\\'");
                return ["--python-expr", $"import bpy; bpy.ops.wm.read_factory_settings(use_empty=True); {op}(filepath='{lit}')"];
            case "obs":
                // OBS öffnet keine Datei direkt: Szenensammlung in den OBS-Ordner legen und per Name laden.
                var name = Path.GetFileNameWithoutExtension(full);
                if (obsScenesDir is not null) { Directory.CreateDirectory(obsScenesDir); File.Copy(full, Path.Combine(obsScenesDir, Path.GetFileName(full)), true); }
                return ["--collection", name];
            default:
                return [full];
        }
    }

    /// <summary>Szenen-Ordner von OBS (Windows: %APPDATA%\obs-studio, Linux: ~/.config/obs-studio).</summary>
    public static string ObsScenesDir() => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "scenes")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "obs-studio", "basic", "scenes");
}

public static class ObsScenes
{
    /// <summary>Minimale OBS-Szenensammlung: eine (leere) Szene je Programmpunkt, in Ablauf-Reihenfolge.</summary>
    public static string Collection(string name, IEnumerable<string> scenes)
    {
        var list = scenes.Select((s, i) => string.IsNullOrWhiteSpace(s) ? $"Szene {i + 1}" : s.Trim()).Distinct().ToList();
        if (list.Count == 0) throw new InvalidOperationException("Bitte zuerst Programmpunkte anlegen.");
        var o = new Dictionary<string, object?>
        {
            ["name"] = name, ["current_scene"] = list[0], ["current_program_scene"] = list[0],
            ["scene_order"] = list.Select(s => new Dictionary<string, string> { ["name"] = s }).ToList(),
            ["sources"] = list.Select(s => new Dictionary<string, object> { ["id"] = "scene", ["versioned_id"] = "scene", ["name"] = s, ["settings"] = new Dictionary<string, object> { ["items"] = Array.Empty<object>() } }).ToList(),
        };
        return System.Text.Json.JsonSerializer.Serialize(o, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
