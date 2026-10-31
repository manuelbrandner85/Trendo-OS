using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Teil 4, Business & Geld – Oberfläche.</summary>
public class Teil4BusinessUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Online verkaufen: Startseite mit Kennzahlen; Kunde, Artikel, Bestellung, Rechnung – Umsatz und Geld stimmen")]
    public void Sell()
    {
        var (w, _, _) = Ui.Open();
        Assert.Equal("verkaufen", MainWindow.StudioPages["STUDIO-ONLINE-BUSINESS-01"]);
        w.Show("verkaufen"); Pump();
        w.Find<TextBox>("sell-cust-name").Text = "Erika Muster"; w.Find<TextBox>("sell-cust-address").Text = "Hauptstraße 1, 10115 Berlin";
        w.Find<Button>("sell-cust-add").Press(); Pump();
        w.Find<TextBox>("sell-prod-name").Text = "Kaffee"; w.Find<TextBox>("sell-prod-price").Text = "10,00"; w.Find<TextBox>("sell-prod-stock").Text = "5";
        w.Find<Button>("sell-prod-add").Press(); Pump();
        Assert.Single(w.Find<WrapPanel>("sell-shop").Children);
        w.Find<ComboBox>("sell-order-customer").SelectedItem = "Erika Muster"; w.Find<ComboBox>("sell-order-product").SelectedItem = "Kaffee";
        w.Find<TextBox>("sell-order-qty").Text = "2";
        w.Find<Button>("sell-order-add").Press(); Pump();
        // 105 (Entscheidung 31.10.2026): Umsatz erst bei Zahlung, Bestand erst bei Lieferung
        Assert.Equal("0,00 €", w.Find<TextBlock>("sell-kpi-month").Text);
        Assert.Equal(5, w.BizShop.Products.All()[0].Stock);
        w.Find<Button>("sell-deliver-0").Press(); Pump();
        Assert.Equal(3, w.BizShop.Products.All()[0].Stock);
        w.Find<Button>("sell-invoice-0").Press(); Pump();
        Assert.Empty(w.Money.All());
        Assert.Null(w.TryFind<Button>("sell-invoice-0"));
        w.Find<Button>("sell-pay-0").Press(); Pump();
        w.Find<Button>("sell-pay-book-0").Press(); Pump();
        Assert.Equal(20m, Assert.Single(w.Money.All()).Amount);
        Assert.Equal("20,00 €", w.Find<TextBlock>("sell-kpi-month").Text);
        Assert.Null(w.TryFind<Button>("sell-pay-0"));
        // dieselben Kunden wie unter „Betrieb“
        Assert.Equal("Erika Muster", Assert.Single(w.BizContacts.All()).Name);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Geschäftsidee U2/U3: Score als Ring mit Ampel, Gründungsschritte abhaken")]
    public void Idea_ring_steps()
    {
        var (w, _, _) = Ui.Open();
        w.Show("business"); Pump();
        Assert.NotNull(w.TryFind<Avalonia.Controls.Shapes.Arc>("biz-ring"));
        Assert.Equal(3, w.Find<StackPanel>("biz-lamp").Children.Count);
        w.Find<Button>("biz-step-0").Press(); Pump();
        Assert.Equal(12, (int)w.Find<ProgressBar>("biz-steps-progress").Value);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Geld: Übersicht mit Saldo und Diagramm, Budget-Warnung, Kategorie in der Tabelle ändern wird gelernt, Steuerberater-ZIP")]
    public void Money_overview()
    {
        var (w, _, _) = Ui.Open();
        w.Show("finanzen"); Pump();
        w.Find<TextBox>("fin-budget-amount").Text = "50"; w.Find<ComboBox>("fin-budget-cat").SelectedItem = "Lebensmittel";
        w.Find<Button>("fin-budget-set").Press(); Pump();
        w.Find<TextBox>("fin-text").Text = "Wochenmarkt"; w.Find<TextBox>("fin-amount").Text = "-60,00";
        w.Find<Button>("fin-add").Press(); Pump();
        Assert.Contains("-60,00", w.Find<TextBlock>("fin-balance").Text);
        w.Find<ComboBox>("fin-row-cat-0").SelectedItem = "Lebensmittel"; Pump();
        Assert.Equal("Lebensmittel", w.Money.All()[0].Category);
        Assert.Equal("Lebensmittel", w.MoneyMemory.Lookup("Wochenmarkt Samstag"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("überschritten"));
        w.Find<TextBox>("fin-tax-year").Text = DateTime.Today.Year.ToString();
        w.Find<Button>("fin-tax-export").Press(); Pump();
        Assert.True(File.Exists(Path.Combine(w.BusinessFolder, $"Steuerberater-{DateTime.Today.Year}.zip")));
        w.Close();
    }
}
