using System.Globalization;
using Trendo.Core.Wissen;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0781 Naturwissenschaft: Periodensystem, Molmasse, Reaktionen ausgleichen, Einheiten mit Dimensionsprüfung, Formelsammlung.</summary>
public class NaturKernTests(ITestOutputHelper log)
{
    // ---------------- Periodensystem ----------------
    [Fact(DisplayName = "T-0781 Periodensystem: 118 Elemente, Ordnungszahlen lückenlos, Symbole eindeutig, Lage im Raster eindeutig")]
    public void Vollstaendig()
    {
        Assert.Equal(118, Elemente.Alle.Count);
        Assert.Equal(Enumerable.Range(1, 118), Elemente.Alle.Select(e => e.Z));
        Assert.Equal(118, Elemente.Alle.Select(e => e.Symbol).Distinct().Count());
        Assert.Equal(118, Elemente.Alle.Select(Elemente.Raster).Distinct().Count());
        Assert.All(Elemente.Alle, e => Assert.True(e.Masse > 0 && e.Name.Length > 1));
        // Massen steigen bis auf die bekannten Ausnahmen (Ar/K, Co/Ni, Te/I, Th/Pa, U/Np …)
        var ausnahmen = Elemente.Alle.Skip(1).Where((e, i) => e.Masse < Elemente.Alle[i].Masse).Select(e => e.Symbol).ToList();
        foreach (var s in new[] { "K", "Ni", "I", "Pa" }) Assert.Contains(s, ausnahmen);
        Assert.True(ausnahmen.Count < 12, string.Join(", ", ausnahmen));
    }

    [Theory(DisplayName = "T-0781 Periodensystem: Stichproben gegen IUPAC/CIAAW (Atomgewicht), Gruppe, Periode, Kategorie, Konfiguration")]
    [InlineData("Fe", 26, 55.845, 4, 8, "Übergangsmetall", "[Ar] 3d6 4s2")]
    [InlineData("O", 8, 15.999, 2, 16, "Nichtmetall", "[He] 2s2 2p4")]
    [InlineData("Na", 11, 22.990, 3, 1, "Alkalimetall", "[Ne] 3s1")]
    [InlineData("Cu", 29, 63.546, 4, 11, "Übergangsmetall", "[Ar] 3d10 4s1")]
    [InlineData("Cl", 17, 35.45, 3, 17, "Halogen", "[Ne] 3s2 3p5")]
    [InlineData("Xe", 54, 131.29, 5, 18, "Edelgas", "[Kr] 4d10 5s2 5p6")]
    [InlineData("Hf", 72, 178.49, 6, 4, "Übergangsmetall", "[Xe] 4f14 5d2 6s2")]
    [InlineData("Pb", 82, 207.2, 6, 14, "Metall", "[Xe] 4f14 5d10 6s2 6p2")]
    [InlineData("Zr", 40, 91.222, 5, 4, "Übergangsmetall", "[Kr] 4d2 5s2")]
    public void Stichproben(string sym, int z, double m, int periode, int gruppe, string kat, string konf)
    {
        var e = Elemente.Symbol(sym)!;
        Assert.Equal(z, e.Z); Assert.Equal(m, e.Masse, 3); Assert.Equal(periode, e.Periode); Assert.Equal(gruppe, e.Gruppe); Assert.Equal(kat, e.Kategorie); Assert.Equal(konf, e.Konfiguration);
        Assert.Same(e, Elemente.Finde(e.Name)); Assert.Same(e, Elemente.Finde(z.ToString(CultureInfo.InvariantCulture)));
    }

