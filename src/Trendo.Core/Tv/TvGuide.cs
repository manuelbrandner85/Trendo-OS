namespace Trendo.Core.Tv;

/// <summary>Erinnerung an eine Sendung (Sender, Titel, Beginn in UTC).</summary>
public sealed record TvReminder(string ChannelId, string ChannelName, string Title, DateTime Start, bool Shown = false);

/// <summary>Programmführer als Raster (Masterprompt 131): Zeitfenster, Sparten, Suche, Erinnerungen, Überschneidungen von Aufnahmen. Ohne Netz.</summary>
public static class TvGuide
{
    public static readonly string[] Kinds = ["Film", "Serie", "Sport", "Nachrichten", "Kinder", "Doku", "Show", "Sonstiges"];

    /// <summary>Farbe einer Sparte (für die Leiste am Rand jeder Sendung).</summary>
    public static string Color(string kind) => kind switch
    {
        "Film" => "#E5484D", "Serie" => "#7C4DFF", "Sport" => "#22B35E", "Nachrichten" => "#2F86F6",
        "Kinder" => "#F5B83D", "Doku" => "#14B8A6", "Show" => "#EC4899", _ => "#64748B",
    };

    /// <summary>Sparte aus der XMLTV-Kategorie (deutsch oder englisch), sonst aus dem Titel.</summary>
    public static string Kind(TvProgramme p)
    {
        var c = (p.Category + " " + p.Title).ToLowerInvariant();
        bool Has(params string[] w) => w.Any(c.Contains);
        if (Has("nachricht", "news", "tagesschau", "heute journal", "tagesthemen", "wetter", "magazin")) return "Nachrichten";
        if (Has("sport", "fußball", "fussball", "bundesliga", "football", "tennis", "formel", "olymp", "handball", "biathlon")) return "Sport";
        if (Has("kinder", "children", "kids", "zeichentrick", "animation", "cartoon")) return "Kinder";
        if (Has("doku", "documentary", "reportage", "natur", "geschichte", "wissen")) return "Doku";
        if (Has("serie", "series", "episode", "folge", "staffel", "sitcom", "soap", "krimi")) return "Serie";
        if (Has("film", "movie", "spielfilm", "thriller", "komödie", "drama", "western")) return "Film";
        if (Has("show", "quiz", "talk", "unterhaltung", "entertainment", "musik", "music", "comedy")) return "Show";
        return "Sonstiges";
    }

    /// <summary>Beginn des Rasters: <paramref name="dayOffset"/> Tage nach heute; ohne Stunde „jetzt“ (auf halbe Stunde abgerundet) bzw. 6 Uhr an anderen Tagen. Ortszeit → UTC.</summary>
    public static DateTime Start(DateTime nowUtc, int dayOffset, double? hour = null)
    {
        var local = nowUtc.ToLocalTime();
        if (hour is null && dayOffset == 0) { var r = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute < 30 ? 0 : 30, 0, DateTimeKind.Local); return r.ToUniversalTime(); }
        var day = local.Date.AddDays(dayOffset);
        return DateTime.SpecifyKind(day.AddHours(hour ?? 6), DateTimeKind.Local).ToUniversalTime();
    }

    /// <summary>Sendungen eines Senders im Fenster, nach Beginn sortiert.</summary>
    public static List<TvProgramme> Row(IEnumerable<TvProgramme> all, TvChannel c, DateTime from, DateTime to) =>
        c.EpgId is null ? [] : all.Where(p => string.Equals(p.Channel, c.EpgId, StringComparison.OrdinalIgnoreCase) && p.Stop > from && p.Start < to).OrderBy(p => p.Start).ToList();

    /// <summary>Suche in Titel, Beschreibung und Kategorie ab <paramref name="fromUtc"/>; Treffer nach Beginn.</summary>
    public static List<(TvChannel Channel, TvProgramme Show)> Search(IEnumerable<TvProgramme> all, IEnumerable<TvChannel> channels, string query, DateTime fromUtc, int max = 40)
    {
        var q = query.Trim(); if (q.Length < 2) return [];
        var byEpg = channels.Where(c => c.EpgId is not null).GroupBy(c => c.EpgId!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return all.Where(p => p.Stop > fromUtc && (p.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Category.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Where(p => byEpg.ContainsKey(p.Channel)).OrderBy(p => p.Start).Take(max).Select(p => (byEpg[p.Channel], p)).ToList();
    }

    /// <summary>Geplante Aufnahmen, die sich zeitlich mit <paramref name="start"/>–<paramref name="stop"/> überschneiden (auf anderen Sendern oder doppelt).</summary>
    public static List<TvRecording> Overlaps(IEnumerable<TvRecording> recs, DateTime start, DateTime stop, string? exceptId = null) =>
        recs.Where(r => r.Id != exceptId && r.State is "geplant" or "läuft" && r.Start < stop && start < r.Stop).OrderBy(r => r.Start).ToList();

    /// <summary>Ist diese Sendung schon zur Aufnahme vorgemerkt?</summary>
    public static bool Planned(IEnumerable<TvRecording> recs, TvChannel c, TvProgramme p) =>
        recs.Any(r => r.ChannelId == c.Id && r.State is "geplant" or "läuft" && r.Start <= p.Start && r.Stop >= p.Stop);

    /// <summary>Erinnerungen, die jetzt fällig sind (höchstens <paramref name="lead"/> vor Beginn, nicht schon vorbei, noch nicht gezeigt).</summary>
    public static List<TvReminder> Due(IEnumerable<TvReminder> all, DateTime nowUtc, TimeSpan lead) =>
        all.Where(r => !r.Shown && r.Start - lead <= nowUtc && nowUtc < r.Start.AddMinutes(10)).ToList();
}
