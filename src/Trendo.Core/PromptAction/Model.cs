using Trendo.Core;

namespace Trendo.Core.PromptAction;

/// <summary>Persistenter Zustandsautomat von Verbesserung 42. Namen entsprechen dem Auftrag.</summary>
public enum WorkflowPhase
{
    Draft, Validating, NeedsClarification, Planned, NeedsApproval,
    Executing, Produced, Qa, Verified, RevisionRequested, Failed, Unsupported, Cancelled
}

/// <summary>Maschinenlesbarer Ergebnisstatus. Kein Wert bedeutet „erfolgreich“, der nicht belegt ist.</summary>
public enum RunStatus
{
    Planned, AwaitingInput, AwaitingApproval, Running, Produced, Verified, Failed, Cancelled, Unsupported
}

public enum SideEffectClass
{
    None, Message, Payment, Publish, DeleteOrRights, Security, Machine, ExternalAi
}

public enum CapabilityLevel
{
    Ready, Partial, Blocked, NotImplemented
}

public enum PrivacyMode
{
    Local, External
}

public sealed record PromptAttachment(string Name, string RelativePath);

public sealed record Provenance(string Source, string? TemplateId, int? TemplateVersion);

public sealed record StudioPromptRequest(
    string RequestId,
    string ProjectId,
    string StudioId,
    string Prompt,
    IReadOnlyList<PromptAttachment> Attachments,
    string Locale,
    string? RequestedAction,
    PrivacyMode PrivacyMode,
    Provenance Provenance);

public sealed record StudioActionSpec(
    int SchemaVersion,
    string StudioId,
    string ActionId,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> RequiredInputs,
    IReadOnlyList<string> OutputKinds,
    SideEffectClass SideEffectClass,
    IReadOnlyList<string> CapabilityRequirements);

public sealed record ResourceBudget(TimeSpan Timeout, int MaxArtifactBytes)
{
    public static ResourceBudget Default { get; } = new(TimeSpan.FromMinutes(2), 20 * 1024 * 1024);
}

public sealed record StudioExecutionContext(
    string ProjectId,
    Principal User,
    CancellationToken Cancellation,
    ResourceBudget Budget);

public sealed record ClarificationQuestion(string Id, string Field, string Question, bool Required);

public sealed record ValidationFinding(string Code, string Message, bool Blocking);

public sealed record PlanStep(string Id, string Label, bool SideEffect);

public sealed record StudioArtifact(
    string ArtifactId,
    string ProjectId,
    string Format,
    string RelativePath,
    bool Editable,
    string Producer,
    IReadOnlyList<string> Dependencies,
    string? Sha256,
    int Version,
    string? PreviewRef,
    string? QaReportRef);

public sealed record StepEvidence(string StepId, string Label, bool Done, string Detail, DateTimeOffset At);

public sealed record QaResult(string CheckId, bool Passed, string Detail, string? Units, string? Inputs, string? ToolVersion);

public sealed record StudioExecutionResult(
    string RunId,
    WorkflowPhase Phase,
    RunStatus Status,
    IReadOnlyList<StepEvidence> StepEvidence,
    IReadOnlyList<StudioArtifact> Artifacts,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Approvals,
    IReadOnlyList<QaResult> QaResults,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string RoutingReason,
    string? Preview,
    IReadOnlyList<ClarificationQuestion> Questions,
    string? Receipt,
    int Version);

public sealed record ActionDraft(string ActionId, IReadOnlyDictionary<string, string> Parameters);

public sealed record ProducedFile(string RelativePath, string Format, bool Editable);

public sealed record AdapterOutcome(
    bool Supported,
    IReadOnlyList<ProducedFile> Files,
    IReadOnlyList<QaResult> Qa,
    string? Preview,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    string? Receipt,
    bool Attempted = false);

public sealed record AdapterCall(
    string ProjectId,
    int Version,
    IReadOnlyDictionary<string, string> Parameters,
    ProjectStore Projects,
    bool SideEffectAuthorized,
    string IdempotencyKey,
    CancellationToken Cancellation);

/// <summary>Fachadapter. Das Modell darf ihn nicht selbst aufrufen; nur der Workflow tut das.</summary>
public interface IStudioActionAdapter
{
    string StudioId { get; }
    string ActionId { get; }
    string Label { get; }
    SideEffectClass Effect { get; }
    CapabilityLevel DeclaredLevel { get; }
    /// <summary>Testname „Klasse.Methode“, nur wenn die Aktion dort wirklich ausgeführt wird. Sonst leer.</summary>
    string Evidence { get; }
    bool CanPerformSideEffect { get; }
    IReadOnlyList<string> RequiredFields { get; }
    IReadOnlyList<string> OutputKinds { get; }
    ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known);
    IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters);
    IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters);
    AdapterOutcome Produce(AdapterCall call);
    AdapterOutcome SideEffect(AdapterCall call);
}

/// <summary>Vorschlag der bestehenden KI. Keine Freigabe, keine Rechte.</summary>
public interface IPromptModel
{
    string Name { get; }
    bool IsLocal { get; }
    Task<string?> CompleteAsync(string system, string user, CancellationToken cancellation);
}

public static class StatusText
{
    public static RunStatus From(WorkflowPhase phase) => phase switch
    {
        WorkflowPhase.NeedsClarification or WorkflowPhase.RevisionRequested => RunStatus.AwaitingInput,
        WorkflowPhase.NeedsApproval => RunStatus.AwaitingApproval,
        WorkflowPhase.Executing => RunStatus.Running,
        WorkflowPhase.Produced or WorkflowPhase.Qa => RunStatus.Produced,
        WorkflowPhase.Verified => RunStatus.Verified,
        WorkflowPhase.Failed => RunStatus.Failed,
        WorkflowPhase.Cancelled => RunStatus.Cancelled,
        WorkflowPhase.Unsupported => RunStatus.Unsupported,
        _ => RunStatus.Planned
    };

    public static string German(RunStatus status) => status switch
    {
        RunStatus.Planned => "Noch nicht gestartet",
        RunStatus.AwaitingInput => "Rückfrage",
        RunStatus.AwaitingApproval => "Freigabe nötig",
        RunStatus.Running => "Wird wirklich ausgeführt",
        RunStatus.Produced => "Datei erstellt",
        RunStatus.Verified => "QA bestanden",
        RunStatus.Failed => "Fehlgeschlagen",
        RunStatus.Cancelled => "Abgebrochen",
        RunStatus.Unsupported => "Nicht unterstützt",
        _ => "Fehlgeschlagen"
    };

    public static string German(CapabilityLevel level) => level switch
    {
        CapabilityLevel.Ready => "bereit",
        CapabilityLevel.Partial => "teilweise",
        CapabilityLevel.Blocked => "blockiert",
        _ => "nicht eingebaut"
    };

    /// <summary>„Plan steht“ nur, wenn wirklich ein Plan vorliegt. Vorher bleibt „Noch nicht gestartet“.</summary>
    public static string For(StudioExecutionResult result) =>
        result.Phase == WorkflowPhase.Planned ? "Plan steht" : German(result.Status);
}
