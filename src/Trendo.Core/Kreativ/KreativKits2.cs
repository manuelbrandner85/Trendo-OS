using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Trendo.Core.Sys;

namespace Trendo.Core.Kreativ;

/// <summary>Bücher schreiben: Manuskript in Kapiteln („# Titel“), Statistik je Kapitel und E-Book im offenen EPUB-3-Format.</summary>
public static partial class BookKit
{
    public sealed record Chapter(string Title, string Body, int Words);
    public sealed record Stats(int Words, int Pages, int ReadMinutes, int Chapters, double GoalPercent);

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'’\-]*")]
    private static partial Regex WordRx();

    public static int Words(string text) => WordRx().Matches(text ?? "").Count;

    /// <summary>Kapitel beginnen mit „# Titel“. Text davor wird zum Kapitel „Vorwort“.</summary>
    public static IReadOnlyList<Chapter> Chapters(string manuscript)
    {
        var list = new List<Chapter>(); string? title = null; var body = new StringBuilder();
        void Flush()
        {
            var b = body.ToString().Trim();
            if (title is not null || b.Length > 0) list.Add(new(title ?? "Vorwort", b, Words(b)));
            body.Clear();
        }
        foreach (var raw in (manuscript ?? "").Replace("\r", "").Split('\n'))
        {
            if (raw.StartsWith("# ", StringComparison.Ordinal)) { Flush(); title = raw[2..].Trim(); if (title.Length == 0) title = $"Kapitel {list.Count + 1}"; }
            else body.Append(raw).Append('\n');
        }
        Flush();
        return list;
    }

    /// <summary>Normseite mit etwa 250 Wörtern, Lesetempo etwa 200 Wörter je Minute.</summary>
    public static Stats Of(string manuscript, int goal)
    {
        var ch = Chapters(manuscript); var w = ch.Sum(c => c.Words);
        return new(w, Math.Max(w == 0 ? 0 : 1, (int)Math.Ceiling(w / 250.0)), Math.Max(w == 0 ? 0 : 1, (int)Math.Ceiling(w / 200.0)), ch.Count,
            goal > 0 ? Math.Round(Math.Min(100, 100.0 * w / goal), 1) : 0);
    }

    /// <summary>Nächste Kapitelüberschrift für „Neues Kapitel“.</summary>
    public static string NextChapter(string manuscript) => $"{(manuscript ?? "").TrimEnd()}\n\n# Kapitel {Chapters(manuscript).Count(c => c.Title != "Vorwort") + 1}\n\n";

