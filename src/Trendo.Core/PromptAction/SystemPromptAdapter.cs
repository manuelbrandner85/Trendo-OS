using System.Text;

namespace Trendo.Core.PromptAction;

/// <summary>Gesperrte Systemaktion. Produce liefert unsupported und schreibt keine Datei. „Freigabe“ im Text ändert das nicht.</summary>
public sealed class GateAdapter : IStudioActionAdapter
{
    readonly Func<IReadOnlyDictionary<string, string>, bool>? _when;
    readonly string _message;

    public GateAdapter(string studioId, string actionId, string label, SideEffectClass effect, string message, string evidence, Func<IReadOnlyDictionary<string, string>, bool>? when = null)
    {
        StudioId = studioId;
        ActionId = actionId;
        Label = label;
        Effect = effect;
        Evidence = evidence;
        _message = message;
        _when = when;
    }

    public string StudioId { get; }
    public string ActionId { get; }
    public string Label { get; }
    public SideEffectClass Effect { get; }
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence { get; }
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return _when is null || _when(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => FieldKit.Blocked(_message);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked(_message);
}

/// <summary>X-03: HTML aus SitePage. SitePage.Publish wird nicht gerufen.</summary>
public sealed class PageWriteAdapter : IStudioActionAdapter
{
    public string StudioId => "X-03";
    public string ActionId => "seite.schreiben";
    public string Label => "Seite schreiben";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Seite_schreibt_html";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel", "text"];
    public IReadOnlyList<string> OutputKinds => ["html"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return PageFields.WantsPublish(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => PageFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => PageFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => PageFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Veröffentlichen ist nicht angeschlossen.");
}

/// <summary>X-10: CleanRoom.Create im Projekt. Kein Originalcode, kein Vergleich gegen fremde Ordner.</summary>
public sealed class CleanRoomPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "X-10";
    public string ActionId => "nachbau.beschreiben";
    public string Label => "Nachbau beschreiben";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Nachbau_schreibt_regeln";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "vorbild"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => CleanFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => CleanFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => CleanFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird kein Original gelesen.");
}

/// <summary>X-09: ein Snapshot. Es wird kein Prozess beendet.</summary>
public sealed class ResourcePromptAdapter : IStudioActionAdapter
{
    public string StudioId => "X-09";
    public string ActionId => "leistung.lesen";
    public string Label => "Leistung lesen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Leistung_liest_prozesse";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => ResourceFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird kein Programm beendet.");
}

/// <summary>X-01: nur die Studionamen. Fusion.Plan und jede Ausführung bleiben aus.</summary>
public sealed class StudioListAdapter : IStudioActionAdapter
{
    public string StudioId => "X-01";
    public string ActionId => "studios.nennen";
    public string Label => "Studios nennen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Studios_werden_nur_genannt";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["ziel"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "ziel") ? [] : [new("ziel", "ziel", "Ziel fehlt. Schreib „Ziel: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Text(parameters, "ziel").Length > 200 ? [new("ziel", "Das Ziel ist zu lang.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call) => StudioListFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts ausgeführt.");
}

/// <summary>X-06: Lizenz aus dem Text. OpenSource.Fetch wird nicht gerufen.</summary>
public sealed class RepoExplainAdapter : IStudioActionAdapter
{
    public string StudioId => "X-06";
    public string ActionId => "github.erklaeren";
    public string Label => "Lizenz erklären";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Github_erklaert_lizenz";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "lizenz"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return RepoFields.WantsClone(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => RepoFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => RepoFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => RepoFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Klonen ist nicht angeschlossen.");
}

/// <summary>SYS-01: TripleMode.DetectHere. Es wird nichts umgestellt und nichts beschrieben.</summary>
public sealed class ModePromptAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-01";
    public string ActionId => "modus.zeigen";
    public string Label => "Modus zeigen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Modus_wird_angezeigt";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => ModeFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts umgestellt.");
}

/// <summary>SYS-05: Plan mit StepLabel. WorkflowEngine wird nicht erzeugt und führt nichts aus.</summary>
public sealed class WorkflowPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-05";
    public string ActionId => "ablauf.planen";
    public string Label => "Ablauf planen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Ablauf_plant_und_verschiebt_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "schritt"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return PlanFields.WantsRun(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => PlanFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => PlanFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => PlanFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Der Ablauf ist nicht angeschlossen.");
}

