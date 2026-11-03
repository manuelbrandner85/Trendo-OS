using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Wunsch 03.11.2026: „Als PDF“ ohne LibreOffice, Senden mit PDF-Anhang, viel mehr Designs und Farben wie bei Canva.</summary>
public class LetterDesignPdfTests
{
    static LetterDraft Draft() => new()
    {
        SenderName = "Erika Muster", SenderStreet = "Musterweg 1", SenderCity = "12345 Musterstadt", SenderContact = "erika@example.org",
        Recipient = "Beispiel GmbH\nHauptstraße 2\n54321 Beispielort", Subject = "Bewerbung", Tagline = "Kauffrau", Body = string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"Absatz {i} mit etwas Text, damit der Brief auf eine zweite Seite läuft.")),
    };

    [Fact(DisplayName = "Brief-PDF ohne LibreOffice: jede der 20 Vorlagen ergibt ein gültiges PDF (Folgeseite bei langem Text), Vorlagen nutzen nur erlaubte Werte")]
    public void Every_design_to_pdf()
    {
        Assert.True(LetterDesigns.Styles.Length >= 20); Assert.True(LetterDesigns.Palette.Length >= 24);
        Assert.Equal(LetterDesigns.Styles.Length, LetterDesigns.Styles.Select(s => s.Id).Distinct().Count());
        var dir = Path.Combine(Path.GetTempPath(), "trendo-letterpdf-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            foreach (var st in LetterDesigns.Styles)
            {
                Assert.Contains(st.Font, LetterDesigns.Fonts); Assert.Contains(st.Header, LetterDesigns.Headers);
                var d = Draft(); LetterDesigns.Apply(d, st.Id);
                Assert.Equal((st.Accent, st.Accent2), (d.Accent, d.Accent2));
                var f = Path.Combine(dir, st.Id + ".pdf");
                var pages = LetterPdf.Write(d, f);
                Assert.True(pages >= 2, st.Id);
                Assert.StartsWith("%PDF", Encoding.ASCII.GetString(File.ReadAllBytes(f), 0, 4));
                Assert.Equal(pages, Sys.PdfTools.Read(f).Pages);
            }
            var plain = Draft(); plain.Body = "kurz"; Assert.Equal(1, LetterPdf.Write(plain, Path.Combine(dir, "kurz.pdf")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Brief-Word-Datei: Fußband als Fußzeile, Rahmen als Seitenrand, zweifarbig mit Streifen in der zweiten Farbe")]
    public void Docx_layouts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-letterdocx-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string Xml(string style, string part)
            {
                var d = Draft(); LetterDesigns.Apply(d, style); var f = Path.Combine(dir, style + ".docx"); Letters.WriteDocx(d, f);
                using var z = System.IO.Compression.ZipFile.OpenRead(f); var e = z.GetEntry(part);
                return e is null ? "" : new StreamReader(e.Open()).ReadToEnd();
            }
            Assert.Contains("w:fill=\"5B3CC4\"", Xml("violett", "word/footer1.xml"));
            Assert.Contains("<w:pgBorders", Xml("rahmen", "word/document.xml"));
            Assert.Contains("w:color=\"C9A227\"", Xml("navy-gold", "word/document.xml"));
            Assert.Contains("<w:jc w:val=\"center\"/>", Xml("frisch", "word/document.xml"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "E-Mail-Entwurf mit Anhang: multipart/mixed, PDF als base64, Entwurf (X-Unsent) – ohne Anhang bleibt es einfacher Text")]
    public void Mail_with_attachment()
    {
        var f = Path.Combine(Path.GetTempPath(), "trendo-anhang-" + Guid.NewGuid().ToString("N") + ".pdf"); File.WriteAllBytes(f, Encoding.ASCII.GetBytes("%PDF-1.4 Test"));
        try
        {
            var eml = MailCenter.ToEml(new MailDraft("a@example.org", "Brief", "Hallo", "", [f]), DateTimeOffset.Now);
            Assert.StartsWith("X-Unsent: 1", eml); Assert.Contains("multipart/mixed", eml);
            Assert.Contains("Content-Type: application/pdf; name=\"" + Path.GetFileName(f) + "\"", eml);
            Assert.Contains(Convert.ToBase64String(File.ReadAllBytes(f)), eml);
            Assert.DoesNotContain("multipart", MailCenter.ToEml(new MailDraft("a@example.org", "x", "y"), DateTimeOffset.Now));
        }
        finally { File.Delete(f); }
    }
}