    static string Inline(string s)
    {
        var x = KreativSafe.Xml(s);
        x = Regex.Replace(x, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
        x = Regex.Replace(x, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        x = Regex.Replace(x, @"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", "<em>$1</em>");
        // Verschachtelte Sternchen können ungültiges XHTML ergeben: dann lieber ohne Hervorhebung
        try { _ = System.Xml.Linq.XElement.Parse("<p>" + x + "</p>"); return x; }
        catch (System.Xml.XmlException) { return KreativSafe.Xml(s); }
    }

    static string Xhtml(string title, string inner) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!DOCTYPE html>\n<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" xml:lang=\"de\" lang=\"de\">\n<head><meta charset=\"utf-8\"/><title>{KreativSafe.Xml(title)}</title></head>\n<body>\n{inner}\n</body>\n</html>\n";

    static string Paragraphs(string body) =>
        string.Join("\n", Regex.Split(body ?? "", @"\n\s*\n").Select(p => p.Trim()).Where(p => p.Length > 0)
            .Select(p => p.StartsWith("## ", StringComparison.Ordinal) ? $"<h2>{Inline(p[3..])}</h2>" : $"<p>{string.Join("<br/>", p.Split('\n').Select(Inline))}</p>"));

    /// <summary>Feste Kennung aus Titel und Autor, damit eine neue Fassung als dasselbe Buch erkannt wird.</summary>
    public static string BookId(string title, string author)
    {
        var h = SHA256.HashData(Encoding.UTF8.GetBytes(title + "\n" + author));
        h[6] = (byte)((h[6] & 0x0F) | 0x50); h[8] = (byte)((h[8] & 0x3F) | 0x80);
        return "urn:uuid:" + new Guid(h.AsSpan(0, 16), bigEndian: true).ToString();
    }

    /// <summary>E-Book (EPUB 3): mimetype unkomprimiert zuerst, Inhaltsverzeichnis, ein XHTML je Kapitel.</summary>
    public static byte[] Epub(string title, string author, string manuscript, DateTimeOffset modified)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Gib dem Buch einen Titel.");
        var ch = Chapters(manuscript);
        if (ch.Count == 0) throw new InvalidOperationException("Das Buch hat noch keinen Text.");
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Put(string name, string text, CompressionLevel level = CompressionLevel.Optimal)
            {
                using var w = new StreamWriter(zip.CreateEntry(name, level).Open(), new UTF8Encoding(false));
                w.Write(text);
            }
            Put("mimetype", "application/epub+zip", CompressionLevel.NoCompression);
            Put("META-INF/container.xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>\n");
            var t = KreativSafe.Xml(title.Trim()); var a = KreativSafe.Xml(author.Trim());
            var items = new StringBuilder(); var spine = new StringBuilder(); var nav = new StringBuilder();
            for (var i = 0; i < ch.Count; i++)
            {
                var id = $"k{i + 1:000}";
                Put($"OEBPS/{id}.xhtml", Xhtml(ch[i].Title, $"<section epub:type=\"chapter\"><h1>{KreativSafe.Xml(ch[i].Title)}</h1>\n{Paragraphs(ch[i].Body)}</section>"));
                items.Append(CultureInfo.InvariantCulture, $"<item id=\"{id}\" href=\"{id}.xhtml\" media-type=\"application/xhtml+xml\"/>");
                spine.Append(CultureInfo.InvariantCulture, $"<itemref idref=\"{id}\"/>");
                nav.Append(CultureInfo.InvariantCulture, $"<li><a href=\"{id}.xhtml\">{KreativSafe.Xml(ch[i].Title)}</a></li>");
            }
            Put("OEBPS/nav.xhtml", Xhtml("Inhalt", $"<nav epub:type=\"toc\" id=\"toc\"><h1>Inhalt</h1><ol>{nav}</ol></nav>"));
            Put("OEBPS/content.opf",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"bookid\" xml:lang=\"de\">" +
                $"<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"bookid\">{BookId(title.Trim(), author.Trim())}</dc:identifier><dc:title>{t}</dc:title><dc:language>de</dc:language>" +
                (a.Length > 0 ? $"<dc:creator>{a}</dc:creator>" : "") +
                $"<meta property=\"dcterms:modified\">{modified.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}</meta></metadata>" +
                $"<manifest><item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>{items}</manifest><spine>{spine}</spine></package>\n");
        }
        return ms.ToArray();
    }
}

/// <summary>Animation &amp; Motion: Ebenen mit Schlüsselbildern je Eigenschaft, Übergänge (Easing), Abtasten und animiertes SVG.</summary>
public static class AnimKit
{
    public sealed record Key(double T, double V, string Ease = "ein-aus");
    public sealed record Layer(string Id, string Shape, string Text, string Color, double W, double H, Dictionary<string, List<Key>> Tracks);

    public static readonly IReadOnlyList<string> Props = ["x", "y", "skalierung", "drehung", "deckkraft"];
    public static readonly IReadOnlyList<(string Id, string Label)> Eases = [("linear", "Gleichmäßig"), ("ein", "Langsam anfangen"), ("aus", "Langsam enden"), ("ein-aus", "Weich"), ("federnd", "Federnd")];
    public static readonly IReadOnlyList<string> Shapes = ["rechteck", "kreis", "text"];
    public const double StageW = 1280, StageH = 720;

    public static double Default(string prop) => prop switch { "skalierung" or "deckkraft" => 1, "x" => StageW / 2, "y" => StageH / 2, _ => 0 };

    public static double Ease(string ease, double p)
    {
        p = Math.Clamp(p, 0, 1);
        return ease switch
        {
            "linear" => p,
            "ein" => p * p,
            "aus" => 1 - (1 - p) * (1 - p),
            "federnd" => Bounce(p),
            _ => p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2,
        };
    }

