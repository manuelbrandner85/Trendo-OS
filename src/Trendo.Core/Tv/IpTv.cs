using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace Trendo.Core.Tv;

/// <summary>Wunsch 27.10.2026: Studio „TV & Live“ – IPTV schauen, frei und vollständig, eigene Trendo-Gestaltung.
/// Kern ohne Netz (QA-019): Wiedergabelisten (M3U/M3U Plus, Xtream-Zugang), Programmführer (XMLTV), Sender, Favoriten, Aufnahmen, Catch-up.
/// Trendo bringt keine Sender mit – nur eigene, legale Quellen des Nutzers.</summary>
/// <param name="Kind">„live“ (Sender), „film“ oder „serie“ – Filme und Serien stehen in eigenen Bereichen.</param>
public sealed record TvChannel(string Id, string Name, string Url, string Group, string? Logo, string? EpgId, int Number, string? Catchup = null, string? CatchupSource = null, int CatchupDays = 0, string Kind = "live");

/// <summary>Film (Video auf Abruf).</summary>
public sealed record TvVod(string Id, string Name, string Category, string Url, string? Cover = null, string? Plot = null, string? Year = null, string? Rating = null);
/// <summary>Serie; Folgen kommen aus der Liste oder (Xtream) auf Abruf vom Anbieter.</summary>
public sealed record TvSeries(string Id, string Name, string Category, string? Cover = null, string? Plot = null, string? Year = null, string? Rating = null, string? XtreamId = null);
public sealed record TvEpisode(string SeriesId, int Season, int Episode, string Title, string Url, string? Plot = null, int Seconds = 0);

public sealed record TvProgramme(string Channel, DateTime Start, DateTime Stop, string Title, string Description = "", string Category = "")
{
    public bool IsOn(DateTime now) => Start <= now && now < Stop;
    public double Progress(DateTime now) => Math.Clamp((now - Start).TotalSeconds / Math.Max(1, (Stop - Start).TotalSeconds), 0, 1);
}

public sealed record TvSource(string Id, string Name, string Kind, string Url, string? EpgUrl = null, string? User = null, DateTime Added = default);
public sealed record TvRecording(string Id, string ChannelId, string ChannelName, string Title, DateTime Start, DateTime Stop, string State = "geplant", string? File = null);

public static partial class M3u
{
    [GeneratedRegex("([a-zA-Z0-9_-]+)=\"([^\"]*)\"")] private static partial Regex Attr();

