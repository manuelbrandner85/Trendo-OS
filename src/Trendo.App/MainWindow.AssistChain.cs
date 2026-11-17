using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Wohin ein KI-Auftrag aus einem Programm geht (eine Entscheidung für alle Einstiege, T-0707).</summary>
public enum AssistRoute
{
    /// <summary>Claude arbeitet im eigenen Bereich rechts (Abo) – mit der Brücke selbst im Programm, sonst im Projekt des Studios.</summary>
    ClaudeBereich,
    /// <summary>Die eine Kette ins offene Dokument: Plan → Vorschau Ja/Nein → ein Rückgängig-Schritt → Datei.</summary>
    Dokument,
    /// <summary>Programm außerhalb der Programm-Hülle: Studio-Ablauf, Ergebnis als Datei im Projekt, mit der Rückmeldung „Noch nicht im Dokument möglich“.</summary>
    Projekt,
}

/// <summary>T-0707 (Paket B5 „Ein Assistenzweg“): Einstiege in die eine Kette.
/// Vorher gab es vier getrennte Wege (Audit 16.11.2026, audit-6 Punkt 2.1): (A) Studio-Ablauf der Auftragszeile → nur Projektdatei,
/// (B) Programm-Assistent → nur Menüband-Befehle ohne KI-Inhalt, (C) Claude-Brücke → eigene Prüfung beim Schreiben, (D) KI-Befehle im Menüband („… mit Claude“)
/// → ohne Claude still nichts. Jetzt entscheidet <see cref="AssistRouteFor"/> für Auftragszeile und Menüband-Befehle gleich, der Assistent ist die Kette ins Dokument
/// (<see cref="AssistChain"/>), und die Claude-Brücke prüft ihre Änderungen mit derselben Regel (<see cref="AssistChain.CheckEdit"/>).</summary>
public partial class MainWindow
{
    /// <summary>Die eine Weiche. Claude im eigenen Bereich nur, wenn Claude dort selbst im Programm arbeitet (ohne Klicks über die Brücke) oder das Programm kein
    /// Dokument der Hülle hat; sonst geht der Auftrag in die Kette ins offene Dokument – die KI dort ist Claude (Abo) oder die KI auf diesem PC.</summary>
    public AssistRoute AssistRouteFor(string? key)
    {
        var target = AssistTargetFor(key);
        if (ClaudeTakesStudioPrompts && (ClaudeNoClicks || target is null)) return AssistRoute.ClaudeBereich;
        return target is not null ? AssistRoute.Dokument : AssistRoute.Projekt;
    }

    /// <summary>Programme aus dem Programmkatalog, die (noch) nicht in der Programm-Hülle laufen – dort sagt die Kette „Noch nicht im Dokument möglich“.
    /// Maßgeblich ist, was gerade angemeldet ist: zieht ein Programm in die Hülle (B6), fällt es hier von selbst heraus.</summary>
    public IReadOnlyList<(string Key, string Name, string Studio)> AssistOutsideShell() =>
        ProgramCatalog.Load().Where(e => !Programs.ContainsKey(e.Id)).OrderBy(e => e.Id, StringComparer.Ordinal).Select(e => (e.Id, e.Name, e.Studio)).ToList();

    /// <summary>Ist die Seite ein Programm außerhalb der Hülle? Dann gibt es eine klare Rückmeldung statt einer stillen Ausgabe; null = Programm der Hülle oder keine Programmseite.</summary>
    public string? AssistNotInDocument(string? key, bool fallsBackToProject = true)
    {
        if (key is null || AssistTargetFor(key) is not null) return null;
        if (Programs.TryGetValue(key, out var spec)) return AssistChain.NotInDocumentText(spec.Title, fallsBackToProject); // Hülle, aber kein Dokument offen
        var entry = ProgramCatalog.Load().FirstOrDefault(e => string.Equals(e.Id, key, StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : AssistChain.NotInDocumentText(entry.Name, fallsBackToProject);
    }

    /// <summary>Für Tests und die Claude-Brücke: letzte Rückmeldung eines KI-Befehls aus dem Menüband.</summary>
    public string? AssistProgramNote { get; private set; }

    /// <summary>Einstieg „KI-Befehl im Menüband“ (z. B. „Weiterschreiben mit Claude“): dieselbe Weiche wie die Auftragszeile. Ohne Claude arbeitet die KI auf diesem PC
    /// über die Kette im offenen Dokument – vorher passierte dann still nichts.</summary>
    public async Task<bool> AssistFromProgram(string key, string studio, string prompt)
    {
        AssistProgramNote = null;
        if (AssistQuiet > 0) return false; // nie aus einer Vorschau des Assistenten heraus
        switch (AssistRouteFor(key))
        {
            case AssistRoute.ClaudeBereich:
                return await ClaudeFromStudioBar(studio, prompt, t => _progMsg[key] = t);
            case AssistRoute.Dokument:
                if (CurrentView != key) Show(key);
                if (AssistHostOnPage() is not { } host || AssistTargetFor(key) is not { } target)
                    return AssistProgramSay(key, "Die Auftragszeile ist ausgeblendet – blende sie ein (Fokusmodus aus), dann arbeitet die KI hier im Dokument.");
                _assistKiError = null;
                if (await AssistStart(target, prompt, host, AssistEntry.Programmbefehl)) return true;
                return AssistProgramSay(key, _assistKiError is { } why ? "Keine KI hat geantwortet: " + why + " – Claude verbinden oder auf der KI-Seite ein Modell für diesen PC einrichten." : "Die KI konnte daraus keine Änderung im Dokument machen. Formuliere den Auftrag oben in der Zeile genauer.");
            default:
                return AssistProgramSay(key, AssistNotInDocument(key, false) ?? AssistChain.NotInDocumentLead + ".");
        }
    }

    bool AssistProgramSay(string key, string text)
    {
        AssistProgramNote = text; _progMsg[key] = text;
        _core.Audit.Append(Principal.LocalOwner.Name, "assist.hinweis", $"{key} {text}");
        if (CurrentView == key) Show(key);
        return false;
    }

    string? _assistKiError;

    /// <summary>Platz des Assistenten in der Auftragszeile der offenen Seite.</summary>
    ContentControl? AssistHostOnPage() => ActivePageRoot?.GetLogicalDescendants().OfType<ContentControl>().FirstOrDefault(c => c.Name == "assist-host");

    /// <summary>Kurze Meldung im Platz des Assistenten (Rechte, ungültiger Auftrag).</summary>
    void AssistNote(ContentControl host, string text) =>
        host.Content = new Border { Name = "assist-note", Background = Brush.Parse("#1AF5A524"), CornerRadius = new Avalonia.CornerRadius(10), Padding = new Avalonia.Thickness(10, 8), Margin = new Avalonia.Thickness(0, 4),
            Child = new TextBlock { Name = "assist-msg", Text = text, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap } };

    /// <summary>Während die KI plant: sichtbar, dass gearbeitet wird.</summary>
    void AssistBusy(ContentControl host, string title) =>
        host.Content = new Border { Name = "assist-busy", Background = Brush.Parse("#0F3F3F3F"), BorderBrush = GoldEdge, BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(12), Padding = new Avalonia.Thickness(12, 10), Margin = new Avalonia.Thickness(0, 4),
            Child = new TextBlock { Text = $"Die KI plant die Änderung in „{title}“ …", Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap } };
}
