using Trendo.Core;
using Trendo.Core.ConsolePort;
using Trendo.Core.Sys;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0757 Fix2: Befunde der Zweitprüfung von a9135704d. Nutzt nur Schnittstellen, die es dort schon gibt (Rotlauf möglich).</summary>
public class T0757Fix2Tests(ITestOutputHelper output)
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

    [Fact(DisplayName = "T-0757 Fix2 MITTEL: „echo“/„rem“ nur als erstes Befehlswort ausgenommen – als Fenstertitel, Variable oder vor ; bleibt es rot")]
    public void Mittel_echo_nur_als_erstes_wort()
    {
        if (!OperatingSystem.IsWindows()) return;
        var cmd = Sys("cmd.exe");
        foreach (var c in new[]
        {
            $"{cmd} /c start \"echo\" powershell -enc {B64}",
            $"{cmd} /c start \"rem\" /min powershell -enc {B64}",
            $"{cmd} /c if not defined echo powershell -enc {B64}",
            $"{Sys("bash.exe")} -c \"echo start; powershell -enc {B64}\"",
            $"{Sys("mshta.exe")} vbscript:Execute(\"echo=1:CreateObject(\"\"WScript.Shell\"\").Run \"\"powershell -enc {B64}\"\",0:close\")",
            $"{cmd} /c node -e \"var echo=1;require('child_process').exec('powershell -enc {B64}')\"",
            $"{cmd} /c start \"cmd /c echo\" powershell -enc {B64}",
            $"{cmd} /c echo x\npowershell -enc {B64}",
        })
            Assert.True(Reason(c) == "Rot Versteckter Befehl", $"{c} → {Reason(c)}");
        // Schutz vor Fehlalarm bleibt: echo/rem als erstes Befehlswort, auch im inneren cmd /c und in Klammern/Anführungszeichen
        foreach (var c in new[]
        {
            $"{cmd} /c echo powershell -e {B64}",
            $"{cmd} /q /c echo powershell -enc {B64}",
            $"{cmd} /c @echo powershell -enc {B64}",
            $"{cmd} /c (echo powershell -enc {B64})",
            $"{cmd} /c \"echo powershell -enc {B64}\"",
            $"{cmd} /c rem powershell -enc {B64}",
            $"{cmd} /c echo \"powershell -enc {B64}\" > notiz.txt",
            $"{Sys("forfiles.exe")} /p C:\\Windows /m win.ini /c \"cmd /c echo powershell -enc {B64}\"",
        })
            Assert.False(Reason(c) == "Rot Versteckter Befehl", $"{c} → {Reason(c)}");
    }

    [Fact(DisplayName = "T-0757 Fix2 NIEDRIG 2: Verschleierung mit Anführungszeichen und „/“ am Programmnamen ist rot")]
    public void Niedrig_verschleierung()
    {
        if (!OperatingSystem.IsWindows()) return;
        var cmd = Sys("cmd.exe");
        foreach (var c in new[]
        {
            $"{cmd} /c pow\"\"ershell -enc {B64}",
            $"{cmd} /c \"power\"shell -enc {B64}",
            $"{cmd} /c powershell.exe/NoP /enc {B64}",
            $"{cmd} /q/cpowershell -enc {B64}",
        })
            Assert.True(Reason(c) == "Rot Versteckter Befehl", $"{c} → {Reason(c)}");
    }

    [Fact(DisplayName = "T-0757 Fix2 NIEDRIG 5: „not released under CC0“ und „NOT available under the MIT License“ werden abgewiesen")]
    public void Niedrig_lizenz_verneinung()
    {
        var r = Path.Combine(Path.GetTempPath(), "trendo-t0757z-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(r);
        var elf = FreePortTests.Elf(r); var own = FreePort.Find(FreePort.OwnId)!;
        foreach (var head in new[] { "This game is not released under CC0.", "This software is NOT available under the MIT License.", "Dieses Spiel ist nicht unter der MIT veröffentlicht." })
        {
            var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, head + "\n\n" + FreePortTests.Mit);
            Assert.False(FreePort.CheckLicense(lic, own, elf).Ok, head);
        }
    }

    [Fact(DisplayName = "T-0757 Fix2 NIEDRIG 5 (echte Dateien): keine Lizenzdatei im NuGet-Cache wird wegen einer Verneinung oder eines neuen Ausschlussworts abgewiesen")]
    public void Niedrig_alle_nuget_lizenzen()
    {
        var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } n ? n : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        if (!Directory.Exists(cache)) { output.WriteLine("Kein NuGet-Cache – übersprungen."); return; }
        var r = Path.Combine(Path.GetTempPath(), "trendo-t0757z-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(r);
        var own = FreePort.Find(FreePort.OwnId)!;
        var files = Directory.EnumerateDirectories(cache).SelectMany(p => Directory.EnumerateDirectories(p))
            .SelectMany(v => Directory.EnumerateFiles(v).Where(f => Path.GetFileName(f).StartsWith("license", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).StartsWith("copying", StringComparison.OrdinalIgnoreCase))).ToList();
        var free = new Dictionary<string, int>(); var bad = new List<string>(); var other = 0;
        foreach (var f in files)
        {
            var dir = Path.Combine(r, Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
            var lic = Path.Combine(dir, "LICENSE"); File.Copy(f, lic); // nur lesen: Kopie im eigenen Temp-Ordner
            var res = FreePort.CheckLicense(lic, own, Path.Combine(dir, "x.elf"));
            if (res.Ok) free[res.Text] = free.GetValueOrDefault(res.Text) + 1;
            else if (res.Text.Contains("verneint") || new[] { "weitergabe verboten", "do not distribute", "not for redistribution", "redistribution prohibited", "for internal use only", "vervielfältigung untersagt", "end user license agreement", "nicht weitergegeben", "confidential", "vertraulich", "all copying prohibited", "kopieren verboten" }.Any(w => res.Text.Contains(w, StringComparison.OrdinalIgnoreCase)))
                bad.Add(Path.GetRelativePath(cache, f) + ": " + res.Text);
            else other++;
        }
        output.WriteLine($"{files.Count} Lizenzdateien: frei {string.Join(", ", free.Select(kv => kv.Key + " " + kv.Value))}; aus anderen Gründen nicht frei (z. B. GPL/MPL) {other}; durch Fix1/Fix2-Regeln abgewiesen {bad.Count}");
        foreach (var b in bad) output.WriteLine("  " + b);
        Assert.Empty(bad);
        Assert.True(free.Values.Sum() >= 10);
        Directory.Delete(r, true);
    }
}
