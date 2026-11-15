using System.Globalization;
using Trendo.Core.Office;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-DECKDESIGN-01 Präsentationen gestalten: aus einer Gliederung eine gestaltete PPTX (vorhandener Präsentationskern) und einen Folienplan
/// mit Folienart, Redezeit und Sprechernotizen. Nichts wird gesendet oder veröffentlicht.</summary>
public sealed class DeckDesignAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-DECKDESIGN-01";
    public string ActionId => "praesentation.gestalten";
    public string Label => "Präsentation gestalten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "DeckDesignTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["gliederung"];
    public IReadOnlyList<string> OutputKinds => ["pptx", "txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "gliederung") ? [] : [new("gliederung", "gliederung", "Was soll auf die Folien? Schreib z. B. „Gliederung:“ und darunter je Folie eine Zeile, Punkte mit „- “.", true)];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "design") && !DeckDesign.Themes.Any(t => t.Id == FieldKit.Text(parameters, "design").ToLowerInvariant()))
            f.Add(new("design", "Design: " + string.Join(", ", DeckDesign.Themes.Select(t => t.Id)) + ".", true));
        if (FieldKit.Has(parameters, "minuten") && (!FieldKit.TryNum(parameters, "minuten", out var m) || m is <= 0 or > 240)) f.Add(new("minuten", "Redezeit in Minuten: 1 bis 240.", true));
        if (FieldKit.Has(parameters, "punkte") && (!int.TryParse(FieldKit.Text(parameters, "punkte"), out var p) || p is < 2 or > 12)) f.Add(new("punkte", "Höchstens-Punkte je Folie: 2 bis 12.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var theme = DeckDesign.Theme(FieldKit.Has(p, "design") ? FieldKit.Text(p, "design").ToLowerInvariant() : "gold");
        var minutes = FieldKit.TryNum(p, "minuten", out var m) ? m : 0;
        var max = int.TryParse(FieldKit.Text(p, "punkte"), out var mp) ? mp : 6;
        DeckResult r;
        try { r = DeckDesign.Build(FieldKit.Text(p, "gliederung"), theme, new DeckOptions(max, 90, minutes)); }
        catch (InvalidOperationException e) { return FieldKit.Fail(e.Message); }
        var pptxRel = FieldKit.Slot("praesentation", call.Version, "praesentation.pptx");
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-deck-" + Guid.NewGuid().ToString("N") + ".pptx");
        try { SlideFiles.WritePptx(r.Doc, tmp); call.Projects.WriteBytes(call.ProjectId, pptxRel, File.ReadAllBytes(tmp)); }
        finally { try { File.Delete(tmp); } catch (IOException) { } }
        var back = SlideFiles.ReadPptx(call.Projects.ResolveInProject(call.ProjectId, pptxRel));
        var ok = back.Slides.Count == r.Doc.Slides.Count && DeckDesign.CheckTheme(theme).Count == 0;
        var qa = new QaResult(ActionId, ok, string.Create(CultureInfo.InvariantCulture, $"{r.Doc.Slides.Count} Folien, {r.Minutes.Sum():0.#} min, Kontrast geprüft."), "min", theme.Id, "DeckDesign");
        var plan = FieldKit.Save(call, FieldKit.Slot("praesentation", call.Version, "folienplan.txt"), DeckDesign.PlanText(r), $"{r.Doc.Slides.Count} Folien, Design „{theme.Name}“", qa);
        return plan with { Files = [.. plan.Files, new ProducedFile(pptxRel, "pptx", true)], Warnings = [.. r.Findings.Select(x => x.Message)] };
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Die Präsentation bleibt im Projekt – gesendet oder veröffentlicht wird nichts.");
}

/// <summary>STUDIO-DOCSCAN-01 Dokumente scannen: Scan-Auftrag mit Dateiname aus Datum und Titel, Ablage-Ordner, Modus und Schritten.
/// Die Bilder selbst werden im Studio entzerrt; die KI liest keine Dateien von der Festplatte.</summary>
public sealed class DocScanAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-DOCSCAN-01";
    public string ActionId => "scan.auftrag";
    public string Label => "Scan-Auftrag anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "DocScanTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "titel") ? [] : [new("titel", "titel", "Was scannst du? Schreib z. B. „Titel: Rechnung Stadtwerke“.", true)];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "modus") && !DocScan.Modes.Any(m => m.Id == FieldKit.Text(parameters, "modus").ToLowerInvariant())) f.Add(new("modus", "Modus: dokument, grau, foto, whiteboard oder original.", true));
        if (FieldKit.Has(parameters, "datum") && !DateTime.TryParse(FieldKit.Text(parameters, "datum"), CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out _)) f.Add(new("datum", "Datum z. B. 15.11.2026.", true));
        if (FieldKit.Has(parameters, "seiten") && (!int.TryParse(FieldKit.Text(parameters, "seiten"), out var n) || n is < 1 or > 500)) f.Add(new("seiten", "Seiten: 1 bis 500.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var title = FieldKit.Text(p, "titel");
        var date = FieldKit.Has(p, "datum") && DateTime.TryParse(FieldKit.Text(p, "datum"), CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var dt) ? dt : DateTime.Today;
        var mode = FieldKit.Has(p, "modus") ? FieldKit.Text(p, "modus").ToLowerInvariant() : "dokument";
        var pages = int.TryParse(FieldKit.Text(p, "seiten"), out var n) ? n : 0;
        var body = DocScan.Plan(date, title, mode, pages, FieldKit.Text(p, "format") == "auto" ? "auto" : "a4");
        var qa = new QaResult(ActionId, body.Contains(DocScan.FileName(date, title), StringComparison.Ordinal), "Dateiname und Ordner bestimmt.", null, mode, "DocScan");
        return FieldKit.Save(call, FieldKit.Slot("scan", call.Version, "scan-auftrag.txt"), body, DocScan.FileName(date, title), qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gescannt wird im Studio aus deinen Fotos – ein Scanner wird nicht angesteuert.");
}
