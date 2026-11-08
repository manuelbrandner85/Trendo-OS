using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 11 – SYS-08: findet den eigentlichen Artikel einer Seite, ohne Menüs, Werbung und Fußzeilen. Eigene Umsetzung des Verfahrens von
/// mozilla/readability (Apache-2.0): Absätze werden bewertet, ihre Punkte wandern zu den umgebenden Containern, der beste Container gewinnt. Kein HTML-Paket nötig;
/// der Durchlauf ist linear, auch bei kaputtem HTML. Findet das Verfahren nichts Überzeugendes, zeigt Trendo wie bisher die ganze Seite.</summary>
public sealed partial class ResearchDesk
{
    private const int MinBlock = 25, MinArticle = 250, MaxDepth = 400, MaxTagLength = 8192, MaxCloseSearch = 64;

    private sealed class Node
    {
        public string Tag = ""; public Node? Parent; public readonly List<Node> Kids = [];
        public int Start, End, Own, OwnLink, Commas, Total, Link;
        public bool Bad; public double Score, Weight;
        public double LinkShare => Total == 0 ? 0 : (double)Link / Total;
        public double Final => (Score + Weight) * (1 - LinkShare);
    }

    private static readonly HashSet<string> Void = ["area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"];
    private static readonly HashSet<string> Inline = ["a", "abbr", "b", "bdi", "bdo", "cite", "code", "del", "dfn", "em", "font", "i", "ins", "kbd", "label", "mark", "q", "s", "samp", "small", "span", "strike", "strong", "sub", "sup", "time", "tt", "u", "var"];
    private static readonly HashSet<string> Scored = ["p", "pre", "td", "blockquote", "div", "section", "article", "main", "dd", "li"];
    private static readonly HashSet<string> OwnText = ["div", "section", "article", "main", "td"];
    private static readonly HashSet<string> BadTags = ["nav", "aside", "footer", "header", "menu", "dialog"];
    private static readonly HashSet<string> ClosesP = ["address", "article", "aside", "blockquote", "div", "dl", "fieldset", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "main", "nav", "ol", "p", "pre", "section", "table", "ul"];
    private static readonly HashSet<string> SelfNesting = ["p", "li", "td", "th", "tr", "dd", "dt"];
    private static readonly HashSet<string> LinkLists = ["ul", "ol", "dl", "table", "div", "section"];

