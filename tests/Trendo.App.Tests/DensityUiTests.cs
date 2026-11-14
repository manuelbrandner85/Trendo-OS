using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>P4: Dichte „Kompakt“ verkleinert auch Innenabstände, die im Code gesetzt sind, und „Normal“ holt sie genau zurück.</summary>
public class Density_Ui
{
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }

    [AvaloniaFact(DisplayName = "Dichte: Kompakt verkleinert Innenabstände von Knöpfen, Normal stellt sie wieder her, Einstellung bleibt gespeichert")]
    public void Kompakt_und_zurueck()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("settings"); Settle();
            Assert.Equal(new Thickness(12, 6), w.Find<Button>("density-kompakt").Padding);
            Tap(w, "density-kompakt");
            Assert.Equal("kompakt", w.Settings.Density);
            Assert.True(w.Classes.Contains("kompakt"));
            Assert.Equal(Density.Shrink(new Thickness(12, 6)), w.Find<Button>("density-normal").Padding);
            Assert.Equal(new Thickness(8, 4), Density.Shrink(new Thickness(12, 6)));
            Tap(w, "density-normal");
            Assert.Equal("normal", w.Settings.Density);
            Assert.False(w.Classes.Contains("kompakt"));
            Assert.Equal(new Thickness(12, 6), w.Find<Button>("density-kompakt").Padding);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Dichte: zweimal Kompakt verkleinert nicht doppelt")]
    public void Nicht_doppelt()
    {
        var b = new Button { Padding = new Thickness(10) };
        var host = new StackPanel { Children = { b } };
        Density.Apply(host, true); Density.Apply(host, true);
        Assert.Equal(new Thickness(7), b.Padding); // 6,5 → 7 (gerundet), nur einmal verkleinert
        Density.Apply(host, false);
        Assert.Equal(new Thickness(10), b.Padding);
    }
}
