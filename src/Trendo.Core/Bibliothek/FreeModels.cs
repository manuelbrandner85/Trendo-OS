using System.IO.Compression;
using System.Text.Json;

namespace Trendo.Core.Bibliothek;

/// <summary>Netzabruf mit Kopfzeilen (z. B. „Authorization“) – für Quellen, die einen eigenen Schlüssel des Nutzers brauchen.</summary>
public interface IWebFetchAuth : IWebFetch
{
    Task<WebReply> GetAsync(string url, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
}

/// <summary>Freie 3D-Modelle aus weiteren Quellen (Freigabe 05.11.2026, Q1–Q4): Thingiverse (offizielle Schnittstelle, eigener App-Schlüssel),
/// Smithsonian Open Access (CC0, Schlüssel von api.data.gov), NASA 3D Resources (gemeinfrei, über GitHub), Sketchfab (Suche frei, Laden mit eigenem Schlüssel).
/// Nur kostenlose Modelle, Lizenz immer dabei, kein Abgreifen von Webseiten. Seiten ohne Schnittstelle laufen über den Browser und den Download-Ordner.</summary>
public sealed class FreeModels(IWebFetch http, string root)
{
    public const string Thingiverse = "Thingiverse", Smithsonian = "Smithsonian", Nasa = "NASA", Sketchfab = "Sketchfab";
    public sealed record Source(string Name, string Hint, string? KeyName, string? KeyHelpUrl);
    public static readonly Source[] Sources =
    [
        new(Thingiverse, "Größte Sammlung für den 3D-Druck (STL)", "thingiverse-schluessel", "https://www.thingiverse.com/developers"),
        new(Smithsonian, "Museumsstücke und Fossilien als 3D-Scan, frei nutzbar (CC0)", "smithsonian-schluessel", "https://api.data.gov/signup/"),
        new(Nasa, "Raumfahrzeuge, Planeten und Geräte der NASA, gemeinfrei", null, null),
        new(Sketchfab, "Über eine Million Modelle unter Creative Commons; Suche frei, Laden mit eigenem Sketchfab-Schlüssel", "sketchfab-schluessel", "https://sketchfab.com/settings/password"),
    ];

    /// <summary>Darf das Modell auch verkauft oder in einem verkauften Produkt genutzt werden? (CC0, CC BY, CC BY-SA, gemeinfrei: ja; NC/ND oder unbekannt: nein)</summary>
    public static bool Commercial(string license)
    {
        var l = license.ToLowerInvariant().Replace('_', '-');
        if (l.Contains("noncommercial") || l.Contains("non-commercial") || l.Contains("by-nc") || l.Contains(" nc") || l.Contains("-nc")) return false;
        return l.Contains("cc0") || l.Contains("public domain") || l.Contains("gemeinfrei") || l.Contains("nasa") || l.Contains("attribution") || l.Contains("cc by") || l.Contains("cc-by") || l.Contains("cc-by-sa");
    }
    /// <summary>Muss der Urheber genannt werden? (alles außer CC0/gemeinfrei)</summary>
    public static bool NeedsCredit(string license) { var l = license.ToLowerInvariant(); return !(l.Contains("cc0") || l.Contains("public domain") || l.Contains("gemeinfrei")); }

    static string Prop(JsonElement e, params string[] path)
    {
        foreach (var p in path) { if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return ""; }
        return e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ValueKind is JsonValueKind.Number ? e.GetRawText() : "";
    }

    async Task<string> Cached(string url, string file, CancellationToken ct)
    {
        var f = Path.Combine(root, "cache", file);
        if (File.Exists(f) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(f)).TotalHours < 24) return await File.ReadAllTextAsync(f, ct);
        var json = (await http.GetAsync(url, ct)).EnsureOk().Text;
        Directory.CreateDirectory(Path.GetDirectoryName(f)!); await File.WriteAllTextAsync(f, json, ct); return json;
    }

