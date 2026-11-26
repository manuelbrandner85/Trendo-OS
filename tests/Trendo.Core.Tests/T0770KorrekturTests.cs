using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0770 Korrekturrunde: M4 (Prüf-Instanz nie auf/über/unter den echten Ordnern) und M5c (ungeprüfte Version ersetzt die gute Sicherung nicht).</summary>
public class T0770KorrekturTests
{
    static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static string Profile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static TheoryData<string> Forbidden() => new()
    {
        Local, Roaming, Profile, Path.GetPathRoot(Local)!,
        Path.Combine(Local, "TrendoOS"), Path.Combine(Local, "TrendoOS", "unter"),
        Path.Combine(Roaming, "TrendoOS"), Path.Combine(Roaming, "TrendoOS", "daten", "tief"),
        Path.Combine(Local, "TrendoWerkzeuge"), Path.Combine(Local, "TrendoWerkzeuge", "x"),
        Local + Path.DirectorySeparatorChar, Path.Combine(Local, "TrendoOS") + Path.DirectorySeparatorChar + ".",
    };

    [Theory(DisplayName = "T-0770 M4: Prüf-Instanz auf, über oder unter den echten Trendo-Ordnern wird abgelehnt")]
    [MemberData(nameof(Forbidden))]
    public void Instance_rejects_real_folders(string value) => Assert.Null(DataLocation.Instance(value));

    [Fact(DisplayName = "T-0770 M4: eigener Ordner im Temp (neben den echten) bleibt erlaubt")]
    public void Instance_allows_own_folder()
    {
        var own = Path.Combine(Path.GetTempPath(), "trendo-auftrag-T-0770", "instanz");
        Assert.Equal(Path.GetFullPath(own), DataLocation.Instance(own));
        Assert.Equal(Path.GetFullPath(Path.Combine(Local, "TrendoOS-Abnahme")), DataLocation.Instance(Path.Combine(Local, "TrendoOS-Abnahme"))); // Geschwister, nicht darunter
        Assert.Null(DataLocation.Instance(@"\\server\freigabe\instanz")); // Netzpfade nicht
    }

    [Fact(DisplayName = "T-0770 M5c: läuft die Version noch nicht als gut, bleibt die vorhandene Sicherung der guten Version")]
    public void Unverified_version_keeps_good_backup()
    {
        var root = Directory.CreateTempSubdirectory("trendo-t0770-boot-").FullName;
        try
        {
            var g = new BootGuard(root);
            Assert.True(g.MayReplaceBackup("1.0")); // noch keine Sicherung: sichern erlaubt
            Directory.CreateDirectory(g.BackupDir); File.WriteAllText(Path.Combine(g.BackupDir, "App-0.9-full.nupkg"), "gut");
            g.Begin("1.0");
            Assert.False(g.MayReplaceBackup("1.0")); // 1.0 noch ungeprüft, gute Sicherung bleibt
            g.MarkOk("1.0");
            Assert.True(g.MayReplaceBackup("1.0")); // jetzt gut: darf ersetzen
            g.Begin("1.1");
            Assert.False(g.MayReplaceBackup("1.1"));
        }
        finally { Directory.Delete(root, true); }
    }
}
