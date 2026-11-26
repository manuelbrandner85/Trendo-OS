using System.Diagnostics;
using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0781, 2. Prüfrunde: Regressionstests je Befund (N1 Simpson/Definitionslücken, N2 °C-Differenz, e-Schreibweise, Niedrig).</summary>
public class T0781Runde2Tests(ITestOutputHelper log)
{
    static Loesung R(MatheArt art, string e, string? v = null, string? von = null, string? bis = null) => MatheKern.Rechne(new MatheAuftrag(art, e, v, von, bis));
    void Zeige(Loesung l) { log.WriteLine(l.Aufgabe + " → " + l.Ergebnis); foreach (var s in l.Schritte) log.WriteLine("  · " + s.Text + ": " + s.Formel); foreach (var p in l.Proben) log.WriteLine("  Probe: " + p.Text); }

    [Theory(DisplayName = "T-0781 R2 N1: ∫√x und ∫ln x von −1 bis 1 ehrlich „nicht definiert“, schnell (vorher: Simpson mit NaN hing)")]
    [InlineData("sqrt(x)")]
    [InlineData("ln(x)")]
    [InlineData("1/sqrt(x)")]
    public void Nicht_definiert(string f)
    {
        var sw = Stopwatch.StartNew();
        var l = R(MatheArt.Integrieren, f, null, "-1", "1"); Zeige(l);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"{sw.ElapsedMilliseconds} ms");
        Assert.Contains("nicht definiert auf dem ganzen Intervall", l.Ergebnis);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
    }

    [Theory(DisplayName = "T-0781 R2 N1: ∫x^(1/3) und ∫x^(−1/3) von −1 bis 1 = 0 (reelle Kubikwurzel, ungerade Funktion; Grenzwert an der Lücke)")]
    [InlineData("x^(1/3)")]
    [InlineData("x^(-1/3)")]
    public void Kubikwurzel(string f)
    {
        var sw = Stopwatch.StartNew();
        var l = R(MatheArt.Integrieren, f, null, "-1", "1"); Zeige(l);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"{sw.ElapsedMilliseconds} ms");
        Assert.EndsWith("= 0", l.Ergebnis);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
        // Test rechnet selbst: ∫₀¹ x^(1/3) = 3/4, ∫₋₁⁰ = −3/4
        var halb = R(MatheArt.Integrieren, f, null, "0", "1"); Zeige(halb);
        Assert.Contains(f == "x^(1/3)" ? "3/4" : "3/2", halb.Ergebnis);
    }

    [Fact(DisplayName = "T-0781 R2 N2: „30 °C − 20 °C in °C“ ist die Differenz 10 °C (nicht −263,15 °C)")]
    public void Differenz_in_celsius()
    {
        var r = Einheiten.Ausrechnen("30 °C - 20 °C in °C");
        Assert.Equal("°C", r.Einheit); Assert.Equal("10 °C", r.Text);
        Assert.True(r.Proben.All(p => p.Ok), string.Join(" | ", r.Proben.Select(p => p.Text)));
        Assert.Equal("18 °F", Einheiten.Ausrechnen("30 °C - 20 °C in °F").Text);
    }

    [Fact(DisplayName = "T-0781 R2 e-Schreibweise eindeutig: „2e + 3“ = 2·e + 3, „2e+3“ = 2000 mit Hinweis-Schritt, „2e3x“ mehrdeutig")]
    public void E_schreibweise()
    {
        Assert.Equal(2 * Math.E + 3, Auswertung.Wert(TermLeser.Lies("2e + 3")), 12);
        Assert.Equal(2000, Auswertung.Wert(TermLeser.Lies("2e+3")), 12);
        var l = R(MatheArt.Gleichung, "x = 2e+3 + 1"); Zeige(l);
        Assert.Contains(l.Schritte, s => s.Text == "Hinweis zur Schreibweise" && s.Formel.Contains("Zehnerpotenz"));
        Assert.Contains("mehrdeutig", Assert.Throws<FormatException>(() => TermLeser.Lies("2e3x")).Message);
        Assert.Equal(3e5, Auswertung.Wert(TermLeser.Lies("3e5")), 6);
        Assert.DoesNotContain(R(MatheArt.Vereinfachen, "2e + 3").Schritte, s => s.Text.StartsWith("Hinweis"));
    }

    [Fact(DisplayName = "T-0781 R2 Niedrig: x^(1/3) = −2 → x = −8; deutsche Meldung bei riesiger Hochzahl; große Terme brechen in der Zeit ab")]
    public void Niedrig()
    {
        var l = R(MatheArt.Gleichung, "x^(1/3) = -2"); Zeige(l);
        Assert.Equal("x = −8", l.Ergebnis.Replace(" (unsicher – siehe Probe)", "")); Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
        var ex = Assert.ThrowsAny<ArgumentException>(() => Einheiten.Ausrechnen("2 m^99999999999"));
        Assert.DoesNotContain("Value was", ex.Message); Assert.Contains("zu groß", ex.Message);
        var sw = Stopwatch.StartNew();
        try { R(MatheArt.Vereinfachen, "(x+y+z+w+v+1)^30"); } catch (Exception e) when (e is OperationCanceledException or OverflowException or ArgumentException) { log.WriteLine(e.Message); }
        Assert.True(sw.ElapsedMilliseconds < 4500, $"{sw.ElapsedMilliseconds} ms");
    }
}
