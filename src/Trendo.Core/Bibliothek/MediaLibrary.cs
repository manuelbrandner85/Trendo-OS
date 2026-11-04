using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Bibliothek;

/// <summary>Freies Bild oder freier Klang aus Wikimedia Commons oder Openverse (Provider = Herkunft, z. B. „Openverse · flickr“). Kind: "bild" oder "klang". Bytes 0 = unbekannt, Seconds nur bei Klängen.</summary>
public sealed record MediaHit(string Id, string Kind, string Title, string Creator, string License, string LicenseUrl, string PageUrl, string FileUrl, string ThumbUrl,
    string Format, long Bytes, int Width, int Height, double Seconds, string Provider = MediaLibrary.Source)
{
    /// <summary>Frei ohne Bedingungen (CC0, gemeinfrei) – sonst muss der Urheber genannt werden.</summary>
    public bool Free => MediaLibrary.IsFree(License);
    /// <summary>Namensnennung nach dem TASL-Schema (Titel, Autor, Quelle, Lizenz).</summary>
    public string Credit => $"„{Title}“" + (Creator.Length > 0 ? $" von {Creator}" : "") + $", {License}" + (LicenseUrl.Length > 0 ? $" ({LicenseUrl})" : "") + $", {Provider} – {PageUrl}";
}

/// <summary>Freie Bilder & Klänge als Medienbrowser (Masterprompt 131, Runde 7): Wikimedia Commons durchsuchen (ohne Schlüssel), nach Lizenz und Format filtern,
/// Vorschaubilder und Vorhören zwischenspeichern und erst auf Klick in die eigene Bibliothek laden – immer mit Herkunft, Urheber und Lizenz.</summary>
public sealed partial class MediaLibrary(IWebFetch http, string root)
{
    public const string Source = "Wikimedia Commons";
    public const long MaxBytes = 100L << 20, MaxPreview = 25L << 20;
    public static readonly (string Id, string Label)[] LicenseFilters = [("alle", "Alle freien Lizenzen"), ("frei", "Ohne Bedingungen (CC0, gemeinfrei)"), ("nennung", "Mit Namensnennung (CC BY, CC BY-SA)")];
    public static readonly string[] ImageFormats = ["jpg", "png", "svg", "gif", "webp", "tif"];
    public static readonly string[] SoundFormats = ["ogg", "mp3", "wav", "flac", "opus", "oga"];
    /// <summary>Erlaubte Herkünfte (Domain-Endungen): Commons, Openverse und die Quellen, die Openverse ausliefert.</summary>
    static readonly string[] Hosts = ["wikimedia.org", "openverse.org", "staticflickr.com", "freesound.org", "jamendo.com", "ccmixter.org"];