    static double Bounce(double p)
    {
        const double n = 7.5625, d = 2.75;
        if (p < 1 / d) return n * p * p;
        if (p < 2 / d) { p -= 1.5 / d; return n * p * p + 0.75; }
        if (p < 2.5 / d) { p -= 2.25 / d; return n * p * p + 0.9375; }
        p -= 2.625 / d; return n * p * p + 0.984375;
    }

    /// <summary>Wert zur Zeit t: vor dem ersten Schlüssel der erste, nach dem letzten der letzte, dazwischen mit dem Übergang des früheren Schlüssels.</summary>
    public static double Value(IReadOnlyList<Key>? keys, string prop, double t)
    {
        if (keys is null || keys.Count == 0) return Default(prop);
        var k = keys.OrderBy(x => x.T).ToList();
        if (t <= k[0].T) return k[0].V;
        if (t >= k[^1].T) return k[^1].V;
        for (var i = 0; i < k.Count - 1; i++)
            if (t <= k[i + 1].T)
            {
                var span = k[i + 1].T - k[i].T;
                var p = span <= 0 ? 1 : (t - k[i].T) / span;
                return k[i].V + (k[i + 1].V - k[i].V) * Ease(k[i].Ease, p);
            }
        return k[^1].V;
    }

    public static double ValueOf(Layer l, string prop, double t) => Value(l.Tracks.GetValueOrDefault(prop), prop, t);

    /// <summary>Setzt oder ersetzt den Schlüssel einer Eigenschaft zur Zeit t (gleiche Zeit auf 1/100 s).</summary>
    public static void SetKey(Layer l, string prop, double t, double v, string ease = "ein-aus")
    {
        if (!Props.Contains(prop)) throw new ArgumentException("Unbekannte Eigenschaft: " + prop);
        if (!l.Tracks.TryGetValue(prop, out var list)) l.Tracks[prop] = list = [];
        list.RemoveAll(k => Math.Abs(k.T - t) < 0.005);
        list.Add(new(Math.Round(Math.Max(0, t), 2), v, Eases.Any(e => e.Id == ease) ? ease : "ein-aus"));
        list.Sort((a, b) => a.T.CompareTo(b.T));
    }

    /// <summary>Länge der Animation: letzter Schlüssel, mindestens 1 Sekunde.</summary>
    public static double Duration(IEnumerable<Layer> layers) =>
        Math.Max(1, layers.SelectMany(l => l.Tracks.Values).SelectMany(k => k).Select(k => k.T).DefaultIfEmpty(0).Max());

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    static string ShapeSvg(Layer l) => l.Shape switch
    {
        "kreis" => $"<ellipse cx=\"0\" cy=\"0\" rx=\"{F(l.W / 2)}\" ry=\"{F(l.H / 2)}\" fill=\"{l.Color}\"/>",
        "text" => $"<text x=\"0\" y=\"0\" font-size=\"{F(l.H)}\" text-anchor=\"middle\" dominant-baseline=\"central\" font-family=\"Segoe UI\" font-weight=\"700\" fill=\"{l.Color}\">{KreativSafe.Xml(l.Text)}</text>",
        _ => $"<rect x=\"{F(-l.W / 2)}\" y=\"{F(-l.H / 2)}\" width=\"{F(l.W)}\" height=\"{F(l.H)}\" rx=\"12\" fill=\"{l.Color}\"/>",
    };

