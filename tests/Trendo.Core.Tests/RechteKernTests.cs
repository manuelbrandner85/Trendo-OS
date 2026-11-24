using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764 (Restliste Rang 41 Teil 2, Audit SYS-11/SYS-12 „Rechte und Freigaben“): Die Rolle der angemeldeten Person reicht bis in den Kern.
/// Ein Kinderprofil (<see cref="Principal.ChildProfile"/>) installiert, aktualisiert und entfernt über keinen Weg etwas und erteilt keine Freigabe –
/// geprüft am Kern selbst, nicht am Knopf. Jeder Weg lehnt ab, BEVOR er etwas lädt, entpackt, startet oder löscht.</summary>
public class RechteKern
{
    static readonly Principal Kind = Principal.ChildProfile;
    static readonly Principal Erwachsen = Principal.LocalOwner;

    static (string Zip, string Sha) Zip(string dir, string entry = "werkzeug.exe")
    {
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, "paket.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var s = new StreamWriter(z.CreateEntry(entry).Open())) s.Write("MZ-Testprogramm");
        return (zip, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip))));
    }

    static void Verweigert(Fixture f, Action a)
    {
        var e = Assert.Throws<UnauthorizedAccessException>(a);
        Assert.Contains("Erwachsenen", e.Message);
        Assert.Contains(f.Core.Audit.ReadAll(), x => x.Action == "rbac.denied" && x.Actor == Kind.Name);
    }

    [Fact(DisplayName = "T-0764 Kern: Recht „Programme verwalten“ hat nur die Rolle Besitzer – Profil-Rolle Kind = Benutzer")]
    public void Rollen()
    {
        Assert.True(Rbac.Has(Erwachsen, Permission.ManageSoftware));
        Assert.False(Rbac.Has(Kind, Permission.ManageSoftware));
        Assert.False(Rbac.Has(new Principal("gast", Role.Guest), Permission.ManageSoftware));
        Assert.Equal(Kind, Principal.ForProfile(child: true));
        Assert.Equal(Erwachsen, Principal.ForProfile(child: false));
    }

    [Fact(DisplayName = "T-0764 Kern: Portable entpacken – Kinderprofil abgelehnt (nichts entpackt), Erwachsene entpacken; Name „..“ löscht nie den Oberordner")]
    public void Portable()
    {
        using var f = new Fixture();
        var (zip, sha) = Zip(Path.Combine(f.Root, "quelle"));
        var tools = Path.Combine(f.Root, "a", "werkzeuge"); Directory.CreateDirectory(tools);
        Verweigert(f, () => Software.InstallPortable(Kind, zip, sha, tools, "werkzeug", f.Core.Audit));
        Assert.False(Directory.Exists(Path.Combine(tools, "werkzeug")));
        Assert.True(File.Exists(Path.Combine(Software.InstallPortable(Erwachsen, zip, sha, tools, "werkzeug", f.Core.Audit), "werkzeug.exe")));
        // vorher: „..“ ergab den Oberordner von „werkzeuge“ – „Ersetzen“ löschte ihn samt Inhalt
        var keep = Path.Combine(f.Root, "a", "bleibt.txt"); File.WriteAllText(keep, "x");
        foreach (var bad in new[] { "..", ".", "...", ".versteckt", "/" })
            Assert.Throws<ArgumentException>(() => Software.InstallPortable(Erwachsen, zip, sha, tools, bad, f.Core.Audit));
        Assert.True(File.Exists(keep));
        Assert.True(Directory.Exists(Path.Combine(tools, "werkzeug")));
    }

    [Fact(DisplayName = "T-0764 Kern: Offline-Installer – Kinderprofil startet nichts; Erwachsene nur mit stimmender Prüfsumme (auch nach Austausch der Datei)")]
    public void Offline_Installer()
    {
        using var f = new Fixture();
        var dir = Path.Combine(f.Root, "offline"); Directory.CreateDirectory(dir);
        var exe = Path.Combine(dir, "setup.exe"); File.WriteAllText(exe, "MZ-Installer");
        File.WriteAllText(exe + ".sha256", Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(exe))));
        Verweigert(f, () => Software.StartOfflineInstaller(Kind, exe, f.Core.Integrations, f.Core.Audit));
        Assert.Empty(f.Launcher.Opened);
        Software.StartOfflineInstaller(Erwachsen, exe, f.Core.Integrations, f.Core.Audit);
        Assert.Single(f.Launcher.Opened);
        File.WriteAllText(exe, "MZ-ausgetauscht"); // zwischen „Prüfen“ und „Ja, starten“ verändert
        Assert.Throws<InvalidOperationException>(() => Software.StartOfflineInstaller(Erwachsen, exe, f.Core.Integrations, f.Core.Audit));
        Assert.Single(f.Launcher.Opened);
    }

    [Fact(DisplayName = "T-0764 Kern: Vollpaket – Kinderprofil richtet kein Paket ein und entfernt keins")]
    public void Vollpaket_Wege()
    {
        using var f = new Fixture();
        var tools = Path.Combine(f.Root, "werkzeuge");
        var pkgDir = Path.Combine(tools, "7zip"); Directory.CreateDirectory(pkgDir); File.WriteAllText(Path.Combine(pkgDir, "7z.exe"), "MZ");
        Verweigert(f, () => Vollpaket.InstallEach(Kind, Path.Combine(f.Root, "gibt-es-nicht"), tools, f.Core.Audit));
        Verweigert(f, () => Vollpaket.InstallMissing(Kind, Path.Combine(f.Root, "gibt-es-nicht"), tools, f.Core.Audit));
        Verweigert(f, () => Vollpaket.InstallPackage(Kind, Path.Combine(f.Root, "gibt-es-nicht"), new Vollpaket.Entry("7zip", "7-Zip", "1", "LGPL-2.1", "7zip.zip", new string('0', 64), 1, "7zip/7z.exe", null), tools, f.Core.Audit));
        Verweigert(f, () => Vollpaket.Uninstall(Kind, tools, "7zip", f.Core.Audit));
        Assert.True(File.Exists(Path.Combine(pkgDir, "7z.exe")));
        Assert.True(Vollpaket.Uninstall(Erwachsen, tools, "7zip", f.Core.Audit));
        Assert.False(Directory.Exists(pkgDir));
    }

    [Fact(DisplayName = "T-0764 Kern: GitHub-Programme, Quellcode-Programme, KI-Laufzeiten/-Modelle und Apps – Kinderprofil abgelehnt, nichts geändert")]
    public void Weitere_Wege()
    {
        using var f = new Fixture();
        var tools = Path.Combine(f.Root, "werkzeuge"); Directory.CreateDirectory(tools);
        var (zip, sha) = Zip(Path.Combine(f.Root, "quelle"), "tool.exe");
        var rel = new GitHubApps.Release("v1", "", []);
        var asset = new GitHubApps.Asset("tool-windows-x64.zip", "https://github.com/o/tool/x", new FileInfo(zip).Length, sha);
        Verweigert(f, () => GitHubApps.Install(Kind, tools, "o/tool", rel, asset, zip, "MIT", "", f.Core.Audit));
        Assert.Empty(GitHubApps.List(tools));
        var gh = GitHubApps.Install(Erwachsen, tools, "o/tool", rel, asset, zip, "MIT", "", f.Core.Audit);
        Verweigert(f, () => GitHubApps.Uninstall(Kind, tools, gh.Id, f.Core.Audit));
        Assert.Single(GitHubApps.List(tools));

        Verweigert(f, () => SourceApps.BeginInstall(Kind, "o/quelle", "o/quelle MIT", f.Core.Audit));
        var plan = SourceApps.PackagePlan("pypi", "werkzeug-cli");
        Verweigert(f, () => SourceApps.Register(Kind, tools, "o/quelle", "pypi:werkzeug-cli", plan, "MIT", "", f.Core.Audit));
        Assert.Empty(SourceApps.List(tools));
        var src = SourceApps.Register(Erwachsen, tools, "o/quelle", "pypi:werkzeug-cli", plan, "MIT", "", f.Core.Audit);
        Verweigert(f, () => SourceApps.Remove(Kind, tools, src.Id, f.Core.Audit));
        Assert.Single(SourceApps.List(tools));

        var store = new KiLibraryStore(tools);
        var rt = KiLibrary.Runtimes.First(r => !r.FromVollpaket);
        Verweigert(f, () => store.InstallRuntime(Kind, rt.Id, zip, f.Core.Audit));
        Assert.False(store.RuntimeReady(rt.Id));
        var m = KiLibrary.Models.First(x => x.Files.Count > 0);
        var file = Path.Combine(f.Root, "modell.bin"); File.WriteAllBytes(file, new byte[m.Files[0].Bytes > 0 && m.Files[0].Bytes < 1 << 20 ? m.Files[0].Bytes : 10]);
        Verweigert(f, () => store.InstallModelFile(Kind, m, m.Files[0], file, null, f.Core.Audit));
        Assert.True(File.Exists(file)); // abgelehnt vor jeder Prüfung – nicht einmal verworfen
        Assert.False(File.Exists(store.ModelFile(m, m.Files[0])));

        var app = WebApps.Catalog[0].Id;
        Verweigert(f, () => WebApps.Install(f.Root, app, f.Core.Audit, Kind));
        Assert.False(WebApps.IsInstalled(f.Root, app));
        WebApps.Install(f.Root, app, f.Core.Audit, Erwachsen);
        Verweigert(f, () => WebApps.Uninstall(f.Root, app, f.Core.Audit, Kind));
        Assert.True(WebApps.IsInstalled(f.Root, app));
    }

    [Fact(DisplayName = "T-0764 Kern: winget (App-Center) – Kinderprofil führt nichts aus, auch nicht mit einer Freigabe eines Erwachsenen")]
    public async Task AppCenter_Kind()
    {
        using var f = new Fixture();
        var run = new FakeRunner((_, a) => (0, a.StartsWith("search") ? SYS04_AppCenter.WingetSearch : "ok"));
        var c = new AppCenter(new WingetBackend(run), f.Core.Approvals, f.Core.Audit);
        var vlc = (await c.Backend.SearchAsync("vlc"))[0];
        // Kind fragt an – freigeben kann es nicht
        var own = c.Plan("install", vlc, Kind);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Approve(own.Id, Kind));
        // Freigabe eines Erwachsenen (z. B. auf der Seite „Freigaben“), ausgeführt im Kinderprofil: abgelehnt, winget läuft nicht
        var r = c.Plan("install", vlc, Kind); f.Core.Approvals.Approve(r.Id, Erwachsen);
        var calls = run.Calls.Count;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.ExecuteAsync(r, Kind));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.RunAsync(r, Kind, Path.Combine(f.Root, "log")));
        Assert.Equal(calls, run.Calls.Count);
        Assert.False(Directory.Exists(Path.Combine(f.Root, "log")));
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), a => a.Action == "execution.start" && a.Detail.StartsWith(r.Id));
        // Erwachsene: dieselbe Freigabe wirkt
        Assert.True((await c.ExecuteAsync(r, Erwachsen)).Ok);
    }

    [Fact(DisplayName = "T-0764 Kern: Zusatzprogramme (DependencyManager) – Kinderprofil installiert nichts, Recht „Programme verwalten“ im Protokoll")]
    public async Task Zusatzprogramme_Kind()
    {
        using var f = new Fixture();
        var run = new FakeRunner((_, _) => (0, "ok"));
        var d = new DependencyManager(f.Core.Audit, run, p => p, _ => false) { ConsentFile = Path.Combine(f.Root, "zustimmung.txt") };
        d.GiveConsent(Erwachsen);
        var c = DependencyCatalog.All.First(x => x.Mode == DeliveryMode.AutoUser && x.WingetId is not null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => d.InstallAsync(Kind, c));
        Assert.Empty(run.Calls);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("ManageSoftware") && a.Detail.Contains(c.Id));
        Assert.Throws<UnauthorizedAccessException>(() => d.GiveConsent(Kind));
    }

    [Fact(DisplayName = "T-0764 Kern: Kinderprofil erteilt keine Freigabe – Tor, Brief-Freigabe, Plugin-Freigabe, Not-Stopp fortsetzen")]
    public void Kind_gibt_nichts_frei()
    {
        using var f = new Fixture();
        var r = f.Core.Approvals.Request(Kind, "deps.install", "ffmpeg einrichten", "ffmpeg");
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Approve(r.Id, Kind));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Deny(r.Id, Kind));
        Assert.Throws<ApprovalException>(() => f.Core.Approvals.Execute(r.Id, "deps.install", "ffmpeg", () => true));
        f.Core.Approvals.Halt(Kind, "Test"); // stoppen darf jeder
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Resume(Kind));
        f.Core.Approvals.Resume(Erwachsen);
        Assert.Throws<UnauthorizedAccessException>(() => LetterGate.Approve(new LetterCase(), Path.Combine(f.Root, "brief.pdf"), Kind, f.Core.Audit));
        var plugins = new PluginHost.Approvals(f.Root);
        var res = new PluginInspect.Result(Path.Combine(f.Root, "p.vst3"), Path.Combine(f.Root, "p.dll"), "VST3", "x64", new string('a', 64), 1000, false, true, [], [], null);
        Assert.Throws<UnauthorizedAccessException>(() => plugins.Approve(res, "offizielle Seite des Herstellers", Kind));
        Assert.Empty(plugins.All());
    }
}
