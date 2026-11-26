using System.Diagnostics;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0770 Korrekturrunde 2: M-A (Verknüpfungen/Junctions umgehen den Instanz-Schutz nicht), M-B (Rückkehr nimmt die tatsächlich
/// gesicherte Version), N-b (fehlende/kaputte start.json blockiert die Erneuerung der Sicherung nicht dauerhaft).</summary>
public class T0770Korrektur2Tests
{
    /// <summary>Echte Junction ohne Adminrechte (mklink /J). Entfernt wird später nur die Verknüpfung, nie ihr Ziel.</summary>
    static bool Junction(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/d /c mklink /J \"{link}\" \"{target}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.WaitForExit(10_000);
        return p.ExitCode == 0 && new DirectoryInfo(link).LinkTarget is not null;
    }

    /// <summary>Nur die Verknüpfung entfernen: Directory.Delete ohne Rekursion löscht bei einer Junction nur den Verweis.</summary>
    static void RemoveLink(string link) { if (new DirectoryInfo(link) is { Exists: true, LinkTarget: not null }) Directory.Delete(link, recursive: false); }

    // Korrektur 3 (N-6): der frühere Test mit einer Junction auf das echte %LOCALAPPDATA% ist ersetzt durch
    // T0770Korrektur3Tests.Junction_to_artificial_real_rejected (nur künstliche Ziele im Temp).

    [Fact(DisplayName = "T-0770 M-A: Werkzeugordner hinter einer Junction in der Elternkette wird beim Deinstallieren nicht gelöscht")]
    public void Removable_tools_rejects_reparse_parent()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tmp = Directory.CreateTempSubdirectory("trendo-t0770-kette-").FullName;
        var real = Path.Combine(tmp, "echt"); Directory.CreateDirectory(Path.Combine(real, DataLocation.ToolsFolderName));
        File.WriteAllText(Path.Combine(real, DataLocation.ToolsFolderName, "bleibt.txt"), "x");
        var link = Path.Combine(tmp, "verweis");
        try
        {
            Assert.True(Junction(link, real), "Junction konnte nicht angelegt werden");
            var viaLink = Path.Combine(link, DataLocation.ToolsFolderName);
            Assert.Null(DataLocation.RemovableTools(viaLink));
            Assert.Equal(-1, DataLocation.RemoveTools(viaLink));
            Assert.True(File.Exists(Path.Combine(real, DataLocation.ToolsFolderName, "bleibt.txt")));
            Assert.NotNull(DataLocation.RemovableTools(Path.Combine(real, DataLocation.ToolsFolderName))); // direkter Pfad bleibt erlaubt
        }
        finally { RemoveLink(link); Directory.Delete(tmp, recursive: true); }
    }

    [Fact(DisplayName = "T-0770 N-b: fehlende oder kaputte start.json blockiert das Erneuern der Sicherung nicht dauerhaft")]
    public void Missing_or_broken_state_does_not_block()
    {
        var root = Directory.CreateTempSubdirectory("trendo-t0770-state-").FullName;
        try
        {
            var g = new BootGuard(root);
            Directory.CreateDirectory(g.BackupDir); File.WriteAllText(Path.Combine(g.BackupDir, "TrendoOS-1.0-full.nupkg"), "alt");
            Assert.True(g.MayReplaceBackup("2.0")); // keine start.json: kein Wissen über „gut“ – nicht für immer blockieren
            File.WriteAllText(Path.Combine(root, "updates", "start.json"), "{kaputt");
            Assert.True(g.MayReplaceBackup("2.0"));
            g.Begin("2.0"); // jetzt gibt es Wissen: 2.0 ungeprüft → gute Sicherung bleibt (M5c)
            Assert.False(g.MayReplaceBackup("2.0"));
        }
        finally { Directory.Delete(root, true); }
    }
}
