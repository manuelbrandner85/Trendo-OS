// Zentrale, verständliche Beschreibung der Studios für die Oberfläche (Karten, Kacheln, Startmenü).
// Technische Kennungen (z. B. CORE-03) und Freigabe-Status bleiben in requirements.yaml und im Tooltip – nicht in der Anzeige.
namespace Trendo.App;

/// <summary>Kurzer deutscher Name, ein einfacher Satz und der Bereich, unter dem das Studio steht.</summary>
public sealed record StudioInfo(string Name, string Sentence, string Area);

public static class StudioCatalog
{
    /// <summary>Bereiche in Anzeigereihenfolge.</summary>
    public static readonly string[] Areas =
    [
        "Arbeiten & Büro", "Business & Geld", "Kreativ & Medien", "3D, Spiele & VR", "Technik & Fachgebiete",
        "Wissen & Lernen", "KI & Automatik", "Entwickeln", "System & Sicherheit",
    ];

    public static readonly IReadOnlyDictionary<string, StudioInfo> ById = new Dictionary<string, StudioInfo>
    {
        ["STUDIO-LETTER-01"] = new("Briefe schreiben", "Schreibt Briefe an Ämter, Firmen und Vermieter – mit Vorlagen und Fristen.", "Arbeiten & Büro"),
        ["SYS-10"] = new("Texte & Tabellen", "Texte, Tabellen und Präsentationen erstellen und bearbeiten.", "Arbeiten & Büro"),
        ["SYS-09"] = new("E-Mails & Termine", "E-Mails, Kontakte und Termine an einem Ort.", "Arbeiten & Büro"),
        ["X-11"] = new("Neues Projekt starten", "Ein Startpunkt für jedes Projekt – vom Dokument bis zum Video.", "Arbeiten & Büro"),
        ["SYS-02"] = new("Kleine Helfer & Musik", "Kleine Helfer: Rechner, Notizen, Bildschirmfoto, Musik und Videos abspielen.", "Arbeiten & Büro"),

        ["STUDIO-ONLINE-BUSINESS-01"] = new("Online verkaufen", "Geschäftsidee planen, gründen und online verkaufen.", "Business & Geld"),
        ["SPEC-11"] = new("Geld & Rechnungen", "Haushaltsbuch, Rechnungen, Kredite und Steuern im Blick.", "Business & Geld"),

        ["CORE-08"] = new("Videos & Fotos bearbeiten", "Videos schneiden, Musik aufnehmen und Bilder gestalten.", "Kreativ & Medien"),
        ["SPEC-15"] = new("Live streamen", "Livestreams und Sendungen planen und durchführen.", "Kreativ & Medien"),
        ["STUDIO-TV-01"] = new("TV & Live", "Fernsehen über das Internet: eigene Senderlisten, Programmführer, Aufnahmen und mehrere Sender gleichzeitig.", "Kreativ & Medien"),
        ["SPEC-08"] = new("Kleidung & Produkte entwerfen", "Kleidergrößen, Schnitte und Produktideen.", "Kreativ & Medien"),
        ["X-07"] = new("Freie Bilder & Klänge finden", "Kostenlose Bilder, Klänge und Schriften finden.", "Kreativ & Medien"),
        ["CORE-12"] = new("KI-Anweisungen schreiben", "Gute Anweisungen für die KI schreiben und sammeln.", "Kreativ & Medien"),

        ["CORE-05"] = new("3D-Modelle erstellen", "3D-Modelle bauen und ansehen – Blender optional.", "3D, Spiele & VR"),
        ["CORE-06"] = new("Spiele bauen", "Eigene Spiele bauen – auch mit der Unreal Engine.", "3D, Spiele & VR"),
        ["SPEC-04"] = new("VR & AR gestalten", "Virtuelle Räume und 3D-Modelle für die Brille vorbereiten.", "3D, Spiele & VR"),
        ["SPEC-09"] = new("Gegenstände einscannen", "Gegenstände und Räume einscannen und vermessen.", "3D, Spiele & VR"),

        ["SPEC-01"] = new("Wohnung planen", "Räume ausmessen, Flächen berechnen und einrichten.", "Technik & Fachgebiete"),
        ["SPEC-02"] = new("Schaltungen berechnen", "Widerstände, LEDs und Schaltungen berechnen.", "Technik & Fachgebiete"),
        ["SPEC-03"] = new("Statik nachrechnen", "Balken, Kräfte und Materialien nachrechnen.", "Technik & Fachgebiete"),
        ["SPEC-10"] = new("Geräte überwachen", "Messwerte von Geräten und Sensoren beobachten.", "Technik & Fachgebiete"),
        ["SPEC-13"] = new("Routen & Karten", "Routen planen, Strecken messen und GPS-Dateien ansehen.", "Technik & Fachgebiete"),
        ["SPEC-14"] = new("Fräsen & Fertigen", "Fräsen, Drehzahlen und Werkstücke planen.", "Technik & Fachgebiete"),
        ["SPEC-16"] = new("Auto & Fahrten", "Verbrauch, Fahrtenbuch und Kosten rund ums Auto.", "Technik & Fachgebiete"),
        ["CORE-09"] = new("Kameras & Roboter steuern", "Kameras und Roboterarme steuern und auswerten.", "Technik & Fachgebiete"),

        ["SPEC-05"] = new("Messwerte auswerten", "Messwerte auswerten und als Diagramm zeigen.", "Wissen & Lernen"),
        ["SPEC-06"] = new("Lernen mit Karteikarten", "Karteikarten, Vokabeln und Prüfungen üben.", "Wissen & Lernen"),
        ["SPEC-07"] = new("Texte übersetzen", "Texte mit der lokalen KI übersetzen.", "Wissen & Lernen"),
        ["CORE-10"] = new("Wissen merken", "Merkt sich, was du gelernt und gemacht hast.", "Wissen & Lernen"),
        ["SYS-08"] = new("Sicher recherchieren", "Sicher im Netz recherchieren – nur lesen.", "Wissen & Lernen"),

        ["CORE-02"] = new("KI fragen", "Dein Helfer, der Fragen beantwortet und Aufgaben erledigt.", "KI & Automatik"),
        ["CORE-03"] = new("KI-Team einsetzen", "Mehrere KI-Helfer arbeiten gemeinsam an einer Aufgabe (lokale KI nötig).", "KI & Automatik"),
        ["X-05"] = new("Kostenlose KI finden", "Kostenlose KI-Modelle finden und als Ersatz nutzen.", "KI & Automatik"),
        ["SYS-05"] = new("Aufgaben automatisieren", "Wiederkehrende Aufgaben automatisch erledigen lassen.", "KI & Automatik"),
        ["X-01"] = new("Großprojekte umsetzen", "Mehrere Studios arbeiten zusammen an einem Projekt.", "KI & Automatik"),

        ["CORE-07"] = new("Webseite bauen", "Webseiten und Apps programmieren.", "Entwickeln"),
        ["X-03"] = new("Apps zusammenklicken", "Apps zusammenklicken – ganz ohne Programmieren.", "Entwickeln"),
        ["X-06"] = new("Projekte von GitHub holen", "Freie Projekte von GitHub finden und nutzen.", "Entwickeln"),
        ["X-04"] = new("Trendo erweitern", "Neue Funktionen für Trendo hinzufügen.", "Entwickeln"),
        ["X-10"] = new("Programme nachbauen", "Programme rechtlich sauber neu entwickeln.", "Entwickeln"),

        ["CORE-01"] = new("Desktop einrichten", "Fenster, Startmenü und Arbeitsfläche von Trendo.", "System & Sicherheit"),
        ["SYS-01"] = new("Startmodus wählen", "Erkennt das Gerät und startet Trendo passend (PC).", "System & Sicherheit"),
        ["CORE-11"] = new("Daten schützen", "Schützt deine Daten, Passwörter und Sicherungen.", "System & Sicherheit"),
        ["SPEC-12"] = new("System auf Gefahren prüfen", "Prüft das System auf Gefahren und sichert Hinweise.", "System & Sicherheit"),
        ["SYS-03"] = new("Surfen & Herunterladen", "Im Internet surfen und Dateien laden.", "System & Sicherheit"),
        ["SYS-04"] = new("Programme installieren", "Programme installieren, aktualisieren und entfernen.", "System & Sicherheit"),
        ["SYS-06"] = new("Dateien teilen", "Ordner im Heimnetz teilen – nur mit deiner Freigabe.", "System & Sicherheit"),
        ["SYS-07"] = new("Geräte erkennen", "Drucker, Kameras und andere Geräte erkennen.", "System & Sicherheit"),
        ["SYS-11"] = new("Programme starten", "Programme ohne Installation starten und verknüpfen.", "System & Sicherheit"),
        ["SYS-12"] = new("Schrift & Farben anpassen", "Größere Schrift, Farben und einfache Bedienung.", "System & Sicherheit"),
        ["SYS-13"] = new("Qualität prüfen", "Prüft, ob alles zusammen funktioniert.", "System & Sicherheit"),
        ["X-02"] = new("Einrichten & Reparieren", "Richtet Trendo automatisch ein und repariert Fehler.", "System & Sicherheit"),
        ["X-08"] = new("Handy verbinden", "Verbindet Trendo mit deinem Handy per Kabel, WLAN oder Bluetooth.", "System & Sicherheit"),
        ["CORE-04"] = new("PC, Pi & Handy verbinden", "PC mit Raspberry Pi und Handy koppeln (Gegenstellen in Arbeit).", "System & Sicherheit"),
        ["X-09"] = new("Leistung anzeigen", "Zeigt, was Speicher und Prozessor gerade tun.", "System & Sicherheit"),
    };

