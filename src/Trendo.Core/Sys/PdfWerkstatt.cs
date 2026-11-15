using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using Trendo.Core.Kreativ;

namespace Trendo.Core.Sys;

/// <summary>Bild neu kodieren (JPEG). Der Kern kennt selbst keinen JPEG-Kodierer – die App reicht SkiaSharp herein. Eingabe: Pixel (RGB oder Grau, 8 Bit,
/// Zeile für Zeile), Breite, Höhe, Kanäle, Qualität 1–100. Ergebnis: JPEG-Bytes oder null (dann bleibt das Bild verlustfrei).</summary>
public delegate byte[]? JpegEncoder(byte[] pixels, int width, int height, int channels, int quality);
/// <summary>JPEG lesen (für JPEG-Bilder in der PDF): Pixel (RGB oder Grau, 8 Bit), Breite, Höhe, Kanäle – oder null.</summary>
public delegate (byte[] Pixels, int Width, int Height, int Channels)? JpegDecoder(byte[] jpeg);

/// <summary>Komprimieren: wie stark. <paramref name="Dpi"/> = höchste Bildauflösung bezogen auf die größte Seite, <paramref name="Quality"/> = JPEG-Qualität.</summary>
public sealed record CompressLevel(string Id, string Name, int Dpi, int Quality)
{
    public static readonly CompressLevel[] All = [new("leicht", "Leicht (fast wie vorher)", 220, 85), new("mittel", "Mittel (Bildschirm und Büro-Druck)", 150, 75), new("stark", "Stark (E-Mail, kleinste Datei)", 96, 60)];
    public static CompressLevel Get(string id) => All.FirstOrDefault(x => x.Id == id) ?? All[1];
}

/// <summary>Ergebnis des Komprimierens – ehrlich gemessen: Bytes vorher/nachher, wie viele Bilder es gab und wie viele kleiner wurden.</summary>
public sealed record CompressReport(long Before, long After, int Images, int Recoded, bool KeptOriginal, string Note)
{
    public double Saved => Before <= 0 ? 0 : 1 - (double)After / Before;
}

/// <summary>Schwarzer Balken: Seite (0-basiert), Lage in PDF-Punkten von links und von oben, Breite, Höhe.</summary>
public sealed record RedactBox(int Page, double X, double YTop, double W, double H);

/// <summary>Unterschied zwischen zwei PDFs: Art (' ' gleich, '+' nur in B, '−' nur in A), Seite in A bzw. B (1-basiert), Zeile.</summary>
public sealed record PdfDiffLine(char Op, int PageA, int PageB, string Text);
public sealed record PdfCompare(IReadOnlyList<PdfDiffLine> Lines, int Added, int Removed, int Same)
{
    public bool Identical => Added == 0 && Removed == 0;
}

/// <summary>STUDIO-PDFTOOLS-01 PDF-Werkstatt (T-0672): Stapel-Arbeit an PDF-Dateien – baut auf dem Kern von „Trendo PDF“ auf (Zusammenfügen, Teilen,
/// Seiten ordnen, Drehen, Passwort, Bild als Unterschrift) und ergänzt Bereiche teilen, Bilder zu PDF, Komprimieren, Wasserzeichen und Stempel,
/// Seitenzahlen, Metadaten, Schwärzen und Vergleichen. Clean-Room nach dem Verhalten von PDF24, PDFsam und Stirling-PDF, ohne fremden Code.
/// Jede Aktion schreibt eine neue Datei – das Original bleibt unverändert.</summary>
public static partial class PdfTools
{
    // ---------- Teilen nach Bereichen ----------

