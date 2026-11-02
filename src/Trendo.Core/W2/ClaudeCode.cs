using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>T-0316: Claude Code als Haupt-KI überall, wo in Trendo programmiert wird – über das Claude-Abo des Nutzers, so einfach wie möglich verbunden.
/// Trendo startet das installierte Claude Code (kein eigener Zugang, kein Schlüssel in Trendo), liest den Anmeldestand mit <c>claude auth status --json</c>
/// (nur „angemeldet“, Anmeldeart, Abo-Stufe – nie E-Mail oder Organisation) und arbeitet in zwei Phasen wie in Claude Code: erst lesen und planen,
/// nach dem Ja umsetzen (dieselbe Sitzung). Jeder Lauf ist eingegrenzt: <c>--restricted</c> (keine Befehle, kein Web, Datei-Werkzeuge nur im Projektordner,
/// fremde Einstellungsdateien aus) und <c>--strict-mcp-config</c> (keine fremden Erweiterungen). Kostenpflichtige KI bleibt optional – ohne Claude arbeitet der Trendo-Agent.</summary>
public static partial class ClaudeCode
{
    /// <summary>Nur das Claude-Abo zählt (Vorgabe 01.11.2026): Anmeldung über claude.ai. Console/API-Schlüssel gelten als nicht verbunden.</summary>
    public sealed record Status(bool Installed, bool LoggedIn, string Method, string Plan, string? Exe)
    {
        public bool Subscription => LoggedIn && Method is "claude.ai" or "oauth_token" or "abo-token";
        /// <summary>Verbunden im Sinne von Trendo: angemeldet UND über das Abo.</summary>
        public bool Ready => Subscription;
        public static readonly Status Missing = new(false, false, "", "", null);
        /// <summary>Kurztext für Leiste und Einstellungen.</summary>
        public string Text => !Installed ? "Claude Code ist nicht installiert." : !LoggedIn ? "Claude Code ist installiert, aber nicht angemeldet." :
            !Subscription ? "Claude Code ist über die kostenpflichtige API/Console angemeldet – Trendo nutzt Claude nur über dein Abo. Bitte mit „Mit Claude verbinden“ über das Abo anmelden." :
            $"✓ Mit deinem Claude-Abo verbunden{(Plan.Length > 0 ? $" ({char.ToUpperInvariant(Plan[0])}{Plan[1..]})" : "")}";
    }

