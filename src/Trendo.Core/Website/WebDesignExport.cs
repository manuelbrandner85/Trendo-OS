using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Stufe 3: Export der ganzen Website. Je Seite und Sprache eine eigene Adresse, je CMS-Eintrag eine Seite aus der Vorlage (W2),
/// Kopf mit Titel, Beschreibung, kanonischer Adresse, Vorschaubild und hreflang (W3, Z5), dazu Sitemap, robots.txt und Weiterleitungen,
/// Übergang zwischen Seiten über View Transitions mit Ersatzweg (W4). Der sichtbare Inhalt kommt nach dem Export per Vorrendern ins HTML (App, echter Browser).</summary>
public static class WebDesignExport
{
    public sealed record Output(string RelPath, string Lang, bool Fallback, bool Indexable);
    public sealed record Result(string? Dir, string? Error, IReadOnlyList<Output> Pages, IReadOnlyList<string> Notes);

    /// <summary>Platzhalter im HTML, an dessen Stelle das Vorrendern den Inhalt setzt.</summary>
    public const string RootMarker = "<div id=\"root\"></div>";

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";
    public static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>Tiefe einer Ausgabedatei („en/rezepte/brot.html“ → 2) und der passende Weg zur Wurzel.</summary>
    public static string BaseOf(string relPath) => string.Concat(Enumerable.Repeat("../", relPath.Count(c => c == '/')));

    /// <summary>Ganze Website exportieren. Der Export-Ordner wird dabei neu aufgebaut.</summary>
    public static Result ExportSite(string dir, string editorDir) => ExportSite(dir, editorDir, Path.Combine(dir, WebDesign.ExportDir));


