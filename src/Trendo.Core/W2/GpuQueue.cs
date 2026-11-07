namespace Trendo.Core;

/// <summary>Grafikspeicher-Regel (Wunsch 04.11.2026): große KI-Aufträge nie gleichzeitig, nur wenn gebraucht. Alle Aufträge, die die Grafikkarte
/// schwer belasten (Musik, Bild, Video, Spuren trennen, Stimmmodell, 3D aus Foto, Geräusche), laufen über diese eine Warteschlange – immer nur einer,
/// die anderen warten in Reihenfolge und lassen sich abbrechen. Vorher wurden z. B. Cover-Bild und Lied gleichzeitig gestartet und blockierten sich.</summary>
public sealed class GpuQueue
{
    public sealed class Ticket(long id, string kind, string title)
    {
        public long Id { get; } = id;
        public string Kind { get; } = kind;
        public string Title { get; } = title;
        public bool Running { get; internal set; }
        public DateTimeOffset Queued { get; } = DateTimeOffset.Now;
        /// <summary>Seit wann der Auftrag läuft (null = wartet noch).</summary>
        public DateTimeOffset? Started { get; internal set; }
    }

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly List<Ticket> _items = [];
    long _next;

    /// <summary>Wird bei jeder Änderung gemeldet (aus beliebigem Thread).</summary>
    public event Action? Changed;

    public IReadOnlyList<Ticket> Items { get { lock (_items) return [.. _items]; } }
    public Ticket? Current { get { lock (_items) return _items.FirstOrDefault(t => t.Running); } }

    /// <summary>Wie viele Aufträge vor diesem warten bzw. laufen (0 = er läuft gerade).</summary>
    public int Ahead(Ticket t) { lock (_items) { var i = _items.IndexOf(t); return i < 0 ? 0 : i; } }

    /// <summary>Auftrag einreihen und ausführen, sobald die Grafikkarte frei ist. <paramref name="onWait"/> meldet „noch n vor dir“.</summary>
    public async Task<T> RunAsync<T>(string kind, string title, Func<CancellationToken, Task<T>> job, CancellationToken ct = default, Action<int>? onWait = null)
    {
        var t = new Ticket(Interlocked.Increment(ref _next), kind, title);
        lock (_items) _items.Add(t);
        Changed?.Invoke();
        try
        {
            if (!_gate.Wait(0))
            {
                onWait?.Invoke(Ahead(t));
                await _gate.WaitAsync(ct);
            }
            try
            {
                t.Running = true; t.Started = DateTimeOffset.Now; Changed?.Invoke();
                return await job(ct);
            }
            finally { _gate.Release(); }
        }
        finally
        {
            lock (_items) _items.Remove(t);
            Changed?.Invoke();
        }
    }

    /// <summary>Platz holen (wartet, bis die Grafikkarte frei ist); mit <c>using</c> wieder freigeben – für bestehende Abläufe ohne Umbau.</summary>
    public async Task<IDisposable> AcquireAsync(string kind, string title, CancellationToken ct = default, Action<int>? onWait = null)
    {
        var t = new Ticket(Interlocked.Increment(ref _next), kind, title);
        lock (_items) _items.Add(t);
        Changed?.Invoke();
        try
        {
            if (!_gate.Wait(0)) { onWait?.Invoke(Ahead(t)); await _gate.WaitAsync(ct); }
        }
        catch { lock (_items) _items.Remove(t); Changed?.Invoke(); throw; }
        t.Running = true; t.Started = DateTimeOffset.Now; Changed?.Invoke();
        return new Slot(() => { _gate.Release(); lock (_items) _items.Remove(t); Changed?.Invoke(); });
    }

    sealed class Slot(Action release) : IDisposable
    {
        int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) release(); }
    }

    public Task RunAsync(string kind, string title, Func<CancellationToken, Task> job, CancellationToken ct = default, Action<int>? onWait = null) =>
        RunAsync<bool>(kind, title, async c => { await job(c); return true; }, ct, onWait);
}
