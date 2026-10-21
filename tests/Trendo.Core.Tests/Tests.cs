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

    private static string Yaml(string mods)
    {
        var f = Path.Combine(Path.GetTempPath(), "trendo-req-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(f, "modules:\n" + mods); return f;
    }

    [Fact(DisplayName = "QA-015: Fehlendes Programm → Blockiert, auch wenn gebaut; echte Liste hat 54 Module")]
    public void Missing_tool_blocked()
    {
        var list = new StudioRegistry().Load(Req, new Probe());
        Assert.Equal(54, list.Count);
        Assert.Equal(Capability.Blocked, list.Single(s => s.Id == "CORE-05").Capability);
        Assert.All(list.Where(s => StudioRegistry.RequiredTools.ContainsKey(s.Id)), s => Assert.NotEqual(Capability.Ready, s.Capability));
    }

    [Fact(DisplayName = "QA-015: Status-Regeln – Bereit nur gebaut+getestet, In Arbeit, Nicht verbunden, Noch nicht gebaut")]
    public void Present_tool_not_connected()
    {
        var y = Yaml("- {id: CORE-05, name: A, approval_status: APPROVED_SPEC, implementation_status: NOT_STARTED, test_status: NOT_RUN}\n" +
                     "- {id: X-01, name: B, approval_status: APPROVED_SPEC, implementation_status: IMPLEMENTED, test_status: PC_VERIFIED}\n" +
                     "- {id: X-02, name: C, approval_status: APPROVED_SPEC, implementation_status: IMPLEMENTED, test_status: NOT_RUN}\n" +
                     "- {id: X-03, name: D, approval_status: APPROVED_SPEC, implementation_status: IN_PROGRESS, test_status: PC_VERIFIED}\n" +
                     "- {id: X-04, name: E, approval_status: APPROVED_SPEC, implementation_status: NOT_STARTED, test_status: NOT_RUN}\n");
        var l = new StudioRegistry().Load(y, new Probe("blender"));
        Assert.Equal(Capability.NotConnected, l.Single(s => s.Id == "CORE-05").Capability);
        Assert.Equal(Capability.Ready, l.Single(s => s.Id == "X-01").Capability);
        Assert.Equal(Capability.InProgress, l.Single(s => s.Id == "X-02").Capability);
        Assert.Equal(Capability.InProgress, l.Single(s => s.Id == "X-03").Capability);
        Assert.Equal(Capability.SpecOnly, l.Single(s => s.Id == "X-04").Capability);
        Assert.Equal("In Arbeit", StudioRegistry.GermanLabel(Capability.InProgress));
        Assert.Equal("Fertig & auf dem PC geprüft", Progress.GermanStatus(new ProgressItem("X", "X", "IMPLEMENTED", "PC_VERIFIED")));
        Assert.Equal("Fertig & getestet", Progress.GermanStatus(new ProgressItem("X", "X", "IMPLEMENTED", "TESTED_BOX")));
        File.Delete(y);
    }

    [Fact(DisplayName = "QA-015: SPEC-07 abgelehnt, übrige nur Spezifikation")]
    public void Rejected_and_spec_only()
    {
        var list = new StudioRegistry().Load(Req, new Probe("blender", "unreal", "libreoffice", "browser"));
        Assert.Equal(Capability.Rejected, list.Single(s => s.Id == "SPEC-07").Capability);
        Assert.Equal(Capability.InProgress, list.Single(s => s.Id == "CORE-01").Capability);
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
        Assert.All(DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated), c => Assert.True(c.WingetId is not null || c.PostInstall is not null || (c.PortableZip is not null && c.PortableSha256 is { Length: 64 })));
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

public class BuildPlan_Progress
{
    static string Repo([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));

    [Fact(DisplayName = "Bauplan: alle 53 genehmigten Module in Wellen, 55 BUS/LETTER-Teilanforderungen, Status live aus requirements.yaml")]
    public void Plan_complete()
    {
        var mods = Progress.Load(Path.Combine(Repo(), "requirements.yaml"), Path.Combine(Repo(), "docs", "buildplan.json"));
        Assert.Equal(65, mods.Count); Assert.Equal(6, mods.Count(m => m.Wave == "W0b")); Assert.Equal(6, mods.Count(m => m.Wave == "W0")); Assert.Equal(53, mods.Count(m => !m.Wave.StartsWith("W0")));
        Assert.Equal(55, mods.Sum(m => m.Subs.Count));
        Assert.DoesNotContain(mods, m => m.Module.Id == "SPEC-07");
        Assert.Equal("W1", mods.First(m => m.Module.Id == "STUDIO-LETTER-01").Wave);
        Assert.All(mods.Where(m => !m.Done), m => Assert.NotEqual("Fertig & getestet", Progress.GermanStatus(m.Module)));
    }
}

public class Wave0_Setup
{
    const long G = 1024L * 1024 * 1024;

    [Fact(DisplayName = "W0-01: Einstellungen werden gespeichert und geladen; unbekanntes Design abgelehnt")]
    public void Settings_roundtrip()
    {
        using var f = new Fixture();
        var s = new UserSettings { Name = "Manuel", Theme = "hell", StudioPacks = ["ki"], SetupDone = true };
        s.Save(f.Core.DataRoot);
        var l = UserSettings.Load(f.Core.DataRoot);
        Assert.Equal("Manuel", l.Name); Assert.Equal("hell", l.Theme); Assert.True(l.SetupDone); Assert.Equal(["ki"], l.StudioPacks);
        Assert.Throws<ArgumentException>(() => new UserSettings { Theme = "neon" }.Save(f.Core.DataRoot));
        File.WriteAllText(UserSettings.PathIn(f.Core.DataRoot), "{kaputt");
        Assert.False(UserSettings.Load(f.Core.DataRoot).SetupDone);
    }

    [Fact(DisplayName = "W0-02/03: Basispaket immer enthalten, Studio-Pakete nur wenn gewählt")]
    public void Packs()
    {
        var none = SetupPacks.ComponentsFor([]).Select(c => c.Id).ToList();
        Assert.Equal(["sumatra", "mpv", "7zip"], none);
        var ki = SetupPacks.ComponentsFor(["ki", "dev"]).Select(c => c.Id).ToList();
        Assert.Contains("ollama", ki); Assert.Contains("git", ki); Assert.DoesNotContain("libreoffice", ki);
        Assert.Equal(DeliveryMode.AutoUser, DependencyCatalog.Get("sumatra").Mode);
    }

    [Fact(DisplayName = "W0-04: KI-Modell nach Hardware (32 GB + RTX → 3B, 8 GB → 0.5B)")]
    public void Ki_by_hardware()
    {
        Assert.Equal(DependencyCatalog.LargeModel, KiAdvisor.Choose(new HardwareInfo(32 * G, "NVIDIA GeForce RTX 5070", 12 * G)).Model);
        Assert.Equal(DependencyCatalog.LargeModel, KiAdvisor.Choose(new HardwareInfo(16 * G, "GPU", 6 * G)).Model);
        Assert.Equal(DependencyCatalog.LocalModel, KiAdvisor.Choose(new HardwareInfo(16 * G, null, 0)).Model);
        Assert.Equal(DependencyCatalog.LocalModel, KiAdvisor.Choose(new HardwareInfo(8 * G, null, 0)).Model);
    }

    [Fact(DisplayName = "W0-05: Sicherung + Wiederherstellung (vorher Sicherheitskopie, Audit nie überschrieben, Rotation)")]
    public void Backup_restore()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var t = new DateTime(2026, 10, 21, 12, 0, 0);
        Assert.True(b.Due(t, 24));
        File.WriteAllText(Path.Combine(root, "notiz.txt"), "v1");
        var z = b.Create(t);
        Assert.False(b.Due(t.AddHours(1), 24));
        File.WriteAllText(Path.Combine(root, "notiz.txt"), "v2");
        var auditBefore = f.Core.Audit.ReadAll().Count;
        b.Restore(z, t.AddHours(2));
        Assert.Equal("v1", File.ReadAllText(Path.Combine(root, "notiz.txt")));
        Assert.True(f.Core.Audit.Verify()); Assert.True(f.Core.Audit.ReadAll().Count > auditBefore);
        Assert.Equal(2, b.List().Count);
        for (var i = 0; i < 12; i++) b.Create(t.AddDays(i + 1));
        Assert.Equal(10, b.List().Count);
    }

    [Fact(DisplayName = "W0b-03: Hintergrund nach Tageszeit")]
    public void Wallpaper_by_time()
    {
        Assert.Equal("morgen", UserSettings.WallpaperFor(new DateTime(2026, 1, 1, 7, 0, 0)));
        Assert.Equal("wellen", UserSettings.WallpaperFor(new DateTime(2026, 1, 1, 12, 0, 0)));
        Assert.Equal("bergsee-nacht", UserSettings.WallpaperFor(new DateTime(2026, 1, 1, 19, 0, 0)));
        Assert.Equal("polarlicht", UserSettings.WallpaperFor(new DateTime(2026, 1, 1, 23, 0, 0)));
        Assert.Equal("bergsee-nacht", new UserSettings().CurrentWallpaper(DateTime.Now));
    }
}

