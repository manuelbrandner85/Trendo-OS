using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Wissen;

/// <summary>Dimension als Exponenten der sieben SI-Basisgrößen: Länge m, Masse kg, Zeit s, Strom A, Temperatur K, Stoffmenge mol, Lichtstärke cd.</summary>
public readonly struct Dimension : IEquatable<Dimension>
{
    readonly int[]? _e;
    public Dimension(params int[] e) { _e = new int[7]; for (var i = 0; i < Math.Min(7, e.Length); i++) _e[i] = e[i]; }
    public int this[int i] => _e?[i] ?? 0;
    public static readonly Dimension Eins = new();
    public static readonly string[] Basis = ["m", "kg", "s", "A", "K", "mol", "cd"];
    public static readonly string[] Namen = ["Länge", "Masse", "Zeit", "Stromstärke", "Temperatur", "Stoffmenge", "Lichtstärke"];
    public bool IstEins { get { var me = this; return Enumerable.Range(0, 7).All(i => me[i] == 0); } }
    public static Dimension operator *(Dimension a, Dimension b) => new([.. Enumerable.Range(0, 7).Select(i => a[i] + b[i])]);
    public static Dimension operator /(Dimension a, Dimension b) => new([.. Enumerable.Range(0, 7).Select(i => a[i] - b[i])]);
    public Dimension Hoch(int n) { var me = this; return new([.. Enumerable.Range(0, 7).Select(i => me[i] * n)]); }
    public Dimension? Wurzel(int n) { var me = this; return Enumerable.Range(0, 7).All(i => me[i] % n == 0) ? new([.. Enumerable.Range(0, 7).Select(i => me[i] / n)]) : null; }
    public bool Equals(Dimension o) { var me = this; return Enumerable.Range(0, 7).All(i => me[i] == o[i]); }
    public override bool Equals(object? obj) => obj is Dimension d && Equals(d);
    public override int GetHashCode() => HashCode.Combine(this[0], this[1], this[2], this[3], this[4], this[5], this[6]);
    public static bool operator ==(Dimension a, Dimension b) => a.Equals(b);
    public static bool operator !=(Dimension a, Dimension b) => !a.Equals(b);

    /// <summary>„kg·m/s²“ aus den Basiseinheiten.</summary>
    public string SiText()
    {
        if (IstEins) return "1";
        var oben = new List<string>(); var unten = new List<string>();
        foreach (var i in new[] { 1, 0, 2, 3, 4, 5, 6 })
        {
            var e = this[i]; if (e == 0) continue;
            var t = Basis[i] + (Math.Abs(e) == 1 ? "" : HochText(Math.Abs(e)));
            (e > 0 ? oben : unten).Add(t);
        }
        var o = oben.Count == 0 ? "1" : string.Join("·", oben);
        return unten.Count == 0 ? o : o + "/" + (unten.Count == 1 ? unten[0] : "(" + string.Join("·", unten) + ")");
    }

    static string HochText(int n) => string.Concat(n.ToString(CultureInfo.InvariantCulture).Select(c => "⁰¹²³⁴⁵⁶⁷⁸⁹"[c - '0']));

    /// <summary>Was die Dimension beschreibt („Länge“, „Geschwindigkeit“ …) oder die SI-Schreibweise.</summary>
    public string Beschreibung() => Einheiten.Groessenname(this) ?? SiText();
    public override string ToString() => SiText();
}