    /// <summary>Wo Claude Code liegen kann (offizieller Installer, npm, PATH). <paramref name="env"/> liefert Umgebungsvariablen.</summary>
    public static IEnumerable<string> Candidates(Func<string, string?> env)
    {
        if (env("USERPROFILE") is { Length: > 0 } home) yield return Path.Combine(home, ".local", "bin", "claude.exe");
        if (env("APPDATA") is { Length: > 0 } ad) yield return Path.Combine(ad, "npm", "claude.cmd");
        if (env("LOCALAPPDATA") is { Length: > 0 } la) yield return Path.Combine(la, "Programs", "claude", "claude.exe");
        foreach (var d in (env("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        { yield return Path.Combine(d.Trim(), "claude.exe"); yield return Path.Combine(d.Trim(), "claude.cmd"); }
    }

    public static string? Find(Func<string, string?> env, Func<string, bool> exists) => Candidates(env).FirstOrDefault(exists);

    /// <summary>Umgebungsvariablen, die Claude Code auf die kostenpflichtige API umschalten würden – werden für jeden Lauf entfernt (nur Abo).</summary>
    public static readonly string[] ApiVariables = ["ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_BASE_URL", "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX", "CLAUDE_CODE_USE_FOUNDRY",
        "ANTHROPIC_BEDROCK_BASE_URL", "ANTHROPIC_VERTEX_PROJECT_ID", "ANTHROPIC_PROFILE", "ANTHROPIC_FEDERATION_RULE_ID", "ANTHROPIC_ORGANIZATION_ID", "ANTHROPIC_IDENTITY_TOKEN_FILE"];

    /// <summary>Claude Code aus Trendo heraus nutzen (Entscheidung des Projektinhabers, 01.11.2026, nach Recherche): Anthropic erlaubt, dass ein Produkt das
    /// unveränderte Claude-Code-Programm mitbenutzt, wenn sich jeder Nutzer selbst über Anthropics eigene Anmeldung mit seinem Abo anmeldet
    /// (code.claude.com/docs/en/legal-and-compliance); „claude -p“ und Drittprogramme zählen dann auf das Abo-Kontingent (support.claude.com, Artikel 15036540, Stand 07.10.2026).
    /// Verboten bleiben: claude.ai-Anmeldung in Trendo anbieten, Anmeldedaten oder Tokens sammeln/speichern/weiterreichen, Nutzung für andere bezahlen oder vermitteln.
    /// Deshalb: Trendo startet nur das installierte Claude Code, angemeldet wird mit „claude auth login“, Trendo speichert keine Tokens; jede Person hat ihre eigene Anmeldung.</summary>
    public const bool EmbeddingApproved = true;

    /// <summary>Argumente, um das echte Claude Code sichtbar (interaktiv) im Projektordner zu öffnen – mit Auftrag und Rolle des Programms. Kein -p: der Nutzer arbeitet selbst darin.</summary>
    public static IReadOnlyList<string> OpenArgs(string? prompt, string? system)
    {
        var a = new List<string>();
        if (system is { Length: > 0 }) { a.Add("--append-system-prompt"); a.Add(system); }
        if (prompt is { Length: > 0 } && prompt.Trim().Length > 0) a.Add(prompt.Trim());
        return a;
    }
    /// <summary>Token-Variable von Claude Code – Trendo setzt sie nie und entfernt sie für jeden Lauf (Anmeldung nur über Anthropics eigenen Ablauf, keine Tokens in Trendo).</summary>
    public const string TokenVariable = "CLAUDE_CODE_OAUTH_TOKEN";

    /// <summary>Eigene Claude-Code-Anmeldung je Trendo-Person: Claude Code liest sie aus <see cref="ConfigVariable"/>. Die Hauptperson nutzt die normale
    /// Anmeldung des Windows-Kontos; jede weitere Person bekommt einen eigenen Ordner in ihrem Datenordner (sonst wäre sie mit dem Claude der Hauptperson verbunden).</summary>
    public const string ConfigVariable = "CLAUDE_CONFIG_DIR";
    public static string? PersonConfigDir(string dataRoot) => PersonStore.IdOf(dataRoot) is null ? null : Path.Combine(dataRoot, "claude-code");


    /// <summary>Antwort von <c>claude auth status --json</c> lesen. Nur die nötigen Felder; E-Mail und Organisation werden bewusst ignoriert.</summary>
    public static Status ParseStatus(string exe, string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json); var r = d.RootElement;
            string S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
            return new(true, r.TryGetProperty("loggedIn", out var li) && li.ValueKind == JsonValueKind.True, S("authMethod"), S("subscriptionType"), exe);
        }
        catch (JsonException) { return new(true, false, "", "", exe); }
    }

    /// <summary>Plan: lesen und planen; Build: freigegebenen Plan umsetzen; Reply: Antwort auf eine Rückfrage in derselben Sitzung; Ask: Frage beantworten (Haupt-KI in jedem Programm);
    /// Chat: Gespräch im Claude-Bereich eines Studios wie in Claude – liest und ändert Dateien im Projektordner, Fortsetzung über die Sitzung.</summary>
    public enum Phase { Plan, Build, Reply, Ask, Chat }

    /// <summary>Sicher (Standard) oder volle Claude-Funktionen (je Projekt freigegeben): Befehle, Web, eigene Skills/Plugins/MCP, Unteragenten – Rechte über Claude Codes Auto-Modus.</summary>
    public enum Level { Sicher, Voll }

    public const string PlanRule = "Arbeite wie Claude Code, aber in Trendo OS. Phase 1: Lies dir das Projekt an, was du brauchst, und ändere NICHTS. " +
        "Antworte mit einem kurzen Plan als nummerierte Liste (eine Zeile je Schritt, deutsch, verständlich). Ist etwas unklar, stelle am Ende genau eine Frage, die man mit Ja oder Nein beantworten kann.";
    public const string AskRule = "Du bist die Haupt-KI in Trendo OS. Antworte auf Deutsch, klar und hilfreich. Ändere keine Dateien.";
    public const string BuildRule = "Der Plan ist freigegeben. Setze ihn jetzt Schritt für Schritt um, führe eine Aufgabenliste (TodoWrite) und arbeite nur im Projektordner. Fasse am Ende in zwei Sätzen zusammen, was du geändert hast.";

    /// <summary>Argumente für einen Lauf. Plan: Datei-Änderungen gesperrt. Build/Reply: dieselbe Sitzung fortsetzen, Änderungen im Projektordner erlaubt.
    /// <paramref name="system"/>: Rolle und Skills des Programms. <paramref name="model"/>: nur wenn der Nutzer eins gewählt hat.</summary>
    public static IReadOnlyList<string> Args(string prompt, Phase phase, string? session = null, string? system = null, string? model = null, Level level = Level.Sicher)
    {
        if (phase is Phase.Build or Phase.Reply && string.IsNullOrWhiteSpace(session)) throw new ArgumentException("Zum Umsetzen braucht es die Sitzung aus dem Plan.");
        var a = new List<string> { "-p", phase == Phase.Build ? BuildRule + (prompt.Trim().Length > 0 ? "\nZusatz vom Nutzer: " + prompt.Trim() : "") : prompt.Trim(),
            "--output-format", "stream-json", "--verbose" };
        // Sicher: keine Befehle, kein Web, Datei-Werkzeuge nur im Projektordner, keine fremden Einstellungen/Erweiterungen.
        // Voll: alles, was Claude Code kann (auch eigene Skills, Plugins, MCP) – Rechte über den Auto-Modus von Claude Code.
        if (level == Level.Sicher) { a.Add("--restricted"); a.Add("--strict-mcp-config"); }
        if (session is { Length: > 0 }) { a.Add("--resume"); a.Add(session); }
        var rule = phase switch { Phase.Plan => PlanRule, Phase.Ask => AskRule, _ => "" };
        a.Add("--append-system-prompt"); a.Add(rule + (system is { Length: > 0 } ? "\n" + system : ""));
        if (phase is Phase.Plan or Phase.Ask) { a.Add("--disallowedTools"); a.Add("Edit Write NotebookEdit MultiEdit"); }
        else { a.Add("--permission-mode"); a.Add(level == Level.Voll ? "auto" : "acceptEdits"); }
        if (model is { Length: > 0 } && Regex.IsMatch(model, "^[a-z0-9.-]{2,60}$")) { a.Add("--model"); a.Add(model); }
        return a;
    }

    // ───────────── Live-Ausgabe ─────────────

    public abstract record Ev;
    public sealed record Init(string Session, string Model) : Ev;
    public sealed record Say(string Text) : Ev;
    /// <summary>Werkzeug-Aufruf, verständlich beschrieben. Bei Änderungen: Datei (relativ zum Projekt) und Vorher/Nachher.</summary>
    public sealed record Tool(string Name, string Text, string? File, string? Before, string? After, bool Changes) : Ev;
    public sealed record ToolDone(bool Error, string Text) : Ev;
    public sealed record Todo(IReadOnlyList<(string Text, string State)> Items) : Ev;
    /// <summary>Ende einer Antwort; <paramref name="Tokens"/>: verarbeitete Tokens (Eingabe, Ausgabe, Zwischenspeicher) – Maß für den Verbrauch am Abo-Kontingent.</summary>
    public sealed record Done(bool Ok, string Text, string Session, int Turns, int Tokens = 0) : Ev;

    /// <summary>Eine Zeile der stream-json-Ausgabe in Ereignisse übersetzen. Unbekanntes wird übergangen.</summary>
    public static IEnumerable<Ev> Parse(string line, string projectDir)
    {
        JsonObject? o;
        try { o = JsonNode.Parse(line) as JsonObject; } catch (JsonException) { yield break; }
        if (o is null) yield break;
        var type = (string?)o["type"]; var sub = (string?)o["subtype"];
        if (type == "system" && sub == "init") { yield return new Init((string?)o["session_id"] ?? "", (string?)o["model"] ?? ""); yield break; }
        if (type == "result")
        {
            int U(string k) => o["usage"]?[k] is JsonValue v && v.TryGetValue<int>(out var x) ? x : 0;
            yield return new Done(sub == "success" && o["is_error"]?.GetValue<bool>() != true, (string?)o["result"] ?? "", (string?)o["session_id"] ?? "", o["num_turns"] is JsonValue nt && nt.TryGetValue<int>(out var n) ? n : 0,
                U("input_tokens") + U("output_tokens") + U("cache_creation_input_tokens") + U("cache_read_input_tokens"));
            yield break;
        }
        if (o["message"]?["content"] is not JsonArray content) yield break;
        foreach (var c in content.OfType<JsonObject>())
        {
            var ct = (string?)c["type"];
            if (type == "assistant" && ct == "text" && (string?)c["text"] is { Length: > 0 } t) yield return new Say(t.Trim());
            else if (type == "assistant" && ct == "tool_use" && c["input"] is JsonObject inp) { if (ToolEvent((string?)c["name"] ?? "", inp, projectDir) is { } ev) yield return ev; }
            else if (type == "user" && ct == "tool_result")
            {
                var txt = c["content"] is JsonArray arr ? string.Join(" ", arr.OfType<JsonObject>().Select(x => (string?)x["text"] ?? "")) : c["content"]?.ToString() ?? "";
                yield return new ToolDone(c["is_error"]?.GetValue<bool>() == true, txt.Length > 300 ? txt[..300] + "…" : txt);
            }
        }
    }

    static Ev? ToolEvent(string name, JsonObject inp, string projectDir)
    {
        string? Rel(string? p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            try { var full = Path.GetFullPath(p); var root = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full[root.Length..].Replace('\\', '/') : Path.GetFileName(full); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return Path.GetFileName(p); }
        }
        var file = Rel((string?)inp["file_path"] ?? (string?)inp["notebook_path"]);
        switch (name)
        {
            case "TodoWrite" when inp["todos"] is JsonArray todos:
                return new Todo(todos.OfType<JsonObject>().Select(t => ((string?)t["content"] ?? "", (string?)t["status"] ?? "pending")).ToList());
            case "Read": return new Tool(name, $"liest {file}", file, null, null, false);
            case "Glob": return new Tool(name, $"sucht Dateien ({(string?)inp["pattern"]})", null, null, null, false);
            case "Grep": return new Tool(name, $"durchsucht den Code nach „{(string?)inp["pattern"]}“", null, null, null, false);
            case "Edit": return new Tool(name, $"ändert {file}", file, (string?)inp["old_string"], (string?)inp["new_string"], true);
            case "MultiEdit": return new Tool(name, $"ändert {file} an mehreren Stellen", file, null, null, true);
            case "Write": return new Tool(name, $"schreibt {file}", file, null, (string?)inp["content"], true);
            case "NotebookEdit": return new Tool(name, $"ändert {file}", file, null, null, true);
            case "ToolSearch": return null;
            default: return new Tool(name, $"nutzt {name}", file, null, null, false);
        }
    }

    [GeneratedRegex(@"^\s*(?:\d{1,2}[.)]|[-*•])\s+(.+)$", RegexOptions.Multiline)] private static partial Regex ListLine();

    /// <summary>Meldet Claude, dass das Abo-Kontingent gerade aufgebraucht ist? Dann sagt Trendo das verständlich statt einer Fehlermeldung.</summary>
    public static bool IsLimit(string? text) => text is not null && Regex.IsMatch(text, @"usage limit|rate limit|limit (?:reached|exceeded)|out of (?:extra )?usage|5-hour limit|weekly limit", RegexOptions.IgnoreCase);
    public const string LimitText = "Dein Claude-Kontingent ist für den Moment aufgebraucht. Es füllt sich von selbst wieder auf – versuch es etwas später noch einmal (Stand siehst du in Claude unter Einstellungen → Nutzung).";

    /// <summary>Planschritte aus der Antwort der Planphase (nummerierte oder Aufzählungs-Zeilen); dazu die Rückfrage, falls die Antwort mit einer Frage endet.</summary>
    public static (IReadOnlyList<string> Steps, string? Question) PlanOf(string text)
    {
        var steps = ListLine().Matches(text).Select(m => Regex.Replace(m.Groups[1].Value.Trim(), @"\*\*(.+?)\*\*", "$1")).Where(s => s.Length > 0).ToList();
        var last = text.Trim().Split('\n').LastOrDefault()?.Trim() ?? "";
        return (steps, last.EndsWith('?') && !ListLine().IsMatch(last) ? last : null);
    }
}

/// <summary>Sicherung eines Projektordners vor einem KI-Lauf: „alles rückgängig“ stellt geänderte und gelöschte Dateien wieder her und entfernt neue.
/// Große Ordner (node_modules, .git, bin, obj) und Dateien über 20 MB bleiben außen vor.</summary>
public sealed class FolderSnapshot
{
    static readonly string[] SkipDirs = ["node_modules", ".git", "bin", "obj", ".venv", "__pycache__", "trendo-bin"];
    public const long MaxFile = 20L << 20;
    public string Root { get; }
    public string Store { get; }
    readonly Dictionary<string, (long Len, DateTime At)> _files = new(StringComparer.OrdinalIgnoreCase);

