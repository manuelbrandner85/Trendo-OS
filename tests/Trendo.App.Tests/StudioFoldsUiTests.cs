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
    [Fact(DisplayName = "Studio-Ordnung: 49 Studios in der Übersicht; jedes Ziel einer Zusammenlegung existiert und bleibt selbst sichtbar")]
    public void Plan_is_consistent()
    {
        Assert.Equal(71, StudioCatalog.ById.Keys.Count(StudioCatalog.InOverview)); // + Stammbaum (T-0788), + Rezepte & Ernährung (T-0787), + acht Studios Wissen & Lernen (T-0779 Quiz/Kurse, T-0780 Referate/Zeitleisten, T-0781 Mathe/Naturwissenschaft, T-0782 Nachhilfe/Programmieren lernen; T-0784), + sieben Büro-Studios, + Künstlerstudio (15.11.2026)
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
        Assert.Equal("71 Studios", w.Find<TextBlock>("studios-count").Text![..10]); // T-0787: + Rezepte & Ernährung; T-0788: + Stammbaum; T-0784: acht neue Studios; zählt erst mit den Einträgen in requirements.yaml
        w.Find<TextBox>("txt-studio-search").Text = "Kostenlose KI finden"; Settle();
        Assert.StartsWith("1 Studios", w.Find<TextBlock>("studios-count").Text);
        w.Find<TextBox>("txt-studio-search").Text = "Texte übersetzen"; Settle();
        Assert.StartsWith("0 Studios", w.Find<TextBlock>("studios-count").Text);
        w.Close();
    }

    /// <summary>Einträge „Verwandte Programme“ im Menü „Mehr“ des Seitenkopfs (liegen erst nach dem Öffnen im Baum).</summary>
    static List<Button> Related(MainWindow w)
    {
        var more = w.ActivePageRoot!.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "page-head-more");
        return more.Flyout is Flyout { Content: Control c } ? c.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("fold-open-", StringComparison.Ordinal) == true).ToList() : [];
    }

    // T-0684 V-13 (freigegeben 16.11.2026): Die Zeile „Gehört hierher:“ über der Seite ist ins Menü „Mehr“ gewandert („Verwandte Programme“).
    // Der Test hält weiter fest, dass jeder eingegliederte Bereich vom aufnehmenden Studio aus mit einem Klick erreichbar ist.
    [AvaloniaFact(DisplayName = "Studio-Ordnung: Das aufnehmende Studio und die Einstellungen führen die Bereiche unter „Mehr“ → „Verwandte Programme“, der Eintrag öffnet den Bereich")]
    public void Host_strip_opens_page()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Show(w.PageForStudio("CORE-04")!); Settle();
        RealClick(w, w.ActivePageRoot!.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "page-head-more")); Settle(); // „Mehr“ ist sichtbar und anklickbar
        Related(w).Single(b => b.Name == "fold-open-X-08").Press(); Settle();
        Assert.Equal(w.PageForStudio("X-08"), w.CurrentView);

        w.Show("settings"); Settle();
        Assert.Contains(Related(w), b => b.Name == "fold-open-X-09");
        Assert.Contains(Related(w), b => b.Name == "fold-open-SYS-13");
        w.Close();
    }
}
