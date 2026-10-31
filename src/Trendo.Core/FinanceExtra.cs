using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

// Teil 4, Business & Geld: Haushaltsbuch (SPEC-11 F1), lernende Kategorien beim Import (F2), Budgets (F4), Export für den Steuerberater (F5),
// Kunden, Artikel, Bestellungen, Rechnung aus Bestellung und Webshop-Vorschau (STUDIO-ONLINE-BUSINESS-01 F1–F3).

public static class FinanceExtra
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static readonly string[] Household = ["Wohnen", "Lebensmittel", "Mobilität", "Versicherung", "Gesundheit", "Freizeit", "Kleidung", "Sparen"];
    public static string[] AllCategories => [.. Finance.Categories.Where(c => c != "Sonstiges"), .. Household, "Sonstiges"];

    static readonly (string Cat, string[] Words)[] HouseholdWords =
    [
        ("Lebensmittel", ["rewe", "edeka", "aldi", "lidl", "netto", "penny", "kaufland", "bäckerei", "baecker", "supermarkt"]),
        ("Wohnen", ["miete", "nebenkosten", "stadtwerke", "strom", "gasversorg", "hausverwaltung", "rundfunk"]),
        ("Versicherung", ["versicherung", "allianz", "huk", "ergo", "axa", "krankenkasse"]),
        ("Gesundheit", ["apotheke", "arzt", "zahnarzt", "optiker"]),
        ("Freizeit", ["netflix", "spotify", "kino", "fitness", "verein", "disney"]),
        ("Kleidung", ["zalando", "h&m", "c&a", "schuh"]),
        ("Mobilität", ["deutschlandticket", "bvg", "mvv", "hvv", "parkhaus", "carsharing"]),
        ("Sparen", ["sparplan", "tagesgeld", "depot"]),
    ];

    /// <summary>Kategorie für einen Buchungstext: zuerst, was der Nutzer selbst zugeordnet hat, dann feste Stichworte.</summary>
    public static string Guess(string text, CategoryMemory? memory = null)
    {
        if (memory?.Lookup(text) is { } learned) return learned;
        var g = Finance.Guess(text); if (g != "Sonstiges") return g;
        var t = text.ToLowerInvariant();
        foreach (var (cat, words) in HouseholdWords) if (words.Any(t.Contains)) return cat;
        return "Sonstiges";
    }

    /// <summary>F2: Kontoauszug lesen und jede Ausgabe einordnen (gelerntes zuerst).</summary>
    public static List<Entry> Categorize(IEnumerable<Entry> entries, CategoryMemory? memory) =>
        entries.Select(e => e.Amount > 0 ? e : e with { Category = Guess(e.Text, memory) }).ToList();

    /// <summary>Saldo, Monatswerte eines Jahres (12 Werte) und Ausgaben je Kategorie eines Monats.</summary>
    public static decimal Balance(IEnumerable<Entry> e) => e.Sum(x => x.Amount);
    public static (decimal Income, decimal Expense)[] YearMonths(IEnumerable<Entry> e, int year)
    {
        var r = new (decimal, decimal)[12];
        foreach (var x in e.Where(x => x.Date.Year == year))
        {
            var m = x.Date.Month - 1;
            r[m] = x.Amount > 0 ? (r[m].Item1 + x.Amount, r[m].Item2) : (r[m].Item1, r[m].Item2 - x.Amount);
        }
        return r;
    }
    public static Dictionary<string, decimal> SpentByCategory(IEnumerable<Entry> e, int year, int month) =>
        e.Where(x => x.Amount < 0 && x.Date.Year == year && x.Date.Month == month).GroupBy(x => x.Category).ToDictionary(g => g.Key, g => -g.Sum(x => x.Amount));

    /// <summary>F5: ZIP für den Steuerberater – Buchungen, Einnahmen-Überschuss je Kategorie und die Rechnungen des Jahres.</summary>
    public static string TaxExport(IEnumerable<Entry> all, string invoiceDir, int year, string outDir)
    {
        var list = all.Where(x => x.Date.Year == year).ToList();
        Directory.CreateDirectory(outDir);
        var zipPath = Path.Combine(outDir, $"Steuerberater-{year}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var z = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        void Text(string name, string content) { using var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(true)); w.Write(content); }
        Text($"Buchungen-{year}.csv", Finance.ExportCsv(list));
        var y = Finance.Year(list, year);
        var eur = new StringBuilder("Kategorie;Netto;\n");
        foreach (var (k, v) in y.ByCategory.OrderBy(k => k.Key)) eur.Append($"\"{k}\";{v.ToString("0.00", De)}\n");
        eur.Append($"\"Einnahmen netto\";{y.IncomeNet.ToString("0.00", De)}\n\"Ausgaben netto\";{y.ExpenseNet.ToString("0.00", De)}\n\"Ergebnis\";{(y.IncomeNet + y.ExpenseNet).ToString("0.00", De)}\n");
        eur.Append($"\"Umsatzsteuer eingenommen\";{y.VatIn.ToString("0.00", De)}\n\"Vorsteuer gezahlt\";{(-y.VatOut).ToString("0.00", De)}\n");
        Text($"Uebersicht-{year}.csv", eur.ToString());
        if (Directory.Exists(invoiceDir))
            foreach (var f in Directory.GetFiles(invoiceDir, $"RE-{year}-*.html")) z.CreateEntryFromFile(f, "Rechnungen/" + Path.GetFileName(f));
        Text("LIESMICH.txt", $"Buchungen und Rechnungen {year}, zusammengestellt am {DateTime.Now:dd.MM.yyyy}.\r\nBuchungen-{year}.csv: alle Buchungen mit Netto und MwSt.\r\nUebersicht-{year}.csv: Summen je Kategorie.\r\nRechnungen: alle geschriebenen Rechnungen als HTML.\r\nDiese Zusammenstellung ersetzt keine Steuerberatung.\r\n");
        return zipPath;
    }

    /// <summary>F3: Logo des Nutzers in die Rechnung (als eingebettetes Bild).</summary>
    public static string WithLogo(string html, string? logoFile)
    {
        if (string.IsNullOrWhiteSpace(logoFile) || !File.Exists(logoFile)) return html;
        var ext = Path.GetExtension(logoFile).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".svg" => "image/svg+xml", ".webp" => "image/webp", _ => "" };
        if (mime.Length == 0) return html;
        var img = $"<p style=\"text-align:right\"><img alt=\"Logo\" style=\"max-height:72px;max-width:240px\" src=\"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(logoFile))}\"></p>";
        var i = html.IndexOf("<body>", StringComparison.Ordinal);
        return i < 0 ? html : html.Insert(i + 6, img);
    }
}

