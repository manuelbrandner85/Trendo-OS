using System.Globalization;
using System.Text.RegularExpressions;

namespace Trendo.Core.Wissen;

public enum GenDateKind { Empty, Exact, About, Before, After, Between, Period, Phrase, Unreadable }

/// <summary>Ein Tag im (gregorianischen) Kalender; Jahr historisch gezählt (−44 = 44 v. Chr., kein Jahr 0), Monat/Tag 0 = unbekannt.</summary>
public readonly record struct DayKey(int Year, int Month = 0, int Day = 0)
{
    public int Astro => Year < 0 ? Year + 1 : Year;
    public double Start => Astro + (Month == 0 ? 0 : (Month - 1) / 12.0) + (Month == 0 || Day == 0 ? 0 : (Day - 1) / 365.25);
    public double End => Month == 0 ? Astro + 1 - 1e-6 : Day == 0 ? Astro + Month / 12.0 - 1e-6 : Start + 1 / 365.25;
}

/// <summary>T-0788: Datum nach GEDCOM 5.5.1 – genau („12 MAR 1850“), ungefähr (ABT/CAL/EST), vor/nach (BEF/AFT), zwischen (BET … AND …),
/// Zeitraum (FROM … TO …), Text („(im Krieg)“, INT … (…)). <see cref="Raw"/> bleibt unverändert erhalten (Rundreise).
/// <see cref="Lo"/>/<see cref="Hi"/> sind frühester und spätester möglicher Zeitpunkt (Dezimaljahr) – für die Prüfungen.
/// Abweichende, aber verständliche Schreibweisen anderer Programme („12.03.1850“) werden gelesen und als <see cref="NonStandard"/> markiert.</summary>
public sealed partial record GenDate(string Raw, GenDateKind Kind, DayKey? A, DayKey? B, string Qual = "", string Phrase = "", bool NonStandard = false)
{
    /// <summary>Spielraum für „ungefähr“ in Jahren (zu beiden Seiten) – nur für die Widerspruchs-Prüfung.</summary>
    public const double AboutYears = 10;

    public static readonly string[] Ged = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    public bool IsEmpty => Kind == GenDateKind.Empty;
    public bool Comparable => Kind is not (GenDateKind.Empty or GenDateKind.Phrase or GenDateKind.Unreadable);

    public double Lo => Kind switch
    {
        GenDateKind.Exact => A!.Value.Start,
        GenDateKind.About => A!.Value.Start - AboutYears,
        GenDateKind.After => A!.Value.Start,
        GenDateKind.Between or GenDateKind.Period => A is { } a ? a.Start : double.NegativeInfinity,
        GenDateKind.Before => double.NegativeInfinity,
        _ => double.NaN,
    };

    public double Hi => Kind switch
    {
        GenDateKind.Exact => A!.Value.End,
        GenDateKind.About => A!.Value.End + AboutYears,
        GenDateKind.Before => A!.Value.End,
        GenDateKind.Between or GenDateKind.Period => B is { } b ? b.End : A is { } a && Kind == GenDateKind.Period ? double.PositiveInfinity : double.PositiveInfinity,
        GenDateKind.After => double.PositiveInfinity,
        _ => double.NaN,
    };

    /// <summary>Zum Sortieren: der wahrscheinlichste Zeitpunkt (Mitte bzw. bekannte Grenze); NaN ohne Datum.</summary>
    public double Sort => Kind switch
    {
        GenDateKind.Exact or GenDateKind.About => (A!.Value.Start + A.Value.End) / 2,
        GenDateKind.Before => A!.Value.Start,
        GenDateKind.After => A!.Value.Start,
        GenDateKind.Between or GenDateKind.Period => A is { } a ? a.Start : B!.Value.Start,
        _ => double.NaN,
    };

    /// <summary>Jahr für kurze Anzeigen (Kasten im Baum).</summary>
    public int? Year => A?.Year ?? B?.Year;

    public static readonly GenDate None = new("", GenDateKind.Empty, null, null);

