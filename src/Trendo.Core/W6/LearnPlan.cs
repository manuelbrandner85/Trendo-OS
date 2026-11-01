namespace Trendo.Core;

/// <summary>Lernplan eines Stapels: Prüfungstermin (optional) und neue Karten pro Tag.</summary>
public sealed class StudyPlan
{
    public string Deck { get; set; } = "";
    public DateTime? Exam { get; set; }
    public int NewPerDay { get; set; } = 10;
    /// <summary>Neue Karten pro Tag automatisch aus dem Prüfungstermin berechnen (sonst fester Wert).</summary>
    public bool Auto { get; set; }
}

/// <summary>Lernkarten mit Lernplan (Masterprompt 131, Runde 6): Prüfungstermin und Tagesziel je Stapel, Tagesplan aus fälligen und neuen Karten
/// (neue Karten werden im Tempo des Plans freigegeben), Kalender der nächsten Wochen mit Prüfungstag, Lernverlauf und Erinnerung.</summary>
public sealed partial class LearnBox
{
    static bool IsNew(Flashcard c) => c.Reps == 0 && c.Lapses == 0 && c.FirstReviewed is null;

    public StudyPlan? Plan(string deck) => Store.Plans.FirstOrDefault(p => p.Deck.Equals(deck, StringComparison.OrdinalIgnoreCase));

    /// <summary>Plan setzen. Mit Prüfungstermin und ohne feste Zahl rechnet Trendo die neuen Karten pro Tag aus (fertig 3 Tage vorher).</summary>
    public StudyPlan SetPlan(string deck, DateTime? exam, int? newPerDay)
    {
        if (!Decks.Contains(deck)) throw new ArgumentException($"Stapel „{deck}“ gibt es nicht.");
        if (exam is { } e && e.Date <= _now().Date) throw new ArgumentException("Der Prüfungstermin muss in der Zukunft liegen.");
        if (newPerDay is < 0 or > 500) throw new ArgumentException("Neue Karten pro Tag: zwischen 0 und 500.");
        var p = Plan(deck) ?? new StudyPlan { Deck = deck };
        p.Exam = exam?.Date; p.Auto = newPerDay is null && exam is not null;
        p.NewPerDay = newPerDay ?? (exam is { } ex ? ExamPlan(ex, deck).NewPerDay : 10);
        if (!Store.Plans.Contains(p)) Store.Plans.Add(p);
        Save(); return p;
    }

    public bool RemovePlan(string deck) { var n = Store.Plans.RemoveAll(p => p.Deck.Equals(deck, StringComparison.OrdinalIgnoreCase)); if (n > 0) Save(); return n > 0; }

    /// <summary>Neue Karten pro Tag laut Plan (bei „automatisch“ täglich neu aus dem verbleibenden Stoff berechnet); ohne Plan: alle.</summary>
    public int NewPerDay(string deck)
    {
        if (Plan(deck) is not { } p) return int.MaxValue;
        if (p.Auto && p.Exam is { } e && e.Date > _now().Date) return ExamPlan(e, deck).NewPerDay;
        return p.NewPerDay;
    }

    public int IntroducedToday(string? deck = null) => Store.Cards.Count(c => (deck is null || c.Deck == deck) && c.FirstReviewed?.Date == _now().Date);

    /// <summary>Tagesplan: fällige Wiederholungen + heute noch erlaubte neue Karten, heute schon geschafft, geschätzte Minuten (≈ 10 s je Karte).</summary>
    public (int Reviews, int NewAllowed, int DoneToday, int Goal, int Minutes) Today(string? deck = null)
    {
        var decks = deck is null ? Decks : [deck];
        var reviews = Store.Cards.Count(c => decks.Contains(c.Deck) && !IsNew(c) && c.Due <= _now());
        var newAllowed = decks.Sum(d => Math.Max(0, Math.Min(Store.Cards.Count(c => c.Deck == d && IsNew(c)), NewPerDay(d) == int.MaxValue ? int.MaxValue : NewPerDay(d) - IntroducedToday(d))));
        var done = Store.ReviewLog.GetValueOrDefault(_now().ToString("yyyy-MM-dd"));
        var goal = done + reviews + newAllowed;
        return (reviews, newAllowed, done, goal, (int)Math.Ceiling((reviews + newAllowed) * 10 / 60.0));
    }

