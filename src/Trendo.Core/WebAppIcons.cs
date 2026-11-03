using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Wunsch 03.11.2026: WhatsApp, Telegram & Co. mit ihrem Original-Symbol in der Leiste. Wie Browser bei installierten Web-Apps:
/// das offizielle App-Symbol von der eigenen Seite des Dienstes (Link-Tags, Web-App-Manifest, apple-touch-icon, favicon). Trendo liefert
/// kein fremdes Logo mit und verändert keins – das Symbol wird bei der Person zwischengespeichert. Hier nur Auswahl und Prüfung (ohne Netzwerk).</summary>
public static partial class WebAppIcons
{
    /// <summary>Erlaubte Quellen je App: eigene Domains und CDNs des Dienstes.</summary>
    public static readonly Dictionary<string, string[]> Hosts = new()
    {
        ["whatsapp"] = ["whatsapp.com", "whatsapp.net"],
        ["telegram"] = ["telegram.org"],
        ["tiktok"] = ["tiktok.com", "tiktokcdn.com", "tiktokcdn-us.com"],
        ["instagram"] = ["instagram.com", "cdninstagram.com"],
        ["facebook"] = ["facebook.com", "fbcdn.net"],
    };

    public static bool Allowed(string appId, Uri u) =>
        u.Scheme == Uri.UriSchemeHttps && Hosts.TryGetValue(appId, out var hs) && hs.Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex LinkRx();
    [GeneratedRegex(@"\b(rel|href|sizes)\s*=\s*(""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)] private static partial Regex AttrRx();

    /// <summary>Symbol-Adressen aus der Startseite (größte zuerst); dazu die Adresse des Manifests, falls genannt.</summary>
    public static (List<Uri> Icons, Uri? Manifest) FromHtml(string html, Uri page)
    {
        var icons = new List<(Uri U, int Size, int Rank)>(); Uri? manifest = null;
        foreach (Match m in LinkRx().Matches(html))
        {
            var a = AttrRx().Matches(m.Value).ToDictionary(x => x.Groups[1].Value.ToLowerInvariant(), x => System.Net.WebUtility.HtmlDecode(x.Groups[3].Success ? x.Groups[3].Value : x.Groups[4].Success ? x.Groups[4].Value : x.Groups[5].Value));
            if (!a.TryGetValue("rel", out var rel) || !a.TryGetValue("href", out var href) || !Uri.TryCreate(page, href, out var u)) continue;
            rel = rel.ToLowerInvariant();
            if (rel == "manifest") { manifest = u; continue; }
            if (!rel.Contains("icon")) continue;
            if (u.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue; // SVG: nicht als Bild ladbar
            icons.Add((u, Size(a.GetValueOrDefault("sizes")), rel.Contains("apple-touch") ? 1 : 0));
        }
        return (icons.OrderByDescending(x => x.Size).ThenByDescending(x => x.Rank).Select(x => x.U).Distinct().ToList(), manifest);
    }

    /// <summary>Symbol-Adressen aus einem Web-App-Manifest (größte zuerst, ohne SVG).</summary>
    public static List<Uri> FromManifest(string json, Uri manifestUrl)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("icons", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
            return arr.EnumerateArray()
                .Select(e => (Src: e.TryGetProperty("src", out var s) ? s.GetString() : null, Size: e.TryGetProperty("sizes", out var z) ? Size(z.GetString()) : 0))
                .Where(x => x.Src is not null && !x.Src.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(manifestUrl, x.Src, out _))
                .OrderByDescending(x => x.Size).Select(x => new Uri(manifestUrl, x.Src!)).Distinct().ToList();
        }
        catch (JsonException) { return []; }
    }

    /// <summary>Feste Orte, die fast jeder Dienst hat (zum Schluss versuchen).</summary>
    public static IEnumerable<Uri> Fallbacks(Uri start) => [new Uri(start, "/apple-touch-icon.png"), new Uri(start, "/favicon.ico")];

    static int Size(string? sizes) =>
        (sizes ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s.Split('x', 'X')[0], out var n) ? n : 0).DefaultIfEmpty(0).Max();

    /// <summary>Ist das ein Bild (PNG, JPEG, WebP, GIF, ICO) mit vernünftiger Größe? Gibt die Dateiendung zurück, sonst null.</summary>
    public static string? ImageKind(byte[] b)
    {
        if (b.Length < 200 || b.Length > 2_000_000) return null;
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b[0] == 0xFF && b[1] == 0xD8) return ".jpg";
        if (b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ".webp";
        if (b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return ".gif";
        if (b[0] == 0 && b[1] == 0 && b[2] == 1 && b[3] == 0) return ".ico";
        return null;
    }

    /// <summary>Zwischengespeichertes Symbol einer App (oder null).</summary>
    public static string? Cached(string dataRoot, string appId) =>
        new[] { ".png", ".webp", ".jpg", ".gif", ".ico" }.Select(e => Path.Combine(WebApps.ProfileDir(dataRoot, appId), "symbol" + e)).FirstOrDefault(File.Exists);

    /// <summary>Symbol speichern (ersetzt ein älteres).</summary>
    public static string Store(string dataRoot, string appId, byte[] data)
    {
        var ext = ImageKind(data) ?? throw new ArgumentException("Kein gültiges Bild.");
        var dir = WebApps.ProfileDir(dataRoot, appId); Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "symbol.*")) File.Delete(old);
        var f = Path.Combine(dir, "symbol" + ext); File.WriteAllBytes(f, data);
        return f;
    }
}
