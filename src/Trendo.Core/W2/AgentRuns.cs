using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;

namespace Trendo.Core;

/// <summary>T-0698: Zustand eines Agenten-Laufs.</summary>
public enum AgentRunState { Laeuft, Fertig, Gestoppt, Grenze, Fehler }

/// <summary>T-0698: ein Agent, der in Trendo wirklich läuft – Ziel, Anweisungen, erlaubte Werkzeuge aus <see cref="AgentTools.All"/>, Modell und Grenzen.
/// Ergebnisse landen im eigenen Projektordner (<see cref="ProjectId"/>).</summary>
public sealed class AgentDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Goal { get; set; } = "";
    public string Instructions { get; set; } = "";
    public List<string> Tools { get; set; } = ["lesen", "schreiben"];
    /// <summary>„“ = Haupt-KI von Trendo, „lokal“ bzw. „lokal:&lt;modell&gt;“ = nur KI auf diesem PC, „claude“ = Claude über das eigene Abo.</summary>
    public string Model { get; set; } = "";
    public int MaxSteps { get; set; } = 12;
    public int MaxMinutes { get; set; } = 10;
    public string? ProjectId { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
}

/// <summary>Ein Eintrag im Laufprotokoll. <paramref name="Kind"/>: start, plan, werkzeug, ergebnis, freigabe, blockiert, fehler, fertig, stopp, grenze.</summary>
public sealed record AgentLogEntry(int Step, DateTimeOffset At, string Kind, string Text);

public sealed class AgentRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string AgentId { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Goal { get; set; } = "";
    public AgentRunState State { get; set; } = AgentRunState.Laeuft;
    public List<AgentLogEntry> Log { get; set; } = [];
    public List<string> FilesWritten { get; set; } = [];
    public string Result { get; set; } = "";
    public string? ResultFile { get; set; }
    public string? Backend { get; set; }
    public string? Model { get; set; }
    public int Steps { get; set; }
    public DateTimeOffset Started { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? Finished { get; set; }
}

/// <summary>Werkzeug, das ein Agent nutzen darf. <paramref name="External"/>: Wirkung außerhalb des Projekts – nur nach Freigabe über <see cref="ApprovalGate"/>.</summary>
public sealed record AgentTool(string Id, string Label, string[] Calls, string Help, bool External);

/// <summary>T-0698: die feste Liste der Trendo-Fähigkeiten für Agenten. Nichts sonst ist aufrufbar.</summary>
public static class AgentTools
{
    public static readonly IReadOnlyList<AgentTool> All =
    [
        new("lesen", "Dateien im Projekt lesen", ["datei.lesen", "dateien.liste"],
            "datei.lesen {\"pfad\":\"notiz.md\"} liest eine Datei im Projekt; dateien.liste {} zeigt alle Dateien im Projekt.", false),
        new("schreiben", "Dateien im Projekt schreiben", ["datei.schreiben"],
            "datei.schreiben {\"pfad\":\"bericht.md\",\"inhalt\":\"…\"} legt eine Datei im Projekt an oder ersetzt sie.", false),
        new("web", "Im Internet nachlesen", ["web.lesen"],
            "web.lesen {\"adresse\":\"https://…\"} liest eine öffentliche Seite im Lesemodus (nichts wird heruntergeladen oder angemeldet).", false),
        new("studio", "Studio-Aktionen", ["studio.aktion"],
            "studio.aktion {\"studio\":\"STUDIO-ID\",\"aktion\":\"aktion.id\",\"auftrag\":\"…\"} lässt ein Trendo-Studio eine Fachaktion im Projekt ausführen.", false),
        new("mail", "E-Mail-Entwurf anlegen", ["email.entwurf"],
            "email.entwurf {\"an\":\"name@example.org\",\"betreff\":\"…\",\"text\":\"…\"} legt einen Entwurf als .eml im Projekt ab. Er wird nie gesendet.", false),
        new("export", "Datei aus dem Projekt hinausgeben", ["datei.exportieren"],
            "datei.exportieren {\"pfad\":\"bericht.md\",\"ziel\":\"C:\\\\Ordner\"} kopiert eine Projektdatei in einen Ordner außerhalb – nur nach Zustimmung der Person.", true),
    ];

