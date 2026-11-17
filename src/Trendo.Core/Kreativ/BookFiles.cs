using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core.Office;

namespace Trendo.Core.Kreativ;

/// <summary>T-0725 (Audit D7, STUDIO-BOOK-01): Manuskript ↔ Textdokument. Damit schreibt „Bücher schreiben“ Word (.docx), OpenDocument (.odt)
/// und ein Druck-PDF über die vorhandenen Schreiber von Trendo Text (kein zweiter Satz) und liest fremde DOCX/ODT/TXT/MD als Manuskript ein.
/// Kapitel = Überschrift 1 („# “), Zwischenüberschrift = Überschrift 2 („## “), **fett**, *kursiv*, Aufzählung („- “). Jedes Kapitel beginnt auf einer neuen Seite.</summary>
public static partial class BookKit
{
    public sealed record Imported(string Title, string Manuscript, int Chapters, ImportReport? Report);

    [GeneratedRegex(@"\*\*\*(.+?)\*\*\*|\*\*(.+?)\*\*|(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)")]
    private static partial Regex InlineRx();

    /// <summary>Zeile mit **fett** / *kursiv* als Absatz mit echten Zeichenformaten.</summary>
    static Para Styled(string line, ParaStyle style = ParaStyle.Text)
    {
        var runs = new List<(string, Fmt, double?)>(); var at = 0;
        foreach (Match m in InlineRx().Matches(line))
        {
            if (m.Index > at) runs.Add((line[at..m.Index], Fmt.None, null));
            if (m.Groups[1].Success) runs.Add((m.Groups[1].Value, Fmt.Bold | Fmt.Italic, null));
            else if (m.Groups[2].Success) runs.Add((m.Groups[2].Value, Fmt.Bold, null));
            else runs.Add((m.Groups[3].Value, Fmt.Italic, null));
            at = m.Index + m.Length;
        }
        if (at < line.Length) runs.Add((line[at..], Fmt.None, null));
        var p = new Para { Style = style }; p.SetRuns(runs); return p;
    }

    /// <summary>Buch als Textdokument: Titelseite (Titel, Autorin/Autor), danach je Kapitel eine neue Seite mit Überschrift 1.</summary>
    public static TextDoc ToTextDoc(string title, string author, string manuscript)
    {
        var ch = Chapters(manuscript);
        if (ch.Count == 0) throw new InvalidOperationException("Das Buch hat noch keinen Text.");
        var doc = new TextDoc { Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim() };
        if (!string.IsNullOrWhiteSpace(title)) doc.Blocks.Add(new Para(title.Trim(), ParaStyle.Title));
        if (!string.IsNullOrWhiteSpace(author)) doc.Blocks.Add(new Para(author.Trim(), ParaStyle.Subtitle));
        foreach (var c in ch)
        {
            if (doc.Blocks.Count > 0) doc.Blocks.Add(new PageBreak());
            doc.Blocks.Add(Styled(c.Title, ParaStyle.Heading1));
            foreach (var block in Regex.Split(c.Body, @"\n\s*\n").Select(b => b.Trim('\n', '\r')).Where(b => b.Trim().Length > 0))
            {
                var lines = block.Split('\n');
                if (lines.All(l => Regex.IsMatch(l, @"^\s*[-*•] ")))
                {
                    foreach (var l in lines) { var p = Styled(Regex.Replace(l, @"^\s*[-*•] ", "").Trim()); p.List = ListKind.Bullet; doc.Blocks.Add(p); }
                    continue;
                }
                if (lines[0].StartsWith("## ", StringComparison.Ordinal)) { doc.Blocks.Add(Styled(lines[0][3..].Trim(), ParaStyle.Heading2)); lines = lines[1..]; if (lines.Length == 0) continue; }
                doc.Blocks.Add(Styled(string.Join(" ", lines.Select(l => l.Trim()).Where(l => l.Length > 0))));
            }
        }
        return doc;
    }