public class SYS13_ReleaseQuality
{
    [Xunit.Fact(DisplayName = "SYS-13: Absturzprotokoll schwärzt Benutzerpfade und behält höchstens 20 Dateien")]
    public void Crashlog_redacts_and_rotates()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-crash-" + Guid.NewGuid().ToString("N"));
        var t0 = new DateTime(2026, 10, 21, 12, 0, 0);
        for (var i = 0; i < 25; i++) Trendo.Core.CrashLog.Write(root, new Exception(@"Fehler in C:\Users\maxmuster\AppData\x und /home/maxmuster/y"), "Test", t0.AddSeconds(i));
        var files = Trendo.Core.CrashLog.List(root);
        Xunit.Assert.Equal(20, files.Count);
        var txt = File.ReadAllText(files[0]);
        Xunit.Assert.DoesNotContain("maxmuster", txt);
        Xunit.Assert.Contains(@"C:\Users\<benutzer>", txt);
        Xunit.Assert.EndsWith("absturz-20261021-120024-000.txt", files[0]);
    }

    [Xunit.Fact(DisplayName = "SYS-13: Release-Skript baut nur von sauberem main")]
    public void Release_script_requires_main()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "installer", "release.sh"))) dir = Path.GetDirectoryName(dir)!;
        var s = File.ReadAllText(Path.Combine(dir, "installer", "release.sh"));
        Xunit.Assert.Contains("\"main\" ]", s);
        Xunit.Assert.Contains("origin/main", s);
        Xunit.Assert.Contains("git status --porcelain", s);
        Xunit.Assert.Contains("--target \"$(git rev-parse HEAD)\"", s);
    }
}

