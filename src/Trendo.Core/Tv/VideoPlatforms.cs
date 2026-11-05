using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Tv;

/// <summary>Video-Plattformen wie YouTube als eigene Kategorie in Trendo TV (Wunsch 05.11.2026, „YouTube und weitere, besser oder ähnlich“).
/// Legal und ohne Schlüssel: PeerTube (freie, offene YouTube-Alternative – Suche über alle Instanzen per SepiaSearch, Wiedergabe direkt im Trendo-Player),
/// Internet Archive (gemeinfreie Filme, Wiedergabe in Trendo), Dailymotion (öffentliche Suche, Wiedergabe auf der offiziellen Seite).
/// YouTube, Vimeo, Twitch und TED: Trendo öffnet die offizielle Seite bzw. den offiziellen Einbett-Player (youtube-nocookie) –
/// kein Herunterladen, keine fremden Zwischen-Dienste, keine Werbe-Umgehung.</summary>
public static class VideoPlatforms
{
    /// <summary>Wie eine Plattform in Trendo läuft: InTrendo = Suche und Wiedergabe im Trendo-Player; Suche = Suche in Trendo, Wiedergabe auf der Original-Seite; Original = Suche und Wiedergabe auf der Original-Seite.</summary>
    public enum Mode { InTrendo, Suche, Original }

    public sealed record Platform(string Id, string Name, Mode Mode, string Color, string Note, string Home);

    public static readonly IReadOnlyList<Platform> All =
    [
        new("youtube", "YouTube", Mode.Original, "#DC2626", "Die größte Video-Plattform – Suche und Wiedergabe im offiziellen Player", "https://www.youtube.com"),
        new("peertube", "PeerTube", Mode.InTrendo, "#F1680D", "Freie YouTube-Alternative ohne Werbung und Tracking – läuft direkt in Trendo", "https://sepiasearch.org"),
        new("archive", "Internet Archive", Mode.InTrendo, "#64748B", "Gemeinfreie Filme, Klassiker und Dokus – läuft direkt in Trendo", "https://archive.org/details/movies"),
        new("dailymotion", "Dailymotion", Mode.Suche, "#2563EB", "Europäische Video-Plattform – Suche in Trendo, Wiedergabe auf Dailymotion", "https://www.dailymotion.com"),
        new("vimeo", "Vimeo", Mode.Original, "#0EA5E9", "Kurzfilme und Videos in hoher Qualität", "https://vimeo.com"),
        new("twitch", "Twitch", Mode.Original, "#7C3AED", "Live-Streams – Gaming, Musik, Talk", "https://www.twitch.tv"),
        new("ted", "TED", Mode.Original, "#E11D48", "Vorträge über Ideen, Wissen und Wissenschaft", "https://www.ted.com/talks"),
    ];

    public static Platform Get(string id) => All.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Unbekannte Plattform: " + id);

    /// <summary>Kategorien mit Suchbegriffen (wie die Leiste oben bei YouTube).</summary>
    public static readonly (string Id, string Label, string Query)[] Categories =
    [
        ("alle", "Alle", ""), ("musik", "🎵 Musik", "musik"), ("gaming", "🎮 Gaming", "gaming"), ("wissen", "🔬 Wissen", "wissenschaft"),
        ("nachrichten", "📰 Nachrichten", "nachrichten"), ("sport", "⚽ Sport", "sport"), ("kinder", "🧸 Kinder", "kinder"),
        ("filme", "🎬 Filme", "film"), ("comedy", "😂 Comedy", "comedy"), ("kochen", "🍳 Kochen", "rezept"), ("technik", "💻 Technik", "technik"),
    ];

    public static string CategoryQuery(string category) => Categories.FirstOrDefault(c => c.Id == category).Query ?? "";

    public sealed record Video(string Platform, string Id, string Title, string Author, string Description, TimeSpan Duration, DateTime? Published, long Views,
        string? Thumbnail, string PageUrl, string? PlayUrl = null, string? ResolveUrl = null);

    static string E(string s) => Uri.EscapeDataString(s);

    // ---------- Adressen ----------
    /// <summary>Offizielle Such-Seite der Plattform (für „auf der Original-Seite suchen“).</summary>
    public static string SearchPage(string platform, string query)
    {
        var q = query.Trim();
        return platform switch
        {
            "youtube" => q.Length == 0 ? "https://www.youtube.com" : "https://www.youtube.com/results?search_query=" + E(q),
            "vimeo" => q.Length == 0 ? "https://vimeo.com/watch" : "https://vimeo.com/search?q=" + E(q),
            "twitch" => q.Length == 0 ? "https://www.twitch.tv/directory" : "https://www.twitch.tv/search?term=" + E(q),
            "ted" => q.Length == 0 ? "https://www.ted.com/talks" : "https://www.ted.com/search?q=" + E(q),
            "dailymotion" => q.Length == 0 ? "https://www.dailymotion.com" : "https://www.dailymotion.com/search/" + E(q),
            "peertube" => "https://sepiasearch.org/search?search=" + E(q),
            "archive" => "https://archive.org/search?query=" + E(q) + "&sin=&and%5B%5D=mediatype%3A%22movies%22",
            _ => Get(platform).Home,
        };
    }

