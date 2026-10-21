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

public sealed class CardStore { public List<Flashcard> Cards { get; set; } = []; }

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
        c.Due = _now().Date.AddDays(c.IntervalDays); Save(); return c;
    }

    public (int Total, int Due, int Learned, int Hard) Progress(string? deck = null)
    {
        var cs = Store.Cards.Where(c => deck is null || c.Deck == deck).ToList();
        return (cs.Count, cs.Count(c => c.Due <= _now()), cs.Count(c => c.IntervalDays >= 21), cs.Count(c => c.Lapses >= 3));
    }

    public void Delete(string id) { Store.Cards.RemoveAll(c => c.Id == id); Save(); }
}
