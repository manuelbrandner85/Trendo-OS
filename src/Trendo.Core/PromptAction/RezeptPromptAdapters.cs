using Trendo.Core.Wissen;

namespace Trendo.Core.PromptAction;

/// <summary>T-0787 STUDIO-RECIPE-01: Rezept aus „Name: …“, „Zutaten: …; …“ und „Schritte: …“ als schema.org-Rezept (JSON-LD) mit Nährwert-Richtwerten – ohne KI, ohne Netz.</summary>
public sealed class RezeptJsonLdAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-RECIPE-01";
    public string ActionId => "rezept.jsonld";
    public string Label => "Rezept als schema.org-Datei";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "T0787_Rezepte";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "zutaten"];
    public IReadOnlyList<string> OutputKinds => ["json"];

    static List<string> Teile(string s) => [.. s.Replace("\r", "").Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0)];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> p)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(p, "name")) q.Add(new("name", "name", "Wie heißt das Gericht? Schreib z. B. „Name: Pfannkuchen“.", true));
        if (!FieldKit.Has(p, "zutaten")) q.Add(new("zutaten", "zutaten", "Welche Zutaten? Schreib z. B. „Zutaten: 250 g Mehl; 500 ml Milch; 3 Eier“.", true));
        return q;
    }

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> p)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(p, "portionen") && !(int.TryParse(FieldKit.Text(p, "portionen"), out var n) && n is > 0 and <= RezeptKit.MaxPortionen)) f.Add(new("portionen", $"Portionen: eine ganze Zahl von 1 bis {RezeptKit.MaxPortionen}.", true));
        if (RezeptKit.Gesundheitsversprechen(FieldKit.Text(p, "name") + " " + FieldKit.Text(p, "schritte")) is { Count: > 0 } v) f.Add(new("name", $"Bitte ohne Gesundheitsversprechen („{v[0]}“).", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var r = new Rezept(RezeptKit.NewId("r"), FieldKit.Text(p, "name"), int.TryParse(FieldKit.Text(p, "portionen"), out var n) ? n : 4,
            [.. Teile(FieldKit.Text(p, "zutaten")).Take(RezeptKit.MaxZutaten).Select(RezeptKit.LiesZutat)], [.. Teile(FieldKit.Text(p, "schritte")).Take(RezeptKit.MaxSchritte)]);
        var nw = RezeptKit.JePortion(r);
        var qa = new QaResult(ActionId, true, $"{r.Zutaten.Count} Zutaten, {r.Schritte.Count} Schritte" + (nw.Erfasst > 0 ? $", ≈ {Math.Round(nw.Kcal)} kcal je Portion (Richtwert)." : "."), null, r.Name, "RezeptKit");
        var warn = nw.Fehlt.Count > 0 ? new List<string> { "Ohne Nährwerte: " + string.Join(", ", nw.Fehlt) } : null;
        return FieldKit.Save(call, FieldKit.Slot("rezepte", call.Version, "rezept.json"), RezeptKit.JsonLd([r]), r.Name, qa, warn);
    }

    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts gesendet oder veröffentlicht.");
}
