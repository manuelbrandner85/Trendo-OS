using System.Net;
using System.Text.Json;

namespace Trendo.Core.Bibliothek;

/// <summary>Vorbild-Funktionen (Openverse, Unsplash, Pexels, Pixabay, Freesound, Google Fonts – nur Funktionen, Clean-Room, 04.11.2026):
/// zweite Quelle Openverse (Flickr, Freesound, Jamendo … ohne Schlüssel), Quellen mischen, Ausrichtung filtern, Namensnennung als Text/HTML/Markdown.</summary>
public sealed partial class MediaLibrary
{
    public static readonly (string Id, string Label)[] Providers = [("commons", "Wikimedia Commons"), ("openverse", "Openverse (Flickr, Freesound …)"), ("beide", "Beide Quellen")];
    public static readonly (string Id, string Label)[] Orientations = [("alle", "Jede Ausrichtung"), ("quer", "Querformat"), ("hoch", "Hochformat"), ("quadrat", "Quadratisch")];
    public static readonly (string Id, string Label)[] CreditFormats = [("text", "Text"), ("html", "HTML (Webseite)"), ("md", "Markdown")];

    /// <summary>Openverse-Suche: nur Lizenzen, die auch kommerziell und mit Änderungen erlaubt sind (CC0, gemeinfrei, CC BY, CC BY-SA).</summary>
    public static string OpenverseUrl(string kind, string query, string license = "alle") =>
        $"https://api.openverse.org/v1/{(kind == "klang" ? "audio" : "images")}/?q={Uri.EscapeDataString(query.Trim())}&page_size=40&mature=false&license="
        + (license switch { "frei" => "cc0,pdm", "nennung" => "by,by-sa", _ => "cc0,pdm,by,by-sa" });

    /// <summary>Lizenzname wie auf Creative Commons: „cc0“ → „CC0 1.0“, „by-sa“ + „4.0“ → „CC BY-SA 4.0“, „pdm“ → „Public Domain Mark“.</summary>
    public static string LicenseName(string code, string version) => code.ToLowerInvariant() switch
    {
        "cc0" => "CC0 " + (version.Length > 0 ? version : "1.0"),
        "pdm" => "Public Domain Mark",
        var c => ("CC " + c.ToUpperInvariant() + " " + version).Trim(),
    };