    public static Result ExportSite(string dir, string editorDir, string outDir)
    {
        var js = Path.Combine(editorDir, "assets", "editor.js"); var css = Path.Combine(editorDir, "assets", "style.css");
        if (!File.Exists(js) || !File.Exists(css)) return new(null, "Editor-Bündel fehlt im Programm (Assets/webdesign).", [], []);
        var site = WebDesignSite.Load(dir);
        var notes = new List<string>();
        var outputs = new List<Output>();
        var usedDocs = new List<JsonObject>();
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        CopyBundle(editorDir, outDir);
        var tokens = WebDesignTokens.Load(dir);
        var cms = WebDesignCms.Load(dir);
        var pages = WebDesignSite.Pages(dir);
        var templates = new Dictionary<string, string>(); // Sammlung → Vorlage-Seite
        var entryPaths = new HashSet<string>();
        foreach (var c in cms) if (WebDesignCms.TemplateOf(dir, c.Id) is { } t) { templates[c.Id] = t; foreach (var e in c.Eintraege) entryPaths.Add(c.Id + "/" + e.Adresse); }
        var normalPages = pages.Where(p => !templates.ContainsValue(p)).ToList();

        // Welche Seiten gibt es in welcher Sprache wirklich (für hreflang)?
        // Übersetzt heißt: Seite in der Sprache vorhanden und nicht nur eine unveränderte Kopie des Originals
        var report = WebDesignI18n.Report(dir);
        bool Translated(string page, string lang) => lang == site.Standardsprache || report.Any(r => r.Adresse == page && r.Sprache == lang && !r.Fehlt && (r.Gesamt == 0 || r.Offen < r.Gesamt));
        var translated = site.Sprachen.ToDictionary(l => l, l => normalPages.Where(p => Translated(p, l)).ToHashSet());

        string PagePath(string lang, string page) => (lang == site.Standardsprache ? "" : lang + "/") + (page == WebDesignSite.Index ? "index.html" : page + ".html");
        string EntryRel(string lang, string coll, string entry) => (lang == site.Standardsprache ? "" : lang + "/") + WebDesignCms.EntryPath(coll, entry);
        string Abs(string rel) => site.Basis + "/" + (rel == "index.html" ? "" : rel.EndsWith("/index.html", StringComparison.Ordinal) ? rel[..^10] : rel);

        foreach (var lang in site.Sprachen)
        {
            var prefix = lang == site.Standardsprache ? "" : lang + "/";
            foreach (var page in normalPages)
            {
                var isTranslated = translated[lang].Contains(page);
                var file = WebDesignSite.FileOf(dir, page, isTranslated ? lang : site.Standardsprache, site.Standardsprache);
                var raw = WebDesign.LoadFile(file, page);
                var rel = PagePath(lang, page);
                var alts = isTranslated ? site.Sprachen.Where(l => translated[l].Contains(page)).Select(l => (l, PagePath(l, page))).ToList() : [];
                var canonical = isTranslated ? rel : PagePath(site.Standardsprache, page); // nicht übersetzt: verweist auf das Original
                if (!isTranslated) notes.Add($"„{page}“ ist in {WebDesignI18n.LangName(lang)} nicht übersetzt; unter /{rel} steht das Original.");
                var o = WritePage(dir, raw, tokens, cms, outDir, rel, isTranslated ? lang : site.Standardsprache, prefix, pages, entryPaths, alts, canonical, site, notes, usedDocs);
                outputs.Add(o with { Fallback = !isTranslated });
            }
            foreach (var (coll, tpl) in templates)
            {
                var c = cms.First(x => x.Id == coll);
                var tplTranslated = translated[lang].Contains(tpl) || lang == site.Standardsprache;
                var tfile = WebDesignSite.FileOf(dir, tpl, lang != site.Standardsprache && WebDesignSite.HasTranslation(dir, tpl, lang) ? lang : site.Standardsprache, site.Standardsprache);
                if (JsonNode.Parse(WebDesign.LoadFile(tfile, tpl)) is not JsonObject tdoc) continue;
                foreach (var e in c.Eintraege)
                {
                    var filled = WebDesignCms.Fill(tdoc, c, e, cms, lang, prefix);
                    var rel = EntryRel(lang, coll, e.Adresse);
                    var alts = site.Sprachen.Select(l => (l, EntryRel(l, coll, e.Adresse))).ToList();
                    var o = WritePage(dir, filled.ToJsonString(), tokens, cms, outDir, rel, lang, prefix, pages, entryPaths, alts, rel, site, notes, usedDocs);
                    outputs.Add(o);
                }
                if (!tplTranslated) notes.Add($"Vorlage „{tpl}“ ist in {WebDesignI18n.LangName(lang)} nicht übersetzt; Einträge nutzen die übersetzten Felder, der Rest bleibt im Original.");
            }
        }

        // Z7: Namensnennung für alle Medien unter CC-BY, die irgendeine Seite nutzt
        var credits = WebDesignAssets.AttributionFor(dir, new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject() }, ["content"] = new JsonArray(usedDocs.Select(d => (JsonNode?)d["content"]?.DeepClone()).ToArray()), ["zones"] = new JsonObject() });
        if (credits.Count > 0) Write(outDir, "namensnennung.html", WebDesignAssets.AttributionHtml(credits));

        // W3: Weiterleitungen als kleine Seiten (überall lauffähig) und als _redirects für Hoster, die das lesen
        var redirectLines = new StringBuilder();
        foreach (var r in site.Weiterleitungen)
        {
            var target = r.Nach.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? r.Nach : (r.Nach == WebDesignSite.Index ? "index.html" : r.Nach + ".html");
            var rel = r.Von + ".html";
            if (outputs.Any(x => x.RelPath == rel)) { notes.Add($"Weiterleitung „/{r.Von}“ überspringt: dort liegt schon eine Seite."); continue; }
            var href = r.Nach.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? r.Nach : BaseOf(rel) + target;
            Write(outDir, rel, $"<!doctype html>\n<html lang=\"{site.Standardsprache}\">\n<head>\n<meta charset=\"UTF-8\" />\n<meta name=\"robots\" content=\"noindex\" />\n<meta http-equiv=\"refresh\" content=\"0; url={Esc(href)}\" />\n" +
                (site.Basis.Length > 0 && !r.Nach.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? $"<link rel=\"canonical\" href=\"{Esc(Abs(target))}\" />\n" : "") +
                $"<title>Weitergeleitet</title>\n</head>\n<body>\n<p>Diese Seite ist umgezogen: <a href=\"{Esc(href)}\">weiter</a>.</p>\n</body>\n</html>\n");
            redirectLines.Append('/').Append(r.Von).Append(' ').Append(r.Nach.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? r.Nach : "/" + (r.Nach == WebDesignSite.Index ? "" : r.Nach)).Append(" 301\n");
        }
        if (redirectLines.Length > 0) Write(outDir, "_redirects", redirectLines.ToString());

        // W3: robots.txt und Sitemap
        if (site.Basis.Length > 0)
        {
            var sm = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
            foreach (var o in outputs.Where(o => o.Indexable && !o.Fallback)) sm.Append("  <url><loc>").Append(WebUtility.HtmlEncode(Abs(o.RelPath))).Append("</loc></url>\n");
            sm.Append("</urlset>\n");
            Write(outDir, "sitemap.xml", sm.ToString());
            Write(outDir, "robots.txt", $"User-agent: *\nAllow: /\nSitemap: {site.Basis}/sitemap.xml\n");
        }
        else
        {
            Write(outDir, "robots.txt", "User-agent: *\nAllow: /\n");
            notes.Add("Ohne Adresse der Website gibt es keine Sitemap, keine kanonischen Adressen und keine Sprachverweise (hreflang).");
        }
        return new(outDir, null, outputs, notes.Distinct().ToList());
    }

    static void CopyBundle(string editorDir, string outDir)
    {
        var assets = Path.Combine(outDir, "assets"); Directory.CreateDirectory(assets);
        File.Copy(Path.Combine(editorDir, "assets", "editor.js"), Path.Combine(assets, "editor.js"), true);
        File.Copy(Path.Combine(editorDir, "assets", "style.css"), Path.Combine(assets, "style.css"), true);
        var lic = Path.Combine(editorDir, "LIZENZEN.txt"); // MIT & Co. verlangen den Urheberhinweis auch in jeder weitergegebenen Seite
        if (File.Exists(lic)) File.Copy(lic, Path.Combine(assets, "LIZENZEN.txt"), true);
    }

    static void Write(string outDir, string rel, string text)
    {
        var f = Path.Combine(outDir, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.WriteAllText(f, text);
    }

    /// <summary>Eine Seite schreiben: Daten aufbereiten, Bilder und Schriften mitnehmen, Kopf mit SEO-Angaben, Platz für den vorgerenderten Inhalt.</summary>
    static Output WritePage(string dir, string raw, JsonObject? tokens, IReadOnlyList<WebDesignCms.Sammlung> cms, string outDir, string rel, string lang, string prefix,
        IReadOnlyList<string> pages, HashSet<string> entryPaths, IReadOnlyList<(string Lang, string Rel)> alts, string canonicalRel, WebDesignSite.Settings site, List<string> notes, List<JsonObject> usedDocs)
    {
        var json = WebDesignComponents.Inject(WebDesignTokens.Inject(raw, tokens), dir);
        var (data, err) = WebDesign.Validate(json);
        if (data is null) { notes.Add($"/{rel}: {err}"); data = WebDesign.Empty(); }
        var doc = JsonNode.Parse(data)!.AsObject();
        WebDesignCms.Resolve(doc, cms, lang, prefix, true);
        RewriteLinks(doc, rel, prefix, pages, entryPaths);
        var props = doc["root"]?["props"] as JsonObject ?? new JsonObject();
        props.Remove("medien"); props.Remove("sammlungen");
        var indexable = !(props["indexieren"] is JsonValue iv && iv.TryGetValue(out bool ix) && !ix);
        WebDesign.ExportAssets(doc, dir, outDir);
        usedDocs.Add(doc);
        var extraCss = WebDesign.PageCss(dir, doc) + TransitionCss(site.Uebergang);
        var head = new StringBuilder();
        var title = Str(props["title"]) is { Length: > 0 } t ? t : "Webseite";
        var description = Str(props["description"]);
        if (description.Length > 0) head.Append($"<meta name=\"description\" content=\"{Esc(description)}\" />\n");
        if (!indexable) head.Append("<meta name=\"robots\" content=\"noindex\" />\n");
        string Abs(string r) => site.Basis + "/" + (r == "index.html" ? "" : r.EndsWith("/index.html", StringComparison.Ordinal) ? r[..^10] : r);
        if (site.Basis.Length > 0)
        {
            head.Append($"<link rel=\"canonical\" href=\"{Esc(Abs(canonicalRel))}\" />\n");
            if (alts.Count > 1)
            {
                foreach (var (l, r) in alts) head.Append($"<link rel=\"alternate\" hreflang=\"{l}\" href=\"{Esc(Abs(r))}\" />\n");
                head.Append($"<link rel=\"alternate\" hreflang=\"x-default\" href=\"{Esc(Abs(alts.First(a => a.Lang == site.Standardsprache).Rel))}\" />\n");
            }
        }
        // Vorschaubild für Teilen-Vorschauen (Open Graph)
        head.Append($"<meta property=\"og:type\" content=\"website\" />\n<meta property=\"og:title\" content=\"{Esc(title)}\" />\n");
        if (description.Length > 0) head.Append($"<meta property=\"og:description\" content=\"{Esc(description)}\" />\n");
        head.Append($"<meta property=\"og:locale\" content=\"{lang.Replace('-', '_')}\" />\n");
        if (site.Basis.Length > 0) head.Append($"<meta property=\"og:url\" content=\"{Esc(Abs(canonicalRel))}\" />\n");
        if (Str(props["vorschaubild"]) is { Length: > 0 } img && WebDesign.IsSafeImagePath(img) && WebDesignCheck.IsLocalFile(dir, img))
        {
            WebDesign.CopyImage(img, dir, outDir);
            head.Append($"<meta property=\"og:image\" content=\"{Esc(site.Basis.Length > 0 ? site.Basis + "/" + img : img)}\" />\n");
        }
        // Startseite der Standardsprache behält „seite.js“ (bisheriger Export), alle anderen Seiten haben ihre Daten unter daten/<sprache>/
        var dataRel = rel == "index.html" ? "seite.js" : "daten/" + rel[..^5] + ".js"; // aus dem Ausgabepfad, damit eine nicht übersetzte Seite nicht das Original überschreibt
        Write(outDir, dataRel, "window.TRENDO_SEITE = " + doc.ToJsonString().Replace("</", "<\\/") + ";\n");
        var b = BaseOf(rel);
        Write(outDir, rel, "<!doctype html>\n" + $"<html lang=\"{lang}\">\n<head>\n<meta charset=\"UTF-8\" />\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n" +
            (b.Length > 0 ? $"<base href=\"{b}\" />\n" : "") + head + $"<title>{Esc(title)}</title>\n<link rel=\"stylesheet\" href=\"assets/style.css\">\n" +
            (extraCss.Length > 0 ? $"<style id=\"t-anpassungen\">\n{extraCss}\n</style>\n" : "") +
            $"<script src=\"{dataRel}\"></script>\n<script defer src=\"assets/editor.js\"></script>\n</head>\n<body>\n{RootMarker}\n<noscript>Diese Seite braucht JavaScript.</noscript>\n</body>\n</html>\n");
        return new(rel, lang, false, indexable);
    }

    /// <summary>W4: Übergang zwischen Seiten über View Transitions; ohne Unterstützung oder bei „weniger Bewegung“ wechselt die Seite einfach.</summary>
    public static string TransitionCss(string uebergang) => uebergang switch
    {
        "einblenden" => "\n@media (prefers-reduced-motion: no-preference) { @view-transition { navigation: auto; } }",
        "schieben" => "\n@media (prefers-reduced-motion: no-preference) { @view-transition { navigation: auto; } ::view-transition-old(root) { animation: 260ms ease-in both t-raus; } ::view-transition-new(root) { animation: 320ms ease-out both t-rein; } }" +
            "\n@keyframes t-raus { to { opacity: 0; transform: translateX(-24px); } }\n@keyframes t-rein { from { opacity: 0; transform: translateX(24px); } }",
        _ => "",
    };

    static readonly HashSet<string> LinkKeys = ["href", "knopfZiel", "knopf2Ziel", "ziel", "impressum", "datenschutz"];

    /// <summary>Interne Links passend zur Ausgabedatei: „ueber-uns“ → Seite in derselben Sprache, „#kontakt“ bleibt auf dieser Seite (trotz &lt;base&gt;).</summary>
    public static void RewriteLinks(JsonNode? n, string selfRel, string prefix, IReadOnlyList<string> pages, HashSet<string> entryPaths)
    {
        var deep = selfRel.Contains('/');
        switch (n)
        {
            case JsonObject o:
                foreach (var key in o.Select(kv => kv.Key).ToList())
                    if (LinkKeys.Contains(key) && o[key] is JsonValue v && v.TryGetValue(out string? s)) o[key] = Link(s, selfRel, prefix, pages, entryPaths, deep);
                    else RewriteLinks(o[key], selfRel, prefix, pages, entryPaths);
                break;
            case JsonArray a: foreach (var x in a) RewriteLinks(x, selfRel, prefix, pages, entryPaths); break;
        }
    }

    static string Link(string s, string selfRel, string prefix, IReadOnlyList<string> pages, HashSet<string> entryPaths, bool deep)
    {
        var t = s.Trim();
        if (t.Length == 0 || t.StartsWith('/') || t.Contains(':')) return s;
        if (t.StartsWith('#')) return t == "#" || !deep ? s : selfRel + t;
        var hash = t.IndexOf('#'); var anchor = hash >= 0 ? t[hash..] : ""; var path = hash >= 0 ? t[..hash] : t;
        var bare = path.EndsWith(".html", StringComparison.Ordinal) ? path[..^5] : path;
        if (bare is "" or "index") return prefix + "index.html" + anchor;
        if (pages.Contains(bare)) return prefix + bare + ".html" + anchor;
        if (entryPaths.Contains(bare)) return prefix + bare + ".html" + anchor;
        return s;
    }

    // ---------- Prüfung auf dem Export (W3) ----------

    /// <summary>Prüft den fertigen Export: Inhalt steht im HTML (ohne Skript), Titel und Beschreibung vorhanden, Sitemap vollständig.</summary>
    public static IReadOnlyList<string> CheckExport(string outDir, IReadOnlyList<Output> pages)
    {
        var res = new List<string>();
        foreach (var p in pages)
        {
            var f = Path.Combine(outDir, p.RelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(f)) { res.Add($"/{p.RelPath} fehlt im Export."); continue; }
            var html = File.ReadAllText(f);
            if (html.Contains(RootMarker, StringComparison.Ordinal)) res.Add($"/{p.RelPath}: Inhalt steht nicht im HTML (nur nach dem Laden per Skript). Suchmaschinen sehen ihn so schlechter.");
            if (!html.Contains("<title>", StringComparison.Ordinal) || html.Contains("<title>Webseite</title>", StringComparison.Ordinal)) res.Add($"/{p.RelPath}: kein eigener Seitentitel.");
            if (!html.Contains("name=\"description\"", StringComparison.Ordinal)) res.Add($"/{p.RelPath}: keine Beschreibung für Suchmaschinen.");
        }
        var sm = Path.Combine(outDir, "sitemap.xml");
        if (File.Exists(sm))
        {
            var text = File.ReadAllText(sm);
            foreach (var p in pages.Where(p => p.Indexable && !p.Fallback))
                if (!text.Contains("/" + (p.RelPath == "index.html" ? "" : p.RelPath.EndsWith("/index.html", StringComparison.Ordinal) ? p.RelPath[..^10] : p.RelPath) + "</loc>", StringComparison.Ordinal))
                    res.Add($"Sitemap: /{p.RelPath} fehlt.");
        }
        return res;
    }
}