    /// <summary>Teilen nach Bereichen wie PDFsam „Split by page ranges“: „1-3; 4-6; 7-z“ – jede Gruppe (durch Semikolon oder Zeilenumbruch getrennt)
    /// wird eine eigene Datei name-teil1.pdf, name-teil2.pdf … Innerhalb einer Gruppe gilt die Seitenangabe von <see cref="ParsePages"/>.</summary>
    public static List<string> SplitRanges(string input, string outDir, string ranges, AuditLog? audit = null)
    {
        var groups = ranges.Split([';', '\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (groups.Length == 0) throw new ArgumentException("Bitte Bereiche angeben, z. B. „1-3; 4-6; 7-z“.");
        Directory.CreateDirectory(outDir);
        using var src = OpenImport(input);
        var lists = groups.Select(g => ParsePages(g, src.PageCount)).ToList(); // erst alle prüfen, dann schreiben
        var stem = Path.GetFileNameWithoutExtension(input); var files = new List<string>();
        for (var k = 0; k < lists.Count; k++)
        {
            using var t = new PdfDocument();
            foreach (var i in lists[k]) t.AddPage(src.Pages[i]);
            var f = Free(Path.Combine(outDir, $"{stem}-teil{k + 1}.pdf"));
            Guard(f, input); Save(t, f); files.Add(f);
        }
        audit?.Append("app", "pdf.split.ranges", $"{Path.GetFileName(input)} → {files.Count} Dateien");
        return files;
    }

    /// <summary>Freier Dateiname: „name.pdf“, sonst „name (2).pdf“ …</summary>
    public static string Free(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!; var stem = Path.GetFileNameWithoutExtension(path); var ext = Path.GetExtension(path);
        for (var i = 2; ; i++) { var f = Path.Combine(dir, $"{stem} ({i}){ext}"); if (!File.Exists(f)) return f; }
    }

    // ---------- Bilder zu PDF ----------

    /// <summary>Bilder (PNG, JPG) zu einer PDF, je Bild eine Seite. <paramref name="pageSize"/>: „a4“ (Bild eingepasst, Hoch-/Querformat nach dem Bild,
    /// Rand <paramref name="marginMm"/>) oder „bild“ (Seite so groß wie das Bild bei 96 dpi). Gibt die Seitenzahl zurück.</summary>
    public static int ImagesToPdf(IReadOnlyList<string> images, string output, string pageSize = "a4", double marginMm = 10, AuditLog? audit = null)
    {
        if (images.Count == 0) throw new ArgumentException("Bitte mindestens ein Bild wählen.");
        Guard(output, [.. images]);
        using var d = new PdfDocument();
        foreach (var f in images)
        {
            if (!File.Exists(f)) throw new ArgumentException("Bild nicht gefunden: " + Path.GetFileName(f));
            var (iw, ih) = XrPlus.ImageSize(File.ReadAllBytes(f));
            if (iw <= 0 || ih <= 0) throw new ArgumentException($"„{Path.GetFileName(f)}“ ist kein PNG- oder JPG-Bild.");
            var page = d.AddPage();
            double pw, ph, m;
            if (pageSize == "bild") { pw = iw * 0.75; ph = ih * 0.75; m = 0; }
            else { (pw, ph) = iw > ih ? (841.89, 595.28) : (595.28, 841.89); m = Math.Clamp(marginMm, 0, 50) * 72 / 25.4; }
            page.Width = XUnit.FromPoint(pw); page.Height = XUnit.FromPoint(ph);
            var s = Math.Min((pw - 2 * m) / iw, (ph - 2 * m) / ih); var w = iw * s; var h = ih * s;
            using var g = XGraphics.FromPdfPage(page);
            using var img = XImage.FromFile(f);
            g.DrawImage(img, (pw - w) / 2, (ph - h) / 2, w, h);
        }
        var n = d.PageCount; Save(d, output);
        audit?.Append("app", "pdf.images", $"{images.Count} Bilder → {Path.GetFileName(output)}");
        return n;
    }

    // ---------- Wasserzeichen, Stempel, Seitenzahlen ----------

    static XColor Rgb(string hex, double alpha = 1)
    {
        var (r, g, b) = PaintColors.Rgb(PaintColors.IsHex(hex) ? hex : "#C00000");
        return XColor.FromArgb((int)Math.Round(Math.Clamp(alpha, 0, 1) * 255), r, g, b);
    }

    static void EachPage(PdfDocument d, string? spec, Action<PdfPage, XGraphics, int> draw)
    {
        foreach (var i in ParsePages(spec ?? "", d.PageCount).Distinct())
        {
            var p = d.Pages[i];
            using var g = XGraphics.FromPdfPage(p, XGraphicsPdfPageOptions.Append);
            draw(p, g, i);
        }
    }

    /// <summary>Wasserzeichen wie bei PDF24: Text schräg (<paramref name="angle"/> Grad) mittig über die Seite, halbdurchsichtig.
    /// Schriftgröße 0 = passend zur Seitenbreite. Seiten nach <see cref="ParsePages"/> (leer = alle).</summary>
    public static void Watermark(string input, string output, string text, double opacity = 0.18, double size = 0, double angle = -45, string color = "#C00000", string? spec = null, AuditLog? audit = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Bitte einen Text für das Wasserzeichen eingeben.");
        EditCopy(input, output, d => EachPage(d, spec, (p, g, _) =>
        {
            var w = p.Width.Point; var h = p.Height.Point;
            var diag = Math.Sqrt(w * w + h * h);
            var fs = size > 0 ? size : Math.Clamp(diag * 0.75 / Math.Max(4, text.Length) * 1.7, 12, 160);
            var font = new XFont("Arial", fs, XFontStyleEx.Bold);
            g.TranslateTransform(w / 2, h / 2); g.RotateTransform(angle);
            var sz = g.MeasureString(text, font);
            g.DrawString(text, font, new XSolidBrush(Rgb(color, opacity)), -sz.Width / 2, sz.Height / 3);
        }));
        audit?.Append("app", "pdf.watermark", Path.GetFileName(output));
    }

    /// <summary>Lagen für Stempel und Seitenzahlen.</summary>
    public static readonly (string Id, string Name)[] Positions =
        [("unten-mitte", "Unten Mitte"), ("unten-rechts", "Unten rechts"), ("unten-links", "Unten links"), ("oben-rechts", "Oben rechts"), ("oben-mitte", "Oben Mitte"), ("oben-links", "Oben links")];

    static (double X, double Y) At(string pos, double pw, double ph, double w, double h, double margin)
    {
        var x = pos.EndsWith("rechts", StringComparison.Ordinal) ? pw - margin - w : pos.EndsWith("links", StringComparison.Ordinal) ? margin : (pw - w) / 2;
        var y = pos.StartsWith("oben", StringComparison.Ordinal) ? margin : ph - margin - h;
        return (x, y);
    }

    /// <summary>Stempel wie in Acrobat: Text im Rahmen (z. B. „ENTWURF“, „GEPRÜFT“, „KOPIE“) an einer Ecke, mit Datum darunter, wenn gewünscht.</summary>
    public static void Stamp(string input, string output, string text, string position = "oben-rechts", string color = "#C00000", bool withDate = true, DateTime? date = null, string? spec = null, AuditLog? audit = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Bitte einen Text für den Stempel eingeben.");
        var day = (date ?? DateTime.Now).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        EditCopy(input, output, d => EachPage(d, spec, (p, g, _) =>
        {
            var font = new XFont("Arial", 18, XFontStyleEx.Bold); var small = new XFont("Arial", 9);
            var t = g.MeasureString(text.ToUpperInvariant(), font); var sd = g.MeasureString(day, small);
            var w = Math.Max(t.Width, withDate ? sd.Width : 0) + 20; var h = t.Height + (withDate ? sd.Height + 2 : 0) + 12;
            var (x, y) = At(position, p.Width.Point, p.Height.Point, w, h, 28);
            var pen = new XPen(Rgb(color, 0.9), 2.2);
            g.DrawRoundedRectangle(pen, new XSolidBrush(XColor.FromArgb(30, 255, 255, 255)), x, y, w, h, 8, 8);
            var brush = new XSolidBrush(Rgb(color, 0.9));
            g.DrawString(text.ToUpperInvariant(), font, brush, x + (w - t.Width) / 2, y + 6 + t.Height * 0.8);
            if (withDate) g.DrawString(day, small, brush, x + (w - sd.Width) / 2, y + 8 + t.Height + sd.Height * 0.8);
        }));
        audit?.Append("app", "pdf.stamp", Path.GetFileName(output));
    }

    /// <summary>Seitenzahl-Text: „{n}“ = Seite, „{N}“ = Gesamtzahl, z. B. „Seite {n} von {N}“.</summary>
    public static string PageLabel(string format, int n, int total) =>
        (string.IsNullOrWhiteSpace(format) ? "{n}" : format).Replace("{n}", n.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal).Replace("{N}", total.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Seitenzahlen wie PDF24 „Seitenzahlen hinzufügen“: Format, Lage, erste Zahl, erste Seite auslassen (Deckblatt). Gibt die Zahl der nummerierten Seiten zurück.</summary>
    public static int PageNumbers(string input, string output, string format = "Seite {n} von {N}", string position = "unten-mitte", int start = 1, bool skipFirst = false, double size = 10, AuditLog? audit = null)
    {
        var count = 0;
        EditCopy(input, output, d =>
        {
            var first = skipFirst ? 1 : 0; var total = d.PageCount - first + start - 1;
            for (var i = first; i < d.PageCount; i++)
            {
                var p = d.Pages[i];
                using var g = XGraphics.FromPdfPage(p, XGraphicsPdfPageOptions.Append);
                var font = new XFont("Arial", Math.Clamp(size, 6, 36));
                var label = PageLabel(format, i - first + start, total);
                var sz = g.MeasureString(label, font);
                var (x, y) = At(position, p.Width.Point, p.Height.Point, sz.Width, sz.Height, 24);
                g.DrawString(label, font, XBrushes.Black, x, y + sz.Height * 0.8);
                count++;
            }
        });
        audit?.Append("app", "pdf.pagenumbers", $"{count} Seiten {Path.GetFileName(output)}");
        return count;
    }

    // ---------- Metadaten ----------

    static readonly string[] MetaKeys = ["Title", "Author", "Subject", "Keywords", "Creator", "Producer"];
    /// <summary>Anzeigenamen der Metadaten.</summary>
    public static readonly (string Key, string Name)[] MetaNames = [("Title", "Titel"), ("Author", "Verfasser"), ("Subject", "Thema"), ("Keywords", "Stichwörter"), ("Creator", "Erstellt mit"), ("Producer", "PDF erzeugt von")];

    /// <summary>Metadaten lesen (Info-Wörterbuch) und ob zusätzlich XMP-Metadaten im Katalog stecken.</summary>
    public static (Dictionary<string, string> Info, bool Xmp) ReadMeta(string path, string? password = null)
    {
        using var d = OpenImport(path, password);
        var info = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in MetaKeys) { var v = d.Info.Elements.GetString("/" + k); if (!string.IsNullOrWhiteSpace(v)) info[k] = v; }
        return (info, d.Internals.Catalog.Elements.ContainsKey("/Metadata"));
    }

    /// <summary>Metadaten setzen (Titel, Verfasser, Thema, Stichwörter). Leerer Wert löscht den Eintrag.</summary>
    public static void WriteMeta(string input, string output, IReadOnlyDictionary<string, string> values, AuditLog? audit = null)
    {
        EditCopy(input, output, d =>
        {
            foreach (var (k, v) in values)
            {
                if (!MetaKeys.Contains(k) || k is "Producer") continue;
                if (string.IsNullOrWhiteSpace(v)) d.Info.Elements.Remove("/" + k); else d.Info.Elements["/" + k] = new PdfString(v.Trim(), PdfStringEncoding.Unicode);
            }
            d.Internals.Catalog.Elements.Remove("/Metadata"); // alte XMP-Angaben würden den neuen widersprechen
        });
        audit?.Append("app", "pdf.meta", Path.GetFileName(output));
    }

    /// <summary>Metadaten entfernen: Info-Wörterbuch leeren, XMP-Metadaten aus Katalog und Seiten löschen. Den Eintrag „PDF erzeugt von“ schreibt die
    /// PDF-Bibliothek beim Speichern selbst – er wird danach mit Leerzeichen gleicher Länge überschrieben (die Datei bleibt gültig).</summary>
    public static void RemoveMeta(string input, string output, AuditLog? audit = null)
    {
        EditCopy(input, output, d =>
        {
            foreach (var k in d.Info.Elements.Keys.ToList()) d.Info.Elements.Remove(k);
            d.Internals.Catalog.Elements.Remove("/Metadata");
            foreach (var p in d.Pages.Cast<PdfPage>()) p.Elements.Remove("/Metadata");
        });
        BlankInfoStrings(output);
        audit?.Append("app", "pdf.meta.remove", Path.GetFileName(output));
    }

    /// <summary>Was die PDF-Bibliothek beim Speichern selbst schreibt, nachträglich mit Leerzeichen gleicher Länge überschreiben – Querverweise bleiben gültig:
    /// Werte von /Producer, /Creator, /Author, /Title … im Info-Wörterbuch, die Werte im XMP-Paket, der Verweis /Metadata im Katalog und der Kommentar am Ende.</summary>
    static void BlankInfoStrings(string file)
    {
        var b = File.ReadAllBytes(file); var s = Encoding.Latin1.GetString(b); var changed = false;
        void Blank(int from, int len) { for (var i = from; i < from + len; i++) b[i] = (byte)' '; changed = true; }
        foreach (Match m in InfoString().Matches(s)) { var v = m.Groups[2]; Blank(v.Index + 1, v.Length - 2); } // Leerraum in Hex-Zeichenketten wird überlesen → leer
        foreach (Match m in XmpValue().Matches(s)) Blank(m.Groups[3].Index, m.Groups[3].Length);
        foreach (Match m in CatalogMetadata().Matches(s)) Blank(m.Index, m.Length);
        foreach (Match m in CreatedComment().Matches(s)) Blank(m.Index, m.Length);
        if (changed) File.WriteAllBytes(file, b);
    }

    [GeneratedRegex(@"/(Producer|Creator|Author|Title|Subject|Keywords)\s*(\((?:\\.|[^\\)])*\)|<[0-9A-Fa-f\s]*>)")]
    private static partial Regex InfoString();
    [GeneratedRegex(@"<(pdf:Producer|pdf:Keywords|xmp:CreatorTool|xmp:CreateDate|xmp:ModifyDate|xmp:MetadataDate|xmpMM:DocumentID|xmpMM:InstanceID|rdf:li)(\s[^>]*)?>([^<]+)</")]
    private static partial Regex XmpValue();
    [GeneratedRegex(@"(?<=/Type\s*/Catalog[^>]*?)/Metadata\s+\d+\s+\d+\s+R|/Metadata\s+\d+\s+\d+\s+R(?=[^>]*/Type\s*/Catalog)")]
    private static partial Regex CatalogMetadata();
    [GeneratedRegex(@"% Created with PDFsharp[^\r\n]*")]
    private static partial Regex CreatedComment();

    // ---------- Komprimieren ----------

    /// <summary>Komprimieren wie PDF24/Stirling: Bilder auf höchstens <see cref="CompressLevel.Dpi"/> (bezogen auf die größte Seite) verkleinern und
    /// neu kodieren – verlustfreie Bilder als Flate, mit <paramref name="jpeg"/> auch als JPEG; JPEG-Bilder werden mit <paramref name="jpegDecode"/>
    /// gelesen und kleiner neu kodiert. Ein Bild wird nur ersetzt, wenn es wirklich kleiner wird. Inhalte werden mit höchster Flate-Stufe gespeichert.
    /// Wird die Datei insgesamt nicht kleiner, bekommt das Ergebnis den Inhalt des Originals (ehrlich gemeldet).</summary>
    public static CompressReport Compress(string input, string output, CompressLevel level, JpegEncoder? jpeg = null, JpegDecoder? jpegDecode = null, AuditLog? audit = null)
    {
        Guard(output, input);
        var before = new FileInfo(input).Length; int images = 0, recoded = 0;
        File.Copy(input, output, true);
        try
        {
            using (var d = OpenModify(output))
            {
                var maxPage = d.Pages.Cast<PdfPage>().Select(p => Math.Max(p.Width.Point, p.Height.Point)).DefaultIfEmpty(842).Max();
                var maxPx = (int)Math.Ceiling(maxPage / 72.0 * level.Dpi);
                var all = d.Internals.GetAllObjects().OfType<PdfDictionary>().Where(o => o.Elements.GetName("/Subtype") == "/Image" && o.Stream is not null).ToList();
                var masks = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
                foreach (var o in all) if (o.Elements.GetObject("/SMask") is PdfDictionary sm) masks.Add(sm);
                foreach (var img in all)
                {
                    images++;
                    try { if (Recode(img, maxPx, level.Quality, masks.Contains(img) ? null : jpeg, jpegDecode)) recoded++; }
                    catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException or IndexOutOfRangeException) { /* Bild bleibt, wie es ist */ }
                }
                d.Options.CompressContentStreams = true;
                d.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                d.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;
                Save(d, output);
            }
            var after = new FileInfo(output).Length;
            if (after >= before)
            {
                File.Copy(input, output, true);
                audit?.Append("app", "pdf.compress", $"{Path.GetFileName(input)} nicht kleiner");
                return new(before, before, images, recoded, true, images == 0 ? "Die Datei enthält keine Bilder und ist schon gut gepackt – kleiner geht es nicht." : "Kleiner ging es nicht – das Ergebnis ist eine Kopie des Originals.");
            }
            audit?.Append("app", "pdf.compress", $"{Path.GetFileName(input)} {before} → {after} Bytes");
            return new(before, after, images, recoded, false, $"{recoded} von {images} Bildern verkleinert oder neu kodiert.");
        }
        catch { try { File.Delete(output); } catch (IOException) { } throw; }
    }

    static int Components(PdfDictionary img)
    {
        var cs = Item(img, "/ColorSpace");
        if (cs is PdfName n) return n.Value switch { "/DeviceRGB" => 3, "/DeviceGray" => 1, "/DeviceCMYK" => 4, _ => 0 };
        if (cs is PdfArray a && a.Elements.Count >= 2 && a.Elements.GetName(0) == "/ICCBased" && a.Elements.GetObject(1) is PdfDictionary icc) return icc.Elements.GetInteger("/N");
        return img.Elements.ContainsKey("/ColorSpace") ? 0 : 1; // ohne Farbraum: Maske (Grau)
    }

    static PdfItem? Item(PdfDictionary d, string key) { var i = d.Elements[key]; return i is PdfReference r ? r.Value : i; }

    static string Filter(PdfDictionary img) => Item(img, "/Filter") switch
    {
        PdfName n => n.Value,
        PdfArray { Elements.Count: 1 } a => a.Elements.GetName(0),
        PdfArray { Elements.Count: 0 } or null => "",
        _ => "?",
    };

    /// <summary>Ein Bild verkleinern und neu kodieren. true = ersetzt (kleiner geworden).</summary>
    static bool Recode(PdfDictionary img, int maxPx, int quality, JpegEncoder? jpeg, JpegDecoder? jpegDecode)
    {
        var e = img.Elements;
        if (e.GetBoolean("/ImageMask") || e.ContainsKey("/Decode") || e.GetInteger("/BitsPerComponent") != 8) return false;
        int w = e.GetInteger("/Width"), h = e.GetInteger("/Height"), c = Components(img);
        if (w <= 0 || h <= 0 || c is not (1 or 3)) return false;
        var filter = Filter(img); var raw = img.Stream.Value; byte[] px;
        if (filter == "/FlateDecode")
        {
            var parms = e.GetObject("/DecodeParms") as PdfDictionary ?? (e.GetObject("/DecodeParms") as PdfArray)?.Elements.GetObject(0) as PdfDictionary;
            if (parms is not null && parms.Elements.GetInteger("/Predictor") > 1) return false; // Vorhersage-Filter (PNG-Zeilen) lassen wir unberührt
            px = Inflate(raw);
            if (px.Length < w * h * c) return false;
        }
        else if (filter == "/DCTDecode" && jpegDecode?.Invoke(raw) is { } dec && dec.Channels == c && dec.Width == w && dec.Height == h) px = dec.Pixels;
        else return false;

        var s = Math.Min(1.0, (double)maxPx / Math.Max(w, h));
        int nw = Math.Max(1, (int)Math.Round(w * s)), nh = Math.Max(1, (int)Math.Round(h * s));
        var small = s < 1 ? Downsample(px, w, h, c, nw, nh) : px.Length == w * h * c ? px : px[..(w * h * c)];
        // Kandidaten: JPEG (wenn möglich) und Flate – der kleinere gewinnt, aber nur wenn kleiner als vorher
        byte[]? best = null; var bestFilter = "";
        if (jpeg?.Invoke(small, nw, nh, c, quality) is { Length: > 0 } j) { best = j; bestFilter = "/DCTDecode"; }
        if (filter == "/FlateDecode" || best is null)
        {
            var f = Deflate(small);
            if (best is null || f.Length < best.Length) { best = f; bestFilter = "/FlateDecode"; }
        }
        if (best is null || best.Length >= raw.Length && nw == w) return false;
        if (best.Length >= raw.Length) return false;
        img.Stream.Value = best;
        e["/Filter"] = new PdfName(bestFilter); e.Remove("/DecodeParms");
        e["/Width"] = new PdfInteger(nw); e["/Height"] = new PdfInteger(nh);
        e["/Length"] = new PdfInteger(best.Length);
        return true;
    }

    /// <summary>Flächenmittel (Box-Filter) – scharf genug für Dokumente, keine Treppen.</summary>
    static byte[] Downsample(byte[] src, int w, int h, int c, int nw, int nh)
    {
        var dst = new byte[nw * nh * c];
        for (var y = 0; y < nh; y++)
        {
            int y0 = y * h / nh, y1 = Math.Max(y0 + 1, (y + 1) * h / nh);
            for (var x = 0; x < nw; x++)
            {
                int x0 = x * w / nw, x1 = Math.Max(x0 + 1, (x + 1) * w / nw);
                for (var k = 0; k < c; k++)
                {
                    long sum = 0;
                    for (var yy = y0; yy < y1; yy++) for (var xx = x0; xx < x1; xx++) sum += src[(yy * w + xx) * c + k];
                    dst[(y * nw + x) * c + k] = (byte)(sum / ((y1 - y0) * (x1 - x0)));
                }
            }
        }
        return dst;
    }

    static byte[] Inflate(byte[] data)
    {
        using var ms = new MemoryStream(data); using var z = new ZLibStream(ms, CompressionMode.Decompress); using var o = new MemoryStream();
        z.CopyTo(o); return o.ToArray();
    }

    static byte[] Deflate(byte[] data)
    {
        using var o = new MemoryStream();
        using (var z = new ZLibStream(o, CompressionLevel.SmallestSize, true)) z.Write(data);
        return o.ToArray();
    }

    // ---------- Schwärzen ----------

    /// <summary>Schwärzen, bei dem der Inhalt wirklich verschwindet: Jede Seite mit Balken wird als Bild (<paramref name="dpi"/>) gezeichnet, die Balken
    /// werden in das Bild gebrannt, und die Seite wird durch dieses Bild ersetzt – Text, Bilder und Anmerkungen darunter sind danach nicht mehr in der Datei.
    /// Seiten ohne Balken bleiben unverändert (Text bleibt dort suchbar). <paramref name="render"/> liefert das PNG einer Seite (0-basiert, dpi).
    /// Gibt die Zahl der geschwärzten Seiten zurück.</summary>
    public static int Redact(string input, string output, IReadOnlyList<RedactBox> boxes, Func<int, int, byte[]> render, int dpi = 150, string? password = null, AuditLog? audit = null)
    {
        if (boxes.Count == 0) throw new ArgumentException("Bitte zuerst Bereiche zum Schwärzen markieren.");
        if (boxes.Any(b => b.W <= 0 || b.H <= 0)) throw new ArgumentException("Ein Balken hat keine Fläche.");
        Guard(output, input);
        using var src = OpenImport(input, password);
        if (boxes.Any(b => b.Page < 0 || b.Page >= src.PageCount)) throw new ArgumentException("Ein Balken liegt auf einer Seite, die es nicht gibt.");
        using var target = new PdfDocument();
        var pages = boxes.Select(b => b.Page).ToHashSet(); var tmp = new List<string>();
        try
        {
            for (var i = 0; i < src.PageCount; i++)
            {
                if (!pages.Contains(i)) { target.AddPage(src.Pages[i]); continue; }
                var sp = src.Pages[i];
                var rot = ((sp.Rotate % 360) + 360) % 360;
                double pw = rot is 90 or 270 ? sp.Height.Point : sp.Width.Point, ph = rot is 90 or 270 ? sp.Width.Point : sp.Height.Point;
                var px = PngCodec.Decode(render(i, dpi));
                var sx = px.W / pw; var sy = px.H / ph;
                foreach (var b in boxes.Where(b => b.Page == i))
                {
                    int x0 = Math.Clamp((int)Math.Floor(b.X * sx), 0, px.W), x1 = Math.Clamp((int)Math.Ceiling((b.X + b.W) * sx), 0, px.W);
                    int y0 = Math.Clamp((int)Math.Floor(b.YTop * sy), 0, px.H), y1 = Math.Clamp((int)Math.Ceiling((b.YTop + b.H) * sy), 0, px.H);
                    for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) { var k = (y * px.W + x) * 4; px.Px[k] = 0; px.Px[k + 1] = 0; px.Px[k + 2] = 0; px.Px[k + 3] = 255; }
                }
                // Hintergrund weiß, damit durchsichtige Stellen nicht schwarz werden
                for (var k = 0; k < px.Px.Length; k += 4) if (px.Px[k + 3] < 255) { var a = px.Px[k + 3] / 255.0; px.Px[k] = (byte)(px.Px[k] * a + 255 * (1 - a)); px.Px[k + 1] = (byte)(px.Px[k + 1] * a + 255 * (1 - a)); px.Px[k + 2] = (byte)(px.Px[k + 2] * a + 255 * (1 - a)); px.Px[k + 3] = 255; }
                var f = Path.Combine(Path.GetTempPath(), $"trendo-schwaerzen-{Guid.NewGuid():N}.png"); tmp.Add(f);
                File.WriteAllBytes(f, PngCodec.Encode(px, fast: false));
                var np = target.AddPage(); np.Width = XUnit.FromPoint(pw); np.Height = XUnit.FromPoint(ph);
                using var g = XGraphics.FromPdfPage(np);
                using var img = XImage.FromFile(f);
                g.DrawImage(img, 0, 0, pw, ph);
            }
            Save(target, output);
        }
        finally { foreach (var f in tmp) try { File.Delete(f); } catch (IOException) { } }
        audit?.Append("app", "pdf.redact", $"{pages.Count} Seiten {Path.GetFileName(output)}");
        return pages.Count;
    }