    public static List<MediaHit> ParseOpenverse(string json, string kind, string license = "alle", string format = "")
    {
        var res = new List<MediaHit>();
        using var d = JsonDocument.Parse(json);
        if (!d.RootElement.TryGetProperty("results", out var rs) || rs.ValueKind != JsonValueKind.Array) return res;
        foreach (var r in rs.EnumerateArray())
        {
            string S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? WebUtility.HtmlDecode(v.GetString() ?? "").Trim() : "";
            long L(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
            var code = S("license"); if (code.Length == 0 || code.Contains("nc") || code.Contains("nd")) continue; // nicht-kommerziell / keine Bearbeitung: nie
            var lic = LicenseName(code, S("license_version"));
            if (license == "frei" && !IsFree(lic)) continue;
            if (license == "nennung" && IsFree(lic)) continue;
            var file = S("url"); if (file.Length == 0) continue;
            var ext = S("filetype").ToLowerInvariant();
            if (ext.Length == 0) ext = Path.GetExtension(new Uri(file).AbsolutePath).TrimStart('.').ToLowerInvariant();
            if (ext == "jpeg") ext = "jpg";
            if (!(kind == "klang" ? SoundFormats : ImageFormats).Contains(ext)) continue;
            if (format.Length > 0 && ext != format) continue;
            var title = S("title"); if (title.Length == 0) title = "Ohne Titel";
            var src = S("source"); var seconds = kind == "klang" ? Math.Round(L("duration") / 1000.0, 1) : 0;
            res.Add(new("ov-" + S("id"), kind, title, S("creator"), lic, S("license_url"), S("foreign_landing_url"), file, kind == "klang" ? "" : S("thumbnail"), ext,
                L("filesize"), (int)L("width"), (int)L("height"), seconds, "Openverse" + (src.Length > 0 ? " · " + src : "")));
        }
        return res;
    }

    /// <summary>Ergebnisse zweier Quellen abwechselnd mischen (wie Openverse über viele Quellen), doppelte Dateien nur einmal.</summary>
    public static List<MediaHit> Mix(List<MediaHit> a, List<MediaHit> b)
    {
        var res = new List<MediaHit>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
            foreach (var h in new[] { i < a.Count ? a[i] : null, i < b.Count ? b[i] : null })
                if (h is not null && seen.Add(h.FileUrl)) res.Add(h);
        return res;
    }

    /// <summary>Ausrichtung eines Bildes: „quer“, „hoch“, „quadrat“ (±8 %) oder „unbekannt“.</summary>
    public static string Orientation(MediaHit h) => h.Width <= 0 || h.Height <= 0 ? "unbekannt" : Math.Abs(h.Width / (double)h.Height - 1) <= 0.08 ? "quadrat" : h.Width > h.Height ? "quer" : "hoch";

    /// <summary>Filter nach Ausrichtung wie bei Unsplash/Pexels; Mindestbreite in Pixeln (0 = egal). Klänge bleiben unberührt.</summary>
    public static List<MediaHit> Filter(IEnumerable<MediaHit> hits, string orientation = "alle", int minWidth = 0) =>
        [.. hits.Where(h => h.Kind == "klang" || ((orientation == "alle" || Orientation(h) == orientation) && (minWidth <= 0 || h.Width >= minWidth)))];

    /// <summary>Namensnennung als Text, HTML (mit Links) oder Markdown – wie „Diesen Inhalt verwenden“ bei Openverse.</summary>
    public static string CreditAs(MediaHit h, string format)
    {
        string E(string s) => WebUtility.HtmlEncode(s);
        var by = h.Creator.Length > 0 ? h.Creator : "unbekannt";
        return format switch
        {
            "html" => $"<a href=\"{E(h.PageUrl)}\">„{E(h.Title)}“</a> von {E(by)}, " + (h.LicenseUrl.Length > 0 ? $"<a href=\"{E(h.LicenseUrl)}\">{E(h.License)}</a>" : E(h.License)) + $", über {E(h.Provider)}",
            "md" => $"[„{Md(h.Title)}“]({h.PageUrl}) von {Md(by)}, " + (h.LicenseUrl.Length > 0 ? $"[{Md(h.License)}]({h.LicenseUrl})" : Md(h.License)) + $", über {Md(h.Provider)}",
            _ => h.Credit,
        };
        static string Md(string s) => s.Replace("[", "\\[").Replace("]", "\\]");
    }
}

/// <summary>Freie Schrift aus Fontsource (Google Fonts und weitere, meist SIL Open Font License).</summary>
public sealed record FontHit(string Id, string Family, string Category, string License, int[] Weights, bool Variable)
{
    public string Kategorie => FreeFonts.Categories.FirstOrDefault(c => c.Id == Category).Label ?? Category;
}

/// <summary>Freie Schriften finden und laden wie bei Google Fonts (Clean-Room, 04.11.2026): Liste ohne Schlüssel von Fontsource,
/// Suche nach Name und Art, Laden als TTF mit Lizenzdatei in „Schriften“ – nur offene Lizenzen (OFL, Apache, UFL).</summary>
public sealed class FreeFonts(IWebFetch http, string root)
{
    public const string ListUrl = "https://api.fontsource.org/v1/fonts";
    public static readonly (string Id, string Label)[] Categories = [("", "Alle Arten"), ("sans-serif", "Ohne Serifen"), ("serif", "Mit Serifen"), ("display", "Auffällig (Überschriften)"), ("handwriting", "Handschrift"), ("monospace", "Festbreite (Code)")];
    static readonly string[] OpenLicenses = ["ofl", "apache", "ufl", "mit", "cc0"];

    public static bool OpenLicense(string l) => OpenLicenses.Any(x => l.Contains(x, StringComparison.OrdinalIgnoreCase));

