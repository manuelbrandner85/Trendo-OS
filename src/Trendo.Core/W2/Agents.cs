using System.Text.Json;

namespace Trendo.Core;

public sealed record AgentRole(string Id, string Name, string Does, string SystemPrompt, bool MayWriteFiles);

public enum AgentTaskState { Neu, Laeuft, Fertig, Fehler, WartetAufFreigabe, Gespeichert }

public sealed class AgentTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string RoleId { get; set; } = "";
    public string? ProjectId { get; set; }
    public string Goal { get; set; } = "";
    public string? Input { get; set; }
    public AgentTaskState State { get; set; } = AgentTaskState.Neu;
    public string Result { get; set; } = "";
    public string? SavedAs { get; set; }
    public string? ApprovalId { get; set; }
    public string? TeamId { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? Finished { get; set; }
}

/// <summary>CORE-03 AI Agent Command Center: 12 Rollen mit eigenem Auftrag und eigenen Rechten, Aufgaben je Projekt getrennt,
/// Team-Abläufe (Ergebnis einer Rolle ist Eingabe der nächsten), Speichern in Projektdateien nur nach Freigabe,
/// Übergabe an Grok Build als Auftragsdatei (Grok Build selbst wird nicht vorgetäuscht).</summary>
public sealed class AgentCenter
{
    public static readonly IReadOnlyList<AgentRole> Roles =
    [
        new("planer", "Planer", "zerlegt ein Ziel in klare Schritte", "Du bist der Planer. Zerlege das Ziel in nummerierte, machbare Schritte mit jeweils einem Satz. Nenne zuerst den wichtigsten Schritt.", false),
        new("recherche", "Rechercheur", "sammelt, was bekannt ist, und sagt, was fehlt", "Du bist der Rechercheur. Sammle bekanntes Wissen zum Thema und trenne klar: Fakten, Annahmen, offene Fragen. Erfinde keine Quellen.", false),
        new("texter", "Texter", "schreibt verständliche Texte", "Du bist der Texter. Schreibe klar, freundlich und in einfachem Deutsch. Kurze Sätze, keine Fachwörter ohne Erklärung.", true),
        new("lektor", "Lektor", "verbessert Rechtschreibung und Stil", "Du bist der Lektor. Verbessere Rechtschreibung, Grammatik und Stil. Gib den verbesserten Text aus und danach höchstens 5 Hinweise.", true),
        new("uebersetzer", "Übersetzer", "übersetzt Deutsch ↔ Englisch", "Du bist der Übersetzer. Übersetze den Text sinngemäß zwischen Deutsch und Englisch. Behalte Ton und Format bei.", true),
        new("programmierer", "Programmierer", "schreibt und erklärt Code", "Du bist der Programmierer. Schreibe sauberen, kommentierten Code und erkläre kurz, wie man ihn startet. Keine geheimen Schlüssel im Code.", true),
        new("pruefer", "Prüfer", "findet Fehler und Lücken", "Du bist der Prüfer. Suche Fehler, Widersprüche und Lücken. Gib eine Liste mit Schweregrad (hoch/mittel/niedrig) und Vorschlag aus.", false),
        new("designer", "Design-Berater", "macht Vorschläge für Aussehen und Bedienung", "Du bist der Design-Berater. Mache konkrete Vorschläge für Aufbau, Farben, Schrift und Bedienung. Denke an gute Lesbarkeit.", false),
        new("daten", "Datenanalyst", "wertet Zahlen und Tabellen aus", "Du bist der Datenanalyst. Werte die Daten aus, nenne Summen, Durchschnitt, Auffälligkeiten. Rechne nachvollziehbar und sag, wenn Daten fehlen.", false),
        new("organisator", "Organisator", "macht Aufgabenlisten und Zeitpläne", "Du bist der Organisator. Erstelle eine Aufgabenliste mit Reihenfolge, geschätzter Dauer und Frist-Vorschlag.", true),
        new("sicherheit", "Sicherheitsprüfer", "prüft Datenschutz und Risiken", "Du bist der Sicherheitsprüfer. Prüfe auf Datenschutz-Risiken, Passwörter, persönliche Daten und unsichere Schritte. Gib klare Empfehlungen.", false),
        new("projektleiter", "Projektleiter", "fasst zusammen und entscheidet nächste Schritte", "Du bist der Projektleiter. Fasse die Ergebnisse zusammen, nenne Entscheidungen, offene Punkte und den nächsten Schritt.", false),
    ];

    public static readonly (string Id, string Name, string[] Roles)[] Teams =
    [
        ("text", "Text schreiben & prüfen", ["planer", "texter", "lektor"]),
        ("code", "Programm bauen & prüfen", ["planer", "programmierer", "pruefer", "sicherheit"]),
        ("projekt", "Projekt planen", ["recherche", "planer", "organisator", "projektleiter"]),
    ];

    private readonly string _file; private readonly AuditLog _audit; private readonly ApprovalGate _gate; private readonly ProjectStore _projects;
    private readonly List<AgentTask> _tasks;

