using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Business;

namespace Trendo.App;

/// <summary>SPEC-11: Geld – Buchungen, Übersicht, Rechnungen. T-0694: als Programm mit Reitern (siehe MainWindow.GeldProgramm.cs).</summary>
public partial class MainWindow
{
    public Ledger Money => new(_core.DataRoot);
    private static string Eu(decimal d) => d.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
    private int _finYear = DateTime.Today.Year;

    private void InitW5Finance() => RegisterTab(BusinessTabs, "finanzen", "Geld", "SPEC-11", FinanceView, "finanzen", "geld", "rechnung", "buchhaltung", "steuer", "mwst", "einnahmen", "ausgaben",
        "kontoauszug", "camt", "mt940", "e-rechnung", "zugferd", "xrechnung", "datev", "kredit", "tilgungsplan", "angebot");

    /// <summary>Kontoauszug direkt übernehmen (ohne Vorschau, für Tests und ältere Wege). T-0694: liest auch CAMT.053 und MT940, mit Rückgängig.</summary>
    public int FinanceImport(string path)
    {
        try
        {
            var n = 0;
            var entries = BankImport.Vorschau(Money.All(), BankImport.Buchungen(BankImport.Lesen(path, new SpaltenGedaechtnis(_core.DataRoot)).Zeilen, MoneyMemory)).Where(x => !x.Duplicate).Select(x => x.Entry).ToList();
            GeldChange("Kontoauszug übernehmen", () => n = Money.Import(entries), refresh: false);
            BudgetWarn(); _core.Audit.Append(Principal.LocalOwner.Name, "finance.import", $"{n} neu"); Say($"{n} Buchungen übernommen (doppelte übersprungen).", "finanzen"); return n;
        }
        catch (Exception ex) { Say("Import hat nicht geklappt: " + Trendo.Core.FriendlyError.Say(ex), "finanzen"); return -1; }
    }

