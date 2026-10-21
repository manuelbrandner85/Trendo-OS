using System.Security.Cryptography;

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
}

/// <summary>Plan → Freigabe → Ausführung. Ohne gültige, einmalige, nicht abgelaufene Freigabe wird nichts ausgeführt.</summary>
public sealed class ApprovalGate(AuditLog audit, TimeProvider? time = null)
{
    private readonly Dictionary<string, ApprovalRequest> _requests = new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _lock = new();

    public ApprovalRequest Request(Principal by, string action, string description, string target, TimeSpan? ttl = null)
    {
        Rbac.Demand(by, Permission.RequestAction, audit);
        var r = new ApprovalRequest
        {
            Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)),
            Action = action, Description = description, Target = target,
            CreatedAt = _time.GetLocalNow(), ExpiresAt = _time.GetLocalNow() + (ttl ?? TimeSpan.FromMinutes(10))
        };
        lock (_lock) _requests[r.Id] = r;
        audit.Append(by.Name, "approval.requested", $"{r.Id} {action} {target}");
        return r;
    }

    public IReadOnlyList<ApprovalRequest> Pending()
    {
        lock (_lock) return _requests.Values.Where(r => r.State == ApprovalState.Pending).ToList();
    }

    /// <summary>Nur die Rolle Owner darf freigeben oder ablehnen.</summary>
    public void Approve(string id, Principal by) => Decide(id, ApprovalState.Approved, by);
    public void Deny(string id, Principal by) => Decide(id, ApprovalState.Denied, by);

    private void Decide(string id, ApprovalState s, Principal by)
    {
        Rbac.Demand(by, Permission.ApproveAction, audit);
        var who = by.Name;
        lock (_lock)
        {
            if (!_requests.TryGetValue(id, out var r) || r.State != ApprovalState.Pending)
                throw new ApprovalException("Keine offene Freigabe mit dieser ID.");
            r.State = s;
        }
        audit.Append(who, s == ApprovalState.Approved ? "approval.approved" : "approval.denied", id);
    }

    /// <summary>Führt die Aktion genau einmal aus, nur wenn freigegeben und Ziel unverändert.</summary>
    public T Execute<T>(string id, string action, string target, Func<T> run)
    {
        ApprovalRequest r;
        lock (_lock)
        {
            if (!_requests.TryGetValue(id, out r!)) throw Block(id, "unbekannt");
            if (r.State == ApprovalState.Pending && _time.GetLocalNow() > r.ExpiresAt) r.State = ApprovalState.Expired;
            if (r.State == ApprovalState.Approved && _time.GetLocalNow() > r.ExpiresAt) r.State = ApprovalState.Expired;
            if (r.State != ApprovalState.Approved) throw Block(id, r.State.ToString());
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
}

public sealed class ApprovalException(string msg) : Exception(msg);
