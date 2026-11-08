using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 6 – SYS-09/P16: fremde .ics mit ical.net 5 (MIT). Dateien wie aus Google, Outlook und Thunderbird.</summary>
public class IcsForeignTests
{
    static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0);
    static string Cal(params string[] lines) => "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//DE\r\n" + string.Join("\r\n", lines) + "\r\nEND:VCALENDAR\r\n";
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-icsf-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "ICS fremd +1: Trendos eigene Dateien bleiben beim bisherigen Leser; Regeln mit Ende, Ausnahmen, Einzeländerungen und Zeitzonen gehen an ical.net")]
    public void Routing()
    {
        var own = Ics.Export([new CalEvent("u", Now, Now.AddHours(1), "Sport", Repeat: Repeat.Woechentlich)]);
        Assert.False(IcsForeign.NeedsFullParser(own));
        Assert.True(IcsForeign.NeedsFullParser(Cal("BEGIN:VEVENT", "RRULE:FREQ=WEEKLY;COUNT=3", "END:VEVENT")));
        Assert.True(IcsForeign.NeedsFullParser(Cal("BEGIN:VEVENT", "DTSTART;TZID=Europe/Berlin:20261110T090000", "END:VEVENT")));
        Assert.True(IcsForeign.NeedsFullParser(Cal("BEGIN:VEVENT", "EXDATE:20261117T090000", "END:VEVENT")));
        Assert.True(IcsForeign.NeedsFullParser(Cal("BEGIN:VEVENT", "RECURRENCE-ID:20261117T090000", "END:VEVENT")));
        Assert.True(IcsForeign.NeedsFullParser(Cal("BEGIN:VEVENT", "RRULE:FREQ=WEEKLY;BYD", " AY=TU", "END:VEVENT"))); // gefaltete Zeile
    }

    [Fact(DisplayName = "ICS fremd +2: „10× jeden zweiten Dienstag“ ergibt genau 10 Termine im 14-Tage-Abstand – nicht endlos wöchentlich")]
    public void Count_interval_byday()
    {
        var (ev, series) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:jf@example.org", "DTSTART:20261110T180000", "DTEND:20261110T193000", "SUMMARY:Jugendfeuerwehr", "RRULE:FREQ=WEEKLY;INTERVAL=2;BYDAY=TU;COUNT=10", "END:VEVENT"), Now);
        Assert.Equal(10, ev.Count); Assert.Equal(["jf@example.org"], series);
        Assert.All(ev, e => { Assert.Equal(DayOfWeek.Tuesday, e.Start.DayOfWeek); Assert.Equal(TimeSpan.FromMinutes(90), e.End - e.Start); Assert.Equal(Repeat.Keine, e.Repeat); Assert.Equal("Jugendfeuerwehr", e.Title); });
        Assert.All(ev.Zip(ev.Skip(1)), p => Assert.Equal(14, (p.Second.Start - p.First.Start).TotalDays));
        Assert.Equal(new DateTime(2027, 3, 16, 18, 0, 0), ev[^1].Start);
        Assert.Equal("jf@example.org#20261110T1800", ev[0].Uid);
    }

    [Fact(DisplayName = "ICS fremd +3: Ausnahme (EXDATE) fällt weg, geänderter Einzeltermin (RECURRENCE-ID) mit neuer Zeit und neuem Titel")]
    public void Exdate_and_override()
    {
        var (ev, _) = IcsForeign.Import(Cal(
            "BEGIN:VEVENT", "UID:team@example.org", "DTSTART:20261109T100000", "DTEND:20261109T103000", "SUMMARY:Teamrunde", "RRULE:FREQ=WEEKLY;COUNT=4", "EXDATE:20261116T100000", "END:VEVENT",
            "BEGIN:VEVENT", "UID:team@example.org", "RECURRENCE-ID:20261123T100000", "DTSTART:20261124T140000", "DTEND:20261124T143000", "SUMMARY:Teamrunde (verschoben)", "END:VEVENT"), Now);
        Assert.Equal([new DateTime(2026, 11, 9, 10, 0, 0), new DateTime(2026, 11, 24, 14, 0, 0), new DateTime(2026, 11, 30, 10, 0, 0)], ev.Select(e => e.Start).ToArray());
        Assert.Equal("Teamrunde (verschoben)", ev[1].Title);
    }

    [Fact(DisplayName = "ICS fremd +4: Zeitzone New York 9:00 wird in die Zeit dieses PCs umgerechnet (inkl. Sommerzeit)")]
    public void Time_zone()
    {
        var (ev, _) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:ny@example.org", "DTSTART;TZID=America/New_York:20261201T090000", "DTEND;TZID=America/New_York:20261201T100000", "SUMMARY:Call New York", "END:VEVENT"), Now);
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var expect = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 12, 1, 9, 0, 0), ny).ToLocalTime();
        Assert.Equal(expect, Assert.Single(ev).Start); Assert.Equal(expect.AddHours(1), ev[0].End);
    }

    [Fact(DisplayName = "ICS fremd +5: UNTIL beendet die Reihe; endlose Regel mit Intervall nur im Zeitfenster (1 Jahr zurück, 2 voraus)")]
    public void Until_and_window()
    {
        var (u, _) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:u", "DTSTART:20261101T080000", "DTEND:20261101T090000", "SUMMARY:Kurs", "RRULE:FREQ=DAILY;UNTIL=20261105T235959", "END:VEVENT"), Now);
        Assert.Equal(5, u.Count);
        var (w, _) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:w", "DTSTART:20200101T070000", "DTEND:20200101T071500", "SUMMARY:Gießen", "RRULE:FREQ=DAILY;INTERVAL=2", "END:VEVENT"), Now);
        var (from, to) = IcsForeign.Window(Now);
        Assert.All(w, e => Assert.InRange(e.Start, from, to)); Assert.InRange(w.Count, 540, 550); Assert.True(w.Count < IcsForeign.MaxOccurrences);
    }

    [Fact(DisplayName = "ICS fremd +6: Geburtstag ganztägig jährlich mit Ende, Ort/Beschreibung mit Sonderzeichen, Erinnerung 1 Stunde, DURATION statt DTEND")]
    public void Allday_text_alarm_duration()
    {
        var (b, _) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:geb", "DTSTART;VALUE=DATE:20270115", "DTEND;VALUE=DATE:20270116", "SUMMARY:Geburtstag Erika", "RRULE:FREQ=YEARLY;COUNT=2", "END:VEVENT"), Now);
        Assert.Equal(2, b.Count); Assert.All(b, e => Assert.True(e.AllDay)); Assert.Equal(new DateTime(2028, 1, 15), b[1].Start); Assert.Equal(new DateTime(2028, 1, 16), b[1].End);
        var (d, _) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:d", "DTSTART:20261112T150000Z", "DURATION:PT45M", "SUMMARY:Arzt", "LOCATION:Praxis\\, Hauptstraße 1", "DESCRIPTION:Karte\\nmitbringen",
            "RRULE:FREQ=WEEKLY;COUNT=1", "BEGIN:VALARM", "ACTION:DISPLAY", "TRIGGER:-PT1H", "END:VALARM", "END:VEVENT"), Now);
        var e = Assert.Single(d);
        Assert.Equal(TimeSpan.FromMinutes(45), e.End - e.Start); Assert.Equal(new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc).ToLocalTime(), e.Start);
        Assert.Equal("Praxis, Hauptstraße 1", e.Location); Assert.Equal("Karte\nmitbringen", e.Notes); Assert.Equal(60, e.RemindMin);
    }

    [Fact(DisplayName = "ICS fremd +7: einfache Reihe ohne Ende bleibt eine Reihe (wöchentlich), auch wenn die Datei sonst ical.net braucht")]
    public void Simple_stays_series()
    {
        var (ev, series) = IcsForeign.Import(Cal(
            "BEGIN:VEVENT", "UID:s", "DTSTART:20261110T190000", "DTEND:20261110T200000", "SUMMARY:Chor", "RRULE:FREQ=WEEKLY", "END:VEVENT",
            "BEGIN:VEVENT", "UID:c", "DTSTART:20261111T190000", "DTEND:20261111T200000", "SUMMARY:Kurs", "RRULE:FREQ=WEEKLY;COUNT=2", "END:VEVENT"), Now);
        var chor = Assert.Single(ev, e => e.Uid == "s"); Assert.Equal(Repeat.Woechentlich, chor.Repeat);
        Assert.Equal(["c"], series); Assert.Equal(2, ev.Count(e => e.Uid.StartsWith("c#", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "ICS fremd +8: Kalender übernimmt die Datei; erneutes Einlesen einer geänderten Reihe ersetzt die alten Einzeltermine")]
    public void Calendar_reimport()
    {
        var c = new Calendar(Tmp());
        static string Series(int n) => Cal("BEGIN:VEVENT", "UID:r@example.org", "DTSTART:20261110T090000", "DTEND:20261110T100000", "SUMMARY:Reha", $"RRULE:FREQ=DAILY;COUNT={n}", "END:VEVENT");
        Assert.Equal((5, 0), c.ImportText(Series(5), Now)); Assert.Equal(5, c.All().Count);
        Assert.Equal((3, 0), c.ImportText(Series(3), Now)); Assert.Equal(3, c.All().Count);
        Assert.Equal(3, c.Occurrences(new DateTime(2026, 11, 1), new DateTime(2026, 12, 1)).Count);
    }

    [Fact(DisplayName = "ICS fremd −1: kaputte Datei → verständliche Meldung statt Absturz; Termin ohne Beginn wird übersprungen")]
    public void Broken()
    {
        var ex = Record.Exception(() => IcsForeign.Import("BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nRRULE:FREQ=WEEKLY;COUNT=x\r\nDTSTART:kaputt\r\n", Now));
        if (ex is not null) Assert.IsType<FormatException>(ex);
        var (ev, _) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:o", "SUMMARY:ohne Beginn", "RRULE:FREQ=DAILY;COUNT=2", "END:VEVENT"), Now);
        Assert.Empty(ev);
    }

    [Fact(DisplayName = "ICS fremd −2: Schutz vor Riesenreihen – minütliche Regel höchstens 5000 Termine")]
    public void Cap()
    {
        var (ev, _) = IcsForeign.Import(Cal("BEGIN:VEVENT", "UID:m", "DTSTART:20261108T000000", "DTEND:20261108T000100", "SUMMARY:Takt", "RRULE:FREQ=MINUTELY", "END:VEVENT"), Now);
        Assert.Equal(IcsForeign.MaxOccurrences, ev.Count);
    }
}

/// <summary>Echter Vergleich mit einer unabhängigen Umsetzung (Python icalendar + recurring-ical-events) über Dateien wie aus Outlook, Google, Apple und den Thunderbird-Feiertagen.</summary>
public class IcsForeignReferenzTests
{
    public sealed class ReferenzFactAttribute : FactAttribute { public ReferenzFactAttribute() { if (EchtWerkzeuge.IcsReferenz() is null) Skip = "Übersprungen – keine Referenz (icalendar) im Prüfordner."; } }

    [ReferenzFact(DisplayName = "ICS echt: jeder Termin im Fenster 08.11.2025–08.11.2028 stimmt mit der unabhängigen Python-Referenz überein (Zeit und Titel)")]
    public void Same_as_reference()
    {
        var refFile = EchtWerkzeuge.IcsReferenz()!; var dir = Path.Combine(Path.GetDirectoryName(refFile)!, "dateien");
        var expected = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(refFile))!;
        var now = new DateTime(2026, 11, 8, 12, 0, 0); var (from, to) = IcsForeign.Window(now);
        Assert.NotEmpty(expected);
        foreach (var (name, rows) in expected)
        {
            var root = Path.Combine(Path.GetTempPath(), "trendo-icsref-" + Guid.NewGuid().ToString("N"));
            var cal = new Trendo.Core.Sys.Calendar(root); cal.ImportText(File.ReadAllText(Path.Combine(dir, name)), now);
            var got = cal.Occurrences(from, to).Select(o => (o.Event.AllDay ? o.Start.ToString("yyyy-MM-dd") : o.Start.ToString("yyyy-MM-dd'T'HH:mm")) + " " + o.Event.Title.Trim()).Order(StringComparer.Ordinal).ToList();
            Assert.True(rows.Order(StringComparer.Ordinal).SequenceEqual(got), $"{name}: erwartet {rows.Count}, Trendo {got.Count}. Nur Referenz: {string.Join(" | ", rows.Except(got).Take(5))}. Nur Trendo: {string.Join(" | ", got.Except(rows).Take(5))}");
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }
}