    /// <summary>Liest M3U / M3U Plus (#EXTINF mit tvg-id, tvg-name, tvg-logo, group-title, catchup …). Liefert Sender und die EPG-Adresse aus dem Kopf (x-tvg-url).</summary>
    public static (List<TvChannel> Channels, string? EpgUrl) Parse(string text, string sourceId = "q")
    {
        var list = new List<TvChannel>();
        string? epg = null;
        Dictionary<string, string>? attrs = null; string? name = null; string? grp = null;
        var n = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0) continue;
            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                var h = Attr().Matches(line).ToDictionary(m => m.Groups[1].Value.ToLowerInvariant(), m => m.Groups[2].Value);
                epg = h.GetValueOrDefault("x-tvg-url") ?? h.GetValueOrDefault("url-tvg");
                if (epg?.Contains(',') == true) epg = epg.Split(',')[0].Trim();
                continue;
            }
            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                attrs = Attr().Matches(line).ToDictionary(m => m.Groups[1].Value.ToLowerInvariant(), m => m.Groups[2].Value);
                var comma = FirstCommaOutsideQuotes(line);
                name = comma >= 0 ? line[(comma + 1)..].Trim() : attrs.GetValueOrDefault("tvg-name");
                continue;
            }
            if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase)) { grp = line[8..].Trim(); continue; }
            if (line.StartsWith('#')) continue;
            if (!Uri.TryCreate(line, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https" or "rtmp" or "rtsp" or "udp" or "rtp")) { attrs = null; continue; }
            attrs ??= [];
            var title = string.IsNullOrWhiteSpace(name) ? attrs.GetValueOrDefault("tvg-name") ?? $"Sender {n + 1}" : name!;
            var number = int.TryParse(attrs.GetValueOrDefault("tvg-chno"), out var no) ? no : ++n;
            if (number > n) n = number;
            var days = int.TryParse(attrs.GetValueOrDefault("catchup-days") ?? attrs.GetValueOrDefault("tvg-rec"), out var cd) ? cd : 0;
            var group = attrs.GetValueOrDefault("group-title") ?? grp ?? "Ohne Gruppe";
            list.Add(new TvChannel($"{sourceId}:{list.Count}", title, line, group,
                NullIfEmpty(attrs.GetValueOrDefault("tvg-logo")), NullIfEmpty(attrs.GetValueOrDefault("tvg-id")), number,
                NullIfEmpty(attrs.GetValueOrDefault("catchup")), NullIfEmpty(attrs.GetValueOrDefault("catchup-source")), days, Classify(line, title, group)));
            attrs = null; name = null; grp = null;
        }
        return (list, epg);
    }

    static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    [GeneratedRegex(@"\.(mp4|mkv|avi|mov|wmv|m4v|webm|mpg)(\?|$)", RegexOptions.IgnoreCase)] private static partial Regex VideoFile();
    [GeneratedRegex(@"^(?<name>.*?)[\s._-]*S(?<s>\d{1,2})\s*[._-]?\s*E(?<e>\d{1,3})", RegexOptions.IgnoreCase)] private static partial Regex SeasonEpisode();
    [GeneratedRegex(@"^(vod|filme?|movies?|kino|cinema)\b", RegexOptions.IgnoreCase)] private static partial Regex FilmGroup();
    [GeneratedRegex(@"^(serien?|series|tv[ -]?shows?)\b", RegexOptions.IgnoreCase)] private static partial Regex SeriesGroup();

    /// <summary>Sender, Film oder Serie? Nach Adresse (/movie/, /series/, Videodatei), Name (S01E02) und Gruppe (VOD, Filme, Serien).</summary>
    public static string Classify(string url, string name, string group)
    {
        var u = url.ToLowerInvariant();
        if (u.Contains("/series/")) return "serie";
        if (u.Contains("/movie/")) return "film";
        var seriesName = SeasonEpisode().IsMatch(name) || SeriesGroup().IsMatch(group);
        if (VideoFile().IsMatch(u)) return seriesName ? "serie" : "film";
        if (FilmGroup().IsMatch(group)) return "film";
        if (SeriesGroup().IsMatch(group) && SeasonEpisode().IsMatch(name)) return "serie";
        return "live";
    }

    /// <summary>„Mein Krimi S02 E05“ → („Mein Krimi“, 2, 5). Ohne Angabe: Staffel 1, Folge in Reihenfolge.</summary>
    public static (string Series, int Season, int Episode) SplitEpisode(string name)
    {
        var m = SeasonEpisode().Match(name);
        return m.Success ? (m.Groups["name"].Value.Trim(' ', '-', '.', '_', '|'), int.Parse(m.Groups["s"].Value), int.Parse(m.Groups["e"].Value)) : (name.Trim(), 1, 0);
    }

    /// <summary>Name steht nach dem ersten Komma außerhalb der Anführungszeichen (Namen dürfen selbst Kommas enthalten).</summary>
    static int FirstCommaOutsideQuotes(string line)
    {
        var q = false;
        for (var i = 0; i < line.Length; i++) { if (line[i] == '"') q = !q; else if (line[i] == ',' && !q) return i; }
        return -1;
    }

    /// <summary>Exportiert Sender als M3U Plus (z. B. für Favoriten-Listen).</summary>
    public static string Write(IEnumerable<TvChannel> channels, string? epgUrl = null)
    {
        var sb = new System.Text.StringBuilder(epgUrl is null ? "#EXTM3U\n" : $"#EXTM3U x-tvg-url=\"{epgUrl}\"\n");
        foreach (var c in channels)
            sb.Append($"#EXTINF:-1 tvg-id=\"{c.EpgId}\" tvg-name=\"{c.Name}\" tvg-logo=\"{c.Logo}\" tvg-chno=\"{c.Number}\" group-title=\"{c.Group}\",{c.Name}\n{c.Url}\n");
        return sb.ToString();
    }
}

/// <summary>Xtream-Zugang (Server, Benutzer, Passwort) → Standard-Adressen für Wiedergabeliste und Programmführer.</summary>
public static class Xtream
{
    public static string Normalize(string server)
    {
        server = server.Trim().TrimEnd('/');
        if (!server.StartsWith("http", StringComparison.OrdinalIgnoreCase)) server = "http://" + server;
        return server;
    }