    public static AgentTool? ForCall(string call) => All.FirstOrDefault(t => t.Calls.Contains(call));
    public static string Label(string id) => All.FirstOrDefault(t => t.Id == id)?.Label ?? id;
}

/// <summary>T-0698: angeschlossene Dienste für Agenten-Werkzeuge. Fehlt einer, meldet das Werkzeug ehrlich „nicht angeschlossen“.</summary>
public sealed class AgentServices
{
    /// <summary>Lesemodus-Abruf (in der App: <c>ReadOnlyWeb.GetAsync</c> – dieselbe Prüfung wie die Recherche).</summary>
    public Func<Uri, CancellationToken, Task<(string Type, byte[] Bytes, Uri Final)>>? ReadWeb { get; init; }
    /// <summary>Studio-Aktionen über den gemeinsamen Prompt-zu-Auftrag-Ablauf.</summary>
    public StudioWorkflow? Studio { get; init; }
    /// <summary>Fragt die Person nach einer Freigabe (die Oberfläche zeigt sie und ruft <see cref="ApprovalGate.Approve"/>). Null = niemand da → blockiert.</summary>
    public Func<ApprovalRequest, CancellationToken, Task<bool>>? AskApproval { get; init; }
    /// <summary>Darf in diesen Ordner exportiert werden? Null = nirgendwohin.</summary>
    public Func<string, bool>? ExportAllowed { get; init; }
    /// <summary>Live-Protokoll für die Oberfläche.</summary>
    public Action<AgentRun, AgentLogEntry>? OnLog { get; init; }
}

/// <summary>Ergebnis eines Team-Laufs mit Datei-Übergabe.</summary>
public sealed record AgentTeamRun(string RunId, AgentRunState State, IReadOnlyList<AgentTask> Tasks, IReadOnlyList<string> HandoffFiles, int Returns, IReadOnlyList<IReadOnlyList<string>> Stages);

/// <summary>T-0698 (STUDIO-AGENTS-01, CORE-03): Agenten laufen wirklich – Schleife Plan → Werkzeug → Ergebnis mit Protokoll, Grenzen, Stopp und Freigabe-Tor.
/// Kein zweites Laufzeitsystem: Teil von <see cref="AgentCenter"/>, KI über <see cref="IntelligenceCore"/>, Wirkungen nach außen über <see cref="ApprovalGate"/>,
/// Zeitplan/Auslöser über die Abläufe (<see cref="WorkflowEngine"/>, Schritt „Agent ausführen“).</summary>
public sealed partial class AgentCenter
{
    public const int MaxStepsLimit = 50, MaxMinutesLimit = 120, MaxReadChars = 60_000, MaxWriteChars = 1_000_000, MaxTeamReturns = 2;

    string DefsFile => Path.Combine(Path.GetDirectoryName(_file)!, "agenten-definitionen.json");
    string RunsDir => Path.Combine(Path.GetDirectoryName(_file)!, "agent-laeufe");
    private readonly object _defLock = new();

    // ───────────── Agenten (Definitionen) ─────────────
    public IReadOnlyList<AgentDefinition> Definitions()
    {
        lock (_defLock)
            try { return File.Exists(DefsFile) ? JsonSerializer.Deserialize<List<AgentDefinition>>(File.ReadAllText(DefsFile)) ?? [] : []; }
            catch (JsonException) { return []; }
    }

    public AgentDefinition? Definition(string id) => Definitions().FirstOrDefault(d => d.Id == id);

    /// <summary>Prüft und speichert einen Agenten (gleiche Id = ersetzen). Ohne Projekt bekommt er ein eigenes („agent-&lt;id&gt;“).</summary>
    public AgentDefinition SaveDefinition(AgentDefinition def)
    {
        def.Name = (def.Name ?? "").Trim(); def.Goal = (def.Goal ?? "").Trim();
        if (def.Name.Length == 0) throw new ArgumentException("Gib dem Agenten einen Namen.");
        if (def.Goal.Length == 0) throw new ArgumentException("Schreib das Ziel des Agenten in ein, zwei Sätzen.");
        if (AgentBlueprint.HasSecret(def.Goal) || AgentBlueprint.HasSecret(def.Instructions)) throw new ArgumentException("Im Ziel oder in den Anweisungen steht etwas, das wie ein Schlüssel oder Passwort aussieht – bitte entfernen.");
        def.Tools = AgentTools.All.Where(t => def.Tools.Contains(t.Id)).Select(t => t.Id).ToList();
        if (def.Tools.Count == 0) throw new ArgumentException("Wähle mindestens ein Werkzeug.");
        if (def.MaxSteps is < 1 or > MaxStepsLimit) throw new ArgumentException($"Schritte bitte zwischen 1 und {MaxStepsLimit}.");
        if (def.MaxMinutes is < 1 or > MaxMinutesLimit) throw new ArgumentException($"Minuten bitte zwischen 1 und {MaxMinutesLimit}.");
        if (def.Model is not ("" or "lokal" or "claude") && !def.Model.StartsWith("lokal:", StringComparison.Ordinal)) throw new ArgumentException("Unbekanntes Modell.");
        if (def.Id.Length == 0) def.Id = AgentBlueprint.Slug(def.Name) is { Length: >= 2 } s ? s : "agent";
        if (!Regex.IsMatch(def.Id, "^[a-z][a-z0-9-]{1,39}$")) throw new ArgumentException("Ungültige Agenten-Kennung.");
        if (def.ProjectId is null || _projects.List().All(p => p.Id != def.ProjectId))
        {
            var pid = "agent-" + def.Id; if (pid.Length > 48) pid = pid[..48].TrimEnd('-');
            if (_projects.List().All(p => p.Id != pid)) _projects.Create(pid, "Agent " + def.Name);
            def.ProjectId = pid;
        }
        lock (_defLock)
        {
            var list = Definitions().ToList();
            var i = list.FindIndex(d => d.Id == def.Id);
            if (i >= 0) { def.Created = list[i].Created; list[i] = def; } else list.Add(def);
            AtomicFile.WriteAllText(DefsFile, JsonSerializer.Serialize(list));
        }
        _audit.Append(Principal.LocalOwner.Name, "agent.definition", $"{def.Id} werkzeuge={string.Join(",", def.Tools)} schritte={def.MaxSteps} minuten={def.MaxMinutes}");
        return def;
    }

