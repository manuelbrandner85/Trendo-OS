using System.Text;
using System.Text.Json;
using Trendo.Core.Kreativ;

namespace Trendo.Core;

/// <summary>T-0741: Zustand einer Rolle in einem Team-Lauf – für die Anzeige „Fortschritt je Rolle“.</summary>
public enum TeamRoleStatus { Wartet, Arbeitet, Fertig, Fehler, Gestoppt }

/// <summary>T-0741: Fortschritt einer Rolle: Zustand, letztes Zwischenergebnis (aus dem Laufprotokoll), Übergabe-Datei.</summary>
public sealed class TeamRoleProgress
{
    public string RoleId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Stage { get; set; }
    public TeamRoleStatus Status { get; set; } = TeamRoleStatus.Wartet;
    /// <summary>Zwischenergebnis: was die Rolle zuletzt getan oder herausgefunden hat.</summary>
    public string Note { get; set; } = "";
    /// <summary>Abschließendes Ergebnis der Rolle (das „fertig“-Feld des Agenten).</summary>
    public string Result { get; set; } = "";
    public string? File { get; set; }
    public string? AgentRunId { get; set; }
    public int Steps { get; set; }
    /// <summary>Wie oft die Rolle schon gearbeitet hat (mehr als 1 nach Rückgabe durch den Prüfer oder nach Fortsetzen).</summary>
    public int Rounds { get; set; }
    public List<string> Tools { get; set; } = [];
}