    public static string PlaylistUrl(string server, string user, string pass) =>
        $"{Normalize(server)}/get.php?username={Uri.EscapeDataString(user)}&password={Uri.EscapeDataString(pass)}&type=m3u_plus&output=ts";

    /// <summary>Zugang aus einer get.php-Adresse zurückgewinnen (Server, Benutzer, Passwort) – für Filme und Serien über die Anbieter-Schnittstelle.</summary>
    public static (string Server, string User, string Pass)? FromPlaylistUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || !u.AbsolutePath.EndsWith("get.php", StringComparison.OrdinalIgnoreCase)) return null;
        var q = u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2)).Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]), StringComparer.OrdinalIgnoreCase);
        return q.TryGetValue("username", out var user) && q.TryGetValue("password", out var pass) ? (u.GetLeftPart(UriPartial.Authority), user, pass) : null;
    }

    public static string Api(string server, string user, string pass, string action, string? extra = null) =>
        $"{Normalize(server)}/player_api.php?username={Uri.EscapeDataString(user)}&password={Uri.EscapeDataString(pass)}&action={action}{extra}";

    static Dictionary<string, string> Categories(string json)
    {
        var map = new Dictionary<string, string>();
        using var d = JsonDocument.Parse(json);
        if (d.RootElement.ValueKind == JsonValueKind.Array)
            foreach (var c in d.RootElement.EnumerateArray()) map[Str(c, "category_id")] = Str(c, "category_name");
        return map;
    }

    static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.ValueKind switch { JsonValueKind.String => v.GetString() ?? "", JsonValueKind.Number => v.GetRawText(), _ => "" } : "";
    static string? Opt(JsonElement e, string key) => Str(e, key) is { Length: > 0 } s ? s : null;

    /// <summary>Filme aus get_vod_streams (+ get_vod_categories für die Kategorienamen).</summary>
    public static List<TvVod> ParseVod(string streams, string categories, string server, string user, string pass, string sourceId)
    {
        var cats = Categories(categories); var list = new List<TvVod>();
        using var d = JsonDocument.Parse(streams);
        if (d.RootElement.ValueKind != JsonValueKind.Array) return list;
        foreach (var v in d.RootElement.EnumerateArray())
        {
            var id = Str(v, "stream_id"); if (id.Length == 0) continue;
            var ext = Opt(v, "container_extension") ?? "mp4";
            var name = Str(v, "name");
            list.Add(new TvVod($"{sourceId}:v{id}", name, cats.GetValueOrDefault(Str(v, "category_id")) ?? "Filme",
                $"{Normalize(server)}/movie/{Uri.EscapeDataString(user)}/{Uri.EscapeDataString(pass)}/{id}.{ext}", Opt(v, "stream_icon"), Opt(v, "plot"),
                Opt(v, "year") ?? (Opt(v, "releasedate") ?? Opt(v, "added"))?.Split('-')[0], Opt(v, "rating")));
        }
        return list;
    }

    /// <summary>Serien aus get_series (+ get_series_categories).</summary>
    public static List<TvSeries> ParseSeries(string series, string categories, string sourceId)
    {
        var cats = Categories(categories); var list = new List<TvSeries>();
        using var d = JsonDocument.Parse(series);
        if (d.RootElement.ValueKind != JsonValueKind.Array) return list;
        foreach (var v in d.RootElement.EnumerateArray())
        {
            var id = Str(v, "series_id"); if (id.Length == 0) continue;
            list.Add(new TvSeries($"{sourceId}:s{id}", Str(v, "name"), cats.GetValueOrDefault(Str(v, "category_id")) ?? "Serien", Opt(v, "cover"), Opt(v, "plot"),
                (Opt(v, "releaseDate") ?? Opt(v, "release_date"))?.Split('-')[0], Opt(v, "rating"), id));
        }
        return list;
    }

    /// <summary>Folgen aus get_series_info.</summary>
    public static List<TvEpisode> ParseEpisodes(string info, string seriesId, string server, string user, string pass)
    {
        var list = new List<TvEpisode>();
        using var d = JsonDocument.Parse(info);
        if (!d.RootElement.TryGetProperty("episodes", out var eps) || eps.ValueKind != JsonValueKind.Object) return list;
        foreach (var season in eps.EnumerateObject())
        {
            if (season.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var e in season.Value.EnumerateArray())
            {
                var id = Str(e, "id"); if (id.Length == 0) continue;
                var ext = Opt(e, "container_extension") ?? "mkv";
                var info2 = e.TryGetProperty("info", out var i) && i.ValueKind == JsonValueKind.Object ? i : default;
                int.TryParse(season.Name, out var sn); int.TryParse(Str(e, "episode_num"), out var en);
                list.Add(new TvEpisode(seriesId, sn == 0 ? 1 : sn, en, Str(e, "title"), $"{Normalize(server)}/series/{Uri.EscapeDataString(user)}/{Uri.EscapeDataString(pass)}/{id}.{ext}",
                    info2.ValueKind == JsonValueKind.Object ? Opt(info2, "plot") : null, info2.ValueKind == JsonValueKind.Object && int.TryParse(Str(info2, "duration_secs"), out var ds) ? ds : 0));
            }
        }
        return list.OrderBy(x => x.Season).ThenBy(x => x.Episode).ToList();
    }

    public static string EpgUrl(string server, string user, string pass) =>
        $"{Normalize(server)}/xmltv.php?username={Uri.EscapeDataString(user)}&password={Uri.EscapeDataString(pass)}";
}

