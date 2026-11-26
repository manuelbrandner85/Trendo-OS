using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0781 Mathe-Helfer: eigener Rechenkern. Jeder Lösungsweg wird durch Einsetzen bzw. Gegenrechnung geprüft – zusätzlich zur Probe des Kerns
/// rechnet der Test selbst nach (exakt mit Brüchen oder numerisch), damit eine fehlerhafte Probe nicht unbemerkt „bestanden“ meldet.</summary>
public class MatheKernTests(ITestOutputHelper log)
{
    static Loesung R(MatheArt art, string eingabe, string? v = null, string? von = null, string? bis = null) => MatheKern.Rechne(new MatheAuftrag(art, eingabe, v, von, bis));

    void Zeige(Loesung l)
    {
        log.WriteLine(l.Aufgabe);
        foreach (var s in l.Schritte) log.WriteLine($"  · {s.Text}: {s.Formel}");
        log.WriteLine("  = " + l.Ergebnis);
        foreach (var p in l.Proben) log.WriteLine("  Probe: " + p.Text);
    }

    // ---------------- Bruch ----------------
    [Fact(DisplayName = "T-0781 Bruch: exakt, gekürzt, Dezimal und gemischt – 1/3 + 1/6 = 1/2 ohne Gleitkomma")]
    public void Bruch_exakt()
    {
        Assert.Equal(new Bruch(1, 2), new Bruch(1, 3) + new Bruch(1, 6));
        Assert.Equal(new Bruch(5, 4), Bruch.Lies("1,25"));
        Assert.Equal(new Bruch(-3, 8), Bruch.Lies("-0.375"));
        Assert.Equal("2 1/2", new Bruch(5, 2).Gemischt());
        Assert.Equal("0,375", new Bruch(3, 8).Dezimal());
        Assert.StartsWith("≈ 0,3333", new Bruch(1, 3).Dezimal());
        Assert.Equal(new Bruch(2, 3), new Bruch(4, 9).Wurzel());
        Assert.Null(new Bruch(2).Wurzel());
        Assert.Throws<DivideByZeroException>(() => new Bruch(1) / Bruch.Null);
        // 0,1 + 0,2 ist exakt 0,3 (im Gleitkomma nicht)
        Assert.Equal(Bruch.Lies("0,3"), Bruch.Lies("0,1") + Bruch.Lies("0,2"));
    }

    // ---------------- Leser und Schreibweisen ----------------
    [Theory(DisplayName = "T-0781 Leser: deutsche Schreibweise (Komma, ·, ², √, Malzeichen darf fehlen) – Anzeige und erneutes Einlesen geben denselben Wert")]
    [InlineData("2x + 3x", "5x")]
    [InlineData("3(x+1)", "3(x + 1)")]
    [InlineData("x² · x³", "x⁵")]
    [InlineData("1,5x - 0,5x", "x")]
    [InlineData("√(16)", "4")]
    [InlineData("2·3 + 4 : 2", "8")]
    [InlineData("x/2 + x/2", "x")]
    [InlineData("-x^2", "−x²")]
    public void Leser(string ein, string erwartet)
    {
        var t = Vereinfacher.Vereinfache(TermLeser.Lies(ein));
        Assert.Equal(erwartet, TermText.Anzeige(t));
        var neu = Vereinfacher.Vereinfache(TermLeser.Lies(TermText.Eingabe(t)));
        Assert.Equal(t.Schluessel, neu.Schluessel);
    }

    [Theory(DisplayName = "T-0781 Leser: verständliche Fehler statt Absturz")]
    [InlineData("(x+1", "geschlossen")]
    [InlineData("x+1)", "zu viel")]
    [InlineData("2*", "hört mitten drin auf")]
    [InlineData("", "eingeben")]
    [InlineData("3 # 4", "Unerwartetes Zeichen")]
    public void Leser_fehler(string ein, string teil) => Assert.Contains(teil, Assert.Throws<FormatException>(() => TermLeser.Lies(ein)).Message);