    /// <summary>Liste lesen und filtern: Suchwort im Namen (leer = alle), Art, nur offene Lizenzen; Treffer am Wortanfang zuerst.</summary>
    public static List<FontHit> Parse(string json, string query = "", string category = "")
    {
        var q = query.Trim(); var res = new List<FontHit>();
        using var d = JsonDocument.Parse(json);
        if (d.RootElement.ValueKind != JsonValueKind.Array) return res;
        foreach (var f in d.RootElement.EnumerateArray())
        {
            string S(string k) => f.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var fam = S("family"); var cat = S("category"); var lic = S("license");
            if (fam.Length == 0 || !OpenLicense(lic)) continue;
            if (category.Length > 0 && cat != category) continue;
            if (q.Length > 0 && !fam.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            if (f.TryGetProperty("subsets", out var sub) && sub.ValueKind == JsonValueKind.Array && !sub.EnumerateArray().Any(x => x.GetString() == "latin")) continue; // Umlaute brauchen Latin
            var w = f.TryGetProperty("weights", out var ws) && ws.ValueKind == JsonValueKind.Array ? ws.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToArray() : [400];
            res.Add(new(S("id"), fam, cat, lic, w, f.TryGetProperty("variable", out var va) && va.ValueKind == JsonValueKind.True));
        }
        return [.. res.OrderByDescending(h => q.Length > 0 && h.Family.StartsWith(q, StringComparison.OrdinalIgnoreCase)).ThenBy(h => h.Family, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>TTF-Datei einer Stärke (latin, normal) – nächste vorhandene Stärke zu <paramref name="weight"/>.</summary>
    public static string FileUrl(FontHit f, int weight = 400)
    {
        var w = f.Weights.Length == 0 ? 400 : f.Weights.OrderBy(x => Math.Abs(x - weight)).First();
        return $"https://cdn.jsdelivr.net/fontsource/fonts/{Uri.EscapeDataString(f.Id)}@latest/latin-{w}-normal.ttf";
    }

    public async Task<List<FontHit>> Search(string query, string category = "", CancellationToken ct = default) => Parse((await http.GetAsync(ListUrl, ct)).EnsureOk().Text, query, category);

    /// <summary>Normale und fette Stärke laden (falls vorhanden), Lizenzdatei daneben. Gibt den Ordner der Schrift zurück.</summary>
    public async Task<string> Download(FontHit f, CancellationToken ct = default)
    {
        if (!OpenLicense(f.License)) throw new InvalidDataException("Diese Schrift hat keine offene Lizenz – nicht geladen.");
        var safe = string.Concat(f.Family.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' ? c : '_')).Trim();
        var dir = Path.Combine(root, "Schriften", safe.Length == 0 ? "Schrift" : safe); Directory.CreateDirectory(dir);
        foreach (var w in new[] { 400, 700 }.Select(x => f.Weights.Length == 0 ? 400 : f.Weights.OrderBy(y => Math.Abs(y - x)).First()).Distinct())
        {
            var r = (await http.GetAsync(FileUrl(f, w), ct)).EnsureOk();
            if (r.Body.Length < 4 || !(r.Body[0] == 0 && r.Body[1] == 1 || r.Body[0] == (byte)'O' && r.Body[1] == (byte)'T' || r.Body[0] == (byte)'t' && r.Body[1] == (byte)'r'))
                throw new InvalidDataException("Die Datei ist keine Schrift – nicht gespeichert.");
            if (r.Body.Length > 20 << 20) throw new InvalidDataException("Die Schrift ist größer als 20 MB.");
            await File.WriteAllBytesAsync(Path.Combine(dir, $"{safe}-{w}.ttf"), r.Body, ct);
        }
        File.WriteAllText(Path.Combine(dir, "LIZENZ.txt"), $"{f.Family}\nLizenz: {f.License}\nQuelle: Fontsource (fontsource.org) – {FileUrl(f)}\nFrei nutzbar, auch kommerziell; die Schrift selbst nicht verkaufen.\n");
        return dir;
    }
}