    [Fact(DisplayName = "T-0781 Periodensystem: CIAAW-Änderungen 2024 (Zr, Gd, Lu) eingepflegt; Lanthanoide/Actinoide in der Fußzeile")]
    public void Neu2024()
    {
        Assert.Equal(91.222, Elemente.Symbol("Zr")!.Masse); Assert.Equal(157.25, Elemente.Symbol("Gd")!.Masse); Assert.Equal(174.97, Elemente.Symbol("Lu")!.Masse);
        Assert.Equal((8, 2), Elemente.Raster(Elemente.Symbol("La")!)); Assert.Equal((9, 16), Elemente.Raster(Elemente.Symbol("Lr")!));
        Assert.True(Elemente.Symbol("Tc")!.MasseGeschaetzt); Assert.Equal("[98]", Elemente.Symbol("Tc")!.MasseText);
    }

    /// <summary>Abgleich mit der PubChem-Periodentabelle (nur als Prüfung, die Daten selbst sind eigene Tabellenwerte). Läuft mit TRENDO_PUBCHEM_CSV=&lt;Datei&gt;.</summary>
    [Fact(DisplayName = "T-0781 Periodensystem: Abgleich mit PubChem (Masse ±0,02, EN ±0,05, Konfiguration) – nur mit TRENDO_PUBCHEM_CSV")]
    public void Abgleich_pubchem()
    {
        var csv = Environment.GetEnvironmentVariable("TRENDO_PUBCHEM_CSV") ?? "";
        if (csv.Length == 0 || !File.Exists(csv)) Ueberspringen.Weil("Abgleich nur mit TRENDO_PUBCHEM_CSV=<pt.csv von pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/CSV>.");
        var zeilen = File.ReadAllLines(csv).Skip(1).Select(Teile).ToList();
        var abw = new List<string>(); var geprueft = 0;
        foreach (var r in zeilen)
        {
            var z = int.Parse(r[0], CultureInfo.InvariantCulture); var e = Elemente.Nummer(z);
            Assert.Equal(r[1], e.Symbol);
            var pm = double.Parse(r[3], CultureInfo.InvariantCulture);
            // PubChem rundet unterschiedlich stark (z. B. „7“ für Li): Toleranz = halbe letzte Stelle der PubChem-Angabe
            var stellen = r[3].Contains('.') ? r[3].Length - r[3].IndexOf('.') - 1 : 0;
            // eigene Werte sind die gekürzten CIAAW-Werte (meist 5 gültige Ziffern): Toleranz = halbe letzte Stelle beider Angaben
            var eigen = e.Masse.ToString("0.#####", CultureInfo.InvariantCulture); var es = eigen.Contains('.') ? eigen.Length - eigen.IndexOf('.') - 1 : 0;
            if (Math.Abs(pm - e.Masse) > (e.MasseGeschaetzt ? 5 : 0.5 * Math.Pow(10, -stellen) + 0.5 * Math.Pow(10, -es) + 0.001)) abw.Add($"{e.Symbol} Masse: eigen {e.Masse}, PubChem {pm}");
            if (r[6].Length > 0 && e.En is { } en && Math.Abs(double.Parse(r[6], CultureInfo.InvariantCulture) - en) > 0.05) abw.Add($"{e.Symbol} EN: eigen {en}, PubChem {r[6]}");
            if (r[6].Length > 0 && e.En is null) abw.Add($"{e.Symbol} EN: eigen keine Angabe, PubChem {r[6]}");
            // Reihenfolge der Schalen ist Schreibweise (PubChem: 4s2 3d6, hier 3d6 4s2) – verglichen wird die Menge der besetzten Schalen
            static string Norm(string k) => string.Join(" ", System.Text.RegularExpressions.Regex.Matches(k.Split('(')[0], @"\[[A-Za-z]+\]|\d[spdf]\d+").Select(m => m.Value).Order());
            if (z <= 103 && Norm(r[5]) != Norm(e.Konfiguration)) abw.Add($"{e.Symbol} Konfiguration: eigen {e.Konfiguration}, PubChem {r[5]}");
            geprueft++;
        }
        foreach (var a in abw) log.WriteLine(a);
        log.WriteLine($"{geprueft} Elemente geprüft, {abw.Count} Abweichungen.");
        if (Environment.GetEnvironmentVariable("TRENDO_ABGLEICH_LOG") is { Length: > 0 } f) File.WriteAllLines(f, [$"Abgleich eigener Elementdaten mit PubChem ({geprueft} Elemente, {DateTime.Now:yyyy-MM-dd})", .. abw, $"{abw.Count} Abweichungen"]);
        Assert.Equal(118, geprueft);
        // Massen der stabilen Elemente müssen passen; bekannte Unterschiede: EN fehlt eigen bei Pm, Eu, Tb, Yb (keine gesicherte Pauling-Angabe)
        // bekannte Abweichungen: Lawrencium – hier 7p1 (Messung der Ionisierungsenergie 2015), PubChem 6d1; EN von Lr ohne gesicherten Wert;
        // Lithium – CIAAW gibt ein Intervall [6,938; 6,997] an, gekürzt 6,94; PubChem rundet auf 7,0
        Assert.All(abw, a => Assert.True(a.StartsWith("Lr ") || a.StartsWith("Li Masse"), a));
    }