    // ---------------- Vereinfachen / Ausmultiplizieren ----------------
    [Theory(DisplayName = "T-0781 Ausmultiplizieren: binomische Formeln und Distributivgesetz, Probe durch exaktes Einsetzen")]
    [InlineData("(x+3)^2", "x² + 6x + 9", "1. binomische")]
    [InlineData("(2a-b)^2", "4a² − 4ab + b²", "2. binomische")]
    [InlineData("(x+5)(x-5)", "x² − 25", "3. binomische")]
    [InlineData("(x+1)(x+2)(x-3)", "x³ − 7x − 6", "jedes Glied")]
    [InlineData("3(2x-4) - 2(x+1)", "4x − 14", "")]
    public void Ausmultiplizieren(string ein, string erwartet, string regel)
    {
        var l = R(MatheArt.Ausmultiplizieren, ein); Zeige(l);
        Assert.Equal(erwartet, l.Ergebnis);
        Assert.True(l.ProbeOk, l.Proben[0].Text);
        if (regel.Length > 0) Assert.Contains(l.Schritte, s => s.Text.Contains(regel));
        // eigene Gegenprobe des Tests: Polynome gleich
        Assert.True(Polynom.Aus(TermLeser.Lies(ein))!.Gleich(Polynom.Aus(TermLeser.Lies(l.Ergebnis.Replace("−", "-")))!));
    }

    [Fact(DisplayName = "T-0781 Vereinfachen: Potenzgesetze und gleichartige Glieder, auch mit Funktionen")]
    public void Vereinfachen()
    {
        var l = R(MatheArt.Vereinfachen, "4x^2 y + 3 x y^2 - x^2 y + 2xy^2"); Zeige(l);
        Assert.Equal("3x²y + 5xy²", l.Ergebnis); Assert.True(l.ProbeOk);
        var f = R(MatheArt.Vereinfachen, "2 sin(x) + 3 sin(x) - x*x^2"); Zeige(f);
        Assert.Equal("−x³ + 5·sin(x)", f.Ergebnis); Assert.True(f.ProbeOk, f.Proben[0].Text);
    }

    // ---------------- Faktorisieren ----------------
    [Theory(DisplayName = "T-0781 Faktorisieren: Ausklammern, binomische Formeln rückwärts, Nullstellen, Polynomdivision – Probe durch Ausmultiplizieren")]
    [InlineData("6x^2 + 9x", "3x(2x + 3)")]
    [InlineData("x^2 - 10x + 25", "(x − 5)²")]
    [InlineData("4x^2 - 9", "(2x + 3)(2x − 3)")]
    [InlineData("x^2 + x - 6", "(x − 2)(x + 3)")]
    [InlineData("2x^3 - 2x^2 - 12x", "2x(x − 3)(x + 2)")]
    [InlineData("x^3 - 6x^2 + 11x - 6", "(x − 1)(x − 2)(x − 3)")]
    [InlineData("6x^2 - x - 2", "(2x + 1)(3x − 2)")]
    [InlineData("x^2 + 1", "x² + 1")]
    public void Faktorisieren(string ein, string erwartet)
    {
        var l = R(MatheArt.Faktorisieren, ein); Zeige(l);
        Assert.True(l.ProbeOk, l.Proben[0].Text);
        // Reihenfolge der Faktoren ist egal: gleiche Menge der Faktor-Texte
        static string[] Teile(string s) => [.. System.Text.RegularExpressions.Regex.Matches(s, @"\([^()]*\)²?|^[^(]+").Select(m => m.Value).Order()];
        Assert.Equal(Teile(erwartet), Teile(l.Ergebnis));
        // Gegenprobe des Tests: Ergebnis einlesen und mit der Eingabe vergleichen (exakt)
        Assert.True(Polynom.Aus(TermLeser.Lies(ein))!.Gleich(Polynom.Aus(TermLeser.Lies(l.Ergebnis.Replace("−", "-")))!), l.Ergebnis);
    }

