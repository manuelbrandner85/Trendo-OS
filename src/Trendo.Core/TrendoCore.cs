namespace Trendo.Core;

/// <summary>Lokaler Trendo-Core (P1: In-Process-Dienst, keine Netzwerkschnittstelle). Bündelt alle Dienste mit einer Datenwurzel.</summary>
public sealed class TrendoCore
{
    public string DataRoot { get; }
    public AuditLog Audit { get; }
    public ProjectStore Projects { get; }
    public ApprovalGate Approvals { get; }
    public ISecretStore Secrets { get; }
    public Integrations Integrations { get; }
    public StudioRegistry Studios { get; } = new();
    public string Version => "0.5.0";
    /// <summary>Offline-Modus: alle netzwerkbezogenen Aktionen werden vom Core abgewiesen.</summary>
    public bool Offline { get; }

    public TrendoCore(string dataRoot, ISecretStore? secrets = null, IProcessLauncher? launcher = null, TimeProvider? time = null, bool offline = false)
    {
        Offline = offline;
        DataRoot = Path.GetFullPath(dataRoot);
        Directory.CreateDirectory(DataRoot);
        Audit = new AuditLog(Path.Combine(DataRoot, "audit", "audit.jsonl"));
        Projects = new ProjectStore(Path.Combine(DataRoot, "projects"), Audit);
        Approvals = new ApprovalGate(Audit, time);
        Secrets = secrets ?? SecretStoreFactory.CreateDefault();
        Integrations = new Integrations(Approvals, Projects, launcher ?? new ShellProcessLauncher(), () => Offline);
        Audit.Append("core", "core.start", $"v{Version} secrets={Secrets.BackendName} offline={Offline}");
    }

    public void Shutdown() => Audit.Append("core", "core.stop", "sauber beendet");

    public static string DefaultDataRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrendoOS");

    /// <summary>Sucht requirements.yaml neben der Anwendung oder aufwärts im Repo.</summary>
    public static string? FindRequirements(string startDir)
    {
        for (var d = new DirectoryInfo(startDir); d is not null; d = d.Parent)
        {
            var f = Path.Combine(d.FullName, "requirements.yaml");
            if (File.Exists(f)) return f;
        }
        return null;
    }
}
