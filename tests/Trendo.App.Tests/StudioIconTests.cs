using Avalonia.Headless.XUnit;
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
}
