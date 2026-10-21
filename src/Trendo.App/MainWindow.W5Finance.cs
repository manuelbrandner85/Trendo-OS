using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-11: Geld – Buchungen, Übersicht, Rechnungen.</summary>
public partial class MainWindow
{
    public Ledger Money => new(_core.DataRoot);
    private static string Eu(decimal d) => d.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
    private int _finYear = DateTime.Today.Year;

    private void InitW5Finance() => RegisterTab(BusinessTabs, "finanzen", "Geld", "SPEC-11", FinanceView, "finanzen", "geld", "rechnung", "buchhaltung", "steuer", "mwst", "einnahmen", "ausgaben");

    public int FinanceImport(string path)
    {
        try { var n = Money.Import(Finance.ImportCsv(File.ReadAllText(path))); _core.Audit.Append(Principal.LocalOwner.Name, "finance.import", $"{n} neu"); Say($"{n} Buchungen übernommen (doppelte übersprungen).", "finanzen"); return n; }
        catch (Exception ex) { Say("Import hat nicht geklappt: " + ex.Message, "finanzen"); return -1; }
    }

    private Control FinanceView()
    {
        var all = Money.All();
        var sp = new StackPanel { Spacing = 8, Children = { P("Behalte Einnahmen und Ausgaben im Blick. Alles bleibt auf diesem PC. Ersetzt keine Steuerberatung.") } };
        var date = new TextBox { Name = "fin-date", Text = DateTime.Today.ToString("dd.MM.yyyy"), Width = 110 };
        var text = new TextBox { Name = "fin-text", PlaceholderText = "Wofür?", Width = 220 };
        var amount = new TextBox { Name = "fin-amount", PlaceholderText = "Betrag, z. B. -12,50", Width = 140 };
        var cat = new ComboBox { Name = "fin-cat", ItemsSource = Finance.Categories, SelectedIndex = 0, Width = 130 };
        var vat = new ComboBox { Name = "fin-vat", ItemsSource = new[] { "ohne MwSt", "7 %", "19 %" }, SelectedIndex = 0, Width = 110 };
        var add = Act("fin-add", "Eintragen", () =>
        {
            try
            {
                var d = DateOnly.Parse(date.Text ?? "", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                Money.Add(new(d, text.Text ?? "", Finance.ParseAmount(amount.Text ?? ""), Finance.Categories[Math.Max(0, cat.SelectedIndex)], vat.SelectedIndex switch { 1 => LegalRates.VatReduced, 2 => LegalRates.VatStandard, _ => 0m }));
                Say("Eingetragen.", "finanzen");
            }
            catch (FormatException) { Say("Bitte Datum (TT.MM.JJJJ) und Betrag prüfen. Ausgaben mit Minus.", "finanzen"); }
        }, true);
        sp.Children.Add(Card(new StackPanel { Spacing = 5, Children = { H("Neue Buchung", 14), new WrapPanel { Children = { date, text, amount, cat, vat, add } } } }));
        var csv = new TextBox { Name = "fin-csv", PlaceholderText = "Pfad zur Bank-CSV", Width = 340 };
        sp.Children.Add(Card(new WrapPanel { Children = { P("Kontoauszug als CSV übernehmen  "), csv, Act("fin-import", "Übernehmen", () => FinanceImport(csv.Text?.Trim('"', ' ') ?? "")),
            Act("fin-export", "Als CSV speichern", () => { var f = Path.Combine(BusinessFolder, $"Buchungen-{DateTime.Today:yyyy-MM-dd}.csv"); File.WriteAllText(f, Finance.ExportCsv(all), new System.Text.UTF8Encoding(true)); Say("Gespeichert: " + Path.GetFileName(f), "finanzen"); }) } }));

        var y = Finance.Year(all, _finYear);
        var sum = new StackPanel { Spacing = 3, Children = { new TextBlock { Name = "fin-year", Text = $"{_finYear}: Gewinn {Eu((y.IncomeNet + y.ExpenseNet))} € (netto)", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Text },
            P($"Einnahmen {Eu(y.IncomeNet)} € · Ausgaben {Eu(-y.ExpenseNet)} € · MwSt eingenommen {Eu(y.VatIn)} € · MwSt gezahlt {Eu(-y.VatOut)} € → an das Finanzamt etwa {Eu((y.VatIn + y.VatOut))} €", Text, 11) } };
        foreach (var m in Finance.Months(all).Where(m => m.Year == _finYear)) sum.Children.Add(P($"{new DateTime(m.Year, m.Month, 1).ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))}: +{Eu(m.Income)} € / {Eu(m.Expenses)} € = {Eu(m.Result)} €", null, 11));
        sp.Children.Add(Card(new StackPanel { Children = { Row(H("Übersicht", 14), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
            Act("fin-prev", "◀", () => { _finYear--; Show("finanzen"); }), Act("fin-next", "▶", () => { _finYear++; Show("finanzen"); }) } }), sum } }));

