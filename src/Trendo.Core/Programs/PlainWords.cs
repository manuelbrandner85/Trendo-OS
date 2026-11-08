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

    /// <summary>Alltagswort in der Stufe Einfach, sonst der unveränderte Text.</summary>
    public static string For(int level, string label) => level == UserLevel.Einfach && Map.TryGetValue(label, out var plain) ? plain : label;
}
