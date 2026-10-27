using System.Globalization;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>E-Mails & Termine F2: Termin-Vorschläge aus dem Text einer E-Mail (Datum, Wochentag, morgen/übermorgen, Uhrzeit).
/// Nur Vorschläge – eingetragen wird erst, wenn der Nutzer klickt.</summary>
public sealed record DateHint(DateTime Start, bool HasTime, string Text);

public static class MailDates
{
    static readonly string[] Months = ["januar", "februar", "märz", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "dezember"];
    static readonly string[] Weekdays = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];
    static readonly Regex Time = new(@"\b([01]?\d|2[0-3])(?:[:.]([0-5]\d))?\s*Uhr\b|\b([01]?\d|2[0-3]):([0-5]\d)\b", RegexOptions.IgnoreCase);
    static readonly Regex Numeric = new(@"\b(\d{1,2})\.\s?(\d{1,2})\.(?:\s?(\d{4}|\d{2}))?(?![\d.])");
    static readonly Regex Named = new(@"\b(\d{1,2})\.\s?(Januar|Februar|März|April|Mai|Juni|Juli|August|September|Oktober|November|Dezember)(?:\s+(\d{4}))?", RegexOptions.IgnoreCase);
    static readonly Regex Weekday = new(@"\b(?:am\s+|nächsten\s+|kommenden\s+)?(Montag|Dienstag|Mittwoch|Donnerstag|Freitag|Samstag|Sonntag)\b");
    static readonly Regex Relative = new(@"\b(übermorgen|morgen)\b");

    public static List<DateHint> Find(string text, DateTime reference)
    {
        var found = new List<(int Pos, DateTime Day, string Text)>();
        var today = reference.Date;
        DateTime? Make(int y, int m, int d) { try { return new DateTime(y, m, d); } catch (ArgumentOutOfRangeException) { return null; } }
        DateTime? Future(int m, int d, int? year)
        {
            if (year is { } y) return Make(y < 100 ? 2000 + y : y, m, d);
            var x = Make(today.Year, m, d);
            return x is { } v && v < today ? Make(today.Year + 1, m, d) : x;
        }
        foreach (Match m in Numeric.Matches(text))
            if (Future(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : null) is { } d) found.Add((m.Index, d, m.Value.Trim()));
        foreach (Match m in Named.Matches(text))
            if (Future(Array.IndexOf(Months, m.Groups[2].Value.ToLowerInvariant()) + 1, int.Parse(m.Groups[1].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : null) is { } d) found.Add((m.Index, d, m.Value.Trim()));
        foreach (Match m in Weekday.Matches(text))
        {
            var want = (DayOfWeek)((Array.IndexOf(Weekdays, m.Groups[1].Value) + 1) % 7);
            var ahead = ((int)want - (int)today.DayOfWeek + 7) % 7; if (ahead == 0) ahead = 7;
            found.Add((m.Index, today.AddDays(ahead), m.Value.Trim()));
        }
        foreach (Match m in Relative.Matches(text)) found.Add((m.Index, today.AddDays(m.Value == "übermorgen" ? 2 : 1), m.Value));

        var res = new List<DateHint>();
        foreach (var (pos, day, label) in found.OrderBy(f => f.Pos))
        {
            if (res.Any(r => r.Start.Date == day)) continue;
            // Uhrzeit: die nächste Angabe hinter dem Datum, sonst die erste im Text
            var t = Time.Match(text, pos); if (!t.Success) t = Time.Match(text);
            if (t.Success)
            {
                var h = int.Parse(t.Groups[1].Success ? t.Groups[1].Value : t.Groups[3].Value);
                var mi = t.Groups[2].Success ? int.Parse(t.Groups[2].Value) : t.Groups[4].Success ? int.Parse(t.Groups[4].Value) : 0;
                res.Add(new(day.AddHours(h).AddMinutes(mi), true, $"{label} · {t.Value.Trim()}"));
            }
            else res.Add(new(day, false, label));
        }
        return res;
    }
}