/// <summary>F2: Was der Nutzer selbst zuordnet, merkt sich Trendo (Empfänger → Kategorie).</summary>
public sealed class CategoryMemory(string dataRoot)
{
    string F => Path.Combine(dataRoot, "finanzen", "kategorien-gelernt.json");
    Dictionary<string, string> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(F)) ?? [] : []; } catch (JsonException) { return []; } }
    public static string Key(string text)
    {
        var words = text.ToLowerInvariant().Split([' ', ',', ';', '/', '-', '.', ':'], StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2 && !w.All(char.IsDigit)).Take(2);
        return string.Join(' ', words);
    }
    public void Learn(string text, string category)
    {
        var k = Key(text); if (k.Length == 0) return;
        var d = Load(); d[k] = category; Directory.CreateDirectory(Path.GetDirectoryName(F)!); File.WriteAllText(F, JsonSerializer.Serialize(d));
    }
    /// <summary>Gleicher Anfang reicht: „Wochenmarkt“ gelernt passt auch zu „Wochenmarkt Samstag“; der längste gelernte Schlüssel gewinnt.</summary>
    public string? Lookup(string text)
    {
        var k = Key(text); if (k.Length == 0) return null;
        return Load().Where(p => k == p.Key || k.StartsWith(p.Key + " ", StringComparison.Ordinal)).OrderByDescending(p => p.Key.Length).Select(p => p.Value).FirstOrDefault();
    }
}

