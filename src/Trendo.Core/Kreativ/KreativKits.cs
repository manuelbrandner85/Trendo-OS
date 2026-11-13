using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Trendo.Core.Kreativ;

/// <summary>Gemeinsame Absicherung der Kreativ-Studios: Text für XML bereinigen, nur lokale Dateien mit Größengrenze.</summary>
public static class KreativSafe
{
    /// <summary>Höchstgröße eines eingebetteten Bildes.</summary>
    public const long MaxImageBytes = 25L * 1024 * 1024;

    /// <summary>Entfernt Zeichen, die in XML nicht erlaubt sind (Steuerzeichen, einzelne Ersatzzeichen), und maskiert den Rest.</summary>
    public static string Xml(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var b = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { b.Append(c).Append(s[++i]); continue; }
            if (XmlConvert.IsXmlChar(c)) b.Append(c);
        }
        return SecurityElement.Escape(b.ToString()) ?? "";
    }

    /// <summary>Kürzt auf höchstens <paramref name="max"/> Textzeichen, ohne ein Emoji oder Ersatzpaar zu zerschneiden.</summary>
    public static string Cut(string s, int max)
    {
        var e = StringInfo.GetTextElementEnumerator(s ?? ""); var b = new StringBuilder(); var n = 0;
        while (n < max && e.MoveNext()) { b.Append(e.GetTextElement()); n++; }
        return b.ToString();
    }

    /// <summary>Nur vollständige lokale Pfade, keine Netzwerkfreigaben (\\rechner\…), Datei muss existieren und darf nicht größer als <paramref name="maxBytes"/> sein.
    /// Projektdateien können von anderen stammen; ein Netzwerkpfad würde beim bloßen Öffnen eine Verbindung aufbauen.</summary>
    public static bool LocalFile(string? path, long maxBytes = long.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return false;
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)) return false;
        try { var f = new FileInfo(path); return f.Exists && f.Length <= maxBytes; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }

    /// <summary>Vollständiger lokaler Pfad, keine Netzwerkfreigabe (\\rechner oder //rechner); muss nicht existieren.</summary>
    public static bool LocalPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal);

    /// <summary>Wie <see cref="LocalFile"/>, für Ordner: vollständig, lokal, vorhanden.</summary>
    public static bool LocalDir(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal) && Directory.Exists(path);
}

/// <summary>Texte zusammenfassen ohne Internet: wichtigste Sätze nach Worthäufigkeit, in der ursprünglichen Reihenfolge. Claude kann es danach ausführlicher.</summary>
public static partial class SummaryKit
{
    public sealed record Summary(IReadOnlyList<string> Sentences, IReadOnlyList<string> Keywords, int Words, int ReadMinutes);

    static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "der","die","das","und","oder","aber","ein","eine","einer","eines","einem","einen","ist","sind","war","waren","wird","werden","wurde","hat","haben","hatte",
        "in","im","am","an","auf","aus","bei","mit","nach","von","vor","zu","zum","zur","für","über","unter","durch","gegen","ohne","um","als","auch","noch","nur",
        "nicht","kein","keine","so","wie","wenn","dass","daß","es","er","sie","wir","ihr","ich","du","man","sich","sein","seine","ihre","ihren","dem","den","des",
        "dieser","diese","dieses","jeder","jede","alle","mehr","sehr","schon","dann","doch","hier","dort","da","was","wer","the","and","of","to","a","is",
    };

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-ZÄÖÜ0-9„""])")]
    private static partial Regex SentenceRx();
    [GeneratedRegex(@"[\p{L}][\p{L}\-]{2,}")]
    private static partial Regex WordRx();

    public static IReadOnlyList<string> Sentences(string text) =>
        SentenceRx().Split(Regex.Replace(text ?? "", @"\s+", " ").Trim()).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    /// <summary>Anzahl Sätze je Länge.</summary>
    public static int CountFor(string length) => length switch { "kurz" => 3, "lang" => 8, _ => 5 };

    public static Summary Summarize(string text, int count)
    {
        var sentences = Sentences(text);
        var words = WordRx().Matches(text ?? "").Select(m => m.Value.ToLowerInvariant()).ToList();
        var freq = words.Where(w => !Stop.Contains(w)).GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());
        var scored = sentences.Select((s, i) =>
        {
            var ws = WordRx().Matches(s).Select(m => m.Value.ToLowerInvariant()).Where(w => !Stop.Contains(w)).ToList();
            var score = ws.Count == 0 ? 0 : ws.Sum(w => freq.GetValueOrDefault(w)) / Math.Sqrt(ws.Count);
            if (i == 0) score *= 1.3; // der erste Satz trägt oft das Thema
            return (s, i, score);
        }).ToList();
        var pick = scored.OrderByDescending(x => x.score).Take(Math.Clamp(count, 1, Math.Max(1, sentences.Count))).OrderBy(x => x.i).Select(x => x.s).ToList();
        var keywords = freq.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Take(8).Select(k => k.Key).ToList();
        return new(pick, keywords, words.Count, Math.Max(1, (int)Math.Ceiling(words.Count / 200.0)));
    }

    public static string Markdown(string title, Summary s) =>
        $"# {(string.IsNullOrWhiteSpace(title) ? "Zusammenfassung" : title.Trim())}\n\n{string.Join("\n", s.Sentences.Select(x => "- " + x))}\n\n**Stichwörter:** {string.Join(", ", s.Keywords)}\n\n_Original: {s.Words} Wörter, etwa {s.ReadMinutes} Minuten Lesezeit._\n";
}

