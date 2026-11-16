using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Knoten einer geprüften HTML-Nachricht: Text oder erlaubtes Element.</summary>
public abstract record MailHtmlNode;
public sealed record MailHtmlText(string Text) : MailHtmlNode;
public sealed record MailHtmlElement(string Tag, Dictionary<string, string> Attr, List<MailHtmlNode> Children) : MailHtmlNode;

/// <summary>Ergebnis der Prüfung: nur erlaubte Elemente; <paramref name="BlockedImages"/> = Bilder aus dem Internet, die (noch) nicht geladen werden.</summary>
public sealed record MailSafeHtml(MailHtmlElement Root, int BlockedImages, int Removed);

/// <summary>T-0689: HTML-Mails sicher machen. Statt Verbotenes herauszuschneiden wird die Nachricht aus einer festen Liste erlaubter Elemente
/// und Attribute neu aufgebaut: keine Skripte, keine Ereignis-Attribute, keine Formulare, Rahmen, Objekte oder Stilangaben; Links nur
/// http/https/mailto; Bilder nur eingebettet (cid/data) – Bilder aus dem Internet erst nach ausdrücklichem Klick.</summary>
public static partial class MailHtml
{
    static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "p", "div", "span", "br", "hr", "b", "strong", "i", "em", "u", "s", "strike", "del", "ins", "small", "big", "sub", "sup", "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "blockquote", "pre", "code", "a", "img", "table", "thead", "tbody", "tfoot", "tr", "td", "th", "center", "font", "dl", "dt", "dd",
    };
    /// <summary>Elemente, deren ganzer Inhalt entfällt.</summary>
    static readonly HashSet<string> Dropped = new(StringComparer.Ordinal)
    {
        "script", "style", "head", "title", "iframe", "frame", "frameset", "object", "embed", "applet", "form", "input", "button", "select", "option", "textarea",
        "svg", "math", "link", "meta", "base", "noscript", "template", "audio", "video", "source", "track", "canvas", "map", "area", "xml",
    };
    static readonly HashSet<string> Void = new(StringComparer.Ordinal) { "br", "hr", "img", "input", "meta", "link", "base", "source", "track", "area", "wbr", "col" };
    static readonly HashSet<string> RawText = new(StringComparer.Ordinal) { "script", "style", "textarea", "title", "xmp", "noscript" };
    public static readonly IReadOnlySet<string> Blocks = new HashSet<string>(StringComparer.Ordinal)
        { "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "blockquote", "pre", "table", "thead", "tbody", "tfoot", "tr", "td", "th", "center", "hr", "dl", "dt", "dd" };

    const int MaxNodes = 20000, MaxDepth = 60;

    [GeneratedRegex(@"^data:image/(png|jpe?g|gif|webp);base64,[A-Za-z0-9+/=\s]+$", RegexOptions.IgnoreCase)] private static partial Regex DataImage();

    /// <summary>Linkziel prüfen: nur http, https und mailto. Steuerzeichen und Leerraum im Schema (Trick „java\tscript:“) werden vorher entfernt.</summary>
    public static string? SafeLink(string href)
    {
        var h = string.Concat(System.Net.WebUtility.HtmlDecode(href).Where(c => !char.IsControl(c))).Trim();
        var scheme = Regex.Match(h, @"^([A-Za-z][A-Za-z0-9+.\-\s]*):");
        if (!scheme.Success) return null; // ohne Schema: kein sicheres Ziel
        var s = Regex.Replace(scheme.Groups[1].Value, @"\s", "").ToLowerInvariant();
        return s is "http" or "https" or "mailto" ? h : null;
    }

    /// <summary>HTML prüfen und als Baum erlaubter Elemente zurückgeben.</summary>
    public static MailSafeHtml Sanitize(string html, bool allowRemoteImages = false)
    {
        var root = new MailHtmlElement("div", [], []);
        var stack = new List<MailHtmlElement> { root };
        int nodes = 0, blocked = 0, removed = 0;
        var i = 0;
        void AddText(string raw)
        {
            if (raw.Length == 0 || nodes++ > MaxNodes) return;
            var pre = stack.Any(e => e.Tag == "pre");
            var t = System.Net.WebUtility.HtmlDecode(raw).Replace(' ', ' ');
            if (!pre) t = Regex.Replace(t, @"\s+", " ");
            if (t.Length > 0) stack[^1].Children.Add(new MailHtmlText(t));
        }
        void CloseTo(string tag)
        {
            var at = stack.FindLastIndex(e => e.Tag == tag);
            if (at > 0) stack.RemoveRange(at, stack.Count - at);
        }
        while (i < html.Length)
        {
            var lt = html.IndexOf('<', i);
            if (lt < 0) { AddText(html[i..]); break; }
            if (lt > i) AddText(html[i..lt]);
            if (html.AsSpan(lt).StartsWith("<!--")) { var end = html.IndexOf("-->", lt + 4, StringComparison.Ordinal); i = end < 0 ? html.Length : end + 3; continue; }
            if (lt + 1 < html.Length && html[lt + 1] is '!' or '?') { var end = html.IndexOf('>', lt); i = end < 0 ? html.Length : end + 1; continue; }
            var m = TagRx().Match(html, lt);
            if (!m.Success || m.Index != lt) { AddText("<"); i = lt + 1; continue; }
            i = lt + m.Length;
            var tag = m.Groups[2].Value.ToLowerInvariant();
            var closing = m.Groups[1].Length > 0;
            if (tag.Contains(':')) tag = tag[(tag.IndexOf(':') + 1)..]; // Namensraum (o:p, v:shape)
            if (closing)
            {
                if (tag == "p" && stack.FindLastIndex(e => e.Tag == "p") <= 0) continue;
                CloseTo(tag); continue;
            }
            if (Dropped.Contains(tag))
            {
                removed++;
                if (!Void.Contains(tag) && !m.Value.EndsWith("/>", StringComparison.Ordinal))
                {
                    // ganzen Inhalt überspringen (bei Skript/Stil bis zum schließenden Tag, sonst verschachtelt zählen)
                    var close = new Regex($@"<\s*/\s*{Regex.Escape(m.Groups[2].Value)}\s*>", RegexOptions.IgnoreCase);
                    if (RawText.Contains(tag)) { var c = close.Match(html, i); i = c.Success ? c.Index + c.Length : html.Length; }
                    else
                    {
                        var open = new Regex($@"<\s*{Regex.Escape(m.Groups[2].Value)}\b", RegexOptions.IgnoreCase); var level = 1;
                        while (level > 0 && i < html.Length)
                        {
                            var c = close.Match(html, i); if (!c.Success) { i = html.Length; break; }
                            var o = open.Match(html, i);
                            if (o.Success && o.Index < c.Index) { level++; i = o.Index + o.Length; } else { level--; i = c.Index + c.Length; }
                        }
                    }
                }
                continue;
            }
            if (!Allowed.Contains(tag)) continue; // unbekannt: Hülle weg, Inhalt bleibt
            // stillschweigend schließende Elemente
            if (tag == "p" || Blocks.Contains(tag) && tag is not ("li" or "tr" or "td" or "th")) { if (stack[^1].Tag == "p") stack.RemoveAt(stack.Count - 1); }
            if (tag == "li" && stack.FindLastIndex(e => e.Tag == "li") is > 0 and var li && stack.FindLastIndex(e => e.Tag is "ul" or "ol") < li) stack.RemoveRange(li, stack.Count - li);
            if (tag is "td" or "th" && stack.FindLastIndex(e => e.Tag is "td" or "th") is > 0 and var td && stack.FindLastIndex(e => e.Tag is "table" or "tr") < td) stack.RemoveRange(td, stack.Count - td);
            if (tag == "tr" && stack.FindLastIndex(e => e.Tag == "tr") is > 0 and var tr && stack.FindLastIndex(e => e.Tag == "table") < tr) stack.RemoveRange(tr, stack.Count - tr);
            if (nodes++ > MaxNodes) continue;
            var attr = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match a in AttrRx().Matches(m.Groups[3].Value))
            {
                var name = a.Groups[1].Value.ToLowerInvariant();
                var value = a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Success ? a.Groups[4].Value : a.Groups[5].Value;
                value = System.Net.WebUtility.HtmlDecode(value);
                if (name.StartsWith("on", StringComparison.Ordinal) || name is "style" or "srcset" or "background" or "formaction" or "xlink:href" or "srcdoc") { if (name != "style") removed++; continue; }
                if (tag == "a" && name == "href") { if (SafeLink(value) is { } ok) attr["href"] = ok; else removed++; }
                else if (tag == "img" && name == "src")
                {
                    var src = string.Concat(value.Where(c => !char.IsControl(c))).Trim();
                    if (src.StartsWith("cid:", StringComparison.OrdinalIgnoreCase)) attr["src"] = "cid:" + src[4..].Trim();
                    else if (DataImage().IsMatch(src)) attr["src"] = src;
                    else if (Regex.IsMatch(src, @"^https?://", RegexOptions.IgnoreCase)) { if (allowRemoteImages) attr["src"] = src; else { attr["blocked"] = "1"; blocked++; } }
                    else removed++;
                }
                else if (tag == "img" && name is "alt" or "title") attr[name] = value;
                else if (tag == "img" && name is "width" or "height" && int.TryParse(value.Replace("px", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var px) && px is > 0 and < 4000) attr[name] = px.ToString(CultureInfo.InvariantCulture);
                else if (tag is "td" or "th" && name == "colspan" && int.TryParse(value, out var cs) && cs is > 0 and < 50) attr[name] = cs.ToString(CultureInfo.InvariantCulture);
            }
            var el = new MailHtmlElement(tag, attr, []);
            stack[^1].Children.Add(el);
            if (!Void.Contains(tag) && !m.Value.EndsWith("/>", StringComparison.Ordinal) && stack.Count < MaxDepth) stack.Add(el);
        }
        return new(root, blocked, removed);
    }

    [GeneratedRegex(@"<(/?)\s*([A-Za-z][A-Za-z0-9:\-]*)((?:\s+[^\s""'<>/=]+(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'<>]+))?|\s*/|\s+)*)\s*>", RegexOptions.Singleline)] private static partial Regex TagRx();
    [GeneratedRegex(@"([^\s""'<>/=]+)(\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'<>]+)))?", RegexOptions.Singleline)] private static partial Regex AttrRx();

    /// <summary>Alle Knoten des Baums (für Prüfungen und Suche).</summary>
    public static IEnumerable<MailHtmlNode> All(MailHtmlNode n)
    {
        yield return n;
        if (n is MailHtmlElement e) foreach (var c in e.Children) foreach (var x in All(c)) yield return x;
    }

    /// <summary>Reiner Text der geprüften Nachricht.</summary>
    public static string TextOf(MailHtmlNode n)
    {
        var sb = new StringBuilder();
        void Go(MailHtmlNode x)
        {
            if (x is MailHtmlText t) { sb.Append(t.Text); return; }
            var e = (MailHtmlElement)x;
            if (e.Tag == "br") sb.Append('\n');
            foreach (var c in e.Children) Go(c);
            if (Blocks.Contains(e.Tag) && sb.Length > 0 && sb[^1] != '\n') sb.Append('\n');
        }
        Go(n);
        return Regex.Replace(sb.ToString(), @"[ \t]*\n[ \t]*", "\n").Trim();
    }

    /// <summary>Geprüfte Nachricht als eigenständige HTML-Seite zum Ansehen im Browser. Zusätzliche Sperre im Dokument selbst
    /// (Content-Security-Policy): keine Skripte, keine Nachlader; Bilder nur eingebettet – aus dem Internet nur, wenn ausdrücklich erlaubt.</summary>
    public static string ToDocument(MailSafeHtml safe, string title, Func<string, MailAttachment?>? cid = null, bool allowRemoteImages = false)
    {
        static string E(string s) => System.Net.WebUtility.HtmlEncode(s);
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"de\"><head><meta charset=\"utf-8\">\n");
        sb.Append($"<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:{(allowRemoteImages ? " https: http:" : "")}; style-src 'unsafe-inline'\">\n");
        sb.Append("<meta name=\"referrer\" content=\"no-referrer\">\n");
        sb.Append($"<title>{E(title)}</title>\n<style>body{{font-family:Segoe UI,Arial,sans-serif;max-width:860px;margin:24px auto;padding:0 16px;line-height:1.5}}img{{max-width:100%;height:auto}}blockquote{{border-left:3px solid #bbb;margin-left:0;padding-left:12px;color:#444}}td,th{{padding:2px 6px;vertical-align:top}}</style>\n</head><body>\n");
        void Go(MailHtmlNode n)
        {
            if (n is MailHtmlText t) { sb.Append(E(t.Text)); return; }
            var e = (MailHtmlElement)n;
            sb.Append('<').Append(e.Tag);
            foreach (var (k, v) in e.Attr)
            {
                if (k == "blocked") continue;
                if (e.Tag == "img" && k == "src" && v.StartsWith("cid:", StringComparison.Ordinal))
                {
                    if (cid?.Invoke(v[4..]) is { } a && Regex.IsMatch(a.ContentType, @"^image/(png|jpe?g|gif|webp)$")) sb.Append($" src=\"data:{a.ContentType};base64,{Convert.ToBase64String(a.Data)}\"");
                    continue;
                }
                sb.Append(' ').Append(k).Append("=\"").Append(E(v)).Append('"');
            }
            if (e.Tag == "a") sb.Append(" rel=\"noopener noreferrer\" target=\"_blank\"");
            if (e.Tag == "img" && e.Attr.ContainsKey("blocked")) sb.Append(" alt=\"[Bild aus dem Internet – nicht geladen]\"");
            sb.Append('>');
            if (Void.Contains(e.Tag)) return;
            foreach (var c in e.Children) Go(c);
            sb.Append("</").Append(e.Tag).Append('>');
        }
        foreach (var c in safe.Root.Children) Go(c);
        return sb.Append("\n</body></html>\n").ToString();
    }
}