/// <summary>F4: Monatsbudget je Kategorie mit Warnung ab 90 %.</summary>
public sealed record BudgetState(string Category, decimal Spent, decimal Limit)
{
    public double Ratio => Limit <= 0 ? 0 : (double)(Spent / Limit);
    public string Level => Ratio >= 1 ? "über" : Ratio >= 0.9 ? "knapp" : "ok";
}

public sealed class BudgetStore(string dataRoot)
{
    string F => Path.Combine(dataRoot, "finanzen", "budgets.json");
    public Dictionary<string, decimal> All() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, decimal>>(File.ReadAllText(F)) ?? [] : []; } catch (JsonException) { return []; } }
    public void Set(string category, decimal limit)
    {
        var d = All(); if (limit <= 0) d.Remove(category); else d[category] = limit;
        Directory.CreateDirectory(Path.GetDirectoryName(F)!); File.WriteAllText(F, JsonSerializer.Serialize(d));
    }
    public List<BudgetState> Status(IEnumerable<Entry> entries, int year, int month)
    {
        var spent = FinanceExtra.SpentByCategory(entries, year, month);
        return All().Select(b => new BudgetState(b.Key, spent.GetValueOrDefault(b.Key), b.Value)).OrderByDescending(b => b.Ratio).ToList();
    }
}

// ---------- Online verkaufen: nutzt Kunden (BizContact) und Ware (Product) der Geschäftsidee aus „Betrieb“ ----------
public sealed record OrderLine(string Product, decimal Qty, decimal PriceNet);
/// <summary>Zahlung zu einer Bestellung; <see cref="Ref"/> (z. B. Kontoauszug-Zeile) macht doppelte Erfassung unmöglich.</summary>
public sealed record ShopPayment(string Ref, DateOnly Date, decimal Amount);
/// <summary>Bestellung. 105 (Entscheidung 31.10.2026): Bestand sinkt erst bei Lieferung, Umsatz zählt erst bei Zahlung.
/// <see cref="StockOut"/> null und <see cref="Payments"/> null kennzeichnen Altdaten (vor 0.9.22: Bestand schon bei Bestellung,
/// Einnahme schon bei Rechnung gebucht) – sie werden nicht noch einmal gebucht.</summary>
public sealed record ShopOrder(string Id, DateOnly Date, string Customer, List<OrderLine> Lines, string Status = "offen", string InvoiceNumber = "",
    bool? StockOut = null, DateOnly? Delivered = null, decimal InvoiceGross = 0, List<ShopPayment>? Payments = null)
{
    public decimal Net => Lines.Sum(l => Math.Round(l.Qty * l.PriceNet, 2));
    public decimal Paid => Payments?.Sum(p => p.Amount) ?? (InvoiceNumber.Length > 0 ? InvoiceGross : 0);
    /// <summary>Offener Betrag der Rechnung (0 ohne Rechnung oder bei Altdaten).</summary>
    public decimal Open => InvoiceNumber.Length == 0 || Payments is null ? 0 : Math.Max(0, InvoiceGross - Paid);
}
/// <summary>Lagerbewegung (Abgang bei Lieferung, Zugang bei Rücknahme) mit Bezug zur Bestellung.</summary>
public sealed record StockMove(string Id, DateOnly Date, string Product, decimal Qty, string OrderId, string Kind);

public sealed class Shop(string dataRoot, string ideaId)
{
    public IdeaList<BizContact> Customers { get; } = new(dataRoot, ideaId, "kontakte");
    public IdeaList<Product> Products { get; } = new(dataRoot, ideaId, "ware");
    public IdeaList<ShopOrder> OrderList { get; } = new(dataRoot, ideaId, "bestellungen");
    public IdeaList<StockMove> Moves { get; } = new(dataRoot, ideaId, "lagerbewegungen");

    /// <summary>Für Bestellungen vorgemerkt, aber noch nicht geliefert.</summary>
    public decimal Reserved(string product) => OrderList.All().Where(o => o.StockOut == false && o.Status != "storniert").SelectMany(o => o.Lines).Where(l => l.Product == product).Sum(l => l.Qty);
    /// <summary>Frei verfügbar = Bestand − vorgemerkt.</summary>
    public decimal Available(string product) => (Products.All().FirstOrDefault(p => p.Name == product)?.Stock ?? 0) - Reserved(product);
    public List<ShopOrder> Orders() => OrderList.All().OrderByDescending(o => o.Date).ToList();

