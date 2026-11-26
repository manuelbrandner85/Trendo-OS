using System.Globalization;
using System.Text;

namespace Trendo.Core.Wissen;

/// <summary>Ein chemisches Element. <see cref="Masse"/>: Standard-Atomgewicht (gekürzt, g/mol); bei Elementen ohne stabile Isotope die Massenzahl
/// des langlebigsten bekannten Isotops (<see cref="MasseGeschaetzt"/> = true, Angabe in eckigen Klammern).</summary>
public sealed record Element(int Z, string Symbol, string Name, double Masse, bool MasseGeschaetzt, double? En, int Periode, int? Gruppe, char Block, string Kategorie, string Zustand)
{
    /// <summary>Elektronenkonfiguration in Kurzform („[Ar] 3d10 4s1“).</summary>
    public string Konfiguration => Elemente.Konfiguration(Z);
    public string MasseText => MasseGeschaetzt ? $"[{Masse:0}]" : Masse.ToString(Masse >= 100 ? "0.00" : "0.0###", CultureInfo.GetCultureInfo("de-DE"));
}

/// <summary>T-0781 Naturwissenschaft: Periodensystem mit eigenen Daten.
/// Quellen (öffentliche Zahlenwerte, eigene Tabelle, keine übernommenen Texte):
/// Atomgewichte nach IUPAC/CIAAW „Standard Atomic Weights“ (gekürzte Werte des Berichts 2021, mit den Änderungen vom 23.10.2024 für Zr, Gd und Lu – ciaaw.org/atomic-weights.htm);
/// Elektronegativität nach Pauling (übliche Tabellenwerte); Elektronenkonfiguration aus der Madelung-Regel mit den bekannten Ausnahmen berechnet;
/// Gruppe, Periode, Block und Kategorie aus der Ordnungszahl. Abgeglichen mit der PubChem-Periodentabelle (Beleg reports/T-0781/elemente-abgleich.txt).</summary>
public static class Elemente
{
    // Ordnungszahl-Reihenfolge: Symbol, deutscher Name, Masse (negativ = Massenzahl des langlebigsten Isotops), Elektronegativität (0 = keine Angabe)
    static readonly (string S, string N, double M, double En)[] Daten =
    [
        ("H", "Wasserstoff", 1.0080, 2.20), ("He", "Helium", 4.0026, 0), ("Li", "Lithium", 6.94, 0.98), ("Be", "Beryllium", 9.0122, 1.57), ("B", "Bor", 10.81, 2.04),
        ("C", "Kohlenstoff", 12.011, 2.55), ("N", "Stickstoff", 14.007, 3.04), ("O", "Sauerstoff", 15.999, 3.44), ("F", "Fluor", 18.998, 3.98), ("Ne", "Neon", 20.180, 0),
        ("Na", "Natrium", 22.990, 0.93), ("Mg", "Magnesium", 24.305, 1.31), ("Al", "Aluminium", 26.982, 1.61), ("Si", "Silicium", 28.085, 1.90), ("P", "Phosphor", 30.974, 2.19),
        ("S", "Schwefel", 32.06, 2.58), ("Cl", "Chlor", 35.45, 3.16), ("Ar", "Argon", 39.95, 0), ("K", "Kalium", 39.098, 0.82), ("Ca", "Calcium", 40.078, 1.00),
        ("Sc", "Scandium", 44.956, 1.36), ("Ti", "Titan", 47.867, 1.54), ("V", "Vanadium", 50.942, 1.63), ("Cr", "Chrom", 51.996, 1.66), ("Mn", "Mangan", 54.938, 1.55),
        ("Fe", "Eisen", 55.845, 1.83), ("Co", "Cobalt", 58.933, 1.88), ("Ni", "Nickel", 58.693, 1.91), ("Cu", "Kupfer", 63.546, 1.90), ("Zn", "Zink", 65.38, 1.65),
        ("Ga", "Gallium", 69.723, 1.81), ("Ge", "Germanium", 72.630, 2.01), ("As", "Arsen", 74.922, 2.18), ("Se", "Selen", 78.971, 2.55), ("Br", "Brom", 79.904, 2.96),
        ("Kr", "Krypton", 83.798, 3.00), ("Rb", "Rubidium", 85.468, 0.82), ("Sr", "Strontium", 87.62, 0.95), ("Y", "Yttrium", 88.906, 1.22), ("Zr", "Zirconium", 91.222, 1.33),
        ("Nb", "Niob", 92.906, 1.6), ("Mo", "Molybdän", 95.95, 2.16), ("Tc", "Technetium", -98, 1.9), ("Ru", "Ruthenium", 101.07, 2.2), ("Rh", "Rhodium", 102.91, 2.28),
        ("Pd", "Palladium", 106.42, 2.20), ("Ag", "Silber", 107.87, 1.93), ("Cd", "Cadmium", 112.41, 1.69), ("In", "Indium", 114.82, 1.78), ("Sn", "Zinn", 118.71, 1.96),
        ("Sb", "Antimon", 121.76, 2.05), ("Te", "Tellur", 127.60, 2.1), ("I", "Iod", 126.90, 2.66), ("Xe", "Xenon", 131.29, 2.6), ("Cs", "Caesium", 132.91, 0.79),
        ("Ba", "Barium", 137.33, 0.89), ("La", "Lanthan", 138.91, 1.10), ("Ce", "Cer", 140.12, 1.12), ("Pr", "Praseodym", 140.91, 1.13), ("Nd", "Neodym", 144.24, 1.14),
        ("Pm", "Promethium", -145, 0), ("Sm", "Samarium", 150.36, 1.17), ("Eu", "Europium", 151.96, 0), ("Gd", "Gadolinium", 157.25, 1.20), ("Tb", "Terbium", 158.93, 0),
        ("Dy", "Dysprosium", 162.50, 1.22), ("Ho", "Holmium", 164.93, 1.23), ("Er", "Erbium", 167.26, 1.24), ("Tm", "Thulium", 168.93, 1.25), ("Yb", "Ytterbium", 173.05, 0),
        ("Lu", "Lutetium", 174.97, 1.27), ("Hf", "Hafnium", 178.49, 1.3), ("Ta", "Tantal", 180.95, 1.5), ("W", "Wolfram", 183.84, 2.36), ("Re", "Rhenium", 186.21, 1.9),
        ("Os", "Osmium", 190.23, 2.2), ("Ir", "Iridium", 192.22, 2.20), ("Pt", "Platin", 195.08, 2.28), ("Au", "Gold", 196.97, 2.54), ("Hg", "Quecksilber", 200.59, 2.00),
        ("Tl", "Thallium", 204.38, 1.62), ("Pb", "Blei", 207.2, 2.33), ("Bi", "Bismut", 208.98, 2.02), ("Po", "Polonium", -209, 2.0), ("At", "Astat", -210, 2.2),
        ("Rn", "Radon", -222, 0), ("Fr", "Francium", -223, 0.7), ("Ra", "Radium", -226, 0.9), ("Ac", "Actinium", -227, 1.1), ("Th", "Thorium", 232.04, 1.3),
        ("Pa", "Protactinium", 231.04, 1.5), ("U", "Uran", 238.03, 1.38), ("Np", "Neptunium", -237, 1.36), ("Pu", "Plutonium", -244, 1.28), ("Am", "Americium", -243, 1.3),
        ("Cm", "Curium", -247, 1.3), ("Bk", "Berkelium", -247, 1.3), ("Cf", "Californium", -251, 1.3), ("Es", "Einsteinium", -252, 1.3), ("Fm", "Fermium", -257, 1.3),
        ("Md", "Mendelevium", -258, 1.3), ("No", "Nobelium", -259, 1.3), ("Lr", "Lawrencium", -262, 0), ("Rf", "Rutherfordium", -267, 0), ("Db", "Dubnium", -268, 0),
        ("Sg", "Seaborgium", -269, 0), ("Bh", "Bohrium", -270, 0), ("Hs", "Hassium", -269, 0), ("Mt", "Meitnerium", -278, 0), ("Ds", "Darmstadtium", -281, 0),
        ("Rg", "Roentgenium", -282, 0), ("Cn", "Copernicium", -285, 0), ("Nh", "Nihonium", -286, 0), ("Fl", "Flerovium", -289, 0), ("Mc", "Moscovium", -290, 0),
        ("Lv", "Livermorium", -293, 0), ("Ts", "Tenness", -294, 0), ("Og", "Oganesson", -294, 0),
    ];

