using System.Globalization;

namespace Trendo.Core;

/// <summary>Verkaufen – Vorbild-Funktionen (SumUp, Square, Shopify POS; nur Funktionen, Clean-Room, 04.11.2026):
/// Artikelnummer/EAN für Scanner, Prüfziffer, Auswertung über einen Zeitraum (Umsatz, Bons, Durchschnittsbon, Bestseller, Wochentage, Stoßzeiten, Vergleich zum Zeitraum davor).</summary>
public static class SalesStats
{
    /// <summary>EAN-13/EAN-8/GTIN: Prüfziffer stimmt (gewichtete Quersumme 3-1).</summary>
    public static bool EanValid(string code)
    {
        if (code.Length is not (8 or 12 or 13 or 14) || !code.All(char.IsDigit)) return false;
        var sum = 0;
        for (var i = 0; i < code.Length - 1; i++) { var d = code[code.Length - 2 - i] - '0'; sum += i % 2 == 0 ? d * 3 : d; }
        return (10 - sum % 10) % 10 == code[^1] - '0';
    }

    /// <summary>Artikelnummer prüfen: frei wählbar (Buchstaben, Ziffern, - _ .), aber reine Ziffernfolgen in EAN-Länge müssen eine gültige Prüfziffer haben.</summary>
    public static string CheckSku(string sku)
    {
        var s = sku.Trim();
        if (s.Length == 0) return "";
        if (s.Length > 32 || !s.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')) throw new ArgumentException("Artikelnummer: höchstens 32 Zeichen, nur Buchstaben, Ziffern und - _ .");
        if (s.All(char.IsDigit) && s.Length is 8 or 13 && !EanValid(s)) throw new ArgumentException($"„{s}“ sieht aus wie ein Strichcode, aber die Prüfziffer stimmt nicht – bitte noch einmal scannen oder abtippen.");
        return s;
    }

    /// <summary>Artikel per Scanner oder Eingabe finden: zuerst genaue Artikelnummer, dann genauer Name (Groß/klein egal).</summary>
    public static Product? Find(IEnumerable<Product> products, string input)
    {
        var q = input.Trim(); if (q.Length == 0) return null;
        var list = products.ToList();
        return list.FirstOrDefault(p => p.Sku.Length > 0 && string.Equals(p.Sku, q, StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(p => string.Equals(p.Name, q, StringComparison.CurrentCultureIgnoreCase));
    }

    public sealed record Report(DateOnly From, DateOnly To, int Bons, decimal Gross, decimal Average, decimal Discounts, int Cancelled,
        List<(string Product, decimal Qty, decimal Gross)> Top, decimal[] ByWeekday, decimal[] ByHour, decimal PreviousGross)
    {
        /// <summary>Veränderung zum gleich langen Zeitraum davor in Prozent (null, wenn es davor nichts gab).</summary>
        public double? Change => PreviousGross <= 0 ? null : Math.Round((double)((Gross - PreviousGross) / PreviousGross * 100), 1);
        /// <summary>Stoßzeit: Stunde mit dem meisten Umsatz (−1 = keine Verkäufe).</summary>
        public int PeakHour => Gross <= 0 ? -1 : Array.IndexOf(ByHour, ByHour.Max());
    }

    /// <summary>Auswertung von <paramref name="from"/> bis <paramref name="to"/> (je einschließlich), Stornos zählen nicht mit. Wochentag 0 = Montag.</summary>
    public static Report Of(IEnumerable<CashSale> all, DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        var list = all.ToList();
        bool In(CashSale s, DateOnly a, DateOnly b) { var d = DateOnly.FromDateTime(s.At); return d >= a && d <= b; }
        var ok = list.Where(s => !s.Cancelled && In(s, from, to)).ToList();
        var days = to.DayNumber - from.DayNumber + 1;
        var prev = list.Where(s => !s.Cancelled && In(s, from.AddDays(-days), from.AddDays(-1))).Sum(s => s.Gross);
        var wd = new decimal[7]; var hr = new decimal[24];
        foreach (var s in ok) { wd[((int)s.At.DayOfWeek + 6) % 7] += s.Gross; hr[s.At.Hour] += s.Gross; }
        var top = ok.SelectMany(s => s.Lines).Where(l => !l.IsDiscount).GroupBy(l => l.Product).Select(g => (g.Key, g.Sum(l => l.Qty), g.Sum(l => l.Sum)))
            .OrderByDescending(x => x.Item3).ThenBy(x => x.Key, StringComparer.CurrentCulture).Take(10).ToList();
        var gross = ok.Sum(s => s.Gross);
        return new(from, to, ok.Count, gross, ok.Count == 0 ? 0 : Math.Round(gross / ok.Count, 2), -ok.SelectMany(s => s.Lines).Where(l => l.IsDiscount).Sum(l => l.Sum),
            list.Count(s => s.Cancelled && In(s, from, to)), top, wd, hr, prev);
    }

    public static readonly string[] Weekdays = [.. Enumerable.Range(0, 7).Select(i => CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.AbbreviatedDayNames[(i + 1) % 7])];
}
