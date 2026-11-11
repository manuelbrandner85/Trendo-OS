using Trendo.Core.Kreativ;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-SUMMARY-01: Zusammenfassung ohne Internet. Sendet nichts.</summary>
public sealed class SummaryPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-SUMMARY-01";
    public string ActionId => "zusammenfassung.schreiben";
    public string Label => "Text zusammenfassen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKitsTests.Zusammenfassung";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["text"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "text") ? [] : [new("text", "text", "Welchen Text soll ich zusammenfassen? Schreib „Text: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        SummaryKit.Sentences(FieldKit.Text(parameters, "text")).Count < 2 ? [new("text", "Der Text braucht mindestens zwei Sätze.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var text = FieldKit.Text(call.Parameters, "text");
        var s = SummaryKit.Summarize(text, SummaryKit.CountFor(FieldKit.Text(call.Parameters, "länge", "laenge")));
        var body = SummaryKit.Markdown(FieldKit.Text(call.Parameters, "titel"), s);
        var qa = new QaResult(ActionId, true, $"{s.Sentences.Count} Kernsätze.", null, string.Join(", ", s.Keywords), "SummaryKit");
        return FieldKit.Save(call, FieldKit.Slot("zusammenfassung", call.Version, "zusammenfassung.md"), body, s.Sentences.FirstOrDefault() ?? "", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts gesendet.");
}

/// <summary>STUDIO-LOGO-01: Logo als SVG mit Kontrastprüfung.</summary>
public sealed class LogoPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-LOGO-01";
    public string ActionId => "logo.entwerfen";
    public string Label => "Logo entwerfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKitsTests.Logo";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name"];
    public IReadOnlyList<string> OutputKinds => ["svg"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "name") ? [] : [new("name", "name", "Wie heißt die Marke? Schreib „Name: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        foreach (var k in new[] { "farbe", "akzent" })
            if (FieldKit.Has(parameters, k) && !LogoKit.IsHex(FieldKit.Text(parameters, k))) f.Add(new(k, "Farbe als #RRGGBB angeben, z. B. #1E3A8A.", true));
        return f;
    }
    public static LogoKit.Spec SpecFrom(IReadOnlyDictionary<string, string> p) => new(
        FieldKit.Text(p, "name"), FieldKit.Text(p, "slogan", "untertitel"),
        FieldKit.Has(p, "farbe") ? FieldKit.Text(p, "farbe") : "#1E3A8A", FieldKit.Has(p, "akzent") ? FieldKit.Text(p, "akzent") : "#D4A017",
        FieldKit.Has(p, "form") ? FieldKit.Text(p, "form").ToLowerInvariant() : "kreis", LogoKit.Fonts[0]);
    public AdapterOutcome Produce(AdapterCall call)
    {
        var spec = SpecFrom(call.Parameters);
        var svg = LogoKit.Svg(spec);
        var c = LogoKit.Contrast(spec.Primary, LogoKit.TextOn(spec.Primary));
        var qa = new QaResult(ActionId, c >= 4.5, $"Kontrast Schrift auf Hauptfarbe {c}:1.", null, spec.Name, "LogoKit");
        return FieldKit.Save(call, FieldKit.Slot("logo", call.Version, "logo.svg"), svg, spec.Name, qa,
            c < 4.5 ? ["Der Kontrast liegt unter 4,5:1. Für kleine Größen eine dunklere Hauptfarbe wählen."] : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Eine Markenanmeldung macht Trendo nicht.");
}

/// <summary>STUDIO-PODCAST-01: Shownotes und Kapitel. Veröffentlicht nichts.</summary>
public sealed class PodcastPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-PODCAST-01";
    public string ActionId => "podcast.shownotes";
    public string Label => "Shownotes und Kapitel";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKitsTests.Podcast";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel", "kapitel"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "titel")) q.Add(new("titel", "titel", "Wie heißt die Folge? Schreib „Titel: …“.", true));
        if (!FieldKit.Has(parameters, "kapitel")) q.Add(new("kapitel", "kapitel", "Welche Kapitel? Schreib z. B. „Kapitel: 00:00 Start; 04:10 Thema“.", true));
        return q;
    }
    static string Lines(IReadOnlyDictionary<string, string> p) => FieldKit.Text(p, "kapitel").Replace(';', '\n');
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!FieldKit.Has(parameters, "kapitel")) return [];
        var (ch, problems) = PodcastKit.ParseChapters(Lines(parameters));
        return ch.Count == 0 ? [new("kapitel", problems.FirstOrDefault() ?? "Keine Kapitel erkannt.", true)] : [];
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var (ch, problems) = PodcastKit.ParseChapters(Lines(call.Parameters));
        var title = FieldKit.Text(call.Parameters, "titel");
        var body = PodcastKit.ShowNotes(title, FieldKit.Text(call.Parameters, "beschreibung"), ch);
        var qa = new QaResult(ActionId, problems.Count == 0, $"{ch.Count} Kapitel.", null, title, "PodcastKit");
        return FieldKit.Save(call, FieldKit.Slot("podcast", call.Version, "shownotes.md"), body, title, qa, problems.Count > 0 ? problems : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Veröffentlichen machst du selbst bei deinem Podcast-Anbieter.");
}

/// <summary>STUDIO-COMIC-01: Comicseite als SVG aus einer kurzen Geschichte.</summary>
public sealed class ComicPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-COMIC-01";
    public string ActionId => "comic.seite";
    public string Label => "Comicseite anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKitsTests.Comic";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["geschichte"];
    public IReadOnlyList<string> OutputKinds => ["svg"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "geschichte") ? [] : [new("geschichte", "geschichte", "Was passiert? Schreib „Geschichte: Bild 1: … Bild 2: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var parts = ComicKit.SplitStory(FieldKit.Text(call.Parameters, "geschichte"));
        var layout = parts.Count <= 3 ? "3-reihen" : parts.Count <= 4 ? "2x2" : "6er";
        var panels = parts.Select(p => new ComicKit.Panel(p, "", "")).ToList();
        var title = FieldKit.Text(call.Parameters, "titel");
        var svg = ComicKit.Svg(layout, panels, title);
        var count = ComicKit.Boxes(layout).Count;
        var qa = new QaResult(ActionId, parts.Count <= count, $"{Math.Min(parts.Count, count)} von {parts.Count} Bildern auf der Seite.", null, title, "ComicKit");
        return FieldKit.Save(call, FieldKit.Slot("comic", call.Version, "seite.svg"), svg, title, qa,
            parts.Count > count ? [$"Nur {count} Bilder passen auf eine Seite, der Rest kommt auf die nächste."] : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts veröffentlicht.");
}
