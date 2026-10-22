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
        P("X-11", "Neues Projekt starten", "Arbeiten & Büro", "ProjectStore", "Projekt anlegen", "Projekt-ID", "Projektordner, Text", "Es entsteht ein Projektordner. Es öffnet sich kein Fenster."),
        P("SYS-02", "Kleine Helfer & Musik", "Arbeiten & Büro", "Calc, NoteStore, FileShelf", "Notiz oder Rechnung", "Text oder Ausdruck", "Notiz, Zahl", "Musik abspielen und Dateien verschieben sind nicht angeschlossen."),
        P("STUDIO-ONLINE-BUSINESS-01", "Online verkaufen", "Business & Geld", "BusinessStudio", "Geschäftsplan rechnen", "Preis, Kosten, Menge", "Markdown", "Nur eine Schätzung aus deinen Zahlen. Kein Shop, keine Zahlung, keine Rechtsberatung."),
        P("SPEC-11", "Geld & Rechnungen", "Business & Geld", "Finance, Ledger", "Rechnung mit Decimal rechnen, nicht buchen", "Verkäufer, Käufer, Positionen", "HTML-Entwurf", "Zahlung und Buchung sind nicht angeschlossen. Keine Steuerberatung."),
        P("CORE-08", "Videos & Fotos bearbeiten", "Kreativ & Medien", "MediaStudio, ffmpeg", "Datei wirklich umwandeln", "Quelldatei im Projekt", "Mediendatei", "Hochladen ist nicht angeschlossen. Umwandeln braucht ffmpeg."),
        P("SPEC-15", "Live streamen", "Kreativ & Medien", "Live-Studio der App", "Sendungsplan", "Titel, Ablauf", "Plan", "Der Ablauf bleibt lokal. Es wird nichts gesendet."),
        P("SPEC-08", "Kleidung & Produkte entwerfen", "Kreativ & Medien", "Größen- und Schnittlogik der App", "Maße prüfen", "Maße", "Größentext", "Nur die Größentabelle. Es wird nichts bestellt."),
        P("X-07", "Freie Bilder & Klänge finden", "Kreativ & Medien", "OpenSource", "Quelle mit Lizenz merken", "Suchwort", "Hinweis", "Nur die Lizenz aus dem Text. Es wird nichts heruntergeladen."),
        P("CORE-12", "KI-Anweisungen schreiben", "Kreativ & Medien", "PromptLibrary", "Vorlage füllen", "Vorlage, Platzhalter", "Text", "Die Vorlage wird gefüllt. Die KI wird nicht gefragt."),
        P("CORE-05", "3D-Modelle erstellen", "3D, Spiele & VR", "Scene3D, Mesh3D, Studio3D", "Modelldatei, Render nur mit Blender", "Form und Maße", "OBJ, optional Blend", "Blender wird aus dem Auftrag nicht gestartet. OBJ und die eingebaute Vorschau brauchen es nicht."),
        P("CORE-06", "Spiele bauen", "3D, Spiele & VR", "GameStudio", "Browser-Spiel, Unreal nur wenn installiert", "Vorlage, Name", "HTML oder uproject", "Der Unreal-Editor wird nicht gestartet. Browser-Spiel und Projektdatei gehen ohne ihn."),
        P("SPEC-04", "VR & AR gestalten", "3D, Spiele & VR", "XrModels", "Raumskizze", "Maße", "Bildzeit", "Nur die Bildzeit. Es läuft keine Brille."),
        P("SPEC-09", "Gegenstände einscannen", "3D, Spiele & VR", "PhotoScan, PointCloud", "Scan aus echten Bildern", "Bilder", "keine", "COLMAP und Meshroom werden aus dem Auftrag nicht gestartet. Es gibt keine Punktwolke."),
        P("SPEC-01", "Wohnung planen", "Technik & Fachgebiete", "Flächenrechnung der App", "Fläche aus Maßen", "Länge, Breite", "SVG, Fläche", "Nur Fläche, Bedarf und Grundriss. Es wird nichts bestellt."),
        P("SPEC-02", "Schaltungen berechnen", "Technik & Fachgebiete", "Elektronik-Rechner der App", "Widerstand und LED prüfen", "Spannung, Strom", "Zahl mit Einheit", "Nur die Rechnung. Es wird keine Schaltung aufgebaut."),
        P("SPEC-03", "Statik nachrechnen", "Technik & Fachgebiete", "Balkenrechnung der App", "Balken mit Annahmen prüfen", "Last, Länge, Profil", "Prüftext", "Nur die eingebaute Balkenrechnung. Keine Prüfung durch eine Fachperson."),
        P("SPEC-10", "Geräte überwachen", "Technik & Fachgebiete", "Messwert-Ansichten der App", "Messreihe zeigen", "Werte", "Tabelle", "Nur die eingetippten Werte. Es wird kein Gerät verbunden."),
        P("SPEC-13", "Routen & Karten", "Technik & Fachgebiete", "Routenlogik der App", "Strecke aus Punkten", "Punkte", "Länge", "Nur die Strecke. Es wird keine Karte geladen."),
        P("SPEC-14", "Fräsen & Fertigen", "Technik & Fachgebiete", "Drehzahlrechnung der App", "Nur rechnen, keine Maschine starten", "Werkzeug, Material", "Drehzahl", "Keine Fräs- oder Maschinenbefehle aus einem Prompt."),
        P("SPEC-16", "Auto & Fahrten", "Technik & Fachgebiete", "Fahrtenlogik der App", "Verbrauch rechnen", "Strecke, Liter", "Kosten", "Nur die Kostenrechnung. Es wird nichts gebucht."),
        P("CORE-09", "Kameras & Roboter steuern", "Technik & Fachgebiete", "WebcamScan", "Kameras nur nennen", "keine", "Liste", "Nur die Namen. Aufnahme, Mikrofon und Bewegung bleiben aus."),
        P("SPEC-05", "Messwerte auswerten", "Wissen & Lernen", "Science", "Messreihe auswerten", "Werte", "Kennzahl", "Nur die eingebaute Statistik. Keine wissenschaftliche Beratung."),
        P("SPEC-06", "Lernen mit Karteikarten", "Wissen & Lernen", "Karteikarten der App", "Karte anlegen", "Vorderseite, Rückseite", "CSV", "Nur die Karte auf diesem PC. Es gibt keinen Kurs."),
        P("SPEC-07", "Texte übersetzen", "Wissen & Lernen", "AgentCenter, Rolle Übersetzer", "Deutsch oder Englisch über die lokale KI", "Text, Ziel", "Text", "Nur Deutsch und Englisch, nur wenn eine lokale KI antwortet. Language Studio 2.0 bleibt abgelehnt."),
        P("CORE-10", "Wissen merken", "Wissen & Lernen", "Knowledge", "Notiz ins eigene Wissen", "Text", "Eintrag", "Nur der eigene Eintrag auf diesem PC. Es wird nichts hochgeladen."),
        P("SYS-08", "Sicher recherchieren", "Wissen & Lernen", "ResearchDesk", "Seite nur lesen", "Adresse", "Lesetext", "Nur der Lesemodus. .onion, das eigene Netz und Downloads bleiben draußen. Offline wird nichts geholt."),
        P("CORE-02", "KI fragen", "KI & Automatik", "IntelligenceCore.AskAsync", "Frage an die lokale KI", "Frage", "Antwort", "Nur die angeschlossene lokale KI. Ohne erreichbare Quelle gibt es keine Antwort."),
        P("CORE-03", "KI-Team einsetzen", "KI & Automatik", "AgentCenter", "Team um Rat fragen", "Aufgabe, Team", "Text", "Nur die vorhandenen Rollen. Ohne lokale KI gibt es keinen Rat."),
        P("X-05", "Kostenlose KI finden", "KI & Automatik", "KiCatalog, nur Loopback", "Lokale Quelle prüfen", "keine", "Status", "Nur Loopback aus dem Katalog. Kostenpflichtige und Online-Konten werden nicht gefragt."),
        P("SYS-05", "Aufgaben automatisieren", "KI & Automatik", "WorkflowEngine", "Ablauf planen", "Schritte", "Ablauf", "Nur der Plan. Der Ablauf verschiebt und kopiert nichts."),
        P("X-01", "Großprojekte umsetzen", "KI & Automatik", "bestehende Studios", "Studios nur nennen", "Ziel", "Plan", "Nur die Namen der Studios. Es wird nichts ausgeführt."),
        P("CORE-07", "Webseite bauen", "Entwickeln", "DevStudio", "Projektvorlage anlegen, nicht veröffentlichen", "Vorlage, Name", "Quelltext", "Veröffentlichen und Push sind nicht angeschlossen. Es wird nichts hochgeladen."),
        P("X-03", "Apps zusammenklicken", "Entwickeln", "PageBuilder", "Seitenentwurf", "Inhalt", "HTML, seite.json", "HTML und Formulardatei im Projekt. Kein Shop, kein 3D, kein Pixel-Streaming. Veröffentlichen ist nicht angeschlossen."),
        P("X-06", "Projekte von GitHub holen", "Entwickeln", "OpenSource", "Nur anzeigen, nicht mergen", "Name", "Hinweis", "Nur die Lizenz aus dem Text. Es wird nichts geklont."),
        P("X-04", "Trendo erweitern", "Entwickeln", "ExtensionCatalog", "Erweiterung entwerfen", "Name", "Entwurf", "Nur der Entwurf. Es wird nichts installiert."),
        P("X-10", "Programme nachbauen", "Entwickeln", "CleanRoom", "Nur eigene Beschreibung", "Verhalten", "Entwurf", "Nur die Beschreibung im Projekt. Keine Rechtsberatung und kein Originalcode."),
        P("CORE-01", "Desktop einrichten", "System & Sicherheit", "UserSettings, Design 41", "Desktop nur zeigen", "keine", "Text", "Nur Anzeige. Design 41 wird aus dem Auftrag nicht geändert."),
        P("SYS-01", "Startmodus wählen", "System & Sicherheit", "OsMode", "Modus nur anzeigen", "keine", "Hinweis", "Nur der erkannte Modus. Es wird nichts umgestellt."),
        P("CORE-11", "Daten schützen", "System & Sicherheit", "ISecretStore", "Speicher nur nennen", "keine", "Text", "Nur der Name des Speichers. Werte werden nicht gelesen, gesetzt oder gelöscht."),
        P("SPEC-12", "System auf Gefahren prüfen", "System & Sicherheit", "ForensicsLab, 120 Stunden", "Forensik unverändert lassen", "keine", "keine", "Verbesserung 34 bleibt abgelehnt. Die 120-Stunden-Regel wird nicht angetastet."),
        P("SYS-03", "Surfen & Herunterladen", "System & Sicherheit", "ResearchDesk", "Datei nach Freigabe laden", "Adresse", "Datei", "Erst nach der Freigabe, höchstens 2 MB, nur http(s) im Lesemodus. Ohne „Laden: ja“ passiert nichts."),
        P("SYS-04", "Programme installieren", "System & Sicherheit", "DependencyManager", "Katalogprogramm nach Zustimmung und Freigabe", "Programm", "Plan oder Prüfergebnis", "Nur Katalogprogramme, nur nach Lizenzdatei und Freigabe. winget läuft nicht von allein."),
        P("SYS-06", "Dateien teilen", "System & Sicherheit", "FolderShare, ShareServer", "Ordner im Projekt nach Freigabe", "Ordner", "Hinweis", "Nur ein Ordner im Projekt, nur nach Freigabe, höchstens 24 Stunden. Ohne „Teilen: ja“ passiert nichts."),
        P("SYS-07", "Geräte erkennen", "System & Sicherheit", "DeviceScanner", "Geräte nur nennen", "keine", "Liste", "Nur die gelesene Liste. Es wird kein Gerät gestartet."),
        P("SYS-11", "Programme starten", "System & Sicherheit", "ExternalApps", "Programm aus der Liste nach Freigabe", "Programm, Datei", "Plan", "Nur die feste Liste und eine Datei im Projekt, nur nach Freigabe."),
        P("SYS-12", "Schrift & Farben anpassen", "System & Sicherheit", "UserSettings, Design 41", "Darstellung nur zeigen", "keine", "Text", "Nur Anzeige. Schrift und Farben bleiben beim laufenden Design."),
        P("SYS-13", "Qualität prüfen", "System & Sicherheit", "AuditLog, SelfHealer", "Protokoll schreiben", "keine", "Protokoll", "Protokoll und Prüfung. Der Explorer wird nicht geöffnet."),
        P("X-02", "Einrichten & Reparieren", "System & Sicherheit", "SelfHealer", "Nur prüfen", "keine", "Protokoll", "Nur die Prüfung. Es wird nichts repariert. Verbesserung 35 bleibt offen."),
        P("X-08", "Handy verbinden", "System & Sicherheit", "MobileBridge", "adb-Liste, wenn adb schon da ist", "keine", "Liste", "Nur „adb devices -l“. Kein Spiegel, kein Foto, keine Installation."),
        P("CORE-04", "PC, Pi & Handy verbinden", "System & Sicherheit", "FusionStore", "Verbund nur zeigen", "keine", "Liste", "Nur die gespeicherten Geräte. Es wird nichts gekoppelt."),
        P("X-09", "Leistung anzeigen", "System & Sicherheit", "ResourceMonitor, SystemMetrics", "Messwerte lesen", "keine", "Messwert", "Nur die gelesenen Messwerte. Es wird kein Programm beendet."),
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
