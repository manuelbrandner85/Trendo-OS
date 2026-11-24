using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0755: ERSETZTES winget – ein eigenes Testprogramm (Batch-Datei), das die Ausgaben von winget nachahmt.
/// Es installiert nichts: „installiert“ heißt hier nur, dass es eine Zustandsdatei in seinem eigenen Temp-Ordner anlegt.
/// Ein echter winget-Lauf ist damit NICHT getestet.</summary>
public static class FakeWinget
{
    public static string Create(string dir)
    {
        Directory.CreateDirectory(dir);
        string Table(string id) => "Name               Id                   Version  Source\r\n" + new string('-', 56) + "\r\n" + $"VLC media player   {id.PadRight(21)}3.0.21   winget\r\n";
        foreach (var id in new[] { "VideoLAN.VLC", "Langsam.Paket" }) File.WriteAllText(Path.Combine(dir, $"list-{id}.txt"), Table(id));
        File.WriteAllText(Path.Combine(dir, "search.txt"), Table("VideoLAN.VLC"));
        // T-0755 (Gegenprüfung): Git mit verfügbarer Aktualisierung 2.45 → 2.47
        string Up(string v) => "Name  Id                   Version  Available  Source\r\n" + new string('-', 56) + "\r\n" + $"Git   {"Git.Git".PadRight(21)}{v.PadRight(9)}{(v == "2.45" ? "2.47" : "").PadRight(11)}winget\r\n";
        File.WriteAllText(Path.Combine(dir, "list-Git.Git.txt"), Up("2.45")); File.WriteAllText(Path.Combine(dir, "list-Git.Git-neu.txt"), Up("2.47"));
        var cmd = Path.Combine(dir, "winget-ersatz.cmd");
        File.WriteAllText(cmd, string.Join("\r\n",
            "@echo off",
            "set \"STATE=%~dp0installiert-%3.txt\"",
            "if /I \"%1\"==\"search\" (type \"%~dp0search.txt\" & exit /b 0)",
            "if /I \"%1\"==\"list\" goto list",
            "if /I \"%1\"==\"install\" goto install",
            "if /I \"%1\"==\"uninstall\" goto uninstall",
            "if /I \"%1\"==\"upgrade\" goto upgrade",
            "echo Unbekannter Befehl",
            "exit /b 1",
            ":install",
            "if not exist \"%~dp0list-%3.txt\" (echo No package found matching input criteria. & exit /b -1978335212)",
            "echo Found VLC media player [%3] Version 3.0.21",
            "echo This application is licensed to you by its owner.",
            "echo Downloading https://example.invalid/vlc-3.0.21-win64.exe",
            "echo   ##########            10.0 MB / 40.0 MB",
            "if /I \"%3\"==\"Langsam.Paket\" ping -n 90 127.0.0.1 >nul",
            "ping -n 2 127.0.0.1 >nul",
            "echo   ####################  40.0 MB / 40.0 MB",
            "echo Successfully verified installer hash",
            "echo Starting package install...",
            "echo ok> \"%STATE%\"",
            "echo Successfully installed",
            "exit /b 0",
            ":uninstall",
            "if not exist \"%STATE%\" (echo No installed package found matching input criteria. & exit /b -1978335212)",
            "echo Found VLC media player [%3]",
            "echo Starting package uninstall...",
            "del \"%STATE%\"",
            "echo Successfully uninstalled",
            "exit /b 0",
            ":upgrade",
            "if not exist \"%~dp0upgrade-geht.txt\" (echo An unexpected error occurred while executing the command & exit /b 1)",
            "copy /y \"%~dp0list-%3-neu.txt\" \"%~dp0list-%3.txt\" >nul",
            "echo Successfully installed",
            "exit /b 0",
            ":list",
            "if exist \"%~dp0quelle-kaputt.txt\" (echo Failed when opening source^(s^); try the 'source reset' command if the problem persists. & exit /b -1978335217)",
            "if not exist \"%STATE%\" (echo No installed package found matching input criteria. & exit /b -1978335212)",
            "type \"%~dp0list-%3.txt\"",
            "exit /b 0", ""));
        return cmd;
    }
}

