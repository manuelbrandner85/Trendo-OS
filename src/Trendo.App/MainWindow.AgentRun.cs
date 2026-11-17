using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0698: Agenten laufen in der App – Modellwahl über <see cref="IntelligenceCore"/>, Werkzeuge über vorhandene Wege (Lesemodus, Studio-Ablauf),
/// Freigaben über <see cref="ApprovalGate"/> mit Ja/Nein in der Lauf-Ansicht, Stopp-Knopf, Live-Protokoll. Kein eigenes Laufzeitsystem: alles in <see cref="AgentCenter"/>.</summary>
public partial class MainWindow
{
    /// <summary>Der zuletzt gestartete Lauf (live, solange er läuft).</summary>
    public AgentRun? AgentLive { get; private set; }
    /// <summary>Läuft gerade ein Agent oder ein Team, das sich stoppen lässt?</summary>
    public bool AgentRunning => _agentCts is not null;
    CancellationTokenSource? _agentCts;
    /// <summary>Offene Freigabe-Frage eines laufenden Agenten (Ja/Nein in der Lauf-Ansicht).</summary>
    public ApprovalRequest? AgentAsking => _agentAsk?.Req;
    (ApprovalRequest Req, TaskCompletionSource<bool> Tcs)? _agentAsk;
    /// <summary>Für Tests: Lesemodus-Abruf ersetzen (sonst <see cref="ReadOnlyWeb.GetPageAsync"/>).</summary>
    public Func<Uri, CancellationToken, Task<(string Type, byte[] Bytes, Uri Final)>>? AgentWebOverride { get; set; }

    /// <summary>KI für einen Agenten: „“ = Haupt-KI (wie überall), „lokal[:modell]“ = nur KI auf diesem PC, „claude“ = nur Claude über das eigene Abo.</summary>
    internal IntelligenceCore AgentKi(string model, out string? preferred)
    {
        preferred = model.StartsWith("lokal:", StringComparison.Ordinal) ? model[6..] : model is "" or "lokal" ? KiModelName : null;
        if (model == "") return Intel;
        IReadOnlyList<IKiBackend> All() => KiBackendsOverride?.Invoke() ?? DefaultKiBackends();
        return model == "claude"
            ? new IntelligenceCore(_core.DataRoot, _core.Audit, () => All().Where(b => b is IMainKiBackend).ToList())
            : new IntelligenceCore(_core.DataRoot, _core.Audit, () => All().Where(b => b is not IRemoteKiBackend).ToList()) { LocalFallbacks = KiFallbacks, LocalModelFailed = KiModelFailed };
    }

    /// <summary>Dienste für Agenten-Werkzeuge. <paramref name="page"/>: Seite, die live neu gezeichnet wird; null = Lauf ohne Person (Ablauf) – dann gibt es keine Zustimmung.</summary>
    internal AgentServices AgentSvc(string? page) => new()
    {
        ReadWeb = _core.Offline ? null : AgentWebOverride ?? ((u, ct) => ReadOnlyWeb.GetPageAsync(u, ct)),
        Studio = PromptFlow,
        ExportAllowed = d =>
        {
            var full = Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var home = Path.GetFullPath(WorkflowHome).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var data = Path.GetFullPath(_core.DataRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(home, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(data, StringComparison.OrdinalIgnoreCase);
        },
        AskApproval = page is null ? null : (req, ct) =>
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => tcs.TrySetResult(false));
            _ui.Post(() => { _agentAsk = (req, tcs); if (CurrentView == page) Show(page); });
            return tcs.Task;
        },
        OnLog = page is null ? null : (r, _) => _ui.Post(() => { AgentLive = r; if (CurrentView == page) Show(page); }),
    };

