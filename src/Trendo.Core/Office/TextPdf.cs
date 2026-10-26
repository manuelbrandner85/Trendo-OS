using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Office;

/// <summary>T9: Trendo-Text-Dokument als PDF (A4, Ränder 2,5 cm). Eigener Seitensatz mit PDFsharp: Zeilenumbruch nach Wörtern, gemischte
/// Zeichenformate, Absatzarten, Listen, Ausrichtung (auch Blocksatz), Tabellen, Bilder, Seitenumbrüche, Seitenzahlen.</summary>
public static class TextPdf
{
    const double Cm = 72 / 2.54;
    const double PageW = 21 * Cm, PageH = 29.7 * Cm, Left = 2.5 * Cm, Right = 2.5 * Cm, Top = 2.5 * Cm, Bottom = 2 * Cm;
    const double Width = PageW - Left - Right;

    sealed record Word(string Text, XFont Font, bool Underline, bool Strike, double Rise, double W, bool SpaceAfter);

    public static int Export(TextDoc doc, string path)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle); // Schriften einrichten
        using var pdf = new PdfDocument();
        pdf.Info.Title = doc.Title ?? Path.GetFileNameWithoutExtension(path);
        pdf.Info.Creator = "Trendo Text";
        PdfPage page = null!; XGraphics g = null!; var y = 0.0;
        void NewPage()
        {
            g?.Dispose();
            page = pdf.AddPage(); page.Width = XUnit.FromPoint(PageW); page.Height = XUnit.FromPoint(PageH);
            g = XGraphics.FromPdfPage(page); y = Top;
        }
        void Need(double h) { if (y + h > PageH - Bottom && y > Top + 0.1) NewPage(); }
        NewPage();
        var number = 0;
        PdfSharp.Pdf.PdfOutline? h1 = null;
        foreach (var b in doc.Blocks)
        {
            switch (b)
            {
                case PageBreak: NewPage(); break;
                case Para p:
                    number = p.List == ListKind.Number ? number + 1 : 0;
                    if (p.Style is ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 && p.Text.Trim().Length > 0)
                    {
                        Need(TextDoc.StyleSize(p.Style) * 1.6); // Überschrift nicht allein am Seitenende
                        // Lesezeichen wie LibreOffice: Überschrift 1 oben, 2 und 3 darunter
                        var title = p.Text.Trim().Replace('\n', ' ');
                        if (p.Style == ParaStyle.Heading1 || h1 is null) h1 = pdf.Outlines.Add(title, page, true);
                        else h1.Outlines.Add(title, page, false);
                    }
                    DrawPara(p, number, g, ref y, Need, () => g);
                    break;
                case Table t: DrawTable(t, ref y, Need, () => g); break;
                case Picture pic:
                    try
                    {
                        using var ms = new MemoryStream(pic.Data);
                        using var img = XImage.FromStream(ms);
                        var w = Math.Min(Width, pic.WidthCm * Cm); var h = w * (pic.HeightCm / Math.Max(0.1, pic.WidthCm));
                        if (h > PageH - Top - Bottom) { h = PageH - Top - Bottom; w = h * pic.WidthCm / pic.HeightCm; }
                        Need(h + 6); g.DrawImage(img, Left, y, w, h); y += h + 8;
                    }
                    catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException) { Need(14); g.DrawString("[Bild konnte nicht gezeichnet werden]", new XFont("Arial", 9), XBrushes.Gray, Left, y + 10); y += 16; }
                    break;
            }
        }
        g.Dispose();
        var pages = pdf.PageCount;
        for (var i = 0; i < pages; i++)
            using (var pg = XGraphics.FromPdfPage(pdf.Pages[i], XGraphicsPdfPageOptions.Append))
                pg.DrawString($"{i + 1} / {pages}", new XFont("Arial", 8), XBrushes.Gray, new XRect(0, PageH - 1.2 * Cm, PageW, 12), XStringFormats.Center);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        pdf.Save(path);
        return pages;
    }

    static XFont Font(Para p, Fmt f, double? size)
    {
        var pt = size ?? TextDoc.StyleSize(p.Style);
        if (f.HasFlag(Fmt.Super) || f.HasFlag(Fmt.Sub)) pt *= 0.62;
        var style = XFontStyleEx.Regular;
        var bold = f.HasFlag(Fmt.Bold) || p.Style is ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3;
        var italic = f.HasFlag(Fmt.Italic) || p.Style == ParaStyle.Quote;
        if (bold) style |= XFontStyleEx.Bold;
        if (italic) style |= XFontStyleEx.Italic;
        return new XFont("Arial", pt, style);
    }

    static XBrush Color(Para p) => p.Style switch
    {
        ParaStyle.Heading1 => new XSolidBrush(XColor.FromArgb(0x1F, 0x38, 0x64)), ParaStyle.Heading2 or ParaStyle.Heading3 => new XSolidBrush(XColor.FromArgb(0x2F, 0x54, 0x96)),
        ParaStyle.Subtitle => new XSolidBrush(XColor.FromArgb(0x59, 0x59, 0x59)), ParaStyle.Quote => new XSolidBrush(XColor.FromArgb(0x40, 0x40, 0x40)), _ => XBrushes.Black,
    };

    static void DrawPara(Para p, int number, XGraphics g0, ref double y, Action<double> need, Func<XGraphics> gfx)
    {
        var indent = p.List != ListKind.None ? 0.9 * Cm : p.Style == ParaStyle.Quote ? 1 * Cm : 0;
        var avail = Width - indent - (p.Style == ParaStyle.Quote ? 1 * Cm : 0);
        var before = p.Style is ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 ? 10.0 : 0;
        y += before;
        // Wörter mit Format; harte Zeilenumbrüche (\n) erzwingen neue Zeilen
        var lines = new List<List<Word>>(); var cur = new List<Word>(); var curW = 0.0;
        var measure = gfx();
        foreach (var (text, f, size) in p.Runs())
        {
            var font = Font(p, f, size);
            var rise = f.HasFlag(Fmt.Super) ? -0.35 * (size ?? TextDoc.StyleSize(p.Style)) : f.HasFlag(Fmt.Sub) ? 0.15 * (size ?? TextDoc.StyleSize(p.Style)) : 0;
            var pieces = text.Replace("\t", "    ").Split('\n');
            for (var k = 0; k < pieces.Length; k++)
            {
                if (k > 0) { lines.Add(cur); cur = []; curW = 0; }
                foreach (var token in Tokens(pieces[k]))
                {
                    var space = token.EndsWith(' ');
                    var word = token.TrimEnd(' ');
                    var w = word.Length == 0 ? 0 : measure.MeasureString(word, font).Width;
                    var sw = space ? measure.MeasureString(" ", font).Width : 0;
                    if (curW + w > avail && cur.Count > 0) { lines.Add(cur); cur = []; curW = 0; }
                    // überlange Wörter hart teilen
                    while (w > avail && word.Length > 1)
                    {
                        var n = word.Length; while (n > 1 && measure.MeasureString(word[..n], font).Width > avail) n--;
                        cur.Add(new Word(word[..n], font, f.HasFlag(Fmt.Underline), f.HasFlag(Fmt.Strike), rise, measure.MeasureString(word[..n], font).Width, false));
                        lines.Add(cur); cur = []; curW = 0; word = word[n..]; w = measure.MeasureString(word, font).Width;
                    }
                    cur.Add(new Word(word, font, f.HasFlag(Fmt.Underline), f.HasFlag(Fmt.Strike), rise, w, space));
                    curW += w + sw;
                }
            }
        }
        lines.Add(cur);
        var baseSize = TextDoc.StyleSize(p.Style);
        var brush = Color(p);
        for (var li = 0; li < lines.Count; li++)
        {
            var line = lines[li];
            var lh = Math.Max(baseSize, line.Count == 0 ? 0 : line.Max(w => w.Font.Size)) * 1.32;
            need(lh); var g = gfx();
            if (li == 0 && p.List != ListKind.None)
                g.DrawString(p.List == ListKind.Bullet ? "•" : number + ".", Font(p, Fmt.None, null), XBrushes.Black, Left + indent - 0.6 * Cm, y + baseSize);
            var total = line.Sum(w => w.W) + line.Take(Math.Max(0, line.Count - 1)).Sum(w => w.SpaceAfter ? g.MeasureString(" ", w.Font).Width : 0);
            var last = li == lines.Count - 1;
            var x = Left + indent + p.Align switch { Align.Center => (avail - total) / 2, Align.Right => avail - total, _ => 0 };
            var gaps = line.Take(Math.Max(0, line.Count - 1)).Count(w => w.SpaceAfter);
            var extra = p.Align == Align.Justify && !last && gaps > 0 ? (avail - total) / gaps : 0;
            foreach (var w in line)
            {
                var by = y + lh * 0.78 + w.Rise;
                if (w.Text.Length > 0) g.DrawString(w.Text, w.Font, brush, x, by);
                if (w.Underline) g.DrawLine(new XPen(XColors.Black, 0.6), x, by + 1.5, x + w.W, by + 1.5);
                if (w.Strike) g.DrawLine(new XPen(XColors.Black, 0.6), x, by - w.Font.Size * 0.3, x + w.W, by - w.Font.Size * 0.3);
                x += w.W + (w.SpaceAfter ? g.MeasureString(" ", w.Font).Width + extra : 0);
            }
            y += lh;
        }
        y += p.Style == ParaStyle.Title ? 10 : 6;
    }

    /// <summary>Text in Wörter samt folgendem Leerzeichen zerlegen.</summary>
    static IEnumerable<string> Tokens(string s)
    {
        var start = 0;
        for (var i = 0; i < s.Length; i++)
            if (s[i] == ' ') { yield return s[start..(i + 1)]; start = i + 1; }
        if (start < s.Length) yield return s[start..];
    }

    static void DrawTable(Table t, ref double y, Action<double> need, Func<XGraphics> gfx)
    {
        var cols = Math.Max(1, t.Cols); var cw = Width / cols;
        var font = new XFont("Arial", 10); var pen = new XPen(XColors.Gray, 0.5);
        foreach (var row in t.Rows)
        {
            var g = gfx();
            var cellLines = Enumerable.Range(0, cols).Select(c => Wrap(c < row.Count ? row[c] : "", font, cw - 6, g)).ToList();
            var h = Math.Max(1, cellLines.Max(l => l.Count)) * 13 + 6;
            need(h); g = gfx();
            for (var c = 0; c < cols; c++)
            {
                var x = Left + c * cw;
                g.DrawRectangle(pen, x, y, cw, h);
                for (var k = 0; k < cellLines[c].Count; k++) g.DrawString(cellLines[c][k], font, XBrushes.Black, x + 3, y + 13 + k * 13);
            }
            y += h;
        }
        y += 8;
    }

    static List<string> Wrap(string text, XFont font, double width, XGraphics g)
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
