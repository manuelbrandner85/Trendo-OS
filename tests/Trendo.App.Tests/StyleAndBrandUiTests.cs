using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

/// Wunsch 02.11.2026: Bedienstil wie Windows, Mac oder Linux wählbar; jeder darf Trendo erweitern – nur Name und Logo bleiben fest.
public class StyleAndBrand_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Bedienstil: in den Einstellungen wählbar; Mac setzt runde Ampel-Knöpfe links (Schließen zuerst), Linux runde Knöpfe rechts – Knöpfe funktionieren weiter")]
    public void Styles()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("settings"); Pump();
            foreach (var id in UserSettings.Styles) Assert.NotNull(w.TryFind<Button>("style-" + id));
            w.Find<Button>("style-mac").Press(); Pump();
            var win = w.ReopenedAs ?? w;
            Assert.Equal("mac", MainWindow.StyleNow);
            win.Show("brief"); Pump();
            var id2 = win.Windows.Active!.Id;
            var caps = win.Find<StackPanel>("win-caps-" + id2);
            Assert.Equal("win-close-" + id2, caps.Children[0].Name); // Mac: Schließen links zuerst
            Assert.Equal(14, ((Button)caps.Children[0]).Width);
            ((Button)caps.Children[2]).Press(); Pump(); // grüner Knopf = maximieren
            Assert.True(win.Windows.Get(id2)!.Maximized);
            win.Show("settings"); Pump();
            win.Find<Button>("style-linux").Press(); Pump();
            var w3 = win.ReopenedAs ?? win;
            Assert.Equal("linux", MainWindow.StyleNow);
            w3.Show("brief"); Pump();
            var caps3 = w3.Find<StackPanel>("win-caps-" + w3.Windows.Active!.Id);
            Assert.EndsWith(w3.Windows.Active!.Id, caps3.Children[2].Name); Assert.StartsWith("win-close-", caps3.Children[2].Name); // Linux: rechts, Schließen zuletzt
            w3.Show("settings"); Pump(); w3.Find<Button>("style-trendo").Press(); Pump();
            Assert.Equal("trendo", MainWindow.StyleNow);
            (w3.ReopenedAs ?? w3).Close();
        }
        finally { try { w.Close(); } catch (InvalidOperationException) { } }
    }

    [Fact(DisplayName = "Trendo erweitern: Erweiterungen dürfen alles außer Name und Logo – Produktname, Marke oder Logo-Datei werden abgelehnt")]
    public void Brand_locked()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ext-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, Extensions.ManifestFile), """{"id":"meine.seite","name":"Meine Seite","version":"1.0.0","author":"Erika Muster","description":"Eigene Startseite","kind":"seite","permissions":[],"entry":"seite.json","license":"MIT"}""");
            File.WriteAllText(Path.Combine(dir, "seite.json"), """{"title":"Willkommen","blocks":[{"text":"Hallo"}]}""");
            Assert.Empty(Extensions.Validate(dir).Errors);
            File.WriteAllText(Path.Combine(dir, "seite.json"), """{"title":"Willkommen","settings":{"productName":"Mein OS"}}""");
            Assert.Contains(Extensions.Validate(dir).Errors, e => e.Contains("Name und Logo von Trendo OS"));
            File.WriteAllText(Path.Combine(dir, "seite.json"), """{"title":"Willkommen"}""");
            File.WriteAllBytes(Path.Combine(dir, "trendo-logo.png"), [0x89, 0x50, 0x4E, 0x47]);
            Assert.Contains(Extensions.Validate(dir).Errors, e => e.Contains("Name und Logo"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
