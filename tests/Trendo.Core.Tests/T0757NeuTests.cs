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

    [Fact(DisplayName = "T-0757 R3/Fix1: Kopie der Liste je PS2Recomp-Ordner im Datenordner; gelöscht wird nur, was in beiden Listen steht; fremde Reste blockieren vorher")]
    public async Task R3_beide_listen()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var calls = new List<string>();
        var rt = Path.Combine(recomp, "ps2xRuntime"); var lists = Path.Combine(r, "daten", "listen");
        FreePort.Job J(string work, bool rights) => Job(r, recomp, work, rights) with { ListDir = lists };
        Assert.Equal("fertig", (await FreePort.RunAsync(J("a", true), FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_a"), null, null, CancellationToken.None)).Status);
        Assert.Equal(File.ReadAllText(Path.Combine(rt, FreePort.Manifest)), File.ReadAllText(FreePort.ListCopy(lists, recomp)));
        Assert.False(File.Exists(Path.Combine(r, "a", FreePort.Manifest))); // nicht mehr je Arbeitsordner
        // anderer Arbeitsordner, gleicher PS2Recomp-Ordner: alte Dateien werden ersetzt
        Assert.Equal("fertig", (await FreePort.RunAsync(J("b", true), FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_b"), null, null, CancellationToken.None)).Status);
        Assert.False(File.Exists(Path.Combine(rt, "src", "runner", "spiel_a.cpp")));
        Assert.True(File.Exists(Path.Combine(rt, "src", "runner", "spiel_b.cpp")));
        // Trendos Kopie fehlt (z. B. anderer Datenordner): spiel_b.cpp ist fremd – Voraussetzung schlägt fehl, nichts startet, nichts wird gelöscht
        var before = calls.Count;
        var o = await FreePort.RunAsync(Job(r, recomp, "c", true) with { ListDir = Path.Combine(r, "andere-daten") }, FreePortTests.AllThere, FreePortTests.FakeTools(calls, "spiel_c"), null, null, CancellationToken.None);
        Assert.Equal("blockiert", o.Status);
        Assert.Contains("nicht als eigene belegen", o.Message);
        Assert.Equal(before, calls.Count);
        Assert.True(File.Exists(Path.Combine(rt, "src", "runner", "spiel_b.cpp")));
        Assert.Contains(FreePort.Prerequisites(Own, "", "", recomp, FreePortTests.AllThere, Path.Combine(r, "andere-daten")), c => c.Id == "reste" && !c.Ok);
        // Aktion „Alte Trendo-Dateien entfernen“: nur, was in beiden Listen steht
        var victim = Path.Combine(rt, "src", "runner", "main.cpp"); File.WriteAllText(victim, "// PS2Recomp");
        File.AppendAllText(Path.Combine(rt, FreePort.Manifest), $"src/runner/main.cpp\t{FreePort.Sha256(victim)}\n"); // untergeschoben, nur in einer Liste
        var removed = FreePort.RemoveOld(recomp, lists);
        Assert.Equal(["include/spiel_b.h", "src/runner/spiel_b.cpp"], removed.Order().ToArray());
        Assert.False(File.Exists(Path.Combine(rt, "src", "runner", "spiel_b.cpp")));
        Assert.True(File.Exists(victim));
        Assert.Equal(["src/runner/main.cpp"], FreePort.Inspect(recomp, lists).Foreign);
    }

    static string ListBoth(string recomp, string lists, string content)
    {
        File.WriteAllText(Path.Combine(recomp, "ps2xRuntime", FreePort.Manifest), content);
        Directory.CreateDirectory(lists); File.WriteAllText(FreePort.ListCopy(lists, recomp), content);
        var outDir = Path.Combine(Path.GetDirectoryName(recomp)!, "aus-" + Guid.NewGuid().ToString("N")[..4]); Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "neu.cpp"), "// neu");
        return outDir;
    }

    [Fact(DisplayName = "T-0757 Fix1 NIEDRIG 7: Pfad-Regel – Eintrag außerhalb von src/runner bzw. include löscht nichts, auch wenn er in beiden Listen steht")]
    public void Manifest_pfadregel()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var lists = Path.Combine(r, "listen");
        var victim = Path.Combine(recomp, "ps2xRuntimeAlt", "opfer.txt"); Directory.CreateDirectory(Path.GetDirectoryName(victim)!); File.WriteAllText(victim, "x");
        var cmake = Path.Combine(recomp, "ps2xRuntime", "CMakeLists.txt"); File.WriteAllText(cmake, "x");
        var outDir = ListBoth(recomp, lists, $"../ps2xRuntimeAlt/opfer.txt\t{FreePort.Sha256(victim)}\nCMakeLists.txt\t{FreePort.Sha256(cmake)}\nsrc/runner/sub/x.cpp\t{FreePort.Sha256(cmake)}\n");
        FreePort.Distribute(outDir, recomp, lists);
        Assert.True(File.Exists(victim)); Assert.True(File.Exists(cmake));
    }

    [Fact(DisplayName = "T-0757 Fix1 NIEDRIG 7: Prüfsumme – eine nachträglich geänderte Datei wird weder gelöscht noch überschrieben, auch wenn sie in beiden Listen steht")]
    public void Manifest_pruefsumme()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var lists = Path.Combine(r, "listen");
        var f = Path.Combine(recomp, "ps2xRuntime", "src", "runner", "neu.cpp"); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, "// original");
        var sha = FreePort.Sha256(f);
        var outDir = ListBoth(recomp, lists, $"src/runner/neu.cpp\t{sha}\n");
        File.WriteAllText(f, "// von Hand geändert");
        Assert.Throws<IOException>(() => FreePort.Distribute(outDir, recomp, lists));
        Assert.Equal("// von Hand geändert", File.ReadAllText(f));
        File.Delete(f);
        var g = Path.Combine(recomp, "ps2xRuntime", "src", "runner", "alt.cpp"); File.WriteAllText(g, "// alt");
        var outDir2 = ListBoth(recomp, lists, $"src/runner/alt.cpp\t{new string('0', 64)}\nsrc/runner/alt.cpp\n");
        FreePort.Distribute(outDir2, recomp, lists);
        Assert.True(File.Exists(g)); // falsche oder fehlende Prüfsumme: nicht gelöscht
    }

    [Fact(DisplayName = "T-0757 Fix1 NIEDRIG 7: Präfix – „..“ im Eintrag führt nicht aus ps2xRuntime heraus, auch in beiden Listen")]
    public void Manifest_praefix()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r); var lists = Path.Combine(r, "listen");
        var victim = Path.Combine(recomp, "x.cpp"); File.WriteAllText(victim, "x");
        var outDir = ListBoth(recomp, lists, $"src/runner/../../x.cpp\t{FreePort.Sha256(victim)}\ninclude/../../x.cpp\t{FreePort.Sha256(victim)}\n");
        Assert.Equal(["src/runner/neu.cpp"], FreePort.Distribute(outDir, recomp, lists));
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

    [Fact(DisplayName = "T-0757 Fix1 C: breite Erkennung ohne Liste der Startprogramme, nur echte Nicht-Ausführung (echo, rem, --shell) ausgenommen")]
    public void C_breit_mit_ausnahmen()
    {
        const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
        Assert.True(SystemScan.EncodedInnerPowerShell($"/c p^owershell -e^nc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"process call create \"powershell -enc {B64}\""));
        Assert.True(SystemScan.EncodedInnerPowerShell($"powershell.exe -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"/c mytool --use powershell -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"/c echo x & powershell -enc {B64}"));
        Assert.True(SystemScan.EncodedInnerPowerShell($"/c \"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -enc {B64}"));
        Assert.False(SystemScan.EncodedInnerPowerShell($"tool --shell powershell -e {B64}"));
        Assert.False(SystemScan.EncodedInnerPowerShell($"/c echo powershell -e {B64}"));
        Assert.False(SystemScan.EncodedInnerPowerShell($"/c rem powershell -enc {B64}"));
        Assert.False(SystemScan.EncodedInnerPowerShell($"/c echo powershell -enc {B64} | more"));
        Assert.False(SystemScan.EncodedInnerPowerShell("/c powershell -File script.ps1"));
    }
}
