using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Geld & Rechnungen (SPEC-11), Teil 4: U1 Übersicht (Saldo, Ein-/Ausgaben je Monat, letzte Buchungen), F1 Haushaltsbuch-Kategorien,
/// F2 Import mit gelernter Kategorie, F4 Budget-Warnung, F5 Export für den Steuerberater, U2 Buchungen als Tabelle (auch in Trendo Tabelle).</summary>
public partial class MainWindow
{
    public CategoryMemory MoneyMemory => new(_core.DataRoot);
    public BudgetStore Budgets => new(_core.DataRoot);

    private Control FinanceOverview(List<Entry> all)
    {
        var today = DateTime.Today;
        var months = FinanceExtra.YearMonths(all, _finYear);
        var cur = months[today.Month - 1];
        var balance = FinanceExtra.Balance(all);
        var kpis = new WrapPanel { Name = "fin-kpis", Children = {
            Kpi("fin-balance", "Kontostand laut Buchungen", Eu(balance) + " €", $"{all.Count} Buchungen", balance >= 0 ? "#2FBF71" : "#F0644B", balance >= 0 ? "#167A45" : "#B2361F"),
            Kpi("fin-month-in", "Einnahmen " + MonthShort[today.Month - 1], Eu(cur.Income) + " €", "dieser Monat", "#3D8BFF", "#1E4FD8"),
            Kpi("fin-month-out", "Ausgaben " + MonthShort[today.Month - 1], Eu(cur.Expense) + " €", "dieser Monat", "#F5A524", "#C46A0A") } };
        var chart = new BarChart { Name = "fin-chart", Height = 200, Labels = MonthShort, Foreground = Muted };
        chart.Series.Add(("Einnahmen", "#2FBF71", months.Select(m => (double)m.Income).ToArray()));
        chart.Series.Add(("Ausgaben", "#F0644B", months.Select(m => (double)m.Expense).ToArray()));
        var legend = new WrapPanel { Children = { new TextBlock { Text = "Einnahmen", Foreground = Brush.Parse("#2FBF71"), FontSize = 12, Margin = new Thickness(0, 0, 14, 0) }, new TextBlock { Text = "Ausgaben", Foreground = Brush.Parse("#F0644B"), FontSize = 12 } } };

        // F4 Budgets
        var budgets = new StackPanel { Name = "fin-budgets", Spacing = 6 };
        foreach (var b in Budgets.Status(all, today.Year, today.Month))
        {
            var color = b.Level switch { "über" => "#F0644B", "knapp" => "#F5A524", _ => "#2FBF71" };
            budgets.Children.Add(new StackPanel { Spacing = 2, Children = {
                Row(new TextBlock { Text = b.Category, Foreground = Text, FontSize = 12.5 }, new TextBlock { Text = $"{Eu(b.Spent)} von {Eu(b.Limit)} €{(b.Level == "über" ? " – überschritten!" : b.Level == "knapp" ? " – fast aufgebraucht" : "")}", Foreground = Brush.Parse(color), FontSize = 12 }),
                new ProgressBar { Value = Math.Min(100, b.Ratio * 100), Maximum = 100, Height = 8, Foreground = Brush.Parse(color) } } });
        }
        var bc = new ComboBox { Name = "fin-budget-cat", ItemsSource = FinanceExtra.AllCategories.Where(c => c != "Einnahmen").ToList(), SelectedIndex = 0, MinWidth = 150 };
        var ba = Field("fin-budget-amount", "€ pro Monat", 110);
        var setB = Act("fin-budget-set", "Budget setzen", () => Try("finanzen", () => { Budgets.Set((string)bc.SelectedItem!, Finance.ParseAmount(Need(ba, "den Betrag"))); Show("finanzen"); }));
        if (budgets.Children.Count == 0) budgets.Children.Add(P("Noch keine Budgets. Lege fest, wie viel du z. B. für Lebensmittel im Monat ausgeben willst – Trendo warnt ab 90 %.", null, 11.5));

        var last = new StackPanel { Spacing = 3 };
        foreach (var e in all.Take(5))
            last.Children.Add(Row(P($"{e.Date:dd.MM.} · {e.Text}", Text, 12), new TextBlock { Text = $"{Eu(e.Amount)} €", Foreground = Brush.Parse(e.Amount < 0 ? "#F0644B" : "#2FBF71"), FontWeight = FontWeight.SemiBold }));
        if (all.Count == 0) last.Children.Add(P("Noch keine Buchungen.", Text));

        return new StackPanel { Spacing = 10, Children = { kpis,
            new Grid { ColumnDefinitions = new ColumnDefinitions("2*,10,*"), Children = {
                Card(new StackPanel { Spacing = 6, Children = { Row(H($"Einnahmen und Ausgaben {_finYear}", 14), legend), chart } }),
                Pos(Card(new StackPanel { Spacing = 6, Children = { H("Zuletzt", 14), last } }), 2) } },
            Card(new StackPanel { Spacing = 6, Children = { H($"Budgets {MonthShort[today.Month - 1]} {today.Year}", 14), budgets, Line(bc, ba, setB) } }) } };
    }

    /// <summary>F4: nach einer Buchung warnen, wenn ein Budget knapp oder überschritten ist.</summary>
    private void BudgetWarn()
    {
        var t = DateTime.Today;
        foreach (var b in Budgets.Status(Money.All(), t.Year, t.Month).Where(b => b.Level != "ok"))
            Toast(b.Level == "über" ? $"Budget „{b.Category}“ ist überschritten: {Eu(b.Spent)} von {Eu(b.Limit)} €." : $"Budget „{b.Category}“ ist fast aufgebraucht: {Eu(b.Spent)} von {Eu(b.Limit)} €.");
    }

    /// <summary>F3 Logo und F5 Export für den Steuerberater.</summary>
    private Control FinanceExtras()
    {
        const string V = "finanzen";
        var logo = Field("inv-logo", "Logo für Rechnungen (PNG, JPG oder SVG)", 300, _settings.InvoiceLogo);
        var year = Field("fin-tax-year", "Jahr", 80, (DateTime.Today.Month <= 3 ? DateTime.Today.Year - 1 : DateTime.Today.Year).ToString());
        return Card(new StackPanel { Spacing = 8, Children = {
            Section("Logo auf Rechnungen", "Dein Logo erscheint oben rechts auf jeder neuen Rechnung.",
                Line(logo, Act("inv-logo-set", "Übernehmen", () => Try(V, () =>
                {
                    var f = Clean(logo);
                    if (f.Length > 0 && (!File.Exists(f) || Path.GetExtension(f).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".svg" or ".webp"))) throw new ArgumentException("Bitte eine Bilddatei (PNG, JPG, SVG) wählen.");
                    _settings.InvoiceLogo = f; SaveSettings(); Say(f.Length > 0 ? "Logo wird auf Rechnungen gesetzt." : "Rechnungen ohne Logo.", V);
                })))),
            Section("Für den Steuerberater", "Ein ZIP mit allen Buchungen des Jahres (CSV), Summen je Kategorie und allen Rechnungen. Ersetzt keine Steuerberatung.",
                Line(year, Act("fin-tax-export", "ZIP erstellen", () => Try(V, () =>
                {
                    var y = int.Parse(Need(year, "das Jahr"));
                    var zip = FinanceExtra.TaxExport(Money.All(), Money.InvoiceDir, y, BusinessFolder);
                    _core.Audit.Append(Principal.LocalOwner.Name, "finance.tax_export", y.ToString()); Say("Gespeichert: " + zip, V);
                }), true))) } });
    }
}
