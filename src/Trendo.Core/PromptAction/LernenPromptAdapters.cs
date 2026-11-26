using System.Text;
using Trendo.Core.Lernen;

namespace Trendo.Core.PromptAction;

/// <summary>T-0782 STUDIO-TUTOR-01: Arbeitsblatt eines Themas (Aufgaben mit Lösungen am Ende) als Markdown – ohne KI, ohne Netz.</summary>
public sealed class TutorSheetAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-TUTOR-01";
    public string ActionId => "arbeitsblatt.erstellen";
    public string Label => "Arbeitsblatt erstellen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "T0782_Nachhilfe";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["thema"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    static Nachhilfe.Thema? Find(string s) => Nachhilfe.Themen.FirstOrDefault(t => t.Id == s || string.Equals(t.Titel, s, StringComparison.OrdinalIgnoreCase))
        ?? Nachhilfe.Themen.FirstOrDefault(t => s.Length >= 4 && t.Titel.Contains(s, StringComparison.OrdinalIgnoreCase));
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var f = FieldKit.Read(prompt, known);
        if (!FieldKit.Has(f, "thema") && Nachhilfe.Themen.FirstOrDefault(t => prompt.Contains(t.Titel, StringComparison.OrdinalIgnoreCase)) is { } hit) f["thema"] = hit.Id;
        return new(ActionId, f);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> p) =>
        FieldKit.Has(p, "thema") ? [] : [new("thema", "thema", "Zu welchem Thema? Schreib z. B. „Thema: Brüche kürzen“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> p) =>
        FieldKit.Has(p, "thema") && Find(FieldKit.Text(p, "thema")) is null ? [new("thema", "Dieses Thema gibt es nicht: " + string.Join(", ", Nachhilfe.Themen.Select(t => t.Titel)), true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var t = Find(FieldKit.Text(call.Parameters, "thema"))!;
        var d = new Programs.ProgramDocument("STUDIO-TUTOR-01", t.Titel, Nachhilfe.Neu(t.Fach, t.VonKlasse));
        d.Data["thema"] = t.Id;
        var n = int.TryParse(FieldKit.Text(call.Parameters, "anzahl"), out var k) ? Math.Clamp(k, 1, 30) : 8;
        var sheet = Nachhilfe.Arbeitsblatt(d, n, call.Version);
        var sb = new StringBuilder();
        foreach (var b in sheet.Blocks.OfType<Office.Para>()) sb.Append(b.Style switch { Office.ParaStyle.Title => "# ", Office.ParaStyle.Heading1 => "## ", _ => "" }).Append(b.Text).Append("\n\n");
        var qa = new QaResult(ActionId, true, $"{n} Aufgaben mit Lösungen.", null, t.Titel, "Nachhilfe");
        return FieldKit.Save(call, FieldKit.Slot("nachhilfe", call.Version, "arbeitsblatt.md"), sb.ToString(), t.Titel, qa, null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts gesendet oder veröffentlicht.");
}

/// <summary>T-0782 STUDIO-CODELEARN-01: Aufgabe des Lernpfads als Python-Datei (Startcode und Tests). Ausgeführt wird hier nichts – die Tests laufen nur im Studio abgeschottet.</summary>
public sealed class CodeLearnTaskAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-CODELEARN-01";
    public string ActionId => "aufgabe.datei";
    public string Label => "Aufgabe als Datei";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "T0782_CodeLernen";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["py"];
    static CodeLernen.Aufgabe Find(string s) => CodeLernen.Track.FirstOrDefault(a => a.Id == s || string.Equals(a.Titel, s, StringComparison.OrdinalIgnoreCase)) ?? CodeLernen.Track[0];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var f = FieldKit.Read(prompt, known);
        if (!FieldKit.Has(f, "aufgabe") && CodeLernen.Track.FirstOrDefault(a => prompt.Contains(a.Titel, StringComparison.OrdinalIgnoreCase)) is { } hit) f["aufgabe"] = hit.Id;
        return new(ActionId, f);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> p) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> p) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var a = Find(FieldKit.Text(call.Parameters, "aufgabe"));
        var d = new Programs.ProgramDocument("STUDIO-CODELEARN-01", a.Titel, CodeLernen.Neu());
        d.Data["aufgabe"] = a.Id;
        var qa = new QaResult(ActionId, true, $"Aufgabe „{a.Titel}“ mit {a.Faelle.Count} Tests.", null, a.Titel, "CodeLernen");
        return FieldKit.Save(call, FieldKit.Slot("programmieren-lernen", call.Version, a.Id + ".py"), CodeLernen.Export(d), a.Titel, qa, null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts ausgeführt, gesendet oder veröffentlicht.");
}
