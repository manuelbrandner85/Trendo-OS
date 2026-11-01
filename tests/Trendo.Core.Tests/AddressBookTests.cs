using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0307: Kontakte als Adressbuch – Felder, Gruppen, Suche, Doppelte, Zusammenführen, Geburtstage, vCard mit allen Feldern und Foto.
public class AddressBookTests
{
    static MailCenter M(Fixture f) => new(f.Core.DataRoot, f.Core.Audit);

    [Fact(DisplayName = "T-0307: Ändern mit Gruppen (ohne Doppelte/Leere), Suche über alle Felder und Ziffern, Gruppenliste; alte Kontakte ohne neue Felder bleiben lesbar")]
    public void Update_search_groups()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Core.DataRoot, "kontakte.json"), """[{"Id":"alt1","Name":"Anna Alt","Email":"anna@example.de","Phone":"","Note":""}]""");
        var m = M(f);
        Assert.Equal("Anna Alt", m.Contacts().Single().Name); Assert.Null(m.Contacts().Single().Groups);
        var e = m.UpdateContact(new Contact("e1", " Erika Muster ", "erika@example.de", Mobile: "+49 171 1234567", Company: "Muster GmbH", City: "10115 Berlin", Groups: ["Familie", " familie ", "", "Verein"]));
        Assert.Equal("Erika Muster", e.Name); Assert.Equal(["Familie", "Verein"], e.Groups);
        Assert.Throws<ArgumentException>(() => m.UpdateContact(e with { Email = "kaputt" }));
        Assert.Throws<ArgumentException>(() => m.UpdateContact(e with { Name = " " }));
        Assert.Single(m.Search("berlin")); Assert.Single(m.Search("1234567")); Assert.Single(m.Search("GmbH")); Assert.Single(m.Search("", "verein")); Assert.Equal(2, m.Search("").Count);
        Assert.Equal([("Familie", 1), ("Verein", 1)], m.GroupNames());
        Assert.Equal("Muster GmbH", M(f).Get("e1").Company); // gespeichert
    }

    [Fact(DisplayName = "T-0307: Doppelte nach Name (auch umgedreht), E-Mail oder Nummer (+49/0); Zusammenführen füllt Lücken, vereint Gruppen und Notizen")]
    public void Duplicates_merge()
    {
        using var f = new Fixture(); var m = M(f);
        m.UpdateContact(new Contact("a", "Erika Muster", "erika@example.de", Groups: ["Familie"], Note: "Schwester"));
        m.UpdateContact(new Contact("b", "Muster, Erika", "", Mobile: "0171 1234567", Groups: ["Verein"]));
        m.UpdateContact(new Contact("c", "E. M.", "", Phone: "+49 (171) 123-4567", Birthday: new DateOnly(1990, 5, 3), Note: "Handy alt"));
        m.UpdateContact(new Contact("d", "Max Beispiel", "max@example.de"));
        var d = Assert.Single(m.Duplicates()); Assert.Equal(["a", "b", "c"], d.Select(c => c.Id).Order());
        var merged = m.Merge("a", ["b", "c"]);
        Assert.Equal(("erika@example.de", "0171 1234567", new DateOnly(1990, 5, 3)), (merged.Email, merged.Mobile, merged.Birthday!.Value));
        Assert.Equal(["Familie", "Verein"], merged.Groups); Assert.Equal("Schwester\nHandy alt", merged.Note);
        Assert.Equal(2, m.Contacts().Count); Assert.Empty(m.Duplicates());
    }

    [Fact(DisplayName = "T-0307: Geburtstage – nächster Termin mit Alter, über den Jahreswechsel, 29. Februar")]
    public void Birthdays()
    {
        using var f = new Fixture(); var m = M(f); var today = new DateOnly(2026, 12, 20);
        m.UpdateContact(new Contact("a", "Anna", "", Birthday: new DateOnly(1980, 12, 24)));
        m.UpdateContact(new Contact("b", "Bernd", "", Birthday: new DateOnly(2000, 1, 5)));
        m.UpdateContact(new Contact("c", "Clara", "", Birthday: new DateOnly(1996, 2, 29)));
        m.UpdateContact(new Contact("d", "Dora", "", Birthday: new DateOnly(1970, 12, 1)));
        var b = m.Birthdays(today, 30);
        Assert.Equal([("Anna", new DateOnly(2026, 12, 24), 46), ("Bernd", new DateOnly(2027, 1, 5), 27)], b.Select(x => (x.Contact.Name, x.Next, x.Age)));
        Assert.Equal(new DateOnly(2027, 2, 28), MailCenter.NextBirthday(new DateOnly(1996, 2, 29), today));
        Assert.Equal(new DateOnly(2028, 2, 29), MailCenter.NextBirthday(new DateOnly(1996, 2, 29), new DateOnly(2027, 3, 1)));
    }

    [Fact(DisplayName = "T-0307: vCard – alle Felder und Foto hinaus und wieder hinein; fremde vCard 4.0 mit Gruppenpräfix, CELL, ADR, BDAY ohne Bindestriche, CATEGORIES")]
    public void Vcard_roundtrip()
    {
        using var f = new Fixture(); var m = M(f);
        var e = m.UpdateContact(new Contact("e1", "Erika Muster", "erika@example.de", "030 123", "Mag Kaffee; schwarz", "0171 555", "Muster GmbH", "Hauptstraße 1", "10115 Berlin", new DateOnly(1985, 7, 14), ["Familie", "Verein"], "", true, "https://example.de"));
        m.SetPhoto("e1", [0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9], ".jpg");
        var vcf = m.ExportVcf();
        Assert.Contains("N:Muster;Erika;;;", vcf); Assert.Contains("PHOTO;ENCODING=b;TYPE=JPEG:", vcf); Assert.Contains("NOTE:Mag Kaffee\\; schwarz", vcf);
        using var g = new Fixture(); var m2 = new MailCenter(g.Core.DataRoot, g.Core.Audit);
        Assert.Equal(1, m2.ImportVcf(vcf)); Assert.Equal(0, m2.ImportVcf(vcf));
        var back = m2.Contacts().Single();
        Assert.Equal((e.Name, e.Email, e.Phone, e.Note, e.Mobile, e.Company, e.Street, e.City, e.Birthday, e.Website), (back.Name, back.Email, back.Phone, back.Note, back.Mobile, back.Company, back.Street, back.City, back.Birthday, back.Website));
        Assert.Equal(["Familie", "Verein"], back.Groups); Assert.Equal([0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9], File.ReadAllBytes(back.Photo));
        Assert.Equal(1, m2.ImportVcf("BEGIN:VCARD\nVERSION:4.0\nFN:Max Beispiel\nitem1.TEL;TYPE=cell:+49 160 1\nTEL;TYPE=work,voice:089 2\nADR;TYPE=work:;;Ring 5;80331 München;;;\nBDAY:19750102\nCATEGORIES:Arbeit\nORG:Beispiel AG;Vertrieb\nEND:VCARD\n"));
        var max = m2.Contacts().Single(c => c.Name == "Max Beispiel");
        Assert.Equal(("+49 160 1", "089 2", "Ring 5", "80331 München", new DateOnly(1975, 1, 2), "Beispiel AG"), (max.Mobile, max.Phone, max.Street, max.City, max.Birthday!.Value, max.Company));
        Assert.Equal(["Arbeit"], max.Groups);
    }
}