public class T0755_Programme
{
    [Fact(DisplayName = "T-0755 SYS-04: Fortschritt, Ausgabezeilen und Nachprüfung werden aus winget-Ausgaben gelesen (nachgebildete Ausgabe)")]
    public void Parse_progress_and_list()
    {
        Assert.Equal(0.25, PackageParsers.Progress("  ██████▒▒▒▒▒▒  10.0 MB / 40.0 MB")!.Value, 3);
        Assert.Equal(0.5, PackageParsers.Progress("\r  ███  1,5 GB / 3 GB")!.Value, 3);
        Assert.Equal(0.45, PackageParsers.Progress("  ██████  45%")!.Value, 3);
        Assert.Null(PackageParsers.Progress("Starting package install..."));
        Assert.Null(PackageParsers.Progress("Version 3.0.21"));
        Assert.Equal("  ███ 40 MB / 40 MB", PackageParsers.CleanLine("  █ 1 MB / 40 MB\r  ███ 40 MB / 40 MB\r"));
        Assert.Equal("Found VLC", PackageParsers.CleanLine("\b-\b\\\b|\bFound VLC"));
        // „winget list --id … -e“: gefunden bzw. „nichts gefunden“
        const string Listed = "Name               Id            Version  Source\r\n-------------------------------------------------\r\nVLC media player   VideoLAN.VLC  3.0.21   winget\r\n";
        Assert.True(PackageParsers.ListedIn(Listed, "videolan.vlc"));
        Assert.False(PackageParsers.ListedIn(Listed, "GIMP.GIMP"));
        Assert.False(PackageParsers.ListedIn("No installed package found matching input criteria.\r\n", "VideoLAN.VLC"));
        Assert.Contains("kein Paket", PackageParsers.ExitText(-1978335212));
        Assert.Equal("winget install --id VideoLAN.VLC -e --scope user --accept-package-agreements --accept-source-agreements --disable-interactivity", new WingetBackend(new RealProcessRunner()).CommandLine("install", "VideoLAN.VLC"));
    }

    [Fact(DisplayName = "T-0755 SYS-04 (ERSETZT: Testprogramm statt winget): Installieren mit Protokoll, Rückgabewert und Nachprüfung; Entfernen prüft nach; Fehler wird erkannt")]
    public async Task Run_with_log_and_verify()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var exe = FakeWinget.Create(Path.Combine(f.Root, "ersatz"));
        var c = new AppCenter(new WingetBackend(new RealProcessRunner(), exe), f.Core.Approvals, f.Core.Audit);
        var logs = Path.Combine(f.Root, "protokolle");
        var vlc = new PackageInfo("VLC media player", "VideoLAN.VLC", "3.0.21", null, "winget");
        Assert.False(await c.Backend.IsInstalledAsync("VideoLAN.VLC"));

        var r = c.Plan("install", vlc); f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        var lines = new List<string>();
        var res = await c.RunAsync(r, Principal.LocalOwner, logs, l => { lock (lines) lines.Add(l); });
        Assert.True(res.ExitOk); Assert.Equal(0, res.ExitCode); Assert.False(res.Cancelled);
        Assert.True(res.Installed); Assert.True(res.Verified);
        Assert.Contains(lines, l => l.Contains("10.0 MB / 40.0 MB"));
        Assert.Contains("Successfully installed", res.Output);
        var log = File.ReadAllText(res.LogFile);
        Assert.Contains("Befehl: " + exe + " install --id VideoLAN.VLC -e --scope user", log);
        Assert.Contains("Successfully installed", log); Assert.Contains("Rückgabewert: 0 – fertig ohne Fehler", log);
        Assert.Contains("Nachprüfung: steht in der Liste der installierten Programme", log); Assert.Contains("Ziel erreicht: ja", log);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "app.pruefung" && a.Detail.Contains("install VideoLAN.VLC bestätigt"));

        var u = c.Plan("uninstall", vlc); f.Core.Approvals.Approve(u.Id, Principal.LocalOwner);
        var ur = await c.RunAsync(u, Principal.LocalOwner, logs);
        Assert.True(ur.ExitOk); Assert.False(ur.Installed); Assert.True(ur.Verified);

        var bad = c.Plan("install", new PackageInfo("Gibt es nicht", "Gibt.Es.Nicht", "")); f.Core.Approvals.Approve(bad.Id, Principal.LocalOwner);
        var br = await c.RunAsync(bad, Principal.LocalOwner, logs);
        Assert.False(br.ExitOk); Assert.Equal(unchecked((int)0x8A150014), br.ExitCode); Assert.False(br.Verified);
        Assert.Contains("kein Paket mit dieser Kennung", File.ReadAllText(br.LogFile));
    }

    [Fact(DisplayName = "T-0755 SYS-04 (ERSETZT: Testprogramm statt winget): Abbrechen beendet den Lauf samt Kindprozess, Protokoll sagt „abgebrochen“, Nachprüfung: nicht installiert")]
    public async Task Cancel_kills_run()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var exe = FakeWinget.Create(Path.Combine(f.Root, "ersatz"));
        var c = new AppCenter(new WingetBackend(new RealProcessRunner(), exe), f.Core.Approvals, f.Core.Audit);
        var r = c.Plan("install", new PackageInfo("Langsam", "Langsam.Paket", "1")); f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        using var cts = new CancellationTokenSource();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var task = c.RunAsync(r, Principal.LocalOwner, Path.Combine(f.Root, "protokolle"), l => { if (l.Contains("10.0 MB")) cts.Cancel(); }, cts.Token);
        var res = await task.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(45), "Der Lauf wurde nicht beendet (Testprogramm wartet 90 s).");
        Assert.True(res.Cancelled); Assert.False(res.ExitOk); Assert.False(res.Installed); Assert.False(res.Verified);
        Assert.False(File.Exists(Path.Combine(f.Root, "ersatz", "installiert-Langsam.Paket.txt")));
        var log = File.ReadAllText(res.LogFile);
        Assert.Contains("10.0 MB / 40.0 MB", log); Assert.Contains("Ergebnis: abgebrochen", log); Assert.DoesNotContain("Successfully installed", log);
        Assert.Contains(f.Core.Audit.ReadAll(), a => a.Action == "app.abgebrochen");
    }
}

