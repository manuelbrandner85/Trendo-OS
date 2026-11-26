using Trendo.Core.Wissen;

namespace Trendo.Core.PromptAction;

/// <summary>T-0781 STUDIO-MATH-01: Aufgabe mit Lösungsweg und Probe als Markdown – gerechnet vom eigenen Kern (<see cref="MatheKern"/>), nie von der KI.</summary>
public sealed class MatheLoesungAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-MATH-01";
    public string ActionId => "mathe.loesen";
    public string Label => "Aufgabe mit Lösungsweg";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "MatheKernTests, WissenAdapterTests.Mathe";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["aufgabe"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "aufgabe") ? [] : [new("aufgabe", "aufgabe", "Welche Aufgabe? Schreib „Aufgabe: 3x + 7 = 22“ (dazu optional „Art: gleichung“).", true)];
    static MatheAuftrag Auftrag(IReadOnlyDictionary<string, string> p) =>
        new(MatheKern.ArtVonId.GetValueOrDefault(FieldKit.Text(p, "art").ToLowerInvariant(), MatheArt.Automatisch), FieldKit.Text(p, "aufgabe"), FieldKit.Text(p, "variable") is { Length: > 0 } v ? v : null,
            FieldKit.Text(p, "von") is { Length: > 0 } a ? a : null, FieldKit.Text(p, "bis") is { Length: > 0 } b ? b : null);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!FieldKit.Has(parameters, "aufgabe")) return [];
        try { MatheKern.Rechne(Auftrag(parameters)); return []; }
        catch (Exception e) when (e is FormatException or ArgumentException or DivideByZeroException or OverflowException) { return [new("aufgabe", e.Message, true)]; }
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var l = MatheKern.Rechne(Auftrag(call.Parameters));
        var md = Rechenblatt.Markdown("Lösungsweg", [BlattEintrag.Aus(l)]);
        var qa = new QaResult(ActionId, l.ProbeOk, l.ProbeOk ? "Probe bestanden." : "Probe NICHT bestanden.", null, l.Eingabe, "MatheKern");
        return FieldKit.Save(call, FieldKit.Slot("mathe", call.Version, "loesungsweg.md"), md, l.Ergebnis, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es gibt hier keine Wirkung nach außen.");
}

/// <summary>T-0781 STUDIO-SCIENCE-01: Molmasse, Reaktion ausgleichen oder Rechnung mit Einheiten als Markdown mit Rechenweg und Probe.</summary>
public sealed class NaturRechnungAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-SCIENCE-01";
    public string ActionId => "natur.rechnen";
    public string Label => "Molmasse, Reaktion oder Einheiten rechnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "NaturKernTests, WissenAdapterTests.Natur";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["art", "eingabe"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    static readonly string[] Arten = ["molmasse", "reaktion", "einheiten"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "art")) q.Add(new("art", "art", "Was soll gerechnet werden? „Art: molmasse“, „Art: reaktion“ oder „Art: einheiten“.", true));
        if (!FieldKit.Has(parameters, "eingabe")) q.Add(new("eingabe", "eingabe", "Was genau? z. B. „Eingabe: H2SO4“, „Eingabe: Fe + O2 -> Fe2O3“ oder „Eingabe: 100 km/h in m/s“.", true));
        return q;
    }
    static BlattEintrag Rechne(IReadOnlyDictionary<string, string> p)
    {
        var art = FieldKit.Text(p, "art").ToLowerInvariant(); var e = FieldKit.Text(p, "eingabe");
        switch (art)
        {
            case "molmasse": { var m = Chemie.Molmasse(e); var ok = Math.Abs(m.Zeilen.Sum(z => z.Anteil) - 1) < 1e-9; return new("Molmasse", "Molare Masse von " + Chemie.Schoen(m.Formel.Text), Chemie.MolmasseWeg(m), $"M = {Chemie.G(m.Summe)} g/mol", "", [new Probe(ok, (ok ? "✓" : "✗") + " Massenanteile ergeben zusammen 100 %.")]); }
            case "reaktion": { var r = Chemie.Ausgleichen(e); return new("Reaktion ausgleichen", "Gleiche aus: " + e, r.Schritte, r.Gleichung, "", r.Proben); }
            case "einheiten": { var r = Einheiten.Ausrechnen(e); return new("Einheiten", "Rechne: " + e, r.Schritte, r.Text, "", r.Proben); }
            default: throw new ArgumentException("Art: molmasse, reaktion oder einheiten.");
        }
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!FieldKit.Has(parameters, "art") || !FieldKit.Has(parameters, "eingabe")) return [];
        if (!Arten.Contains(FieldKit.Text(parameters, "art").ToLowerInvariant())) return [new("art", "Art: molmasse, reaktion oder einheiten.", true)];
        try { Rechne(parameters); return []; }
        catch (Exception e) when (e is FormatException or ArgumentException or DivideByZeroException or OverflowException) { return [new("eingabe", e.Message, true)]; }
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var b = Rechne(call.Parameters);
        var ok = b.Proben.All(p => p.Ok);
        var qa = new QaResult(ActionId, ok, ok ? "Probe bestanden." : "Probe NICHT bestanden.", null, FieldKit.Text(call.Parameters, "eingabe"), "Chemie/Einheiten");
        return FieldKit.Save(call, FieldKit.Slot("naturwissenschaft", call.Version, "rechnung.md"), Rechenblatt.Markdown("Rechnung", [b]), b.Ergebnis, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es gibt hier keine Wirkung nach außen.");
}