/// <summary>Physikalische Größe: Wert in SI-Basiseinheiten, absolute Unsicherheit (0 = keine) und Dimension.</summary>
public readonly record struct Groesse(double Wert, Dimension Dim, double Unsicherheit = 0)
{
    public static Groesse Zahl(double v) => new(v, Dimension.Eins);

    static Groesse Fortpflanzen(char op, Groesse a, Groesse b, double wert, Dimension dim)
    {
        if (a.Unsicherheit == 0 && b.Unsicherheit == 0) return new(wert, dim);
        // Gauß'sche Fehlerfortpflanzung aus dem Messwerte-Studio (SPEC-05, StatsPlus.Propagate)
        if (op is '*' or '/' && (a.Wert == 0 || b.Wert == 0)) return new(wert, dim, 0);
        var (_, u) = StatsPlus.Propagate(op, (a.Wert, a.Unsicherheit), (b.Wert, b.Unsicherheit));
        return new(wert, dim, u);
    }

    public static Groesse operator +(Groesse a, Groesse b) => a.Dim != b.Dim ? throw new DimensionsFehler(a.Dim, b.Dim, "+") : Fortpflanzen('+', a, b, a.Wert + b.Wert, a.Dim);
    public static Groesse operator -(Groesse a, Groesse b) => a.Dim != b.Dim ? throw new DimensionsFehler(a.Dim, b.Dim, "−") : Fortpflanzen('-', a, b, a.Wert - b.Wert, a.Dim);
    public static Groesse operator *(Groesse a, Groesse b) => Fortpflanzen('*', a, b, a.Wert * b.Wert, a.Dim * b.Dim);
    public static Groesse operator /(Groesse a, Groesse b) => b.Wert == 0 ? throw new DivideByZeroException("Teilen durch 0 geht nicht.") : Fortpflanzen('/', a, b, a.Wert / b.Wert, a.Dim / b.Dim);
    public static Groesse operator -(Groesse a) => a with { Wert = -a.Wert };

    public Groesse Hoch(Bruch e)
    {
        Dimension dim;
        if (e.IstGanz) dim = Dim.Hoch((int)e.Z);
        else if (e.N <= 12 && Dim.Wurzel((int)e.N) is { } w) dim = w.Hoch((int)e.Z);
        else throw new ArgumentException($"Die Einheit {Dim.SiText()} lässt sich nicht hoch {e} nehmen.");
        var v = e.IstGanz ? Math.Pow(Wert, (double)e.Z) : Math.Pow(Wert, e.AlsDouble());
        var u = Unsicherheit == 0 || Wert == 0 ? 0 : Math.Abs(v) * Math.Abs(e.AlsDouble()) * Unsicherheit / Math.Abs(Wert);
        return new(v, dim, u);
    }
}

public sealed class DimensionsFehler(Dimension a, Dimension b, string op)
    : ArgumentException($"Einheiten passen nicht: {a.Beschreibung()} ({a.SiText()}) {op} {b.Beschreibung()} ({b.SiText()}) geht nicht – addieren und vergleichen lassen sich nur gleiche Größen.")
{
    public Dimension A { get; } = a;
    public Dimension B { get; } = b;
}

/// <summary>Einheit: Faktor zur SI-Basiseinheit und Dimension (°C/°F mit Verschiebung, nur beim Umrechnen eines Einzelwerts).</summary>
public sealed record Einheit(string Symbol, string Name, double Faktor, Dimension Dim, double Verschiebung = 0);

/// <summary>T-0781 Naturwissenschaft: Einheiten mit Dimensionsprüfung – eigene Tabelle (SI-Broschüre, 9. Auflage; Zahlenwerte der Konstanten CODATA 2022).
/// Rechnen mit Einheiten über den Termleser des Mathe-Helfers: „3 km / 20 min in m/s“.</summary>
public static class Einheiten
{
    static readonly Dimension L = new(1), M = new(0, 1), T = new(0, 0, 1), I = new(0, 0, 0, 1), Th = new(0, 0, 0, 0, 1), N = new(0, 0, 0, 0, 0, 1), J0 = new(0, 0, 0, 0, 0, 0, 1);
    static readonly Dimension Kraft = M * L / (T * T), Energie = Kraft * L, Leistung = Energie / T, Druck = Kraft / (L * L), Ladung = I * T, Spannung = Leistung / I;

