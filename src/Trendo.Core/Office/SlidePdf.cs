using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Office;

/// <summary>T-0229 P6: Präsentation als PDF – eine Seite je Folie (16:9), optional mit Notizen darunter (Handzettel). Ohne Programmname.</summary>
public static class SlidePdf
{
    const double Pt = 72 / 2.54;

    public static int Export(SlideDoc doc, string path, bool withNotes = false)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle);
        using var pdf = new PdfDocument();
        pdf.Info.Title = doc.Slides.FirstOrDefault()?.Title ?? Path.GetFileNameWithoutExtension(path);
        double w = SlideDoc.WidthCm * Pt, h = SlideDoc.HeightCm * Pt;
        foreach (var s in doc.Slides)
        {
            var page = pdf.AddPage();
            page.Width = XUnit.FromPoint(w); page.Height = XUnit.FromPoint(withNotes ? h * 1.6 : h);
            using var g = XGraphics.FromPdfPage(page);
            Draw(g, doc, s, new XRect(0, 0, w, h));
            if (withNotes && s.Notes.Length > 0)
            {
                var f = new XFont("Arial", 14); var y = h + 24;
                foreach (var line in Wrap(s.Notes, f, w - 72, g)) { g.DrawString(line, f, XBrushes.Black, 36, y); y += 18; }
            }
        }
        if (pdf.PageCount == 0) { var p = pdf.AddPage(); p.Width = XUnit.FromPoint(w); p.Height = XUnit.FromPoint(h); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var pages = pdf.PageCount;
        pdf.Save(path);
        return pages;
    }

    static XColor Col(string hex) { var h = hex.TrimStart('#'); if (h.Length == 6) h = "FF" + h; var v = Convert.ToUInt32(h, 16); return XColor.FromArgb((int)(v >> 24), (int)((v >> 16) & 255), (int)((v >> 8) & 255), (int)(v & 255)); }

    /// <summary>Eine Folie in ein Rechteck zeichnen (für PDF und Druck).</summary>
    public static void Draw(XGraphics g, SlideDoc doc, Slide s, XRect area)
    {
        var th = doc.Theme; var k = area.Width / SlideDoc.WidthCm;
        g.DrawRectangle(new XSolidBrush(Col(s.Background ?? th.Background)), area);
        foreach (var shp in s.Shapes)
        {
            var r = new XRect(area.X + shp.X * k, area.Y + shp.Y * k, Math.Max(1, shp.W * k), Math.Max(1, shp.H * k));
            switch (shp.Kind)
            {
                case ShapeKind.Bild when shp.Image is { Length: > 0 }:
                    try { using var ms = new MemoryStream(shp.Image); using var img = XImage.FromStream(ms); g.DrawImage(img, r); }
                    catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException) { g.DrawRectangle(XPens.LightGray, r); }
                    break;
                case ShapeKind.Tabelle when shp.Table is { Count: > 0 } t:
                {
                    var cols = t.Max(x => x.Count); var cw = r.Width / Math.Max(1, cols); var rh = r.Height / t.Count; 
                    for (var i = 0; i < t.Count; i++)
                        for (var c = 0; c < cols; c++)
                        {
                            var cell = new XRect(r.X + c * cw, r.Y + i * rh, cw, rh);
                            g.DrawRectangle(new XPen(XColors.White, 1), new XSolidBrush(i == 0 ? Col(th.Accent) : i % 2 == 0 ? Col("#EEF2F7") : XColors.White), cell);
                            var txt = c < t[i].Count ? t[i][c] : "";
                            g.DrawString(txt, Font(th.Font, FontPt(shp.FontSize, k), i == 0 ? XFontStyleEx.Bold : XFontStyleEx.Regular), i == 0 ? XBrushes.White : new XSolidBrush(Col("#1F2937")), new XRect(cell.X + 4, cell.Y, cell.Width - 8, cell.Height), XStringFormats.CenterLeft);
                        }
                    break;
                }
                case ShapeKind.Diagramm when shp.ChartValues is { Count: > 0 }:
                {
                    var data = new ChartData(shp.ChartLabels ?? [], [(shp.Text.Length > 0 ? shp.Text : "Werte", shp.ChartValues.ToArray())]);
                    SheetPdf.DrawChart(g, new SheetChart(shp.ChartKind, "A1", shp.Text), data, r);
                    break;
                }
                default:
                {
                    if (shp.Fill is { } fill)
                    {
                        var b = new XSolidBrush(Col(fill));
                        if (shp.Kind == ShapeKind.Ellipse) g.DrawEllipse(b, r);
                        else if (shp.Kind == ShapeKind.Pfeil)
                        {
                            var pts = new[] { new XPoint(r.Left, r.Top + r.Height * 0.25), new XPoint(r.Right - r.Height * 0.5, r.Top + r.Height * 0.25), new XPoint(r.Right - r.Height * 0.5, r.Top), new XPoint(r.Right, r.Top + r.Height / 2), new XPoint(r.Right - r.Height * 0.5, r.Bottom), new XPoint(r.Right - r.Height * 0.5, r.Top + r.Height * 0.75), new XPoint(r.Left, r.Top + r.Height * 0.75) };
                            g.DrawPolygon(b, pts, XFillMode.Winding);
                        }
                        else g.DrawRectangle(b, r);
                    }
                    var text = shp.Kind == ShapeKind.Medien ? "▶ " + Path.GetFileName(shp.MediaPath ?? "") : shp.Text;
                    if (text.Length == 0) break;
                    var color = new XSolidBrush(Col(shp.Color ?? (shp.Role == "title" ? th.Title : th.Text)));
                    if (shp.Kind != ShapeKind.Medien && shp.Runs is { } runs) { DrawRuns(g, shp, runs, th, r, k, color); break; }
                    var lines = new List<(string Text, XFont Font, double Indent, bool Bullet)>();
                    foreach (var raw in text.Split('\n'))
                    {
                        var lvl = raw.StartsWith('\t') ? 1 : 0; var size = FontPt(shp.FontSize * (lvl == 1 ? 0.85 : 1), k);
                        var font = Font(th.Font, size, shp.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                        var indent = shp.Bullets ? (0.95 + lvl * 1.27) * k : 0;
                        var wrapped = Wrap(raw.TrimStart('\t'), font, r.Width - indent - 8, g);
                        for (var i = 0; i < wrapped.Count; i++) lines.Add((wrapped[i], font, indent, shp.Bullets && i == 0 && wrapped[i].Length > 0));
                    }
                    var total = lines.Sum(l => l.Font.Size * 1.2);
                    var y = shp.Kind == ShapeKind.Text ? r.Top + 4 : r.Top + Math.Max(4, (r.Height - total) / 2);
                    foreach (var (line, font, indent, bullet) in lines)
                    {
                        var lh = font.Size * 1.2;
                        if (bullet) g.DrawString("•", font, color, r.Left + 4 + indent - font.Size * 0.8, y + font.Size);
                        var fmt = shp.Align switch { Align.Center => XStringFormats.TopCenter, Align.Right => XStringFormats.TopRight, _ => XStringFormats.TopLeft };
                        g.DrawString(line, font, color, new XRect(r.Left + 4 + indent, y, r.Width - 8 - indent, lh), fmt);
                        y += lh;
                    }
                    break;
                }
            }
        }
    }

    /// <summary>T-0701: Textfeld mit mehreren Textformaten – Wort für Wort mit eigener Größe, Fett/Kursiv, Unterstreichung, Farbe und Schrift, umbrochen auf die Feldbreite.</summary>
    static void DrawRuns(XGraphics g, SlideShape shp, List<TextRun> runs, SlideTheme th, XRect r, double k, XBrush dflt)
    {
        var chars = TextRuns.Expand(runs, shp.Text);
        var lines = new List<(List<(string Text, XFont Font, XBrush Brush, bool Under)> Parts, double Indent, bool Bullet, double Height)>();
        var pos = 0;
        foreach (var raw in shp.Text.Split('\n'))
        {
            var lvl = raw.StartsWith('\t') ? 1 : 0; var line = raw.TrimStart('\t'); var start = pos + raw.Length - line.Length; pos += raw.Length + 1;
            var indent = shp.Bullets ? (0.95 + lvl * 1.27) * k : 0; var width = r.Width - 8 - indent;
            var cur = new List<(string, XFont, XBrush, bool)>(); double x = 0, h = FontPt(shp.FontSize * (lvl == 1 ? 0.85 : 1), k) * 1.2; var first = true;
            // in Wörter mit Leerzeichen zerlegen, je Wort das Format seines ersten Zeichens
            for (var i = 0; i < line.Length;)
            {
                var j = i; while (j < line.Length && line[j] != ' ') j++; while (j < line.Length && line[j] == ' ') j++;
                for (var a = i; a < j;)
                {
                    var look = chars[start + a]; var b = a; while (b < j && chars[start + b] == look) b++;
                    var size = FontPt(look.Size ?? shp.FontSize * (lvl == 1 ? 0.85 : 1), k);
                    var style = (look.Bold ?? shp.Bold, look.Italic == true) switch { (true, true) => XFontStyleEx.BoldItalic, (true, false) => XFontStyleEx.Bold, (false, true) => XFontStyleEx.Italic, _ => XFontStyleEx.Regular };
                    var font = Font(look.Font is { Length: > 0 } fn && !fn.StartsWith('+') ? fn : th.Font, size, style);
                    var piece = line[a..b]; var w = g.MeasureString(piece, font).Width;
                    if (x + w > width && x > 0 && a == i) { lines.Add((cur, indent, shp.Bullets && first, h)); first = false; cur = []; x = 0; h = size * 1.2; }
                    cur.Add((piece, font, look.Color is { } c ? new XSolidBrush(Col(c)) : dflt, look.Underline is not null)); x += w; h = Math.Max(h, size * 1.2);
                    a = b;
                }
                i = j;
            }
            lines.Add((cur, indent, shp.Bullets && first && line.Trim().Length > 0, h));
        }
        var total = lines.Sum(l => l.Height);
        var y = shp.Kind == ShapeKind.Text ? r.Top + 4 : r.Top + Math.Max(4, (r.Height - total) / 2);
        foreach (var (parts, indent, bullet, height) in lines)
        {
            var lineW = parts.Sum(p => g.MeasureString(p.Text, p.Font).Width);
            var x = r.Left + 4 + indent + shp.Align switch { Align.Center => (r.Width - 8 - indent - lineW) / 2, Align.Right => r.Width - 8 - indent - lineW, _ => 0 };
            if (bullet && parts.Count > 0) g.DrawString("•", parts[0].Font, dflt, r.Left + 4 + indent - parts[0].Font.Size * 0.8, y + parts[0].Font.Size);
            foreach (var (t, font, brush, under) in parts)
            {
                var w = g.MeasureString(t, font).Width;
                g.DrawString(t, font, brush, x, y + height * 0.8);
                if (under) g.DrawLine(new XPen(XColors.Black, Math.Max(0.5, font.Size / 16)), x, y + height * 0.88, x + w, y + height * 0.88);
                x += w;
            }
            y += height;
        }
    }

    /// <summary>Schrift des Designs; fehlt sie auf dem PC (z. B. Calibri ohne Office), Arial.</summary>
    static XFont Font(string family, double size, XFontStyleEx style = XFontStyleEx.Regular)
    {
        try { return new XFont(family, size, style); }
        catch (InvalidOperationException) { return new XFont("Arial", size, style); }
    }

    /// <summary>Schriftgröße (Punkt bei Original-Foliengröße) auf den Maßstab der Zeichnung umrechnen.</summary>
    static double FontPt(double size, double k) => Math.Max(4, size * k / Pt);

    static List<string> Wrap(string text, XFont font, double width, XGraphics g)
    {
        var lines = new List<string>(); var line = "";
        foreach (var w in text.Split(' '))
        {
            var test = line.Length == 0 ? w : line + " " + w;
            if (g.MeasureString(test, font).Width > width && line.Length > 0) { lines.Add(line); line = w; } else line = test;
        }
        lines.Add(line);
        return lines;
    }
}
