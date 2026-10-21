using Microsoft.Extensions.Time.Testing;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public sealed class FakeLauncher : IProcessLauncher
{
    public List<string> Opened { get; } = [];
    public void Open(string target) => Opened.Add(target);
}

public sealed class Fixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "trendo-test-" + Guid.NewGuid().ToString("N"));
    public FakeLauncher Launcher { get; } = new();
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.Now);
    public TrendoCore Core { get; }
    public Fixture() => Core = new TrendoCore(Root, new InMemorySecretStore(), Launcher, Time);
    public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
}

/// QA-001: Start und sauberes Beenden
public class QA001_StartStop
{
    [Fact(DisplayName = "QA-001: Core startet, legt Datenstruktur an und beendet sauber mit Audit-Eintrag")]
    public void Core_starts_and_stops_cleanly()
    {
        using var f = new Fixture();
        Assert.True(Directory.Exists(Path.Combine(f.Root, "projects")));
        f.Core.Shutdown();
        var log = f.Core.Audit.ReadAll();
        Assert.Equal("core.start", log[0].Action);
        Assert.Equal("core.stop", log[^1].Action);
        Assert.True(f.Core.Audit.Verify());
    }
}

/// QA-003: Projektisolation über Datei/API
public class QA003_ProjectIsolation
{
    [Theory(DisplayName = "QA-003: Pfadausbrüche werden abgewiesen und auditiert")]
    [InlineData("../b/files/secret.txt")]
    [InlineData("..\\..\\x.txt")]
    [InlineData("sub/../../../escape.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("")]
    public void Escapes_are_denied(string rel)
    {
        using var f = new Fixture();
        f.Core.Projects.Create("projekt-a", "A");
        f.Core.Projects.Create("projekt-b", "B");
        Assert.Throws<ProjectIsolationException>(() => f.Core.Projects.ResolveInProject("projekt-a", rel));
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "isolation.denied");
    }

    [Fact(DisplayName = "QA-003: Projekt A kann Dateien von Projekt B nicht lesen")]
    public void Cross_project_read_denied()
    {
        using var f = new Fixture();
        f.Core.Projects.Create("projekt-a", "A");
        f.Core.Projects.Create("projekt-b", "B");
        f.Core.Projects.WriteFile("projekt-b", "geheim.txt", "B-only");
        Assert.Equal("B-only", f.Core.Projects.ReadFile("projekt-b", "geheim.txt"));
        Assert.Throws<ProjectIsolationException>(() => f.Core.Projects.ReadFile("projekt-a", "../../projekt-b/files/geheim.txt"));
    }

    [Theory(DisplayName = "QA-003: Ungültige Projekt-IDs werden abgewiesen")]
    [InlineData("..")] [InlineData("A")] [InlineData("a/b")] [InlineData("x")]
    public void Invalid_ids(string id)
    {
        using var f = new Fixture();
        Assert.ThrowsAny<Exception>(() => f.Core.Projects.Create(id, id));
    }

    [Fact(DisplayName = "QA-003: Symlink im Projekt nach außen wird abgewiesen")]
    public void Symlink_escape_denied()
    {
        using var f = new Fixture();
        f.Core.Projects.Create("projekt-a", "A");
        var outside = Directory.CreateDirectory(Path.Combine(f.Root, "outside")).FullName;
        var link = Path.Combine(f.Core.Projects.ProjectFilesRoot("projekt-a"), "link");
        try { Directory.CreateSymbolicLink(link, outside); }
        catch (Exception) { return; } // Windows ohne Entwicklermodus/Admin: Symlink nicht erstellbar → nicht testbar, kein Risiko
        Assert.Throws<ProjectIsolationException>(() => f.Core.Projects.ResolveInProject("projekt-a", "link/x.txt"));
    }
}

/// QA-004: Berechtigungen / Approval-Gate
public class QA004_ApprovalGate
{
    [Fact(DisplayName = "QA-004: Ohne Freigabe wird nichts ausgeführt")]
    public void No_execution_without_approval()
    {
        using var f = new Fixture();
        f.Core.Projects.Create("projekt-a", "A");
        var r = f.Core.Integrations.PlanOpenProjectFolder(Principal.LocalOwner, "projekt-a");
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Abgelehnte Freigabe blockiert")]
    public void Denied_blocks()
    {
        using var f = new Fixture();
        var r = f.Core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org");
        f.Core.Approvals.Deny(r.Id, Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Freigabe ist einmalig (kein Replay)")]
    public void Approval_single_use()
    {
        using var f = new Fixture();
        var r = f.Core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org");
        f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        f.Core.Integrations.Execute(r);
        Assert.Single(f.Launcher.Opened);
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Abgelaufene Freigabe blockiert")]
    public void Expired_blocks()
    {
        using var f = new Fixture();
        var r = f.Core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org");
        f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        f.Time.Advance(TimeSpan.FromMinutes(11));
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Freigabe gilt nicht für anderes Ziel")]
    public void Target_mismatch_blocks()
    {
        using var f = new Fixture();
        var r = f.Core.Approvals.Request(Principal.LocalOwner, Integrations.OpenUrl, "x", "https://example.org/");
        f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        Assert.Throws<ApprovalException>(() => f.Core.Approvals.Execute(r.Id, Integrations.OpenUrl, "https://evil.example/", () => true));
    }

    [Theory(DisplayName = "QA-004: Nur https-URLs dürfen geplant werden")]
    [InlineData("http://example.org")] [InlineData("file:///C:/Windows")] [InlineData("javascript:alert(1)")] [InlineData("nicht-url")]
    public void Only_https(string url)
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Core.Integrations.PlanOpenUrl(Principal.LocalOwner, url));
    }

    [Fact(DisplayName = "QA-004: Audit-Log ist hash-verkettet; Manipulation wird erkannt")]
    public void Audit_tamper_detected()
    {
        using var f = new Fixture();
        f.Core.Projects.Create("projekt-a", "A");
        Assert.True(f.Core.Audit.Verify());
        var lines = File.ReadAllLines(f.Core.Audit.FilePath);
        lines[1] = lines[1].Replace("projekt-a", "projekt-x");
        File.WriteAllLines(f.Core.Audit.FilePath, lines);
        Assert.False(f.Core.Audit.Verify());
    }

    [Fact(DisplayName = "Secrets: Werte werden gespeichert/gelöscht und nie ins Audit-Log geschrieben")]
    public void Secrets_not_logged()
    {
        using var f = new Fixture();
        f.Core.Secrets.Set("test-key", "S3cr3t-Wert");
        Assert.Equal("S3cr3t-Wert", f.Core.Secrets.Get("test-key"));
        Assert.True(f.Core.Secrets.Delete("test-key"));
        Assert.Null(f.Core.Secrets.Get("test-key"));
        Assert.DoesNotContain("S3cr3t-Wert", File.ReadAllText(f.Core.Audit.FilePath));
    }

    [Fact(DisplayName = "Secrets (nur Windows): Credential Manager Roundtrip")]
    public void Windows_credential_roundtrip()
    {
        if (!OperatingSystem.IsWindows()) return; // auf Linux nicht anwendbar
        var s = new WindowsCredentialStore();
        var key = "selftest-" + Guid.NewGuid().ToString("N");
        s.Set(key, "wert-äöü");
        Assert.Equal("wert-äöü", s.Get(key));
        Assert.True(s.Delete(key));
        Assert.Null(s.Get(key));
    }
}

/// QA-015: Studio-Kapazitätsanzeige bei fehlendem Tool korrekt BLOCKED
public class QA015_StudioStatus
{
    private sealed class Probe(params string[] present) : IToolProbe { public bool IsAvailable(string t) => present.Contains(t); }
    private static string Req => Path.Combine(AppContext.BaseDirectory, "requirements.yaml");

    [Fact(DisplayName = "QA-015: Fehlendes Programm → Blockiert; nichts als Bereit")]
    public void Missing_tool_blocked()
    {
        var list = new StudioRegistry().Load(Req, new Probe());
        Assert.Equal(54, list.Count);
        Assert.Equal(Capability.Blocked, list.Single(s => s.Id == "CORE-05").Capability);
        Assert.DoesNotContain(list, s => s.Capability == Capability.Ready);
    }

    [Fact(DisplayName = "QA-015: Programm vorhanden, Adapter fehlt → Nicht verbunden (nicht Bereit)")]
    public void Present_tool_not_connected()
    {
        var list = new StudioRegistry().Load(Req, new Probe("blender"));
        Assert.Equal(Capability.NotConnected, list.Single(s => s.Id == "CORE-05").Capability);
    }

    [Fact(DisplayName = "QA-015: SPEC-07 abgelehnt, übrige nur Spezifikation")]
    public void Rejected_and_spec_only()
    {
        var list = new StudioRegistry().Load(Req, new Probe("blender", "unreal", "libreoffice", "browser"));
        Assert.Equal(Capability.Rejected, list.Single(s => s.Id == "SPEC-07").Capability);
        Assert.Equal(Capability.SpecOnly, list.Single(s => s.Id == "CORE-01").Capability);
    }
}

/// QA-019: Offline-Betrieb
public class QA019_Offline
{
    [Fact(DisplayName = "QA-019: Kernfunktionen laufen ohne Netzwerk (keine Netz-Abhängigkeit im Core)")]
    public void Core_works_offline()
    {
        using var f = new Fixture();
        f.Core.Projects.Create("offline-test", "Offline");
        f.Core.Projects.WriteFile("offline-test", "notiz.txt", "ok");
        var list = new StudioRegistry().Load(Path.Combine(AppContext.BaseDirectory, "requirements.yaml"), new DefaultToolProbe());
        Assert.NotEmpty(list);
        var asm = typeof(TrendoCore).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain("System.Net.Http", asm);
        Assert.DoesNotContain("System.Net.Sockets", asm);
    }
}

/// QA-019 (simuliert): Netzwerk blockiert
public class QA019_SimulatedNoNetwork
{
    [Fact(DisplayName = "QA-019 (simuliert): Blockierter HttpClient schlägt fehl, Core-Workflows laufen trotzdem")]
    public async Task Blocked_network_core_still_works()
    {
        using var http = new HttpClient(new BlockedNetworkHandler());
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync("https://example.org"));
        var root = Path.Combine(Path.GetTempPath(), "trendo-off-" + Guid.NewGuid().ToString("N"));
        var l = new FakeLauncher();
        var core = new TrendoCore(root, new InMemorySecretStore(), l, offline: true);
        core.Projects.Create("off", "Off");
        core.Projects.WriteFile("off", "a.txt", "x");
        Assert.Throws<OfflineException>(() => core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org"));
        var r = core.Integrations.PlanOpenProjectFolder(Principal.LocalOwner, "off");
        core.Approvals.Approve(r.Id, Principal.LocalOwner); core.Integrations.Execute(r);
        Assert.Single(l.Opened);
        Assert.Contains("offline=True", core.Audit.ReadAll()[0].Detail);
    }
}

public class SelfTestCli
{
    [Fact(DisplayName = "Selbsttest Explorer: läuft nur über protokollierte Freigabe")]
    public void Selftest_logs_approval()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-st-" + Guid.NewGuid().ToString("N"));
        var l = new FakeLauncher();
        var core = new TrendoCore(root, new InMemorySecretStore(), l);
        Assert.Equal(0, SelfTest.OpenExplorer(core, "test-operator", TextWriter.Null));
        var log = core.Audit.ReadAll();
        Assert.Contains(log, e => e.Action == "approval.approved" && e.Actor == "test-operator");
        Assert.Contains(log, e => e.Action == "execution.done");
        Assert.Single(l.Opened);
    }
}

/// <summary>HttpMessageHandler, der jede Anfrage verweigert. Nur für simulierte Offline-Tests (der Core selbst hat keine Netzwerkabhängigkeit).</summary>
public sealed class BlockedNetworkHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => throw new HttpRequestException("Netzwerk im Offline-Modus blockiert (simuliert).");
}

public class Rbac_Core
{
    [Fact(DisplayName = "RBAC Core: Guest/User dürfen nicht freigeben, Owner schon")]
    public void Only_owner_approves()
    {
        using var f = new Fixture();
        var r = f.Core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Approve(r.Id, new Principal("u", Role.User)));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Deny(r.Id, new Principal("g", Role.Guest)));
        f.Core.Approvals.Approve(r.Id, new Principal("o", Role.Owner));
        f.Core.Integrations.Execute(r);
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "RBAC Core: Guest darf keine Aktion anfordern")]
    public void Guest_cannot_request()
    {
        using var f = new Fixture();
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.PlanOpenUrl(new Principal("g", Role.Guest), "https://example.org"));
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "rbac.denied");
    }

    [Theory(DisplayName = "RBAC Core: Rechtematrix")]
    [InlineData(Role.Guest, Permission.ViewStudios, true)] [InlineData(Role.Guest, Permission.ViewProjects, false)]
    [InlineData(Role.User, Permission.RequestAction, true)] [InlineData(Role.User, Permission.ApproveAction, false)]
    [InlineData(Role.User, Permission.ViewAudit, false)] [InlineData(Role.Owner, Permission.ApproveAction, true)]
    public void Matrix(Role r, Permission p, bool expected) => Assert.Equal(expected, Rbac.Has(new Principal("x", r), p));
}

