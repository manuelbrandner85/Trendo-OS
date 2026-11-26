using System.Diagnostics;
using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0781 Nachbesserung nach der Gegenprüfung („nicht freigabefähig“): jede Gegenprobe des Prüfers als Regressionstest.
/// Grundregel: Kann der Kern eine Aufgabe nicht sicher vollständig lösen, sagt er das (Probe „unsicher“, ProbeOk = false) – nie eine falsche Antwort mit grüner Probe.
/// Der Test rechnet jede erwartete Lösung selbst nach (unabhängig vom Kern).</summary>
public class T0781GegenprobeTests(ITestOutputHelper log)
{
    static Loesung R(MatheArt art, string e, string? v = null, string? von = null, string? bis = null) => MatheKern.Rechne(new MatheAuftrag(art, e, v, von, bis));
    void Zeige(Loesung l) { log.WriteLine(l.Aufgabe + " → " + l.Ergebnis); foreach (var s in l.Schritte) log.WriteLine("  · " + s.Text + ": " + s.Formel); foreach (var p in l.Proben) log.WriteLine("  Probe: " + p.Text); }

    /// <summary>Die Lösungsmenge enthält genau diese Zahlen (Vergleich über die Zahlen im Ergebnistext).</summary>
    static void Enthaelt(Loesung l, params double[] werte)
    {
        var zahlen = System.Text.RegularExpressions.Regex.Matches(l.Ergebnis.Replace("−", "-").Replace(".", ""), @"-?\d+(?:,\d+)?(?:/\d+)?")
            .Select(m => Bruch.Lies(m.Value).AlsDouble()).ToList();
        foreach (var w in werte) Assert.True(zahlen.Any(z => Math.Abs(z - w) <= 1e-5 * Math.Max(1, Math.Abs(w))), $"{w} fehlt in „{l.Ergebnis}“");
    }

    // ---------- 1. Variable im Nenner ----------
    [Theory(DisplayName = "T-0781 G1 Variable im Nenner: 1/x=2, 3/x=6, 5/x=1/2, 2/(x+1)=1 – richtige Lösung, Lösungsweg mit Hauptnenner, Probe exakt; kein Endlos-Umformen")]
    [InlineData("1/x = 2", 0.5)]
    [InlineData("3/x = 6", 0.5)]
    [InlineData("5/x = 1/2", 10)]
    [InlineData("2/(x+1) = 1", 1)]
    [InlineData("x + 1/x = 2", 1)]
    public void Nenner(string g, double x)
    {
        var l = R(MatheArt.Gleichung, g); Zeige(l);
        Enthaelt(l, x);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
        Assert.True(l.Schritte.Count < 10);
        Assert.All(l.Schritte, s => Assert.False(string.IsNullOrWhiteSpace(s.Text)));
        var t = g.Split('='); var w = new Dictionary<string, double> { ["x"] = x };
        Assert.Equal(Auswertung.Wert(TermLeser.Lies(t[0]), w), Auswertung.Wert(TermLeser.Lies(t[1]), w), 9);
    }

    [Theory(DisplayName = "T-0781 G1 Definitionsmenge: x/x = 1 (alle außer 0), x²/x = 0 (keine Lösung), 1/x = 0 (keine Lösung) – ehrlich statt Fehler")]
    [InlineData("x/x = 1", "außer")]
    [InlineData("x^2/x = 0", "keine")]
    [InlineData("1/x = 0", "keine")]
    public void Definitionsmenge(string g, string teil)
    {
        var l = R(MatheArt.Gleichung, g); Zeige(l);
        Assert.Contains(teil, l.Ergebnis);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
    }

