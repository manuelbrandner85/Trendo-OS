using Trendo.Core;
using Trendo.Core.ConsolePort;
using Trendo.Core.Sys;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0757 Fix1: Befunde der Gegenprüfung von d456edd43. Nutzt nur Schnittstellen, die es auf d456edd43 schon gibt (Job über Reflexion),
/// damit der Rotlauf dort möglich ist.</summary>
public class T0757Fix1Tests(ITestOutputHelper output)
{
    static string Root() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0757f-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static FreePort.Title Own => FreePort.Find(FreePort.OwnId)!;

    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert.");
    const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
    static string Sys(string f) => Path.Combine(Environment.SystemDirectory, f);
    /// <summary>Eintrag mit dem Befehl; die Programmdatei ist die echte, sonst kernel32.dll (es geht nur um den Befehl).</summary>
    static StartEntry E(string cmd)
    {
        var f = SystemScan.FileFromCommand(cmd);
        return new(SystemScan.KindRun, "Test", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", cmd, f is not null && File.Exists(f) ? f : Sys("kernel32.dll"));
    }
    static void Red(string cmd) { var f = SystemScan.Assess(E(cmd), Ms); Assert.True(f.Light == Ampel.Rot && f.Reason == "Versteckter Befehl", $"{cmd} → {f.Light} {f.Reason}"); }

    [Fact(DisplayName = "T-0757 Fix1 HOCH: cmd-Varianten (call, /r, /q/c, if, for, start mit Schaltern, start/min, @ , ; /cpowershell), Pfade mit Leerzeichen, conhost mit Schaltern und forfiles sind rot")]
    public void Hoch_cmd_varianten_rot()
    {
        if (!OperatingSystem.IsWindows()) return;
        var cmd = Sys("cmd.exe"); var pwsh = @"C:\Program Files\PowerShell\7\pwsh.exe";
        foreach (var c in new[]
        {
            $"{cmd} /c call powershell -enc {B64}",
            $"{cmd} /r powershell -enc {B64}",
            $"{cmd} /q/c powershell -enc {B64}",
            $"{cmd} /c if 1==1 powershell -enc {B64}",
            $"{cmd} /c for /l %i in (1,1,1) do powershell -enc {B64}",
            $"{cmd} /c start /D C:\\Windows powershell -enc {B64}",
            $"{cmd} /c start /affinity 1 powershell -enc {B64}",
            $"{cmd} /c start/min powershell -enc {B64}",
            $"{cmd} /c \"{pwsh}\" -enc {B64}",
            $"{cmd} /c start \"\" \"{pwsh}\" -enc {B64}",
            $"{cmd} /c \"\"{pwsh}\" -EncodedCommand {B64}\"",
            $"{Sys("conhost.exe")} --headless --width 80 --height 25 powershell -enc {B64}",
            $"{Sys("conhost.exe")} --headless cmd /c call powershell -enc {B64}",
            $"{Sys("forfiles.exe")} /p C:\\Windows /m win.ini /c \"cmd /c call powershell -enc {B64}\"",
            $"{cmd} /c @powershell -enc {B64}",
            $"{cmd} /cpowershell -enc {B64}",
            $"{cmd} /c ,powershell -enc {B64}",
            $"{cmd} /c ;powershell -enc {B64}",
            $"{cmd} /c (powershell -enc {B64})",
            $"{cmd} /c p^owershell -e^nc {B64}",
        }) Red(c);
    }

    [Fact(DisplayName = "T-0757 Fix1 MITTEL 2: auch mshta, pcalua, rundll32, bash und node, die PowerShell mit verschlüsseltem Befehl starten, sind rot")]
    public void Mittel_andere_startprogramme_rot()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var c in new[]
        {
            $"{Sys("mshta.exe")} vbscript:Execute(\"CreateObject(\"\"WScript.Shell\"\").Run \"\"powershell -enc {B64}\"\",0:close\")",
            $"{Sys("pcalua.exe")} -a powershell.exe -c \"-enc {B64}\"",
            $"{Sys("rundll32.exe")} shell32.dll,ShellExec_RunDLL powershell -enc {B64}",
            $"{Sys("bash.exe")} -c \"powershell -enc {B64}\"",
            $"\"C:\\Program Files\\nodejs\\node.exe\" -e \"require('child_process').exec('powershell -enc {B64}')\"",
        }) Red(c);
        // Nicht-Ausführung bleibt ausgenommen
        foreach (var c in new[]
        {
            $"{Sys("cmd.exe")} /c echo powershell -enc {B64}",
            $"{Sys("cmd.exe")} /c echo.powershell -enc {B64} > x.txt",
            $"{Sys("cmd.exe")} /c rem powershell -enc {B64}",
            $"\"C:\\Program Files\\dotnet\\dotnet.exe\" tool --shell powershell -e {B64}",
            $"\"C:\\Program Files\\nodejs\\node.exe\" -e {B64}",
        }) Assert.NotEqual("Versteckter Befehl", SystemScan.Assess(E(c), Ms).Reason);
    }

    /// <summary>Job mit allen Feldern über Reflexion; <paramref name="lists"/> nur, wenn es den Parameter ListDir gibt.</summary>
    static FreePort.Job MakeJob(string elf, string lic, string recomp, string work, string? lists)
    {
        var c = typeof(FreePort.Job).GetConstructors().OrderByDescending(x => x.GetParameters().Length).First();
        object?[] given = [Own, elf, lic, recomp, (IReadOnlyList<string>)[], work];
        return (FreePort.Job)c.Invoke(c.GetParameters().Select((p, i) => i < given.Length ? given[i] : p.Name == "ListDir" ? lists : p.ParameterType == typeof(bool) ? true : p.DefaultValue).ToArray());
    }

    static (string Elf, string Lic) Project(string r, string name)
    {
        var proj = Path.Combine(r, name); Directory.CreateDirectory(proj);
        var lic = Path.Combine(proj, "LICENSE"); File.WriteAllText(lic, FreePortTests.Mit);
        return (FreePortTests.Elf(proj), lic);
    }

    [Fact(DisplayName = "T-0757 Fix1 MITTEL 3: zweiter Titel im selben PS2Recomp-Ordner läuft durch, auch wenn der erste Arbeitsordner weg ist; alte Dateien werden ersetzt")]
    public async Task Mittel_zweiter_titel()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var lists = Path.Combine(r, "trendo-daten", "listen"); var calls = new List<string>();
        var (e1, l1) = Project(r, "projekt-a");
        Assert.Equal("fertig", (await FreePort.RunAsync(MakeJob(e1, l1, recomp, Path.Combine(r, "projekt-a", "frei", "cubes"), lists), FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_a"), null, null, CancellationToken.None)).Status);
        Directory.Delete(Path.Combine(r, "projekt-a", "frei"), true); // Arbeitsordner weg
        var (e2, l2) = Project(r, "projekt-b");
        var o = await FreePort.RunAsync(MakeJob(e2, l2, recomp, Path.Combine(r, "projekt-b", "frei", "eigen"), lists), FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_b"), null, null, CancellationToken.None);
        output.WriteLine($"{o.Status}: {o.Message}");
        Assert.Equal("fertig", o.Status);
        var runner = Path.Combine(recomp, "ps2xRuntime", "src", "runner");
        Assert.False(File.Exists(Path.Combine(runner, "spiel_a.cpp")));
        Assert.True(File.Exists(Path.Combine(runner, "spiel_b.cpp")));
    }

    [Fact(DisplayName = "T-0757 Fix1 MITTEL 3: fremde Reste im PS2Recomp-Ordner blockieren schon bei den Voraussetzungen – nichts wird gestartet")]
    public async Task Mittel_konflikt_vor_dem_start()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var calls = new List<string>();
        var runner = Path.Combine(recomp, "ps2xRuntime", "src", "runner"); Directory.CreateDirectory(runner);
        var victim = Path.Combine(runner, "fremd.cpp"); File.WriteAllText(victim, "// fremd");
        File.WriteAllText(Path.Combine(recomp, "ps2xRuntime", FreePort.Manifest), $"src/runner/fremd.cpp\t{FreePort.Sha256(victim)}\n");
        var (e, l) = Project(r, "projekt");
        var o = await FreePort.RunAsync(MakeJob(e, l, recomp, Path.Combine(r, "arbeit"), Path.Combine(r, "listen")), FreePortTests.AllThere, FreePortTests.FakeTools(calls), null, null, CancellationToken.None);
        output.WriteLine($"{o.Status}: {o.Message} · {calls.Count} Aufrufe");
        Assert.Equal("blockiert", o.Status);
        Assert.Empty(calls);
        Assert.True(File.Exists(victim));
    }

    [Fact(DisplayName = "T-0757 Fix1 NIEDRIG 4: weitere Weitergabeverbote und verneinte Lizenzkennzeichen werden abgewiesen")]
    public void Niedrig_lizenzwoerter_und_verneinung()
    {
        var r = Root(); var elf = FreePortTests.Elf(r);
        foreach (var head in new[]
        {
            "Weitergabe verboten.", "Do not distribute.", "Not for redistribution.", "Redistribution prohibited.", "For internal use only.",
            "Vervielfältigung untersagt.", "End User License Agreement", "This game is NOT CC0.", "Dieses Spiel steht nicht unter CC0.",
            "This software is not licensed under the MIT license.",
        })
        {
            var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, head + "\n\n" + FreePortTests.Mit);
            Assert.False(FreePort.CheckLicense(lic, Own, elf).Ok, head);
        }
        File.WriteAllText(Path.Combine(r, "LICENSE"), FreePortTests.Mit);
        Assert.Equal((true, "MIT"), FreePort.CheckLicense(Path.Combine(r, "LICENSE"), Own, elf));
    }

    [Fact(DisplayName = "T-0757 Fix1 NIEDRIG 6: BOOT2 mit Unterordner oder doppeltem Backslash zeigt auf eine Spiel-Disc")]
    public void Niedrig_boot2_unterordner()
    {
        var r = Root(); var elf = FreePortTests.Elf(r, "MEIN.ELF");
        foreach (var boot in new[] { @"cdrom0:\DATA\SLUS_200.62;1", @"cdrom0:\\SLUS_200.62;1", @"cdrom0:\\DATA\\SCES_501.23;1", "cdrom0:/A/B/SLPM_652.34;1" })
        {
            File.WriteAllText(Path.Combine(r, "SYSTEM.CNF"), $"BOOT2 = {boot}\nVER = 1.00\n");
            Assert.Contains("SYSTEM.CNF", FreePort.CheckElf(elf) ?? "(frei)");
        }
        File.WriteAllText(Path.Combine(r, "SYSTEM.CNF"), "BOOT2 = cdrom0:\\SPIEL\\MEIN.ELF;1\n");
        Assert.Null(FreePort.CheckElf(elf));
    }
}
