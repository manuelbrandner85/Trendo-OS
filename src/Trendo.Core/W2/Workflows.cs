using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

public enum TriggerKind { Manuell, Taeglich, Intervall, NeueDatei }
public enum StepKind { OrdnerAnlegen, Kopieren, Verschieben, DatumVoranstellen, TextSchreiben, Zippen, KiZusammenfassen }

public sealed class WfStep
{
    public StepKind Kind { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Pattern { get; set; } = "*";
    public string Text { get; set; } = "";
}

public sealed class Workflow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public TriggerKind Trigger { get; set; }
    public string At { get; set; } = "08:00";
    public int EveryMinutes { get; set; } = 60;
    public string WatchFolder { get; set; } = "";
    public string WatchPattern { get; set; } = "*";
    public List<WfStep> Steps { get; set; } = [];
    public string? ApprovedHash { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public List<string> KnownFiles { get; set; } = [];

    public string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Trigger, At, EveryMinutes, WatchFolder, WatchPattern, Steps }))))[..16];
    public bool NeedsApproval => Steps.Any(s => s.Kind is StepKind.Verschieben or StepKind.DatumVoranstellen);
    public bool IsApproved => !NeedsApproval || ApprovedHash == Hash();
}

public sealed record WfRun(string WorkflowId, DateTimeOffset At, bool DryRun, bool Ok, List<string> Lines);

/// <summary>SYS-05 Universal Automation & Workflow Engine: Abläufe aus einfachen Schritten, Auslöser (von Hand, täglich, alle X Minuten, neue Datei),
/// Probelauf ohne Änderungen, verschiebende/umbenennende Abläufe nur nach Freigabe (Änderung = neue Freigabe), nur in erlaubten Ordnern, Protokoll.</summary>
public sealed class WorkflowEngine
{
    private readonly string _file, _runs; private readonly AuditLog _audit; private readonly ApprovalGate _gate;
    // T-0250 (V110): kein Doppelstart – weder zwei Zeitgeber-Durchläufe gleichzeitig noch derselbe Ablauf zweimal (auch nicht aus zwei Trendo-Fenstern)
    private int _dueBusy;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> Running = new();
    /// <summary>Eine Sperrdatei gilt als verwaist, wenn sie älter ist (z. B. nach einem Absturz).</summary>
    public static readonly TimeSpan LeaseTimeout = TimeSpan.FromHours(2);
    private string LeaseFile(string id) => Path.Combine(Path.GetDirectoryName(_file)!, "laeuft-" + string.Concat(id.Where(char.IsLetterOrDigit)) + ".lock");