    [GeneratedRegex(@"^(?:(\d{1,2})\s+)?(?:([A-Z]{3})\s+)?(\d{1,4})(?:/(\d{2}))?(?:\s*(B\.?\s?C\.?|BCE))?$")]
    private static partial Regex DateRx();
    [GeneratedRegex(@"^@#D([A-Z ]+)@\s*")]
    private static partial Regex CalRx();

    /// <summary>GEDCOM-Datum lesen (Groß-/Kleinschreibung egal). Unlesbares bleibt als <see cref="GenDateKind.Unreadable"/> mit Rohtext erhalten.</summary>
    /// <summary>T-0788 (Gegenprüfung G2): gleiche Datumstexte kommen in großen Stammbäumen oft vor – gelesene Daten werden wiederverwendet (unveränderlich).</summary>
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GenDate> Seen = new(StringComparer.Ordinal);

    public static GenDate Parse(string? raw)
    {
        var key = raw ?? "";
        if (Seen.TryGetValue(key, out var hit)) return hit;
        var g = ParseNew(key);
        if (Seen.Count < 200_000) Seen.TryAdd(key, g);
        return g;
    }

    static GenDate ParseNew(string? raw)
    {
        var r = (raw ?? "").Trim();
        if (r.Length == 0) return None;
        var u = Regex.Replace(r.ToUpperInvariant(), @"\s+", " ");
        if (u.StartsWith('(') && u.EndsWith(')')) return new(r, GenDateKind.Phrase, null, null, Phrase: r[1..^1].Trim());
        if (u.StartsWith("INT ", StringComparison.Ordinal))
        {
            var p = r.IndexOf('('); var core = p > 0 ? u[4..u.IndexOf('(')].Trim() : u[4..];
            var phrase = p > 0 ? r[(p + 1)..].TrimEnd(')').Trim() : "";
            return One(core) is { } k ? new(r, GenDateKind.About, k, null, "INT", phrase) : Lenient(r);
        }
        foreach (var q in new[] { "ABT", "CAL", "EST" })
            if (u.StartsWith(q + " ", StringComparison.Ordinal)) return One(u[(q.Length + 1)..]) is { } k ? new(r, GenDateKind.About, k, null, q) : Lenient(r);
        if (u.StartsWith("BEF ", StringComparison.Ordinal)) return One(u[4..]) is { } b ? new(r, GenDateKind.Before, b, null) : Lenient(r);
        if (u.StartsWith("AFT ", StringComparison.Ordinal)) return One(u[4..]) is { } a ? new(r, GenDateKind.After, a, null) : Lenient(r);
        if (u.StartsWith("BET ", StringComparison.Ordinal) && u.IndexOf(" AND ", StringComparison.Ordinal) is var i and > 0)
            return One(u[4..i]) is { } x && One(u[(i + 5)..]) is { } y ? new(r, GenDateKind.Between, x, y) : Lenient(r);
        if (u.StartsWith("FROM ", StringComparison.Ordinal))
        {
            var t = u.IndexOf(" TO ", StringComparison.Ordinal);
            var x = One(t > 0 ? u[5..t] : u[5..]); var y = t > 0 ? One(u[(t + 4)..]) : null;
            return x is not null && (t < 0 || y is not null) ? new(r, GenDateKind.Period, x, y) : Lenient(r);
        }
        if (u.StartsWith("TO ", StringComparison.Ordinal)) return One(u[3..]) is { } y2 ? new(r, GenDateKind.Period, null, y2) : Lenient(r);
        return One(u) is { } e ? new(r, GenDateKind.Exact, e, null) : Lenient(r);
    }

