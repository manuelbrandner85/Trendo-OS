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
        var r = f.Core.Integrations.PlanOpenProjectFolder("projekt-a");
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Abgelehnte Freigabe blockiert")]
    public void Denied_blocks()
    {
        using var f = new Fixture();
        var r = f.Core.Integrations.PlanOpenUrl("https://example.org");
        f.Core.Approvals.Deny(r.Id);
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Freigabe ist einmalig (kein Replay)")]
    public void Approval_single_use()
    {
        using var f = new Fixture();
        var r = f.Core.Integrations.PlanOpenUrl("https://example.org");
        f.Core.Approvals.Approve(r.Id);
        f.Core.Integrations.Execute(r);
        Assert.Single(f.Launcher.Opened);
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Abgelaufene Freigabe blockiert")]
    public void Expired_blocks()
    {
        using var f = new Fixture();
        var r = f.Core.Integrations.PlanOpenUrl("https://example.org");
        f.Core.Approvals.Approve(r.Id);
        f.Time.Advance(TimeSpan.FromMinutes(11));
        Assert.Throws<ApprovalException>(() => f.Core.Integrations.Execute(r));
        Assert.Empty(f.Launcher.Opened);
    }

    [Fact(DisplayName = "QA-004: Freigabe gilt nicht für anderes Ziel")]
    public void Target_mismatch_blocks()
    {
        using var f = new Fixture();
        var r = f.Core.Approvals.Request(Integrations.OpenUrl, "x", "https://example.org/");
        f.Core.Approvals.Approve(r.Id);
        Assert.Throws<ApprovalException>(() => f.Core.Approvals.Execute(r.Id, Integrations.OpenUrl, "https://evil.example/", () => true));
    }

    [Theory(DisplayName = "QA-004: Nur https-URLs dürfen geplant werden")]
    [InlineData("http://example.org")] [InlineData("file:///C:/Windows")] [InlineData("javascript:alert(1)")] [InlineData("nicht-url")]
    public void Only_https(string url)
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Core.Integrations.PlanOpenUrl(url));
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
        Assert.Throws<OfflineException>(() => core.Integrations.PlanOpenUrl("https://example.org"));
        var r = core.Integrations.PlanOpenProjectFolder("off");
        core.Approvals.Approve(r.Id); core.Integrations.Execute(r);
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
