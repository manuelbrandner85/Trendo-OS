using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Feste Wertelisten der Fach-Werkzeuge. Die Rechenfunktionen bleiben unverändert und lesen genau diese Wörter.</summary>
public static class FachChoices
{
    public static readonly string[] JaNeinWerte = ["ja", "nein"];
    public static readonly string[] Werkstoff = ["Baustahl", "Edelstahl", "Aluminium", "Fichte", "Beton"];
    public static readonly string[] Einheiten =
    [
        "mm", "cm", "m", "km", "in", "zoll", "ft",
        "N", "kN", "kg", "lbf", "Pa", "kPa", "MPa", "bar", "psi",
        "Nm", "kNm", "W", "kW", "PS",
    ];
    public static readonly string[] Schrauben = ["M3", "M4", "M5", "M6", "M8", "M10", "M12", "M16", "M20", "M24"];
    public static readonly string[] Klassen = ["8.8", "10.9", "12.9", "A2-70"];
    public static readonly string[] Qualitaet = ["720p30", "720p60", "1080p30", "1080p60", "1440p30", "1440p60", "2160p30", "2160p60"];

    public static bool JaNein(string label) => label.Contains("(j/n)", StringComparison.OrdinalIgnoreCase);

    public static string SichtLabel(string label) => Regex.Replace(label, @"\s*\(j/n\)", "", RegexOptions.IgnoreCase).Trim();

    public static string[]? For(string tool, string field) => (tool, field) switch
    {
        ("arch-heat", "std") => ["altbau", "teilsaniert", "neubau", "kfw", "passivhaus"],
        ("arch-light", "kind") => ["wohnzimmer", "schlafzimmer", "küche", "bad", "büro", "flur", "kinderzimmer", "werkstatt"],
        ("el-comb", "mode") => ["parallel", "reihe"],
        ("el-comb", "kind") => ["r", "c"],
        ("eng-beam", "case") or ("eng-buckle", "fall") => ["1", "2", "3", "4"],
        ("eng-beam", "mat") or ("eng-heat", "mat") or ("eng-hollow", "mat") or ("eng-buckle", "mat") or ("eng-axial", "mat") or ("eng-mass", "mat") => Werkstoff,
        ("eng-unit", "from") or ("eng-unit", "to") => Einheiten,
        ("eng-bolt", "size") => Schrauben,
        ("eng-bolt", "grade") => Klassen,
        ("sci-n", "c") => ["90", "95", "99"],
        ("sci-err", "op") => ["+", "−", "*", "/"],
        ("geo-utm", "h") => ["N", "S"],
        ("scan-scale", "from") or ("scan-scale", "to") => ["mm", "cm", "m", "in", "ft"],
        ("cnc-feeds", "mat") => Cnc.Materials.Keys.ToArray(),
        ("cnc-mrr", "mat") => CncPlus.Kc.Keys.ToArray(),
        ("bc-rate", "pf") => ["YouTube", "Twitch", "Facebook"],
        ("bc-rate", "q") => Qualitaet,
        ("bc-loud", "pf") => BroadcastPlus.LoudTargets.Keys.ToArray(),
        ("rob-see", "color") or ("rob-count", "color") or ("rob-mask", "color") => Vision.Hues.Keys.ToArray(),
        ("car-company", "kind") => ["verbrenner", "hybrid", "elektro"],
        ("fa-body", "who") => ["Damen", "Herren"],
        _ => null,
    };

    public static string? Einheit(string label)
    {
        var m = Regex.Match(label, @"\(([^()]*)\)\s*$");
        if (!m.Success) return null;
        var u = m.Groups[1].Value.Trim();
        if (u.Length == 0 || u.Contains(' ') || u.Contains(',') || u.Any(char.IsDigit)) return null;
        if (u.Contains("optional", StringComparison.OrdinalIgnoreCase) || u.Equals("j/n", StringComparison.OrdinalIgnoreCase)) return null;
        return u;
    }

    public static bool IstZahl(string label, string standard)
    {
        if (JaNein(label)) return false;
        if (Einheit(label) is not null) return true;
        if (string.IsNullOrWhiteSpace(standard)) return false;
        try { Fach.Num(standard); } catch (FormatException) { return false; }
        var l = label.ToLowerInvariant();
        if (l.Contains("code") || l.Contains("aufdruck") || l.Contains("text") || l.Contains("datei") || l.Contains("name") || l.Contains("farbe") || l.Contains("pfad") || l.Contains("zeile") || l.Contains("oder"))
            return false;
        return true;
    }

    /// <summary>Seite eines Fach-Studios zum Werkzeug-Namen. Null, wenn das Werkzeug zu keinem Studio gehört.</summary>
    public static string? Studio(string toolId)
    {
        (string Prefix, string Page)[] map =
        [
            ("arch-", "architektur"), ("el-", "elektronik"), ("eng-", "technik"), ("xr-", "xr"),
            ("sci-", "wissenschaft"), ("learn-", "lernen"), ("fa-", "mode"), ("scan-", "scan3d"),
            ("twin-", "zwilling"), ("geo-", "karten"), ("cnc-", "cnc"), ("bc-", "sendung"),
            ("car-", "fahrzeug"), ("rob-", "roboter"),
        ];
        foreach (var (prefix, page) in map)
            if (toolId.StartsWith(prefix, StringComparison.Ordinal)) return page;
        return null;
    }

    public static bool Gehoert(string studio, string key) =>
        key.Contains(Prefix(studio), StringComparison.Ordinal);

    static string Prefix(string studio) => studio switch
    {
        "architektur" => "arch-", "elektronik" => "el-", "technik" => "eng-", "xr" => "xr-",
        "wissenschaft" => "sci-", "lernen" => "learn-", "mode" => "fa-", "scan3d" => "scan-",
        "zwilling" => "twin-", "karten" => "geo-", "cnc" => "cnc-", "sendung" => "bc-",
        "fahrzeug" => "car-", "roboter" => "rob-", _ => studio + "-",
    };
}

/// <summary>Eingaben und letzte Ergebnisse eines Fach-Studios. Liegt neben den bestehenden Projektdateien, überschreibt sie nicht.</summary>
public sealed class FachStand
{
    public Dictionary<string, string> Eingaben { get; set; } = [];
    public Dictionary<string, string> Ergebnisse { get; set; } = [];
}
