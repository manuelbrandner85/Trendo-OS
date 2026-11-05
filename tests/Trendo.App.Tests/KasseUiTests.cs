using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0304: Verkaufen › Kasse mit echten Klicks – Artikel antippen, Menge ändern, bar mit Rückgeld, Bon als PDF, Storno nur nach Ja, Tagesabschluss.
public class Kasse_Ui
{
    [AvaloniaFact(DisplayName = "T-0304: Kasse – Kacheln in den Korb, +/−, „passend“/Rückgeld, Kassieren bucht Geld und Bestand, Bon-PDF, Storno Nein/Ja, Tagesabschluss-PDF")]
    public void Ui_register()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        var k = w.Kasse; k.Shop.Products.Add(new Product("Kaffee", "", 0, 10.0, 5, 0, 0)); k.Shop.Products.Add(new Product("Tee", "", 0, 4.0, 1, 0, 0));
        w.Show("verkaufen"); Settle();
        Assert.NotNull(w.Find<WrapPanel>("kasse-articles"));
        Click("kasse-art-0"); Click("kasse-art-0"); Click("kasse-art-1");
        Assert.Equal([("Kaffee", 2m), ("Tee", 1m)], w.KasseCart);
        Assert.False(w.Find<Button>("kasse-art-1").IsEnabled); // Tee: nur 1 vorrätig, liegt schon im Korb
        Click("kasse-minus-0"); Click("kasse-plus-0"); Assert.Equal(2m, w.KasseCart[0].Qty);
        Assert.Equal("24,00 €", w.Find<TextBlock>("kasse-total").Text); // Standard: Kleinunternehmer, Preise ohne MwSt (MwSt prüft CashRegisterTests)
        // zu wenig gegeben → Kassieren lehnt ab
        w.Find<TextBox>("kasse-given").Text = "20"; Settle();
        Assert.StartsWith("Es fehlen 4,00", w.Find<TextBlock>("kasse-change").Text);
        Click("kasse-checkout"); Assert.Empty(w.Money.All()); Assert.Equal(2, w.KasseCart.Count);
        Click("kasse-quick-50"); Assert.Equal("Rückgeld 26,00 €", w.Find<TextBlock>("kasse-change").Text);
        Click("kasse-checkout");
        Assert.Empty(w.KasseCart); Assert.Equal("Rückgeld: 26,00 €", w.Find<TextBlock>("kasse-done-change").Text);
        Assert.Equal(24m, w.Money.All().Single().Amount);
        Assert.Equal(3, k.Shop.Products.All().Single(p => p.Name == "Kaffee").Stock);
        Click("kasse-receipt");
        Assert.Single(Directory.GetFiles(root, "BON-*.pdf", SearchOption.AllDirectories));
        // zweiter Verkauf mit Karte
        Click("kasse-art-0"); Click("kasse-pay-karte"); Click("kasse-checkout");
        Assert.Equal("34,00 €", w.Find<TextBlock>("kasse-day-gross").Text);
        // Storno: Nein ändert nichts, Ja bucht zurück
        Click("kasse-storno-0"); Click("kasse-storno-no-0"); Assert.Equal(2, w.Money.All().Count);
        Click("kasse-storno-0"); Click("kasse-storno-yes-0");
        Assert.Equal(24m, FinanceExtra.Balance(w.Money.All()));
        Assert.Equal(3, k.Shop.Products.All().Single(p => p.Name == "Kaffee").Stock);
        Assert.Equal("24,00 €", w.Find<TextBlock>("kasse-day-gross").Text);
        Click("kasse-day-pdf");
        Assert.Single(Directory.GetFiles(root, "Tagesabschluss-*.pdf", SearchOption.AllDirectories));
        // Online-Bereich bleibt erreichbar
        Click("sell-tab-online"); Assert.NotNull(w.Find<WrapPanel>("sell-shop"));
    }
    [AvaloniaFact(DisplayName = "Kasse wie SumUp/Square: Strichcode scannen legt den Artikel in den Korb, Rabatt 10 % senkt die Summe, Auswertung zeigt Umsatz, Bestseller und Stoßzeit; Artikelnummer mit falscher Prüfziffer abgelehnt")]
    public void Ui_scan_discount_stats()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
            void Enter(string name, string text) { var t = w.Find<TextBox>(name); t.Text = text; t.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle(); }
            var k = w.Kasse; k.Shop.Products.Add(new Product("Kaffee", "", 0, 10.0, 20, 0, 0, "4006381333931")); k.Shop.Products.Add(new Product("Tee", "", 0, 4.0, 20, 0, 0));
            w.Show("verkaufen"); Settle();
            Enter("kasse-find", "4006381333931"); Enter("kasse-find", "4006381333931");
            Assert.Equal([("Kaffee", 2m)], w.KasseCart); Assert.Equal("", w.Find<TextBox>("kasse-find").Text);
            Enter("kasse-discount", "10 %");
            Assert.Equal("18,00 €", w.Find<TextBlock>("kasse-total").Text); Assert.Contains("2,00", w.Find<TextBlock>("kasse-discount-info").Text);
            Enter("kasse-discount", "zehn"); Assert.Contains("Rabatt bitte", w.Find<TextBlock>("kasse-discount-info").Text);
            Enter("kasse-discount", "10 %");
            Click("kasse-pay-karte"); Click("kasse-checkout");
            Assert.Equal(18m, w.KasseLast!.Gross);
            // Auswertung
            Click("sell-tab-auswertung");
            Assert.Contains("18,00", string.Join("|", w.GetLogicalTexts()));
            Assert.NotNull(w.TryFind<StackPanel>("stat-top")); Assert.Contains($"Stoßzeit: {DateTime.Now.Hour}–", w.Find<TextBlock>("stat-peak").Text);
            Click("stat-days-7"); Assert.Equal(7, w.KasseStatDays); Assert.Equal(1, w.KasseReport().Bons); Assert.Equal(2m, w.KasseReport().Discounts);
            // Artikelnummer beim Anlegen
            Click("sell-tab-online");
            w.Find<TextBox>("sell-prod-name").Text = "Kakao"; w.Find<TextBox>("sell-prod-price").Text = "3"; w.Find<TextBox>("sell-prod-sku").Text = "4006381333932";
            Click("sell-prod-add"); Assert.DoesNotContain(k.Shop.Products.All(), p => p.Name == "Kakao");
            w.Find<TextBox>("sell-prod-name").Text = "Kakao"; w.Find<TextBox>("sell-prod-price").Text = "3"; w.Find<TextBox>("sell-prod-sku").Text = "96385074"; Click("sell-prod-add");
            Assert.Equal("96385074", k.Shop.Products.All().Single(p => p.Name == "Kakao").Sku);
        }
        finally { w.Close(); }
    }
}