public class Metrics_Core
{
    [Fact(DisplayName = "Systemmetriken: RAM/Disk echt messbar, CPU nach zweitem Messpunkt, Werte 0–100 oder null")]
    public void Metrics_plausible()
    {
        var m = new SystemMetrics();
        var a = m.Sample();
        Assert.Null(a.CpuPercent); // erster Messpunkt hat kein Intervall
        Thread.Sleep(300);
        var b = m.Sample();
        foreach (var v in new[] { b.CpuPercent, b.RamPercent, b.DiskPercent, b.GpuPercent })
            if (v is { } x) Assert.InRange(x, 0, 100);
        Assert.NotNull(b.RamPercent); Assert.NotNull(b.DiskPercent);
    }

    [Fact(DisplayName = "Direktstart: nur Owner, auditiert")]
    public void Direct_launch_owner_only()
    {
        using var f = new Fixture();
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(new Principal("u", Role.User), "x", f.Core.Audit));
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, "x", f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
    }
}

public class Dependencies_Core
{
    sealed class FakeRunner : IProcessRunner
    {
        public List<(string F, string A)> Calls = []; public Action? OnRun;
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        { Calls.Add((file, args)); OnRun?.Invoke(); line?.Invoke("ok"); return Task.FromResult((0, "ok")); }
    }
    static (DependencyManager M, FakeRunner R, HashSet<string> Files, Fixture F) Make()
    {
        var f = new Fixture(); var files = new HashSet<string>(); var r = new FakeRunner();
        var m = new DependencyManager(f.Core.Audit, r, p => p.Replace("%LOCALAPPDATA%", "L").Replace("%USERPROFILE%", "U").Replace("%ProgramFiles%", "P"), files.Contains)
        { ConsentFile = Path.Combine(f.Core.DataRoot, "c.txt"), };
        m.OllamaUp = () => Task.FromResult(true);
        return (m, r, files, f);
    }

