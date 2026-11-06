using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Bibliothek;

/// <summary>Freie Bilder & Klänge – Profi 29 (05.11.2026): ähnliche Bilder (Openverse „related“, für Commons über die Stichworte des Titels),
/// Merklisten wie Unsplash Collections (je Projekt, mit Lizenz und Namensnennung je Eintrag) und Schriftvorschau mit eigenem Text.</summary>
public sealed partial class MediaLibrary
{
    /// <summary>Adresse für ähnliche Bilder bei Openverse; null für Treffer aus Wikimedia Commons.</summary>
    public static string? RelatedUrl(MediaHit h) =>
        h.Id.StartsWith("ov-", StringComparison.Ordinal) ? $"https://api.openverse.org/v1/{(h.Kind == "klang" ? "audio" : "images")}/{Uri.EscapeDataString(h.Id[3..])}/related/" : null;

    static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase) { "file", "datei", "jpg", "jpeg", "png", "the", "and", "der", "die", "das", "und", "von", "mit", "im", "in", "am", "of", "a", "an", "photo", "foto", "bild", "image", "ohne", "titel", "img", "dsc", "dscn", "dcim", "pic", "untitled" };

    /// <summary>Stichworte aus dem Titel (für ähnliche Commons-Bilder): bis zu drei aussagekräftige Wörter.</summary>
    public static string Keywords(string title) =>
        string.Join(" ", Regex.Matches(title, @"\p{L}{3,}").Select(m => m.Value).Where(w => !Stop.Contains(w)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3));

    /// <summary>Ähnliche Bilder oder Klänge: Openverse „related“, sonst Suche mit den Stichworten des Titels. Das Ausgangsbild selbst fällt heraus.</summary>
    public async Task<List<MediaHit>> Related(MediaHit h, string license = "alle", CancellationToken ct = default)
    {
        List<MediaHit> res;
        if (RelatedUrl(h) is { } url) res = ParseOpenverse((await http.GetAsync(url, ct)).EnsureOk().Text, h.Kind, license);
        else
        {
            var k = Keywords(h.Title); if (k.Length == 0) throw new InvalidOperationException("Zu diesem Titel lassen sich keine Stichworte finden – bitte selbst suchen.");
            res = await Search(h.Kind, k, license, "", ct, "beide");
        }
        return [.. res.Where(x => x.Id != h.Id && x.FileUrl != h.FileUrl)];
    }
}

/// <summary>Merklisten: benannte Sammlungen (z. B. je Projekt) mit den vollständigen Treffern, damit Lizenz und Namensnennung immer dabei sind.</summary>
public sealed class MediaCollections(string root)
{
    public sealed record Entry(MediaHit Hit, DateTimeOffset Added, string Note = "");
    public sealed record Collection(string Name, List<Entry> Entries);

    string FilePath => Path.Combine(root, "merklisten.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public List<Collection> All()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Collection>>(File.ReadAllText(FilePath), Opt) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(List<Collection> all) { Directory.CreateDirectory(root); var tmp = FilePath + ".neu"; File.WriteAllText(tmp, JsonSerializer.Serialize(all, Opt)); SafeFile.Move(tmp, FilePath); }

    static string Clean(string name)
    {
        var n = Regex.Replace(name ?? "", @"\s+", " ").Trim();
        if (n.Length == 0) throw new ArgumentException("Bitte einen Namen für die Merkliste, z. B. das Projekt.");
        return n.Length > 60 ? n[..60] : n;
    }

    /// <summary>Treffer merken (Liste wird bei Bedarf angelegt). Doppelte Einträge bleiben einfach. Nur freie Lizenzen – „nicht kommerziell“ und „keine Bearbeitung“ nie.</summary>
    public bool Add(string list, MediaHit h, string note = "")
    {
        var name = Clean(list);
        var lic = h.License.ToLowerInvariant();
        if (Regex.IsMatch(lic, @"(^|[\s\-])(nc|nd)([\s\-]|$)") || !(MediaLibrary.IsFree(h.License) || Regex.IsMatch(lic, @"(^|[\s\-])by([\s\-]|$)"))) throw new InvalidOperationException($"„{h.Title}“ hat keine freie Lizenz ({h.License}) – nicht gemerkt.");
        var all = All(); var c = all.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (c is null) { if (all.Count >= 50) throw new InvalidOperationException("Höchstens 50 Merklisten."); c = new Collection(name, []); all.Add(c); }
        if (c.Entries.Any(e => e.Hit.Id == h.Id)) return false;
        if (c.Entries.Count >= 500) throw new InvalidOperationException("Höchstens 500 Einträge je Merkliste.");
        c.Entries.Add(new Entry(h, DateTimeOffset.Now, note.Trim()));
        Save(all); return true;
    }

    public void Remove(string list, string id) { var all = All(); if (all.FirstOrDefault(x => x.Name == list) is { } c) { c.Entries.RemoveAll(e => e.Hit.Id == id); Save(all); } }
    public void Delete(string list) { var all = All(); all.RemoveAll(x => x.Name == list); Save(all); }
    public void Rename(string list, string to) { var all = All(); var n = Clean(to); if (all.Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase) && x.Name != list)) throw new InvalidOperationException("Diesen Namen gibt es schon."); var i = all.FindIndex(x => x.Name == list); if (i >= 0) { all[i] = all[i] with { Name = n }; Save(all); } }

    /// <summary>Alle Namensnennungen einer Merkliste im gewünschten Format (Text, HTML, Markdown) – gemeinfreie Werke zuletzt, als Dank.</summary>
    public string Credits(string list, string format = "text")
    {
        var c = All().FirstOrDefault(x => x.Name == list) ?? throw new InvalidOperationException("Diese Merkliste gibt es nicht.");
        var lines = c.Entries.OrderBy(e => e.Hit.Free).Select(e => MediaLibrary.CreditAs(e.Hit, format)).ToList();
        return format == "html" ? "<ul>\n" + string.Concat(lines.Select(l => "  <li>" + l + "</li>\n")) + "</ul>" : string.Join(format == "md" ? "\n\n" : "\n", lines);
    }
}

/// <summary>Schriftvorschau mit eigenem Text: eine Stärke (400) in einen Zwischenspeicher laden.</summary>
public sealed partial class FreeFontsPreview(IWebFetch http, string cacheDir)
{
    public async Task<string> File400(FontHit f, CancellationToken ct = default)
    {
        if (!FreeFonts.OpenLicense(f.License)) throw new InvalidDataException("Keine offene Lizenz.");
        var safe = string.Concat(f.Id.Select(c => char.IsLetterOrDigit(c) || c is '-' ? c : '_'));
        var file = Path.Combine(cacheDir, safe + "-400.ttf");
        if (File.Exists(file) && new FileInfo(file).Length > 1000) return file; // nur gültige Schriften wiederverwenden
        var r = (await http.GetAsync(FreeFonts.FileUrl(f, 400), ct)).EnsureOk();
        if (r.Body.Length < 4 || r.Body.Length > 20 << 20 || !(r.Body[0] == 0 && r.Body[1] == 1 || r.Body[0] == (byte)'O' && r.Body[1] == (byte)'T' || r.Body[0] == (byte)'t' && r.Body[1] == (byte)'r'))
            throw new InvalidDataException("Die Datei ist keine Schrift.");
        Directory.CreateDirectory(cacheDir); await File.WriteAllBytesAsync(file, r.Body, ct);
        return file;
    }
}