    FolderSnapshot(string root, string store) { Root = Path.GetFullPath(root); Store = store; }

    static IEnumerable<string> Files(string root) =>
        Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(f => !Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar).Any(p => SkipDirs.Contains(p, StringComparer.OrdinalIgnoreCase)));

    public static FolderSnapshot Take(string root, string store)
    {
        var s = new FolderSnapshot(root, store);
        if (Directory.Exists(store)) Directory.Delete(store, true);
        Directory.CreateDirectory(store);
        foreach (var f in Files(s.Root))
        {
            var fi = new FileInfo(f); if (fi.Length > MaxFile) continue;
            var rel = Path.GetRelativePath(s.Root, f); var dst = Path.Combine(store, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.Copy(f, dst, true);
            s._files[rel] = (fi.Length, fi.LastWriteTimeUtc);
        }
        return s;
    }

    /// <summary>Was sich seit der Sicherung geändert hat (relativ, mit „neu“, „geändert“, „gelöscht“).</summary>
    public IReadOnlyList<(string File, string Change)> Changes()
    {
        var now = Files(Root).Where(f => new FileInfo(f).Length <= MaxFile).ToDictionary(f => Path.GetRelativePath(Root, f), f => f, StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, string)>();
        foreach (var (rel, f) in now)
            if (!_files.ContainsKey(rel)) list.Add((rel.Replace('\\', '/'), "neu"));
            else if (!File.ReadAllBytes(f).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(Store, rel)))) list.Add((rel.Replace('\\', '/'), "geändert"));
        foreach (var rel in _files.Keys.Where(k => !now.ContainsKey(k))) list.Add((rel.Replace('\\', '/'), "gelöscht"));
        return list.OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string Original(string rel) => File.ReadAllText(Path.Combine(Store, rel.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Alles zurück auf den Stand der Sicherung.</summary>
    public void Restore()
    {
        foreach (var (rel, change) in Changes())
        {
            var target = Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (change == "neu") File.Delete(target);
            else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(Path.Combine(Store, rel.Replace('/', Path.DirectorySeparatorChar)), target, true); }
        }
    }
}
