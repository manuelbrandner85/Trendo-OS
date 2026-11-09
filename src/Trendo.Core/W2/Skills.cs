using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>T-0317: Skills für professionelle Ergebnisse – Fachregeln je Studio als lesbare Markdown-Dateien (wie Skills in Claude):
/// Kopf (name, description, studios, lizenz, quelle), Fachteil und „## Checkliste“ für die Qualitätsprüfung.
/// Drei Quellen: mitgeliefert (im Programm), eigene (in der vorhandenen <see cref="PromptLibrary"/>, Kategorie „Skill“, mit Versionen),
/// geladen aus vertrauten Quellen (nur nach Zustimmung, mit Herkunft und Lizenz). Kein zweites Prompt-System.</summary>
public sealed record Skill(string Id, string Name, string Description, IReadOnlyList<string> Studios, string Body, IReadOnlyList<string> Checklist, string Origin, string License, string Source)
{
    public bool BuiltIn => Origin == "mitgeliefert";
    /// <summary>Fachgebiets-Skill: wird nur geladen, wenn die Aufgabe eines dieser Stichworte enthält. Leer = gilt immer für seine Studios.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];
    public bool AppliesTo(string? key, string? studio) => Studios.Contains("*") || key is not null && Studios.Contains(key, StringComparer.OrdinalIgnoreCase) || studio is not null && Studios.Contains(studio, StringComparer.OrdinalIgnoreCase);
}

public static partial class SkillFiles
{
    [GeneratedRegex(@"\A---\s*\n(.*?)\n---\s*\n?", RegexOptions.Singleline)] private static partial Regex Front();
    [GeneratedRegex(@"^##\s+Checkliste\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex CheckHead();
    [GeneratedRegex(@"[^a-z0-9-]")] private static partial Regex NotSlug();

    public static string Slug(string s)
    {
        var t = s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss").Replace(' ', '-');
        t = NotSlug().Replace(t, ""); while (t.Contains("--")) t = t.Replace("--", "-");
        return t.Trim('-') is { Length: > 0 } x ? x[..Math.Min(60, x.Length)] : "skill";
    }

    /// <summary>Skill aus Markdown lesen. Fehlt der Kopf, gilt die erste Überschrift als Name.</summary>
    public static Skill Parse(string markdown, string origin, string? fallbackId = null)
    {
        var text = markdown.Replace("\r\n", "\n");
        var head = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Front().Match(text) is { Success: true } m)
        {
            foreach (var line in m.Groups[1].Value.Split('\n'))
                if (line.IndexOf(':') is var i and > 0) head[line[..i].Trim()] = line[(i + 1)..].Trim().Trim('"');
            text = text[m.Length..];
        }
        var name = head.GetValueOrDefault("name") is { Length: > 0 } n ? n : Regex.Match(text, @"^#\s+(.+)$", RegexOptions.Multiline) is { Success: true } h ? h.Groups[1].Value.Trim() : fallbackId ?? "Skill";
        var studios = (head.GetValueOrDefault("studios") ?? "*").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var checklist = new List<string>(); var body = text.Trim();
        if (CheckHead().Match(text) is { Success: true } ch)
        {
            var rest = text[(ch.Index + ch.Length)..];
            var end = Regex.Match(rest, @"^##\s", RegexOptions.Multiline) is { Success: true } nx ? nx.Index : rest.Length;
            checklist = rest[..end].Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("- ") || l.StartsWith("* ")).Select(l => Regex.Replace(l[2..], @"^\[[ xX]\]\s*", "").Trim()).Where(l => l.Length > 0).ToList();
        }
        return new Skill(Slug(head.GetValueOrDefault("id") ?? fallbackId ?? name), name, head.GetValueOrDefault("description") ?? "", studios, body, checklist, origin,
            head.GetValueOrDefault("lizenz") ?? head.GetValueOrDefault("license") ?? "", head.GetValueOrDefault("quelle") ?? head.GetValueOrDefault("source") ?? "")
        { Keywords = (head.GetValueOrDefault("stichworte") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) };
    }

