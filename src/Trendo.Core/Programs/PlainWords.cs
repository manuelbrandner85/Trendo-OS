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
    };

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
