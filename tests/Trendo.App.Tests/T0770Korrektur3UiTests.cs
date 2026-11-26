using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0770 Korrekturrunde 3, N-2: Die Sicherung schreibt nur in Trendos Sicherungsordner „updates/vorherige-version“.</summary>
public class T0770Korrektur3UiTests
{
    [Fact(DisplayName = "T-0770 N-2: ReplaceBackup lehnt einen anderen Zielordner ab und schreibt dort nichts")]
    public void Replace_backup_only_own_folder()
    {
        var root = Directory.CreateTempSubdirectory("trendo-t0770-ziel-").FullName;
        try
        {
            var src = Path.Combine(root, "TrendoOS-1.1-full.nupkg"); File.WriteAllText(src, "neu");
            foreach (var wrong in new[] { Path.Combine(root, "irgendwo"), Path.Combine(root, "vorherige-version"), Path.Combine(root, "updates") })
            {
                Directory.CreateDirectory(wrong); File.WriteAllText(Path.Combine(wrong, "fremd.nupkg"), "fremd");
                Assert.False(VelopackUpdater.ReplaceBackup(src, wrong, _ => long.MaxValue, out var note));
                Assert.Contains("falscher Zielordner", note);
                Assert.Equal(["fremd.nupkg"], Directory.GetFiles(wrong).Select(Path.GetFileName).ToArray()); // nichts gelöscht, nichts kopiert
            }
            var ok = Path.Combine(root, "daten", "updates", "vorherige-version");
            Assert.True(VelopackUpdater.ReplaceBackup(src, ok, _ => long.MaxValue, out _));
        }
        finally { Directory.Delete(root, true); }
    }
}
