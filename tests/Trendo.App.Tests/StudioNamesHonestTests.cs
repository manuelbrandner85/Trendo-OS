using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0213: ehrliche Studionamen, SPEC-07 ausgeblendet (bleibt im Katalog).</summary>
public class StudioNamesHonestTests
{
    [Theory(DisplayName = "T-0213: Studionamen versprechen nicht mehr, als das Studio kann")]
    [InlineData("CORE-06", "Spiele-Vorlagen")]
    [InlineData("SPEC-15", "Livestream planen")]
    [InlineData("CORE-09", "Kamerabilder auswerten")]
    [InlineData("SPEC-09", "Scans bearbeiten")]
    [InlineData("CORE-07", "Webseite aus Vorlage")]
    [InlineData("CORE-05", "3D-Modelle erstellen")]
    public void Namen(string id, string name) => Assert.Equal(name, StudioCatalog.For(id)!.Name);

    [Fact(DisplayName = "T-0213: SPEC-07 bleibt im Katalog, ist aber ausgeblendet")]
    public void Spec07_ausgeblendet()
    {
        Assert.True(StudioCatalog.ById.ContainsKey("SPEC-07"));
        Assert.True(StudioCatalog.IsHidden("SPEC-07"));
        Assert.Single(StudioCatalog.ById, e => e.Value.Hidden);
    }
}