/// <summary>X-04: Textentwurf. ExtensionCatalog.InstallVerified wird nicht gerufen.</summary>
public sealed class ExtensionDraftAdapter : IStudioActionAdapter
{
    public string StudioId => "X-04";
    public string ActionId => "erweiterung.entwerfen";
    public string Label => "Erweiterung entwerfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Erweiterung_ist_nur_entwurf";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "zweck"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return ExtensionFields.WantsInstall(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => ExtensionFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => ExtensionFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => ExtensionFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Die Installation ist nicht angeschlossen.");
}

public static class SystemActions
{
    public const string GateEvidence = "PromptActionTests.Gesperrte_Studios_schreiben_nichts";

    public static IReadOnlyList<IStudioActionAdapter> Create() =>
    [
        new GateAdapter("CORE-09", "kamera.steuern", "Kamera steuern", SideEffectClass.Machine, "Die Kamera ist nicht angeschlossen. Es gibt keine Bewegung.", GateEvidence),
        new GateAdapter("SPEC-07", "text.uebersetzen", "Text übersetzen", SideEffectClass.ExternalAi, "Die Übersetzung ist nicht angeschlossen. Language Studio 2.0 bleibt abgelehnt.", GateEvidence),
        new GateAdapter("SYS-08", "seite.lesen", "Seite lesen", SideEffectClass.Message, "Die Recherche ist nicht angeschlossen. Es wird nichts aus dem Netz gelesen.", GateEvidence),
        new GateAdapter("CORE-02", "ki.fragen", "KI fragen", SideEffectClass.ExternalAi, "Die lokale KI ist nicht angeschlossen. Es gibt keine Antwort.", GateEvidence),
        new GateAdapter("CORE-03", "team.fragen", "Team fragen", SideEffectClass.ExternalAi, "Das KI-Team ist nicht angeschlossen. Es gibt keinen Rat.", GateEvidence),
        new GateAdapter("X-05", "quelle.pruefen", "Quelle prüfen", SideEffectClass.ExternalAi, "Die kostenlose KI ist nicht angeschlossen. Es gibt keine Netzabfrage.", GateEvidence),
        new GateAdapter("CORE-01", "desktop.aendern", "Desktop ändern", SideEffectClass.Security, "Der Desktop ist nicht angeschlossen. Design 41 bleibt.", GateEvidence),
        new GateAdapter("CORE-11", "geheimnis.aendern", "Geheimnis ändern", SideEffectClass.Security, "Ein zweites Freigabesystem ist nicht angeschlossen.", GateEvidence),
        new GateAdapter("SPEC-12", "forensik.aendern", "Forensik ändern", SideEffectClass.Security, "Die Forensik ist nicht angeschlossen. Die 120-Stunden-Regel bleibt.", GateEvidence),
        new GateAdapter("SYS-03", "datei.laden", "Datei laden", SideEffectClass.Message, "Der Download ist nicht angeschlossen. Es wird nichts geladen.", GateEvidence),
        new GateAdapter("SYS-04", "programm.installieren", "Programm installieren", SideEffectClass.Security, "Die Installation ist nicht angeschlossen. Es wird nichts installiert.", GateEvidence),
        new GateAdapter("SYS-06", "datei.teilen", "Datei teilen", SideEffectClass.Publish, "Die Freigabe ist nicht angeschlossen. Es wird nichts geteilt.", GateEvidence),
        new GateAdapter("SYS-07", "geraete.nennen", "Geräte nennen", SideEffectClass.Machine, "Die Geräteabfrage ist nicht angeschlossen. Es gibt keine Liste.", GateEvidence),
        new GateAdapter("SYS-11", "programm.starten", "Programm starten", SideEffectClass.Machine, "Das Programm ist nicht angeschlossen. Es wird nichts gestartet.", GateEvidence),
        new GateAdapter("SYS-12", "darstellung.aendern", "Darstellung ändern", SideEffectClass.Security, "Das Theme ist nicht angeschlossen. Design 41 bleibt.", GateEvidence),
        new GateAdapter("SYS-13", "pruefung.starten", "Prüfung starten", SideEffectClass.None, "Die Prüfung ist nicht angeschlossen. Sie startet nicht aus dem Auftrag.", GateEvidence),
        new GateAdapter("X-02", "reparatur.starten", "Reparatur starten", SideEffectClass.Security, "Die Reparatur ist nicht angeschlossen. Verbesserung 35 bleibt offen.", GateEvidence),
        new GateAdapter("X-08", "handy.verbinden", "Handy verbinden", SideEffectClass.Machine, "Das Handy ist nicht angeschlossen. Es wird keine Verbindung geöffnet.", GateEvidence),
        new GateAdapter("CORE-04", "verbund.oeffnen", "Verbund öffnen", SideEffectClass.Machine, "Der Verbund ist nicht angeschlossen. Es wird nichts gekoppelt.", GateEvidence),
        new GateAdapter("X-03", "seite.veroeffentlichen", "Seite veröffentlichen", SideEffectClass.Publish, "Veröffentlichen ist nicht angeschlossen. Es wurde nichts hochgeladen.", "PromptActionTests.Seite_wird_nicht_veroeffentlicht", PageFields.WantsPublish),
        new PageWriteAdapter(),
        new GateAdapter("X-06", "projekt.klonen", "Projekt klonen", SideEffectClass.Publish, "Klonen ist nicht angeschlossen. Es wurde nichts geholt.", "PromptActionTests.Github_klont_nichts", RepoFields.WantsClone),
        new RepoExplainAdapter(),
        new GateAdapter("SYS-05", "ablauf.starten", "Ablauf starten", SideEffectClass.DeleteOrRights, "Der Ablauf ist nicht angeschlossen. Es wurde nichts verschoben.", "PromptActionTests.Ablauf_startet_nichts", PlanFields.WantsRun),
        new WorkflowPlanAdapter(),
        new GateAdapter("X-04", "erweiterung.installieren", "Erweiterung installieren", SideEffectClass.Security, "Die Installation ist nicht angeschlossen. Es wurde nichts installiert.", "PromptActionTests.Erweiterung_wird_nicht_installiert", ExtensionFields.WantsInstall),
        new ExtensionDraftAdapter(),
        new CleanRoomPromptAdapter(),
        new ResourcePromptAdapter(),
        new StudioListAdapter(),
        new ModePromptAdapter(),
    ];
}

static class PageFields
{
    public static bool WantsPublish(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Yes(parameters, "veroeffentlichen") || FieldKit.Yes(parameters, "publish") || FieldKit.Yes(parameters, "hochladen");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "titel")) list.Add(new("titel", "titel", "Titel fehlt.", true));
        if (!FieldKit.Has(parameters, "text")) list.Add(new("text", "text", "Text fehlt.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Text(parameters, "titel").Length > 120) return [new("titel", "Der Titel ist zu lang.", true)];
        if (FieldKit.Text(parameters, "text").Length > 8000) return [new("text", "Der Text ist zu lang.", true)];
        var color = FieldKit.Text(parameters, "farbe");
        if (color.Length > 0 && !Hex(color)) return [new("farbe", "Farbe ist ein Wert wie #112233.", true)];
        return [];
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var page = Build(call.Parameters);
        var html = page.ToHtml();
        var warnings = new List<string> { "Es wurde nichts veröffentlicht." };
        if (FieldKit.Has(call.Parameters, "link") && SitePage.SafeUrl(FieldKit.Text(call.Parameters, "link")) == "#")
            warnings.Add("Der Link ist unsicher und wird zu #.");
        var qa = new QaResult("seite.html", html.StartsWith("<!doctype html>\n", StringComparison.Ordinal) && html.EndsWith("</html>\n", StringComparison.Ordinal), "Seite gelesen.", null, page.Title, "SitePage");
        return FieldKit.Save(call, FieldKit.Slot("baukasten", call.Version, "seite.html"), html, page.Title + " · HTML im Projekt", qa, warnings);
    }

    public static SitePage Build(IReadOnlyDictionary<string, string> parameters)
    {
        var page = new SitePage { Title = FieldKit.Text(parameters, "titel") };
        var color = FieldKit.Text(parameters, "farbe");
        if (Hex(color)) page.Accent = color;
        page.Blocks.Add(new Block("h1", page.Title));
        page.Blocks.Add(new Block("p", FieldKit.Text(parameters, "text")));
        var list = FieldKit.Text(parameters, "liste");
        if (list.Length > 0) page.Blocks.Add(new Block("list", list));
        var button = FieldKit.Text(parameters, "knopf");
        if (button.Length > 0) page.Blocks.Add(new Block("button", button, FieldKit.Text(parameters, "link")));
        page.Blocks.Add(new Block("p", "Es wurde nichts veröffentlicht."));
        return page;
    }

    static bool Hex(string color) =>
        color.Length == 7 && color[0] == '#' && color[1..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}

static class CleanFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "name")) list.Add(new("name", "name", "Name fehlt.", true));
        if (!FieldKit.Has(parameters, "vorbild")) list.Add(new("vorbild", "vorbild", "Vorbild fehlt. Nur ein Name, kein Ordner.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Text(parameters, "name").Length > 80) return [new("name", "Der Name ist zu lang.", true)];
        if (FieldKit.Text(parameters, "vorbild").Length > 200) return [new("vorbild", "Das Vorbild ist zu lang.", true)];
        return [];
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var name = FieldKit.Text(call.Parameters, "name");
        var original = FieldKit.Text(call.Parameters, "vorbild");
        if (name.Length == 0 || original.Length == 0) return FieldKit.Fail("Name oder Vorbild fehlt.");
        try
        {
            var parent = Path.Combine(call.Projects.ProjectFilesRoot(call.ProjectId), "nachbau", "v" + call.Version);
            Directory.CreateDirectory(parent);
            var dir = CleanRoom.Create(parent, name, original);
            var safe = DevStudio.SafeName(name);
            var rulesRel = "nachbau/v" + call.Version + "/" + safe + "/REGELN.md";
            var descRel = "nachbau/v" + call.Version + "/" + safe + "/1-Beschreibung/BESCHREIBUNG.md";
            var rules = call.Projects.ReadFile(call.ProjectId, rulesRel);
            var desc = call.Projects.ReadFile(call.ProjectId, descRel);
            var ok = rules.Contains("Keine Rechtsberatung.", StringComparison.Ordinal)
                && desc.Contains(name, StringComparison.Ordinal)
                && desc.Contains(original, StringComparison.Ordinal)
                && Path.GetFileName(dir) == safe;
            var qa = new QaResult("nachbau.regeln", ok, ok ? "Regeln gelesen." : "Die Beschreibung stimmt nach dem Lesen nicht.", null, name, "CleanRoom");
            var files = new List<ProducedFile> { new(rulesRel, "md", true), new(descRel, "md", true) };
            return new AdapterOutcome(true, files, [qa], name + " · Beschreibung, keine Rechtsberatung", ["Keine Rechtsberatung."], ok ? [] : ["Die Beschreibung stimmt nach dem Lesen nicht."], null);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or ProjectIsolationException) { return FieldKit.Fail(ex.Message); }
    }
}

