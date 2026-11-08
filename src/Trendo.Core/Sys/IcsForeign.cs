using System.Text.RegularExpressions;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using IcalCalendar = Ical.Net.Calendar;

namespace Trendo.Core.Sys;

/// <summary>Alle Studios, Runde 6 – SYS-09/P16: fremde Kalenderdateien (Google, Outlook, Apple, Thunderbird) mit ical.net (MIT) lesen.
/// Trendos eigener Leser kennt nur „jeden Tag/Woche/Monat/Jahr ohne Ende“. Regeln mit Ende (COUNT/UNTIL), Intervall, Wochentagen,
/// Ausnahmen (EXDATE), geänderten Einzelterminen (RECURRENCE-ID) und Zeitzonen (TZID) löst ical.net auf: Einfache Reihen bleiben Reihen,
/// alle anderen werden im Zeitfenster als einzelne Termine übernommen (UID „…#JJJJMMTTTHHMM“), damit nichts verloren geht und nichts dazukommt.</summary>
public static partial class IcsForeign
{
    /// <summary>Höchstens so viele Einzeltermine je Datei (Schutz vor endlosen Regeln).</summary>
    public const int MaxOccurrences = 5000;
    /// <summary>Zeitfenster für aufgelöste Reihen: ein Jahr zurück, zwei Jahre voraus.</summary>
    public static (DateTime From, DateTime To) Window(DateTime now) => (now.Date.AddYears(-1), now.Date.AddYears(2));

    [GeneratedRegex(@"\r?\n[ \t]")] private static partial Regex Fold();
    [GeneratedRegex(@"^(?:RRULE:(?!FREQ=(?:DAILY|WEEKLY|MONTHLY|YEARLY);?$)|EXDATE[;:]|RDATE[;:]|RECURRENCE-ID[;:]|(?:DTSTART|DTEND|DUE)[^:]*;TZID=)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ComplexLine();

    /// <summary>Braucht die Datei den vollständigen Leser? (Regeln mit Ende/Intervall/Tagen, Ausnahmen, Einzeländerungen, Zeitzonen)</summary>
    public static bool NeedsFullParser(string ics) => ComplexLine().IsMatch(Fold().Replace(ics ?? "", "").Replace("\r", ""));

    /// <summary>Termine lesen. <paramref name="now"/> bestimmt das Zeitfenster für aufgelöste Reihen.</summary>
    /// <exception cref="FormatException">Datei ist keine lesbare Kalenderdatei.</exception>
    public static (List<CalEvent> Events, IReadOnlyList<string> ExpandedSeries) Import(string ics, DateTime now)
    {
        IcalCalendar cal;
        try { cal = IcalCalendar.Load(ics) ?? throw new FormatException("Keine Kalenderdaten gefunden."); }
        catch (Exception ex) when (ex is not FormatException) { throw new FormatException("Die Kalenderdatei ließ sich nicht lesen: " + ex.Message, ex); }

        var events = new List<CalEvent>(); var expanded = new List<string>();
        var masters = cal.Events.Where(e => e.RecurrenceId is null && e.DtStart is not null).ToList();
        var withOverrides = cal.Events.Where(e => e.RecurrenceId is not null).Select(e => e.Uid ?? "").ToHashSet();
        var complex = new HashSet<string>();
        foreach (var e in masters)
        {
            var uid = string.IsNullOrWhiteSpace(e.Uid) ? Guid.NewGuid().ToString("N") : e.Uid!;
            var simple = SimpleRepeat(e, withOverrides.Contains(uid));
            if (simple is { } rep) { events.Add(Make(e, uid, Local(e.DtStart!), End(e), rep)); continue; }
            complex.Add(uid);
        }
        if (complex.Count > 0)
        {
            var (from, to) = Window(now); var n = 0;
            try { Expand(); }
            catch (Exception ex) when (ex is not (FormatException or OutOfMemoryException)) { throw new FormatException("Eine Wiederholungsregel in der Kalenderdatei ließ sich nicht auflösen: " + ex.Message, ex); }
            expanded.AddRange(complex);

            void Expand()
            {
                foreach (var o in cal.GetOccurrences(new CalDateTime(from.ToUniversalTime(), "UTC")))
                {
                    var st = Local(o.Period.StartTime); if (st >= to) break; // Fenster zuerst: auch endlose einfache Reihen beenden den Lauf
                    if (o.Source is not CalendarEvent src || !complex.Contains(src.Uid ?? "")) continue;
                    var en = o.Period.EffectiveEndTime is { } pe ? Local(pe) : st + (End(src) - Local(src.DtStart!));
                    events.Add(Make(src, src.Uid! + "#" + st.ToString("yyyyMMdd'T'HHmm", System.Globalization.CultureInfo.InvariantCulture), st, en, Repeat.Keine));
                    if (++n >= MaxOccurrences) break;
                }
            }
        }
        return (events, expanded);
    }

    static Repeat? SimpleRepeat(CalendarEvent e, bool hasOverrides)
    {
        if (hasOverrides || e.ExceptionDates.GetAllDates().Any() || e.RecurrenceDates.GetAllDates().Any() || !IsLocalOrFloating(e.DtStart!)) return null;
        if (e.RecurrenceRules.Count == 0) return Repeat.Keine;
        if (e.RecurrenceRules.Count > 1) return null;
        var r = e.RecurrenceRules[0];
        if (r.Interval > 1 || r.Count is not null || r.Until is not null || r.ByDay.Count > 0 || r.ByMonthDay.Count > 0 || r.ByMonth.Count > 0 || r.BySetPosition.Count > 0
            || r.ByYearDay.Count > 0 || r.ByWeekNo.Count > 0 || r.ByHour.Count > 0 || r.ByMinute.Count > 0 || r.BySecond.Count > 0) return null;
        return r.Frequency switch
        {
            Ical.Net.FrequencyType.Daily => Repeat.Taeglich, Ical.Net.FrequencyType.Weekly => Repeat.Woechentlich,
            Ical.Net.FrequencyType.Monthly => Repeat.Monatlich, Ical.Net.FrequencyType.Yearly => Repeat.Jaehrlich, _ => null,
        };
    }

    // Reihe mit fremder Zeitzone bleibt nur dann eine Reihe, wenn die Zeitzone keine Rolle spielt (ganztägig, ohne Zone oder UTC wird aufgelöst).
    static bool IsLocalOrFloating(CalDateTime d) => !d.HasTime || d.TzId is null;

    static DateTime Local(CalDateTime d) => !d.HasTime ? d.Value.Date : d.TzId is null ? d.Value : d.AsUtc.ToLocalTime();

    static DateTime End(CalendarEvent e)
    {
        var st = Local(e.DtStart!);
        if (e.DtEnd is { } de) return Local(de);
        if (e.Duration is { } du) return st + du.ToTimeSpanUnspecified();
        return e.DtStart!.HasTime ? st.AddHours(1) : st.AddDays(1);
    }

    static CalEvent Make(CalendarEvent e, string uid, DateTime start, DateTime end, Repeat rep) =>
        new(uid, start, end, e.Summary ?? "", e.Location ?? "", e.Description ?? "", !e.DtStart!.HasTime, Remind(e), rep);

    static int Remind(CalendarEvent e)
    {
        var a = e.Alarms.FirstOrDefault(x => x.Trigger?.Duration is not null);
        if (a is null) return e.Alarms.Count > 0 ? 0 : 15;
        var m = -(int)Math.Round(a.Trigger!.Duration!.Value.ToTimeSpanUnspecified().TotalMinutes);
        return Math.Max(0, m);
    }
}
