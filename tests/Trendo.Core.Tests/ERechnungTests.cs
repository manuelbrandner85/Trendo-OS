using System.Xml.Linq;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 6 – SPEC-11: E-Rechnung als XRechnung (CII 2.3) mit ZUGFeRD-csharp 18 (Apache-2.0). Echte Dateien, wieder eingelesen.</summary>
public class ERechnungTests
{
    static readonly XNamespace Rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100", Ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    static Invoice Inv(bool small = false) => new("2026-017", new DateOnly(2026, 11, 8), "Erika Muster\nMusterweg 1\n12345 Musterstadt", "Max Beispiel GmbH\nHauptstraße 5\n54321 Beispielort",
        [new("Stuhl Eiche", 2, 59.50m), new("Tisch rund", 1, 349m)], 19m, small);
    static ERechnungPartei Seller() => ERechnung.Parse("Erika Muster\nMusterweg 1\n12345 Musterstadt", vatId: "DE123456789", email: "rechnung@example.org");
    static ERechnungPartei Buyer() => ERechnung.Parse("Max Beispiel GmbH\nHauptstraße 5\n54321 Beispielort", email: "einkauf@example.com");
    const string Iban = "DE02 1203 0000 0000 2020 51";

    [Fact(DisplayName = "E-Rechnung +1: Anschrift aus Trendo-Text zerlegt (Name, Straße, PLZ, Ort; auch „D-12345“)")]
    public void Parse()
    {
        var p = ERechnung.Parse("Erika Muster\nMusterweg 1\nD-12345 Musterstadt");
        Assert.Equal(("Erika Muster", "Musterweg 1", "12345", "Musterstadt", "DE"), (p.Name, p.Street, p.Zip, p.City, p.Country));
        Assert.Equal("", ERechnung.Parse("Nur Name").Zip);
    }

    [Fact(DisplayName = "E-Rechnung +2: Summen – 468 € netto, 88,92 € USt, 556,92 € brutto; Kleinunternehmer ohne USt")]
    public void Sums()
    {
        Assert.Equal(new ERechnung.Totals(468m, 88.92m, 556.92m), ERechnung.Sum(Inv()));
        Assert.Equal(new ERechnung.Totals(468m, 0m, 468m), ERechnung.Sum(Inv(small: true)));
    }

    [Fact(DisplayName = "E-Rechnung +3: XRechnung schreiben und wieder einlesen – Nummer, Summen, Positionen, Parteien, Leitweg-ID gleich")]
    public void Roundtrip()
    {
        var xml = ERechnung.Write(Inv(), Seller(), Buyer(), Iban, "04011000-12345-67");
        var r = ERechnung.Read(xml);
        Assert.Equal(("2026-017", 556.92m, 88.92m, 2, "Erika Muster", "Max Beispiel GmbH", "04011000-12345-67"), (r.Number, r.Gross, r.Vat, r.Lines, r.Seller, r.Buyer, r.Reference));
    }

    [Fact(DisplayName = "E-Rechnung +4: XML enthält Profil XRechnung 3.0, IBAN, USt-IdNr., E-Mail-Adressen und Kategorie S mit 19 %")]
    public void Xml_content()
    {
        var x = XDocument.Parse(ERechnung.Utf8(ERechnung.Write(Inv(), Seller(), Buyer(), Iban, "Bestellung 4711")));
        var guideline = x.Descendants(Ram + "GuidelineSpecifiedDocumentContextParameter").Single().Element(Ram + "ID")!.Value;
        Assert.Contains("xeinkauf.de:kosit:xrechnung", guideline);
        Assert.Equal("DE02120300000000202051", x.Descendants(Ram + "IBANID").Single().Value);
        Assert.Contains(x.Descendants(Ram + "ID"), e => e.Value == "DE123456789" && (string?)e.Attribute("schemeID") == "VA");
        Assert.Contains(x.Descendants(Ram + "URIID"), e => e.Value == "rechnung@example.org" && (string?)e.Attribute("schemeID") == "EM");
        Assert.Contains(x.Descendants(Ram + "URIID"), e => e.Value == "einkauf@example.com");
        var tax = x.Descendants(Ram + "ApplicableTradeTax").Last();
        Assert.Equal(("S", "19"), (tax.Element(Ram + "CategoryCode")!.Value, decimal.Parse(tax.Element(Ram + "RateApplicablePercent")!.Value, System.Globalization.CultureInfo.InvariantCulture).ToString("0")));
        Assert.Equal("Bestellung 4711", x.Descendants(Ram + "BuyerReference").Single().Value);
    }

    [Fact(DisplayName = "E-Rechnung +5: Kleinunternehmer (§ 19 UStG) – Kategorie E, 0 USt, Befreiungsgrund im XML")]
    public void Small_business()
    {
        var x = XDocument.Parse(ERechnung.Utf8(ERechnung.Write(Inv(small: true), Seller() with { VatId = "", TaxNumber = "12/345/67890" }, Buyer(), Iban, "Kunde 17")));
        var tax = x.Descendants(Ram + "ApplicableTradeTax").Last();
        Assert.Equal("E", tax.Element(Ram + "CategoryCode")!.Value); Assert.Contains("§ 19 UStG", tax.Element(Ram + "ExemptionReason")!.Value);
        Assert.Contains(x.Descendants(Ram + "ID"), e => e.Value == "12/345/67890" && (string?)e.Attribute("schemeID") == "FC");
        Assert.Equal(468m, ERechnung.Read(System.Text.Encoding.UTF8.GetBytes(x.ToString())).Gross);
    }

    [Fact(DisplayName = "E-Rechnung −1: fehlende Pflichtangaben werden einzeln und verständlich genannt, es wird nichts geschrieben")]
    public void Check_missing()
    {
        var e = ERechnung.Check(Inv() with { Lines = [] }, ERechnung.Parse("Erika Muster"), Buyer() with { Email = "" }, "kein iban", "");
        Assert.Contains("Die Rechnung hat keine Positionen.", e);
        Assert.Contains(e, x => x.StartsWith("Verkäufer: PLZ und Ort")); Assert.Contains(e, x => x.StartsWith("Verkäufer: E-Mail")); Assert.Contains(e, x => x.StartsWith("Käufer: E-Mail"));
        Assert.Contains("Verkäufer: USt-IdNr. oder Steuernummer fehlt.", e); Assert.Contains("IBAN fehlt oder ist ungültig.", e); Assert.Contains(e, x => x.StartsWith("Käuferreferenz fehlt"));
        Assert.Throws<ArgumentException>(() => ERechnung.Write(Inv(), Seller(), Buyer(), Iban, ""));
    }

    [Fact(DisplayName = "E-Rechnung −3: IBAN mit Prüfsumme – gültige Beispiel-IBAN ja, eine Ziffer falsch, falsche Länge oder Text nein")]
    public void Iban_check()
    {
        Assert.True(ERechnung.IbanValid(Iban)); Assert.True(ERechnung.IbanValid("GB82 WEST 1234 5698 7654 32"));
        Assert.False(ERechnung.IbanValid("DE02 1203 0000 0000 2020 52")); Assert.False(ERechnung.IbanValid("DE02 1203 0000 0000 2020 5"));
        Assert.False(ERechnung.IbanValid("kein iban")); Assert.False(ERechnung.IbanValid(""));
    }

    [Fact(DisplayName = "E-Rechnung −2: Position mit Menge 0 oder ohne Text wird abgelehnt")]
    public void Bad_lines() => Assert.Contains("Jede Position braucht Text und eine Menge über 0.", ERechnung.Check(Inv() with { Lines = [new("", 0, 5m)] }, Seller(), Buyer(), Iban, "x"));

    [Fact(DisplayName = "E-Rechnung +6: Dateiname nur aus sicheren Zeichen; Wurzelelement ist eine CII-Rechnung")]
    public void File_name_and_root()
    {
        Assert.Equal("xrechnung-2026-017.xml", ERechnung.FileName("2026-017")); Assert.Equal("xrechnung-A-B-C.xml", ERechnung.FileName("A/B C"));
        Assert.Equal(Rsm + "CrossIndustryInvoice", XDocument.Parse(ERechnung.Utf8(ERechnung.Write(Inv(), Seller(), Buyer(), Iban, "x"))).Root!.Name);
    }
}
