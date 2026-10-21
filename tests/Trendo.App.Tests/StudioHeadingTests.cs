using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Seitenüberschrift einer Studio-Seite ist der Name aus StudioCatalog, nicht ein zweiter fest eingetragener Text.</summary>
public class StudioHeadingTests
{
    [Fact(DisplayName = "StudioCatalog.NameOf: Katalogname, sonst der bisherige Text")]
    public void NameOf_uses_catalog_or_fallback()
    {
        Assert.Equal("3D-Modelle erstellen", StudioCatalog.NameOf("CORE-05", "3D-Studio"));
        Assert.Equal("Markt", StudioCatalog.NameOf("BUS-003", "Markt"));
        Assert.Equal("bleibt", StudioCatalog.NameOf(null, "bleibt"));
        Assert.Equal(StudioCatalog.ById["CORE-05"].Name, StudioCatalog.NameOf("CORE-05", "anders"));
    }

    [AvaloniaFact(DisplayName = "Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog")]
    public void Heading_matches_catalog()
    {
        var (w, _, _) = Ui.Open();
        var pages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, key) in MainWindow.StudioPages)
            if (StudioCatalog.ById.ContainsKey(id)) pages[key] = id;
        foreach (var (key, id) in w.PageModules)
            if (StudioCatalog.ById.ContainsKey(id)) pages[key] = id;
        // Einstiege, die PageForStudio zuordnet, ohne RegisterTab
        foreach (var key in new[] { "brief", "akten", "akte", "ausgabe", "briefkoepfe" }) pages[key] = "STUDIO-LETTER-01";
        pages["office"] = "SYS-10";

        Assert.True(pages.Count >= 50, $"nur {pages.Count} Studio-Seiten");
        Assert.Equal("CORE-05", pages["3d"]);
        foreach (var (key, id) in pages)
        {
            w.Show(key);
            var title = Heading(w);
            Assert.True(title == StudioCatalog.ById[id].Name, $"{key} ({id}): „{title}“ statt „{StudioCatalog.ById[id].Name}“");
        }

        // Seiten ohne Studio und Studios ohne Katalogeintrag behalten ihre Überschrift. Reiter bleiben kurz.
        w.Show("projects"); Assert.Equal("Projekte", Heading(w));
        w.Show("settings"); Assert.Equal("Einstellungen", Heading(w));
        w.Show("ki"); Assert.Equal("KI", Heading(w));
        w.Show("apps"); Assert.Equal("Apps", Heading(w));
        w.Show("biz-markt"); Assert.Equal("Markt", Heading(w));
        w.Show("3d");
        var tab = w.Find<Button>("kreativ-tab-3d").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("3D", tab);
        Assert.DoesNotContain("3D-Modelle erstellen", tab);
        w.Show("architektur");
        var fach = w.Find<Button>("fach-tab-architektur").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains(StudioCatalog.ById["SPEC-01"].Name, fach);
        w.Close();
    }

    private static string Heading(MainWindow w) =>
        w.ActivePageRoot!.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "page-title").Text!;
}
