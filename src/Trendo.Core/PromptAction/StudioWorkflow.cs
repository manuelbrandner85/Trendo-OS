using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Trendo.Core;

namespace Trendo.Core.PromptAction;

/// <summary>Gemeinsamer Prompt-zu-Auftrag-Ablauf. Facharbeit machen nur registrierte Adapter. Die KI schlägt höchstens Felder vor.</summary>
public sealed class StudioWorkflow
{
    public const int SchemaVersion = 1;
    private static readonly Regex RunIdPattern = new("^[A-Za-z0-9_-]{4,40}$", RegexOptions.CultureInvariant);
    private static readonly string[] ForbiddenKeys = ["approval", "approved", "consent", "freigabe", "sideeffect", "side-effect"];

    private readonly TrendoCore _core;
    private readonly List<IStudioActionAdapter> _adapters;
    private readonly IPromptModel? _model;
    private readonly PromptLibrary _prompts;
    private readonly ConsentStore _consent;
    private readonly RunStore _runs;
    private readonly EffectLedger _ledger;
    private readonly TimeProvider _time;
    private readonly AuditLog _audit;
    private readonly PromptServices _services;

    public StudioWorkflow(TrendoCore core, IEnumerable<IStudioActionAdapter>? adapters = null, IPromptModel? model = null, TimeProvider? time = null, PromptLibrary? prompts = null, PromptServices? services = null)
    {
        _core = core;
        _adapters = (adapters ?? PromptActions.CreateDefault()).ToList();
        _model = model;
        _time = time ?? TimeProvider.System;
        _prompts = prompts ?? new PromptLibrary(core.DataRoot, core.Audit);
        _consent = new ConsentStore(core.DataRoot, core.Audit, _time);
        _runs = new RunStore(core.Projects);
        _ledger = new EffectLedger(core.Projects);
        _audit = core.Audit;
        _services = PromptServices.For(core, services);
    }

    public IReadOnlyList<IStudioActionAdapter> Adapters => _adapters;
    public ConsentStore Consent => _consent;

    public Task<StudioExecutionResult> StartAsync(StudioPromptRequest request, StudioExecutionContext ctx) =>
        Task.FromResult(Start(request, ctx));

    public StudioExecutionResult Start(StudioPromptRequest request, StudioExecutionContext ctx)
    {
        var runId = request.RequestId ?? "";
        if (!string.Equals(request.ProjectId, ctx.ProjectId, StringComparison.Ordinal))
            return FailedNow(runId, "Projekt im Auftrag und in der Ausführung stimmen nicht überein.");
        try { Rbac.Demand(ctx.User, Permission.RequestAction, _audit); }
        catch (UnauthorizedAccessException ex) { return FailedNow(runId, ex.Message); }
        if (!RunIdPattern.IsMatch(runId))
            return FailedNow(runId, "Die Auftragsnummer ist ungültig.");
        if ((request.Prompt ?? "").Length > 20000)
            return FailedNow(runId, "Der Auftrag ist zu lang.");
        try
        {
            if (_runs.TryLoad(request.ProjectId, runId) is not null)
                return FailedNow(runId, "Diesen Auftrag gibt es schon.");
        }
        catch (ProjectIsolationException ex) { return FailedNow(runId, ex.Message); }

        var now = _time.GetLocalNow();
        var state = new RunState
        {
            RunId = runId,
            ProjectId = request.ProjectId,
            StudioId = request.StudioId,
            Prompt = request.Prompt ?? "",
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? "de" : request.Locale,
            Privacy = request.PrivacyMode,
            StartedAt = now,
            UpdatedAt = now,
            History = [new HistoryEntry { At = now, Prompt = request.Prompt ?? "" }]
        };
        if (!TakeAttachments(state, request.Attachments)) return Finish(state);
        return Validate(state, ctx, request.RequestedAction);
    }

    public StudioExecutionResult Answer(string projectId, string runId, IReadOnlyDictionary<string, string> answers, StudioExecutionContext ctx)
    {
        var state = TryNeed(projectId, runId, ctx, out var error);
        if (state is null) return error!;
        if (state.Phase is not (WorkflowPhase.NeedsClarification or WorkflowPhase.RevisionRequested))
        {
            state.Warnings.Add("Gerade ist keine Rückfrage offen.");
            return Finish(state, save: false);
        }
        foreach (var (k, v) in answers)
        {
            if (Forbidden(k)) continue;
            state.Parameters[k] = v ?? "";
        }
        state.Questions.Clear();
        return Validate(state, ctx, null);
    }

