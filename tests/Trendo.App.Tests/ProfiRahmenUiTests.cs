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
            var rail = w.Find<StackPanel>("prog-tools");
            Assert.Equal(7, rail.Children.Count);
            Assert.Null(w.TryFind<Control>("prog-ribbon")?.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "prog-cmd-img-tool-brush" && b.Parent is WrapPanel));
            w.Find<Button>("prog-cmd-img-tool-brush").Press(); Pump();
            Assert.Equal("brush", w.ImgTool);
            Assert.Contains("Pinsel", w.Find<TextBlock>("prog-status-text").Text);
            Assert.Equal(w.Find<Button>("prog-cmd-img-tool-brush").Background, w.TryFind<Button>("prog-save")!.Background); // aktiv = Akzentfarbe wie der Hauptknopf

            w.Find<Button>("prog-toggle-werkzeuge").Press(); Pump();
            Assert.Null(w.TryFind<StackPanel>("prog-tools"));
            w.Find<Button>("prog-toggle-eigenschaften").Press(); Pump();
            Assert.Null(w.TryFind<Border>("prog-properties"));
            Assert.Contains("bild:werkzeuge", Trendo.Core.UserSettings.Load(w.Core.DataRoot).HiddenPanels); // gemerkt
            w.Show("settings"); w.Show("bild"); Pump();
            Assert.Null(w.TryFind<StackPanel>("prog-tools"));
            w.Find<Button>("prog-toggle-werkzeuge").Press(); w.Find<Button>("prog-toggle-eigenschaften").Press(); Pump();
            Assert.NotNull(w.TryFind<StackPanel>("prog-tools")); Assert.NotNull(w.TryFind<Border>("prog-properties"));
        }
        finally { w.Close(); }
    }
}