static class ResourceFields
{
    public static AdapterOutcome Write(AdapterCall call)
    {
        var snap = ResourceMonitor.Snapshot(8);
        var sb = new StringBuilder();
        sb.AppendLine("Arbeitsspeicher, nur gelesen. Es wird kein Programm beendet.");
        if (snap.Count == 0) sb.AppendLine("Keine Prozesse gelesen.");
        foreach (var proc in snap)
            sb.Append("- ").Append(proc.Name.Replace('\n', ' ').Replace('\r', ' ')).Append(": ").Append(proc.Count).Append(" × ").Append(ResourceMonitor.Size(proc.Bytes)).AppendLine();
        var body = sb.ToString();
        var qa = new QaResult("leistung.snapshot", body.Contains("kein Programm beendet", StringComparison.Ordinal), "Messwerte gelesen.", null, snap.Count.ToString(), "ResourceMonitor");
        var preview = snap.Count + " Prozesse gelesen";
        return FieldKit.Save(call, FieldKit.Slot("leistung", call.Version, "speicher.txt"), body, preview, qa, ["Es wird kein Programm beendet."]);
    }
}

static class StudioListFields
{
    public static AdapterOutcome Write(AdapterCall call)
    {
        var goal = FieldKit.Text(call.Parameters, "ziel");
        if (goal.Length == 0) return FieldKit.Fail("Ziel fehlt.");
        var sb = new StringBuilder();
        sb.Append("# Plan: ").Append(goal).AppendLine();
        sb.AppendLine();
        sb.AppendLine("Es wird nichts ausgeführt. Nur die Namen der Studios.");
        sb.AppendLine();
        foreach (var group in CapabilityCatalog.Profiles.GroupBy(p => p.Area))
        {
            sb.Append("## ").AppendLine(group.Key);
            foreach (var studio in group)
                sb.Append("- ").Append(studio.Title).Append(" (").Append(studio.Id).AppendLine(")");
            sb.AppendLine();
        }
        var body = sb.ToString();
        var count = CapabilityCatalog.Ids.Count;
        var named = CapabilityCatalog.Profiles.All(p => body.Contains(p.Title + " (" + p.Id + ")", StringComparison.Ordinal));
        var qa = new QaResult("studios.namen", named && body.Contains("Es wird nichts ausgeführt.", StringComparison.Ordinal), "Namen gelesen.", null, count.ToString(), "CapabilityCatalog");
        return FieldKit.Save(call, FieldKit.Slot("produktion", call.Version, "plan.md"), body, goal + " · " + count + " Studios genannt", qa, ["Es wird nichts ausgeführt."]);
    }
}