    public bool RemoveDefinition(string id)
    {
        lock (_defLock)
        {
            var list = Definitions().ToList(); var n = list.RemoveAll(d => d.Id == id);
            if (n > 0) { AtomicFile.WriteAllText(DefsFile, JsonSerializer.Serialize(list)); _audit.Append(Principal.LocalOwner.Name, "agent.definition.entfernt", id); }
            return n > 0;
        }
    }

    // ───────────── Läufe ─────────────
    public IReadOnlyList<AgentRun> Runs(string? agentId = null, int max = 30)
    {
        if (!Directory.Exists(RunsDir)) return [];
        var list = new List<AgentRun>();
        foreach (var f in Directory.GetFiles(RunsDir, "*.json"))
            try { if (JsonSerializer.Deserialize<AgentRun>(File.ReadAllText(f)) is { } r && (agentId is null || r.AgentId == agentId)) list.Add(r); }
            catch (Exception ex) when (ex is JsonException or IOException) { }
        foreach (var r in list.Where(r => r.State == AgentRunState.Laeuft && !_live.ContainsKey(r.Id))) { r.State = AgentRunState.Fehler; r.Log.Add(new(r.Steps, DateTimeOffset.Now, "fehler", "Unterbrochen (Trendo wurde beendet).")); }
        return list.OrderByDescending(r => r.Started).Take(max).ToList();
    }

    public AgentRun? GetRun(string id) => Runs(null, int.MaxValue).FirstOrDefault(r => r.Id == id);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _live = new();

    void SaveRun(AgentRun r) { lock (r) AtomicFile.WriteAllText(Path.Combine(RunsDir, r.Id + ".json"), JsonSerializer.Serialize(r)); }

