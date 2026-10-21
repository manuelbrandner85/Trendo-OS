namespace Trendo.Core;

public enum Grade { Falsch = 0, Schwer = 3, Gut = 4, Leicht = 5 }

public sealed class Flashcard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Deck { get; set; } = "Allgemein";
    public string Front { get; set; } = "";
    public string Back { get; set; } = "";
    public double Ease { get; set; } = 2.5;
    public int IntervalDays { get; set; }
    public int Reps { get; set; }
    public int Lapses { get; set; }
    public DateTime Due { get; set; }
}

public sealed class CardStore { public List<Flashcard> Cards { get; set; } = []; public List<DateTime> ReviewDays { get; set; } = []; }

/// <summary>SPEC-06: Lernen & Training – Karteikarten mit Wiederholung in wachsenden Abständen (SM-2).</summary>
public sealed class LearnBox(string dataRoot, Func<DateTime>? now = null)
{
    private readonly Func<DateTime> _now = now ?? (() => DateTime.Now);
    public CardStore Store { get; } = Fach.Load<CardStore>(dataRoot, "lernen", "karten");
    private void Save() => Fach.Save(dataRoot, "lernen", "karten", Store);

    public Flashcard Add(string deck, string front, string back)
    {
        if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back)) throw new ArgumentException("Bitte Frage und Antwort ausfüllen.");
        var c = new Flashcard { Deck = string.IsNullOrWhiteSpace(deck) ? "Allgemein" : deck.Trim(), Front = front.Trim(), Back = back.Trim(), Due = _now() };
        Store.Cards.Add(c); Save(); return c;
    }

    /// <summary>Import „Frage;Antwort“ oder Tab-getrennt (z. B. aus Anki-Export). Doppelte werden übersprungen.</summary>
    public int Import(string deck, string text)
    {
        var n = 0;
        foreach (var l in text.Replace("\r", "").Split('\n'))
        {
            var p = l.Contains('\t') ? l.Split('\t', 2) : l.Split(';', 2);
            if (p.Length < 2 || p[0].Trim() == "" || p[1].Trim() == "" || l.TrimStart().StartsWith('#')) continue;
            if (Store.Cards.Any(c => c.Deck == deck && c.Front == p[0].Trim())) continue;
            Store.Cards.Add(new Flashcard { Deck = deck, Front = p[0].Trim(), Back = p[1].Trim(), Due = _now() }); n++;
        }
        Save(); return n;
    }

    public IReadOnlyList<string> Decks => Store.Cards.Select(c => c.Deck).Distinct().OrderBy(d => d).ToList();
    public IReadOnlyList<Flashcard> DueCards(string? deck = null) => Store.Cards.Where(c => (deck is null || c.Deck == deck) && c.Due <= _now()).OrderBy(c => c.Due).ToList();

    /// <summary>SM-2: Falsch → morgen nochmal von vorn; sonst 1 → 6 → Intervall × Leichtigkeit.</summary>
    public Flashcard Answer(string id, Grade g)
    {
        var c = Store.Cards.First(x => x.Id == id); var q = (int)g;
        if (q < 3) { c.Reps = 0; c.IntervalDays = 1; c.Lapses++; }
        else { c.IntervalDays = c.Reps switch { 0 => 1, 1 => 6, _ => (int)Math.Round(c.IntervalDays * c.Ease) }; c.Reps++; }
        c.Ease = Math.Max(1.3, c.Ease + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02)));
        c.Due = _now().Date.AddDays(c.IntervalDays); if (!Store.ReviewDays.Contains(_now().Date)) Store.ReviewDays.Add(_now().Date); Save(); return c;
    }

    public (int Total, int Due, int Learned, int Hard) Progress(string? deck = null)
    {
        var cs = Store.Cards.Where(c => deck is null || c.Deck == deck).ToList();
        return (cs.Count, cs.Count(c => c.Due <= _now()), cs.Count(c => c.IntervalDays >= 21), cs.Count(c => c.Lapses >= 3));
    }

    public void Delete(string id) { Store.Cards.RemoveAll(c => c.Id == id); Save(); }

    // ---------- Ausbau ----------

    /// <summary>F07: Stapel als CSV (Frage;Antwort), passend zum Import.</summary>
    public string Export(string? deck = null) => string.Join("\n", Store.Cards.Where(c => deck is null || c.Deck == deck).Select(c => $"{c.Front.Replace(';', ',')};{c.Back.Replace(';', ',')}")) + "\n";

    /// <summary>F08: Karte ändern.</summary>
    public Flashcard Edit(string id, string front, string back)
    {
        if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back)) throw new ArgumentException("Bitte Frage und Antwort ausfüllen.");
        var c = Store.Cards.FirstOrDefault(x => x.Id == id) ?? throw new ArgumentException("Karte nicht gefunden."); c.Front = front.Trim(); c.Back = back.Trim(); Save(); return c;
    }

    /// <summary>F09: Auswahl-Quiz: richtige Antwort + bis zu 3 andere Antworten aus demselben Stapel, gemischt.</summary>
    public (Flashcard Card, IReadOnlyList<string> Options, int Correct) Quiz(string? deck = null, int? seed = null)
    {
        var pool = Store.Cards.Where(c => deck is null || c.Deck == deck).ToList();
        if (pool.Select(c => c.Back).Distinct().Count() < 2) throw new InvalidOperationException("Für ein Quiz braucht der Stapel mindestens 2 verschiedene Antworten.");
        var rnd = seed is { } s ? new Random(s) : Random.Shared; var due = pool.Where(c => c.Due <= _now()).ToList(); var card = (due.Count > 0 ? due : pool)[rnd.Next((due.Count > 0 ? due : pool).Count)];
        var opts = pool.Select(c => c.Back).Where(b => b != card.Back).Distinct().OrderBy(_ => rnd.Next()).Take(3).Append(card.Back).OrderBy(_ => rnd.Next()).ToList();
        return (card, opts, opts.IndexOf(card.Back));
    }

    public static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1]; for (var i = 0; i <= a.Length; i++) d[i, 0] = i; for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++) for (var j = 1; j <= b.Length; j++) d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    /// <summary>F10: Eingetippte Antwort prüfen: Groß/klein egal, 1 Tippfehler je 6 Zeichen erlaubt.</summary>
    public static (bool Ok, bool Typo) CheckTyped(string expected, string typed)
    {
        static string N(string s) => string.Join(' ', s.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.', '!');
        var e = N(expected); var t = N(typed); if (e == t) return (true, false);
        var d = Levenshtein(e, t); return d <= Math.Max(1, e.Length / 6) && t.Length > 0 ? (true, true) : (false, false);
    }

    /// <summary>F11: Lernserie: Tage in Folge bis heute (oder gestern).</summary>
    public (int Streak, bool Today) Streak()
    {
        var days = Store.ReviewDays.Select(d => d.Date).ToHashSet(); var today = _now().Date; var start = days.Contains(today) ? today : today.AddDays(-1); var n = 0;
        while (days.Contains(start.AddDays(-n))) n++; return (n, days.Contains(today));
    }

    /// <summary>F12: Fällige Karten je Tag für die nächsten 7 Tage (überfällige zählen heute).</summary>
    public IReadOnlyList<(DateTime Day, int Due)> Forecast(int days = 7) { var t = _now().Date; return Enumerable.Range(0, days).Select(i => (t.AddDays(i), Store.Cards.Count(c => i == 0 ? c.Due.Date <= t : c.Due.Date == t.AddDays(i)))).ToList(); }

    /// <summary>F13: Stapel umbenennen (oder mit bestehendem zusammenführen).</summary>
    public int RenameDeck(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("Bitte einen neuen Namen angeben.");
        var n = 0; foreach (var c in Store.Cards.Where(c => c.Deck.Equals(from.Trim(), StringComparison.OrdinalIgnoreCase))) { c.Deck = to.Trim(); n++; }
        if (n == 0) throw new ArgumentException($"Stapel „{from}“ gibt es nicht."); Save(); return n;
    }

    /// <summary>F14: Problemkarten: 3× oder öfter vergessen, oder Leichtigkeit am Minimum.</summary>
    public IReadOnlyList<Flashcard> Leeches() => Store.Cards.Where(c => c.Lapses >= 3 || c.Reps > 0 && c.Ease <= 1.31).OrderByDescending(c => c.Lapses).ToList();

    /// <summary>F15: Umgekehrte Karten erzeugen (Antwort → Frage), Stapel „… (umgekehrt)“; vorhandene werden übersprungen.</summary>
    public int AddReversed(string deck)
    {
        var target = deck + " (umgekehrt)"; var n = 0;
        foreach (var c in Store.Cards.Where(c => c.Deck == deck).ToList())
            if (!Store.Cards.Any(x => x.Deck == target && x.Front == c.Back)) { Store.Cards.Add(new Flashcard { Deck = target, Front = c.Back, Back = c.Front, Due = _now() }); n++; }
        Save(); return n;
    }

    /// <summary>F16: Prüfungsplan: neue (noch nie gelernte) Karten gleichmäßig verteilen, 3 Tage vorher fertig.</summary>
    public (int NewPerDay, int DaysLeft, int NewCards, string Advice) ExamPlan(DateTime exam, string? deck = null)
    {
        var days = (exam.Date - _now().Date).Days; if (days <= 0) throw new ArgumentException("Der Prüfungstermin muss in der Zukunft liegen.");
        var fresh = Store.Cards.Count(c => (deck is null || c.Deck == deck) && c.Reps == 0 && c.Lapses == 0); var learnDays = Math.Max(1, days - 3); var per = (int)Math.Ceiling((double)fresh / learnDays);
        return (per, days, fresh, per > 50 ? "Sehr viel – lieber früher anfangen oder Stoff aufteilen." : days <= 3 ? "Kurz vor der Prüfung: nur noch wiederholen." : "Gut machbar, wenn du jeden Tag lernst.");
    }
}
