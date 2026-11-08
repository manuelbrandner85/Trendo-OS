using System.Text;
using System.Text.RegularExpressions;
using s2industries.ZUGFeRD;

namespace Trendo.Core;

/// <summary>Anschrift und Kennungen einer Partei für die E-Rechnung (Name, Straße, PLZ, Ort, Land, USt-IdNr. oder Steuernummer, E-Mail).</summary>
public sealed record ERechnungPartei(string Name, string Street, string Zip, string City, string Country = "DE", string VatId = "", string TaxNumber = "", string Email = "");

/// <summary>Alle Studios, Runde 6 – SPEC-11 „Geld &amp; Rechnungen“: E-Rechnung als XRechnung (CII, Version 2.3) mit ZUGFeRD-csharp 18 (Apache-2.0).
/// In Deutschland müssen Firmen E-Rechnungen seit 2025 empfangen und ab 2027/28 versenden. Trendo prüft die Pflichtangaben vor dem Schreiben
/// und liest die Datei zur Kontrolle wieder ein.</summary>
public static partial class ERechnung
{
    [GeneratedRegex(@"^\s*(?:D-)?(\d{5})\s+(.+?)\s*$")] private static partial Regex ZipCity();
    [GeneratedRegex(@"^[A-Z]{2}[A-Z0-9]{2,30}$")] private static partial Regex IbanRx();
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")] private static partial Regex MailRx();

