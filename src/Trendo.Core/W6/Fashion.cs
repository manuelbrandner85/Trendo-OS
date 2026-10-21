using System.Text;

namespace Trendo.Core;

public sealed record ProductVariant(string Sku, string Size, string Color, string Material);
public sealed record PriceCalc(double Cost, double Net, double Gross, double Vat, double MarginPct);

/// <summary>SPEC-08: Mode & Produkt – Größen umrechnen, Größe aus Maßen, Stoffbedarf, Varianten (SKU), Preiskalkulation.</summary>
public static class Fashion
{
    // Damen-Konfektion: EU-Größe → Brust/Taille/Hüfte (cm), US, UK
    private static readonly (int Eu, int Bust, int Waist, int Hip, string Us, int Uk, string Letter)[] Women =
    [
        (32, 76, 60, 84, "0", 4, "XXS"), (34, 80, 64, 88, "2", 6, "XS"), (36, 84, 68, 92, "4", 8, "S"), (38, 88, 72, 96, "6", 10, "M"), (40, 92, 76, 100, "8", 12, "M"),
        (42, 96, 80, 104, "10", 14, "L"), (44, 100, 84, 108, "12", 16, "L"), (46, 106, 90, 114, "14", 18, "XL"), (48, 112, 96, 120, "16", 20, "XXL"), (50, 118, 102, 126, "18", 22, "3XL"),
    ];
    // Herren: EU-Größe → Brust/Taille
    private static readonly (int Eu, int Chest, int Waist, string Letter)[] Men =
    [
        (44, 88, 76, "XS"), (46, 92, 80, "S"), (48, 96, 84, "M"), (50, 100, 88, "M"), (52, 104, 92, "L"), (54, 108, 98, "XL"), (56, 112, 104, "XL"), (58, 116, 110, "XXL"), (60, 120, 116, "3XL"),
    ];

    public static string WomenSize(int eu)
    {
        var r = Women.FirstOrDefault(x => x.Eu == eu); if (r.Eu == 0) throw new ArgumentException("Damengrößen EU 32 bis 50 (gerade Zahlen).");
        return $"EU {r.Eu} = US {r.Us} = UK {r.Uk} = {r.Letter} (Brust {r.Bust}, Taille {r.Waist}, Hüfte {r.Hip} cm)";
    }

    /// <summary>Größe aus Körpermaßen: die kleinste Größe, bei der kein Maß zu eng ist.</summary>
    public static (int Eu, string Letter, string Note) FromBody(bool women, double chest, double waist, double hip = 0)
    {
        if (women)
        {
            var r = Women.FirstOrDefault(x => x.Bust >= chest - 1 && x.Waist >= waist - 1 && x.Hip >= hip - 1);
            if (r.Eu == 0) return (52, "4XL", "Größer als die Tabelle – bitte beim Hersteller nachsehen.");
            var spread = new[] { Women.First(x => x.Bust >= chest - 1).Eu, Women.First(x => x.Waist >= waist - 1).Eu, hip > 0 ? Women.First(x => x.Hip >= hip - 1).Eu : 0 }.Where(x => x > 0).ToArray();
            return (r.Eu, r.Letter, spread.Max() - spread.Min() >= 4 ? "Maße liegen in verschiedenen Größen – Schnitt mit Stretch oder Änderung empfohlen." : "Passt gut.");
        }
        var m = Men.FirstOrDefault(x => x.Chest >= chest - 1 && x.Waist >= waist - 1);
        return m.Eu == 0 ? (62, "4XL", "Größer als die Tabelle.") : (m.Eu, m.Letter, "Passt gut.");
    }

    /// <summary>Schuhe: EU ↔ Fußlänge (cm) ↔ UK ↔ US (Herren; US Herren = UK + 1, US Damen = UK + 2,5).</summary>
    public static (double Eu, double Cm, double Uk, double UsMen, double UsWomen) Shoe(double eu)
    {
        if (eu < 16 || eu > 52) throw new ArgumentException("Schuhgrößen EU 16 bis 52.");
        var last = eu * 2 / 3.0; var cm = last - 1.5; var uk = Math.Round((3 * last / 2.54 - 25) * 2, MidpointRounding.AwayFromZero) / 2;
        return (eu, Math.Round(cm, 1), uk, uk + 1, uk + 2.5);
    }
    public static double ShoeFromFoot(double footCm) => Math.Round((footCm + 1.5) * 1.5 * 2, MidpointRounding.AwayFromZero) / 2;

