using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>STUDIO-ONLINE-BUSINESS-01 (Teil): Idee erfassen (BUS-001), Geschäftsmodelle (BUS-002), Bewertung (BUS-005),
/// Go/No-Go (BUS-010), Bauplan (BUS-011), Preis & Gewinnschwelle (BUS-012), rechtliche Schritte DE (BUS-014). Alles lokal, ohne Online-Dienst.</summary>
public sealed record BusinessModel(string Id, string Name, string How, string Plus, string Minus, double TypicalMargin, double StartCapital);

public sealed class BusinessIdea
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Problem { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Solution { get; set; } = "";
    public string ModelId { get; set; } = "abo";
    public double Price { get; set; }            // € pro Verkauf bzw. pro Monat
    public double VariableCost { get; set; }     // € je Verkauf
    public double FixedCostMonth { get; set; }   // € pro Monat
    public double CustomersMonth { get; set; }   // geschätzte Verkäufe/Kunden pro Monat
    public int Competitors { get; set; }         // bekannte Mitbewerber
    public int Skill { get; set; } = 3;          // 1–5: wie gut kannst du das
    public double HoursWeek { get; set; } = 10;
    public double Capital { get; set; }          // verfügbares Startgeld €
    public string Notes { get; set; } = "";
}

public sealed record ScorePart(string Label, int Points, int Max, string Why);
public sealed record Verdict(int Score, string Decision, IReadOnlyList<ScorePart> Parts, IReadOnlyList<string> Blockers);

public static class BusinessStudio
{
    public static readonly IReadOnlyList<BusinessModel> Models =
    [
        new("abo", "Abo (monatlich)", "Kunden zahlen jeden Monat für einen Dienst.", "planbare Einnahmen", "Kunden müssen dauerhaft zufrieden sein", 0.75, 500),
        new("digital", "Digitales Produkt", "E-Book, Vorlage, Kurs – einmal erstellen, oft verkaufen.", "fast keine Kosten pro Verkauf", "Sichtbarkeit ist schwer", 0.9, 200),
        new("dienst", "Dienstleistung", "Du arbeitest für Kunden, z. B. Webseiten oder Texte.", "schnell startklar, wenig Geld nötig", "Zeit = Geld, schwer zu wachsen", 0.6, 100),
        new("shop", "Online-Shop (eigene Ware)", "Du kaufst oder machst Ware und verkaufst sie.", "greifbares Produkt", "Lager, Versand, Retouren", 0.35, 3000),
        new("dropship", "Versand durch Lieferanten", "Ein Lieferant verschickt direkt an deine Kunden.", "kein Lager", "kleine Marge, wenig Kontrolle", 0.2, 500),
        new("marktplatz", "Marktplatz / Vermittlung", "Du bringst Anbieter und Käufer zusammen und nimmst Gebühr.", "wächst mit anderen", "braucht beide Seiten gleichzeitig", 0.8, 2000),
        new("werbung", "Inhalte mit Werbung", "Blog, Video oder Podcast, Geld über Werbung/Partner.", "günstig zu starten", "lange Anlaufzeit", 0.7, 100),
        new("lokal", "Lokales Angebot online", "Ein Angebot vor Ort (Kurs, Reparatur) mit Online-Buchung.", "wenig Konkurrenz vor Ort", "begrenzte Reichweite", 0.55, 800),
        new("software", "Software / App", "Ein Programm, für das Kunden zahlen.", "sehr gut skalierbar", "Bauen dauert, Pflege nötig", 0.85, 1000),
        new("affiliate", "Empfehlungen (Partnerprogramme)", "Du empfiehlst Produkte und bekommst Provision.", "kein eigenes Produkt nötig", "abhängig vom Partner", 0.95, 50),
    ];

    public static BusinessModel Model(string id) => Models.FirstOrDefault(m => m.Id == id) ?? Models[0];

