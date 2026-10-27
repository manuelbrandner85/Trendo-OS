using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Tv;

/// <summary>Wunsch 27.10.2026: TV & Live per Sprache/Text steuern („blende alle Sender aus und behalte nur deutsche“ …).
/// Erst feste deutsche Regeln (ohne KI, sofort), sonst übersetzt die KI den Satz in genau eine der erlaubten Aktionen (JSON).
/// Es gibt nur diese Aktionen – nichts anderes kann über den Assistenten ausgelöst werden.</summary>
public static partial class TvCommands
{
    /// <param name="Kind">only-country, show-all, hide-groups, show-groups, lock-groups, unlock-groups, play, favorite, record, films, series, live, guide, search, provider, lang</param>
    /// <param name="Extra">bei only-country/show-all/provider/lang: Bereich live, film, serie oder leer (alles); bei films/series: Kategorie</param>
    public sealed record Action(string Kind, string Arg = "", string Extra = "");

    public static readonly string[] Kinds = ["only-country", "show-all", "hide-groups", "show-groups", "lock-groups", "unlock-groups", "play", "favorite", "record", "films", "series", "live", "guide", "search", "provider", "lang"];

    /// <summary>Streaming-Anbieter und Sender, nach denen viele Listen Filme und Serien einordnen („DE • NETFLIX SERIEN“, „TR • DISNEY PLUS DIZILER“).</summary>
    public static readonly (string Name, string[] Keys)[] Providers =
    [
        ("Netflix", ["netflix"]), ("Amazon Prime", ["amazon", "prime video", " prime"]), ("Disney+", ["disney"]), ("Apple TV+", ["apple"]),
        ("HBO Max", ["hbo", "hmax"]), ("Paramount+", ["paramount"]), ("RTL+", ["rtl+", "rtl plus", "tvnow"]), ("Joyn", ["joyn"]), ("Sky", ["sky"]),
        ("WOW", ["wow "]), ("MagentaTV", ["magenta"]), ("DAZN", ["dazn"]), ("Crunchyroll", ["crunchyroll"]), ("ARD/ZDF", [" ard ", "zdf", "mediathek"]),
    ];

    /// <summary>Gehört eine Kategorie zu einem Anbieter?</summary>
    public static bool HasProvider(string category, string provider)
    {
        var c = " " + category.ToLowerInvariant().Replace('•', ' ').Replace('|', ' ') + " ";
        var p = Providers.FirstOrDefault(x => x.Name == provider);
        return p.Keys is not null && p.Keys.Any(c.Contains);
    }

    /// <summary>Anbieter, die in den Kategorien vorkommen (für die Schnellwahl).</summary>
    public static List<string> ProvidersIn(IEnumerable<string> categories)
    {
        var cats = categories.Distinct().ToList();
        return Providers.Select(p => p.Name).Where(p => cats.Any(c => HasProvider(c, p))).ToList();
    }

    [GeneratedRegex(@"^\s*\|?\s*([A-Za-z]{2,5})\s*(?:[•|:\-–]|\s{2,})")] private static partial Regex CountryPrefix();

    /// <summary>Sprache/Land einer Kategorie: „DE • DRAMA“ → de, „TR • NETFLIX DIZILER“ → tr, „Germany Sport“ → de, „EU • MULTI“ → eu.</summary>
    public static string? CountryOf(string category)
    {
        var m = CountryPrefix().Match(category);
        if (m.Success)
        {
            var t = m.Groups[1].Value.ToLowerInvariant();
            t = t switch { "ger" or "deu" => "de", "gb" or "en" or "eng" => "uk", "usa" => "us", "tur" => "tr", "ita" => "it", "fra" => "fr", "esp" => "es", _ => t };
            if (t is not ("xxx" or "vod" or "hd" or "fhd" or "uhd" or "4k" or "the")) return t;
        }
        foreach (var code in Countries.Keys) if (IsCountry(category, code)) return code;
        return null;
    }

