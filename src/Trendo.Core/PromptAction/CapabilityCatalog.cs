using System.Text;

namespace Trendo.Core.PromptAction;

public sealed record StudioProfile(
    string Id,
    string Title,
    string Area,
    string Existing,
    string PlannedAction,
    string Inputs,
    string Outputs,
    string Blocker);

/// <summary>Alle Einträge aus StudioCatalog. Reife kommt nur aus registrierten Adaptern, nie aus der Wunschliste.</summary>
public static class CapabilityCatalog
{
    private static readonly StudioProfile[] All =
    [
        P("STUDIO-LETTER-01", "Briefe schreiben", "Arbeiten & Büro", "Letters, LetterArchive, LetterCase, PdfExport", "Brief als DOCX entwerfen", "Absender, Empfänger, Betreff, Text", "DOCX, optional PDF", "PDF nur, wenn LibreOffice (soffice) vorhanden ist."),
        P("SYS-10", "Texte & Tabellen", "Arbeiten & Büro", "Ooxml, Csv, Pdf, Writing", "Dokument oder Tabelle mit geprüfter Formel", "Text oder Zellen", "DOCX, XLSX", "Nur Grundrechenarten und SUMME. LibreOffice rechnet die Datei nicht nach."),
        P("SYS-09", "E-Mails & Termine", "Arbeiten & Büro", "MailCenter", "Entwurf, Versand nur mit Freigabe", "Empfänger, Betreff, Text", "EML-Entwurf", "Versand ist nicht angeschlossen. Trendo schickt selbst keine E-Mail."),
        P("X-11", "Neues Projekt starten", "Arbeiten & Büro", "ProjectStore", "Projekt anlegen", "Projekt-ID", "Projektordner", "Kein Fachadapter registriert."),
        P("SYS-02", "Kleine Helfer & Musik", "Arbeiten & Büro", "Calc, NoteStore, FileShelf", "Notiz oder Rechnung", "Text oder Ausdruck", "Notiz, Zahl", "Kein Fachadapter registriert."),
        P("STUDIO-ONLINE-BUSINESS-01", "Online verkaufen", "Business & Geld", "BusinessStudio", "Geschäftsplan rechnen", "Preis, Kosten, Menge", "Bewertung", "Kein Fachadapter registriert."),
        P("SPEC-11", "Geld & Rechnungen", "Business & Geld", "Finance, Ledger", "Rechnung mit Decimal rechnen, nicht buchen", "Verkäufer, Käufer, Positionen", "HTML-Entwurf", "Kein Fachadapter registriert."),
        P("CORE-08", "Videos & Fotos bearbeiten", "Kreativ & Medien", "MediaStudio, ffmpeg", "Datei wirklich umwandeln", "Quelldatei im Projekt", "Mediendatei", "Kein Fachadapter registriert."),
        P("SPEC-15", "Live streamen", "Kreativ & Medien", "Live-Studio der App", "Sendungsplan", "Titel, Ablauf", "Plan", "Kein Fachadapter registriert."),
        P("SPEC-08", "Kleidung & Produkte entwerfen", "Kreativ & Medien", "Größen- und Schnittlogik der App", "Maße prüfen", "Maße", "Zuschnitt", "Kein Fachadapter registriert."),
        P("X-07", "Freie Bilder & Klänge finden", "Kreativ & Medien", "OpenSource", "Quelle mit Lizenz merken", "Suchwort", "Liste", "Kein Fachadapter registriert."),
        P("CORE-12", "KI-Anweisungen schreiben", "Kreativ & Medien", "PromptLibrary", "Vorlage füllen", "Vorlage, Platzhalter", "Text", "Kein Fachadapter registriert."),
        P("CORE-05", "3D-Modelle erstellen", "3D, Spiele & VR", "Scene3D, Mesh3D, Studio3D", "Modelldatei, Render nur mit Blender", "Form und Maße", "OBJ, optional Blend", "Kein Fachadapter registriert."),
        P("CORE-06", "Spiele bauen", "3D, Spiele & VR", "GameStudio", "Browser-Spiel, Unreal nur wenn installiert", "Vorlage, Name", "HTML oder uproject", "Kein Fachadapter registriert."),
        P("SPEC-04", "VR & AR gestalten", "3D, Spiele & VR", "XrModels", "Raumskizze", "Maße", "Beschreibung", "Kein Fachadapter registriert."),
        P("SPEC-09", "Gegenstände einscannen", "3D, Spiele & VR", "PhotoScan, PointCloud", "Scan aus echten Bildern", "Bilder", "Punktwolke", "Kein Fachadapter registriert."),
        P("SPEC-01", "Wohnung planen", "Technik & Fachgebiete", "Flächenrechnung der App", "Fläche aus Maßen", "Länge, Breite", "Zahl mit Einheit", "Kein Fachadapter registriert."),
        P("SPEC-02", "Schaltungen berechnen", "Technik & Fachgebiete", "Elektronik-Rechner der App", "Widerstand und LED prüfen", "Spannung, Strom", "Zahl mit Einheit", "Kein Fachadapter registriert."),
        P("SPEC-03", "Statik nachrechnen", "Technik & Fachgebiete", "Balkenrechnung der App", "Balken mit Annahmen prüfen", "Last, Länge, Profil", "Prüfwert", "Kein Fachadapter registriert."),
        P("SPEC-10", "Geräte überwachen", "Technik & Fachgebiete", "Messwert-Ansichten der App", "Messreihe zeigen", "Werte", "Tabelle", "Kein Fachadapter registriert."),
        P("SPEC-13", "Routen & Karten", "Technik & Fachgebiete", "Routenlogik der App", "Strecke aus Punkten", "Punkte", "Länge", "Kein Fachadapter registriert."),
        P("SPEC-14", "Fräsen & Fertigen", "Technik & Fachgebiete", "Drehzahlrechnung der App", "Nur rechnen, keine Maschine starten", "Werkzeug, Material", "Drehzahl", "Keine Fräs- oder Maschinenbefehle aus einem Prompt."),
        P("SPEC-16", "Auto & Fahrten", "Technik & Fachgebiete", "Fahrtenlogik der App", "Verbrauch rechnen", "Strecke, Liter", "Kosten", "Kein Fachadapter registriert."),
        P("CORE-09", "Kameras & Roboter steuern", "Technik & Fachgebiete", "Robotik-Modelle der App", "Keine Bewegung aus einem Prompt", "keine", "keine", "Keine Kamerasteuerung und keine Roboterbefehle aus einem Prompt."),
        P("SPEC-05", "Messwerte auswerten", "Wissen & Lernen", "Science", "Messreihe auswerten", "Werte", "Kennzahl", "Kein Fachadapter registriert."),
        P("SPEC-06", "Lernen mit Karteikarten", "Wissen & Lernen", "Karteikarten der App", "Karte anlegen", "Vorderseite, Rückseite", "Karte", "Kein Fachadapter registriert."),
        P("SPEC-07", "Texte übersetzen", "Wissen & Lernen", "bestehendes Basisstudio, kein 2.0-Ausbau", "keine neue Übersetzung über diesen Auftrag", "keine", "keine", "Language & Localization Studio 2.0 bleibt abgelehnt. Kein neuer Übersetzungsadapter."),
        P("CORE-10", "Wissen merken", "Wissen & Lernen", "Knowledge", "Notiz ins eigene Wissen", "Text", "Eintrag", "Kein Fachadapter registriert."),
        P("SYS-08", "Sicher recherchieren", "Wissen & Lernen", "Research, nur Lesen", "Seite nur lesen", "Adresse", "Lesetext", "Kein Fachadapter registriert."),
        P("CORE-02", "KI fragen", "KI & Automatik", "IntelligenceCore.AskAsync", "Frage an die lokale KI", "Frage", "Antwort", "Kein Fachadapter registriert."),
        P("CORE-03", "KI-Team einsetzen", "KI & Automatik", "Agents", "Rolle um Rat fragen", "Aufgabe", "Text", "Kein Fachadapter registriert."),
        P("X-05", "Kostenlose KI finden", "KI & Automatik", "KiSources, nur Loopback", "Lokale Quelle prüfen", "keine", "Status", "Kein Fachadapter registriert."),
        P("SYS-05", "Aufgaben automatisieren", "KI & Automatik", "WorkflowEngine", "Ablauf planen", "Schritte", "Ablauf", "Kein Fachadapter registriert."),
        P("X-01", "Großprojekte umsetzen", "KI & Automatik", "bestehende Studios", "Studios nur nennen", "Ziel", "Plan", "Kein Fachadapter registriert."),
        P("CORE-07", "Webseite bauen", "Entwickeln", "DevStudio", "Projektvorlage anlegen, nicht veröffentlichen", "Vorlage, Name", "Quelltext", "Kein Fachadapter registriert."),
        P("X-03", "Apps zusammenklicken", "Entwickeln", "PageBuilder", "Seitenentwurf", "Inhalt", "HTML", "Kein Fachadapter registriert."),
        P("X-06", "Projekte von GitHub holen", "Entwickeln", "OpenSource", "Nur anzeigen, nicht mergen", "Name", "Hinweis", "Kein Fachadapter registriert."),
        P("X-04", "Trendo erweitern", "Entwickeln", "ExtensionCatalog", "Erweiterung entwerfen", "Name", "Entwurf", "Kein Fachadapter registriert."),
        P("X-10", "Programme nachbauen", "Entwickeln", "CleanRoom", "Nur eigene Beschreibung", "Verhalten", "Entwurf", "Kein Fachadapter registriert."),
        P("CORE-01", "Desktop einrichten", "System & Sicherheit", "Shell, StartMenu, Design 41", "keine zweite Oberfläche", "keine", "keine", "Desktop, Startmenü und Fenster bleiben beim laufenden Design-41-Auftrag."),
        P("SYS-01", "Startmodus wählen", "System & Sicherheit", "OsMode", "Modus nur anzeigen", "keine", "Hinweis", "Kein Fachadapter registriert."),
        P("CORE-11", "Daten schützen", "System & Sicherheit", "ApprovalGate, Secrets, Backup, Rbac", "Freigabe bleibt beim bestehenden Tor", "keine", "keine", "Kein zweites Freigabe- oder Geheimnissystem."),
        P("SPEC-12", "System auf Gefahren prüfen", "System & Sicherheit", "ForensicsLab, 120 Stunden", "Forensik unverändert lassen", "keine", "keine", "Verbesserung 34 bleibt abgelehnt. Die 120-Stunden-Regel wird nicht angetastet."),
        P("SYS-03", "Surfen & Herunterladen", "System & Sicherheit", "Browser-Lesemodus", "Nichts im Hintergrund laden", "Adresse", "Hinweis", "Kein Fachadapter registriert."),
        P("SYS-04", "Programme installieren", "System & Sicherheit", "DependencyManager, nur prüfen", "Nichts installieren", "Name", "Prüfergebnis", "Kein Fachadapter registriert."),
        P("SYS-06", "Dateien teilen", "System & Sicherheit", "ShareServer", "Freigabe nur mit bestehendem Tor", "Pfad", "Hinweis", "Kein Fachadapter registriert."),
        P("SYS-07", "Geräte erkennen", "System & Sicherheit", "Devices", "Geräte nur nennen", "keine", "Liste", "Kein Fachadapter registriert."),
        P("SYS-11", "Programme starten", "System & Sicherheit", "ExternalApps", "Programm nur über die bestehende Prüfung", "Name", "Hinweis", "Kein Fachadapter registriert."),
        P("SYS-12", "Schrift & Farben anpassen", "System & Sicherheit", "UserSettings, Design 41", "Darstellung nicht aus einem Prompt umbauen", "keine", "keine", "Design 41 bleibt. Kein Prompt ändert das Theme."),
        P("SYS-13", "Qualität prüfen", "System & Sicherheit", "Quality, SelfTest", "Vorhandene Prüfung starten", "keine", "Protokoll", "Kein Fachadapter registriert."),
        P("X-02", "Einrichten & Reparieren", "System & Sicherheit", "SetupPacks, Heal", "Nur anzeigen, Verbesserung 35 bleibt offen", "keine", "Hinweis", "Verbesserung 35 ist nicht entschieden und wird nicht aktiviert."),
        P("X-08", "Handy verbinden", "System & Sicherheit", "Companion", "Verbindung nicht aus einem Prompt öffnen", "keine", "Hinweis", "Kein Fachadapter registriert."),
        P("CORE-04", "PC, Pi & Handy verbinden", "System & Sicherheit", "Fusion", "Verbund nur anzeigen", "keine", "Hinweis", "Kein Fachadapter registriert."),
        P("X-09", "Leistung anzeigen", "System & Sicherheit", "ResourceMonitor, SystemMetrics", "Messwerte lesen", "keine", "Messwert", "Kein Fachadapter registriert."),
    ];

