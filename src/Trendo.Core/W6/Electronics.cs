using System.Text;

namespace Trendo.Core;

public sealed record BomLine(string Ref, string Value, string Part, int Qty, double UnitPrice);

/// <summary>SPEC-02: Elektronik – Farbcode, Ohmsches Gesetz, LED-Vorwiderstand, Spannungsteiler (E-Reihen), Stückliste.</summary>
public static class Electronics
{
    public static readonly string[] Colors = ["schwarz", "braun", "rot", "orange", "gelb", "grün", "blau", "violett", "grau", "weiß"];
    private static readonly Dictionary<string, double> Tol = new() { ["braun"] = 1, ["rot"] = 2, ["grün"] = 0.5, ["blau"] = 0.25, ["violett"] = 0.1, ["gold"] = 5, ["silber"] = 10 };
    public static readonly double[] E12 = [1.0, 1.2, 1.5, 1.8, 2.2, 2.7, 3.3, 3.9, 4.7, 5.6, 6.8, 8.2];
    public static readonly double[] E24 = [1.0, 1.1, 1.2, 1.3, 1.5, 1.6, 1.8, 2.0, 2.2, 2.4, 2.7, 3.0, 3.3, 3.6, 3.9, 4.3, 4.7, 5.1, 5.6, 6.2, 6.8, 7.5, 8.2, 9.1];

