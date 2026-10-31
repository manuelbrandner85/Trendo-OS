using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Office;

/// <summary>T-0230 Ausbau Trendo Text: Inhaltsverzeichnis (W5), Serienbrief, Etiketten, Umschläge (W6), Vergleichen mit Annehmen/Ablehnen und
/// Barrierefreiheit prüfen (W7).</summary>
public static partial class TextTools
{
    // ---------- W5 Inhaltsverzeichnis ----------
    public sealed record TocEntry(int Level, string Text, int Block);

    public static List<TocEntry> Toc(TextDoc doc) =>
        doc.Blocks.Select((b, i) => (b, i)).Where(x => x.b is Para { Style: ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 } p && p.Text.Trim().Length > 0)
            .Select(x => { var p = (Para)x.b; return new TocEntry(p.Style == ParaStyle.Heading1 ? 1 : p.Style == ParaStyle.Heading2 ? 2 : 3, p.Text.Trim(), x.i); }).ToList();

    /// <summary>Nummer jeder Fußnote/Endnote in Dokumentreihenfolge (je Art eigene Zählung).</summary>
    public static Dictionary<(int Block, int Index), int> NoteNumbers(TextDoc doc, MarkKind kind)
    {
        var res = new Dictionary<(int, int), int>(); var n = 0;
        for (var b = 0; b < doc.Blocks.Count; b++)
            if (doc.Blocks[b] is Para p)
                for (var i = 0; i < p.Marks.Count; i++) if (p.Marks[i].Kind == kind) res[(b, i)] = ++n;
        return res;
    }

    // ---------- W6 Serienbrief ----------
    static readonly Regex FieldRx = new(@"«([^»]+)»|\{\{?([^{}]+)\}?\}", RegexOptions.Compiled);