    /// <summary>Stoffbedarf: Schnittteile (B × H cm) auf Stoffbreite legen (Regalpacken), plus Nahtzugabe und 10 % Reserve.</summary>
    public static (double Meters, int Rows) Fabric(IEnumerable<(double W, double H, int Qty)> pieces, double fabricWidthCm = 140, double seam = 1.5)
    {
        var all = pieces.SelectMany(p => Enumerable.Repeat((W: p.W + 2 * seam, H: p.H + 2 * seam), p.Qty))
            .Select(p => p.W > fabricWidthCm && p.H <= fabricWidthCm ? (W: p.H, H: p.W) : p).OrderByDescending(p => p.H).ToList();
        if (all.Any(p => p.W > fabricWidthCm)) throw new ArgumentException("Ein Schnittteil ist breiter als der Stoff.");
        double len = 0, rowW = 0, rowH = 0; var rows = 0;
        foreach (var p in all) { if (rowW + p.W > fabricWidthCm) { len += rowH; rowW = 0; rowH = 0; } if (rowW == 0) rows++; rowW += p.W; rowH = Math.Max(rowH, p.H); }
        len += rowH;
        return (Math.Ceiling(len * 1.1 / 10) / 10, rows); // auf 10 cm aufgerundet, in m
    }

    public static IReadOnlyList<ProductVariant> Variants(string baseSku, IEnumerable<string> sizes, IEnumerable<string> colors, string material = "")
    {
        string Code(string s) => new string(s.ToUpperInvariant().Replace("Ä", "AE").Replace("Ö", "OE").Replace("Ü", "UE").Replace("ß", "SS").Where(char.IsLetterOrDigit).ToArray()) is var c && c.Length > 0 ? c[..Math.Min(4, c.Length)] : "X";
        var b = Code(baseSku.Length > 0 ? baseSku : "ART"); if (baseSku.Trim().Length > 0) b = new string(baseSku.ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());
        var cl = colors.Select(c => c.Trim()).Where(c => c != "").Distinct().ToList(); var sz = sizes.Select(s => s.Trim()).Where(s => s != "").Distinct().ToList();
        if (cl.Count == 0 || sz.Count == 0) throw new ArgumentException("Bitte mindestens eine Größe und eine Farbe angeben.");
        return cl.SelectMany(c => sz.Select(s => new ProductVariant($"{b}-{Code(c)}-{Code(s)}", s, c, material))).ToList();
    }

    public static string VariantsCsv(IEnumerable<ProductVariant> v)
    {
        var sb = new StringBuilder("SKU;Größe;Farbe;Material\n");
        foreach (var x in v) sb.Append($"{x.Sku};{x.Size};{x.Color};{x.Material}\n");
        return sb.ToString();
    }

    /// <summary>Kalkulation: Material + Arbeit + Gemeinkosten-% → Selbstkosten; + Gewinn-% → netto; + MwSt → brutto, auf ,90 / ,99 gerundet.</summary>
    public static PriceCalc Price(double material, double labour, double overheadPct, double profitPct, double vatPct = 19 /* LegalRates.VatStandard, § 12 Abs. 1 UStG */, bool charm = true)
    {
        if (material < 0 || labour < 0) throw new ArgumentException("Kosten dürfen nicht negativ sein.");
        var cost = (material + labour) * (1 + overheadPct / 100); var net = cost * (1 + profitPct / 100); var gross = net * (1 + vatPct / 100);
        if (charm && gross >= 1) gross = Math.Ceiling(gross) - (gross < 10 ? 0.01 : 0.10);
        var netFinal = gross / (1 + vatPct / 100);
        return new(Math.Round(cost, 2), Math.Round(netFinal, 2), Math.Round(gross, 2), Math.Round(gross - netFinal, 2), Math.Round(netFinal <= 0 ? 0 : (netFinal - cost) / netFinal * 100, 1));
    }
}
