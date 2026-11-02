using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Schöne Antworten (Wunsch 02.11.2026): Claudes Markdown in Bausteine zerlegen – Überschriften, Absätze, Listen, Tabellen, Zitate, Trennlinien
/// und Codeblöcke mit Sprache. Die Oberfläche zeichnet daraus Text mit Fett, Kursiv, Inline-Code und Links, Tabellen als Raster und Code mit Farben.</summary>
public static partial class ChatMarkdown
{
    public abstract record Block;
    public sealed record Heading(int Level, string Text) : Block;
    public sealed record Para(string Text) : Block;
    public sealed record ListBlock(bool Ordered, IReadOnlyList<string> Items, int Start = 1) : Block;
    public sealed record Table(IReadOnlyList<string> Head, IReadOnlyList<IReadOnlyList<string>> Rows) : Block;
    public sealed record Code(string Lang, string Text) : Block;
    public sealed record Quote(string Text) : Block;
    public sealed record Rule : Block;

    public enum Kind { Plain, Bold, Italic, Code, Link }
    /// <summary>Stück Fließtext; bei <see cref="Kind.Link"/> steht die Adresse in <paramref name="Url"/>.</summary>
    public sealed record Span(string Text, Kind Kind, string? Url = null);

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$")] private static partial Regex HeadingLine();
    [GeneratedRegex(@"^\s*[-*+]\s+(.*)$")] private static partial Regex BulletLine();
    [GeneratedRegex(@"^\s*(\d{1,4})[.)]\s+(.*)$")] private static partial Regex NumberLine();
    [GeneratedRegex(@"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$")] private static partial Regex TableRule();
    [GeneratedRegex(@"^\s*([-*_])(\s*\1){2,}\s*$")] private static partial Regex RuleLine();

