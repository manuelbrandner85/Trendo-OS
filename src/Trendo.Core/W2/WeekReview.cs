using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>Vorschlag 32 (freigegeben 02.11.2026): Wochenrückblick im Wissen – was diese Woche dazukam, was gelernt wurde, was noch offen ist.
/// Rein aus den eigenen Daten auf diesem PC (Wissensseiten und Karteikarten); die KI-Zusammenfassung ist ein Zusatz und wird nur auf Klick erstellt.</summary>
public static class WeekReview
{
    public sealed record Day(DateTime Date, int Reviews);
    public sealed record Result(DateTime From, DateTime To, int Week, IReadOnlyList<KnowledgeItem> NewPages, IReadOnlyList<string> OpenLinks,
        IReadOnlyList<Day> Days, int Reviews, int FirstTimeCards, IReadOnlyList<Flashcard> Hard, int DueNow, IReadOnlyList<string> Topics)
    {
        public int ActiveDays => Days.Count(d => d.Reviews > 0);
        public bool Empty => NewPages.Count == 0 && Reviews == 0;
    }

    public static int IsoWeek(DateTime d) => ISOWeek.GetWeekOfYear(d);

    /// <summary>Rückblick über die 7 Tage bis einschließlich <paramref name="today"/>.</summary>
    public static Result Build(IReadOnlyList<KnowledgeItem> items, CardStore cards, DateTime today)
    {
        var to = today.Date; var from = to.AddDays(-6);
        var pages = items.Where(i => i.Created.LocalDateTime.Date >= from && i.Created.LocalDateTime.Date <= to && i.Source != "Rückmeldung")
            .OrderByDescending(i => i.Created).ToList();
        var titles = new HashSet<string>(items.Select(i => i.Title.Trim()), StringComparer.CurrentCultureIgnoreCase);
        var open = pages.SelectMany(p => KnowledgeStore.Links(p.Text)).Where(l => !titles.Contains(l.Trim())).Distinct(StringComparer.CurrentCultureIgnoreCase).Take(12).ToList();
        var days = Enumerable.Range(0, 7).Select(i => from.AddDays(i)).Select(d => new Day(d, cards.ReviewLog.GetValueOrDefault(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))).ToList();
        var first = cards.Cards.Count(c => c.FirstReviewed is { } f && f.Date >= from && f.Date <= to);
        var hard = cards.Cards.Where(c => c.Lapses >= 2 || (c.Ease < 2.0 && c.Reps > 0)).OrderByDescending(c => c.Lapses).ThenBy(c => c.Ease).Take(8).ToList();
        var due = cards.Cards.Count(c => c.Due.Date <= to);
        // Themen: häufige Schlagwörter und Wörter der neuen Seiten (ohne Füllwörter)
        var topics = pages.SelectMany(p => p.Tags).Concat(pages.SelectMany(p => Words(p.Title + " " + p.Text)))
            .GroupBy(w => w.ToLowerInvariant()).Where(g => g.Count() >= 2 || pages.Any(p => p.Tags.Contains(g.Key, StringComparer.OrdinalIgnoreCase)))
            .OrderByDescending(g => g.Count()).Take(8).Select(g => g.Key).ToList();
        return new(from, to, IsoWeek(to), pages, open, days, days.Sum(d => d.Reviews), first, hard, due, topics);
    }

    static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase) { "einen", "einer", "eines", "einem", "diese", "dieser", "dieses", "diesem", "nicht", "werden", "wurde", "wurden", "haben", "hatte",
        "können", "kann", "durch", "unter", "zwischen", "immer", "schon", "noch", "sowie", "damit", "dabei", "dann", "denn", "wenn", "weil", "auch", "aber", "oder", "sind", "seine", "ihrer", "ihren", "welche", "mehr", "sehr", "etwas", "alles", "allen" };

    /// <summary>Wörter ab 5 Buchstaben, Umlaute bleiben, ohne Füllwörter und [[Verweis-Klammern]].</summary>
    static IEnumerable<string> Words(string s) =>
        System.Text.RegularExpressions.Regex.Matches(s, @"\p{L}{5,}").Select(m => m.Value).Where(w => !Filler.Contains(w));

    /// <summary>Rückblick als Wissensseite (Markdown, mit [[Verweisen]] auf die neuen Seiten).</summary>
    public static string Markdown(Result r, string? summary = null)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder();
        sb.AppendLine($"# Wochenrückblick KW {r.Week} ({r.From.ToString("d. MMMM", de)} – {r.To.ToString("d. MMMM yyyy", de)})").AppendLine();
        if (summary is { Length: > 0 }) sb.AppendLine("## Kurz gesagt").AppendLine(summary.Trim()).AppendLine();
        sb.AppendLine($"## Neu im Wissen ({r.NewPages.Count})");
        if (r.NewPages.Count == 0) sb.AppendLine("Diese Woche keine neuen Seiten.");
        foreach (var p in r.NewPages) sb.AppendLine($"- [[{p.Title}]] – {p.Created.LocalDateTime.ToString("ddd, d.M.", de)}");
        sb.AppendLine().AppendLine("## Gelernt");
        sb.AppendLine($"{r.Reviews} Karten wiederholt an {r.ActiveDays} von 7 Tagen, {r.FirstTimeCards} zum ersten Mal. Jetzt fällig: {r.DueNow}.");
        if (r.Hard.Count > 0) { sb.AppendLine().AppendLine("Noch schwierig:"); foreach (var c in r.Hard) sb.AppendLine($"- {c.Front} ({c.Deck})"); }
        if (r.OpenLinks.Count > 0) { sb.AppendLine().AppendLine("## Noch offen"); foreach (var l in r.OpenLinks) sb.AppendLine($"- [[{l}]] – Seite fehlt noch"); }
        if (r.Topics.Count > 0) sb.AppendLine().AppendLine("Themen der Woche: " + string.Join(", ", r.Topics));
        return sb.ToString();
    }

    /// <summary>Auftrag an die KI: kurze, ehrliche Zusammenfassung nur aus diesen Seiten (keine Erfindungen).</summary>
    public static string Prompt(Result r) =>
        "Fasse meine Lernwoche in 3–5 Sätzen auf Deutsch zusammen: Was habe ich Neues festgehalten, was hängt zusammen, was sollte ich als Nächstes vertiefen? " +
        "Nutze nur die folgenden Notizen, erfinde nichts dazu.\n\n" +
        string.Join("\n\n", r.NewPages.Take(12).Select(p => $"### {p.Title}\n{(p.Text.Length > 700 ? p.Text[..700] + " …" : p.Text)}")) +
        (r.Hard.Count > 0 ? "\n\nSchwierige Karteikarten: " + string.Join("; ", r.Hard.Select(c => c.Front)) : "") +
        (r.OpenLinks.Count > 0 ? "\n\nNoch offene Themen: " + string.Join(", ", r.OpenLinks) : "");
}
