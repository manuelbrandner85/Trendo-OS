using System.Text;

namespace Trendo.Core;

/// <summary>SPEC-08 Ausbau: Kinder-, BH-, Ringgrößen, Textilkennzeichnung, Gradierung, Garn, EAN-13, Staffelpreise, Datenblatt.</summary>
public static class FashionPlus
{
    private static readonly int[] KidSizes = [50, 56, 62, 68, 74, 80, 86, 92, 98, 104, 110, 116, 122, 128, 134, 140, 146, 152, 158, 164, 170, 176];

    /// <summary>F10: Kindergröße = nächste Konfektionsgröße ≥ Körpergröße (DE-Größen in 6-cm-Schritten).</summary>
    public static (int Size, string Age) KidSize(double heightCm)
    {
        if (heightCm < 40 || heightCm > 180) throw new ArgumentException("Körpergröße zwischen 40 und 180 cm.");
        var s = KidSizes.FirstOrDefault(k => k >= heightCm - 1); if (s == 0) s = 176;
        var age = s switch { <= 56 => "0–1 Monat", <= 62 => "2–3 Monate", <= 68 => "4–6 Monate", <= 74 => "7–9 Monate", <= 80 => "10–12 Monate", <= 86 => "1–1½ Jahre", <= 92 => "2 Jahre", _ => $"ca. {(s - 80) / 6} Jahre" };
        return (s, age);
    }