    /// <summary>Bestellung anlegen; Preis wird festgehalten, die Menge wird nur vorgemerkt (Bestand sinkt erst bei Lieferung), Interessenten werden zu Kunden.</summary>
    public ShopOrder AddOrder(string customer, DateOnly date, params (string Product, decimal Qty)[] lines)
    {
        var contacts = Customers.All(); var ci = contacts.FindIndex(c => c.Name == customer);
        if (ci < 0) throw new ArgumentException("Bitte einen Kunden wählen.");
        if (lines.Length == 0 || lines.Any(l => l.Qty <= 0)) throw new ArgumentException("Bitte mindestens einen Artikel mit Menge eintragen.");
        var prods = Products.All(); var res = new List<OrderLine>();
        foreach (var (name, qty) in lines)
        {
            var i = prods.FindIndex(p => p.Name == name); if (i < 0) throw new ArgumentException($"Den Artikel „{name}“ gibt es nicht.");
            res.Add(new(name, qty, (decimal)prods[i].Price));
        }
        if (contacts[ci].Stage is "Interessent" or "Angebot") { contacts[ci] = contacts[ci] with { Stage = "Kunde" }; Customers.Save(contacts); }
        var o = new ShopOrder(Guid.NewGuid().ToString("N")[..8], date, customer, res, StockOut: false, Payments: []); OrderList.Add(o); return o;
    }

    ShopOrder Get(string id) => OrderList.All().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Diese Bestellung gibt es nicht.");

    /// <summary>Lieferung: jetzt sinkt der Bestand (nicht unter 0), je Artikel eine Lagerbewegung. Zweimal liefern geht nicht.</summary>
    public ShopOrder Deliver(string orderId, DateOnly date)
    {
        var o = Get(orderId);
        if (o.Status == "storniert") throw new InvalidOperationException("Diese Bestellung ist storniert.");
        if (o.StockOut != false) throw new InvalidOperationException("Diese Bestellung ist schon geliefert.");
        var prods = Products.All();
        foreach (var l in o.Lines)
        {
            var i = prods.FindIndex(p => p.Name == l.Product);
            if (i >= 0) prods[i] = prods[i] with { Stock = Math.Max(0, prods[i].Stock - (int)l.Qty) };
            Moves.Add(new(Guid.NewGuid().ToString("N")[..8], date, l.Product, -l.Qty, o.Id, "Lieferung"));
        }
        Products.Save(prods);
        var done = o with { StockOut = true, Delivered = date, Status = o.Status == "offen" ? "geliefert" : o.Status };
        Set(done); return done;
    }

    /// <summary>Stornieren: vor der Lieferung ohne Bestandsänderung; nach der Lieferung als Rücknahme mit Gegenbewegung (Bestand wieder hoch).
    /// Gibt es schon Zahlungen, ist zuerst eine Erstattung nötig – dann wird nicht storniert.</summary>
    public ShopOrder Cancel(string orderId, DateOnly date)
    {
        var o = Get(orderId);
        if (o.Status == "storniert") return o;
        if (o.Paid > 0 && o.Payments is not null) throw new InvalidOperationException("Für diese Bestellung gibt es schon Zahlungen – bitte zuerst erstatten.");
        if (o.StockOut == true)
        {
            var prods = Products.All();
            foreach (var l in o.Lines)
            {
                var i = prods.FindIndex(p => p.Name == l.Product);
                if (i >= 0) prods[i] = prods[i] with { Stock = prods[i].Stock + (int)l.Qty };
                Moves.Add(new(Guid.NewGuid().ToString("N")[..8], date, l.Product, l.Qty, o.Id, "Rücknahme"));
            }
            Products.Save(prods);
        }
        var c = o with { Status = "storniert", StockOut = o.StockOut == true ? false : o.StockOut };
        Set(c); return c;
    }

