using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Pod;

/// <summary>Produktart mit typischem Druckbereich. Die Maße sind Richtwerte; die genauen Werte nennt der Anbieter je Artikel.</summary>
public sealed record PodProduct(string Id, string Name, double PrintWidthCm, double PrintHeightCm, bool NeedsTransparency);

public enum PodLevel { Gut, Pruefen, Problem }
public sealed record PodFinding(PodLevel Level, string Text);
public sealed record PodCheckResult(double Dpi, double PrintedWidthCm, double PrintedHeightCm, double Coverage, IReadOnlyList<PodFinding> Findings)
{
    public PodLevel Level => Findings.Count == 0 ? PodLevel.Gut : Findings.Max(f => f.Level);
}

/// <summary>Print on Demand P3: Druckdatei gegen den Druckbereich prüfen – reine Rechnung aus Pixelmaßen und Transparenz, ohne Dienst im Internet.</summary>
public static class PodCheck
{
    public const double MinDpi = 150, GoodDpi = 300;

    public static readonly IReadOnlyList<PodProduct> Products =
    [
        new("tshirt", "T-Shirt", 30, 40, true),
        new("hoodie", "Kapuzenpullover", 30, 40, true),
        new("beutel", "Stoffbeutel", 30, 35, true),
        new("tasse", "Tasse", 20, 8.5, false),
        new("poster", "Poster 30 × 40 cm", 30, 40, false),
    ];

    public static PodProduct Product(string? id) => Products.FirstOrDefault(p => p.Id == id) ?? Products[0];

    public static PodCheckResult Check(PodProduct p, int width, int height, bool hasAlpha)
    {
        if (width <= 0 || height <= 0) return new(0, 0, 0, 0, [new(PodLevel.Problem, "Das Bild konnte nicht gelesen werden. Möglich sind PNG und JPEG.")]);
        double wIn = p.PrintWidthCm / 2.54, hIn = p.PrintHeightCm / 2.54;
        var dpi = Math.Max(width / wIn, height / hIn); // das Bild passt ganz in den Druckbereich, die engere Seite bestimmt die Größe
        double printedW = width / dpi * 2.54, printedH = height / dpi * 2.54;
        var coverage = printedW * printedH / (p.PrintWidthCm * p.PrintHeightCm);
        var findings = new List<PodFinding>();
        var needW = (int)Math.Ceiling(wIn * GoodDpi); var needH = (int)Math.Ceiling(hIn * GoodDpi);
        if (dpi < MinDpi - 1) findings.Add(new(PodLevel.Problem, $"Nur {dpi:0} DPI: Das wird im Druck unscharf. Mindestens {MinDpi:0} DPI, besser {needW} × {needH} Pixel."));
        else if (dpi < GoodDpi - 1) findings.Add(new(PodLevel.Pruefen, $"{dpi:0} DPI: brauchbar, aber nicht ideal. Am besten {needW} × {needH} Pixel."));
        if (p.NeedsTransparency && !hasAlpha) findings.Add(new(PodLevel.Pruefen, "Das Bild hat keinen durchsichtigen Hintergrund. Auf Stoff wird der Hintergrund als Rechteck mitgedruckt."));
        if (coverage < 0.5) findings.Add(new(PodLevel.Pruefen, $"Das Motiv füllt nur {coverage * 100:0} % des Druckbereichs."));
        if (!p.NeedsTransparency && coverage < 0.98) findings.Add(new(PodLevel.Pruefen, "Das Seitenverhältnis passt nicht ganz: Es bleiben Ränder, oder der Anbieter schneidet zu."));
        return new(dpi, printedW, printedH, coverage, findings);
    }

    /// <summary>Hat die Datei einen Alphakanal? PNG: Farbtyp 4 oder 6 oder ein tRNS-Block; JPEG kennt keine Transparenz.</summary>
    public static bool HasAlpha(byte[] d)
    {
        if (d.Length < 26 || d[0] != 0x89 || d[1] != 'P') return false;
        if (d[25] is 4 or 6) return true;
        for (var i = 8; i + 8 <= d.Length;)
        {
            var len = d[i] << 24 | d[i + 1] << 16 | d[i + 2] << 8 | d[i + 3];
            if (len < 0) return false;
            var type = Encoding.ASCII.GetString(d, i + 4, 4);
            if (type == "tRNS") return true;
            if (type is "IDAT" or "IEND") return false;
            i += 12 + len;
        }
        return false;
    }
}