    public static double Margin(BusinessIdea i) => i.Price <= 0 ? 0 : (i.Price - i.VariableCost) / i.Price;
    public static double ProfitMonth(BusinessIdea i) => (i.Price - i.VariableCost) * i.CustomersMonth - i.FixedCostMonth;
    /// <summary>Verkäufe pro Monat bis zur Gewinnschwelle; null wenn jeder Verkauf Verlust macht.</summary>
    public static double? BreakEven(BusinessIdea i) => i.Price - i.VariableCost <= 0 ? null : Math.Ceiling(i.FixedCostMonth / (i.Price - i.VariableCost));
    public static double HourlyWage(BusinessIdea i) => i.HoursWeek <= 0 ? 0 : ProfitMonth(i) / (i.HoursWeek * 4.33);

    public static Verdict Evaluate(BusinessIdea i)
    {
        var parts = new List<ScorePart>(); var blockers = new List<string>();
        int clarity = (i.Problem.Trim().Length >= 20 ? 8 : i.Problem.Trim().Length > 0 ? 4 : 0) + (i.Audience.Trim().Length >= 5 ? 6 : 0) + (i.Solution.Trim().Length >= 20 ? 6 : i.Solution.Trim().Length > 0 ? 3 : 0);
        parts.Add(new("Klarheit", clarity, 20, clarity >= 16 ? "Problem, Kunden und Lösung sind klar." : "Beschreibe genauer: Welches Problem? Für wen? Wie löst du es?"));
        var m = Margin(i); var mp = (int)Math.Round(Math.Clamp(m, 0, 0.8) / 0.8 * 20);
        parts.Add(new("Marge", mp, 20, m <= 0 ? "Jeder Verkauf macht Verlust." : $"Von jedem Euro bleiben {m * 100:0} Cent."));
        if (i.Price > 0 && m <= 0) blockers.Add("Der Preis ist kleiner als die Kosten pro Verkauf.");
        var be = BreakEven(i); int bp;
        if (be is null) bp = 0;
        else if (i.CustomersMonth <= 0) bp = 3;
        else { var ratio = be.Value / i.CustomersMonth; bp = ratio <= 0.5 ? 20 : ratio <= 1 ? 14 : ratio <= 2 ? 6 : 0; }
        parts.Add(new("Gewinnschwelle", bp, 20, be is null ? "Keine Gewinnschwelle erreichbar." : $"Ab {be:0} Verkäufen im Monat deckst du die festen Kosten (geschätzt: {i.CustomersMonth:0})."));
        int cp = i.Competitors switch { 0 => 7, <= 3 => 15, <= 10 => 11, <= 30 => 6, _ => 2 };
        parts.Add(new("Markt", cp, 15, i.Competitors == 0 ? "Keine Mitbewerber gefunden – prüfe, ob es Nachfrage gibt." : i.Competitors > 30 ? "Sehr viele Mitbewerber – du brauchst einen klaren Unterschied." : "Es gibt Nachfrage und Platz für dich."));
        var need = Model(i.ModelId).StartCapital; var kp = i.Capital >= need ? 10 : i.Capital >= need / 2 ? 5 : 0;
        parts.Add(new("Startgeld", kp, 10, i.Capital >= need ? "Dein Startgeld reicht für den Anfang." : $"Für dieses Modell sind etwa {need:0} € üblich."));
        var sp = Math.Clamp(i.Skill, 1, 5) * 2 + (i.HoursWeek >= 10 ? 5 : i.HoursWeek >= 4 ? 3 : 0);
        parts.Add(new("Du selbst", sp, 15, i.HoursWeek < 4 ? "Unter 4 Stunden pro Woche wird es sehr langsam." : "Können und Zeit passen."));
        if (i.HoursWeek <= 0) blockers.Add("Ohne Zeit geht es nicht.");
        var score = parts.Sum(p => p.Points);
        var decision = blockers.Count > 0 ? "Nicht starten" : score >= 70 ? "Starten" : score >= 50 ? "Erst testen" : "Überarbeiten";
        return new(score, decision, parts, blockers);
    }

