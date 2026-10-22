namespace Trendo.Core;

/// <summary>Hält steuerbare lokale Automationen an. Ein Schalter für Abläufe und Agenten, auch vom Handy.</summary>
public sealed class NotStopp
{
    private readonly object _lock = new();
    private CancellationTokenSource _cancel = new();

    public bool IsStopped { get; private set; }
    public DateTimeOffset? Since { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>Token bricht laufende KI-Aufrufe ab, sobald <see cref="StopAll"/> gedrückt wird.</summary>
    public CancellationToken Token { get { lock (_lock) return _cancel.Token; } }

    public void StopAll(AuditLog audit, string by, string reason = "Not-Stopp")
    {
        lock (_lock)
        {
            IsStopped = true;
            Since = DateTimeOffset.Now;
            Reason = string.IsNullOrWhiteSpace(reason) ? "Not-Stopp" : reason.Trim();
            try { _cancel.Cancel(); } catch (ObjectDisposedException) { }
        }
        audit.Append(by, "automation.notstopp", Reason);
    }

    /// <summary>Automatik wieder erlauben. Laufende Aufträge starten nicht von selbst neu.</summary>
    public void Resume(AuditLog audit, string by)
    {
        var changed = false;
        lock (_lock)
        {
            if (!IsStopped) return;
            IsStopped = false;
            Since = null;
            Reason = null;
            var old = _cancel;
            _cancel = new CancellationTokenSource();
            old.Dispose();
            changed = true;
        }
        if (changed) audit.Append(by, "automation.notstopp.aufgehoben", "");
    }
}
