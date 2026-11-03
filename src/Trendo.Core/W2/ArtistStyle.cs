using System.Text.Json;

namespace Trendo.Core;

/// <summary>Trendo Musik „Im Stil von“ aus dem Web (Wunsch 03.11.2026): Genres, Herkunft und Zeitraum eines Künstlers aus MusicBrainz (CC0),
/// dazu die Kurzbeschreibung aus Wikipedia (CC BY-SA, nur angezeigt, mit Quelle). Daraus werden Stilmerkmale für die Musik-KI – ohne Namen.</summary>
public static class ArtistStyle
{
    public sealed record Artist(string Name, string Mbid, string Type, string? Gender, string? Country, int? Begin, int? End, List<string> Tags);
    public sealed record Wiki(string Title, string Extract, string Url);

    public static Uri SearchUri(string name) => new("https://musicbrainz.org/ws/2/artist/?fmt=json&limit=5&query=" + Uri.EscapeDataString($"artist:\"{name.Trim()}\""));
    public static Uri GenresUri(string mbid) => new($"https://musicbrainz.org/ws/2/artist/{mbid}?inc=genres&fmt=json");
    public static Uri WikiUri(string lang, string title) => new($"https://{lang}.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(title.Trim().Replace(' ', '_')));

    static int? Year(JsonElement ls, string p) => ls.TryGetProperty(p, out var v) && v.GetString() is { Length: >= 4 } s && int.TryParse(s[..4], out var y) ? y : null;

    /// <summary>Bester Treffer der MusicBrainz-Künstlersuche (Trefferwert ab 90) – oder null.</summary>
    public static Artist? ParseSearch(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("artists", out var arr)) return null;
            foreach (var a in arr.EnumerateArray())
            {
                if (a.TryGetProperty("score", out var sc) && sc.GetInt32() < 90) continue;
                var tags = a.TryGetProperty("tags", out var t) ? t.EnumerateArray().OrderByDescending(x => x.GetProperty("count").GetInt32()).Select(x => x.GetProperty("name").GetString() ?? "").ToList() : [];
                var ls = a.TryGetProperty("life-span", out var l) ? l : default;
                // die Suche schreibt das Geschlecht klein („female“), die Abfrage groß – einheitlich groß
                return new(a.GetProperty("name").GetString() ?? "", a.GetProperty("id").GetString() ?? "", a.TryGetProperty("type", out var ty) ? ty.GetString() ?? "" : "",
                    a.TryGetProperty("gender", out var g) && g.GetString() is { Length: > 0 } gs ? char.ToUpperInvariant(gs[0]) + gs[1..] : null, a.TryGetProperty("country", out var c) ? c.GetString() : null,
                    ls.ValueKind == JsonValueKind.Object ? Year(ls, "begin") : null, ls.ValueKind == JsonValueKind.Object ? Year(ls, "end") : null, tags);
            }
            return null;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    /// <summary>Kuratierte Genres des Künstlers (MusicBrainz „genres“), nach Stimmen sortiert.</summary>
    public static List<string> ParseGenres(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            return d.RootElement.TryGetProperty("genres", out var g)
                ? [.. g.EnumerateArray().OrderByDescending(x => x.GetProperty("count").GetInt32()).Select(x => x.GetProperty("name").GetString() ?? "").Where(x => x.Length > 0)] : [];
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return []; }
    }

    /// <summary>Wikipedia-Kurzbeschreibung (nur echte Artikel, keine Begriffsklärung).</summary>
    public static Wiki? ParseWiki(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            if (r.TryGetProperty("type", out var t) && t.GetString() != "standard") return null;
            var ex = r.GetProperty("extract").GetString() ?? "";
            return ex.Length < 20 ? null : new(r.GetProperty("title").GetString() ?? "", ex, r.GetProperty("content_urls").GetProperty("desktop").GetProperty("page").GetString() ?? "");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    // Länder- und Sprachangaben sind keine Klangmerkmale; „seen live“ u. Ä. sind Nutzermarken ohne Aussage
    static readonly HashSet<string> NoStyle = new(StringComparer.OrdinalIgnoreCase)
    { "german", "deutsch", "french", "british", "english", "american", "usa", "uk", "swedish", "italian", "spanish", "austrian", "seen live", "favorites", "favourites" };

    /// <summary>Stilmerkmale (englisch, für die Musik-KI): Genres, Jahrzehnt (nur Bands), Gesang – ohne den Namen und ohne Länder-Marken.</summary>
    public static List<string> Traits(Artist a, IReadOnlyList<string> genres)
    {
        var name = a.Name.ToLowerInvariant();
        var t = new List<string>();
        foreach (var g in genres.Concat(a.Tags))
        {
            var x = g.Trim().ToLowerInvariant();
            if (x.Length < 3 || NoStyle.Contains(x) || x.Contains(name) || name.Contains(x) || x.Any(char.IsDigit) && !x.EndsWith('s')) continue;
            if (!t.Contains(x)) t.Add(x);
            if (t.Count == 6) break;
        }
        if (a.Type == "Group" && a.Begin is int b) t.Add($"{b / 10 * 10}s"); // bei Personen ist „Beginn“ das Geburtsjahr, nicht die Zeit ihrer Musik
        if (a.Type == "Person" && a.Gender is "Female" or "Male") t.Add(a.Gender == "Female" ? "female vocals" : "male vocals");
        else if (a.Type == "Group") t.Add("band");
        return t;
    }
}
