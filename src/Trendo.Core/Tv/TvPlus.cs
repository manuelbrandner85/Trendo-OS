using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Tv;

/// <summary>Stelle, an der ein Film oder eine Folge zuletzt stand (für „Weiterschauen“).</summary>
public sealed record TvResume(string Key, string Title, string Url, double Pos, double Dur, DateTime At, string Kind = "film", string? SeriesId = null, int Season = 0, int Episode = 0, string? Cover = null)
{
    /// <summary>Lohnt sich das Weiterschauen? Mehr als 30 s gesehen und noch nicht am Ende (unter 95 %).</summary>
    public bool Open => Pos > 30 && (Dur <= 0 || Pos < Dur * 0.95);
    public double Progress => Dur > 0 ? Math.Clamp(Pos / Dur, 0, 1) : 0;
}

/// <summary>Aufnahme-Regel: alles aufnehmen, was im Programmführer zum Stichwort passt (z. B. „FC Bayern“), optional nur auf einem Sender.</summary>
public sealed record TvRecordRule(string Id, string Keyword, string? ChannelId = null);

/// <summary>Zusatzinfos zu einem Film vom Anbieter (Xtream get_vod_info).</summary>
public sealed record TvVodInfo(string? Plot, string? Rating, string? Director, string? Cast, string? Genre, string? Duration, string? TrailerUrl, string? Year);

