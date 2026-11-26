using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0781, 3. Prüfrunde: langsam konvergente uneigentliche Integrale (vorher fälschlich „existiert nicht“), Divergenz weiter erkannt,
/// schmale Definitionslücke am Rand als „nicht überall definiert“ statt „divergiert“.</summary>
public class T0781Runde3Tests(ITestOutputHelper log)
{
    Loesung I(string f, string von, string bis)
    {
        var l = MatheKern.Rechne(new MatheAuftrag(MatheArt.Integrieren, f, null, von, bis));
        log.WriteLine(l.Aufgabe + " → " + l.Ergebnis);
        foreach (var s in l.Schritte) log.WriteLine("  · " + s.Text + ": " + s.Formel);
        foreach (var p in l.Proben) log.WriteLine("  Probe: " + p.Text);
        return l;
    }

    [Theory(DisplayName = "T-0781 R3 HOCH: langsam konvergente uneigentliche Integrale haben einen Wert (Test rechnet selbst: ∫₀¹ x^a = 1/(a+1))")]
    [InlineData("x^(-2/3)", "-1", "1", 6)]
    [InlineData("x^(-2/3)", "0", "1", 3)]
    [InlineData("x^(-3/4)", "0", "1", 4)]
    [InlineData("x^(-0.9)", "0", "1", 10)]
    [InlineData("5*x^(-3/4)", "0", "1", 20)]
    public void Konvergent(string f, string von, string bis, double soll)
    {
        var l = I(f, von, bis);
        Assert.DoesNotContain("existiert nicht", l.Ergebnis);
        Assert.DoesNotContain("unsicher", l.Ergebnis);
        Assert.EndsWith("= " + soll.ToString(System.Globalization.CultureInfo.InvariantCulture), l.Ergebnis);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
    }

    [Theory(DisplayName = "T-0781 R3 HOCH: weiterhin divergent – ∫1/x, ∫1/x², ∫x^(−1) von 0 bis 1")]
    [InlineData("1/x")]
    [InlineData("1/x^2")]
    [InlineData("x^(-1)")]
    [InlineData("x^(-1.5)")]
    public void Divergent(string f)
    {
        var l = I(f, "0", "1");
        Assert.Contains("existiert nicht", l.Ergebnis);
        Assert.Contains("divergiert", l.Ergebnis);
    }

    [Fact(DisplayName = "T-0781 R3 HOCH: ohne Potenzregel (Extrapolation): ∫ x^(−2/3) + x^(1/3) von 0 bis 1 = 15/4 – kein „existiert nicht“")]
    public void Extrapolation_ohne_potenzregel()
    {
        var l = I("x^(-2/3) + x^(1/3)", "0", "1");
        Assert.DoesNotContain("existiert nicht", l.Ergebnis);
        Assert.Contains("= 15/4", l.Ergebnis); // 3 + 3/4
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
    }

    [Theory(DisplayName = "T-0781 R3 NIEDRIG: schmale Definitionslücke am Rand → „nicht überall definiert“, nicht „divergiert“")]
    [InlineData("sqrt(x)", "-0.0001", "1")]
    [InlineData("ln(x)", "-0.000001", "1")]
    [InlineData("sqrt(1-x)", "0", "1.000001")]
    public void Schmale_luecke_am_rand(string f, string von, string bis)
    {
        var l = I(f, von, bis);
        Assert.Contains("nicht überall definiert", l.Ergebnis);
        Assert.DoesNotContain("divergiert", l.Ergebnis);
    }
}
