using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0757 Fix3: Umgehungen der echo/rem-Ausnahme aus der Drittprüfung von fbedfc5f0 und die enge --shell-Ausnahme.
/// Nutzt nur Schnittstellen, die es dort schon gibt (Rotlauf möglich).</summary>
public class T0757Fix3Tests
{
    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert.");
    const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
    static string Sys(string f) => Path.Combine(Environment.SystemDirectory, f);
    static StartEntry E(string cmd)
    {
        var f = SystemScan.FileFromCommand(cmd);
        return new(SystemScan.KindRun, "Test", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", cmd, f is not null && File.Exists(f) ? f : Sys("kernel32.dll"));
    }
    static string Reason(string cmd) => SystemScan.Assess(E(cmd), Ms) is var f ? $"{f.Light} {f.Reason}" : "";

    [Fact(DisplayName = "T-0757 Fix3: echo\\..\\, rem\\..\\, cd && echo\\..\\, for ( cmd /c echo ) do powershell und if … ( cmd /c echo ) else powershell sind rot")]
    public void Echo_umgehungen_rot()
    {
        if (!OperatingSystem.IsWindows()) return;
        var cmd = Sys("cmd.exe");
        foreach (var c in new[]
        {
            $"{cmd} /c echo\\..\\WindowsPowerShell\\v1.0\\powershell.exe -enc {B64}",
            $"{cmd} /c rem\\..\\WindowsPowerShell\\v1.0\\powershell.exe -enc {B64}",
            $"{cmd} /c cd /d %SystemRoot%\\System32 && echo\\..\\WindowsPowerShell\\v1.0\\powershell.exe -enc {B64}",
            $"{cmd} /c for %a in ( cmd /c echo ) do powershell -enc {B64}",
            $"{cmd} /c if 1==0 ( cmd /c echo ) else powershell -enc {B64}",
        })
            Assert.True(Reason(c) == "Rot Versteckter Befehl", $"{c} → {Reason(c)}");
    }

    [Fact(DisplayName = "T-0757 Fix3: Ausnahme nur für --shell[= ]powershell ohne Pfad und ohne weiteren Befehl danach")]
    public void Shell_ausnahme_eng()
    {
        Assert.False(SystemScan.EncodedInnerPowerShell($"tool --shell powershell -e {B64}"));
        Assert.False(SystemScan.EncodedInnerPowerShell($"tool --shell=pwsh -e {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"tool --shell C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"tool --shell powershell.exe -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"tool --shell powershell -e x & powershell -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"/c tool --shell powershell; powershell -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"tool -shell powershell -enc {B64}"));
    }
}
