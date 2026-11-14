using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Claude arbeitet direkt im Programm des Studios (Wunsch 02.11.2026): legt Claude ein Projekt an, legt Trendo im Programm des Studios (Spiele bauen, Grundriss & Wohnen,
/// Schaltung, Bildbearbeitung, Videoschnitt …) gleich ein Dokument mit dem Namen des Projekts an, speichert es im Projektordner, öffnet das Programm und Claude arbeitet darin.
/// Beim Fortsetzen öffnet sich dasselbe Dokument wieder.</summary>
public partial class MainWindow
{
    /// <summary>Programm des Studios, in dem Claude arbeitet: die offene Seite, wenn sie ein Programm des Studios ist, sonst das erste Programm des Studios. null = Studio ohne eigenes Programm.</summary>
    string? ClaudeProgramPage(ClaudeDock dock)
    {
        if (dock.Studio is not { } studio) return null;
        bool Of(string p) => Programs.TryGetValue(p, out var s) && s.Studio == studio;
        return Of(dock.Page) ? dock.Page : Of(CurrentView) ? CurrentView : Programs.Values.FirstOrDefault(s => s.Studio == studio)?.Key;
    }

    /// <summary>Für das Projekt das Dokument im Programm bereitstellen und das Programm zeigen. Neues Projekt: das gerade offene, noch nicht verknüpfte Dokument übernehmen
    /// (wenn du es vor dir hast), sonst ein neues anlegen – benannt wie das Projekt, gespeichert im Projektordner. Gibt das (ggf. aktualisierte) Projekt zurück.</summary>
    ClaudeChats.Chat ClaudeEnsureProgram(ClaudeDock dock, ClaudeChats.Chat chat)
    {
        if (ClaudeProgramPage(dock) is not { } page) return chat;
        var studio = ChatStudio(dock);
        try
        {
            ProgramDocument doc;
            if (chat.Doc is { } linked && File.Exists(linked)) doc = ProgramOpen(page, linked); // Projekt fortsetzen: sein Dokument
            else
            {
                var open = ProgramActive(page);
                var linkedElsewhere = open?.Path is { } op && CChats.All(studio).Any(c => c.Id != chat.Id && c.Doc is { } d && string.Equals(Path.GetFullPath(d), Path.GetFullPath(op), StringComparison.OrdinalIgnoreCase));
                // dein eigenes Dokument (gespeichert oder schon bearbeitet), das du gerade vor dir hast: Claude arbeitet darin – eine nur geöffnete Vorlage nicht
                if (open is not null && CurrentView == page && chat.Messages.Count == 0 && !linkedElsewhere && (open.Path is not null || open.Dirty)) doc = open;
                else { doc = ProgramNew(page, Programs[page].Templates.Any(t => t.Id == "leer") ? "leer" : null); doc.Title = chat.Title; }
                if (doc.Path is null)
                {
                    Directory.CreateDirectory(chat.Folder);
                    var file = Path.Combine(chat.Folder, Regex.Replace(doc.Title, @"[\\/:*?""<>|]", "-") + ProgramDocument.Extension);
                    _progActive[page] = ProgramDocs(page).IndexOf(doc);
                    ProgramSave(page, file);
                }
            }
            chat = CChats.SetDoc(studio, chat.Id, doc.Path!);
            _progMsg[page] = $"Claude arbeitet in „{doc.Title}“ – Änderungen erscheinen hier live.";
            _core.Audit.Append(Principal.LocalOwner.Name, "claude.programm.dokument", $"{studio} · {Path.GetFileName(doc.Path)}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException) { _claudeMsg = "Das Programm-Dokument ließ sich nicht anlegen: " + Trendo.Core.FriendlyError.Say(e); return chat; }
        ClaudeMoveToPage(dock, page);
        return chat;
    }

    /// <summary>Programm zeigen und den Claude-Bereich dorthin mitnehmen (jede Seite ist ein eigenes Fenster); Claude ist dann neben dem Programm, nicht darüber.</summary>
    void ClaudeMoveToPage(ClaudeDock dock, string page)
    {
        var oldId = _claudeDocks.FirstOrDefault(kv => kv.Value == dock).Key;
        if (CurrentView != page) Show(page);
        if (Windows.Active?.Id is { } newId && oldId is not null && newId != oldId)
        {
            // Claude-Bereich aus dem alten Fenster lösen: dort steht wieder nur die Seite
            if (dock.PageHost?.Content is Avalonia.Controls.Control oldContent && _frames.TryGetValue(oldId, out var of)) { dock.PageHost.Content = null; of.Body.Content = oldContent; }
            dock.Split?.Children.Remove(dock.Panel); dock.Split = null; dock.PageHost = null;
            _claudeDocks.Remove(oldId); _claudeDocks.Remove(newId, out var other); if (other is not null) CloseChat(other);
            _claudeDocks[newId] = dock;
        }
        dock.Page = page;
        if (dock.Max) ClaudeDockSetMax(dock, false); // Programm links, Claude rechts
        Show(page);
        foreach (var b in dock.Panel.GetLogicalDescendantsOf<Avalonia.Controls.Button>().Where(b => b.Name == "claude-dock-max")) b.Content = "Groß";
    }

    /// <summary>Was Claude neben dem Dokument gebaut hat, im Programm zeigen – im Spiele-Programm das spielbare Spiel (Vorschaubild, Spielen, Ordner).</summary>
    Avalonia.Controls.Control? ClaudeProgramExtra(string key) => key == GK && _gameClaude is { } gd && Directory.Exists(gd) ? ClaudeGameCard(gd) : null;

    /// <summary>Für Tests: Programm-Seite und Dokument des offenen Claude-Projekts.</summary>
    public (string? Page, string? Doc) ClaudeProjectProgram() => Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d) ? (ClaudeProgramPage(d), ClaudeChatCurrent()?.Doc) : (null, null);
}