    public static readonly string[] LegalStepsDe =
    [
        "Gewerbe anmelden (Gewerbeamt) – bei freien Berufen stattdessen Finanzamt",
        "Fragebogen zur steuerlichen Erfassung (ELSTER) ausfüllen, Kleinunternehmer ja/nein entscheiden",
        "Impressum auf der Webseite",
        "Datenschutzerklärung (DSGVO) und Cookie-Hinweis, falls nötig",
        "AGB und Widerrufsbelehrung bei Verkäufen an Privatkunden",
        "Preise mit Mehrwertsteuer angeben (außer Kleinunternehmer)",
        "Rechnungen mit Pflichtangaben schreiben",
        "Markenname prüfen (DPMA-Register) und Domain sichern",
        "Geschäftskonto und Buchhaltung einrichten",
        "Versicherungen prüfen (z. B. Betriebshaftpflicht)",
    ];

    public static string Blueprint(BusinessIdea i)
    {
        var c = CultureInfo.GetCultureInfo("de-DE"); var v = Evaluate(i); var m = Model(i.ModelId);
        var sb = new StringBuilder();
        sb.AppendLine($"# Bauplan: {i.Name}").AppendLine().AppendLine("_Alle Zahlen sind deine Schätzungen – keine Garantie._").AppendLine();
        sb.AppendLine("## Idee").AppendLine($"- Problem: {i.Problem}").AppendLine($"- Für wen: {i.Audience}").AppendLine($"- Lösung: {i.Solution}").AppendLine($"- Modell: {m.Name} – {m.How}").AppendLine();
        sb.AppendLine("## Zahlen pro Monat");
        sb.AppendLine(string.Create(c, $"- Preis: {i.Price:C} · Kosten je Verkauf: {i.VariableCost:C} · Feste Kosten: {i.FixedCostMonth:C}"));
        sb.AppendLine(string.Create(c, $"- Geschätzte Verkäufe: {i.CustomersMonth:0} · Gewinn: {ProfitMonth(i):C} · Stundenlohn: {HourlyWage(i):C}"));
        var be = BreakEven(i); sb.AppendLine(be is null ? "- Gewinnschwelle: nicht erreichbar" : $"- Gewinnschwelle: {be:0} Verkäufe pro Monat").AppendLine();
        sb.AppendLine($"## Bewertung: {v.Score}/100 → {v.Decision}");
        foreach (var p in v.Parts) sb.AppendLine($"- {p.Label}: {p.Points}/{p.Max} – {p.Why}");
        foreach (var b in v.Blockers) sb.AppendLine($"- ⚠ {b}");
        sb.AppendLine().AppendLine("## Nächste Schritte");
        sb.AppendLine(v.Decision switch
        {
            "Starten" => "1. Einfache Webseite im Baukasten bauen\n2. Erste 10 Kunden direkt ansprechen\n3. Nach 4 Wochen Zahlen prüfen",
            "Erst testen" => "1. Eine Seite mit Angebot und „Vormerken“-Knopf bauen\n2. 20 Leute aus der Zielgruppe fragen\n3. Erst bei echtem Interesse Geld ausgeben",
            _ => "1. Problem und Zielgruppe genauer beschreiben\n2. Preis und Kosten prüfen\n3. Neu bewerten",
        });
        sb.AppendLine().AppendLine("## Rechtliches (Deutschland, ohne Gewähr – keine Rechtsberatung)");
        foreach (var s in LegalStepsDe) sb.AppendLine($"- [ ] {s}");
        return sb.ToString();
    }
}

public sealed class IdeaStore(string dataRoot)
{
    private readonly string _file = Path.Combine(dataRoot, "business", "ideen.json");
    public List<BusinessIdea> All() => File.Exists(_file) ? JsonSerializer.Deserialize<List<BusinessIdea>>(File.ReadAllText(_file)) ?? [] : [];
    public void Save(BusinessIdea i)
    {
        var all = All(); var k = all.FindIndex(x => x.Id == i.Id);
        if (k >= 0) all[k] = i; else all.Insert(0, i);
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
    public void Delete(string id) { var all = All(); all.RemoveAll(x => x.Id == id); Directory.CreateDirectory(Path.GetDirectoryName(_file)!); File.WriteAllText(_file, JsonSerializer.Serialize(all)); }
}