    static string[] Teile(string line)
    {
        var r = new List<string>(); var cur = new System.Text.StringBuilder(); var q = false;
        foreach (var c in line) { if (c == '"') q = !q; else if (c == ',' && !q) { r.Add(cur.ToString()); cur.Clear(); } else cur.Append(c); }
        r.Add(cur.ToString()); return [.. r];
    }

    // ---------------- Molmasse ----------------
    [Theory(DisplayName = "T-0781 Molmasse: Summenformel mit Klammern, Kristallwasser – Summe der Anteile = 100 %")]
    [InlineData("H2O", 18.015)]
    [InlineData("H2SO4", 98.072)]
    [InlineData("Ca(OH)2", 74.092)]
    [InlineData("CuSO4·5H2O", 249.68)]
    [InlineData("C6H12O6", 180.156)]
    [InlineData("Al2(SO4)3", 342.14)]
    [InlineData("K4[Fe(CN)6]", 368.345)]
    public void Molmasse(string f, double m)
    {
        var r = Chemie.Molmasse(f);
        Assert.Equal(m, r.Summe, 1);
        Assert.Equal(1.0, r.Zeilen.Sum(z => z.Anteil), 9);
        // Gegenrechnung: von Hand aus der Elementtabelle
        var hand = r.Formel.Atome.Sum(a => a.Anzahl * Elemente.Symbol(a.Symbol)!.Masse);
        Assert.Equal(hand, r.Summe, 9);
        foreach (var s in Chemie.MolmasseWeg(r)) log.WriteLine($"{s.Text}: {s.Formel}");
    }

    [Theory(DisplayName = "T-0781 Summenformel: verständliche Fehler (Groß-/Kleinschreibung, unbekanntes Element)")]
    [InlineData("h2o", "Großbuchstaben")]
    [InlineData("Xy2", "kein Element")]
    [InlineData("Ca(OH2", "geschlossen")]
    public void Formel_fehler(string f, string teil) => Assert.Contains(teil, Assert.Throws<FormatException>(() => Chemie.Lies(f)).Message);

