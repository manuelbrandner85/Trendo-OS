using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_Spec11_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-11 (UI): Buchung eintragen, Rechnung erstellen, Jahresgewinn wird angezeigt")]
    public void Money()
    {
        var (w, _, _) = Ui.Open();
        w.Show("finanzen");
        w.Find<TextBox>("fin-text").Text = "Papier"; w.Find<TextBox>("fin-amount").Text = "-20,00";
        w.Find<Button>("fin-add").Press();
        w.Find<TextBox>("inv-item").Text = "Beratung"; w.Find<TextBox>("inv-unit").Text = "100";
        w.Find<Button>("inv-create").Press();
        Assert.Equal(2, w.Money.All().Count);
        Assert.Contains("80,00", w.Find<TextBlock>("fin-year").Text);
        Assert.Single(w.Money.InvoiceNumbers());
        w.Close();
    }
}

public class W5_SmallBusiness_Ui
{
    [AvaloniaFact(DisplayName = "§ 19 UStG (UI): Kleinunternehmer-Prüfung auf Geld- und Business-Seite")]
    public void Check()
    {
        var (w, _, _) = Ui.Open();
        w.Money.Add(new(new DateOnly(DateTime.Today.Year, 1, 5), "Auftrag", 1190m, "Einnahmen", 19m));
        w.Show("finanzen");
        Assert.Equal("1000", w.Find<TextBox>("fin-ku-cur").Text);
        Assert.StartsWith("✓ Ja", W6Ui.Run(w, "fin-ku"));
        Assert.Contains("Vorjahresumsatz über 25.000 €", W6Ui.Run(w, "fin-ku", ("prev", "30000")));
        Assert.Contains("Gründungsjahr", W6Ui.Run(w, "fin-ku", ("cur", "26000"), ("new", "j")));
        w.Show("business");
        Assert.StartsWith("✗ Nein", W6Ui.Run(w, "biz-ku", ("cur", "100001")));
        w.Close();
    }
}