public class CORE01_Search
{
    [Xunit.Fact(DisplayName = "CORE-01: Suche findet mit Umlauten, Wortanfang und mehreren Wörtern; sortiert nach Treffergüte")]
    public void Search_ranks()
    {
        var gs = new Trendo.Core.GlobalSearch();
        gs.Add("Seite", "Einstellungen", "page:settings", "design", "größe");
        gs.Add("Studio", "Briefstudio", "studio:L", "brief");
        gs.Add("Datei", "Kündigung-Fitnessstudio.docx", "/x/k.docx", "kuendigung", "fitnessstudio");
        Xunit.Assert.Equal("page:settings", gs.Find("groesse")[0].Target);
        Xunit.Assert.Equal("page:settings", gs.Find("Größe")[0].Target);
        Xunit.Assert.Equal("studio:L", gs.Find("brief")[0].Target);
        Xunit.Assert.Single(gs.Find("kündigung fitness"));
        Xunit.Assert.Empty(gs.Find("kündigung brief"));
        Xunit.Assert.Empty(gs.Find("   "));
    }

    [Xunit.Fact(DisplayName = "CORE-01: Dateisuche begrenzt Tiefe und Anzahl")]
    public void Files_bounded()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-gs-" + Guid.NewGuid().ToString("N"));
        var deep = Path.Combine(root, "a", "b", "c", "d"); Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(root, "rechnung-2026.pdf"), ""); File.WriteAllText(Path.Combine(deep, "versteckt.txt"), "");
        var gs = new Trendo.Core.GlobalSearch(); gs.AddFiles(root, maxDepth: 3);
        Xunit.Assert.Single(gs.Find("rechnung"));
        Xunit.Assert.Empty(gs.Find("versteckt"));
        Directory.Delete(root, true);
    }

    [Xunit.Fact(DisplayName = "SYS-12: Anzeigegröße wird auf erlaubte Stufen begrenzt")]
    public void Scale_clamped()
    {
        Xunit.Assert.Equal(1.3, Trendo.Core.UserSettings.ClampScale(5));
        Xunit.Assert.Equal(1.0, Trendo.Core.UserSettings.ClampScale(0.2));
        Xunit.Assert.Equal(1.15, Trendo.Core.UserSettings.ClampScale(1.16));
    }
}

