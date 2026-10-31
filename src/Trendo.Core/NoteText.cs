using System.Text.RegularExpressions;
using Trendo.Core.Office;

namespace Trendo.Core;

/// <summary>Masterprompt 131 / Notizbuch: Seiten sind einfacher Text (Markdown) mit wenigen Regeln – erste Zeile = Titel, #Schlagwort, [[Verknüpfung]] zu einer anderen Notiz,
/// Checklisten „- [ ]“ / „- [x]“, Bilder „![](Pfad)“. So bleiben Notizen auch außerhalb von Trendo lesbar (Markdown).</summary>
public static partial class NoteText
{
    [GeneratedRegex(@"(?<![\p{L}\d&])#([\p{L}\d_-]{2,40})")] private static partial Regex TagRx();
    [GeneratedRegex(@"\[\[([^\[\]\n]{1,80})\]\]")] private static partial Regex LinkRx();
    [GeneratedRegex(@"^(\s*)- \[( |x|X)\] (.*)$")] private static partial Regex CheckRx();
    [GeneratedRegex(@"!\[[^\]]*\]\(([^)\n]+)\)")] private static partial Regex ImageRx();

    public static string Title(string text) { var t = text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim().TrimStart('#', ' ') ?? ""; return t.Length == 0 ? "Ohne Titel" : t.Length > 80 ? t[..80] + "…" : t; }
    public static string Snippet(string text) { var rest = string.Join(" ", text.Split('\n').Skip(1).Select(l => l.Trim()).Where(l => l.Length > 0)); return rest.Length > 90 ? rest[..90] + "…" : rest; }
    public static List<string> Tags(string text) => TagRx().Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public static List<string> Links(string text) => LinkRx().Matches(text).Select(m => m.Groups[1].Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public static List<string> Images(string text) => ImageRx().Matches(text).Select(m => m.Groups[1].Value.Trim()).ToList();
    public static (int Done, int Total) Checklist(string text) { var l = text.Split('\n').Select(x => CheckRx().Match(x)).Where(m => m.Success).ToList(); return (l.Count(m => m.Groups[2].Value != " "), l.Count); }

    /// <summary>Checklisten-Punkt in Zeile <paramref name="line"/> abhaken oder wieder öffnen.</summary>
    public static string Toggle(string text, int line)
    {
        var lines = text.Split('\n'); if (line < 0 || line >= lines.Length) return text;
        var m = CheckRx().Match(lines[line].TrimEnd('\r')); if (!m.Success) return text;
        lines[line] = $"{m.Groups[1].Value}- [{(m.Groups[2].Value == " " ? "x" : " ")}] {m.Groups[3].Value}";
        return string.Join('\n', lines);
    }

    /// <summary>Seiten, die auf diese Seite verweisen ([[Seitenname]]).</summary>
    public static List<(string Section, string Page)> Backlinks(IEnumerable<(string Section, string Page, string Text)> pages, string page) =>
        pages.Where(p => !string.Equals(p.Page, page, StringComparison.OrdinalIgnoreCase) && Links(p.Text).Any(l => string.Equals(l, page, StringComparison.OrdinalIgnoreCase))).Select(p => (p.Section, p.Page)).ToList();

    /// <summary>Als Textdokument (für den PDF-Export): Titel als Überschrift, Checklisten mit ☐/☑, Verknüpfungen als Text.</summary>
    public static TextDoc ToDoc(string title, string text)
    {
        var lines = (title + "\n" + text).Replace("\r", "").Split('\n'); var d = new TextDoc { Blocks = [] }; var first = true;
        foreach (var raw in lines)
        {
            var l = LinkRx().Replace(ImageRx().Replace(raw, m => $"[Bild: {Path.GetFileName(m.Groups[1].Value)}]"), m => m.Groups[1].Value);
            if (CheckRx().Match(l) is { Success: true } c) l = $"{c.Groups[1].Value}{(c.Groups[2].Value == " " ? "☐" : "☑")} {c.Groups[3].Value}";
            if (first && l.Trim().Length > 0) { d.Blocks.Add(new Para { Text = l.Trim().TrimStart('#', ' '), Style = ParaStyle.Title }); first = false; continue; }
            d.Blocks.Add(new Para { Text = l });
        }
        if (d.Blocks.Count == 0) d.Blocks.Add(new Para());
        return d;
    }

}