/// <summary>Logos und Markenpaket als SVG: Monogramm oder Wortmarke aus Name und Farben, mit Kontrastprüfung nach WCAG.</summary>
public static partial class LogoKit
{
    public sealed record Spec(string Name, string Tagline, string Primary, string Accent, string Layout, string Font);

    public static readonly IReadOnlyList<(string Id, string Label)> Layouts = [("kreis", "Kreis"), ("schild", "Schild"), ("quadrat", "Quadrat"), ("wort", "Wortmarke"), ("linie", "Mit Linie")];
    public static readonly IReadOnlyList<string> Fonts = ["Segoe UI", "Georgia", "Trebuchet MS", "Verdana", "Courier New"];

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexRx();
    public static bool IsHex(string c) => HexRx().IsMatch(c ?? "");

    public static string Initials(string name)
    {
        var parts = (name ?? "").Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => KreativSafe.Cut(p, 1).ToUpper(CultureInfo.CurrentCulture)));
    }

    static double Lum(string hex)
    {
        double Ch(int i) { var v = Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(1) + 0.7152 * Ch(3) + 0.0722 * Ch(5);
    }

    /// <summary>Kontrastverhältnis nach WCAG 2 (1 bis 21).</summary>
    public static double Contrast(string a, string b)
    {
        if (!IsHex(a) || !IsHex(b)) throw new ArgumentException("Farben als #RRGGBB angeben.");
        var (x, y) = (Lum(a), Lum(b));
        return Math.Round((Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05), 2);
    }

    /// <summary>Lesbare Schriftfarbe auf der Hauptfarbe.</summary>
    public static string TextOn(string bg) => Contrast(bg, "#FFFFFF") >= Contrast(bg, "#111111") ? "#FFFFFF" : "#111111";

    static string X(string s) => KreativSafe.Xml(s);

    public static string Svg(Spec s)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) throw new ArgumentException("Gib einen Namen an.");
        if (!IsHex(s.Primary) || !IsHex(s.Accent)) throw new ArgumentException("Farben als #RRGGBB angeben.");
        var font = Fonts.Contains(s.Font) ? s.Font : Fonts[0];
        var ini = X(Initials(s.Name)); var name = X(s.Name.Trim()); var tag = X(s.Tagline.Trim()); var on = TextOn(s.Primary);
        var mark = s.Layout switch
        {
            "schild" => $"<path d=\"M40,20 H140 V90 Q140,140 90,160 Q40,140 40,90 Z\" fill=\"{s.Primary}\"/><text x=\"90\" y=\"102\" font-size=\"52\" text-anchor=\"middle\" fill=\"{on}\" font-family=\"{font}\" font-weight=\"700\">{ini}</text>",
            "quadrat" => $"<rect x=\"30\" y=\"30\" width=\"120\" height=\"120\" rx=\"26\" fill=\"{s.Primary}\"/><text x=\"90\" y=\"108\" font-size=\"56\" text-anchor=\"middle\" fill=\"{on}\" font-family=\"{font}\" font-weight=\"700\">{ini}</text>",
            "wort" => "",
            "linie" => $"<rect x=\"30\" y=\"118\" width=\"120\" height=\"8\" fill=\"{s.Accent}\"/><text x=\"90\" y=\"104\" font-size=\"60\" text-anchor=\"middle\" fill=\"{s.Primary}\" font-family=\"{font}\" font-weight=\"700\">{ini}</text>",
            _ => $"<circle cx=\"90\" cy=\"90\" r=\"62\" fill=\"{s.Primary}\"/><circle cx=\"90\" cy=\"90\" r=\"62\" fill=\"none\" stroke=\"{s.Accent}\" stroke-width=\"6\"/><text x=\"90\" y=\"108\" font-size=\"52\" text-anchor=\"middle\" fill=\"{on}\" font-family=\"{font}\" font-weight=\"700\">{ini}</text>",
        };
        var tx = s.Layout == "wort" ? 30 : 180;
        var words = $"<text x=\"{tx}\" y=\"92\" font-size=\"44\" fill=\"{s.Primary}\" font-family=\"{font}\" font-weight=\"700\">{name}</text>" +
                    (tag.Length > 0 ? $"<text x=\"{tx}\" y=\"126\" font-size=\"20\" fill=\"{s.Accent}\" font-family=\"{font}\">{tag}</text>" : "");
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"640\" height=\"180\" viewBox=\"0 0 640 180\">{mark}{words}</svg>";
    }

    public static string BrandMarkdown(Spec s) =>
        $"# Markenpaket: {s.Name.Trim()}\n\n| Farbe | Wert | Kontrast zu Weiß | Kontrast zu Schwarz |\n|---|---|---|---|\n" +
        $"| Hauptfarbe | {s.Primary} | {Contrast(s.Primary, "#FFFFFF")}:1 | {Contrast(s.Primary, "#000000")}:1 |\n" +
        $"| Akzent | {s.Accent} | {Contrast(s.Accent, "#FFFFFF")}:1 | {Contrast(s.Accent, "#000000")}:1 |\n\n" +
        $"Schrift: {(Fonts.Contains(s.Font) ? s.Font : Fonts[0])}\n\nFür Fließtext braucht es mindestens 4,5:1 Kontrast (WCAG AA), für große Schrift 3:1.\n" +
        "Vor der Anmeldung als Marke beim Amt prüfen, ob es den Namen oder ein ähnliches Zeichen schon gibt.\n";
}