    /// <summary>105-05: Zahlung erfassen – bucht genau diesen Betrag als Einnahme. Gleiche Referenz zweimal ändert nichts.
    /// Mehr als offen ist, wird abgewiesen.</summary>
    public ShopOrder Pay(string orderId, decimal amount, DateOnly date, string reference, Ledger ledger, bool smallBusiness)
    {
        var o = Get(orderId);
        if (o.InvoiceNumber.Length == 0) throw new InvalidOperationException("Erst die Rechnung schreiben, dann die Zahlung erfassen.");
        if (o.Payments is null) throw new InvalidOperationException("Diese Bestellung stammt aus einer älteren Version – die Einnahme ist dort schon gebucht.");
        if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Bitte eine Referenz angeben (z. B. Datum und Betrag vom Kontoauszug).");
        if (o.Payments.Any(p => p.Ref == reference.Trim())) return o; // schon erfasst – nichts doppelt buchen
        if (amount <= 0) throw new ArgumentException("Der Betrag muss größer als 0 sein.");
        if (amount > o.Open) throw new ArgumentException($"Offen sind nur {o.Open:0.00} € – mehr kann nicht verbucht werden.");
        ledger.Add(new(date, $"Zahlung zu Rechnung {o.InvoiceNumber}", amount, "Einnahmen", smallBusiness ? 0 : LegalRates.VatStandard));
        var pays = new List<ShopPayment>(o.Payments) { new(reference.Trim(), date, amount) };
        var paid = pays.Sum(p => p.Amount);
        var done = o with { Payments = pays, Status = paid >= o.InvoiceGross ? "bezahlt" : "teilbezahlt" };
        Set(done); return done;
    }

    void Set(ShopOrder o) { var l = OrderList.All(); var i = l.FindIndex(x => x.Id == o.Id); if (i >= 0) { l[i] = o; OrderList.Save(l); } }
    public void SetStatus(string id, string status) { if (OrderList.All().FirstOrDefault(o => o.Id == id) is { } o) Set(o with { Status = status }); }

    /// <summary>F2: Rechnung aus einer Bestellung – schreibt die Rechnung und merkt Nummer und Betrag. Die Einnahme wird erst mit <see cref="Pay"/> gebucht.</summary>
    public (Invoice Invoice, string File) Invoice(string orderId, Ledger ledger, string seller, bool smallBusiness, string? logo = null)
    {
        var o = OrderList.All().FirstOrDefault(x => x.Id == orderId) ?? throw new InvalidOperationException("Diese Bestellung gibt es nicht.");
        if (o.InvoiceNumber.Length > 0) throw new InvalidOperationException($"Für diese Bestellung gibt es schon die Rechnung {o.InvoiceNumber}.");
        var c = Customers.All().FirstOrDefault(x => x.Name == o.Customer);
        var buyer = c is null ? o.Customer : string.Join("\n", new[] { c.Company, c.Name, c.Address }.Where(x => x.Trim().Length > 0));
        var inv = new Invoice(Finance.NextInvoiceNumber(ledger.InvoiceNumbers(), o.Date), o.Date, seller, buyer, o.Lines.Select(l => new InvoiceLine(l.Product, l.Qty, l.PriceNet)).ToList(), LegalRates.VatStandard, smallBusiness);
        var file = ledger.WriteInvoice(inv);
        if (!string.IsNullOrWhiteSpace(logo)) File.WriteAllText(file, FinanceExtra.WithLogo(File.ReadAllText(file), logo));
        Set(o with { InvoiceNumber = inv.Number, InvoiceGross = Finance.Totals(inv).Gross, Payments = o.Payments ?? [], Status = o.Status is "offen" or "geliefert" ? "berechnet" : o.Status });
        return (inv, file);
    }

    /// <summary>Umsatz netto nach Zahlungseingang (Datum der Zahlung, anteilig netto); Altdaten wie bisher zum Bestelldatum.</summary>
    IEnumerable<(DateOnly Date, decimal Net)> Received() =>
        Orders().Where(o => o.Status != "storniert").SelectMany(o => o.Payments is null
            ? (o.InvoiceNumber.Length > 0 ? [(o.Date, o.Net)] : Array.Empty<(DateOnly, decimal)>())
            : o.Payments.Select(p => (p.Date, o.InvoiceGross > 0 ? Math.Round(p.Amount * o.Net / o.InvoiceGross, 2) : 0m)));

