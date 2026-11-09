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

    public static JsonObject? ReadUserConfig(string? path = null)
    {
        path ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");
        try { return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null; } catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    public sealed record Result(string ProjectDir, IReadOnlyList<string> Added, IReadOnlyList<string> Missing);

    /// <summary>Legt bzw. aktualisiert <c>.mcp.json</c> im Projektordner und liefert den Ordner. Fehlende Server stehen in <see cref="Result.Missing"/>.</summary>
    public static Result EnsureProjectConfig(string projectDir, string studioId, string? engine = null, JsonObject? userConfig = null)
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
            if (FindServer(userConfig, p) is { } s) { servers[Prefix + p] = s; added.Add(p); }
            else missing.Add(p);
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
            "blender" => Newest("*.blend") is { } b ? [b] : [],
            _ => [],
        };
    }

    /// <summary>Unity im Batchmode (nur vorhandene Installation): Argumente für einen Aufruf einer statischen Methode im Projekt; Log in den Projektordner.</summary>
    public static IReadOnlyList<string> UnityBatchArgs(string projectDir, string executeMethod) =>
        ["-batchmode", "-nographics", "-quit", "-projectPath", projectDir, "-executeMethod", executeMethod, "-logFile", Path.Combine(projectDir, "Logs", "trendo-unity.log")];
}
