using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core;

/// <summary>Eine Bon-Zeile: Artikel, Menge, Preis brutto (zum Zeitpunkt des Verkaufs).</summary>
/// <remarks>T-0753 M-C: <see cref="ItemId"/> und <see cref="Variant"/> binden die Zeile fest an den Artikel (leer bei Altdaten); der Bon bleibt sonst wie ausgegeben.</remarks>
public sealed record CashLine(string Product, decimal Qty, decimal PriceGross, string ItemId = "", string Variant = "")
{
    /// <summary>Rabattzeile („Rabatt …“, negativer Betrag) – kein Artikel, kein Bestand.</summary>
    public bool IsDiscount => Product.StartsWith("Rabatt", StringComparison.Ordinal) && PriceGross < 0;
    public decimal Sum => Math.Round(Qty * PriceGross, 2);
}

/// <summary>Ein Kassenverkauf (Bon).</summary>
public sealed record CashSale(string Id, string No, DateTime At, List<CashLine> Lines, string Payment, decimal Given, decimal VatRate, bool Cancelled = false)
{
    public decimal Gross => Lines.Sum(l => l.Sum);
    public decimal Vat => VatRate <= 0 ? 0 : Math.Round(Gross - Gross / (1 + VatRate / 100m), 2);
    public decimal Change => Payment == "bar" ? Math.Max(0, Given - Gross) : 0;
}

/// <summary>Tagesabschluss: Bons, Umsatz brutto, MwSt, bar/Karte, Stornos und Verkauf je Artikel.</summary>
public sealed record CashDay(DateOnly Date, int Count, decimal Gross, decimal Vat, decimal Cash, decimal Card, int Cancelled, List<(string Product, decimal Qty, decimal Gross)> ByProduct);