    public static string LanguageLabel(string code) => code switch
    {
        "de" => "Deutsch", "at" => "Österreich", "ch" => "Schweiz", "uk" => "Englisch", "us" => "USA", "tr" => "Türkisch", "fr" => "Französisch", "it" => "Italienisch",
        "es" => "Spanisch", "pl" => "Polnisch", "nl" => "Niederländisch", "ar" => "Arabisch", "eu" => "Europa/Mehrsprachig", "exyu" => "Ex-Jugoslawien", "al" => "Albanisch",
        "ru" => "Russisch", "ro" => "Rumänisch", "gr" => "Griechisch", "pt" => "Portugiesisch", "kurd" => "Kurdisch", _ => code.ToUpperInvariant(),
    };

    /// <summary>Welcher Bereich ist gemeint? „Filme“ → film, „Serien“ → serie, „Sender“ → live, sonst alles.</summary>
    public static string Scope(string text)
    {
        var t = text.ToLowerInvariant();
        return Regex.IsMatch(t, @"\bfilm|filme\b") ? "film" : t.Contains("serie") ? "serie" : Regex.IsMatch(t, @"\b(sender|kanäle|kanal|live|fernseh)") ? "live" : "";
    }

    /// <summary>Länder mit ihren Kennungen in Gruppen- und Sendernamen.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Countries = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = ["de", "ger", "germany", "german", "deutsch", "deutschland", "🇩🇪"],
        ["at"] = ["at", "austria", "österreich", "oesterreich", "🇦🇹"],
        ["ch"] = ["ch", "swiss", "schweiz", "switzerland", "🇨🇭"],
        ["uk"] = ["uk", "gb", "united kingdom", "england", "british", "🇬🇧"],
        ["us"] = ["us", "usa", "united states", "american", "🇺🇸"],
        ["tr"] = ["tr", "turkey", "türkei", "turkiye", "türkiye", "turkish", "🇹🇷"],
        ["fr"] = ["fr", "france", "frankreich", "french", "🇫🇷"],
        ["it"] = ["it", "italy", "italien", "italia", "🇮🇹"],
        ["es"] = ["es", "spain", "spanien", "españa", "🇪🇸"],
        ["pl"] = ["pl", "poland", "polen", "polska", "🇵🇱"],
        ["nl"] = ["nl", "netherlands", "niederlande", "holland", "🇳🇱"],
        ["ar"] = ["ar", "arabic", "arabisch", "arab"],
    };

    static readonly (string Word, string Code)[] CountryWords =
    [
        ("deutsch", "de"), ("deutschland", "de"), ("german", "de"), ("österreich", "at"), ("oesterreich", "at"), ("schweiz", "ch"), ("schweizer", "ch"),
        ("englisch", "uk"), ("britisch", "uk"), ("amerikanisch", "us"), ("usa", "us"), ("türkisch", "tr"), ("tuerkisch", "tr"), ("französisch", "fr"),
        ("italienisch", "it"), ("spanisch", "es"), ("polnisch", "pl"), ("niederländisch", "nl"), ("holländisch", "nl"), ("arabisch", "ar"),
    ];

    /// <summary>Gehört eine Gruppe (oder ihr Sender) zu einem Land? „DE | Sport“, „Germany“, „DE: ZDF HD“, „Deutschland“ …</summary>
    public static bool IsCountry(string text, string code)
    {
        if (!Countries.TryGetValue(code, out var keys)) return false;
        var t = text.ToLowerInvariant();
        foreach (var k in keys)
        {
            if (k.Length <= 3 && k.All(char.IsAsciiLetter))
            {
                if (Regex.IsMatch(t, $@"(^|[^a-z]){Regex.Escape(k)}([^a-z]|$)")) return true;
            }
            else if (t.Contains(k)) return true;
        }
        return false;
    }

    /// <summary>Gruppen, die zu einem Land gehören: nach Gruppenname – oder wenn die meisten Sender darin so heißen.</summary>
    public static List<string> CountryGroups(IEnumerable<TvChannel> channels, string code) =>
        channels.GroupBy(c => c.Group).Where(g => IsCountry(g.Key, code) || g.Count(c => IsCountry(c.Name, code)) * 2 > g.Count()).Select(g => g.Key).ToList();

    [GeneratedRegex(@"\b(erwachsen\w*|adult|xxx|18\+|porn\w*|erotik)\b", RegexOptions.IgnoreCase)] private static partial Regex AdultWords();

    /// <summary>Feste Regeln für die häufigen Wünsche (ohne KI). Liefert leer, wenn der Satz nicht sicher verstanden wurde.</summary>
    public static List<Action> Rules(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        var list = new List<Action>();
        if (t.Length == 0) return list;
        var country = CountryWords.FirstOrDefault(c => t.Contains(c.Word)).Code;
        var scope = Scope(t);
        var prov = Providers.FirstOrDefault(p => p.Keys.Any(k => (" " + t + " ").Contains(k))).Name;
        // „Spiel ZDF“, „Schalt auf Sky Sport“ meinen Sender – Anbieter wie Sky/ARD/ZDF nur, wenn Filme oder Serien gemeint sind
        if (prov is "Sky" or "ARD/ZDF" or "Joyn" or "RTL+" or "DAZN" or "MagentaTV" or "WOW" && scope is not ("film" or "serie")) prov = null;
        if (country is not null && Regex.IsMatch(t, @"\b(nur|behalt\w*|ausschließlich|lediglich|alle\s+(anderen\s+)?aus)\b"))
        {
            list.Add(new("only-country", country, scope));
            if (prov is not null) list.Add(new("provider", prov, scope == "film" ? "film" : "serie"));
            return list;
        }
        if (Regex.IsMatch(t, @"\b(alle|wieder)\b.*\b(zeig\w*|einblend\w*|anzeig\w*)\b|\balle\s+\w*\s*wieder\b|zurücksetzen|\bwieder\s+alles?\b|\balles?\s+wieder\b")) { list.Add(new("show-all", "", scope)); return list; }
        if (AdultWords().IsMatch(t) && Regex.IsMatch(t, @"\b(sperr\w*|schütz\w*|kindersicher\w*)\b")) { list.Add(new("lock-groups", "adult")); return list; }
        if (AdultWords().IsMatch(t) && Regex.IsMatch(t, @"\b(ausblend\w*|versteck\w*|weg)\b")) { list.Add(new("hide-groups", "adult")); return list; }
        var m = Regex.Match(t, @"\b(?:blend\w*|versteck\w*)\s+(?:die\s+)?(?:gruppe\s+)?[„""]?(.+?)[“""]?\s+aus\b");
        if (m.Success) { list.Add(new("hide-groups", m.Groups[1].Value)); return list; }
        m = Regex.Match(t, @"\b(?:nimm|aufnehmen|aufzeichnen|nehme)\b\s*(?:mir\s+)?(?:heute\s+(?:abend\s+)?|morgen\s+)?(?:den|die|das)?\s*(.+?)(?:\s+auf)?$");
        if (Regex.IsMatch(t, @"\b(nimm|aufnehmen|aufzeichnen|nehme)\b") && m.Success && m.Groups[1].Value.Trim().Length > 1) { list.Add(new("record", m.Groups[1].Value.Trim(' ', '.', '!'))); return list; }
        m = Regex.Match(t, @"\b(?:favorit\w*)\b.*?(?:mach\w*|setz\w*)?\s*(.+)$|\b(?:mach|setz)\w*\s+(.+?)\s+zu(?:m|r)?\s+favorit");
        if (Regex.IsMatch(t, @"favorit") && m.Success) { var a = (m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : m.Groups[1].Value).Replace("zum", "").Replace("favoriten", "").Trim(' ', '.', '!'); if (a.Length > 1) { list.Add(new("favorite", a)); return list; } }
        if (prov is not null)
        {
            list.Add(new("provider", prov, scope == "film" ? "film" : "serie"));
            if (country is not null) list.Add(new("lang", country, scope == "film" ? "film" : "serie"));
            return list;
        }
        if (country is not null && scope is "film" or "serie") list.Add(new("lang", country, scope));
        m = Regex.Match(t, @"\b(?:spiel\w*|schalt\w*\s+(?:auf|zu)|mach\s+an|öffne|zeig\s+mir\s+den\s+sender)\s+(.+)$");
        if (m.Success && scope is not ("film" or "serie")) { list.Add(new("play", m.Groups[1].Value.Replace(" an", "").Trim(' ', '.', '!'))); return list; }
        m = Regex.Match(t, @"\b(?:zeig\w*|such\w*|finde\w*)\b.*?\b(?:filme?)\b\s*(?:mit|über|zu)?\s*(.*)$");
        if (m.Success && Regex.IsMatch(t, @"\bfilme?\b")) { list.Add(new("films", m.Groups[1].Value.Trim(' ', '.', '!'), Regex.Match(t, @"(\w+)-?filme").Groups[1].Value)); return list; }
        m = Regex.Match(t, @"\b(?:zeig\w*|such\w*|finde\w*)\b.*?serien?\b\s*(?:mit|über|zu)?\s*(.*)$");
        if (m.Success) { list.Add(new("series", m.Groups[1].Value.Trim(' ', '.', '!'), Regex.Match(t, @"(\w+)-?serien?\b").Groups[1].Value)); return list; }
        if (list.Count > 0) return list; // „deutsche Filme“: nur die Sprache
        if (Regex.IsMatch(t, @"programmführer|was läuft|tv-programm|fernsehprogramm")) { list.Add(new("guide")); return list; }
        return list;
    }

    /// <summary>Anweisung an die KI, wenn die Regeln nichts verstanden haben: Antwort nur als JSON mit erlaubten Aktionen.</summary>
    public const string KiSystem = "Du übersetzt Wünsche für ein IPTV-Programm in Aktionen. Antworte NUR mit JSON, z. B. [{\"kind\":\"only-country\",\"arg\":\"de\"}]. " +
        "Erlaubte kind-Werte: only-country (arg: Ländercode de, at, ch, uk, us, tr, fr, it, es, pl, nl, ar), show-all, hide-groups (arg: Teil des Gruppennamens oder adult), " +
        "show-groups, lock-groups (arg wie hide-groups), unlock-groups, play (arg: Sendername), favorite (arg: Sendername), record (arg: Titel der Sendung), " +
        "films (arg: Suchwort, extra: Kategorie), series (arg: Suchwort, extra: Kategorie), live, guide, search (arg: Sendername), " +
        "provider (arg: Netflix, Amazon Prime, Disney+, Apple TV+, HBO Max, Paramount+, RTL+, Joyn, Sky; extra: film oder serie), lang (arg: Ländercode; extra: film oder serie). " +
        "Bei only-country und show-all ist extra der Bereich: live (Sender), film, serie oder leer für alles. Unbekanntes: [].";

    /// <summary>KI-Antwort lesen – nur bekannte Aktionen, höchstens 5.</summary>
    public static List<Action> ParseKi(string answer)
    {
        var start = answer.IndexOf('['); var end = answer.LastIndexOf(']');
        if (start < 0 || end <= start) return [];
        try
        {
            using var d = JsonDocument.Parse(answer[start..(end + 1)]);
            var list = new List<Action>();
            foreach (var e in d.RootElement.EnumerateArray())
            {
                string S(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                var kind = S("kind");
                if (Kinds.Contains(kind)) list.Add(new(kind, S("arg"), S("extra")));
            }
            return list.Take(5).ToList();
        }
        catch (JsonException) { return []; }
    }

    /// <summary>Bester Sender zu einem gesprochenen Namen („zdf“ → „DE: ZDF HD“).</summary>
    public static TvChannel? FindChannel(IEnumerable<TvChannel> channels, string name)
    {
        var n = name.Trim().ToLowerInvariant();
        if (n.Length == 0) return null;
        if (int.TryParse(n, out var num)) return channels.FirstOrDefault(c => c.Number == num);
        string Clean(string s) => Regex.Replace(s.ToLowerInvariant(), @"^(de|at|ch|uk|us)\s*[:|-]\s*|\b(hd|fhd|uhd|4k|sd|hevc|raw)\b", "").Trim();
        var all = channels.ToList();
        return all.FirstOrDefault(c => Clean(c.Name) == n) ?? all.FirstOrDefault(c => Clean(c.Name).StartsWith(n)) ?? all.FirstOrDefault(c => c.Name.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