/// <summary>Wunsch 02.11.2026 (TV & Live auf Highend): Weiterschauen, nächste Folge, Startseite mit Reihen, Kinder-Profil, Sprachwahl,
/// doppelte Sender zusammenfassen, Aufnahme-Regeln, Film-Infos. Reine Logik – die Oberfläche steht in MainWindow.TvPlus.</summary>
public static partial class TvPlus
{
    /// <summary>Schlüssel für die gemerkte Stelle: unabhängig vom Passwort in der Adresse (Platzhalter bleibt gleich).</summary>
    public static string ResumeKey(string url)
    {
        var h = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url.Trim()));
        return Convert.ToHexString(h, 0, 10).ToLowerInvariant();
    }

    /// <summary>Nächste Folge: in derselben Staffel die nächste Nummer, sonst die erste der nächsten Staffel.</summary>
    public static TvEpisode? NextEpisode(IReadOnlyList<TvEpisode> eps, int season, int episode) =>
        eps.Where(e => e.Season == season && e.Episode > episode).OrderBy(e => e.Episode).FirstOrDefault()
        ?? eps.Where(e => e.Season > season).OrderBy(e => e.Season).ThenBy(e => e.Episode).FirstOrDefault();

    /// <summary>Knöpfe zur richtigen Zeit: „Intro überspringen“ in den ersten 3 Minuten (nach 5 s), „Nächste Folge“ in den letzten 2 Minuten, Countdown in den letzten 12 Sekunden.</summary>
    public static (bool SkipIntro, bool SkipCredits, bool Countdown) EpisodeMoments(double pos, double dur) =>
        (pos > 5 && pos < 180 && dur > 600, dur > 600 && pos > dur - 120 && pos < dur - 12, dur > 60 && pos >= dur - 12);

    [GeneratedRegex(@"(^|[^a-z])(kinder|kids|kid|family|familie|animation|anime|zeichentrick|cartoon|disney junior|junior|kika|toggo|nick|nickelodeon|super rtl|baby|jugend)([^a-z]|$)", RegexOptions.IgnoreCase)] private static partial Regex KidsWords();

    /// <summary>Für Kinder geeignet (nach Gruppen-/Kategorie- oder Sendername)? Erwachsenen-Inhalte nie.</summary>
    public static bool IsKids(string group, string? name = null) => !TvCommands.IsAdult(group) && !TvCommands.IsAdult(name) && (KidsWords().IsMatch(group) || (name is not null && KidsWords().IsMatch(name)));

    [GeneratedRegex(@"\b(uhd|4k|fhd|1080p?|hd|720p?|sd|576p?|hevc|h265|raw|50fps|backup|\(.*?\)|\[.*?\])\b", RegexOptions.IgnoreCase)] private static partial Regex QualityWords();
    [GeneratedRegex(@"^\s*\|?\s*[A-Za-z]{2,5}\s*(?:[•|:\-–]|\s{2,})\s*")] private static partial Regex Prefix();

    /// <summary>Qualität aus dem Sendernamen: UHD/4K 4 · FHD/1080 3 · HD/720 2 · SD 1 · sonst 2 (unbekannt = wie HD).</summary>
    public static int QualityRank(string name)
    {
        var n = name.ToLowerInvariant();
        if (Regex.IsMatch(n, @"\b(uhd|4k|2160p?)\b")) return 4;
        if (Regex.IsMatch(n, @"\b(fhd|1080p?)\b")) return 3;
        if (Regex.IsMatch(n, @"\b(hd|720p?)\b")) return 2;
        if (Regex.IsMatch(n, @"\b(sd|576p?|480p?)\b")) return 1;
        return 2;
    }

    /// <summary>Name ohne Länderkürzel und Qualität: „DE: ZDF HD“ und „ZDF FHD“ → „zdf“.</summary>
    public static string BaseName(string name) => Regex.Replace(QualityWords().Replace(Prefix().Replace(name, ""), " "), @"[\s\-_.:|•]+", " ").Trim().ToLowerInvariant();

    /// <summary>Doppelte Sender zusammenfassen: je Sendername der beste (höchste Qualität, sonst der erste); die übrigen als Ausweich-Qualitäten.</summary>
    public static (List<TvChannel> Best, Dictionary<string, List<TvChannel>> Alternatives) MergeDuplicates(IEnumerable<TvChannel> channels)
    {
        var best = new List<TvChannel>(); var alt = new Dictionary<string, List<TvChannel>>();
        foreach (var g in channels.GroupBy(c => BaseName(c.Name) is { Length: > 0 } b ? b : c.Id))
        {
            var ordered = g.OrderByDescending(c => QualityRank(c.Name)).ThenBy(c => c.Number).ToList();
            best.Add(ordered[0]);
            if (ordered.Count > 1) alt[ordered[0].Id] = ordered;
        }
        var order = channels.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        best.Sort((a, b) => order[a.Id].CompareTo(order[b.Id]));
        return (best, alt);
    }

    /// <summary>mpv-Einstellungen für die Sprache: Ton in der Wunschsprache, Untertitel in der Wunschsprache nur, wenn der Ton fremd ist.</summary>
    public static string[] LanguageArgs(string lang)
    {
        var l = lang switch { "en" => "en,eng", "tr" => "tr,tur", "fr" => "fr,fra,fre", _ => "de,deu,ger" };
        return [$"--alang={l}", $"--slang={l}", "--subs-with-matching-audio=no", "--sub-auto=fuzzy"];
    }

    /// <summary>Reihen für die Startseite wie bei Streaming-Diensten.</summary>
    public sealed record Row(string Id, string Title, IReadOnlyList<TvVod> Items);

    public static List<Row> Rows(IReadOnlyList<TvVod> all, IReadOnlyList<TvResume> resume, int perRow = 18)
    {
        var rows = new List<Row>();
        double Rate(TvVod v) => double.TryParse((v.Rating ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;
        var popular = all.Where(v => Rate(v) > 0).OrderByDescending(Rate).Take(perRow).ToList();
        if (popular.Count > 0) rows.Add(new("beliebt", "Beliebt", popular));
        var neu = all.Reverse().Take(perRow).ToList(); // Anbieter liefern neue Einträge meist hinten
        if (neu.Count > 0) rows.Add(new("neu", "Neu dazugekommen", neu));
        var last = resume.Where(r => r.Kind == "film").OrderByDescending(r => r.At).FirstOrDefault();
        if (last is not null && all.FirstOrDefault(v => v.Url == last.Url) is { } seen)
        {
            var same = all.Where(v => v.Category == seen.Category && v.Url != seen.Url).OrderByDescending(Rate).Take(perRow).ToList();
            if (same.Count > 0) rows.Add(new("weil", $"Weil du „{seen.Name}“ geschaut hast", same));
        }
        foreach (var g in all.GroupBy(v => v.Category).OrderByDescending(g => g.Count()).Take(4))
            rows.Add(new("kat-" + g.Key, g.Key, g.OrderByDescending(Rate).Take(perRow).ToList()));
        return rows;
    }

    /// <summary>Großes Bild oben: der best bewertete Film mit Bild (oder der erste mit Bild).</summary>
    public static TvVod? Hero(IReadOnlyList<TvVod> all) =>
        all.Where(v => !string.IsNullOrEmpty(v.Cover)).OrderByDescending(v => double.TryParse((v.Rating ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0).FirstOrDefault();

    /// <summary>Passt eine Sendung zu einer Aufnahme-Regel? Stichwort im Titel (ohne Groß/Klein), optional nur auf dem Sender der Regel.</summary>
    public static bool Matches(TvRecordRule rule, string channelId, string title) =>
        (rule.ChannelId is null || rule.ChannelId == channelId) && title.Contains(rule.Keyword.Trim(), StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Film-Infos aus der Antwort von get_vod_info lesen (Trailer als YouTube-Adresse, sonst null).</summary>
    public static TvVodInfo? ParseVodInfo(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("info", out var i) || i.ValueKind != JsonValueKind.Object) return null;
            string? S(string k) => i.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : i.TryGetProperty(k, out var n) && n.ValueKind == JsonValueKind.Number ? n.ToString() : null;
            var t = S("youtube_trailer");
            var trailer = t is null ? null : Regex.IsMatch(t, "^[A-Za-z0-9_-]{6,20}$") ? "https://www.youtube.com/watch?v=" + t : Uri.TryCreate(t, UriKind.Absolute, out var u) && u.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ? u.ToString() : null;
            return new(S("plot") ?? S("description"), S("rating"), S("director"), S("cast") ?? S("actors"), S("genre"), S("duration"), trailer, S("releasedate") ?? S("year"));
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Stream-Nummer aus einer Xtream-Filmadresse („…/movie/u/p/7.mkv“ → 7).</summary>
    public static string? StreamIdOf(string url) => Regex.Match(url, @"/movie/[^/]+/[^/]+/(\d+)") is { Success: true } m ? m.Groups[1].Value : null;
}
