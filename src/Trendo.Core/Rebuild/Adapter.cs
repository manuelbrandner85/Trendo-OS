using System.Text;
using Trendo.Core.PromptAction;

namespace Trendo.Core.Rebuild;

/// <summary>X-10 über den gemeinsamen Auftragsablauf. „Los“ baut, sonst entsteht nur der Plan.</summary>
public sealed class RebuildAdapter : IStudioActionAdapter
{
    private readonly Func<RebuildPorts> _ports;

    public RebuildAdapter(Func<RebuildPorts>? ports = null) =>
        _ports = ports ?? (() => new RebuildPorts());

    public string StudioId => "X-10";
    public string ActionId => "nachbauen";
    public string Label => "Eigene App planen und bauen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "RebuildTests.Notiz_wird_gebaut";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["beschreibung"];
    public IReadOnlyList<string> OutputKinds => ["Avalonia-Projekt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var text = (prompt ?? "").Trim();
        if (text.Length == 0 && known.TryGetValue("beschreibung", out var fromField)) text = fromField.Trim();
        if (text.Length == 0) return null;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in known) map[kv.Key] = kv.Value;
        if (!map.TryGetValue("beschreibung", out var described) || string.IsNullOrWhiteSpace(described))
            map["beschreibung"] = text;
        return new ActionDraft(ActionId, map);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("beschreibung", out var text) && !string.IsNullOrWhiteSpace(text)
            ? []
            : [new ClarificationQuestion("beschreibung", "beschreibung", "Beschreibe das Programm in einem Satz.", true)];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];

    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Diese Aktion hat keine Wirkung nach außen."], null);

    public AdapterOutcome Produce(AdapterCall call)
    {
        var prompt = call.Parameters.TryGetValue("beschreibung", out var text) ? text.Trim() : "";
        if (prompt.Length == 0)
            return new(true, [], [new QaResult("plan", false, "Die Beschreibung fehlt.", null, null, null)], null, [], ["Die Beschreibung fehlt."], null);
        var root = call.Projects.ResolveInProject(call.ProjectId, "nachbau");
        Directory.CreateDirectory(root);
        var ports = _ports();
        var research = On(call.Parameters, "recherche");
        var los = On(call.Parameters, "los");
        var job = RebuildEngine.TryLoad(root);
        if (job is not null && Revisions.IsChange(prompt))
            job = RebuildEngine.Revise(job, prompt, ports, rebuild: false);
        else if (job is null || !string.Equals(job.Prompt, prompt, StringComparison.Ordinal) || research)
            job = RebuildEngine.Plan(root, prompt, research, ports);
        if (call.Parameters.TryGetValue("funktionen", out var list) && list.Trim().Length > 0)
        {
            var ids = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ticks = job.Blueprint.Features.ToDictionary(f => f.Id, f => ids.Contains(f.Id), StringComparer.OrdinalIgnoreCase);
            job = RebuildEngine.ApplyTicks(job, ticks);
        }
        if (los) job = RebuildEngine.Build(job, ports);
        var files = new List<ProducedFile>();
        var blueprint = Path.Combine(root, "blueprint.json");
        if (File.Exists(blueprint)) files.Add(new ProducedFile("nachbau/blueprint.json", "json", true));
        var readme = Path.Combine(ProjectWriter.AppDir(root), "README.md");
        if (File.Exists(readme) && new FileInfo(readme).Length <= 20 * 1024 * 1024)
            files.Add(new ProducedFile("nachbau/2-Umsetzung/app/README.md", "md", true));
        var preview = Preview(job);
        if (!los)
            return new(true, files, [new QaResult("plan", File.Exists(blueprint), File.Exists(blueprint) ? "Der Plan steht." : "Der Plan fehlt.", null, null, null)], preview,
                ["Die App ist noch nicht gebaut. Auf der Seite Programme nachbauen die Punkte ankreuzen und Los drücken."], [], null);
        if (!job.Built)
        {
            var detail = job.Log.LastOrDefault(l => l.Length > 0) ?? "Der Bau ist nicht gelungen.";
            return new(true, files, [new QaResult("bau", false, detail, null, null, "dotnet")], preview, [], [detail], null);
        }
        return new(true, files, [new QaResult("bau", true, "dotnet build und dotnet test sind durch. Die App heißt " + job.Blueprint.OwnName + ".", null, null, "dotnet")], preview,
            ["Die Setup-Datei entsteht erst mit „Als Setup-Datei erstellen“. Sie ist nicht signiert."], [], null);
    }

    static bool On(IReadOnlyDictionary<string, string> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value)) return false;
        return value.Trim().ToLowerInvariant() is "ja" or "an" or "true" or "1";
    }

    static string Preview(RebuildJob job)
    {
        var sb = new StringBuilder();
        sb.AppendLine(job.Blueprint.OwnName);
        sb.AppendLine(job.Blueprint.NameReason);
        foreach (var feature in job.Blueprint.Features.Where(f => f.On))
            sb.Append("• ").Append(PrioText.Of(feature.Priority)).Append(": ").AppendLine(feature.Title);
        return sb.ToString().Trim();
    }
}