    public static string PeerTubeSearchUrl(string query, int count = 24) =>
        $"https://sepiasearch.org/api/v1/search/videos?search={E(query.Trim().Length == 0 ? "trending" : query.Trim())}&count={Math.Clamp(count, 1, 50)}&start=0&nsfw=false&sort=-match&isLive=false";

    public static string DailymotionSearchUrl(string query, int count = 24) =>
        $"https://api.dailymotion.com/videos?fields=id,title,description,duration,created_time,views_total,thumbnail_360_url,url,owner.screenname&limit={Math.Clamp(count, 1, 50)}&family_filter=true"
        + (query.Trim().Length == 0 ? "&sort=trending" : "&sort=relevance&search=" + E(query.Trim()));

    public static string ArchiveSearchUrl(string query, int count = 24)
    {
        var q = query.Trim().Length == 0 ? "collection:(feature_films)" : $"({query.Trim()}) AND collection:(feature_films OR prelinger OR classic_tv OR documentaries)";
        return "https://archive.org/advancedsearch.php?q=" + E(q + " AND mediatype:(movies)") + "&fl%5B%5D=identifier&fl%5B%5D=title&fl%5B%5D=creator&fl%5B%5D=description&fl%5B%5D=date&fl%5B%5D=downloads"
            + $"&sort%5B%5D=downloads+desc&rows={Math.Clamp(count, 1, 50)}&page=1&output=json";
    }

    /// <summary>YouTube-Link (watch, youtu.be, shorts, embed) → Video-ID; null, wenn es kein YouTube-Link ist.</summary>
    public static string? YouTubeId(string link)
    {
        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http")) return null;
        var host = u.Host.ToLowerInvariant(); string? id = null;
        if (host is "youtu.be") id = u.AbsolutePath.Trim('/').Split('/')[0];
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "www.youtube-nocookie.com")
        {
            var seg = u.AbsolutePath.Trim('/').Split('/');
            if (seg[0] == "watch") id = System.Web.HttpUtility.ParseQueryString(u.Query)["v"];
            else if (seg.Length > 1 && seg[0] is "shorts" or "embed" or "live") id = seg[1];
        }
        return id is { Length: 11 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? id : null;
    }

    /// <summary>Offizieller Einbett-Player von YouTube im datensparsamen Modus.</summary>
    public static string YouTubeEmbed(string id) => $"https://www.youtube-nocookie.com/embed/{id}?autoplay=1&rel=0";

