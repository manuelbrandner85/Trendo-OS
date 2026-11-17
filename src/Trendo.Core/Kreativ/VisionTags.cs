using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Kreativ;

/// <summary>T-0699 – „Bilder erkennen“ erkennt wirklich: Ergebnis je Bild aus drei echten, lokalen Quellen –
/// Objekte mit Rahmen (YOLOX-S über <see cref="ObjectDetect"/>), Bildbeschreibung mit Schlagworten (Bildverstehens-Modell in Ollama oder Claude über das Abo)
/// und Text im Bild (Tesseract). Daraus entstehen Schlagworte, eine Suche („zeig alle Fotos mit Hund“), eine Seitendatei je Foto (XMP, das Foto selbst bleibt
/// unverändert) und eine CSV-Liste. Fehlt eine Quelle, bleibt ihr Teil leer und das Ergebnis sagt, was gelaufen ist – nichts wird erfunden.</summary>
public static partial class VisionTags
{
    public sealed record Box(string Label, double Score, int X1, int Y1, int X2, int Y2);

    /// <summary>Erkennung eines Bildes. <paramref name="Sources"/>: welche Werkzeuge wirklich gelaufen sind (z. B. „YOLOX-S“, „qwen2.5vl:7b“, „Tesseract“).</summary>
    public sealed record Recognition(string File, string Sha256, int Width, int Height, IReadOnlyList<string> Tags, string Description, IReadOnlyList<Box> Objects,
        string Text, IReadOnlyList<string> Sources, DateTime CreatedUtc);

    public const string SidecarMarker = "trendo:erkennung";
    public const int MaxTags = 24;

    /// <summary>Frage an das Bildverstehens-Modell: kurze Beschreibung und eine feste Schlagwort-Zeile, die <see cref="ParseDescription"/> liest.</summary>
    public const string DescribeQuestion = "Beschreibe dieses Foto sachlich auf Deutsch in höchstens zwei Sätzen: was ist zu sehen, wo, welche Stimmung. " +
        "Nenne keine Namen von Personen und rate keine Identität. Schreibe danach eine eigene Zeile, die mit „Schlagworte:“ beginnt, " +
        "mit 5 bis 10 deutschen Schlagworten (Einzahl, durch Komma getrennt), z. B. „Schlagworte: Hund, Wiese, Ball, Sommer, draußen“.";

