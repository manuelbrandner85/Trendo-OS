using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0755 Fix3 (Drittprüfung): dotnet als Code-Lader, ProgramData-Ziele, Ausnahme „eigener Programmordner“ nur unter Programme/Windows,
/// Squirrel-Update.exe --processStart, Skript-Argumente (.js/.mjs/.cjs), "-enc" und -enc:. Rot auf acda02ef0, grün danach.</summary>
public class T0755_Fix3
{
    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert.");
    static StartEntry Run(string cmd) => new(SystemScan.KindRun, "Test", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", cmd, SystemScan.FileFromCommand(cmd));
    static string TestDir() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "trendo-t0755-test-" + Guid.NewGuid().ToString("N")[..8]);

    [Fact(DisplayName = "T-0755 Fix3 MITTEL: dotnet.exe mit Bibliothek aus C:\\ProgramData ist nie grün (Code-Lader, ProgramData mindestens gelb)")]
    public void Dotnet_programdata_not_green()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        if (!File.Exists(dotnet)) { Ueberspringen.Weil("dotnet.exe fehlt unter Programme\\dotnet."); return; }
        var f = SystemScan.Assess(Run($"\"{dotnet}\" C:\\ProgramData\\x\\evil.dll"), Ms);
        Assert.NotEqual(Ampel.Gruen, f.Light); Assert.Contains("ProgramData", f.Explanation);
        // gewöhnliches Programm mit Bibliothek aus ProgramData: mindestens gelb
        var k32 = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        var plain = SystemScan.Assess(new StartEntry(SystemScan.KindRun, "App", "HKCU", $"\"{k32}\" C:\\ProgramData\\x\\plugin.dll", k32), Ms);
        Assert.Equal(Ampel.Gelb, plain.Light);
    }

    [Fact(DisplayName = "T-0755 Fix3 MITTEL (echte Dateien): Microsoft-signierte Kopien im Benutzerordner mit Skript daneben sind rot – „eigener Programmordner“ gilt nur unter Programme/Windows")]
    public void Own_folder_exception_only_in_program_files()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = TestDir(); Directory.CreateDirectory(Path.Combine(dir, "x"));
        try
        {
            var ping = Path.Combine(dir, "helfer.exe"); File.Copy(Path.Combine(Environment.SystemDirectory, "PING.EXE"), ping);
            File.WriteAllText(Path.Combine(dir, "x", "evil.ps1"), "# Test");
            var sig = Authenticode.Check(ping); Assert.Equal(SigState.Gueltig, sig.State);
            Assert.Equal(Ampel.Rot, SystemScan.Assess(Run($"\"{ping}\" \"{Path.Combine(dir, "x", "evil.ps1")}\""), sig).Light);
            var dotnetSrc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
            if (File.Exists(dotnetSrc))
            {
                var dn = Path.Combine(dir, "dotnet.exe"); File.Copy(dotnetSrc, dn);
                File.WriteAllText(Path.Combine(dir, "x", "evil.dll"), "kein echtes Programm");
                Assert.Equal(Ampel.Rot, SystemScan.Assess(Run($"\"{dn}\" x\\evil.dll"), Authenticode.Check(dn)).Light);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        Assert.False(Directory.Exists(dir));
    }

    [Fact(DisplayName = "T-0755 Fix3 MITTEL (echte Datei): Squirrel-Update.exe --processStart, Skript-Argument (.js) und \"-enc\"/-enc: sind nie grün")]
    public void Squirrel_scripts_and_enc_variants()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = TestDir(); Directory.CreateDirectory(dir);
        try
        {
            var upd = Path.Combine(dir, "Update.exe"); File.Copy(Path.Combine(Environment.SystemDirectory, "PING.EXE"), upd);
            var sig = Authenticode.Check(upd);
            var f = SystemScan.Assess(Run($"\"{upd}\" --processStart evil.exe"), sig);
            Assert.NotEqual(Ampel.Gruen, f.Light); Assert.Contains("evil.exe", f.Explanation);
            Assert.NotEqual(Ampel.Gruen, SystemScan.Assess(Run($"\"{upd}\" --processStartAndWait \"evil.exe\""), sig).Light);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        Assert.False(Directory.Exists(dir));
        var k32 = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        foreach (var js in new[] { @"C:\Werkzeuge\start.js", @"C:\Werkzeuge\start.mjs", @"C:\Werkzeuge\start.cjs" })
            Assert.Equal(Ampel.Gelb, SystemScan.Assess(new StartEntry(SystemScan.KindRun, "App", "HKCU", $"\"{k32}\" {js}", k32), Ms).Light);
        var ps = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
        foreach (var c in new[] { $"\"{ps}\" \"-enc\" {B64}", $"\"{ps}\" -enc:{B64}", $"\"{ps}\" \"-EncodedCommand\" \"{B64}\"" })
            Assert.Equal("Versteckter Befehl", SystemScan.Assess(Run(c), Ms).Reason);
    }

    [Fact(DisplayName = "T-0755 Fix3: Ampel ist ehrlich – „Hinweis-Ampel, keine Virenprüfung“, grün heißt „keine bekannte Auffälligkeit“, nirgends „sicher“ oder „sauber“")]
    public void Honest_wording()
    {
        var k32 = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        var g = SystemScan.Assess(new StartEntry(SystemScan.KindRun, "App", "HKCU", k32, k32), Ms);
        Assert.Equal(Ampel.Gruen, g.Light); Assert.Contains("keine bekannte Auffälligkeit", g.Explanation + " " + g.Tip, StringComparison.OrdinalIgnoreCase);
        var html = SystemScan.ReportHtml(new SystemScan.Result(DateTimeOffset.Now, [g], 0));
        Assert.Contains("keine Virenprüfung", html); Assert.Contains("keine bekannte Auffälligkeit", html);
        foreach (var t in new[] { g.Explanation, g.Tip, html })
            Assert.DoesNotMatch(@"(?i)\b(ist|sind|wirkt|scheint)\s+(sicher|sauber)\b|\bsauber\b", t);
    }
}