    /// <summary>Animiertes SVG (SMIL), abgetastet mit <paramref name="fps"/> Bildern je Sekunde, läuft in Browsern in Schleife.</summary>
    public static string Svg(IReadOnlyList<Layer> layers, string background, int fps = 30)
    {
        var dur = Duration(layers); var n = Math.Clamp((int)Math.Ceiling(dur * fps), 2, 3000);
        var times = Enumerable.Range(0, n + 1).Select(i => dur * i / n).ToList();
        var keyTimes = string.Join(";", times.Select(t => (t / dur).ToString("0.######", CultureInfo.InvariantCulture)));
        string Values(Func<double, string> f) => string.Join(";", times.Select(f));
        var bg = LogoKit.IsHex(background) ? background : "#101827";
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(StageW)}\" height=\"{F(StageH)}\" viewBox=\"0 0 {F(StageW)} {F(StageH)}\"><rect width=\"100%\" height=\"100%\" fill=\"{bg}\"/>");
        var common = $"dur=\"{F(dur)}s\" repeatCount=\"indefinite\" calcMode=\"linear\" keyTimes=\"{keyTimes}\"";
        foreach (var l in layers)
        {
            if (!LogoKit.IsHex(l.Color)) throw new ArgumentException($"Farbe der Ebene „{l.Id}“ als #RRGGBB angeben.");
            sb.Append("<g>");
            sb.Append($"<animateTransform attributeName=\"transform\" type=\"translate\" {common} values=\"{Values(t => F(ValueOf(l, "x", t)) + " " + F(ValueOf(l, "y", t)))}\"/>");
            sb.Append("<g>");
            sb.Append($"<animateTransform attributeName=\"transform\" type=\"rotate\" {common} values=\"{Values(t => F(ValueOf(l, "drehung", t)))}\"/>");
            sb.Append($"<animateTransform attributeName=\"transform\" type=\"scale\" additive=\"sum\" {common} values=\"{Values(t => F(ValueOf(l, "skalierung", t)))}\"/>");
            sb.Append($"<g><animate attributeName=\"opacity\" {common} values=\"{Values(t => F(Math.Clamp(ValueOf(l, "deckkraft", t), 0, 1)))}\"/>{ShapeSvg(l)}</g>");
            sb.Append("</g></g>");
        }
        return sb.Append("</svg>").ToString();
    }

    /// <summary>Bildfolge zu Video (H.264, für jeden Player).</summary>
    public static IReadOnlyList<string> VideoArgs(string framePattern, int fps, string output) =>
        ["-y", "-framerate", fps.ToString(CultureInfo.InvariantCulture), "-i", framePattern, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart", output];
}

/// <summary>Fotos verwalten: Plan zum Einsortieren nach Aufnahmedatum in einen Zielordner, wahlweise umbenennen, ohne zu überschreiben.</summary>
public static class PhotoOrganizer
{
    public static readonly IReadOnlyList<(string Id, string Label)> Schemes = [("jahr-monat", "Jahr / Monat"), ("jahr", "Nur Jahr"), ("tag", "Jahr / Tag")];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static string Folder(DateTime d, string scheme) => scheme switch
    {
        "jahr" => d.ToString("yyyy", De),
        "tag" => Path.Combine(d.ToString("yyyy", De), d.ToString("yyyy-MM-dd", De)),
        _ => Path.Combine(d.ToString("yyyy", De), d.ToString("MM", De) + " " + d.ToString("MMMM", De)),
    };

    public static string FileName(Photo p, bool rename) => rename ? p.Taken.ToString("yyyy-MM-dd HH-mm-ss", De) + Path.GetExtension(p.Path).ToLowerInvariant() : Path.GetFileName(p.Path);

    /// <summary>Plan: Ziel „Zielordner/Schema/Datei“. Gleiche Namen bekommen „ (2)“, liegt ein Foto schon richtig, bleibt es weg. Doppelte Dateien (gleicher Inhalt) können übersprungen werden.</summary>
    public static List<Move> Plan(IEnumerable<Photo> photos, string target, string scheme, bool rename, bool skipDuplicates = true)
    {
        if (!KreativSafe.LocalPath(target)) throw new ArgumentException("Wähle einen Zielordner auf diesem Rechner.");
        var list = photos.OrderBy(p => p.Taken).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToList();
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (skipDuplicates)
            foreach (var g in PhotoAlbum.Duplicates(list)) foreach (var d in g.Skip(1)) skip.Add(d.Path);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var plan = new List<Move>();
        foreach (var p in list.Where(p => !skip.Contains(p.Path)))
        {
            var dir = Path.Combine(target, Folder(p.Taken, scheme));
            var name = FileName(p, rename); var dst = Path.Combine(dir, name);
            if (string.Equals(Path.GetFullPath(dst), Path.GetFullPath(p.Path), StringComparison.OrdinalIgnoreCase)) continue;
            // schon einsortiert (gleicher Inhalt unter dem Namen oder einem „(n)“-Namen)? Dann nicht noch einmal
            var already = false;
            for (var i = 2; used.Contains(dst) || File.Exists(dst); i++)
            {
                if (!used.Contains(dst) && SameContent(dst, p.Path)) { already = true; break; }
                dst = Path.Combine(dir, Path.GetFileNameWithoutExtension(name) + $" ({i})" + Path.GetExtension(name));
            }
            if (already) continue;
            used.Add(dst); plan.Add(new(p.Path, dst));
        }
        return plan;
    }

