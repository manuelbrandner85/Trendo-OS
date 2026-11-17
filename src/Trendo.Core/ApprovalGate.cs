using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core;

public enum ApprovalState { Pending, Approved, Denied, Executed, Expired }

public sealed class ApprovalRequest
{
    public required string Id { get; init; }
    public required string Action { get; init; }
    public required string Description { get; init; }
    public required string Target { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset ExpiresAt { get; init; }
    public ApprovalState State { get; internal set; } = ApprovalState.Pending;
    /// <summary>T-0005 Änderungsvorschau: was genau passiert (Dateien, Ziele), vor der Freigabe sichtbar.</summary>
    public string? Preview { get; init; }
    /// <summary>B4 (T-0706): wer angefragt hat – nur diese Person (oder wer freigeben darf) kann die Anfrage zurückziehen.</summary>
    public string RequestedBy { get; init; } = "";
    /// <summary>T-0713: Die Anfrage kann nach einem Neustart weiter entschieden und ausgeführt werden, weil alles Nötige im Kern gespeichert ist
    /// (Ziel in der Anfrage, z. B. „Ordner/URL öffnen“, oder ein gespeicherter KI-Auftrag). Nur solche offenen Anfragen überleben einen Neustart.</summary>
    public bool Resumable { get; init; }
    /// <summary>T-0713: nach einem Neustart aus der Ablage geladen – vor Freigabe und Ausführung wird im Protokoll nachgesehen, ob ein anderes
    /// Trendo-Fenster mit demselben Datenordner sie inzwischen entschieden oder ausgeführt hat (einmalig bleibt einmalig).</summary>
    internal bool Restored { get; init; }
}

/// <summary>Plan → Freigabe → Ausführung. Ohne gültige, einmalige, nicht abgelaufene Freigabe wird nichts ausgeführt.
/// T-0713: Offene, fortsetzbare Anfragen (<see cref="ApprovalRequest.Resumable"/>) werden atomar gespeichert und beim Start wieder geladen –
/// abgelaufene verfallen, erteilte Freigaben werden nie gespeichert (nach einem Neustart braucht es immer eine neue Entscheidung).</summary>
public sealed class ApprovalGate
{
    private readonly Dictionary<string, ApprovalRequest> _requests = new();
    private readonly AuditLog audit;
    private readonly TimeProvider _time;
    private readonly string? _store;
    private readonly object _lock = new();
    private CancellationTokenSource _halt = new();

    /// <param name="storeFile">T-0713: Datei für offene, fortsetzbare Anfragen (null = nur im Arbeitsspeicher).</param>
    public ApprovalGate(AuditLog audit, TimeProvider? time = null, string? storeFile = null)
    {
        this.audit = audit;
        _time = time ?? TimeProvider.System;
        _store = storeFile;
        Restore();
    }

    /// <summary>T-0005 Not-Stopp: solange aktiv, wird nichts freigegeben und nichts ausgeführt.</summary>
    public bool Halted { get; private set; }
    public string HaltReason { get; private set; } = "";
    /// <summary>Wird beim Not-Stopp abgebrochen. Laufende Automationen prüfen ihn zwischen den Schritten.</summary>
    public CancellationToken HaltToken { get { lock (_lock) return _halt.Token; } }
    public event Action? HaltChanged;

    /// <summary>Hält alles an: offene und erteilte, noch nicht ausgeführte Freigaben verfallen, laufende Automationen brechen ab.
    /// Stoppen darf jeder – Sicherheit geht vor Rechten. Gibt die Zahl der verworfenen Freigaben zurück.</summary>
    public int Halt(Principal by, string reason)
    {
        CancellationTokenSource old;
        int dropped;
        lock (_lock)
        {
            Halted = true; HaltReason = reason;
            var open = _requests.Values.Where(r => r.State is ApprovalState.Pending or ApprovalState.Approved).ToList();
            foreach (var r in open) r.State = ApprovalState.Denied;
            dropped = open.Count;
            old = _halt;
            if (open.Any(r => r.Resumable)) Save();
        }
        old.Cancel();
        audit.Append(by.Name, "notstopp.aktiv", $"{reason}; verworfen={dropped}");
        HaltChanged?.Invoke();
        return dropped;
    }

