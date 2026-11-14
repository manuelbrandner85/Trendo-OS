using System.Text.Json;
using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 6 – SPEC-11: dieselbe Rechnung zusätzlich als E-Rechnung (XRechnung, CII 2.3) mit ZUGFeRD-csharp.
/// Eigene Angaben (USt-IdNr./Steuernummer, E-Mail, IBAN) werden nur lokal im Datenordner gemerkt.</summary>
public partial class MainWindow
{
    sealed record ERechnungAngaben(string VatId = "", string TaxNumber = "", string Email = "", string Iban = "");
    string ERechnungFile => Path.Combine(_core.DataRoot, "finanzen", "e-rechnung.json");
    ERechnungAngaben ERechnungLoad() { try { return File.Exists(ERechnungFile) ? JsonSerializer.Deserialize<ERechnungAngaben>(File.ReadAllText(ERechnungFile)) ?? new() : new(); } catch (JsonException) { return new(); } }
    string _erMsg = ""; public string ERechnungMessage => _erMsg;

    /// <summary>Rechnung erstellen und zusätzlich als XRechnung speichern (öffentlich für Tests). Rückgabe: Pfad der XML-Datei oder null.</summary>
    public string? FinanceERechnung(string seller, string buyer, string item, string qty, string unit, bool smallBusiness,
        string vatId, string taxNumber, string sellerMail, string iban, string buyerMail, string reference)
    {
        try
        {
            var inv = new Invoice(Finance.NextInvoiceNumber(Money.InvoiceNumbers(), DateOnly.FromDateTime(DateTime.Today)), DateOnly.FromDateTime(DateTime.Today), seller, buyer,
                [new(item, Finance.ParseAmount(qty.Length > 0 ? qty : "1"), Finance.ParseAmount(unit))], LegalRates.VatStandard, smallBusiness);
            var s = ERechnung.Parse(seller, vatId, taxNumber, sellerMail); var b = ERechnung.Parse(buyer, email: buyerMail);
            if (ERechnung.Check(inv, s, b, iban, reference) is { Count: > 0 } errors) { _erMsg = "⚠ " + string.Join(" ", errors); Show("finanzen"); return null; }
            Directory.CreateDirectory(Path.GetDirectoryName(ERechnungFile)!);
            File.WriteAllText(ERechnungFile, JsonSerializer.Serialize(new ERechnungAngaben(vatId.Trim(), taxNumber.Trim(), sellerMail.Trim(), iban.Trim())));
            var html = Money.WriteInvoice(inv);
            var xml = Path.Combine(Path.GetDirectoryName(html)!, ERechnung.FileName(inv.Number));
            File.WriteAllBytes(xml, ERechnung.Write(inv, s, b, iban, reference));
            var check = ERechnung.Read(File.ReadAllBytes(xml));
            Money.Add(new(inv.Date, $"Rechnung {inv.Number}", Finance.Totals(inv).Gross, "Einnahmen", inv.SmallBusiness ? 0 : LegalRates.VatStandard));
            _core.Audit.Append(Principal.LocalOwner.Name, "finance.erechnung", inv.Number);
            _erMsg = $"✓ Rechnung {inv.Number} als E-Rechnung gespeichert: {Path.GetFileName(xml)} ({check.Gross:N2} € brutto, wieder eingelesen und geprüft). Die lesbare Fassung liegt daneben.";
            Show("finanzen"); return xml;
        }
        catch (FormatException) { _erMsg = "⚠ Bitte Menge und Preis als Zahl eingeben."; Show("finanzen"); return null; }
    }

    Control ERechnungPanel(TextBox seller, TextBox buyer, TextBox item, TextBox qty, TextBox unit, CheckBox small)
    {
        var a = ERechnungLoad();
        var vat = new TextBox { Name = "er-vat", Text = a.VatId, PlaceholderText = "Deine USt-IdNr. (DE…)", Width = 200 };
        var tax = new TextBox { Name = "er-tax", Text = a.TaxNumber, PlaceholderText = "oder Steuernummer", Width = 160 };
        var mail = new TextBox { Name = "er-mail", Text = a.Email, PlaceholderText = "Deine E-Mail", Width = 200 };
        var iban = new TextBox { Name = "er-iban", Text = a.Iban, PlaceholderText = "Deine IBAN", Width = 240 };
        var bmail = new TextBox { Name = "er-buyer-mail", PlaceholderText = "E-Mail des Kunden", Width = 200 };
        var refx = new TextBox { Name = "er-ref", PlaceholderText = "Käuferreferenz / Leitweg-ID (Behörden)", Width = 260 };
        var go = Act("er-create", "Rechnung + E-Rechnung (XRechnung) erstellen", () => FinanceERechnung(seller.Text ?? "", buyer.Text ?? "", item.Text ?? "", qty.Text ?? "1", unit.Text ?? "",
            small.IsChecked == true, vat.Text ?? "", tax.Text ?? "", mail.Text ?? "", iban.Text ?? "", bmail.Text ?? "", refx.Text ?? ""));
        var sp = new StackPanel { Spacing = 4, Children = {
            P("Für Firmen Pflicht: Empfang seit 2025, Versand ab 2027/28. Trendo schreibt XRechnung (CII) – lesbar für Buchhaltungsprogramme und Behörden.", Muted, 11),
            new WrapPanel { Children = { vat, tax, mail, iban } }, new WrapPanel { Children = { bmail, refx } }, go } };
        if (_erMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "er-msg", Text = _erMsg, FontSize = 11.5, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return new Expander { Name = "er-box", Header = "Auch als E-Rechnung (XRechnung)", IsExpanded = _erMsg.Length > 0, Content = sp };
    }
}