    static bool SameContent(string a, string b)
    {
        try { return new FileInfo(a).Length == new FileInfo(b).Length && J.Sha(a) == J.Sha(b); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public sealed record LogItem(string From, string To, string Sha);
    public sealed record RunLog(DateTime When, string Kind, List<LogItem> Items);

    /// <summary>Kopiert oder verschiebt Datei für Datei direkt zum Ziel (ohne Zwischennamen), überschreibt nie.
    /// Das Protokoll für „Rückgängig“ entsteht auch, wenn der Lauf mittendrin abbricht (z. B. Platte voll); eine halbe Kopie wird gelöscht.</summary>
    public static string Run(List<Move> plan, string undoDir, bool copy)
    {
        var done = new List<LogItem>();
        Directory.CreateDirectory(undoDir);
        var log = Path.Combine(undoDir, $"fotos-{(copy ? "kopiert" : "verschoben")}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
        try
        {
            foreach (var m in plan)
            {
                if (File.Exists(m.To) || !File.Exists(m.From)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(m.To)!);
                var sha = J.Sha(m.From);
                if (copy)
                {
                    try { File.Copy(m.From, m.To, overwrite: false); }
                    catch (IOException) { if (File.Exists(m.To) && new FileInfo(m.To).Length != new FileInfo(m.From).Length) File.Delete(m.To); throw; }
                }
                else File.Move(m.From, m.To, overwrite: false);
                done.Add(new(m.From, m.To, sha));
            }
        }
        finally { File.WriteAllText(log, JsonSerializer.Serialize(new RunLog(DateTime.Now, copy ? "kopieren" : "verschieben", done))); }
        return log;
    }

    /// <summary>Macht einen Lauf rückgängig: Kopien werden nur gelöscht, wenn ihr Inhalt unverändert ist; verschobene Fotos kommen zurück, wenn am alten Platz nichts liegt.
    /// Was nicht geht, bleibt unangetastet und wird gezählt.</summary>
    public static (int Done, int Skipped) Undo(string log)
    {
        var l = JsonSerializer.Deserialize<RunLog>(File.ReadAllText(log)) ?? throw new InvalidDataException("Protokoll unlesbar.");
        int ok = 0, skip = 0;
        foreach (var m in l.Items)
        {
            if (!File.Exists(m.To) || J.Sha(m.To) != m.Sha) { skip++; continue; }
            if (l.Kind == "kopieren") { File.Delete(m.To); ok++; }
            else if (!File.Exists(m.From)) { Directory.CreateDirectory(Path.GetDirectoryName(m.From)!); File.Move(m.To, m.From); ok++; }
            else skip++;
        }
        File.Delete(log);
        return (ok, skip);
    }

    /// <summary>Überblick: Fotos je Jahr, ohne Aufnahmedatum, doppelte.</summary>
    public static (IReadOnlyList<(int Year, int Count)> Years, int NoExif, int Duplicates) Summary(IReadOnlyList<Photo> photos) =>
        (photos.GroupBy(p => p.Taken.Year).OrderByDescending(g => g.Key).Select(g => (g.Key, g.Count())).ToList(),
         photos.Count(p => !p.FromExif), PhotoAlbum.Duplicates(photos).Sum(g => g.Count - 1));
}

/// <summary>Bilder mit KI: Stile, Formate, Aufruf der lokalen Bild-KI (stable-diffusion.cpp) und Herkunftsnachweis je Bild.</summary>
public static partial class ImageAiKit
{
    public sealed record Style(string Id, string Label, string Prompt);
    public static readonly IReadOnlyList<Style> Styles =
    [
        new("foto", "Foto", "photorealistic photograph, natural light, realistic materials, high detail"),
        new("illustration", "Illustration", "clean digital illustration, soft shading, vibrant but balanced colors"),
        new("aquarell", "Aquarell", "watercolor painting on textured paper, soft edges, gentle colors"),
        new("3d", "3D-Render", "3d render, studio lighting, soft shadows, clean materials"),
        new("pixel", "Pixel-Art", "pixel art, 16-bit, crisp pixels, limited palette"),
        new("skizze", "Bleistiftskizze", "pencil sketch, graphite on paper, hatching"),
        new("ohne", "Ohne Stil", ""),
    ];
    public static readonly IReadOnlyList<(string Id, string Label, int W, int H)> Formats = [("quadrat", "Quadrat 1:1", 1, 1), ("quer", "Quer 16:9", 16, 9), ("hoch", "Hoch 9:16", 9, 16), ("foto", "Foto 4:3", 4, 3)];
    public const string Negative = "text, letters, watermark, logo, signature, blurry, deformed, extra fingers, bad anatomy, lowres";

    /// <summary>Größe in Vielfachen von 64; Fläche wie Grundgröße² (SDXL 1024, SD 512).</summary>
    public static (int W, int H) Size(string format, int baseSize)
    {
        var f = Formats.FirstOrDefault(x => x.Id == format); if (f.Id is null) f = Formats[0];
        var area = (double)baseSize * baseSize; var w = Math.Sqrt(area * f.W / f.H); var h = area / w;
        static int R(double v) => Math.Max(256, (int)Math.Round(v / 64) * 64);
        return (R(w), R(h));
    }

    // Wortstämme ohne Ende-Grenze, damit auch gebeugte Formen und Zusammensetzungen greifen („nackter“, „Nacktfoto“, „nudity“)
    [GeneratedRegex(@"\b(nackt|nude|nudit|naked|porn|sex|erotik|erotisch|nsfw|topless|unbekleid|oben ohne|dessous|lingerie|hentai)", RegexOptions.IgnoreCase)]
    private static partial Regex BlockRx();

    /// <summary>Grund, warum die Beschreibung nicht gemalt wird, sonst null.</summary>
    public static string? Blocked(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return "Beschreibe, was auf dem Bild sein soll.";
        if (prompt.Length > 1500) return "Die Beschreibung ist zu lang (höchstens 1500 Zeichen).";
        return BlockRx().IsMatch(prompt) ? "Solche Bilder malt Trendo nicht." : null;
    }

    /// <summary>Nur die Inhaltssperre, ohne Längengrenze (für Liedtexte und andere lange Felder).</summary>
    public static bool Offensive(string? text) => BlockRx().IsMatch(text ?? "");

    public static string FullPrompt(string prompt, string style) =>
        Styles.FirstOrDefault(s => s.Id == style) is { Prompt.Length: > 0 } s ? $"{prompt.Trim()}, {s.Prompt}" : prompt.Trim();

    /// <summary>Startwert je Variante: gleich bleibende Abstände, damit eine Variante wiederholbar ist.</summary>
    public static int Seed(int baseSeed, int variant) => unchecked((baseSeed & 0x7FFFFFFF) + variant * 7919) & 0x7FFFFFFF;

    public static IReadOnlyList<string> Args(string modelFile, string prompt, string style, string negative, string output, int w, int h, int seed, bool xl) =>
        ["-m", modelFile, "-p", FullPrompt(prompt, style), "-n", string.IsNullOrWhiteSpace(negative) ? Negative : negative, "-o", output,
         "--steps", xl ? "4" : "1", "--cfg-scale", "1.0", "-W", w.ToString(CultureInfo.InvariantCulture), "-H", h.ToString(CultureInfo.InvariantCulture),
         "--seed", seed.ToString(CultureInfo.InvariantCulture), "--vae-tiling"];

    public sealed record Provenance(string Generator, string Model, string Prompt, string Style, string Negative, int Seed, int Width, int Height, DateTime CreatedUtc, string Sha256, bool AiGenerated = true);

    /// <summary>Herkunftsnachweis neben dem Bild (Datei.png.json): KI-erzeugt, Modell, Beschreibung, Startwert, Prüfsumme.</summary>
    public static string WriteProvenance(string png, Provenance p)
    {
        var f = png + ".json";
        File.WriteAllText(f, JsonSerializer.Serialize(p with { Sha256 = J.Sha(png) }, new JsonSerializerOptions { WriteIndented = true }));
        return f;
    }
}
