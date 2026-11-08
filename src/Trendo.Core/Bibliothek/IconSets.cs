using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 8 – X-07 „Freie Bilder &amp; Klänge finden“: Symbole aus den Iconify-Symbolsätzen (über 200 Sätze, Lizenz je Satz).
/// Reine Daten (JSON mit SVG-Inhalt) von einem fest gewählten Stand des Repos iconify/icon-sets; Trendo liest sie mit eigenem Code, führt nichts aus
/// und bietet nur Sätze mit freier Lizenz an. Bei CC BY gehört die Namensnennung dazu – Trendo schreibt sie mit.</summary>
public static partial class IconSets
{
    public const string Commit = "3eb33380c7dbc256d098ada3f5a5863ec4b145dc";
    public static string Raw(string path) => $"https://raw.githubusercontent.com/iconify/icon-sets/{Commit}/{path}";
    public static string CollectionsUrl => Raw("collections.json");
    public static string SetUrl(string prefix) => ValidPrefix(prefix) ? Raw($"json/{prefix}.json") : throw new ArgumentException("Ungültiger Symbolsatz.");

    public enum Use { Frei, MitNennung, Ausgeschlossen }

    /// <summary>Lizenzregel: frei nutzbar · mit Namensnennung · ausgeschlossen (nicht-kommerziell, „share-alike“, GPL/MPL-Pflichten, unbekannt).</summary>
    public static Use Policy(string spdx) => spdx switch
    {
        "MIT" or "Apache-2.0" or "ISC" or "BSD-2-Clause" or "BSD-3-Clause" or "Unlicense" or "CC0-1.0" or "OFL-1.1" => Use.Frei,
        "CC-BY-4.0" or "CC-BY-3.0" => Use.MitNennung,
        _ => Use.Ausgeschlossen,
    };

    public sealed record Set(string Prefix, string Name, int Total, string Spdx, string LicenseTitle, string LicenseUrl, string Author, string AuthorUrl, string Category, int Height)
    {
        public Use Use => Policy(Spdx);
        public string Attribution => $"„{Name}“ von {Author} – {LicenseTitle} ({LicenseUrl})";
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")] private static partial Regex PrefixRx();
    public static bool ValidPrefix(string p) => p.Length is > 0 and <= 40 && PrefixRx().IsMatch(p);

    /// <summary>collections.json lesen; Reihenfolge: freie Sätze zuerst, dann nach Größe.</summary>
    public static List<Set> ParseCollections(string json)
    {
        var o = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("Keine Symbolsatz-Liste.");
        var list = new List<Set>();
        foreach (var (prefix, v) in o)
        {
            if (v is not JsonObject s || !ValidPrefix(prefix)) continue;
            var lic = s["license"] as JsonObject; var au = s["author"] as JsonObject;
            list.Add(new(prefix, Str(s["name"], prefix), s["total"]?.GetValue<int>() ?? 0, Str(lic?["spdx"], "?"), Str(lic?["title"], "?"), Str(lic?["url"], ""),
                Str(au?["name"], "?"), Str(au?["url"], ""), Str(s["category"], ""), s["height"] is JsonValue h && h.TryGetValue<int>(out var hh) ? hh : 0));
        }
        return [.. list.OrderBy(x => x.Use).ThenByDescending(x => x.Total)];
    }

    static string Str(JsonNode? n, string def) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : def;

    /// <summary>Ein geladener Symbolsatz: Symbole (Inhalt, Größe) und Aliasse.</summary>
    public sealed class Icons
    {
        readonly JsonObject _icons, _aliases; readonly int _w, _h;
        public string Prefix { get; }
        public Icons(string json)
        {
            var o = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("Kein Symbolsatz.");
            Prefix = Str(o["prefix"], ""); if (!ValidPrefix(Prefix)) throw new FormatException("Ungültiger Symbolsatz.");
            _icons = o["icons"] as JsonObject ?? throw new FormatException("Symbolsatz ohne Symbole.");
            _aliases = o["aliases"] as JsonObject ?? [];
            _w = Int(o["width"], 16); _h = Int(o["height"], 16);
        }
        static int Int(JsonNode? n, int d) => n is JsonValue v && v.TryGetValue<double>(out var x) ? (int)x : d;
        static bool Hidden(JsonNode? n) => n?["hidden"] is JsonValue h && h.TryGetValue<bool>(out var b) && b;
        /// <summary>Sichtbare Symbole (veraltete, als „hidden“ markierte zählen nicht – wie in Iconifys eigener Zählung).</summary>
        public int Count => _icons.Count(k => !Hidden(k.Value));
        public int AliasCount => _aliases.Count(k => !Hidden(k.Value));
        public IEnumerable<string> Names => _icons.Where(k => !Hidden(k.Value)).Select(k => k.Key).Concat(_aliases.Where(k => !Hidden(k.Value)).Select(k => k.Key));