    /// <summary>Als Markdown mit Kopf schreiben (eigene und geladene Skills).</summary>
    public static string ToMarkdown(string name, string description, IEnumerable<string> studios, string body, IEnumerable<string> checklist, string? license = null, string? source = null)
    {
        var sb = new StringBuilder("---\n");
        sb.Append($"name: {name.Trim()}\ndescription: {description.Trim().Replace('\n', ' ')}\nstudios: {string.Join(", ", studios)}\n");
        if (license is { Length: > 0 }) sb.Append($"lizenz: {license}\n");
        if (source is { Length: > 0 }) sb.Append($"quelle: {source}\n");
        sb.Append("---\n\n").Append(body.Trim()).Append('\n');
        var items = checklist.Where(c => c.Trim().Length > 0).ToList();
        if (items.Count > 0 && !CheckHead().IsMatch(body)) { sb.Append("\n## Checkliste\n"); foreach (var c in items) sb.Append("- ").Append(c.Trim()).Append('\n'); }
        return sb.ToString();
    }

    /// <summary>Format, das Claude Code als Skill liest (<c>.claude/skills/&lt;name&gt;/SKILL.md</c>: Kopf mit name und description).</summary>
    public static string ToClaudeSkill(Skill s) =>
        $"---\nname: {s.Id}\ndescription: {(s.Description.Length > 0 ? s.Description : s.Name).Replace('\n', ' ')}\n---\n\n{s.Body}\n";
}

/// <summary>Bibliothek aller Skills: mitgelieferte (eingebettet), eigene (PromptLibrary, Kategorie „Skill“), geladene (Datenordner/skills).</summary>
public sealed class SkillLibrary(string dataRoot, PromptLibrary prompts, AuditLog audit)
{
    public const string Category = "Skill";
    public const int MaxImportBytes = 100_000;
    /// <summary>Höchstlänge der Fachregeln, die einer KI mitgegeben werden (Zeichen).</summary>
    public const int MaxPromptChars = 7000;
    string LoadedDir => Path.Combine(dataRoot, "skills");

