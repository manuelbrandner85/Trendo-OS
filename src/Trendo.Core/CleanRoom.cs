using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>X-10: Sauberer Nachbau (Clean Room) – getrennte Rollen, Beschreibung statt Code, Tagebuch, Ähnlichkeitsprüfung gegen das Original. Keine Rechtsberatung.</summary>
public sealed record JournalEntry(DateTimeOffset At, string Role, string Text);
public sealed record Similar(string Ours, string Theirs, double Score);

public static partial class CleanRoom
{
    public static readonly string[] Roles = ["Beschreiben", "Umsetzen", "Prüfen"];
    public static readonly string[] Rules =
    [
        "Wer das Original ansieht, schreibt nur die Beschreibung – kein Code, keine Kopien.",
        "Wer umsetzt, sieht nur die Beschreibung – nie das Original.",
        "Die Beschreibung sagt WAS das Programm tut (Verhalten, Formate, Bildschirme), nicht WIE es intern gebaut ist.",
        "Keine Namen, Logos oder Texte des Originals übernehmen (Marke, Urheberrecht).",
        "Original nur so untersuchen, wie es Lizenz und Gesetz erlauben (z. B. § 69d/§ 69e UrhG).",
        "Jeder Schritt kommt ins Tagebuch – mit Rolle und Zeit.",
        "Vor der Veröffentlichung: Ähnlichkeitsprüfung gegen das Original und eine rechtliche Prüfung durch Fachleute.",
    ];

    public static string Create(string parent, string name, string original)
    {
        var d = Path.Combine(parent, DevStudio.SafeName(name));
        if (Directory.Exists(d)) throw new IOException($"Das Projekt „{Path.GetFileName(d)}“ gibt es schon.");
        foreach (var s in new[] { "1-Beschreibung", "2-Umsetzung", "3-Pruefung" }) Directory.CreateDirectory(Path.Combine(d, s));
        File.WriteAllText(Path.Combine(d, "1-Beschreibung", "BESCHREIBUNG.md"),
            $"# Beschreibung: {name}\n\nVorbild (nur zur Einordnung): {original}\n\n## Was soll es tun?\n\n## Bildschirme und Bedienung\n\n## Dateiformate und Schnittstellen\n\n## Nicht übernehmen\n- Namen, Logos, Texte, Code des Originals\n");
        File.WriteAllText(Path.Combine(d, "REGELN.md"), "# Regeln für den sauberen Nachbau\n\n" + string.Concat(Rules.Select((r, i) => $"{i + 1}. {r}\n")) + "\nKeine Rechtsberatung.\n");
        Log(d, "Prüfen", $"Projekt angelegt. Vorbild: {original}");
        return d;
    }

    static string JournalFile(string d) => Path.Combine(d, "tagebuch.jsonl");

    public static void Log(string dir, string role, string text)
    {
        if (!Roles.Contains(role)) throw new ArgumentException("Unbekannte Rolle");
        File.AppendAllText(JournalFile(dir), JsonSerializer.Serialize(new JournalEntry(DateTimeOffset.Now, role, text.Replace('\n', ' '))) + "\n");
    }

    public static IReadOnlyList<JournalEntry> Journal(string dir) =>
        File.Exists(JournalFile(dir)) ? File.ReadAllLines(JournalFile(dir)).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<JournalEntry>(l)!).ToList() : [];

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/|#[^\n]*|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'", RegexOptions.Singleline)] private static partial Regex Noise();
    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*|\d+|[^\sA-Za-z0-9_]")] private static partial Regex Tok();
    static readonly HashSet<string> Kw = ["if", "else", "for", "while", "return", "class", "public", "private", "static", "void", "int", "var", "def", "function", "const", "let", "new", "import", "using", "namespace", "true", "false", "null", "string"];

    /// <summary>Tokens ohne Kommentare/Strings; Bezeichner werden zu „V“ normalisiert, damit Umbenennen nicht hilft.</summary>
    public static List<string> Tokens(string code) =>
        Tok().Matches(Noise().Replace(code, " ")).Select(m => m.Value).Select(t => char.IsLetter(t[0]) || t[0] == '_' ? (Kw.Contains(t) ? t : "V") : char.IsDigit(t[0]) ? "N" : t).ToList();

    public static HashSet<string> Shingles(string code, int k = 8)
    {
        var t = Tokens(code); var s = new HashSet<string>();
        for (var i = 0; i + k <= t.Count; i++) s.Add(string.Join(' ', t.Skip(i).Take(k)));
        return s;
    }

    /// <summary>Anteil gemeinsamer Abschnitte (Jaccard 0..1).</summary>
    public static double Similarity(string a, string b)
    {
        var x = Shingles(a); var y = Shingles(b);
        if (x.Count == 0 || y.Count == 0) return 0;
        return (double)x.Intersect(y).Count() / x.Union(y).Count();
    }

    static readonly string[] CodeExt = [".cs", ".js", ".ts", ".py", ".java", ".c", ".h", ".cpp", ".hpp", ".go", ".rs", ".php", ".rb", ".kt", ".swift"];

    public static IReadOnlyList<Similar> Compare(string ours, string theirs, double threshold = 0.25)
    {
        IEnumerable<string> Files(string d) => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Where(f => CodeExt.Contains(Path.GetExtension(f).ToLowerInvariant()) && new FileInfo(f).Length < 2_000_000);
        var th = Files(theirs).Select(f => (f, s: Shingles(File.ReadAllText(f)))).Where(x => x.s.Count > 0).ToList();
        var res = new List<Similar>();
        foreach (var f in Files(ours))
        {
            var s = Shingles(File.ReadAllText(f)); if (s.Count == 0) continue;
            foreach (var (tf, ts) in th)
            {
                var sc = (double)s.Intersect(ts).Count() / s.Union(ts).Count();
                if (sc >= threshold) res.Add(new(Path.GetRelativePath(ours, f), Path.GetRelativePath(theirs, tf), Math.Round(sc, 3)));
            }
        }
        return res.OrderByDescending(r => r.Score).ToList();
    }

    public static string Report(string dir, IReadOnlyList<Similar> sims, string? theirs)
    {
        var sb = new StringBuilder($"# Prüfbericht: {Path.GetFileName(dir)}\n\nErstellt: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}\n_Technische Hilfe, keine Rechtsberatung._\n\n## Ähnlichkeit zum Original\n");
        if (theirs is null) sb.AppendLine("Nicht geprüft (kein Original-Ordner angegeben).");
        else if (sims.Count == 0) sb.AppendLine("Keine Datei über der Schwelle (25 % gemeinsame Abschnitte).");
        else foreach (var s in sims) sb.AppendLine($"- ⚠ {s.Ours} ↔ {s.Theirs}: {s.Score:P0}");
        sb.AppendLine("\n## Tagebuch");
        foreach (var j in Journal(dir)) sb.AppendLine($"- {j.At:yyyy-MM-dd HH:mm} [{j.Role}] {j.Text}");
        var roles = Journal(dir).Select(j => j.Role).Distinct().ToList();
        sb.AppendLine("\n## Rollen").AppendLine(roles.Contains("Beschreiben") && roles.Contains("Umsetzen") ? "Beide Rollen haben Einträge." : "⚠ Es fehlen Einträge für Beschreiben oder Umsetzen.");
        var p = Path.Combine(dir, "3-Pruefung", "PRUEFBERICHT.md"); File.WriteAllText(p, sb.ToString()); return p;
    }
}
