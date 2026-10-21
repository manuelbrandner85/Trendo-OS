using System.Globalization;

namespace Trendo.Core.Shell;

/// <summary>Startmenü-Regeln: angeheftete Studios, zuletzt benutzt, A–Z-Liste. Reine Logik, ohne Oberfläche testbar.</summary>
public static class StartMenu
{
    public const int MaxRecent = 8, MaxPinned = 12;
    static readonly CultureInfo De = new("de-DE");

    public static List<string> Pin(IList<string> pinned, string id) { var l = pinned.ToList(); if (!l.Contains(id) && l.Count < MaxPinned) l.Add(id); return l; }
    public static List<string> Unpin(IList<string> pinned, string id) => pinned.Where(p => p != id).ToList();
    public static bool IsPinned(IList<string> pinned, string id) => pinned.Contains(id);

    /// <summary>Neuestes zuerst, ohne Doppelte, höchstens 8. Der Startbildschirm selbst zählt nicht.</summary>
    public static List<string> Touch(IList<string> recent, string key)
    {
        if (key is "start" or "") return recent.ToList();
        var l = recent.Where(r => r != key).ToList(); l.Insert(0, key); return l.Take(MaxRecent).ToList();
    }

    /// <summary>Alphabetisch nach deutschem Alphabet, gruppiert nach Anfangsbuchstabe (Ziffern und Zeichen unter „#“, Umlaute beim Grundbuchstaben).</summary>
    public static List<(string Letter, List<T> Items)> AtoZ<T>(IEnumerable<T> items, Func<T, string> name) =>
        items.OrderBy(name, StringComparer.Create(De, true))
             .GroupBy(i => Letter(name(i)))
             .Select(g => (g.Key, g.ToList())).ToList();

    public static string Letter(string s)
    {
        var c = s.TrimStart().FirstOrDefault();
        if (!char.IsLetter(c)) return "#";
        var u = char.ToUpper(c, De);
        return u switch { 'Ä' => "A", 'Ö' => "O", 'Ü' => "U", _ => u.ToString() };
    }
}