    public StudioExecutionResult Approve(string projectId, string runId, Principal by, StudioExecutionContext ctx)
    {
        using var _ = PromptAmbient.Use(_services);
        var state = TryNeed(projectId, runId, ctx, out var error);
        if (state is null) return error!;
        if (state.Phase != WorkflowPhase.NeedsApproval)
        {
            state.Warnings.Add("Dieser Auftrag wartet nicht auf eine Freigabe.");
            return Finish(state, save: false);
        }
        try { _core.Approvals.Approve(state.ApprovalId!, by); }
        catch (Exception ex) when (ex is ApprovalException or UnauthorizedAccessException)
        {
            state.Errors.Add(ex.Message);
            return Finish(state);
        }
        state.Errors.Clear();
        return RunSideEffect(state, ctx);
    }

    public StudioExecutionResult Deny(string projectId, string runId, Principal by, StudioExecutionContext ctx)
    {
        var state = TryNeed(projectId, runId, ctx, out var error);
        if (state is null) return error!;
        if (state.Phase != WorkflowPhase.NeedsApproval) return Finish(state, save: false);
        try { _core.Approvals.Deny(state.ApprovalId!, by); }
        catch (Exception ex) when (ex is ApprovalException or UnauthorizedAccessException)
        {
            state.Errors.Add(ex.Message);
            return Finish(state);
        }
        Mark(state, WorkflowPhase.Cancelled, "Abgelehnt", "Die Freigabe wurde abgelehnt. Nichts davon wurde ausgeführt.");
        state.Warnings.Add("Freigabe abgelehnt.");
        return Finish(state);
    }

    public StudioExecutionResult Cancel(string projectId, string runId, Principal by, StudioExecutionContext ctx)
    {
        var state = TryNeed(projectId, runId, ctx, out var error);
        if (state is null) return error!;
        if (state.Phase is WorkflowPhase.Verified or WorkflowPhase.Cancelled) return Finish(state, save: false);
        if (state.IdempotencyKey is { } key && _ledger.Get(state.ProjectId, key) is { State: "claimed" or "done" or "blocked" })
        {
            state.Warnings.Add("Die Wirkung wurde schon angestoßen und wird nicht zurückgenommen.");
            return Finish(state, save: false);
        }
        if (state.ApprovalId is not null)
        {
            try { _core.Approvals.Deny(state.ApprovalId, by); } catch (Exception) { /* schon entschieden oder keine Freigaberechte */ }
        }
        Mark(state, WorkflowPhase.Cancelled, "Abbruch", "Abgebrochen, bevor eine Wirkung nach außen lief.");
        return Finish(state);
    }

    public StudioExecutionResult Revise(string projectId, string runId, string prompt, StudioExecutionContext ctx)
    {
        var state = TryNeed(projectId, runId, ctx, out var error);
        if (state is null) return error!;
        if (state.Phase is WorkflowPhase.Executing) return Finish(state, save: false);
        var hadFile = state.Artifacts.Count > 0;
        state.History.Add(new HistoryEntry { At = _time.GetLocalNow(), Prompt = prompt ?? "" });
        state.Prompt = prompt ?? "";
        if (hadFile) state.Version++;
        state.Parameters.Clear();
        state.ApprovalId = null;
        state.SideEffectReceipt = null;
        state.Questions.Clear();
        state.Artifacts.Clear();
        state.Qa.Clear();
        state.Preview = null;
        state.IdempotencyKey = null;
        Mark(state, WorkflowPhase.RevisionRequested, "Änderung", "Neue Fassung " + state.Version + ".");
        return Validate(state, ctx, null);
    }

    /// <summary>Schreibt die Datei erst nach dem Plan. Eine Wirkung nach außen wartet danach auf Freigabe.</summary>
    public StudioExecutionResult Los(string projectId, string runId, StudioExecutionContext ctx)
    {
        using var _ = PromptAmbient.Use(_services);
        var state = TryNeed(projectId, runId, ctx, out var error);
        if (state is null) return error!;
        if (state.Phase != WorkflowPhase.Planned)
        {
            state.Warnings.Add("Zuerst den Plan prüfen. Es wird nichts geschrieben.");
            return Finish(state, save: false);
        }
        var adapter = _adapters.FirstOrDefault(a => string.Equals(a.StudioId, state.RoutedStudioId, StringComparison.OrdinalIgnoreCase) && string.Equals(a.ActionId, state.ActionId, StringComparison.OrdinalIgnoreCase));
        if (adapter is null)
        {
            state.Errors.Add("Der Adapter für diesen Plan ist nicht mehr da.");
            Mark(state, WorkflowPhase.Failed, "Adapter", state.Errors[0]);
            return Finish(state);
        }
        return Build(state, adapter, ctx);
    }

