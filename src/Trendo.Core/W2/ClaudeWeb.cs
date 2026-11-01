using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Claude im Web mit Trendo verbinden (Wunsch 01.11.2026): einmal bei claude.ai mit dem eigenen Abo anmelden – Trendo merkt sich die Anmeldung
/// in einem eigenen Browser-Profil je Person. Jedes Studio hat seine eigenen Claude-Projekte (wie in Claude: fortsetzen, neu anlegen, entfernen);
/// die Anweisungen eines Projekts legen Rolle, Fachregeln und Fachgebiet fest. Trendo nutzt dabei keine Schnittstelle des Abos – es zeigt nur die Webseite,
/// auf der der Nutzer selbst arbeitet.</summary>
public sealed partial class ClaudeWeb(string dataRoot)
{
    public const string Login = "https://claude.ai/login", NewChat = "https://claude.ai/new", Projects = "https://claude.ai/projects", Logout = "https://claude.ai/logout";
    public string Profile => Path.Combine(dataRoot, "claude-web");
    string StateFile => Path.Combine(Profile, "trendo.json");

    /// <summary>Claude-Projekt eines Studios: Kennung bei claude.ai, Name (in Trendo vergeben), zuletzt geöffnet.</summary>
    public sealed record StudioProject(string Id, string Name, DateTimeOffset Last);

    sealed class State
    {
        public DateTimeOffset? Connected { get; set; }
        public Dictionary<string, string> Chats { get; set; } = [];
        public Dictionary<string, List<StudioProject>> StudioProjects { get; set; } = [];
        public Dictionary<string, string> Pending { get; set; } = [];
    }
    State Load() { try { return File.Exists(StateFile) ? JsonSerializer.Deserialize<State>(File.ReadAllText(StateFile)) ?? new() : new(); } catch (JsonException) { return new(); } }
    void Save(State s) { Directory.CreateDirectory(Profile); File.WriteAllText(StateFile, JsonSerializer.Serialize(s)); }

    public bool Connected => Load().Connected is not null;
    public DateTimeOffset? ConnectedAt => Load().Connected;

    /// <summary>Ist diese Seite eine Seite nach der Anmeldung (also nicht Anmeldung, Bestätigungslink oder fremde Seite)?</summary>
    public static bool IsSignedInPage(Uri u) =>
        u.Scheme == "https" && u.Host is "claude.ai" or "www.claude.ai" &&
        !Regex.IsMatch(u.AbsolutePath, @"^/(login|logout|oauth|magic-link|signup|onboarding|verify|auth)(/|$)", RegexOptions.IgnoreCase);