public sealed record PodPriceInput(decimal ProductCost, decimal Shipping, decimal FeePercent, decimal FeeFixed, decimal VatPercent);
public sealed record PodPriceResult(decimal Gross, decimal Net, decimal Vat, decimal Fees, decimal Profit, decimal MarginPercent);

/// <summary>Print on Demand P6: Preis und Gewinn je Stück in Decimal. Herstell- und Versandkosten sind Nettowerte. Mit 0 % Steuer rechnet es wie bei der Kleinunternehmer-Regelung.</summary>
public static class PodPrice
{
    public static PodPriceResult Calc(PodPriceInput p, decimal gross)
    {
        var net = gross / (1 + p.VatPercent / 100m);
        var fees = gross * p.FeePercent / 100m + p.FeeFixed;
        var profit = net - p.ProductCost - p.Shipping - fees;
        return new(R(gross), R(net), R(gross - net), R(fees), R(profit), net == 0 ? 0 : Math.Round(profit / net * 100m, 1));
    }

    /// <summary>Verkaufspreis brutto für einen gewünschten Gewinn je Stück; null, wenn die Gebühren zusammen mit der Steuer jeden Preis auffressen.</summary>
    public static decimal? ForProfit(PodPriceInput p, decimal profit)
    {
        var keep = 1 / (1 + p.VatPercent / 100m) - p.FeePercent / 100m;
        return keep <= 0 ? null : Math.Ceiling((profit + p.ProductCost + p.Shipping + p.FeeFixed) / keep * 100m) / 100m;
    }

    static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Print on Demand P7/P8: Hinweise, keine Rechtsberatung. Die Wortliste kann fremde Marken nie vollständig finden; sie fängt nur die häufigsten Fälle.</summary>
public static partial class PodLegal
{
    public const string DpmaSearch = "https://register.dpma.de/DPMAregister/marke/einsteiger", EuipoSearch = "https://euipo.europa.eu/eSearch/";

    static readonly string[] Marks =
    [
        "Adidas", "Nike", "Puma", "Gucci", "Chanel", "Louis Vuitton", "Supreme", "Apple", "Google", "Coca-Cola", "Ferrari", "Porsche", "BMW", "Mercedes", "Harley-Davidson",
        "Disney", "Pixar", "Marvel", "Star Wars", "Harry Potter", "Pokémon", "Pokemon", "Nintendo", "Super Mario", "Hello Kitty", "Barbie", "Lego", "Minecraft", "Fortnite", "Batman", "Superman", "Spider-Man", "Mickey Mouse",
        "FC Bayern", "Borussia Dortmund", "Real Madrid", "NBA", "FIFA", "Bundesliga", "Olympia",
    ];

    /// <summary>Bekannte Marken und Figuren im Text. Keine Treffer heißt nicht, dass der Text frei ist.</summary>
    public static IReadOnlyList<string> MarkHits(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return Marks.Where(m => Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(m) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();
    }

    public static readonly IReadOnlyList<(string Id, string Text)> Checklist =
    [
        ("rechte", "Ich habe die Rechte am Motiv. KI-Motive habe ich auf fremde Marken, Figuren und Gesichter geprüft."),
        ("marke", "Ich habe Text und Name des Motivs in den Markenregistern gesucht."),
        ("gewerbe", "Ich habe mich informiert, ob ich ein Gewerbe anmelden muss."),
        ("impressum", "Mein Shop hat Impressum, Datenschutzerklärung, AGB und Widerrufsbelehrung."),
        ("gpsr", "Ich habe die Herstellerangaben nach der EU-Produktsicherheitsverordnung (GPSR) beim Produkt hinterlegt. Wer als Hersteller gilt, habe ich geklärt."),
        ("verpackung", "Ich habe geprüft, ob ich mich im Verpackungsregister LUCID eintragen und Verpackungen lizenzieren muss."),
        ("textil", "Bei Textilien stimmen die Angaben zu Material und Pflege."),
        ("steuer", "Ich weiß, ob ich Umsatzsteuer ausweise oder die Kleinunternehmer-Regelung nutze."),
    ];

    public static string Listing(string title, string description, IEnumerable<string> tags, PodProduct product, decimal gross)
    {
        var sb = new StringBuilder($"# {title}\n\n");
        sb.Append(CultureInfo.GetCultureInfo("de-DE"), $"Produkt: {product.Name}  \nPreis: {gross:0.00} € inkl. Steuer\n\n{description.Trim()}\n\n");
        var list = tags.Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (list.Count > 0) sb.Append("Schlagwörter: ").Append(string.Join(", ", list)).Append('\n');
        return sb.ToString();
    }
}
