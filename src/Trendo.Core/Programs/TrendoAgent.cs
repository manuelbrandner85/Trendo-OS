using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Rückfrage im Plan: Frage und Antwortknöpfe (Empfehlung zuerst).</summary>
public sealed record AgentAsk(string Question, IReadOnlyList<string> Options);
/// <summary>Plan des Agenten: Schritte als Checkliste, Rückfragen, ein Satz Zusammenfassung.</summary>
public sealed record AgentPlan(IReadOnlyList<string> Steps, IReadOnlyList<AgentAsk> Questions, string Summary);
/// <summary>Vorgeschlagene Änderung einer Datei (relativ zum Projekt); Before = null bei neuer Datei.</summary>
public sealed record AgentChange(string Path, string? Before, string After, int Added, int Removed)
{
    public bool IsNew => Before is null;
}
/// <summary>Ergebnis einer Prüfung nach dem Übernehmen.</summary>
public sealed record AgentCheck(bool Ok, string Text);

/// <summary>Trendo-Agent wie Claude Code (T-0316, Wunsch 01.11.2026): arbeitet mit der KI, die Trendo gerade hat (lokal oder freie Quelle), in Web-, Spiele- und
/// Code-Projekten: Plan → Ja/Nein und Rückfragen → Änderungen als Vorher/Nachher je Datei → Übernehmen (Sicherung vorher) → Prüfen → Nachbessern.
/// Nur Textdateien im Projektordner, keine Befehle, keine Pfade nach draußen.</summary>
public static partial class TrendoAgent
{
    public const int MaxChanges = 12, MaxBytes = 300_000, MaxRounds = 3;
    static readonly string[] TextExt = [".html", ".htm", ".css", ".js", ".mjs", ".ts", ".json", ".md", ".txt", ".py", ".cs", ".gd", ".xml", ".svg", ".yml", ".yaml", ".csv", ".ini", ".toml", ".lua"];
    static readonly string[] Skip = ["node_modules", ".git", "bin", "obj", ".claude", "dist", "build", "__pycache__", ".venv"];