    static DayKey? One(string s)
    {
        s = s.Trim(); var julian = false;
        if (CalRx().Match(s) is { Success: true } cm)
        {
            var cal = cm.Groups[1].Value.Trim();
            if (cal is not ("GREGORIAN" or "JULIAN")) return null; // hebräisch, französisch republikanisch: nicht vergleichbar
            julian = cal == "JULIAN";
            s = s[cm.Length..];
        }
        var m = DateRx().Match(s);
        if (!m.Success) return null;
        var day = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var mon = 0;
        if (m.Groups[2].Success) { mon = Array.IndexOf(Ged, m.Groups[2].Value) + 1; if (mon == 0) return null; }
        else if (day > 0) return null;
        var year = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        if (m.Groups[4].Success)
        {
            // Doppeljahr 1699/00: nur das Folgejahr ist erlaubt (T-0788 Gegenprüfung G7) → neues Jahr
            if (int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) != (year + 1) % 100) return null;
            year++;
        }
        if (year == 0) return null;
        if (m.Groups[5].Success) year = -year;
        if (day > 0 && (mon == 0 || day > DaysIn(year, mon, julian || year < 0))) return null;
        return new DayKey(year, mon, day);
    }

    // ---------------- Deutsche Eingabe ----------------

    [GeneratedRegex(@"^(\d{1,2})\.(\d{1,2})\.(\d{1,4})$")] private static partial Regex DeDmy();
    [GeneratedRegex(@"^(\d{1,2})\.(\d{1,4})$")] private static partial Regex DeMy();
    [GeneratedRegex(@"^(?:(\d{1,2})\.?\s+)?([A-Za-zäÄ]{3,9})\.?\s+(\d{1,4})$")] private static partial Regex DeNamed();
    [GeneratedRegex(@"^(\d{1,4})$")] private static partial Regex DeY();
    [GeneratedRegex(@"\s*(v\.\s?Chr\.?|vor Christus|B\.?\s?C\.?)$", RegexOptions.IgnoreCase)] private static partial Regex DeBc();
    [GeneratedRegex(@"^(\d{1,4}(?:\s*v\.\s?Chr\.)?)\s*[–-]\s*(\d{1,4}(?:\s*v\.\s?Chr\.)?)$", RegexOptions.IgnoreCase)] private static partial Regex DeRange();

    static readonly string[] DeMonths = ["jan", "feb", "mär", "apr", "mai", "jun", "jul", "aug", "sep", "okt", "nov", "dez"];

    static DayKey? OneDe(string s)
    {
        s = s.Trim(); var bc = false;
        if (DeBc().Match(s) is { Success: true } b) { bc = true; s = s[..b.Index].Trim(); }
        DayKey? k = null;
        if (DeDmy().Match(s) is { Success: true } m1) k = Make(m1.Groups[1].Value, m1.Groups[2].Value, m1.Groups[3].Value);
        else if (DeMy().Match(s) is { Success: true } m2 && int.Parse(m2.Groups[1].Value, CultureInfo.InvariantCulture) is >= 1 and <= 12 && m2.Groups[2].Value.Length >= 3) k = Make("0", m2.Groups[1].Value, m2.Groups[2].Value);
        else if (DeNamed().Match(s) is { Success: true } m3)
        {
            var name = m3.Groups[2].Value.ToLowerInvariant().Replace("mrz", "mär").Replace("maerz", "mär");
            var mon = Array.FindIndex(DeMonths, x => name.StartsWith(x, StringComparison.Ordinal)) + 1;
            if (mon == 0) mon = Array.IndexOf(Ged, name.ToUpperInvariant()[..Math.Min(3, name.Length)]) + 1; // „12 MAR 1850“ klein geschrieben
            if (mon > 0) k = Make(m3.Groups[1].Success ? m3.Groups[1].Value : "0", mon.ToString(CultureInfo.InvariantCulture), m3.Groups[3].Value);
        }
        else if (DeY().Match(s) is { Success: true } m4) k = Make("0", "0", m4.Groups[1].Value);
        if (k is { } kk && bc) k = kk.Day > 0 && kk.Day > DaysIn(-kk.Year, kk.Month, true) ? null : kk with { Year = -kk.Year }; // v. Chr.: julianisch prüfen (G7)
        return k;
    }

    static DayKey? Make(string d, string m, string y)
    {
        var day = int.Parse(d, CultureInfo.InvariantCulture); var mon = int.Parse(m, CultureInfo.InvariantCulture); var year = int.Parse(y, CultureInfo.InvariantCulture);
        if (year == 0 || mon > 12 || (day > 0 && mon == 0)) return null;
        if (day > 0 && day > DaysIn(year, mon, year < 0)) return null;
        return new DayKey(year, mon, day);
    }

    /// <summary>T-0788 (Gegenprüfung G7): Tage im Monat – gregorianisch, julianisch (auch proleptisch für Jahre vor Christus: Schaltjahr, wenn das
    /// astronomische Jahr durch 4 teilbar ist, z. B. 45 v. Chr. = −44).</summary>
    public static int DaysIn(int year, int month, bool julian)
    {
        if (month != 2) return month is 4 or 6 or 9 or 11 ? 30 : 31;
        var astro = year < 0 ? year + 1 : year;
        var leap = julian ? astro % 4 == 0 : astro % 4 == 0 && (astro % 100 != 0 || astro % 400 == 0);
        return leap ? 29 : 28;
    }

    /// <summary>T-0788 (Gegenprüfung G7): Spanne „zwischen“/„von … bis“ mit Ende vor dem Anfang.</summary>
    public bool Reversed => Kind is GenDateKind.Between or GenDateKind.Period && A is { } a && B is { } b && a.Start > b.End;

    /// <summary>Deutsche Schreibweisen (auch von anderen Programmen exportiert): „12.03.1850“, „März 1850“, „ca. 1850“, „um 1850“, „vor 1850“, „nach 1850“,
    /// „zwischen 1850 und 1860“, „von 1850 bis 1860“, „1850–1860“, „44 v. Chr.“, „(Text)“. Liefert null, wenn nichts davon passt.</summary>
    public static GenDate? ParseGerman(string? input, string? raw = null)
    {
        var s = Regex.Replace((input ?? "").Trim(), @"\s+", " "); raw ??= s;
        if (s.Length == 0) return None;
        s = Regex.Replace(s, @"^(ca\.|ca|um|etwa|circa|vor|nach|ab|bis)(?=\d)", "$1 ", RegexOptions.IgnoreCase); // „ca.1850“ (Gegenprüfung G7)
        if (s.StartsWith('(') && s.EndsWith(')')) return new(raw, GenDateKind.Phrase, null, null, Phrase: s[1..^1].Trim());
        var low = s.ToLowerInvariant();
        foreach (var (p, q) in new[] { ("ca. ", "ABT"), ("ca ", "ABT"), ("um ", "ABT"), ("etwa ", "ABT"), ("circa ", "ABT"), ("errechnet ", "CAL"), ("geschätzt ", "EST") })
            if (low.StartsWith(p, StringComparison.Ordinal)) return OneDe(s[p.Length..]) is { } k ? new(raw, GenDateKind.About, k, null, q) : null;
        if (low.StartsWith("vor ", StringComparison.Ordinal)) return OneDe(s[4..]) is { } b ? new(raw, GenDateKind.Before, b, null) : null;
        if (low.StartsWith("bis ", StringComparison.Ordinal)) return OneDe(s[4..]) is { } b2 ? new(raw, GenDateKind.Period, null, b2) : null;
        if (low.StartsWith("nach ", StringComparison.Ordinal)) return OneDe(s[5..]) is { } a ? new(raw, GenDateKind.After, a, null) : null;
        if (low.StartsWith("ab ", StringComparison.Ordinal)) return OneDe(s[3..]) is { } a2 ? new(raw, GenDateKind.Period, a2, null) : null;
        if (low.StartsWith("zwischen ", StringComparison.Ordinal) && low.IndexOf(" und ", StringComparison.Ordinal) is var i and > 0)
            return OneDe(s[9..i]) is { } x && OneDe(s[(i + 5)..]) is { } y ? new(raw, GenDateKind.Between, x, y) : null;
        if (low.StartsWith("von ", StringComparison.Ordinal) && low.IndexOf(" bis ", StringComparison.Ordinal) is var j and > 0)
            return OneDe(s[4..j]) is { } x2 && OneDe(s[(j + 5)..]) is { } y2 ? new(raw, GenDateKind.Period, x2, y2) : null;
        if (DeRange().Match(s) is { Success: true } rm && OneDe(rm.Groups[1].Value) is { } r1 && OneDe(rm.Groups[2].Value) is { } r2) return new(raw, GenDateKind.Between, r1, r2);
        return OneDe(s) is { } e ? new(raw, GenDateKind.Exact, e, null) : null;
    }

    static GenDate Lenient(string raw) => ParseGerman(raw, raw) is { } g && g.Kind != GenDateKind.Empty ? g with { NonStandard = g.Kind != GenDateKind.Phrase } : new(raw, GenDateKind.Unreadable, null, null);

    /// <summary>Eingabe der Person (deutsch oder GEDCOM) → GEDCOM-Schreibweise. Wirft mit einem Satz, wenn das Datum nicht lesbar ist.</summary>
    public static string FromInput(string? input)
    {
        var s = (input ?? "").Trim();
        if (s.Length == 0) return "";
        var g = Parse(s);
        if (g.Kind is GenDateKind.Unreadable || g.NonStandard && g.Kind != GenDateKind.Phrase)
            g = ParseGerman(s) ?? throw new ArgumentException($"Datum „{s}“ nicht lesbar. Beispiele: 12.03.1850, März 1850, 1850, um 1850, vor 1850, zwischen 1850 und 1860, 44 v. Chr.");
        if (g.Reversed) throw new ArgumentException($"Spanne verkehrt: „{s}“ – das Ende liegt vor dem Anfang.");
        return g.ToGedcom();
    }

    static string GedOne(DayKey k) => (k.Day > 0 ? k.Day.ToString(CultureInfo.InvariantCulture) + " " : "") + (k.Month > 0 ? Ged[k.Month - 1] + " " : "") + Math.Abs(k.Year).ToString(CultureInfo.InvariantCulture) + (k.Year < 0 ? " B.C." : "");

    /// <summary>Genormte GEDCOM-Schreibweise (bei Text in Klammern).</summary>
    public string ToGedcom() => Kind switch
    {
        GenDateKind.Empty => "",
        GenDateKind.Exact => GedOne(A!.Value),
        GenDateKind.About => (Qual is "CAL" or "EST" ? Qual : "ABT") + " " + GedOne(A!.Value),
        GenDateKind.Before => "BEF " + GedOne(A!.Value),
        GenDateKind.After => "AFT " + GedOne(A!.Value),
        GenDateKind.Between => $"BET {GedOne(A!.Value)} AND {GedOne(B!.Value)}",
        GenDateKind.Period => A is { } a ? "FROM " + GedOne(a) + (B is { } b ? " TO " + GedOne(b) : "") : "TO " + GedOne(B!.Value),
        GenDateKind.Phrase => "(" + Phrase + ")",
        _ => Raw,
    };

    static string DeOne(DayKey k, bool shortForm)
    {
        var y = Math.Abs(k.Year).ToString(CultureInfo.InvariantCulture) + (k.Year < 0 ? " v. Chr." : "");
        if (shortForm || k.Month == 0) return y;
        return (k.Day > 0 ? k.Day.ToString(CultureInfo.InvariantCulture) + ". " : "") + HistDate.Months[k.Month - 1] + " " + y;
    }

    /// <summary>Deutsche Anzeige: „12. März 1850“, „um 1850“, „vor 1850“, „zwischen 1850 und 1860“ …; <paramref name="shortForm"/> nur mit Jahren.</summary>
    public string Show(bool shortForm = false) => Kind switch
    {
        GenDateKind.Empty => "",
        GenDateKind.Exact => DeOne(A!.Value, shortForm),
        GenDateKind.About => (Qual switch { "CAL" => "errechnet ", "EST" => "geschätzt ", _ => shortForm ? "~" : "um " }) + DeOne(A!.Value, shortForm) + (Phrase.Length > 0 && !shortForm ? $" ({Phrase})" : ""),
        GenDateKind.Before => "vor " + DeOne(A!.Value, shortForm),
        GenDateKind.After => "nach " + DeOne(A!.Value, shortForm),
        GenDateKind.Between => shortForm ? $"{DeOne(A!.Value, true)}–{DeOne(B!.Value, true)}" : $"zwischen {DeOne(A!.Value, false)} und {DeOne(B!.Value, false)}",
        GenDateKind.Period => A is { } a ? (B is { } b ? $"von {DeOne(a, shortForm)} bis {DeOne(b, shortForm)}" : "ab " + DeOne(a, shortForm)) : "bis " + DeOne(B!.Value, shortForm),
        GenDateKind.Phrase => Phrase,
        _ => Raw,
    };
}
