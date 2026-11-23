using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Pod;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0753 Gegenprüfung in der Oberfläche: Kasse mit Varianten (M1), Fehlermeldung beim POD-Aktualisieren im Shop-Bereich (N1).</summary>
public class SellEditFix_Ui
{
    static void Click(MainWindow w, string name) { while (w.TryFind<Button>("toast-close") is { IsEffectivelyVisible: true } c) RealClick(w, c); var x = V<Control>(w, name); x.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }

    [AvaloniaFact(DisplayName = "M1 (UI): Kasse zeigt jede Variante als Kachel, Verkauf senkt den Bestand der Variante")]
    public void Kasse_variant_tiles()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Kasse.Shop.AddProduct(new Product("Shirt", "", 0, 0, 0, 0, 0, "", "", "", [new("M", 20, 3, "SH-M"), new("L", 22, 1, "SH-L")]));
            w.Show("verkaufen"); Settle();
            Assert.Contains(w.GetLogicalTexts(), t => t == "Shirt – L");
            Click(w, "kasse-art-1"); Click(w, "kasse-pay-karte"); Click(w, "kasse-checkout");
            Assert.Equal(22m, w.KasseLast!.Gross); // Preis der Variante L, nicht der ersten
            var p = w.Kasse.Shop.Products.All()[0];
            Assert.Equal((3, 0, 3), (p.Variants![0].Stock, p.Variants[1].Stock, p.Stock));
            Assert.False(w.Find<Button>("kasse-art-1").IsEnabled); // L ausverkauft
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "N1 (UI): „Artikel aktualisieren“ mit vorgemerkter Variante, die beim Anbieter wegfällt – Meldung steht im Shop-Bereich")]
    public void Pod_update_message()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.PodShopItems.Add(new("i", "Shirt Wald", 24.9m, "", 5, [new("S", 1), new("M", 2)]));
            w.PodShopItemToShop("i");
            var s = w.BizShop;
            s.AddCustomer(new BizContact("Erika Muster", "", "", "Kunde", null, 0));
            s.PlaceOrder("Erika Muster", DateOnly.FromDateTime(DateTime.Today), ("Shirt Wald – S", 1));
            w.PodShopItems.Replace(new("i", "Shirt Wald", 24.9m, "", 5, [new("M", 2)]));
            w.Show("verkaufen"); Settle(); Click(w, "sell-tab-online");
            Click(w, "shop-takeover-0");
            Assert.Contains("vorgemerkt", w.Find<TextBlock>("shop-msg").Text);
        }
        finally { w.Close(); }
    }
}