    /// <summary>Liest 4- oder 5-Ring-Farbcode („gelb violett rot gold“) → Ohm und Toleranz.</summary>
    public static (double Ohm, double TolerancePct) DecodeBands(string bands)
    {
        var b = bands.ToLowerInvariant().Replace("gruen", "grün").Replace("weiss", "weiß").Split([' ', ',', '-', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (b.Length is < 3 or > 5) throw new ArgumentException("Bitte 3 bis 5 Farben angeben, z. B. „gelb violett rot gold“.");
        int Dig(string c) { var i = Array.IndexOf(Colors, c); return i >= 0 ? i : throw new ArgumentException($"Unbekannte Ringfarbe „{c}“."); }
        var digits = b.Length == 5 ? 3 : 2;
        double v = 0; for (var i = 0; i < digits; i++) v = v * 10 + Dig(b[i]);
        var m = b[digits] switch { "gold" => -1, "silber" => -2, var c => Dig(c) };
        var tol = b.Length > digits + 1 ? (Tol.TryGetValue(b[digits + 1], out var t) ? t : throw new ArgumentException($"„{b[digits + 1]}“ ist keine Toleranzfarbe.")) : 20;
        return (Math.Round(v * Math.Pow(10, m), 6), tol);
    }

    /// <summary>Wert → 4-Ring-Farbcode (2 Ziffern, Multiplikator, gold).</summary>
    public static string EncodeBands(double ohm)
    {
        if (ohm < 0.1 || ohm >= 1e10) throw new ArgumentException("Wert außerhalb von 0,1 Ω bis 10 GΩ.");
        var exp = (int)Math.Floor(Math.Log10(ohm)) - 1; var two = (int)Math.Round(ohm / Math.Pow(10, exp));
        if (two >= 100) { two /= 10; exp++; }
        var mult = exp switch { -1 => "gold", -2 => "silber", _ => Colors[exp] };
        return $"{Colors[two / 10]} {Colors[two % 10]} {mult} gold";
    }

    public static string Ohm(double v) => v >= 1e6 ? Fach.F(v / 1e6, 2) + " MΩ" : v >= 1e3 ? Fach.F(v / 1e3, 2) + " kΩ" : Fach.F(v, 2) + " Ω";

    /// <summary>Ohmsches Gesetz: zwei von U, I, R angeben (andere 0) → alle drei plus Leistung.</summary>
    public static (double U, double I, double R, double P) OhmsLaw(double u, double i, double r)
    {
        var given = (u > 0 ? 1 : 0) + (i > 0 ? 1 : 0) + (r > 0 ? 1 : 0);
        if (given != 2) throw new ArgumentException("Gib genau zwei Werte an und lass den dritten auf 0.");
        if (u <= 0) u = i * r; else if (i <= 0) i = u / r; else r = u / i;
        return (u, i, r, u * i);
    }

    public static double Nearest(double v, double[] series, bool roundUp = false)
    {
        var dec = Math.Pow(10, Math.Floor(Math.Log10(v)));
        var cands = series.Select(s => s * dec).Concat(series.Select(s => s * dec * 10)).Concat(series.Select(s => s * dec / 10));
        return Math.Round(roundUp ? cands.Where(c => c >= v - 1e-9).Min() : cands.OrderBy(c => Math.Abs(c - v)).First(), 6);
    }

    /// <summary>Vorwiderstand für LEDs: nächster E12-Wert nach oben, Leistung im Widerstand.</summary>
    public static (double Exact, double E12, double Current, double Power) LedResistor(double supply, double ledVolt, double mA, int count = 1)
    {
        var drop = supply - ledVolt * count;
        if (drop <= 0) throw new ArgumentException("Die Versorgung ist zu klein für diese LED(s) in Reihe.");
        if (mA <= 0) throw new ArgumentException("Der Strom muss größer als 0 sein.");
        var exact = drop / (mA / 1000); var e = Nearest(exact, E12, true); var i = drop / e;
        return (exact, e, i * 1000, drop * i);
    }

    /// <summary>Spannungsteiler: beste E24-Kombination für Wunschspannung (R2 fest oder gesucht).</summary>
    public static (double R1, double R2, double Vout, double ErrorPct) Divider(double vin, double vout, double r2 = 10000)
    {
        if (vout <= 0 || vout >= vin) throw new ArgumentException("Die Ausgangsspannung muss zwischen 0 und der Eingangsspannung liegen.");
        var best = (R1: 0.0, R2: 0.0, V: 0.0, E: double.MaxValue);
        foreach (var dec in new[] { 1e2, 1e3, 1e4, 1e5 })
            foreach (var a in E24)
            {
                var r1 = a * dec; var v = vin * r2 / (r1 + r2); var e = Math.Abs(v - vout) / vout * 100;
                if (e < best.E) best = (r1, r2, v, e);
            }
        return (best.R1, best.R2, Math.Round(best.V, 4), Math.Round(best.E, 3));
    }

    /// <summary>Stückliste aus Zeilen „R1, 10k, 0805, 2, 0,02“ – gruppiert gleiche Teile, summiert.</summary>
    public static IReadOnlyList<BomLine> ParseBom(string text)
    {
        var res = new List<BomLine>(); var n = 0;
        foreach (var raw in text.Split('\n'))
        {
            n++; var l = raw.Trim(); if (l == "" || l.StartsWith('#')) continue;
            var c = l.Split(';').Length >= 3 ? l.Split(';') : l.Split(',', 5);
            if (c.Length < 3) throw new FormatException($"Zeile {n}: Bitte mindestens „Bauteil, Wert, Gehäuse“ angeben.");
            var qty = c.Length > 3 && c[3].Trim() != "" ? (int)Fach.Num(c[3], "Menge") : 1;
            var price = c.Length > 4 && c[4].Trim() != "" ? Fach.Num(c[4], "Preis") : 0;
            res.Add(new(c[0].Trim(), c[1].Trim(), c[2].Trim(), qty, price));
        }
        return res.GroupBy(b => (b.Value.ToLowerInvariant(), b.Part.ToLowerInvariant()))
            .Select(g => new BomLine(string.Join(" ", g.Select(x => x.Ref)), g.First().Value, g.First().Part, g.Sum(x => x.Qty), g.Max(x => x.UnitPrice))).ToList();
    }

    public static string BomCsv(IEnumerable<BomLine> bom)
    {
        var sb = new StringBuilder("Bauteile;Wert;Gehäuse;Menge;Stückpreis;Summe\n");
        foreach (var b in bom) sb.Append($"{b.Ref};{b.Value};{b.Part};{b.Qty};{Fach.F(b.UnitPrice, 3)};{Fach.F(b.Qty * b.UnitPrice)}\n");
        return sb.ToString();
    }
}
