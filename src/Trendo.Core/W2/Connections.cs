using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Paket D (0.9.48, Wunsch 02.11.2026): GitHub je Person anbinden und Projekte hochladen – mit der offiziellen GitHub CLI.
/// Die Anmeldung macht der Nutzer selbst im Browser; der Schlüssel liegt im eigenen gh-Ordner der Person (GH_CONFIG_DIR), Trendo liest ihn nie.</summary>
public static partial class GitHubLink
{
    /// <summary>Eigener gh-Ordner je Person (Datenordner der Person).</summary>
    public static string ConfigDir(string dataRoot) => Path.Combine(dataRoot, "github");
    public static Dictionary<string, string> Env(string dataRoot) => new() { ["GH_CONFIG_DIR"] = ConfigDir(dataRoot), ["GH_PROMPT_DISABLED"] = "1" };

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")] private static partial Regex RepoName();

    /// <summary>Repo-Name aus einem Projektnamen: nur Buchstaben, Ziffern, Punkt, Strich, Unterstrich.</summary>
    public static string SafeRepoName(string name)
    {
        // Akzente weg (é → e), damit „Café“ nicht zu „caf“ wird
        var folded = string.Concat(name.Trim().ToLowerInvariant().Replace("ß", "ss").Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
        var s = Regex.Replace(folded, @"[^a-z0-9._-]+", "-").Trim('-', '.');
        return s.Length == 0 ? "trendo-projekt" : s.Length > 90 ? s[..90] : s;
    }

    /// <summary>Befehl für die Anmeldung im Browser (läuft in einem sichtbaren Fenster, der Nutzer meldet sich selbst an).</summary>
    public static string[] LoginArgs => ["auth", "login", "--web", "--hostname", "github.com", "--git-protocol", "https"];
    public static string[] StatusArgs => ["auth", "status", "--hostname", "github.com"];
    public static string[] LogoutArgs => ["auth", "logout", "--hostname", "github.com"];

    /// <summary>Angemeldeter Name aus der Ausgabe von „gh auth status“ (null = nicht angemeldet).</summary>
    public static string? UserFrom(string statusOutput) =>
        Regex.Match(statusOutput, @"Logged in to github\.com (?:account|as) ([A-Za-z0-9-]+)") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Schritte zum Hochladen eines Ordners als neues Repo: git init (falls nötig), alles vormerken, Stand speichern, Repo anlegen und hochladen.</summary>
    public static List<(string Exe, string[] Args)> PublishSteps(string folder, string repo, bool isPrivate, string message = "Stand aus Trendo")
    {
        if (!RepoName().IsMatch(repo)) throw new ArgumentException("Ungültiger Repo-Name (nur Buchstaben, Ziffern, . _ -).");
        var steps = new List<(string, string[])>();
        if (!Directory.Exists(Path.Combine(folder, ".git"))) steps.Add(("git", ["init", "-q", "-b", "main"]));
        EnsureIgnore(folder);
        steps.Add(("git", ["add", "-A"]));
        steps.Add(("git", ["-c", "user.name=Trendo", "-c", "user.email=trendo@localhost", "commit", "-q", "--allow-empty", "-m", message]));
        steps.Add(("gh", ["repo", "create", repo, isPrivate ? "--private" : "--public", "--source", ".", "--remote", "origin", "--push"]));
        return steps;
    }

    /// <summary>Trendo-interne Ordner (.trendo, Sicherungen) kommen nie ins Repo.</summary>
    public static void EnsureIgnore(string folder)
    {
        var f = Path.Combine(folder, ".gitignore");
        var lines = File.Exists(f) ? File.ReadAllLines(f).ToList() : [];
        var need = new[] { ".trendo/", "node_modules/", ".env", "*.key" }.Where(x => !lines.Contains(x)).ToList();
        if (need.Count > 0) File.AppendAllLines(f, need);
    }
}

/// <summary>Paket D: Erweiterungen von Claude Code im Blick – Skills, Plugins, Marktplätze, MCP-Server. Nur lesen; Änderungen laufen über die
/// offiziellen „claude“-Befehle nach Ja. Geheime Werte (env, Kopfzeilen) werden nie gelesen oder angezeigt.</summary>
public static class ClaudeExtensions
{
    public sealed record Skill(string Name, string Description, string Dir);
    public sealed record Plugin(string Id, string Version, bool Enabled);
    public sealed record Mcp(string Name, string Kind, string Target);

    /// <summary>Konfigurationsordner, den Trendos Claude benutzt: eigener Personen-Ordner oder der Standard (~/.claude).</summary>
    public static (string Dir, string ClaudeJson) Config(string? personDir, string home) =>
        personDir is not null ? (personDir, Path.Combine(personDir, ".claude.json")) : (Path.Combine(home, ".claude"), Path.Combine(home, ".claude.json"));