    /// <summary>Ablauf für diesen Lauf belegen – false, wenn er gerade schon läuft.</summary>
    private bool Lease(string id)
    {
        if (!Running.TryAdd(_file + "|" + id, 0)) return false;
        var f = LeaseFile(id);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            if (File.Exists(f) && DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > LeaseTimeout) File.Delete(f); // verwaist
            using (new FileStream(f, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            return true;
        }
        catch (IOException) { Running.TryRemove(_file + "|" + id, out _); return false; }
    }

    private void Release(string id) { try { File.Delete(LeaseFile(id)); } catch (IOException) { } Running.TryRemove(_file + "|" + id, out _); }
    private readonly IReadOnlyList<string> _allowed;
    private readonly List<Workflow> _items;
    public Func<string, Task<string?>>? Summarize { get; set; }

    public WorkflowEngine(string dataRoot, AuditLog audit, ApprovalGate gate, IEnumerable<string>? allowedRoots = null)
    {
        _audit = audit; _gate = gate;
        _file = Path.Combine(dataRoot, "ablaeufe", "ablaeufe.json"); _runs = Path.Combine(dataRoot, "ablaeufe", "protokoll.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        _allowed = (allowedRoots ?? [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]).Where(r => r != "").Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).ToList();
        try { _items = File.Exists(_file) ? JsonSerializer.Deserialize<List<Workflow>>(File.ReadAllText(_file)) ?? [] : []; } catch { _items = []; }
    }

    public static readonly (string Id, string Name, Func<string, Workflow> Make)[] Templates =
    [
        ("pdf", "Downloads aufräumen: PDFs in einen Ordner", home => new Workflow { Name = "PDFs aus Downloads einsortieren", Trigger = TriggerKind.NeueDatei, WatchFolder = Path.Combine(home, "Downloads"), WatchPattern = "*.pdf",
            Steps = [new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "Documents", "PDF") }, new() { Kind = StepKind.Verschieben, From = Path.Combine(home, "Downloads"), To = Path.Combine(home, "Documents", "PDF"), Pattern = "*.pdf" }] }),
        ("notiz", "Jeden Morgen eine Tagesnotiz", home => new Workflow { Name = "Tagesnotiz", Trigger = TriggerKind.Taeglich, At = "08:00",
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "Documents", "Tagesnotizen", "{datum}.txt"), Text = "Notizen für {datum}\n\n- " }] }),
        ("fotos", "Fotos mit Datum versehen", home => new Workflow { Name = "Fotos datieren", Trigger = TriggerKind.Manuell,
            Steps = [new() { Kind = StepKind.DatumVoranstellen, From = Path.Combine(home, "Pictures"), Pattern = "*.jpg" }] }),
        ("archiv", "Ordner wöchentlich als ZIP sichern", home => new Workflow { Name = "Dokumente als ZIP", Trigger = TriggerKind.Intervall, EveryMinutes = 7 * 24 * 60,
            Steps = [new() { Kind = StepKind.Zippen, From = Path.Combine(home, "Documents"), To = Path.Combine(home, "Documents-Archiv", "dokumente-{datum}.zip") }] }),
    ];

    public static string StepLabel(StepKind k) => k switch
    {
        StepKind.OrdnerAnlegen => "Ordner anlegen", StepKind.Kopieren => "Dateien kopieren", StepKind.Verschieben => "Dateien verschieben",
        StepKind.DatumVoranstellen => "Datum vor den Namen setzen", StepKind.TextSchreiben => "Textdatei schreiben", StepKind.Zippen => "Als ZIP packen",
        _ => "Mit KI zusammenfassen",
    };

    public IReadOnlyList<Workflow> All() => _items.ToList();
    public Workflow? Get(string id) => _items.FirstOrDefault(w => w.Id == id);
    public Workflow Add(Workflow w) { _items.Add(w); Save(); _audit.Append(Principal.LocalOwner.Name, "workflow.add", $"{w.Id} {w.Name}"); return w; }
    public void Update(Workflow w) { Save(); _audit.Append(Principal.LocalOwner.Name, "workflow.update", $"{w.Id} hash={w.Hash()}"); }
    public bool Remove(string id) { var n = _items.RemoveAll(w => w.Id == id); Save(); return n > 0; }

    public ApprovalRequest RequestApproval(Workflow w, string? preview = null) => _gate.Request(Principal.LocalOwner, "workflow.approve", $"Ablauf „{w.Name}“ darf Dateien verschieben/umbenennen", $"{w.Id}:{w.Hash()}", TimeSpan.FromHours(1), preview);

    public void ApplyApproval(Workflow w, string requestId)
    {
        _gate.Execute(requestId, "workflow.approve", $"{w.Id}:{w.Hash()}", () => { w.ApprovedHash = w.Hash(); return true; });
        Save();
    }

    public bool InAllowed(string path)
    {
        var full = Path.GetFullPath(path);
        return _allowed.Any(r => (full + Path.DirectorySeparatorChar).StartsWith(r, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    }

    /// <summary>Welche Abläufe sind jetzt dran? Neue-Datei-Auslöser merken sich bekannte Dateien.</summary>
    public IReadOnlyList<(Workflow W, string? File)> Due(DateTimeOffset now)
    {
        var due = new List<(Workflow, string?)>();
        foreach (var w in _items.Where(w => w.Enabled))
            switch (w.Trigger)
            {
                case TriggerKind.Taeglich when TimeOnly.TryParse(w.At, out var at):
                    var today = new DateTimeOffset(now.Date + at.ToTimeSpan(), now.Offset);
                    if (now >= today && (w.LastRun is null || w.LastRun < today)) due.Add((w, null));
                    break;
                case TriggerKind.Intervall when w.EveryMinutes > 0:
                    if (w.LastRun is null || now - w.LastRun >= TimeSpan.FromMinutes(w.EveryMinutes)) due.Add((w, null));
                    break;
                case TriggerKind.NeueDatei when Directory.Exists(w.WatchFolder):
                    var files = Directory.GetFiles(w.WatchFolder, w.WatchPattern).Select(Path.GetFileName).OfType<string>().ToList();
                    var first = w.LastRun is null && w.KnownFiles.Count == 0;
                    var fresh = files.Except(w.KnownFiles).ToList();
                    w.KnownFiles = files;
                    if (!first) foreach (var f in fresh) due.Add((w, Path.Combine(w.WatchFolder, f)));
                    if (first) { w.LastRun = now; Save(); }
                    break;
            }
        return due;
    }

    public async Task<WfRun> RunAsync(Workflow w, bool dryRun, string? triggerFile = null, DateTimeOffset? now = null)
    {
        var t = now ?? DateTimeOffset.Now; var lines = new List<string>();
        string V(string s) => s.Replace("{datum}", t.ToString("yyyy-MM-dd")).Replace("{zeit}", t.ToString("HH-mm")).Replace("{datei}", triggerFile is null ? "" : Path.GetFileName(triggerFile));
        if (!dryRun && !w.IsApproved) { lines.Add("Gestoppt: Dieser Ablauf verschiebt oder benennt Dateien um und braucht zuerst deine Freigabe."); return Log(new(w.Id, t, dryRun, false, lines)); }
        if (!dryRun && _gate.Halted) { lines.Add("Gestoppt: Der Not-Stopp ist aktiv. Es wurde nichts geändert."); return Log(new(w.Id, t, dryRun, false, lines)); }
        if (!dryRun && !Lease(w.Id)) { lines.Add("Läuft gerade schon – nicht doppelt gestartet."); _audit.Append("ablauf", "workflow.skipped", w.Id + " läuft schon"); return new(w.Id, t, dryRun, false, lines); }
        try { return await RunLeasedAsync(w, dryRun, triggerFile, t, lines, V); }
        finally { if (!dryRun) Release(w.Id); }
    }

    private async Task<WfRun> RunLeasedAsync(Workflow w, bool dryRun, string? triggerFile, DateTimeOffset t, List<string> lines, Func<string, string> V)
    {
        var ok = true; var halt = _gate.HaltToken;
        foreach (var s in w.Steps)
        {
            if (!dryRun && halt.IsCancellationRequested) { ok = false; lines.Add("Not-Stopp: Der Ablauf wurde angehalten. Weitere Schritte laufen nicht."); break; }
            try
            {
                var from = V(s.From); var to = V(s.To);
                foreach (var p in new[] { from, to }.Where(p => p != "")) if (!InAllowed(p)) throw new UnauthorizedAccessException($"Ordner nicht erlaubt: {p}");
                IEnumerable<string> Src() => triggerFile is not null && s.Kind is StepKind.Kopieren or StepKind.Verschieben or StepKind.DatumVoranstellen or StepKind.KiZusammenfassen && Path.GetDirectoryName(triggerFile) == Path.GetFullPath(from).TrimEnd(Path.DirectorySeparatorChar)
                    ? [triggerFile] : Directory.Exists(from) ? Directory.GetFiles(from, s.Pattern) : [];
                switch (s.Kind)
                {
                    case StepKind.OrdnerAnlegen:
                        lines.Add($"Ordner anlegen: {to}"); if (!dryRun) Directory.CreateDirectory(to); break;
                    case StepKind.Kopieren or StepKind.Verschieben:
                        var list = Src().ToList();
                        if (list.Count == 0) lines.Add($"{StepLabel(s.Kind)}: keine passenden Dateien in {from}");
                        foreach (var f in list)
                        {
                            if (!dryRun) halt.ThrowIfCancellationRequested();
                            var dest = Unique(Path.Combine(to, Path.GetFileName(f)));
                            lines.Add($"{(s.Kind == StepKind.Kopieren ? "Kopieren" : "Verschieben")}: {Path.GetFileName(f)} → {dest}");
                            if (dryRun) continue;
                            Directory.CreateDirectory(to);
                            if (s.Kind == StepKind.Kopieren) File.Copy(f, dest); else File.Move(f, dest);
                        }
                        break;
                    case StepKind.DatumVoranstellen:
                        foreach (var f in Src().Where(f => !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"^\d{4}-\d{2}-\d{2}_")).ToList())
                        {
                            var dest = Unique(Path.Combine(Path.GetDirectoryName(f)!, File.GetLastWriteTime(f).ToString("yyyy-MM-dd") + "_" + Path.GetFileName(f)));
                            lines.Add($"Umbenennen: {Path.GetFileName(f)} → {Path.GetFileName(dest)}"); if (!dryRun) File.Move(f, dest);
                        }
                        break;
                    case StepKind.TextSchreiben:
                        lines.Add($"Textdatei schreiben: {to}{(File.Exists(to) ? " (gibt es schon, wird nicht überschrieben)" : "")}");
                        if (!dryRun && !File.Exists(to)) { Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.WriteAllText(to, V(s.Text)); }
                        break;
                    case StepKind.Zippen:
                        lines.Add($"Als ZIP packen: {from} → {to}");
                        if (!dryRun) { Directory.CreateDirectory(Path.GetDirectoryName(to)!); if (File.Exists(to)) File.Delete(to); ZipFile.CreateFromDirectory(from, to); }
                        break;
                    case StepKind.KiZusammenfassen:
                        foreach (var f in Src().Where(f => Path.GetExtension(f).ToLowerInvariant() is ".txt" or ".md").Take(10).ToList())
                        {
                            var dest = Path.Combine(to == "" ? Path.GetDirectoryName(f)! : to, Path.GetFileNameWithoutExtension(f) + ".zusammenfassung.txt");
                            lines.Add($"KI fasst zusammen: {Path.GetFileName(f)} → {Path.GetFileName(dest)}");
                            if (dryRun) continue;
                            var r = Summarize is null ? null : await Summarize(File.ReadAllText(f));
                            if (r is null) throw new InvalidOperationException("Die KI ist gerade nicht erreichbar.");
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.WriteAllText(dest, r);
                        }
                        break;
                }
            }
            catch (OperationCanceledException) when (halt.IsCancellationRequested) { ok = false; lines.Add("Not-Stopp: Der Ablauf wurde angehalten. Weitere Schritte laufen nicht."); break; }
            catch (Exception ex) { ok = false; lines.Add($"Hat nicht geklappt bei „{StepLabel(s.Kind)}“: {ex.Message}"); break; }
        }
        if (!dryRun) { w.LastRun = t; Save(); }
        return Log(new(w.Id, t, dryRun, ok, lines));
    }

    public async Task<IReadOnlyList<WfRun>> RunDueAsync(DateTimeOffset now)
    {
        var res = new List<WfRun>();
        if (_gate.Halted) return res; // Not-Stopp: zeitgesteuerte Abläufe ruhen
        if (Interlocked.Exchange(ref _dueBusy, 1) == 1) return res; // der vorige Zeitgeber-Durchlauf ist noch nicht fertig
        try { foreach (var (w, f) in Due(now)) res.Add(await RunAsync(w, false, f, now)); }
        finally { Volatile.Write(ref _dueBusy, 0); }
        return res;
    }

    public IReadOnlyList<WfRun> History(string? id = null, int max = 20) => !File.Exists(_runs) ? [] :
        File.ReadAllLines(_runs).Where(l => l.Length > 0).Select(l => { try { return JsonSerializer.Deserialize<WfRun>(l); } catch { return null; } }).OfType<WfRun>()
            .Where(r => id is null || r.WorkflowId == id).Reverse().Take(max).ToList();

    private WfRun Log(WfRun r)
    {
        if (!r.DryRun) { File.AppendAllText(_runs, JsonSerializer.Serialize(r) + "\n"); _audit.Append("ablauf", r.Ok ? "workflow.run" : "workflow.failed", $"{r.WorkflowId} schritte={r.Lines.Count}"); }
        return r;
    }

    private static string Unique(string p)
    {
        if (!File.Exists(p)) return p;
        var d = Path.GetDirectoryName(p)!; var n = Path.GetFileNameWithoutExtension(p); var e = Path.GetExtension(p);
        for (var i = 2; ; i++) { var c = Path.Combine(d, $"{n} ({i}){e}"); if (!File.Exists(c)) return c; }
    }

    private void Save() => File.WriteAllText(_file, JsonSerializer.Serialize(_items));
}
