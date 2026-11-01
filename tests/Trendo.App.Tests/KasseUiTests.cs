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
}