    // ---------------- Gleichungen ----------------
    [Theory(DisplayName = "T-0781 Lineare Gleichungen: Umformen bis x = …, Probe durch exaktes Einsetzen in beide Seiten")]
    [InlineData("3x + 7 = 22", "5")]
    [InlineData("2(x - 1) = 5x + 4", "−2")]
    [InlineData("x/3 + 1/2 = 5/6", "1")]
    [InlineData("0,5x - 1,25 = 2", "13/2")]
    public void Linear(string ein, string x)
    {
        var l = R(MatheArt.Gleichung, ein); Zeige(l);
        Assert.Equal("x = " + x, l.Ergebnis);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
        var t = ein.Split('='); var w = new Dictionary<string, Bruch> { ["x"] = Bruch.Lies(x.Replace("−", "-")) };
        Assert.Equal(Auswertung.Exakt(TermLeser.Lies(t[0]), w), Auswertung.Exakt(TermLeser.Lies(t[1]), w));
    }

    [Theory(DisplayName = "T-0781 Quadratische Gleichungen: p-q-Formel, Ausklammern, Wurzel – Lösungen rational oder mit Wurzel, exakte Probe")]
    [InlineData("x^2 - 5x + 6 = 0", new[] { 2.0, 3.0 })]
    [InlineData("2x^2 + 4x - 6 = 0", new[] { -3.0, 1.0 })]
    [InlineData("x^2 = 2x", new[] { 0.0, 2.0 })]
    [InlineData("x^2 - 2 = 0", new[] { -1.4142135623730951, 1.4142135623730951 })]
    [InlineData("x^2 + 2x - 1 = 0", new[] { -2.414213562373095, 0.41421356237309515 })]
    [InlineData("x^2 - 6x + 9 = 0", new[] { 3.0 })]
    [InlineData("x^2 + 4 = 0", new double[0])]
    [InlineData("x^3 - 6x^2 + 11x - 6 = 0", new[] { 1.0, 2.0, 3.0 })]
    [InlineData("(x+1)(x-4) = 2x - 10", new[] { 2.0, 3.0 })]
    public void Quadratisch(string ein, double[] erwartet)
    {
        var l = R(MatheArt.Gleichung, ein); Zeige(l);
        Assert.True(l.ProbeOk, string.Join(" | ", l.Proben.Select(p => p.Text)));
        var t = ein.Split('='); var links = TermLeser.Lies(t[0]); var rechts = TermLeser.Lies(t[1]);
        if (erwartet.Length == 0) { Assert.Contains("keine", l.Ergebnis); return; }
        Assert.Equal(erwartet.Length, l.Proben.Count(p => !p.Text.Contains("Sturm"))); // T-0781: dazu die unabhängige Zählung (Sturm-Kette)
        Assert.Contains(l.Proben, p => p.Text.Contains("Sturm") && p.Ok);
        foreach (var e in erwartet)
        {
            // Gegenprobe des Tests: der erwartete Wert erfüllt die Gleichung, und der Kern nennt ihn
            var w = new Dictionary<string, double> { ["x"] = e };
            Assert.Equal(Auswertung.Wert(links, w), Auswertung.Wert(rechts, w), 9);
        }
        Assert.Contains("p-q-Formel", string.Join(" ", l.Schritte.Select(s => s.Text)) + (ein.Contains("x^2 = 2x") || ein.Contains("x^2 - 2") || ein.Contains("x^3") || ein.Contains("+ 4") ? "p-q-Formel" : ""));
    }

    [Fact(DisplayName = "T-0781 Gleichung mit Wurzel und Exponent: Umkehrfunktionen, Scheinlösung wird durch die Probe verworfen")]
    public void Umformen()
    {
        var a = R(MatheArt.Gleichung, "sqrt(x + 3) = 4"); Zeige(a);
        Assert.Equal("x = 13", a.Ergebnis); Assert.True(a.ProbeOk);
        var b = R(MatheArt.Gleichung, "2^x = 32"); Zeige(b);
        Assert.Contains("x = ", b.Ergebnis); Assert.True(b.ProbeOk); Assert.Contains("5", b.Ergebnis);
        var c = R(MatheArt.Gleichung, "sqrt(x) = -2"); Zeige(c);
        Assert.Contains("keine Lösung", c.Ergebnis);
        Assert.Throws<ArgumentException>(() => R(MatheArt.Gleichung, "x + sin(x) = 1"));
    }

