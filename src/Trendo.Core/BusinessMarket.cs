using System.Text.Json;

namespace Trendo.Core;

/// <summary>Speichert Listen je Geschäftsidee lokal unter business/&lt;idee&gt;/&lt;name&gt;.json.</summary>
public sealed class IdeaList<T>(string dataRoot, string ideaId, string name) where T : class
{
    private readonly string _file = Path.Combine(dataRoot, "business", DevStudio.SafeName(ideaId), name + ".json");
    public List<T> All() => File.Exists(_file) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(_file)) ?? [] : [];
    public void Save(List<T> l) { Directory.CreateDirectory(Path.GetDirectoryName(_file)!); File.WriteAllText(_file, JsonSerializer.Serialize(l, new JsonSerializerOptions { WriteIndented = true })); }
    public void Add(T x) { var l = All(); l.Add(x); Save(l); }
    public void RemoveAt(int i) { var l = All(); if (i >= 0 && i < l.Count) { l.RemoveAt(i); Save(l); } }
}

public sealed record Competitor(string Name, string Url, double Price, string Strengths, string Weaknesses, string Description = "");
public sealed record ProblemNote(string Who, string Problem, int Pain, int Frequency, string Source = "Gespräch");
public sealed record Experiment(string Hypothesis, string Metric, double Target, double? Result = null, string Notes = "")
{
    public string State => Result is null ? "läuft" : Result >= Target ? "bestätigt" : "nicht bestätigt";
}
public sealed record Variant(string Method, string Text);

