using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0772 Punkt 5: „/shell:powershell -enc …“ (Doppelpunkt vor dem Programmnamen) war in „System auf Gefahren prüfen“ nicht rot.
/// Nutzt nur Schnittstellen, die es auf main 542dc1ce7 schon gibt (Rotlauf möglich).</summary>
public class T0772GefahrenTests
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

    [Fact(DisplayName = "T-0772: /shell:powershell -enc … (auch pwsh, .exe, ^, Anführungszeichen) ist ein versteckter Befehl")]
    public void Shell_doppelpunkt_rot()
    {
        foreach (var a in new[]
        {
            $"tool /shell:powershell -enc {B64}",
            $"tool /shell:pwsh -e {B64}",
            $"tool /shell:powershell.exe -EncodedCommand {B64}",
            $"tool /shell:p^owershell -enc {B64}",
            $"tool \"/shell:powershell\" -enc {B64}",
            $"tool --shell:powershell -enc {B64}",
            $"tool -shell:powershell -enc {B64}",
        })
            Assert.True(SystemScan.EncodedInnerPowerShell(a), a);
        if (!OperatingSystem.IsWindows()) return;
        var c = $"{Sys("rundll32.exe")} shell32.dll,Control_RunDLL /shell:powershell -enc {B64}";
        Assert.True(Reason(c) == "Rot Versteckter Befehl", $"{c} → {Reason(c)}");
    }

    [Fact(DisplayName = "T-0772: harmloses „powershell“ nach Doppelpunkt bleibt ohne Fund (kein verschlüsselter Befehl, --shell-Ausnahme bleibt)")]
    public void Kein_neuer_fehlalarm()
    {
        foreach (var a in new[]
        {
            "tool /shell:powershell",
            "tool /shell:powershell -NoProfile -File C:\\Skripte\\start.ps1",
            "tool --profile:powershell --theme dunkel",
            "notiz Typ:powershell Hinweis:-e kurz",
            "C:\\Tools\\app.exe --open file:powershell-handbuch.pdf",
            $"tool --shell powershell -e {B64}",
            $"tool --shell=pwsh -e {B64}",
            $"tool /shell:powershell-core -enc {B64}",
            $"tool /shell:mypowershell -enc {B64}",
        })
            Assert.False(SystemScan.EncodedInnerPowerShell(a), a);
        if (!OperatingSystem.IsWindows()) return;
        var harmlos = $"{Sys("cmd.exe")} /c start wt.exe /shell:powershell";
        Assert.False(Reason(harmlos) == "Rot Versteckter Befehl", $"{harmlos} → {Reason(harmlos)}");
    }
}
