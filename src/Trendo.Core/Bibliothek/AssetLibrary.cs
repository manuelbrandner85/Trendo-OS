using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Bibliothek;

/// <summary>Treffer in einer freien 3D-Quelle. Kind: "modell", "material" oder "himmel" (HDRI).</summary>
public sealed record AssetHit(string Source, string Id, string Name, string Kind, string Category, string ThumbUrl, string License, string Author, string PageUrl, double[]? SizeCm = null, int Polycount = 0);

/// <summary>Heruntergeladenes Objekt in der eigenen Bibliothek – mit Lizenz und Quelle, damit die Namensnennung (falls nötig) immer mitkommt.</summary>
public sealed class LibraryItem
{
    public string Source { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "modell";
    public string License { get; set; } = "";
    public string Author { get; set; } = "";
    public string PageUrl { get; set; } = "";
    public string File { get; set; } = "";
    public DateTime Added { get; set; }
    public double[]? SizeCm { get; set; }
}

/// <summary>T-0279: Freie 3D-Quellen live durchsuchen und Objekte auf Klick in die eigene Bibliothek holen.
/// Poly Haven (CC0: Modelle, HDRI) und ambientCG (CC0: Materialien) – beide ohne Schlüssel, mit Trendo-Kennung, Liste 1 Tag zwischengespeichert.</summary>
public sealed class AssetLibrary(IWebFetch http, string root)
{
    public const string PolyHaven = "Poly Haven", AmbientCg = "ambientCG";
    string IndexFile => Path.Combine(root, "index.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public List<LibraryItem> Items()
    {
        try { return File.Exists(IndexFile) ? JsonSerializer.Deserialize<List<LibraryItem>>(File.ReadAllText(IndexFile)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    void Save(List<LibraryItem> items) { Directory.CreateDirectory(root); File.WriteAllText(IndexFile, JsonSerializer.Serialize(items, Opt)); }

    async Task<string> Cached(string url, string file, CancellationToken ct)
    {
        var f = Path.Combine(root, "cache", file);
        if (File.Exists(f) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(f)).TotalHours < 24) return await File.ReadAllTextAsync(f, ct);
        var json = (await http.GetAsync(url, ct)).EnsureOk().Text;
        Directory.CreateDirectory(Path.GetDirectoryName(f)!); await File.WriteAllTextAsync(f, json, ct); return json;
    }

    /// <summary>Suche: Poly Haven über die ganze Liste (Name, Schlagwörter, Kategorien), ambientCG über dessen Suche.</summary>
    public async Task<List<AssetHit>> Search(string query, string kind = "modell", CancellationToken ct = default)
    {
        var q = query.Trim().ToLowerInvariant(); var res = new List<AssetHit>();
        if (kind is "modell" or "himmel")
        {
            var t = kind == "modell" ? "models" : "hdris";
            using var d = JsonDocument.Parse(await Cached($"https://api.polyhaven.com/assets?t={t}", $"polyhaven-{t}.json", ct));
            foreach (var p in d.RootElement.EnumerateObject())
            {
                var a = p.Value; var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? p.Name : p.Name;
                var tags = a.TryGetProperty("tags", out var tg) ? tg.EnumerateArray().Select(x => x.GetString() ?? "") : [];
                var cats = a.TryGetProperty("categories", out var ca) ? ca.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
                var hay = (name + " " + string.Join(' ', tags) + " " + string.Join(' ', cats) + " " + p.Name).ToLowerInvariant();
                if (q.Length > 0 && !Words(q).All(w => hay.Contains(w) || hay.Contains(Translate(w)))) continue;
                double[]? size = a.TryGetProperty("dimensions", out var dm) && dm.GetArrayLength() == 3 ? dm.EnumerateArray().Select(x => Math.Round(x.GetDouble() / 10, 1)).ToArray() : null; // mm → cm
                var author = a.TryGetProperty("authors", out var au) ? string.Join(", ", au.EnumerateObject().Select(x => x.Name)) : "";
                res.Add(new(PolyHaven, p.Name, name, kind, cats.FirstOrDefault() ?? "", $"https://cdn.polyhaven.com/asset_img/thumbs/{p.Name}.png?width=256&height=256", "CC0 1.0", author,
                    $"https://polyhaven.com/a/{p.Name}", size, a.TryGetProperty("polycount", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt32() : 0));
            }
            return res.OrderByDescending(h => q.Length > 0 && h.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ThenBy(h => h.Name).ToList();
        }
        // Materialien: ambientCG
        var url = $"https://ambientcg.com/api/v2/full_json?type=Material&q={Uri.EscapeDataString(Translate(q))}&limit=40&include=previewData,displayData";
        using var md = JsonDocument.Parse((await http.GetAsync(url, ct)).EnsureOk().Text);
        foreach (var a in md.RootElement.GetProperty("foundAssets").EnumerateArray())
        {
            var id = a.GetProperty("assetId").GetString() ?? "";
            var thumb = a.TryGetProperty("previewImage", out var pi) && pi.TryGetProperty("256-PNG", out var t256) ? t256.GetString() ?? "" : "";
            var name = a.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? id : id;
            res.Add(new(AmbientCg, id, name, "material", a.TryGetProperty("displayCategory", out var dc) ? dc.GetString() ?? "" : "", thumb, "CC0 1.0", "ambientCG", $"https://ambientcg.com/view?id={id}"));
        }
        return res;
    }

    static IEnumerable<string> Words(string q) => q.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Deutsche Suchwörter für die englisch verschlagworteten Quellen.</summary>
    public static string Translate(string w) => w switch
    {
        "stuhl" => "chair", "sessel" => "armchair", "sofa" or "couch" => "sofa", "tisch" => "table", "lampe" or "leuchte" => "lamp", "pflanze" => "plant", "baum" => "tree",
        "regal" => "shelf", "schrank" => "cabinet", "bett" => "bed", "vase" => "vase", "holz" => "wood", "stein" => "rock", "fliese" or "fliesen" => "tiles", "beton" => "concrete",
        "stoff" => "fabric", "metall" => "metal", "ziegel" => "bricks", "parkett" => "wood floor", "teppich" => "carpet", "kiste" => "crate", "fass" => "barrel", "werkzeug" => "tool",
        "himmel" => "sky", "innen" => "indoor", "abend" => "sunset", "leder" => "leather", "marmor" => "marble", "gras" => "grass", "erde" => "ground", "putz" => "plaster",
        _ => w,
    };

    /// <summary>Objekt herunterladen (nur auf ausdrücklichen Klick) und in der Bibliothek vermerken. Modelle als glTF mit 1k-Texturen,
    /// Himmel als 1k-HDR, Materialien als 1K-JPG-Satz (Farbe, Normal, Rauheit …).</summary>
    public async Task<LibraryItem> Download(AssetHit hit, CancellationToken ct = default)
    {
        var dir = Path.Combine(root, Safe(hit.Source), Safe(hit.Id)); Directory.CreateDirectory(dir); string main;
        if (hit.Source == PolyHaven)
        {
            using var d = JsonDocument.Parse((await http.GetAsync($"https://api.polyhaven.com/files/{hit.Id}", ct)).EnsureOk().Text);
            if (hit.Kind == "modell")
            {
                var g = d.RootElement.GetProperty("gltf").GetProperty("1k").GetProperty("gltf");
                main = Path.Combine(dir, Path.GetFileName(new Uri(g.GetProperty("url").GetString()!).LocalPath));
                await Fetch(g.GetProperty("url").GetString()!, main, ct);
                foreach (var inc in g.GetProperty("include").EnumerateObject())
                {
                    var target = Path.GetFullPath(Path.Combine(dir, inc.Name));
                    if (!target.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ungültiger Dateipfad in der Paketliste.");
                    await Fetch(inc.Value.GetProperty("url").GetString()!, target, ct);
                }
            }
            else
            {
                var h = d.RootElement.GetProperty("hdri").GetProperty("1k").GetProperty("hdr").GetProperty("url").GetString()!;
                main = Path.Combine(dir, Path.GetFileName(new Uri(h).LocalPath)); await Fetch(h, main, ct);
            }
        }
        else
        {
            using var d = JsonDocument.Parse((await http.GetAsync($"https://ambientcg.com/api/v2/full_json?id={Uri.EscapeDataString(hit.Id)}&include=downloadData", ct)).EnsureOk().Text);
            var dl = d.RootElement.GetProperty("foundAssets")[0].GetProperty("downloadFolders").GetProperty("default").GetProperty("downloadFiletypeCategories").GetProperty("zip").GetProperty("downloads")
                .EnumerateArray().FirstOrDefault(x => x.GetProperty("attribute").GetString() == "1K-JPG");
            if (dl.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Für dieses Material gibt es kein 1K-Paket.");
            var zip = Path.Combine(dir, hit.Id + "_1K-JPG.zip"); await Fetch(dl.GetProperty("fullDownloadPath").GetString()!, zip, ct);
            using (var z = ZipFile.OpenRead(zip))
                foreach (var e in z.Entries.Where(e => e.Name.Length > 0))
                {
                    var target = Path.GetFullPath(Path.Combine(dir, e.Name)); if (!target.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)) continue;
                    e.ExtractToFile(target, true);
                }
            File.Delete(zip); main = dir;
        }
        var item = new LibraryItem { Source = hit.Source, Id = hit.Id, Name = hit.Name, Kind = hit.Kind, License = hit.License, Author = hit.Author, PageUrl = hit.PageUrl, File = main, Added = DateTime.Now, SizeCm = hit.SizeCm };
        var items = Items(); items.RemoveAll(i => i.Source == item.Source && i.Id == item.Id); items.Add(item); Save(items);
        File.WriteAllText(Path.Combine(dir, "LIZENZ.txt"), $"{hit.Name}\nQuelle: {hit.Source} – {hit.PageUrl}\nLizenz: {hit.License}\nUrheber: {hit.Author}\n");
        return item;
    }

    /// <summary>Fremd geladenes Objekt (weitere Quellen, Download-Ordner) eintragen und Lizenz samt Namensnennung daneben legen.</summary>
    public LibraryItem Register(LibraryItem item, string dir)
    {
        var items = Items(); items.RemoveAll(i => i.Source == item.Source && i.Id == item.Id); items.Add(item); Save(items);
        Directory.CreateDirectory(dir);
        var credit = FreeModels.NeedsCredit(item.License) ? $"Namensnennung (bitte bei Weitergabe angeben): „{item.Name}“ von {item.Author}, {item.Source}, {item.License}" : "Keine Namensnennung nötig.";
        var commercial = FreeModels.Commercial(item.License) ? "Kommerzielle Nutzung: erlaubt." : "Kommerzielle Nutzung: nicht erlaubt oder unklar – nur privat nutzen.";
        File.WriteAllLines(Path.Combine(dir, "LIZENZ.txt"), [item.Name, $"Quelle: {item.Source} – {item.PageUrl}", $"Lizenz: {item.License}", $"Urheber: {item.Author}", "", credit, commercial]);
        return item;
    }

    static readonly string[] Hosts = ["polyhaven.org", "polyhaven.com", "ambientcg.com", "struffelproductions.com"];
    static bool Allowed(Uri u) => u.Scheme == "https" && Hosts.Any(h => u.Host == h || u.Host.EndsWith("." + h));

    async Task Fetch(string url, string file, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || !Allowed(u))
            throw new InvalidDataException("Die Quelle verweist auf eine fremde Adresse – nicht geladen.");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var bytes = (await http.GetAsync(u.AbsoluteUri, ct)).EnsureOk().Body; await File.WriteAllBytesAsync(file, bytes, ct);
    }

    /// <summary>Vorschaubild eines Treffers (zwischengespeichert); null, wenn es keines gibt oder die Adresse fremd ist.</summary>
    public async Task<string?> Thumb(AssetHit hit, CancellationToken ct = default)
    {
        if (hit.ThumbUrl.Length == 0 || !Uri.TryCreate(hit.ThumbUrl, UriKind.Absolute, out var u) || !Allowed(u)) return null;
        var f = Path.Combine(root, "cache", "vorschau", Safe(hit.Source) + "-" + Safe(hit.Id) + ".png");
        if (!File.Exists(f)) await Fetch(hit.ThumbUrl, f, ct);
        return f;
    }

    /// <summary>T-0279: Eigenes Modell (z. B. aus Blender umgewandelt) in die Bibliothek übernehmen – Quelle „Eigene Datei“, Lizenz trägt der Nutzer selbst.</summary>
    public LibraryItem AddLocal(string glb, string name, string origin)
    {
        if (!File.Exists(glb)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
        var id = Safe(Path.GetFileNameWithoutExtension(name)).ToLowerInvariant(); var dir = Path.Combine(root, Safe(Own), id); Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, id + Path.GetExtension(glb).ToLowerInvariant());
        if (!string.Equals(Path.GetFullPath(glb), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) File.Copy(glb, target, true);
        var item = new LibraryItem { Source = Own, Id = id, Name = Path.GetFileNameWithoutExtension(name), Kind = "modell", License = "eigene Datei", Author = "", PageUrl = origin, File = target, Added = DateTime.Now };
        var items = Items(); items.RemoveAll(i => i.Source == Own && i.Id == id); items.Add(item); Save(items);
        return item;
    }
    public const string Own = "Eigene Datei";

    public void Remove(LibraryItem item)
    {
        var items = Items(); items.RemoveAll(i => i.Source == item.Source && i.Id == item.Id); Save(items);
        var dir = Path.Combine(root, Safe(item.Source), Safe(item.Id)); if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    /// <summary>Namensnennungen aller Objekte (für Bericht und Export).</summary>
    public string Credits() => string.Join("\n", Items().Select(i => $"{i.Name} – {i.Author} – {i.Source} ({i.License}) – {i.PageUrl}"));

    static string Safe(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
}