    public static readonly IReadOnlyList<Element> Alle = Bauen();
    static readonly Dictionary<string, Element> NachSymbol = Alle.ToDictionary(e => e.Symbol, StringComparer.Ordinal);

    public static Element? Symbol(string s) => NachSymbol.GetValueOrDefault(s);
    public static Element Nummer(int z) => z is >= 1 and <= 118 ? Alle[z - 1] : throw new ArgumentOutOfRangeException(nameof(z), "Ordnungszahl 1 bis 118.");

    /// <summary>Suche nach Symbol, Name oder Ordnungszahl („Fe“, „eisen“, „26“).</summary>
    public static Element? Finde(string text)
    {
        var t = text.Trim();
        if (int.TryParse(t, out var z) && z is >= 1 and <= 118) return Alle[z - 1];
        return NachSymbol.GetValueOrDefault(t) ?? Alle.FirstOrDefault(e => string.Equals(e.Name, t, StringComparison.OrdinalIgnoreCase) || string.Equals(e.Symbol, t, StringComparison.OrdinalIgnoreCase));
    }

    public static readonly string[] Kategorien = ["Alkalimetall", "Erdalkalimetall", "Übergangsmetall", "Lanthanoid", "Actinoid", "Metall", "Halbmetall", "Nichtmetall", "Halogen", "Edelgas"];

