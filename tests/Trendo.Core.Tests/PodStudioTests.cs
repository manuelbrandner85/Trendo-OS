using Trendo.Core.Pod;
using Xunit;

namespace Trendo.Core.Tests;

public class PodStudioTests
{
    static PodPriceInput In(decimal vat = 19) => new(12m, 4m, 6.5m, 0.2m, vat);

    [Fact(DisplayName = "POD P3: Druck-Auflösung und Füllgrad werden richtig gerechnet")]
    public void Dpi_and_coverage()
    {
        var shirt = PodCheck.Product("tshirt");
        var r = PodCheck.Check(shirt, 3543, 4724, true); // genau 30 × 40 cm bei 300 DPI
        Assert.InRange(r.Dpi, 299.9, 300.1);
        Assert.InRange(r.Coverage, 0.99, 1.01);
        Assert.Equal(PodLevel.Gut, r.Level);
    }

    [Fact(DisplayName = "POD P3: zu klein ist ein Problem, mittlere Auflösung und fehlende Transparenz sind zu prüfen")]
    public void Levels()
    {
        var shirt = PodCheck.Product("tshirt");
        var small = PodCheck.Check(shirt, 800, 1000, true);
        Assert.Equal(PodLevel.Problem, small.Level);
        Assert.Contains(small.Findings, f => f.Text.Contains("unscharf"));
        var mid = PodCheck.Check(shirt, 2400, 3200, false);
        Assert.Equal(PodLevel.Pruefen, mid.Level);
        Assert.Contains(mid.Findings, f => f.Text.Contains("Rechteck"));
        Assert.Equal(PodLevel.Problem, PodCheck.Check(shirt, 0, 0, false).Level);
    }

    [Fact(DisplayName = "POD P3: Poster braucht keine Transparenz, meldet aber ein falsches Seitenverhältnis")]
    public void Poster_ratio()
    {
        var poster = PodCheck.Product("poster");
        Assert.DoesNotContain(PodCheck.Check(poster, 3543, 4724, false).Findings, f => f.Text.Contains("durchsichtig"));
        Assert.Contains(PodCheck.Check(poster, 4724, 4724, false).Findings, f => f.Text.Contains("Seitenverhältnis"));
    }

    [Fact(DisplayName = "POD P3: Transparenz wird aus dem PNG-Kopf erkannt, JPEG hat keine")]
    public void Alpha()
    {
        static byte[] Png(byte colorType, params byte[] tail)
        {
            byte[] head = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 1, 0, 0, 0, 1, 8, colorType, 0, 0, 0, 0, 0, 0, 0];
            return [.. head, .. tail];
        }
        Assert.True(PodCheck.HasAlpha(Png(6)));
        Assert.False(PodCheck.HasAlpha(Png(2, 0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0, 0, 0, 0)));
        Assert.True(PodCheck.HasAlpha(Png(3, 0, 0, 0, 1, (byte)'t', (byte)'R', (byte)'N', (byte)'S', 0, 0, 0, 0, 0)));
        Assert.False(PodCheck.HasAlpha([0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact(DisplayName = "POD P6: Gewinn je Stück mit Steuer und Gebühren, 0 % Steuer wie Kleinunternehmer")]
    public void Price()
    {
        var r = PodPrice.Calc(In(), 29.99m);
        Assert.Equal(25.20m, r.Net);
        Assert.Equal(4.79m, r.Vat);
        Assert.Equal(2.15m, r.Fees);
        Assert.Equal(7.05m, r.Profit);
        Assert.Equal(11.84m, PodPrice.Calc(In(0), 29.99m).Profit);
    }

    [Fact(DisplayName = "POD P6: Preis für Wunschgewinn ergibt mindestens diesen Gewinn; unmögliche Gebühren ergeben keinen Preis")]
    public void Target_price()
    {
        var g = PodPrice.ForProfit(In(), 8m)!.Value;
        var p = PodPrice.Calc(In(), g).Profit;
        Assert.InRange(p, 8m, 8.02m);
        Assert.Null(PodPrice.ForProfit(new(1, 1, 90, 0, 19), 5));
    }

    [Fact(DisplayName = "POD P7: bekannte Marken werden auch in anderer Schreibweise gefunden, Wortteile nicht")]
    public void Marks()
    {
        Assert.Equal(["Nike", "Star Wars"], PodLegal.MarkHits("Cooles NIKE Shirt im star wars Stil"));
        Assert.Empty(PodLegal.MarkHits("Der Spike ist legotisch"));
        Assert.Empty(PodLegal.MarkHits(null));
        Assert.Contains("https://", PodLegal.DpmaSearch);
    }
}