    [Fact(DisplayName = "Katalog: jede Komponente hat Lizenz; jede Bundle-Manifest-Zeile ist abgedeckt; Unreal nur geführt")]
    public void Catalog_complete()
    {
        Assert.All(DependencyCatalog.All, c => Assert.False(string.IsNullOrWhiteSpace(c.License)));
        Assert.Equal(DeliveryMode.Guided, DependencyCatalog.Get("unreal").Mode);
        Assert.Null(DependencyCatalog.Get("unreal").WingetId);
        Assert.All(DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated), c => Assert.True(c.WingetId is not null || c.PostInstall is not null));
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "docs", "spec", "SOFTWARE_BUNDLE_MANIFEST.csv"))) dir = dir.Parent;
        var csv = File.ReadAllLines(Path.Combine(dir.FullName, "docs", "spec", "SOFTWARE_BUNDLE_MANIFEST.csv")).Skip(1)
            .Select(l => l.Split(',')[0].ToLowerInvariant()).ToList();
        foreach (var name in csv)
            Assert.Contains(DependencyCatalog.All, c => name.Split(' ').Any(w => w.Length >= 3 && c.Name.ToLowerInvariant().Contains(w)));
    }

    [Fact(DisplayName = "Installation ohne Zustimmung verweigert; nach Zustimmung winget --scope user; Erfolg nur bei echter Erkennung")]
    public async Task Install_flow()
    {
        var (m, r, files, f) = Make(); using var _ = f;
        var git = DependencyCatalog.Get("git");
        await Assert.ThrowsAsync<InvalidOperationException>(() => m.InstallAsync(Principal.LocalOwner, git));
        Assert.Throws<UnauthorizedAccessException>(() => m.GiveConsent(new Principal("u", Role.User)));
        m.GiveConsent(Principal.LocalOwner);
        Assert.False(await m.InstallAsync(Principal.LocalOwner, git)); // ExitCode 0, aber nicht erkannt → kein Erfolg
        Assert.Contains("--scope user", r.Calls[0].A); Assert.Contains("Git.Git", r.Calls[0].A);
        r.OnRun = () => files.Add(@"L\Programs\Git\cmd\git.exe");
        Assert.True(await m.InstallAsync(Principal.LocalOwner, git));
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "deps.install.ok");
        await Assert.ThrowsAsync<InvalidOperationException>(() => m.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("unreal")));
        var lo = DependencyCatalog.Get("libreoffice"); await m.InstallAsync(Principal.LocalOwner, lo);
        Assert.DoesNotContain("--scope user", r.Calls[^1].A);
    }

    [Fact(DisplayName = "Modell: Download über erkannte Ollama-Installation, erkannt über Manifest-Datei")]
    public async Task Model_flow()
    {
        var (m, r, files, f) = Make(); using var _ = f; m.GiveConsent(Principal.LocalOwner);
        var model = DependencyCatalog.Get("model");
        await Assert.ThrowsAsync<InvalidOperationException>(() => m.InstallAsync(Principal.LocalOwner, model));
        files.Add(@"L\Programs\Ollama\ollama.exe");
        r.OnRun = () => files.Add(Path.Combine("U", ".ollama", "models", "manifests", "registry.ollama.ai", "library", "qwen2.5", "0.5b"));
        Assert.True(await m.InstallAsync(Principal.LocalOwner, model));
        Assert.Equal("pull qwen2.5:0.5b", r.Calls[0].A);
    }
}