    static IReadOnlyList<Skill>? _builtIn;
    public static IReadOnlyList<Skill> BuiltIn => _builtIn ??= typeof(SkillLibrary).Assembly.GetManifestResourceNames()
        .Where(n => n.Contains(".Skills.", StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n)
        .Select(n => { using var s = typeof(SkillLibrary).Assembly.GetManifestResourceStream(n)!; using var r = new StreamReader(s, Encoding.UTF8); return SkillFiles.Parse(r.ReadToEnd(), "mitgeliefert", n.Split('.')[^2]); })
        .ToList();

    public IReadOnlyList<Skill> All()
    {
        var list = new List<Skill>(BuiltIn);
        list.AddRange(prompts.All().Where(p => p.Category == Category).Select(p => SkillFiles.Parse(p.Text, "eigen", p.Id) with { Id = p.Id }));
        if (Directory.Exists(LoadedDir))
            foreach (var f in Directory.GetFiles(LoadedDir, "*.md").OrderBy(f => f))
                try { list.Add(SkillFiles.Parse(File.ReadAllText(f), "geladen", Path.GetFileNameWithoutExtension(f)) with { Id = "geladen-" + Path.GetFileNameWithoutExtension(f) }); }
                catch (IOException) { }
        return list;
    }

    /// <summary>Skills für eine Seite bzw. ein Studio (eigene und geladene vor mitgelieferten, damit eigene Regeln Vorrang haben).</summary>
    public IReadOnlyList<Skill> For(string? key, string? studio) =>
        All().Where(s => s.Keywords.Count == 0 && s.AppliesTo(key, studio) && !s.Studios.Contains("*")).OrderBy(s => s.BuiltIn ? 1 : 0).ThenBy(s => s.Name).ToList();

    /// <summary>Höchstlänge der Fachregeln für eine KI auf diesem PC (Zeichen): kleine Modelle haben wenig Platz für Anweisungen.</summary>
    public const int MaxPromptCharsLocal = 2400;

    /// <summary>Skills passend zur Aufgabe: eigene zuerst, dann die Fachgebiete mit den meisten Treffern, zuletzt die Grundregeln.</summary>
    public IReadOnlyList<Skill> ForTask(string? key, string? studio, string? task)
    {
        var always = For(key, studio);
        var topical = string.IsNullOrWhiteSpace(task) ? [] : All()
            .Where(s => s.Keywords.Count > 0 && s.AppliesTo(key, studio) && !s.Studios.Contains("*"))
            .Select(s => (Skill: s, Hits: s.Keywords.Count(k => task.Contains(k, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Hits > 0).OrderByDescending(x => x.Hits).ThenBy(x => x.Skill.Name).Select(x => x.Skill).ToList();
        return [.. always.Where(s => !s.BuiltIn), .. topical, .. always.Where(s => s.BuiltIn)];
    }

    /// <summary>Fachregeln passend zur Aufgabe, höchstens <paramref name="maxChars"/> Zeichen und nur ganze Zeilen. Unter dem Standardwert (lokale KI) entfällt die Checkliste, weil Trendo selbst prüft.</summary>
    public string? PromptText(string? key, string? studio, string? task, int maxChars = MaxPromptChars)
    {
        var skills = ForTask(key, studio, task); if (skills.Count == 0) return null;
        var sb = new StringBuilder();
        foreach (var s in skills)
        {
            var body = maxChars < MaxPromptChars ? Regex.Split(s.Body, @"^##\s+Checkliste\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)[0] : s.Body;
            foreach (var line in $"### Skill: {s.Name}\n{body.Trim()}\n".Split('\n'))
            {
                if (sb.Length + line.Length + 1 > maxChars) return sb.ToString().Trim();
                sb.Append(line).Append('\n');
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Fachregeln als Text für die KI (gekürzt auf <see cref="MaxPromptChars"/>).</summary>
    public string? PromptText(string? key, string? studio)
    {
        var skills = For(key, studio); if (skills.Count == 0) return null;
        var sb = new StringBuilder();
        foreach (var s in skills)
        {
            var part = $"### Skill: {s.Name}\n{s.Body}\n";
            if (sb.Length + part.Length > MaxPromptChars) { sb.Append(part[..Math.Max(0, MaxPromptChars - sb.Length)]); break; }
            sb.Append(part);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Checkliste für die Qualitätsprüfung (aus allen passenden Skills, ohne Doppelte).</summary>
    public IReadOnlyList<(string Skill, string Item)> Checklist(string? key, string? studio) =>
        For(key, studio).SelectMany(s => s.Checklist.Select(c => (s.Name, c))).DistinctBy(x => x.c.ToLowerInvariant()).Take(20).ToList();

    /// <summary>Eigenen Skill anlegen oder als neue Version speichern (PromptLibrary: Versionen und Wiederherstellen inklusive).</summary>
    public Skill SaveOwn(string? id, string name, string description, IEnumerable<string> studios, string body, IEnumerable<string> checklist)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte einen Namen für den Skill.");
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Bitte die Fachregeln eintragen.");
        var md = SkillFiles.ToMarkdown(name, description, studios.Where(s => s.Trim().Length > 0).DefaultIfEmpty("*"), body, checklist);
        var t = prompts.Save(id, name.Trim(), Category, md, id is null ? "angelegt" : "geändert");
        return SkillFiles.Parse(t.Text, "eigen", t.Id) with { Id = t.Id };
    }

    public bool DeleteOwn(string id) => prompts.Get(id) is { Category: Category } && prompts.Delete(id);

    /// <summary>Skill aus einer vertrauten Quelle übernehmen (nach Zustimmung): nur Text, höchstens 100 KB, mit Herkunft und Lizenz.</summary>
    public Skill Import(string markdown, string sourceUrl, string license)
    {
        if (Encoding.UTF8.GetByteCount(markdown) > MaxImportBytes) throw new InvalidDataException("Der Skill ist zu groß (höchstens 100 KB).");
        if (markdown.Contains('\0')) throw new InvalidDataException("Das ist keine Textdatei.");
        var s = SkillFiles.Parse(markdown, "geladen");
        var md = SkillFiles.ToMarkdown(s.Name, s.Description, s.Studios, s.Body, s.Checklist, license, sourceUrl);
        Directory.CreateDirectory(LoadedDir);
        var f = Path.Combine(LoadedDir, s.Id + ".md");
        File.WriteAllText(f + ".tmp", md); SafeFile.Move(f + ".tmp", f);
        audit.Append(Principal.LocalOwner.Name, "skill.import", $"{s.Id} quelle={sourceUrl} lizenz={license}");
        return SkillFiles.Parse(md, "geladen", s.Id) with { Id = "geladen-" + s.Id };
    }

    public bool RemoveLoaded(string id)
    {
        var f = Path.Combine(LoadedDir, id.StartsWith("geladen-") ? id["geladen-".Length..] : id) + ".md";
        if (!File.Exists(f) || Path.GetDirectoryName(Path.GetFullPath(f)) != Path.GetFullPath(LoadedDir)) return false;
        File.Delete(f); audit.Append(Principal.LocalOwner.Name, "skill.remove", id); return true;
    }

    /// <summary>Skill prüfen wie die Regeln für Claude Skills und GPTs (Clean-Room, 05.11.2026): kurzer Name, Beschreibung sagt, wann der Skill gilt,
    /// genug Fachregeln, mindestens drei Prüfpunkte, nicht zu lang für die KI. Leere Liste = alles gut.</summary>
    public static List<string> Lint(Skill s)
    {
        var h = new List<string>();
        if (s.Name.Trim().Length > 64) h.Add("Der Name ist länger als 64 Zeichen – kürzer ist leichter zu finden.");
        if (s.Description.Trim().Length == 0) h.Add("Die Beschreibung fehlt: Sie sagt der KI, wann der Skill gilt (z. B. „Für Briefe an Behörden“).");
        else if (s.Description.Length > 1024) h.Add("Die Beschreibung ist länger als 1024 Zeichen – bitte auf eine Zeile kürzen.");
        if (s.Body.Trim().Length < 60) h.Add("Die Fachregeln sind sehr kurz – mindestens ein paar klare Stichpunkte helfen der KI.");
        if (s.Body.Length > MaxPromptChars) h.Add($"Die Fachregeln sind länger als {MaxPromptChars} Zeichen – die KI bekommt nur den Anfang.");
        if (s.Checklist.Count < 3) h.Add("Weniger als drei Prüfpunkte – mit einer Checkliste prüft die KI, ob das Ergebnis passt.");
        return h;
    }

    /// <summary>Eigenen oder geladenen Skill als Datei sichern (SKILL.md-Format) – zum Weitergeben.</summary>
    public string ExportFile(string id, string path)
    {
        var s = All().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Diesen Skill gibt es nicht.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, SkillFiles.ToMarkdown(s.Name, s.Description, s.Studios, s.Body, s.Checklist, s.License.Length > 0 ? s.License : null, s.Source.Length > 0 ? s.Source : null));
        audit.Append(Principal.LocalOwner.Name, "skill.export", Path.GetFileName(path));
        return path;
    }

    /// <summary>Skill aus einer eigenen Datei laden (.md, höchstens 100 KB, nur Text) – erscheint unter „Geladene Skills“.</summary>
    public Skill ImportFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Die Datei gibt es nicht.");
        if (Path.GetExtension(path).ToLowerInvariant() is not (".md" or ".txt")) throw new InvalidDataException("Bitte eine Skill-Datei (.md).");
        if (new FileInfo(path).Length > MaxImportBytes) throw new InvalidDataException("Der Skill ist zu groß (höchstens 100 KB).");
        return Import(File.ReadAllText(path), "Datei " + Path.GetFileName(path), "vom Absender");
    }

    /// <summary>Passende Skills in einen Projektordner legen, damit Claude Code sie als eigene Skills nutzt (<c>.claude/skills/&lt;id&gt;/SKILL.md</c>). Liefert die Anzahl.</summary>
    public int ExportForClaude(string projectDir, string? key, string? studio)
    {
        var n = 0;
        foreach (var s in All().Where(x => x.AppliesTo(key, studio) && !x.Studios.Contains("*"))) // auch Fachgebiete: Claude Code wählt selbst nach der Beschreibung
        {
            var d = Path.Combine(projectDir, ".claude", "skills", "trendo-" + s.Id); Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d, "SKILL.md"), SkillFiles.ToClaudeSkill(s)); n++;
        }
        return n;
    }
}