    string IndexFile => Path.Combine(root, "index.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static bool IsFree(string license) { var l = license.ToLowerInvariant(); return l.Contains("cc0") || l.StartsWith("pd", StringComparison.Ordinal) || l.Contains("public domain") || l.Contains("gemeinfrei"); }
    static bool NonCommercial(string license) => license.Contains("NC", StringComparison.Ordinal);

    /// <summary>Suchadresse der Commons-API: Dateien (Namensraum 6) vom Typ Bild oder Klang, mit Lizenz, Urheber, Größe und Vorschaubild.</summary>
    public static string SearchUrl(string kind, string query, int offset = 0) =>
        "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=50"
        + "&gsrsearch=" + Uri.EscapeDataString(query.Trim() + (kind == "klang" ? " filetype:audio" : " filetype:bitmap|drawing"))
        + (offset > 0 ? "&gsroffset=" + offset : "")
        + "&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=320&iiextmetadatafilter=LicenseShortName|Artist|LicenseUrl|ObjectName";

    [GeneratedRegex("<[^>]+>")] private static partial Regex Tags();

    /// <summary>Antwort lesen und filtern: Lizenz („alle“, „frei“, „nennung“) und Format (Dateiendung oder leer). Nicht-kommerzielle Lizenzen fallen immer heraus.</summary>
    public static List<MediaHit> Parse(string json, string kind, string license = "alle", string format = "")
    {
        var res = new List<MediaHit>();
        using var d = JsonDocument.Parse(json);
        if (!d.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages)) return res;
        var ordered = pages.EnumerateObject().Select(p => p.Value).OrderBy(p => p.TryGetProperty("index", out var i) ? i.GetInt32() : int.MaxValue);
        foreach (var p in ordered)
        {
            if (!p.TryGetProperty("imageinfo", out var infos) || infos.GetArrayLength() == 0) continue;
            var ii = infos[0];
            string Meta(string key) => ii.TryGetProperty("extmetadata", out var em) && em.TryGetProperty(key, out var v) && v.TryGetProperty("value", out var s) && s.ValueKind == JsonValueKind.String
                ? WebUtility.HtmlDecode(Tags().Replace(s.GetString() ?? "", "")).Trim() : "";
            var title = p.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (title.StartsWith("File:", StringComparison.Ordinal)) title = title[5..];
            var ext = Path.GetExtension(title).TrimStart('.').ToLowerInvariant(); if (ext == "jpeg") ext = "jpg"; if (ext == "tiff") ext = "tif";
            if (!(kind == "klang" ? SoundFormats : ImageFormats).Contains(ext)) continue;
            if (format.Length > 0 && ext != format) continue;
            var lic = Meta("LicenseShortName"); if (lic.Length == 0 || NonCommercial(lic)) continue;
            if (license == "frei" && !IsFree(lic)) continue;
            if (license == "nennung" && IsFree(lic)) continue;
            var name = Meta("ObjectName"); if (name.Length == 0) name = Path.GetFileNameWithoutExtension(title);
            string S(string key) => ii.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            int I(string key) => ii.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
            var file = S("url"); var cut = file.IndexOf('?'); if (cut > 0) file = file[..cut];
            var thumb = kind == "klang" ? "" : S("thumburl");
            res.Add(new(p.TryGetProperty("pageid", out var id) ? id.GetRawText() : title, kind, name, Meta("Artist"), lic, Meta("LicenseUrl"), S("descriptionurl"), file, thumb, ext,
                ii.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number ? sz.GetInt64() : 0, I("width"), I("height"),
                ii.TryGetProperty("duration", out var du) && du.ValueKind == JsonValueKind.Number ? Math.Round(du.GetDouble(), 1) : 0));
        }
        return res;
    }

    public async Task<List<MediaHit>> Search(string kind, string query, string license = "alle", string format = "", CancellationToken ct = default, string provider = "commons")
    {
        if (query.Trim().Length == 0) throw new ArgumentException("Bitte ein Suchwort eingeben, z. B. „Regen“ oder „Berge“.");
        var a = provider is "commons" or "beide" ? Parse((await http.GetAsync(SearchUrl(kind, query), ct)).EnsureOk().Text, kind, license, format) : [];
        var b = provider is "openverse" or "beide" ? ParseOpenverse((await http.GetAsync(OpenverseUrl(kind, query, license), ct)).EnsureOk().Text, kind, license, format) : [];
        return Mix(a, b);
    }

    static bool Allowed(string url, out Uri u) => Uri.TryCreate(url, UriKind.Absolute, out u!) && u.Scheme == "https" && HostOk(u.Host);
    static bool HostOk(string host) => Hosts.Any(x => host == x || host.EndsWith("." + x, StringComparison.Ordinal));

    async Task<string> Fetch(string url, string file, long max, CancellationToken ct)
    {
        if (!Allowed(url, out var u)) throw new InvalidDataException("Die Quelle verweist auf eine fremde Adresse – nicht geladen.");
        var body = (await http.GetAsync(u.AbsoluteUri, ct)).EnsureOk().Body;
        if (body.LongLength > max) throw new InvalidDataException($"Die Datei ist größer als {max >> 20} MB – nicht geladen.");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllBytesAsync(file, body, ct);
        return file;
    }