    public StudioExecutionResult Undo(string projectId, string runId, StudioExecutionContext ctx)
    {
        if (!string.Equals(projectId, ctx.ProjectId, StringComparison.Ordinal))
            return FailedNow(runId, "Projekt stimmt nicht überein.");
        try { Rbac.Demand(ctx.User, Permission.RequestAction, _audit); }
        catch (UnauthorizedAccessException ex) { return FailedNow(runId, ex.Message); }
        try
        {
            if (!_runs.TryRollback(projectId, runId)) return FailedNow(runId, "Nichts zum Rückgängigmachen.");
        }
        catch (ProjectIsolationException ex) { return FailedNow(runId, ex.Message); }
        return Get(projectId, runId);
    }

    public StudioExecutionResult Retry(string projectId, string runId, StudioExecutionContext ctx)
    {
        var state = TryNeed(projectId, runId, ctx, out var error);
        if (state is null) return error!;
        if (state.IdempotencyKey is { } key && _ledger.Get(state.ProjectId, key) is { } entry && entry.State is "claimed" or "done" or "blocked")
        {
            state.Warnings.Add(entry.State == "done"
                ? "Schon erledigt. Es gibt keinen zweiten Versuch."
                : "Der erste Versuch bleibt stehen, damit nichts doppelt passiert.");
            if (entry.Receipt is not null) state.SideEffectReceipt = entry.Receipt;
            return Finish(state, save: false);
        }
        if (state.Phase is WorkflowPhase.Failed or WorkflowPhase.Executing)
            return Validate(state, ctx, state.ActionId);
        return Finish(state, save: false);
    }

    public StudioExecutionResult Resume(string projectId, string runId, StudioExecutionContext ctx)
    {
        RunState? state;
        try { state = _runs.TryLoad(projectId, runId); }
        catch (ProjectIsolationException ex) { return FailedNow(runId, ex.Message); }
        if (state is null) return FailedNow(runId, "Auftrag nicht gefunden.");
        if (state.SchemaVersion > SchemaVersion)
        {
            state.Phase = WorkflowPhase.Unsupported;
            state.Errors.Add("Diese Auftragsfassung ist neuer als diese Trendo-Version. Sie wurde nicht ausgeführt.");
            return Finish(state, save: false);
        }
        if (state.SchemaVersion < SchemaVersion)
        {
            state.SchemaVersion = SchemaVersion;
            state.Warnings.Add("Ältere Auftragsfassung wurde auf Fassung " + SchemaVersion + " gehoben.");
        }
        if (state.Phase == WorkflowPhase.Executing)
        {
            if (state.IdempotencyKey is { } key && _ledger.Get(projectId, key) is { State: "claimed" or "done" or "blocked" } entry)
            {
                state.SideEffectReceipt = entry.Receipt ?? state.SideEffectReceipt;
                Mark(state, entry.State == "done" ? WorkflowPhase.Verified : WorkflowPhase.Failed, "Wiederaufnahme",
                    entry.State == "done" ? "Die Wirkung war schon belegt." : "Kein zweiter Versuch nach einem offenen Versuch.");
                return Finish(state);
            }
            return Validate(state, ctx, state.ActionId);
        }
        return Finish(state, save: false);
    }

    public StudioExecutionResult Get(string projectId, string runId)
    {
        try
        {
            var state = _runs.TryLoad(projectId, runId);
            return state is null ? FailedNow(runId, "Auftrag nicht gefunden.") : Finish(state, save: false);
        }
        catch (ProjectIsolationException ex) { return FailedNow(runId, ex.Message); }
    }

    public bool TryRollback(string projectId, string runId) => _runs.TryRollback(projectId, runId);

