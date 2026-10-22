using System.Text.Json;
using Trendo.Core;
using System.Text.Json.Serialization;

namespace Trendo.Core.PromptAction;

public sealed class HistoryEntry
{
    public DateTimeOffset At { get; set; }
    public string Prompt { get; set; } = "";
}

public sealed class RunState
{
    public int SchemaVersion { get; set; } = StudioWorkflow.SchemaVersion;
    public string RunId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public WorkflowPhase Phase { get; set; }
    public string StudioId { get; set; } = "";
    public string RoutedStudioId { get; set; } = "";
    public string RoutingReason { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Locale { get; set; } = "de";
    public PrivacyMode Privacy { get; set; }
    public string? TemplateId { get; set; }
    public int? TemplateVersion { get; set; }
    public string? ActionId { get; set; }
    public SideEffectClass SideEffect { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ClarificationQuestion> Questions { get; set; } = [];
    public List<ValidationFinding> Findings { get; set; } = [];
    public List<PlanStep> Steps { get; set; } = [];
    public string? ApprovalId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? SideEffectReceipt { get; set; }
    public List<StepEvidence> Evidence { get; set; } = [];
    public List<StudioArtifact> Artifacts { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<QaResult> Qa { get; set; } = [];
    public List<string> AttachmentNotes { get; set; } = [];
    public string? Preview { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<HistoryEntry> History { get; set; } = [];
}

public sealed class RunStore
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ProjectStore _projects;
    public RunStore(ProjectStore projects) => _projects = projects;

    public static string Rel(string runId) => "auftraege/" + runId + "/lauf.json";

    public void Save(RunState state)
    {
        var full = _projects.ResolveInProject(state.ProjectId, Rel(state.RunId));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        if (File.Exists(full)) File.Copy(full, full + ".bak", true);
        var tmp = full + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json));
        File.Move(tmp, full, true);
    }

    public RunState? TryLoad(string projectId, string runId)
    {
        var full = _projects.ResolveInProject(projectId, Rel(runId));
        if (!File.Exists(full)) return null;
        var state = JsonSerializer.Deserialize<RunState>(File.ReadAllText(full), Json);
        if (state is null) return null;
        state.Parameters = new Dictionary<string, string>(state.Parameters ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        return state;
    }

    public bool TryRollback(string projectId, string runId)
    {
        var full = _projects.ResolveInProject(projectId, Rel(runId));
        var bak = full + ".bak";
        if (!File.Exists(bak)) return false;
        File.Copy(bak, full, true);
        return true;
    }
}

/// <summary>Einmal-Schlüssel für Wirkungen nach außen. „claimed“ wird nicht erneut ausgeführt.</summary>
public sealed class EffectLedger
{
    private static readonly JsonSerializerOptions Json = RunStore.Json;
    private readonly ProjectStore _projects;
    private readonly object _lock = new();

    public EffectLedger(ProjectStore projects) => _projects = projects;

    private sealed class FileDto
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<string, Entry> Keys { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class Entry
    {
        public string State { get; set; } = "";
        public string? Receipt { get; set; }
        public string RunId { get; set; } = "";
    }

    private string Full(string projectId) => _projects.ResolveInProject(projectId, "auftraege/_wirkung.json");

    public Entry? Get(string projectId, string key)
    {
        lock (_lock)
        {
            var dto = Read(projectId);
            return dto.Keys.TryGetValue(key, out var e) ? e : null;
        }
    }

    public void Claim(string projectId, string key, string runId)
    {
        lock (_lock)
        {
            var dto = Read(projectId);
            if (dto.Keys.TryGetValue(key, out var e) && e.State is "claimed" or "done" or "blocked")
                throw new InvalidOperationException("Diese Wirkung wurde schon angestoßen.");
            dto.Keys[key] = new Entry { State = "claimed", RunId = runId };
            Write(projectId, dto);
        }
    }

    public void Done(string projectId, string key, string receipt)
    {
        lock (_lock)
        {
            var dto = Read(projectId);
            dto.Keys[key] = new Entry { State = "done", Receipt = receipt, RunId = dto.Keys.TryGetValue(key, out var e) ? e.RunId : "" };
            Write(projectId, dto);
        }
    }

    public void Block(string projectId, string key, string runId)
    {
        lock (_lock)
        {
            var dto = Read(projectId);
            dto.Keys[key] = new Entry { State = "blocked", RunId = runId };
            Write(projectId, dto);
        }
    }

    private FileDto Read(string projectId)
    {
        var full = Full(projectId);
        if (!File.Exists(full)) return new FileDto();
        try { return JsonSerializer.Deserialize<FileDto>(File.ReadAllText(full), Json) ?? new FileDto(); }
        catch (JsonException) { return new FileDto(); }
    }

    private void Write(string projectId, FileDto dto)
    {
        var full = Full(projectId);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var tmp = full + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dto, Json));
        File.Move(tmp, full, true);
    }
}
