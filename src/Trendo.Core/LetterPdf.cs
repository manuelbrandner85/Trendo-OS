using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core;

/// <summary>Brief als PDF ohne LibreOffice (Wunsch 03.11.2026: „Als PDF“ ging nicht, wenn LibreOffice fehlt). Gleiche Lage wie die Vorschau:
/// DIN 5008 Form A/B, Anschriftfeld, Informationsblock, Betreff, Text mit Seitenumbruch, Gruß, Unterschrift, Anlagen, Falz- und Lochmarken,
/// Seitenzahlen und alle Kopf-Layouts der Vorlagen. Maße in mm, gezeichnet mit PDFsharp (MIT).</summary>
public static class LetterPdf
{
    const double Mm = 72 / 25.4;
    static readonly XColor Grey = XColor.FromArgb(0x5C, 0x6B, 0x7A);

    /// <summary>Schreibt den Brief als PDF; gibt die Seitenzahl zurück.</summary>
    public static int Write(LetterDraft d, string path)
    {
        Sys.PdfTools.EnsureFonts();
        LetterDesigns.Normalize(d);
        var formA = d.Form == "A"; var shift = formA ? -18.0 : 0;
        var family = d.Designed ? d.Font : "Arial";
        var accent = Hex(d.Accent); var accent2 = Hex(LetterDesigns.Second(d));
        XFont F(double pt, bool bold = false)
        {
            try { return new XFont(family, pt, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular); }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException) { return new XFont("Arial", pt, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular); }
        }
        var lineH = 11 * 0.3528 * 1.2; // mm, wie die Vorschau

        using var doc = new PdfDocument();
        doc.Info.Title = d.Subject.Trim().Length > 0 ? d.Subject.Trim() : "Brief"; doc.Info.Author = d.SenderName;
        var measure = XGraphics.CreateMeasureContext(new XSize(595, 842), XGraphicsUnit.Point, XPageDirection.Downwards);
        List<string> Wrap(string text, XFont f, double widthMm)
        {
            var res = new List<string>(); var max = widthMm * Mm;
            foreach (var para in text.Replace("\r", "").Split('\n'))
            {
                if (para.Length == 0) { res.Add(""); continue; }
                var line = "";
                foreach (var word in para.Split(' '))
                {
                    var test = line.Length == 0 ? word : line + " " + word;
                    if (measure.MeasureString(test, f).Width <= max || line.Length == 0) { line = test; continue; }
                    res.Add(line); line = word;
                }
                res.Add(line);
            }
            return res;
        }

        var pages = new List<(PdfPage Page, XGraphics G)>();
        XGraphics NewPage()
        {
            var p = doc.AddPage(); p.Width = XUnit.FromMillimeter(210); p.Height = XUnit.FromMillimeter(297);
            var g = XGraphics.FromPdfPage(p); pages.Add((p, g));
            if (d.PrintMarks) foreach (var (_, y, len) in Letters.Marks(formA)) g.DrawLine(new XPen(XColors.Gray, 0.3 * Mm), 3 * Mm, y * Mm, (3 + len) * Mm, y * Mm);
            if (d.Designed && d.Header == "rahmen") g.DrawRectangle(new XPen(accent, 0.5 * Mm), 8 * Mm, 8 * Mm, 194 * Mm, 281 * Mm);
            if (d.Designed && d.Header == "fussband")
            {
                g.DrawRectangle(new XSolidBrush(accent), 0, 285 * Mm, 210 * Mm, 12 * Mm);
                Text(g, string.Join("  ·  ", new[] { d.SenderName, d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => s.Trim().Length > 0)), F(8.5), XColors.White, 15, 288.5, 180, XStringFormats.TopCenter);
            }
            return g;
        }
        void Text(XGraphics g, string t, XFont f, XColor c, double x, double y, double w, XStringFormat? fmt = null) =>
            g.DrawString(t, f, new XSolidBrush(c), new XRect(x * Mm, y * Mm, w * Mm, 20 * Mm), fmt ?? XStringFormats.TopLeft);
        void Lines(XGraphics g, IEnumerable<string> lines, XFont f, XColor c, double x, double y, double w, double step, XStringFormat? fmt = null)
        { foreach (var l in lines) { Text(g, l, f, c, x, y, w, fmt); y += step; } }