public class SYS02_X09_Utilities
{
    [Xunit.Theory(DisplayName = "SYS-02: Rechner rechnet korrekt (Punkt vor Strich, Klammern, Komma, Prozent, Potenz)")]
    [Xunit.InlineData("2+3*4", "14")] [Xunit.InlineData("(12,5 + 7) × 3", "58,5")] [Xunit.InlineData("19 % × 250", "47,5")]
    [Xunit.InlineData("2^3^2", "512")] [Xunit.InlineData("-4 + 10 ÷ 4", "-1,5")] [Xunit.InlineData("1000000*3", "3.000.000")]
    public void Calc_ok(string e, string r) => Xunit.Assert.Equal(r, Trendo.Core.Calc.Format(Trendo.Core.Calc.Eval(e)));

    [Xunit.Theory(DisplayName = "SYS-02: Rechner lehnt Unsinn ab statt zu raten")]
    [Xunit.InlineData("2+")] [Xunit.InlineData("(1+2")] [Xunit.InlineData("abc")] [Xunit.InlineData("1/0")] [Xunit.InlineData("Process.Start()")]
    public void Calc_rejects(string e) => Xunit.Assert.ThrowsAny<Exception>(() => Trendo.Core.Calc.Eval(e));

    [Xunit.Fact(DisplayName = "SYS-02: Notizen werden gespeichert, geändert und gelöscht")]
    public void Notes_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-notes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var s = new Trendo.Core.NoteStore(root);
        var a = s.Add(" Milch kaufen "); s.Add("Steuer");
        Xunit.Assert.Equal(["Steuer", "Milch kaufen"], s.All().Select(n => n.Text));
        s.Update(a.Id, "Milch und Brot"); s.Delete(s.All()[0].Id);
        Xunit.Assert.Equal("Milch und Brot", Xunit.Assert.Single(new Trendo.Core.NoteStore(root).All()).Text);
        Directory.Delete(root, true);
    }

    [Xunit.Fact(DisplayName = "X-09: Prozesse werden nach Namen gruppiert, nach Speicher sortiert und Studios zugeordnet")]
    public void Resources_grouped()
    {
        var g = Trendo.Core.ResourceMonitor.Group([("chrome", 300L << 20), ("chrome", 200L << 20), ("ollama", 2L << 30), ("notepad", 10L << 20)], top: 2);
        Xunit.Assert.Equal(2, g.Count);
        Xunit.Assert.Equal(("ollama", "KI"), (g[0].Name, g[0].Studio));
        Xunit.Assert.Equal((2, 500L << 20), (g[1].Count, g[1].Bytes));
        Xunit.Assert.Equal("2,0 GB", Trendo.Core.ResourceMonitor.Size(2L << 30).Replace('.', ','));
    }

    [Xunit.Fact(DisplayName = "SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien")]
    public void Shelf_recent()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-shelf-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllText(Path.Combine(root, "alt.docx"), ""); File.SetLastWriteTime(Path.Combine(root, "alt.docx"), DateTime.Now.AddDays(-3));
        File.WriteAllText(Path.Combine(root, "sub", "neu.pdf"), ""); File.WriteAllText(Path.Combine(root, "~$alt.docx"), ""); File.WriteAllText(Path.Combine(root, "song.mp3"), "");
        var office = Trendo.Core.FileShelf.Recent([root], Trendo.Core.FileShelf.Office);
        Xunit.Assert.Equal(["neu.pdf", "alt.docx"], office.Select(f => f.Name));
        Xunit.Assert.Equal("Musik", Trendo.Core.FileShelf.Kind("x.MP3"));
        Directory.Delete(root, true);
    }
}