/// <summary>XMLTV-Programmführer. Liest gestreamt (auch große Dateien) und behält nur ein Zeitfenster.</summary>
public static class Xmltv
{
    /// <summary>„20261027180000 +0100“ → UTC.</summary>
    public static DateTime ParseTime(string s)
    {
        s = s.Trim();
        var main = s.Length >= 14 ? s[..14] : s.PadRight(14, '0');
        var t = DateTime.ParseExact(main, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var rest = s.Length > 14 ? s[14..].Trim() : "";
        if (rest.Length >= 5 && (rest[0] == '+' || rest[0] == '-') && int.TryParse(rest[1..3], out var hh) && int.TryParse(rest[3..5], out var mm))
            t -= (rest[0] == '-' ? -1 : 1) * new TimeSpan(hh, mm, 0);
        return DateTime.SpecifyKind(t, DateTimeKind.Utc);
    }

    public static List<TvProgramme> Parse(Stream xml, DateTime from, DateTime to, ISet<string>? onlyChannels = null)
    {
        var list = new List<TvProgramme>();
        using var r = XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, XmlResolver = null });
        while (r.Read())
        {
            if (r.NodeType != XmlNodeType.Element || r.Name != "programme") continue;
            var ch = r.GetAttribute("channel") ?? ""; var st = r.GetAttribute("start"); var sp = r.GetAttribute("stop");
            if (st is null || (onlyChannels is not null && !onlyChannels.Contains(ch))) { r.Skip(); continue; }
            DateTime start, stop;
            try { start = ParseTime(st); stop = sp is null ? start.AddMinutes(30) : ParseTime(sp); } catch (FormatException) { r.Skip(); continue; }
            if (stop < from || start > to) { r.Skip(); continue; }
            string title = "", desc = "", cat = "";
            using (var sub = r.ReadSubtree())
            {
                sub.Read();
                while (!sub.EOF)
                {
                    // ReadElementContentAsString springt selbst zum nächsten Knoten – dann nicht noch einmal Read()
                    if (sub.NodeType == XmlNodeType.Element && sub.Depth > 0 && sub.Name is "title" or "desc" or "category")
                    {
                        var nm = sub.Name; var v = sub.ReadElementContentAsString();
                        if (nm == "title" && title.Length == 0) title = v; else if (nm == "desc" && desc.Length == 0) desc = v; else if (nm == "category" && cat.Length == 0) cat = v;
                        continue;
                    }
                    sub.Read();
                }
            }
            list.Add(new TvProgramme(ch, start, stop, title.Trim(), desc.Trim(), cat.Trim()));
        }
        return list.OrderBy(p => p.Channel).ThenBy(p => p.Start).ToList();
    }
}

