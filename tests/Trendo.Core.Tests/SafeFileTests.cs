using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Sicheres Ersetzen: hält ein anderes Programm (Virenscanner, Suchindex) die Zieldatei kurz fest, geht das Speichern nicht verloren.
public class SafeFileTests
{
    [Fact(DisplayName = "SafeFile: kurz gesperrte Zieldatei → wartet und ersetzt; fehlende Quelle meldet sofort")]
    public void Retries_while_locked()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-safefile-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var dest = Path.Combine(dir, "daten.json"); File.WriteAllText(dest, "alt");
            var tmp = dest + ".tmp"; File.WriteAllText(tmp, "neu");
            var held = new ManualResetEventSlim();
            var holder = Task.Run(() => { using var f = new FileStream(dest, FileMode.Open, FileAccess.Read, FileShare.None); held.Set(); Thread.Sleep(400); });
            held.Wait();
            SafeFile.Move(tmp, dest);
            holder.Wait();
            Assert.Equal("neu", File.ReadAllText(dest)); Assert.False(File.Exists(tmp));
            Assert.Throws<FileNotFoundException>(() => SafeFile.Move(Path.Combine(dir, "fehlt.tmp"), dest));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "SafeFile: Ordner umbenennen gelingt, auch wenn eine Datei darin kurz festgehalten wird")]
    public void Move_directory_waits_for_lock()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-safefile-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var src = Directory.CreateDirectory(Path.Combine(root, "neu")).FullName; var dst = Path.Combine(root, "fertig");
            var f = Path.Combine(src, "datei.txt"); File.WriteAllText(f, "x");
            var held = new ManualResetEventSlim();
            var holder = Task.Run(() => { using var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.None); held.Set(); Thread.Sleep(300); }); // wie ein Scanner, der die Datei gerade liest
            held.Wait();
            SafeFile.MoveDirectory(src, dst);
            holder.Wait();
            Assert.True(File.Exists(Path.Combine(dst, "datei.txt"))); Assert.False(Directory.Exists(src));
            Assert.Throws<DirectoryNotFoundException>(() => SafeFile.MoveDirectory(Path.Combine(root, "fehlt"), Path.Combine(root, "x")));
        }
        finally { Directory.Delete(root, true); }
    }
}
