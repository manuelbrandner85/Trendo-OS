using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude als Standard (C1–C4, freigegeben 08.11.2026): Ist Claude Code über das Abo verbunden und als Haupt-KI eingeschaltet, setzt Claude jeden Auftrag
/// aus der KI-Zeile eines Studios um – im Claude-Bereich rechts, im Projekt des Studios, wie in Claude Code (Schritte live, Rückfragen, Ergebnis im Programm).
/// Weitere Aufträge aus derselben Zeile gehen in dieselbe Sitzung (ändern, ergänzen). Die Werkzeug-KIs (Bild, Video, Musik, Stimme, Mitschrift) bestellt Claude selbst.
/// Ohne Claude, ohne Abo oder während einer Abo-Pause arbeitet wie bisher die Trendo-Planung mit der KI auf dem PC.</summary>
public partial class MainWindow
{
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
