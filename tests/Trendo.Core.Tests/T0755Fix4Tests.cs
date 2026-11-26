using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0755 Fix4 (Viertprüfung): ungültige Signatur geht vor Squirrel- und Werkzeug-Zweig, Squirrel prüft Netzadressen,
/// „Versteckter Befehl“ nur bei PowerShell. Rot auf d09aea8ba, grün danach.</summary>
public class T0755_Fix4
{
    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert.");
    static StartEntry Run(string cmd) => new(SystemScan.KindRun, "Test", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", cmd, SystemScan.FileFromCommand(cmd));

    [Fact(DisplayName = "T-0755 Fix4 MITTEL (echte Datei): manipulierte Update.exe (ein Byte verändert, Signatur ungültig) mit --processStart ist rot, auch mit Adresse in --process-start-args")]
    public void Tampered_update_exe_is_red()
    {
        if (!OperatingSystem.IsWindows()) return;
        var src = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"); // eingebettet signiert
        if (!File.Exists(src)) { Ueberspringen.Weil("dotnet.exe fehlt unter Programme\\dotnet."); return; }
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "trendo-t0755-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var upd = Path.Combine(dir, "Update.exe");
            var bytes = File.ReadAllBytes(src); bytes[bytes.Length / 3] ^= 0x5A; File.WriteAllBytes(upd, bytes);
            var sig = Authenticode.Check(upd);
            Assert.Equal(SigState.Ungueltig, sig.State);
            var a = SystemScan.Assess(Run($"\"{upd}\" --processStart chat.exe"), sig);
            Assert.Equal(Ampel.Rot, a.Light); Assert.Equal("Signatur ungültig", a.Reason);
            Assert.Equal(Ampel.Rot, SystemScan.Assess(Run($"\"{upd}\" --processStart chat.exe --process-start-args \"http://example.invalid/x\""), sig).Light);
            // gültig signierter Squirrel-Starter mit Adresse in den Start-Argumenten: rot (lädt aus dem Netz)
            var ok = new SignatureInfo(SigState.Gueltig, "Beispiel Software GmbH", "Gültig signiert.");
            var net = SystemScan.Assess(Run($"\"{upd}\" --processStart chat.exe --process-start-args \"http://example.invalid/x\""), ok);
            Assert.Equal(Ampel.Rot, net.Light); Assert.Equal("Lädt aus dem Netz", net.Reason);
            // ohne Adresse bleibt es gelb (Squirrel-Abweichung bestätigt)
            Assert.Equal(Ampel.Gelb, SystemScan.Assess(Run($"\"{upd}\" --processStart chat.exe"), ok).Light);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        Assert.False(Directory.Exists(dir));
    }

    [Fact(DisplayName = "T-0755 Fix4: ungültige Signatur bei einem Windows-Werkzeug ist rot (vor dem Werkzeug-Zweig)")]
    public void Invalid_signature_before_tool_branch()
    {
        if (!OperatingSystem.IsWindows()) return;
        var bad = new SignatureInfo(SigState.Ungueltig, "Microsoft Corporation", "Die Datei wurde nach dem Signieren verändert.");
        var f = SystemScan.Assess(Run(Path.Combine(Environment.SystemDirectory, "regsvr32.exe") + " /s scrobj.dll"), bad);
        Assert.Equal(Ampel.Rot, f.Light); Assert.Equal("Signatur ungültig", f.Reason);
    }

    [Fact(DisplayName = "T-0755 Fix4 NIEDRIG: „Versteckter Befehl“ nur bei PowerShell – node -e und dotnet /e: nicht; dotnet --info mit passendem Text")]
    public void Encoded_only_for_powershell()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
        var k32 = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        StartEntry E(string cmd) => new(SystemScan.KindRun, "Test", "HKCU", cmd, k32);
        Assert.NotEqual("Versteckter Befehl", SystemScan.Assess(E($"\"C:\\Program Files\\nodejs\\node.exe\" -e {B64}"), Ms).Reason);
        Assert.NotEqual("Versteckter Befehl", SystemScan.Assess(E($"\"C:\\Program Files\\dotnet\\dotnet.exe\" /e:{B64}"), Ms).Reason);
        Assert.Equal("Versteckter Befehl", SystemScan.Assess(E($"\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -e {B64}"), Ms).Reason);
        Assert.Equal("Versteckter Befehl", SystemScan.Assess(Run($"powershell.exe -enc {B64}"), Ms).Reason);
        var info = SystemScan.Assess(E("\"C:\\Program Files\\dotnet\\dotnet.exe\" --info"), Ms);
        Assert.DoesNotContain("Ziel konnte Trendo nicht ablesen", info.Explanation);
        Assert.Contains("Info", info.Explanation, StringComparison.OrdinalIgnoreCase);
    }
}