    /// <summary>Absatz zurück in Markdown-Auszeichnung (fett/kursiv), Leerzeichen am Rand bleiben außerhalb der Sternchen.</summary>
    static string Marked(Para p)
    {
        var sb = new StringBuilder();
        foreach (var (text, fmt, _) in p.Runs())
        {
            var mark = (fmt.HasFlag(Fmt.Bold), fmt.HasFlag(Fmt.Italic)) switch { (true, true) => "***", (true, false) => "**", (false, true) => "*", _ => "" };
            var core = text.Trim();
            if (mark.Length == 0 || core.Length == 0) { sb.Append(text); continue; }
            var lead = text[..(text.Length - text.TrimStart().Length)]; var tail = text[text.TrimEnd().Length..];
            sb.Append(lead).Append(mark).Append(core).Append(mark).Append(tail);
        }
        return sb.ToString();
    }

    /// <summary>Textdokument (z. B. aus Word) als Manuskript: Überschrift 1 → Kapitel, Überschrift 2/3 → „## “, Aufzählungen bleiben, Tabellen als Zeilen.
    /// Der erste Titel-Absatz wird zum Buchtitel; gibt es keine Überschrift 1, beginnt das Manuskript mit „# Kapitel 1“.</summary>
    public static (string Title, string Manuscript) FromTextDoc(TextDoc doc)
    {
        var sb = new StringBuilder(); string? title = null; var seenChapter = false;
        void Block(string s) { if (sb.Length > 0) sb.Append("\n\n"); sb.Append(s); }
        foreach (var b in doc.Blocks)
            switch (b)
            {
                case Para { Style: ParaStyle.Title } p when title is null && !seenChapter && p.Text.Trim().Length > 0: title = p.Text.Trim(); break;
                case Para { Style: ParaStyle.Subtitle } when !seenChapter: break; // Autorin/Untertitel der Titelseite
                case Para { Style: ParaStyle.Heading1 or ParaStyle.Title } p when p.Text.Trim().Length > 0: seenChapter = true; Block("# " + p.Text.Trim()); break;
                case Para { Style: ParaStyle.Heading2 or ParaStyle.Heading3 } p when p.Text.Trim().Length > 0: Block("## " + p.Text.Trim()); break;
                case Para p when p.Text.Trim().Length > 0:
                    if (p.List != ListKind.None)
                    {
                        var line = "- " + Marked(p).Trim();
                        if (sb.Length > 0 && Regex.IsMatch(sb.ToString(), @"(^|\n)- [^\n]*$")) sb.Append('\n').Append(line); else Block(line);
                    }
                    else Block(Marked(p).Trim());
                    break;
                case Table t: foreach (var r in t.Rows) Block(string.Join(" · ", r.Where(c => c.Trim().Length > 0))); break;
            }
        var md = sb.ToString();
        if (!md.StartsWith("# ", StringComparison.Ordinal) && !seenChapter) md = "# Kapitel 1\n\n" + md;
        else if (!md.StartsWith("# ", StringComparison.Ordinal)) md = "# Vorwort\n\n" + md;
        return (title ?? doc.Title ?? "", md.TrimEnd() + "\n");
    }

    public static readonly string[] ImportExtensions = ["docx", "odt", "txt", "md"];

    /// <summary>Fremde Datei als Manuskript einlesen. Markdown wird unverändert übernommen; DOCX/ODT/TXT über die Leser von Trendo Text
    /// (mit Importbericht: was die Datei enthält, das ein Manuskript nicht hält – z. B. Bilder, Kommentare). Das Original wird nie verändert.</summary>
    public static Imported Import(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".md")
        {
            var md = File.ReadAllText(path).Replace("\r", "");
            var t = Path.GetFileNameWithoutExtension(path);
            if (!md.TrimStart().StartsWith("# ", StringComparison.Ordinal)) md = "# Kapitel 1\n\n" + md;
            return new(t, md, Chapters(md).Count, null);
        }
        if (!ImportExtensions.Contains(ext.TrimStart('.'))) throw new ArgumentException($"Das Format {ext} lässt sich nicht als Manuskript öffnen – bitte .docx, .odt, .txt oder .md.");
        var doc = TextFiles.Open(path);
        var (title, ms) = FromTextDoc(doc);
        if (title.Length == 0) title = Path.GetFileNameWithoutExtension(path);
        return new(title, ms, Chapters(ms).Count, doc.Import);
    }
}
