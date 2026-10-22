namespace Trendo.Core.PromptAction;

/// <summary>
/// Dienste, die ein Auftrag benutzen darf. Ohne gesetzte Methode passiert die Wirkung nicht.
/// „Freigabe“ im Text setzt hier nichts.
/// </summary>
public sealed class PromptServices
{
    public bool Offline { get; init; } = true;
    public string? DataRoot { get; init; }
    public AuditLog? Audit { get; init; }
    public ApprovalGate? Gate { get; init; }
    public ProjectStore? Projects { get; init; }
    /// <summary>Lokale Frage. null heißt: es ist keine KI angeschlossen.</summary>
    public Func<string, string?, CancellationToken, Task<KiAnswer>>? Ask { get; init; }
    public Func<string>? SecretBackend { get; init; }
    public Func<bool>? AuditIntact { get; init; }
    public string VideoSys { get; init; } = "/sys/class/video4linux";
    public Func<IReadOnlyList<DeviceInfo>>? Devices { get; init; }
    public Func<string?>? FindAdb { get; init; }
    public IProcessRunner? Runner { get; init; }
    public Func<string, bool>? FileExists { get; init; }
    public string? ConsentFile { get; init; }
    public Func<Uri, CancellationToken, Task<(string Type, byte[] Bytes, Uri Final)>>? ReadBytes { get; init; }
    public Func<IReadOnlyList<KiSourceInfo>, CancellationToken, Task<IReadOnlyList<(string Name, bool Open)>>>? Probe { get; init; }
    /// <summary>Startet ein Programm aus dem Katalog. null heißt: es wird nichts gestartet.</summary>
    public Func<string, IReadOnlyList<string>, string>? Launch { get; init; }
    /// <summary>Öffnet die Freigabe. null heißt: es wird nichts geteilt.</summary>
    public Func<string, TimeSpan, string>? StartShare { get; init; }

    public static PromptServices Closed { get; } = new() { Offline = true };

    public static PromptServices For(TrendoCore core, PromptServices? given) => new()
    {
        Offline = given?.Offline ?? core.Offline,
        DataRoot = given?.DataRoot ?? core.DataRoot,
        Audit = given?.Audit ?? core.Audit,
        Gate = given?.Gate ?? core.Approvals,
        Projects = given?.Projects ?? core.Projects,
        Ask = given?.Ask,
        SecretBackend = given?.SecretBackend ?? (() => core.Secrets.BackendName),
        AuditIntact = given?.AuditIntact ?? (() => core.Audit.Verify()),
        VideoSys = given is null || string.IsNullOrWhiteSpace(given.VideoSys) ? "/sys/class/video4linux" : given.VideoSys,
        Devices = given?.Devices,
        FindAdb = given?.FindAdb,
        Runner = given?.Runner,
        FileExists = given?.FileExists,
        ConsentFile = given?.ConsentFile ?? Path.Combine(core.DataRoot, "deps-consent.txt"),
        ReadBytes = given?.ReadBytes,
        Probe = given?.Probe,
        Launch = given?.Launch,
        StartShare = given?.StartShare,
    };
}

/// <summary>Gilt nur für den laufenden Auftrag. Tests laufen parallel und sehen sich nicht.</summary>
public static class PromptAmbient
{
    static readonly AsyncLocal<PromptServices?> Slot = new();

    public static PromptServices Current => Slot.Value ?? PromptServices.Closed;

    public static IDisposable Use(PromptServices services)
    {
        var prior = Slot.Value;
        Slot.Value = services;
        return new Pop(() => Slot.Value = prior);
    }

    sealed class Pop(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
