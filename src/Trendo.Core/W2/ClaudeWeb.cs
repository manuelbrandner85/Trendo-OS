using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Claude im Web mit Trendo verbinden (Wunsch 01.11.2026): einmal bei claude.ai mit dem eigenen Abo anmelden – Trendo merkt sich die Anmeldung
/// in einem eigenen Browser-Profil. Danach ist Claude in jedem Studio angemeldet; jedes Studio hat seinen eigenen Chat, und Aufträge gehen mit Rolle und
/// Fachregeln des Studios hinein. Trendo nutzt dabei keine Schnittstelle des Abos – es zeigt nur die Webseite, auf der der Nutzer selbst arbeitet.</summary>
public sealed partial class ClaudeWeb(string dataRoot)
{
    public const string Login = "https://claude.ai/login", NewChat = "https://claude.ai/new";
    public string Profile => Path.Combine(dataRoot, "claude-web");
    string StateFile => Path.Combine(Profile, "trendo.json");

    sealed class State { public DateTimeOffset? Connected { get; set; } public Dictionary<string, string> Chats { get; set; } = []; public HashSet<string> Briefed { get; set; } = []; }
    State Load() { try { return File.Exists(StateFile) ? JsonSerializer.Deserialize<State>(File.ReadAllText(StateFile)) ?? new() : new(); } catch (JsonException) { return new(); } }
    void Save(State s) { Directory.CreateDirectory(Profile); File.WriteAllText(StateFile, JsonSerializer.Serialize(s)); }

    public bool Connected => Load().Connected is not null;
    public DateTimeOffset? ConnectedAt => Load().Connected;

    /// <summary>Ist diese Seite eine Seite nach der Anmeldung (also nicht Anmeldung, Bestätigungslink oder fremde Seite)?</summary>
    public static bool IsSignedInPage(Uri u) =>
        u.Scheme == "https" && u.Host is "claude.ai" or "www.claude.ai" &&
        !Regex.IsMatch(u.AbsolutePath, @"^/(login|logout|oauth|magic-link|signup|onboarding|verify|auth)(/|$)", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"^/chat/([0-9a-f-]{8,})", RegexOptions.IgnoreCase)] private static partial Regex ChatRx();
    public static string? ChatId(Uri u) => u.Host is "claude.ai" or "www.claude.ai" && ChatRx().Match(u.AbsolutePath) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Navigation in der Webansicht verarbeiten: erste Seite nach der Anmeldung = verbunden; ein Chat wird dem Studio zugeordnet.
    /// Gibt true zurück, wenn sich etwas geändert hat.</summary>
    public bool Navigated(Uri u, string? studio)
    {
        var s = Load(); var changed = false;
        if (IsSignedInPage(u) && s.Connected is null) { s.Connected = DateTimeOffset.Now; changed = true; }
        if (studio is { Length: > 0 } && ChatId(u) is { } id && s.Chats.GetValueOrDefault(studio) != id) { s.Chats[studio] = id; changed = true; }
        if (changed) Save(s);
        return changed;
    }

    /// <summary>Wohin die Webansicht im Studio öffnet: nicht verbunden → Anmeldung; sonst der Chat des Studios oder ein neuer.</summary>
    public string StartUrl(string? studio)
    {
        var s = Load();
        if (s.Connected is null) return Login;
        return studio is not null && s.Chats.TryGetValue(studio, out var id) ? $"https://claude.ai/chat/{id}" : NewChat;
    }

    public void NewStudioChat(string studio) { var s = Load(); if (s.Chats.Remove(studio) | s.Briefed.Remove(studio)) Save(s); }

    /// <summary>Verbindung trennen: Trendo vergisst Anmeldung und Chats (die Anmeldedaten im Profil löscht der Aufrufer, wenn gewünscht).</summary>
    public void Disconnect() => Save(new State());

    /// <summary>Text für das Eingabefeld: beim ersten Auftrag im Chat eines Studios mit Rolle und Fachregeln davor, danach nur der Auftrag.</summary>
    public string Message(string? studio, string studioName, string role, IReadOnlyList<Skill> skills, string text)
    {
        var s = Load();
        if (studio is null || s.Briefed.Contains(studio)) return text.Trim();
        s.Briefed.Add(studio); Save(s);
        return Brief(studioName, role, skills) + "\n\nMein Auftrag: " + text.Trim();
    }

    public static string Brief(string studioName, string role, IReadOnlyList<Skill> skills)
    {
        var sb = new StringBuilder($"[Trendo OS · Studio „{studioName}“]\n{role}");
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