    public static bool IsText(string path) => TextExt.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Textdateien des Projekts (relativ, mit „/“), ohne Abhängigkeits- und Build-Ordner, kleinste Tiefe zuerst.</summary>
    public static List<string> ProjectFiles(string root, int max = 80)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => System.IO.Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(r => !r.Split('/').SkipLast(1).Any(d => Skip.Contains(d, StringComparer.OrdinalIgnoreCase)) && IsText(r) && new FileInfo(System.IO.Path.Combine(root, r)).Length <= MaxBytes)
            .OrderBy(r => r.Count(c => c == '/')).ThenBy(r => r, StringComparer.OrdinalIgnoreCase).Take(max).ToList();
    }

    /// <summary>Projektinhalt für die KI: Dateiliste und die Inhalte bis zum Zeichenbudget (kleine, wichtige Dateien zuerst).</summary>
    public static string Context(string root, int budget = 24_000)
    {
        var files = ProjectFiles(root); var sb = new StringBuilder("Dateien im Projekt:\n");
        foreach (var f in files) sb.Append("- ").Append(f).Append('\n');
        foreach (var f in files.OrderBy(f => new FileInfo(System.IO.Path.Combine(root, f)).Length))
        {
            var text = File.ReadAllText(System.IO.Path.Combine(root, f));
            if (sb.Length + text.Length > budget) continue;
            sb.Append($"\n=== {f} ===\n").Append(text).Append('\n');
        }
        return sb.ToString();
    }

    public static string PlanPrompt(string goal, string role, string root) =>
        $"{role}\nDu arbeitest wie Claude Code im Projektordner. Erstelle zuerst einen kurzen Plan, ändere noch nichts.\n" +
        "Antworte NUR mit JSON: {\"zusammenfassung\":\"ein Satz\",\"plan\":[\"Schritt\", …],\"fragen\":[{\"frage\":\"…\",\"optionen\":[\"Empfehlung\",\"Alternative\"]}]}\n" +
        "Stelle Fragen nur, wenn etwas wirklich unklar ist (höchstens 3). Alles auf Deutsch.\n\n" +
        $"Auftrag: {goal}\n\n{Context(root)}";

    public static string ChangePrompt(string goal, AgentPlan plan, IReadOnlyDictionary<string, string> answers, string role, string root, string? feedback = null) =>
        $"{role}\nSetze den Plan jetzt um. Antworte NUR mit JSON: {{\"zusammenfassung\":\"was du geändert hast\",\"dateien\":[{{\"pfad\":\"relativer/pfad.html\",\"inhalt\":\"vollständiger neuer Inhalt\"}}]}}\n" +
        $"Regeln: nur Textdateien im Projektordner, jeweils der VOLLSTÄNDIGE Inhalt, höchstens {MaxChanges} Dateien, keine Befehle, keine Pfade mit „..“.\n\n" +
        $"Auftrag: {goal}\nPlan:\n{string.Join("\n", plan.Steps.Select((s, i) => $"{i + 1}. {s}"))}\n" +
        (answers.Count > 0 ? "Antworten auf deine Fragen:\n" + string.Join("\n", answers.Select(a => $"- {a.Key}: {a.Value}")) + "\n" : "") +
        (feedback is { Length: > 0 } ? $"\nDie Prüfung nach deiner letzten Änderung hat ergeben – bitte nachbessern:\n{feedback}\n" : "") +
        $"\n{Context(root)}";

    [GeneratedRegex(@"\{[\s\S]*\}")] private static partial Regex JsonBlock();
    [GeneratedRegex(@"^\s*(?:\d+[.)]|[-*•])\s+(.+)$", RegexOptions.Multiline)] private static partial Regex ListLine();

    static JsonElement? Json(string answer)
    {
        var t = answer.Trim(); var fence = Regex.Match(t, @"```(?:json)?\s*([\s\S]*?)```"); if (fence.Success) t = fence.Groups[1].Value;
        var m = JsonBlock().Match(t); if (!m.Success) return null;
        try { using var d = JsonDocument.Parse(m.Value, new JsonDocumentOptions { AllowTrailingCommas = true }); return d.RootElement.Clone(); }
        catch (JsonException) { return null; }
    }
    static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>Plan lesen: JSON, sonst nummerierte oder Spiegelstrich-Zeilen als Schritte.</summary>
    public static AgentPlan ParsePlan(string answer)
    {
        if (Json(answer) is { } j)
        {
            var steps = j.TryGetProperty("plan", out var p) && p.ValueKind == JsonValueKind.Array ? p.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.ToString()).Where(x => x.Trim().Length > 0).ToList() : [];
            var qs = new List<AgentAsk>();
            if (j.TryGetProperty("fragen", out var f) && f.ValueKind == JsonValueKind.Array)
                foreach (var q in f.EnumerateArray().Take(3))
                {
                    var opts = q.TryGetProperty("optionen", out var o) && o.ValueKind == JsonValueKind.Array ? o.EnumerateArray().Select(x => x.ToString()).Where(x => x.Length > 0).Take(4).ToList() : [];
                    if (S(q, "frage") is { Length: > 0 } text) qs.Add(new(text, opts.Count > 0 ? opts : ["Ja", "Nein"]));
                }
            if (steps.Count > 0) return new(steps, qs, S(j, "zusammenfassung"));
        }
        var lines = ListLine().Matches(answer).Select(m => m.Groups[1].Value.Trim()).Where(x => x.Length > 0).Take(12).ToList();
        return new(lines.Count > 0 ? lines : [answer.Trim()], [], "");
    }

    /// <summary>Pfad prüfen: relativ, im Projekt, Textdatei. Sonst null.</summary>
    public static string? SafePath(string root, string rel)
    {
        rel = rel.Trim().Replace('\\', '/').TrimStart('/');
        if (rel.Length == 0 || rel.Contains("..") || rel.Contains(':') || !IsText(rel) || rel.Split('/').Any(d => Skip.Contains(d, StringComparer.OrdinalIgnoreCase))) return null;
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, rel));
        return full.StartsWith(System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? rel : null;
    }

    /// <summary>Änderungen lesen (JSON „dateien“), unsichere Pfade und unveränderte Dateien weglassen; Hinweise zu Verworfenem in <paramref name="rejected"/>.</summary>
    public static List<AgentChange> ParseChanges(string answer, string root, List<string>? rejected = null)
    {
        var res = new List<AgentChange>();
        if (Json(answer) is not { } j || !j.TryGetProperty("dateien", out var arr) || arr.ValueKind != JsonValueKind.Array) { rejected?.Add("Die KI hat keine Dateien geliefert."); return res; }
        foreach (var f in arr.EnumerateArray())
        {
            var rel = SafePath(root, S(f, "pfad")); var text = S(f, "inhalt");
            if (rel is null) { rejected?.Add($"„{S(f, "pfad")}“ liegt nicht sicher im Projekt oder ist keine Textdatei – verworfen."); continue; }
            if (Encoding.UTF8.GetByteCount(text) > MaxBytes) { rejected?.Add($"„{rel}“ ist zu groß – verworfen."); continue; }
            var full = System.IO.Path.Combine(root, rel); var before = File.Exists(full) ? File.ReadAllText(full) : null;
            if (before == text || res.Any(c => c.Path == rel)) continue;
            var (add, rem) = Count(before ?? "", text);
            res.Add(new(rel, before, text, add, rem));
            if (res.Count >= MaxChanges) break;
        }
        return res;
    }

    /// <summary>Geänderte Zeilen zählen (längste gemeinsame Teilfolge der Zeilen).</summary>
    public static (int Added, int Removed) Count(string before, string after)
    {
        var d = Diff(before, after, int.MaxValue);
        return (d.Count(l => l.StartsWith('+')), d.Count(l => l.StartsWith('-')));
    }

    /// <summary>Unterschied Zeile für Zeile: „+ “ neu, „- “ weg, „  “ gleich (nur Zeilen in der Nähe von Änderungen); höchstens <paramref name="max"/> Zeilen.</summary>
    public static List<string> Diff(string before, string after, int max = 400)
    {
        string[] a = before.Length == 0 ? [] : before.Replace("\r", "").Split('\n'), b = after.Length == 0 ? [] : after.Replace("\r", "").Split('\n');
        if ((long)a.Length * b.Length > 4_000_000) return [.. a.Select(x => "- " + x).Concat(b.Select(x => "+ " + x)).Take(max)];
        var l = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--) for (var k = b.Length - 1; k >= 0; k--) l[i, k] = a[i] == b[k] ? l[i + 1, k + 1] + 1 : Math.Max(l[i + 1, k], l[i, k + 1]);
        var ops = new List<string>(); int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
            if (x < a.Length && y < b.Length && a[x] == b[y]) { ops.Add("  " + a[x]); x++; y++; }
            else if (y < b.Length && (x >= a.Length || l[x, y + 1] >= l[x + 1, y])) ops.Add("+ " + b[y++]);
            else ops.Add("- " + a[x++]);
        var keep = new bool[ops.Count];
        for (var i = 0; i < ops.Count; i++) if (ops[i][0] != ' ') for (var k = Math.Max(0, i - 2); k <= Math.Min(ops.Count - 1, i + 2); k++) keep[k] = true;
        var res = new List<string>(); var gap = false;
        for (var i = 0; i < ops.Count && res.Count < max; i++)
            if (keep[i]) { res.Add(ops[i]); gap = false; } else if (!gap) { res.Add("  …"); gap = true; }
        return res.Count == 1 && res[0] == "  …" ? [] : res;
    }

    public static void Apply(string root, IEnumerable<AgentChange> changes)
    {
        foreach (var c in changes)
        {
            var full = System.IO.Path.Combine(root, SafePath(root, c.Path) ?? throw new InvalidOperationException($"Unsicherer Pfad: {c.Path}"));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, c.After, new UTF8Encoding(false));
        }
    }

    [GeneratedRegex(@"(?:href|src)\s*=\s*[""']([^""'#?]+)[""']", RegexOptions.IgnoreCase)] private static partial Regex LocalRef();

    /// <summary>Prüfen nach dem Übernehmen: Webseiten (Titel, Sprache, lokale Verweise vorhanden), JSON gültig, Klammern in Skripten ausgeglichen.</summary>
    public static List<AgentCheck> Check(string root, IEnumerable<string> files)
    {
        var res = new List<AgentCheck>();
        foreach (var rel in files)
        {
            var full = System.IO.Path.Combine(root, rel); if (!File.Exists(full)) continue;
            var t = File.ReadAllText(full); var ext = System.IO.Path.GetExtension(rel).ToLowerInvariant();
            if (ext is ".html" or ".htm")
            {
                res.Add(Regex.IsMatch(t, @"<title>\s*\S", RegexOptions.IgnoreCase) ? new(true, $"{rel}: hat einen Seitentitel") : new(false, $"{rel}: kein <title> – Suchmaschinen und Browser-Reiter zeigen nichts"));
                if (!Regex.IsMatch(t, @"<html[^>]*\blang\s*=", RegexOptions.IgnoreCase)) res.Add(new(false, $"{rel}: <html> ohne lang=\"de\" – Vorlesehilfen erkennen die Sprache nicht"));
                if (!Regex.IsMatch(t, @"name\s*=\s*[""']viewport", RegexOptions.IgnoreCase)) res.Add(new(false, $"{rel}: kein viewport-Meta – auf dem Handy zu klein"));
                foreach (Match m in LocalRef().Matches(t))
                {
                    var r = m.Groups[1].Value.Trim(); if (r.Contains(':') || r.StartsWith("//") || r.Length == 0) continue;
                    var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full)!, r.TrimStart('/')));
                    if (!File.Exists(target) && !Directory.Exists(target)) res.Add(new(false, $"{rel}: Verweis auf „{r}“ – die Datei fehlt"));
                }
                foreach (Match m in Regex.Matches(t, @"<img\b(?![^>]*\balt\s*=)[^>]*>", RegexOptions.IgnoreCase)) res.Add(new(false, $"{rel}: Bild ohne alt-Text"));
            }
            else if (ext == ".json")
                try { using var _ = JsonDocument.Parse(t); res.Add(new(true, $"{rel}: gültiges JSON")); } catch (JsonException e) { res.Add(new(false, $"{rel}: JSON fehlerhaft – {e.Message}")); }
            else if (ext is ".js" or ".mjs" or ".ts" or ".css" or ".cs" or ".gd")
            {
                var code = Regex.Replace(t, @"//[^\n]*|/\*[\s\S]*?\*/|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`", "");
                foreach (var (o, c) in new[] { ('{', '}'), ('(', ')'), ('[', ']') })
                    if (code.Count(ch => ch == o) != code.Count(ch => ch == c)) res.Add(new(false, $"{rel}: Klammern {o}{c} nicht ausgeglichen"));
            }
        }
        if (res.Count == 0) res.Add(new(true, "Keine Auffälligkeiten gefunden."));
        return res;
    }

    public static string Feedback(IEnumerable<AgentCheck> checks) => string.Join("\n", checks.Where(c => !c.Ok).Select(c => "- " + c.Text));
}