    /// <summary>Markdown-Text in Bausteine zerlegen. Unvollständige Codeblöcke (während Claude noch schreibt) gelten bis zum Ende als Code.</summary>
    public static IReadOnlyList<Block> Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var blocks = new List<Block>(); var para = new List<string>();
        void Flush() { if (para.Count > 0) { blocks.Add(new Para(string.Join(" ", para.Select(p => p.Trim())))); para.Clear(); } }
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l.TrimStart().StartsWith("```"))
            {
                Flush();
                var lang = l.TrimStart()[3..].Trim().ToLowerInvariant(); var code = new List<string>();
                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```"); i++) code.Add(lines[i]);
                blocks.Add(new Code(lang, string.Join("\n", code).TrimEnd()));
                continue;
            }
            if (l.Trim().Length == 0) { Flush(); continue; }
            if (HeadingLine().Match(l) is { Success: true } h) { Flush(); blocks.Add(new Heading(h.Groups[1].Length, h.Groups[2].Value)); continue; }
            if (RuleLine().IsMatch(l)) { Flush(); blocks.Add(new Rule()); continue; }
            if (l.TrimStart().StartsWith('|') && i + 1 < lines.Length && TableRule().IsMatch(lines[i + 1]))
            {
                Flush();
                var head = Cells(l); var rows = new List<IReadOnlyList<string>>();
                for (i += 2; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++) rows.Add(Cells(lines[i]));
                i--; blocks.Add(new Table(head, rows)); continue;
            }
            if (l.TrimStart().StartsWith('>'))
            {
                Flush(); var q = new List<string>();
                for (; i < lines.Length && lines[i].TrimStart().StartsWith('>'); i++) q.Add(lines[i].TrimStart()[1..].Trim());
                i--; blocks.Add(new Quote(string.Join(" ", q))); continue;
            }
            if (BulletLine().IsMatch(l) || NumberLine().IsMatch(l))
            {
                Flush();
                var ordered = NumberLine().IsMatch(l);
                var start = ordered ? int.Parse(NumberLine().Match(l).Groups[1].Value) : 1;
                var items = new List<string>();
                for (; i < lines.Length; i++)
                {
                    var m = ordered ? NumberLine().Match(lines[i]) : BulletLine().Match(lines[i]);
                    if (m.Success) items.Add(m.Groups[ordered ? 2 : 1].Value.Trim());
                    else if (lines[i].StartsWith("  ") && lines[i].Trim().Length > 0 && items.Count > 0) items[^1] += " " + lines[i].Trim(); // Fortsetzungszeile
                    else break;
                }
                i--; blocks.Add(new ListBlock(ordered, items, start)); continue;
            }
            if (l.TrimEnd().EndsWith(':')) { Flush(); para.Add(l); Flush(); continue; } // „Stärken:“ o. Ä. steht als eigene Zeile
            para.Add(l);
        }
        Flush();
        return blocks;
    }

    static List<string> Cells(string row)
    {
        var t = row.Trim(); if (t.StartsWith('|')) t = t[1..]; if (t.EndsWith('|')) t = t[..^1];
        return t.Split('|').Select(c => c.Trim()).ToList();
    }

    [GeneratedRegex(@"`(?<c>[^`]+)`|\*\*(?<b>.+?)\*\*|__(?<b2>.+?)__|\[(?<lt>[^\]]+)\]\((?<lu>https?://[^\s)]+)\)|(?<![\w*])\*(?<i>[^*\s][^*]*?)\*(?![\w*])|(?<![\w_])_(?<i2>[^_\s][^_]*?)_(?![\w_])|(?<u>https?://[^\s)<>]+[^\s).,;:!?<>])")]
    private static partial Regex InlineRx();

    /// <summary>Fließtext in Stücke: Fett, Kursiv, `Code`, [Link](https://…) und nackte Adressen.</summary>
    public static IReadOnlyList<Span> Inline(string text)
    {
        var list = new List<Span>(); var at = 0;
        foreach (Match m in InlineRx().Matches(text))
        {
            if (m.Index > at) list.Add(new Span(text[at..m.Index], Kind.Plain));
            if (m.Groups["c"].Success) list.Add(new Span(m.Groups["c"].Value, Kind.Code));
            else if (m.Groups["b"].Success || m.Groups["b2"].Success) list.Add(new Span(m.Groups["b"].Success ? m.Groups["b"].Value : m.Groups["b2"].Value, Kind.Bold));
            else if (m.Groups["lt"].Success) list.Add(new Span(m.Groups["lt"].Value, Kind.Link, m.Groups["lu"].Value));
            else if (m.Groups["i"].Success || m.Groups["i2"].Success) list.Add(new Span(m.Groups["i"].Success ? m.Groups["i"].Value : m.Groups["i2"].Value, Kind.Italic));
            else list.Add(new Span(m.Groups["u"].Value, Kind.Link, m.Groups["u"].Value));
            at = m.Index + m.Length;
        }
        if (at < text.Length) list.Add(new Span(text[at..], Kind.Plain));
        return list;
    }

    /// <summary>Text ohne Markdown-Zeichen (zum Vorlesen und für Kurzfassungen).</summary>
    public static string Plain(string text) =>
        string.Join("\n", Parse(text).Select(b => b switch
        {
            Heading h => string.Concat(Inline(h.Text).Select(s => s.Text)),
            Para p => string.Concat(Inline(p.Text).Select(s => s.Text)),
            ListBlock l => string.Join("\n", l.Items.Select(x => string.Concat(Inline(x).Select(s => s.Text)))),
            Table t => string.Join("\n", t.Rows.Select(r => string.Join(", ", r))),
            Quote q => string.Concat(Inline(q.Text).Select(s => s.Text)),
            _ => "",
        }).Where(x => x.Length > 0));
}

/// <summary>Code-Farben wie im Editor: Schlüsselwörter, Texte, Kommentare, Zahlen – einfache Erkennung für die gängigen Sprachen, ohne fremde Bibliothek.</summary>
public static partial class CodeColors
{
    public enum Kind { Plain, Keyword, String, Comment, Number, Tag }
    public sealed record Token(string Text, Kind Kind);

    static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        // JavaScript/TypeScript, C#, Python, Java, Go, Rust, Shell, SQL (Großschreibung für SQL)
        "abstract","as","async","await","break","case","catch","class","const","continue","def","default","do","elif","else","enum","export","extends","false","finally","fn","for","from",
        "func","function","if","implements","import","in","interface","is","lambda","let","match","mut","namespace","new","None","null","nil","package","pass","private","protected","public",
        "raise","readonly","return","static","struct","super","switch","this","throw","True","False","true","try","type","typeof","using","var","void","while","with","yield","pub","impl","self",
        "int","string","bool","double","float","long","char","byte","decimal","object","record","sealed","override","virtual","get","set","then","fi","done","echo","local",
        "SELECT","FROM","WHERE","INSERT","INTO","VALUES","UPDATE","DELETE","CREATE","TABLE","JOIN","ON","AND","OR","NOT","ORDER","BY","GROUP","LIMIT","AS",
    };

    [GeneratedRegex(@"(?<com>//[^\n]*|/\*[\s\S]*?\*/|<!--[\s\S]*?-->|(?<![\w$])#(?![!{])[^\n]*)|(?<str>""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])*'|`(?:\\.|[^`\\])*`)|(?<tag></?[A-Za-z][\w-]*|/?>)|(?<num>\b\d+(?:\.\d+)?\b)|(?<word>[A-Za-z_$][\w$]*)")]
    private static partial Regex TokenRx();

    /// <summary>Code in farbige Stücke zerlegen. <paramref name="lang"/> steuert nur, ob # ein Kommentar ist (Python, Shell, YAML …) und ob HTML-Tags gefärbt werden.</summary>
    public static IReadOnlyList<Token> Tokens(string lang, string code)
    {
        var hashComments = lang is "py" or "python" or "sh" or "bash" or "shell" or "ps1" or "powershell" or "yaml" or "yml" or "toml" or "r" or "rb" or "ruby" or "gd" or "gdscript";
        var markup = lang is "html" or "xml" or "svg" or "vue" or "htm";
        var list = new List<Token>(); var at = 0;
        foreach (Match m in TokenRx().Matches(code))
        {
            Kind k;
            if (m.Groups["com"].Success) { if (m.Value.StartsWith('#') && !hashComments) continue; k = Kind.Comment; }
            else if (m.Groups["str"].Success) k = Kind.String;
            else if (m.Groups["tag"].Success) { if (!markup) continue; k = Kind.Tag; }
            else if (m.Groups["num"].Success) k = Kind.Number;
            else if (Keywords.Contains(m.Value)) k = Kind.Keyword;
            else continue;
            if (m.Index > at) list.Add(new Token(code[at..m.Index], Kind.Plain));
            list.Add(new Token(m.Value, k)); at = m.Index + m.Length;
        }
        if (at < code.Length) list.Add(new Token(code[at..], Kind.Plain));
        return list;
    }
}