/// <summary>Podcast: Kapitel, Shownotes, Kapiteldatei für ffmpeg, Lautheit nach üblichem Podcast-Ziel und RSS-Feed.</summary>
public static partial class PodcastKit
{
    public sealed record Chapter(TimeSpan Start, string Title);
    public sealed record Episode(string Title, string Description, string FileName, long Bytes, TimeSpan Duration, DateTimeOffset Published, string Guid);

    [GeneratedRegex(@"^\s*(?:([0-9]{1,2}):)?([0-9]{1,2}):([0-9]{2})\s+(.+)$")]
    private static partial Regex ChapterRx();

    /// <summary>Liest Zeilen wie „00:00 Einleitung“ oder „1:02:30 Fazit“.</summary>
    public static (IReadOnlyList<Chapter> Chapters, IReadOnlyList<string> Problems) ParseChapters(string text)
    {
        var list = new List<Chapter>(); var problems = new List<string>();
        foreach (var (line, i) in (text ?? "").Split('\n').Select((l, i) => (l.Trim(), i + 1)).Where(x => x.Item1.Length > 0))
        {
            var m = ChapterRx().Match(line);
            if (!m.Success) { problems.Add($"Zeile {i}: Zeit fehlt, Beispiel „03:15 Thema“."); continue; }
            var h = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); var sec = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (sec > 59 || (m.Groups[1].Success && min > 59)) { problems.Add($"Zeile {i}: ungültige Zeit."); continue; }
            list.Add(new(new TimeSpan(h, min, sec), m.Groups[4].Value.Trim()));
        }
        var sorted = list.OrderBy(c => c.Start).ToList();
        if (sorted.Count > 0 && sorted[0].Start != TimeSpan.Zero) problems.Add("Das erste Kapitel sollte bei 00:00 beginnen.");
        if (sorted.Select(c => c.Start).Distinct().Count() != sorted.Count) problems.Add("Zwei Kapitel haben dieselbe Zeit.");
        return (sorted, problems);
    }

    static string Clock(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : t.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    public static string ShowNotes(string title, string description, IReadOnlyList<Chapter> chapters) =>
        $"# {title.Trim()}\n\n{description.Trim()}\n\n## Kapitel\n\n{string.Join("\n", chapters.Select(c => $"- {Clock(c.Start)} {c.Title}"))}\n";

    static string Esc(string s) => Regex.Replace(s, @"([=;#\\\n])", @"\$1");

    /// <summary>Kapiteldatei im FFMETADATA-Format. Ende eines Kapitels ist der Beginn des nächsten, das letzte endet mit der Gesamtlänge.</summary>
    public static string FfMetadata(string title, IReadOnlyList<Chapter> chapters, TimeSpan total)
    {
        var sb = new StringBuilder(";FFMETADATA1\ntitle=" + Esc(title) + "\n");
        for (var i = 0; i < chapters.Count; i++)
        {
            var end = i + 1 < chapters.Count ? chapters[i + 1].Start : total;
            if (end <= chapters[i].Start) end = chapters[i].Start + TimeSpan.FromSeconds(1);
            sb.Append(CultureInfo.InvariantCulture, $"[CHAPTER]\nTIMEBASE=1/1000\nSTART={(long)chapters[i].Start.TotalMilliseconds}\nEND={(long)end.TotalMilliseconds}\ntitle={Esc(chapters[i].Title)}\n");
        }
        return sb.ToString();
    }

    /// <summary>Lautheit auf übliche Podcast-Werte (-16 LUFS, Spitze -1,5 dBTP), Ausgabe als MP3.</summary>
    public static IReadOnlyList<string> LoudnessArgs(string input, string output) =>
        ["-y", "-i", input, "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-ar", "44100", "-b:a", "128k", output];

    /// <summary>Dateityp der Folge aus der Endung, damit Podcast-Apps richtig abspielen.</summary>
    public static string AudioType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".m4a" or ".mp4" or ".aac" => "audio/mp4",
        ".ogg" or ".oga" or ".opus" => "audio/ogg",
        ".wav" => "audio/wav",
        ".flac" => "audio/flac",
        _ => "audio/mpeg",
    };

    public static string Rss(string show, string author, string description, string baseUrl, IEnumerable<Episode> episodes)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var b) || b.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Gib die Internetadresse mit https:// an, unter der die Folgen liegen.");
        XNamespace it = "http://www.itunes.com/dtds/podcast-1.0.dtd";
        var root = baseUrl.TrimEnd('/') + "/";
        var channel = new XElement("channel", new XElement("title", show), new XElement("link", root), new XElement("language", "de-de"), new XElement("description", description),
            new XElement(it + "author", author), new XElement(it + "explicit", "false"));
        foreach (var e in episodes)
            channel.Add(new XElement("item", new XElement("title", e.Title), new XElement("description", e.Description),
                new XElement("enclosure", new XAttribute("url", root + Uri.EscapeDataString(e.FileName)), new XAttribute("length", e.Bytes), new XAttribute("type", AudioType(e.FileName))),
                new XElement("guid", new XAttribute("isPermaLink", "false"), e.Guid), new XElement("pubDate", e.Published.ToString("r", CultureInfo.InvariantCulture)),
                new XElement(it + "duration", ((long)e.Duration.TotalSeconds).ToString(CultureInfo.InvariantCulture))));
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("rss", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "itunes", it), channel));
        return doc.Declaration + "\n" + doc.Root;
    }
}