    // ---------------- Ausgleichen ----------------
    [Theory(DisplayName = "T-0781 Reaktionen ausgleichen: kleinste ganze Zahlen, Probe Atom für Atom, Ladung und Masse")]
    [InlineData("H2 + O2 -> H2O", "2 H₂ + O₂ → 2 H₂O")]
    [InlineData("CH4 + O2 -> CO2 + H2O", "CH₄ + 2 O₂ → CO₂ + 2 H₂O")]
    [InlineData("Fe + O2 -> Fe2O3", "4 Fe + 3 O₂ → 2 Fe₂O₃")]
    [InlineData("C3H8 + O2 = CO2 + H2O", "C₃H₈ + 5 O₂ → 3 CO₂ + 4 H₂O")]
    [InlineData("Al + HCl -> AlCl3 + H2", "2 Al + 6 HCl → 2 AlCl₃ + 3 H₂")]
    [InlineData("KMnO4 + HCl -> KCl + MnCl2 + H2O + Cl2", "2 KMnO₄ + 16 HCl → 2 KCl + 2 MnCl₂ + 8 H₂O + 5 Cl₂")]
    [InlineData("MnO4^- + Fe^2+ + H^+ -> Mn^2+ + Fe^3+ + H2O", "MnO₄⁻ + 5 Fe²⁺ + 8 H⁺ → Mn²⁺ + 5 Fe³⁺ + 4 H₂O")]
    public void Ausgleichen(string ein, string erwartet)
    {
        var r = Chemie.Ausgleichen(ein);
        foreach (var s in r.Schritte) log.WriteLine($"{s.Text}: {s.Formel}");
        Assert.Equal(erwartet, r.Gleichung);
        Assert.True(r.ProbeOk, string.Join(" | ", r.Proben.Select(p => p.Text)));
        // eigene Gegenprobe: jedes Element zählen
        foreach (var el in r.Links.Concat(r.Rechts).SelectMany(x => x.F.Atome.Select(a => a.Symbol)).Distinct())
            Assert.Equal(r.Links.Sum(x => x.K * x.F.Anzahl(el)), r.Rechts.Sum(x => x.K * x.F.Anzahl(el)));
    }

    [Fact(DisplayName = "T-0781 Ausgleichen: unmögliche Reaktion ehrlich abgelehnt")]
    public void Unmoeglich() => Assert.Contains("nicht", Assert.Throws<ArgumentException>(() => Chemie.Ausgleichen("H2O -> CO2")).Message);

    // ---------------- Einheiten ----------------
    [Theory(DisplayName = "T-0781 Rechnen mit Einheiten: Umrechnen und Mitrechnen, Rückrechnung als Probe")]
    [InlineData("100 km/h in m/s", 27.7777777778, "m/s")]
    [InlineData("3 km / 20 min", 2.5, "m/s")]
    [InlineData("9,81 m/s² · 2 s in km/h", 70.632, "km/h")]
    [InlineData("12 V · 0,5 A", 6, "W")]
    [InlineData("2 kWh in MJ", 7.2, "MJ")]
    [InlineData("1 bar in hPa", 1000, "hPa")]
    [InlineData("20 °C in K", 293.15, "K")]
    [InlineData("98,6 °F in °C", 37, "°C")]
    [InlineData("5 kg · 9,81 m/s²", 49.05, "N")]
    [InlineData("250 ml in l", 0.25, "l")]
    public void Einheiten_rechnen(string ein, double wert, string einheit)
    {
        var r = Einheiten.Ausrechnen(ein);
        log.WriteLine($"{ein} → {r.Text}");
        Assert.Equal(einheit, r.Einheit);
        var (w, _) = r.Einheit == Einheiten.SiEinheit(r.Ergebnis.Dim) ? (r.Ergebnis.Wert, 0) : Einheiten.Aus(r.Ergebnis, r.Einheit);
        Assert.Equal(wert, w, 6);
        Assert.True(r.Proben.All(p => p.Ok));
    }

    [Theory(DisplayName = "T-0781 Dimensionsprüfung: ungleiche Größen addieren oder falsch umrechnen wird abgelehnt")]
    [InlineData("3 m + 2 s", "Länge")]
    [InlineData("5 kg in m", "umrechnen")]
    [InlineData("sin(3 m)", "keine Einheit")]
    [InlineData("4 Furlong", "Unbekannte Einheit")]
    public void Dimension_fehler(string ein, string teil) => Assert.Contains(teil, Assert.ThrowsAny<ArgumentException>(() => Einheiten.Ausrechnen(ein)).Message);

