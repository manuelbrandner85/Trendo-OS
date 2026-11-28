using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>C5/C6 (Freigabe „Alle C1–C6 ja“, 08.11.2026): Claude arbeitet direkt in Blender, Unreal oder Godot – über MCP im Projektordner.
/// Trendo schreibt je Projekt eine <c>.mcp.json</c> (liest Claude Code dort) mit genau den Servern, die das Studio braucht, übernommen aus der
/// vorhandenen Claude-Einstellung der Person (<c>~/.claude.json</c>, nur lesen). Globale Einstellungen werden nie geändert.
/// Fremde Einträge in der Projektdatei bleiben erhalten; Trendo verwaltet nur seine eigenen („trendo-…“).
/// Unity: kein MCP, nur vorhandene Installation über die Kommandozeile (Batchmode) – Unity wird nie geladen.</summary>
public static class StudioMcp
{
    public const string FileName = ".mcp.json";
    public const string Prefix = "trendo-";
    public const string KittlMcpUrl = "https://mcp.kittl.com/mcp";

    /// <summary>Welche Programme ein Studio braucht (Engine des Spiels entscheidet bei CORE-06).</summary>
    public static IReadOnlyList<string> Needs(string studioId, string? engine = null) => studioId switch
    {
        "CORE-05" or "SPEC-04" or "SPEC-09" => ["blender"],
        "CORE-06" => (engine ?? "").ToLowerInvariant() switch { var e when e.Contains("godot") => ["godot", "blender"], var e when e.Contains("unity") => ["blender"], "browser" or "web" => [], _ => ["unreal", "blender"] },
        _ => [],
    };

    /// <summary>Server-Einträge aus der Claude-Einstellung der Person, passend zu einem Programm (Name enthält „blender“, „unreal“ oder „godot“).</summary>
    public static JsonObject? FindServer(JsonObject? userConfig, string program)
    {
        if (userConfig?["mcpServers"] is not JsonObject servers) return null;
        foreach (var (name, node) in servers)
            if (name.Contains(program, StringComparison.OrdinalIgnoreCase) && !name.StartsWith(Prefix, StringComparison.Ordinal) && node is JsonObject s && s["command"] is not null)
                return (JsonObject)s.DeepClone();
        return null;
    }

    /// <summary>T-0785 (H3): lesende bzw. ungefährliche Werkzeuge des Blender-MCP, die Claude ohne Rückfrage nutzen darf. <c>execute_blender_code*</c> bleibt
    /// nutzbar, ist aber NICHT vorab erlaubt – es läuft durch die Auto-Prüfung von Claude Code (beliebiges Python).</summary>
    public static readonly IReadOnlyList<string> BlenderSafeTools =
    [
        "get_blendfile_summary_datablocks", "get_blendfile_summary_datablocks_for_cli", "get_blendfile_summary_missing_files", "get_blendfile_summary_missing_files_for_cli",
        "get_blendfile_summary_of_linked_libraries", "get_blendfile_summary_of_linked_libraries_for_cli", "get_blendfile_summary_path_info", "get_blendfile_summary_path_info_for_cli",
        "get_blendfile_summary_usage_guess", "get_blendfile_summary_usage_guess_for_cli", "get_object_detail_summary", "get_objects_summary", "get_python_api_docs",
        "get_screenshot_of_area_as_image", "get_screenshot_of_window_as_image", "get_screenshot_of_window_as_json",
        "jump_to_tab_by_name", "jump_to_tab_by_space_type", "jump_to_view3d_object_by_name", "jump_to_view3d_object_data_by_name",
        "render_thumbnail_to_path", "render_viewport_to_path", "search_api_docs", "search_manual_docs",
    ];

    /// <summary>Freigabeliste für <c>--allowedTools</c> (einzelne Werkzeuge, kein ganzer Server).</summary>
    public static string BlenderAllowedTools => string.Join(" ", BlenderSafeTools.Select(t => "mcp__" + Prefix + "blender__" + t));

