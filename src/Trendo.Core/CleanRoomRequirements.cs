using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core;

/// <summary>T-0762 (X-10): Eine Anforderung des sauberen Nachbaus – WAS das eigene Programm tun soll (nie Code des Originals).
/// <see cref="Source"/> = Herkunft (z. B. „Handbuch S. 12“, „eigene Beobachtung“), <see cref="Date"/> = Tag der Erfassung (yyyy-MM-dd),
/// <see cref="Link"/> = verknüpfter Test bzw. Datei im Projekt („2-Umsetzung/test_notiz.py#test_speichern“), <see cref="Note"/> = Grund aus dem letzten Abgleich.</summary>
public sealed record Requirement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("funktion")] string Function,
    [property: JsonPropertyName("erwartet")] string Expected,
    [property: JsonPropertyName("pruefung")] string Check,
    [property: JsonPropertyName("quelle")] string Source,
    [property: JsonPropertyName("datum")] string Date,
    [property: JsonPropertyName("stufe")] string Stage,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("test")] string Link = "",
    [property: JsonPropertyName("abgleich")] string Note = "");

public static partial class CleanRoom
{
    public const string StatusOpen = "offen", StatusPartly = "teilweise", StatusDone = "erfüllt";
    public static readonly string[] Statuses = [StatusOpen, StatusPartly, StatusDone];
    public static readonly string[] Stages = ["Muss", "Soll", "Kann"];
    static readonly JsonSerializerOptions ReqJson = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string RequirementsFile(string dir) => Path.Combine(dir, "1-Beschreibung", "anforderungen.json");