    /// <summary>In einer Quelle suchen. <paramref name="key"/>: Schlüssel des Nutzers (falls die Quelle einen braucht).</summary>
    public async Task<List<AssetHit>> Search(string source, string query, string? key, CancellationToken ct = default)
    {
        var q = Uri.EscapeDataString(AssetLibrary.Translate(query.Trim().ToLowerInvariant()));
        var res = new List<AssetHit>();
        switch (source)
        {
            case Thingiverse:
            {
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Für Thingiverse brauchst du einen kostenlosen App-Schlüssel (thingiverse.com/developers) – einmal eintragen, er bleibt geschützt gespeichert.");
                using var d = JsonDocument.Parse((await http.GetAsync($"https://api.thingiverse.com/search/{q}?type=things&per_page=30&access_token={Uri.EscapeDataString(key)}", ct)).EnsureOk().Text);
                var hits = d.RootElement.ValueKind == JsonValueKind.Array ? d.RootElement : d.RootElement.TryGetProperty("hits", out var h) ? h : default;
                if (hits.ValueKind == JsonValueKind.Array)
                    foreach (var t in hits.EnumerateArray())
                        res.Add(new(Thingiverse, Prop(t, "id"), Prop(t, "name"), "modell", "3D-Druck", Prop(t, "thumbnail"), "Lizenz beim Laden prüfen", Prop(t, "creator", "name"), Prop(t, "public_url")));
                break;
            }
            case Smithsonian:
            {
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Für das Smithsonian brauchst du einen kostenlosen Schlüssel von api.data.gov – einmal eintragen.");
                var url = $"https://api.si.edu/openaccess/api/v1.0/search?q={q}+AND+online_media_type:%223D+Images%22&rows=30&api_key={Uri.EscapeDataString(key)}";
                using var d = JsonDocument.Parse((await http.GetAsync(url, ct)).EnsureOk().Text);
                if (d.RootElement.TryGetProperty("response", out var r) && r.TryGetProperty("rows", out var rows))
                    foreach (var row in rows.EnumerateArray())
                    {
                        var content = row.TryGetProperty("content", out var c) ? c : default;
                        var usage = Prop(content, "descriptiveNonRepeating", "metadata_usage", "access");
                        if (usage.Length > 0 && !usage.Equals("CC0", StringComparison.OrdinalIgnoreCase)) continue; // nur frei nutzbare
                        var model = SmithsonianModelUrl(content);
                        if (model is null) continue;
                        var thumb = ""; if (content.ValueKind == JsonValueKind.Object && content.TryGetProperty("descriptiveNonRepeating", out var dn) && dn.TryGetProperty("online_media", out var om) && om.TryGetProperty("media", out var media))
                            foreach (var m in media.EnumerateArray()) if (Prop(m, "thumbnail") is { Length: > 0 } th) { thumb = th; break; }
                        res.Add(new(Smithsonian, Prop(row, "id"), Prop(row, "title"), "modell", Prop(row, "unitCode"), thumb, "CC0 1.0", "Smithsonian Institution", Prop(content, "descriptiveNonRepeating", "record_link"), Polycount: 0));
                    }
                break;
            }
            case Nasa:
            {
                var json = await Cached("https://api.github.com/repos/nasa/NASA-3D-Resources/git/trees/master?recursive=1", "nasa-3d.json", ct);
                using var d = JsonDocument.Parse(json);
                var words = query.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(AssetLibrary.Translate).ToArray();
                foreach (var t in d.RootElement.GetProperty("tree").EnumerateArray())
                {
                    var path = Prop(t, "path");
                    if (!path.StartsWith("3D Models/", StringComparison.Ordinal) || !(path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))) continue;
                    var name = path.Split('/')[1];
                    if (words.Length > 0 && !words.All(w => name.Contains(w, StringComparison.OrdinalIgnoreCase))) continue;
                    if (res.Any(x => x.Name == name)) continue; // je Modell eine Datei (erste: GLB vor STL vor OBJ in der Sortierung des Baums)
                    res.Add(new(Nasa, path, name, "modell", "Raumfahrt", "", "Gemeinfrei (NASA-Nutzungsregeln: kein NASA-Logo, keine Werbung mit der NASA)", "NASA", "https://science.nasa.gov/3d-resources/"));
                    if (res.Count >= 40) break;
                }
                break;
            }
            case Sketchfab:
            {
                using var d = JsonDocument.Parse((await http.GetAsync($"https://api.sketchfab.com/v3/search?type=models&downloadable=true&count=24&q={q}", ct)).EnsureOk().Text);
                if (d.RootElement.TryGetProperty("results", out var rs))
                    foreach (var m in rs.EnumerateArray())
                    {
                        var lic = m.TryGetProperty("license", out var l) ? (l.ValueKind == JsonValueKind.Object ? Prop(l, "label") : l.ValueKind == JsonValueKind.String ? l.GetString() ?? "" : "") : "";
                        var thumb = ""; if (m.TryGetProperty("thumbnails", out var th) && th.TryGetProperty("images", out var imgs)) thumb = imgs.EnumerateArray().Select(i => (W: i.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 0, U: Prop(i, "url"))).Where(i => i.W is >= 200 and <= 720).OrderBy(i => i.W).Select(i => i.U).FirstOrDefault() ?? "";
                        res.Add(new(Sketchfab, Prop(m, "uid"), Prop(m, "name"), "modell", "", thumb, lic.Length > 0 ? lic : "Creative Commons (beim Laden prüfen)", Prop(m, "user", "displayName"), Prop(m, "viewerUrl"),
                            Polycount: m.TryGetProperty("faceCount", out var fc) && fc.ValueKind == JsonValueKind.Number ? fc.GetInt32() : 0));
                    }
                break;
            }
            default: throw new ArgumentException("Unbekannte Quelle.");
        }
        return res;
    }

    /// <summary>Beim Smithsonian: erste herunterladbare 3D-Datei (GLB bevorzugt, sonst OBJ/STL) in den Medien eines Eintrags.</summary>
    public static string? SmithsonianModelUrl(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("descriptiveNonRepeating", out var dn) || !dn.TryGetProperty("online_media", out var om) || !om.TryGetProperty("media", out var media)) return null;
        var urls = new List<string>();
        foreach (var m in media.EnumerateArray())
        {
            if (m.TryGetProperty("resources", out var res)) foreach (var r in res.EnumerateArray()) if (Prop(r, "url") is { Length: > 0 } u) urls.Add(u);
            if (Prop(m, "content") is { Length: > 0 } cu) urls.Add(cu);
        }
        return urls.FirstOrDefault(u => u.Contains(".glb", StringComparison.OrdinalIgnoreCase)) ?? urls.FirstOrDefault(u => u.Contains(".obj", StringComparison.OrdinalIgnoreCase) || u.Contains(".stl", StringComparison.OrdinalIgnoreCase));
    }

    static readonly string[] Hosts = ["thingiverse.com", "si.edu", "githubusercontent.com", "sketchfab.com", "sketchfab-prod-media.s3.amazonaws.com", "media.sketchfab.com"];
    static bool Allowed(Uri u) => u.Scheme == "https" && (Hosts.Any(h => u.Host == h || u.Host.EndsWith("." + h)) || (u.Host.EndsWith(".amazonaws.com") && u.Host.Contains("sketchfab")));

    async Task<byte[]> Get(string url, CancellationToken ct, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || !Allowed(u)) throw new InvalidDataException("Die Quelle verweist auf eine fremde Adresse – nicht geladen.");
        var r = headers is not null && http is IWebFetchAuth a ? await a.GetAsync(u.AbsoluteUri, headers, ct) : await http.GetAsync(u.AbsoluteUri, ct);
        return r.EnsureOk().Body;
    }

    /// <summary>Modell laden (nur auf Klick), in die Bibliothek legen, Lizenz und Namensnennung daneben (LIZENZ.txt).</summary>
    public async Task<LibraryItem> Download(AssetLibrary lib, AssetHit hit, string? key, CancellationToken ct = default)
    {
        var dir = Path.Combine(root, Safe(hit.Source), Safe(hit.Id)); Directory.CreateDirectory(dir);
        string main; var license = hit.License; var author = hit.Author;
        switch (hit.Source)
        {
            case Thingiverse:
            {
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Thingiverse-Schlüssel fehlt.");
                var tok = Uri.EscapeDataString(key);
                using (var info = JsonDocument.Parse((await http.GetAsync($"https://api.thingiverse.com/things/{Uri.EscapeDataString(hit.Id)}?access_token={tok}", ct)).EnsureOk().Text))
                { license = Prop(info.RootElement, "license") is { Length: > 0 } l ? l : license; author = Prop(info.RootElement, "creator", "name") is { Length: > 0 } a ? a : author; }
                using var files = JsonDocument.Parse((await http.GetAsync($"https://api.thingiverse.com/things/{Uri.EscapeDataString(hit.Id)}/files?access_token={tok}", ct)).EnsureOk().Text);
                var f = files.RootElement.EnumerateArray().Select(x => (Name: Prop(x, "name"), Url: Prop(x, "download_url") is { Length: > 0 } du ? du : Prop(x, "direct_url")))
                    .Where(x => Model(x.Name)).OrderBy(x => Rank(x.Name)).FirstOrDefault();
                if (f.Name is null) throw new InvalidOperationException("Dieses Ding hat keine 3D-Datei (STL, 3MF, OBJ).");
                main = Path.Combine(dir, Safe(f.Name));
                await File.WriteAllBytesAsync(main, await Get(f.Url + (f.Url.Contains('?') ? "&" : "?") + "access_token=" + tok, ct), ct);
                break;
            }
            case Smithsonian:
            {
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Smithsonian-Schlüssel fehlt.");
                using var d = JsonDocument.Parse((await http.GetAsync($"https://api.si.edu/openaccess/api/v1.0/content/{Uri.EscapeDataString(hit.Id)}?api_key={Uri.EscapeDataString(key)}", ct)).EnsureOk().Text);
                var content = d.RootElement.TryGetProperty("response", out var r) && r.TryGetProperty("content", out var c) ? c : default;
                var url = SmithsonianModelUrl(content) ?? throw new InvalidOperationException("Keine 3D-Datei zum Herunterladen gefunden.");
                main = Path.Combine(dir, Safe(Path.GetFileName(new Uri(url).LocalPath)) is { Length: > 0 } n && Model(n) ? n : hit.Id + ".glb");
                await File.WriteAllBytesAsync(main, await Get(url, ct), ct);
                break;
            }
            case Nasa:
            {
                var url = "https://raw.githubusercontent.com/nasa/NASA-3D-Resources/master/" + string.Join("/", hit.Id.Split('/').Select(Uri.EscapeDataString));
                var bytes = await Get(url, ct);
                if (bytes.Length < 200 && System.Text.Encoding.ASCII.GetString(bytes).StartsWith("version https://git-lfs", StringComparison.Ordinal)) // große Datei liegt im LFS-Speicher
                    bytes = await Get("https://media.githubusercontent.com/media/nasa/NASA-3D-Resources/master/" + string.Join("/", hit.Id.Split('/').Select(Uri.EscapeDataString)), ct);
                main = Path.Combine(dir, Safe(Path.GetFileName(hit.Id)));
                await File.WriteAllBytesAsync(main, bytes, ct);
                break;
            }
            case Sketchfab:
            {
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Zum Laden von Sketchfab brauchst du deinen eigenen Sketchfab-Schlüssel (Einstellungen → Passwort & API) – einmal eintragen.");
                if (http is not IWebFetchAuth) throw new InvalidOperationException("Laden mit Schlüssel wird hier nicht unterstützt.");
                using var d = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(await Get($"https://api.sketchfab.com/v3/models/{Uri.EscapeDataString(hit.Id)}/download", ct, new Dictionary<string, string> { ["Authorization"] = "Token " + key })));
                var glb = Prop(d.RootElement, "glb", "url"); var gltf = Prop(d.RootElement, "gltf", "url");
                if (glb.Length > 0) { main = Path.Combine(dir, Safe(hit.Id) + ".glb"); await File.WriteAllBytesAsync(main, await Get(glb, ct), ct); }
                else if (gltf.Length > 0)
                {
                    var zip = Path.Combine(dir, "paket.zip"); await File.WriteAllBytesAsync(zip, await Get(gltf, ct), ct);
                    using (var z = ZipFile.OpenRead(zip))
                        foreach (var e in z.Entries.Where(e => e.Name.Length > 0))
                        {
                            var target = Path.GetFullPath(Path.Combine(dir, e.FullName)); if (!target.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!); e.ExtractToFile(target, true);
                        }
                    File.Delete(zip);
                    main = Directory.GetFiles(dir, "*.gltf", SearchOption.AllDirectories).FirstOrDefault() ?? throw new InvalidOperationException("Im Paket liegt keine glTF-Datei.");
                }
                else throw new InvalidOperationException("Sketchfab bietet dieses Modell nicht zum Laden an.");
                break;
            }
            default: throw new ArgumentException("Unbekannte Quelle.");
        }
        return lib.Register(new LibraryItem { Source = hit.Source, Id = hit.Id, Name = hit.Name, Kind = "modell", License = license, Author = author, PageUrl = hit.PageUrl, File = main, Added = DateTime.Now }, dir);
    }

    /// <summary>Vorschaubild eines Treffers (zwischengespeichert); null bei fremder Adresse.</summary>
    public async Task<string?> Thumb(AssetHit hit, CancellationToken ct = default)
    {
        if (hit.ThumbUrl.Length == 0 || !Uri.TryCreate(hit.ThumbUrl, UriKind.Absolute, out var u) || !Allowed(u)) return null;
        var f = Path.Combine(root, "cache", "vorschau", Safe(hit.Source) + "-" + Safe(hit.Id) + ".img");
        if (!File.Exists(f)) { Directory.CreateDirectory(Path.GetDirectoryName(f)!); await File.WriteAllBytesAsync(f, await Get(hit.ThumbUrl, ct), ct); }
        return f;
    }

    /// <summary>Q2: eine selbst heruntergeladene Datei (aus dem Download-Ordner) mit Seite und Lizenz in die Bibliothek übernehmen; ZIP-Pakete werden entpackt.</summary>
    public LibraryItem Pickup(AssetLibrary lib, string file, string site, string license, string author, string pageUrl)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Datei ist nicht mehr im Download-Ordner.");
        var name = Path.GetFileNameWithoutExtension(file);
        var dir = Path.Combine(root, Safe(site), Safe(name) + "-" + DateTime.Now.ToString("HHmmss")); Directory.CreateDirectory(dir);
        string main;
        if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using (var z = ZipFile.OpenRead(file))
                foreach (var e in z.Entries.Where(e => e.Name.Length > 0))
                {
                    var target = Path.GetFullPath(Path.Combine(dir, e.FullName)); if (!target.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!); e.ExtractToFile(target, true);
                }
            main = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(f => Model(Path.GetFileName(f)) || f.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)).OrderBy(f => Rank(f)).FirstOrDefault() ?? dir;
        }
        else { main = Path.Combine(dir, Path.GetFileName(file)); File.Copy(file, main, true); }
        var kind = Model(Path.GetFileName(main)) || main.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) ? "modell" : main == dir ? "paket" : "datei";
        return lib.Register(new LibraryItem { Source = site, Id = Path.GetFileName(dir), Name = name, Kind = kind, License = license, Author = author, PageUrl = pageUrl, File = main, Added = DateTime.Now }, dir);
    }

    static bool Model(string n) => n.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".3mf", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
    static int Rank(string n) => n.EndsWith(".3mf", StringComparison.OrdinalIgnoreCase) ? 0 : n.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    static string Safe(string s) { var c = string.Concat(s.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-')).Trim('.', '-'); return c.Length == 0 ? "modell" : c.Length > 80 ? c[..80] : c; }
}

/// <summary>Q2: Seiten ohne offene Schnittstelle (Printables, MakerWorld, Cults3D, MyMiniFactory, Thangs, itch.io): Du lädst im Browser selbst herunter,
/// Trendo übernimmt die neue Datei aus dem Download-Ordner – kein Abgreifen der Seite, kein Passwort.</summary>
public static class DownloadPickup
{
    public sealed record Site(string Name, string Url, string Kind);
    public static readonly Site[] Sites =
    [
        new("Printables", "https://www.printables.com/model?q=", "3D-Druck"),
        new("MakerWorld", "https://makerworld.com/de/search/models?keyword=", "3D-Druck"),
        new("Cults3D", "https://cults3d.com/de/suche?q=", "3D-Druck"),
        new("MyMiniFactory", "https://www.myminifactory.com/search/?query=", "3D-Druck"),
        new("Thangs", "https://thangs.com/search/", "3D-Druck"),
        new("itch.io (freie Spiele-Assets)", "https://itch.io/game-assets/free/tag-", "Spiele"),
    ];
    public static readonly string[] Extensions = [".stl", ".3mf", ".obj", ".glb", ".gltf", ".fbx", ".step", ".stp", ".zip", ".png", ".wav", ".ogg", ".mp3"];

    /// <summary>Neue passende Dateien im Download-Ordner seit <paramref name="since"/> (neueste zuerst, halb geladene ausgenommen).</summary>
    public static List<string> NewFiles(string downloads, DateTime since, IEnumerable<string>? exts = null)
    {
        if (!Directory.Exists(downloads)) return [];
        var ok = (exts ?? Extensions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. new DirectoryInfo(downloads).EnumerateFiles().Where(f => ok.Contains(f.Extension) && f.LastWriteTime >= since && !f.Name.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) && !f.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTime).Select(f => f.FullName)];
    }

    /// <summary>Suchadresse einer Seite (Suchwort angehängt).</summary>
    public static string SearchUrl(Site s, string query) => s.Url + Uri.EscapeDataString(query.Trim());
}