/// <summary>T-0741: gespeicherter Stand eines Team-Laufs (<c>ki/team-laeufe/&lt;id&gt;.json</c>). Damit bleibt nach einem Stopp sichtbar, was erarbeitet ist,
/// und der Lauf lässt sich an der offenen Stufe fortsetzen.</summary>
public sealed class AgentTeamState
{
    public string Id { get; set; } = "";
    public string TeamId { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string Run { get; set; } = "";
    public string Goal { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string? Model { get; set; }
    public int MaxStepsPerRole { get; set; } = 8;
    public AgentRunState State { get; set; } = AgentRunState.Laeuft;
    /// <summary>Stufe, an der gerade gearbeitet wird bzw. an der ein Fortsetzen beginnt.</summary>
    public int Stage { get; set; }
    /// <summary>In der aktuellen Stufe schon fertige Rollen (parallele Prüfer) – beim Fortsetzen laufen nur die übrigen.</summary>
    public List<string> StageDone { get; set; } = [];
    public int Returns { get; set; }
    public string? Review { get; set; }
    public int Num { get; set; }
    public List<string> Handoffs { get; set; } = [];
    public List<string> TaskIds { get; set; } = [];
    public List<TeamRoleProgress> Roles { get; set; } = [];
    public string? Snapshot { get; set; }
    public DateTimeOffset Started { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? Finished { get; set; }
}

/// <summary>T-0741 (CORE-03 „KI-Team“): Team mit Datei-Übergabe aus T-0698, jetzt mit Fortschritt je Rolle, Stopp mit sichtbarem Zwischenstand, Fortsetzen
/// nach Stopp und Werkzeugen je Rolle (Dateien lesen, Notiz/Datei schreiben, im Internet nachlesen nur nach Freigabe über <see cref="ApprovalGate"/>).
/// Kein zweites Agentensystem: jede Rolle läuft als Agent über <see cref="RunAgentAsync"/>.</summary>
public sealed partial class AgentCenter
{
    /// <summary>Freigabe-Aktion „im Internet nachlesen“ einer Team-Rolle.</summary>
    public const string TeamWebAction = "agent.team.web";
    string TeamRunsDir => Path.Combine(Path.GetDirectoryName(_file)!, "team-laeufe");
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _liveTeams = new();

    /// <summary>Prüfer-Rollen (können zurückgeben, laufen untereinander parallel).</summary>
    public static bool IsReviewer(string roleId) => roleId is "pruefer" or "sicherheit" || roleId.EndsWith(".pruefer", StringComparison.Ordinal);

    /// <summary>Recherche-Rollen bekommen zusätzlich das Werkzeug „Im Internet nachlesen“ – im Team jeder Abruf nur nach Freigabe.</summary>
    public static bool IsResearcher(string roleId) => roleId.Contains("recherch", StringComparison.OrdinalIgnoreCase);

    /// <summary>Stufen eines Teams: aufeinanderfolgende Prüfer bilden eine Stufe und arbeiten parallel (unabhängig voneinander); alle anderen nacheinander.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Stages(IReadOnlyList<string> roles)
    {
        var stages = new List<List<string>>();
        foreach (var r in roles)
            if (IsReviewer(r) && stages.Count > 0 && stages[^1].All(IsReviewer)) stages[^1].Add(r); else stages.Add([r]);
        return stages;
    }

    /// <summary>Werkzeuge einer Team-Rolle (Ids aus <see cref="AgentTools.All"/>).</summary>
    public List<string> TeamTools(string roleId)
    {
        var role = RoleOf(roleId);
        var tools = new List<string> { "lesen" };
        if (role.MayWriteFiles) tools.Add("schreiben");
        if (IsResearcher(roleId)) tools.Add("web");
        return tools;
    }

    // ───────────── gespeicherte Team-Läufe ─────────────
    public IReadOnlyList<AgentTeamState> TeamRuns(string? projectId = null, int max = 20)
    {
        if (!Directory.Exists(TeamRunsDir)) return [];
        var list = new List<AgentTeamState>();
        foreach (var f in Directory.GetFiles(TeamRunsDir, "*.json"))
            try { if (JsonSerializer.Deserialize<AgentTeamState>(File.ReadAllText(f)) is { } s && (projectId is null || s.ProjectId == projectId)) list.Add(Interrupted(s)); }
            catch (Exception ex) when (ex is JsonException or IOException) { }
        return list.OrderByDescending(s => s.Started).Take(max).ToList();
    }

    public AgentTeamState? TeamRun(string id)
    {
        var f = Path.Combine(TeamRunsDir, id + ".json");
        try { return File.Exists(f) && JsonSerializer.Deserialize<AgentTeamState>(File.ReadAllText(f)) is { } s ? Interrupted(s) : null; }
        catch (Exception ex) when (ex is JsonException or IOException) { return null; }
    }

    /// <summary>Ein Lauf, der „läuft“, aber nicht mehr lebt (Trendo wurde beendet), gilt als gestoppt – er lässt sich fortsetzen.</summary>
    AgentTeamState Interrupted(AgentTeamState s)
    {
        if (s.State != AgentRunState.Laeuft || _liveTeams.ContainsKey(s.Id)) return s;
        s.State = AgentRunState.Gestoppt;
        foreach (var r in s.Roles.Where(r => r.Status == TeamRoleStatus.Arbeitet)) { r.Status = TeamRoleStatus.Gestoppt; r.Note = "Unterbrochen (Trendo wurde beendet). " + r.Note; }
        return s;
    }

    void SaveTeam(AgentTeamState s, Action<AgentTeamState>? onProgress)
    {
        Directory.CreateDirectory(TeamRunsDir);
        lock (s) AtomicFile.WriteAllText(Path.Combine(TeamRunsDir, s.Id + ".json"), JsonSerializer.Serialize(s)); // parallele Prüfer: Stand und Schreiben unter einer Sperre
        try { onProgress?.Invoke(s); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    /// <summary>Lässt sich dieser Team-Lauf fortsetzen? Ja, wenn er gestoppt oder an einem Fehler hängengeblieben ist und gerade nicht läuft.</summary>
    public bool CanResumeTeam(string id) =>
        TeamRun(id) is { } s && !_liveTeams.ContainsKey(id) && s.State is AgentRunState.Gestoppt or AgentRunState.Fehler && Team(s.TeamId) is not null
        && _projects.List().Any(p => p.Id == s.ProjectId);

    AgentTeamRun TeamResult(AgentTeamState s)
    {
        var tasks = s.TaskIds.Select(Get).OfType<AgentTask>().ToList();
        var team = Team(s.TeamId);
        return new(s.Id, s.State, tasks, s.Handoffs.ToList(), s.Returns, team is null ? [] : Stages(team.Value.Roles));
    }

    // ───────────── Lauf ─────────────
    /// <summary>Team-Lauf mit echten Dateien: Jede Rolle arbeitet als Agent im Projekt (Werkzeuge siehe <see cref="TeamTools"/>) und gibt ihr Ergebnis als Datei
    /// unter „team/&lt;lauf&gt;/“ weiter. Prüfer antworten mit „OK …“ oder „ZURÜCK: …“ – dann arbeitet die letzte bearbeitende Stufe mit dem Prüfbericht nach
    /// (höchstens <see cref="MaxTeamReturns"/>-mal). Stopp über <paramref name="ct"/>; der Stand liegt danach gespeichert vor (<see cref="TeamRun"/>) und lässt sich
    /// mit <see cref="ResumeTeamFilesAsync"/> fortsetzen. <paramref name="onProgress"/> meldet jede Änderung je Rolle.</summary>
    public async Task<AgentTeamRun> RunTeamFilesAsync(string teamId, string goal, IntelligenceCore ki, string projectId, AgentServices? svc = null, CancellationToken ct = default, string? model = null,
        int maxStepsPerRole = 8, Action<AgentTeamState>? onProgress = null)
    {
        var team = Team(teamId) ?? throw new InvalidOperationException("Dieses Team gibt es nicht: " + teamId);
        if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Ziel fehlt");
        if (_projects.List().All(p => p.Id != projectId)) throw new ProjectIsolationException("Projekt unbekannt");
        var run = Guid.NewGuid().ToString("N")[..6];
        var stages = Stages(team.Roles);
        var st = new AgentTeamState
        {
            Id = $"{teamId.Replace(':', '-')}-{run}", TeamId = teamId, TeamName = team.Name, Run = run, Goal = goal.Trim(), ProjectId = projectId, Model = model, MaxStepsPerRole = maxStepsPerRole,
            Roles = [.. stages.SelectMany((s, i) => s.Select(r => new TeamRoleProgress { RoleId = r, Name = RoleOf(r).Name, Stage = i, Tools = TeamTools(r) }))],
        };
        _audit.Append(Principal.LocalOwner.Name, "agent.team.dateien.start", $"{teamId}-{run} stufen={stages.Count}");
        return await ContinueTeamAsync(st, ki, svc, ct, model, onProgress);
    }

    /// <summary>Gestoppten oder an einem Fehler hängengebliebenen Team-Lauf fortsetzen: fertige Rollen und ihre Übergabe-Dateien bleiben, weiter geht es an der offenen Stufe
    /// (in einer Prüfer-Stufe nur mit den Prüfern, die noch nicht fertig waren).</summary>
    public async Task<AgentTeamRun> ResumeTeamFilesAsync(string id, IntelligenceCore ki, AgentServices? svc = null, CancellationToken ct = default, string? model = null, Action<AgentTeamState>? onProgress = null)
    {
        if (!CanResumeTeam(id)) throw new InvalidOperationException("Dieser Team-Lauf lässt sich nicht fortsetzen (fertig, läuft gerade oder unbekannt).");
        var st = TeamRun(id)!;
        foreach (var r in st.Roles.Where(r => r.Status is TeamRoleStatus.Gestoppt or TeamRoleStatus.Fehler)) r.Status = TeamRoleStatus.Wartet;
        _audit.Append(Principal.LocalOwner.Name, "agent.team.fortsetzen", $"{id} ab Stufe {st.Stage + 1}");
        return await ContinueTeamAsync(st, ki, svc, ct, model ?? st.Model, onProgress);
    }

    async Task<AgentTeamRun> ContinueTeamAsync(AgentTeamState st, IntelligenceCore ki, AgentServices? svc, CancellationToken ct, string? model, Action<AgentTeamState>? onProgress)
    {
        var team = Team(st.TeamId) ?? throw new InvalidOperationException("Dieses Team gibt es nicht: " + st.TeamId);
        var stages = Stages(team.Roles);
        _liveTeams[st.Id] = 0;
        var state = AgentRunState.Fertig;
        try
        {
            lock (st) { st.State = AgentRunState.Laeuft; st.Finished = null; }
            SaveTeam(st, onProgress);
            for (var si = st.Stage; si < stages.Count; si++)
            {
                lock (st) st.Stage = si;
                if (ct.IsCancellationRequested || _gate.Halted) { state = AgentRunState.Gestoppt; break; }
                var todo = stages[si].Where(r => !st.StageDone.Contains(r)).ToList();
                List<string> before; lock (st) before = st.Handoffs.ToList();
                var reviewNow = st.Review;
                var results = await Task.WhenAll(todo.Select(roleId =>
                {
                    int num; lock (st) num = ++st.Num;
                    return RunTeamRoleAsync(st, roleId, num, before, reviewNow, ki, svc, ct, model, onProgress);
                }).ToList()); // Prüfer einer Stufe parallel, sonst genau eine Rolle
                lock (st)
                    foreach (var (task, file, runState) in results)
                    {
                        st.TaskIds.Add(task.Id);
                        if (runState == AgentRunState.Fertig) { st.Handoffs.Add(file); st.StageDone.Add(task.RoleId); }
                    }
                SaveTeam(st, onProgress);
                if (results.Any(r => r.State == AgentRunState.Gestoppt)) { state = AgentRunState.Gestoppt; break; }
                if (results.Any(r => r.State is AgentRunState.Fehler or AgentRunState.Grenze)) { state = AgentRunState.Fehler; break; }
                // Stufe vollständig
                lock (st) { st.StageDone.Clear(); st.Review = null; }
                if (stages[si].All(IsReviewer))
                {
                    var back = st.Roles.Where(p => p.Stage == si && p.Result.TrimStart().StartsWith("ZURÜCK", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (back.Count > 0 && st.Returns < MaxTeamReturns)
                    {
                        var target = Enumerable.Range(0, si).Reverse().FirstOrDefault(i => !stages[i].All(IsReviewer), -1);
                        lock (st) { st.Returns++; st.Review = string.Join(", ", back.Select(b => b.File)); }
                        _audit.Append(Principal.LocalOwner.Name, "agent.team.zurueck", $"{st.Id} runde={st.Returns} an={(target < 0 ? "-" : string.Join("+", stages[target]))}");
                        if (target >= 0)
                        {
                            lock (st)
                                foreach (var p in st.Roles.Where(p => p.Stage >= target)) { p.Status = TeamRoleStatus.Wartet; if (p.Stage > target) p.Note = "wartet auf die überarbeitete Fassung"; }
                            si = target - 1; SaveTeam(st, onProgress); continue;
                        }
                    }
                    else if (back.Count > 0) { state = AgentRunState.Grenze; _audit.Append(Principal.LocalOwner.Name, "agent.team.zurueck.grenze", st.Id); break; }
                }
                lock (st) st.Stage = si + 1;
                SaveTeam(st, onProgress);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            state = ex is OperationCanceledException ? AgentRunState.Gestoppt : AgentRunState.Fehler;
            _audit.Append(Principal.LocalOwner.Name, "agent.team.fehler", $"{st.Id} {ex.GetType().Name}");
        }
        finally
        {
            lock (st) { st.State = state; st.Finished = DateTimeOffset.Now; }
            _liveTeams.TryRemove(st.Id, out _);
            SaveTeam(st, onProgress);
            _audit.Append(Principal.LocalOwner.Name, "agent.team.dateien.ende", $"{st.Id} zustand={state} rollen={st.TaskIds.Count} zurueck={st.Returns}");
        }
        return TeamResult(st);
    }

    async Task<(AgentTask Task, string File, AgentRunState State)> RunTeamRoleAsync(AgentTeamState st, string roleId, int num, IReadOnlyList<string> before, string? review,
        IntelligenceCore ki, AgentServices? svc, CancellationToken ct, string? model, Action<AgentTeamState>? onProgress)
    {
        var role = RoleOf(roleId);
        var p = st.Roles.FirstOrDefault(x => x.RoleId == roleId) ?? new TeamRoleProgress { RoleId = roleId, Name = role.Name };
        var file = $"team/{st.Run}/{num:00}-{roleId.Replace('.', '-')}.md";
        var input = (before.Count > 0 ? "Übergabe-Dateien der Vorgänger (mit datei.lesen öffnen): " + string.Join(", ", before) : "Du beginnst – es gibt noch keine Übergabe-Dateien.")
            + (review is not null ? $"\nDer Prüfer hat zurückgegeben. Lies seinen Bericht ({review}) und verbessere die Arbeit entsprechend." : "");
        var t = Create(roleId, st.Goal, st.ProjectId, input, $"dateien:{st.Id}");
        t.State = AgentTaskState.Laeuft; Save();
        var tools = TeamTools(roleId);
        lock (st) { p.Status = TeamRoleStatus.Arbeitet; p.Rounds++; p.Steps = 0; p.Note = "beginnt …"; p.Tools = tools; p.File = null; p.Result = ""; }
        SaveTeam(st, onProgress);
        var def = new AgentDefinition
        {
            Id = AgentBlueprint.Slug(role.Name) is { Length: >= 2 } s ? s : "rolle", Name = role.Name, Goal = st.Goal, Tools = tools, MaxSteps = st.MaxStepsPerRole, MaxMinutes = 15, ProjectId = st.ProjectId,
            Instructions = role.SystemPrompt + "\nDu arbeitest in einem Team. Lies zuerst die Übergabe-Dateien. Dein Ergebnis (das „fertig“-Feld) wird als Übergabe-Datei für die nächste Rolle gespeichert."
                // Echtlauf T-0741 (qwen3:14b, Erstversuch): ein Planer ohne Schreibrecht versuchte 8 Schritte lang, die Datei des Texters anzulegen – darum die Zuständigkeit klar nennen
                + $"\nDu bist nur für deinen Teil als {role.Name} zuständig ({role.Does}). Was im Ziel anderen Rollen zugedacht ist, überlässt du ihnen. Sobald dein Teil steht, antworte mit dem „fertig“-Feld."
                + (tools.Contains("schreiben") ? "\nNotizen und Zwischenstände legst du mit datei.schreiben als Datei im Projekt ab."
                    : "\nDu darfst keine Dateien schreiben: Schreib dein Ergebnis vollständig ins „fertig“-Feld.")
                + (tools.Contains("web") ? "\nIm Internet nachlesen (web.lesen) geht nur, wenn die Person zustimmt – frag nur, wenn es nötig ist." : "")
                + (IsReviewer(roleId) ? "\nDu bist Prüfer: Beginne dein „fertig“-Ergebnis mit „OK:“, wenn die Arbeit passt, sonst mit „ZURÜCK:“ und nenne konkret, was verbessert werden muss." : ""),
        };
        var roleSvc = new AgentServices
        {
            ReadWeb = TeamWeb(svc, role.Name, st, p, onProgress), Studio = svc?.Studio, AskApproval = svc?.AskApproval, ExportAllowed = svc?.ExportAllowed,
            OnLog = (r, e) =>
            {
                lock (st)
                {
                    p.AgentRunId = r.Id; p.Steps = r.Steps;
                    if (e.Kind != "start") p.Note = NoteFor(e);
                }
                SaveTeam(st, onProgress);
                svc?.OnLog?.Invoke(r, e);
            },
        };
        AgentRun r;
        try { r = await RunAgentAsync(def, ki, roleSvc, input, ct, model); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            t.Result = FriendlyError.Say(ex); t.State = AgentTaskState.Fehler; t.Finished = DateTimeOffset.Now; Save();
            lock (st) { p.Status = TeamRoleStatus.Fehler; p.Note = t.Result; }
            SaveTeam(st, onProgress);
            return (t, file, AgentRunState.Fehler);
        }
        // Was die Rolle bis hier erarbeitet hat, bleibt – auch nach Stopp: Ergebnis oder Zwischenstand als Übergabe-Datei
        var body = new StringBuilder($"# {role.Name}\n\n");
        if (r.Result.Length > 0) body.Append(r.Result).Append('\n');
        else
        {
            body.Append("(kein abschließendes Ergebnis – ").Append(StateText(r.State)).Append(")\n\n## Bisher erarbeitet\n\n");
            List<AgentLogEntry> log; lock (r) log = [.. r.Log];
            var work = log.Where(e => e.Kind is "plan" or "werkzeug" or "ergebnis" or "blockiert" or "freigabe").TakeLast(12).ToList();
            if (work.Count == 0) body.Append("- noch nichts\n");
            foreach (var e in work) body.Append("- ").Append(e.Kind).Append(": ").Append(Short(e.Text.Replace("\n", " "), 600)).Append('\n');
        }
        if (r.FilesWritten.Count > 0) body.Append($"\nGeschriebene Dateien: {string.Join(", ", r.FilesWritten.Distinct())}\n");
        _projects.WriteFile(st.ProjectId, file, body.ToString());
        t.Result = r.Result; t.SavedAs = file; t.Finished = DateTimeOffset.Now;
        t.State = r.State == AgentRunState.Fertig ? AgentTaskState.Fertig : AgentTaskState.Fehler; Save();
        lock (st)
        {
            p.Result = r.Result; p.File = file; p.Steps = r.Steps; p.AgentRunId = r.Id;
            p.Status = r.State switch { AgentRunState.Fertig => TeamRoleStatus.Fertig, AgentRunState.Gestoppt => TeamRoleStatus.Gestoppt, _ => TeamRoleStatus.Fehler };
            if (r.Result.Length > 0) p.Note = Short(r.Result.Replace("\n", " "), 300);
            else if (r.Log.LastOrDefault() is { } last) p.Note = NoteFor(last);
        }
        SaveTeam(st, onProgress);
        return (t, file, r.State);
    }

    static string NoteFor(AgentLogEntry e)
    {
        var lead = e.Kind switch
        {
            "werkzeug" => "nutzt Werkzeug: ", "ergebnis" => "Zwischenergebnis: ", "plan" => "plant: ", "freigabe" => "", "blockiert" => "blockiert: ",
            "fehler" => "Fehler: ", "fertig" => "fertig: ", "stopp" => "", "grenze" => "", _ => "",
        };
        return Short(lead + e.Text.Replace("\n", " "), 300);
    }

    /// <summary>„Im Internet nachlesen“ für eine Team-Rolle: jeder Abruf wird beim <see cref="ApprovalGate"/> angefragt und nur nach Zustimmung der Person ausgeführt.
    /// Ohne Person (kein <see cref="AgentServices.AskApproval"/>) oder bei „Nein“ blockiert das Tor und es wird nichts abgerufen.</summary>
    Func<Uri, CancellationToken, Task<(string Type, byte[] Bytes, Uri Final)>>? TeamWeb(AgentServices? svc, string roleName, AgentTeamState st, TeamRoleProgress p, Action<AgentTeamState>? onProgress)
    {
        if (svc?.ReadWeb is not { } read) return null;
        return async (u, ct) =>
        {
            var target = u.AbsoluteUri;
            var req = _gate.Request(Principal.LocalOwner, TeamWebAction, $"KI-Team: {roleName} möchte im Internet nachlesen ({u.Host})", target, TimeSpan.FromMinutes(30), target);
            lock (st) p.Note = $"wartet auf deine Freigabe: im Internet nachlesen ({u.Host})";
            SaveTeam(st, onProgress);
            var yes = svc.AskApproval is { } ask && await ask(req, ct);
            ct.ThrowIfCancellationRequested();
            if (!yes)
            {
                try { _gate.Execute(req.Id, TeamWebAction, target, () => true); } catch (ApprovalException) { } // Tor blockiert und schreibt es ins Prüfprotokoll
                throw new ApprovalException(svc.AskApproval is null
                    ? "Blockiert: Niemand konnte zustimmen. Es wurde nichts aus dem Internet abgerufen."
                    : "Blockiert: Die Person hat dem Abruf nicht zugestimmt. Es wurde nichts aus dem Internet abgerufen.");
            }
            return await _gate.Execute(req.Id, TeamWebAction, target, () => read(u, ct));
        };
    }

    /// <summary>Zwischenstand eines Team-Laufs als Datei im Projekt speichern („team/&lt;lauf&gt;/zwischenstand.md“): Ziel, Zustand, je Rolle Zustand, Zwischenergebnis,
    /// Ergebnis und Übergabe-Datei. Gibt den Pfad im Projekt zurück.</summary>
    public string SaveTeamSnapshot(string id)
    {
        var s = TeamRun(id) ?? throw new InvalidOperationException("Diesen Team-Lauf gibt es nicht.");
        var sb = new StringBuilder();
        sb.Append("# KI-Team „").Append(s.TeamName).Append("“ – Zwischenstand\n\n");
        sb.Append("- Lauf: ").Append(s.Id).Append('\n');
        sb.Append("- Zustand: ").Append(StateText(s.State)).Append('\n');
        sb.Append("- Gestartet: ").Append(s.Started.ToString("yyyy-MM-dd HH:mm:ss")).Append(s.Finished is { } fin ? ", zuletzt: " + fin.ToString("HH:mm:ss") : "").Append('\n');
        if (s.Returns > 0) sb.Append("- Prüfer hat ").Append(s.Returns).Append("× zurückgegeben\n");
        sb.Append("\n## Ziel\n\n").Append(s.Goal).Append("\n\n## Rollen\n");
        foreach (var r in s.Roles)
        {
            sb.Append("\n### ").Append(r.Name).Append(" – ").Append(StatusText(r.Status)).Append('\n');
            sb.Append("\n- Werkzeuge: ").Append(string.Join(", ", r.Tools.Select(AgentTools.Label))).Append('\n');
            if (r.Steps > 0) sb.Append("- Schritte: ").Append(r.Steps).Append('\n');
            if (r.File is { } f) sb.Append("- Übergabe-Datei: ").Append(f).Append('\n');
            if (r.Note.Length > 0 && r.Note != r.Result) sb.Append("- Zuletzt: ").Append(r.Note).Append('\n');
            if (r.Result.Length > 0) sb.Append("\n").Append(r.Result.Trim()).Append('\n');
        }
        var rel = $"team/{s.Run}/zwischenstand.md";
        _projects.WriteFile(s.ProjectId, rel, sb.ToString());
        s.Snapshot = rel;
        lock (s) AtomicFile.WriteAllText(Path.Combine(TeamRunsDir, s.Id + ".json"), JsonSerializer.Serialize(s));
        _audit.Append(Principal.LocalOwner.Name, "agent.team.zwischenstand", $"{s.Id} -> {rel}");
        return rel;
    }

    /// <summary>Zustand einer Rolle in Alltagswörtern.</summary>
    public static string StatusText(TeamRoleStatus s) => s switch
    {
        TeamRoleStatus.Arbeitet => "arbeitet", TeamRoleStatus.Fertig => "fertig", TeamRoleStatus.Fehler => "Fehler", TeamRoleStatus.Gestoppt => "gestoppt", _ => "wartet",
    };
}
