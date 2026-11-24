using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 2. Prüfrunde (N-A … N-G). Startet nie ein echtes fremdes Programm: Starten über den FakeLauncher, Downloads/Schritte/npm/Updater ersetzt;
/// nur die Kommandozeilen-Probe startet Trendo selbst (mit einem lokalen Testpaket).</summary>
public class RechteKernFix2Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var l = new FakeLauncher();
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, l, root);
    }

    static (KiLibraryModel M, KiPyApp App) Docs() =>
        ((KiLibraryModel)typeof(MainWindow).GetField("DocsModel", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!, KiPyApps.For(KiRuntime.Dokumente)!); // Modell der Seite „Wissen → Dokumente einlesen“

    [AvaloniaFact(DisplayName = "T-0764 N-A: Python-KI aus einem Studio – Kind: nichts geladen, verständliche Meldung; Erwachsene: erst Rückfrage, Laden nur im Tor")]
    public async Task PythonKi()
    {
        var (c, _, _) = Open(true);
        try
        {
            var calls = new List<string>();
            c.KiDownloadOverride = (_, _) => { calls.Add("download"); throw new IOException("Ersatz: kein Download"); };
            c.KiStepOverride = (_, _, _, _) => { calls.Add("schritt"); return Task.FromResult(new Cli.Result(1, "", "Ersatz")); };
            var (m, app) = Docs();
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => c.KiPyInstall(m, app));
            Assert.Contains("Erwachsenen-Profil", e.Message);
            // über das Studio (Wissen → Dokumente einlesen): kein Absturz, nichts geladen
            await (Task)typeof(MainWindow).GetMethod("WissenDocsSetup", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(c, null)!;
            Assert.Empty(calls);
            Assert.Contains(c.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
        }
        finally { c.Close(); }

        var (w, _, _) = Open(false);
        try
        {
            var calls = new List<string>();
            w.KiDownloadOverride = (_, _) => { calls.Add(w.Core.Audit.ReadAll().Any(a => a.Action == "execution.start" && a.Detail.Contains(MainWindow.KiPyAction)) ? "download-im-tor" : "download-ohne-tor"); throw new IOException("Ersatz: kein Download"); };
            w.KiStepOverride = (_, _, _, _) => { calls.Add("schritt"); return Task.FromResult(new Cli.Result(1, "", "Ersatz")); };
            var (m, app) = Docs();
            w.FreigabeConfirm = _ => Task.FromResult(false);
            await Assert.ThrowsAsync<InvalidOperationException>(() => w.KiPyInstall(m, app));
            Assert.Empty(calls);
            w.FreigabeConfirm = _ => Task.FromResult(true);
            await Assert.ThrowsAnyAsync<Exception>(() => w.KiPyInstall(m, app)); // der ersetzte Download schlägt danach fehl
            Assert.Equal("download-im-tor", Assert.Single(calls));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 N-C/N-D: Starten zentral geprüft – Kind startet keine fremde .exe, Verknüpfung, .hta/.cpl … (Suche, Downloads, Dateimanager); Dokumente und Trendo-eigene Programme ja")]
    public async Task Starten_zentral()
    {
        var (c, l, root) = Open(true);
        try
        {
            string F(string n) { var f = Path.Combine(root, "ablage", n); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, "x"); return f; }
            foreach (var n in new[] { "vlc-3.0.21-win64.exe", "7z2408-x64.exe", "report.pdf.exe", "Spiel.lnk", "x.url", "y.cpl", "z.hta", "a.application", "b.msu", "c.jar", "d.diagcab", "e.settingcontent-ms", "setup.msi" })
                await c.OpenHit(new SearchHit("datei", n, F(n), 1));
            Assert.Empty(l.Opened);
            Assert.Contains("Erwachsenen-Profil", c.LastLaunchRefused);
            // Dokument: erlaubt; Trendo-eigenes Programm im Werkzeugordner: erlaubt
            var doc = F("brief.pdf"); await c.OpenHit(new SearchHit("datei", "brief", doc, 1));
            var own = Path.Combine(root, "werkzeuge", "notiz", "notiz.exe"); Directory.CreateDirectory(Path.GetDirectoryName(own)!); File.WriteAllText(own, "MZ");
            await c.OpenHit(new SearchHit("datei", "notiz", own, 1));
            Assert.Equal([doc, own], l.Opened);
            // Dateimanager: dieselbe Regel vor dem Testersatz
            var opened = new List<string>(); c.FilesOpenOverride = opened.Add;
            c.FilesOpen(F("vlc-3.0.21-win64.exe")); c.FilesOpen(F("Spiel.lnk")); c.FilesOpen(doc);
            Assert.Equal([doc], opened);
            Assert.Contains("Erwachsenen-Profil", c.FilesMessage + c.LastLaunchRefused);
        }
        finally { c.Close(); }

        var (w, wl, wroot) = Open(false);
        try
        {
            var exe = Path.Combine(wroot, "vlc-3.0.21-win64.exe"); File.WriteAllText(exe, "MZ");
            await w.OpenHit(new SearchHit("datei", "vlc", exe, 1));
            Assert.Equal([exe], wl.Opened); // Erwachsene: wie bisher (FakeLauncher, nichts wird wirklich gestartet)
        }
        finally { w.Close(); }
    }

    sealed class FakeUpdater : ITrendoUpdater
    {
        public int Rollbacks;
        public bool IsInstalled => true;
        public string Current => "0.9.109";
        public Task<(string Version, string? Notes)?> CheckAsync() => Task.FromResult<(string, string?)?>(null);
        public Task DownloadAsync(Action<int> progress, CancellationToken ct) => Task.CompletedTask;
        public void ApplyAndRestart() { }
        public void ApplyOnExit() { }
        public bool BackupCurrent(string dir) => true;
        public bool RollbackTo(string dir, string version) { Rollbacks++; return true; }
    }

    [AvaloniaFact(DisplayName = "T-0764 N-E: „Zurück zu Version …“ – Kind: abgelehnt, Updater nicht gefragt; Erwachsene: ja; KI-Brücke fragt bei upd-*")]
    public void Rueckkehr()
    {
        var (c, _, _) = Open(true);
        try
        {
            var u = new FakeUpdater(); c.Updater = u;
            Assert.False(c.RollbackNow("0.9.108"));
            Assert.Equal(0, u.Rollbacks);
            Assert.Contains(c.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("trendo.rollback"));
        }
        finally { c.Close(); }
        var (w, _, _) = Open(false);
        try { var u = new FakeUpdater(); w.Updater = u; Assert.True(w.RollbackNow("0.9.108")); Assert.Equal(1, u.Rollbacks); }
        finally { w.Close(); }
        Assert.True(ClaudeActions.NeedsConsent("Zurück zu Version 0.9.108", "upd-rollback", true));
        Assert.True(ClaudeActions.NeedsConsent("Version 0.9.110 jetzt installieren", "upd-install", true));
    }

    [AvaloniaFact(DisplayName = "T-0764 N-G: Erwachsene – n8n erst nach eigener Rückfrage und nur im Freigabe-Tor eingerichtet")]
    public async Task N8n_Zwei_Schritte()
    {
        var (w, _, _) = Open(false);
        try
        {
            var calls = new List<string>();
            w.AutomationInstallOverride = (_, _) => { calls.Add(w.Core.Audit.ReadAll().Any(a => a.Action == "execution.start" && a.Detail.EndsWith("deps.npm n8n")) ? "im-tor" : "ohne-tor"); return Task.FromResult(true); };
            w.FreigabeConfirm = _ => Task.FromResult(false);
            await w.AutomationInstall("n8n");
            Assert.Empty(calls);
            w.FreigabeConfirm = _ => Task.FromResult(true);
            await w.AutomationInstall("n8n");
            Assert.Equal(["im-tor"], calls);
        }
        finally { w.Close(); }
    }

    // ---------- N-B: Kommandozeile auf einem Familien-PC ----------
    static (string Data, string Vp, string Wz) Familie(string root, string? pin)
    {
        var data = Path.Combine(root, "daten"); var vp = Path.Combine(root, "vollpaket"); var wz = Path.Combine(root, "werkzeuge");
        Directory.CreateDirectory(vp); Directory.CreateDirectory(wz); Directory.CreateDirectory(data);
        new UserSettings { Child = false, Name = "Erika Muster", SetupDone = true }.Save(data);
        new PersonStore(data).Add("Kind", "", child: true);
        if (pin is not null) new ProfileLock(data).Set(ProfileLock.Kind.Pin, pin);
        var zip = Path.Combine(vp, "testtool.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var s = new StreamWriter(z.CreateEntry("testtool.exe").Open())) s.Write("MZ-Test");
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        File.WriteAllText(Path.Combine(vp, "manifest.json"), JsonSerializer.Serialize(new { programs = new[] { new { id = "testtool", name = "Testwerkzeug", version = "1", license = "MIT", file = "testtool.zip", sha256 = sha, bytes = new FileInfo(zip).Length, exe = "testtool/testtool.exe", source_url = (string?)null } } }));
        return (data, vp, wz);
    }

    static (int Exit, string Out) Cli(params string[] args)
    {
        var psi = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "TrendoOS.exe" : "TrendoOS")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEnd(); p.WaitForExit(120_000);
        return (p.ExitCode, o);
    }

    [Fact(DisplayName = "T-0764 N-B echt: Familien-PC – Kommandozeile installiert nur mit richtiger PIN des Profils (ohne/mit falscher PIN Exit 4, PIN nie in der Ausgabe)")]
    public void Kommandozeile_Familie()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-cli-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (d1, v1, w1) = Familie(Path.Combine(root, "ohne-sperre"), null);
            var r = Cli("--data-root", d1, "--install-deps", "vollpaket", "--vollpaket", v1, "--werkzeuge", w1, "--offline");
            Assert.Equal(4, r.Exit); Assert.Contains("mehrere Personen", r.Out); Assert.False(Directory.Exists(Path.Combine(w1, "testtool")));

            var (d2, v2, w2) = Familie(Path.Combine(root, "mit-pin"), "4826");
            r = Cli("--data-root", d2, "--install-deps", "vollpaket", "--vollpaket", v2, "--werkzeuge", w2, "--offline");
            Assert.Equal(4, r.Exit); Assert.False(Directory.Exists(Path.Combine(w2, "testtool")));
            r = Cli("--data-root", d2, "--install-deps", "vollpaket", "--vollpaket", v2, "--werkzeuge", w2, "--offline", "--pin", "9999");
            Assert.Equal(4, r.Exit); Assert.DoesNotContain("9999", r.Out); Assert.False(Directory.Exists(Path.Combine(w2, "testtool")));
            r = Cli("--data-root", d2, "--install-deps", "vollpaket", "--vollpaket", v2, "--werkzeuge", w2, "--offline", "--pin", "4826");
            Assert.True(r.Exit == 0, r.Out); Assert.DoesNotContain("4826", r.Out);
            Assert.True(File.Exists(Path.Combine(w2, "testtool", "testtool.exe")));
            foreach (var f in Directory.GetFiles(d2, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != "anmeldung.json")) // Prüfwert der PIN liegt nur dort (gehasht)
                Assert.DoesNotMatch(@"(?i)pin.{0,40}4826|(?<![0-9a-f])4826(?![0-9a-f])", File.ReadAllText(f)); // nicht als eigener Wert (Prüfsummen dürfen die Ziffern zufällig enthalten)
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
