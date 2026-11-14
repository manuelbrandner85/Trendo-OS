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
        ["GitHub & Code"] = "Programmieren",
        ["Mobile"] = "Handy",
        ["Credits"] = "Punkte",
        ["Skills"] = "KI-Fähigkeiten",
        ["Projektdaten"] = "Gespeicherte Daten",
        ["Export"] = "Speichern als",
        ["KI-Rollen und Teams"] = "KI-Helfer und Teams",
    };

    /// <summary>Alltagswort für feste Texte des Desktops und der Einstellungen (alle Stufen).</summary>
    public static string Plain(string label) => Everyday.TryGetValue(label, out var p) ? p : label;

    /// <summary>Hinweis mit dem Fachwort, wenn das Alltagswort anders lautet (sonst null).</summary>
    public static string? Technical(string label) => Everyday.TryGetValue(label, out var p) && p != label ? $"Fachwort: {label}" : null;

    /// <summary>Alltagswort in der Stufe Einfach, sonst der unveränderte Text.</summary>
    public static string For(int level, string label) => level == UserLevel.Einfach && Map.TryGetValue(label, out var plain) ? plain : label;
}