    static string Safe(string s) { var x = string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')).Trim('_'); return x.Length > 60 ? x[..60] : x.Length == 0 ? "datei" : x; }

    /// <summary>Vorschaubild (zwischengespeichert); null bei Klängen oder fremder Adresse.</summary>
    public async Task<string?> Thumb(MediaHit h, CancellationToken ct = default)
    {
        if (h.ThumbUrl.Length == 0 || !Allowed(h.ThumbUrl, out _)) return null;
        var f = Path.Combine(root, "cache", "vorschau", Safe(h.Id) + Path.GetExtension(new Uri(h.ThumbUrl).LocalPath).ToLowerInvariant());
        return File.Exists(f) ? f : await Fetch(h.ThumbUrl, f, 5L << 20, ct);
    }

    /// <summary>Klang zum Vorhören holen (zwischengespeichert, höchstens 25 MB).</summary>
    public async Task<string> Preview(MediaHit h, CancellationToken ct = default)
    {
        if (h.Bytes > MaxPreview) throw new InvalidDataException("Zum Vorhören zu groß – lade den Klang in die Bibliothek.");
        var f = Path.Combine(root, "cache", "vorhoeren", Safe(h.Id) + "." + h.Format);
        return File.Exists(f) ? f : await Fetch(h.FileUrl, f, MaxPreview, ct);
    }

    public List<LibraryItem> Items()
    {
        try { return File.Exists(IndexFile) ? JsonSerializer.Deserialize<List<LibraryItem>>(File.ReadAllText(IndexFile)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    /// <summary>Auf Klick in die Bibliothek laden: Datei in „Bilder“ bzw. „Klänge“, daneben eine Herkunftsdatei; Eintrag im Verzeichnis und in HERKUNFT.txt.</summary>
    public async Task<LibraryItem> Download(MediaHit h, CancellationToken ct = default)
    {
        if (h.Bytes > MaxBytes) throw new InvalidDataException($"Die Datei ist größer als {MaxBytes >> 20} MB.");
        var have = Items().FirstOrDefault(i => i.Id == h.Id && File.Exists(i.File));
        if (have is not null) return have;
        var dir = Path.Combine(root, h.Kind == "klang" ? "Klänge" : "Bilder");
        var stem = Safe(h.Title); var file = Path.Combine(dir, stem + "." + h.Format);
        for (var n = 2; File.Exists(file); n++) file = Path.Combine(dir, $"{stem} ({n}).{h.Format}");
        await Fetch(h.FileUrl, file, MaxBytes, ct);
        File.WriteAllText(file + ".herkunft.txt", $"{h.Title}\nUrheber: {h.Creator}\nLizenz: {h.License} {h.LicenseUrl}\nQuelle: {h.Provider} – {h.PageUrl}\nDatei: {h.FileUrl}\nNamensnennung: {(h.Free ? "nicht nötig, aber nett" : "nötig")}\n{h.Credit}\n");
        var item = new LibraryItem { Source = h.Provider, Id = h.Id, Name = h.Title, Kind = h.Kind, License = h.License, Author = h.Creator, PageUrl = h.PageUrl, File = file, Added = DateTime.Now };
        var items = Items(); items.RemoveAll(i => i.Id == h.Id); items.Add(item);
        Directory.CreateDirectory(root); File.WriteAllText(IndexFile, JsonSerializer.Serialize(items, Opt));
        File.WriteAllText(Path.Combine(root, "HERKUNFT.txt"), Credits() + "\n");
        return item;
    }

    /// <summary>Namensnennungen aller geladenen Medien (für Abspann, Impressum oder Bericht).</summary>
    public string Credits() => string.Join("\n", Items().Select(i => $"„{i.Name}“{(i.Author.Length > 0 ? " von " + i.Author : "")}, {i.License}, {i.Source} – {i.PageUrl}"));
}