/// <summary>Comics und Storyboards: Seitenaufteilung, Sprechblasen, Bildunterschriften und Bilder als SVG-Seite.</summary>
public static partial class ComicKit
{
    public sealed record Panel(string Speech, string Caption, string ImagePath);
    public sealed record Box(double X, double Y, double W, double H);

    public const double PageW = 1000, PageH = 1414, Margin = 40, Gap = 20;
    public static readonly IReadOnlyList<(string Id, string Label)> Layouts = [("2x2", "Vier Bilder"), ("3-reihen", "Drei Reihen"), ("1+2", "Groß und zwei"), ("6er", "Sechs Bilder")];

    public static IReadOnlyList<Box> Boxes(string layout)
    {
        double w = PageW - 2 * Margin, h = PageH - 2 * Margin;
        IEnumerable<Box> Grid(int cols, int rows) =>
            from r in Enumerable.Range(0, rows) from c in Enumerable.Range(0, cols)
            select new Box(Margin + c * (w + Gap) / cols, Margin + r * (h + Gap) / rows, (w + Gap) / cols - Gap, (h + Gap) / rows - Gap);
        return layout switch
        {
            "3-reihen" => Grid(1, 3).ToList(),
            "6er" => Grid(2, 3).ToList(),
            "1+2" => [new(Margin, Margin, w, (h - Gap) / 2), new(Margin, Margin + (h + Gap) / 2, (w - Gap) / 2, (h - Gap) / 2), new(Margin + (w + Gap) / 2, Margin + (h + Gap) / 2, (w - Gap) / 2, (h - Gap) / 2)],
            _ => Grid(2, 2).ToList(),
        };
    }