    /// <summary>Lern-Reihenfolge für heute: zuerst fällige Wiederholungen (älteste zuerst), dann neue Karten bis zum Tagesziel des Plans.</summary>
    public IReadOnlyList<Flashcard> SessionQueue(string? deck = null)
    {
        var decks = deck is null ? Decks : [deck];
        var reviews = Store.Cards.Where(c => decks.Contains(c.Deck) && !IsNew(c) && c.Due <= _now()).OrderBy(c => c.Due).ToList();
        var fresh = new List<Flashcard>();
        foreach (var d in decks)
        {
            var allowed = NewPerDay(d) == int.MaxValue ? int.MaxValue : Math.Max(0, NewPerDay(d) - IntroducedToday(d));
            fresh.AddRange(Store.Cards.Where(c => c.Deck == d && IsNew(c) && c.Due <= _now()).Take(allowed));
        }
        return [.. reviews, .. fresh];
    }

    /// <summary>Kalender: je Tag fällige Wiederholungen, geplante neue Karten und ob die Prüfung ist (für die nächsten Wochen).</summary>
    public IReadOnlyList<(DateTime Day, int Reviews, int New, bool Exam)> Calendar(int days = 28, string? deck = null)
    {
        var t = _now().Date; var decks = deck is null ? Decks : [deck];
        var remainingNew = decks.ToDictionary(d => d, d => Store.Cards.Count(c => c.Deck == d && IsNew(c)));
        var list = new List<(DateTime, int, int, bool)>();
        for (var i = 0; i < days; i++)
        {
            var day = t.AddDays(i);
            var rev = Store.Cards.Count(c => decks.Contains(c.Deck) && !IsNew(c) && (i == 0 ? c.Due.Date <= t : c.Due.Date == day));
            var neu = 0;
            foreach (var d in decks)
            {
                var per = NewPerDay(d); var p = Plan(d);
                if (p?.Exam is { } e && day > e.Date.AddDays(-3)) per = 0; // die letzten 3 Tage nur wiederholen
                var take = i == 0 ? Math.Min(remainingNew[d], per == int.MaxValue ? remainingNew[d] : Math.Max(0, per - IntroducedToday(d))) : Math.Min(remainingNew[d], per == int.MaxValue ? 0 : per);
                remainingNew[d] -= take; neu += take;
            }
            list.Add((day, rev, neu, decks.Any(d => Plan(d)?.Exam?.Date == day)));
        }
        return list;
    }

    /// <summary>Lernverlauf: Wiederholungen je Tag der letzten Wochen (für die Verlaufskacheln).</summary>
    public IReadOnlyList<(DateTime Day, int Count)> History(int days = 84)
    {
        var t = _now().Date;
        return Enumerable.Range(0, days).Select(i => t.AddDays(i - days + 1)).Select(d => (d, Store.ReviewLog.GetValueOrDefault(d.ToString("yyyy-MM-dd")))).ToList();
    }

    /// <summary>Erinnerung für heute (null, wenn nichts ansteht oder das Ziel geschafft ist).</summary>
    public string? Reminder()
    {
        var (rev, neu, done, goal, min) = Today();
        if (rev + neu == 0) return null;
        var exam = Store.Plans.Where(p => p.Exam is { } e && e.Date >= _now().Date).OrderBy(p => p.Exam).FirstOrDefault();
        return $"Heute zu lernen: {rev} Wiederholung{(rev == 1 ? "" : "en")} und {neu} neue Karte{(neu == 1 ? "" : "n")} (etwa {min} min)." +
            (done > 0 ? $" Schon geschafft: {done} von {goal}." : "") + (exam is { Exam: { } ed } ? $" Prüfung „{exam.Deck}“ in {(ed.Date - _now().Date).Days} Tagen." : "");
    }
}