        // Rechnung
        var seller = new TextBox { Name = "inv-seller", PlaceholderText = "Dein Name und Adresse", AcceptsReturn = true, Width = 260, Height = 60 };
        var buyer = new TextBox { Name = "inv-buyer", PlaceholderText = "Kunde: Name und Adresse", AcceptsReturn = true, Width = 260, Height = 60 };
        var item = new TextBox { Name = "inv-item", PlaceholderText = "Leistung", Width = 220 }; var qty = new TextBox { Name = "inv-qty", Text = "1", Width = 60 }; var unit = new TextBox { Name = "inv-unit", PlaceholderText = "Preis netto", Width = 110 };
        var small = new CheckBox { Name = "inv-small", Content = new TextBlock { Text = "Kleinunternehmer (keine MwSt)", Foreground = Text }, IsChecked = true };
        var make = Act("inv-create", "Rechnung erstellen", () =>
        {
            try
            {
                var inv = new Invoice(Finance.NextInvoiceNumber(Money.InvoiceNumbers(), DateOnly.FromDateTime(DateTime.Today)), DateOnly.FromDateTime(DateTime.Today), seller.Text ?? "", buyer.Text ?? "",
                    [new(item.Text ?? "", Finance.ParseAmount(qty.Text ?? "1"), Finance.ParseAmount(unit.Text ?? ""))], LegalRates.VatStandard, small.IsChecked == true);
                var p = Money.WriteInvoice(inv);
                Money.Add(new(inv.Date, $"Rechnung {inv.Number}", Finance.Totals(inv).Gross, "Einnahmen", inv.SmallBusiness ? 0 : LegalRates.VatStandard));
                _core.Audit.Append(Principal.LocalOwner.Name, "finance.invoice", inv.Number);
                Launch(p); Say($"Rechnung {inv.Number} erstellt und als Einnahme eingetragen.", "finanzen");
            }
            catch (FormatException) { Say("Bitte Menge und Preis als Zahl eingeben.", "finanzen"); }
        }, true);
        sp.Children.Add(Card(new StackPanel { Spacing = 5, Children = { H("Rechnung schreiben", 14), new WrapPanel { Children = { seller, buyer } }, new WrapPanel { Children = { item, qty, unit } }, small, make } }));

        sp.Children.Add(SmallBizCheck("fin-ku"));
        sp.Children.Add(H("Letzte Buchungen", 14));
        var k = 0;
        foreach (var e in all.Take(25))
        {
            var idx = k;
            sp.Children.Add(Card(Row(P($"{e.Date:dd.MM.yyyy} · {e.Text} · {e.Category}", Text, 11.5), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                new TextBlock { Text = $"{Eu(e.Amount)} €", Foreground = e.Amount < 0 ? Brush.Parse("#F97316") : Brush.Parse("#22C55E"), VerticalAlignment = VerticalAlignment.Center },
                Act($"fin-del-{k}", "Löschen", () => { Money.RemoveAt(idx); Show("finanzen"); }) } })));
            k++;
        }
        return BusinessPage("Geld", sp);
    }
}