    public static List<Skill> Skills(string cfg)
    {
        var dir = Path.Combine(cfg, "skills"); if (!Directory.Exists(dir)) return [];
        var list = new List<Skill>();
        foreach (var d in Directory.GetDirectories(dir))
        {
            var f = Path.Combine(d, "SKILL.md"); if (!File.Exists(f)) continue;
            var head = File.ReadLines(f).Take(30).ToList();
            string? V(string k) => head.FirstOrDefault(l => l.StartsWith(k + ":"))?[(k.Length + 1)..].Trim().Trim('"');
            list.Add(new(V("name") ?? Path.GetFileName(d), V("description") ?? "", d));
        }
        return list.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<Plugin> Plugins(string cfg)
    {
        var f = Path.Combine(cfg, "plugins", "installed_plugins.json"); if (!File.Exists(f)) return [];
        var enabled = new Dictionary<string, bool>();
        try { if (JsonNode.Parse(File.ReadAllText(Path.Combine(cfg, "settings.json")))?["enabledPlugins"] is JsonObject ep) foreach (var (k, v) in ep) enabled[k] = v?.GetValue<bool>() == true; } catch (Exception e) when (e is JsonException or IOException or InvalidOperationException) { }
        try
        {
            var pl = JsonNode.Parse(File.ReadAllText(f))?["plugins"] as JsonObject; if (pl is null) return [];
            return pl.Select(p => new Plugin(p.Key, (string?)(p.Value as JsonArray)?.FirstOrDefault()?["version"] ?? "", enabled.GetValueOrDefault(p.Key, true))).OrderBy(p => p.Id).ToList();
        }
        catch (JsonException) { return []; }
    }

    public static List<string> Marketplaces(string cfg)
    {
        var f = Path.Combine(cfg, "plugins", "known_marketplaces.json");
        try { return File.Exists(f) && JsonNode.Parse(File.ReadAllText(f)) is JsonObject o ? o.Select(x => x.Key).OrderBy(x => x).ToList() : []; } catch (JsonException) { return []; }
    }

    /// <summary>MCP-Server (Nutzer-Ebene): Name, Art (stdio/http/sse) und Ziel (Programm oder Adresse) – ohne env und Kopfzeilen.</summary>
    public static List<Mcp> McpServers(string claudeJson)
    {
        if (!File.Exists(claudeJson)) return [];
        try
        {
            if (JsonNode.Parse(File.ReadAllText(claudeJson))?["mcpServers"] is not JsonObject m) return [];
            return m.Select(x => new Mcp(x.Key, (string?)x.Value?["type"] ?? ((string?)x.Value?["url"] is not null ? "http" : "stdio"),
                (string?)x.Value?["url"] is { } u ? Redact(u) : Path.GetFileName((string?)x.Value?["command"] ?? ""))).OrderBy(x => x.Name).ToList();
        }
        catch (JsonException) { return []; }
        static string Redact(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Host}{u.AbsolutePath}" : "(Adresse)"; // ohne Abfrage – dort stehen manchmal Schlüssel
    }

    static readonly Regex Safe = new(@"^[A-Za-z0-9@._/:-]{1,200}$");
    static string Check(string v, string what) => Safe.IsMatch(v) ? v : throw new ArgumentException($"Ungültige Angabe für {what}.");

    // Befehle für die offiziellen Claude-Code-Werkzeuge (laufen erst nach Ja, mit demselben Konfigurationsordner)
    public static string[] PluginInstall(string id) => ["plugin", "install", Check(id, "Plugin")];
    public static string[] PluginRemove(string id) => ["plugin", "uninstall", Check(id, "Plugin")];
    public static string[] PluginEnable(string id, bool on) => ["plugin", on ? "enable" : "disable", Check(id, "Plugin")];
    public static string[] MarketplaceAdd(string source) => ["plugin", "marketplace", "add", Check(source, "Marktplatz")];
    public static string[] McpAddHttp(string name, string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "https" or "http"
        ? ["mcp", "add", "--scope", "user", "--transport", "http", Check(name, "Name"), u.ToString()] : throw new ArgumentException("Bitte eine Adresse mit https:// angeben.");
    public static string[] McpAddCommand(string name, string command, params string[] args) => ["mcp", "add", "--scope", "user", Check(name, "Name"), "--", Check(command, "Programm"), .. args.Select(a => Check(a, "Argument"))];
    public static string[] McpRemove(string name) => ["mcp", "remove", "--scope", "user", Check(name, "Name")];

    /// <summary>Skill aus einem Ordner übernehmen (er braucht eine SKILL.md). Vorhandenes bleibt unangetastet.</summary>
    public static string AddSkill(string cfg, string sourceDir)
    {
        if (!File.Exists(Path.Combine(sourceDir, "SKILL.md"))) throw new ArgumentException("Im Ordner fehlt SKILL.md – das ist kein Skill.");
        var target = Path.Combine(cfg, "skills", Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar)));
        if (Directory.Exists(target)) throw new InvalidOperationException("Einen Skill mit diesem Namen gibt es schon.");
        foreach (var f in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var t = Path.Combine(target, Path.GetRelativePath(sourceDir, f)); Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.Copy(f, t);
        }
        return target;
    }
}
