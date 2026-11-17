namespace Trendo.Core.Programs;

/// <summary>Stufe „Einfach“: Beschriftungen in Alltagssprache. Nur für feste Texte der Oberfläche, nie für Inhalte der Person. In Erweitert und Professionell bleiben die Fachwörter.</summary>
public static class PlainWords
{
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Inspektor"] = "Eigenschaften",
        ["INSPEKTOR"] = "EIGENSCHAFTEN",
        ["Spielbibel"] = "Spielkonzept",
        ["Spiel prüfen"] = "Auf Fehler prüfen",
        ["Bauen"] = "Spiel erstellen",
        ["Spielen / Öffnen"] = "Spielen",
        ["Exportieren"] = "Speichern als",
        ["Produktion"] = "Fortschritt",
        ["Assets"] = "Bilder & Klänge",
        ["Veröffentlichen"] = "Weitergeben",
        ["Missionen"] = "Aufgaben im Spiel",
    };

    /// <summary>E10 (T-0616, 14.11.2026): Alltagswörter für Desktop, linke Leiste, Widgets und Einstellungen – in allen Stufen.
    /// Das Fachwort bleibt als Hinweis (ToolTip) sichtbar, siehe <see cref="Technical"/>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Everyday = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Command Center"] = "KI-Zentrale",
        ["Agenten"] = "KI-Helfer",
        ["Agenten Live"] = "KI-Helfer bei der Arbeit",
        ["Aktive Agenten"] = "Aktive KI-Helfer",
        ["Assets"] = "KI-Quellen",
        ["KI-Router"] = "Welche KI hilft",
        ["Trendo Ökosystem"] = "Dein Trendo",
        ["Studio Manager"] = "Studios auf dem Desktop",
        ["Open Source"] = "Freie Programme",
        ["Mobile"] = "Handy",
        ["Credits"] = "Punkte",
        ["Skills"] = "KI-Fähigkeiten",
        ["Projektdaten"] = "Gespeicherte Daten",
        ["Export"] = "Speichern als",
        ["KI-Rollen und Teams"] = "KI-Helfer und Teams",
    };

    /// <summary>V7 (T-0663, 15.11.2026): feste Knopf-Beschriftungen mit Fachwort – in der Stufe „Einfach“ in Alltagssprache, das Fachwort steht im Hinweis.
    /// Nur ganze Beschriftungen (nie Teile von Inhalten der Person); Markennamen bei Anmeldung und Installation bleiben.</summary>
    public static readonly IReadOnlyDictionary<string, string> SimpleLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["2 UV"] = "2 Abwicklung",
        ["6 Render"] = "6 Bild erstellen",
        ["Als CSV speichern"] = "Als Tabelle speichern",
        ["Als CSV (Google, Outlook)"] = "Als Tabelle (Google, Outlook)",
        ["Ablaufplan (CSV)"] = "Ablaufplan als Tabelle",
        ["CSV importieren …"] = "Tabelle einlesen …",
        ["ZIP erstellen"] = "Gepackte Datei erstellen",
        ["Exportieren (ZIP)"] = "Als gepackte Datei speichern",
        ["Plotter-Datei (DXF)"] = "Datei für den Schneideplotter",
        ["Als SVG (1:1)"] = "Als Zeichnung (1:1)",
        ["Auch als E-Rechnung (XRechnung)"] = "Auch als E-Rechnung für Behörden",
        ["Pixel Streaming nur dokumentieren"] = "Unreal im Browser nur beschreiben",
        ["Als ICS speichern"] = "Als Kalenderdatei speichern",
        ["Programme auf GitHub finden"] = "Freie Programme im Internet finden",
        ["Freie Programme auf GitHub suchen"] = "Freie Programme im Internet suchen",
        ["Projekte von GitHub holen"] = "Projekte aus dem Internet holen",
        ["Fehlende Programme von GitHub nachladen"] = "Fehlende Programme nachladen",
        ["+ Persona"] = "+ Stil-Profil",
        ["Exportieren"] = "Als Datei speichern",
        // V-11 (T-0700): weitere Fachwörter an Knöpfen
        ["ICS einlesen …"] = "Kalenderdatei einlesen …",
        ["Alle als vCard sichern"] = "Alle Kontakte als Datei sichern",
        ["Als vCard"] = "Als Kontaktdatei",
        ["Kontakte sichern (.vcf)"] = "Kontakte als Datei sichern",
        ["Störpunkte entfernen"] = "Fehlerhafte Punkte entfernen",
        ["GPX (Navi, Uhr)"] = "Für Navi oder Uhr",
        ["GeoJSON (QGIS)"] = "Für Kartenprogramme",
        ["KML (Google Earth)"] = "Für Google Earth",
        ["Ergebnisse (CSV)"] = "Ergebnisse als Tabelle",
        ["CSV (Excel)"] = "Als Tabelle (Excel)",
        ["Diagramm (SVG)"] = "Diagramm als Zeichnung",
        ["SVG (Bild)"] = "Als Zeichnung",
        ["Entwurf als SVG"] = "Entwurf als Zeichnung",
        ["Entwurf als PNG (Druckgröße)"] = "Druckdatei speichern",
        ["Eurocode-Nachweis"] = "Nachweis nach Norm",
        ["On-Air-Uhr"] = "Sendeuhr",
        ["Etiketten (PDF)"] = "Adress-Etiketten drucken",
        ["Als ICS"] = "Als Kalenderdatei",
        ["Gästeliste laden (CSV)"] = "Gästeliste aus Tabelle laden",
    };

    /// <summary>V-11 (T-0700): feste Texte, Überschriften und Platzhalter mit Fachwort – in „Einfach“ in Alltagssprache. Ganze Texte (ein vorangestelltes Zeichen wie „✓ “ darf davorstehen),
    /// nie Inhalte der Person. In Erweitert und Professionell bleibt das Fachwort; in „Einfach“ steht es im Hinweis.</summary>
    public static readonly IReadOnlyDictionary<string, string> SimpleTexts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["winget (Benutzer)"] = "über Windows, ohne Admin-Rechte",
        ["Get-PnpDevice"] = "Windows erkennt die Geräte",
        ["GPIO / Sensoren"] = "Sensoren anschließen",
        ["GPIO am Pi"] = "Anschlüsse am Raspberry Pi",
        ["GPX laden"] = "Tour-Datei laden",
        ["CSV laden"] = "Tabelle laden",
        ["GPX-Datei (Uhr, Handy, Navi)"] = "Tour-Datei von Uhr, Handy oder Navi",
        ["CSV-Datei (Excel, Messgerät)"] = "Tabelle aus Excel oder vom Messgerät",
        ["vCard (.vcf) oder CSV (Google, Outlook) einlesen"] = "Kontakte aus Handy, Google oder Outlook einlesen",
        ["Prüfsumme (SHA-256) vom Hersteller"] = "Prüfzahl vom Hersteller (steht beim Download)",
        ["Slicen in Bambu Studio"] = "Für den Drucker vorbereiten (Bambu Studio)",
        ["Gewählt: COLMAP"] = "Rechenweg: COLMAP (Standard)",
        ["Gewählt: Meshroom"] = "Rechenweg: Meshroom",
        ["Texturqualität"] = "Bildqualität der Oberfläche",
        ["EXIF"] = "Aufnahmedaten",
    };

    /// <summary>Alltagswort in „Einfach“ für einen festen Text (auch mit einem Zeichen davor, z. B. „✓ GPIO / Sensoren“); null, wenn es keins gibt.</summary>
    public static string? SimpleText(string text)
    {
        var t = text.Trim();
        if (SimpleTexts.TryGetValue(t, out var p)) return p;
        var sp = t.IndexOf(' ');
        if (sp is > 0 and <= 2 && !char.IsLetterOrDigit(t[0]) && SimpleTexts.TryGetValue(t[(sp + 1)..], out var q)) return t[..(sp + 1)] + q;
        return null;
    }

    /// <summary>V-11 (T-0700): Tastenkürzel gehört in den Hinweis, nicht in den Knopf: „Teilen (S)“ → („Teilen“, „S“). Sonst null.</summary>
    public static (string Label, string Key)? WithoutShortcut(string label)
    {
        var m = System.Text.RegularExpressions.Regex.Match(label.Trim(), @"^(?<w>.*\p{L}.*?) \((?<k>(Strg|Umschalt|Alt)\+[^()]+|Entf|Leertaste|Esc|Enter|Tab|F\d{1,2}|[A-Z])\)$");
        return m.Success ? (m.Groups["w"].Value, m.Groups["k"].Value) : null;
    }

    /// <summary>Wörter, die nur an einem bestimmten Knopf ersetzt werden (sonst wäre die Bedeutung falsch, z. B. „GitHub“ beim Hochladen).</summary>
    public static readonly IReadOnlyDictionary<string, string> SimpleByName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hub-tab-github"] = "Freie Programme",
    };

    /// <summary>Alltagswort für einen Knopf mit Namen und fester Beschriftung (null, wenn es keins gibt).</summary>
    public static string? SimpleFor(string name, string label) => SimpleByName.TryGetValue(name, out var byName) ? byName : Simple(label);

    /// <summary>Alltagswort in „Einfach“ für eine feste Knopf-Beschriftung (null, wenn es keins gibt).</summary>
    public static string? Simple(string label) => SimpleLabels.TryGetValue(label.Trim(), out var p) ? p : null;

    /// <summary>Alltagswort für feste Texte des Desktops und der Einstellungen (alle Stufen).</summary>
    public static string Plain(string label) => Everyday.TryGetValue(label, out var p) ? p : label;

    /// <summary>Hinweis mit dem Fachwort, wenn das Alltagswort anders lautet (sonst null).</summary>
    public static string? Technical(string label) => Everyday.TryGetValue(label, out var p) && p != label ? $"Fachwort: {label}" : null;

    /// <summary>Alltagswort in der Stufe Einfach, sonst der unveränderte Text.</summary>
    public static string For(int level, string label) => level == UserLevel.Einfach && Map.TryGetValue(label, out var plain) ? plain : label;
}