    /// <summary>Anmeldefenster, die in Trendo bleiben müssen (Google, Apple, SSO von claude.ai): Würden sie im normalen Browser aufgehen,
    /// wäre man dort angemeldet und nicht in Trendo (Fehler gemeldet am 01.11.2026).</summary>
    public static bool IsAuthPopup(Uri u) =>
        u.Scheme == "https" && (u.Host is "claude.ai" or "accounts.google.com" or "appleid.apple.com" or "idmsa.apple.com" or "anthropic.com"
            || u.Host.EndsWith(".claude.ai", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".anthropic.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>Anmeldelink aus der E-Mail von Claude (eingefügt vom Nutzer): nur https-Adressen von claude.ai, sonst null.</summary>
    public static Uri? LoginLink(string? text) =>
        Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var u) && u.Scheme == "https" && u.Host is "claude.ai" or "www.claude.ai" && u.AbsolutePath.Length > 1 ? u : null;

    [GeneratedRegex(@"^/chat/([0-9a-f-]{8,})", RegexOptions.IgnoreCase)] private static partial Regex ChatRx();
    [GeneratedRegex(@"^/project/([0-9a-f-]{8,})", RegexOptions.IgnoreCase)] private static partial Regex ProjectRx();
    public static string? ChatId(Uri u) => u.Host is "claude.ai" or "www.claude.ai" && ChatRx().Match(u.AbsolutePath) is { Success: true } m ? m.Groups[1].Value : null;
    public static string? ProjectId(Uri u) => u.Host is "claude.ai" or "www.claude.ai" && ProjectRx().Match(u.AbsolutePath) is { Success: true } m ? m.Groups[1].Value : null;
    public static string ProjectUrl(string id) => $"https://claude.ai/project/{id}";

    /// <summary>Projekte des Studios, zuletzt geöffnetes zuerst.</summary>
    public IReadOnlyList<StudioProject> ProjectsOf(string? studio) =>
        studio is not null && Load().StudioProjects.TryGetValue(studio, out var l) ? l.OrderByDescending(p => p.Last).ToList() : [];
    /// <summary>Zuletzt geöffnetes Projekt des Studios (oder null).</summary>
    public string? ProjectOf(string? studio) => ProjectsOf(studio).FirstOrDefault()?.Id;
    /// <summary>Wartet das Studio auf ein neu angelegtes Projekt (Name in Trendo vergeben, Projekt bei Claude noch nicht erstellt)?</summary>
    public string? PendingOf(string? studio) => studio is not null && Load().Pending.TryGetValue(studio, out var n) ? n : null;

    /// <summary>Neues Projekt für das Studio anlegen: Name merken; das nächste bei Claude geöffnete neue Projekt bekommt ihn.</summary>
    public void BeginProject(string studio, string name)
    {
        var s = Load(); s.Pending[studio] = string.IsNullOrWhiteSpace(name) ? "Neues Projekt" : name.Trim(); Save(s);
    }

    /// <summary>Gerade geöffnetes Projekt bei Claude zu diesem Studio hinzufügen (für schon vorhandene Projekte). Gibt false zurück, wenn die Seite kein Projekt ist.</summary>
    public bool AddProject(string studio, Uri page, string name)
    {
        if (ProjectId(page) is not { } id) return false;
        var s = Load(); var l = s.StudioProjects.TryGetValue(studio, out var x) ? x : s.StudioProjects[studio] = [];
        l.RemoveAll(p => p.Id == id);
        l.Add(new StudioProject(id, string.IsNullOrWhiteSpace(name) ? "Projekt" : name.Trim(), DateTimeOffset.Now));
        Save(s); return true;
    }

    /// <summary>Projekt aus der Liste des Studios entfernen (bei Claude bleibt es bestehen, bis man es dort löscht).</summary>
    public bool RemoveProject(string studio, string id)
    {
        var s = Load();
        if (!s.StudioProjects.TryGetValue(studio, out var l) || l.RemoveAll(p => p.Id == id) == 0) return false;
        Save(s); return true;
    }

    public bool RenameProject(string studio, string id, string name)
    {
        var s = Load();
        if (string.IsNullOrWhiteSpace(name) || !s.StudioProjects.TryGetValue(studio, out var l) || l.FindIndex(p => p.Id == id) is var i && i < 0) return false;
        l[i] = l[i] with { Name = name.Trim() }; Save(s); return true;
    }

    /// <summary>Navigation in der Webansicht verarbeiten: erste Seite nach der Anmeldung = verbunden; ein Chat wird dem Studio zugeordnet; ein neu angelegtes
    /// Projekt (siehe <see cref="BeginProject"/>) kommt in die Liste des Studios, ein schon bekanntes gilt als zuletzt geöffnet. Fremde Projekte werden nicht übernommen.
    /// Gibt true zurück, wenn sich etwas geändert hat.</summary>
    public bool Navigated(Uri u, string? studio)
    {
        var s = Load(); var changed = false;
        if (IsSignedInPage(u) && s.Connected is null) { s.Connected = DateTimeOffset.Now; changed = true; }
        if (studio is { Length: > 0 } && ChatId(u) is { } id && s.Chats.GetValueOrDefault(studio) != id) { s.Chats[studio] = id; changed = true; }
        if (studio is { Length: > 0 } && ProjectId(u) is { } pid)
        {
            var l = s.StudioProjects.TryGetValue(studio, out var x) ? x : s.StudioProjects[studio] = [];
            if (l.FindIndex(p => p.Id == pid) is var i && i >= 0) { l[i] = l[i] with { Last = DateTimeOffset.Now }; changed = true; }
            else if (s.Pending.Remove(studio, out var name)) { l.Add(new StudioProject(pid, name, DateTimeOffset.Now)); changed = true; }
        }
        if (changed) Save(s);
        return changed;
    }

    /// <summary>Wohin die Webansicht im Studio öffnet: nicht verbunden → Anmeldung; sonst das zuletzt geöffnete Projekt (mit seinen Chats, wie in Claude),
    /// sonst der letzte Chat, sonst ein neuer Chat.</summary>
    public string StartUrl(string? studio)
    {
        var s = Load();
        if (s.Connected is null) return Login;
        if (ProjectOf(studio) is { } p) return ProjectUrl(p);
        return studio is not null && s.Chats.TryGetValue(studio, out var id) ? $"https://claude.ai/chat/{id}" : NewChat;
    }

    public void NewStudioChat(string studio) { var s = Load(); if (s.Chats.Remove(studio)) Save(s); }

    /// <summary>Verbindung trennen: Trendo vergisst Anmeldung, Chats und Projekte (abgemeldet wird bei claude.ai über <see cref="Logout"/>).</summary>
    public void Disconnect() => Save(new State());

    public static string Brief(string studioName, string role, IReadOnlyList<Skill> skills)
    {
        var sb = new StringBuilder($"[Trendo OS · Studio „{studioName}“]\n{role}\n\n{ScopeRule(studioName)}");
        if (skills.Count > 0)
        {
            sb.Append("\n\nArbeite nach diesen Fachregeln und prüfe dein Ergebnis vor dem Abschluss gegen die Checklisten:");
            foreach (var k in skills.Take(4))
            {
                sb.Append($"\n\n## {k.Name}\n{(k.Body.Length > 1500 ? k.Body[..1500] + " …" : k.Body)}");
                if (k.Checklist.Count > 0) sb.Append("\nCheckliste: " + string.Join("; ", k.Checklist));
            }
        }
        return sb.ToString();
    }

    /// <summary>Fachgebiet festlegen: Claude bleibt beim Studio und verweist sonst auf das passende Trendo-Studio.</summary>
    public static string ScopeRule(string studioName) =>
        $"Du arbeitest ausschließlich für das Studio „{studioName}“ in Trendo OS. Bleib bei diesem Fachgebiet: Planen, bauen, erklären und prüfen nur dafür. " +
        "Bittet man dich um etwas aus einem anderen Fachgebiet, lehne freundlich ab und nenne das passende Trendo-Studio. Antworte auf Deutsch.";

    /// <summary>Anweisungen für ein Claude-Projekt des Studios (einmal beim Erstellen unter „Anweisungen“ einfügen).</summary>
    public static string ProjectInstructions(string studioName, string role, IReadOnlyList<Skill> skills) => Brief(studioName, role, skills);
    public static string ProjectName(string studioName, string name) => $"{name} · Trendo {studioName}";

    /// <summary>Fachregeln eines Studios als Skill-Paket (ZIP mit einem Ordner je Skill und SKILL.md) zum einmaligen Hochladen bei Claude.</summary>
    public static string SkillPackage(IReadOnlyList<Skill> skills, string studioId, string targetDir)
    {
        if (skills.Count == 0) throw new InvalidOperationException("Für dieses Studio gibt es noch keine Fachregeln.");
        Directory.CreateDirectory(targetDir);
        var zip = Path.Combine(targetDir, $"trendo-skills-{Regex.Replace(studioId.ToLowerInvariant(), "[^a-z0-9-]", "-")}.zip");
        if (File.Exists(zip)) File.Delete(zip);
        using var z = ZipFile.Open(zip, ZipArchiveMode.Create);
        foreach (var k in skills)
        {
            var e = z.CreateEntry($"trendo-{k.Id}/SKILL.md");
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write(SkillFiles.ToClaudeSkill(k));
        }
        return zip;
    }
}