    /// <summary>U1: Kennzahlen – Umsatz netto (eingegangen) im Monat und im Jahr, Kunden, offene Bestellungen (noch nicht geliefert oder berechnet).</summary>
    public (decimal Month, decimal Year, int Customers, int Open) Figures(DateOnly today)
    {
        var r = Received().ToList();
        return (r.Where(x => x.Date.Year == today.Year && x.Date.Month == today.Month).Sum(x => x.Net), r.Where(x => x.Date.Year == today.Year).Sum(x => x.Net),
            Customers.All().Count(c => c.Stage == "Kunde"), Orders().Count(x => x.Status == "offen"));
    }

    /// <summary>Summe der offenen Rechnungsbeträge (brutto).</summary>
    public decimal OpenReceivables() => Orders().Where(o => o.Status != "storniert").Sum(o => o.Open);

    /// <summary>U1: Umsatz netto je Monat eines Jahres (nach Zahlungseingang).</summary>
    public decimal[] MonthRevenue(int year) { var r = new decimal[12]; foreach (var (d, n) in Received().Where(x => x.Date.Year == year)) r[d.Month - 1] += n; return r; }

    /// <summary>F3: Webshop-Vorschau als eigenständige HTML-Seite (ohne Bezahlfunktion, ohne Fremddienste).</summary>
    public static string ShopHtml(string shopName, IEnumerable<Product> products, bool smallBusiness)
    {
        string E(string s) => WebUtility.HtmlEncode(s);
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder($"<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{E(shopName)}</title>");
        sb.Append("<style>body{font-family:system-ui,sans-serif;margin:0;background:#f5f7fa;color:#1f2933}header{background:#1f2933;color:#fff;padding:24px 16px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:16px;padding:16px;max-width:1100px;margin:auto}.c{background:#fff;border-radius:12px;padding:16px;box-shadow:0 2px 8px #0001}.p{font-size:1.3em;font-weight:600}.s{color:#667}footer{text-align:center;color:#667;padding:16px;font-size:.9em}</style></head><body>");
        sb.Append($"<header><h1>{E(shopName)}</h1></header><main>");
        foreach (var a in products)
        {
            var gross = smallBusiness ? (decimal)a.Price : Math.Round((decimal)a.Price * (1 + LegalRates.VatStandard / 100m), 2, MidpointRounding.AwayFromZero);
            sb.Append($"<div class=c><h3>{E(a.Name)}</h3><p class=p>{gross.ToString("N2", de)} €</p><p class=s>{(a.Stock > 0 ? $"{a.Stock} vorrätig" : "derzeit nicht vorrätig")}</p></div>");
        }
        sb.Append($"</main><footer>{(smallBusiness ? "Gemäß § 19 UStG wird keine Umsatzsteuer berechnet." : "Preise inkl. MwSt.")} Vorschau – Bestellung und Bezahlung sind hier nicht eingerichtet.</footer></body></html>");
        return sb.ToString();
    }
}

/// <summary>U3: Schritte der Gründung je Idee (abhakbar).</summary>
public sealed class FoundingSteps(string dataRoot)
{
    public static readonly string[] Steps = ["Idee ehrlich prüfen", "Bauplan schreiben", "Gewerbe anmelden", "Geschäftskonto eröffnen", "Preise festlegen", "Kunden und Artikel anlegen", "Webshop oder Angebot zeigen", "Erste Rechnung schreiben"];
    string F => Path.Combine(dataRoot, "business", "gruendung.json");
    Dictionary<string, List<int>> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, List<int>>>(File.ReadAllText(F)) ?? [] : []; } catch (JsonException) { return []; } }
    public HashSet<int> Done(string ideaId) => Load().TryGetValue(ideaId, out var l) ? [.. l] : [];
    public void Toggle(string ideaId, int step)
    {
        var d = Load(); var l = d.TryGetValue(ideaId, out var x) ? x : [];
        if (!l.Remove(step)) l.Add(step);
        d[ideaId] = l; Directory.CreateDirectory(Path.GetDirectoryName(F)!); File.WriteAllText(F, JsonSerializer.Serialize(d));
    }
    public int Percent(string ideaId) => Done(ideaId).Count * 100 / Steps.Length;
}
