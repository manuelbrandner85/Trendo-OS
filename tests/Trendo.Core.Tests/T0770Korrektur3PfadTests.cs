using System.Diagnostics;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0770 Korrekturrunde 3 – Teil mit bestehender Schnittstelle (Verhaltens-Rot auf altem Code): FinalPath löst subst auf,
/// MigrateLegacy folgt keiner Verknüpfung. Nur künstliche Ziele im Temp.</summary>
public class T0770Korrektur3PfadTests
{
    static int Cmd(string args)
    {
        using var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /c " + args)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.StandardOutput.ReadToEnd(); p.WaitForExit(15_000); return p.ExitCode;
    }
    static string SubstList()
    {
        using var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "subst.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        var o = p.StandardOutput.ReadToEnd(); p.WaitForExit(10_000); return o;
    }
    static bool Junction(string link, string target) => Cmd($"mklink /J \"{link}\" \"{target}\"") == 0 && new DirectoryInfo(link).LinkTarget is not null;
    static void RemoveLink(string link) { if (new DirectoryInfo(link) is { Exists: true, LinkTarget: not null }) Directory.Delete(link, recursive: false); }
    static char? FreeLetter() { var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet(); for (var c = 'W'; c >= 'K'; c--) if (!used.Contains(c)) return c; return null; }

    [Fact(DisplayName = "T-0770 M-1: FinalPath löst ein subst-Laufwerk auf den echten Ordner auf")]
    public void FinalPath_resolves_subst()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tmp = Directory.CreateTempSubdirectory("trendo-t0770-subst2-").FullName;
        var echt = Path.Combine(tmp, "echt"); Directory.CreateDirectory(Path.Combine(echt, "TrendoOS"));
        var drive = $"{FreeLetter() ?? throw new InvalidOperationException("kein freier Laufwerksbuchstabe")}:";
        try
        {
            Assert.Equal(0, Cmd($"subst {drive} \"{echt}\""));
            Assert.Equal(DataLocation.FinalPath(Path.Combine(echt, "TrendoOS")), DataLocation.FinalPath(drive + @"\TrendoOS"));
            Assert.Equal(DataLocation.FinalPath(echt), DataLocation.FinalPath(drive + @"\"));
            Assert.Equal(DataLocation.FinalPath(Path.Combine(echt, "neu", "tief")), DataLocation.FinalPath(drive + @"\neu\tief")); // nicht vorhandene Teile angehängt
        }
        finally
        {
            Cmd($"subst {drive} /d");
            Assert.DoesNotContain(drive, SubstList(), StringComparison.OrdinalIgnoreCase);
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact(DisplayName = "T-0770 M-2: MigrateLegacy verschiebt nichts, wenn der Altordner eine Verknüpfung ist oder in einer liegt")]
    public void Migrate_refuses_links()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tmp = Directory.CreateTempSubdirectory("trendo-t0770-migr-").FullName;
        var fremd = Path.Combine(tmp, "fremd"); Directory.CreateDirectory(Path.Combine(fremd, "alt")); File.WriteAllText(Path.Combine(fremd, "alt", "daten.txt"), "bleibt");
        File.WriteAllText(Path.Combine(fremd, "einstellung.txt"), "bleibt");
        var inst = Path.Combine(tmp, "inst"); Directory.CreateDirectory(inst);
        var legacy = Path.Combine(inst, "TrendoOS-alt"); var kette = Path.Combine(tmp, "kette");
        try
        {
            Assert.True(Junction(legacy, fremd));
            Assert.Equal(0, DataLocation.MigrateLegacy(legacy, Path.Combine(inst, "TrendoOS")));
            Assert.True(File.Exists(Path.Combine(fremd, "einstellung.txt")));
            Assert.True(Junction(kette, fremd)); // Verknüpfung in der Kette über dem Altordner
            Assert.Equal(0, DataLocation.MigrateLegacy(Path.Combine(kette, "alt"), Path.Combine(inst, "TrendoOS2")));
            Assert.True(File.Exists(Path.Combine(fremd, "alt", "daten.txt")));
        }
        finally { RemoveLink(legacy); RemoveLink(kette); Directory.Delete(tmp, recursive: true); }
    }
}
