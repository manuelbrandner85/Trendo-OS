using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Annotations;

namespace Trendo.Core.Office;

/// <summary>T9/T-0230: Trendo-Text-Dokument als PDF. Eigener Seitensatz mit PDFsharp: Seite einrichten (A4 hoch/quer, Ränder, Spalten,
/// Seitenrand-Linie), Kopf-/Fußzeile mit „Seite n von m“, Wasserzeichen, Design (Schrift, Überschriftenfarbe), Zeilenumbruch nach Wörtern,
/// gemischte Zeichenformate, Absatzarten, Listen, Einzug und Abstand, Ausrichtung (auch Blocksatz), Tabellen, Bilder mit Beschriftung,
/// Seitenumbrüche, Inhaltsverzeichnis mit Seitenzahlen, Links (anklickbar), Kommentare (als PDF-Notiz), Fußnoten unten auf der Seite,
/// Endnoten am Schluss und Änderungen (eingefügt grün, gelöscht rot durchgestrichen). Ohne Programmname.</summary>
public static class TextPdf
{
    const double Cm = 72 / 2.54;
    static readonly XColor InsColor = XColor.FromArgb(0x15, 0x80, 0x3D), DelColor = XColor.FromArgb(0xB9, 0x1C, 0x1C), LinkColor = XColor.FromArgb(0x05, 0x63, 0xC1);

    sealed record Word(string Text, XFont Font, bool Underline, bool Strike, double Rise, double W, bool SpaceAfter, XColor? Color, string? Link, string? Comment, bool Highlight);