    /// <summary>Platzhalter im Dokument: «Name» oder {Name}.</summary>
    public static List<string> Fields(TextDoc doc) =>
        doc.Paras.SelectMany(p => FieldRx.Matches(p.Text).Select(m => (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim()))
            .Concat(doc.Blocks.OfType<Table>().SelectMany(t => t.Rows.SelectMany(r => r)).SelectMany(c => FieldRx.Matches(c).Select(m => (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim())))
            .Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Ein Dokument je Datenzeile, durch Seitenumbrüche getrennt. Felder werden nach Spaltenüberschrift ersetzt (Groß/klein egal).</summary>
    public static TextDoc MailMerge(TextDoc template, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var res = new TextDoc { Title = template.Title, Page = template.Page.Copy() };
        var first = true;
        foreach (var row in rows)
        {
            if (row.All(c => c.Trim().Length == 0)) continue;
            if (!first) res.Blocks.Add(new PageBreak());
            first = false;
            string Value(string field) { var i = headers.ToList().FindIndex(h => h.Trim().Equals(field.Trim(), StringComparison.CurrentCultureIgnoreCase)); return i >= 0 && i < row.Count ? row[i] : ""; }
            string Fill(string s) => FieldRx.Replace(s, m => Value(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value));
            foreach (var b in template.Blocks)
            {
                var c = b.Copy();
                if (c is Para p)
                    foreach (Match m in FieldRx.Matches(p.Text).Cast<Match>().Reverse()) TextDoc.ReplaceAt(p, m.Index, m.Length, Value(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value));
                else if (c is Table t) t.Rows = t.Rows.Select(r => r.Select(Fill).ToList()).ToList();
                res.Blocks.Add(c);
            }
        }
        if (res.Blocks.Count == 0) res.Blocks.Add(new Para());
        return res;
    }

    // ---------- W7 Vergleichen, Änderungen annehmen/ablehnen ----------
    /// <summary>Vergleicht zwei Fassungen absatzweise (und in geänderten Absätzen wortweise). Ergebnis: neue Fassung mit Markierungen
    /// „eingefügt“ und gelöschtem Text, markiert als „gelöscht“ – wie „Änderungen nachverfolgen“.</summary>
    public static TextDoc Compare(TextDoc older, TextDoc newer)
    {
        var a = older.Paras.ToList(); var bp = newer.Blocks.OfType<Para>().ToList();
        var anchors = Lcs(a.Select(p => p.Text).ToList(), bp.Select(p => p.Text).ToList());
        var before = Enumerable.Range(0, bp.Count + 1).Select(_ => new List<Para>()).ToList(); // gelöschte Absätze vor neuem Absatz j
        var shown = new Para[bp.Count];
        static Para Deleted(Para p) { var d = (Para)p.Copy(); d.Marks = [new Mark(0, d.Text.Length, MarkKind.Delete)]; return d; }
        int i = 0, j = 0;
        foreach (var (ai, bj) in anchors.Append((a.Count, bp.Count)))
        {
            // zwischen zwei gleichen Absätzen: paarweise als geändert, Überhang gelöscht bzw. eingefügt
            while (i < ai && j < bj && IsChanged(a[i].Text, bp[j].Text)) { shown[j] = WordDiff(a[i], bp[j]); i++; j++; }
            while (i < ai) before[j].Add(Deleted(a[i++]));
            while (j < bj) { var ins = (Para)bp[j].Copy(); if (ins.Text.Length > 0) ins.Marks.Add(new Mark(0, ins.Text.Length, MarkKind.Insert)); shown[j++] = ins; }
            if (ai < a.Count && bj < bp.Count) { shown[bj] = (Para)bp[bj].Copy(); i = ai + 1; j = bj + 1; }
        }
        var res = new TextDoc { Title = newer.Title, Page = newer.Page.Copy() }; var k = 0;
        foreach (var blk in newer.Blocks)
        {
            if (blk is not Para) { res.Blocks.Add(blk.Copy()); continue; }
            res.Blocks.AddRange(before[k]); res.Blocks.Add(shown[k]); k++;
        }
        res.Blocks.AddRange(before[bp.Count]);
        if (res.Blocks.Count == 0) res.Blocks.Add(new Para());
        return res;
    }

    static bool IsChanged(string x, string y)
    {
        if (x.Length == 0 || y.Length == 0) return false;
        var wx = x.Split(' '); var wy = y.Split(' ');
        return Lcs(wx.ToList(), wy.ToList()).Count * 2 >= Math.Max(wx.Length, wy.Length); // mehr als die Hälfte gleich → geänderter Absatz
    }

    static Para WordDiff(Para oldP, Para newP)
    {
        var wa = Tokens(oldP.Text); var wb = Tokens(newP.Text);
        var common = Lcs(wa, wb);
        var res = (Para)newP.Copy(); res.Marks = [.. newP.Marks.Where(m => m.Kind is not (MarkKind.Insert or MarkKind.Delete))];
        // Text neu aufbauen: gemeinsame und neue Wörter aus der neuen Fassung, gelöschte Wörter eingeschoben
        var text = new System.Text.StringBuilder(); var marks = new List<Mark>();
        int ia = 0, ib = 0;
        void Del(int to) { while (ia < to) { marks.Add(new Mark(text.Length, wa[ia].Length, MarkKind.Delete)); text.Append(wa[ia]); ia++; } }
        void Ins(int to) { while (ib < to) { marks.Add(new Mark(text.Length, wb[ib].Length, MarkKind.Insert)); text.Append(wb[ib]); ib++; } }
        foreach (var (ca, cb) in common) { Del(ca); Ins(cb); text.Append(wb[cb]); ia = ca + 1; ib = cb + 1; }
        Del(wa.Count); Ins(wb.Count);
        res.Spans = []; res.Text = text.ToString();
        res.Marks.AddRange(Merge(marks));
        return res;
    }

    static IEnumerable<Mark> Merge(List<Mark> marks)
    {
        Mark? cur = null;
        foreach (var m in marks)
        {
            if (cur is { } c && c.Kind == m.Kind && c.End == m.Start) cur = c with { Length = c.Length + m.Length };
            else { if (cur is not null) yield return cur; cur = m; }
        }
        if (cur is not null) yield return cur;
    }

    static List<string> Tokens(string s) => Regex.Matches(s, @"\s+|[^\s]+").Select(m => m.Value).ToList();

    /// <summary>108-49: Wortweiser Unterschied für einen Vorschlag – (Text, Art) mit Art 0 gleich, −1 entfernt, +1 neu.</summary>
    public static List<(string Text, int Kind)> DiffWords(string oldText, string newText)
    {
        var wa = Tokens(oldText); var wb = Tokens(newText); var res = new List<(string, int)>();
        int ia = 0, ib = 0;
        void Add(string t, int k) { if (res.Count > 0 && res[^1].Item2 == k) res[^1] = (res[^1].Item1 + t, k); else res.Add((t, k)); }
        foreach (var (ca, cb) in Lcs(wa, wb))
        {
            while (ia < ca) Add(wa[ia++], -1);
            while (ib < cb) Add(wb[ib++], 1);
            Add(wb[cb], 0); ia = ca + 1; ib = cb + 1;
        }
        while (ia < wa.Count) Add(wa[ia++], -1);
        while (ib < wb.Count) Add(wb[ib++], 1);
        return res;
    }

    /// <summary>Längste gemeinsame Teilfolge als Indexpaare (A in der alten, B in der neuen Liste).</summary>
    static List<(int A, int B)> Lcs(List<string> a, List<string> b)
    {
        var n = a.Count; var m = b.Count;
        if ((long)n * m > 4_000_000) return Enumerable.Range(0, Math.Min(n, m)).Where(i => a[i] == b[i]).Select(i => (i, i)).ToList();
        var d = new int[n + 1, m + 1];
        for (var x = n - 1; x >= 0; x--) for (var y = m - 1; y >= 0; y--) d[x, y] = a[x] == b[y] ? d[x + 1, y + 1] + 1 : Math.Max(d[x + 1, y], d[x, y + 1]);
        var res = new List<(int, int)>(); int i = 0, j = 0;
        while (i < n && j < m) { if (a[i] == b[j]) { res.Add((i, j)); i++; j++; } else if (d[i + 1, j] >= d[i, j + 1]) i++; else j++; }
        return res;
    }

    public static bool HasChanges(TextDoc doc) => doc.Paras.Any(p => p.Marks.Any(m => m.Kind is MarkKind.Insert or MarkKind.Delete));

    /// <summary>Alle Änderungen annehmen (gelöschter Text verschwindet) oder ablehnen (eingefügter Text verschwindet).</summary>
    public static int Resolve(TextDoc doc, bool accept)
    {
        var n = 0;
        for (var b = doc.Blocks.Count - 1; b >= 0; b--)
        {
            if (doc.Blocks[b] is not Para p) continue;
            var remove = accept ? MarkKind.Delete : MarkKind.Insert; var keep = accept ? MarkKind.Insert : MarkKind.Delete;
            var dels = p.Marks.Where(m => m.Kind == remove).OrderByDescending(m => m.Start).ToList();
            n += dels.Count + p.Marks.Count(m => m.Kind == keep);
            var whole = dels.Count == 1 && dels[0].Start == 0 && dels[0].Length == p.Text.Length && p.Text.Length > 0;
            p.Marks.RemoveAll(m => m.Kind == keep);
            foreach (var m in dels) { p.Marks.Remove(m); TextDoc.ReplaceAt(p, m.Start, m.Length, ""); }
            if (whole) doc.Blocks.RemoveAt(b);
        }
        if (doc.Blocks.Count == 0) doc.Blocks.Add(new Para());
        return n;
    }

    // ---------- W7 Barrierefreiheit prüfen ----------
    public sealed record Issue(int Block, string Text, string Fix);

    public static List<Issue> Accessibility(TextDoc doc)
    {
        var res = new List<Issue>(); var last = 0; var headings = 0;
        for (var i = 0; i < doc.Blocks.Count; i++)
            switch (doc.Blocks[i])
            {
                case Picture pic when pic.Alt.Trim().Length == 0:
                    res.Add(new(i, "Bild ohne Alternativtext", "Beschreibe kurz, was das Bild zeigt (Bild anklicken → „Alternativtext“).")); break;
                case Table t when t.Rows.Count > 0 && t.Rows[0].All(c => c.Trim().Length == 0):
                    res.Add(new(i, "Tabelle ohne Kopfzeile", "Trage in die erste Zeile Spaltenüberschriften ein.")); break;
                case Para p:
                    var lvl = p.Style switch { ParaStyle.Heading1 => 1, ParaStyle.Heading2 => 2, ParaStyle.Heading3 => 3, _ => 0 };
                    if (lvl > 0)
                    {
                        headings++;
                        if (p.Text.Trim().Length == 0) res.Add(new(i, "Leere Überschrift", "Text eintragen oder die Absatzart auf „Text“ stellen."));
                        if (lvl > last + 1) res.Add(new(i, $"Überschrift {lvl} folgt auf {(last == 0 ? "keine Überschrift" : "Überschrift " + last)}", $"Ebene überspringen erschwert Bildschirmlesern die Gliederung – nimm Überschrift {last + 1}."));
                        last = lvl;
                    }
                    foreach (var m in p.Marks.Where(m => m.Kind == MarkKind.Link))
                        if (p.Text.Substring(m.Start, Math.Min(m.Length, p.Text.Length - m.Start)).Trim().ToLowerInvariant() is "hier" or "klicken" or "hier klicken" or "link" or "mehr")
                            res.Add(new(i, $"Linktext „{p.Text.Substring(m.Start, m.Length)}“ sagt nicht, wohin er führt", "Schreibe das Ziel in den Linktext, z. B. „Fahrplan der Stadtwerke“."));
                    if (p.Text.Length > 40 && p.Text.Count(char.IsLetter) > 30 && p.Text.Where(char.IsLetter).All(char.IsUpper))
                        res.Add(new(i, "Langer Text nur in GROSSBUCHSTABEN", "Normale Schreibweise ist leichter zu lesen; zum Hervorheben „Fett“ nutzen."));
                    break;
            }
        if (headings == 0 && doc.Paras.Count(p => p.Text.Trim().Length > 0) > 12) res.Add(new(0, "Keine Überschriften", "Gliedere längere Texte mit Überschriften – das hilft beim Lesen und beim Inhaltsverzeichnis."));
        return res;
    }

    // ---------- W6 Etiketten und Umschläge (PDF) ----------
    const double Mm = 72 / 25.4;

    /// <summary>Adressetiketten A4, 3 × 8 Felder à 70 × 37 mm (gängiges Format). Jede Adresse: Zeilen mit „\n“.</summary>
    public static int Labels(IEnumerable<string> addresses, string path, int cols = 3, int rows = 8, double w = 70, double h = 37)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle);
        using var pdf = new PdfDocument(); var list = addresses.Where(a => a.Trim().Length > 0).ToList();
        var font = new XFont("Arial", 10);
        double left = (210 - cols * w) / 2 * Mm, top = (297 - rows * h) / 2 * Mm;
        for (var i = 0; i < Math.Max(1, list.Count); i++)
        {
            if (i % (cols * rows) == 0) { var pg = pdf.AddPage(); pg.Width = XUnit.FromMillimeter(210); pg.Height = XUnit.FromMillimeter(297); }
            if (i >= list.Count) break;
            using var g = XGraphics.FromPdfPage(pdf.Pages[pdf.PageCount - 1], XGraphicsPdfPageOptions.Append);
            var slot = i % (cols * rows); var x = left + slot % cols * w * Mm; var y = top + slot / cols * h * Mm;
            var lines = list[i].Replace("\r", "").Split('\n');
            for (var l = 0; l < lines.Length && l < 6; l++) g.DrawString(lines[l], font, XBrushes.Black, x + 5 * Mm, y + 8 * Mm + l * 13);
        }
        var pages = pdf.PageCount;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); pdf.Save(path);
        return pages;
    }

    /// <summary>Briefumschlag DIN lang (220 × 110 mm): Absender klein oben links, Empfänger im Fenster-Bereich.</summary>
    public static int Envelopes(string sender, IEnumerable<string> recipients, string path)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle);
        using var pdf = new PdfDocument();
        foreach (var r in recipients.Where(a => a.Trim().Length > 0).DefaultIfEmpty(""))
        {
            var pg = pdf.AddPage(); pg.Width = XUnit.FromMillimeter(220); pg.Height = XUnit.FromMillimeter(110);
            using var g = XGraphics.FromPdfPage(pg);
            var small = new XFont("Arial", 8); var big = new XFont("Arial", 12);
            var y = 12 * Mm; foreach (var l in sender.Replace("\r", "").Split('\n').Where(l => l.Length > 0)) { g.DrawString(l, small, XBrushes.Black, 15 * Mm, y); y += 10; }
            y = 55 * Mm; foreach (var l in r.Replace("\r", "").Split('\n')) { g.DrawString(l, big, XBrushes.Black, 110 * Mm, y); y += 16; }
        }
        var pages = pdf.PageCount;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); pdf.Save(path);
        return pages;
    }
}
