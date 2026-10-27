using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class StudioIconTests
{
    private static List<StudioEntry> Studios() =>
        new StudioRegistry().Load(Path.Combine(AppContext.BaseDirectory, "requirements.yaml"), new Probe("browser")).ToList();

    [AvaloniaFact(DisplayName = "Symbole: jedes Studio aus requirements.yaml hat ein eigenes, eindeutiges Symbol")]
    public void Every_studio_has_unique_icon()
    {
        var studios = Studios();
        Assert.True(studios.Count >= 54, $"nur {studios.Count} Studios geladen");
        foreach (var s in studios) Assert.True(StudioIcons.For(s.Id) is not null, $"kein Symbol für {s.Id}");
        var icons = studios.Select(s => StudioIcons.For(s.Id)!).ToList();
        Assert.Equal(icons.Count, icons.Select(i => i.Fluent).Distinct().Count());
        Assert.Equal(icons.Count, icons.Select(i => i.Path).Distinct().Count());
    }

    [AvaloniaFact(DisplayName = "Symbole: alle Pfade sind gültige Avalonia-Geometrien mit Akzentfarben")]
    public void All_paths_parse()
    {
        foreach (var i in StudioIcons.ById.Values.Concat(StudioIcons.Areas.Values))
        {
            Assert.NotNull(Geometry.Parse(i.Path));
            Color.Parse(i.C1); Color.Parse(i.C2);
        }
    }

    [AvaloniaFact(DisplayName = "Symbole: jede Karte unter „Alle Studios“ und jeder Fach-Reiter zeigt sein Studio-Symbol")]
    public void Cards_and_tabs_show_icons()
    {
        var (w, _, _) = Ui.Open();
        w.Show("studios");
        var cards = w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("studio-card-") == true).ToList();
        Assert.True(cards.Count >= 54, $"nur {cards.Count} Karten");
        foreach (var c in cards)
        {
            var id = c.Name!["studio-card-".Length..];
            Assert.Contains(c.GetLogicalDescendants().OfType<Border>(), b => b.Name == "studio-icon-" + id);
        }
        // G3: Fachstudios zeigen keine Reiterleiste mehr mit allen anderen Fachstudios – ihre Symbole stehen auf den Karten oben
        w.Show(MainWindow.StudioPages["SPEC-16"]);
        Assert.Null(w.TryFind<Border>("fach-tabicon-"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Alle Studios: kurze deutsche Namen und ein Satz, nach Bereichen gruppiert, keine Kennungen oder Fachbegriffe sichtbar")]
    public void Cards_are_plain_german()
    {
        foreach (var s in Studios()) Assert.True(StudioCatalog.For(s.Id) is not null, $"kein Name für {s.Id}");
        var (w, _, _) = Ui.Open();
        w.Show("studios");
        var cards = w.GetLogicalDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("studio-card-") == true).ToList();
        Assert.True(cards.Count >= 54);
        var bad = new System.Text.RegularExpressions.Regex(@"\b(CORE|SYS|SPEC|X|STUDIO)-|APPROVED|REJECTED|Ultimate|GODMODE|\b2\.0\b|Fusion|Blockiert|Nicht verbunden|Benötigtes Programm");
        foreach (var c in cards)
            foreach (var t in c.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsVisible))
                Assert.False(bad.IsMatch(t.Text ?? ""), $"{c.Name}: „{t.Text}“");
        var heads = w.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Name?.StartsWith("studio-area-") == true).Select(t => t.Text).ToList();
        Assert.True(heads.Count >= 8, $"nur {heads.Count} Bereiche");
        Assert.Contains("Technik & Fachgebiete", heads);
        Assert.Equal("Auto & Fahrten", Avalonia.Automation.AutomationProperties.GetName(cards.Single(c => c.Name == "studio-card-SPEC-16")));
        w.Close();
    }
}