public class LETTER01_Studio
{
    static Trendo.Core.LetterDraft Good() => new()
    {
        SenderName = "Erika Muster", SenderStreet = "Hauptstraße 1", SenderCity = "10115 Berlin", Recipient = "Stadtwerke GmbH\nKundenservice\nAm Werk 5\n80331 München",
        Subject = "Kündigung meines Stromvertrags 4711", Body = "hiermit kündige ich den Vertrag zum 31.12.2026.", Place = "Berlin", Date = DateTime.Today,
    };

    [Xunit.Fact(DisplayName = "LETTER-018: vollständiger Brief ohne Pflichtfehler; fehlende Felder und Platzhalter blockieren")]
    public void Validate()
    {
        Xunit.Assert.DoesNotContain(Trendo.Core.Letters.Validate(Good()), i => i.Blocking);
        var d = Good(); Trendo.Core.Letters.ApplyTemplate(d, "widerspruch"); d.SenderName = ""; d.Recipient = "Amt";
        var issues = Trendo.Core.Letters.Validate(d);
        Xunit.Assert.Contains(issues, i => i.Field == "Absender" && i.Blocking);
        Xunit.Assert.Contains(issues, i => i.Message.Contains("[Datum]"));
        Xunit.Assert.Contains(issues, i => i.Field == "Empfänger" && i.Message.Contains("unvollständig"));
        var g = Good(); g.Closing = "Mit freundlichen Grüßen,"; g.Recipient = "A\nB\nC\nD\nE\nF\nG";
        Xunit.Assert.Contains(Trendo.Core.Letters.Validate(g), i => i.Field == "Gruß");
        Xunit.Assert.Contains(Trendo.Core.Letters.Validate(g), i => i.Message.Contains("6 Zeilen"));
    }

    [Xunit.Fact(DisplayName = "LETTER-002/004: Briefarten liefern Vorlagen; rechtliche Arten sind markiert")]
    public void Categories()
    {
        Xunit.Assert.True(Trendo.Core.Letters.Categories.Length >= 7);
        Xunit.Assert.True(Trendo.Core.Letters.Category("widerspruch").Legal);
        var d = new Trendo.Core.LetterDraft(); Trendo.Core.Letters.ApplyTemplate(d, "kuendigung");
        Xunit.Assert.StartsWith("Kündigung", d.Subject);
        Xunit.Assert.Equal("frei", Trendo.Core.Letters.Category("gibt-es-nicht").Id);
    }

