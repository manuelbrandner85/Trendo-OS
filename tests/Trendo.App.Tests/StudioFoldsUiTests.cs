using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Aufgeräumte Studio-Liste: Zusammengelegte Studios bleiben erreichbar.</summary>
public class StudioFolds_Ui
{
    [Fact(DisplayName = "Studio-Ordnung: 37 Studios in der Übersicht; jedes Ziel einer Zusammenlegung existiert und bleibt selbst sichtbar")]
    public void Plan_is_consistent()
    {
        Assert.Equal(37, StudioCatalog.ById.Keys.Count(StudioCatalog.InOverview));
        Assert.Equal(18, StudioCatalog.FoldedInto.Count);
        foreach (var (id, host) in StudioCatalog.FoldedInto)
        {
            Assert.NotNull(StudioCatalog.For(id));
            if (host != StudioCatalog.SettingsHost) Assert.True(StudioCatalog.InOverview(host), id + " -> " + host);
        }
    }

    [AvaloniaFact(DisplayName = "Studio-Ordnung: Zusammengelegtes fehlt in der Übersicht, Suche findet es, SPEC-07 nirgends")]
    public void Overview_and_search()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Show("studios"); Settle();
        Assert.Equal("37 Studios", w.Find<TextBlock>("studios-count").Text![..10]);
        w.Find<TextBox>("txt-studio-search").Text = "Kostenlose KI finden"; Settle();
        Assert.StartsWith("1 Studios", w.Find<TextBlock>("studios-count").Text);
        w.Find<TextBox>("txt-studio-search").Text = "Texte übersetzen"; Settle();
        Assert.StartsWith("0 Studios", w.Find<TextBlock>("studios-count").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Studio-Ordnung: Das aufnehmende Studio und die Einstellungen zeigen die Leiste, der Knopf öffnet den Bereich")]
    public void Host_strip_opens_page()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Show(w.PageForStudio("CORE-04")!); Settle();
        Assert.NotNull(w.GetLogicalDescendants().OfType<DockPanel>().FirstOrDefault(d => d.Name == "fold-strip"));
        RealClick(w, w.Find<Button>("fold-open-X-08")); Settle();
        Assert.Equal(w.PageForStudio("X-08"), w.CurrentView);

        w.Show("settings"); Settle();
        Assert.NotNull(w.TryFind<Button>("fold-open-X-09"));
        Assert.NotNull(w.TryFind<Button>("fold-open-SYS-13"));
        w.Close();
    }
}
