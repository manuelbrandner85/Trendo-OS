namespace Trendo.Core.Office;

/// <summary>Selbst nachgebaut (04.11.2026, statt LibreOffice): Office-Dateien mit Trendos eigenen Lesern in PDF oder ODT umwandeln.
/// Word (.docx), ODT, Text → Trendo Text; Excel (.xlsx), ODS, CSV → Trendo Tabelle; PowerPoint (.pptx), ODP → Trendo Präsentation.
/// Ehrlich: Layout wird nachgebaut, nicht pixelgenau wie in Word; alte Binärformate (.doc/.xls/.ppt) kann Trendo nicht lesen.</summary>
public static class OfficeConvert
{
    public static readonly string[] Supported = [".docx", ".odt", ".txt", ".md", ".xlsx", ".ods", ".csv", ".pptx", ".odp"];
    public static bool CanConvert(string file) => Supported.Contains(Path.GetExtension(file).ToLowerInvariant());

    /// <summary>Datei als PDF neben das Original (oder nach <paramref name="output"/>); gibt den PDF-Pfad zurück.</summary>
    public static string ToPdf(string file, string? output = null)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        output ??= Path.ChangeExtension(file, ".pdf");
        switch (ext)
        {
            case ".docx": TextPdf.Export(DocxFormat.Read(file), output); break;
            case ".odt": TextPdf.Export(OdtFormat.Read(file), output); break;
            case ".txt" or ".md": TextPdf.Export(OdtFormat.ReadTxt(file), output); break;
            case ".xlsx" or ".ods" or ".csv":
            {
                var d = ext switch { ".xlsx" => SheetFiles.ReadXlsx(file), ".ods" => SheetFiles.ReadOds(file), _ => SheetFiles.ReadCsv(file) };
                SheetPdf.Export(d, d.Sheets[0], output); break;
            }
            case ".pptx": SlidePdf.Export(SlideFiles.ReadPptx(file), output); break;
            case ".odp": SlidePdf.Export(SlideFiles.ReadOdp(file), output); break;
            default: throw new NotSupportedException(ext is ".doc" or ".xls" or ".ppt" ? "Alte Office-Formate (.doc/.xls/.ppt) kann Trendo nicht lesen – bitte als .docx/.xlsx/.pptx speichern." : "Dieses Format kann Trendo nicht umwandeln.");
        }
        return output;
    }

    /// <summary>Word-Datei als ODT (OpenDocument) – z. B. für Ämter, die ODT verlangen.</summary>
    public static string DocxToOdt(string docx, string? output = null)
    {
        output ??= Path.ChangeExtension(docx, ".odt");
        OdtFormat.Write(DocxFormat.Read(docx), output);
        return output;
    }
}
