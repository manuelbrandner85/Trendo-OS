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
