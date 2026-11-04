using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Adressbuch – Vorbild-Funktionen (Google Kontakte, Outlook; 04.11.2026): CSV hinein und hinaus, Nummern vereinheitlichen, Adressetiketten als PDF.</summary>
public class AddressBookPlusTests
{
    static MailCenter M(Fixture f) => new(f.Core.DataRoot, f.Core.Audit);

    [Fact(DisplayName = "CSV: eigene Datei hin und zurück mit Anführungszeichen, Gruppen und Geburtstag; Doppelte werden übersprungen")]
    public void Csv_roundtrip()
    {
        using var f = new Fixture(); var m = M(f);
        m.UpdateContact(new Contact("a", "Erika Muster", "erika@example.de", Phone: "030 1234567", Note: "Sagt \"Hallo\", gern", Company: "Muster GmbH", Street: "Hauptstraße 1", City: "10115 Berlin", Birthday: new DateOnly(1990, 5, 17), Groups: ["Familie", "Verein"]));
        var csv = m.ExportCsv();
        Assert.StartsWith("Name,Vorname,Nachname,E-Mail", csv); Assert.Contains("\"Sagt \"\"Hallo\"\", gern\"", csv); Assert.Contains("Familie ::: Verein", csv);
        using var g = new Fixture(); var other = M(g);
        Assert.Equal(1, other.ImportCsv(csv)); Assert.Equal(0, other.ImportCsv(csv));
        var c = other.Contacts().Single();
        Assert.Equal(("Erika Muster", "Sagt \"Hallo\", gern", "10115 Berlin", new DateOnly(1990, 5, 17)), (c.Name, c.Note, c.City, c.Birthday!.Value));
        Assert.Equal(["Familie", "Verein"], c.Groups);
    }

    [Fact(DisplayName = "CSV aus Google Kontakte (Komma) und deutschem Outlook (Semikolon, Vor-/Nachname) wird erkannt; ungültige E-Mail fällt weg; leere Datei meldet sich")]
    public void Csv_foreign()
    {
        using var f = new Fixture(); var m = M(f);
        var google = "First Name,Last Name,E-mail 1 - Value,Phone 1 - Value,Organization 1 - Name,Group Membership\r\nErika,Muster,erika@example.de,+49 171 1234567,Muster GmbH,* myContacts ::: Kunden\r\n";
        Assert.Equal(1, m.ImportCsv(google));
        var e = m.Contacts().Single(); Assert.Equal(("Erika Muster", "erika@example.de", "+49 171 1234567", "Muster GmbH"), (e.Name, e.Email, e.Phone, e.Company)); Assert.Equal(["Kunden"], e.Groups);
        var outlook = "﻿Vorname;Nachname;E-Mail-Adresse;Mobiltelefon;Straße privat;Ort privat;Geburtstag\r\nMax;Beispiel;kaputt;0160 999888;Weg 2;20095 Hamburg;17.05.1985\r\n";
        Assert.Equal(1, m.ImportCsv(outlook));
        var x = m.Contacts().Single(c => c.Name == "Max Beispiel");
        Assert.Equal(("", "0160 999888", "Weg 2", "20095 Hamburg", new DateOnly(1985, 5, 17)), (x.Email, x.Mobile, x.Street, x.City, x.Birthday!.Value));
        Assert.Throws<InvalidOperationException>(() => m.ImportCsv("Name\r\n"));
    }

    [Fact(DisplayName = "Nummern vereinheitlichen: 0 → +49, 00 → +, Vorwahl bleibt getrennt; ohne Vorwahl und zu kurz bleibt alles; FixPhones zählt Änderungen")]
    public void Phones()
    {
        Assert.Equal("+49 171 1234567", MailCenter.NormalizePhone("0171 1234567"));
        Assert.Equal("+49 30 1234567", MailCenter.NormalizePhone("030/123 45 67"));
        Assert.Equal("+49 1711234567", MailCenter.NormalizePhone("01711234567"));
        Assert.Equal("+43 1 5551234", MailCenter.NormalizePhone("0043 1 555 1234"));
        Assert.Equal("+49 30 1234567", MailCenter.NormalizePhone("+49 (0)30 1234567".Replace("(0)", "")));
        Assert.Equal("1234567", MailCenter.NormalizePhone("1234567")); Assert.Equal("0171", MailCenter.NormalizePhone("0171")); Assert.Equal("", MailCenter.NormalizePhone(" "));
        using var f = new Fixture(); var m = M(f);
        m.UpdateContact(new Contact("a", "Erika Muster", "", Phone: "030 1234567", Mobile: "+49 171 1234567")); m.UpdateContact(new Contact("b", "Max Beispiel", "", Phone: "+49 40 555"));
        Assert.Equal(1, m.FixPhones()); Assert.Equal("+49 30 1234567", m.Get("a").Phone); Assert.Equal(0, m.FixPhones());
    }

    [Fact(DisplayName = "Etiketten: 24 je Bogen, angebrochener Bogen ab Etikett 20, ohne Anschrift übersprungen; Anschrift aus gefüllten Zeilen; nichts zu drucken meldet sich")]
    public void Labels()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-etiketten-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var list = Enumerable.Range(1, 30).Select(i => new Contact($"c{i}", $"Erika Muster {i}", "", Street: $"Weg {i}", City: "10115 Berlin")).Append(new Contact("x", "Ohne Anschrift", "")).ToList();
            var f = Path.Combine(dir, "e.pdf");
            Assert.Equal(2, MailCenter.WriteLabels(list, f)); Assert.True(new FileInfo(f).Length > 1000);
            Assert.Equal(3, MailCenter.WriteLabels(list, f, "3475", startAt: 20)); // 5 auf dem ersten Bogen, 24 + 1 danach
            Assert.Equal(3, MailCenter.WriteLabels(list, f, "l7163")); // 14 je Bogen
            Assert.Equal(["Erika Muster", "Muster GmbH", "Weg 1", "10115 Berlin"], MailCenter.LabelLines(new Contact("a", "Erika Muster", "", Company: "Muster GmbH", Street: "Weg 1", City: "10115 Berlin")));
            Assert.Throws<InvalidOperationException>(() => MailCenter.WriteLabels([new Contact("x", "Ohne", "")], f));
        }
        finally { Directory.Delete(dir, true); }
    }
}