    /// <summary>Fortsetzen nur mit Freigaberecht. Verworfene Freigaben bleiben verworfen.</summary>
    public void Resume(Principal by)
    {
        Rbac.Demand(by, Permission.ApproveAction, audit);
        lock (_lock)
        {
            if (!Halted) return;
            Halted = false; HaltReason = "";
            _halt.Dispose(); _halt = new CancellationTokenSource();
        }
        audit.Append(by.Name, "notstopp.aufgehoben", "");
        HaltChanged?.Invoke();
    }

    /// <param name="resumable">T-0713: Anfrage überlebt einen Neustart (nur wenn der Kern sie danach selbst ausführen kann).</param>
    public ApprovalRequest Request(Principal by, string action, string description, string target, TimeSpan? ttl = null, string? preview = null, bool resumable = false)
    {
        Rbac.Demand(by, Permission.RequestAction, audit);
        var r = new ApprovalRequest
        {
            Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)),
            Action = action, Description = description, Target = target, Preview = preview, RequestedBy = by.Name, Resumable = resumable,
            CreatedAt = _time.GetLocalNow(), ExpiresAt = _time.GetLocalNow() + (ttl ?? TimeSpan.FromMinutes(10))
        };
        // erst ins Protokoll, dann speichern: beim Laden nach einem Absturz kennt das Protokoll jede gespeicherte Anfrage
        audit.Append(by.Name, "approval.requested", $"{r.Id} {action} {target}");
        lock (_lock) { _requests[r.Id] = r; if (resumable) Save(); }
        return r;
    }

    /// <summary>Offene, noch nicht abgelaufene Anfragen. T-0713: Abgelaufene werden dabei als „abgelaufen“ markiert (im Protokoll) und nicht mehr angeboten.</summary>
    public IReadOnlyList<ApprovalRequest> Pending()
    {
        List<ApprovalRequest> open, expired;
        lock (_lock)
        {
            var now = _time.GetLocalNow();
            expired = _requests.Values.Where(r => r.State == ApprovalState.Pending && now > r.ExpiresAt).ToList();
            foreach (var r in expired) r.State = ApprovalState.Expired;
            if (expired.Any(r => r.Resumable)) Save();
            open = _requests.Values.Where(r => r.State == ApprovalState.Pending).ToList();
        }
        foreach (var r in expired) audit.Append("core", "approval.expired", r.Id);
        return open;
    }

    /// <summary>Nur die Rolle Owner darf freigeben oder ablehnen.</summary>
    public void Approve(string id, Principal by) => Decide(id, ApprovalState.Approved, by);
    public void Deny(string id, Principal by) => Decide(id, ApprovalState.Denied, by);

    /// <summary>B4 (T-0706): Die anfragende Seite zieht eine offene Anfrage zurück (Person hat „Abbrechen“ gewählt, oder ein Kinderprofil
    /// darf nicht freigeben). Braucht nur das Recht anzufragen; kann nie etwas freigeben. Nicht offene Anfragen bleiben unverändert.</summary>
    public void Withdraw(string id, Principal by)
    {
        Rbac.Demand(by, Permission.RequestAction, audit);
        lock (_lock)
        {
            if (!_requests.TryGetValue(id, out var r) || r.State != ApprovalState.Pending) return;
            if (r.RequestedBy != by.Name && !Rbac.Has(by, Permission.ApproveAction))
                throw new UnauthorizedAccessException("Nur wer angefragt hat, kann diese Anfrage zurückziehen.");
            r.State = ApprovalState.Denied;
            if (r.Resumable) Save();
        }
        audit.Append(by.Name, "approval.withdrawn", id);
    }

    /// <summary>T-0713: Eine offene oder erteilte, aber noch nicht ausgeführte Freigabe verwerfen (z. B. Sammel-Freigabe, deren Einrichtung abgebrochen wurde).
    /// Danach kann sie nie mehr ausgeführt werden. Gleiche Rechte wie <see cref="Withdraw"/>; schon ausgeführte bleiben unverändert.</summary>
    public void Discard(string id, Principal by)
    {
        Rbac.Demand(by, Permission.RequestAction, audit);
        lock (_lock)
        {
            if (!_requests.TryGetValue(id, out var r) || r.State is not (ApprovalState.Pending or ApprovalState.Approved)) return;
            if (r.RequestedBy != by.Name && !Rbac.Has(by, Permission.ApproveAction))
                throw new UnauthorizedAccessException("Nur wer angefragt hat, kann diese Freigabe verwerfen.");
            r.State = ApprovalState.Denied;
            if (r.Resumable) Save();
        }
        audit.Append(by.Name, "approval.discarded", id);
    }

    private void Decide(string id, ApprovalState s, Principal by)
    {
        Rbac.Demand(by, Permission.ApproveAction, audit);
        var who = by.Name;
        var expired = false;
        lock (_lock)
        {
            if (s == ApprovalState.Approved && Halted) throw new ApprovalException("Not-Stopp ist aktiv. Erst fortsetzen, dann freigeben.");
            if (!_requests.TryGetValue(id, out var r) || r.State != ApprovalState.Pending)
                throw new ApprovalException("Keine offene Freigabe mit dieser ID.");
            if (r.Resumable && _store is not null && ClosedElsewhere(id, decisions: true))
            {
                r.State = ApprovalState.Denied;
                Save();
                audit.Append("core", "approval.closed.elsewhere", id);
                throw new ApprovalException("Diese Anfrage wurde schon in einem anderen Trendo-Fenster entschieden.");
            }
            // T-0713: abgelaufene Anfragen kann niemand mehr freigeben. Ablehnen einer abgelaufenen Anfrage ist ohne Wirkung (sie verfällt ohnehin).
            if (_time.GetLocalNow() > r.ExpiresAt) { r.State = ApprovalState.Expired; expired = true; }
            else r.State = s;
            if (r.Resumable) Save();
        }
        if (expired)
        {
            audit.Append(who, "approval.expired", id);
            if (s == ApprovalState.Approved) throw new ApprovalException("Die Anfrage ist abgelaufen. Bitte noch einmal anfragen – es wurde nichts ausgeführt.");
            return;
        }
        audit.Append(who, s == ApprovalState.Approved ? "approval.approved" : "approval.denied", id);
    }

    /// <summary>Führt die Aktion genau einmal aus, nur wenn freigegeben und Ziel unverändert.</summary>
    public T Execute<T>(string id, string action, string target, Func<T> run)
    {
        ApprovalRequest r;
        lock (_lock)
        {
            if (Halted) throw Block(id, "Not-Stopp aktiv");
            if (!_requests.TryGetValue(id, out r!)) throw Block(id, "unbekannt");
            if ((r.State is ApprovalState.Pending or ApprovalState.Approved) && _time.GetLocalNow() > r.ExpiresAt)
            {
                r.State = ApprovalState.Expired;
                if (r.Resumable) Save();
                audit.Append("core", "approval.expired", id); // T-0713: Ablauf eigens im Protokoll
                throw Block(id, "abgelaufen");
            }
            if (r.State != ApprovalState.Approved) throw Block(id, r.State.ToString());
            if (r.Resumable && _store is not null && ClosedElsewhere(id, decisions: false)) { r.State = ApprovalState.Denied; throw Block(id, "schon anderswo ausgeführt"); }
            if (r.Action != action || r.Target != target) throw Block(id, "Aktion/Ziel weicht von Freigabe ab");
            r.State = ApprovalState.Executed;
        }
        audit.Append("core", "execution.start", $"{id} {action} {target}");
        var result = run();
        audit.Append("core", "execution.done", id);
        return result;
    }

    private ApprovalException Block(string id, string why)
    {
        audit.Append("core", "execution.blocked", $"{id}: {why}");
        return new ApprovalException($"Ausführung blockiert: {why}");
    }

    // ───────────── T-0713: offene, fortsetzbare Anfragen über einen Neustart retten ─────────────
    /// <summary>Gespeichert wird nur, was zum Wiederfinden nötig ist. Beschreibung, Vorschau, Anfragende Person und Zeitpunkt kommen beim Laden aus dem
    /// Protokoll bzw. dem Kern (nie aus der Datei) – so lässt sich über die Ablage weder ein Text fälschen noch die Laufzeit verlängern.</summary>
    sealed record Stored(string Id, string Action, string Target, DateTimeOffset ExpiresAt);
    /// <summary>Höchste Laufzeit einer geladenen Anfrage ab ihrem Eintrag im Protokoll (alle fortsetzbaren Anfragen gelten 10 Minuten).</summary>
    public static readonly TimeSpan MaxResumableTtl = TimeSpan.FromMinutes(10);
    /// <summary>Nur diese Wirkungen kann der Kern nach einem Neustart selbst prüfen und ausführen.</summary>
    public static bool ResumableAction(string action) => action is Integrations.OpenUrl or Integrations.OpenFolder || action.StartsWith("prompt:", StringComparison.Ordinal);

    static string DescribeRestored(string action, string target) => action switch
    {
        Integrations.OpenUrl => "URL im Standardbrowser öffnen: " + (Uri.TryCreate(target, UriKind.Absolute, out var u) ? u.Host : target),
        Integrations.OpenFolder => "Projektordner im Explorer öffnen",
        _ => "KI-Auftrag mit Wirkung nach außen (vor dem Neustart angefragt – Vorschau im Auftrag)",
    };
    sealed record StoreFile(int Version, List<Stored> Requests);

    /// <summary>Nur offene, fortsetzbare, nicht abgelaufene Anfragen – atomar (alte oder neue Fassung, nie eine halbe). Aufruf unter <c>_lock</c>.
    /// T-0726: zwischen Prozessen gesperrt (<see cref="LockStore"/>) und zusammengeführt – Anfragen, die ein anderes Trendo-Fenster mit demselben
    /// Datenordner abgelegt hat, bleiben erhalten (vorher überschrieb jedes Fenster die Ablage mit nur seinen eigenen Anfragen).
    /// Ausgenommen: was dieses Tor kennt (es hat entschieden oder hält sie offen) und was es beim Laden als abgelaufen/ungültig verworfen hat.</summary>
    private void Save()
    {
        if (_store is null) return;
        try
        {
            using var sperre = LockStore();
            var now = _time.GetLocalNow();
            var keep = _requests.Values.Where(r => r.Resumable && r.State == ApprovalState.Pending && r.ExpiresAt >= now)
                .Where(r => !r.Restored || !ClosedElsewhere(r.Id, decisions: true)) // nach einem Neustart in zwei Fenstern geladen, im anderen schon entschieden
                .Select(r => new Stored(r.Id, r.Action, r.Target, r.ExpiresAt)).ToList();
            var others = ReadStore()?.Requests?.Where(x => x is not null && !string.IsNullOrEmpty(x.Id) && !_requests.ContainsKey(x.Id) && !_dropped.Contains(x.Id) && x.ExpiresAt >= now) ?? [];
            keep.AddRange(others.DistinctBy(x => x.Id));
            if (keep.Count == 0) { if (File.Exists(_store)) File.Delete(_store); return; }
            AtomicFile.WriteAllText(_store, JsonSerializer.Serialize(new StoreFile(1, keep)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            audit.Append("core", "approval.store.failed", e.GetType().Name); // Freigaben laufen weiter, nur der Neustart-Schutz fehlt
        }
    }

    /// <summary>T-0726: beim Laden verworfene Einträge (abgelaufen, ungültig) – sie werden beim Zusammenführen nicht wieder übernommen.</summary>
    private readonly HashSet<string> _dropped = new(StringComparer.Ordinal);

    private StoreFile? ReadStore()
    {
        try { return File.Exists(_store) ? JsonSerializer.Deserialize<StoreFile>(File.ReadAllText(_store!)) : null; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>T-0726: Sperre der Ablage zwischen Prozessen – eine Sperrdatei neben offen.json, exklusiv geöffnet (das Betriebssystem gibt sie beim
    /// Prozessende frei, auch nach einem Absturz). Wartet höchstens rund 1 s, dann <see cref="IOException"/> (die Ablage wird dann einmal nicht geschrieben).</summary>
    private FileStream LockStore()
    {
        var f = _store + ".sperre";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(f))!);
        for (var i = 0; ; i++)
        {
            try { return new FileStream(f, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1); }
            catch (IOException) when (i < 100) { Thread.Sleep(10); } // höchstens ~1 s (läuft unter _lock, z. B. auch beim Not-Stopp)
        }
    }

    /// <summary>Hat das Protokoll diese Anfrage schon abgeschlossen? <paramref name="decisions"/>: auch Freigabe/Ablehnung zählen (vor der eigenen Entscheidung),
    /// sonst nur eine Ausführung (nach der eigenen Freigabe).
    /// Ein unlesbares Protokoll zählt als „abgeschlossen“ (sicher scheitern statt doppelt ausführen).</summary>
    private bool ClosedElsewhere(string id, bool decisions)
    {
        try
        {
            return audit.ReadAll().Any(a => a.Detail.Split(' ')[0] == id && (a.Action == "execution.start"
                || decisions && a.Action is "approval.approved" or "approval.denied" or "approval.withdrawn" or "approval.discarded" or "approval.expired"));
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or IOException) { return true; }
    }

    /// <summary>Beim Start: gespeicherte offene Anfragen laden. Abgelaufene verfallen. Übernommen wird nur, was das Prüfprotokoll (Hash-Kette intakt)
    /// genau so als angefragt und seitdem nicht entschieden kennt – eine von Hand eingefügte Anfrage taucht nie in der Freigaben-Liste auf.
    /// Geladen wird immer als „offen“: eine Freigabe muss nach dem Neustart neu erteilt werden.</summary>
    private void Restore()
    {
        if (_store is null || !File.Exists(_store)) return;
        StoreFile? file;
        try { using (LockStore()) file = ReadStore(); } // T-0726: nicht mitten in den Schreibvorgang eines anderen Fensters hinein lesen
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { file = null; } // Start darf daran nie scheitern
        var now = _time.GetLocalNow();
        var restored = new List<string>();
        int expired = 0, rejected = 0;
        if (file is { Version: 1, Requests: { Count: > 0 } list })
        {
            IReadOnlyList<AuditEntry> log;
            try { log = audit.Trusted(); } // T-0726: mit Schlüssel nur gültig signierte Einträge (ein Abschnitt ohne Signatur zählt nicht)
            catch (Exception e) when (e is JsonException or IOException) { log = []; } // unlesbares Protokoll: nichts übernehmen
            var asked = log.Where(a => a.Action == "approval.requested").GroupBy(a => a.Detail, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var closed = log.Where(a => a.Action is "approval.approved" or "approval.denied" or "approval.withdrawn" or "approval.discarded" or "approval.expired" or "execution.start")
                .Select(a => a.Detail.Split(' ')[0]).ToHashSet(StringComparer.Ordinal);
            foreach (var s in list.Where(x => x is not null).DistinctBy(x => x.Id))
            {
                if (s.ExpiresAt < now) { expired++; Drop(s.Id); continue; }
                if (string.IsNullOrEmpty(s.Id) || s.Action is null || s.Target is null || !ResumableAction(s.Action)
                    || !asked.TryGetValue($"{s.Id} {s.Action} {s.Target}", out var entry) || closed.Contains(s.Id)) { rejected++; Drop(s.Id); continue; }
                var until = s.ExpiresAt < entry.At + MaxResumableTtl ? s.ExpiresAt : entry.At + MaxResumableTtl;
                if (until < now) { expired++; Drop(s.Id); continue; }
                _requests[s.Id] = new ApprovalRequest
                {
                    Id = s.Id, Action = s.Action, Target = s.Target, Description = DescribeRestored(s.Action, s.Target), CreatedAt = entry.At, ExpiresAt = until,
                    RequestedBy = entry.Actor, Resumable = true, Restored = true
                };
                restored.Add(s.Id);
            }
        }
        else if (file is not { Version: 1 }) rejected++; // unlesbar oder fremde Fassung: nichts übernehmen
        void Drop(string? id) { if (!string.IsNullOrEmpty(id)) _dropped.Add(id); }
        lock (_lock) Save(); // Datei auf den geprüften Stand bringen (Abgelaufenes und Ungültiges raus)
        try
        {
            foreach (var id in restored) audit.Append("core", "approval.restored", id);
            if (expired + rejected > 0) audit.Append("core", "approval.restore.dropped", $"abgelaufen={expired} ungültig={rejected}");
        }
        catch (Exception e) when (e is JsonException or IOException) { } // unlesbares Protokoll: das Laden selbst lässt den Start nicht scheitern
    }
}

public sealed class ApprovalException(string msg) : Exception(msg);