    // ---------- 2. Verlorene Lösungen ----------
    [Theory(DisplayName = "T-0781 G2 alle Lösungen: |x−3|=5 → −2 und 8, (ln x)²=4 → e⁻² und e², √(x²)=3 → ±3, |2x|=4 → ±2")]
    [InlineData("|x - 3| = 5", new[] { -2.0, 8.0 })]
    [InlineData("(ln(x))^2 = 4", new[] { 0.1353352832366127, 7.38905609893065 })]
    [InlineData("sqrt(x^2) = 3", new[] { -3.0, 3.0 })]
    [InlineData("abs(2x) = 4", new[] { -2.0, 2.0 })]
    public void Alle_loesungen(string g, double[] xs)
    {
        var l = R(MatheArt.Gleichung, g); Zeige(l);
        var t = g.Split('=');
        Enthaelt(l, xs);
        foreach (var x in xs)
        {
            var w = new Dictionary<string, double> { ["x"] = x };
            Assert.Equal(Auswertung.Wert(TermLeser.Lies(t[0]), w), Auswertung.Wert(TermLeser.Lies(t[1]), w), 9); // Test rechnet selbst nach
        }
        Assert.True(l.Proben.Count(p => p.Ok) >= xs.Length, string.Join(" | ", l.Proben.Select(p => p.Text)));
        Assert.True(l.ProbeOk);
    }

    [Theory(DisplayName = "T-0781 G2 sin/cos/tan: allgemeine Lösung mit k ∈ ℤ (nicht nur Hauptwert); |c| > 1 ehrlich „keine Lösung“")]
    [InlineData("sin(x) = 0.5")]
    [InlineData("cos(x) = 1")]
    [InlineData("tan(x) = 1")]
    [InlineData("sin(2x) = 0.5")]
    public void Trig(string g)
    {
        var l = R(MatheArt.Gleichung, g); Zeige(l);
        Assert.Contains("k ∈ ℤ", l.Ergebnis);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
        if (g.StartsWith("sin(x)")) Assert.Contains("5π/6", l.Ergebnis.Replace("5·π/6", "5π/6"));
        var none = R(MatheArt.Gleichung, "sin(x) = 2"); Zeige(none);
        Assert.Contains("keine", none.Ergebnis); Assert.True(none.ProbeOk, string.Join(" | ", none.Proben.Select(p => p.Text)));
    }

    // ---------- 3. Polynom-Nullstellen ----------
    [Fact(DisplayName = "T-0781 G3 Nullstellen außerhalb ±1000 und dicht beieinander: x³ − 2000000001 = 0, x⁴ − 4,0001x² + 4,0002 = 0 (4 Lösungen); Probe zählt die Nullstellen unabhängig (Sturm)")]
    public void Nullstellen()
    {
        var a = R(MatheArt.Gleichung, "x^3 - 2000000001 = 0"); Zeige(a);
        Enthaelt(a, Math.Cbrt(2000000001));
        Assert.True(a.ProbeOk);
        var b = R(MatheArt.Gleichung, "x^4 - 4,0001x^2 + 4,0002 = 0"); Zeige(b);
        // Test rechnet selbst: x² = 2 und x² = 2,0001
        Enthaelt(b, -Math.Sqrt(2.0001), -Math.Sqrt(2), Math.Sqrt(2), Math.Sqrt(2.0001));
        Assert.Contains(b.Proben, p => p.Text.Contains("Sturm"));
        Assert.True(b.ProbeOk);
        var c = R(MatheArt.Gleichung, "x^5 - x - 1 = 0"); Zeige(c);
        Assert.Contains(c.Proben, p => p.Text.Contains("Sturm") && p.Ok);
        // sehr hoher Grad: entweder ehrlich abgelehnt oder vollständig (x = −1 und x = 1) – nie nur x = 1
        try { var h = R(MatheArt.Gleichung, "x^720720 - 1 = 0"); Zeige(h); Enthaelt(h, -1, 1); Assert.True(h.ProbeOk); }
        catch (ArgumentException ex) { log.WriteLine(ex.Message); }
    }

