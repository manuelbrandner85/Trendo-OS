using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record WebsiteFinding(string Area, string Level, string Text, string Code);

public sealed record WebsiteQualityReport(int Score, string Note, IReadOnlyList<WebsiteFinding> Findings)
{
    public string Plain()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Score).Append(" von 100. ").Append(Note);
        foreach (var f in Findings.Where(f => f.Level != "Ok"))
            sb.Append('\n').Append(f.Area).Append(": ").Append(f.Text);
        return sb.ToString();
    }
}

/// <summary>Lokale Prüfung statt Lighthouse: Handy, Ladezeit, Barrierefreiheit, SEO, Links. Es wird nichts ins Netz geladen.</summary>
public static class WebsiteQuality
{
    public const string FileName = "qualitaet.json";
    static readonly string[] Areas = ["Handy", "Ladezeit", "Barrierefreiheit", "SEO", "Links"];
    static readonly HashSet<string> Fixable = ["viewport", "description", "lang", "alt", "video-muted", "external-script", "external-style", "focus", "motion", "media", "tap", "contrast", "overflow"];

    public static WebsiteQualityReport Check(string dir)
    {
        var findings = new List<WebsiteFinding>();
        var htmlFiles = HtmlFiles(dir).ToList();
        var cssPath = Path.Combine(dir, "stil.css");
        var css = File.Exists(cssPath) ? File.ReadAllText(cssPath) : "";
        if (htmlFiles.Count == 0) findings.Add(new("SEO", "Fehler", "Es liegt keine HTML-Datei im Ordner.", "missing-html"));

        var bytes = 0L;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var htmlPath in htmlFiles)
        {
            var html = File.ReadAllText(htmlPath);
            var name = Path.GetFileName(htmlPath);
            CheckDocument(name, html, css, findings);
            foreach (var (kind, value) in Refs(html))
            {
                if (value.StartsWith('#'))
                {
                    var id = value[1..];
                    if (id.Length > 0 && !html.Contains("id=\"" + id + "\"", StringComparison.Ordinal) && !html.Contains("id='" + id + "'", StringComparison.Ordinal))
                        findings.Add(new("Links", "Fehler", name + ": Sprungziel „" + id + "“ fehlt.", "anchor"));
                    continue;
                }
                if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!value.Contains('@')) findings.Add(new("Links", "Fehler", name + ": die E-Mail-Adresse ist unvollständig.", "mailto"));
                    continue;
                }
                if (value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                { findings.Add(new("Links", "Fehler", name + ": ein Link will Programmcode starten.", "javascript")); continue; }
                if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var area = kind == "script" ? "Ladezeit" : kind == "style" ? "Ladezeit" : "Links";
                    var level = kind is "script" or "style" ? "Fehler" : "Hinweis";
                    var code = kind == "script" ? "external-script" : kind == "style" ? "external-style" : "external-link";
                    findings.Add(new(area, level, name + ": verweist aufs Netz (" + Trim(value) + "). Die Prüfung öffnet das nicht.", code));
                    continue;
                }
                var local = Resolve(htmlPath, value);
                if (local is null || !File.Exists(local))
                    findings.Add(new("Links", "Fehler", name + ": „" + Trim(value) + "“ liegt nicht im Ordner.", "missing"));
                else if (seen.Add(local)) bytes += new FileInfo(local).Length;
            }
            if (seen.Add(htmlPath)) bytes += new FileInfo(htmlPath).Length;
        }
        if (File.Exists(cssPath) && seen.Add(cssPath)) bytes += new FileInfo(cssPath).Length;

        if (!css.Contains("min-height: 44px", StringComparison.Ordinal) && !css.Contains("min-height:44px", StringComparison.Ordinal))
            findings.Add(new("Handy", "Fehler", "Schaltflächen sind nicht als mindestens 44 Pixel hoch festgelegt.", "tap"));
        if (!css.Contains("max-width: 700px", StringComparison.Ordinal))
            findings.Add(new("Handy", "Fehler", "Es fehlt eine Anpassung für schmale Bildschirme.", "media"));
        if (!css.Contains("max-width: 100%", StringComparison.Ordinal))
            findings.Add(new("Handy", "Fehler", "Bilder können breiter als das Handy werden.", "overflow"));
        if (!css.Contains("font-size: 1.125rem", StringComparison.Ordinal) && !css.Contains("font-size:18px", StringComparison.Ordinal))
            findings.Add(new("Handy", "Hinweis", "Die Grundschrift sollte etwa 18 Pixel haben.", "font"));
        if (!css.Contains(":focus-visible", StringComparison.Ordinal))
            findings.Add(new("Barrierefreiheit", "Fehler", "Die Tastatur-Markierung fehlt.", "focus"));
        if (!css.Contains("prefers-reduced-motion", StringComparison.Ordinal))
            findings.Add(new("Barrierefreiheit", "Fehler", "Bei „weniger Bewegung“ laufen die Animationen weiter.", "motion"));
        Contrast(css, findings);

        if (bytes > 6_000_000) findings.Add(new("Ladezeit", "Fehler", "Die Seite ist über 6 MB schwer.", "weight"));
        else if (bytes > 2_500_000) findings.Add(new("Ladezeit", "Hinweis", "Die Seite ist über 2,5 MB. Das dauert auf dem Handy.", "weight"));
        else if (!findings.Any(f => f.Area == "Ladezeit" && f.Level == "Fehler"))
            findings.Add(new("Ladezeit", "Ok", "Lokal etwa " + (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB, ohne Abruf aus dem Netz.", "weight-ok"));

        foreach (var area in Areas)
            if (!findings.Any(f => f.Area == area))
                findings.Add(new(area, "Ok", "Nichts aufgefallen.", area.ToLowerInvariant() + "-ok"));

        var errors = findings.Count(f => f.Level == "Fehler");
        var hints = findings.Count(f => f.Level == "Hinweis");
        var score = Math.Clamp(100 - errors * 14 - hints * 4, 0, 100);
        return new(score, Note(score), findings);
    }

    public static WebsiteQualityReport Fix(string dir)
    {
        var cssPath = Path.Combine(dir, "stil.css");
        foreach (var htmlPath in HtmlFiles(dir))
        {
            var html = File.ReadAllText(htmlPath);
            var next = html;
            if (!next.Contains("<html", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Regex.IsMatch(next, "<html[^>]*\\blang=", RegexOptions.IgnoreCase))
                next = Regex.Replace(next, "<html\\b", "<html lang=\"de\"", RegexOptions.IgnoreCase);
            if (!next.Contains("charset", StringComparison.OrdinalIgnoreCase))
                next = next.Replace("<head>", "<head>\n<meta charset=\"utf-8\">", StringComparison.OrdinalIgnoreCase);
            if (!next.Contains("name=\"viewport\"", StringComparison.OrdinalIgnoreCase) && !next.Contains("name='viewport'", StringComparison.OrdinalIgnoreCase))
                next = Regex.Replace(next, "<head>", "<head>\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">", RegexOptions.IgnoreCase);
            if (!next.Contains("name=\"description\"", StringComparison.OrdinalIgnoreCase) && !next.Contains("name='description'", StringComparison.OrdinalIgnoreCase))
            {
                var title = Regex.Match(next, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;
                var desc = System.Net.WebUtility.HtmlEncode(WebsitePlan.Describe(System.Net.WebUtility.HtmlDecode(title), ""));
                next = Regex.Replace(next, "</head>", "<meta name=\"description\" content=\"" + desc + "\">\n</head>", RegexOptions.IgnoreCase);
            }
            next = Regex.Replace(next, "<img\\b[^>]*>", m => m.Value.Contains("alt=", StringComparison.OrdinalIgnoreCase) ? m.Value : m.Value.TrimEnd('>').TrimEnd('/') + " alt=\"Bild\">", RegexOptions.IgnoreCase);
            next = Regex.Replace(next, "<video\\b[^>]*>", m => m.Value.Contains("muted", StringComparison.OrdinalIgnoreCase) ? m.Value : m.Value.Replace("<video", "<video muted", StringComparison.OrdinalIgnoreCase), RegexOptions.IgnoreCase);
            next = Regex.Replace(next, "<script\\b[^>]*\\bsrc\\s*=\\s*['\"]https?:[^>]*>\\s*</script>", "", RegexOptions.IgnoreCase);
            next = Regex.Replace(next, "<link\\b[^>]*\\bhref\\s*=\\s*['\"]https?:[^>]*>", "", RegexOptions.IgnoreCase);
            if (next != html) File.WriteAllText(htmlPath, next);
        }
        if (File.Exists(cssPath))
        {
            var css = File.ReadAllText(cssPath);
            if (!css.Contains(":focus-visible", StringComparison.Ordinal))
                css += "\nbutton:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible { outline: 3px solid var(--accent); outline-offset: 3px; }\n";
            if (!css.Contains("prefers-reduced-motion", StringComparison.Ordinal))
                css += "\n@media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation: none !important; transition: none !important; } }\n";
            if (!css.Contains("max-width: 700px", StringComparison.Ordinal))
                css += "\n@media (max-width: 700px) { .kopf { flex-direction: column; align-items: flex-start; } .raster { grid-template-columns: 1fr; } }\n";
            if (!css.Contains("min-height: 44px", StringComparison.Ordinal))
                css += "\nbutton, .knopf, input { min-height: 44px; min-width: 44px; }\n";
            if (!css.Contains("max-width: 100%", StringComparison.Ordinal))
                css += "\nimg, video, canvas { max-width: 100%; }\n";
            css = FixContrast(css, dir);
            File.WriteAllText(cssPath, css);
        }
        var report = Check(dir);
        Save(dir, report);
        return report;
    }

    public static void Save(string dir, WebsiteQualityReport report) => WebsiteJson.Write(Path.Combine(dir, FileName), report);

    public static bool CanFix(WebsiteFinding f) => Fixable.Contains(f.Code);

    static void CheckDocument(string name, string html, string css, List<WebsiteFinding> findings)
    {
        if (!Regex.IsMatch(html, "<html[^>]*\\blang=", RegexOptions.IgnoreCase))
            findings.Add(new("Barrierefreiheit", "Fehler", name + ": die Sprache der Seite fehlt.", "lang"));
        if (!html.Contains("name=\"viewport\"", StringComparison.OrdinalIgnoreCase) && !html.Contains("name='viewport'", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("Handy", "Fehler", name + ": der Hinweis fürs Handy-Format fehlt.", "viewport"));
        var title = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!title.Success || title.Groups[1].Value.Trim().Length == 0)
            findings.Add(new("SEO", "Fehler", name + ": der Seitentitel fehlt.", "title"));
        else if (System.Net.WebUtility.HtmlDecode(title.Groups[1].Value).Length > 70)
            findings.Add(new("SEO", "Hinweis", name + ": der Titel ist sehr lang.", "title-long"));
        if (!html.Contains("name=\"description\"", StringComparison.OrdinalIgnoreCase) && !html.Contains("name='description'", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("SEO", "Fehler", name + ": die Kurzbeschreibung fehlt.", "description"));
        var h1 = Regex.Matches(html, "<h1\\b", RegexOptions.IgnoreCase).Count;
        if (h1 != 1) findings.Add(new("SEO", "Fehler", name + ": es sollte genau eine Hauptüberschrift sein.", "h1"));
        if (!html.Contains("<main", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("Barrierefreiheit", "Fehler", name + ": der Hauptbereich fehlt.", "main"));
        foreach (Match img in Regex.Matches(html, "<img\\b[^>]*>", RegexOptions.IgnoreCase))
            if (!img.Value.Contains("alt=", StringComparison.OrdinalIgnoreCase))
                findings.Add(new("Barrierefreiheit", "Fehler", name + ": ein Bild hat keinen Ersatztext.", "alt"));
        foreach (Match video in Regex.Matches(html, "<video\\b[^>]*>", RegexOptions.IgnoreCase))
            if (!video.Value.Contains("muted", StringComparison.OrdinalIgnoreCase))
                findings.Add(new("Barrierefreiheit", "Hinweis", name + ": ein Video ist nicht stumm geschaltet.", "video-muted"));
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var id = Regex.Match(input.Value, "\\bid\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
            if (!id.Success) { findings.Add(new("Barrierefreiheit", "Fehler", name + ": ein Feld hat keine Beschriftung.", "label")); continue; }
            if (!html.Contains("for=\"" + id.Groups[1].Value + "\"", StringComparison.Ordinal) && !html.Contains("for='" + id.Groups[1].Value + "'", StringComparison.Ordinal))
                findings.Add(new("Barrierefreiheit", "Fehler", name + ": das Feld „" + id.Groups[1].Value + "“ hat keine Beschriftung.", "label"));
        }
        foreach (Match canvas in Regex.Matches(html, "<canvas\\b[^>]*>", RegexOptions.IgnoreCase))
            if (!canvas.Value.Contains("aria-hidden", StringComparison.OrdinalIgnoreCase) && !canvas.Value.Contains("aria-label", StringComparison.OrdinalIgnoreCase))
                findings.Add(new("Barrierefreiheit", "Hinweis", name + ": eine Zeichenfläche hat keinen Namen.", "canvas"));
    }

    static void Contrast(string css, List<WebsiteFinding> findings)
    {
        var bg = Var(css, "background");
        var text = Var(css, "text");
        var muted = Var(css, "muted");
        var accent = Var(css, "accent");
        var accentText = Var(css, "accent-text");
        if (bg is null || text is null) return;
        void Pair(string? a, string? b, string what)
        {
            if (a is null || b is null) return;
            var ratio = WebsiteColor.Contrast(a, b);
            if (ratio < 4.5) findings.Add(new("Barrierefreiheit", "Fehler", what + " ist zu schwach (" + ratio.ToString("0.0", CultureInfo.InvariantCulture) + ":1).", "contrast"));
        }
        Pair(text, bg, "Text auf dem Hintergrund");
        Pair(muted, bg, "Leiser Text auf dem Hintergrund");
        Pair(accentText, accent, "Schrift auf der Schaltfläche");
    }

    static string FixContrast(string css, string dir)
    {
        var bg = Var(css, "background");
        if (bg is null) return css;
        css = Lift(css, "text", bg);
        css = Lift(css, "muted", bg);
        var accent = Var(css, "accent");
        if (accent is not null && Var(css, "accent-text") is { } ink && WebsiteColor.Contrast(ink, accent) < 4.5)
        {
            var better = WebsiteColor.Contrast("#111111", accent) >= WebsiteColor.Contrast("#ffffff", accent) ? "#111111" : "#ffffff";
            css = SetVar(css, "accent-text", better);
        }
        SyncStyle(dir, css);
        return css;
    }

    static string Lift(string css, string name, string bg)
    {
        var cur = Var(css, name);
        if (cur is null || WebsiteColor.Contrast(cur, bg) >= 4.5) return css;
        var dark = WebsiteColor.Contrast("#161616", bg);
        var light = WebsiteColor.Contrast("#f7f7f5", bg);
        return SetVar(css, name, dark >= light ? "#161616" : "#f7f7f5");
    }

    static void SyncStyle(string dir, string css)
    {
        var stil = Path.Combine(dir, "stil.json");
        if (!File.Exists(stil)) return;
        try
        {
            var style = WebsiteJson.Read<WebsiteStyle>(stil);
            string Keep(string name, string current) => Var(css, name) ?? current;
            var next = style with
            {
                Background = Keep("background", style.Background),
                Surface = Keep("surface", style.Surface),
                Text = Keep("text", style.Text),
                Muted = Keep("muted", style.Muted),
                Accent = Keep("accent", style.Accent),
                AccentText = Keep("accent-text", style.AccentText)
            };
            WebsiteJson.Write(stil, next);
            var inhalt = Path.Combine(dir, "inhalt.json");
            if (!File.Exists(inhalt)) return;
            var content = WebsiteJson.Read<WebsiteContent>(inhalt);
            content.Style = next;
            content.Sections ??= [];
            content.Frames ??= [];
            WebsiteJson.Write(inhalt, content);
        }
        catch (JsonException) { }
    }

    static string? Var(string css, string name)
    {
        var m = Regex.Match(css, "--" + name + @":\s*(#[0-9A-Fa-f]{6})");
        return m.Success ? m.Groups[1].Value : null;
    }

    static string SetVar(string css, string name, string hex) =>
        Regex.Replace(css, "(--" + name + @":\s*)#[0-9A-Fa-f]{6}", "$1" + hex);

    static IEnumerable<(string Kind, string Value)> Refs(string html)
    {
        foreach (Match m in Regex.Matches(html, "<(script|link|img|a|source|video)\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var tag = m.Groups[1].Value.ToLowerInvariant();
            var attr = tag is "a" or "link" ? "href" : "src";
            var value = Regex.Match(m.Value, "\\b" + attr + "\\s*=\\s*['\"]([^'\"]*)['\"]", RegexOptions.IgnoreCase);
            if (!value.Success) continue;
            var v = value.Groups[1].Value.Trim();
            if (v.Length == 0 || v.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var kind = tag switch { "script" => "script", "link" => "style", "img" or "source" or "video" => "media", _ => "link" };
            var cut = v.IndexOfAny(['#', '?']);
            if (cut == 0) yield return (kind, v);
            else yield return (kind, cut > 0 ? v[..cut] : v);
        }
    }

    static string? Resolve(string htmlFile, string rel)
    {
        if (string.IsNullOrWhiteSpace(rel)) return null;
        var baseDir = Path.GetFullPath(Path.GetDirectoryName(htmlFile)!);
        var full = Path.GetFullPath(Path.Combine(baseDir, rel.Replace('/', Path.DirectorySeparatorChar)));
        var root = baseDir;
        while (!File.Exists(Path.Combine(root, "index.html")) && Directory.GetParent(root) is { } parent) root = parent.FullName;
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal) ? full : null;
    }

    static IEnumerable<string> HtmlFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.html", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + ".vorschau" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    static string Trim(string s) => s.Length > 80 ? s[..80] + "…" : s;

    public static string Note(int score) => score switch
    {
        >= 90 => "Sehr gut. Handy, Ladezeit, Barrierefreiheit, Suche und Links sind in Ordnung.",
        >= 75 => "Gut. Ein paar Stellen kannst du noch verbessern.",
        >= 50 => "Geht so. Bitte die markierten Punkte ansehen.",
        _ => "Schwach. Bitte zuerst die Fehler beheben."
    };
}

public static class WebsiteColor
{
    public static double Contrast(string a, string b)
    {
        var l1 = Lum(a);
        var l2 = Lum(b);
        var hi = Math.Max(l1, l2);
        var lo = Math.Min(l1, l2);
        return (hi + 0.05) / (lo + 0.05);
    }

    static double Lum(string hex)
    {
        double Ch(int i)
        {
            var c = Convert.ToInt32(hex.TrimStart('#')[i..(i + 2)], 16) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        var h = hex.TrimStart('#');
        if (h.Length != 6) return 0;
        return 0.2126 * Ch(0) + 0.7152 * Ch(2) + 0.0722 * Ch(4);
    }
}
