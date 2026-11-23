using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0755 Fix5 (fünfte Prüfung): verschlüsselter PowerShell-Befehl auch, wenn PowerShell erst in den Argumenten gestartet wird
/// (cmd /c, start, conhost --headless, forfiles /c). Rot auf f129c6aaf, grün danach.</summary>
public class T0755_Fix5
{
    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert.");
    const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
    static string Sys(string f) => Path.Combine(Environment.SystemDirectory, f);
    static StartEntry Run(string cmd) => new(SystemScan.KindRun, "Test", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", cmd, SystemScan.FileFromCommand(cmd));

    [Fact(DisplayName = "T-0755 Fix5 MITTEL: cmd /c powershell -enc, start /min powershell.exe -w hidden -enc, conhost --headless powershell -enc und forfiles /c \"powershell -enc\" sind rot „Versteckter Befehl“")]
    public void Encoded_powershell_inside_arguments()
    {
        if (!OperatingSystem.IsWindows()) return;
        var ps = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        foreach (var cmd in new[]
        {
            $"{Sys("cmd.exe")} /c powershell -enc {B64}",
            $"{Sys("cmd.exe")} /c start /min powershell.exe -w hidden -enc {B64}",
            $"{Sys("conhost.exe")} --headless powershell.exe -enc {B64}",
            $"{Sys("forfiles.exe")} /p C:\\Windows /m win.ini /c \"powershell -enc {B64}\"",
            $"{Sys("cmd.exe")} /c \"\"{ps}\" -NoP -EncodedCommand {B64}\"",
            $"{Sys("cmd.exe")} /c pwsh -e {B64}",
        })
        {
            var f = SystemScan.Assess(Run(cmd), Ms);
            Assert.True(f.Light == Ampel.Rot && f.Reason == "Versteckter Befehl", $"{cmd} → {f.Light} {f.Reason}");
        }
        // node -e / dotnet /e: und ein Text „powershell“ ohne verschlüsselten Befehl bleiben nicht „Versteckter Befehl“
        var k32 = Sys("kernel32.dll");
        StartEntry E(string cmd) => new(SystemScan.KindRun, "Test", "HKCU", cmd, k32);
        Assert.NotEqual("Versteckter Befehl", SystemScan.Assess(E($"\"C:\\Program Files\\nodejs\\node.exe\" -e {B64}"), Ms).Reason);
        Assert.NotEqual("Versteckter Befehl", SystemScan.Assess(E($"\"C:\\Program Files\\dotnet\\dotnet.exe\" /e:{B64}"), Ms).Reason);
        Assert.NotEqual("Versteckter Befehl", SystemScan.Assess(Run($"{Sys("cmd.exe")} /c echo powershell & node -e {B64}"), Ms).Reason);
    }
}