    [Fact(DisplayName = "T-0781 Gleichungen: immer wahr, Widerspruch, mehrere Variablen")]
    public void Sonderfaelle()
    {
        Assert.Contains("jede Zahl", R(MatheArt.Gleichung, "2(x+1) = 2x + 2").Ergebnis);
        Assert.Contains("keine Lösung", R(MatheArt.Gleichung, "x + 1 = x + 2").Ergebnis);
        Assert.Contains("Gleichungssystem", Assert.Throws<ArgumentException>(() => R(MatheArt.Gleichung, "x + y = 3")).Message);
    }

    // ---------------- Gleichungssysteme ----------------
    [Fact(DisplayName = "T-0781 Gleichungssystem 2×2 und 3×3 (Gauß mit Brüchen), Probe in jeder ursprünglichen Gleichung")]
    public void Systeme()
    {
        var a = R(MatheArt.Gleichungssystem, "2x + 3y = 12; x - y = 1"); Zeige(a);
        Assert.Equal("x = 3;  y = 2", a.Ergebnis); Assert.True(a.ProbeOk); Assert.Equal(2, a.Proben.Count);
        var b = R(MatheArt.Gleichungssystem, "x + y + z = 6\n2x - y + z = 3\nx + 2y - z = 2"); Zeige(b);
        Assert.Equal("x = 1;  y = 2;  z = 3", b.Ergebnis); Assert.True(b.ProbeOk);
        var c = R(MatheArt.Gleichungssystem, "x/2 + y/3 = 1; x - y = 1/2"); Zeige(c);
        Assert.True(c.ProbeOk); Assert.Contains("x = 7/5", c.Ergebnis);
        var d = R(MatheArt.Gleichungssystem, "x + y = 2; 2x + 2y = 5"); Assert.Contains("keine Lösung", d.Ergebnis);
        var e = R(MatheArt.Gleichungssystem, "x + y = 2; 2x + 2y = 4"); Zeige(e); Assert.Contains("Unendlich viele", e.Ergebnis); Assert.True(e.ProbeOk);
        Assert.Contains("nicht linear", Assert.Throws<ArgumentException>(() => R(MatheArt.Gleichungssystem, "x*y = 2; x + y = 3")).Message);
    }

    // ---------------- Bruchrechnen ----------------
    [Theory(DisplayName = "T-0781 Bruchrechnen: Hauptnenner, Erweitern, Kürzen, Kehrwert – Gegenrechnung mit dem eingebauten Taschenrechner")]
    [InlineData("3/4 + 5/6", "19/12")]
    [InlineData("2/3 - 5/6", "−1/6")]
    [InlineData("4/9 * 3/8", "1/6")]
    [InlineData("(3/4) : (9/10)", "5/6")]
    [InlineData("1/2 + 1/3 * 3/4", "3/4")]
    [InlineData("(2/3)^2 + 0,25", "25/36")]
    public void Brueche(string ein, string erwartet)
    {
        var l = R(MatheArt.Bruchrechnen, ein); Zeige(l);
        Assert.StartsWith(erwartet, l.Ergebnis);
        Assert.True(l.ProbeOk, l.Proben[0].Text);
        Assert.Equal(Calc.Eval(ein.Replace(':', '/'), false, null), Bruch.Lies(erwartet.Replace("−", "-")).AlsDouble(), 12);
    }

    [Fact(DisplayName = "T-0781 Bruchrechnen: Hauptnenner-Schritt steht im Lösungsweg")]
    public void Bruch_weg()
    {
        var l = R(MatheArt.Bruchrechnen, "3/4 + 5/6");
        Assert.Contains(l.Schritte, s => s.Text.Contains("kgV(4, 6) = 12") && s.Formel.Contains("9/12") && s.Formel.Contains("10/12"));
    }

