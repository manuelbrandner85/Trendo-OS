using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>K6 und Z8: Bausteine als lesbares HTML (Code neben dem Design) und HTML zurück in Bausteine.
/// Das Einlesen übernimmt der Kern von GrapesJS (BSD-3-Clause) im Browser und liefert einen Baum ({tag, attrs, kinder} oder {text});
/// hier wird der Baum geprüft und auf Bausteine abgebildet. Was nicht abbildbar ist, bleibt als HTML-Block erhalten und steht in der Meldeliste.
/// HTML-Blöcke speichern keinen HTML-Text, sondern einen geprüften Baum aus erlaubten Elementen; der Editor baut daraus Elemente (kein innerHTML).</summary>
public static partial class WebDesignHtml
{
    public sealed record Result(JsonArray Content, IReadOnlyList<string> Report, int Mapped, int HtmlBlocks);

    public const int MaxNodes = 4000, MaxDepth = 32;
    /// <summary>Erlaubte Elemente in HTML-Blöcken (Text, Listen, Tabellen, Gliederung). Alles andere wird entpackt oder, wenn gefährlich, entfernt.</summary>
    public static readonly HashSet<string> AllowedTags = ["p", "br", "strong", "b", "em", "i", "u", "s", "small", "span", "a", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "code",
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "figure", "figcaption", "img", "hr", "div", "section", "article", "header", "footer", "nav", "aside", "dl", "dt", "dd", "sup", "sub", "mark", "abbr", "time", "address", "main"];
    /// <summary>Elemente, deren Inhalt kein Seiteninhalt ist oder die Code ausführen: werden samt Inhalt entfernt.</summary>
    public static readonly HashSet<string> DropTags = ["script", "style", "iframe", "object", "embed", "template", "svg", "math", "canvas", "noscript", "audio", "video", "input", "select", "textarea", "link", "meta", "base", "frame", "frameset", "applet", "head", "title"];
    static readonly Dictionary<string, string[]> AllowedAttrs = new() { ["a"] = ["href", "title"], ["img"] = ["src", "alt", "width", "height"], ["td"] = ["colspan", "rowspan"], ["th"] = ["colspan", "rowspan", "scope"], ["time"] = ["datetime"], ["abbr"] = ["title"], ["ol"] = ["start"] };