static class RepoFields
{
    public static bool WantsClone(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Yes(parameters, "klonen") || FieldKit.Yes(parameters, "holen") || FieldKit.Yes(parameters, "merge");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "name")) list.Add(new("name", "name", "Projektname fehlt.", true));
        if (!FieldKit.Has(parameters, "lizenz")) list.Add(new("lizenz", "lizenz", "Lizenz fehlt. Zum Beispiel „Lizenz: MIT“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Text(parameters, "name").Length > 120) return [new("name", "Der Name ist zu lang.", true)];
        if (FieldKit.Text(parameters, "lizenz").Length > 40) return [new("lizenz", "Die Lizenz ist zu lang.", true)];
        return [];
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var name = FieldKit.Text(call.Parameters, "name");
        var spdx = FieldKit.Text(call.Parameters, "lizenz");
        if (name.Length == 0 || spdx.Length == 0) return FieldKit.Fail("Name oder Lizenz fehlt.");
        var explain = OpenSource.Explain(spdx);
        var kind = OpenSource.Classify(spdx);
        var body = name + "\n" + explain + "\n" + kind + "\nEs wurde nichts geklont.\n";
        var again = OpenSource.Explain(spdx);
        var qa = new QaResult("github.lizenz", again == explain && OpenSource.Classify(spdx) == kind, "Lizenz gelesen.", null, spdx, "OpenSource");
        return FieldKit.Save(call, FieldKit.Slot("github", call.Version, "hinweis.txt"), body, explain, qa, ["Es wurde nichts geklont."]);
    }
}

