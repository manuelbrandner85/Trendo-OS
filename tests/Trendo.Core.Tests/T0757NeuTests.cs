using Trendo.Core;
using Trendo.Core.ConsolePort;
using Trendo.Core.Shell;
using Trendo.Core.Sys;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0757: Tests der neuen Schnittstellen (Bestätigung bei „Eigenes Homebrew“, Liste im Arbeitsordner, Windows-Formel, Laufwerkspfad).
/// Auf main übersetzt diese Datei nicht (Schnittstellen fehlen); das Verhalten selbst prüft <see cref="T0757RestpunkteTests"/> auch auf main.</summary>
public class T0757NeuTests(ITestOutputHelper output)
{
    static string Root() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0757n-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static FreePort.Title Own => FreePort.Find(FreePort.OwnId)!;

    static FreePort.Job Job(string r, string recomp, string work, bool rights)
    {
        var proj = Path.Combine(r, "projekt"); Directory.CreateDirectory(proj);
        var lic = Path.Combine(proj, "LICENSE"); File.WriteAllText(lic, FreePortTests.Mit);
        return new(Own, FreePortTests.Elf(proj), lic, recomp, [], Path.Combine(r, work), rights);
    }

    [Fact(DisplayName = "T-0757 R1: „Eigenes Homebrew“ startet nur mit „Ich habe die Rechte an diesem Programm“; die Bestätigung steht in ergebnis.json")]
    public async Task R1_bestaetigung()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var calls = new List<string>();
        var o = await FreePort.RunAsync(Job(r, recomp, "arbeit", false), FreePortTests.AllThere, FreePortTests.FakeTools(calls), null, null, CancellationToken.None);
        Assert.Equal("blockiert", o.Status);
        Assert.Contains(FreePort.RightsText, o.Message);
        Assert.Empty(calls);
        o = await FreePort.RunAsync(Job(r, recomp, "arbeit", true), FreePortTests.AllThere, FreePortTests.FakeTools(calls), null, null, CancellationToken.None);
        Assert.Equal("fertig", o.Status);
        Assert.Contains($"\"rechte_bestaetigt\": \"{FreePort.RightsText}\"", File.ReadAllText(o.ResultPath!));
        Assert.Contains("Bestätigt", File.ReadAllText(o.LogPath));
        Assert.True(FreePort.NeedsRights(Own));
        Assert.False(FreePort.NeedsRights(FreePort.Find("cubes-demo")!)); // Katalogtitel: über Prüfsummen belegt
    }

    [Fact(DisplayName = "T-0757 R3: Liste auch im Arbeitsordner; gelöscht wird nur, was in beiden Listen steht; fremde Reste werden weder gelöscht noch gemischt")]
    public async Task R3_beide_listen()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var calls = new List<string>();
        var rt = Path.Combine(recomp, "ps2xRuntime");
        Assert.Equal("fertig", (await FreePort.RunAsync(Job(r, recomp, "a", true), FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_a"), null, null, CancellationToken.None)).Status);
        Assert.Equal(File.ReadAllText(Path.Combine(rt, FreePort.Manifest)), File.ReadAllText(Path.Combine(r, "a", FreePort.Manifest)));
        // gleicher Arbeitsordner: alte Dateien werden ersetzt
        Assert.Equal("fertig", (await FreePort.RunAsync(Job(r, recomp, "a", true), FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_b"), null, null, CancellationToken.None)).Status);
        Assert.False(File.Exists(Path.Combine(rt, "src", "runner", "spiel_a.cpp")));
        Assert.True(File.Exists(Path.Combine(rt, "src", "runner", "spiel_b.cpp")));
        // anderer Arbeitsordner (andere Liste): spiel_b.cpp gehört ihm nicht – nichts löschen, nichts mischen
        var o = await FreePort.RunAsync(Job(r, recomp, "anderes-projekt", true), FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_c"), null, null, CancellationToken.None);
        Assert.Equal("fehler", o.Status);
        Assert.Contains("nicht selbst einsortiert", o.Message);
        Assert.True(File.Exists(Path.Combine(rt, "src", "runner", "spiel_b.cpp")));
        Assert.False(File.Exists(Path.Combine(rt, "src", "runner", "spiel_c.cpp")));
        // Liste nur im Arbeitsordner, nicht im PS2Recomp-Ordner: auch dann wird nichts gelöscht
        var outDir = Path.Combine(r, "aus"); Directory.CreateDirectory(outDir); File.WriteAllText(Path.Combine(outDir, "neu.cpp"), "// neu");
        var victim = Path.Combine(rt, "src", "runner", "main.cpp"); File.WriteAllText(victim, "// PS2Recomp");
        var work = Directory.CreateDirectory(Path.Combine(r, "w2")).FullName;
        File.WriteAllText(Path.Combine(work, FreePort.Manifest), $"src/runner/main.cpp\t{FreePort.Sha256(victim)}\n");
        File.Delete(Path.Combine(rt, FreePort.Manifest));
        Assert.Equal(["src/runner/neu.cpp"], FreePort.Distribute(outDir, recomp, work));
        Assert.True(File.Exists(victim));
    }

    [Fact(DisplayName = "T-0757 B: Windows-Voreinstellung des Papierkorbs = min(40 GB, Größe)·10 % + max(0, Größe − 40 GB)·5 %")]
    public void B_formel()
    {
        const long G = 1L << 30;
        Assert.Equal(2 * G, FileBrowser.WindowsDefaultBinBytes(20 * G));
        Assert.Equal(4 * G, FileBrowser.WindowsDefaultBinBytes(40 * G));
        Assert.Equal(4 * G + 3 * G, FileBrowser.WindowsDefaultBinBytes(100 * G));
        Assert.Equal(4 * G + 48 * G, FileBrowser.WindowsDefaultBinBytes(1000 * G));
        Assert.Equal(0, FileBrowser.WindowsDefaultBinBytes(0));
    }

    [Fact(DisplayName = "T-0757 B: Reparse-Point als Element hat Größe 0; Laufwerkspfad über GetVolumePathName (echt, nur lesend)")]
    public void B_groesse_und_laufwerk()
    {
        var r = Root();
        try
        {
            File.WriteAllBytes(Path.Combine(r, "a.bin"), new byte[123]);
            Assert.Equal(123, FileBrowser.ItemBytes(r));
            if (!OperatingSystem.IsWindows()) return;
            var drive = Path.GetPathRoot(r)!;
            Assert.Equal(drive, FileBrowser.VolumePath(Path.Combine(r, "a.bin")), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(drive, FileBrowser.VolumePath(Path.Combine(r, "gibt-es-nicht", "x.txt")), StringComparer.OrdinalIgnoreCase);
            var p = FileBrowser.ReadWindowsPolicy(Path.Combine(r, "a.bin"));
            Assert.NotNull(p);
            output.WriteLine($"Papierkorb {drive}: sofort löschen = {p!.NukeOnDelete}, höchstens {FileBrowser.SizeText(p.MaxBytes ?? 0)}");
            var link = Path.Combine(r, "verknuepfung");
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, r]) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
            proc.WaitForExit(10_000);
            Assert.True(Directory.Exists(link));
            Assert.Equal(0, FileBrowser.ItemBytes(link));
            Assert.Equal(drive, FileBrowser.VolumePath(link), StringComparer.OrdinalIgnoreCase);
            Directory.Delete(link);
        }
        finally { Directory.Delete(r, true); }
    }

    [Fact(DisplayName = "T-0757 C: innere PowerShell-Prüfung nur für cmd, conhost, forfiles, explorer und wmic")]
    public void C_nur_startprogramme()
    {
        const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
        Assert.True(SystemScan.EncodedInnerPowerShell(@"C:\Windows\System32\cmd.exe", $"/c p^owershell -e^nc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell(@"C:\Windows\System32\wbem\wmic.exe", $"process call create \"powershell -enc {B64}\""));
        Assert.True(SystemScan.EncodedInnerPowerShell(@"C:\Windows\explorer.exe", $"powershell.exe -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell(@"C:\Windows\System32\forfiles.exe", $"/p C:\\Windows /m win.ini /c \"cmd /c p^owershell -enc {B64}\""));
        Assert.False(SystemScan.EncodedInnerPowerShell(@"C:\Program Files\dotnet\dotnet.exe", $"tool --shell powershell -e {B64}"));
        Assert.False(SystemScan.EncodedInnerPowerShell(@"C:\Windows\System32\cmd.exe", $"/c echo powershell -e {B64}"));
        Assert.False(SystemScan.EncodedInnerPowerShell(@"C:\Windows\System32\cmd.exe", $"/c mytool --use powershell -enc {B64}"));
    }
}
