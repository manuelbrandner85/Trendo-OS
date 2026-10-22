using System.Text.Json;
using Trendo.Core;

namespace Trendo.Core.PromptAction;

/// <summary>Einwilligung für einen externen KI-Anbieter. Standard ist aus. Werte sind keine Geheimnisse.</summary>
public sealed class ProviderConsent
{
    public bool ExternalAllowed { get; set; }
    public string? Scope { get; set; }
    public string? Disclosure { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public bool AllowsExternal(DateTimeOffset now) =>
        ExternalAllowed && RevokedAt is null && ExpiresAt is DateTimeOffset exp && now < exp && !string.IsNullOrWhiteSpace(Scope);

    public static ProviderConsent Deny { get; } = new();
}

public sealed class ConsentStore
{
    private readonly string _file;
    private readonly AuditLog _audit;
    private readonly TimeProvider _time;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public ConsentStore(string dataRoot, AuditLog audit, TimeProvider? time = null)
    {
        _file = Path.Combine(dataRoot, "ki", "einwilligung.json");
        _audit = audit;
        _time = time ?? TimeProvider.System;
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
    }

    public ProviderConsent Load()
    {
        if (!File.Exists(_file)) return ProviderConsent.Deny;
        try { return JsonSerializer.Deserialize<ProviderConsent>(File.ReadAllText(_file)) ?? ProviderConsent.Deny; }
        catch (JsonException) { return ProviderConsent.Deny; }
    }

    /// <summary>Nur nach angezeigter Kosten- und Datenangabe. Die Frist ist Pflicht, es gibt keine Dauerfreigabe.</summary>
    public ProviderConsent Grant(string scope, string disclosure, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("Der Umfang der Einwilligung fehlt.");
        if (string.IsNullOrWhiteSpace(disclosure)) throw new ArgumentException("Kosten, Daten und Zweck müssen genannt sein.");
        if (ttl <= TimeSpan.Zero || ttl > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(ttl), "Die Einwilligung gilt höchstens einen Tag.");
        var c = new ProviderConsent
        {
            ExternalAllowed = true,
            Scope = scope.Trim(),
            Disclosure = disclosure.Trim(),
            ExpiresAt = _time.GetLocalNow() + ttl
        };
        File.WriteAllText(_file, JsonSerializer.Serialize(c, Json));
        _audit.Append(Principal.LocalOwner.Name, "ki.consent.grant", $"umfang={c.Scope} bis={c.ExpiresAt:yyyy-MM-dd HH:mm}");
        return c;
    }

    public ProviderConsent Revoke(string reason)
    {
        var c = Load();
        c.ExternalAllowed = false;
        c.RevokedAt = _time.GetLocalNow();
        c.RevokedReason = string.IsNullOrWhiteSpace(reason) ? "widerrufen" : reason.Trim();
        File.WriteAllText(_file, JsonSerializer.Serialize(c, Json));
        _audit.Append(Principal.LocalOwner.Name, "ki.consent.revoke", "widerrufen");
        return c;
    }
}
