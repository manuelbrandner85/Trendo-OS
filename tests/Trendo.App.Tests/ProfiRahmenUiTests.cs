using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// Paket F1 (Profi-Bauplan A1, freigegeben 02.11.2026): Werkzeuge links, große Mitte, Eigenschaften rechts – jeder Rand einklappbar und gemerkt.
public class ProfiRahmen_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt")]
    public void Tools_rail_and_toggles()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("bild"); w.ProgramNew("bild"); w.Show("bild"); Pump();
            var rail = w.Find<Panel>("prog-tools");
            Assert.Equal(13, rail.Children.Count); // mit Vektorpinsel, Auswahl, Verlauf, Füllen, Reparieren, Klonen (04.11.2026)
            Assert.Null(w.TryFind<Control>("prog-ribbon")?.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "prog-cmd-img-tool-brush" && b.Parent is WrapPanel));
            w.Find<Button>("prog-cmd-img-tool-brush").Press(); Pump();
            Assert.Equal("brush", w.ImgTool);
            Assert.Contains("Pinsel", w.Find<TextBlock>("prog-status-text").Text);
            // T-0686 (V-03): „gewählt“ ist ein goldener Rand; golden gefüllt ist nur die Hauptaktion – „Speichern“ nicht mehr (früher: aktiv = Akzentfläche wie „Speichern“)
            var brush = w.Find<Button>("prog-cmd-img-tool-brush"); var other = w.Find<Button>("prog-cmd-img-tool-eraser");
            Assert.NotEqual(other.BorderBrush, brush.BorderBrush); Assert.NotEqual(other.Background, brush.Background);
            Assert.False(MainWindow.IsGold(brush)); Assert.False(MainWindow.IsGold(w.Find<Button>("prog-save")));

            w.Find<Button>("prog-toggle-werkzeuge").Press(); Pump();
            Assert.Null(w.TryFind<Panel>("prog-tools"));
            w.Find<Button>("prog-toggle-eigenschaften").Press(); Pump();
            Assert.Null(w.TryFind<Border>("prog-properties"));
            Assert.Contains("bild:werkzeuge", Trendo.Core.UserSettings.Load(w.Core.DataRoot).HiddenPanels); // gemerkt
            w.Show("settings"); w.Show("bild"); Pump();
            Assert.Null(w.TryFind<Panel>("prog-tools"));
            w.Find<Button>("prog-toggle-werkzeuge").Press(); w.Find<Button>("prog-toggle-eigenschaften").Press(); Pump();
            Assert.NotNull(w.TryFind<Panel>("prog-tools")); Assert.NotNull(w.TryFind<Border>("prog-properties"));
        }
        finally { w.Close(); }
    }
}