    /// <summary>F11: BH-Größe (EU): Unterbrustband = auf 5 cm gerundet; Cup aus Differenz Brust − Unterbrust (12–14 = A, je 2 cm weiter).</summary>
    public static string BraSize(double underbust, double bust)
    {
        if (underbust < 58 || underbust > 130 || bust <= underbust) throw new ArgumentException("Unterbrust 58–130 cm, Brustumfang größer als Unterbrust.");
        var band = (int)(Math.Round(underbust / 5, MidpointRounding.AwayFromZero) * 5); var diff = bust - underbust;
        string[] cups = ["AA", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K"]; var i = (int)Math.Floor((diff - 10) / 2); i = Math.Clamp(i, 0, cups.Length - 1);
        return $"{band}{cups[i]}";
    }

    /// <summary>F12: Ringgröße EU = Innenumfang in mm; Durchmesser = Umfang/π; US ≈ (Umfang − 36,5)/2,55 (Halbgrößen).</summary>
    public static (double Eu, double DiameterMm, double Us) Ring(double circumferenceMm)
    {
        if (circumferenceMm < 38 || circumferenceMm > 76) throw new ArgumentException("Innenumfang zwischen 38 und 76 mm.");
        var us = Math.Round((circumferenceMm - 36.5) / 2.55 * 2, MidpointRounding.AwayFromZero) / 2;
        return (Math.Round(circumferenceMm), Math.Round(circumferenceMm / Math.PI, 1), Math.Max(0, us));
    }

    private static readonly Dictionary<string, string> Fibres = new(StringComparer.OrdinalIgnoreCase)
    {
        ["baumwolle"] = "Baumwolle", ["cotton"] = "Baumwolle", ["polyester"] = "Polyester", ["elasthan"] = "Elasthan", ["spandex"] = "Elasthan", ["wolle"] = "Wolle", ["schurwolle"] = "Schurwolle",
        ["viskose"] = "Viskose", ["leinen"] = "Leinen", ["seide"] = "Seide", ["polyamid"] = "Polyamid", ["nylon"] = "Polyamid", ["acryl"] = "Polyacryl", ["polyacryl"] = "Polyacryl", ["kaschmir"] = "Kaschmir", ["modal"] = "Modal", ["lyocell"] = "Lyocell",
    };

    /// <summary>F13: Textilkennzeichnung (EU 1007/2011): Summe 100 %, zulässige Faserbezeichnungen, absteigend sortiert.</summary>
    public static (bool Ok, string Label, IReadOnlyList<string> Problems) TextileLabel(string text)
    {
        var p = new List<string>(); var parts = new List<(string Name, double Pct)>();
        foreach (var raw in text.Split([',', ';', '/', '+'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = raw.Trim(); var m = System.Text.RegularExpressions.Regex.Match(t, @"(\d+(?:[.,]\d+)?)\s*%?\s*(.+)|(.+?)\s+(\d+(?:[.,]\d+)?)\s*%");
            if (!m.Success) { p.Add($"„{t}“: bitte als „95 % Baumwolle“ angeben."); continue; }
            var (pct, name) = m.Groups[1].Success ? (Fach.Num(m.Groups[1].Value), m.Groups[2].Value.Trim()) : (Fach.Num(m.Groups[4].Value), m.Groups[3].Value.Trim());
            if (!Fibres.TryGetValue(name, out var std)) { p.Add($"„{name}“ ist keine zulässige Faserbezeichnung (z. B. Baumwolle, Polyester, Elasthan)."); std = name; }
            else if (!std.Equals(name, StringComparison.OrdinalIgnoreCase)) p.Add($"„{name}“ muss „{std}“ heißen.");
            parts.Add((std, pct));
        }
        var sum = parts.Sum(x => x.Pct); if (Math.Abs(sum - 100) > 0.01) p.Add($"Die Anteile ergeben {Fach.F(sum, 1)} % statt 100 %.");
        return (p.Count == 0, string.Join(", ", parts.OrderByDescending(x => x.Pct).Select(x => $"{Fach.F(x.Pct, x.Pct % 1 == 0 ? 0 : 1)} % {x.Name}")), p);
    }

    /// <summary>F14: Gradierung: Maß der Basisgröße plus Sprung je Größenschritt.</summary>
    public static IReadOnlyList<(string Size, double Value)> Grade(IReadOnlyList<string> sizes, string baseSize, double baseValue, double step)
    {
        var bi = sizes.ToList().FindIndex(s => s.Equals(baseSize.Trim(), StringComparison.OrdinalIgnoreCase)); if (bi < 0) throw new ArgumentException("Die Basisgröße steht nicht in der Größenliste.");
        return sizes.Select((s, i) => (s, Math.Round(baseValue + (i - bi) * step, 1))).ToList();
    }

    /// <summary>F15: Garnbedarf aus Maschenprobe (Maschen × Reihen auf 10 × 10 cm) und Lauflänge (m je Knäuel).</summary>
    public static (int Stitches, int Rows, double Meters, int Balls) Yarn(double widthCm, double heightCm, double stitches10, double rows10, double metersPerBall, double cmPerStitch = 3.0, double reservePct = 10)
    {
        if (widthCm <= 0 || heightCm <= 0 || stitches10 <= 0 || rows10 <= 0 || metersPerBall <= 0) throw new ArgumentException("Alle Werte müssen größer als 0 sein.");
        var s = (int)Math.Round(widthCm * stitches10 / 10); var r = (int)Math.Round(heightCm * rows10 / 10);
        var m = s * r * (cmPerStitch * 10 / stitches10) / 100 * (1 + reservePct / 100); // Garn je Masche ~ 3 Maschenbreiten
        return (s, r, Math.Round(m), (int)Math.Ceiling(m / metersPerBall));
    }

    /// <summary>F16: EAN-13: Prüfziffer berechnen bzw. prüfen.</summary>
    public static int EanCheckDigit(string twelve)
    {
        var d = new string(twelve.Where(char.IsDigit).ToArray()); if (d.Length != 12) throw new FormatException("Bitte die ersten 12 Ziffern eingeben.");
        var sum = d.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum(); return (10 - sum % 10) % 10;
    }
    public static bool EanValid(string ean) { var d = new string(ean.Where(char.IsDigit).ToArray()); return d.Length == 13 && EanCheckDigit(d[..12]) == d[12] - '0'; }

    /// <summary>F17: Staffelpreise: Stufen „ab Menge → Rabatt %“; Preis je Stück und gesamt.</summary>
    public static (double Unit, double Total, double DiscountPct) Tier(double listPrice, int qty, IEnumerable<(int From, double Pct)> tiers)
    {
        if (qty < 1 || listPrice < 0) throw new ArgumentException("Menge ab 1, Preis nicht negativ.");
        var pct = tiers.Where(t => qty >= t.From).Select(t => t.Pct).DefaultIfEmpty(0).Max(); var unit = Math.Round(listPrice * (1 - pct / 100), 2);
        return (unit, Math.Round(unit * qty, 2), pct);
    }

    /// <summary>F18: Produktdatenblatt (Markdown).</summary>
    public static string DataSheet(string name, string sku, string description, string material, IEnumerable<ProductVariant> variants, PriceCalc? price, string care = "")
    {
        var vs = variants.ToList(); var sb = new StringBuilder($"# {name}\n\nArtikelnummer: {sku}\n\n{description}\n\n## Material\n\n{material}\n");
        if (care != "") sb.Append($"\n## Pflege\n\n{care}\n");
        if (vs.Count > 0) { sb.Append($"\n## Varianten ({vs.Count})\n\n| SKU | Größe | Farbe |\n|---|---|---|\n"); foreach (var v in vs) sb.Append($"| {v.Sku} | {v.Size} | {v.Color} |\n"); }
        if (price is not null) sb.Append($"\n## Preis\n\n{Fach.F(price.Gross)} € inkl. MwSt. ({Fach.F(price.Net)} € netto)\n");
        return sb.ToString();
    }
}