    /// <summary>Studio-Liste mit den alltagsnahen Namen (gilt dann überall: Karten, Kacheln, Startmenü, Fenstertitel).</summary>
    public static IReadOnlyList<Trendo.Core.StudioEntry> Friendly(IEnumerable<Trendo.Core.StudioEntry> list) =>
        list.Select(s => For(s.Id) is { } i ? s with { Name = i.Name } : s).ToList();

    /// <summary>Anzeigenamen der Seitenleiste (Schlüssel = bisherige Bezeichnung, bleibt für Steuerelement-Namen erhalten).</summary>
    public static readonly IReadOnlyDictionary<string, string> SideLabels = new Dictionary<string, string>
    {
        ["Online Business"] = "Online verkaufen", ["3D & Games"] = "3D & Spiele", ["Kreativ & Medien"] = "Videos, Fotos & Musik",
        ["Office & Dokumente"] = "Texte & Tabellen", ["Briefstudio"] = "Briefe schreiben", ["Kommunikation"] = "E-Mails & Termine",
        ["Tools & Entwickler"] = "Programmieren", ["System & Sicherheit"] = "Schutz & System", ["KI Assistent"] = "KI fragen",
    };

    /// <summary>Info zu einem Studio oder null.</summary>
    public static StudioInfo? For(string? id) => id is not null && ById.TryGetValue(id, out var i) ? i : null;

    /// <summary>Anzeigename aus dem Katalog. Fehlt der Eintrag, bleibt der bisherige Text.</summary>
    public static string NameOf(string? id, string fallback) => For(id)?.Name ?? fallback;
}
