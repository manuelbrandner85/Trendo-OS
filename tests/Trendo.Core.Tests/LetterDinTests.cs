using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0310: Brief nach DIN 5008 – Informationsblock, Empfänger und Anrede aus dem Adressbuch, Serienbrief an Kontakte, Form A im DOCX.
public class LetterDinTests
{
    static readonly Contact Erika = new("e", "Erika Muster", "erika@example.de", Street: "Hauptstraße 1", City: "10115 Berlin", Company: "Muster GmbH");
    static readonly Contact Firma = new("f", "Beispiel AG", "", Street: "Ring 5", City: "80331 München", Company: "Beispiel AG");
    static readonly Contact Ohne = new("o", "Max Ohne", "max@example.de");

    [Fact(DisplayName = "T-0310: Informationsblock – nur gefüllte Zeilen, Ort und Datum zuletzt")]
    public void Info()
    {
        var d = new LetterDraft { Place = "Berlin", Date = new DateTime(2026, 11, 2), Reference = "K-123", OurReference = "em/26" };
        Assert.Equal(["Ihr Zeichen: K-123", "Unser Zeichen: em/26", "Berlin, 2. November 2026"], Letters.InfoBlock(d));
        d.YourMessage = "28.10.2026"; Assert.Equal("Ihre Nachricht vom: 28.10.2026", Letters.InfoBlock(d)[1]);
    }

    [Fact(DisplayName = "T-0310: Anschrift und Anrede aus Kontakten (Person/Firma); Serienbrief ersetzt Felder, überspringt Kontakte ohne Anschrift")]
    public void From_contacts()
    {
        Assert.Equal("Muster GmbH\nErika Muster\nHauptstraße 1\n10115 Berlin", Letters.RecipientOf(Erika));
        Assert.Equal("Guten Tag Erika Muster,", Letters.SalutationFor(Erika));
        Assert.Equal("Beispiel AG\nRing 5\n80331 München", Letters.RecipientOf(Firma)); Assert.Equal("Sehr geehrte Damen und Herren,", Letters.SalutationFor(Firma));
        var tpl = new LetterDraft { Subject = "Einladung für {Vorname}", Body = "wir freuen uns auf dich in {Ort}, liebe/r {Nachname}." };
        var skipped = new List<string>();
        var list = Letters.SerialFromContacts(tpl, [Erika, Firma, Ohne], skipped);
        Assert.Equal(2, list.Count); Assert.Equal(["Max Ohne"], skipped);
        Assert.Equal(("Einladung für Erika", "Guten Tag Erika Muster,"), (list[0].Subject, list[0].Salutation));
        Assert.Equal("wir freuen uns auf dich in Berlin, liebe/r Muster.", list[0].Body);
        Assert.Equal("Sehr geehrte Damen und Herren,", list[1].Salutation); Assert.StartsWith("Beispiel AG", list[1].Recipient);
        Assert.Equal("Einladung für {Vorname}", tpl.Subject); // Vorlage bleibt
        tpl.Salutation = "Hallo {Vorname},"; Assert.Equal("Hallo Erika,", Letters.SerialFromContacts(tpl, [Erika])[0].Salutation);
    }

    [Fact(DisplayName = "T-0310: DOCX – Form A setzt das Anschriftfeld höher, Informationsblock steht im Dokument")]
    public void Docx_form()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-brief-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var d = new LetterDraft { SenderName = "Erika Muster", SenderStreet = "Hauptstraße 1", SenderCity = "10115 Berlin", Recipient = "Beispiel AG\nRing 5\n80331 München", Subject = "Test", Body = "Hallo.", OurReference = "em/26", YourMessage = "28.10.2026" };
            var b = Path.Combine(dir, "b.docx"); Letters.WriteDocx(d, b); d.Form = "A"; var a = Path.Combine(dir, "a.docx"); Letters.WriteDocx(d, a);
            string Xml(string f) { using var z = System.IO.Compression.ZipFile.OpenRead(f); using var r = new StreamReader(z.GetEntry("word/document.xml")!.Open()); return r.ReadToEnd(); }
            Assert.Contains("w:before=\"1134\"", Xml(b)); Assert.Contains("w:before=\"397\"", Xml(a));
            var text = Letters.ReadDocxText(a); Assert.Contains("Unser Zeichen: em/26", text); Assert.Contains("Ihre Nachricht vom: 28.10.2026", text);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