static class ModeFields
{
    public static AdapterOutcome Write(AdapterCall call)
    {
        var mode = TripleMode.DetectHere();
        var label = TripleMode.Label(mode);
        var body = "Modus: " + label + "\nEs wird nichts umgestellt.\n";
        var again = TripleMode.Label(TripleMode.DetectHere());
        var qa = new QaResult("modus.label", again == label && body.Contains(label, StringComparison.Ordinal), "Modus gelesen.", null, label, "TripleMode");
        return FieldKit.Save(call, FieldKit.Slot("modus", call.Version, "modus.txt"), body, label, qa, ["Es wird nichts umgestellt."]);
    }
}

static class PlanFields
{
    public static bool WantsRun(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Yes(parameters, "ausfuehren") || FieldKit.Yes(parameters, "starten");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "name")) list.Add(new("name", "name", "Name des Ablaufs fehlt.", true));
        if (!FieldKit.Has(parameters, "schritt")) list.Add(new("schritt", "schritt", "Schritt fehlt. Zum Beispiel „Schritt: Ordner Notizen anlegen“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Text(parameters, "name").Length > 80) return [new("name", "Der Name ist zu lang.", true)];
        var steps = Steps(parameters);
        if (steps.Count == 0) return [new("schritt", "Schritt fehlt.", true)];
        if (steps.Count > 20) return [new("schritt", "Höchstens 20 Schritte.", true)];
        if (steps.Any(s => s.Length > 200)) return [new("schritt", "Ein Schritt ist zu lang.", true)];
        return [];
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var name = FieldKit.Text(call.Parameters, "name");
        var steps = Steps(call.Parameters);
        if (name.Length == 0 || steps.Count == 0) return FieldKit.Fail("Name oder Schritt fehlt.");
        var kinds = steps.Select(KindOf).ToList();
        var sb = new StringBuilder();
        sb.Append("# Ablauf: ").AppendLine(name);
        sb.AppendLine();
        sb.AppendLine("Der Ablauf ist nicht gelaufen.");
        for (var i = 0; i < steps.Count; i++)
            sb.Append(i + 1).Append(". ").Append(steps[i]).Append(" — ").AppendLine(WorkflowEngine.StepLabel(kinds[i]));
        sb.AppendLine();
        if (kinds.Any(k => k is StepKind.Verschieben or StepKind.DatumVoranstellen))
            sb.AppendLine("Verschieben und Umbenennen brauchen die bestehende Freigabe. Dieser Auftrag führt sie nicht aus.");
        sb.AppendLine("Es wurde nichts verschoben und nichts kopiert.");
        var body = sb.ToString();
        var warnings = new List<string> { "Es wurde nichts verschoben und nichts kopiert." };
        if (kinds.Contains(StepKind.KiZusammenfassen)) warnings.Add("Die KI wurde nicht gefragt.");
        var qa = new QaResult("ablauf.plan", body.Contains("nicht gelaufen", StringComparison.Ordinal) && steps.All(s => body.Contains(s, StringComparison.Ordinal)), "Plan gelesen.", null, name, "WorkflowEngine.StepLabel");
        return FieldKit.Save(call, FieldKit.Slot("ablauf", call.Version, "plan.txt"), body, name + " · " + steps.Count + " Schritte, nicht gelaufen", qa, warnings);
    }

    static List<string> Steps(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Text(parameters, "schritt").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    static StepKind KindOf(string line)
    {
        var text = PromptFields.Norm(line);
        if (text.Contains("verschieb", StringComparison.Ordinal)) return StepKind.Verschieben;
        if (text.Contains("datum", StringComparison.Ordinal)) return StepKind.DatumVoranstellen;
        if (text.Contains("kopier", StringComparison.Ordinal)) return StepKind.Kopieren;
        if (text.Contains("zip", StringComparison.Ordinal)) return StepKind.Zippen;
        if (text.Contains("zusammenfass", StringComparison.Ordinal)) return StepKind.KiZusammenfassen;
        if (text.Contains("ordner", StringComparison.Ordinal)) return StepKind.OrdnerAnlegen;
        return StepKind.TextSchreiben;
    }
}

static class ExtensionFields
{
    public static bool WantsInstall(IReadOnlyDictionary<string, string> parameters) => FieldKit.Yes(parameters, "installieren");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "name")) list.Add(new("name", "name", "Name fehlt.", true));
        if (!FieldKit.Has(parameters, "zweck")) list.Add(new("zweck", "zweck", "Zweck fehlt.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Text(parameters, "name").Length > 80) return [new("name", "Der Name ist zu lang.", true)];
        if (FieldKit.Text(parameters, "zweck").Length > 2000) return [new("zweck", "Der Zweck ist zu lang.", true)];
        return [];
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var name = FieldKit.Text(call.Parameters, "name");
        var purpose = FieldKit.Text(call.Parameters, "zweck");
        if (name.Length == 0 || purpose.Length == 0) return FieldKit.Fail("Name oder Zweck fehlt.");
        var body = "# Erweiterung: " + name + "\n\nZweck: " + purpose + "\n\nEs wurde nichts installiert.\n";
        var qa = new QaResult("erweiterung.text", body.Contains(name, StringComparison.Ordinal) && body.Contains("nichts installiert", StringComparison.Ordinal), "Entwurf gelesen.", null, name, "Text");
        return FieldKit.Save(call, FieldKit.Slot("erweiterung", call.Version, "entwurf.md"), body, name + " · nur Entwurf", qa, ["Es wurde nichts installiert."]);
    }
}
