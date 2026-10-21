using Trendo.Core;
using Xunit;

public class W5_Bus_Brand
{
    [Fact(DisplayName = "BUS-013: Namensprüfung, Farbpalette (Gegenfarbe stimmt), Kontrast nach WCAG, CSS")]
    public void Brand()
    {
        Assert.True(BusinessBrand.CheckName("Sitterly").Score >= 90);
        var bad = BusinessBrand.CheckName("xkrzt-2000-strmpfbndr");
        Assert.True(bad.Score < 60); Assert.Contains("sitterly.de", BusinessBrand.CheckName("Sitterly").Domains);
        Assert.Equal("#FF0000", BusinessBrand.Hex(0, 1, 0.5)); Assert.Equal("#00FFFF", BusinessBrand.Palette("#FF0000")[1].Hex);
        Assert.Equal(21, BusinessBrand.Contrast("#000000", "#FFFFFF"), 1); Assert.Equal(1, BusinessBrand.Contrast("#777777", "#777777"), 3);
        var p = BusinessBrand.Palette("#2F86F6"); Assert.Equal(6, p.Count); Assert.True(BusinessBrand.Contrast(p[4].Hex, p[5].Hex) >= 4.5);
        Assert.Contains("--hauptfarbe: #2F86F6;", BusinessBrand.BrandCss(p));
        Assert.Equal("#2F86F6", BusinessBrand.Palette("rot")[0].Hex);
    }

    [Fact(DisplayName = "BUS-015/021/022: Kundenweg mit Lücken und Ablaufdiagramm, Suchmaschinen-Prüfung, Trichter mit Engpass")]
    public void Journey_Seo_Funnel()
    {
        var md = BusinessBrand.JourneyMarkdown(new() { Name = "X" }, [new("Entdecken", "Instagram", "neugierig", "mehr Videos")], [new("Kunde", "bucht"), new("Ich", "bestätigt")]);
        Assert.Contains("| Entdecken | Instagram |", md); Assert.Contains("Noch leer: Vergleichen", md); Assert.Contains("S0 --> S1", md);
        Assert.Equal("hundesitter-muenchen", BusinessBrand.Slug("Hundesitter München!"));
        var s = BusinessBrand.Seo("hundesitter berlin", "Sitterly", "Hundesitter Berlin für Berufstätige. Wir finden Sitter. " + string.Join(' ', Enumerable.Repeat("Hund Spaziergang Pflege", 120)));
        Assert.Equal("hundesitter berlin | Sitterly", s.Title); Assert.True(s.Density > 0 && s.Density < 3); Assert.Contains("Sieht gut aus.", s.Hints);
        Assert.Contains(BusinessBrand.Seo("katze", "B", "Kurz.").Hints, h => h.Contains("kommt im Text nicht vor"));
        var f = BusinessBrand.Funnel([new("Besucher", 1000), new("Interessenten", 100), new("Kunden", 50)], 20);
        Assert.Contains("Interessenten", f.Bottleneck); Assert.Equal(1000, f.Revenue); Assert.Equal(0.5, f.Stages[2].Rate);
    }
}