    [GeneratedRegex(@"\s+")] private static partial Regex Ws();
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,60}$")] private static partial Regex IdPattern();
    [GeneratedRegex("^[0-9]{1,4}$")] private static partial Regex SmallNumber();

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";
    public static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // ---------- Bausteine → HTML (K6) ----------

    /// <summary>Inhalt einer Seite als HTML. Jeder Baustein ist ein Element mit data-typ und data-b (id); Felder stehen als Elemente mit data-feld,
    /// Auswahlwerte als data-p-…, alles Übrige (Bewegung, Anpassungen, Überschreibungen) als data-json-…. So lässt sich der Code verlustfrei zurücklesen.</summary>
    public static string ToHtml(JsonObject doc)
    {
        var sb = new StringBuilder();
        if (doc["content"] is JsonArray c) foreach (var b in c.OfType<JsonObject>()) Block(sb, b, 0);
        return sb.ToString();
    }

    static void Block(StringBuilder sb, JsonObject b, int depth)
    {
        var type = Str(b["type"]); var p = b["props"] as JsonObject ?? new JsonObject();
        var pad = new string(' ', depth * 2);
        var fields = WebDesignActions.Fields(type);
        var known = fields.Select(f => f.Key).ToHashSet();
        sb.Append(pad).Append($"<section data-typ=\"{Esc(type)}\" data-b=\"{Esc(Str(p["id"]))}\"");
        foreach (var f in fields.Where(f => f.Kind is "wahl" or "zahl" or "farbe" or "stufe" or "email"))
            if (p[f.Key] is JsonValue v) sb.Append($" data-p-{f.Key.ToLowerInvariant()}=\"{Esc(v.TryGetValue(out string? s) ? s : v.ToJsonString())}\"");
        foreach (var (k, v) in p)
            if (k is not "id" and not "eintraege" and not "knoten" && !known.Contains(k) && v is not null && IdPattern().IsMatch(k)) sb.Append($" data-json-{k.ToLowerInvariant()}=\"{Esc(v.ToJsonString())}\"");
        sb.Append(">\n");
        var inner = pad + "  ";
        foreach (var f in fields)
            switch (f.Kind)
            {
                case "text": sb.Append(inner).Append($"<p data-feld=\"{f.Key}\">{TextHtml(Str(p[f.Key]))}</p>\n"); break;
                case "link": sb.Append(inner).Append($"<a data-feld=\"{f.Key}\" href=\"{Esc(Str(p[f.Key]))}\">{Esc(Str(p[f.Key]))}</a>\n"); break;
                case "bild": sb.Append(inner).Append($"<img data-feld=\"{f.Key}\" src=\"{Esc(Str(p[f.Key]))}\" alt=\"\">\n"); break;
                case "liste":
                    sb.Append(inner).Append($"<ul data-liste=\"{f.Key}\">\n");
                    if (p[f.Key] is JsonArray items)
                        foreach (var item in items.OfType<JsonObject>())
                        {
                            sb.Append(inner).Append("  <li>");
                            foreach (var sf in f.Sub)
                                sb.Append(sf.Kind switch
                                {
                                    "link" => $"<a data-feld=\"{sf.Key}\" href=\"{Esc(Str(item[sf.Key]))}\">{Esc(Str(item[sf.Key]))}</a>",
                                    "bild" => $"<img data-feld=\"{sf.Key}\" src=\"{Esc(Str(item[sf.Key]))}\" alt=\"\">",
                                    "text" => $"<span data-feld=\"{sf.Key}\">{TextHtml(Str(item[sf.Key]))}</span>",
                                    _ => $"<span data-feld=\"{sf.Key}\" data-wert=\"{Esc(item[sf.Key] is JsonValue sv ? (sv.TryGetValue(out string? ss) ? ss : sv.ToJsonString()) : "")}\"></span>",
                                });
                            sb.Append("</li>\n");
                        }
                    sb.Append(inner).Append("</ul>\n");
                    break;
                case "slot":
                    sb.Append(inner).Append($"<div data-slot=\"{f.Key}\">\n");
                    if (p[f.Key] is JsonArray kids) foreach (var k in kids.OfType<JsonObject>()) Block(sb, k, depth + 2);
                    sb.Append(inner).Append("</div>\n");
                    break;
            }
        if (type == "Html" && p["knoten"] is JsonArray nodes) foreach (var n in nodes) sb.Append(inner).Append(NodeHtml(n)).Append('\n');
        sb.Append(pad).Append("</section>\n");
    }

    static string TextHtml(string s) => Esc(s).Replace("\r", "").Replace("\n", "<br>");

    /// <summary>Geprüften Baum als HTML schreiben (für die Code-Ansicht).</summary>
    public static string NodeHtml(JsonNode? n)
    {
        if (n is not JsonObject o) return "";
        if (o["text"] is not null) return Esc(Str(o["text"]));
        var tag = Str(o["tag"]);
        if (!AllowedTags.Contains(tag)) return "";
        var sb = new StringBuilder("<" + tag);
        if (o["attrs"] is JsonObject a) foreach (var (k, v) in a) sb.Append($" {k}=\"{Esc(Str(v))}\"");
        sb.Append('>');
        if (tag is "br" or "hr" or "img") return sb.ToString();
        if (o["kinder"] is JsonArray kids) foreach (var k in kids) sb.Append(NodeHtml(k));
        return sb.Append("</").Append(tag).Append('>').ToString();
    }

    /// <summary>K6: Zeile im Code, in der der Baustein mit dieser id beginnt (1-basiert), sonst 0.</summary>
    public static int LineOf(string html, string id)
    {
        var needle = $"data-b=\"{Esc(id)}\"";
        var lines = html.Split('\n');
        for (var i = 0; i < lines.Length; i++) if (lines[i].Contains(needle, StringComparison.Ordinal)) return i + 1;
        return 0;
    }

    // ---------- Baum prüfen ----------

    /// <summary>Baum auf erlaubte Elemente und Attribute zurückschneiden. Links nur mit erlaubter Adresse, Bilder nur als Bilddatei im Projekt.
    /// Gefährliches fällt samt Inhalt weg, harmlose unbekannte Hüllen werden entpackt (der Text bleibt).</summary>
    public static JsonArray Sanitize(JsonNode? nodes, List<string>? report = null)
    {
        var count = 0; var dropped = new SortedSet<string>(StringComparer.Ordinal); var unwrapped = new SortedSet<string>(StringComparer.Ordinal);
        JsonArray Walk(JsonNode? n, int depth)
        {
            var res = new JsonArray();
            if (n is not JsonArray arr || depth > MaxDepth) return res;
            foreach (var item in arr)
            {
                if (++count > MaxNodes) break;
                if (item is not JsonObject o) continue;
                if (o["text"] is not null) { var t = Str(o["text"]); if (t.Length > 0) res.Add(new JsonObject { ["text"] = t.Length > 20_000 ? t[..20_000] : t }); continue; }
                var tag = Str(o["tag"]).ToLowerInvariant();
                if (DropTags.Contains(tag)) { dropped.Add(tag); continue; }
                var kids = Walk(o["kinder"], depth + 1);
                if (!AllowedTags.Contains(tag)) { unwrapped.Add(tag.Length > 0 ? tag : "?"); foreach (var k in kids.ToList()) { kids.Remove(k); res.Add(k); } continue; }
                var attrs = new JsonObject();
                if (o["attrs"] is JsonObject a && AllowedAttrs.TryGetValue(tag, out var allowed))
                    foreach (var name in allowed)
                    {
                        var v = Str(a[name]);
                        if (v.Length == 0 || v.Length > 2000 || v.Any(char.IsControl)) continue;
                        if (name == "href" && !WebDesign.IsAllowedUrl(v)) continue;
                        if (name == "src" && !WebDesign.IsSafeImagePath(v)) continue;
                        if (name is "colspan" or "rowspan" or "width" or "height" or "start" && !SmallNumber().IsMatch(v)) continue;
                        attrs[name] = v;
                    }
                if (tag == "img" && attrs["src"] is null) { dropped.Add("img (ohne Bild im Projekt)"); continue; }
                res.Add(new JsonObject { ["tag"] = tag, ["attrs"] = attrs, ["kinder"] = kids });
            }
            return res;
        }
        var result = Walk(nodes, 0);
        if (report is not null)
        {
            if (dropped.Count > 0) report.Add("Entfernt (kein Seiteninhalt oder nicht sicher): " + string.Join(", ", dropped) + ".");
            if (unwrapped.Count > 0) report.Add("Hülle entfernt, Inhalt behalten: " + string.Join(", ", unwrapped) + ".");
            if (count > MaxNodes) report.Add($"Sehr großes HTML: nach {MaxNodes} Elementen abgeschnitten.");
        }
        return result;
    }

    /// <summary>Text eines Teilbaums wie im Browser: Leerraum zusammengefasst, &lt;br&gt; als Zeilenumbruch.</summary>
    public static string TextOf(JsonNode? n)
    {
        var sb = new StringBuilder();
        void Walk(JsonNode? x)
        {
            if (x is not JsonObject o) return;
            if (o["text"] is not null) { sb.Append(Ws().Replace(Str(o["text"]), " ")); return; }
            if (Str(o["tag"]) == "br") { sb.Append('\n'); return; }
            if (o["kinder"] is JsonArray k) foreach (var c in k) Walk(c);
        }
        Walk(n);
        return string.Join('\n', sb.ToString().Split('\n').Select(l => l.Trim())).Trim();
    }

    // ---------- HTML-Baum → Bausteine (K6 zurück, Z8) ----------

    /// <summary>Kontext beim Einlesen: Bilder aus einem Quellordner werden ins Projekt kopiert (nur Dateien innerhalb des Quellordners).</summary>
    public sealed class Context(string projectDir, string? sourceDir)
    {
        public string ProjectDir { get; } = projectDir;
        public string? SourceDir { get; } = sourceDir;
        public List<string> Report { get; } = [];
        public int Mapped, HtmlBlocks, Ids;
        public HashSet<string> UsedIds { get; } = [];
        public string NewId(string type) { string id; do id = $"{type.ToLowerInvariant()}-i{++Ids}"; while (!UsedIds.Add(id)); return id; }
    }

    static readonly HashSet<string> Containers = ["div", "main", "body", "span", "center", "font"];
    static readonly HashSet<string> Areas = ["section", "article", "header", "footer", "aside"];
    static readonly HashSet<string> Inline = ["strong", "b", "em", "i", "u", "s", "small", "span", "a", "sup", "sub", "mark", "abbr", "time", "code"];

    /// <summary>Baum in Bausteine übersetzen. Eigene Bausteine (data-typ) kommen verlustfrei zurück, fremdes HTML wird so gut wie möglich abgebildet.</summary>
    public static Result FromTree(JsonArray tree, Context ctx)
    {
        foreach (var id in Ids(tree)) ctx.UsedIds.Add(id);
        var content = Blocks(tree, ctx, 0);
        return new(content, ctx.Report, ctx.Mapped, ctx.HtmlBlocks);
    }

    static IEnumerable<string> Ids(JsonNode? n)
    {
        if (n is JsonArray a) { foreach (var x in a) foreach (var i in Ids(x)) yield return i; yield break; }
        if (n is not JsonObject o) yield break;
        if (Str(o["attrs"]?["data-b"]) is { Length: > 0 } id) yield return id;
        foreach (var i in Ids(o["kinder"])) yield return i;
    }

    static JsonArray Blocks(JsonNode? nodes, Context ctx, int depth)
    {
        var res = new JsonArray();
        if (nodes is not JsonArray arr || depth > MaxDepth) return res;
        var pendingInline = new JsonArray(); // lose Texte und Inline-Elemente zwischen Blöcken sammeln
        void Flush()
        {
            if (pendingInline.Count == 0) return;
            var p = new JsonObject { ["tag"] = "p", ["attrs"] = new JsonObject(), ["kinder"] = pendingInline };
            pendingInline = new JsonArray();
            if (TextOf(p).Length == 0) return;
            res.Add(Paragraph(p, ctx));
        }
        foreach (var item in arr)
        {
            if (item is not JsonObject o) continue;
            if (o["text"] is not null) { pendingInline.Add(o.DeepClone()); continue; }
            var tag = Str(o["tag"]).ToLowerInvariant();
            if (Inline.Contains(tag) && Str(o["attrs"]?["data-typ"]).Length == 0 && !(tag == "a" && Standalone(arr, o))) { pendingInline.Add(o.DeepClone()); continue; }
            Flush();
            foreach (var b in Element(o, ctx, depth)) res.Add(b);
        }
        Flush();
        return res;
    }

    static bool Standalone(JsonArray siblings, JsonObject a) => siblings.OfType<JsonObject>().All(x => x == a || x["text"] is not null && Str(x["text"]).Trim().Length == 0 || Str(x["tag"]) is not ("" or "strong" or "em" or "b" or "i" or "span"));

    static IEnumerable<JsonObject> Element(JsonObject o, Context ctx, int depth)
    {
        var tag = Str(o["tag"]).ToLowerInvariant();
        var attrs = o["attrs"] as JsonObject ?? new JsonObject();
        var typ = Str(attrs["data-typ"]);
        if (typ.Length > 0 && WebDesignActions.ComponentNames.Contains(typ)) { yield return Own(o, typ, ctx, depth); yield break; }
        if (DropTags.Contains(tag)) { ctx.Report.Add($"<{tag}> nicht übernommen (kein Seiteninhalt oder nicht sicher)."); yield break; }
        switch (tag)
        {
            case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                ctx.Mapped++;
                if (tag == "h1") ctx.Report.Add("Hauptüberschrift „" + Short(TextOf(o)) + "“ als Überschrift (h2) übernommen; für eine große Startüberschrift eignet sich der Baustein Hero.");
                yield return New(ctx, "Ueberschrift", new JsonObject { ["text"] = TextOf(o), ["level"] = tag is "h1" or "h2" ? "h2" : "h3" });
                yield break;
            case "p":
                yield return Paragraph(o, ctx); yield break;
            case "img":
                if (Image(attrs, ctx) is { } src) { ctx.Mapped++; yield return New(ctx, "Bild", new JsonObject { ["src"] = src, ["alt"] = Str(attrs["alt"]) }); }
                yield break;
            case "a":
                ctx.Mapped++;
                yield return New(ctx, "Knopf", new JsonObject { ["label"] = TextOf(o), ["href"] = Link(Str(attrs["href"])) });
                yield break;
            case "nav":
                var links = Links(o);
                if (links.Count > 0 && OnlyLinks(o)) // reine Linkliste: Navigation; mit weiterem Inhalt bleibt sie HTML
                {
                    ctx.Mapped++;
                    yield return New(ctx, "Navigation", new JsonObject { ["marke"] = "", ["links"] = new JsonArray(links.Take(12).Cast<JsonNode>().ToArray()), ["knopf"] = "", ["knopfZiel"] = "#" });
                    yield break;
                }
                break;
        }
        if (Areas.Contains(tag) && depth < 2)
        {
            var inner = Blocks(o["kinder"], ctx, depth + 1);
            if (inner.Count == 0) yield break;
            ctx.Mapped++;
            yield return New(ctx, "Bereich", new JsonObject { ["content"] = inner, ["tone"] = "hell" });
            yield break;
        }
        if (Containers.Contains(tag) || Areas.Contains(tag))
        {
            var flat = Blocks(o["kinder"], ctx, depth + 1);
            var list = flat.OfType<JsonObject>().ToList(); flat.Clear(); // aus der Zwischenliste lösen, damit die Bausteine neu eingehängt werden können
            foreach (var b in list) yield return b;
            yield break;
        }
        // Rest (Listen, Tabellen, Zitate, Formulare …): als HTML-Block erhalten
        yield return HtmlBlock(new JsonArray(o.DeepClone()), ctx, $"<{tag}>");
    }

    static bool OnlyLinks(JsonObject nav)
    {
        bool Ok(JsonNode? n) => n is not JsonObject o || o["text"] is not null && Str(o["text"]).Trim().Length == 0 || Str(o["tag"]) is "a" || Str(o["tag"]) is "ul" or "ol" or "li" or "div" or "span" && (o["kinder"] as JsonArray ?? []).All(Ok);
        return (nav["kinder"] as JsonArray ?? []).All(Ok);
    }

    static List<JsonObject> Links(JsonObject n)
    {
        var res = new List<JsonObject>();
        void Walk(JsonNode? x) { if (x is not JsonObject o) return; if (Str(o["tag"]) == "a") { res.Add(new JsonObject { ["label"] = TextOf(o), ["href"] = Link(Str(o["attrs"]?["href"])) }); return; } if (o["kinder"] is JsonArray k) foreach (var c in k) Walk(c); }
        Walk(n);
        return res;
    }

    static string Short(string s) => s.Length > 40 ? s[..40] + "…" : s;
    static string Link(string href) => href.Length > 0 && WebDesign.IsAllowedUrl(href) ? href : "#";

    static JsonObject Paragraph(JsonObject p, Context ctx)
    {
        // Nur Text und Zeilenumbrüche: Text-Baustein. Mit Formatierung oder Links: HTML-Block, damit nichts verloren geht.
        bool Plain(JsonNode? n) => n is JsonObject o && (o["text"] is not null || Str(o["tag"]) == "br");
        if ((p["kinder"] as JsonArray ?? []).All(Plain)) { ctx.Mapped++; return New(ctx, "Text", new JsonObject { ["text"] = TextOf(p), ["align"] = "left" }); }
        return HtmlBlock(new JsonArray(p.DeepClone()), ctx, null);
    }

    static JsonObject HtmlBlock(JsonArray nodes, Context ctx, string? what)
    {
        CopyImages(nodes, ctx);
        var clean = Sanitize(nodes, ctx.Report);
        ctx.HtmlBlocks++;
        if (what is not null) ctx.Report.Add($"{what} „{Short(TextOf(new JsonObject { ["kinder"] = clean.DeepClone() }))}“ bleibt als HTML-Block erhalten.");
        return New(ctx, "Html", new JsonObject { ["knoten"] = clean });
    }

    static void CopyImages(JsonNode? n, Context ctx)
    {
        if (n is JsonArray a) { foreach (var x in a) CopyImages(x, ctx); return; }
        if (n is not JsonObject o) return;
        if (Str(o["tag"]) == "img" && o["attrs"] is JsonObject at) { var src = Image(at, ctx); if (src is null) at.Remove("src"); else at["src"] = src; }
        CopyImages(o["kinder"], ctx);
    }

    /// <summary>Bild übernehmen: schon im Projekt → bleibt; relativ zum Quellordner → wird nach bilder/import/ kopiert; aus dem Internet → abgelehnt.</summary>
    static string? Image(JsonObject attrs, Context ctx)
    {
        var src = Str(attrs["src"]).Trim();
        if (src.Length == 0) return null;
        if (src.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || src.StartsWith("https:", StringComparison.OrdinalIgnoreCase) || src.StartsWith("//", StringComparison.Ordinal) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        { ctx.Report.Add($"Bild „{Short(src)}“ liegt nicht im Ordner der Seite und wurde nicht übernommen. Lade es über die Medien des Projekts."); return null; }
        if (ctx.SourceDir is null) return WebDesign.IsSafeImagePath(src) && WebDesignCheck.IsLocalFile(ctx.ProjectDir, src) ? src : null;
        if (!WebDesign.IsSafeImagePath(src)) { ctx.Report.Add($"Bild „{Short(src)}“ ist keine Bilddatei im Ordner der Seite."); return null; }
        var root = Path.GetFullPath(ctx.SourceDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var file = Path.GetFullPath(Path.Combine(ctx.SourceDir, src.Split('#', '?')[0].Replace('/', Path.DirectorySeparatorChar)));
        if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(file) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
        { ctx.Report.Add($"Bild „{Short(src)}“ fehlt im Ordner der Seite."); return null; }
        var target = Path.Combine(ctx.ProjectDir, "bilder", "import");
        Directory.CreateDirectory(target);
        var name = Path.GetFileName(file); var dest = Path.Combine(target, name);
        for (var i = 2; File.Exists(dest) && !SameFile(dest, file); i++) dest = Path.Combine(target, $"{Path.GetFileNameWithoutExtension(name)}-{i}{Path.GetExtension(name)}");
        if (!File.Exists(dest)) File.Copy(file, dest);
        return "bilder/import/" + Path.GetFileName(dest);
    }

    static bool SameFile(string a, string b) => new FileInfo(a).Length == new FileInfo(b).Length && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));

    static JsonObject New(Context ctx, string type, JsonObject props)
    {
        props["id"] = ctx.NewId(type);
        return new JsonObject { ["type"] = type, ["props"] = props };
    }

    /// <summary>Eigener Baustein aus dem Code zurück: Felder aus data-feld, Werte aus data-p-…, Übriges aus data-json-….</summary>
    static JsonObject Own(JsonObject el, string type, Context ctx, int depth)
    {
        var attrs = el["attrs"] as JsonObject ?? new JsonObject();
        var props = new JsonObject();
        var id = Str(attrs["data-b"]);
        props["id"] = IdPattern().IsMatch(id) ? id : ctx.NewId(type);
        var fields = WebDesignActions.Fields(type);
        foreach (var f in fields)
            switch (f.Kind)
            {
                case "text": if (Field(el, f.Key) is { } t) props[f.Key] = TextOf(t); break;
                case "link": if (Field(el, f.Key) is { } l) props[f.Key] = Link(Str(l["attrs"]?["href"])); break;
                case "bild": if (Field(el, f.Key) is { } im) props[f.Key] = Str(im["attrs"]?["src"]) is { Length: > 0 } s && (WebDesign.IsSafeImagePath(s) || s.StartsWith("{{", StringComparison.Ordinal)) ? s : ""; break;
                case "wahl" or "farbe" or "email": if (attrs["data-p-" + f.Key.ToLowerInvariant()] is { } v) props[f.Key] = Str(v); break;
                case "zahl" or "stufe":
                    if (attrs["data-p-" + f.Key.ToLowerInvariant()] is { } z && double.TryParse(Str(z), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) props[f.Key] = d == Math.Floor(d) && Math.Abs(d) < int.MaxValue ? (int)d : d;
                    break;
                case "liste":
                    if (Find(el, x => Str(x["attrs"]?["data-liste"]) == f.Key) is { } ul)
                    {
                        var items = new JsonArray();
                        foreach (var li in (ul["kinder"] as JsonArray ?? []).OfType<JsonObject>().Where(x => Str(x["tag"]) == "li"))
                        {
                            var item = new JsonObject();
                            foreach (var sf in f.Sub)
                                if (Field(li, sf.Key) is { } fe)
                                    item[sf.Key] = sf.Kind switch
                                    {
                                        "link" => Link(Str(fe["attrs"]?["href"])),
                                        "bild" => Str(fe["attrs"]?["src"]) is { Length: > 0 } s2 && WebDesign.IsSafeImagePath(s2) ? s2 : "",
                                        "text" => TextOf(fe),
                                        _ => Str(fe["attrs"]?["data-wert"]),
                                    };
                            items.Add(item);
                        }
                        props[f.Key] = items;
                    }
                    break;
                case "slot":
                    props[f.Key] = Find(el, x => Str(x["attrs"]?["data-slot"]) == f.Key) is { } slot ? Blocks(slot["kinder"], ctx, depth + 1) : new JsonArray();
                    break;
            }
        foreach (var (k, v) in attrs)
            if (k.StartsWith("data-json-", StringComparison.Ordinal) && k.Length > 10)
            {
                var key = JsonKey(k[10..], fields);
                try { if (key is not null && JsonNode.Parse(Str(v)) is { } node) props[key] = node; }
                catch (JsonException) { ctx.Report.Add($"{type} {props["id"]}: Angabe „{key}“ war beschädigt und wurde weggelassen."); }
            }
        if (type == "Html")
        {
            var nodes = new JsonArray((el["kinder"] as JsonArray ?? []).Select(x => x?.DeepClone()).ToArray());
            CopyImages(nodes, ctx);
            props["knoten"] = Sanitize(nodes, ctx.Report);
            ctx.HtmlBlocks++;
        }
        else ctx.Mapped++;
        return new JsonObject { ["type"] = type, ["props"] = props };
    }

    /// <summary>HTML-Attribute sind kleingeschrieben; bekannte gemischte Namen (z. B. „ueberschreibungen“, „anpassungen“) wiederherstellen.</summary>
    static string? JsonKey(string lower, IReadOnlyList<WebDesignActions.FeldInfo> fields)
    {
        string[] known = ["anpassungen", "bewegung", "zeitleiste", "komponente", "ueberschreibungen", "motion", "seconds"];
        return known.FirstOrDefault(k => k == lower) ?? fields.FirstOrDefault(f => f.Key.ToLowerInvariant() == lower)?.Key ?? (IdPattern().IsMatch(lower) ? lower : null);
    }

    /// <summary>Erstes Element mit data-feld = key innerhalb dieses Bausteins (nicht in verschachtelten Bausteinen oder Listen).</summary>
    static JsonObject? Field(JsonObject el, string key) => Find(el, x => Str(x["attrs"]?["data-feld"]) == key);

    static JsonObject? Find(JsonObject el, Func<JsonObject, bool> match)
    {
        foreach (var k in (el["kinder"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (k["text"] is not null) continue;
            if (match(k)) return k;
            if (Str(k["attrs"]?["data-typ"]).Length > 0 || Str(k["attrs"]?["data-liste"]).Length > 0 || Str(k["attrs"]?["data-slot"]).Length > 0) continue;
            if (Find(k, match) is { } r) return r;
        }
        return null;
    }
}