    private static StudioProfile P(string id, string title, string area, string existing, string action, string inputs, string outputs, string blocker) =>
        new(id, title, area, existing, action, inputs, outputs, blocker);

    public static IReadOnlyList<StudioProfile> Profiles => All;
    public static IReadOnlyList<string> Ids => All.Select(p => p.Id).ToList();
    public static StudioProfile? Find(string? id) => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    public static string Title(string id) => Find(id)?.Title ?? id;
    public static string Area(string id) => Find(id)?.Area ?? "";

    public static CapabilityLevel Level(string id, IReadOnlyList<IStudioActionAdapter> adapters)
    {
        var mine = adapters.Where(a => string.Equals(a.StudioId, id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count == 0) return CapabilityLevel.NotImplemented;
        if (mine.All(a => a.DeclaredLevel == CapabilityLevel.Blocked)) return CapabilityLevel.Blocked;
        if (mine.All(a => a.DeclaredLevel == CapabilityLevel.Ready)) return CapabilityLevel.Ready;
        if (mine.Any(a => a.DeclaredLevel is CapabilityLevel.Ready or CapabilityLevel.Partial)) return CapabilityLevel.Partial;
        return mine[0].DeclaredLevel;
    }

    public static string UiLine(string id, IReadOnlyList<IStudioActionAdapter> adapters)
    {
        var profile = Find(id);
        var title = profile?.Title ?? id;
        var mine = adapters.Where(a => string.Equals(a.StudioId, id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count == 0) return title + ": " + StatusText.German(CapabilityLevel.NotImplemented) + ". " + (profile?.Blocker ?? "Kein Fachadapter registriert.");
        return title + ": " + StatusText.German(Level(id, adapters)) + ". " + string.Join(", ", mine.Select(a => a.Label));
    }

    public static string Markdown(IReadOnlyList<IStudioActionAdapter> adapters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Studio-Abdeckung – Verbesserung 42");
        sb.AppendLine();
        sb.AppendLine("Erzeugt aus `CapabilityCatalog` und den wirklich registrierten Adaptern. `ready` steht nur an einem Adapter, dessen Beleg ein vorhandener Test ist. Einträge ohne Adapter sind `not_implemented`.");
        sb.AppendLine();
        sb.AppendLine("| Studio-ID | Titel | Bereich | Vorhandene Bausteine | Vorgesehene Aktion | Eingaben | Adapter | Ausgabe | Reife | Beleg | Blocker |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var p in All)
        {
            var mine = adapters.Where(a => string.Equals(a.StudioId, p.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            var adapter = mine.Count == 0 ? "keiner" : string.Join(", ", mine.Select(a => a.ActionId));
            var evidence = mine.Count == 0 ? "—" : string.Join(", ", mine.Select(a => string.IsNullOrWhiteSpace(a.Evidence) ? "—" : a.Evidence));
            var blocker = mine.Count == 0 ? p.Blocker : mine.Any(a => a.DeclaredLevel != CapabilityLevel.Ready) ? p.Blocker : "—";
            sb.Append("| ").Append(p.Id).Append(" | ").Append(p.Title).Append(" | ").Append(p.Area).Append(" | ")
                .Append(p.Existing).Append(" | ").Append(p.PlannedAction).Append(" | ").Append(p.Inputs).Append(" | ")
                .Append(adapter).Append(" | ").Append(p.Outputs).Append(" | ").Append(LevelName(Level(p.Id, adapters))).Append(" | ")
                .Append(evidence).Append(" | ").Append(blocker).AppendLine(" |");
        }
        sb.AppendLine();
        sb.AppendLine($"Anzahl: {All.Length} Katalogeinträge, {adapters.Count} registrierte Aktionen.");
        return sb.ToString();
    }

    public static string LevelName(CapabilityLevel level) => level switch
    {
        CapabilityLevel.Ready => "ready",
        CapabilityLevel.Partial => "partial",
        CapabilityLevel.Blocked => "blocked",
        _ => "not_implemented"
    };
}
