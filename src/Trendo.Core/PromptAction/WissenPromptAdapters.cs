using System.Text;
using Trendo.Core.Wissen;

namespace Trendo.Core.PromptAction;

/// <summary>T-0780 STUDIO-REFERAT-01 „Referate &amp; Hausarbeiten“ (Auftrag außerhalb des Studios): Gliederungs-Gerüst zu einem Thema als Markdown –
/// Einleitung, Hauptteil mit den genannten Punkten, Fazit, Literaturverzeichnis. Den Text schreibt die Person (oder die KI im offenen Dokument); Quellen werden hier nie erfunden.</summary>
public sealed class ReferatGliederungAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-REFERAT-01";
    public string ActionId => "referat.gliederung";
    public string Label => "Gliederung anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "WissenStudiosTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["thema"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "thema") ? [] : [new("thema", "thema", "Worum geht es? Schreib „Thema: …“ (dazu gern „Punkte: A; B; C“ für den Hauptteil).", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Text(parameters, "thema").Length > 300 ? [new("thema", "Das Thema ist zu lang (höchstens 300 Zeichen).", true)] : [];

    public AdapterOutcome Produce(AdapterCall call)
    {
        var topic = FieldKit.Text(call.Parameters, "thema");
        var points = FieldKit.Text(call.Parameters, "punkte", "hauptteil").Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(12).ToList();
        var sb = new StringBuilder($"# {topic}\n\n1 Einleitung\n1.1 Fragestellung\n2 Hauptteil\n");
        for (var i = 0; i < points.Count; i++) sb.Append($"2.{i + 1} {points[i]}\n");
        sb.Append("3 Fazit\n\nLiteraturverzeichnis: entsteht im Studio aus deinen eigenen Quellen.\n");
        var outline = ReferatKit.ParseOutline(sb.ToString().Split('\n', 2)[1]);
        var qa = new QaResult(ActionId, outline.Count >= 3, $"{outline.Count} Gliederungspunkte.", null, topic, "ReferatKit.ParseOutline");
        return FieldKit.Save(call, FieldKit.Slot("referat", call.Version, "gliederung.md"), sb.ToString(), $"Gliederung „{topic}“ mit {outline.Count} Punkten", qa,
            points.Count == 0 ? ["Hauptteil ohne Unterpunkte – „Punkte: …“ ergänzen oder im Studio gliedern."] : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts abgegeben oder hochgeladen.");
}

/// <summary>T-0780 STUDIO-TIMELINE-01 „Zeitleisten“ (Auftrag außerhalb des Studios): Zeilen „Datum | Titel | Spur“ prüfen und als CSV ablegen –
/// dieselbe Datumslogik wie im Studio (auch v. Chr., ungefähre Daten). Unlesbare Zeilen werden genannt, nichts wird geraten.</summary>
public sealed class ZeitleisteZeilenAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-TIMELINE-01";
    public string ActionId => "zeitleiste.zeilen";
    public string Label => "Ereignisse als Tabelle";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "WissenStudiosTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["ereignisse"];
    public IReadOnlyList<string> OutputKinds => ["csv"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var f = FieldKit.Read(prompt, known);
        if (!f.ContainsKey("ereignisse") && prompt.Contains('|')) f["ereignisse"] = string.Join("\n", prompt.Split('\n').Where(l => l.Contains('|')));
        return new(ActionId, f);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "ereignisse") ? [] : [new("ereignisse", "ereignisse", "Welche Ereignisse? Eine Zeile je Eintrag, z. B. „1789 | Französische Revolution“ oder „44 v. Chr. | Tod Caesars“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "ereignisse") && TimelineKit.ParseLines(FieldKit.Text(parameters, "ereignisse")).Items.Count == 0 ? [new("ereignisse", "Keine Zeile hat ein lesbares Datum.", true)] : [];

    public AdapterOutcome Produce(AdapterCall call)
    {
        var (rows, bad) = TimelineKit.ParseLines(FieldKit.Text(call.Parameters, "ereignisse"));
        var t = new Timeline();
        foreach (var (s, e, title, track, text) in rows) t.Items.Add(new(TimelineKit.NewId("e"), title, s, e, TimelineKit.TrackFor(t, track.Length > 0 ? track : "Allgemein"), text));
        var qa = new QaResult(ActionId, rows.Count > 0, $"{rows.Count} Einträge, {bad.Count} unlesbar.", null, $"{rows.Count} Einträge", "TimelineKit.ParseLines");
        return FieldKit.Save(call, FieldKit.Slot("zeitleiste", call.Version, "zeitleiste.csv"), TimelineKit.ToCsv(t), $"{rows.Count} Einträge", qa, bad.Count > 0 ? [.. bad.Take(5)] : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts veröffentlicht.");
}
