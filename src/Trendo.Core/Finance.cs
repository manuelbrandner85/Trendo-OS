using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>SPEC-11: Geld & Business – Einnahmen/Ausgaben, Monats- und Jahresübersicht (EÜR-artig), Mehrwertsteuer, CSV-Import/-Export, Rechnungen.</summary>
public sealed record Entry(DateOnly Date, string Text, decimal Amount, string Category = "Sonstiges", decimal VatRate = 0m)
{
    public decimal Net => VatRate == 0 ? Amount : Math.Round(Amount / (1 + VatRate / 100m), 2);
    public decimal Vat => Amount - Net;
}
public sealed record MonthSum(int Year, int Month, decimal Income, decimal Expenses) { public decimal Result => Income + Expenses; }
public sealed record InvoiceLine(string Text, decimal Qty, decimal UnitNet);
public sealed record Invoice(string Number, DateOnly Date, string Seller, string Buyer, IReadOnlyList<InvoiceLine> Lines, decimal VatRate, bool SmallBusiness, string TaxId = "");

public static class Finance
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static readonly string[] Categories = ["Einnahmen", "Material", "Software", "Werbung", "Büro", "Fahrt", "Gebühren", "Sonstiges"];

    public static decimal ParseAmount(string s)
    {
        s = s.Trim().Replace("€", "").Replace(" ", "").Replace("\u00A0", "");
        if (s.Contains(',') && s.Contains('.')) s = s.IndexOf(',') > s.IndexOf('.') ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        else if (s.Contains(',')) s = s.Replace(',', '.');
        return decimal.Parse(s, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    static string[] SplitCsv(string line, char sep)
    {
        var res = new List<string>(); var sb = new StringBuilder(); var q = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = !q; }
            else if (c == sep && !q) { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        res.Add(sb.ToString()); return res.ToArray();
    }

    /// <summary>Liest Bank-CSV (Trennzeichen ; oder ,). Spalten werden über Kopfzeile erkannt: Datum/Buchungstag, Betrag, Verwendungszweck/Text/Beschreibung.</summary>
    public static IReadOnlyList<Entry> ImportCsv(string csv)
    {
        var lines = csv.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var hi = Array.FindIndex(lines, l => l.Contains("Betrag", StringComparison.OrdinalIgnoreCase) || l.Contains("Amount", StringComparison.OrdinalIgnoreCase));
        if (hi < 0) throw new InvalidDataException("Keine Spalte „Betrag“ gefunden.");
        var sep = lines[hi].Count(c => c == ';') >= lines[hi].Count(c => c == ',') ? ';' : ',';
        var h = SplitCsv(lines[hi], sep).Select(x => x.Trim().ToLowerInvariant()).ToArray();
        int Col(params string[] names) { foreach (var n in names) { var k = Array.FindIndex(h, x => x.Contains(n)); if (k >= 0) return k; } return -1; }
        int cd = Col("buchungstag", "datum", "date"), ca = Col("betrag", "amount"), ct = Col("verwendungszweck", "beschreibung", "text", "empfänger", "name", "description");
        var res = new List<Entry>();
        foreach (var l in lines.Skip(hi + 1))
        {
            var f = SplitCsv(l, sep); if (f.Length <= Math.Max(cd, ca)) continue;
            if (!DateOnly.TryParse(f[cd].Trim(), De, out var d) && !DateOnly.TryParse(f[cd].Trim(), CultureInfo.InvariantCulture, out d)) continue;
            decimal a; try { a = ParseAmount(f[ca]); } catch (FormatException) { continue; }
            res.Add(new(d, ct >= 0 && ct < f.Length ? f[ct].Trim() : "", a, a > 0 ? "Einnahmen" : Guess(ct >= 0 && ct < f.Length ? f[ct] : "")));
        }
        return res;
    }

    public static string Guess(string text)
    {
        var t = text.ToLowerInvariant();
        if (new[] { "adobe", "microsoft", "github", "software", "lizenz", "hosting", "domain" }.Any(t.Contains)) return "Software";
        if (new[] { "google ads", "meta", "werbung", "facebook" }.Any(t.Contains)) return "Werbung";
        if (new[] { "tank", "db ", "bahn", "aral", "shell" }.Any(t.Contains)) return "Fahrt";
        if (new[] { "gebühr", "entgelt", "paypal fee", "kontoführung" }.Any(t.Contains)) return "Gebühren";
        if (new[] { "büro", "papier", "drucker" }.Any(t.Contains)) return "Büro";
        return "Sonstiges";
    }

    public static string ExportCsv(IEnumerable<Entry> e)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Datum;Text;Betrag;Kategorie;MwSt-Satz;Netto;MwSt\n");
        foreach (var x in e.OrderBy(x => x.Date))
            sb.Append($"{x.Date.ToString("dd.MM.yyyy", De)};{Q(x.Text)};{x.Amount.ToString("0.00", De)};{Q(x.Category)};{x.VatRate.ToString("0.##", De)};{x.Net.ToString("0.00", De)};{x.Vat.ToString("0.00", De)}\n");
        return sb.ToString();
    }

    public static IReadOnlyList<MonthSum> Months(IEnumerable<Entry> e) =>
        e.GroupBy(x => (x.Date.Year, x.Date.Month)).OrderBy(g => g.Key)
         .Select(g => new MonthSum(g.Key.Year, g.Key.Month, g.Where(x => x.Amount > 0).Sum(x => x.Amount), g.Where(x => x.Amount < 0).Sum(x => x.Amount))).ToList();

    /// <summary>Jahresübersicht: Einnahmen/Ausgaben netto je Kategorie, eingenommene und gezahlte Mehrwertsteuer.</summary>
    public static (decimal IncomeNet, decimal ExpenseNet, decimal VatIn, decimal VatOut, IReadOnlyDictionary<string, decimal> ByCategory) Year(IEnumerable<Entry> e, int year)
    {
        var y = e.Where(x => x.Date.Year == year).ToList();
        return (y.Where(x => x.Amount > 0).Sum(x => x.Net), y.Where(x => x.Amount < 0).Sum(x => x.Net),
            y.Where(x => x.Amount > 0).Sum(x => x.Vat), y.Where(x => x.Amount < 0).Sum(x => x.Vat),
            y.GroupBy(x => x.Category).ToDictionary(g => g.Key, g => g.Sum(x => x.Net)));
    }

    public static string NextInvoiceNumber(IEnumerable<string> existing, DateOnly date)
    {
        var prefix = $"RE-{date.Year}-";
        var max = existing.Where(n => n.StartsWith(prefix)).Select(n => int.TryParse(n[prefix.Length..], out var k) ? k : 0).DefaultIfEmpty(0).Max();
        return prefix + (max + 1).ToString("000");
    }

    public static (decimal Net, decimal Vat, decimal Gross) Totals(Invoice i)
    {
        var net = i.Lines.Sum(l => Math.Round(l.Qty * l.UnitNet, 2));
        var vat = i.SmallBusiness ? 0 : Math.Round(net * i.VatRate / 100m, 2);
        return (net, vat, net + vat);
    }

    public static string InvoiceHtml(Invoice i, string heading = "Rechnung")
    {
        if (heading is not ("Rechnung" or "Angebot")) heading = "Rechnung";
        string E(string s) => WebUtility.HtmlEncode(s).Replace("\n", "<br>");
        string M(decimal d) => d.ToString("N2", De) + " €";
        var (net, vat, gross) = Totals(i);
        var sb = new StringBuilder($"<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\"><title>{heading} {E(i.Number)}</title><style>body{{font-family:system-ui,sans-serif;max-width:720px;margin:2rem auto;color:#111}}table{{width:100%;border-collapse:collapse}}td,th{{padding:6px;border-bottom:1px solid #ddd;text-align:left}}.r{{text-align:right}}</style></head><body>");
        sb.Append($"<p>{E(i.Seller)}</p><p><b>An:</b><br>{E(i.Buyer)}</p><h2>{heading} {E(i.Number)}</h2><p>Datum: {i.Date.ToString("dd.MM.yyyy", De)} · Leistungsdatum: {i.Date.ToString("dd.MM.yyyy", De)}</p>");
        sb.Append("<table><tr><th>Leistung</th><th class=r>Menge</th><th class=r>Einzelpreis</th><th class=r>Summe</th></tr>");
        foreach (var l in i.Lines) sb.Append($"<tr><td>{E(l.Text)}</td><td class=r>{l.Qty.ToString("0.##", De)}</td><td class=r>{M(l.UnitNet)}</td><td class=r>{M(Math.Round(l.Qty * l.UnitNet, 2))}</td></tr>");
        sb.Append($"<tr><td colspan=3 class=r>Netto</td><td class=r>{M(net)}</td></tr>");
        if (!i.SmallBusiness) sb.Append($"<tr><td colspan=3 class=r>MwSt {i.VatRate.ToString("0.##", De)} %</td><td class=r>{M(vat)}</td></tr>");
        sb.Append($"<tr><td colspan=3 class=r><b>Gesamt</b></td><td class=r><b>{M(gross)}</b></td></tr></table>");
        if (i.SmallBusiness) sb.Append("<p>Gemäß § 19 UStG wird keine Umsatzsteuer berechnet.</p>");
        if (i.TaxId.Length > 0) sb.Append($"<p>Steuernummer / USt-IdNr.: {E(i.TaxId)}</p>");
        return sb.Append("</body></html>").ToString();
    }
}

public sealed class Ledger(string dataRoot)
{
    private readonly string _file = Path.Combine(dataRoot, "finanzen", "buchungen.json");
    public string InvoiceDir => Path.Combine(dataRoot, "finanzen", "rechnungen");
    public List<Entry> All() => File.Exists(_file) ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(_file)) ?? [] : [];
    public void Save(List<Entry> all) { Directory.CreateDirectory(Path.GetDirectoryName(_file)!); File.WriteAllText(_file, JsonSerializer.Serialize(all.OrderByDescending(e => e.Date).ToList())); }
    public void Add(Entry e) { var a = All(); a.Add(e); Save(a); }
    /// <summary>Fügt importierte Buchungen hinzu, doppelte (gleiches Datum, Betrag, Text) werden übersprungen.</summary>
    public int Import(IEnumerable<Entry> e)
    {
        var a = All(); var keys = a.Select(x => (x.Date, x.Amount, x.Text)).ToHashSet(); var n = 0;
        foreach (var x in e) if (keys.Add((x.Date, x.Amount, x.Text))) { a.Add(x); n++; }
        Save(a); return n;
    }
    public void RemoveAt(int i) { var a = All(); if (i >= 0 && i < a.Count) { a.RemoveAt(i); Save(a); } }

    public const string ReversalPrefix = "Storno: ";
    /// <summary>T-0253 (105): Buchung stornieren statt löschen – eine Gegenbuchung mit umgekehrtem Betrag, die ursprüngliche bleibt nachvollziehbar.
    /// Eine schon stornierte Buchung und eine Storno-Buchung selbst lassen sich nicht noch einmal stornieren.</summary>
    public Entry Reverse(int i, DateOnly date)
    {
        var a = All();
        if (i < 0 || i >= a.Count) throw new ArgumentException("Diese Buchung gibt es nicht.");
        var e = a[i];
        if (e.Text.StartsWith(ReversalPrefix, StringComparison.Ordinal)) throw new InvalidOperationException("Das ist schon eine Storno-Buchung.");
        if (IsReversed(a, e)) throw new InvalidOperationException("Diese Buchung ist schon storniert.");
        var r = new Entry(date, ReversalPrefix + e.Text + $" (vom {e.Date:dd.MM.yyyy})", -e.Amount, e.Category, e.VatRate);
        a.Add(r); Save(a); return r;
    }

    /// <summary>Gibt es zu dieser Buchung schon eine Gegenbuchung?</summary>
    public static bool IsReversed(IEnumerable<Entry> all, Entry e) =>
        all.Any(x => x.Text == ReversalPrefix + e.Text + $" (vom {e.Date:dd.MM.yyyy})" && x.Amount == -e.Amount);
    /// <summary>Kategorie einer Buchung ändern (Index wie in <see cref="All"/>).</summary>
    public void SetCategory(int i, string category) { var a = All(); if (i >= 0 && i < a.Count) { a[i] = a[i] with { Category = category }; Save(a); } }
    public IEnumerable<string> InvoiceNumbers() => Directory.Exists(InvoiceDir) ? Directory.GetFiles(InvoiceDir, "RE-*.html").Select(f => Path.GetFileNameWithoutExtension(f)) : [];
    public string WriteInvoice(Invoice i) { Directory.CreateDirectory(InvoiceDir); var p = Path.Combine(InvoiceDir, i.Number + ".html"); if (File.Exists(p)) throw new IOException("Diese Rechnungsnummer gibt es schon."); File.WriteAllText(p, Finance.InvoiceHtml(i)); return p; }
}
