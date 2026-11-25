using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0768 (Sammel 0.9.110): Kinderschutz aus T-0764 auch für die neuen Wege der Pakete T-0762 (GitHub-Start, Installer, Bauanleitung),
/// T-0763 (Programmdateien ersetzen, Quarantäne zurückholen) und T-0766 (Erweiterungen installieren, Werkstatt). Kind: abgelehnt mit „rbac.denied“,
/// nichts entsteht; Erwachsene: unverändert.</summary>
public class SammelT0768KinderschutzUi
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster", SetupDone = true }.Save(root);
        var l = new FakeLauncher();
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        Assert.Equal(child, !Rbac.Has(w.CurrentPrincipal, Permission.ManageSoftware));
        return (w, l, root);
    }

    static bool Denied(MainWindow w, string part) => w.Core.Audit.ReadAll().Any(a => a.Action == "rbac.denied" && a.Detail.Contains(part));

    [AvaloniaTheory(DisplayName = "T-0768 (T-0766 × T-0764): Erweiterung installieren und eigene Erweiterung bauen – Kind abgelehnt, nichts angelegt; Erwachsene unverändert")]
    [InlineData(true)]
    [InlineData(false)]
    public void Erweiterungen(bool child)
    {
        var (w, _, root) = Open(child);
        try
        {
            var zip = Path.Combine(root, "erweiterung.zip"); File.WriteAllBytes(zip, [0x50, 0x4B, 0x05, 0x06, .. new byte[18]]); // leeres ZIP
            w.ExtInstall(zip); Settle();
            Assert.Equal(child, Denied(w, "ext.install"));
            if (child)
            {
                Assert.Throws<UnauthorizedAccessException>(() => w.ExtWorkshopNew("Meine Erweiterung"));
                Assert.Null(w.ExtWorkshopDir);
                Assert.Empty(ExtensionWorkshop.List(w.Core.DataRoot));
                Assert.True(Denied(w, "ext.workshop"));
            }
            else
            {
                var dir = w.ExtWorkshopNew("Meine Erweiterung");
                Assert.True(Directory.Exists(dir));
                Assert.False(Denied(w, "ext.workshop"));
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory(DisplayName = "T-0768 (T-0762 × T-0764): GitHub-Programm starten – Kind: keine Prüfung, keine Start-Erlaubnis, nichts gestartet; Erwachsene: nicht abgelehnt")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GitHub_Start(bool child)
    {
        var (w, l, _) = Open(child);
        try
        {
            var tools = w.ToolsRootForTests; var id = GitHubApps.IdFor("beispiel/werkzeug");
            var dir = Path.Combine(tools, id); Directory.CreateDirectory(dir); var exe = Path.Combine(dir, "werkzeug.exe"); File.WriteAllBytes(exe, "MZ"u8.ToArray());
            var entry = GitHubApps.Register(Principal.LocalOwner, tools, "beispiel/werkzeug", new GitHubApps.Release("v1.0", "v1.0", []), new GitHubApps.Asset("werkzeug.zip", "https://github.com/beispiel/werkzeug", 2, null), "werkzeug.exe", new string('0', 64), "MIT", "Beispiel");
            w.GitHubSignatureOverride = _ => new Trendo.Core.Sys.SignatureInfo(Trendo.Core.Sys.SigState.Unsigniert, null, "nicht signiert");
            Assert.False(w.GitHubLaunch(entry)); await w.GitHubStartCheckTask; Settle();
            Assert.Empty(l.Opened);
            Assert.Equal(child, Denied(w, "start beispiel/werkzeug"));
            if (child) { Assert.Null(w.GitHubPendingStart); Assert.False(w.GitHubConfirmStart()); Assert.Null(GitHubApps.List(tools).Single().StartApproved); }
            else w.GitHubCancelStart(); // Erwachsene: das Tor prüft wie bisher (T0762_GitHubNachbau_Ui deckt den Ablauf ab)
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory(DisplayName = "T-0768 (T-0763 × T-0764): Selbstheilung – Programmdateien ersetzen und Datei aus der Quarantäne zurückholen nur Erwachsene")]
    [InlineData(true)]
    [InlineData(false)]
    public void Selbstheilung_zurueckholen(bool child)
    {
        var (w, _, root) = Open(child);
        try
        {
            var q = Path.Combine(root, "quarantaene", "20261125-120000"); Directory.CreateDirectory(q);
            var f = Path.Combine(q, "notiz.json"); File.WriteAllText(f, "{}");
            var item = new SelfHealer.QuarantineItem("20261125-120000", f, "notiz.json");
            w.HealRestore(item); Settle();
            Assert.Equal(child, Denied(w, "heal.restore"));
            Assert.Equal(child, File.Exists(f)); // Kind: bleibt in der Quarantäne
            Assert.Equal(!child, File.Exists(Path.Combine(root, "notiz.json")));
        }
        finally { w.Close(); }
    }

    [Fact(DisplayName = "T-0768 N5 (T-0767 × T-0764, echter Handy-Zugang): Kinderprofil – Upload von „spiel.exe“ ergibt 403 und nichts liegt im Eingang; ein Foto geht")]
    public async Task Handy_kind_keine_programmdatei()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-t0768-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        var ct = TestContext.Current.CancellationToken;
        var access = new PhoneAccess(d); var inbox = Path.Combine(d, "vom"); var audit = new AuditLog(Path.Combine(d, "audit.jsonl"));
        using var scope = Trendo.Core.Sys.ChildWrite.TestScope(d); // Testordner als freigegebener Ort – geprüft wird dann die Endung
        var port = FreePort();
        await using (var srv = await PhoneLinkServer.StartAsync(access, [System.Net.IPAddress.Loopback], port, inbox, Path.Combine(d, "zum"), audit,
            (_, _) => { }, (_, _) => { }, checkTarget: t => Trendo.Core.Sys.ChildWrite.WriteProblem(Principal.ForProfile(true), t, audit)))
        {
            using var phone = new HttpClient(new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() }) { BaseAddress = new Uri($"http://127.0.0.1:{srv.Port}") };
            var o = access.NewOffer();
            Assert.Equal(System.Net.HttpStatusCode.OK, (await phone.PostAsync($"/p/{o.Secret}", new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Kinderhandy" }), ct)).StatusCode);
            using (var f = new MultipartFormDataContent()) { f.Add(new ByteArrayContent([0x4D, 0x5A, 0, 0]), "f", "spiel.exe"); Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await phone.PostAsync("/api/upload", f, ct)).StatusCode); }
            Assert.Empty(Directory.Exists(inbox) ? Directory.GetFileSystemEntries(inbox) : []);
            Assert.Contains(audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("spiel.exe"));
            using (var f = new MultipartFormDataContent()) { f.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "f", "foto.jpg"); Assert.Equal(System.Net.HttpStatusCode.OK, (await phone.PostAsync("/api/upload", f, ct)).StatusCode); }
            Assert.True(File.Exists(Path.Combine(inbox, "foto.jpg")));
        }
        try { Directory.Delete(d, true); } catch (IOException) { }
    }

    static int FreePort() { var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); l.Start(); var p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
}
