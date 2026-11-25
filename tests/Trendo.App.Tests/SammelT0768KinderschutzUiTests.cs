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
}