    /// <summary>Mehrzeilige Anschrift wie in Trendo („Name\nStraße\nPLZ Ort“) in Felder zerlegen. Ohne erkennbare PLZ/Ort bleiben die Felder leer (Prüfung meldet es).</summary>
    public static ERechnungPartei Parse(string block, string vatId = "", string taxNumber = "", string email = "")
    {
        var lines = (block ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        string name = lines.FirstOrDefault() ?? "", street = "", zip = "", city = "";
        foreach (var l in lines.Skip(1))
        {
            var m = ZipCity().Match(l);
            if (m.Success && zip.Length == 0) { zip = m.Groups[1].Value; city = m.Groups[2].Value; }
            else if (street.Length == 0 && zip.Length == 0) street = l;
        }
        return new ERechnungPartei(name, street, zip, city, "DE", vatId.Trim(), taxNumber.Trim(), email.Trim());
    }

    /// <summary>Pflichtangaben prüfen (vereinfachte XRechnung-Regeln) – leere Liste = in Ordnung.</summary>
    public static IReadOnlyList<string> Check(Invoice inv, ERechnungPartei seller, ERechnungPartei buyer, string iban, string buyerReference)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(inv.Number)) e.Add("Rechnungsnummer fehlt.");
        if (inv.Lines.Count == 0) e.Add("Die Rechnung hat keine Positionen.");
        if (inv.Lines.Any(l => l.Qty <= 0 || string.IsNullOrWhiteSpace(l.Text))) e.Add("Jede Position braucht Text und eine Menge über 0.");
        foreach (var (p, who) in new[] { (seller, "Verkäufer"), (buyer, "Käufer") })
        {
            if (string.IsNullOrWhiteSpace(p.Name)) e.Add($"{who}: Name fehlt.");
            if (p.Zip.Length == 0 || p.City.Length == 0) e.Add($"{who}: PLZ und Ort fehlen (Zeile „12345 Ort“).");
            if (!MailRx().IsMatch(p.Email)) e.Add($"{who}: E-Mail-Adresse fehlt oder ist ungültig (für XRechnung Pflicht).");
        }
        if (seller.VatId.Length == 0 && seller.TaxNumber.Length == 0) e.Add("Verkäufer: USt-IdNr. oder Steuernummer fehlt.");
        if (!IbanValid(iban)) e.Add("IBAN fehlt oder ist ungültig.");
        if (string.IsNullOrWhiteSpace(buyerReference)) e.Add("Käuferreferenz fehlt (bei Behörden die Leitweg-ID, sonst z. B. Bestellnummer oder Kundennummer).");
        return e;
    }

    /// <summary>Echte IBAN-Prüfung: Ländercode, zwei Prüfziffern, Länge 15–34 (Deutschland genau 22), Prüfsumme Modulo 97 = 1.</summary>
    public static bool IbanValid(string iban)
    {
        var s = new string((iban ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (!IbanRx().IsMatch(s) || s.Length is < 15 or > 34 || !char.IsDigit(s[2]) || !char.IsDigit(s[3])) return false;
        if (s.StartsWith("DE", StringComparison.Ordinal) && s.Length != 22) return false;
        var r = 0;
        foreach (var c in s[4..] + s[..4])
        {
            var v = char.IsDigit(c) ? c - '0' : c - 'A' + 10;
            r = v >= 10 ? (r * 100 + v) % 97 : (r * 10 + v) % 97;
        }
        return r == 1;
    }

    public sealed record Totals(decimal Net, decimal Vat, decimal Gross);
    public static Totals Sum(Invoice inv)
    {
        var net = inv.Lines.Sum(l => Math.Round(l.Qty * l.UnitNet, 2));
        var vat = inv.SmallBusiness ? 0m : Math.Round(net * inv.VatRate / 100m, 2);
        return new Totals(net, vat, net + vat);
    }

    /// <summary>XRechnung (UN/CEFACT CII, Version 2.3) als UTF-8-XML. Kleinunternehmer (§ 19 UStG): Steuerkategorie E mit Befreiungsgrund.</summary>
    public static byte[] Write(Invoice inv, ERechnungPartei seller, ERechnungPartei buyer, string iban, string buyerReference, int paymentDays = 14)
    {
        if (Check(inv, seller, buyer, iban, buyerReference) is { Count: > 0 } errors) throw new ArgumentException(string.Join(" ", errors));
        var d = InvoiceDescriptor.CreateInvoice(inv.Number, inv.Date.ToDateTime(TimeOnly.MinValue), CurrencyCodes.EUR);
        d.ReferenceOrderNo = buyerReference.Trim();
        d.SetSeller(seller.Name, seller.Zip, seller.City, seller.Street, Country(seller.Country));
        d.SetSellerContact(name: seller.Name, emailAddress: seller.Email);
        d.SetSellerElectronicAddress(seller.Email, ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp);
        if (seller.VatId.Length > 0) d.AddSellerTaxRegistration(seller.VatId, TaxRegistrationSchemeID.VA);
        if (seller.TaxNumber.Length > 0) d.AddSellerTaxRegistration(seller.TaxNumber, TaxRegistrationSchemeID.FC);
        d.SetBuyer(buyer.Name, buyer.Zip, buyer.City, buyer.Street, Country(buyer.Country));
        d.SetBuyerElectronicAddress(buyer.Email, ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp);
        var cat = inv.SmallBusiness ? TaxCategoryCodes.E : TaxCategoryCodes.S; var rate = inv.SmallBusiness ? 0m : inv.VatRate;
        foreach (var l in inv.Lines)
            d.AddTradeLineItem(name: l.Text, netUnitPrice: l.UnitNet, unitCode: QuantityCodes.H87, billedQuantity: l.Qty, lineTotalAmount: Math.Round(l.Qty * l.UnitNet, 2),
                taxType: TaxTypes.VAT, categoryCode: cat, taxPercent: rate);
        var t = Sum(inv);
        if (inv.SmallBusiness) d.AddApplicableTradeTax(t.Net, 0m, 0m, TaxTypes.VAT, TaxCategoryCodes.E, exemptionReason: "Kein Ausweis von Umsatzsteuer, da Kleinunternehmer gemäß § 19 UStG");
        else d.AddApplicableTradeTax(t.Net, rate, t.Vat, TaxTypes.VAT, TaxCategoryCodes.S);
        d.SetPaymentMeans(PaymentMeansTypeCodes.SEPACreditTransfer, "Überweisung");
        d.AddCreditorFinancialAccount(new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant(), null, name: seller.Name);
        d.AddTradePaymentTerms($"Zahlbar innerhalb von {paymentDays} Tagen ohne Abzug.", inv.Date.ToDateTime(TimeOnly.MinValue).AddDays(paymentDays));
        d.SetTotals(t.Net, 0m, 0m, t.Net, t.Vat, t.Gross, 0m, t.Gross);
        using var ms = new MemoryStream();
        d.Save(ms, ZUGFeRDVersion.Version23, Profile.XRechnung, ZUGFeRDFormats.CII);
        return ms.ToArray();
    }

    static CountryCodes Country(string code) => Enum.TryParse<CountryCodes>(code.Trim().ToUpperInvariant(), out var c) ? c : CountryCodes.DE;

    /// <summary>Kontrolle: Datei wieder einlesen und die Kernwerte zurückgeben (Nummer, Summen, Positionen).</summary>
    public static (string Number, decimal Gross, decimal Vat, int Lines, string Seller, string Buyer, string Reference) Read(byte[] xml)
    {
        using var ms = new MemoryStream(xml);
        var d = InvoiceDescriptor.Load(ms);
        return (d.InvoiceNo, d.GrandTotalAmount ?? 0m, d.TaxTotalAmount ?? 0m, d.TradeLineItems.Count, d.Seller?.Name ?? "", d.Buyer?.Name ?? "", d.ReferenceOrderNo ?? "");
    }

    /// <summary>Dateiname wie üblich: „xrechnung-&lt;nummer&gt;.xml“ (nur sichere Zeichen).</summary>
    public static string FileName(string number) => "xrechnung-" + string.Concat(number.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')) + ".xml";

    public static string Utf8(byte[] b) => Encoding.UTF8.GetString(b);
}
