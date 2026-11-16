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
    /// <summary>T-0681: Fehlversuche in Folge (0 nach einem geglückten Lauf). <see cref="LastRun"/> zählt nur geglückte Läufe.</summary>
    public int FailCount { get; set; }
    /// <summary>T-0681: Meldung des letzten fehlgeschlagenen Laufs (null, sobald wieder einer geklappt hat).</summary>
    public string? LastError { get; set; }
    /// <summary>T-0681: Zeitpunkt des letzten Versuchs, geglückt oder nicht.</summary>
    public DateTimeOffset? LastAttempt { get; set; }

    public string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Trigger, At, EveryMinutes, WatchFolder, WatchPattern, Steps }))))[..16];
    public bool NeedsApproval => Steps.Any(s => s.Kind is StepKind.Verschieben or StepKind.DatumVoranstellen);
    public bool IsApproved => !NeedsApproval || ApprovedHash == Hash();
}

public sealed record WfRun(string WorkflowId, DateTimeOffset At, bool DryRun, bool Ok, List<string> Lines)
{
    /// <summary>Ablauf-Editor: Ergebnis je Schritt (für die Anzeige am Baustein). Schritte nach einem Fehler fehlen.</summary>
    public List<WfStepResult> Steps { get; init; } = [];
}
/// <summary>Ergebnis eines Schritts: Zeilen im Protokoll ab <paramref name="First"/>, Anzahl betroffener Dateien bzw. Aktionen, ob er geklappt hat.</summary>
public sealed record WfStepResult(int Step, bool Ok, int First, int Count, string Summary);

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
    /// <summary>T-0681: So oft wird ein fehlgeschlagener Ablauf Takt für Takt erneut versucht; danach erst wieder zum nächsten regulären Zeitpunkt.</summary>
    public const int MaxRetries = 3;
    /// <summary>T-0681: Meldung, wenn die Ablaufdatei beim Start beschädigt oder nicht lesbar war (null = alles in Ordnung).</summary>
    public string? LoadProblem { get; private set; }
    private bool _saveBlocked;

    public WorkflowEngine(string dataRoot, AuditLog audit, ApprovalGate gate, IEnumerable<string>? allowedRoots = null)
    {
        _audit = audit; _gate = gate;
        _file = Path.Combine(dataRoot, "ablaeufe", "ablaeufe.json"); _runs = Path.Combine(dataRoot, "ablaeufe", "protokoll.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        _allowed = (allowedRoots ?? [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]).Where(r => r != "").Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).ToList();
        _items = Load();
    }

    /// <summary>T-0681: Eine beschädigte Datei wird nicht still als „keine Abläufe“ gelesen: Sie wird als „.beschaedigt-&lt;zeit&gt;“ aufgehoben und gemeldet.
    /// Ist sie nur gerade nicht lesbar (von einem anderen Programm belegt), wird nichts überschrieben, bis Trendo neu gestartet ist.</summary>
    private List<Workflow> Load()
    {
        foreach (var old in Directory.GetFiles(Path.GetDirectoryName(_file)!, "ablaeufe.json.*.tmp")) // Reste eines abgebrochenen Speicherns
            try { if (DateTime.UtcNow - File.GetLastWriteTimeUtc(old) > TimeSpan.FromMinutes(10)) File.Delete(old); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (!File.Exists(_file)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<Workflow>>(File.ReadAllText(_file));
            if (list is null || list.Any(w => w is null)) throw new JsonException("Inhalt fehlt.");
            return list;
        }
        catch (JsonException)
        {
            var keep = _file + ".beschaedigt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            for (var n = 2; File.Exists(keep); n++) keep = _file + ".beschaedigt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + n;
            try { File.Move(_file, keep); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _saveBlocked = true; keep = _file; }
            LoadProblem = _saveBlocked
                ? "Die Datei mit deinen Abläufen ist beschädigt und ließ sich nicht beiseitelegen. Trendo ändert sie nicht – starte Trendo neu."
                : $"Die Datei mit deinen Abläufen war beschädigt. Trendo hat sie aufgehoben als „{Path.GetFileName(keep)}“ (Ordner {Path.GetDirectoryName(keep)}) und startet ohne Abläufe – es wurde nichts gelöscht.";
            _audit.Append("ablauf", "workflow.datei.beschaedigt", Path.GetFileName(keep));
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _saveBlocked = true;
            LoadProblem = "Die Datei mit deinen Abläufen ist gerade nicht lesbar (von einem anderen Programm belegt?). Trendo ändert sie nicht – schließe das andere Programm und starte Trendo neu.";
            _audit.Append("ablauf", "workflow.datei.nicht-lesbar", ex.GetType().Name);
            return [];
        }
    }

    /// <summary>T-0681: Fehlerstand eines Ablaufs als Satz für die Anzeige (null = kein Fehler offen).</summary>
    public static string? FailText(Workflow w) => w.LastError is not { Length: > 0 } err ? null :
        $"Letzter Lauf fehlgeschlagen ({w.FailCount} {(w.FailCount == 1 ? "Versuch" : "Versuche")}{(w.LastAttempt is { } a ? $", zuletzt {a:dd.MM. HH:mm}" : "")}): {err}"
        + (w.Trigger == TriggerKind.Manuell || !w.Enabled ? "" : w.FailCount >= MaxRetries ? " Trendo versucht es erst zum nächsten regulären Zeitpunkt wieder." : " Trendo versucht es beim nächsten Takt noch einmal.");

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
    public void Update(Workflow w) { w.FailCount = 0; Save(); _audit.Append(Principal.LocalOwner.Name, "workflow.update", $"{w.Id} hash={w.Hash()}"); }
    public bool Remove(string id) { var n = _items.RemoveAll(w => w.Id == id); Save(); return n > 0; }

    public ApprovalRequest RequestApproval(Workflow w, string? preview = null) => _gate.Request(Principal.LocalOwner, "workflow.approve", $"Ablauf „{w.Name}“ darf Dateien verschieben/umbenennen", $"{w.Id}:{w.Hash()}", TimeSpan.FromHours(1), preview);

    public void ApplyApproval(Workflow w, string requestId)
    {
        _gate.Execute(requestId, "workflow.approve", $"{w.Id}:{w.Hash()}", () => { w.ApprovedHash = w.Hash(); return true; });
        w.FailCount = 0; // nach der Freigabe gleich wieder versuchen
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
                    if (now >= today && (w.LastRun is null || w.LastRun < today) && !(w.FailCount >= MaxRetries && w.LastAttempt >= today)) due.Add((w, null)); // nach der Obergrenze erst morgen wieder
                    break;
                case TriggerKind.Intervall when w.EveryMinutes > 0:
                    if ((w.LastRun is null || now - w.LastRun >= TimeSpan.FromMinutes(w.EveryMinutes))
                        && !(w.FailCount >= MaxRetries && w.LastAttempt is { } tried && now - tried < TimeSpan.FromMinutes(w.EveryMinutes))) due.Add((w, null)); // nach der Obergrenze erst zum nächsten Intervall
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

    /// <param name="onStep">Wird vor jedem Schritt aufgerufen (Ablauf-Editor: Baustein leuchtet auf).</param>
    public async Task<WfRun> RunAsync(Workflow w, bool dryRun, string? triggerFile = null, DateTimeOffset? now = null, Func<int, Task>? onStep = null)
    {
        var t = now ?? DateTimeOffset.Now; var lines = new List<string>();
        string V(string s) => s.Replace("{datum}", t.ToString("yyyy-MM-dd")).Replace("{zeit}", t.ToString("HH-mm")).Replace("{datei}", triggerFile is null ? "" : Path.GetFileName(triggerFile));
        if (!dryRun && !w.IsApproved) { lines.Add("Gestoppt: Dieser Ablauf verschiebt oder benennt Dateien um und braucht zuerst deine Freigabe."); Mark(w, t, false, lines[^1], triggerFile); return Log(new(w.Id, t, dryRun, false, lines)); }
        if (!dryRun && _gate.Halted) { lines.Add("Gestoppt: Der Not-Stopp ist aktiv. Es wurde nichts geändert."); return Log(new(w.Id, t, dryRun, false, lines)); }
        if (!dryRun && !Lease(w.Id)) { lines.Add("Läuft gerade schon – nicht doppelt gestartet."); _audit.Append("ablauf", "workflow.skipped", w.Id + " läuft schon"); return new(w.Id, t, dryRun, false, lines); }
        try { return await RunLeasedAsync(w, dryRun, triggerFile, t, lines, V, onStep); }
        finally { if (!dryRun) Release(w.Id); }
    }

    private async Task<WfRun> RunLeasedAsync(Workflow w, bool dryRun, string? triggerFile, DateTimeOffset t, List<string> lines, Func<string, string> V, Func<int, Task>? onStep)
    {
        var ok = true; var halt = _gate.HaltToken; var results = new List<WfStepResult>();
        for (var si = 0; si < w.Steps.Count; si++)
        {
            var s = w.Steps[si]; var first = lines.Count;
            if (!dryRun && halt.IsCancellationRequested) { ok = false; lines.Add("Not-Stopp: Der Ablauf wurde angehalten. Weitere Schritte laufen nicht."); break; }
            if (onStep is not null) await onStep(si);
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
            catch (OperationCanceledException) when (halt.IsCancellationRequested) { ok = false; lines.Add("Not-Stopp: Der Ablauf wurde angehalten. Weitere Schritte laufen nicht."); results.Add(new(si, false, first, 0, "angehalten")); break; }
            catch (Exception ex) { ok = false; lines.Add($"Hat nicht geklappt bei „{StepLabel(s.Kind)}“: {ex.Message}"); results.Add(new(si, false, first, 0, ex.Message)); break; }
            var n = lines.Count - first; var none = n == 1 && lines[first].Contains("keine passenden Dateien");
            results.Add(new(si, true, first, none ? 0 : n, none ? "nichts zu tun" : n == 1 ? lines[first] : $"{n} {(s.Kind is StepKind.Kopieren or StepKind.Verschieben or StepKind.DatumVoranstellen or StepKind.KiZusammenfassen ? "Dateien" : "Aktionen")}"));
        }
        if (!dryRun) Mark(w, t, ok, ok ? null : lines.LastOrDefault(), triggerFile);
        return Log(new(w.Id, t, dryRun, ok, lines) { Steps = results });
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

    /// <summary>T-0681: Nur ein geglückter Lauf zählt als „zuletzt gelaufen“. Ein Fehlschlag bleibt fällig (bis <see cref="MaxRetries"/> Versuche Takt für Takt,
    /// danach zum nächsten regulären Zeitpunkt) und steht mit Meldung am Ablauf.</summary>
    private void Mark(Workflow w, DateTimeOffset t, bool ok, string? error, string? triggerFile)
    {
        w.LastAttempt = t;
        if (ok) { w.LastRun = t; w.FailCount = 0; w.LastError = null; }
        else
        {
            w.FailCount++; w.LastError = error ?? "Unbekannter Fehler.";
            // Neue-Datei-Auslöser: die Datei gilt wieder als „neu“, damit der nächste Takt sie erneut versucht
            if (triggerFile is not null && w.FailCount < MaxRetries) w.KnownFiles.Remove(Path.GetFileName(triggerFile));
        }
        Save();
    }

    /// <summary>T-0681: atomar speichern – erst vollständig in eine eigene Datei schreiben, dann in einem Schritt ersetzen.
    /// Ein Absturz mitten im Speichern lässt die bisherige Datei unversehrt.</summary>
    private void Save()
    {
        if (_saveBlocked) throw new IOException(LoadProblem);
        var tmp = _file + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(fs, _items);
                fs.Flush(flushToDisk: true);
            }
            for (var i = 0; ; i++)
                try { File.Move(tmp, _file, overwrite: true); break; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && i < 20) { Thread.Sleep(25); } // kurz von einem Leser belegt
        }
        finally { try { File.Delete(tmp); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }
}