    [Xunit.Fact(DisplayName = "LETTER-003/016/022: DOCX ist gültig (A4, Ränder DIN), enthält alle Teile; Archiv versioniert statt zu überschreiben")]
    public void Docx_and_archive()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-letter-" + Guid.NewGuid().ToString("N"));
        var d = Good(); d.Body = "Absatz eins & <Test>.\n\nAbsatz zwei."; d.Attachments = "Vertragskopie";
        var a = new Trendo.Core.LetterArchive(root);
        var (docx, json, v1) = a.Save(d); var (docx2, _, v2) = a.Save(d);
        Xunit.Assert.Equal((1, 2), (v1, v2)); Xunit.Assert.NotEqual(docx, docx2); Xunit.Assert.EndsWith("-v2.docx", docx2);
        Xunit.Assert.StartsWith($"{DateTime.Today:yyyy-MM-dd}-Kündigung-meines-Stromvertrags-4711", Path.GetFileName(docx));
        var txt = Trendo.Core.Letters.ReadDocxText(docx);
        foreach (var part in new[] { "Erika Muster · Hauptstraße 1 · 10115 Berlin", "80331 München", "Kündigung meines Stromvertrags 4711", "Absatz eins & <Test>.", "Absatz zwei.", "Mit freundlichen Grüßen", "Anlagen", "Vertragskopie", "Berlin, " })
            Xunit.Assert.Contains(part, txt);
        string xml;
        using (var z = System.IO.Compression.ZipFile.OpenRead(docx)) using (var sr = new StreamReader(z.GetEntry("word/document.xml")!.Open())) { xml = sr.ReadToEnd(); Xunit.Assert.NotNull(z.GetEntry("[Content_Types].xml")); }
        System.Xml.Linq.XDocument.Parse(xml);
        Xunit.Assert.Contains("w:w=\"11906\" w:h=\"16838\"", xml); Xunit.Assert.Contains("w:left=\"1418\"", xml);
        Xunit.Assert.Equal(d.Subject, Trendo.Core.LetterArchive.Load(docx)!.Subject);
        Directory.Delete(root, true);
    }
}

public class SYS03_Browser
{
    [Xunit.Theory(DisplayName = "SYS-03: Adressen werden sicher normalisiert (nur http/https, sonst Suche oder Ablehnung)")]
    [Xunit.InlineData("tagesschau.de", "https://tagesschau.de/")]
    [Xunit.InlineData("http://example.org/x", "http://example.org/x")]
    [Xunit.InlineData("wetter berlin", "https://duckduckgo.com/?q=wetter%20berlin")]
    [Xunit.InlineData("javascript:alert(1)", null)] [Xunit.InlineData("file:///C:/Windows", null)] [Xunit.InlineData("ftp://x.org", null)] [Xunit.InlineData("  ", null)]
    public void Normalize(string input, string? expected) => Xunit.Assert.Equal(expected, Trendo.Core.Browsers.Normalize(input)?.AbsoluteUri);

    [Xunit.Fact(DisplayName = "SYS-03: Browser werden über bekannte Pfade gefunden; riskante und unfertige Downloads erkannt")]
    public void Find_and_flags()
    {
        var found = Trendo.Core.Browsers.Find(p => p.Contains("chrome") || p.Contains("Firefox"), p => p.Replace("%ProgramFiles%", @"C:\PF").Replace("%ProgramFiles(x86)%", @"C:\PF86").Replace("%LOCALAPPDATA%", @"C:\L"));
        Xunit.Assert.Equal(["chrome", "firefox"], found.Select(b => b.Id));
        Xunit.Assert.True(Trendo.Core.Browsers.Risky("setup.EXE")); Xunit.Assert.False(Trendo.Core.Browsers.Risky("foto.jpg"));
        Xunit.Assert.True(Trendo.Core.Browsers.Unfinished("film.mp4.crdownload"));
    }
}

public class FriendlyStatusTests
{
    [Xunit.Fact(DisplayName = "Freundlicher Status: Bereit / Braucht ein Zusatzprogramm / Noch nicht verfügbar, teilweise gebaut ohne Hinweis")]
    public void Friendly()
    {
        Xunit.Assert.Equal("Bereit", Trendo.Core.StudioRegistry.FriendlyLabel(Trendo.Core.Capability.Ready));
        Xunit.Assert.Equal("Braucht ein Zusatzprogramm", Trendo.Core.StudioRegistry.FriendlyLabel(Trendo.Core.Capability.Blocked));
        Xunit.Assert.Equal("Noch nicht verfügbar", Trendo.Core.StudioRegistry.FriendlyLabel(Trendo.Core.Capability.SpecOnly));
        Xunit.Assert.Equal("Noch nicht verfügbar", Trendo.Core.StudioRegistry.FriendlyLabel(Trendo.Core.Capability.Rejected));
        Xunit.Assert.Null(Trendo.Core.StudioRegistry.FriendlyLabel(Trendo.Core.Capability.InProgress));
    }
}