    private StudioExecutionResult Validate(RunState state, StudioExecutionContext ctx, string? requestedAction)
    {
        if (ctx.Cancellation.IsCancellationRequested)
        {
            Mark(state, WorkflowPhase.Cancelled, "Abbruch", "Abbruch vor der Ausführung.");
            return Finish(state);
        }
        state.Errors.Clear();
        state.Findings.Clear();
        if (string.IsNullOrWhiteSpace(state.Prompt) && state.Parameters.Count == 0)
        {
            state.Errors.Add("Bitte schreib zuerst, was das Studio tun soll.");
            Mark(state, WorkflowPhase.Failed, "Leer", state.Errors[0]);
            return Finish(state);
        }
        if (CapabilityCatalog.Find(state.StudioId) is null)
        {
            state.Errors.Add("Dieses Studio gibt es nicht.");
            Mark(state, WorkflowPhase.Failed, "Studio", state.Errors[0]);
            return Finish(state);
        }

        var known = Known(state);
        var (routed, reason) = Route(state.StudioId, known);
        state.RoutedStudioId = routed;
        state.RoutingReason = reason;
        if (ApplyTemplate(state, known) is { } stop) return stop;
        known = Known(state);
        if (CapabilityCatalog.Find(routed) is null)
        {
            state.Errors.Add("Das genannte Studio gibt es nicht.");
            Mark(state, WorkflowPhase.Failed, "Studio", state.Errors[0]);
            return Finish(state);
        }

        var mine = _adapters.Where(a => string.Equals(a.StudioId, routed, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(requestedAction))
            mine = mine.Where(a => string.Equals(a.ActionId, requestedAction, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count == 0)
        {
            state.Errors.Add("Für „" + CapabilityCatalog.Title(routed) + "“ gibt es noch keine Aktion, die Trendo wirklich ausführt.");
            Mark(state, WorkflowPhase.Unsupported, "Nicht unterstützt", state.Errors[0]);
            state.Artifacts.Clear();
            return Finish(state);
        }

        var draft = mine.Select(a => a.TryParse(EffectivePrompt(state), known)).FirstOrDefault(d => d is not null);
        IStudioActionAdapter? adapter = null;
        if (draft is not null)
            adapter = mine.First(a => string.Equals(a.ActionId, draft.ActionId, StringComparison.OrdinalIgnoreCase));
        else
        {
            var suggested = Suggest(state, mine, ctx.Cancellation);
            if (suggested is not null && mine.FirstOrDefault(a => string.Equals(a.ActionId, suggested.Value.Action, StringComparison.OrdinalIgnoreCase)) is { } picked)
            {
                adapter = picked;
                draft = new ActionDraft(picked.ActionId, suggested.Value.Parameters);
            }
            else if (suggested is not null)
            {
                state.Errors.Add("Die vorgeschlagene Aktion gibt es in diesem Studio nicht.");
                Mark(state, WorkflowPhase.Unsupported, "Vorschlag", state.Errors[0]);
                return Finish(state);
            }
        }

        if (adapter is null || draft is null)
        {
            state.Questions = [new ClarificationQuestion("aktion", "aktion", "Welche Aktion meinst du? Möglich: " + string.Join(", ", mine.Select(a => a.Label)) + ".", true)];
            Mark(state, WorkflowPhase.NeedsClarification, "Rückfrage", state.Questions[0].Question);
            return Finish(state);
        }

        var parameters = new Dictionary<string, string>(draft.Parameters, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in known) parameters[kv.Key] = kv.Value;
        Strip(parameters);
        state.ActionId = adapter.ActionId;
        state.SideEffect = adapter.Effect;
        state.Parameters = parameters;
        state.Questions = adapter.Missing(parameters).ToList();
        if (state.Questions.Any(q => q.Required))
        {
            Mark(state, WorkflowPhase.NeedsClarification, "Rückfrage", "Es fehlen Angaben.");
            return Finish(state);
        }
        state.Findings = adapter.Validate(parameters).ToList();
        if (state.Findings.Any(f => f.Blocking))
        {
            state.Errors.AddRange(state.Findings.Where(f => f.Blocking).Select(f => f.Message));
            Mark(state, WorkflowPhase.Failed, "Prüfung", "Der Auftrag ist so nicht ausführbar.");
            return Finish(state);
        }

        state.IdempotencyKey = Key(state);
        state.Steps =
        [
            new PlanStep("entwurf", "Lokal erstellen", false),
            new PlanStep("qa", "Prüfen", false)
        ];
        if (adapter.Effect != SideEffectClass.None)
            state.Steps.Add(new PlanStep("wirkung", "Wirkung nur nach Freigabe", true));
        Mark(state, WorkflowPhase.Planned, "Plan", reason);
        foreach (var step in state.Steps)
            state.Evidence.Add(new StepEvidence(step.Id, step.Label, false, "Steht im Plan.", state.UpdatedAt));
        state.Artifacts.Clear();
        state.Qa.Clear();
        state.Preview = "Noch keine Datei. Der Plan steht, es wurde nichts geschrieben.";
        return Finish(state);
    }

    private StudioExecutionResult Build(RunState state, IStudioActionAdapter adapter, StudioExecutionContext ctx)
    {
        if (!Produce(state, adapter, ctx)) return Finish(state);
        if (state.Artifacts.Count == 0)
        {
            state.Errors.Add("Es wurde keine Datei geschrieben.");
            state.Preview = "Hier gibt es keine Vorschau, weil keine Datei entstanden ist.";
            Mark(state, WorkflowPhase.Failed, "Datei", state.Errors[^1]);
            return Finish(state);
        }
        if (string.IsNullOrWhiteSpace(state.Preview))
            state.Preview = "Für " + state.Artifacts[0].Format + " gibt es keine Textvorschau. Die Datei liegt im Projekt: " + state.Artifacts[0].RelativePath;
        if (adapter.Effect == SideEffectClass.None)
            return Judge(state);

        if (!adapter.CanPerformSideEffect)
        {
            state.Warnings.Add("Die Wirkung nach außen ist nicht angeschlossen. Es wurde nichts gesendet, bezahlt oder veröffentlicht.");
            Mark(state, WorkflowPhase.Unsupported, "Wirkung", state.Warnings[^1]);
            return Finish(state);
        }
        try
        {
            var approval = _core.Approvals.Request(ctx.User, GateAction(adapter), Describe(state), state.IdempotencyKey!, TimeSpan.FromMinutes(10));
            state.ApprovalId = approval.Id;
        }
        catch (UnauthorizedAccessException ex)
        {
            state.Errors.Add(ex.Message);
            Mark(state, WorkflowPhase.Failed, "Recht", ex.Message);
            return Finish(state);
        }
        Mark(state, WorkflowPhase.NeedsApproval, "Freigabe", "Wartet auf eine einmalige Freigabe.");
        return Finish(state);
    }

    private StudioExecutionResult? ApplyTemplate(RunState state, Dictionary<string, string> known)
    {
        if (!known.TryGetValue("vorlage", out var id) || string.IsNullOrWhiteSpace(id)) return null;
        var template = _prompts.Get(id.Trim());
        if (template is null)
        {
            state.Warnings.Add("Die Vorlage „" + id.Trim() + "“ gibt es nicht.");
            return null;
        }
        state.TemplateId = template.Id;
        state.TemplateVersion = template.Version;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in PromptLibrary.Variables(template.Text))
        {
            if (known.TryGetValue(name, out var direct) && !string.IsNullOrWhiteSpace(direct)) values[name] = direct;
            else if (known.TryGetValue(PromptFields.Norm(name), out var norm) && !string.IsNullOrWhiteSpace(norm)) values[name] = norm;
        }
        var (text, missing) = PromptLibrary.Fill(template.Text, values);
        if (missing.Count > 0)
        {
            state.Questions = missing.Select(m => new ClarificationQuestion(m, m, "Was soll bei „" + m + "“ stehen?", true)).ToList();
            Mark(state, WorkflowPhase.NeedsClarification, "Vorlage", "In der Vorlage fehlen Angaben.");
            return Finish(state);
        }
        state.Prompt = text;
        state.Parameters["vorlage"] = template.Id;
        return null;
    }

    private (string Action, Dictionary<string, string> Parameters)? Suggest(RunState state, List<IStudioActionAdapter> mine, CancellationToken ct)
    {
        if (_model is null) return null;
        var consent = _consent.Load();
        var external = !_model.IsLocal;
        if (external && (state.Privacy != PrivacyMode.External || !consent.AllowsExternal(_time.GetLocalNow())))
        {
            _audit.Append("ki", "ki.external.blocked", "quelle=" + _model.Name);
            state.Warnings.Add("Eine externe KI wurde nicht gefragt. Dafür fehlt die Einwilligung, oder sie ist abgelaufen.");
            return null;
        }
        if (!external && state.Privacy == PrivacyMode.External)
            state.Warnings.Add("Es ist nur die lokale KI angeschlossen.");
        var allowed = string.Join(", ", mine.Select(a => a.ActionId));
        var system = "Du schlägst nur Felder vor. Antworte mit einem JSON-Objekt {\"actionId\":\"\",\"parameters\":{}}. "
            + "Erlaubte actionId: " + allowed + ". Keine Freigabe, keine Zahlung, kein Versand. Erfinde keine fehlenden Pflichtangaben.";
        var user = "UNTRUSTED_START\n" + state.Prompt + "\n" + string.Join("\n", state.AttachmentNotes) + "\nUNTRUSTED_END";
        string? text;
        try { text = Task.Run(async () => await _model.CompleteAsync(system, user, ct).ConfigureAwait(false), ct).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { return null; }
        catch (Exception) { state.Warnings.Add("Die KI hat nicht geantwortet. Es wird nichts geraten."); return null; }
        if (string.IsNullOrWhiteSpace(text)) return null;
        return ReadSuggestion(text);
    }

    private static (string Action, Dictionary<string, string> Parameters)? ReadSuggestion(string text)
    {
        var a = text.IndexOf('{');
        var b = text.LastIndexOf('}');
        if (a < 0 || b <= a) return null;
        try
        {
            using var doc = JsonDocument.Parse(text[a..(b + 1)]);
            var root = doc.RootElement;
            if (!root.TryGetProperty("actionId", out var idEl)) return null;
            var id = idEl.GetString() ?? "";
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("parameters", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in p.EnumerateObject())
                {
                    if (Forbidden(prop.Name)) continue;
                    map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? "" : prop.Value.ToString();
                }
            }
            if (id.Length == 0) return null;
            return (id, map);
        }
        catch (JsonException) { return null; }
    }

    private bool Produce(RunState state, IStudioActionAdapter adapter, StudioExecutionContext ctx)
    {
        if (ctx.Cancellation.IsCancellationRequested)
        {
            Mark(state, WorkflowPhase.Cancelled, "Abbruch", "Abbruch vor dem Erstellen.");
            return false;
        }
        Mark(state, WorkflowPhase.Executing, "Erstellen", "Die Datei wird wirklich geschrieben.");
        AdapterOutcome outcome;
        try
        {
            outcome = adapter.Produce(new AdapterCall(state.ProjectId, state.Version, state.Parameters, _core.Projects, false, state.IdempotencyKey ?? "", ctx.Cancellation));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectIsolationException or ArgumentException)
        {
            state.Errors.Add(ex.Message);
            Mark(state, WorkflowPhase.Failed, "Erstellen", ex.Message);
            return false;
        }
        return Accept(state, adapter, outcome, sideEffect: false);
    }

    private StudioExecutionResult RunSideEffect(RunState state, StudioExecutionContext ctx)
    {
        var adapter = _adapters.FirstOrDefault(a => string.Equals(a.StudioId, state.RoutedStudioId, StringComparison.OrdinalIgnoreCase) && string.Equals(a.ActionId, state.ActionId, StringComparison.OrdinalIgnoreCase));
        if (adapter is null)
        {
            state.Errors.Add("Der Adapter für diese Aktion ist nicht mehr da.");
            Mark(state, WorkflowPhase.Failed, "Adapter", state.Errors[0]);
            return Finish(state);
        }
        var key = state.IdempotencyKey ?? "";
        var existing = _ledger.Get(state.ProjectId, key);
        if (existing is { State: "done", Receipt: not null })
        {
            state.SideEffectReceipt = existing.Receipt;
            state.Preview = existing.Receipt;
            state.Warnings.Add("Schon erledigt. Es gibt keinen zweiten Versuch.");
            return Judge(state);
        }
        if (existing is { State: "claimed" or "blocked" })
        {
            state.Errors.Add("Der erste Versuch bleibt stehen, damit nichts doppelt passiert.");
            Mark(state, WorkflowPhase.Failed, "Wirkung", state.Errors[0]);
            return Finish(state);
        }
        if (!adapter.CanPerformSideEffect)
        {
            state.Warnings.Add("Die Wirkung ist nicht angeschlossen.");
            Mark(state, WorkflowPhase.Unsupported, "Wirkung", state.Warnings[^1]);
            return Finish(state);
        }
        try
        {
            var receipt = _core.Approvals.Execute(state.ApprovalId!, GateAction(adapter), key, () =>
            {
                if (!adapter.CanPerformSideEffect) throw new InvalidOperationException("Die Wirkung ist nicht angeschlossen.");
                var again = _ledger.Get(state.ProjectId, key);
                if (again is { State: "done", Receipt: not null }) return again.Receipt;
                if (again is { State: "claimed" or "blocked" }) throw new InvalidOperationException("Kein zweiter Versuch.");
                _ledger.Claim(state.ProjectId, key, state.RunId);
                var outcome = adapter.SideEffect(new AdapterCall(state.ProjectId, state.Version, state.Parameters, _core.Projects, true, key, ctx.Cancellation));
                if (!outcome.Supported || string.IsNullOrWhiteSpace(outcome.Receipt))
                {
                    if (outcome.Attempted || outcome.Supported)
                        _ledger.Block(state.ProjectId, key, state.RunId);
                    else
                        _ledger.Release(state.ProjectId, key);
                    throw new InvalidOperationException(outcome.Errors.FirstOrDefault() ?? "Nicht ausgeführt.");
                }
                _ledger.Done(state.ProjectId, key, outcome.Receipt);
                return outcome.Receipt;
            });
            state.SideEffectReceipt = receipt;
            if (!string.IsNullOrWhiteSpace(receipt)) state.Preview = receipt;
            state.Evidence.Add(new StepEvidence("wirkung", "Ausgeführt", true, "Beleg " + receipt, _time.GetLocalNow()));
        }
        catch (Exception ex) when (ex is ApprovalException or InvalidOperationException or UnauthorizedAccessException)
        {
            state.Errors.Add(ex.Message);
            Mark(state, ex.Message.Contains("nicht angeschlossen", StringComparison.Ordinal) ? WorkflowPhase.Unsupported : WorkflowPhase.Failed, "Wirkung", ex.Message);
            return Finish(state);
        }
        return Judge(state);
    }

    private bool Accept(RunState state, IStudioActionAdapter adapter, AdapterOutcome outcome, bool sideEffect)
    {
        state.Warnings.AddRange(outcome.Warnings);
        state.Errors.AddRange(outcome.Errors);
        if (!outcome.Supported)
        {
            Mark(state, WorkflowPhase.Unsupported, "Nicht unterstützt", state.Errors.FirstOrDefault() ?? "Die Aktion wird nicht ausgeführt.");
            return false;
        }
        var artifacts = new List<StudioArtifact>();
        foreach (var file in outcome.Files)
        {
            string full;
            try { full = _core.Projects.ResolveInProject(state.ProjectId, file.RelativePath); }
            catch (ProjectIsolationException ex)
            {
                state.Errors.Add(ex.Message);
                Mark(state, WorkflowPhase.Failed, "Pfad", ex.Message);
                return false;
            }
            if (!File.Exists(full))
            {
                state.Errors.Add("Die Datei fehlt: " + file.RelativePath);
                Mark(state, WorkflowPhase.Failed, "Datei", state.Errors[^1]);
                return false;
            }
            var info = new FileInfo(full);
            if (info.Length > ResourceBudget.Default.MaxArtifactBytes)
            {
                state.Errors.Add("Die Datei ist zu groß.");
                Mark(state, WorkflowPhase.Failed, "Größe", state.Errors[^1]);
                return false;
            }
            artifacts.Add(new StudioArtifact(Path.GetFileName(file.RelativePath), state.ProjectId, file.Format, file.RelativePath.Replace('\\', '/'), file.Editable, adapter.ActionId, [], ForensicsLab.Sha256(full), state.Version, null, null));
        }
        state.Artifacts = artifacts;
        state.Qa = outcome.Qa.ToList();
        state.Preview = outcome.Preview;
        if (sideEffect && outcome.Receipt is not null) state.SideEffectReceipt = outcome.Receipt;
        return true;
    }

    private StudioExecutionResult Judge(RunState state)
    {
        Mark(state, WorkflowPhase.Qa, "QA", "Prüfung der erzeugten Datei.");
        if (state.Artifacts.Count == 0)
            state.Errors.Add("Es wurde keine Datei geschrieben.");
        if (state.Errors.Count > 0 || state.Qa.Count == 0 || state.Qa.Any(q => !q.Passed))
        {
            if (state.Qa.Count == 0) state.Errors.Add("Die Aktion hat keine Prüfung geliefert.");
            state.Errors.AddRange(state.Qa.Where(q => !q.Passed).Select(q => q.Detail));
            if (state.Artifacts.Count == 0) state.Preview = "Hier gibt es keine Vorschau, weil keine Datei entstanden ist.";
            Mark(state, WorkflowPhase.Failed, "QA", "Die Prüfung ist nicht bestanden.");
            return Finish(state);
        }
        Mark(state, WorkflowPhase.Verified, "Fertig", "Datei geprüft.");
        return Finish(state);
    }

    private bool TakeAttachments(RunState state, IReadOnlyList<PromptAttachment> attachments)
    {
        foreach (var attachment in attachments ?? [])
        {
            try
            {
                var full = _core.Projects.ResolveInProject(state.ProjectId, attachment.RelativePath);
                if (new FileInfo(full).LinkTarget is not null)
                {
                    state.Errors.Add("Ein Anhang ist ein Verweis nach außen und wurde nicht gelesen.");
                    Mark(state, WorkflowPhase.Failed, "Anhang", state.Errors[^1]);
                    return false;
                }
                if (!File.Exists(full)) { state.AttachmentNotes.Add(attachment.Name + ": fehlt"); continue; }
                var len = new FileInfo(full).Length;
                if (len > 100_000) { state.AttachmentNotes.Add(attachment.Name + ": zu groß, nicht gelesen"); continue; }
                var text = File.ReadAllText(full);
                state.AttachmentNotes.Add(attachment.Name + ": " + text.Length + " Zeichen");
            }
            catch (ProjectIsolationException)
            {
                state.Errors.Add("Ein Anhang liegt außerhalb des Projekts und wurde nicht gelesen.");
                Mark(state, WorkflowPhase.Failed, "Anhang", state.Errors[0]);
                return false;
            }
            catch (IOException ex)
            {
                state.Errors.Add(ex.Message);
                Mark(state, WorkflowPhase.Failed, "Anhang", ex.Message);
                return false;
            }
        }
        return true;
    }

    private RunState? TryNeed(string projectId, string runId, StudioExecutionContext ctx, out StudioExecutionResult? error)
    {
        error = null;
        if (!string.Equals(projectId, ctx.ProjectId, StringComparison.Ordinal))
        {
            error = FailedNow(runId, "Projekt stimmt nicht überein.");
            return null;
        }
        try { Rbac.Demand(ctx.User, Permission.RequestAction, _audit); }
        catch (UnauthorizedAccessException ex) { error = FailedNow(runId, ex.Message); return null; }
        try
        {
            var state = _runs.TryLoad(projectId, runId);
            if (state is null) { error = FailedNow(runId, "Auftrag nicht gefunden."); return null; }
            return state;
        }
        catch (ProjectIsolationException ex) { error = FailedNow(runId, ex.Message); return null; }
    }

    private StudioExecutionResult Finish(RunState state, bool save = true)
    {
        state.UpdatedAt = _time.GetLocalNow();
        if (save)
        {
            try { _runs.Save(state); }
            catch (ProjectIsolationException ex) { state.Errors.Add(ex.Message); state.Phase = WorkflowPhase.Failed; }
            _audit.Append("prompt", "prompt.phase", $"studio={state.RoutedStudioId} aktion={state.ActionId} phase={state.Phase} zeichen={state.Prompt.Length}");
        }
        return new StudioExecutionResult(state.RunId, state.Phase, StatusText.From(state.Phase), state.Evidence, state.Artifacts, state.Errors, state.Warnings,
            state.ApprovalId is null ? [] : [state.ApprovalId], state.Qa, state.StartedAt, state.UpdatedAt, state.RoutingReason, state.Preview, state.Questions, state.SideEffectReceipt, state.Version);
    }

    private void Mark(RunState state, WorkflowPhase phase, string label, string detail)
    {
        state.Phase = phase;
        state.UpdatedAt = _time.GetLocalNow();
        state.Evidence.Add(new StepEvidence(phase.ToString(), label, true, detail, state.UpdatedAt));
    }

    private StudioExecutionResult FailedNow(string runId, string message)
    {
        var now = _time.GetLocalNow();
        return new StudioExecutionResult(runId, WorkflowPhase.Failed, RunStatus.Failed, [], [], [message], [], [], [], now, now, "", null, [], null, 0);
    }

    private static Dictionary<string, string> Known(RunState state)
    {
        var map = PromptFields.Read(state.Prompt);
        foreach (var kv in state.Parameters) map[kv.Key] = kv.Value;
        Strip(map);
        return map;
    }

    private static string EffectivePrompt(RunState state) => state.Prompt;

    private static (string Id, string Reason) Route(string current, IReadOnlyDictionary<string, string> known)
    {
        if (known.TryGetValue("studio", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            var clean = id.Trim();
            var profile = CapabilityCatalog.Find(clean);
            if (profile is null) return (clean, "Der Auftrag nennt ein unbekanntes Studio.");
            if (!string.Equals(profile.Id, current, StringComparison.OrdinalIgnoreCase))
                return (profile.Id, "Der Auftrag nennt das Studio " + profile.Title + " (" + profile.Id + ").");
        }
        if (known.TryGetValue("weiterleiten", out var title) && !string.IsNullOrWhiteSpace(title))
        {
            var profile = CapabilityCatalog.Profiles.FirstOrDefault(p => string.Equals(p.Title, title.Trim(), StringComparison.OrdinalIgnoreCase));
            if (profile is not null && !string.Equals(profile.Id, current, StringComparison.OrdinalIgnoreCase))
                return (profile.Id, "Weitergeleitet an " + profile.Title + ".");
        }
        return (current, "Bleibt im aktuellen Studio.");
    }

    private static string Key(RunState state)
    {
        var body = state.ProjectId + "\n" + state.RoutedStudioId + "\n" + state.ActionId + "\n" + state.SideEffect + "\n"
            + string.Join("\n", state.Parameters.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + "=" + kv.Value.Trim()));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    private static string GateAction(IStudioActionAdapter adapter) => "prompt:" + adapter.ActionId;

    private static string Describe(RunState state)
    {
        var bits = state.Parameters.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Take(6)
            .Select(kv => kv.Key + "=" + Trim(Redact(kv.Value), 40));
        return "Einmalige Freigabe für " + state.ActionId + ". " + string.Join(", ", bits) + ". Gilt nur für diesen Vorgang.";
    }

    private static void Strip(Dictionary<string, string> map)
    {
        foreach (var key in map.Keys.Where(Forbidden).ToList()) map.Remove(key);
    }

    private static bool Forbidden(string key)
    {
        var n = PromptFields.Norm(key);
        return ForbiddenKeys.Contains(n) || n.Contains("passwort") || n.Contains("apikey") || n.Contains("api-key");
    }

    private static string Redact(string value) =>
        Regex.Replace(value, "(?i)(passwort|password|api[-_ ]?key|secret)\\s*[:=]\\s*\\S+", "$1=[entfernt]");

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