    private Control FinanceView() => BusinessPage("Geld", GeldProgramm(() =>
    {
        var all = Money.All();
        var sp = new StackPanel { Spacing = 8, Children = { P("Behalte Einnahmen und Ausgaben im Blick – privat wie geschäftlich. Alles bleibt auf diesem PC. Ersetzt keine Steuerberatung."), FinanceOverview(all) } };
        sp.Children.Add(HouseholdView());
        sp.Children.Add(GeldNeueBuchung());
        var y = Finance.Year(all, _finYear);
        var sum = new StackPanel { Spacing = 3, Children = { new TextBlock { Name = "fin-year", Text = $"{_finYear}: Gewinn {Eu((y.IncomeNet + y.ExpenseNet))} € (netto)", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Text },
            P($"Einnahmen {Eu(y.IncomeNet)} € · Ausgaben {Eu(-y.ExpenseNet)} € · MwSt eingenommen {Eu(y.VatIn)} € · MwSt gezahlt {Eu(-y.VatOut)} € → an das Finanzamt etwa {Eu((y.VatIn + y.VatOut))} €", Text, 11) } };
        foreach (var m in Finance.Months(all).Where(m => m.Year == _finYear)) sum.Children.Add(P($"{new DateTime(m.Year, m.Month, 1).ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))}: +{Eu(m.Income)} € / {Eu(m.Expenses)} € = {Eu(m.Result)} €", null, 11));
        sp.Children.Add(Card(new StackPanel { Children = { Row(H("Jahr", 14), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
            SimpleWords.Named(Act("fin-prev", "◀", () => { _finYear--; Show("finanzen"); }), "Voriges Jahr"), SimpleWords.Named(Act("fin-next", "▶", () => { _finYear++; Show("finanzen"); }), "Nächstes Jahr") } }), sum } }));
        sp.Children.Add(SmallBizCheck("fin-ku"));
        return sp;
    }));

    /// <summary>Neue Buchung von Hand (mit Rückgängig) und CSV-Export.</summary>
    Control GeldNeueBuchung()
    {
        var date = new TextBox { Name = "fin-date", Text = DateTime.Today.ToString("dd.MM.yyyy"), Width = 110 };
        var text = new TextBox { Name = "fin-text", PlaceholderText = "Wofür?", Width = 220 };
        var amount = new TextBox { Name = "fin-amount", PlaceholderText = "Betrag, z. B. -12,50", Width = 140 };
        var cat = new ComboBox { Name = "fin-cat", ItemsSource = FinanceExtra.AllCategories, SelectedIndex = 0, Width = 150 };
        var vat = new ComboBox { Name = "fin-vat", ItemsSource = new[] { "ohne MwSt", "7 %", "19 %" }, SelectedIndex = 0, Width = 110 };
        var add = Act("fin-add", "Eintragen", () =>
        {
            DateOnly d; decimal a;
            try { d = DateOnly.Parse(date.Text ?? "", System.Globalization.CultureInfo.GetCultureInfo("de-DE")); a = Finance.ParseAmount(amount.Text ?? ""); }
            catch (FormatException) { Say("Bitte Datum (TT.MM.JJJJ) und Betrag prüfen. Ausgaben mit Minus.", "finanzen"); return; }
            if (GeldChange("Neue Buchung", () => Money.Add(new(d, text.Text ?? "", a, FinanceExtra.AllCategories[Math.Max(0, cat.SelectedIndex)], vat.SelectedIndex switch { 1 => LegalRates.VatReduced, 2 => LegalRates.VatStandard, _ => 0m })), refresh: false))
            { Say("Eingetragen.", "finanzen"); BudgetWarn(); }
        }, true);
        var export = Act("fin-export", "Als CSV speichern", () => { var f = Path.Combine(BusinessFolder, $"Buchungen-{DateTime.Today:yyyy-MM-dd}.csv"); File.WriteAllText(f, Finance.ExportCsv(Money.All()), new System.Text.UTF8Encoding(true)); Say("Gespeichert: " + Path.GetFileName(f), "finanzen"); });
        return Card(new StackPanel { Spacing = 5, Children = { H("Neue Buchung", 14), new WrapPanel { Children = { date, text, amount, cat, vat, add } }, new WrapPanel { Children = { export } } } });
    }

    /// <summary>Schnelle Rechnung mit einer Position (älterer Weg, bleibt erhalten): schreibt HTML wie bisher und zusätzlich ein PDF; Steuernummer kommt aus „Meine Firma“.</summary>
    Control GeldSchnellRechnung()
    {
        var firma = GeldBuch().Firma;
        var seller = new TextBox { Name = "inv-seller", PlaceholderText = "Dein Name und Adresse", Text = firma.Anschrift, AcceptsReturn = true, Width = 260, Height = 60 };
        var buyer = new TextBox { Name = "inv-buyer", PlaceholderText = "Kunde: Name und Adresse", AcceptsReturn = true, Width = 260, Height = 60 };
        var item = new TextBox { Name = "inv-item", PlaceholderText = "Leistung", Width = 220 }; var qty = new TextBox { Name = "inv-qty", Text = "1", Width = 60 }; var unit = new TextBox { Name = "inv-unit", PlaceholderText = "Preis netto", Width = 110 };
        var small = new CheckBox { Name = "inv-small", Content = new TextBlock { Text = "Kleinunternehmer (keine MwSt)", Foreground = Text }, IsChecked = _settings.SmallBusiness };
        small.IsCheckedChanged += (_, _) => { _settings.SmallBusiness = small.IsChecked == true; SaveSettings(); };
        var make = Act("inv-create", "Rechnung erstellen", () =>
        {
            Invoice inv; string? pdf = null; var missing = new List<string>();
            try
            {
                var f = GeldBuch().Firma; var taxId = f.SteuerNr.Length > 0 ? f.SteuerNr : f.UstId;
                inv = new Invoice(Finance.NextInvoiceNumber(Money.InvoiceNumbers(), DateOnly.FromDateTime(DateTime.Today)), DateOnly.FromDateTime(DateTime.Today), seller.Text ?? "", buyer.Text ?? "",
                    [new(item.Text ?? "", Finance.ParseAmount(qty.Text ?? "1"), Finance.ParseAmount(unit.Text ?? ""))], LegalRates.VatStandard, small.IsChecked == true, taxId);
            }
            catch (FormatException) { Say("Bitte Menge und Preis als Zahl eingeben.", "finanzen"); return; }
            GeldChange("Rechnung erstellen", () =>
            {
                var p = Money.WriteInvoice(inv);
                if (_settings.InvoiceLogo.Length > 0) File.WriteAllText(p, FinanceExtra.WithLogo(File.ReadAllText(p), _settings.InvoiceLogo));
                // dieselbe Rechnung als PDF über den Rechnungs-Kern – mit Pflichtangaben-Prüfung
                var b = GeldBuch(); var lines = inv.Seller.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var f2 = new Firma { Name = lines.FirstOrDefault() ?? "", Strasse = lines.Length > 2 ? lines[1] : "", PlzOrt = lines.Length > 1 ? lines[^1] : "", SteuerNr = b.Firma.SteuerNr, UstId = b.Firma.UstId, Iban = b.Firma.Iban, Bank = b.Firma.Bank, Email = b.Firma.Email, Telefon = b.Firma.Telefon };
                var tmp = new RechnungsBuch { Firma = f2, Steuer = b.Steuer };
                var v = Rechnungen.Neu(tmp, "rechnung", inv.Date); v.KundeAnschrift = inv.Buyer.Replace("\r", ""); v.Kleinunternehmer = inv.SmallBusiness; v.Nummer = inv.Number;
                v.Positionen = [.. inv.Lines.Select(l => new Position { Text = l.Text, Menge = l.Qty, Preis = l.UnitNet, Ust = inv.SmallBusiness ? 0 : inv.VatRate })];
                missing = Rechnungen.Fehlend(tmp, v);
                pdf = Path.ChangeExtension(p, ".pdf"); RechnungPdf.Write(tmp, v, pdf, _settings.InvoiceLogo);
                Money.Add(new(inv.Date, $"Rechnung {inv.Number}", Finance.Totals(inv).Gross, "Einnahmen", inv.SmallBusiness ? 0 : LegalRates.VatStandard));
                _core.Audit.Append(Principal.LocalOwner.Name, "finance.invoice", inv.Number);
            }, refresh: false);
            if (pdf is not null) Launch(pdf);
            Say($"Rechnung {inv.Number} erstellt (PDF und HTML) und als Einnahme eingetragen." + (missing.Count > 0 ? " Achtung, für eine ordnungsgemäße Rechnung fehlt noch: " + string.Join(" ", missing) : ""), "finanzen");
        });
        return new Expander { Name = "geld-schnell", Header = "Schnelle Rechnung mit einer Position (wird sofort als Einnahme gebucht)", Content = new StackPanel { Spacing = 5, Children = {
            new WrapPanel { Children = { seller, buyer } }, new WrapPanel { Children = { item, qty, unit } }, small, make,
            ERechnungPanel(seller, buyer, item, qty, unit, small) } } }; // Alle Studios, Runde 6: auch als E-Rechnung (XRechnung)
    }
}