    void Log(AgentRun r, AgentServices? svc, string kind, string text)
    {
        var e = new AgentLogEntry(r.Steps, DateTimeOffset.Now, kind, text.Length > 2000 ? text[..2000] + " …" : text);
        lock (r) r.Log.Add(e);
        SaveRun(r);
        try { svc?.OnLog?.Invoke(r, e); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    /// <summary>Werkzeug-Aufruf aus der Antwort der KI. Form: {"gedanke":"…","werkzeug":"datei.lesen","argumente":{…}} oder {"gedanke":"…","fertig":"…"}.</summary>
    public sealed record ToolCall(string? Thought, string? Tool, JsonObject Args, string? Done);

    public static ToolCall? ParseCall(string answer)
    {
        if (AgentAnswer.ExtractJson(answer) is not { } json) return null;
        JsonObject? o;
        try { o = JsonNode.Parse(json) as JsonObject; } catch (JsonException) { return null; }
        if (o is null) return null;
        string? S(string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : o[k]?.ToJsonString();
        var done = S("fertig"); var tool = S("werkzeug");
        if (done is null && tool is null) return null;
        var args = o["argumente"] as JsonObject ?? [];
        return new(S("gedanke"), tool?.Trim(), (JsonObject)args.DeepClone(), done);
    }

    static string SystemFor(AgentDefinition def, string projectName)
    {
        var sb = new StringBuilder();
        sb.Append("Du bist der Agent „").Append(def.Name).Append("“ in Trendo OS. Du arbeitest Schritt für Schritt an einem Ziel und nutzt dafür Werkzeuge.\n");
        sb.Append("Ziel: ").Append(def.Goal).Append('\n');
        if (def.Instructions.Trim().Length > 0) sb.Append("Anweisungen:\n").Append(def.Instructions.Trim()).Append('\n');
        sb.Append("Dein Arbeitsordner ist das Projekt „").Append(projectName).Append("“. Pfade sind immer relativ zu diesem Projekt.\n");
        sb.Append("Erlaubte Werkzeuge:\n");
        foreach (var t in AgentTools.All.Where(t => def.Tools.Contains(t.Id))) sb.Append("- ").Append(t.Help).Append('\n');
        sb.Append("Antworte in JEDEM Schritt nur mit genau einem JSON-Objekt, ohne Text davor oder danach:\n");
        sb.Append("{\"gedanke\":\"was du als Nächstes tust\",\"werkzeug\":\"name\",\"argumente\":{…}}\n");
        sb.Append("Wenn das Ziel erreicht ist: {\"gedanke\":\"…\",\"fertig\":\"kurze Zusammenfassung des Ergebnisses\"}\n");
        sb.Append("Erfinde keine Werkzeugergebnisse – du bekommst das echte Ergebnis als nächste Nachricht. Antworte auf Deutsch.");
        return sb.ToString();
    }

    /// <summary>Einen Agenten ausführen: Plan → Werkzeug → Ergebnis, bis „fertig“, Stopp (<paramref name="ct"/> oder Not-Stopp) oder eine Grenze (Schritte, Zeit) greift.
    /// Jede Werkzeugnutzung steht im Protokoll; das Ergebnis liegt als Datei im Projekt unter „ergebnisse/“.</summary>
    public async Task<AgentRun> RunAgentAsync(AgentDefinition def, IntelligenceCore ki, AgentServices? svc = null, string? input = null, CancellationToken ct = default, string? model = null, string? runId = null)
    {
        if (def.ProjectId is null || _projects.List().All(p => p.Id != def.ProjectId)) throw new ProjectIsolationException("Der Agent hat kein Projekt. Erst speichern.");
        var project = _projects.List().First(p => p.Id == def.ProjectId);
        var run = new AgentRun { AgentId = def.Id, AgentName = def.Name, ProjectId = def.ProjectId, Goal = def.Goal };
        if (runId is not null) run.Id = runId;
        Directory.CreateDirectory(RunsDir);
        _live[run.Id] = 0;
        _audit.Append("agent:" + def.Id, "agent.lauf.start", $"{run.Id} projekt={def.ProjectId} werkzeuge={string.Join(",", def.Tools)}");
        Log(run, svc, "start", $"Ziel: {def.Goal}" + (input is { Length: > 0 } ? $"\nEingabe: {input}" : "") + $"\nWerkzeuge: {string.Join(", ", def.Tools.Select(AgentTools.Label))} · höchstens {def.MaxSteps} Schritte, {def.MaxMinutes} Minuten");
        using var time = new CancellationTokenSource(TimeSpan.FromMinutes(def.MaxMinutes));
        using var all = CancellationTokenSource.CreateLinkedTokenSource(ct, _gate.HaltToken, time.Token);
        var history = new List<KiMessage>();
        var question = $"Ziel: {def.Goal}" + (input is { Length: > 0 } ? $"\n\nEingabe:\n{input}" : "") + "\n\nWas ist dein erster Schritt? Antworte nur mit JSON.";
        var system = SystemFor(def, project.Name);
        var noJson = 0;
        try
        {
            while (true)
            {
                if (run.Steps >= def.MaxSteps) { run.State = AgentRunState.Grenze; Log(run, svc, "grenze", $"Schrittgrenze erreicht ({def.MaxSteps} Schritte). Der Agent hat aufgehört."); break; }
                all.Token.ThrowIfCancellationRequested();
                run.Steps++;
                var a = await ki.AskAsync(new KiRequest(question, system, model, history.ToList(), UseKnowledge: false), all.Token);
                all.Token.ThrowIfCancellationRequested();
                if (!a.Ok) { run.State = AgentRunState.Fehler; Log(run, svc, "fehler", a.Text); break; }
                run.Backend = a.Backend; run.Model = a.Model;
                history.Add(new("user", question)); history.Add(new("assistant", a.Text));
                var call = ParseCall(a.Text);
                if (call is null)
                {
                    noJson++;
                    Log(run, svc, "plan", "Antwort ohne Werkzeugaufruf: " + Short(a.Text, 400));
                    if (noJson >= 2) { run.Result = a.Text.Trim(); run.State = AgentRunState.Fertig; Log(run, svc, "fertig", "Die KI hat ohne Werkzeug geantwortet – ihre Antwort ist das Ergebnis."); break; }
                    question = "Bitte antworte nur mit einem JSON-Objekt in der verlangten Form (werkzeug + argumente oder fertig).";
                    continue;
                }
                noJson = 0;
                if (call.Thought is { Length: > 0 } th) Log(run, svc, "plan", th);
                if (call.Done is not null) { run.Result = call.Done.Trim(); run.State = AgentRunState.Fertig; Log(run, svc, "fertig", run.Result); break; }
                Log(run, svc, "werkzeug", $"{call.Tool} {Short(call.Args.ToJsonString(), 300)}");
                var (ok, text, kind) = await UseToolAsync(def, run, call, svc, all.Token);
                Log(run, svc, kind, text);
                question = $"Ergebnis von {call.Tool} ({(ok ? "geklappt" : "nicht geklappt")}):\n{Short(text, 8000)}\n\nWas ist der nächste Schritt? Antworte nur mit JSON.";
            }
        }
        catch (OperationCanceledException)
        {
            if (time.IsCancellationRequested && !ct.IsCancellationRequested && !_gate.HaltToken.IsCancellationRequested)
            { run.State = AgentRunState.Grenze; Log(run, svc, "grenze", $"Zeitgrenze erreicht ({def.MaxMinutes} Minuten). Der Agent hat aufgehört."); }
            else { run.State = AgentRunState.Gestoppt; Log(run, svc, "stopp", _gate.HaltToken.IsCancellationRequested ? "Not-Stopp: Der Agent wurde angehalten." : "Gestoppt: Der Agent wurde angehalten. Was bis hier geschrieben ist, bleibt."); }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            run.State = AgentRunState.Fehler; Log(run, svc, "fehler", FriendlyError.Say(ex));
        }
        finally
        {
            run.Finished = DateTimeOffset.Now;
            try
            {
                var rel = $"ergebnisse/{def.Id}-{run.Started:yyyyMMdd-HHmmss}-{run.Id[..4]}.md";
                _projects.WriteFile(def.ProjectId, rel, ResultMarkdown(run));
                run.ResultFile = rel;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectIsolationException) { Log(run, svc, "fehler", "Ergebnisdatei nicht geschrieben: " + ex.Message); }
            SaveRun(run);
            _live.TryRemove(run.Id, out _);
            _audit.Append("agent:" + def.Id, "agent.lauf.ende", $"{run.Id} zustand={run.State} schritte={run.Steps} dateien={run.FilesWritten.Count}");
        }
        return run;
    }

    static string Short(string s, int n) => s.Length > n ? s[..n] + " …" : s;

    static string StateText(AgentRunState s) => s switch
    {
        AgentRunState.Fertig => "fertig", AgentRunState.Gestoppt => "gestoppt", AgentRunState.Grenze => "an einer Grenze beendet", AgentRunState.Fehler => "nicht geklappt", _ => "läuft",
    };
    public static string StateLabel(AgentRunState s) => StateText(s);

    static string ResultMarkdown(AgentRun r)
    {
        var sb = new StringBuilder();
        sb.Append("# Agent „").Append(r.AgentName).Append("“ – Ergebnis\n\n");
        sb.Append("- Lauf: ").Append(r.Id).Append('\n');
        sb.Append("- Zustand: ").Append(StateText(r.State)).Append('\n');
        sb.Append("- Gestartet: ").Append(r.Started.ToString("yyyy-MM-dd HH:mm:ss")).Append(", beendet: ").Append(r.Finished?.ToString("HH:mm:ss") ?? "-").Append('\n');
        sb.Append("- Schritte: ").Append(r.Steps).Append(", KI: ").Append(r.Backend ?? "-").Append(r.Model is null ? "" : " (" + r.Model + ")").Append('\n');
        if (r.FilesWritten.Count > 0) sb.Append("- Geschriebene Dateien: ").Append(string.Join(", ", r.FilesWritten.Distinct())).Append('\n');
        sb.Append("\n## Ziel\n\n").Append(r.Goal).Append("\n\n## Ergebnis\n\n").Append(r.Result.Length > 0 ? r.Result : "(kein abschließendes Ergebnis)").Append("\n\n## Protokoll\n\n");
        List<AgentLogEntry> log; lock (r) log = [.. r.Log];
        foreach (var e in log) sb.Append("- ").Append(e.At.ToString("HH:mm:ss")).Append(" · Schritt ").Append(e.Step).Append(" · ").Append(e.Kind).Append(": ").Append(e.Text.Replace("\n", " ")).Append('\n');
        return sb.ToString();
    }

    static string Arg(JsonObject a, string k) => a[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : a[k]?.ToJsonString() ?? "";

    /// <summary>Ein Werkzeug ausführen. Nur Werkzeuge des Agenten; Projektgrenze über <see cref="ProjectStore.ResolveInProject"/>; Wirkung nach außen nur über das Freigabe-Tor.</summary>
    async Task<(bool Ok, string Text, string Kind)> UseToolAsync(AgentDefinition def, AgentRun run, ToolCall call, AgentServices? svc, CancellationToken ct)
    {
        var name = call.Tool ?? "";
        var tool = AgentTools.ForCall(name);
        if (tool is null) return (false, $"Das Werkzeug „{name}“ gibt es nicht. Erlaubt: {string.Join(", ", AgentTools.All.Where(t => def.Tools.Contains(t.Id)).SelectMany(t => t.Calls))}.", "blockiert");
        if (!def.Tools.Contains(tool.Id))
        {
            _audit.Append("agent:" + def.Id, "agent.werkzeug.verweigert", $"{run.Id} {name}");
            return (false, $"Abgewiesen: „{tool.Label}“ ist für diesen Agenten nicht erlaubt.", "blockiert");
        }
        var pid = def.ProjectId!;
        try
        {
            switch (name)
            {
                case "dateien.liste":
                {
                    var root = _projects.ProjectFilesRoot(pid);
                    var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).OrderBy(f => f).Take(200).ToList();
                    return (true, files.Count == 0 ? "Das Projekt ist noch leer." : string.Join("\n", files), "ergebnis");
                }
                case "datei.lesen":
                {
                    var p = _projects.ResolveInProject(pid, Arg(call.Args, "pfad"));
                    if (!File.Exists(p)) return (false, $"Die Datei „{Arg(call.Args, "pfad")}“ gibt es im Projekt nicht.", "fehler");
                    var text = await File.ReadAllTextAsync(p, ct);
                    return (true, text.Length > MaxReadChars ? text[..MaxReadChars] + "\n…(gekürzt)" : text, "ergebnis");
                }
                case "datei.schreiben":
                {
                    var rel = Arg(call.Args, "pfad"); var content = Arg(call.Args, "inhalt");
                    if (content.Length > MaxWriteChars) return (false, "Der Inhalt ist zu lang.", "fehler");
                    if (AgentBlueprint.HasSecret(content)) return (false, "Abgewiesen: Der Inhalt sieht nach einem Schlüssel oder Passwort aus.", "blockiert");
                    var full = _projects.ResolveInProject(pid, rel);
                    var existed = File.Exists(full);
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    AtomicFile.WriteAllText(full, content);
                    _audit.Append("agent:" + def.Id, "file.write", $"{pid}:{rel}");
                    lock (run) run.FilesWritten.Add(rel.Replace('\\', '/'));
                    return (true, $"{(existed ? "Ersetzt" : "Angelegt")}: {rel} ({content.Length} Zeichen).", "ergebnis");
                }
                case "web.lesen":
                {
                    var verdict = ResearchDesk.Classify(Arg(call.Args, "adresse"));
                    if (verdict.Kind != UrlKind.Lesemodus) return (false, "Abgewiesen: " + verdict.Reason, "blockiert");
                    if (svc?.ReadWeb is null) return (false, "Das Lesen im Internet ist hier nicht angeschlossen. Es wurde nichts abgerufen.", "fehler");
                    var (type, bytes, final) = await svc.ReadWeb(verdict.Uri!, ct);
                    if (type is not ("text/html" or "text/plain" or "application/xhtml+xml")) return (false, "Das ist ein Download, keine Seite – im Lesemodus wird nichts heruntergeladen.", "blockiert");
                    var html = Encoding.UTF8.GetString(bytes);
                    var page = type == "text/plain" ? new ReaderPage(final.Host, html, []) : ResearchDesk.ToReader(html, final);
                    var body = page.Title + "\n\n" + page.Text;
                    return (true, $"Quelle: {final}\n" + (body.Length > 20_000 ? body[..20_000] + "\n…(gekürzt)" : body), "ergebnis");
                }
                case "email.entwurf":
                {
                    var to = Arg(call.Args, "an").Trim(); var subject = Arg(call.Args, "betreff").Replace("\r", " ").Replace("\n", " ").Trim(); var text = Arg(call.Args, "text");
                    if (!Regex.IsMatch(to, @"^[^@\s<>,;]+@[^@\s<>,;]+\.[^@\s<>,;]+$")) return (false, "Die Empfänger-Adresse sieht nicht wie eine E-Mail-Adresse aus.", "fehler");
                    if (subject.Length == 0) return (false, "Der Betreff fehlt.", "fehler");
                    var slug = AgentBlueprint.Slug(subject); if (slug.Length == 0) slug = "entwurf";
                    var rel = $"entwuerfe/{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{slug}.eml";
                    var eml = MailCenter.ToEml(new MailDraft(to, subject, text), DateTimeOffset.Now); // dasselbe Entwurfsformat wie das Mail-Programm (SYS-09)
                    _projects.WriteFile(pid, rel, eml);
                    lock (run) run.FilesWritten.Add(rel);
                    return (true, $"Entwurf abgelegt: {rel}. Er wurde NICHT gesendet – die Person öffnet ihn selbst im Mailprogramm.", "ergebnis");
                }
                case "datei.exportieren":
                {
                    var rel = Arg(call.Args, "pfad"); var dest = Arg(call.Args, "ziel").Trim();
                    var src = _projects.ResolveInProject(pid, rel);
                    if (!File.Exists(src)) return (false, $"Die Datei „{rel}“ gibt es im Projekt nicht.", "fehler");
                    if (dest.Length == 0 || !Path.IsPathRooted(dest)) return (false, "Das Ziel muss ein vollständiger Ordnerpfad sein.", "fehler");
                    if (svc?.ExportAllowed?.Invoke(dest) != true) return (false, "Abgewiesen: In diesen Ordner darf ein Agent nichts legen.", "blockiert");
                    var target = Path.Combine(Path.GetFullPath(dest), Path.GetFileName(src));
                    var approved = await AskGateAsync(def, run, svc, "agent.export", $"Agent „{def.Name}“ möchte {rel} nach {dest} kopieren", target, $"Kopie von {rel} → {target}", ct);
                    if (approved.Id is not { } id) return (false, approved.Why, "blockiert");
                    try
                    {
                        _gate.Execute(id, "agent.export", target, () => { Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(src, target, overwrite: false); return true; });
                    }
                    catch (ApprovalException ex) { return (false, "Blockiert ohne Zustimmung: " + ex.Message + ". Es wurde nichts kopiert.", "blockiert"); }
                    return (true, $"Kopiert nach {target}.", "ergebnis");
                }
                case "studio.aktion":
                {
                    if (svc?.Studio is not { } wf) return (false, "Studio-Aktionen sind hier nicht angeschlossen.", "fehler");
                    var studio = Arg(call.Args, "studio").Trim(); var action = Arg(call.Args, "aktion").Trim(); var task = Arg(call.Args, "auftrag");
                    if (studio.Length == 0 || task.Trim().Length == 0) return (false, "Studio und Auftrag fehlen.", "fehler");
                    var rid = "agent-" + Guid.NewGuid().ToString("N")[..10];
                    var ctx = new StudioExecutionContext(pid, Principal.LocalOwner, ct, ResourceBudget.Default);
                    var res = wf.Start(new StudioPromptRequest(rid, pid, studio, task, [], "de", action.Length == 0 ? null : action, PrivacyMode.Local, new Provenance("agent:" + def.Id, null, null)), ctx);
                    if (res.Status == RunStatus.Planned) res = wf.Los(pid, rid, ctx); // Plan steht → Datei im Projekt erzeugen (Wirkung nach außen wartet danach auf Freigabe)
                    if (res.Status == RunStatus.AwaitingApproval)
                    {
                        var target = $"{pid}/{rid}";
                        var approved = await AskGateAsync(def, run, svc, "agent.studio", $"Agent „{def.Name}“: Studio-Aktion mit Wirkung nach außen ({studio} {action})", target, res.Preview, ct);
                        if (approved.Id is not { } id) { wf.Cancel(pid, rid, Principal.LocalOwner, ctx); return (false, approved.Why, "blockiert"); }
                        try { res = _gate.Execute(id, "agent.studio", target, () => wf.Approve(pid, rid, Principal.LocalOwner, ctx)); }
                        catch (ApprovalException ex) { wf.Cancel(pid, rid, Principal.LocalOwner, ctx); return (false, "Blockiert ohne Zustimmung: " + ex.Message, "blockiert"); }
                    }
                    var files = res.Artifacts.Select(x => x.RelativePath).ToList();
                    lock (run) run.FilesWritten.AddRange(files);
                    var okStudio = res.Status is RunStatus.Produced or RunStatus.Verified;
                    var msg = $"Studio-Aktion: {res.Status}" + (files.Count > 0 ? "\nDateien: " + string.Join(", ", files) : "") + (res.Errors.Count > 0 ? "\nFehler: " + string.Join("; ", res.Errors) : "")
                        + (res.Questions.Count > 0 ? "\nRückfragen: " + string.Join("; ", res.Questions.Select(q => q.Question)) : "");
                    return (okStudio, msg, okStudio ? "ergebnis" : "fehler");
                }
            }
            return (false, $"Das Werkzeug „{name}“ ist nicht verfügbar.", "fehler");
        }
        catch (ProjectIsolationException) { return (false, "Abgewiesen: Zugriff außerhalb des Projekts.", "blockiert"); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        { return (false, "Hat nicht geklappt: " + FriendlyError.Say(ex), "fehler"); }
    }

    /// <summary>Freigabe beim Tor anfragen und die Person fragen. Ergebnis: Freigabe-Id (dann <see cref="ApprovalGate.Execute"/> aufrufen) oder Grund für die Ablehnung.
    /// Auch bei „Nein“ wird die Ausführung beim Tor versucht – das Tor blockiert und protokolliert sie.</summary>
    async Task<(string? Id, string Why)> AskGateAsync(AgentDefinition def, AgentRun run, AgentServices? svc, string action, string description, string target, string? preview, CancellationToken ct)
    {
        var req = _gate.Request(Principal.LocalOwner, action, description, target, TimeSpan.FromMinutes(30), preview);
        Log(run, svc, "freigabe", $"Wartet auf deine Freigabe: {description}");
        var yes = svc?.AskApproval is { } ask && await ask(req, ct);
        if (!yes)
        {
            try { _gate.Execute(req.Id, action, target, () => true); } catch (ApprovalException) { } // Tor blockiert und schreibt es ins Prüfprotokoll
            return (null, svc?.AskApproval is null ? "Blockiert: Niemand konnte zustimmen (Lauf ohne Person). Es wurde nichts nach außen gegeben." : "Blockiert: Die Person hat nicht zugestimmt. Es wurde nichts nach außen gegeben.");
        }
        return (req.Id, "");
    }

    // ───────────── Auslöser über Abläufe ─────────────
    /// <summary>Schritt „Agent ausführen“ eines Ablaufs (SYS-05). Eine auslösende Datei wird nach „eingang/“ ins Projekt kopiert und dem Agenten genannt.
    /// Ohne Person gibt es keine Zustimmung – Wirkungen nach außen bleiben dann blockiert.</summary>
    public async Task<(bool Ok, string Summary)> RunForWorkflowAsync(string agentId, string? triggerFile, IntelligenceCore ki, AgentServices? svc, CancellationToken ct, string? model = null)
    {
        var def = Definition(agentId) ?? throw new InvalidOperationException($"Den Agenten „{agentId}“ gibt es nicht mehr.");
        string? input = null;
        if (triggerFile is not null && File.Exists(triggerFile))
        {
            if (new FileInfo(triggerFile).Length > 20 * 1024 * 1024) throw new InvalidOperationException("Die neue Datei ist zu groß für den Agenten (höchstens 20 MB).");
            var rel = "eingang/" + Path.GetFileName(triggerFile);
            var dest = _projects.ResolveInProject(def.ProjectId!, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(triggerFile, dest, overwrite: true);
            input = $"Neue Datei im Projekt: {rel}";
        }
        var r = await RunAgentAsync(def, ki, svc, input, ct, model);
        return (r.State == AgentRunState.Fertig, $"Agent „{def.Name}“: {StateText(r.State)} nach {r.Steps} Schritten. Ergebnis: {r.ResultFile ?? "-"}");
    }

    // ───────────── Team mit Datei-Übergabe ─────────────
    /// <summary>Prüfer-Rollen (können zurückgeben, laufen untereinander parallel).</summary>
    public static bool IsReviewer(string roleId) => roleId is "pruefer" or "sicherheit" || roleId.EndsWith(".pruefer", StringComparison.Ordinal);

    /// <summary>Stufen eines Teams: aufeinanderfolgende Prüfer bilden eine Stufe und arbeiten parallel (unabhängig voneinander); alle anderen nacheinander.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Stages(IReadOnlyList<string> roles)
    {
        var stages = new List<List<string>>();
        foreach (var r in roles)
            if (IsReviewer(r) && stages.Count > 0 && stages[^1].All(IsReviewer)) stages[^1].Add(r); else stages.Add([r]);
        return stages;
    }

    /// <summary>Team-Lauf mit echten Dateien: Jede Rolle arbeitet als Agent im Projekt (lesen, schreiben wenn erlaubt) und gibt ihr Ergebnis als Datei
    /// unter „team/&lt;lauf&gt;/“ weiter. Prüfer antworten mit „OK …“ oder „ZURÜCK: …“ – dann arbeitet die letzte bearbeitende Stufe mit dem Prüfbericht nach
    /// (höchstens <see cref="MaxTeamReturns"/>-mal). Stopp über <paramref name="ct"/>.</summary>
    public async Task<AgentTeamRun> RunTeamFilesAsync(string teamId, string goal, IntelligenceCore ki, string projectId, AgentServices? svc = null, CancellationToken ct = default, string? model = null, int maxStepsPerRole = 8)
    {
        var team = Team(teamId) ?? throw new InvalidOperationException("Dieses Team gibt es nicht: " + teamId);
        if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Ziel fehlt");
        if (_projects.List().All(p => p.Id != projectId)) throw new ProjectIsolationException("Projekt unbekannt");
        var run = Guid.NewGuid().ToString("N")[..6]; var dir = $"team/{run}";
        var stages = Stages(team.Roles); var tasks = new List<AgentTask>(); var handoffs = new List<string>(); var returns = 0; var n = 0;
        var state = AgentRunState.Fertig; string? review = null;
        _audit.Append(Principal.LocalOwner.Name, "agent.team.dateien.start", $"{teamId}-{run} stufen={stages.Count}");
        for (var si = 0; si < stages.Count; si++)
        {
            if (ct.IsCancellationRequested || _gate.Halted) { state = AgentRunState.Gestoppt; break; }
            var stage = stages[si]; var before = handoffs.ToList(); var reviewNow = review; review = null;
            var jobs = stage.Select(roleId =>
            {
                var num = Interlocked.Increment(ref n);
                return RunTeamRoleAsync(roleId, num, goal, before, reviewNow, dir, projectId, teamId, run, ki, svc, ct, model, maxStepsPerRole);
            }).ToList();
            var results = await Task.WhenAll(jobs); // Prüfer einer Stufe parallel, sonst genau eine Rolle
            foreach (var (task, file, runState) in results) { tasks.Add(task); handoffs.Add(file); }
            if (results.Any(r => r.State == AgentRunState.Gestoppt)) { state = AgentRunState.Gestoppt; break; }
            if (results.Any(r => r.State is AgentRunState.Fehler or AgentRunState.Grenze)) { state = AgentRunState.Fehler; break; }
            if (stage.All(IsReviewer))
            {
                var back = results.Where(r => r.Task.Result.TrimStart().StartsWith("ZURÜCK", StringComparison.OrdinalIgnoreCase)).ToList();
                if (back.Count > 0 && returns < MaxTeamReturns)
                {
                    returns++;
                    review = string.Join(", ", back.Select(b => b.File));
                    var target = Enumerable.Range(0, si).Reverse().FirstOrDefault(i => !stages[i].All(IsReviewer), -1);
                    _audit.Append(Principal.LocalOwner.Name, "agent.team.zurueck", $"{teamId}-{run} runde={returns} an={(target < 0 ? "-" : string.Join("+", stages[target]))}");
                    if (target >= 0) { si = target - 1; continue; }
                }
                else if (back.Count > 0) { state = AgentRunState.Grenze; _audit.Append(Principal.LocalOwner.Name, "agent.team.zurueck.grenze", $"{teamId}-{run}"); break; }
            }
        }
        _audit.Append(Principal.LocalOwner.Name, "agent.team.dateien.ende", $"{teamId}-{run} zustand={state} rollen={tasks.Count} zurueck={returns}");
        return new($"{teamId}-{run}", state, tasks, handoffs, returns, stages);
    }

    async Task<(AgentTask Task, string File, AgentRunState State)> RunTeamRoleAsync(string roleId, int num, string goal, IReadOnlyList<string> before, string? review, string dir, string projectId,
        string teamId, string run, IntelligenceCore ki, AgentServices? svc, CancellationToken ct, string? model, int maxSteps)
    {
        var role = RoleOf(roleId);
        var file = $"{dir}/{num:00}-{roleId.Replace('.', '-')}.md";
        var input = (before.Count > 0 ? "Übergabe-Dateien der Vorgänger (mit datei.lesen öffnen): " + string.Join(", ", before) : "Du beginnst – es gibt noch keine Übergabe-Dateien.")
            + (review is not null ? $"\nDer Prüfer hat zurückgegeben. Lies seinen Bericht ({review}) und verbessere die Arbeit entsprechend." : "");
        var t = Create(roleId, goal, projectId, input, $"dateien:{teamId}-{run}");
        t.State = AgentTaskState.Laeuft; Save();
        var tools = role.MayWriteFiles ? new List<string> { "lesen", "schreiben" } : ["lesen"];
        var def = new AgentDefinition
        {
            Id = AgentBlueprint.Slug(role.Name) is { Length: >= 2 } s ? s : "rolle", Name = role.Name, Goal = goal, Tools = tools, MaxSteps = maxSteps, MaxMinutes = 15, ProjectId = projectId,
            Instructions = role.SystemPrompt + "\nDu arbeitest in einem Team. Lies zuerst die Übergabe-Dateien. Dein Ergebnis (das „fertig“-Feld) wird als Übergabe-Datei für die nächste Rolle gespeichert."
                + (IsReviewer(roleId) ? "\nDu bist Prüfer: Beginne dein „fertig“-Ergebnis mit „OK:“, wenn die Arbeit passt, sonst mit „ZURÜCK:“ und nenne konkret, was verbessert werden muss." : ""),
        };
        var r = await RunAgentAsync(def, ki, svc, input, ct, model);
        var body = $"# {role.Name}\n\n{(r.Result.Length > 0 ? r.Result : "(kein Ergebnis – " + StateText(r.State) + ")")}\n" + (r.FilesWritten.Count > 0 ? $"\nGeschriebene Dateien: {string.Join(", ", r.FilesWritten.Distinct())}\n" : "");
        _projects.WriteFile(projectId, file, body);
        t.Result = r.Result; t.SavedAs = file; t.Finished = DateTimeOffset.Now;
        t.State = r.State == AgentRunState.Fertig ? AgentTaskState.Fertig : AgentTaskState.Fehler; Save();
        return (t, file, r.State);
    }
}