/// <summary>Catch-up (zeitversetzt sehen bei Anbietern, die es anbieten): Vorlage aus catchup-source mit {utc}, {lutc}, {start}, {duration}, ${start} …</summary>
public static class Catchup
{
    public static string? UrlFor(TvChannel c, TvProgramme p)
    {
        if (c.CatchupDays <= 0 && c.Catchup is null) return null;
        var dur = (int)(p.Stop - p.Start).TotalSeconds;
        var start = new DateTimeOffset(p.Start).ToUnixTimeSeconds();
        var tpl = c.CatchupSource;
        if (tpl is null)
            return c.Catchup switch
            {
                "append" or "default" => null,
                "shift" or "timeshift" => c.Url + (c.Url.Contains('?') ? "&" : "?") + $"utc={start}&lutc={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                _ => null,
            };
        var url = tpl.Replace("${start}", start.ToString()).Replace("{utc}", start.ToString()).Replace("{start}", start.ToString())
            .Replace("{lutc}", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()).Replace("${timestamp}", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            .Replace("{duration}", dur.ToString()).Replace("${duration}", dur.ToString())
            .Replace("{Y}", p.Start.ToString("yyyy")).Replace("{m}", p.Start.ToString("MM")).Replace("{d}", p.Start.ToString("dd")).Replace("{H}", p.Start.ToString("HH")).Replace("{M}", p.Start.ToString("mm"));
        return c.Catchup == "append" ? c.Url + url : url;
    }
}

/// <summary>Bibliothek: Quellen, Sender, Favoriten, Zuletzt gesehen, ausgeblendete Gruppen, Kindersicherung, Programmführer, Aufnahmen.</summary>
public sealed class TvLibrary(string dataRoot)
{
    string Dir => Path.Combine(dataRoot, "tv");
    sealed class State
    {
        public List<TvSource> Sources { get; set; } = [];
        public List<string> Favorites { get; set; } = [];
        public List<string> Recent { get; set; } = [];
        public List<string> HiddenGroups { get; set; } = [];
        public List<string> LockedGroups { get; set; } = [];
        public List<TvRecording> Recordings { get; set; } = [];
        public string? LastChannel { get; set; }
    }
    static readonly JsonSerializerOptions J = new() { WriteIndented = false };
    State Load() { try { return File.Exists(Path.Combine(Dir, "tv.json")) ? JsonSerializer.Deserialize<State>(File.ReadAllText(Path.Combine(Dir, "tv.json"))) ?? new() : new(); } catch (Exception e) when (e is IOException or JsonException) { return new(); } }
    void Save(State s) { Directory.CreateDirectory(Dir); var f = Path.Combine(Dir, "tv.json"); File.WriteAllText(f + ".tmp", JsonSerializer.Serialize(s, J)); File.Move(f + ".tmp", f, true); }
    void Change(Action<State> a) { var s = Load(); a(s); Save(s); }

    public IReadOnlyList<TvSource> Sources => Load().Sources;
    public IReadOnlyList<string> Favorites => Load().Favorites;
    public IReadOnlyList<string> Recent => Load().Recent;
    public IReadOnlyList<string> HiddenGroups => Load().HiddenGroups;
    public IReadOnlyList<string> LockedGroups => Load().LockedGroups;
    public IReadOnlyList<TvRecording> Recordings => Load().Recordings;
    public string? LastChannel => Load().LastChannel;

    public TvSource AddSource(string name, string kind, string url, string? epg = null, string? user = null)
    {
        var src = new TvSource(Guid.NewGuid().ToString("N")[..8], string.IsNullOrWhiteSpace(name) ? "Meine Sender" : name.Trim(), kind, url, epg, user, DateTime.UtcNow);
        Change(s => s.Sources.Add(src));
        return src;
    }

    public void RemoveSource(string id)
    {
        Change(s => { s.Sources.RemoveAll(x => x.Id == id); s.Favorites.RemoveAll(f => f.StartsWith(id + ":")); s.Recent.RemoveAll(f => f.StartsWith(id + ":")); });
        foreach (var f in new[] { $"{id}.m3u", $"{id}.epg.json", $"{id}.vod.json", $"{id}.series.json" }) { var p = Path.Combine(Dir, f); if (File.Exists(p)) File.Delete(p); }
        var fd = Path.Combine(Dir, "folgen");
        if (Directory.Exists(fd)) foreach (var f in Directory.GetFiles(fd, id + "_*.json")) File.Delete(f);
    }

    public void SetEpgUrl(string id, string? url) => Change(s => { var i = s.Sources.FindIndex(x => x.Id == id); if (i >= 0) s.Sources[i] = s.Sources[i] with { EpgUrl = url }; });

