namespace Trendo.Core.Programs;

/// <summary>Masterprompt 131 / 7: drei Bedienstufen für alle Programme – gleiche Fach-Engine, gleiche Daten, nur mehr oder weniger Werkzeuge.
/// Einfach: das Wichtigste, verständlich beschriftet. Erweitert: alle Fachwerkzeuge. Profi: zusätzlich Befehlssuche, Verlauf und direkte Projektdaten.</summary>
public static class UserLevel
{
    public const int Einfach = 0, Erweitert = 1, Profi = 2;
    public static readonly string[] Ids = ["einfach", "erweitert", "profi"];
    public static readonly string[] Names = ["Einfach", "Erweitert", "Professionell"];
    public static int Of(string? id) { var i = Array.IndexOf(Ids, (id ?? "").ToLowerInvariant()); return i < 0 ? Erweitert : i; }
    public static string Id(int level) => Ids[Math.Clamp(level, 0, 2)];
    /// <summary>Ein Werkzeug ist sichtbar, wenn seine Stufe höchstens der gewählten entspricht.</summary>
    public static bool Shows(int chosen, int toolLevel) => toolLevel <= chosen;
}
