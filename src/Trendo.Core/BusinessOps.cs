using System.Globalization;
using System.Numerics;
using System.Text;

namespace Trendo.Core;

public sealed record Member(string Name, string Email, string Plan, string Team, string Role, DateOnly Since, bool Active = true);
public sealed record PlanTier(string Name, double PriceMonth);
public sealed record BizContact(string Name, string Company, string Email, string Stage, DateOnly? Next, double Value, string Notes = "", string Address = "");
public sealed record Product(string Name, string Supplier, double Cost, double Price, int Stock, int LeadDays, double DailySales);
public sealed record Ticket(string Title, string Customer, string Priority, DateTimeOffset Created, string State = "offen", string Answer = "");
public sealed record DayStats(DateOnly Date, int Visits, int Signups, int Sales);

/// <summary>BUS-017 Mitglieder & Teams, BUS-018 Bezahlen, BUS-019 Kundenkontakte, BUS-020 Ware & Lieferanten, BUS-023 Kundendienst, BUS-024 Auswertung, BUS-025 Zahlen-Übersicht.</summary>
public static class BusinessOps
{
    // BUS-017
    public static readonly string[] Roles = ["Inhaber", "Verwalter", "Mitglied"];
    public static bool CanManage(string role) => role is "Inhaber" or "Verwalter";
    public static double Mrr(IEnumerable<Member> m, IEnumerable<PlanTier> plans)
    {
        var p = plans.ToDictionary(x => x.Name, x => x.PriceMonth);
        return m.Where(x => x.Active).Sum(x => p.GetValueOrDefault(x.Plan));
    }
    /// <summary>Mitglieder nur des eigenen Teams sichtbar – Teams sind voneinander getrennt.</summary>
    public static IReadOnlyList<Member> VisibleTo(IEnumerable<Member> all, Member viewer) => all.Where(x => x.Team == viewer.Team).ToList();

    // BUS-018
    public sealed record PayOption(string Name, double Percent, double Fixed, string Note);
    /// <summary>Gebühren-Richtwerte (Stand Oktober 2026, ohne Gewähr – aktuelle Preise beim Anbieter prüfen).</summary>
    public static readonly PayOption[] PayOptions =
    [
        new("Überweisung (SEPA)", 0, 0, "kostenlos, Geld kommt in 1–2 Tagen, kein automatischer Abgleich"),
        new("PayPal", 2.49, 0.35, "bekannt, schnell; Konto nötig"),
        new("Stripe (Karte EU)", 1.5, 0.25, "Karten, Apple/Google Pay; Konto nötig"),
        new("SumUp (Karte vor Ort)", 1.39, 0, "für Verkauf vor Ort mit Kartenleser"),
    ];
    public static double Fee(PayOption o, double amount) => Math.Round(amount * o.Percent / 100 + o.Fixed, 2);

    public static bool IbanValid(string iban)
    {
        var s = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (s.Length is < 15 or > 34 || !char.IsLetter(s[0]) || !char.IsLetter(s[1])) return false;
        var r = s[4..] + s[..4]; var sb = new StringBuilder();
        foreach (var c in r) sb.Append(char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString());
        return BigInteger.Parse(sb.ToString()) % 97 == 1;
    }
    /// <summary>GiroCode (EPC-QR, Version 002) – Kunden scannen ihn in der Banking-App.</summary>
    public static string GiroCode(string name, string iban, decimal amount, string purpose, string bic = "")
    {
        if (!IbanValid(iban)) throw new ArgumentException("IBAN ist ungültig.");
        if (amount <= 0 || amount > 999_999_999.99m) throw new ArgumentException("Betrag ungültig.");
        string Clean(string x, int max) { x = x.Replace("\n", " ").Replace("\r", " ").Trim(); return x.Length > max ? x[..max] : x; }
        return string.Join('\n', "BCD", "002", "1", "SCT", bic.Trim(), Clean(name, 70), new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant(),
            "EUR" + amount.ToString("0.00", CultureInfo.InvariantCulture), "", "", Clean(purpose, 140));
    }