    [GeneratedRegex(@"^\W*(?:schlagw(?:o|ö)rter?|stichw(?:o|ö)rter?|tags|keywords)\W*\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TagLineRx();

    /// <summary>T-0703: Qwen 2.5 VL schreibt die Schlagworte oft ohne Zeilenumbruch hinter den letzten Satz („… alltäglich. Schlagworte: Hund, …“).</summary>
    [GeneratedRegex(@"(?<=[.!?…])\s*\W*(?:schlagw(?:o|ö)rter?|stichw(?:o|ö)rter?|tags|keywords)\W*\s*:\s*([^\n]+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TagInlineRx();

    /// <summary>Antwort des Modells in Beschreibung und Schlagworte trennen. Ohne Schlagwort-Zeile: nur Beschreibung, keine erfundenen Schlagworte.</summary>
    public static (string Description, IReadOnlyList<string> Tags) ParseDescription(string? answer)
    {
        var a = (answer ?? "").Replace("\r", "").Trim();
        if (a.Length == 0) return ("", []);
        var m = TagLineRx().Match(a);
        if (!m.Success) m = TagInlineRx().Match(a);
        var tags = m.Success ? CleanTags(m.Groups[1].Value.Split([',', ';', '·', '|'])) : [];
        var desc = (m.Success ? a.Remove(m.Index, m.Length) : a).Replace("**", "").Trim();
        desc = Regex.Replace(desc, @"^\W*(?:beschreibung)\s*:\s*", "", RegexOptions.IgnoreCase).Trim();
        desc = Regex.Replace(desc, @"\s*\n\s*", " ");
        return (KreativSafe.Cut(desc, 1200), tags);
    }

    static List<string> CleanTags(IEnumerable<string> raw)
    {
        var list = new List<string>();
        foreach (var r in raw)
        {
            var t = Regex.Replace(r, @"[„“""'\.\*#\[\]\(\)]", "").Trim();
            if (t.Length is < 2 or > 40 || t.Any(char.IsControl)) continue;
            t = char.ToUpper(t[0], CultureInfo.GetCultureInfo("de-DE")) + t[1..];
            if (!list.Contains(t, StringComparer.OrdinalIgnoreCase)) list.Add(t);
        }
        return list.Take(MaxTags).ToList();
    }

    /// <summary>Erkennung zusammenführen: Objekte (deutsche Einzahl, sicherste zuerst) vor den Schlagworten der Beschreibung; doppelte Wörter nur einmal.</summary>
    public static Recognition Build(string file, string sha, int width, int height, ObjectDetect.Result? objects, string description, IReadOnlyList<string> descTags, string text,
        IReadOnlyList<string> sources, DateTime createdUtc)
    {
        var boxes = (objects?.Hits ?? []).OrderByDescending(h => h.Score).Select(h => new Box(h.Label, Math.Round(h.Score, 3), h.X1, h.Y1, h.X2, h.Y2)).ToList();
        var tags = CleanTags(boxes.Select(b => b.Label).Concat(descTags));
        if (text.Trim().Length > 0 && !tags.Contains("Text", StringComparer.OrdinalIgnoreCase)) tags.Add("Text");
        return new(file, sha, width, height, tags.Take(MaxTags).ToList(), description.Trim(), boxes, text.Trim(), sources, createdUtc);
    }

    // ---------- Suche ----------

    static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "zeig", "zeige", "zeigt", "such", "suche", "finde", "find", "alle", "allen", "jede", "jedes", "die", "der", "das", "den", "dem", "des", "ein", "eine", "einen", "einem", "einer",
        "mit", "auf", "in", "im", "am", "an", "von", "vom", "und", "oder", "wo", "wo's", "denen", "dem", "drauf", "darauf", "zu", "sehen", "sieht", "man", "ist", "sind", "gibt", "es",
        "foto", "fotos", "bild", "bilder", "aufnahme", "aufnahmen", "photo", "photos", "mir", "bitte", "meine", "meinen", "nur", "welche", "welches", "was",
    };

    /// <summary>Suchbegriffe aus einer Alltagsfrage: „zeig alle Fotos mit Hund“ → [hund].</summary>
    public static IReadOnlyList<string> Terms(string query) =>
        Regex.Split((query ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(t => t.Length >= 2 && !Stop.Contains(t)).Distinct().ToList();

    /// <summary>Einzahl und Mehrzahl gehören zusammen (Hund/Hunde, Katze/Katzen, Kuh/Kühe – aus der Objektliste, sonst einfache Endungen).</summary>
    static IEnumerable<string> Forms(string term)
    {
        yield return term;
        foreach (var (one, many) in ObjectDetect.German.Values)
        {
            if (string.Equals(one, term, StringComparison.OrdinalIgnoreCase)) yield return many.ToLowerInvariant();
            if (string.Equals(many, term, StringComparison.OrdinalIgnoreCase)) yield return one.ToLowerInvariant();
        }
        foreach (var end in new[] { "en", "n", "e", "s", "er" })
            if (term.Length > end.Length + 2 && term.EndsWith(end, StringComparison.Ordinal)) yield return term[..^end.Length];
    }

    static bool WordHit(string haystack, string form) =>
        Regex.IsMatch(haystack, @"(?<![\p{L}\p{N}])" + Regex.Escape(form) + @"(?:e|en|n|s|er|es)?(?![\p{L}\p{N}])", RegexOptions.IgnoreCase);

    /// <summary>Jeder Suchbegriff muss in Schlagworten, Objekten, Beschreibung oder Text im Bild vorkommen (ganze Wörter; „Hund“ findet nicht „Hundert“).</summary>
    public static bool Matches(Recognition r, string query)
    {
        var terms = Terms(query);
        if (terms.Count == 0) return true;
        var hay = string.Join(" | ", r.Tags.Concat(r.Objects.Select(o => o.Label)).Append(r.Description).Append(r.Text).Append(Path.GetFileNameWithoutExtension(r.File)));
        return terms.All(t => Forms(t).Any(f => WordHit(hay, f)));
    }

    public static List<Recognition> Search(IEnumerable<Recognition> list, string query) => list.Where(r => Matches(r, query)).ToList();

    // ---------- Seitendatei (XMP) ----------

    /// <summary>Seitendatei neben dem Foto: „foto.jpg.xmp“ (so lesen digiKam, darktable und Bildverwaltungen die Schlagworte). Das Foto bleibt unverändert.</summary>
    public static string SidecarPath(string image) => image + ".xmp";

    static string X(string s) => SecurityElement.Escape(s) ?? "";

    public static string Xmp(Recognition r)
    {
        var sb = new StringBuilder();
        sb.Append("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n<x:xmpmeta xmlns:x=\"adobe:ns:meta/\" x:xmptk=\"Trendo OS\">\n");
        sb.Append(" <rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");
        sb.Append("  <rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:trendo=\"https://trendo-os.local/ns/erkennung/1.0/\"");
        sb.Append(CultureInfo.InvariantCulture, $" trendo:erkennung=\"1\" trendo:sha256=\"{X(r.Sha256)}\" trendo:quellen=\"{X(string.Join(", ", r.Sources))}\" trendo:erstellt=\"{r.CreatedUtc:yyyy-MM-ddTHH:mm:ssZ}\">\n");
        if (r.Tags.Count > 0)
        {
            sb.Append("   <dc:subject><rdf:Bag>\n");
            foreach (var t in r.Tags) sb.Append("    <rdf:li>").Append(X(t)).Append("</rdf:li>\n");
            sb.Append("   </rdf:Bag></dc:subject>\n");
        }
        if (r.Description.Length > 0) sb.Append("   <dc:description><rdf:Alt><rdf:li xml:lang=\"de\">").Append(X(r.Description)).Append("</rdf:li></rdf:Alt></dc:description>\n");
        if (r.Objects.Count > 0)
        {
            sb.Append("   <trendo:objekte><rdf:Seq>\n");
            foreach (var o in r.Objects) sb.Append(CultureInfo.InvariantCulture, $"    <rdf:li>{X(o.Label)} {o.Score:0.00} {o.X1},{o.Y1},{o.X2},{o.Y2}</rdf:li>\n");
            sb.Append("   </rdf:Seq></trendo:objekte>\n");
        }
        if (r.Text.Length > 0) sb.Append("   <trendo:text>").Append(X(KreativSafe.Cut(r.Text, 4000))).Append("</trendo:text>\n");
        sb.Append("  </rdf:Description>\n </rdf:RDF>\n</x:xmpmeta>\n<?xpacket end=\"w\"?>\n");
        return sb.ToString();
    }

    /// <summary>Seitendatei schreiben. Eine fremde Seitendatei (z. B. von einer Bildverwaltung) wird nie überschrieben – dann false.</summary>
    public static bool WriteSidecar(Recognition r)
    {
        var p = SidecarPath(r.File);
        if (File.Exists(p) && !File.ReadAllText(p).Contains(SidecarMarker + "=\"1\"", StringComparison.Ordinal)) return false;
        AtomicFile.WriteAllText(p, Xmp(r));
        return true;
    }

    /// <summary>Eigene Seitendatei wieder lesen (nur wenn das Foto noch dasselbe ist – Prüfsumme). Fremde oder veraltete Seitendateien: null.</summary>
    public static (IReadOnlyList<string> Tags, string Description)? ReadSidecar(string image, string sha)
    {
        var p = SidecarPath(image);
        if (!File.Exists(p)) return null;
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(File.ReadAllText(p));
            System.Xml.Linq.XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#", dc = "http://purl.org/dc/elements/1.1/", tr = "https://trendo-os.local/ns/erkennung/1.0/";
            var d = doc.Descendants(rdf + "Description").FirstOrDefault();
            if (d is null || (string?)d.Attribute(tr + "erkennung") != "1" || (string?)d.Attribute(tr + "sha256") != sha) return null;
            var tags = d.Element(dc + "subject")?.Descendants(rdf + "li").Select(x => x.Value).ToList() ?? [];
            var desc = d.Element(dc + "description")?.Descendants(rdf + "li").FirstOrDefault()?.Value ?? "";
            return (tags, desc);
        }
        catch (System.Xml.XmlException) { return null; }
    }

    // ---------- Doppelte ----------

    /// <summary>Gleiche Fotos (gleiche Prüfsumme) als Gruppen, größte Gruppe zuerst.</summary>
    public static List<List<string>> Duplicates(IEnumerable<Recognition> list) =>
        list.Where(r => r.Sha256.Length > 0).GroupBy(r => r.Sha256).Where(g => g.Count() > 1).Select(g => g.Select(r => r.File).Order(StringComparer.OrdinalIgnoreCase).ToList())
            .OrderByDescending(g => g.Count).ToList();

    // ---------- Speichern im Dokument, CSV ----------

    public static JsonObject ToJson(Recognition r) => new()
    {
        ["file"] = r.File, ["sha"] = r.Sha256, ["w"] = r.Width, ["h"] = r.Height, ["tags"] = new JsonArray(r.Tags.Select(t => (JsonNode)t).ToArray()),
        ["desc"] = r.Description, ["text"] = r.Text, ["sources"] = new JsonArray(r.Sources.Select(t => (JsonNode)t).ToArray()), ["created"] = r.CreatedUtc.ToString("O", CultureInfo.InvariantCulture),
        ["objects"] = new JsonArray(r.Objects.Select(o => (JsonNode)new JsonObject { ["label"] = o.Label, ["score"] = o.Score, ["box"] = new JsonArray(o.X1, o.Y1, o.X2, o.Y2) }).ToArray()),
    };

    /// <summary>Aus dem Dokument lesen; kaputte Einträge werden übersprungen.</summary>
    public static Recognition? FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        try
        {
            static string S(JsonNode? x) => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
            static int I(JsonNode? x) => x is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;
            var objs = (o["objects"] as JsonArray ?? []).OfType<JsonObject>().Select(b =>
            {
                var box = (b["box"] as JsonArray ?? []).Select(I).ToArray();
                return box.Length == 4 ? new Box(S(b["label"]), b["score"] is JsonValue sv && sv.TryGetValue<double>(out var sc) ? sc : 0, box[0], box[1], box[2], box[3]) : null;
            }).OfType<Box>().ToList();
            var file = S(o["file"]);
            if (file.Length == 0) return null;
            return new(file, S(o["sha"]), I(o["w"]), I(o["h"]), (o["tags"] as JsonArray ?? []).Select(S).Where(t => t.Length > 0).ToList(), S(o["desc"]), objs, S(o["text"]),
                (o["sources"] as JsonArray ?? []).Select(S).Where(t => t.Length > 0).ToList(),
                DateTime.TryParse(S(o["created"]), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var c) ? c : DateTime.MinValue);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or JsonException) { return null; }
    }

    static string C(string s)
    {
        if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
        return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Liste mit Semikolon und Schlagworten (für Tabellenprogramme und Bildverwaltungen).</summary>
    public static string Csv(IReadOnlyList<Recognition> list)
    {
        var sb = new StringBuilder("Datei;Ordner;Schlagworte;Beschreibung;Objekte;Text im Bild;Erkannt mit\r\n");
        foreach (var r in list)
        {
            var objs = string.Join(", ", r.Objects.GroupBy(o => o.Label).Select(g => $"{g.Count()}× {g.Key}"));
            sb.Append(string.Join(";", C(Path.GetFileName(r.File)), C(Path.GetDirectoryName(r.File) ?? ""), C(string.Join(", ", r.Tags)), C(r.Description), C(objs),
                C(Regex.Replace(r.Text, @"\s+", " ").Trim()), C(string.Join(", ", r.Sources)))).Append("\r\n");
        }
        return sb.ToString();
    }
}
