using Trendo.Core.Office;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Selbst nachgebaut statt LibreOffice (04.11.2026): Word, Excel, PowerPoint und OpenDocument als PDF, Word als ODT – ohne Zusatzprogramm.</summary>
public class OfficeConvertTests
{
    [Fact(DisplayName = "Office ohne LibreOffice: DOCX, ODT, XLSX, CSV, PPTX werden zu PDF; DOCX zu ODT; alte Formate werden ehrlich abgelehnt")]
    public void Convert_all()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-office-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        try
        {
            var txt = Path.Combine(d, "a.txt"); File.WriteAllText(txt, "Sehr geehrte Damen und Herren,\n\nhiermit kündige ich fristgerecht.\n\nMit freundlichen Grüßen\nErika Muster");
            var doc = OdtFormat.ReadTxt(txt);
            var docx = Path.Combine(d, "brief.docx"); DocxFormat.Write(doc, docx);
            var pdf = OfficeConvert.ToPdf(docx);
            Assert.True(Pdf.Pages(pdf) >= 1);
            var odt = OfficeConvert.DocxToOdt(docx);
            Assert.Contains("kündige", OdtFormat.Read(odt).PlainText());
            Assert.True(Pdf.Pages(OfficeConvert.ToPdf(odt, Path.Combine(d, "odt.pdf"))) >= 1);

            var csv = Path.Combine(d, "t.csv"); File.WriteAllText(csv, "Monat;Betrag\nJanuar;120\nFebruar;80");
            var xlsx = Path.Combine(d, "t.xlsx"); SheetFiles.WriteXlsx(SheetFiles.ReadCsv(csv), xlsx);
            Assert.True(Pdf.Pages(OfficeConvert.ToPdf(xlsx)) >= 1);
            Assert.True(Pdf.Pages(OfficeConvert.ToPdf(csv, Path.Combine(d, "csv.pdf"))) >= 1);

            var pptx = Path.Combine(d, "f.pptx"); SlideFiles.WritePptx(new SlideDoc(), pptx);
            Assert.True(Pdf.Pages(OfficeConvert.ToPdf(pptx)) >= 1);

            Assert.Contains("Alte Office-Formate", Assert.Throws<NotSupportedException>(() => OfficeConvert.ToPdf(Path.Combine(d, "alt.doc"))).Message);
            Assert.False(OfficeConvert.CanConvert("x.doc")); Assert.True(OfficeConvert.CanConvert("x.DOCX"));
        }
        finally { Directory.Delete(d, true); }
    }
}
