using System.Text;
using System.Text.RegularExpressions;
using s2industries.ZUGFeRD;

namespace Trendo.Core;

/// <summary>Anschrift und Kennungen einer Partei für die E-Rechnung (Name, Straße, PLZ, Ort, Land, USt-IdNr. oder Steuernummer, E-Mail).</summary>
/// <summary>T-0675: Position der E-Rechnung mit Einheit und Steuersatz (Netto-Einzelpreis nach Rabatt, Netto-Gesamt gerundet).</summary>
public sealed record ERechnungZeile(string Text, decimal Menge, string Einheit, decimal NettoEinzel, decimal NettoGesamt, decimal Ust);

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

    /// <summary>T-0675 (Rechnungen &amp; Angebote): Einheit als UN/ECE-Code (Rec. 20) – Stück H87, Stunde HUR, Tag DAY, Pauschale LS, Monat MON, km KMT, kg KGM, m MTR, m² MTK, Liter LTR.</summary>
    public static QuantityCodes Unit(string einheit) => (einheit ?? "").Trim().ToLowerInvariant().TrimEnd('.') switch
    {
        "std" or "stunde" or "stunden" or "h" => QuantityCodes.HUR, "tag" or "tage" => QuantityCodes.DAY, "pauschal" or "pauschale" => QuantityCodes.LS,
        "monat" or "monate" => QuantityCodes.MON, "km" => QuantityCodes.KMT, "kg" => QuantityCodes.KGM, "m" => QuantityCodes.MTR, "m²" or "qm" => QuantityCodes.MTK,
        "liter" or "l" => QuantityCodes.LTR, _ => QuantityCodes.H87,
    };

    /// <summary>T-0694: Pflichtangaben je Format. „xrechnung“ (Behörden, streng: E-Mail beider Seiten, IBAN, Käuferreferenz) oder „zugferd“
    /// (EN 16931 im PDF: Anschriften, Steuernummer/USt-IdNr.; IBAN nur wenn angegeben, dann gültig). Leere Liste = in Ordnung.</summary>
    public static IReadOnlyList<string> CheckDetailed(string number, ERechnungPartei seller, ERechnungPartei buyer, string iban, string buyerReference,
        IReadOnlyList<ERechnungZeile> lines, bool smallBusiness, string exemptionReason, bool xrechnung)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(number)) e.Add("Rechnungsnummer fehlt.");
        if (lines.Count == 0) e.Add("Die Rechnung hat keine Positionen.");
        if (lines.Any(l => l.Menge == 0 || string.IsNullOrWhiteSpace(l.Text))) e.Add("Jede Position braucht Text und eine Menge.");
        foreach (var (p, who) in new[] { (seller, "Meine Firma"), (buyer, "Kunde") })
        {
            if (string.IsNullOrWhiteSpace(p.Name)) e.Add($"{who}: Name fehlt.");
            if (p.Zip.Length == 0 || p.City.Length == 0) e.Add($"{who}: PLZ und Ort fehlen (Zeile „12345 Ort“).");
            if (xrechnung && !MailRx().IsMatch(p.Email)) e.Add($"{who}: E-Mail-Adresse fehlt oder ist ungültig (für XRechnung Pflicht).");
        }
        if (seller.VatId.Length == 0 && seller.TaxNumber.Length == 0) e.Add("Meine Firma: USt-IdNr. oder Steuernummer fehlt.");
        if (xrechnung ? !IbanValid(iban) : iban.Trim().Length > 0 && !IbanValid(iban)) e.Add("IBAN fehlt oder ist ungültig.");
        if (xrechnung && string.IsNullOrWhiteSpace(buyerReference)) e.Add("Käuferreferenz fehlt (bei Behörden die Leitweg-ID, sonst z. B. Bestellnummer oder Kundennummer).");
        if (!smallBusiness && lines.Any(l => l.Ust == 0) && string.IsNullOrWhiteSpace(exemptionReason)) e.Add("Bei 0 % fehlt der Befreiungsgrund.");
        return e;
    }

    /// <summary>T-0675/T-0694: E-Rechnung mit Steuersatz und Einheit je Position (mehrere Sätze). Kleinunternehmer → Kategorie E mit § 19-Hinweis;
    /// 0 % ohne Kleinunternehmer → Kategorie E mit dem angegebenen Befreiungsgrund. <paramref name="xrechnung"/> = true schreibt das Profil XRechnung (CII 2.3),
    /// false das Profil EN 16931 („Comfort“) für die Einbettung als factur-x.xml in ein ZUGFeRD-PDF.</summary>
    public static byte[] WriteDetailed(string number, DateOnly date, DateOnly? delivery, ERechnungPartei seller, ERechnungPartei buyer, string iban, string buyerReference,
        IReadOnlyList<ERechnungZeile> lines, bool smallBusiness, int paymentDays = 14, string exemptionReason = "", bool xrechnung = true)
    {
        var errors = CheckDetailed(number, seller, buyer, iban, buyerReference, lines, smallBusiness, exemptionReason, xrechnung);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        var d = InvoiceDescriptor.CreateInvoice(number, date.ToDateTime(TimeOnly.MinValue), CurrencyCodes.EUR);
        if (buyerReference.Trim().Length > 0) d.ReferenceOrderNo = buyerReference.Trim();
        d.ActualDeliveryDate = (delivery ?? date).ToDateTime(TimeOnly.MinValue);
        d.SetSeller(seller.Name, seller.Zip, seller.City, seller.Street, Country(seller.Country));
        if (seller.Email.Length > 0)
        {
            d.SetSellerContact(name: seller.Name, emailAddress: seller.Email);
            d.SetSellerElectronicAddress(seller.Email, ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp);
        }
        if (seller.VatId.Length > 0) d.AddSellerTaxRegistration(seller.VatId, TaxRegistrationSchemeID.VA);
        if (seller.TaxNumber.Length > 0) d.AddSellerTaxRegistration(seller.TaxNumber, TaxRegistrationSchemeID.FC);
        d.SetBuyer(buyer.Name, buyer.Zip, buyer.City, buyer.Street, Country(buyer.Country));
        if (buyer.Email.Length > 0) d.SetBuyerElectronicAddress(buyer.Email, ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp);
        if (buyer.VatId.Length > 0) d.AddBuyerTaxRegistration(buyer.VatId, TaxRegistrationSchemeID.VA);
        TaxCategoryCodes Cat(decimal rate) => smallBusiness || rate == 0 ? TaxCategoryCodes.E : TaxCategoryCodes.S;
        foreach (var l in lines)
            d.AddTradeLineItem(name: l.Text, netUnitPrice: l.NettoEinzel, unitCode: Unit(l.Einheit), billedQuantity: l.Menge, lineTotalAmount: l.NettoGesamt,
                taxType: TaxTypes.VAT, categoryCode: Cat(l.Ust), taxPercent: smallBusiness ? 0 : l.Ust);
        decimal net = 0, tax = 0;
        foreach (var g in lines.GroupBy(l => smallBusiness ? 0 : l.Ust).OrderByDescending(g => g.Key))
        {
            var n = g.Sum(l => l.NettoGesamt); var t = Math.Round(n * g.Key / 100m, 2, MidpointRounding.AwayFromZero); net += n; tax += t;
            if (Cat(g.Key) == TaxCategoryCodes.E)
                d.AddApplicableTradeTax(n, 0m, 0m, TaxTypes.VAT, TaxCategoryCodes.E, exemptionReason: smallBusiness ? "Kein Ausweis von Umsatzsteuer, da Kleinunternehmer gemäß § 19 UStG" : exemptionReason.Trim());
            else d.AddApplicableTradeTax(n, g.Key, t, TaxTypes.VAT, TaxCategoryCodes.S);
        }
        if (IbanValid(iban))
        {
            d.SetPaymentMeans(PaymentMeansTypeCodes.SEPACreditTransfer, "Überweisung");
            d.AddCreditorFinancialAccount(new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant(), null, name: seller.Name);
        }
        d.AddTradePaymentTerms($"Zahlbar innerhalb von {paymentDays} Tagen ohne Abzug.", date.ToDateTime(TimeOnly.MinValue).AddDays(paymentDays));
        d.SetTotals(net, 0m, 0m, net, tax, net + tax, 0m, net + tax);
        using var ms = new MemoryStream();
        d.Save(ms, ZUGFeRDVersion.Version23, xrechnung ? Profile.XRechnung : Profile.Comfort, ZUGFeRDFormats.CII);
        return ms.ToArray();
    }

    /// <summary>T-0694: eine E-Rechnung (auch eine fremde, eingehende) vollständig lesen – Summen, Steuer je Satz und alle Positionen.</summary>
    public sealed record Gelesen(string Number, DateOnly Date, DateOnly? Delivery, string Seller, string SellerTaxId, string Buyer, string Reference, string Iban,
        decimal Net, decimal Vat, decimal Gross, IReadOnlyList<(decimal Rate, decimal Basis, decimal Tax)> Taxes, IReadOnlyList<ERechnungZeile> Lines, string Profile);

    public static Gelesen ReadDetailed(byte[] xml)
    {
        using var ms = new MemoryStream(xml);
        var d = InvoiceDescriptor.Load(ms);
        static DateOnly? Day(DateTime? t) => t is { } x ? DateOnly.FromDateTime(x) : null;
        var taxId = d.SellerTaxRegistration?.Select(t => t.No).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "";
        var iban = d.CreditorBankAccounts?.Select(a => a.IBAN).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "";
        return new Gelesen(d.InvoiceNo ?? "", Day(d.InvoiceDate) ?? default, Day(d.ActualDeliveryDate), d.Seller?.Name ?? "", taxId, d.Buyer?.Name ?? "", d.ReferenceOrderNo ?? "", iban,
            d.TaxBasisAmount ?? d.LineTotalAmount ?? 0m, d.TaxTotalAmount ?? 0m, d.GrandTotalAmount ?? 0m,
            [.. d.GetApplicableTradeTaxes().Select(t => (t.Percent, t.BasisAmount, t.TaxAmount))],
            [.. d.TradeLineItems.Select(l => new ERechnungZeile(l.Name ?? "", l.BilledQuantity, l.UnitCode?.ToString() ?? "", (decimal?)l.NetUnitPrice ?? 0m, (decimal?)l.LineTotalAmount ?? 0m, l.TaxPercent))],
            d.Profile.ToString());
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
