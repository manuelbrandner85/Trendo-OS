using System.Globalization;
using System.Text.RegularExpressions;
using Trendo.Core.Sys;

namespace Trendo.Core.Assist;

/// <summary>Ein Vorschlag, den Trendo macht – ausgeführt wird er erst nach „Ja“. <paramref name="Key"/> ist stabil, damit ein „Nein“ gemerkt wird.</summary>
public sealed record Suggestion(string Key, string Title, string Detail, string Icon = "💡");

/// <summary>Masterprompt 131 / KI-Unterstützung: Programme denken mit. Die Regeln laufen lokal und nachvollziehbar (keine Cloud, nichts wird ohne Ja ausgeführt).</summary>
public static partial class Suggest
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Erste freie Lücke ab <paramref name="from"/> an Werktagen zwischen <paramref name="dayStart"/> und <paramref name="dayEnd"/> Uhr,
    /// in 15-Minuten-Schritten, die mit keinem (nicht ganztägigen) Termin kollidiert; höchstens <paramref name="days"/> Tage voraus.</summary>
    public static DateTime? FreeSlot(IEnumerable<(DateTime Start, DateTime End)> busy, DateTime from, TimeSpan length, int dayStart = 9, int dayEnd = 18, int days = 14)
    {
        var b = busy.OrderBy(x => x.Start).ToList();
        var t = Sys.Calendar.Snap(from.AddMinutes(14)); if (t < from) t = t.AddMinutes(15);
        var stop = from.Date.AddDays(days);
        while (t < stop)
        {
            if (t.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || t.Hour < dayStart) { t = t.Hour < dayStart && t.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) ? t.Date.AddHours(dayStart) : t.Date.AddDays(1).AddHours(dayStart); continue; }
            var end = t + length;
            if (end > t.Date.AddHours(dayEnd)) { t = t.Date.AddDays(1).AddHours(dayStart); continue; }
            var clash = b.FirstOrDefault(x => x.Start < end && x.End > t);
            if (clash == default) return t;
            t = Sys.Calendar.Snap(clash.End); if (t < clash.End) t = t.AddMinutes(15);
        }
        return null;
    }

    /// <summary>Kalender: für offene Aufgaben mit Fälligkeit in den nächsten 7 Tagen (oder überfällig), für die noch kein Arbeitsblock eingetragen ist,
    /// eine freie Stunde vor der Fälligkeit vorschlagen.</summary>
    public static List<(Suggestion S, TodoItem Todo, DateTime Start, DateTime End)> TaskBlocks(IReadOnlyList<TodoItem> todos, IReadOnlyList<(CalEvent Event, DateTime Start, DateTime End)> occ, DateTime now)
    {
        var res = new List<(Suggestion, TodoItem, DateTime, DateTime)>();
        var busy = occ.Where(o => !o.Event.AllDay).Select(o => (o.Start, o.End)).ToList();
        foreach (var t in todos.Where(t => !t.Done && t.Due is { } d && d.Date <= now.Date.AddDays(7)).OrderBy(t => t.Due).ThenBy(t => t.Priority).Take(3))
        {
            if (occ.Any(o => o.Event.Notes.Contains("aufgabe:" + t.Uid))) continue;
            var len = TimeSpan.FromMinutes(t.Priority == 1 ? 90 : 60);
            if (FreeSlot(busy, now, len) is not { } s) continue;
            var due = t.Due!.Value.Date;
            var late = due < now.Date;
            res.Add((new Suggestion("block:" + t.Uid, $"Zeit für „{t.Title}“ einplanen", $"{(late ? "Überfällig seit " + due.ToString("d. MMM", De) : "Fällig " + due.ToString("ddd d. MMM", De))} – frei ist {s:ddd d. MMM, HH:mm}–{s + len:HH:mm}. Als Termin eintragen?", "🗓"), t, s, s + len));
            busy.Add((s, s + len));
        }
        return res;
    }

    [GeneratedRegex(@"\b(?:bis|spätestens|frist|fällig|deadline|bis zum|termin)\b[^\d]{0,20}(\d{1,2})\.\s?(\d{1,2})\.(\d{2,4})?", RegexOptions.IgnoreCase)]
    private static partial Regex DeadlineRx();

    /// <summary>Aufgaben aus E-Mails: Betreff mit Frist („bis 12.11.“, „Frist: 3.12.2026“) oder Bitte („bitte … zurücksenden/überweisen/bestätigen“).</summary>
    public static List<(Suggestion S, string Title, DateTime? Due)> TasksFromMail(IEnumerable<(long Uid, string From, string Subject)> mails, IReadOnlyList<TodoItem> todos, DateTime now)
    {
        var res = new List<(Suggestion, string, DateTime?)>();
        foreach (var (uid, from, subject) in mails)
        {
            var sub = subject.Trim(); if (sub.Length == 0) continue;
            DateTime? due = null;
            if (DeadlineRx().Match(sub) is { Success: true } m)
            {
                var y = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : now.Year; if (y < 100) y += 2000;
                if (DateTime.TryParseExact($"{m.Groups[1].Value}.{m.Groups[2].Value}.{y}", "d.M.yyyy", De, DateTimeStyles.None, out var d)) { if (!m.Groups[3].Success && d < now.Date.AddDays(-30)) d = d.AddYears(1); due = d; }
            }
            var ask = Regex.IsMatch(sub, @"\b(bitte|erinnerung|mahnung|rückmeldung|zurücksenden|unterschreiben|überweisen|bestätigen|antwort erbeten)\b", RegexOptions.IgnoreCase);
            if (due is null && !ask) continue;
            var title = (Regex.Replace(sub, @"^(re|aw|fw|wg):\s*", "", RegexOptions.IgnoreCase)).Trim();
            if (todos.Any(t => string.Equals(t.Title, title, StringComparison.OrdinalIgnoreCase))) continue;
            var who = from.Contains('<') ? from[..from.IndexOf('<')].Trim().Trim('"') : from;
            res.Add((new Suggestion("mail:" + uid, $"Aufgabe aus E-Mail: „{title}“", $"Von {who}{(due is { } dd ? $" · Frist {dd:d. MMM yyyy}" : "")}. In die Aufgaben übernehmen?", "✉"), title, due));
        }
        return res;
    }

    /// <summary>Abläufe: wiederkehrende Unordnung erkennen – viele PDFs im Download-Ordner, viele Bildschirmfotos, Fotos ohne Datum im Namen.</summary>
    public static List<(Suggestion S, string Template)> Workflows(string home, IEnumerable<string> existingTemplates)
    {
        var res = new List<(Suggestion, string)>(); var have = existingTemplates.ToHashSet();
        int Count(string dir, string pattern) { try { return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, pattern).Count() : 0; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; } }
        var dl = Path.Combine(home, "Downloads");
        var pdfs = Count(dl, "*.pdf");
        if (pdfs >= 5 && !have.Contains("pdf")) res.Add((new Suggestion("wf:pdf", "Downloads aufräumen", $"Im Download-Ordner liegen {pdfs} PDFs. Neue PDFs automatisch nach Dokumente\\PDF einsortieren?", "📥"), "pdf"));
        var pics = Path.Combine(home, "Pictures");
        var undated = 0; try { if (Directory.Exists(pics)) undated = Directory.EnumerateFiles(pics, "*.jpg").Count(f => !Regex.IsMatch(Path.GetFileName(f), @"^\d{4}-\d{2}-\d{2}")); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (undated >= 10 && !have.Contains("fotos")) res.Add((new Suggestion("wf:fotos", "Fotos mit Datum versehen", $"{undated} Fotos haben kein Datum im Namen. Datum automatisch voranstellen (nach Freigabe)?", "📅"), "fotos"));
        return res;
    }

    /// <summary>Videoschnitt: Stille-Abschnitte aus der FFmpeg-Ausgabe von <c>silencedetect</c> (silence_start/silence_end, Sekunden).</summary>
    public static List<(double Start, double End)> Silences(string ffmpegStderr)
    {
        var res = new List<(double, double)>(); double? s = null;
        foreach (Match m in Regex.Matches(ffmpegStderr, @"silence_(start|end):\s*(-?[\d.]+)"))
        {
            var v = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (m.Groups[1].Value == "start") s = Math.Max(0, v); else if (s is { } a) { res.Add((a, v)); s = null; }
        }
        return res;
    }

    public static string[] SilenceArgs(string file, double noiseDb = -35, double minSeconds = 1) =>
        ["-hide_banner", "-nostats", "-i", file, "-af", $"silencedetect=noise={noiseDb.ToString(CultureInfo.InvariantCulture)}dB:d={minSeconds.ToString(CultureInfo.InvariantCulture)}", "-f", "null", "-"];
}