    // ---------- 4. Uneigentliche Integrale ----------
    [Theory(DisplayName = "T-0781 G4 Integrale über Polstellen: ∫₋₁¹1/x², ∫₋₁¹1/x, ∫₀¹1/(x−0,5) existieren nicht; ∫₀¹1/√x = 2, ∫₀¹ln x = −1 (Grenzwert)")]
    [InlineData("1/x^2", "-1", "1", null)]
    [InlineData("1/x", "-1", "1", null)]
    [InlineData("1/(x-0.5)", "0", "1", null)]
    [InlineData("1/(x+1)", "-2", "0", null)]
    [InlineData("1/sqrt(x)", "0", "1", 2.0)]
    [InlineData("ln(x)", "0", "1", -1.0)]
    public void Uneigentlich(string f, string a, string b, double? wert)
    {
        var l = R(MatheArt.Integrieren, f, null, a, b); Zeige(l);
        if (wert is null) { Assert.Contains("existiert nicht", l.Ergebnis); Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text))); return; }
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
        Enthaelt(l, wert.Value);
    }

    // ---------- 5. ΔT in °C ----------
    [Fact(DisplayName = "T-0781 G5 Temperaturdifferenz: Q = c·m·ΔT mit 1 kg und ΔT = 20 °C ergibt 83 720 J (nicht 1,2 MJ); 30 °C − 20 °C = 10 K")]
    public void DeltaT()
    {
        var e = Formelsammlung.Rechne("waerme", "Q", new Dictionary<string, GroesseEingabe> { ["m"] = new("1", "kg"), ["ΔT"] = new("20", "°C") });
        Assert.Equal(83720, e.Ergebnis.Wert, 6); Assert.True(e.ProbeOk);
        var d = Einheiten.Ausrechnen("30 °C - 20 °C");
        Assert.Equal(10, d.Ergebnis.Wert, 9); Assert.Equal("K", d.Einheit);
    }

    // ---------- 6. Wissenschaftliche Schreibweise ----------
    [Fact(DisplayName = "T-0781 G6 Zehnerpotenzen: 3e5, 6,022e23, 1,6e-19 – und die Konstante e bleibt (2e, e^2, 3·e)")]
    public void Zehnerpotenz()
    {
        Assert.Equal(300000, Auswertung.Wert(TermLeser.Lies("3e5")), 6);
        Assert.Equal(6.022, Auswertung.Wert(TermLeser.Lies("6,022e23")) / 1e23, 12);
        Assert.Equal(1.6, Auswertung.Wert(TermLeser.Lies("1,6e-19")) * 1e19, 12);
        Assert.Equal(2 * Math.E, Auswertung.Wert(TermLeser.Lies("2e")), 12);
        Assert.Equal(Math.E * Math.E, Auswertung.Wert(TermLeser.Lies("e^2")), 12);
        Assert.Equal(3 * Math.E, Auswertung.Wert(TermLeser.Lies("3·e")), 12);
        Assert.Equal(1.6, Einheiten.Ausrechnen("1,6e-19 C").Ergebnis.Wert * 1e19, 12);
        Assert.Equal(6.022, Einheiten.Ausrechnen("6,022e23 mol").Ergebnis.Wert / 1e23, 12);
        Assert.Equal("x = 300000", R(MatheArt.Gleichung, "3e5 = x").Ergebnis);
    }

    // ---------- 7. Einfrieren ----------
    [Theory(DisplayName = "T-0781 G7 Riesenzahlen frieren nicht ein: klare Meldung in kurzer Zeit")]
    [InlineData(MatheArt.Vereinfachen, "(10^4000)^4000")]
    [InlineData(MatheArt.Vereinfachen, "((2^4096)^4096)^4096")]
    [InlineData(MatheArt.Vereinfachen, "9^9^9")]
    [InlineData(MatheArt.Gleichung, "x^2 = 10^3000 + 7")]
    public void Kein_einfrieren(MatheArt art, string e)
    {
        var sw = Stopwatch.StartNew();
        try { var l = R(art, e); Zeige(l); Assert.False(l.ProbeOk && l.Ergebnis.Length == 0); }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or FormatException or OperationCanceledException) { log.WriteLine(ex.Message); }
        Assert.True(sw.ElapsedMilliseconds < 4000, $"{sw.ElapsedMilliseconds} ms");
    }

    [Fact(DisplayName = "T-0781 G7 Wurzel einer riesigen Zahl (Newton-Verfahren) ist schnell und exakt")]
    public void Isqrt()
    {
        var sw = Stopwatch.StartNew();
        var n = System.Numerics.BigInteger.Pow(10, 3000) + 12345;
        var r = Bruch.ISqrt(n);
        Assert.True(r * r <= n && (r + 1) * (r + 1) > n);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"{sw.ElapsedMilliseconds} ms");
    }

    // ---------- 8. PDF-Hochzahlen ----------
    [Fact(DisplayName = "T-0781 G8 PDF: Folgen von Hochzeichen zusammenhängend ersetzen (10⁻¹⁹ → 10^(-19), xⁿ⁻¹ → x^(n-1), SO₄²⁻ → SO4^(2-))")]
    public void Pdf_hochzahlen()
    {
        Assert.Equal("1,6 · 10^(-19) C", Rechenblatt.PdfText("1,6 · 10⁻¹⁹ C"));
        Assert.Equal("x^(n-1)", Rechenblatt.PdfText("xⁿ⁻¹"));
        Assert.Equal("SO4^(2-)", Rechenblatt.PdfText("SO₄²⁻"));
        Assert.Equal("x², 10^(23), x^4", Rechenblatt.PdfText("x², 10²³, x⁴"));
        Assert.Equal("[Ar] 3d^(10) 4s¹", Rechenblatt.PdfText("[Ar] 3d¹⁰ 4s¹"));
    }

    // ---------- MITTEL ----------
    [Fact(DisplayName = "T-0781 M Linse mit f = g: „Bild im Unendlichen“ statt „—“; Teilen durch 0 nie mit grüner Probe")]
    public void Linse()
    {
        var e = Formelsammlung.Rechne("linse", "b", new Dictionary<string, GroesseEingabe> { ["f"] = new("10", "cm"), ["g"] = new("10", "cm") });
        Assert.Contains("Unendlich", e.Text);
        var t = Formelsammlung.Rechne("geschwindigkeit", "t", new Dictionary<string, GroesseEingabe> { ["s"] = new("0", "m"), ["v"] = new("0", "m/s") });
        Assert.False(t.ProbeOk); Assert.DoesNotContain("—", t.Text);
    }

    // ---------- NIEDRIG ----------
    [Fact(DisplayName = "T-0781 N x⁰ und (−8)^(1/3) ohne falsch rote Probe; x/x mit Hinweis auf den Definitionsbereich")]
    public void Niedrig()
    {
        Assert.True(R(MatheArt.Vereinfachen, "x^0").ProbeOk);
        Assert.True(R(MatheArt.Bruchrechnen, "(-8)^(1/3)").ProbeOk);
        var v = R(MatheArt.Vereinfachen, "x/x"); Zeige(v);
        Assert.Contains(v.Schritte, s => s.Formel.Contains("x ≠ 0") || s.Text.Contains("x ≠ 0"));
    }

    [Fact(DisplayName = "T-0781 N Cu2+ ohne ^, mL und mT, Lu und Lr in Gruppe 3 (IUPAC)")]
    public void Niedrig_natur()
    {
        Assert.Equal(2, Chemie.Lies("Cu2+").Ladung);
        Assert.Equal("Cu²⁺ + Zn → Cu + Zn²⁺", Chemie.Ausgleichen("Cu2+ + Zn -> Cu + Zn2+").Gleichung);
        Assert.Equal(0.001, Einheiten.Ausrechnen("1 mL in l").Ergebnis.Wert * 1000, 9);
        Assert.Equal(0.001, Einheiten.Ausrechnen("1 mT").Ergebnis.Wert, 12);
        Assert.Equal(3, Elemente.Symbol("Lu")!.Gruppe); Assert.Equal(3, Elemente.Symbol("Lr")!.Gruppe);
        Assert.Null(Elemente.Symbol("La")!.Gruppe);
    }
}
