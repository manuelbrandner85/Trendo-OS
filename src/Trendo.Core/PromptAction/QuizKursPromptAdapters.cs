using System.Globalization;
using System.Text;
using Trendo.Core.Wissen;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-QUIZ-01 Quiz erstellen (T-0779): Fragen im Fragen-Textformat („? Frage“, „+ richtig“, „- falsch“, „L: …[Lücke]“, „Z: …“ mit „A = B“, „W: …“) prüfen
/// und als eigenständiges HTML-Quiz ins Projekt legen. Veröffentlicht nichts.</summary>
public sealed class QuizAnlegenAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-QUIZ-01";
    public string ActionId => "quiz.anlegen";
    public string Label => "Quiz anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "T0779QuizKurseTests.Quiz_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["fragen"];
    public IReadOnlyList<string> OutputKinds => ["html"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var f = FieldKit.Read(prompt, known);
        if (!FieldKit.Has(f, "fragen") && QuizText.Parse(prompt).Fragen.Count > 0) f["fragen"] = prompt;
        return new(ActionId, f);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "fragen") ? [] : [new("fragen", "fragen", "Schreib die Fragen so: „? Frage“, darunter „+ richtige Antwort“ und „- falsche Antwort“ – je Frage ein Block.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "fragen") && QuizText.Parse(FieldKit.Text(parameters, "fragen")).Fragen.Count == 0) f.Add(new("fragen", "Es ist keine lesbare Frage dabei (z. B. „? Frage“ mit „+ richtig“ und „- falsch“).", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var (fragen, skipped) = QuizText.Parse(FieldKit.Text(p, "fragen"));
        var q = new Quiz { Titel = FieldKit.Has(p, "titel") ? FieldKit.Text(p, "titel").Split('\n')[0].Trim() : "Quiz", Fragen = fragen };
        if (FieldKit.TryNum(p, "zeit", out var z) && z is >= 0 and <= 3600) q.Sekunden = (int)z;
        var check = QuizKit.Pruefen(q);
        var qa = new QaResult(ActionId, check.All(c => !c.Fehler), $"{q.Fragen.Count} Fragen, {q.MaxPunkte.ToString("0.##", CultureInfo.InvariantCulture)} Punkte, {check.Count(c => c.Fehler)} Fehler, {skipped.Count} ausgelassen.", null, null, "Quiz");
        return FieldKit.Save(call, FieldKit.Slot("quiz", call.Version, "quiz.html"), QuizHtml.Html(q), $"{q.Fragen.Count} Fragen", qa, [.. skipped.Select(s => "Ausgelassen: " + s)]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Das Quiz gibst du selbst weiter – Trendo veröffentlicht nichts.");
}

/// <summary>STUDIO-COURSE-01 Kurse erstellen (T-0779): Gliederung („# Kapitel“, „## Lektion“, Text darunter) zu einem Kurs als Webseite ins Projekt. Lädt nichts auf eine Lernplattform.</summary>
public sealed class KursGliederungAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-COURSE-01";
    public string ActionId => "kurs.gliederung";
    public string Label => "Kurs aus Gliederung";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "T0779QuizKurseTests.Kurs_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel", "inhalt"];
    public IReadOnlyList<string> OutputKinds => ["html"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var f = FieldKit.Read(prompt, known);
        if (!FieldKit.Has(f, "inhalt") && prompt.Contains("## ", StringComparison.Ordinal)) f["inhalt"] = string.Join("\n", prompt.Replace("\r", "").Split('\n').Where(l => !l.TrimStart().StartsWith("Titel:", StringComparison.OrdinalIgnoreCase)));
        return new(ActionId, f);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var m = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "titel")) m.Add(new("titel", "titel", "Wie heißt der Kurs? Schreib z. B. „Titel: Erste Hilfe am Hund“.", true));
        if (!FieldKit.Has(parameters, "inhalt")) m.Add(new("inhalt", "inhalt", "Schreib die Gliederung: „# Kapitel“, „## Lektion“ und darunter den Lerntext.", true));
        return m;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var k = KursKit.AusGliederung(FieldKit.Text(p, "titel").Split('\n')[0], FieldKit.Text(p, "inhalt"));
        var check = KursKit.Pruefen(k); var n = KursKit.Lektionen(k).Count;
        var qa = new QaResult(ActionId, check.All(c => !c.Fehler), $"{k.Kapitel.Count} Kapitel, {n} Lektionen, {check.Count(c => c.Fehler)} Fehler.", null, null, "Kurse");
        return FieldKit.Save(call, FieldKit.Slot("kurs", call.Version, "kurs.html"), KursHtml.Html(k), $"{k.Kapitel.Count} Kapitel, {n} Lektionen", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Auf eine Lernplattform lädst du den Kurs selbst hoch (SCORM-Paket aus dem Studio) – Trendo veröffentlicht nichts.");
}
