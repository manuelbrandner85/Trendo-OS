using System.Diagnostics;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0770 Korrekturrunde 3: M-1 (subst-Laufwerk), M-2 (Verknüpfung unterhalb einer gültigen Instanz, MigrateLegacy), N-6 (Junction nur auf
/// künstliche Ziele). Alle „echten Ordner“ sind hier künstliche Ordner im Temp – kein Test zielt auf das echte %LOCALAPPDATA%.</summary>
public class T0770Korrektur3Tests
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

    [Fact(DisplayName = "T-0770 M-1: subst-Laufwerk wird auf den echten Pfad aufgelöst – über, auf oder unter einem (künstlichen) echten Ordner abgelehnt")]
    public void Subst_drive_resolved()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tmp = Directory.CreateTempSubdirectory("trendo-t0770-subst-").FullName;
        var echt = Path.Combine(tmp, "echt"); var real = Path.Combine(echt, "TrendoOS"); Directory.CreateDirectory(real);
        var letter = FreeLetter() ?? throw new InvalidOperationException("kein freier Laufwerksbuchstabe");
        var drive = $"{letter}:";
        try
        {
            Assert.Equal(0, Cmd($"subst {drive} \"{echt}\""));
            Assert.Equal(DataLocation.FinalPath(real), DataLocation.FinalPath(drive + @"\TrendoOS")); // aufgelöst wie der echte Pfad
            IReadOnlyList<string> roots = [real];
            Assert.Null(DataLocation.Instance(drive + @"\", roots));          // Laufwerk = Elternordner des echten Ordners
            Assert.Null(DataLocation.Instance(drive + @"\TrendoOS", roots));  // = echter Ordner
            Assert.Null(DataLocation.Instance(drive + @"\TrendoOS\x", roots)); // darunter
            Assert.Equal(DataLocation.FinalPath(Path.Combine(echt, "anders")), DataLocation.Instance(drive + @"\anders", roots)); // Geschwister: erlaubt, aufgelöst
        }
        finally
        {
            Cmd($"subst {drive} /d");
            Assert.DoesNotContain(drive, SubstList(), StringComparison.OrdinalIgnoreCase); // rückstandslos
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact(DisplayName = "T-0770 M-1/N-6: Junction auf einen künstlichen echten Ordner oder dessen Elternordner wird nicht als Instanz angenommen")]
    public void Junction_to_artificial_real_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tmp = Directory.CreateTempSubdirectory("trendo-t0770-j3-").FullName;
        var echt = Path.Combine(tmp, "echt"); var real = Path.Combine(echt, "TrendoWerkzeuge"); Directory.CreateDirectory(real);
        var j1 = Path.Combine(tmp, "auf-eltern"); var j2 = Path.Combine(tmp, "auf-echt");
        IReadOnlyList<string> roots = [real];
        try
        {
            Assert.True(Junction(j1, echt)); Assert.True(Junction(j2, real));
            Assert.Null(DataLocation.Instance(j1, roots));
            Assert.Null(DataLocation.Instance(j2, roots));
            Assert.NotNull(DataLocation.Instance(Path.Combine(tmp, "eigen"), roots));
        }
        finally { RemoveLink(j1); RemoveLink(j2); Assert.True(Directory.Exists(real)); Directory.Delete(tmp, recursive: true); }
    }

    [Fact(DisplayName = "T-0770 M-2: Verknüpfung unterhalb einer gültigen Instanz (TrendoOS-alt, TrendoWerkzeuge, TrendoOS) macht die Instanz ungültig")]
    public void Link_below_instance_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tmp = Directory.CreateTempSubdirectory("trendo-t0770-unter-").FullName;
        var real = Path.Combine(tmp, "echt", "TrendoOS"); Directory.CreateDirectory(real);
        IReadOnlyList<string> roots = [real];
        var links = new List<string>();
        try
        {
            foreach (var sub in new[] { "TrendoOS-alt", DataLocation.ToolsFolderName, "TrendoOS" })
            {
                var inst = Path.Combine(tmp, "inst-" + sub); Directory.CreateDirectory(inst);
                Assert.NotNull(DataLocation.Instance(inst, roots)); // ohne Verknüpfung gültig
                var link = Path.Combine(inst, sub); Assert.True(Junction(link, real)); links.Add(link);
                Assert.Null(DataLocation.Instance(inst, roots));
            }
        }
        finally { foreach (var l in links) RemoveLink(l); Assert.True(Directory.Exists(real)); Directory.Delete(tmp, recursive: true); }
    }
}
