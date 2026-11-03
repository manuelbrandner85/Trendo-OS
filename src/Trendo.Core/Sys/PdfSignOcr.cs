using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Sys;

/// <summary>F3 PDF (Vorbild-Analyse „Nächste Schritte“): Unterschrift als Bild fest auf eine Seite setzen und Scans durchsuchbar machen (Tesseract).</summary>
public static partial class PdfTools
{
    /// <summary>Bild (z. B. die gezeichnete Unterschrift) fest auf die Seite zeichnen. Lage in PDF-Punkten: <paramref name="x"/> von links,
    /// <paramref name="yTop"/> von oben, Breite <paramref name="width"/>; die Höhe folgt dem Seitenverhältnis des Bilds. Gibt die Höhe zurück.</summary>
    public static double AddImage(string input, string output, int pageIndex, string image, double x, double yTop, double width, AuditLog? audit = null)
    {
        if (!File.Exists(image)) throw new ArgumentException("Das Bild der Unterschrift fehlt.");
        var (iw, ih) = XrPlus.ImageSize(File.ReadAllBytes(image));
        if (iw <= 0 || ih <= 0) throw new ArgumentException("Bitte ein PNG- oder JPG-Bild.");
        var height = width * ih / iw;
        EditCopy(input, output, d =>
        {
            if (pageIndex < 0 || pageIndex >= d.PageCount) throw new ArgumentException("Diese Seite gibt es nicht.");
            var page = d.Pages[pageIndex];
            using var g = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            using var img = XImage.FromFile(image);
            g.DrawImage(img, x, yTop, width, height);
        });
        audit?.Append("app", "pdf.sign", $"S.{pageIndex + 1} {Path.GetFileName(output)}");
        return height;
    }

    /// <summary>Durchsuchbare PDF aus Seitenbildern: Tesseract legt je Seite eine unsichtbare Textebene unter das Bild, danach werden die Seiten
    /// zusammengefügt. <paramref name="dpi"/> muss zur Auflösung der Bilder passen, damit die Seitengröße stimmt. Gibt (Seiten, Fehlermeldung) zurück.</summary>
    public static async Task<(int Pages, string? Error)> OcrSearchableAsync(IProcessRunner run, string tesseract, IReadOnlyList<string> pageImages, string output,
        string? tessdata = null, string lang = "deu", int dpi = 300, Action<int>? progress = null, AuditLog? audit = null, CancellationToken ct = default)
    {
        if (pageImages.Count == 0) return (0, "Keine Seiten.");
        var work = Path.Combine(Path.GetTempPath(), "trendo-ocr-pdf-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        try
        {
            var parts = new List<string>();
            for (var i = 0; i < pageImages.Count; i++)
            {
                progress?.Invoke(i);
                var outBase = Path.Combine(work, $"s{i:0000}");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(180));
                var (exit, _) = await run.RunAsync(tesseract, (tessdata is null ? "" : $"--tessdata-dir \"{tessdata}\" ") + $"\"{pageImages[i]}\" \"{outBase}\" -l {lang} --dpi {dpi} pdf", null, cts.Token);
                if (exit != 0 || !File.Exists(outBase + ".pdf")) return (0, $"Texterkennung auf Seite {i + 1} fehlgeschlagen (Exit {exit}). Ist das Sprachpaket „{lang}“ da?");
                parts.Add(outBase + ".pdf");
            }
            int n;
            if (parts.Count == 1) { Guard(output, pageImages[0]); File.Copy(parts[0], output, true); n = 1; } // eine Seite: nichts zusammenzufügen
            else n = Merge(parts, output);
            audit?.Append("app", "pdf.ocr", $"{n} Seiten {Path.GetFileName(output)}");
            return (n, null);
        }
        catch (OperationCanceledException) { return (0, "Die Texterkennung hat zu lange gebraucht."); }
        finally { try { Directory.Delete(work, true); } catch (IOException) { } }
    }
}