    static List<Element> Bauen()
    {
        var l = new List<Element>();
        for (var z = 1; z <= 118; z++)
        {
            var (s, n, m, en) = Daten[z - 1];
            var (p, g, block) = Lage(z);
            l.Add(new Element(z, s, n, Math.Abs(m), m < 0, en > 0 ? en : null, p, g, block, Kategorie(z, g), Zustand(z)));
        }
        return l;
    }

    /// <summary>Periode, Gruppe (null für Lanthanoide/Actinoide außer La/Ac in der Fußzeile) und Block aus der Ordnungszahl.</summary>
    public static (int Periode, int? Gruppe, char Block) Lage(int z)
    {
        int[] enden = [2, 10, 18, 36, 54, 86, 118];
        var p = Array.FindIndex(enden, e => z <= e) + 1;
        var start = p == 1 ? 1 : enden[p - 2] + 1; var k = z - start; // Stelle in der Periode (0-basiert)
        switch (p)
        {
            case 1: return (1, z == 1 ? 1 : 18, 's');
            case 2 or 3: return (p, k < 2 ? k + 1 : k + 11, k < 2 ? 's' : 'p');
            case 4 or 5: return (p, k + 1, k < 2 ? 's' : k < 12 ? 'd' : 'p');
            default:
                if (k < 2) return (p, k + 1, 's');
                if (k < 17) return (p, null, 'f'); // La–Lu bzw. Ac–Lr
                return (p, k - 13, k < 26 ? 'd' : 'p'); // Gruppe 3 bleibt in Periode 6/7 frei (La/Ac stehen in der Fußzeile)
        }
    }

    static string Kategorie(int z, int? g)
    {
        if (z is >= 57 and <= 71) return "Lanthanoid";
        if (z is >= 89 and <= 103) return "Actinoid";
        if (g == 18) return "Edelgas";
        if (g == 17) return "Halogen";
        if (z == 1) return "Nichtmetall";
        if (g == 1) return "Alkalimetall";
        if (g == 2) return "Erdalkalimetall";
        if (g is >= 3 and <= 12) return "Übergangsmetall";
        if (z is 5 or 14 or 32 or 33 or 51 or 52) return "Halbmetall";
        if (z is 6 or 7 or 8 or 15 or 16 or 34) return "Nichtmetall";
        return "Metall";
    }

    static string Zustand(int z) => z switch
    {
        1 or 2 or 7 or 8 or 9 or 10 or 17 or 18 or 36 or 54 or 86 => "gasförmig",
        35 or 80 => "flüssig",
        >= 100 => "unbekannt",
        _ => "fest",
    };

    // ---------- Elektronenkonfiguration ----------
    static readonly (int N, int L)[] Madelung = BaueMadelung();
    static (int, int)[] BaueMadelung()
    {
        var r = new List<(int, int)>();
        for (var s = 1; s <= 8; s++) for (var l = (s - 1) / 2; l >= 0; l--) { var n = s - l; if (n > l) r.Add((n, l)); }
        return [.. r];
    }

