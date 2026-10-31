using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Masterprompt 131 / 19: Programmkatalog – jedes geöffnete Programm steht im mitgelieferten Katalog, Suche, Bereichsfilter, „Öffnen“ startet das echte Programm.</summary>
public class ProgramCatalog_Ui
{
    [AvaloniaFact(DisplayName = "Programmkatalog UI: mitgelieferter Katalog ist aktuell (alle angemeldeten Programme), Suche nach Format, Bereich filtern, Öffnen startet das Programm")]
    public void Catalog_page()
    {
        var (w, _, _) = Ui.Open();
        var shipped = ProgramCatalog.Load().Select(e => e.Id).ToHashSet();
        var missing = w.ProgramCatalogRuntime().Where(e => !shipped.Contains(e.Id)).Select(e => e.Id).ToList();
        Assert.True(missing.Count == 0, "Katalog neu erzeugen (TRENDO_KATALOG), fehlt: " + string.Join(", ", missing));
        w.Show("programmkatalog"); Settle();
        Assert.Contains("Programmen", w.Find<TextBlock>("cat-summary").Text);
        var q = w.Find<TextBox>("cat-q"); q.Text = ".gpx"; RealClick(w, w.Find<Button>("cat-search")); Settle();
        Assert.Contains("Karten & Touren", Texts(w));
        Assert.DoesNotContain("Trendo Tabelle", Texts(w));
        w.Find<TextBox>("cat-q").Text = ""; RealClick(w, w.Find<Button>("cat-search")); Settle();
        RealClick(w, w.Find<Button>("cat-area-" + Array.IndexOf(StudioCatalog.Areas, "Technik & Fachgebiete"))); Settle();
        Assert.Contains("Grundriss & Wohnen", Texts(w)); Assert.DoesNotContain("Trendo Text", Texts(w));
        var idx = Enumerable.Range(0, 60).First(i => w.TryFind<Border>($"cat-item-{i}") is { } b && Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(b).OfType<TextBlock>().Any(t => t.Text == "Grundriss & Wohnen"));
        var open = w.Find<Button>($"cat-open-{idx}"); open.BringIntoView(); Settle(); RealClick(w, open); Settle();
        Assert.NotNull(w.TryFind<WrapPanel>("prog-templates")); // echtes Programm (Startseite der Programm-Hülle), keine Infoseite
    }

    static string Texts(MainWindow w) => string.Join("\n", Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w.Find<WrapPanel>("cat-list")).OfType<TextBlock>().Select(t => t.Text));
}
