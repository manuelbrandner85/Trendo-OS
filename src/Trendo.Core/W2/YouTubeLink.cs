using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Trendo Musik Y1 (freigegeben 03.11.2026): YouTube-Link → Interpret und Titel über die offizielle oEmbed-Schnittstelle von YouTube.
/// Es wird nichts heruntergeladen (YouTube-Nutzungsbedingungen, § 95a UrhG) und kein Liedtext geholt – nur Titel und Kanal.</summary>
public static partial class YouTubeLink
{
    /// <summary>Video-Adresse in einheitlicher Form – oder null, wenn es kein YouTube-Link ist.</summary>
    public static Uri? Normalize(string input)
    {
        var s = input.Trim();
        if (!s.Contains("://")) s = "https://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http")) return null;
        var host = u.Host.ToLowerInvariant();
        string? id = null;
        if (host is "youtu.be") id = u.AbsolutePath.Trim('/').Split('/')[0];
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com")
        {
            if (u.AbsolutePath == "/watch") id = System.Web.HttpUtility.ParseQueryString(u.Query)["v"];
            else if (u.AbsolutePath.StartsWith("/shorts/") || u.AbsolutePath.StartsWith("/live/")) id = u.AbsolutePath.Split('/')[2];
        }
        return id is { } v && VideoId().IsMatch(v) ? new Uri("https://www.youtube.com/watch?v=" + v) : null;
    }

    public static Uri OEmbedUri(Uri video) => new("https://www.youtube.com/oembed?format=json&url=" + Uri.EscapeDataString(video.AbsoluteUri));

    /// <summary>Titel und Kanal aus der oEmbed-Antwort – oder null.</summary>
    public static (string Title, string Channel)? ParseOEmbed(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var t = d.RootElement.GetProperty("title").GetString() ?? "";
            var c = d.RootElement.TryGetProperty("author_name", out var a) ? a.GetString() ?? "" : "";
            return t.Trim().Length == 0 ? null : (t.Trim(), c.Trim());
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    /// <summary>Interpret und Songtitel aus dem Videotitel („Interpret – Titel (Official Video)“); ohne Trennstrich ist der Kanal der Interpret.</summary>
    public static (string Artist, string Song) Split(string title, string channel)
    {
        var t = title;
        for (var i = 0; i < 4; i++) t = Extras().Replace(t, "").Trim(); // mehrere Zusätze hintereinander
        t = Regex.Replace(t, @"\s+", " ").Trim(' ', '-', '|');
        var m = Separator().Match(t);
        if (m.Success) return (t[..m.Index].Trim(), t[(m.Index + m.Length)..].Trim().Trim('"', '“', '„', '”'));
        var artist = ChannelExtras().Replace(channel, "").Trim();
        return (artist, t.Trim('"', '“', '„', '”'));
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")] private static partial Regex VideoId();
    [GeneratedRegex(@"\s*[\(\[][^\)\]]*(official|video|audio|lyric|lyrics|remaster|remastered|4k|hd|hq|visualizer|clip|musikvideo)[^\)\]]*[\)\]]", RegexOptions.IgnoreCase)] private static partial Regex Extras();
    [GeneratedRegex(@"\s+[-–—|]\s+|\s*�\s*")] private static partial Regex Separator();
    [GeneratedRegex(@"(\s*-\s*Topic|VEVO|\s+Official|\s+Offiziell)$", RegexOptions.IgnoreCase)] private static partial Regex ChannelExtras();
}