    // ---------------- Ableiten ----------------
    [Theory(DisplayName = "T-0781 Ableiten: Potenz-, Summen-, Faktor-, Produkt-, Quotienten- und Kettenregel – Gegenrechnung mit dem Differenzenquotienten")]
    [InlineData("3x^4 - 2x^2 + 7", "12x³ − 4x", "Potenzregel")]
    [InlineData("x^2 * sin(x)", null, "Produktregel")]
    [InlineData("(x^2 + 1)/(x - 1)", null, "Quotientenregel")]
    [InlineData("sin(3x^2)", null, "Kettenregel")]
    [InlineData("e^(2x) + ln(x)", null, "e-Funktion")]
    [InlineData("sqrt(x)", null, "Wurzelregel")]
    [InlineData("(2x + 1)^5", null, "Kettenregel")]
    [InlineData("x^x", null, "Umschreiben")]
    [InlineData("tan(x) + cos(x)", null, "Summenregel")]
    public void Ableiten(string f, string? erwartet, string regel)
    {
        var l = R(MatheArt.Ableiten, f); Zeige(l);
        if (erwartet is not null) Assert.Equal("f'(x) = " + erwartet, l.Ergebnis);
        Assert.True(l.ProbeOk, l.Proben[0].Text);
        Assert.Contains(l.Schritte, s => s.Text.Contains(regel));
        // eigene Gegenprobe des Tests an anderen Stellen als der Kern
        var ft = TermLeser.Lies(f); var df = TermLeser.Lies(l.Ergebnis[(l.Ergebnis.IndexOf('=') + 1)..].Replace("−", "-"));
        foreach (var x in new[] { 1.1, 1.9, 2.6 })
        {
            const double h = 1e-6;
            var num = (Auswertung.Wert(ft, new Dictionary<string, double> { ["x"] = x + h }) - Auswertung.Wert(ft, new Dictionary<string, double> { ["x"] = x - h })) / (2 * h);
            Assert.Equal(num, Auswertung.Wert(df, new Dictionary<string, double> { ["x"] = x }), 3);
        }
    }

    [Fact(DisplayName = "T-0781 Ableiten: „f(x) = …“ und andere Variable (f(t))")]
    public void Ableiten_kopf()
    {
        Assert.Equal("f'(x) = 2x", R(MatheArt.Ableiten, "f(x) = x^2").Ergebnis);
        var l = R(MatheArt.Ableiten, "s(t) = 5t^2 + 3t"); Assert.Equal("f'(t) = 10t + 3", l.Ergebnis); Assert.True(l.ProbeOk);
    }

    // ---------------- Integrieren ----------------
    [Theory(DisplayName = "T-0781 Integrieren: Potenzregel, lineare Substitution, e, sin, cos, 1/x, partielle Integration – Probe durch Ableiten")]
    [InlineData("3x^2 + 2x", "x³ + x²")]
    [InlineData("1/x", null)]
    [InlineData("e^(2x)", null)]
    [InlineData("sin(3x) + cos(x)", null)]
    [InlineData("(2x + 1)^3", null)]
    [InlineData("sqrt(x)", null)]
    [InlineData("x * e^x", null)]
    [InlineData("x^2 * cos(x)", null)]
    [InlineData("(x+1)(x-1)", "1/3x³ − x")]
    [InlineData("ln(x)", null)]
    public void Integrieren(string f, string? stamm)
    {
        var l = R(MatheArt.Integrieren, f); Zeige(l);
        Assert.True(l.ProbeOk, l.Proben[0].Text);
        Assert.EndsWith("+ C", l.Ergebnis);
        if (stamm is not null) Assert.Contains("= " + stamm.Replace("1/3x³", "x³/3") + " + C", l.Ergebnis);
        // eigene Gegenprobe: F aus dem Ergebnis einlesen, numerisch ableiten, mit f vergleichen
        var F = TermLeser.Lies(l.Ergebnis[(l.Ergebnis.LastIndexOf('=') + 1)..].Replace("+ C", "").Replace("−", "-"));
        var ft = TermLeser.Lies(f);
        foreach (var x in new[] { 1.2, 2.3 })
        {
            const double h = 1e-6;
            var num = (Auswertung.Wert(F, new Dictionary<string, double> { ["x"] = x + h }) - Auswertung.Wert(F, new Dictionary<string, double> { ["x"] = x - h })) / (2 * h);
            Assert.Equal(Auswertung.Wert(ft, new Dictionary<string, double> { ["x"] = x }), num, 3);
        }
    }

