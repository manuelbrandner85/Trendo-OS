using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764 Nachbesserung nach der Gegenprüfung (H1–H3, M2, M3, M4): weitere Wege, über die ein Kinderprofil installierte oder entfernte,
/// und Wege, die ohne Freigabe luden oder starteten. Jeder Test ruft den Weg selbst auf (nicht nur den Knopf).</summary>
public class RechteKernFix1Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) OpenChild()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        new UserSettings { Child = true, Name = "Kind" }.Save(root);
        var l = new FakeLauncher();
        var core = new TrendoCore(root, new InMemorySecretStore(), l, offline: false);
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.FreigabeConfirm = _ => Task.FromResult(true);
        w.Show(); Settle();
        return (w, l, root);
    }

    static void FakeLmStudio(MainWindow w, string root, List<string> calls)
    {
        w.LocalAppDataDir = Path.Combine(root, "local");
        var setup = Path.Combine(root, "LM-Studio-Setup.exe"); File.WriteAllText(setup, "MZ");
        w.KiDownloadOverride = (u, _) =>
        {
            calls.Add("download:" + (w.Core.Audit.ReadAll().Any(a => a.Action == "execution.start" && a.Detail.EndsWith("app.install ki-source:lmstudio")) ? "nach-freigabe" : "ohne-freigabe"));
            return Task.FromResult<(string, string?)>((setup, null));
        };
        w.KiInstallerRunOverride = _ => { calls.Add("installer"); var d = Path.Combine(w.LocalAppDataDir, "Programs", "LM Studio"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "LM Studio.exe"), "x"); return Task.CompletedTask; };
    }

    [AvaloniaFact(DisplayName = "T-0764 H1: KI-Quelle (LM Studio) – Kinderprofil lädt und startet nichts; Erwachsene laden erst nach Freigabe im Tor; „Nein“ im Dialog lädt nichts")]
    public async Task KiQuelle_Installer()
    {
        var (c, _, croot) = OpenChild();
        try
        {
            var calls = new List<string>(); FakeLmStudio(c, croot, calls);
            Assert.False(await c.KiSourceInstall("lmstudio"));
            Assert.Empty(calls);
            Assert.Contains(c.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
            c.Show("ki-quellen"); Settle();
            Assert.False(c.Find<Button>("src-install-lmstudio").IsEnabled);
        }
        finally { c.Close(); }

        var (w, _, root) = Ui.Open();
        try
        {
            var calls = new List<string>(); FakeLmStudio(w, root, calls);
            Assert.True(await w.KiSourceInstall("lmstudio")); Settle();
            Assert.Equal(["download:nach-freigabe", "installer"], calls);
            // „Aktualisieren“ ohne Karte: eigener Dialog – „Nein“ lädt nichts
            calls.Clear(); w.FreigabeConfirm = _ => Task.FromResult(false);
            Assert.False(await w.KiSourceInstall("lmstudio", karte: false));
            Assert.Empty(calls);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 H2: Kinderprofil startet keinen Deinstaller (KI-Quelle, GitHub-Installer) – Ablehnung vor dem Deinstaller, richtige Meldung")]
    public void Deinstaller_Kind()
    {
        var (w, _, root) = OpenChild();
        try
        {
            w.LocalAppDataDir = Path.Combine(root, "local");
            var d = Path.Combine(w.LocalAppDataDir, "Programs", "LM Studio"); Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d, "LM Studio.exe"), "x"); File.WriteAllText(Path.Combine(d, "Uninstall LM Studio.exe"), "kein echtes Programm");
            Assert.Throws<UnauthorizedAccessException>(() => w.KiSourceRemove("lmstudio"));
            Assert.Throws<UnauthorizedAccessException>(() => w.GitHubRunUninstaller(w.CurrentPrincipal, Path.Combine(d, "LM Studio.exe")));
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action is "github.uninstall.installer" or "ki.source.remove");
            Assert.True(File.Exists(Path.Combine(d, "LM Studio.exe")));
            // über die Seite: Meldung sagt, warum nichts passiert ist
            w.Show("ki-quellen"); Settle();
            w.Find<Button>("src-remove-lmstudio").Press(); Settle();
            w.Find<Button>("src-remove-lmstudio").Press(); Settle();
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Erwachsenen-Profil"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 H3: Claude Code einrichten – Kind: nichts; Erwachsene: erst Rückfrage und Freigabe im Tor, dann (Testmodus) Ausführung; „Abbrechen“ führt nichts aus")]
    public async Task Claude_einrichten()
    {
        var (c, _, _) = OpenChild();
        try
        {
            await c.ClaudeInstall();
            var log = c.Core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
            Assert.DoesNotContain(log, a => a.Action is "claude.testmodus" or "claude.install.start" or "execution.start");
        }
        finally { c.Close(); }

        var (w, _, _) = Ui.Open();
        try
        {
            w.FreigabeConfirm = _ => Task.FromResult(false);
            await w.ClaudeInstall();
            var log = w.Core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "approval.denied");
            Assert.DoesNotContain(log, a => a.Action is "claude.testmodus" or "execution.start");
            w.FreigabeConfirm = _ => Task.FromResult(true);
            await w.ClaudeInstall();
            log = w.Core.Audit.ReadAll();
            Assert.Equal("app.install", w.LastWirkung!.Action); Assert.Equal(MainWindow.ClaudeInstallTarget, w.LastWirkung.Target);
            var exec = log.ToList().FindIndex(a => a.Action == "execution.start" && a.Detail.EndsWith("app.install " + MainWindow.ClaudeInstallTarget));
            var test = log.ToList().FindIndex(a => a.Action == "claude.testmodus");
            Assert.True(exec >= 0 && test > exec, "Testmodus erst innerhalb der freigegebenen Ausführung");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 M3: Dateimanager – Kind öffnet keinen Installer/kein Skript, Dokumente schon; vor Testersatz geprüft")]
    public void Dateien_Installer()
    {
        var (w, _, root) = OpenChild();
        try
        {
            var opened = new List<string>(); w.FilesOpenOverride = opened.Add;
            var setup = Path.Combine(root, "setup.exe"); File.WriteAllText(setup, "MZ");
            var doc = Path.Combine(root, "brief.pdf"); File.WriteAllText(doc, "%PDF");
            w.FilesOpen(setup);
            Assert.Empty(opened);
            Assert.Contains("Erwachsenen-Profil", w.FilesMessage);
            w.FilesOpen(doc);
            Assert.Equal([doc], opened);
        }
        finally { w.Close(); }
    }

    sealed class SameDisk : IDiskResolver
    {
        public string FinalPath(string path) => Path.GetFullPath(path);
        public (int Type, int Number)? DiskOf(string root) => (7, 1); // alles auf einem Gerät
    }
    sealed class Ejector : IDriveEjector { public int Calls; public EjectResult Eject(string r) { Calls++; return new(true, "weg"); } }

    [AvaloniaFact(DisplayName = "T-0764 M4 UI: Stick, der auf demselben Gerät wie Trendos Daten liegt, wird nicht ausgeworfen (Zuordnung ersetzt)")]
    public async Task Auswurf_selbes_Geraet()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var ej = new Ejector();
            w.DriveSourceOverride = () => [new("E:\\", DriveType.Removable, "STICK", 16_000_000_000, 8_000_000_000, "FAT32")];
            w.EjectorOverride = ej; w.DiskResolverOverride = new SameDisk();
            var r = await w.EjectDriveAsync("E:\\");
            Assert.False(r.Ok); Assert.Contains("demselben Gerät", r.Message);
            Assert.Equal(0, ej.Calls);
        }
        finally { w.Close(); }
    }

    static (string Data, string Vp, string Wz) CliFixture(string root, bool child)
    {
        var data = Path.Combine(root, "daten"); var vp = Path.Combine(root, "vollpaket"); var wz = Path.Combine(root, "werkzeuge");
        Directory.CreateDirectory(data); Directory.CreateDirectory(vp); Directory.CreateDirectory(wz);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster", SetupDone = true }.Save(data);
        var zip = Path.Combine(vp, "testtool.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var s = new StreamWriter(z.CreateEntry("testtool.exe").Open())) s.Write("MZ-Test");
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        File.WriteAllText(Path.Combine(vp, "manifest.json"), JsonSerializer.Serialize(new { programs = new[] { new { id = "testtool", name = "Testwerkzeug", version = "1", license = "MIT", file = "testtool.zip", sha256 = sha, bytes = new FileInfo(zip).Length, exe = "testtool/testtool.exe", source_url = (string?)null } } }));
        return (data, vp, wz);
    }

    static (int Exit, string Out) RunCli(params string[] args)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "TrendoOS.exe" : "TrendoOS");
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEnd(); p.WaitForExit(120_000);
        return (p.ExitCode, o);
    }

    [Fact(DisplayName = "T-0764 M2 echt: Kommandozeile --install-deps im Datenordner eines Kinderprofils bricht ab (Rückgabewert 4, nichts eingerichtet); Erwachsene richten ein")]
    public void Kommandozeile_Kind()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-cli-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (data, vp, wz) = CliFixture(Path.Combine(root, "kind"), child: true);
            var (exit, o) = RunCli("--data-root", data, "--install-deps", "vollpaket", "--vollpaket", vp, "--werkzeuge", wz, "--offline");
            Assert.Equal(4, exit);
            Assert.Contains("Erwachsenen-Profil", o);
            Assert.False(Directory.Exists(Path.Combine(wz, "testtool")));
            var (data2, vp2, wz2) = CliFixture(Path.Combine(root, "erwachsen"), child: false);
            var (exit2, o2) = RunCli("--data-root", data2, "--install-deps", "vollpaket", "--vollpaket", vp2, "--werkzeuge", wz2, "--offline");
            Assert.True(exit2 == 0, o2);
            Assert.True(File.Exists(Path.Combine(wz2, "testtool", "testtool.exe")));
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0764 Nachsuche: n8n/Node-RED, FreeSewing, axe-core (npm), Claude-Plugins/MCP und „Trendo vom PC entfernen“ – Kinderprofil: nichts, vor dem Testmodus geprüft")]
    public async Task Weitere_Wege_Kind()
    {
        var (w, _, _) = OpenChild();
        var conn = 0; var prev = MainWindow.ConnRunOverride;
        try
        {
            var npm = 0; w.AutomationInstallOverride = (_, _) => { npm++; return Task.FromResult(true); };
            MainWindow.ConnRunOverride = (_, args, _, _, _) => { if (args.Contains("plugin")) conn++; return Task.FromResult((0, "")); }; // Seite „Verbindungen“ fragt beim Zeichnen gh ab – gezählt wird nur der Plugin-Aufruf
            var pc = 0; w.UninstallPcOverride = () => pc++;
            await w.AutomationInstall("n8n");
            await w.SewingSetup();
            await w.WebAxeSetup();
            await w.ClaudeExtRun(["plugin", "install", "x"], "fertig");
            w.StickOnlyFinish();
            Assert.Equal(0, npm); Assert.Equal(0, conn); Assert.Equal(0, pc);
            Assert.Equal(5, w.Core.Audit.ReadAll().Count(a => a.Action == "rbac.denied" && a.Actor == "kinderprofil"));
            Assert.Equal(Rbac.NurErwachseneSoftware, w.SewingMessage);
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action == "setup.nur-stick");
        }
        finally { MainWindow.ConnRunOverride = prev; w.Close(); }
    }
}