    // ---------- Vergleichen ----------

    static string Norm(string line) => Regex.Replace(line, @"\s+", " ").Trim();

    /// <summary>Zwei PDFs Zeile für Zeile vergleichen (Text je Seite). Leerzeilen und unterschiedliche Leerzeichen zählen nicht.
    /// Ergebnis mit Seitennummer in A bzw. B – wie die Vergleichsansicht in Acrobat, aber als Text.</summary>
    public static PdfCompare Compare(IReadOnlyList<string> pagesA, IReadOnlyList<string> pagesB)
    {
        static List<(int Page, string Line)> Lines(IReadOnlyList<string> pages) =>
            [.. pages.SelectMany((t, i) => t.Replace("\r", "").Split('\n').Select(Norm).Where(l => l.Length > 0).Select(l => (i + 1, l)))];
        var a = Lines(pagesA); var b = Lines(pagesB);
        var diff = Writing.Diff(string.Join('\n', a.Select(x => x.Line)), string.Join('\n', b.Select(x => x.Line)));
        var res = new List<PdfDiffLine>(); int ia = 0, ib = 0, add = 0, rem = 0, same = 0;
        foreach (var (op, line) in diff)
        {
            if (a.Count == 0 && op != '+' || b.Count == 0 && op != '−') continue; // leere Seite liefert eine leere Zeile
            int pa = ia < a.Count ? a[ia].Page : a.Count > 0 ? a[^1].Page : 0, pb = ib < b.Count ? b[ib].Page : b.Count > 0 ? b[^1].Page : 0;
            switch (op)
            {
                case ' ': res.Add(new(' ', pa, pb, line)); ia++; ib++; same++; break;
                case '+': res.Add(new('+', pa, pb, line)); ib++; add++; break;
                default: res.Add(new('−', pa, pb, line)); ia++; rem++; break;
            }
        }
        return new(res, add, rem, same);
    }

    /// <summary>Vergleich als Text (zum Speichern): „+ S.2: …“ neu in B, „− S.1: …“ nur in A.</summary>
    public static string CompareText(PdfCompare c, string nameA, string nameB)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Vergleich: A = {nameA}, B = {nameB}\n{c.Removed} Zeilen nur in A, {c.Added} Zeilen nur in B, {c.Same} gleich.\n\n");
        foreach (var l in c.Lines.Where(l => l.Op != ' '))
            sb.Append(l.Op == '+' ? $"+ B S.{l.PageB}: " : $"− A S.{l.PageA}: ").Append(l.Text).Append('\n');
        if (c.Identical) sb.Append("Der Text beider Dateien ist gleich.\n");
        return sb.ToString();
    }
}