    static readonly List<Einheit> Liste = Baue();
    static readonly Dictionary<string, Einheit> NachSymbol = Liste.GroupBy(e => e.Symbol, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    public static IReadOnlyList<Einheit> Alle => Liste;

    static List<Einheit> Baue()
    {
        var l = new List<Einheit>();
        void E(string s, string n, double f, Dimension d, bool vorsaetze = false, double off = 0)
        {
            l.Add(new Einheit(s, n, f, d, off));
            if (!vorsaetze) return;
            foreach (var (p, pn, pf) in new[] { ("k", "Kilo", 1e3), ("M", "Mega", 1e6), ("G", "Giga", 1e9), ("T", "Tera", 1e12), ("h", "Hekto", 1e2), ("d", "Dezi", 1e-1), ("c", "Zenti", 1e-2), ("m", "Milli", 1e-3), ("µ", "Mikro", 1e-6), ("u", "Mikro", 1e-6), ("n", "Nano", 1e-9), ("p", "Piko", 1e-12) })
                l.Add(new Einheit(p + s, pn + n.ToLowerInvariant(), pf * f, d));
        }
        E("m", "Meter", 1, L, true);
        E("g", "Gramm", 1e-3, M, true);
        E("t", "Tonne", 1000, M);
        E("s", "Sekunde", 1, T, true);
        E("min", "Minute", 60, T); E("h", "Stunde", 3600, T); E("d", "Tag", 86400, T); E("a", "Jahr", 365.25 * 86400, T);
        E("A", "Ampere", 1, I, true);
        E("K", "Kelvin", 1, Th, true);
        E("°C", "Grad Celsius", 1, Th, false, 273.15);
        E("°F", "Grad Fahrenheit", 5.0 / 9, Th, false, 459.67 * 5 / 9);
        E("mol", "Mol", 1, N, true);
        E("cd", "Candela", 1, J0);
        E("N", "Newton", 1, Kraft, true);
        E("J", "Joule", 1, Energie, true);
        E("Wh", "Wattstunde", 3600, Energie, true);
        E("eV", "Elektronvolt", 1.602176634e-19, Energie, true);
        E("cal", "Kalorie", 4.184, Energie, true);
        E("W", "Watt", 1, Leistung, true);
        E("PS", "Pferdestärke", 735.49875, Leistung);
        E("Pa", "Pascal", 1, Druck, true);
        E("bar", "Bar", 1e5, Druck, true);
        E("atm", "Atmosphäre", 101325, Druck);
        E("Hz", "Hertz", 1, Dimension.Eins / T, true);
        E("C", "Coulomb", 1, Ladung, true);
        E("V", "Volt", 1, Spannung, true);
        E("Ω", "Ohm", 1, Spannung / I, true);
        E("Ohm", "Ohm", 1, Spannung / I, true);
        E("F", "Farad", 1, Ladung / Spannung, true);
        E("T", "Tesla", 1, M / (T * T * I), false);
        E("l", "Liter", 1e-3, L * L * L, true);
        E("L", "Liter", 1e-3, L * L * L, false);
        E("ha", "Hektar", 1e4, L * L);
        E("rad", "Radiant", 1, Dimension.Eins);
        E("°", "Grad", Math.PI / 180, Dimension.Eins);
        E("Da", "Dalton", 1.66053906892e-27, M);
        E("M", "molar (mol/l)", 1000, N / (L * L * L), false);
        // „km/h“ und „mph“ als eigene Wörter
        E("mph", "Meile pro Stunde", 0.44704, L / T);
        E("kn", "Knoten", 1852.0 / 3600, L / T);
        E("Zoll", "Zoll", 0.0254, L); E("ft", "Fuß", 0.3048, L); E("mi", "Meile", 1609.344, L);
        return l;
    }

    /// <summary>Einheit nach Symbol (erst exakt, dann mit Vorsatz).</summary>
    public static Einheit? Finde(string s) => NachSymbol.GetValueOrDefault(s.Trim());

    static readonly (Dimension D, string Name, string Si)[] Benannt =
    [
        (L, "Länge", "m"), (M, "Masse", "kg"), (T, "Zeit", "s"), (I, "Stromstärke", "A"), (Th, "Temperatur", "K"), (N, "Stoffmenge", "mol"),
        (L / T, "Geschwindigkeit", "m/s"), (L / (T * T), "Beschleunigung", "m/s²"), (Kraft, "Kraft", "N"), (Energie, "Energie", "J"), (Leistung, "Leistung", "W"),
        (Druck, "Druck", "Pa"), (Ladung, "Ladung", "C"), (Spannung, "Spannung", "V"), (Spannung / I, "Widerstand", "Ω"), (Ladung / Spannung, "Kapazität", "F"),
        (Dimension.Eins / T, "Frequenz", "Hz"), (L * L, "Fläche", "m²"), (L * L * L, "Volumen", "m³"), (M / (L * L * L), "Dichte", "kg/m³"),
        (M * L / T, "Impuls", "kg·m/s"), (N / (L * L * L), "Konzentration", "mol/m³"), (M / N, "molare Masse", "kg/mol"), (Energie / (M * Th), "spezifische Wärmekapazität", "J/(kg·K)"),
        (Energie / (N * Th), "molare Gaskonstante", "J/(mol·K)"), (Energie * T, "Wirkung", "J·s"), (Dimension.Eins, "Zahl (ohne Einheit)", ""),
    ];

    public static string? Groessenname(Dimension d) => Benannt.FirstOrDefault(b => b.D == d).Name;
    /// <summary>Passende SI-Einheit für die Anzeige („N“ statt „kg·m/s²“).</summary>
    public static string SiEinheit(Dimension d) => Benannt.FirstOrDefault(b => b.D == d) is { Name: not null } b ? b.Si : d.SiText();

    static readonly Regex Wort = new(@"(?<![\p{L}_])(°[CF]?|[\p{L}µΩ]+)(?![\p{L}_])", RegexOptions.Compiled);

    /// <summary>Einheitenausdruck („kg·m/s²“, „km/h“, „J/(mol·K)“) als Größe 1·Einheit.</summary>
    public static Groesse Lies(string einheit)
    {
        var e = (einheit ?? "").Trim();
        if (e.Length == 0 || e == "1") return Groesse.Zahl(1);
        if (Finde(e) is { } direkt) { if (direkt.Verschiebung != 0) return new Groesse(direkt.Faktor, direkt.Dim); return new Groesse(direkt.Faktor, direkt.Dim); }
        return Rechne(e, nurEinheit: true);
    }

    /// <summary>Wert in einer Einheit → SI (°C und °F mit Verschiebung, nur bei einer einfachen Temperatureinheit).</summary>
    public static Groesse InSi(double wert, string einheit, double unsicherheit = 0)
    {
        var e = (einheit ?? "").Trim();
        if (Finde(e) is { Verschiebung: not 0 } t) return new Groesse(wert * t.Faktor + t.Verschiebung, t.Dim, unsicherheit * t.Faktor);
        var g = Lies(e);
        return new Groesse(wert * g.Wert, g.Dim, unsicherheit * Math.Abs(g.Wert));
    }

    /// <summary>SI-Wert in eine Zieleinheit umrechnen (Dimension muss passen).</summary>
    public static (double Wert, double Unsicherheit) Aus(Groesse g, string einheit)
    {
        var e = (einheit ?? "").Trim();
        if (Finde(e) is { Verschiebung: not 0 } t)
        {
            if (g.Dim != t.Dim) throw new DimensionsFehler(g.Dim, t.Dim, "in");
            return ((g.Wert - t.Verschiebung) / t.Faktor, g.Unsicherheit / t.Faktor);
        }
        var z = Lies(e);
        if (z.Dim != g.Dim) throw new ArgumentException($"{g.Dim.Beschreibung()} ({g.Dim.SiText()}) lässt sich nicht in {e} umrechnen – das ist {z.Dim.Beschreibung()} ({z.Dim.SiText()}).");
        return (g.Wert / z.Wert, g.Unsicherheit / Math.Abs(z.Wert));
    }

    /// <summary>Rechnung mit Einheiten: „3 km / 20 min“, „9,81 m/s² · 2 s“, „12 V · 0,5 A“. Bekannte Einheiten werden zu Größen, Zahlen bleiben Zahlen.</summary>
    public static Groesse Rechne(string ausdruck, bool nurEinheit = false)
    {
        var map = new Dictionary<string, Groesse>(StringComparer.Ordinal);
        var unbekannt = new List<string>();
        var text = Wort.Replace(ausdruck.Replace("°C", "°C").Trim(), m =>
        {
            var w = m.Value;
            if (w is "pi" or "π" or "e" or "sqrt" or "exp" && !nurEinheit || Fkt.Namen.Contains(w) && !nurEinheit) return w;
            if (Finde(w) is not { } u) { unbekannt.Add(w); return w; }
            if (u.Verschiebung != 0) throw new ArgumentException($"Mit {u.Symbol} lässt sich nicht rechnen (der Nullpunkt ist verschoben) – rechne in K oder nimm „Umrechnen“.");
            var key = "U_" + map.Count;
            map[key] = new Groesse(u.Faktor, u.Dim);
            return key;
        });
        if (unbekannt.Count > 0) throw new ArgumentException($"Unbekannte Einheit: {string.Join(", ", unbekannt.Distinct())}. Bekannt sind z. B. m, km, s, min, h, kg, g, N, J, kWh, W, Pa, bar, V, A, Ω, mol, K, l.");
        // „3 km / 20 min“: Zahl und direkt folgende Einheit gehören zusammen (sonst rechnete der Leser (3 km / 20)·min);
        // eine Einheitengruppe ohne Leerzeichen („m/s²“, „kg·m/s²“) ist eine Einheit
        const string U = @"U_\d+(?:\^-?\d+|[²³])?";
        text = Regex.Replace(text, $@"(\d[\d.,]*(?:[eE]-?\d+)?)\s*({U}(?:[/·*]{U})*)", m => $"({m.Groups[1].Value}*({m.Groups[2].Value}))");
        var t = TermLeser.Lies(text);
        return Werte(t, map);
    }

    /// <summary>Term mit Größen auswerten – mit Dimensionsprüfung bei + und −, Funktionen nur für Zahlen ohne Einheit.</summary>
    public static Groesse Werte(Term t, IReadOnlyDictionary<string, Groesse> werte) => t switch
    {
        Zahl z => Groesse.Zahl(z.Wert.AlsDouble()),
        Konst k => Groesse.Zahl(k.Wert),
        Var v => werte.TryGetValue(v.Name, out var g) ? g : throw new ArgumentException($"Für „{v.Name}“ fehlt ein Wert."),
        Summe s => s.Glieder.Select(x => Werte(x, werte)).Aggregate((a, b) => a + b),
        Produkt p => p.Faktoren.Select(x => Werte(x, werte)).Aggregate((a, b) => a * b),
        Potenz w when w.Exponent is Zahl e => Werte(w.Basis, werte).Hoch(e.Wert),
        Potenz w when w.Basis is Konst { Name: "e" } => Expo(Werte(w.Exponent, werte)),
        Potenz w => Ohne(Werte(w.Basis, werte), "hoch") is var b && Ohne(Werte(w.Exponent, werte), "hoch") is var ex ? Groesse.Zahl(Math.Pow(b.Wert, ex.Wert)) : default,
        Fkt f => Fn(f.Name, Werte(f.Argument, werte)),
        _ => throw new ArgumentException("Unbekannter Ausdruck."),
    };

    static Groesse Ohne(Groesse g, string was) => g.Dim.IstEins ? g : throw new ArgumentException($"Bei „{was}“ darf keine Einheit stehen ({g.Dim.SiText()}).");
    static Groesse Expo(Groesse g) { Ohne(g, "e hoch"); var v = Math.Exp(g.Wert); return new Groesse(v, Dimension.Eins, v * g.Unsicherheit); }

    static Groesse Fn(string name, Groesse a)
    {
        if (name == "abs") return a with { Wert = Math.Abs(a.Wert) };
        Ohne(a, name);
        var v = name switch
        {
            "sin" => Math.Sin(a.Wert), "cos" => Math.Cos(a.Wert), "tan" => Math.Tan(a.Wert), "asin" => Math.Asin(a.Wert), "acos" => Math.Acos(a.Wert), "atan" => Math.Atan(a.Wert),
            "ln" => Math.Log(a.Wert), "log" => Math.Log10(a.Wert), _ => double.NaN,
        };
        // Unsicherheit über die Ableitung (numerisch)
        var u = 0.0;
        if (a.Unsicherheit > 0) { var h = Math.Max(1e-9, Math.Abs(a.Wert) * 1e-7); u = Math.Abs((Fn(name, Groesse.Zahl(a.Wert + h)).Wert - Fn(name, Groesse.Zahl(a.Wert - h)).Wert) / (2 * h)) * a.Unsicherheit; }
        return new Groesse(v, Dimension.Eins, u);
    }

    /// <summary>Rechnung mit Einheiten und optionaler Zieleinheit („100 km/h in m/s“, „3 kWh in J“, „20 °C in K“) – mit Rechenweg und Dimensionsprüfung.</summary>
    public static (Groesse Ergebnis, string Einheit, string Text, IReadOnlyList<Schritt> Schritte, IReadOnlyList<Probe> Proben) Ausrechnen(string eingabe)
    {
        var e = (eingabe ?? "").Trim();
        if (e.Length == 0) throw new FormatException("Bitte eine Rechnung mit Einheiten eingeben, z. B. 100 km/h in m/s.");
        string? ziel = null;
        var pos = e.LastIndexOf(" in ", StringComparison.Ordinal);
        if (pos > 0) { ziel = e[(pos + 4)..].Trim(); e = e[..pos].Trim(); }
        var schritte = new List<Schritt>();
        Groesse g;
        // Temperatur mit Nullpunkt (°C, °F) nur als Einzelwert umrechnen
        var tm = Regex.Match(e, @"^(-?[\d.,]+)\s*(°[CF]|K)$");
        if (tm.Success)
        {
            var v = double.Parse(tm.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            g = InSi(v, tm.Groups[2].Value);
            schritte.Add(new Schritt("Temperatur in Kelvin (Nullpunkt verschoben: 0 °C = 273,15 K)", $"{Zahl(v)} {tm.Groups[2].Value} = {Zahl(g.Wert)} K"));
        }
        else
        {
            g = Rechne(e);
            schritte.Add(new Schritt("Jede Einheit in SI-Basiseinheiten umrechnen und mitrechnen", $"{e} = {Zahl(g.Wert)} {SiEinheit(g.Dim)}"));
        }
        schritte.Add(new Schritt("Dimension des Ergebnisses", $"{g.Dim.SiText()} – {g.Dim.Beschreibung()}"));
        var einheit = ziel ?? SiEinheit(g.Dim);
        var (w, u) = ziel is null ? (g.Wert, g.Unsicherheit) : Aus(g, ziel);
        var text = Text(w, u, einheit);
        if (ziel is not null) schritte.Add(new Schritt($"In {ziel} umrechnen", $"{Zahl(g.Wert)} {SiEinheit(g.Dim)} = {text}"));
        // Probe: Ergebnis zurück in SI rechnen, muss den SI-Wert wieder ergeben
        var zurueck = InSi(w, einheit);
        var ok = zurueck.Dim == g.Dim && Math.Abs(zurueck.Wert - g.Wert) <= 1e-9 * Math.Max(1e-300, Math.Abs(g.Wert));
        return (g, einheit, text, schritte, [new Probe(ok, $"{(ok ? "✓" : "✗")} Rückrechnung: {text} sind wieder {Zahl(zurueck.Wert)} {SiEinheit(zurueck.Dim)}.")]);
    }

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Zahl mit sinnvoller Stellenzahl (6 gültige Ziffern, große und kleine Werte in Zehnerpotenz).</summary>
    public static string Zahl(double v, int stellen = 6)
    {
        if (!double.IsFinite(v)) return "—";
        if (v == 0) return "0";
        var a = Math.Abs(v);
        if (a >= 1e7 || a < 1e-4)
        {
            var e = (int)Math.Floor(Math.Log10(a)); var m = v / Math.Pow(10, e);
            if (Math.Abs(Math.Round(m, stellen - 1)) >= 10) { m /= 10; e++; }
            return Math.Round(m, stellen - 1).ToString("0.#####", De).Replace('-', '−') + " · 10" + string.Concat(e.ToString(CultureInfo.InvariantCulture).Select(c => c == '-' ? '⁻' : "⁰¹²³⁴⁵⁶⁷⁸⁹"[c - '0']));
        }
        var nach = Math.Max(0, stellen - 1 - (int)Math.Floor(Math.Log10(a)));
        return Math.Round(v, Math.Min(nach, 12)).ToString("#,##0.############", De).Replace('-', '−');
    }

    /// <summary>„20 s“ oder „(9,81 ± 0,02) m/s²“.</summary>
    public static string Text(double wert, double unsicherheit, string einheit)
    {
        var e = einheit.Length == 0 ? "" : " " + einheit;
        if (unsicherheit <= 0) return Zahl(wert) + e;
        // Unsicherheit auf 2 gültige Ziffern, Wert auf dieselbe Stelle
        var st = (int)Math.Floor(Math.Log10(unsicherheit)) - 1;
        var u = Math.Round(unsicherheit / Math.Pow(10, st)) * Math.Pow(10, st);
        var w = Math.Round(wert / Math.Pow(10, st)) * Math.Pow(10, st);
        var nk = Math.Max(0, -st);
        string F(double x) => x.ToString("0." + new string('0', Math.Min(nk, 12)), De).TrimEnd(',').Replace('-', '−');
        return $"({F(w)} ± {F(u)}){e}";
    }
}
