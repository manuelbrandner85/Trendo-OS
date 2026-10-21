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