public class ToolCheck_Core
{
    sealed class R(int exit) : IProcessRunner
    { public Task<(int Exit, string Output)> RunAsync(string f, string a, Action<string>? l = null, CancellationToken c = default) => Task.FromResult((exit, "Blender 4.2.0\nmore")); }

    [Fact(DisplayName = "Prüfung: vorhandenes Programm wird gestartet (--version), integriert und nicht neu installiert; kaputt = Broken")]
    public async Task Check_working_and_broken()
    {
        using var f = new Fixture();
        var files = new HashSet<string> { @"P\Blender Foundation\Blender 4.2\blender.exe" };
        DependencyManager M(int exit) => new(f.Core.Audit, new R(exit), p => p.Replace("%ProgramFiles%", "P"), x => files.Contains(x) || x.Contains('*') && files.Any(y => y.StartsWith(x.Split('*')[0])))
        { ToolsFile = Path.Combine(f.Core.DataRoot, "tools.json") };
        var ok = await M(0).CheckAsync(DependencyCatalog.Get("blender"));
        Assert.Equal(ToolState.Working, ok.State); Assert.Equal("Blender 4.2.0", ok.Version);
        Assert.Contains("blender", File.ReadAllText(Path.Combine(f.Core.DataRoot, "tools.json")));
        var bad = await M(1).CheckAsync(DependencyCatalog.Get("blender"));
        Assert.Equal(ToolState.Broken, bad.State);
        var miss = await new DependencyManager(f.Core.Audit, new R(0), p => p, _ => false).CheckAsync(DependencyCatalog.Get("git"));
        Assert.Equal(ToolState.Missing, miss.State);
    }
}