/// <summary>BUS-003/004/006/007/008/009: Markt, Probleme, neue Varianten, Branchen-Mix, Eigenständigkeit, Tests am Markt.</summary>
public static class BusinessMarket
{
    // BUS-003
    public sealed record PricePosition(double Min, double Median, double Max, string Where, IReadOnlyList<(string Word, int Count)> CommonWeaknesses);
    public static PricePosition? Prices(IReadOnlyList<Competitor> c, double ourPrice)
    {
        var p = c.Where(x => x.Price > 0).Select(x => x.Price).OrderBy(x => x).ToList();
        if (p.Count == 0) return null;
        var med = p.Count % 2 == 1 ? p[p.Count / 2] : (p[p.Count / 2 - 1] + p[p.Count / 2]) / 2;
        var where = ourPrice <= 0 ? "Noch kein eigener Preis." : ourPrice < p[0] ? "Du bist am günstigsten." : ourPrice > p[^1] ? "Du bist am teuersten – das braucht einen klaren Mehrwert." : ourPrice <= med ? "Du liegst im unteren Mittelfeld." : "Du liegst im oberen Mittelfeld.";
        var weak = c.SelectMany(x => Words(x.Weaknesses)).GroupBy(w => w).Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).Select(g => (g.Key, g.Count())).Take(8).ToList();
        return new(p[0], med, p[^1], where, weak);
    }

    static readonly HashSet<string> Stop = ["und", "oder", "der", "die", "das", "ein", "eine", "ist", "zu", "nicht", "mit", "für", "von", "auf", "kein", "keine", "sehr", "zu", "im", "in", "es", "man", "the", "and", "of"];
    public static IEnumerable<string> Words(string s) => GlobalSearch.Norm(s).Split([' ', ',', '.', ';', ':', '!', '?', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2 && !Stop.Contains(w));

    // BUS-004
    public sealed record Niche(string Who, string Problem, int Score, int Mentions);
    public static IReadOnlyList<Niche> Niches(IReadOnlyList<ProblemNote> notes) =>
        notes.GroupBy(n => (Who: GlobalSearch.Norm(n.Who), Prob: string.Join(' ', Words(n.Problem).Take(4))))
             .Select(g => new Niche(g.First().Who, g.First().Problem, g.Sum(n => Math.Clamp(n.Pain, 1, 5) * Math.Clamp(n.Frequency, 1, 5)), g.Count()))
             .OrderByDescending(n => n.Score).ThenByDescending(n => n.Mentions).ToList();

    // BUS-006 – SCAMPER: sieben Fragen, jede liefert eine konkrete Variante aus deinen eigenen Angaben
    public static IReadOnlyList<Variant> Variants(BusinessIdea i)
    {
        string a = i.Audience.Length > 0 ? i.Audience : "deine Kunden", s = i.Solution.Length > 0 ? i.Solution : i.Name, p = i.Problem.Length > 0 ? i.Problem : "das Problem";
        return
        [
            new("Ersetzen", $"Was wäre, wenn {s} komplett digital (oder komplett vor Ort) liefe?"),
            new("Kombinieren", $"Biete {s} zusammen mit einer passenden Schulung oder Vorlage an."),
            new("Anpassen", $"Übertrage {s} auf eine zweite Zielgruppe neben {a}."),
            new("Verändern", $"Mach eine Premium-Version mit persönlicher Betreuung und eine günstige Selbstbedienungs-Version."),
            new("Anders nutzen", $"Verkaufe das Wissen dahinter: Anleitung, wie man {p} selbst löst."),
            new("Weglassen", $"Streiche alles bis auf den Kern – was ist die kleinste Version, die {p} schon löst?"),
            new("Umdrehen", $"Lass {a} selbst Anbieter werden – du vermittelst nur."),
        ];
    }

    // BUS-007
    public static readonly (string Branche, string Idee)[] Industries =
    [
        ("Fitnessstudio", "Mitgliedschaft mit Check-in"), ("Streaming", "monatliches Abo mit Empfehlungen"), ("Supermarkt", "Treuepunkte"), ("Fluglinie", "Preise nach Nachfrage"),
        ("Bibliothek", "Ausleihen statt kaufen"), ("Spielwelt", "Level, Abzeichen und Ranglisten"), ("Werkstatt", "Wartungsplan mit Erinnerung"), ("Wochenmarkt", "Direkt vom Erzeuger"),
        ("Hotel", "Buchung mit Zusatzleistungen"), ("Verein", "Gemeinschaft mit Beitrag"),
    ];
    public static IReadOnlyList<Variant> Fusions(BusinessIdea i, int max = 10) =>
        Industries.Take(max).Select(x => new Variant("Mix mit " + x.Branche, $"{(i.Name.Length > 0 ? i.Name : "Deine Idee")} + {x.Idee}: Übernimm das Prinzip „{x.Idee}“ aus der Branche {x.Branche}.")).ToList();

    // BUS-008
    public static double TextSimilarity(string a, string b)
    {
        var x = Words(a).ToHashSet(); var y = Words(b).ToHashSet();
        return x.Count == 0 || y.Count == 0 ? 0 : (double)x.Intersect(y).Count() / x.Union(y).Count();
    }
    public static IReadOnlyList<(string Name, double Score)> Originality(BusinessIdea i, IReadOnlyList<Competitor> c)
    {
        var ours = $"{i.Problem} {i.Solution} {i.Audience}";
        return c.Select(x => (x.Name, Math.Round(TextSimilarity(ours, $"{x.Description} {x.Strengths}"), 2))).OrderByDescending(x => x.Item2).ToList();
    }
    public static string OriginalityText(double top) => top >= 0.5 ? "Sehr ähnlich zu einem Mitbewerber – finde einen klaren Unterschied." : top >= 0.25 ? "Teilweise ähnlich – betone, was bei dir anders ist." : "Deutlich eigenständig.";

    // BUS-009
    public static (int Done, int Confirmed, string Verdict) Validation(IReadOnlyList<Experiment> e)
    {
        var done = e.Count(x => x.Result is not null); var ok = e.Count(x => x.State == "bestätigt");
        var v = done == 0 ? "Noch nicht getestet." : ok * 2 >= done && done >= 2 ? "Der Markt bestätigt die Idee." : ok == 0 ? "Bisher keine Bestätigung – Idee anpassen." : "Gemischt – weiter testen.";
        return (done, ok, v);
    }
    public static readonly Experiment[] ExperimentIdeas =
    [
        new("Mindestens 20 von 100 Besuchern tragen sich auf der Angebotsseite ein.", "Eintragungen in %", 20),
        new("10 Gespräche mit der Zielgruppe – mindestens 6 nennen das Problem von sich aus.", "Gespräche mit Problem", 6),
        new("3 Vorbestellungen in 2 Wochen.", "Vorbestellungen", 3),
    ];
}