/// <summary>Kasse für den Direktverkauf (Masterprompt 131): dieselben Artikel und derselbe Bestand wie „Online verkaufen“, Einnahmen landen in „Geld“.
/// Bestand sinkt sofort (Lagerbewegung „Kasse“), Storno bucht alles zurück. Bon und Tagesabschluss als PDF. Kein Ersatz für eine zertifizierte Registrierkasse (TSE).</summary>
public sealed class CashRegister(string dataRoot, string ideaId)
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public Shop Shop { get; } = new(dataRoot, ideaId);
    public IdeaList<CashSale> Sales { get; } = new(dataRoot, ideaId, "kasse");
    /// <summary>T-0753 M-B: Bon-Zeilen des letzten Stornos, deren Artikel es nicht mehr gibt – Geld zurückgebucht, Bestand nicht.</summary>
    public List<string> NotRestocked { get; } = [];

    public static decimal GrossPrice(Product p, bool smallBusiness) => smallBusiness ? (decimal)p.Price : Math.Round((decimal)p.Price * (1 + LegalRates.VatStandard / 100m), 2);
    public static decimal Gross(double net, bool smallBusiness) => smallBusiness ? (decimal)net : Math.Round((decimal)net * (1 + LegalRates.VatStandard / 100m), 2);

    /// <summary>Kassieren: prüft Bestand und (bei bar) den gegebenen Betrag, senkt den Bestand, bucht die Einnahme und legt den Bon an.</summary>
    /// <summary>Rabatt wie bei SumUp/Square: Prozent auf den ganzen Bon oder fester Betrag (nicht beides, nie mehr als die Summe).</summary>
    public static CashLine? DiscountLine(decimal gross, decimal percent, decimal euro)
    {
        if (percent < 0 || percent > 100) throw new ArgumentException("Rabatt bitte zwischen 0 und 100 %.");
        if (euro < 0) throw new ArgumentException("Rabatt bitte als positiven Betrag.");
        if (percent > 0 && euro > 0) throw new ArgumentException("Bitte entweder Prozent oder Euro als Rabatt.");
        var d = percent > 0 ? Math.Round(gross * percent / 100m, 2) : Math.Min(euro, gross);
        return d <= 0 ? null : new CashLine(percent > 0 ? $"Rabatt {percent:0.##} %" : "Rabatt", 1, -d);
    }

    public CashSale Checkout(IReadOnlyList<(string Product, decimal Qty)> cart, string payment, decimal given, DateTime now, Ledger ledger, bool smallBusiness, decimal discountPercent = 0, decimal discountEuro = 0)
    {
        if (cart.Count == 0 || cart.Any(c => c.Qty <= 0)) throw new ArgumentException("Der Warenkorb ist leer.");
        if (payment is not ("bar" or "karte")) throw new ArgumentException("Bitte bar oder Karte wählen.");
        var prods = Shop.Products.All(); var lines = new List<CashLine>();
        foreach (var g in cart.GroupBy(c => c.Product))
        {
            // T-0753 M1: Varianten („Shirt – M“) mit eigenem Preis und Bestand; ein Artikel mit Varianten wird nur über eine Variante verkauft
            var (pi, vi) = Shop.FindItem(prods, g.Key);
            if (pi < 0) throw new ArgumentException($"Den Artikel „{g.Key}“ gibt es nicht.");
            if (vi < 0 && prods[pi].Variants is { Count: > 0 }) throw new ArgumentException($"Bitte bei „{g.Key}“ die Variante wählen.");
            var qty = g.Sum(x => x.Qty); var free = Shop.Available(g.Key);
            if (qty > free) throw new InvalidOperationException(free <= 0 ? $"„{g.Key}“ ist nicht mehr vorrätig." : $"Von „{g.Key}“ sind nur noch {free:0.##} vorrätig.");
            Shop.EnsureIds(prods);
            lines.Add(new(g.Key, qty, Gross(vi >= 0 ? prods[pi].Variants![vi].Price : prods[pi].Price, smallBusiness), prods[pi].Id, vi >= 0 ? prods[pi].Variants![vi].Name : ""));
        }
        if (DiscountLine(lines.Sum(l => l.Sum), discountPercent, discountEuro) is { } disc) lines.Add(disc);
        var gross = lines.Sum(l => l.Sum);
        if (payment == "bar" && given < gross) throw new ArgumentException($"Es fehlen noch {(gross - given).ToString("N2", De)} €.");
        var today = DateOnly.FromDateTime(now); var n = Sales.All().Count(s => DateOnly.FromDateTime(s.At) == today) + 1;
        var sale = new CashSale(Guid.NewGuid().ToString("N")[..8], $"BON-{now:yyyyMMdd}-{n:0000}", now, lines, payment, payment == "bar" ? given : gross, smallBusiness ? 0 : LegalRates.VatStandard);
        foreach (var l in lines.Where(x => !x.IsDiscount)) { var (pi, vi) = Shop.Resolve(prods, l.Product, l.ItemId, l.Variant); Shop.AddStockAt(prods, pi, vi, -(int)Math.Ceiling(l.Qty)); } // T-0753 M1/M-C
        Shop.Products.Save(prods);
        foreach (var l in lines.Where(x => !x.IsDiscount)) Shop.Moves.Add(new(Guid.NewGuid().ToString("N")[..8], today, l.Product, -l.Qty, sale.Id, "Kasse"));
        ledger.Add(new(today, $"Kasse {sale.No} ({(payment == "bar" ? "bar" : "Karte")})", gross, "Einnahmen", sale.VatRate));
        Sales.Add(sale);
        return sale;
    }

    /// <summary>Bon stornieren: Bestand zurück, Gegenbuchung in „Geld“. Zweimal geht nicht.</summary>
    public CashSale Cancel(string id, DateTime now, Ledger ledger)
    {
        var all = Sales.All(); var i = all.FindIndex(s => s.Id == id);
        if (i < 0) throw new InvalidOperationException("Diesen Bon gibt es nicht.");
        var s = all[i]; if (s.Cancelled) throw new InvalidOperationException("Dieser Bon ist schon storniert.");
        var prods = Shop.Products.All(); NotRestocked.Clear();
        foreach (var l in s.Lines.Where(x => !x.IsDiscount))
        {
            // T-0753 M-B/M-C: der Bon bleibt wie ausgegeben; der Artikel wird über die feste Kennung gefunden (auch umbenannt),
            // gelöschte werden ohne Bestandsänderung storniert (sichtbar über NotRestocked) – ein neuer gleichnamiger Artikel bekommt nichts
            var (pi, vi) = Shop.Resolve(prods, l.Product, l.ItemId, l.Variant, s.At);
            if (pi < 0) { NotRestocked.Add(l.Product); continue; }
            Shop.AddStockAt(prods, pi, vi, (int)Math.Ceiling(l.Qty));
            Shop.Moves.Add(new(Guid.NewGuid().ToString("N")[..8], DateOnly.FromDateTime(now), vi >= 0 ? Shop.Label(prods[pi], prods[pi].Variants![vi]) : prods[pi].Name, l.Qty, s.Id, "Kassen-Storno"));
        }
        Shop.Products.Save(prods);
        ledger.Add(new(DateOnly.FromDateTime(now), $"Storno Kasse {s.No}", -s.Gross, "Einnahmen", s.VatRate));
        all[i] = s with { Cancelled = true }; Sales.Save(all);
        return all[i];
    }

    public List<CashSale> Of(DateOnly day) => Sales.All().Where(s => DateOnly.FromDateTime(s.At) == day).OrderByDescending(s => s.At).ToList();

    public CashDay Day(DateOnly day)
    {
        var all = Of(day); var ok = all.Where(s => !s.Cancelled).ToList();
        var by = ok.SelectMany(s => s.Lines).Where(l => !l.IsDiscount).GroupBy(l => l.Product).Select(g => (g.Key, g.Sum(l => l.Qty), g.Sum(l => l.Sum))).OrderByDescending(x => x.Item3).ToList();
        return new(day, ok.Count, ok.Sum(s => s.Gross), ok.Sum(s => s.Vat), ok.Where(s => s.Payment == "bar").Sum(s => s.Gross), ok.Where(s => s.Payment == "karte").Sum(s => s.Gross), all.Count(s => s.Cancelled), by);
    }

    /// <summary>Bon als PDF (80 mm breit, wie ein Kassenbon).</summary>
    public static void ReceiptPdf(string path, CashSale s, string seller)
    {
        Sys.PdfTools.Fonts.Ensure();
        var rows = s.Lines.Count; var hMm = 95 + rows * 9 + (s.Vat > 0 ? 6 : 10);
        using var doc = new PdfDocument(); var page = doc.AddPage(); page.Width = XUnit.FromMillimeter(80); page.Height = XUnit.FromMillimeter(hMm);
        using var g = XGraphics.FromPdfPage(page);
        var bold = new XFont("Arial", 10, XFontStyleEx.Bold); var f = new XFont("Arial", 8); var big = new XFont("Arial", 12, XFontStyleEx.Bold);
        double w = page.Width.Point, y = 24, l = 10, r = w - 10;
        void Right(string t, XFont font, double yy) { var sz = g.MeasureString(t, font); g.DrawString(t, font, XBrushes.Black, new XPoint(r - sz.Width, yy)); }
        void Center(string t, XFont font) { var sz = g.MeasureString(t, font); g.DrawString(t, font, XBrushes.Black, new XPoint((w - sz.Width) / 2, y)); y += font.Size + 4; }
        foreach (var line in (seller.Trim().Length > 0 ? seller : "Mein Laden").Split('\n')) Center(line.Trim(), y == 24 ? bold : f);
        y += 4; g.DrawLine(new XPen(XColors.Black, 0.5) { DashStyle = XDashStyle.Dash }, l, y, r, y); y += 12;
        g.DrawString($"{s.No}", f, XBrushes.Black, new XPoint(l, y)); Right(s.At.ToString("dd.MM.yyyy HH:mm", De), f, y); y += 14;
        foreach (var x in s.Lines)
        {
            g.DrawString(x.Product.Length > 28 ? x.Product[..28] + "…" : x.Product, f, XBrushes.Black, new XPoint(l, y)); Right(x.Sum.ToString("N2", De), f, y); y += 10;
            g.DrawString($"  {x.Qty:0.##} × {x.PriceGross.ToString("N2", De)} €", f, XBrushes.Gray, new XPoint(l, y)); y += 12;
        }
        g.DrawLine(new XPen(XColors.Black, 0.5), l, y, r, y); y += 14;
        g.DrawString("SUMME", big, XBrushes.Black, new XPoint(l, y)); Right(s.Gross.ToString("N2", De) + " €", big, y); y += 16;
        if (s.Payment == "bar") { g.DrawString("Bar gegeben", f, XBrushes.Black, new XPoint(l, y)); Right(s.Given.ToString("N2", De) + " €", f, y); y += 11; g.DrawString("Rückgeld", f, XBrushes.Black, new XPoint(l, y)); Right(s.Change.ToString("N2", De) + " €", f, y); y += 11; }
        else { g.DrawString("Bezahlt mit Karte", f, XBrushes.Black, new XPoint(l, y)); y += 11; }
        y += 4;
        if (s.Vat > 0) { g.DrawString($"enthaltene MwSt {s.VatRate:0} %", f, XBrushes.Gray, new XPoint(l, y)); Right(s.Vat.ToString("N2", De) + " €", f, y); y += 12; }
        else { g.DrawString("Gemäß § 19 UStG wird keine Umsatzsteuer", f, XBrushes.Gray, new XPoint(l, y)); y += 10; g.DrawString("berechnet.", f, XBrushes.Gray, new XPoint(l, y)); y += 12; }
        if (s.Cancelled) { y += 4; Center("STORNIERT", bold); }
        y += 6; Center("Vielen Dank für deinen Einkauf!", f);
        doc.Save(path);
    }

    /// <summary>Tagesabschluss als PDF (A4).</summary>
    public static void DayPdf(string path, CashDay d, IReadOnlyList<CashSale> sales, string seller)
    {
        Sys.PdfTools.Fonts.Ensure();
        using var doc = new PdfDocument(); var page = doc.AddPage(); page.Width = XUnit.FromMillimeter(210); page.Height = XUnit.FromMillimeter(297);
        using var g = XGraphics.FromPdfPage(page);
        var h = new XFont("Arial", 15, XFontStyleEx.Bold); var b = new XFont("Arial", 10, XFontStyleEx.Bold); var f = new XFont("Arial", 9);
        double y = 56, l = 50, r = page.Width.Point - 50;
        void Row(string a, string v, XFont font) { g.DrawString(a, font, XBrushes.Black, new XPoint(l, y)); var sz = g.MeasureString(v, font); g.DrawString(v, font, XBrushes.Black, new XPoint(r - sz.Width, y)); y += font.Size + 6; }
        g.DrawString($"Tagesabschluss {d.Date:dddd, dd.MM.yyyy}", h, XBrushes.Black, new XPoint(l, y)); y += 18;
        if (seller.Trim().Length > 0) { g.DrawString(seller.Split('\n')[0], f, XBrushes.Gray, new XPoint(l, y)); y += 16; }
        y += 6;
        Row("Bons", d.Count.ToString(De), b); Row("Umsatz brutto", d.Gross.ToString("N2", De) + " €", b); Row("davon MwSt", d.Vat.ToString("N2", De) + " €", f);
        Row("bar", d.Cash.ToString("N2", De) + " €", f); Row("Karte", d.Card.ToString("N2", De) + " €", f); Row("Stornos", d.Cancelled.ToString(De), f);
        y += 10; g.DrawString("Verkauf je Artikel", b, XBrushes.Black, new XPoint(l, y)); y += 16;
        foreach (var (p, q, s) in d.ByProduct) Row($"{p} · {q:0.##}×", s.ToString("N2", De) + " €", f);
        y += 10; g.DrawString("Bons", b, XBrushes.Black, new XPoint(l, y)); y += 16;
        foreach (var s in sales.OrderBy(x => x.At)) { if (y > page.Height.Point - 60) break; Row($"{s.At:HH:mm} {s.No} · {(s.Payment == "bar" ? "bar" : "Karte")}{(s.Cancelled ? " · STORNIERT" : "")}", s.Gross.ToString("N2", De) + " €", f); }
        g.DrawString($"Erstellt am {DateTime.Now:dd.MM.yyyy HH:mm} – kein Ersatz für eine zertifizierte Registrierkasse (TSE).", f, XBrushes.Gray, new XPoint(l, page.Height.Point - 36));
        doc.Save(path);
    }
}
