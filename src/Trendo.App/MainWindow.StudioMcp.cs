using Trendo.Core;

namespace Trendo.App;

/// <summary>C5/C6: Claude arbeitet direkt in Blender, Unreal oder Godot. Für Sitzung A (Claude-Chat): <see cref="StudioMcpPrepare"/> vor dem Start von Claude Code
/// im Projektordner aufrufen; <see cref="StudioLaunchProgram"/> startet das Programm sichtbar mit dem Projekt (protokollierter Startweg, kein Shell-Parsing).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Programmstart nachbilden (Programm, Argumente).</summary>
    public static Action<string, IReadOnlyList<string>>? StudioLaunchOverride { get; set; }

    /// <summary>Schreibt die <c>.mcp.json</c> des Projekts (nur Server, die das Studio braucht, aus der vorhandenen Claude-Einstellung) und liefert den Ordner samt fehlenden Servern.</summary>
    public StudioMcp.Result StudioMcpPrepare(string projectDir, string studioId, string? engine = null)
    {
        var r = StudioMcp.EnsureProjectConfig(projectDir, studioId, engine);
        _core.Audit.Append(Principal.LocalOwner.Name, "studio.mcp", $"{studioId} +{string.Join(",", r.Added)} fehlt:{string.Join(",", r.Missing)}");
        return r;
    }

    /// <summary>Startet Blender, Unreal oder Godot sichtbar mit dem Projekt. Unity wird nicht gestartet (nur Batchmode über <see cref="StudioMcp.UnityBatchArgs"/>).
    /// Liefert null bei Erfolg, sonst einen verständlichen Grund.</summary>
    public string? StudioLaunchProgram(string program, string projectDir)
    {
        if (program is not ("blender" or "unreal" or "godot")) return $"„{program}“ wird nicht automatisch gestartet.";
        var exe = _opt.Isolated && StudioLaunchOverride is null ? null : EngineDetect().FirstOrDefault(e => e.Id.Equals(program, StringComparison.OrdinalIgnoreCase) && e.Installed && e.Exe is not null)?.Exe;
        if (StudioLaunchOverride is { } fake) { fake(exe ?? program, StudioMcp.LaunchArgs(program, projectDir)); return null; }
        if (exe is null) return $"{program} ist auf diesem PC nicht installiert.";
        _core.Integrations.LaunchArgs(Principal.LocalOwner, exe, _core.Audit, StudioMcp.LaunchArgs(program, projectDir));
        return null;
    }
}
