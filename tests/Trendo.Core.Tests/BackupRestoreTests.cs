using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0214: Wiederherstellen stürzt nicht ab.
public class BackupRestoreTests
{
    [Fact(DisplayName = "T-0214: Zwei Wiederherstellungen in derselben Sekunde + schreibgeschützte Datei – kein Absturz")]
    public void Restore_twice_same_second_and_readonly()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var t = new DateTime(2026, 10, 22, 3, 0, 0);
        var ro = Path.Combine(root, "Projekte", ".git", "objects", "ab", "cd");
        Directory.CreateDirectory(Path.GetDirectoryName(ro)!); File.WriteAllText(ro, "alt");
        File.SetAttributes(ro, FileAttributes.ReadOnly);
        var z = b.Create(t);
        File.SetAttributes(ro, FileAttributes.Normal); File.WriteAllText(ro, "neu"); File.SetAttributes(ro, FileAttributes.ReadOnly);
        var n1 = b.Restore(z, t); var n2 = b.Restore(z, t);
        Assert.True(n1 > 0); Assert.Equal(n1, n2);
        Assert.Equal("alt", File.ReadAllText(ro));
        Assert.Equal(b.List().Count, b.List().Select(x => x.Name).Distinct().Count());
        Assert.True(f.Core.Audit.Verify());
    }
}
