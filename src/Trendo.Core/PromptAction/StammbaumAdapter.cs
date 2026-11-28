using Trendo.Core.Wissen;

namespace Trendo.Core.PromptAction;

/// <summary>T-0788 STUDIO-FAMILY-01 „Stammbaum“ (Auftrag außerhalb des Studios): Zeilen „Name | Geschlecht | Geburt | Geburtsort | Tod | Sterbeort | Vater | Mutter“
/// (und „Ehe: A + B | Datum | Ort“) prüfen und als GEDCOM 5.5.1 ablegen – dieselbe Datums- und Prüflogik wie im Studio. Lebende Personen sind ausgeblendet;
/// unlesbare Zeilen werden genannt, nichts wird geraten.</summary>
public sealed class StammbaumGedcomAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-FAMILY-01";
    public string ActionId => "stammbaum.gedcom";
    public string Label => "Personen als GEDCOM";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "T0788StammbaumTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["personen"];
    public IReadOnlyList<string> OutputKinds => ["ged"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var f = FieldKit.Read(prompt, known);
        if (!f.ContainsKey("personen") && prompt.Contains('|')) f["personen"] = string.Join("\n", prompt.Split('\n').Where(l => l.Contains('|')));
        return new(ActionId, f);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "personen") ? [] : [new("personen", "personen", "Welche Personen? Eine Zeile je Person, z. B. „Erika Muster | w | 12.03.1950 | Musterstadt | | | Max Muster | Anna Muster“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "personen") && FamilyKit.ParseLines(FieldKit.Text(parameters, "personen")) is var (ps, ms, _) && ps.Count + ms.Count == 0 ? [new("personen", "Keine Zeile ist lesbar.", true)] : [];

    public AdapterOutcome Produce(AdapterCall call)
    {
        var (ps, ms, bad) = FamilyKit.ParseLines(FieldKit.Text(call.Parameters, "personen"));
        var t = new FamilyTree();
        FamilyKit.AddLines(t, ps, ms);
        var issues = FamilyKit.Check(t);
        var ged = FamilyKit.ToGedcom(t);
        var qa = new QaResult(ActionId, t.Persons.Count > 0 && issues.All(i => !i.Error), $"{t.Persons.Count} Personen, {t.Families.Count} Familien, {issues.Count(i => i.Error)} Widersprüche, {bad.Count} unlesbare Zeilen.", null, $"{t.Persons.Count} Personen", "FamilyKit.Check");
        var notes = bad.Take(5).Select(b => "Nicht lesbar: " + b).Concat(issues.Where(i => i.Error).Take(5).Select(i => i.Text)).ToList();
        return FieldKit.Save(call, FieldKit.Slot("stammbaum", call.Version, "stammbaum.ged"), ged, $"{t.Persons.Count} Personen als GEDCOM", qa, notes.Count > 0 ? notes : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts hochgeladen oder mit Online-Stammbäumen abgeglichen.");
}