    /// <summary>Steht ein Server in der Projektdatei?</summary>
    public static bool HasServer(string? mcpFile, string name)
    {
        try { return mcpFile is not null && File.Exists(mcpFile) && JsonNode.Parse(File.ReadAllText(mcpFile))?["mcpServers"]?[name] is JsonObject; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>T-0785: Hinweis an Claude, wenn Blender verbunden ist – Trendo hält die Verbindung und startet Blender nach einem Absturz neu.</summary>
    public static string BlenderRule(bool maySave) => "Blender ist über den MCP-Server „trendo-blender“ sichtbar verbunden; du hast alle seine Werkzeuge (Szene lesen, Python ausführen, rendern, exportieren). " +
        "Trendo hält die Verbindung: Scheitert ein Blender-Werkzeug mit einem Verbindungsfehler, verbindet Trendo neu (nach einem Absturz startet es Blender neu) – warte dann etwa 10 Sekunden und wiederhole den Schritt. " +
        (maySave ? "Die offene Datei gehört zum Projekt: wichtige Zwischenstände speicherst du mit bpy.ops.wm.save_mainfile(). "
                 : "Die offene Datei gehört der Person und nicht zum Projekt: speichere sie NICHT (kein save_mainfile, kein save_as über ihre Datei), außer die Person sagt es ausdrücklich. Trendo kann das nicht technisch sperren – halte dich daran. ") +
        "Vor dem Löschen oder Überschreiben von Dateien außerhalb der Szene fragst du nach.";

    public static JsonObject? ReadUserConfig(string? path = null)
    {
        path ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");
        try { return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null; } catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    public sealed record Result(string ProjectDir, IReadOnlyList<string> Added, IReadOnlyList<string> Missing);

    /// <summary>Legt bzw. aktualisiert <c>.mcp.json</c> im Projektordner und liefert den Ordner. Fehlende Server stehen in <see cref="Result.Missing"/>.</summary>
    public static Result EnsureProjectConfig(string projectDir, string studioId, string? engine = null, JsonObject? userConfig = null, bool kittl = false, int? blenderPort = null)
    {
        Directory.CreateDirectory(projectDir);
        var file = Path.Combine(projectDir, FileName);
        JsonObject doc;
        try { doc = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? new JsonObject() : new JsonObject(); }
        catch (JsonException) { File.Copy(file, file + ".kaputt", true); doc = new JsonObject(); } // beschädigte Datei sichern, nicht still überschreiben
        if (doc["mcpServers"] is not JsonObject servers) doc["mcpServers"] = servers = new JsonObject();
        foreach (var key in servers.Select(kv => kv.Key).Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).ToList()) servers.Remove(key); // nur eigene ersetzen
        userConfig ??= ReadUserConfig();
        engine ??= EngineOfProject(projectDir); // Spiele: Engine aus den Projektdateien, wenn der Aufrufer sie nicht kennt
        var added = new List<string>(); var missing = new List<string>();
        foreach (var p in Needs(studioId, engine))
            if (FindServer(userConfig, p) is { } s)
            {
                // T-0785: Blender-Brücke auf dem Port, den Trendos Verbindung hält (eigene Instanz) – die Einstellung der Person bleibt unverändert
                if (p == "blender" && blenderPort is { } port) { if (s["env"] is not JsonObject env) s["env"] = env = new JsonObject(); env[BlenderBridge.PortVariable] = port.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                servers[Prefix + p] = s; added.Add(p);
            }
            else missing.Add(p);
        if (kittl) { servers[Prefix + "kittl"] = new JsonObject { ["type"] = "http", ["url"] = KittlMcpUrl }; added.Add("kittl"); } // offizieller Zugang; die Anmeldung läuft über Claude im Browser, Trendo sieht keine Zugangsdaten
        File.WriteAllText(file, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return new(projectDir, added, missing);
    }

    /// <summary>Engine eines Spielprojekts aus seinen Dateien: .uproject → unreal, project.godot → godot, ProjectSettings/ProjectVersion.txt → unity, sonst null.</summary>
    /// <summary>T-0630: vorhandene Projektdateien zählen vor der ausdrücklichen Wahl im Dokument; eine reine Regelentscheidung zählt hier nicht.</summary>
    public static string? EngineFor(string projectDir, string? chosen) => EngineOfProject(projectDir) ?? (string.IsNullOrWhiteSpace(chosen) ? null : chosen);

    public static string? EngineOfProject(string projectDir)
    {
        if (!Directory.Exists(projectDir)) return null;
        if (Directory.EnumerateFiles(projectDir, "*.uproject", SearchOption.TopDirectoryOnly).Any()) return "unreal";
        if (File.Exists(Path.Combine(projectDir, "project.godot"))) return "godot";
        if (File.Exists(Path.Combine(projectDir, "ProjectSettings", "ProjectVersion.txt"))) return "unity";
        return null;
    }

    /// <summary>Argumente zum sichtbaren Start eines Programms mit dem Projekt: Unreal mit der .uproject-Datei, Godot im Editor mit dem Projektordner,
    /// Blender mit der jüngsten .blend-Datei im Projekt (sonst leer). Nur Dateien innerhalb des Projektordners.</summary>
    public static IReadOnlyList<string> LaunchArgs(string program, string projectDir)
    {
        string? Newest(string pattern) => Directory.Exists(projectDir) ? Directory.EnumerateFiles(projectDir, pattern, SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
        return program switch
        {
            "unreal" => Newest("*.uproject") is { } u ? [u] : [],
            "godot" => File.Exists(Path.Combine(projectDir, "project.godot")) ? ["--editor", "--path", projectDir] : [],
            "blender" => Directory.Exists(projectDir) && Directory.EnumerateFiles(projectDir, "*.blend", SearchOption.TopDirectoryOnly).Where(BlenderLink.IsProjectBlend).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() is { } b ? [b] : [], // T-0785: ohne Trendos Sicherungen und wiederhergestellte Kopien
            _ => [],
        };
    }

    /// <summary>Unity im Batchmode (nur vorhandene Installation): Argumente für einen Aufruf einer statischen Methode im Projekt; Log in den Projektordner.</summary>
    public static IReadOnlyList<string> UnityBatchArgs(string projectDir, string executeMethod) =>
        ["-batchmode", "-nographics", "-quit", "-projectPath", projectDir, "-executeMethod", executeMethod, "-logFile", Path.Combine(projectDir, "Logs", "trendo-unity.log")];
}