    /// <summary>Geladene Wiedergabeliste ablegen (die App lädt sie herunter oder liest die Datei).</summary>
    public void StorePlaylist(string id, string m3u) { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, $"{id}.m3u"), m3u); }
    public void StoreEpg(string id, IReadOnlyList<TvProgramme> programmes) { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, $"{id}.epg.json"), JsonSerializer.Serialize(programmes, J)); }

    /// <summary>Alle Sender aller Quellen (mit Quellen-Kennung), Nummern fortlaufend.</summary>
    public List<TvChannel> Channels()
    {
        var all = new List<TvChannel>();
        foreach (var src in Sources)
        {
            var f = Path.Combine(Dir, $"{src.Id}.m3u");
            if (!File.Exists(f)) continue;
            var (parsed, _) = M3u.Parse(File.ReadAllText(f), src.Id);
            var ch = parsed.Where(c => c.Kind == "live").ToList();
            var offset = all.Count == 0 ? 0 : all.Max(c => c.Number);
            all.AddRange(all.Count == 0 ? ch : ch.Select(c => c with { Number = c.Number + offset }));
        }
        return all;
    }

    /// <summary>Filme: aus den Wiedergabelisten (erkannt) und – bei Xtream – mit Cover und Beschreibung vom Anbieter.</summary>
    public List<TvVod> Vod()
    {
        var all = new List<TvVod>();
        foreach (var src in Sources)
        {
            var api = Path.Combine(Dir, $"{src.Id}.vod.json");
            if (File.Exists(api)) { try { all.AddRange(JsonSerializer.Deserialize<List<TvVod>>(File.ReadAllText(api)) ?? []); continue; } catch (JsonException) { } }
            var f = Path.Combine(Dir, $"{src.Id}.m3u");
            if (File.Exists(f)) all.AddRange(M3u.Parse(File.ReadAllText(f), src.Id).Channels.Where(c => c.Kind == "film").Select(c => new TvVod(c.Id, c.Name, c.Group, c.Url, c.Logo)));
        }
        return all;
    }

    /// <summary>Serien: aus den Listen nach „Name S01 E02“ gruppiert – oder vom Anbieter (Xtream).</summary>
    public List<TvSeries> Series()
    {
        var all = new List<TvSeries>();
        foreach (var src in Sources)
        {
            var api = Path.Combine(Dir, $"{src.Id}.series.json");
            if (File.Exists(api)) { try { all.AddRange(JsonSerializer.Deserialize<List<TvSeries>>(File.ReadAllText(api)) ?? []); continue; } catch (JsonException) { } }
            foreach (var g in PlaylistEpisodes(src.Id).GroupBy(e => e.SeriesId))
            {
                var first = M3uEntries(src.Id).First(c => c.Kind == "serie" && SeriesKey(src.Id, c) == g.Key);
                all.Add(new TvSeries(g.Key, M3u.SplitEpisode(first.Name).Series, first.Group, first.Logo));
            }
        }
        return all;
    }

    IEnumerable<TvChannel> M3uEntries(string srcId)
    {
        var f = Path.Combine(Dir, $"{srcId}.m3u");
        return File.Exists(f) ? M3u.Parse(File.ReadAllText(f), srcId).Channels : [];
    }

    static string SeriesKey(string srcId, TvChannel c) => $"{srcId}:s-" + M3u.SplitEpisode(c.Name).Series.ToLowerInvariant();

    List<TvEpisode> PlaylistEpisodes(string srcId)
    {
        var n = 0;
        return M3uEntries(srcId).Where(c => c.Kind == "serie").Select(c =>
        {
            var (_, s, e) = M3u.SplitEpisode(c.Name);
            return new TvEpisode(SeriesKey(srcId, c), s, e == 0 ? ++n : e, c.Name, c.Url);
        }).ToList();
    }

    /// <summary>Folgen einer Serie (aus der Liste oder zwischengespeichert vom Anbieter).</summary>
    public List<TvEpisode> Episodes(string seriesId)
    {
        var srcId = seriesId.Split(':')[0];
        var cache = Path.Combine(Dir, "folgen", seriesId.Replace(':', '_') + ".json");
        if (File.Exists(cache)) { try { return JsonSerializer.Deserialize<List<TvEpisode>>(File.ReadAllText(cache)) ?? []; } catch (JsonException) { } }
        return PlaylistEpisodes(srcId).Where(e => e.SeriesId == seriesId).OrderBy(e => e.Season).ThenBy(e => e.Episode).ToList();
    }

    public void StoreVod(string srcId, IReadOnlyList<TvVod> vod) { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, $"{srcId}.vod.json"), JsonSerializer.Serialize(vod, J)); }
    public void StoreSeries(string srcId, IReadOnlyList<TvSeries> s) { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, $"{srcId}.series.json"), JsonSerializer.Serialize(s, J)); }
    public void StoreEpisodes(string seriesId, IReadOnlyList<TvEpisode> eps)
    {
        var d = Path.Combine(Dir, "folgen"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, seriesId.Replace(':', '_') + ".json"), JsonSerializer.Serialize(eps, J));
    }

    public List<TvProgramme> Programmes()
    {
        var all = new List<TvProgramme>();
        foreach (var src in Sources)
        {
            var f = Path.Combine(Dir, $"{src.Id}.epg.json");
            try { if (File.Exists(f)) all.AddRange(JsonSerializer.Deserialize<List<TvProgramme>>(File.ReadAllText(f)) ?? []); }
            catch (JsonException) { }
        }
        return all;
    }

    public bool ToggleFavorite(string channelId)
    {
        var on = false;
        Change(s => { if (!s.Favorites.Remove(channelId)) { s.Favorites.Add(channelId); on = true; } });
        return on;
    }

    public void Watched(string channelId) => Change(s => { s.Recent.Remove(channelId); s.Recent.Insert(0, channelId); if (s.Recent.Count > 20) s.Recent.RemoveAt(20); s.LastChannel = channelId; });
    public void ToggleHiddenGroup(string g) => Change(s => { if (!s.HiddenGroups.Remove(g)) s.HiddenGroups.Add(g); });
    public void ToggleLockedGroup(string g) => Change(s => { if (!s.LockedGroups.Remove(g)) s.LockedGroups.Add(g); });

    public TvRecording Plan(TvChannel c, string title, DateTime start, DateTime stop)
    {
        if (stop <= start) throw new ArgumentException("Das Ende muss nach dem Anfang liegen.");
        var r = new TvRecording(Guid.NewGuid().ToString("N")[..8], c.Id, c.Name, string.IsNullOrWhiteSpace(title) ? c.Name : title, start, stop);
        Change(s => s.Recordings.Add(r));
        return r;
    }

    public void UpdateRecording(string id, string state, string? file = null) =>
        Change(s => { var i = s.Recordings.FindIndex(r => r.Id == id); if (i >= 0) s.Recordings[i] = s.Recordings[i] with { State = state, File = file ?? s.Recordings[i].File }; });

    public void RemoveRecording(string id) => Change(s => s.Recordings.RemoveAll(r => r.Id == id));

    /// <summary>Aufnahmen, die jetzt starten sollen (geplant, Startzeit erreicht, Ende noch nicht vorbei).</summary>
    public IReadOnlyList<TvRecording> Due(DateTime nowUtc) => Recordings.Where(r => r.State == "geplant" && r.Start <= nowUtc && r.Stop > nowUtc).ToList();

    /// <summary>Jetzt und danach für einen Sender.</summary>
    public static (TvProgramme? Now, TvProgramme? Next) NowNext(IEnumerable<TvProgramme> all, TvChannel c, DateTime nowUtc)
    {
        if (c.EpgId is null) return (null, null);
        var list = all.Where(p => string.Equals(p.Channel, c.EpgId, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Start).ToList();
        var now = list.FirstOrDefault(p => p.IsOn(nowUtc));
        var next = list.FirstOrDefault(p => p.Start >= (now?.Stop ?? nowUtc));
        return (now, next);
    }

    /// <summary>Dateiname für eine Aufnahme (ohne verbotene Zeichen).</summary>
    public static string RecordingFile(string folder, TvRecording r)
    {
        var safe = new string($"{r.Start.ToLocalTime():yyyy-MM-dd HH-mm} {r.ChannelName} – {r.Title}".Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray()).Trim();
        return Path.Combine(folder, safe.Length > 120 ? safe[..120] : safe) + ".ts";
    }
}