    /// <summary>Liste laden; fehlende oder beschädigte Datei = leere Liste (die beschädigte bleibt liegen, wird erst beim Speichern ersetzt).</summary>
    public static List<Requirement> LoadRequirements(string dir)
    {
        var f = RequirementsFile(dir);
        try { return File.Exists(f) ? JsonSerializer.Deserialize<List<Requirement>>(File.ReadAllText(f), ReqJson) ?? [] : []; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Speichern ohne Datenverlust bei Absturz (<see cref="AtomicFile"/>).</summary>
    public static void SaveRequirements(string dir, IEnumerable<Requirement> list)
    {
        var f = RequirementsFile(dir); Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        AtomicFile.WriteAllText(f, JsonSerializer.Serialize(list.ToList(), ReqJson));
    }

    public static string NextRequirementId(IEnumerable<Requirement> list)
    {
        var max = list.Select(r => r.Id.StartsWith("A-", StringComparison.Ordinal) && int.TryParse(r.Id[2..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"A-{max + 1:000}";
    }

    public static Requirement NewRequirement(IEnumerable<Requirement> list, string function = "", string source = "eigene Beobachtung", DateTimeOffset? at = null) =>
        new(NextRequirementId(list), function, "", "", source, (at ?? DateTimeOffset.Now).ToString("yyyy-MM-dd"), Stages[0], StatusOpen);

    static string Cell(string s) => s.Replace("\r", "").Replace("\n", " ").Replace("|", "\\|").Trim();

    /// <summary>Markdown-Tabelle mit allen Spalten (ID, Funktion, erwartetes Verhalten, Prüfung, Quelle, Datum, Stufe, Status, Test, Abgleich).</summary>
    public static string RequirementsTable(IReadOnlyList<Requirement> list)
    {
        var sb = new StringBuilder("| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle (Herkunft) | Datum | Stufe | Status | Test/Datei | Abgleich |\n|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (var r in list)
            sb.Append($"| {Cell(r.Id)} | {Cell(r.Function)} | {Cell(r.Expected)} | {Cell(r.Check)} | {Cell(r.Source.Length > 0 ? r.Source : "– (fehlt)")} | {Cell(r.Date)} | {Cell(r.Stage)} | {Cell(r.Status)} | {Cell(r.Link.Length > 0 ? r.Link : "– (keiner)")} | {Cell(r.Note)} |\n");
        return sb.ToString();
    }

    /// <summary>Export als eigenständiges Markdown-Dokument (Standard: „3-Pruefung/ANFORDERUNGEN.md“).</summary>
    public static string ExportRequirements(string dir, string? target = null)
    {
        var list = LoadRequirements(dir);
        target ??= Path.Combine(dir, "3-Pruefung", "ANFORDERUNGEN.md");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        var md = $"# Anforderungen: {Path.GetFileName(dir.TrimEnd('\\', '/'))}\n\nStand: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz} · {list.Count(r => r.Status == StatusDone)} erfüllt · {list.Count(r => r.Status == StatusPartly)} teilweise · {list.Count(r => r.Status == StatusOpen)} offen\n_Beschreibt nur, WAS das eigene Programm tun soll – kein Code des Originals._\n\n" + RequirementsTable(list);
        AtomicFile.WriteAllText(target, md);
        return target;
    }

    /// <summary>Verknüpfung „Datei#Test“ auflösen. Nur Dateien innerhalb des Projektordners; sonst null.</summary>
    public static (string? File, string? Test) ResolveLink(string dir, string link)
    {
        if (string.IsNullOrWhiteSpace(link)) return (null, null);
        var hash = link.IndexOf('#');
        var rel = (hash >= 0 ? link[..hash] : link).Trim().Replace('/', Path.DirectorySeparatorChar);
        var test = hash >= 0 ? link[(hash + 1)..].Trim() : null;
        try
        {
            var root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, rel));
            return (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && rel.Length > 0 ? full : null, string.IsNullOrEmpty(test) ? null : test);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return (null, test); }
    }

    public sealed record Verdict(string Status, string Reason);

    /// <summary>Abgleich einer Anforderung: erfüllt nur, wenn ein verknüpfter Test im letzten echten Testlauf bestanden hat.
    /// Datei ohne Test oder Test noch nicht gelaufen = teilweise; kein Test, fehlende Datei oder fehlgeschlagener Test = offen.</summary>
    public static Verdict Reconcile(string dir, Requirement r, DevBuild.TestRun? run)
    {
        var (file, test) = ResolveLink(dir, r.Link);
        if (r.Link.Trim().Length == 0) return new(StatusOpen, "Kein Test verknüpft.");
        if (file is null) return new(StatusOpen, "Die Verknüpfung zeigt aus dem Projekt heraus.");
        if (!File.Exists(file)) return new(StatusOpen, $"Datei „{r.Link.Split('#')[0]}“ fehlt.");
        if (test is null) return new(StatusPartly, "Datei vorhanden, aber kein Test benannt (Datei#Testname).");
        var hit = run?.Cases.FirstOrDefault(c => c.Name == test || c.Name.EndsWith("." + test, StringComparison.Ordinal) || c.Name.EndsWith(" " + test, StringComparison.Ordinal));
        if (hit is not null) return hit.Passed ? new(StatusDone, $"Test „{test}“ bestanden.") : hit.Skipped ? new(StatusPartly, $"Test „{test}“ übersprungen.") : new(StatusOpen, $"Test „{test}“ fehlgeschlagen: {hit.Message}");
        string text; try { text = File.ReadAllText(file); } catch (IOException) { text = ""; }
        return text.Contains(test, StringComparison.Ordinal)
            ? new(StatusPartly, run is null ? $"Test „{test}“ steht in der Datei, ist aber noch nicht gelaufen." : $"Test „{test}“ steht in der Datei, kam im Testlauf aber nicht vor.")
            : new(StatusOpen, $"Test „{test}“ steht nicht in der Datei.");
    }

    /// <summary>Alle Anforderungen abgleichen: Status und Grund werden übernommen.</summary>
    public static List<Requirement> ReconcileAll(string dir, IEnumerable<Requirement> list, DevBuild.TestRun? run) =>
        list.Select(r => Reconcile(dir, r, run) is var v ? r with { Status = v.Status, Note = v.Reason } : r).ToList();

    /// <summary>Echter Testlauf in „2-Umsetzung“ (dotnet test, python unittest/pytest, node --test). Null mit Grund, wenn es keinen Test oder kein Werkzeug gibt.</summary>
    public static (DevBuild.TestRun? Run, string Log) RunTests(string dir, int timeoutMs = 600_000)
    {
        var impl = Path.Combine(dir, "2-Umsetzung");
        if (!Directory.Exists(impl)) return (null, "Kein Ordner „2-Umsetzung“.");
        var p = DevBuild.Detect(impl);
        var results = Path.Combine(Path.GetTempPath(), "trendo-nachbau-test-" + Guid.NewGuid().ToString("N")[..10]);
        try
        {
            var plan = DevBuild.Test(p, results);
            if (plan.Cmd is not { } cmd) return (null, plan.Blocked ?? plan.Note ?? "Für dieses Projekt kennt Trendo keinen Test.");
            Directory.CreateDirectory(results);
            var r = Cli.Run(cmd.Exe, cmd.Args, cmd.Cwd, timeoutMs, cmd.Env, cmd.Utf8 ? new UTF8Encoding(false) : null);
            var run = DevBuild.ReadResults(p, results, r.Out + "\n" + r.Err);
            return (run, $"$ {cmd.Display}\n(Exit {r.Exit}) {run.Summary}");
        }
        finally { try { if (Directory.Exists(results)) Directory.Delete(results, true); } catch (IOException) { } }
    }

    /// <summary>Bearbeitbare Anforderungsliste mit Rückgängig. Jede Änderung wird sofort sicher gespeichert (kein Verlust bei Absturz).</summary>
    public sealed class RequirementsDoc(string dir)
    {
        readonly Stack<List<Requirement>> _undo = new();
        public string Dir { get; } = dir;
        public List<Requirement> Items { get; private set; } = LoadRequirements(dir);
        public bool CanUndo => _undo.Count > 0;

        public void Change(Func<List<Requirement>, List<Requirement>> edit)
        {
            var before = Items.ToList();
            var after = edit(Items.ToList());
            if (after.SequenceEqual(before)) return;
            _undo.Push(before); if (_undo.Count > 200) { var keep = _undo.Take(200).Reverse().ToList(); _undo.Clear(); foreach (var k in keep) _undo.Push(k); }
            Items = after; SaveRequirements(Dir, Items);
        }

        public Requirement Add(string function = "", string source = "eigene Beobachtung") { var r = NewRequirement(Items, function, source); Change(l => [.. l, r]); return r; }
        public void Update(string id, Func<Requirement, Requirement> edit) => Change(l => l.Select(r => r.Id == id ? edit(r) : r).ToList());
        public void Remove(string id) => Change(l => l.Where(r => r.Id != id).ToList());
        public bool Undo() { if (_undo.Count == 0) return false; Items = _undo.Pop(); SaveRequirements(Dir, Items); return true; }
    }

    // ───────────── Vergleich nebeneinander ─────────────

    /// <summary>Tokens mit Zeilennummer (1-basiert); Kommentare und Texte zählen nicht, Zeilen bleiben erhalten.</summary>
    static List<(string Tok, int Line)> LineTokens(string code)
    {
        var clean = Noise().Replace(code, m => new string(m.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray()));
        var res = new List<(string, int)>();
        var line = 1; var pos = 0;
        foreach (System.Text.RegularExpressions.Match m in Tok().Matches(clean))
        {
            for (; pos < m.Index; pos++) if (clean[pos] == '\n') line++;
            var t = m.Value;
            res.Add((char.IsLetter(t[0]) || t[0] == '_' ? (Kw.Contains(t) ? t : "V") : char.IsDigit(t[0]) ? "N" : t, line));
        }
        return res;
    }

    /// <summary>Welche Zeilen beider Texte zu gemeinsamen Abschnitten (k Tokens, wie <see cref="Similarity"/>) gehören – für die Ansicht nebeneinander.</summary>
    public static (SortedSet<int> Ours, SortedSet<int> Theirs) MatchLines(string ours, string theirs, int k = 8)
    {
        var a = LineTokens(ours); var b = LineTokens(theirs);
        static Dictionary<string, List<int>> Index(List<(string Tok, int Line)> t, int k)
        {
            var d = new Dictionary<string, List<int>>();
            for (var i = 0; i + k <= t.Count; i++) { var key = string.Join(' ', t.Skip(i).Take(k).Select(x => x.Tok)); if (!d.TryGetValue(key, out var l)) d[key] = l = []; l.Add(i); }
            return d;
        }
        var ia = Index(a, k); var ib = Index(b, k);
        var la = new SortedSet<int>(); var lb = new SortedSet<int>();
        foreach (var (key, starts) in ia)
        {
            if (!ib.TryGetValue(key, out var other)) continue;
            foreach (var s in starts) for (var j = s; j < s + k; j++) la.Add(a[j].Line);
            foreach (var s in other) for (var j = s; j < s + k; j++) lb.Add(b[j].Line);
        }
        return (la, lb);
    }
}
