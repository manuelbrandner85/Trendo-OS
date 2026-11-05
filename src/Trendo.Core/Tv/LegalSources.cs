using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Tv;

/// <summary>Legale Quellen für Filme und Serien (Wunsch 05.11.2026, „2, 3, 5 kategorisiert“): Mediatheken der öffentlich-rechtlichen Sender
/// (in Trendo durchsuchen und abspielen), kostenlose Dienste mit Werbung und Abo-Dienste (öffnen nur die offizielle Seite oder App).
/// Keine Piraterie-Quellen, keine fremden Logos – Trendo zeigt Namen und eigene Farbkacheln.</summary>
public static class LegalSources
{
    public enum Kind { Mediathek, Kostenlos, Abo }

    public sealed record Provider(string Id, string Name, Kind Kind, string Url, string Color, string Note, IReadOnlyList<string> Categories, string? Channel = null);

    public static readonly string[] Categories = ["Filme", "Serien", "Dokus", "Kinder", "Nachrichten", "Sport"];

    public static readonly IReadOnlyList<Provider> All =
    [
        // Mediatheken – Inhalte in Trendo suchen und abspielen (über die freie Suche von MediathekViewWeb)
        new("ard", "ARD Mediathek", Kind.Mediathek, "https://www.ardmediathek.de", "#1E3A8A", "Das Erste und die Dritten", ["Filme", "Serien", "Dokus", "Nachrichten", "Sport"], "ARD"),
        new("zdf", "ZDFmediathek", Kind.Mediathek, "https://www.zdf.de", "#F97316", "ZDF, ZDFneo, ZDFinfo", ["Filme", "Serien", "Dokus", "Nachrichten", "Sport"], "ZDF"),
        new("arte", "arte", Kind.Mediathek, "https://www.arte.tv/de/", "#FF4F00", "Kultur, Dokus, Kino", ["Filme", "Serien", "Dokus"], "ARTE.DE"),
        new("3sat", "3sat", Kind.Mediathek, "https://www.3sat.de/mediathek", "#B91C1C", "Kultur und Wissenschaft", ["Dokus", "Filme"], "3Sat"),
        new("phoenix", "phoenix", Kind.Mediathek, "https://www.phoenix.de/mediathek", "#0E7490", "Politik und Ereignisse", ["Dokus", "Nachrichten"], "PHOENIX"),
        new("kika", "KiKA", Kind.Mediathek, "https://www.kika.de", "#16A34A", "Für Kinder", ["Kinder", "Serien"], "KiKA"),
        new("orf", "ORF ON", Kind.Mediathek, "https://on.orf.at", "#DC2626", "Österreich (manches nur in Österreich)", ["Filme", "Serien", "Dokus", "Nachrichten", "Sport"], "ORF"),
        new("srf", "SRF Play", Kind.Mediathek, "https://www.srf.ch/play/tv", "#BE123C", "Schweiz (manches nur in der Schweiz)", ["Filme", "Serien", "Dokus", "Nachrichten"], "SRF"),
        // Kostenlos mit Werbung – offizielle Seite öffnen
        new("pluto", "Pluto TV", Kind.Kostenlos, "https://pluto.tv/de", "#FACC15", "Kostenlose Sender und Filme mit Werbung", ["Filme", "Serien", "Kinder", "Nachrichten"]),
        new("rakuten", "Rakuten TV (kostenlos)", Kind.Kostenlos, "https://www.rakuten.tv/de", "#E11D48", "Kostenloser Bereich mit Werbung", ["Filme", "Serien"]),
        new("plex", "Plex – kostenlose Filme", Kind.Kostenlos, "https://watch.plex.tv/de", "#EAB308", "Filme und Sender kostenlos mit Werbung", ["Filme", "Serien", "Dokus"]),
        new("joyn", "Joyn", Kind.Kostenlos, "https://www.joyn.de", "#7C3AED", "Sender live und Mediathek, Grundangebot kostenlos", ["Serien", "Filme", "Nachrichten", "Sport"]),
        new("rtlfree", "RTL+ (kostenloser Teil)", Kind.Kostenlos, "https://plus.rtl.de", "#2563EB", "Ausgewählte Folgen ohne Abo", ["Serien", "Nachrichten"]),
        // Abo-Dienste – nur die offizielle Seite oder App öffnen, Anmeldung macht der Nutzer selbst
        new("netflix", "Netflix", Kind.Abo, "https://www.netflix.com", "#B91C1C", "Abo – Filme und Serien", ["Filme", "Serien", "Dokus", "Kinder"]),
        new("disney", "Disney+", Kind.Abo, "https://www.disneyplus.com", "#1D4ED8", "Abo – Disney, Pixar, Marvel, Star Wars", ["Filme", "Serien", "Kinder", "Dokus"]),
        new("prime", "Prime Video", Kind.Abo, "https://www.primevideo.com", "#0EA5E9", "Abo – Filme, Serien, Sport", ["Filme", "Serien", "Sport"]),
        new("appletv", "Apple TV+", Kind.Abo, "https://tv.apple.com", "#525252", "Abo – eigene Serien und Filme", ["Serien", "Filme"]),
        new("paramount", "Paramount+", Kind.Abo, "https://www.paramountplus.com/de/", "#1E40AF", "Abo – Filme und Serien", ["Filme", "Serien"]),
        new("wow", "WOW", Kind.Abo, "https://www.wowtv.de", "#0F766E", "Abo – Serien, Filme, Sport", ["Serien", "Filme", "Sport"]),
        new("rtlplus", "RTL+ Premium", Kind.Abo, "https://plus.rtl.de", "#2563EB", "Abo – Serien und Shows", ["Serien", "Filme"]),
        new("magenta", "MagentaTV", Kind.Abo, "https://www.magenta.tv", "#DB2777", "Abo – Fernsehen und Mediathek", ["Filme", "Serien", "Sport", "Nachrichten"]),
        new("dazn", "DAZN", Kind.Abo, "https://www.dazn.com/de-DE", "#171717", "Abo – Sport live", ["Sport"]),
        new("crunchyroll", "Crunchyroll", Kind.Abo, "https://www.crunchyroll.com/de", "#EA580C", "Abo – Anime", ["Serien", "Kinder"]),
    ];