    /// <summary>Ausnahmen von der Madelung-Regel (Grundzustand laut üblichen Tabellen): (n, l) → Elektronenzahl, geändert gegenüber der Regel.</summary>
    static readonly Dictionary<int, string> Ausnahmen = new()
    {
        [24] = "[Ar] 3d5 4s1", [29] = "[Ar] 3d10 4s1", [41] = "[Kr] 4d4 5s1", [42] = "[Kr] 4d5 5s1", [44] = "[Kr] 4d7 5s1", [45] = "[Kr] 4d8 5s1", [46] = "[Kr] 4d10", [47] = "[Kr] 4d10 5s1",
        [57] = "[Xe] 5d1 6s2", [58] = "[Xe] 4f1 5d1 6s2", [64] = "[Xe] 4f7 5d1 6s2", [78] = "[Xe] 4f14 5d9 6s1", [79] = "[Xe] 4f14 5d10 6s1",
        [89] = "[Rn] 6d1 7s2", [90] = "[Rn] 6d2 7s2", [91] = "[Rn] 5f2 6d1 7s2", [92] = "[Rn] 5f3 6d1 7s2", [93] = "[Rn] 5f4 6d1 7s2", [96] = "[Rn] 5f7 6d1 7s2", [103] = "[Rn] 5f14 7s2 7p1",
    };

    static readonly int[] Edelgase = [2, 10, 18, 36, 54, 86];
    static readonly string[] EdelSym = ["He", "Ne", "Ar", "Kr", "Xe", "Rn"];

    public static string Konfiguration(int z)
    {
        if (Ausnahmen.TryGetValue(z, out var a)) return a;
        var kern = Array.FindLastIndex(Edelgase, e => e < z);
        var start = kern < 0 ? 0 : Edelgase[kern];
        var besetzt = new SortedDictionary<(int N, int L), int>();
        var rest = z;
        foreach (var (n, l) in Madelung)
        {
            if (rest == 0) break;
            var cap = 2 * (2 * l + 1); var e = Math.Min(cap, rest); rest -= e;
            besetzt[(n, l)] = e;
        }
        // nur die Schalen nach dem Edelgas-Kern
        var kernSchalen = new HashSet<(int, int)>(); var r2 = start;
        foreach (var (n, l) in Madelung) { if (r2 == 0) break; var cap = 2 * (2 * l + 1); r2 -= Math.Min(cap, r2); kernSchalen.Add((n, l)); }
        var sb = new StringBuilder();
        if (kern >= 0) sb.Append('[').Append(EdelSym[kern]).Append(']');
        foreach (var ((n, l), e) in besetzt.Where(kv => !kernSchalen.Contains(kv.Key)).OrderBy(kv => kv.Key.N).ThenBy(kv => kv.Key.L))
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(n).Append("spdf"[l]).Append(e);
        }
        return sb.ToString();
    }

    /// <summary>Hochgestellte Schreibweise für die Anzeige („3d¹⁰ 4s¹“).</summary>
    public static string Hochgestellt(string konf)
    {
        var sb = new StringBuilder(); var hoch = false;
        foreach (var c in konf)
        {
            if (c is 's' or 'p' or 'd' or 'f') { hoch = true; sb.Append(c); continue; }
            if (c == ' ' || c == '[' || c == ']') hoch = false;
            sb.Append(hoch && char.IsAsciiDigit(c) ? "⁰¹²³⁴⁵⁶⁷⁸⁹"[c - '0'] : c);
        }
        return sb.ToString();
    }

    /// <summary>Lage im Raster 18 × 10 (Zeile 0–9, Spalte 0–17); Lanthanoide/Actinoide in den Zeilen 8 und 9.</summary>
    public static (int Zeile, int Spalte) Raster(Element e)
    {
        if (e.Gruppe is { } g) return (e.Periode - 1, g - 1);
        var basis = e.Periode == 6 ? 57 : 89;
        return (e.Periode == 6 ? 8 : 9, 2 + (e.Z - basis));
    }

    /// <summary>Tabelle aller Elemente als CSV (Semikolon, Dezimalkomma – öffnet in LibreOffice und Excel).</summary>
    public static string Csv()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder("Ordnungszahl;Symbol;Name;Atommasse (g/mol);Masse geschätzt;Elektronegativität;Periode;Gruppe;Block;Kategorie;Zustand bei 25 °C;Elektronenkonfiguration\r\n");
        foreach (var e in Alle)
            sb.Append(e.Z).Append(';').Append(e.Symbol).Append(';').Append(e.Name).Append(';').Append(e.Masse.ToString("0.0####", de)).Append(';').Append(e.MasseGeschaetzt ? "ja" : "nein").Append(';')
              .Append(e.En?.ToString("0.00", de) ?? "").Append(';').Append(e.Periode).Append(';').Append(e.Gruppe?.ToString(CultureInfo.InvariantCulture) ?? "").Append(';').Append(e.Block).Append(';')
              .Append(e.Kategorie).Append(';').Append(e.Zustand).Append(';').Append(e.Konfiguration).Append("\r\n");
        return sb.ToString();
    }
}