public class DataLocation_And_Model
{
    [Fact(DisplayName = "Datenordner: Migration aus Programmordner, Velopack-Dateien bleiben, nichts wird überschrieben")]
    public void Migrate_legacy()
    {
        var t = Path.Combine(Path.GetTempPath(), "trendo-mig-" + Guid.NewGuid().ToString("N")); var old = Path.Combine(t, "local"); var neu = Path.Combine(t, "roaming");
        try
        {
            Directory.CreateDirectory(Path.Combine(old, "current")); Directory.CreateDirectory(Path.Combine(old, "projects", "p1")); Directory.CreateDirectory(Path.Combine(old, "audit"));
            File.WriteAllText(Path.Combine(old, "Update.exe"), "x"); File.WriteAllText(Path.Combine(old, "Trendo OS.exe"), "x"); File.WriteAllText(Path.Combine(old, "config.json"), "alt"); File.WriteAllText(Path.Combine(old, "audit", "audit.jsonl"), "a");
            Directory.CreateDirectory(neu); File.WriteAllText(Path.Combine(neu, "config.json"), "neu");
            Assert.Equal(2, DataLocation.MigrateLegacy(old, neu)); // projects + audit; config existiert schon
            Assert.True(Directory.Exists(Path.Combine(neu, "projects", "p1"))); Assert.True(File.Exists(Path.Combine(neu, "audit", "audit.jsonl")));
            Assert.Equal("neu", File.ReadAllText(Path.Combine(neu, "config.json")));
            Assert.True(Directory.Exists(Path.Combine(old, "current"))); Assert.True(File.Exists(Path.Combine(old, "Update.exe"))); Assert.True(File.Exists(Path.Combine(old, "Trendo OS.exe")));
            Assert.Equal(0, DataLocation.MigrateLegacy(old, neu));
        }
        finally { Directory.Delete(t, true); }
    }