    public static IEnumerable<Provider> Of(Kind kind, string category = "Alle") =>
        All.Where(p => p.Kind == kind && (category == "Alle" || p.Categories.Contains(category)));

    public static string KindLabel(Kind k) => k switch { Kind.Mediathek => "Mediatheken", Kind.Kostenlos => "Kostenlos mit Werbung", _ => "Abo-Dienste" };

    // ---------- Mediathek-Suche (MediathekViewWeb, freie Schnittstelle über die Mediatheken der öffentlich-rechtlichen Sender) ----------
    public const string MediathekApi = "https://mediathekviewweb.de/api/query";

    public sealed record Video(string Channel, string Topic, string Title, string Description, DateTime Date, TimeSpan Duration, string Url, string? UrlHd, string Website);

    /// <summary>Suchauftrag: eigener Suchtext oder Vorgabe je Kategorie; optional nur ein Sender; Filme erst ab 60 Minuten.</summary>
    public static string QueryJson(string query, string category, string? channel, int size = 40)
    {
        var queries = new JsonArray();
        var q = query.Trim();
        var (preset, minMinutes) = category switch
        {
            "Filme" => ("film", 60), "Serien" => ("serie", 15), "Dokus" => ("doku", 20), "Nachrichten" => ("nachrichten", 5), "Sport" => ("sport", 5), _ => ("", 0),
        };
        if (category == "Kinder" && channel is null) channel = "KiKA";
        if (q.Length > 0) queries.Add(new JsonObject { ["fields"] = new JsonArray("title", "topic"), ["query"] = q });
        else if (preset.Length > 0) queries.Add(new JsonObject { ["fields"] = new JsonArray("topic", "title"), ["query"] = preset });
        if (channel is { Length: > 0 }) queries.Add(new JsonObject { ["fields"] = new JsonArray("channel"), ["query"] = channel });
        var o = new JsonObject { ["queries"] = queries, ["sortBy"] = "timestamp", ["sortOrder"] = "desc", ["future"] = false, ["offset"] = 0, ["size"] = Math.Clamp(size, 1, 100) };
        if (minMinutes > 0 && q.Length == 0) o["duration_min"] = minMinutes * 60;
        return o.ToJsonString();
    }

    /// <summary>Antwort lesen; Einträge ohne Video-Adresse oder mit http statt https werden übergangen.</summary>
    public static List<Video> Parse(string json)
    {
        var list = new List<Video>();
        JsonNode? root;
        try { root = JsonNode.Parse(json); } catch (JsonException) { throw new InvalidOperationException("Die Mediathek-Suche hat keine lesbare Antwort geliefert."); }
        if (root?["err"] is JsonNode err && err.GetValueKind() != JsonValueKind.Null) throw new InvalidOperationException("Mediathek-Suche: " + err);
        foreach (var r in (root?["result"]?["results"] as JsonArray ?? []).OfType<JsonObject>())
        {
            string S(string k) => r[k]?.GetValueKind() == JsonValueKind.String ? r[k]!.GetValue<string>() : r[k]?.ToString() ?? "";
            var url = S("url_video"); var hd = S("url_video_hd");
            if (!url.StartsWith("https://", StringComparison.Ordinal) && hd.StartsWith("https://", StringComparison.Ordinal)) url = hd;
            if (!url.StartsWith("https://", StringComparison.Ordinal)) continue;
            long.TryParse(S("timestamp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts);
            long.TryParse(S("duration"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dur);
            list.Add(new Video(S("channel"), S("topic"), S("title"), S("description"), DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime, TimeSpan.FromSeconds(dur), url,
                hd.StartsWith("https://", StringComparison.Ordinal) ? hd : null, S("url_website")));
        }
        return list;
    }

    /// <summary>Suche ausführen (HTTP-Zugang von außen, damit Tests ohne Netz laufen).</summary>
    public static async Task<List<Video>> SearchAsync(HttpClient http, string query, string category, string? channel, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, MediathekApi) { Content = new StringContent(QueryJson(query, category, channel), Encoding.UTF8, "text/plain") };
        req.Headers.UserAgent.ParseAdd("TrendoOS");
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Mediathek-Suche nicht erreichbar ({(int)res.StatusCode}).");
        return Parse(await res.Content.ReadAsStringAsync(ct));
    }

    public static string DurationText(TimeSpan d) => d.TotalHours >= 1 ? $"{(int)d.TotalHours} Std. {d.Minutes} Min." : $"{Math.Max(1, (int)Math.Round(d.TotalMinutes))} Min.";
}