    // BUS-019
    public static readonly string[] Stages = ["Interessent", "Angebot", "Kunde", "Verloren"];
    public static IReadOnlyList<BizContact> Due(IEnumerable<BizContact> c, DateOnly today) => c.Where(x => x.Next is { } n && n <= today && x.Stage != "Verloren").OrderBy(x => x.Next).ToList();
    public static double PipelineValue(IEnumerable<BizContact> c) => c.Where(x => x.Stage is "Interessent" or "Angebot").Sum(x => x.Stage == "Angebot" ? x.Value * 0.5 : x.Value * 0.1);

    // BUS-020
    public static int ReorderPoint(Product p) => (int)Math.Ceiling(p.DailySales * p.LeadDays * 1.2);
    public static bool NeedsOrder(Product p) => p.Stock <= ReorderPoint(p);
    public static int OrderQty(Product p, int days = 30) => Math.Max(0, (int)Math.Ceiling(p.DailySales * (days + p.LeadDays)) - p.Stock);
    public static double UnitMargin(Product p) => p.Price <= 0 ? 0 : (p.Price - p.Cost) / p.Price;

    // BUS-023
    public static readonly string[] Priorities = ["hoch", "normal", "niedrig"];
    public static TimeSpan Sla(string prio) => prio switch { "hoch" => TimeSpan.FromHours(4), "niedrig" => TimeSpan.FromHours(72), _ => TimeSpan.FromHours(24) };
    public static bool Overdue(Ticket t, DateTimeOffset now) => t.State != "gelöst" && now - t.Created > Sla(t.Priority);
    public static string Faq(IEnumerable<Ticket> t) =>
        "# Häufige Fragen\n\n" + string.Concat(t.Where(x => x.State == "gelöst" && x.Answer.Trim().Length > 0).GroupBy(x => GlobalSearch.Norm(x.Title)).OrderByDescending(g => g.Count())
            .Select(g => $"## {g.First().Title}\n\n{g.Last().Answer.Trim()}\n\n"));

    // BUS-024
    public static IReadOnlyList<DayStats> ImportStats(string csv)
    {
        var res = new List<DayStats>();
        foreach (var l in csv.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = l.Split(l.Contains(';') ? ';' : ',');
            if (f.Length < 4 || !DateOnly.TryParse(f[0].Trim(), CultureInfo.GetCultureInfo("de-DE"), out var d) && !DateOnly.TryParse(f[0].Trim(), CultureInfo.InvariantCulture, out d)) continue;
            if (int.TryParse(f[1], out var v) && int.TryParse(f[2], out var s) && int.TryParse(f[3], out var k)) res.Add(new(d, v, s, k));
        }
        return res.OrderBy(x => x.Date).ToList();
    }
    public sealed record Trend(double SignupRate, double SaleRate, double PrevSaleRate, string Text);
    public static Trend Analyze(IReadOnlyList<DayStats> s, int window = 7)
    {
        double R(IEnumerable<DayStats> x, Func<DayStats, int> f) { var v = x.Sum(y => y.Visits); return v == 0 ? 0 : (double)x.Sum(f) / v; }
        var last = s.TakeLast(window).ToList(); var prev = s.SkipLast(window).TakeLast(window).ToList();
        var now = R(last, x => x.Sales); var before = R(prev, x => x.Sales);
        var txt = prev.Count == 0 ? "Noch zu wenig Daten für einen Vergleich." : now > before * 1.1 ? "Es geht aufwärts." : now < before * 0.9 ? "Es geht abwärts – was hat sich geändert?" : "Ungefähr gleich.";
        return new(R(last, x => x.Signups), now, before, txt);
    }

    // BUS-025
    public sealed record PlanVsActual(double PlanProfitMonth, double ActualProfitMonth, double Months, string Text);
    public static PlanVsActual Compare(BusinessIdea i, IEnumerable<Entry> ledger, DateOnly from, DateOnly to)
    {
        var e = ledger.Where(x => x.Date >= from && x.Date <= to).ToList();
        var months = Math.Max(1, (to.Year - from.Year) * 12 + to.Month - from.Month + 1);
        var actual = (double)e.Sum(x => x.Net) / months; var plan = BusinessStudio.ProfitMonth(i);
        var txt = e.Count == 0 ? "Noch keine Buchungen im Zeitraum." : actual >= plan ? "Besser als geplant." : actual >= plan * 0.7 ? "Etwas unter Plan." : "Deutlich unter Plan – Kosten oder Preis prüfen.";
        return new(plan, Math.Round(actual, 2), months, txt);
    }
}