        /// <summary>Suche nach Teilwort (mehrere Wörter = alle müssen vorkommen), höchstens <paramref name="max"/> Treffer, kürzeste Namen zuerst.</summary>
        public List<string> Search(string query, int max = 60)
        {
            var words = (query ?? "").ToLowerInvariant().Split(' ', '-', '_').Where(w => w.Length > 0).ToArray();
            return [.. Names.Where(n => words.All(w => n.Contains(w, StringComparison.Ordinal))).OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).Take(max)];
        }

        /// <summary>SVG eines Symbols (Aliasse mit Spiegeln/Drehen aufgelöst); null, wenn unbekannt oder unsicher.</summary>
        public string? Svg(string name, int size = 24)
        {
            int rotate = 0; bool hFlip = false, vFlip = false; var cur = name;
            for (var depth = 0; depth < 5 && _aliases[cur] is JsonObject a; depth++)
            {
                rotate += Int(a["rotate"], 0); hFlip ^= a["hFlip"]?.GetValue<bool>() == true; vFlip ^= a["vFlip"]?.GetValue<bool>() == true;
                cur = Str(a["parent"], "");
            }
            if (_icons[cur] is not JsonObject icon) return null;
            rotate += Int(icon["rotate"], 0); hFlip ^= icon["hFlip"]?.GetValue<bool>() == true; vFlip ^= icon["vFlip"]?.GetValue<bool>() == true;
            var body = Str(icon["body"], ""); if (!SafeBody(body)) return null;
            int l = Int(icon["left"], 0), t = Int(icon["top"], 0), w = Int(icon["width"], _w), h = Int(icon["height"], _h);
            var tr = new List<string>();
            if (hFlip || vFlip) tr.Add(string.Create(CultureInfo.InvariantCulture, $"translate({(hFlip ? 2 * l + w : 0)} {(vFlip ? 2 * t + h : 0)}) scale({(hFlip ? -1 : 1)} {(vFlip ? -1 : 1)})"));
            if (rotate % 4 != 0) tr.Insert(0, string.Create(CultureInfo.InvariantCulture, $"rotate({rotate % 4 * 90} {l + w / 2.0} {t + h / 2.0})"));
            if (tr.Count > 0) body = $"<g transform=\"{string.Join(" ", tr)}\">{body}</g>";
            var height = size; var width = (int)Math.Round(size * (double)w / h);
            return string.Create(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"{l} {t} {w} {h}\">{body}</svg>");
        }
    }

    [GeneratedRegex(@"<\s*(script|foreignObject|iframe|image)\b|\bon[a-z]+\s*=|javascript:|(?:href|src)\s*=\s*(?:[""'](?!#)|[^""'#\s])|url\(\s*(?:[""'](?!#)|[^""'#\s])", RegexOptions.IgnoreCase)] private static partial Regex UnsafeRx();
    /// <summary>Nur Zeichenanweisungen erlaubt – keine Skripte, Ereignisse, externen Verweise oder eingebetteten Fremdinhalte.</summary>
    public static bool SafeBody(string body) => body.Length is > 0 and < 200_000 && !UnsafeRx().IsMatch(body);

    /// <summary>Symbol als SVG speichern; bei CC BY die Namensnennung in „Lizenzen.txt“ im selben Ordner ergänzen.</summary>
    public static string Save(string folder, Set set, Icons icons, string name, int size = 64)
    {
        if (set.Use == Use.Ausgeschlossen) throw new InvalidOperationException($"„{set.Name}“ ({set.Spdx}) ist für Trendo nicht frei genug.");
        var svg = icons.Svg(name, size) ?? throw new InvalidOperationException("Dieses Symbol gibt es nicht oder es ist nicht sicher darstellbar.");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{set.Prefix}-{string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-'))}.svg");
        File.WriteAllText(file, svg);
        var line = $"{Path.GetFileName(file)}: {set.Attribution}";
        var lic = Path.Combine(folder, "Lizenzen.txt");
        var have = File.Exists(lic) ? File.ReadAllLines(lic) : [];
        if (!have.Contains(line)) File.AppendAllLines(lic, [line]);
        return file;
    }
}