/// <summary>T-0755 SPEC-12: Systemprüfung mit ECHTEN Dateien und der echten Windows-Signaturprüfung (WinVerifyTrust) – nichts ersetzt.</summary>
public class T0755_Gefahren
{
    static string RuntimeFile(string name) => Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), name);

    /// <summary>Echte Windows-Verknüpfung über die Windows-Shell anlegen (nur im eigenen Temp-Ordner).</summary>
    static void MakeLink(string lnk, string target, string args = "")
    {
        var ps = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{lnk.Replace("'", "''")}');$s.TargetPath='{target.Replace("'", "''")}';$s.Arguments='{args.Replace("'", "''")}';$s.Save()";
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell", ["-NoProfile", "-NonInteractive", "-Command", ps]) { CreateNoWindow = true, UseShellExecute = false })!;
        Assert.True(p.WaitForExit(60000)); Assert.True(File.Exists(lnk), "Verknüpfung wurde nicht angelegt");
    }

    [Fact(DisplayName = "T-0755 SPEC-12 (echt): Signaturprüfung – Windows-Datei gültig von Microsoft, eingebettete Signatur gültig, unsignierte Datei erkannt, veränderte Datei ungültig")]
    public void Real_signatures()
    {
        if (!OperatingSystem.IsWindows()) return;
        var k = Authenticode.Check(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));
        Assert.Equal(SigState.Gueltig, k.State); Assert.Contains("Microsoft", k.Publisher);
        var ex = Authenticode.Check(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        Assert.Equal(SigState.Gueltig, ex.State); Assert.Contains("Microsoft", ex.Publisher);
        var core = RuntimeFile("coreclr.dll");
        var c = Authenticode.Check(core);
        Assert.Equal(SigState.Gueltig, c.State); Assert.False(c.ViaCatalog); Assert.Contains("Microsoft", c.Publisher);

        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0755-sig-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var unsigned = Path.Combine(dir, "unsigniert.exe"); File.Copy(typeof(SystemScan).Assembly.Location, unsigned);
            Assert.Equal(SigState.Unsigniert, Authenticode.Check(unsigned).State);
            var bad = Path.Combine(dir, "veraendert.dll"); var bytes = File.ReadAllBytes(core); bytes[bytes.Length / 3] ^= 0x5A; File.WriteAllBytes(bad, bytes);
            var b = Authenticode.Check(bad);
            Assert.Equal(SigState.Ungueltig, b.State); Assert.Contains("verändert", b.Detail);
            Assert.Equal(SigState.Fehlt, Authenticode.Check(Path.Combine(dir, "gibt-es-nicht.exe")).State);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0755 SPEC-12: Ampel-Regeln – grün nur gültig + bekannt, gelb unsigniert/unbekannt/Skript, rot bei fehlender Datei, ungültiger Signatur, %TEMP% oder verstecktem Befehl")]
    public void Ampel_rules()
    {
        var k32 = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        StartEntry E(string file, string cmd = "") => new(SystemScan.KindRun, "Test", "HKCU", cmd.Length > 0 ? cmd : file, file);
        Assert.Equal(Ampel.Gruen, SystemScan.Assess(E(k32), new(SigState.Gueltig, "Microsoft Windows", "")).Light);
        var unk = SystemScan.Assess(E(k32), new(SigState.Gueltig, "Beispiel Software GmbH", ""));
        Assert.Equal(Ampel.Gelb, unk.Light); Assert.Equal("Herausgeber unbekannt", unk.Reason);
        Assert.Equal(Ampel.Gelb, SystemScan.Assess(E(k32), new(SigState.Unsigniert, null, "")).Light);
        Assert.Equal(Ampel.Rot, SystemScan.Assess(E(k32), new(SigState.Ungueltig, "Microsoft", "verändert")).Light);
        Assert.Equal(Ampel.Rot, SystemScan.Assess(E(@"C:\gibt\es\nicht.exe"), new(SigState.Fehlt, null, "")).Light);
        var ps = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var enc = SystemScan.Assess(E(ps, $"\"{ps}\" -NoP -enc SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA"), new(SigState.Gueltig, "Microsoft Windows", ""));
        Assert.Equal(Ampel.Rot, enc.Light); Assert.Equal("Versteckter Befehl", enc.Reason);
        var script = SystemScan.Assess(E(ps, $"\"{ps}\" -File C:\\Skripte\\start.ps1"), new(SigState.Gueltig, "Microsoft Windows", ""));
        Assert.Equal(Ampel.Gelb, script.Light); Assert.Equal("Startet ein Skript", script.Reason);
        Assert.True(SystemScan.KnownPublisher("Intel Corporation")); Assert.False(SystemScan.KnownPublisher("Intelligent Software Ltd")); Assert.False(SystemScan.KnownPublisher("Digitalwerk GmbH"));
        Assert.NotNull(SystemScan.SuspiciousPlace(Path.Combine(Path.GetTempPath(), "x.exe")));
        Assert.Null(SystemScan.SuspiciousPlace(k32));
        // Befehle → Datei
        Assert.Equal(k32, SystemScan.FileFromCommand($"rundll32.exe \"{k32}\",Start"), ignoreCase: true);
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "acproxy.dll"), SystemScan.FileFromCommand(@"%windir%\system32\rundll32.exe /d acproxy.dll,PerformAutochkOperations", f => f.EndsWith("acproxy.dll", StringComparison.OrdinalIgnoreCase) || File.Exists(f)), ignoreCase: true);
        Assert.Equal(@"C:\Program Files\Beispiel\app.exe", SystemScan.FileFromCommand(@"C:\Program Files\Beispiel\app.exe --minimized", _ => false));
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "cmd.exe"), SystemScan.FileFromCommand("cmd /c echo x"), ignoreCase: true);
        Assert.Equal(@"C:\A B\c.exe", SystemScan.FileFromCommand("\"C:\\A B\\c.exe\" /x", _ => false));
    }

    [Fact(DisplayName = "T-0755 SPEC-12 (echt): Autostart-Ordner mit echten Verknüpfungen und einer unsignierten Temp-Datei – Ampel je Eintrag, Bericht HTML und CSV")]
    public void Startup_folder_real_files()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0755-auto-" + Guid.NewGuid().ToString("N")); var start = Path.Combine(dir, "Autostart"); Directory.CreateDirectory(start);
        try
        {
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            MakeLink(Path.Combine(start, "Explorer.lnk"), explorer);
            MakeLink(Path.Combine(start, "Weg.lnk"), @"C:\Trendo-Test-gibt-es-nicht\weg.exe");
            MakeLink(Path.Combine(start, "Skript.lnk"), Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c echo hallo");
            File.Copy(typeof(SystemScan).Assembly.Location, Path.Combine(start, "testprogramm.exe")); // unsigniert, liegt in %TEMP%
            File.WriteAllText(Path.Combine(start, "desktop.ini"), "[.ShellClassInfo]");

            var r = SystemScan.Run(new SystemScan.Sources([start], RunKeys: false, Tasks: false, Programs: false));
            Assert.Equal(4, r.Findings.Count);
            Finding F(string n) => r.Findings.Single(f => f.Entry.Name == n);
            Assert.Equal(explorer, F("Explorer").Entry.File, ignoreCase: true);
            Assert.Equal(Ampel.Gruen, F("Explorer").Light); Assert.Contains("Microsoft", F("Explorer").Signature.Publisher);
            Assert.Equal(Ampel.Rot, F("Weg").Light); Assert.Equal("Datei fehlt", F("Weg").Reason); Assert.Contains("shell:startup", F("Weg").Tip);
            Assert.Equal(Ampel.Gelb, F("Skript").Light); Assert.Contains("/c echo hallo", F("Skript").Entry.Command);
            var t = F("testprogramm.exe");
            Assert.Equal(Ampel.Rot, t.Light); Assert.Equal("Verdächtiger Ort", t.Reason); Assert.Equal(SigState.Unsigniert, t.Signature.State); Assert.Contains("%TEMP%", t.Explanation);
            Assert.Equal(Ampel.Rot, r.Findings[0].Light); // Rot zuerst
            var html = SystemScan.ReportHtml(r); var csv = SystemScan.ReportCsv(r);
            Assert.Contains("testprogramm.exe", html); Assert.Contains("Trendo hat nur gelesen", html); Assert.Equal(5, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.True(Directory.Exists(start) && File.Exists(Path.Combine(start, "Weg.lnk"))); // nichts verändert
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0755 SPEC-12 (echt, nur lesend): Prüfung dieses PCs – Run-Schlüssel, geplante Aufgaben und installierte Programme werden gelesen und bewertet")]
    public void Real_system_read_only()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = SystemScan.FromRunKeys().ToList(); // darf leer sein, darf nicht scheitern
        Assert.All(run, e => Assert.Equal(SystemScan.KindRun, e.Kind));
        var tasks = SystemScan.FromScheduledTasks(out var skipped);
        Assert.True(tasks.Count + skipped > 0, "Die Aufgabenplanung lieferte nichts.");
        var programs = SystemScan.FromInstalledPrograms().ToList();
        Assert.NotEmpty(programs);
        var r = SystemScan.Run(SystemScan.Sources.System);
        Assert.NotEmpty(r.Findings);
        Assert.All(r.Findings, f => { Assert.NotEmpty(f.Explanation); Assert.NotEmpty(f.Tip); });
        Assert.Contains(r.Findings, f => f.Light == Ampel.Gruen); // z. B. Windows-eigene Aufgaben
    }
}

public class T0755_DownloadsLinks
{
    [Fact(DisplayName = "T-0755 SYS-03: SHA-256 wird immer berechnet, Verlauf bleibt nach „Fertige aus der Liste nehmen“ und über Neustart; Links sammeln ohne Doppelte")]
    public void Sha_history_links()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0755-dl-" + Guid.NewGuid().ToString("N")); var dir = Path.Combine(root, "dl"); Directory.CreateDirectory(dir);
        try
        {
            var q = new DownloadQueue(root);
            var j = q.Add(new Uri("https://example.org/a.txt"), dir); q.Started(j);
            File.WriteAllText(j.Target, "Hallo"); q.Completed(j);
            Assert.Equal("753692ec36adb4c794c973945eb2a99c1649703ea6f76bf259abb4fb838e013e", j.Sha256); // SHA-256 von „Hallo“
            Assert.Null(j.HashResult); Assert.Equal(DownloadQueue.State.Fertig, j.Status);
            var bad = q.Add(new Uri("https://example.org/b.txt"), dir, new string('0', 64)); q.Started(bad);
            File.WriteAllText(bad.Target, "x"); q.Completed(bad);
            Assert.Equal(DownloadQueue.State.Fehler, bad.Status); Assert.NotNull(bad.Sha256);
            Assert.Equal(1, q.ClearFinished());
            var h = new DownloadQueue(root).History.Load();
            Assert.Equal(2, h.Count); Assert.False(h[0].Ok); Assert.True(h[1].Ok); Assert.Equal(j.Sha256, h[1].Sha256);
            Assert.Equal(2, Bookmarks.Collect(root, "https://example.org/x.pdf und nochmal https://example.org/x.pdf, dazu http://example.net/"));
            Assert.Equal(0, Bookmarks.Collect(root, "https://example.org/x.pdf"));
            Assert.All(Bookmarks.Load(root), b => Assert.Equal(Bookmarks.Collected, b.Folder));
            Bookmarks.Remove(root, "http://example.net/"); Assert.Single(Bookmarks.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