    public static int Export(TextDoc doc, string path)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle); // Schriften einrichten
        // Zwei Durchgänge: der erste ermittelt die Seiten der Überschriften für das Inhaltsverzeichnis
        var first = new Layout(doc, null);
        using (var probe = first.Render()) { }
        var final = new Layout(doc, first.HeadingPages);
        using var pdf = final.Render();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var pages = pdf.PageCount;
        pdf.Save(path);
        return pages;
    }

    sealed class Layout(TextDoc doc, Dictionary<int, int>? tocPages)
    {
        readonly PageSetup page = doc.Page;
        readonly double pageW = (doc.Page.Landscape ? 29.7 : 21) * Cm, pageH = (doc.Page.Landscape ? 21 : 29.7) * Cm;
        double Top => page.Top * Cm; double Bottom => page.Bottom * Cm; double Left => page.Left * Cm;
        double Body => pageW - (page.Left + page.Right) * Cm;
        int Cols => Math.Clamp(page.Columns, 1, 3);
        const double Gap = 0.8 * Cm;
        double ColW => (Body - Gap * (Cols - 1)) / Cols;
        public Dictionary<int, int> HeadingPages { get; } = [];
        PdfDocument pdf = null!; PdfPage cur = null!; XGraphics g = null!; double y; int col;
        double footReserve; readonly List<(int No, string Text)> pageNotes = [];
        readonly List<(int No, string Text)> endnotes = [];
        int footNo, endNo, figure;
        XColor Heading => ParseColor(page.HeadingColor);

        double X0 => Left + col * (ColW + Gap);
        double Limit => pageH - Bottom - footReserve;

        public PdfDocument Render()
        {
            pdf = new PdfDocument();
            pdf.Info.Title = doc.Title ?? doc.Paras.FirstOrDefault(p => p.Text.Trim().Length > 0)?.Text.Trim() ?? "";
            NewPage();
            var number = 0; PdfOutline? h1 = null;
            for (var bi = 0; bi < doc.Blocks.Count; bi++)
            {
                switch (doc.Blocks[bi])
                {
                    case PageBreak: NewPage(); break;
                    case TocBlock toc: DrawToc(toc); break;
                    case Para p:
                        number = p.List == ListKind.Number ? number + 1 : 0;
                        if (p.Style is ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 && p.Text.Trim().Length > 0)
                        {
                            Need(TextDoc.StyleSize(p.Style) * 1.6 + 10); // Überschrift nicht allein am Seitenende
                            HeadingPages[bi] = pdf.PageCount;
                            // Lesezeichen wie LibreOffice: Überschrift 1 oben, 2 und 3 darunter
                            var title = p.Text.Trim().Replace('\n', ' ');
                            if (p.Style == ParaStyle.Heading1 || h1 is null) h1 = pdf.Outlines.Add(title, cur, true);
                            else h1.Outlines.Add(title, cur, false);
                        }
                        DrawPara(p, number, bi);
                        break;
                    case Table t: DrawTable(t); break;
                    case Picture pic: DrawPicture(pic); break;
                }
            }
            FlushFootnotes();
            if (endnotes.Count > 0)
            {
                Need(40); y += 12;
                Text("Anmerkungen", F(13, XFontStyleEx.Bold), new XSolidBrush(Heading)); y += 6;
                foreach (var (no, t) in endnotes) foreach (var line in Wrap($"{ToRoman(no)}  {t}", F(9.5), ColW)) { Need(13); g.DrawString(line, F(9.5), XBrushes.Black, X0, y + 10); y += 13; }
            }
            g.Dispose();
            // Kopf-/Fußzeile, Seitenzahlen
            var pages = pdf.PageCount;
            for (var i = 0; i < pages; i++)
                using (var pg = XGraphics.FromPdfPage(pdf.Pages[i], XGraphicsPdfPageOptions.Append))
                {
                    var small = F(8.5); var gray = new XSolidBrush(XColor.FromArgb(0x59, 0x59, 0x59));
                    if (page.Header.Length > 0) pg.DrawString(page.Header, small, gray, new XRect(Left, Top * 0.45, Body, 12), XStringFormats.TopRight);
                    var fy = pageH - Bottom * 0.55;
                    if (page.Footer.Length > 0) pg.DrawString(page.Footer, small, gray, new XRect(Left, fy, Body, 12), XStringFormats.TopLeft);
                    if (page.PageNumbers) pg.DrawString($"Seite {i + 1} von {pages}", small, gray, new XRect(Left, fy, Body, 12), page.Footer.Length > 0 ? XStringFormats.TopRight : XStringFormats.TopCenter);
                }
            return pdf;
        }

        // ---------- Seiten und Spalten ----------
        void NewPage()
        {
            FlushFootnotes();
            g?.Dispose();
            cur = pdf.AddPage(); cur.Width = XUnit.FromPoint(pageW); cur.Height = XUnit.FromPoint(pageH);
            g = XGraphics.FromPdfPage(cur); y = Top; col = 0; footReserve = 0;
            if (page.Watermark.Length > 0)
            {
                var st = g.Save();
                g.TranslateTransform(pageW / 2, pageH / 2); g.RotateTransform(-45);
                g.DrawString(page.Watermark, F(Math.Min(110, 900.0 / Math.Max(4, page.Watermark.Length)), XFontStyleEx.Bold), new XSolidBrush(XColor.FromArgb(48, 128, 128, 128)), new XRect(-pageW, -60, pageW * 2, 120), XStringFormats.Center);
                g.Restore(st);
            }
            if (page.PageBorder) g.DrawRectangle(new XPen(XColor.FromArgb(0x80, 0x80, 0x80), 0.8), 0.9 * Cm, 0.9 * Cm, pageW - 1.8 * Cm, pageH - 1.8 * Cm);
        }

        /// <summary>Platz für h Punkte? Sonst nächste Spalte oder Seite.</summary>
        void Need(double h)
        {
            if (y + h <= Limit || y <= Top + 0.1) return;
            if (col + 1 < Cols) { col++; y = Top; return; }
            NewPage();
        }

        void FlushFootnotes()
        {
            if (pageNotes.Count == 0 || g is null) return;
            var fy = pageH - Bottom - footReserve + 4;
            g.DrawLine(new XPen(XColors.Gray, 0.5), Left, fy, Left + 4 * Cm, fy); fy += 3;
            foreach (var (no, t) in pageNotes) foreach (var line in Wrap($"{no}  {t}", F(8.5), Body)) { g.DrawString(line, F(8.5), XBrushes.Black, Left, fy + 9); fy += 11; }
            pageNotes.Clear();
        }

        XFont F(double size, XFontStyleEx style = XFontStyleEx.Regular)
        {
            try { return new XFont(page.Font, size, style); } catch (InvalidOperationException) { return new XFont("Arial", size, style); }
        }

        void Text(string s, XFont f, XBrush b) { var h = f.Size * 1.3; Need(h); g.DrawString(s, f, b, X0, y + f.Size); y += h; }

        XFont Font(Para p, Fmt f, double? size)
        {
            var pt = size ?? TextDoc.StyleSize(p.Style);
            if (f.HasFlag(Fmt.Super) || f.HasFlag(Fmt.Sub)) pt *= 0.62;
            var style = XFontStyleEx.Regular;
            if (f.HasFlag(Fmt.Bold) || p.Style is ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3) style |= XFontStyleEx.Bold;
            if (f.HasFlag(Fmt.Italic) || p.Style == ParaStyle.Quote) style |= XFontStyleEx.Italic;
            return F(pt, style);
        }

        XColor ColorOf(Para p) => p.Style switch
        {
            ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 => Heading,
            ParaStyle.Subtitle => XColor.FromArgb(0x59, 0x59, 0x59), ParaStyle.Quote => XColor.FromArgb(0x40, 0x40, 0x40), _ => XColors.Black,
        };

        // ---------- Absätze ----------
        void DrawPara(Para p, int number, int block)
        {
            var indent = p.IndentCm * Cm + (p.List != ListKind.None ? 0.9 * Cm : p.Style == ParaStyle.Quote ? 1 * Cm : 0);
            var avail = ColW - indent - (p.Style == ParaStyle.Quote ? 1 * Cm : 0);
            y += p.Style is ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 ? 10.0 : 0;
            var lines = new List<List<Word>>(); var line = new List<Word>(); var lineW = 0.0;
            var baseColor = ColorOf(p);
            // Abschnitte nach Format und Markierungen; Fußnotenzeichen als hochgestellte Zahl
            var cuts = new SortedSet<int> { 0, p.Text.Length };
            foreach (var s in p.Spans) { cuts.Add(Math.Clamp(s.Start, 0, p.Text.Length)); cuts.Add(Math.Clamp(s.End, 0, p.Text.Length)); }
            foreach (var m in p.Marks) { cuts.Add(Math.Clamp(m.Start, 0, p.Text.Length)); cuts.Add(Math.Clamp(m.End, 0, p.Text.Length)); }
            var pts = cuts.ToList();
            var notesHere = new List<(int No, Word Ref, string Text)>();
            void AddText(string text, Fmt f, double? size, XColor? color, string? link, string? comment, bool hl)
            {
                var font = Font(p, f, size);
                var rise = f.HasFlag(Fmt.Super) ? -0.35 * (size ?? TextDoc.StyleSize(p.Style)) : f.HasFlag(Fmt.Sub) ? 0.15 * (size ?? TextDoc.StyleSize(p.Style)) : 0;
                var pieces = text.Replace("\t", "    ").Split('\n');
                for (var k = 0; k < pieces.Length; k++)
                {
                    if (k > 0) { lines.Add(line); line = []; lineW = 0; }
                    foreach (var token in Tokens(pieces[k]))
                    {
                        var space = token.EndsWith(' '); var word = token.TrimEnd(' ');
                        var w = word.Length == 0 ? 0 : g.MeasureString(word, font).Width; var sw = space ? g.MeasureString(" ", font).Width : 0;
                        if (lineW + w > avail && line.Count > 0) { lines.Add(line); line = []; lineW = 0; }
                        while (w > avail && word.Length > 1) // überlange Wörter hart teilen
                        {
                            var n = word.Length; while (n > 1 && g.MeasureString(word[..n], font).Width > avail) n--;
                            line.Add(new Word(word[..n], font, f.HasFlag(Fmt.Underline) || link is not null, f.HasFlag(Fmt.Strike), rise, g.MeasureString(word[..n], font).Width, false, color, link, comment, hl));
                            lines.Add(line); line = []; lineW = 0; word = word[n..]; w = g.MeasureString(word, font).Width;
                        }
                        line.Add(new Word(word, font, f.HasFlag(Fmt.Underline) || link is not null, f.HasFlag(Fmt.Strike), rise, w, space, color, link, comment, hl));
                        lineW += w + sw;
                    }
                }
            }
            void Notes(int at)
            {
                foreach (var m in p.Marks.Where(m => m.Length == 0 && m.Start == at && m.Kind is MarkKind.Footnote or MarkKind.Endnote))
                {
                    var foot = m.Kind == MarkKind.Footnote; var no = foot ? ++footNo : ++endNo;
                    AddText(foot ? no.ToString() : ToRoman(no), Fmt.Super, null, null, null, null, false);
                    if (foot) notesHere.Add((no, line[^1], m.Value)); else endnotes.Add((no, m.Value));
                }
            }
            Notes(0);
            for (var k = 0; k + 1 < pts.Count; k++)
            {
                int x = pts[k], e = pts[k + 1]; if (e <= x) continue;
                var (f, size) = p.At(x);
                var link = p.Marks.FirstOrDefault(m => m.Kind == MarkKind.Link && m.Start <= x && m.End >= e)?.Value;
                var comment = p.Marks.FirstOrDefault(m => m.Kind == MarkKind.Comment && m.Length > 0 && m.Start <= x && m.End >= e)?.Value;
                XColor? color = link is not null ? LinkColor : null;
                if (p.Marks.Any(m => m.Kind == MarkKind.Insert && m.Start <= x && m.End >= e)) { color = InsColor; f |= Fmt.Underline; }
                if (p.Marks.Any(m => m.Kind == MarkKind.Delete && m.Start <= x && m.End >= e)) { color = DelColor; f |= Fmt.Strike; }
                AddText(p.Text[x..e], f, size, color, link, comment, comment is not null);
                Notes(e);
            }
            lines.Add(line);
            var baseSize = TextDoc.StyleSize(p.Style);
            var placedComments = new HashSet<string>();
            for (var li = 0; li < lines.Count; li++)
            {
                var ln = lines[li];
                var lh = Math.Max(baseSize, ln.Count == 0 ? 0 : ln.Max(w => w.Font.Size)) * 1.32;
                // Fußnoten dieser Zeile brauchen unten Platz
                var lineNotes = notesHere.Where(n => ln.Contains(n.Ref)).ToList();
                var noteH = lineNotes.Sum(n => Wrap(n.Text, F(8.5), Body).Count * 11.0) + (pageNotes.Count == 0 && lineNotes.Count > 0 ? 8 : 0);
                Need(lh + noteH);
                foreach (var n in lineNotes) pageNotes.Add((n.No, n.Text));
                footReserve += noteH;
                if (li == 0 && p.List != ListKind.None)
                    g.DrawString(p.List == ListKind.Bullet ? "•" : number + ".", Font(p, Fmt.None, null), XBrushes.Black, X0 + indent - 0.6 * Cm, y + baseSize);
                var total = ln.Sum(w => w.W) + ln.Take(Math.Max(0, ln.Count - 1)).Sum(w => w.SpaceAfter ? g.MeasureString(" ", w.Font).Width : 0);
                var last = li == lines.Count - 1;
                var x = X0 + indent + p.Align switch { Align.Center => (avail - total) / 2, Align.Right => avail - total, _ => 0 };
                var gaps = ln.Take(Math.Max(0, ln.Count - 1)).Count(w => w.SpaceAfter);
                var extra = p.Align == Align.Justify && !last && gaps > 0 ? (avail - total) / gaps : 0;
                foreach (var w in ln)
                {
                    var by = y + lh * 0.78 + w.Rise;
                    var adv = w.W + (w.SpaceAfter ? g.MeasureString(" ", w.Font).Width + extra : 0);
                    if (w.Highlight) g.DrawRectangle(new XSolidBrush(XColor.FromArgb(110, 255, 230, 120)), x, y + lh * 0.12, adv, lh * 0.82);
                    var brush = new XSolidBrush(w.Color ?? baseColor); var pen = new XPen(w.Color ?? XColors.Black, 0.6);
                    if (w.Text.Length > 0) g.DrawString(w.Text, w.Font, brush, x, by);
                    if (w.Underline) g.DrawLine(pen, x, by + 1.5, x + w.W, by + 1.5);
                    if (w.Strike) g.DrawLine(pen, x, by - w.Font.Size * 0.3, x + w.W, by - w.Font.Size * 0.3);
                    if (w.Link is { } url && Uri.TryCreate(url, UriKind.Absolute, out _)) cur.AddWebLink(new PdfRectangle(new XRect(x, pageH - by - 2, Math.Max(1, w.W), w.Font.Size + 3)), url);
                    if (w.Comment is { } c && placedComments.Add(c))
                        cur.Annotations.Add(new PdfTextAnnotation { Title = "Kommentar", Contents = c, Icon = PdfTextAnnotationIcon.Comment, Rectangle = new PdfRectangle(new XRect(Math.Min(pageW - 20, X0 + ColW + 4), pageH - y - 16, 14, 14)) });
                    x += adv;
                }
                y += lh;
            }
            y += p.SpaceAfterPt ?? (p.Style == ParaStyle.Title ? 10 : 6);
        }

        static string ToRoman(int n) { var vals = new[] { 10, 9, 5, 4, 1 }; var syms = new[] { "x", "ix", "v", "iv", "i" }; var s = ""; for (var i = 0; i < vals.Length; i++) while (n >= vals[i]) { s += syms[i]; n -= vals[i]; } return s; }

        // ---------- Inhaltsverzeichnis ----------
        void DrawToc(TocBlock toc)
        {
            y += 6;
            Text(toc.Title, F(16, XFontStyleEx.Bold), new XSolidBrush(Heading)); y += 4;
            foreach (var e in TextTools.Toc(doc))
            {
                var f = F(e.Level == 1 ? 11 : 10.5, e.Level == 1 ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                var h = f.Size * 1.5; Need(h);
                var ind = (e.Level - 1) * 0.6 * Cm; var no = tocPages is not null && tocPages.TryGetValue(e.Block, out var pg) ? pg.ToString() : "…";
                var nw = g.MeasureString(no, f).Width; var avail = ColW - ind - nw - 6;
                var text = e.Text; while (text.Length > 1 && g.MeasureString(text, f).Width > avail - 10) text = text[..^1];
                g.DrawString(text, f, XBrushes.Black, X0 + ind, y + f.Size);
                var tw = g.MeasureString(text, f).Width; var dots = "";
                while (g.MeasureString(dots + " .", f).Width < avail - tw - 4) dots += " .";
                g.DrawString(dots, f, XBrushes.Gray, X0 + ind + tw + 2, y + f.Size);
                g.DrawString(no, f, XBrushes.Black, X0 + ColW - nw, y + f.Size);
                y += h;
            }
            y += 8;
        }

        // ---------- Tabellen und Bilder ----------
        void DrawTable(Table t)
        {
            var cols = Math.Max(1, t.Cols); var cw = ColW / cols;
            var font = F(10); var pen = new XPen(XColors.Gray, 0.5);
            foreach (var row in t.Rows)
            {
                var cellLines = Enumerable.Range(0, cols).Select(c => Wrap(c < row.Count ? row[c] : "", font, cw - 6)).ToList();
                var h = Math.Max(1, cellLines.Max(l => l.Count)) * 13 + 6;
                Need(h);
                for (var c = 0; c < cols; c++)
                {
                    var x = X0 + c * cw;
                    g.DrawRectangle(pen, x, y, cw, h);
                    for (var k = 0; k < cellLines[c].Count; k++) g.DrawString(cellLines[c][k], font, XBrushes.Black, x + 3, y + 13 + k * 13);
                }
                y += h;
            }
            y += 8;
        }

        void DrawPicture(Picture pic)
        {
            try
            {
                using var ms = new MemoryStream(pic.Data);
                using var img = XImage.FromStream(ms);
                var w = Math.Min(ColW, pic.WidthCm * Cm); var h = w * (pic.HeightCm / Math.Max(0.1, pic.WidthCm));
                var maxH = pageH - Top - Bottom - 30;
                if (h > maxH) { h = maxH; w = h * pic.WidthCm / pic.HeightCm; }
                Need(h + 6); g.DrawImage(img, X0, y, w, h); y += h + 8;
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException) { Need(14); g.DrawString("[Bild konnte nicht gezeichnet werden]", F(9), XBrushes.Gray, X0, y + 10); y += 16; }
            if (pic.Caption.Trim().Length > 0)
            {
                var f = F(9, XFontStyleEx.Italic);
                foreach (var line in Wrap($"Abbildung {++figure}: {pic.Caption.Trim()}", f, ColW)) { Need(12); g.DrawString(line, f, new XSolidBrush(XColor.FromArgb(0x59, 0x59, 0x59)), X0, y + 9); y += 12; }
                y += 6;
            }
        }

        List<string> Wrap(string text, XFont font, double width)
        {
            var lines = new List<string>();
            foreach (var part in text.Split('\n'))
            {
                var line = "";
                foreach (var w in part.Split(' '))
                {
                    var test = line.Length == 0 ? w : line + " " + w;
                    if (g.MeasureString(test, font).Width > width && line.Length > 0) { lines.Add(line); line = w; } else line = test;
                }
                lines.Add(line);
            }
            return lines;
        }
    }

    /// <summary>Text in Wörter samt folgendem Leerzeichen zerlegen.</summary>
    static IEnumerable<string> Tokens(string s)
    {
        var start = 0;
        for (var i = 0; i < s.Length; i++)
            if (s[i] == ' ') { yield return s[start..(i + 1)]; start = i + 1; }
        if (start < s.Length) yield return s[start..];
    }

    static XColor ParseColor(string hex)
    {
        var h = hex.TrimStart('#'); if (h.Length == 6) h = "FF" + h;
        return uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v) ? XColor.FromArgb((int)(v >> 24), (int)((v >> 16) & 255), (int)((v >> 8) & 255), (int)(v & 255)) : XColors.Black;
    }
}
