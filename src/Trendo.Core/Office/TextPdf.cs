using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Annotations;

namespace Trendo.Core.Office;

/// <summary>T9/T-0230: Trendo-Text-Dokument als PDF. Der Seitensatz kommt aus <see cref="FlowDoc"/> (T-0692: derselbe Umbruch wie am Bildschirm):
/// Seite einrichten (A4 hoch/quer, Ränder, Spalten, Seitenrand-Linie), Kopf-/Fußzeile mit „Seite n von m“, Wasserzeichen, Design (Schrift,
/// Überschriftenfarbe), Zeilenumbruch nach Wörtern, gemischte Zeichenformate, Absatzarten, Listen, Einzug und Abstand, Ausrichtung (auch Blocksatz),
/// Tabellen, Bilder mit Beschriftung, Seitenumbrüche, Inhaltsverzeichnis mit Seitenzahlen, Links (anklickbar), Kommentare (als PDF-Notiz),
/// Fußnoten unten auf der Seite, Endnoten am Schluss und Änderungen (eingefügt grün, gelöscht rot durchgestrichen). Ohne Programmname.
/// Hier wird nur noch gemalt: jede Seite des Satzes mit PDFsharp.</summary>
public static class TextPdf
{
    public static int Export(TextDoc doc, string path) => Export(FlowDoc.Build(doc), path);

    /// <summary>Einen fertigen Seitensatz als PDF speichern; liefert die Seitenzahl.</summary>
    public static int Export(FlowDoc flow, string path)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle); // Schriften einrichten
        using var pdf = Paint(flow);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var pages = pdf.PageCount;
        pdf.Save(path);
        return pages;
    }

    static XColor C(uint argb) => XColor.FromArgb((int)(argb >> 24), (int)((argb >> 16) & 255), (int)((argb >> 8) & 255), (int)(argb & 255));

    static PdfDocument Paint(FlowDoc flow)
    {
        var pdf = new PdfDocument();
        pdf.Info.Title = flow.Title;
        PdfOutline? h1 = null;
        foreach (var fp in flow.Pages)
        {
            var page = pdf.AddPage(); page.Width = XUnit.FromPoint(flow.PageW); page.Height = XUnit.FromPoint(flow.PageH);
            using var g = XGraphics.FromPdfPage(page);
            foreach (var item in fp.Items)
                switch (item)
                {
                    case FlowWatermark wm:
                        var st = g.Save();
                        g.TranslateTransform(flow.PageW / 2, flow.PageH / 2); g.RotateTransform(-45);
                        g.DrawString(wm.Text, FlowMeasure.Font(new FlowFont(wm.Family, wm.Size, true, false)), new XSolidBrush(XColor.FromArgb(48, 128, 128, 128)), new XRect(-flow.PageW, -60, flow.PageW * 2, 120), XStringFormats.Center);
                        g.Restore(st);
                        break;
                    case FlowFill f: g.DrawRectangle(new XSolidBrush(C(f.Color)), f.X, f.Y, f.W, f.H); break;
                    case FlowStroke s: g.DrawRectangle(new XPen(C(s.Color), s.Width), s.X, s.Y, s.W, s.H); break;
                    case FlowRule r: g.DrawLine(new XPen(C(r.Color), r.Width), r.X1, r.Y1, r.X2, r.Y2); break;
                    case FlowText t:
                        if (t.Text.Length > 0) g.DrawString(t.Text, FlowMeasure.Font(t.Font), new XSolidBrush(C(t.Color)), t.X, t.Y);
                        if (t.Underline || t.Strike)
                        {
                            var pen = new XPen(C(t.Pen), 0.6);
                            if (t.Underline) g.DrawLine(pen, t.X, t.Y + 1.5, t.X + t.W, t.Y + 1.5);
                            if (t.Strike) g.DrawLine(pen, t.X, t.Y - t.Font.Size * 0.3, t.X + t.W, t.Y - t.Font.Size * 0.3);
                        }
                        break;
                    case FlowAligned a:
                        g.DrawString(a.Text, FlowMeasure.Font(a.Font), new XSolidBrush(C(a.Color)), new XRect(a.X, a.Y, a.W, 12), a.Align switch { 1 => XStringFormats.TopCenter, 2 => XStringFormats.TopRight, _ => XStringFormats.TopLeft });
                        break;
                    case FlowImage im:
                        try { using var ms = new MemoryStream(im.Pic.Data); using var img = XImage.FromStream(ms); g.DrawImage(img, im.X, im.Y, im.W, im.H); }
                        catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException) { g.DrawString("[Bild konnte nicht gezeichnet werden]", FlowMeasure.Font(new FlowFont("Arial", 9, false, false)), XBrushes.Gray, im.X, im.Y + 10); }
                        break;
                    case FlowLink l: page.AddWebLink(new PdfRectangle(new XRect(l.X, flow.PageH - l.Y - 2, l.W, l.H + 3)), l.Url); break;
                    case FlowComment c:
                        page.Annotations.Add(new PdfTextAnnotation { Title = "Kommentar", Contents = c.Text, Icon = PdfTextAnnotationIcon.Comment, Rectangle = new PdfRectangle(new XRect(c.X, flow.PageH - c.Y - 16, 14, 14)) });
                        break;
                    case FlowOutline o:
                        if (o.Top || h1 is null) h1 = pdf.Outlines.Add(o.Title, page, true); else h1.Outlines.Add(o.Title, page, false);
                        break;
                }
        }
        return pdf;
    }
}
