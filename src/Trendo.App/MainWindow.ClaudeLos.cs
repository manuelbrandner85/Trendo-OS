using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude als Standard (C1–C4, freigegeben 08.11.2026): Ist Claude Code über das Abo verbunden und als Haupt-KI eingeschaltet, setzt Claude jeden Auftrag
/// aus der KI-Zeile eines Studios um – im Claude-Bereich rechts, im Projekt des Studios, wie in Claude Code (Schritte live, Rückfragen, Ergebnis im Programm).
/// Weitere Aufträge aus derselben Zeile gehen in dieselbe Sitzung (ändern, ergänzen). Die Werkzeug-KIs (Bild, Video, Musik, Stimme, Mitschrift) bestellt Claude selbst.
/// Ohne Claude, ohne Abo oder während einer Abo-Pause arbeitet wie bisher die Trendo-Planung mit der KI auf dem PC.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Projekt-MCP-Vorbereitung auch im abgeschotteten Testmodus ausführen.</summary>
    public static Func<string, string, StudioMcp.Result>? StudioMcpOverride { get; set; }

    /// <summary>C5: MCP-Server, die das Studio braucht (Blender, Unreal, Godot), in die .mcp.json des Projekts – Pfad der Datei oder null, wenn nichts eingetragen ist.
    /// Claude Code lädt Projekt-Server im Druckmodus nicht von selbst; darum gibt Trendo die Datei ausdrücklich mit (--mcp-config).</summary>
    string? ClaudeStudioMcp(string folder, string? studio)
    {
        if (studio is null) return null;
        try
        {
            StudioMcp.Result r;
            if (StudioMcpOverride is { } o) r = o(folder, studio);
            else if (_opt.Isolated) return null;
            else r = StudioMcpPrepare(folder, studio);
            var file = Path.Combine(folder, ".mcp.json");
            return r.Added.Count > 0 && File.Exists(file) ? file : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return null; }
    }

    static IReadOnlyList<string> ClaudeWithMcp(IReadOnlyList<string> args, string? mcpFile, string? bridgeFile = null)
    {
        var files = new[] { mcpFile, bridgeFile }.OfType<string>().ToList();
        if (files.Count == 0) return args;
        IReadOnlyList<string> all = bridgeFile is null ? args : [.. args, "--allowedTools", TrendoMcp.AllowedTools];
        return [.. all, "--mcp-config", .. files];
    }

    /// <summary>Übernimmt Claude die Aufträge der Studio-Zeile? (verbunden über das Abo, als Haupt-KI eingeschaltet, kein Abo-Limit gerade)</summary>
    public bool ClaudeTakesStudioPrompts =>
        (!_opt.Isolated || ClaudeStatusOverride is not null) && _claude.Ready && KiSourceSettings.Load(_core.DataRoot).ClaudeMain && !CLimit.Active(DateTimeOffset.Now);

    /// <summary>Auftrag aus der Studio-Zeile an Claude geben. false = Claude übernimmt nicht (dann plant Trendo selbst).</summary>
    public async Task<bool> ClaudeFromStudioBar(string? studio, string text, Action<string>? status = null)
    {
        if (string.IsNullOrWhiteSpace(text) || !ClaudeTakesStudioPrompts || Windows.Active?.Id is not { } id) return false;
        var opened = !_claudeDocks.ContainsKey(id);
        if (opened) ClaudeDockToggle(studio, null);
        if (!_claudeDocks.TryGetValue(id, out var dock)) return false;
        if (opened) ClaudeDockSetMax(dock, false); // Studio bleibt sichtbar – Claude arbeitet daneben im Programm
        if (dock.Tab != "chat") ClaudeDockShowTab(dock, "chat");
        if (_chatUis.TryGetValue(dock, out var ui) && ui.Running) { status?.Invoke("Claude arbeitet noch – dein Auftrag steht im Claude-Bereich bereit."); ui.Input.Text = text.Trim(); return true; }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.studio.los", studio ?? "");
        status?.Invoke("✳ Claude arbeitet rechts im Projekt …");
        await ClaudeChatSend(dock, text);
        status?.Invoke("✳ Claude ist fertig – hier ändern oder ergänzen, Claude arbeitet im selben Projekt weiter");
        return true;
    }
}
