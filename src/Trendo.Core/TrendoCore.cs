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
    public string Version => "0.2.0-p1";
    /// <summary>Offline-Modus: alle netzwerkbezogenen Aktionen werden vom Core abgewiesen.</summary>
    public bool Offline { get; }

    public TrendoCore(string dataRoot, ISecretStore? secrets = null, IProcessLauncher? launcher = null, TimeProvider? time = null, bool offline = false)
    {
        Offline = offline;
        DataRoot = Path.GetFullPath(dataRoot);
        Directory.CreateDirectory(DataRoot);
        Audit = new AuditLog(Path.Combine(DataRoot, "audit", "audit.jsonl"));
        Projects = new ProjectStore(Path.Combine(DataRoot, "projects"), Audit);
        var store = secrets ?? SecretStoreFactory.CreateDefault();
        Secrets = PersonStore.IdOf(DataRoot) is { } person ? new ScopedSecretStore(store, "person-" + person) : store; // jede Person eigene Schlüssel
        // T-0726: Prüfprotokoll mit Schlüssel aus dem geschützten Speicher signieren (ein Schlüssel je Windows-Benutzer, nicht im Datenordner) – vor dem
        // Freigabe-Tor, das beim Laden die Kette prüft. Ohne dauerhaften Speicher bleibt es bei der Hash-Kette.
        if (AuditKey.LoadOrCreate(store) is { } key)
        {
            Audit.UseKey(key);
            // Einträge ohne Signatur (ältere Fassung nach einem Rücksprung, Tresor beim Start nicht erreichbar) sichtbar festhalten statt für immer „beschädigt“
            try { Audit.AcknowledgeUnsigned("core"); } catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { }
        }
        Approvals = new ApprovalGate(Audit, time, Path.Combine(DataRoot, "freigaben", "offen.json")); // T-0713: offene, fortsetzbare Anfragen überleben einen Neustart
        Integrations = new Integrations(Approvals, Projects, launcher ?? new ShellProcessLauncher(), () => Offline);
        Audit.Append("core", "core.start", $"v{Version} secrets={Secrets.BackendName} offline={Offline} protokoll={(Audit.Keyed ? "signiert" : "hash-kette")}");
    }

    public void Shutdown() => Audit.Append("core", "core.stop", "sauber beendet");

    /// <summary>%APPDATA%\\TrendoOS – getrennt vom Programmordner (%LOCALAPPDATA%\\TrendoOS); alte Daten werden einmalig migriert.</summary>
    public static string DefaultDataRoot()
    {
        var d = DataLocation.Default();
        try { DataLocation.MigrateLegacy(DataLocation.Legacy(), d); } catch { /* Migration darf Start nicht verhindern */ }
        return d;
    }

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
