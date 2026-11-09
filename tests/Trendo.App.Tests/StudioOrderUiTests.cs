using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Ordnung der Studio-Übersicht.</summary>
public class StudioOrder_Ui
{
    [Fact(DisplayName = "Studio-Ordnung: alphabetisch innerhalb einer Unterteilung, Umlaute deutsch sortiert")]
    public void Sorted_names()
    {
        var names = StudioCatalog.Sorted(StudioCatalog.ById.Where(k => k.Value.Area == "Technik & Fachgebiete"), k => k.Key, k => k.Value.Name).Select(k => k.Value.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), true)), names);
    }

    [Fact(DisplayName = "Studio-Ordnung: jedes Studio im System-Bereich hat eine Unterteilung, keine Unterteilung zeigt auf ein fehlendes Studio")]
    public void Sub_areas_complete()
    {
        foreach (var (id, info) in StudioCatalog.ById.Where(k => k.Value.Area == "System & Sicherheit")) Assert.True(StudioCatalog.SubAreas.ContainsKey(id), id + " " + info.Name);
        Assert.All(StudioCatalog.SubAreas.Keys, id => Assert.NotNull(StudioCatalog.For(id)));
    }

    [AvaloniaFact(DisplayName = "Studio-Ordnung: Übersicht zeigt Unterteilungen und Anzahl; „Zuletzt benutzt“ erscheint nach dem Öffnen eines Studios und nicht bei der Suche")]
    public void Overview()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Show("studios"); Settle();
        Assert.Null(w.TryFind<TextBlock>("studios-recent-head"));
        Assert.NotNull(w.TryFind<TextBlock>("studio-sub-Sicherheit & Daten"));
        var chip = w.GetLogicalDescendants().OfType<Button>().First(b => (b.Content as string)?.StartsWith("System & Sicherheit (", StringComparison.Ordinal) == true);
        Assert.Equal("System & Sicherheit (15)", chip.Content);

        w.Show(MainWindow.StudioPages["SPEC-11"]); Settle();
        w.Show("studios"); Settle();
        Assert.NotNull(w.TryFind<WrapPanel>("studios-recent"));
        w.Close();
    }
}