    /// <summary>Agent speichern und starten. Läuft schon einer, wird abgelehnt (ein Lauf zur Zeit, Stopp bleibt eindeutig).</summary>
    public async Task<AgentRun> StartAgentAsync(AgentDefinition def, string page)
    {
        if (_agentCts is not null) throw new InvalidOperationException("Es läuft schon ein Agent. Erst stoppen oder warten.");
        def = Agents.SaveDefinition(def);
        _agentCts = new CancellationTokenSource();
        var ki = AgentKi(def.Model, out var pref);
        try
        {
            var r = await Agents.RunAgentAsync(def, ki, AgentSvc(page), null, _agentCts.Token, pref);
            AgentLive = r; return r;
        }
        finally { _agentCts.Dispose(); _agentCts = null; _agentAsk = null; }
    }

    /// <summary>Stopp-Knopf: bricht den laufenden Agenten oder das Team ab; eine offene Freigabe-Frage gilt als „Nein“.</summary>
    public void StopAgent()
    {
        _agentCts?.Cancel();
        _agentAsk?.Tcs.TrySetResult(false);
        _core.Audit.Append(Principal.LocalOwner.Name, "agent.stopp", AgentLive?.Id ?? "-");
    }

    /// <summary>Antwort der Person auf die Freigabe-Frage eines laufenden Agenten.</summary>
    public void AnswerAgentApproval(bool yes)
    {
        if (_agentAsk is not { } a) return;
        try { if (yes) yes = FreigabeErteilen(a.Req.Id); else if (CanApprove) _core.Approvals.Deny(a.Req.Id, CurrentPrincipal); else Zurueckziehen(a.Req.Id); } // B4: Rolle der angemeldeten Person
        catch (ApprovalException) { yes = false; }
        _agentAsk = null;
        a.Tcs.TrySetResult(yes);
    }

    /// <summary>T-0698: KI-Team mit Datei-Übergabe (Prüfer kann zurückgeben, Prüfer parallel, Stopp jederzeit). Ohne gewähltes Projekt arbeitet es im Projekt „ki-team“.</summary>
    public async Task<AgentTeamRun?> RunTeamFilesAsync(string goal, string team)
    {
        if (_agentCts is not null) { _agentMsg = "Es läuft schon ein Agent oder Team. Erst stoppen oder warten."; return null; }
        var project = _agentProject;
        if (project is null)
        {
            if (_core.Projects.List().All(p => p.Id != "ki-team")) _core.Projects.Create("ki-team", "KI-Team");
            project = "ki-team";
        }
        _agentBusy = true; _agentCts = new CancellationTokenSource();
        try
        {
            var ki = AgentKi("", out var pref);
            var r = await Agents.RunTeamFilesAsync(team, goal, ki, project, AgentSvc("ki-rollen"), _agentCts.Token, pref);
            _agentMsg = r.State switch
            {
                AgentRunState.Fertig => $"Team fertig: {r.Tasks.Count} Rollen-Läufe, Übergabe-Dateien unter „team/{r.RunId[(r.RunId.LastIndexOf('-') + 1)..]}/“ im Projekt „{_core.Projects.List().First(p => p.Id == project).Name}“" + (r.Returns > 0 ? $" · Prüfer hat {r.Returns}× zurückgegeben." : "."),
                AgentRunState.Gestoppt => $"Team gestoppt nach {r.Tasks.Count} Rollen-Läufen. Fertige Übergabe-Dateien bleiben im Projekt.",
                AgentRunState.Grenze => $"Team beendet: Der Prüfer hat {r.Returns}× zurückgegeben – mehr Runden gibt es nicht. Sieh dir die Übergabe-Dateien an.",
                _ => "Team abgebrochen: " + (r.Tasks.LastOrDefault()?.Result is { Length: > 0 } x ? x : "Eine Rolle hat nicht geklappt (KI nicht erreichbar?)."),
            };
            return r;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ProjectIsolationException or IOException)
        { _agentMsg = "Hat nicht geklappt: " + FriendlyError.Say(ex); return null; }
        finally { _agentCts.Dispose(); _agentCts = null; _agentAsk = null; _agentBusy = false; }
    }
}