    public AgentCenter(string dataRoot, AuditLog audit, ApprovalGate gate, ProjectStore projects)
    {
        _audit = audit; _gate = gate; _projects = projects;
        _file = Path.Combine(dataRoot, "ki", "agenten.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        try { _tasks = File.Exists(_file) ? JsonSerializer.Deserialize<List<AgentTask>>(File.ReadAllText(_file)) ?? [] : []; } catch { _tasks = []; }
        foreach (var t in _tasks.Where(t => t.State == AgentTaskState.Laeuft)) { t.State = AgentTaskState.Fehler; t.Result = "Unterbrochen (Trendo wurde beendet)."; }
    }

    public static AgentRole Role(string id) => Roles.First(r => r.Id == id);
    public IReadOnlyList<AgentTask> Tasks(string? projectId = null) => _tasks.Where(t => projectId is null || t.ProjectId == projectId).OrderByDescending(t => t.Created).ToList();
    public AgentTask? Get(string id) => _tasks.FirstOrDefault(t => t.Id == id);

    public AgentTask Create(string roleId, string goal, string? projectId = null, string? input = null, string? teamId = null)
    {
        _ = Role(roleId);
        if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Ziel fehlt");
        if (projectId is not null && _projects.List().All(p => p.Id != projectId)) throw new ProjectIsolationException("Projekt unbekannt");
        var t = new AgentTask { RoleId = roleId, Goal = goal.Trim(), ProjectId = projectId, Input = input, TeamId = teamId };
        _tasks.Add(t); Save();
        _audit.Append(Principal.LocalOwner.Name, "agent.create", $"{t.Id} rolle={roleId} projekt={projectId ?? "-"}");
        return t;
    }

    public async Task<AgentTask> RunAsync(AgentTask t, IntelligenceCore ki, string? model = null, CancellationToken ct = default)
    {
        var r = Role(t.RoleId);
        t.State = AgentTaskState.Laeuft; Save();
        var q = t.Input is { Length: > 0 } inp ? $"Auftrag: {t.Goal}\n\nVorarbeit/Eingabe:\n{inp}" : $"Auftrag: {t.Goal}";
        var a = await ki.AskAsync(new KiRequest(q, r.SystemPrompt + " Antworte auf Deutsch.", model), ct);
        t.Result = a.Text; t.State = a.Ok ? AgentTaskState.Fertig : AgentTaskState.Fehler; t.Finished = DateTimeOffset.Now; Save();
        _audit.Append("agent:" + r.Id, a.Ok ? "agent.done" : "agent.failed", $"{t.Id} {a.Text.Length}Z");
        return t;
    }

    /// <summary>Team-Ablauf: jede Rolle bekommt das Ergebnis der vorherigen. Bricht beim ersten Fehler ab.</summary>
    public async Task<IReadOnlyList<AgentTask>> RunTeamAsync(string teamId, string goal, IntelligenceCore ki, string? projectId = null, string? model = null, CancellationToken ct = default)
    {
        var team = Teams.First(x => x.Id == teamId); var run = Guid.NewGuid().ToString("N")[..6];
        var done = new List<AgentTask>(); string? prev = null;
        foreach (var role in team.Roles)
        {
            var t = Create(role, goal, projectId, prev, $"{teamId}-{run}");
            await RunAsync(t, ki, model, ct); done.Add(t);
            if (t.State != AgentTaskState.Fertig) break;
            prev = t.Result;
        }
        return done;
    }

    /// <summary>Speichern in eine Projektdatei: nur Rollen mit Schreibrecht, nur nach Freigabe, nur innerhalb des Projekts.</summary>
    public ApprovalRequest RequestSave(AgentTask t, string fileName)
    {
        if (t.ProjectId is null) throw new InvalidOperationException("Aufgabe gehört zu keinem Projekt.");
        if (!Role(t.RoleId).MayWriteFiles) throw new UnauthorizedAccessException($"{Role(t.RoleId).Name} darf keine Dateien schreiben.");
        if (t.State is not (AgentTaskState.Fertig or AgentTaskState.WartetAufFreigabe)) throw new InvalidOperationException("Aufgabe ist nicht fertig.");
        _projects.ResolveInProject(t.ProjectId, fileName);
        var req = _gate.Request(Principal.LocalOwner, "agent.save", $"{Role(t.RoleId).Name}: Ergebnis als {fileName} im Projekt speichern", $"{t.ProjectId}/{fileName}");
        t.ApprovalId = req.Id; t.State = AgentTaskState.WartetAufFreigabe; Save();
        return req;
    }

    public void ExecuteSave(AgentTask t, string fileName)
    {
        _gate.Execute(t.ApprovalId ?? "", "agent.save", $"{t.ProjectId}/{fileName}", () => { _projects.WriteFile(t.ProjectId!, fileName, t.Result); return true; });
        t.SavedAs = fileName; t.State = AgentTaskState.Gespeichert; Save();
    }

    /// <summary>Übergabe an Grok Build: schreibt einen Auftrag als Markdown ins Projekt. Ausgeführt wird er erst, wenn Grok Build verbunden ist.</summary>
    public string ExportGrokBuildJob(AgentTask t)
    {
        if (t.ProjectId is null) throw new InvalidOperationException("Aufgabe gehört zu keinem Projekt.");
        var name = $"grok-build-auftrag-{t.Id}.md";
        var md = $"# Auftrag für Grok Build\n\n- Erstellt: {DateTimeOffset.Now:yyyy-MM-dd HH:mm}\n- Rolle: {Role(t.RoleId).Name}\n- Status: wartet auf Grok Build (nicht verbunden)\n- Regeln: nur im Projektordner arbeiten, keine Adminrechte, kein Push ohne Freigabe, Testergebnisse ehrlich als Rohlog\n\n## Ziel\n{t.Goal}\n\n## Vorarbeit\n{(t.Result.Length > 0 ? t.Result : "(keine)")}\n";
        _projects.WriteFile(t.ProjectId, name, md);
        _audit.Append(Principal.LocalOwner.Name, "agent.grokbuild.export", $"{t.Id} -> {name}");
        return name;
    }

    public bool Remove(string id) { var n = _tasks.RemoveAll(t => t.Id == id); if (n > 0) Save(); return n > 0; }
    private void Save() => File.WriteAllText(_file, JsonSerializer.Serialize(_tasks));
}