    /// <summary>Teilt eine Geschichte in Bilder: Absätze oder Zeilen, die mit „Bild“ beginnen.</summary>
    public static IReadOnlyList<string> SplitStory(string story)
    {
        var text = (story ?? "").Replace("\r", "");
        var parts = BildRx().IsMatch(text) ? BildRx().Split(text) : text.Split("\n\n");
        return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
    }

    /// <summary>„Sprechblase | Bildunterschrift“ in die zwei Teile trennen.</summary>
    public static Panel ParsePanel(string part, string image = "")
    {
        var i = (part ?? "").IndexOf('|');
        return i < 0 ? new(part?.Trim() ?? "", "", image) : new(part![..i].Trim(), part[(i + 1)..].Trim(), image);
    }

    [GeneratedRegex(@"(?im)^\s*bild\s*[0-9]+\s*[:.)-]\s*")]
    private static partial Regex BildRx();

    static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > width) { yield return line.ToString(); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    static string X(string s) => KreativSafe.Xml(s);

    static string? DataUri(string path)
    {
        if (!KreativSafe.LocalFile(path, KreativSafe.MaxImageBytes)) return null;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
        return mime is null ? null : $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
    }

    public static string Svg(string layout, IReadOnlyList<Panel> panels, string title = "")
    {
        var boxes = Boxes(layout);
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{PageW}\" height=\"{PageH}\" viewBox=\"0 0 {PageW} {PageH}\"><rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>");
        if (title.Trim().Length > 0) sb.Append($"<text x=\"{Margin}\" y=\"30\" font-size=\"24\" font-family=\"Segoe UI\" font-weight=\"700\">{X(title.Trim())}</text>");
        for (var i = 0; i < boxes.Count; i++)
        {
            var b = boxes[i]; var p = i < panels.Count ? panels[i] : new Panel("", "", "");
            var f = FormattableString.Invariant;
            sb.Append(f($"<clipPath id=\"c{i}\"><rect x=\"{b.X}\" y=\"{b.Y}\" width=\"{b.W}\" height=\"{b.H}\"/></clipPath>"));
            if (DataUri(p.ImagePath) is { } img) sb.Append(f($"<image href=\"{img}\" x=\"{b.X}\" y=\"{b.Y}\" width=\"{b.W}\" height=\"{b.H}\" preserveAspectRatio=\"xMidYMid slice\" clip-path=\"url(#c{i})\"/>"));
            sb.Append(f($"<rect x=\"{b.X}\" y=\"{b.Y}\" width=\"{b.W}\" height=\"{b.H}\" fill=\"none\" stroke=\"#111\" stroke-width=\"4\"/>"));
            var speech = Wrap(p.Speech, 26).Take(5).ToList();
            if (speech.Count > 0)
            {
                var bw = Math.Min(b.W - 40, 26 * 13 + 30); var bh = speech.Count * 26 + 24;
                sb.Append(f($"<rect x=\"{b.X + 20}\" y=\"{b.Y + 20}\" width=\"{bw}\" height=\"{bh}\" rx=\"18\" fill=\"#FFFFFF\" stroke=\"#111\" stroke-width=\"3\"/>"));
                sb.Append(f($"<path d=\"M{b.X + 60},{b.Y + 20 + bh} l14,26 l10,-26 z\" fill=\"#FFFFFF\" stroke=\"#111\" stroke-width=\"3\"/>"));
                for (var l = 0; l < speech.Count; l++) sb.Append(f($"<text x=\"{b.X + 36}\" y=\"{b.Y + 50 + l * 26}\" font-size=\"20\" font-family=\"Comic Sans MS, Segoe UI\">{X(speech[l])}</text>"));
            }
            if (p.Caption.Trim().Length > 0)
                sb.Append(f($"<rect x=\"{b.X}\" y=\"{b.Y + b.H - 44}\" width=\"{b.W}\" height=\"44\" fill=\"#FFF3B0\" stroke=\"#111\" stroke-width=\"3\"/><text x=\"{b.X + 14}\" y=\"{b.Y + b.H - 16}\" font-size=\"18\" font-family=\"Segoe UI\">{X(KreativSafe.Cut(p.Caption.Trim(), 70))}</text>"));
        }
        return sb.Append("</svg>").ToString();
    }
}