    // ---------- Antworten lesen ----------
    static JsonNode? Root(string json, string what)
    {
        try { return JsonNode.Parse(json); } catch (JsonException) { throw new InvalidOperationException(what + " hat keine lesbare Antwort geliefert."); }
    }
    static string S(JsonNode? n) => n is null ? "" : n.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : n.GetValueKind() is JsonValueKind.Array ? string.Join(", ", n.AsArray().Select(x => x?.ToString())) : n.ToString();
    static long L(JsonNode? n) => long.TryParse(S(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    static string? Https(string s) => s.StartsWith("https://", StringComparison.Ordinal) ? s : null;
    static DateTime? Date(string s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d.ToLocalTime() : null;
    static string Plain(string html) => System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")).Trim();

    /// <summary>SepiaSearch (PeerTube): Wiedergabe-Adresse wird beim Abspielen über die Instanz geholt.</summary>
    public static List<Video> ParsePeerTube(string json)
    {
        var list = new List<Video>();
        foreach (var r in (Root(json, "Die PeerTube-Suche")?["data"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var page = Https(S(r["url"])); var uuid = S(r["uuid"]); var host = S(r["account"]?["host"]);
            if (page is null || uuid.Length == 0 || host.Length == 0 || r["isLive"]?.GetValueKind() == JsonValueKind.True) continue;
            var instance = new Uri(page).Host; // die Instanz, auf der das Video liegt
            list.Add(new Video("peertube", uuid, S(r["name"]), S(r["channel"]?["displayName"]) is { Length: > 0 } ch ? ch : S(r["account"]?["displayName"]), Plain(S(r["description"])),
                TimeSpan.FromSeconds(L(r["duration"])), Date(S(r["publishedAt"])), L(r["views"]), Https(S(r["thumbnailUrl"])), page, ResolveUrl: $"https://{instance}/api/v1/videos/{uuid}"));
        }
        return list;
    }

    /// <summary>PeerTube-Video: beste Datei bis 1080p (MP4), sonst HLS-Wiedergabeliste.</summary>
    public static string? PeerTubePlayUrl(string json)
    {
        var r = Root(json, "PeerTube");
        var files = (r?["files"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        foreach (var pl in (r?["streamingPlaylists"] as JsonArray ?? []).OfType<JsonObject>()) files.AddRange((pl["files"] as JsonArray ?? []).OfType<JsonObject>());
        var best = files.Select(f => (Url: Https(S(f["fileUrl"])), Res: (int)L(f["resolution"]?["id"]))).Where(f => f.Url is not null && f.Res is > 0 and <= 1080).OrderByDescending(f => f.Res).FirstOrDefault();
        if (best.Url is not null) return best.Url;
        return (r?["streamingPlaylists"] as JsonArray ?? []).OfType<JsonObject>().Select(p => Https(S(p["playlistUrl"]))).FirstOrDefault(u => u is not null);
    }

    public static List<Video> ParseDailymotion(string json)
    {
        var list = new List<Video>();
        foreach (var r in (Root(json, "Die Dailymotion-Suche")?["list"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var page = Https(S(r["url"])); if (page is null) continue;
            var ts = L(r["created_time"]);
            list.Add(new Video("dailymotion", S(r["id"]), S(r["title"]), S(r["owner.screenname"]), Plain(S(r["description"])), TimeSpan.FromSeconds(L(r["duration"])),
                ts > 0 ? DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime : null, L(r["views_total"]), Https(S(r["thumbnail_360_url"])), page));
        }
        return list;
    }

    public static List<Video> ParseArchive(string json)
    {
        var list = new List<Video>();
        foreach (var r in (Root(json, "Die Archiv-Suche")?["response"]?["docs"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var id = S(r["identifier"]); if (id.Length == 0 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) continue;
            var desc = Plain(S(r["description"])); if (desc.Length > 400) desc = desc[..400] + " …";
            list.Add(new Video("archive", id, S(r["title"]), S(r["creator"]), desc, TimeSpan.Zero, Date(S(r["date"])), L(r["downloads"]),
                $"https://archive.org/services/img/{id}", $"https://archive.org/details/{id}", ResolveUrl: $"https://archive.org/metadata/{id}"));
        }
        return list;
    }

    /// <summary>Archiv-Eintrag: abspielbare Datei – bevorzugt „h.264“-MP4, sonst eine andere MP4, sonst Ogg-Video; null, wenn das Archiv keine abspielbare Datei hat.</summary>
    public static string? ArchivePlayUrl(string json)
    {
        var r = Root(json, "Das Archiv"); var id = S(r?["metadata"]?["identifier"]); if (id.Length == 0) return null;
        var mp4 = (r?["files"] as JsonArray ?? []).OfType<JsonObject>().Select(f => (Name: S(f["name"]), Format: S(f["format"])))
            .Where(f => (f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".ogv", StringComparison.OrdinalIgnoreCase)) && !f.Name.Contains("..")).ToList();
        var pick = mp4.FirstOrDefault(f => f.Format.Contains("h.264", StringComparison.OrdinalIgnoreCase) && !f.Format.Contains("HD", StringComparison.Ordinal)).Name
            ?? mp4.FirstOrDefault(f => f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)).Name ?? mp4.FirstOrDefault().Name;
        return pick is null ? null : $"https://archive.org/download/{E(id)}/{string.Join('/', pick.Split('/').Select(E))}";
    }

    // ---------- Suche ----------
    static async Task<string> Get(HttpClient http, string url, string what, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url); req.Headers.UserAgent.ParseAdd("TrendoOS");
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"{what} nicht erreichbar ({(int)res.StatusCode}).");
        return await res.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Suche auf einer Plattform mit Suche in Trendo; Kategorie ergänzt den Suchbegriff.</summary>
    public static async Task<List<Video>> SearchAsync(HttpClient http, string platform, string query, string category = "alle", CancellationToken ct = default)
    {
        var q = query.Trim().Length > 0 ? query.Trim() : CategoryQuery(category);
        return platform switch
        {
            "peertube" => ParsePeerTube(await Get(http, PeerTubeSearchUrl(q), "Die PeerTube-Suche", ct)),
            "dailymotion" => ParseDailymotion(await Get(http, DailymotionSearchUrl(q), "Die Dailymotion-Suche", ct)),
            "archive" => ParseArchive(await Get(http, ArchiveSearchUrl(q), "Die Archiv-Suche", ct)),
            _ => throw new InvalidOperationException($"{Get(platform).Name} wird auf der offiziellen Seite durchsucht."),
        };
    }

    /// <summary>Wiedergabe-Adresse für den Trendo-Player (PeerTube, Archiv); null = auf der Original-Seite ansehen.</summary>
    public static async Task<string?> PlayUrlAsync(HttpClient http, Video v, CancellationToken ct = default)
    {
        if (v.PlayUrl is not null) return v.PlayUrl;
        if (v.ResolveUrl is null) return null;
        var json = await Get(http, v.ResolveUrl, "Das Video", ct);
        return v.Platform switch { "peertube" => PeerTubePlayUrl(json), "archive" => ArchivePlayUrl(json), _ => null };
    }

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static string ViewsText(long v) => v >= 1_000_000 ? (v / 1_000_000.0).ToString("0.#", De) + " Mio. Aufrufe" : v >= 1000 ? (v / 1000.0).ToString("0.#", De) + " Tsd. Aufrufe" : v > 0 ? $"{v} Aufrufe" : "";
}