    [GeneratedRegex(@"-ad-|\bads?\b|ad-break|agegate|banner|breadcrumb|combx|comment|community|consent|cookie|cover-wrap|disqus|extra|footer|gdpr|header|legends|menu|newsletter|navbar|navigation|\bnav|noprint|pager|pagination|paywall|popup|promo|related|remark|replies|rss|share|shoutbox|sidebar|skyscraper|social|sponsor|supplemental|teaser|\btoc\b", RegexOptions.IgnoreCase)] private static partial Regex Unlikely();
    [GeneratedRegex(@"article|body|column|content|main|shadow", RegexOptions.IgnoreCase)] private static partial Regex Maybe();
    [GeneratedRegex(@"article|body|content|entry|hentry|h-entry|main|page|post|text|blog|story", RegexOptions.IgnoreCase)] private static partial Regex Positive();
    [GeneratedRegex(@"-ad-|hidden|banner|combx|comment|contact|foot|gdpr|masthead|media|meta|outbrain|promo|related|scroll|share|shoutbox|sidebar|skyscraper|sponsor|shopping|tags|tool|widget", RegexOptions.IgnoreCase)] private static partial Regex Negative();
    [GeneratedRegex(@"\brole\s*=\s*[""']?(navigation|banner|complementary|contentinfo|dialog|search|menu|menubar|alert)\b|\bhidden\b(?!\s*=\s*[""']?false)|\baria-hidden\s*=\s*[""']?true|display\s*:\s*none|visibility\s*:\s*hidden", RegexOptions.IgnoreCase)] private static partial Regex HiddenOrRole();
    [GeneratedRegex(@"\b(class|id)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)] private static partial Regex ClassOrId();

    /// <summary>Liefert das HTML des Artikels oder null, wenn kein Artikel sicher erkennbar ist (dann gilt die ganze Seite). Erwartet HTML ohne Skripte und Kommentare.</summary>
    public static string? ExtractArticle(string h)
    {
        var root = ParseTree(h);
        var all = new List<Node>(); Collect(root, all);
        var top = all.Where(n => n.Score > 0 && !n.Bad).OrderByDescending(n => n.Final).Take(5).ToList();
        if (top.Count == 0 || top[0].Final <= 0) return null;
        var best = Climb(top[0], top.Skip(1).Where(n => n.Final >= top[0].Final * 0.75).ToList(), root);

        var parts = new List<Node> { best };
        if (best.Parent is { } parent)
        {
            var threshold = Math.Max(10, best.Final * 0.2);
            parts = parent.Kids.Where(k => k == best || (!k.Bad && !DropKid(k) && ((k.Score > 0 && k.Final >= threshold) || (k.Tag == "p" && k.Total > 80 && k.LinkShare < 0.25) || (k.Tag == "section" && best.Tag == "section" && k.Total > 200 && k.LinkShare < 0.4)))).ToList();
        }
        var sb = new StringBuilder();
        foreach (var part in parts) { var pos = part.Start; Emit(part, h, sb, ref pos); sb.Append(h, pos, part.End - pos).Append('\n'); }
        var article = sb.ToString();

        var length = PlainText(article).Length;
        if (length < MinArticle) return null;
        var whole = root.Total;
        return whole > 0 && (length < whole * 0.1 || (length < whole * 0.25 && length < 800)) ? null : article; // Übersichtsseiten: wenige Sätze unter vielen Anreißern sind kein Artikel
    }

    /// <summary>Besteht die Seite aus mehreren gleichwertigen Blöcken (etwa einer Liste von Warnungen), zählt ihr gemeinsamer Container – nicht der zufällig beste Block.</summary>
    private static Node Climb(Node best, List<Node> rivals, Node root)
    {
        static bool Inside(Node n, Node container) { for (var a = n.Parent; a is not null; a = a.Parent) if (a == container) return true; return false; }
        var others = rivals.Where(r => !Inside(r, best)).ToList();
        if (others.Count >= 1)
            for (var a = best.Parent; a is not null && a != root; a = a.Parent)
                if (!a.Bad && others.Count(r => Inside(r, a)) >= Math.Min(2, others.Count)) return a;
        var last = best.Score; var threshold = last / 3;
        for (var a = best.Parent; a is not null && a != root && a.Tag != "body"; a = a.Parent)
        {
            if (a.Bad) break;
            if (a.Score == 0) continue;
            if (a.Score < threshold) break;
            if (a.Score > last) return a;
            last = a.Score;
        }
        return best;
    }

    private static void Collect(Node n, List<Node> all) { all.Add(n); foreach (var k in n.Kids) Collect(k, all); }

    private static bool DropKid(Node k) => k.Bad || (LinkLists.Contains(k.Tag) && k.Total > 0 && k.LinkShare > 0.5);

    private static void Emit(Node n, string h, StringBuilder sb, ref int pos)
    {
        foreach (var k in n.Kids)
        {
            if (DropKid(k)) { sb.Append(h, pos, k.Start - pos); pos = k.End; }
            else Emit(k, h, sb, ref pos);
        }
    }

    private static Node ParseTree(string h)
    {
        var root = new Node { Tag = "#root", End = h.Length };
        var cur = root; int depth = 0, links = 0, i = 0;

        void Finish(Node n, int end)
        {
            n.End = end;
            if (n.Tag == "a") links = Math.Max(0, links - 1);
            var p = n.Parent!;
            if (Inline.Contains(n.Tag))
            {
                p.Own += n.Own; p.OwnLink += n.OwnLink; p.Commas += n.Commas; p.Total += n.Total; p.Link += n.Link;
                return;
            }
            if (!n.Bad && n.Own >= MinBlock && Scored.Contains(n.Tag))
            {
                var points = (n.Tag == "li" ? 1 : 1 + n.Commas + Math.Min(n.Own / 100.0, 3)) * (1 - (double)n.OwnLink / n.Own); // Listeneinträge zählen wenig: Quellenverzeichnisse sollen Fließtext nicht überstimmen
                if (OwnText.Contains(n.Tag)) n.Score += points;
                var a = n.Parent; var share = 1.0;
                for (var level = 0; level < 3 && a is not null; level++, a = a.Parent) { a.Score += points * share; share = level == 0 ? 0.5 : 1.0 / 3; }
            }
            n.Total += n.Own; n.Link += n.OwnLink;
            p.Total += n.Total; p.Link += n.Link;
        }
        void Pop(int end) { var n = cur; cur = n.Parent!; depth--; Finish(n, end); }

        while (i < h.Length)
        {
            var lt = h.IndexOf('<', i);
            var textEnd = lt < 0 ? h.Length : lt;
            if (textEnd > i) AddText(cur, h, i, textEnd, links > 0);
            if (lt < 0) break;
            if (lt + 1 >= h.Length) break;
            var c = h[lt + 1];
            if (!(char.IsAsciiLetter(c) || c == '/')) { i = lt + 1; continue; }
            var gt = TagEnd(h, lt);
            if (gt < 0) { i = -gt; continue; } // kein gültiges Ende: gelesenen Abschnitt überspringen, bleibt dadurch linear

            var closing = c == '/';
            var nameStart = lt + (closing ? 2 : 1); var nameEnd = nameStart;
            while (nameEnd < gt && (char.IsAsciiLetterOrDigit(h[nameEnd]) || h[nameEnd] is '-' or ':')) nameEnd++;
            var tag = h[nameStart..nameEnd].ToLowerInvariant();
            i = gt + 1;
            if (tag.Length == 0) continue;

            if (closing)
            {
                Node? hit = cur; var steps = 0;
                while (hit is not null && hit != root && hit.Tag != tag && steps++ < MaxCloseSearch) hit = hit.Parent;
                if (hit is null || hit == root || hit.Tag != tag) continue;
                while (cur != hit) Pop(lt);
                Pop(gt + 1);
                continue;
            }
            if (Void.Contains(tag) || h[gt - 1] == '/') continue;

            if (cur.Tag == "p" && ClosesP.Contains(tag)) Pop(lt);
            else if (cur.Tag == tag && SelfNesting.Contains(tag)) Pop(lt);
            if (depth >= MaxDepth) continue;

            var attrs = h[nameEnd..gt];
            var node = new Node { Tag = tag, Parent = cur, Start = lt, Bad = (cur.Bad && tag is not ("main" or "article")) || IsBad(tag, attrs), Weight = WeightOf(tag, attrs) };
            cur.Kids.Add(node); cur = node; depth++;
            if (tag == "a") links++;
        }
        while (cur != root) Pop(h.Length);
        return root;
    }

    /// <summary>Position der schließenden Klammer; negativ das Ende des erfolglos gelesenen Abschnitts. Anführungszeichen schützen „&gt;“ in Attributen.</summary>
    private static int TagEnd(string h, int lt)
    {
        var limit = Math.Min(h.Length, lt + MaxTagLength);
        var quote = '\0';
        for (var i = lt + 1; i < limit; i++)
        {
            var c = h[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; }
            else if (c is '"' or '\'') quote = c;
            else if (c == '>') return i;
        }
        return -Math.Max(limit, lt + 1);
    }

    private static void AddText(Node n, string h, int start, int end, bool inLink)
    {
        var length = 0; var commas = 0; var space = true;
        for (var i = start; i < end; i++)
        {
            var c = h[i];
            if (char.IsWhiteSpace(c)) { if (!space) { length++; space = true; } continue; }
            length++; space = false;
            if (c is ',' or '，' or '、' or '،') commas++;
        }
        if (length == 0) return;
        n.Own += length; n.Commas += commas;
        if (inLink) n.OwnLink += length;
    }

    private static string Names(string attrs)
    {
        var sb = new StringBuilder();
        foreach (Match m in ClassOrId().Matches(attrs))
            sb.Append(m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value).Append(' ');
        return sb.ToString();
    }

    private static bool IsBad(string tag, string attrs)
    {
        if (BadTags.Contains(tag)) return true;
        if (tag is "html" or "body" or "main" or "article") return false;
        if (HiddenOrRole().IsMatch(attrs)) return true;
        var names = Names(attrs);
        return names.Length > 0 && Unlikely().IsMatch(names) && !Maybe().IsMatch(names);
    }

    private static double WeightOf(string tag, string attrs)
    {
        double w = tag switch
        {
            "article" or "main" => 10,
            "div" => 5,
            "pre" or "td" or "blockquote" => 3,
            "ol" or "ul" or "dl" or "dd" or "dt" or "li" => -3,
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "th" => -5,
            _ => 0,
        };
        var names = Names(attrs);
        if (names.Length > 0) { if (Negative().IsMatch(names)) w -= 25; if (Positive().IsMatch(names)) w += 25; }
        return w;
    }
}