    [Fact(DisplayName = "Datenordner liegt nicht im Velopack-Programmordner")]
    public void Default_separate() => Assert.NotEqual(Path.GetFullPath(DataLocation.Legacy()), Path.GetFullPath(DataLocation.Default()));

    [Fact(DisplayName = "KI-Modell: 3B Standard ab 16 GB RAM wenn vorhanden, sonst 0.5B; Auswahl wird gespeichert")]
    public void Model_choice()
    {
        using var f = new Fixture();
        const long G = 1024L * 1024 * 1024;
        Assert.Equal(DependencyCatalog.LargeModel, KiModel.Current(f.Core.DataRoot, 32 * G, _ => true));
        Assert.Equal(DependencyCatalog.LocalModel, KiModel.Current(f.Core.DataRoot, 8 * G, _ => true));
        Assert.Equal(DependencyCatalog.LocalModel, KiModel.Current(f.Core.DataRoot, 32 * G, m => m == DependencyCatalog.LocalModel));
        KiModel.Select(f.Core.DataRoot, DependencyCatalog.LocalModel, f.Core.Audit);
        Assert.Equal(DependencyCatalog.LocalModel, KiModel.Current(f.Core.DataRoot, 32 * G, _ => true));
        Assert.Throws<ArgumentException>(() => KiModel.Select(f.Core.DataRoot, "llama", f.Core.Audit));
    }
}
