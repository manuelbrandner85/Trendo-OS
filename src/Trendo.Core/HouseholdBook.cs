namespace Trendo.Core;

/// <summary>Fixkosten oder Abo, aus den Buchungen erkannt.</summary>
public sealed record Recurring(string Name, string Category, decimal Amount, string Rhythm, DateOnly Last, DateOnly Next, int Count)
{
    /// <summary>Kosten pro Monat (jährliche Zahlungen anteilig).</summary>
    public decimal PerMonth => Rhythm == "jährlich" ? Math.Round(Amount / 12, 2) : Rhythm == "vierteljährlich" ? Math.Round(Amount / 3, 2) : Amount;
}

/// <summary>Haushaltsbuch (Masterprompt 131): Monatsansicht, Fixkosten/Abos erkennen, Import-Vorschau. Ohne Netz, rechnet nur mit den Buchungen.</summary>
public static class HouseholdBook
{
    /// <summary>Buchungen eines Monats mit ihrem Index in <see cref="Ledger.All"/> (für Ändern und Stornieren), neueste zuerst.</summary>
    public static List<(int Index, Entry Entry)> Month(IReadOnlyList<Entry> all, int year, int month) =>
        all.Select((e, i) => (i, e)).Where(x => x.e.Date.Year == year && x.e.Date.Month == month).OrderByDescending(x => x.e.Date).ToList();

    /// <summary>Einnahmen, Ausgaben (positiv), Übrig und Sparquote eines Monats.</summary>
    public static (decimal Income, decimal Expense, decimal Left, double SaveRate) Sum(IEnumerable<Entry> month)
    {
        var inc = month.Where(e => e.Amount > 0).Sum(e => e.Amount); var exp = -month.Where(e => e.Amount < 0).Sum(e => e.Amount);
        return (inc, exp, inc - exp, inc <= 0 ? 0 : (double)((inc - exp) / inc));
    }

    /// <summary>Ausgaben je Kategorie, größte zuerst.</summary>
    public static List<(string Category, decimal Amount)> ByCategory(IEnumerable<Entry> month) =>
        month.Where(e => e.Amount < 0).GroupBy(e => e.Category).Select(g => (g.Key, -g.Sum(e => e.Amount))).Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).ToList();

    /// <summary>Wiederkehrende Ausgaben: gleicher Empfänger (erste Wörter), ähnlicher Betrag (±15 %), Abstand etwa ein Monat, ein Vierteljahr oder ein Jahr – und noch aktiv.</summary>
    public static List<Recurring> FindRecurring(IEnumerable<Entry> all, DateOnly today)
    {
        var list = all.ToList();
        var res = new List<Recurring>();
        var groups = list.Where(e => e.Amount < 0 && !e.Text.StartsWith(Ledger.ReversalPrefix, StringComparison.Ordinal) && !Ledger.IsReversed(list, e))
            .GroupBy(e => CategoryMemory.Key(e.Text)).Where(g => g.Key.Length > 0 && g.Count() >= 2);
        foreach (var g in groups)
        {
            var xs = g.OrderBy(e => e.Date).ToList();
            var amounts = xs.Select(e => -e.Amount).OrderBy(a => a).ToList(); var median = amounts[amounts.Count / 2];
            var similar = xs.Where(e => Math.Abs(-e.Amount - median) <= median * 0.15m).ToList();
            if (similar.Count < 2 || similar.Count == 2 && similar[0].Amount != similar[1].Amount) continue; // zwei Mal nur bei genau gleichem Betrag (Abo), sonst mindestens drei Mal
            var gaps = similar.Zip(similar.Skip(1), (a, b) => b.DayNumber() - a.DayNumber()).ToList();
            string? rhythm = gaps.All(d => d is >= 26 and <= 35) ? "monatlich" : gaps.All(d => d is >= 85 and <= 97) ? "vierteljährlich" : gaps.All(d => d is >= 350 and <= 380) ? "jährlich" : null;
            if (rhythm is null) continue;
            var last = similar[^1].Date; var next = rhythm switch { "monatlich" => last.AddMonths(1), "vierteljährlich" => last.AddMonths(3), _ => last.AddYears(1) };
            var grace = rhythm switch { "monatlich" => 20, "vierteljährlich" => 45, _ => 60 };
            if (today.DayNumber > next.DayNumber + grace) continue; // gekündigt oder ausgelaufen
            res.Add(new(similar[^1].Text, similar[^1].Category, -similar[^1].Amount, rhythm, last, next, similar.Count));
        }
        return res.OrderByDescending(r => r.PerMonth).ToList();
    }

    static int DayNumber(this Entry e) => e.Date.DayNumber;

    /// <summary>Import-Vorschau: jede Buchung mit geratener Kategorie und ob sie schon im Haushaltsbuch steht.</summary>
    public static List<(Entry Entry, bool Duplicate)> Preview(IEnumerable<Entry> existing, IEnumerable<Entry> incoming, CategoryMemory? memory)
    {
        var keys = existing.Select(x => (x.Date, x.Amount, x.Text)).ToHashSet();
        var seen = new HashSet<(DateOnly, decimal, string)>();
        return FinanceExtra.Categorize(incoming, memory).Select(e => (e, keys.Contains((e.Date, e.Amount, e.Text)) || !seen.Add((e.Date, e.Amount, e.Text)))).ToList();
    }
}