        var g1 = NewPage();
        // Kopf: Absender (klassisch) oder Design-Kopf
        if (d.Designed) DesignHead(g1);
        else Lines(g1, new[] { d.SenderName, d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => !string.IsNullOrWhiteSpace(s)), F(9), Grey, 105, formA ? 8 : 15, 85, 9 * 0.3528 * 1.2, XStringFormats.TopRight);
        // Rücksendeangabe, Anschrift, Informationsblock
        var back = string.Join(" · ", new[] { d.SenderName, d.SenderStreet, d.SenderCity }.Where(s => !string.IsNullOrWhiteSpace(s)));
        while (back.Length > 4 && measure.MeasureString(back, F(7)).Width > 80 * Mm) back = back[..^2].TrimEnd() + "…";
        Text(g1, back, F(7), Grey, 25, 58 + shift, 80);
        Lines(g1, d.Recipient.Replace("\r", "").Split('\n'), F(11), XColors.Black, 25, 62.7 + shift, 80, lineH);
        Lines(g1, Letters.InfoBlock(d), F(9), XColors.Black, 125, 50 + shift, 75, 9 * 0.3528 * 1.2);
        // Betreff, Anrede
        var y = 98.5 + shift;
        var subj = Wrap(d.Subject, F(11, true), 165);
        Lines(g1, subj, F(11, true), d.Designed ? accent : XColors.Black, 25, y, 165, lineH); y += Math.Max(1, subj.Count) * lineH + 8.5;
        var sal = Wrap(d.Salutation, F(11), 165);
        Lines(g1, sal, F(11), XColors.Black, 25, y, 165, lineH); y += Math.Max(1, sal.Count) * lineH + 4.2;
        // Text mit Seitenumbruch (unten frei; Fußband braucht mehr Platz)
        var bottom = d.Designed && d.Header == "fussband" ? 270.0 : 277; const double FollowTop = 27;
        double sigH = 0; byte[]? sig = null;
        if (File.Exists(d.SignatureFile)) { sig = File.ReadAllBytes(d.SignatureFile); var (sw, sh) = XrPlus.ImageSize(sig); sigH = sw > 0 ? 45.0 * sh / sw : 0; if (sigH == 0) sig = null; }
        var att = string.IsNullOrWhiteSpace(d.Attachments) ? [] : Wrap(d.Attachments.Trim(), F(11), 165);
        var endH = 4.2 + lineH + (sigH > 0 ? sigH + 1 : 12) + lineH + (att.Count > 0 ? 6 + (1 + att.Count) * lineH : 0);
        var g = g1;
        void Next() { g = NewPage(); y = FollowTop; }
        foreach (var l in Wrap(d.Body, F(11), 165)) { if (y + lineH > bottom) Next(); Text(g, l, F(11), XColors.Black, 25, y, 170); y += lineH; }
        if (y + endH > bottom) Next();
        y += 4.2; Text(g, d.Closing, F(11), XColors.Black, 25, y, 165); y += lineH;
        if (sig is not null)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "trendo-sig-" + Guid.NewGuid().ToString("N") + (sig[0] == 0x89 ? ".png" : ".jpg"));
            try { File.WriteAllBytes(tmp, sig); using var img = XImage.FromFile(tmp); g.DrawImage(img, 25 * Mm, (y + 1) * Mm, 45 * Mm, sigH * Mm); }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException) { }
            finally { try { File.Delete(tmp); } catch (IOException) { } }
            y += sigH + 1;
        }
        else y += 12;
        Text(g, d.SenderName, F(11), XColors.Black, 25, y, 165); y += lineH;
        if (att.Count > 0) { Text(g, "Anlagen", F(11, true), XColors.Black, 25, y + 6, 165); Lines(g, att, F(11), XColors.Black, 25, y + 6 + lineH, 165, lineH); }
        // Seitenzahlen und Kopf der Folgeseiten
        if (pages.Count > 1)
            for (var i = 0; i < pages.Count; i++)
            {
                var pg = pages[i].G;
                Text(pg, $"Seite {i + 1} von {pages.Count}", F(8), Grey, 145, d.Designed && d.Header == "fussband" ? 279 : 287, 60);
                if (i == 0) continue;
                if (d.Designed) pg.DrawRectangle(new XSolidBrush(accent), 25 * Mm, 20 * Mm, 165 * Mm, 0.5 * Mm);
                Text(pg, (d.SenderName.Trim().Length > 0 ? d.SenderName.Trim() + " · " : "") + d.Subject, F(8), Grey, 25, 15, 160);
            }
        foreach (var (_, pg) in pages) pg.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmpPdf = path + ".tmp"; doc.Save(tmpPdf); SafeFile.Move(tmpPdf, path);
        return pages.Count;

        void DesignHead(XGraphics g0)
        {
            var hdrH = formA ? 22.0 : 32.0; var h = d.Header;
            var onBand = h is "band" or "zweifarbig";
            if (h == "band") g0.DrawRectangle(new XSolidBrush(accent), 0, 0, 210 * Mm, hdrH * Mm);
            if (h == "zweifarbig") { g0.DrawRectangle(new XSolidBrush(accent), 0, 0, 210 * Mm, (hdrH - 3) * Mm); g0.DrawRectangle(new XSolidBrush(accent2), 0, (hdrH - 3) * Mm, 210 * Mm, 3 * Mm); }
            if (h == "seitenleiste") g0.DrawRectangle(new XSolidBrush(accent), 0, 0, 7 * Mm, 297 * Mm);
            var hasPhoto = File.Exists(d.Photo);
            var nameC = onBand ? XColors.White : accent; var softC = onBand ? XColor.FromArgb(230, 255, 255, 255) : Grey;
            var contact = new[] { d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (h == "mittig")
            {
                Text(g0, d.SenderName, F(formA ? 16 : 20, true), accent, 25, formA ? 4 : 7, 160, XStringFormats.TopCenter);
                if (d.Tagline.Length > 0) Text(g0, d.Tagline, F(10), Grey, 25, formA ? 11.5 : 15.5, 160, XStringFormats.TopCenter);
                Text(g0, string.Join("  ·  ", contact), F(8.5), Grey, 15, formA ? 16 : 21.5, 180, XStringFormats.TopCenter);
                g0.DrawRectangle(new XSolidBrush(accent), 75 * Mm, (hdrH - 3) * Mm, 60 * Mm, 0.6 * Mm);
            }
            else
            {
                Text(g0, d.SenderName, F(formA ? 16 : 20, true), nameC, 25, formA ? 4 : 7, 125);
                if (d.Tagline.Length > 0) Text(g0, d.Tagline, F(10), softC, 25, formA ? 11.5 : 15.5, 125);
                Lines(g0, contact, F(8.5), softC, hasPhoto ? 103 : 130, formA ? 4 : 7, 60, 8.5 * 0.3528 * 1.2, XStringFormats.TopRight);
                if (h is "linie" or "seitenleiste" or "rahmen") g0.DrawRectangle(new XSolidBrush(accent), 25 * Mm, (hdrH - 3) * Mm, 165 * Mm, 0.6 * Mm);
            }
            if (hasPhoto)
            {
                double pw = formA ? 19 : 26, ph = formA ? 24 : 33, px = formA ? 178 : 170, py = 4;
                try
                {
                    using var img = XImage.FromFile(d.Photo);
                    var scale = Math.Max(pw / img.PixelWidth, ph / img.PixelHeight);
                    var (iw, ih) = (img.PixelWidth * scale, img.PixelHeight * scale);
                    var st = g0.Save(); g0.IntersectClip(new XRect(px * Mm, py * Mm, pw * Mm, ph * Mm));
                    g0.DrawImage(img, (px - (iw - pw) / 2) * Mm, (py - (ih - ph) / 2) * Mm, iw * Mm, ih * Mm);
                    g0.Restore(st);
                    g0.DrawRectangle(new XPen(onBand ? XColors.White : accent, 0.6 * Mm), px * Mm, py * Mm, pw * Mm, ph * Mm);
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException) { }
            }
        }
    }

    static XColor Hex(string hex)
    {
        var v = Convert.ToInt32(hex.TrimStart('#'), 16);
        return XColor.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
    }
}