    // ---------------- Formelsammlung ----------------
    [Fact(DisplayName = "T-0781 Formelsammlung: jede Formel ist dimensionsrichtig, jede Umstellung stimmt (Gegenrechnung mit Zufallswerten)")]
    public void Formeln_konsistent()
    {
        var rnd = new Random(781);
        foreach (var f in Formelsammlung.Alle)
        {
            // Dimension: beide Seiten mit den üblichen Einheiten
            var dims = f.Groessen.ToDictionary(g => Formelsammlung.Ersatz(g.Zeichen), g => Einheiten.Lies(g.Einheit) with { Wert = 1 + rnd.NextDouble() });
            var seiten = Formelsammlung.Ersatz(f.Gleichung).Split('=');
            var l = Einheiten.Werte(TermLeser.Lies(seiten[0]), dims); var r = Einheiten.Werte(TermLeser.Lies(seiten[1]), dims);
            Assert.True(l.Dim == r.Dim, $"{f.Id}: links {l.Dim}, rechts {r.Dim}");
            // jede Umstellung: aus den anderen Größen berechnen, dann muss die Gleichung stimmen
            foreach (var (z, form) in f.Umgestellt)
            {
                var werte = f.Groessen.Where(g => g.Zeichen != z).ToDictionary(g => g.Zeichen, g => new GroesseEingabe((1 + rnd.NextDouble() * 5).ToString("0.####", CultureInfo.GetCultureInfo("de-DE")), g.Einheit));
                var e = Formelsammlung.Rechne(f.Id, z, werte);
                Assert.True(e.ProbeOk, $"{f.Id} nach {z}: {string.Join(" | ", e.Proben.Select(p => p.Text))}");
            }
            Assert.Equal(f.Groessen.Select(g => g.Zeichen).Order(), f.Umgestellt.Keys.Order().Concat(f.Groessen.Select(g => g.Zeichen).Except(f.Umgestellt.Keys)).Order());
        }
        Assert.True(Formelsammlung.Alle.Count >= 25);
    }

    [Fact(DisplayName = "T-0781 Formelsammlung: Beispiel freier Fall mit Unsicherheit (Gauß wie im Messwerte-Studio), Werte in km/h und cm")]
    public void Beispiel()
    {
        var e = Formelsammlung.Rechne("freier-fall", "t", new Dictionary<string, GroesseEingabe> { ["h"] = new("20", "m", "0,1") });
        foreach (var s in e.Schritte) log.WriteLine($"{s.Text}: {s.Formel}");
        Assert.Equal(Math.Sqrt(2 * 20 / 9.80665), e.Ergebnis.Wert, 9);
        Assert.True(e.Ergebnis.Unsicherheit > 0 && e.Ergebnis.Unsicherheit < 0.01);
        Assert.StartsWith("t = (2,019", e.Text);
        Assert.True(e.ProbeOk);
        var v = Formelsammlung.Rechne("geschwindigkeit", "v", new Dictionary<string, GroesseEingabe> { ["s"] = new("42,195", "km"), ["t"] = new("2,5", "h") }, "km/h");
        Assert.Equal("v = 16,878 km/h", v.Text);
        Assert.Contains("passt", v.Schritte.First(s => s.Text.StartsWith("Dimension")).Formel);
        Assert.Contains("braucht eine Einheit", Assert.Throws<ArgumentException>(() => Formelsammlung.Rechne("geschwindigkeit", "v", new Dictionary<string, GroesseEingabe> { ["s"] = new("42", "kg"), ["t"] = new("2", "h") })).Message);
    }

    [Fact(DisplayName = "T-0781 Element-CSV: 119 Zeilen, Semikolon und Dezimalkomma")]
    public void Csv()
    {
        var c = Elemente.Csv(); var z = c.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(119, z.Length); Assert.StartsWith("26;Fe;Eisen;55,845;", z[26]);
    }
}