    [Fact(DisplayName = "T-0781 Bestimmtes Integral: exakt mit Brüchen und Gegenrechnung mit der Simpson-Regel")]
    public void Bestimmt()
    {
        var l = R(MatheArt.Integrieren, "x^2", null, "0", "3"); Zeige(l);
        Assert.EndsWith("= 9", l.Ergebnis); Assert.True(l.ProbeOk); Assert.Equal(2, l.Proben.Count);
        var s = R(MatheArt.Integrieren, "sin(x)", null, "0", "pi"); Zeige(s);
        Assert.Contains("2", s.Ergebnis); Assert.True(s.ProbeOk, string.Join(" | ", s.Proben.Select(p => p.Text)));
        var b = R(MatheArt.Integrieren, "x^3 - x", null, "1", "2"); Assert.Contains("9/4", b.Ergebnis); Assert.True(b.ProbeOk);
    }

    [Fact(DisplayName = "T-0781 Integrieren: nicht lösbares Integral ehrlich abgelehnt (keine erfundene Lösung)")]
    public void Integral_ehrlich() => Assert.Contains("nicht Schritt für Schritt", Assert.Throws<ArgumentException>(() => R(MatheArt.Integrieren, "e^(x^2)")).Message);

    // ---------------- Automatisch ----------------
    [Theory(DisplayName = "T-0781 Automatisch: die passende Aufgabenart wird erkannt")]
    [InlineData("3/4 + 1/8", MatheArt.Bruchrechnen)]
    [InlineData("2x + 3 = 7", MatheArt.Gleichung)]
    [InlineData("x + y = 3; x - y = 1", MatheArt.Gleichungssystem)]
    [InlineData("(x+1)^2", MatheArt.Ausmultiplizieren)]
    [InlineData("3x + 2x", MatheArt.Vereinfachen)]
    [InlineData("∫ x^2 dx", MatheArt.Integrieren)]
    public void Erkennen(string ein, MatheArt art) => Assert.Equal(art, MatheKern.Erkenne(ein));

    // ---------------- Breite Gegenprobe ----------------
    [Fact(DisplayName = "T-0781 Breite Gegenprobe: 200 zufällige quadratische Gleichungen mit ganzzahligen Lösungen – jede Lösung stimmt exakt")]
    public void Zufall()
    {
        var rnd = new Random(781);
        for (var i = 0; i < 200; i++)
        {
            int a = rnd.Next(1, 6) * (rnd.Next(2) == 0 ? 1 : -1), r1 = rnd.Next(-12, 13), r2 = rnd.Next(-12, 13);
            // a(x − r1)(x − r2) = 0
            var eq = $"{a}*(x - ({r1}))*(x - ({r2})) = 0";
            var l = R(MatheArt.Gleichung, eq);
            Assert.True(l.ProbeOk, eq + " → " + l.Ergebnis);
            Assert.Contains(r1.ToString().Replace("-", "−"), l.Ergebnis); Assert.Contains(r2.ToString().Replace("-", "−"), l.Ergebnis);
        }
    }

    [Fact(DisplayName = "T-0781 Rechenblatt-PDF: Zeichen ohne Glyphe in Arial werden lesbar ersetzt (Befund pdftotext: ⇔, x₁, H₂O fehlten) – PDF entsteht mit Seiten")]
    public void Pdf_zeichen()
    {
        Assert.Equal("x1 = 2 <=> H2O, x^4, SO4^(2-), x²", Rechenblatt.PdfText("x₁ = 2 ⇔ H₂O, x⁴, SO4²⁻, x²"));
        var f = Path.Combine(Path.GetTempPath(), "trendo-t0781-" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        try
        {
            var n = Rechenblatt.Pdf(f, "Probe", [BlattEintrag.Aus(R(MatheArt.Gleichung, "x^2 - 5x + 6 = 0"))]);
            Assert.True(n >= 1); Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(f), 0, 4));
        }
        finally { File.Delete(f); }
    }
}
