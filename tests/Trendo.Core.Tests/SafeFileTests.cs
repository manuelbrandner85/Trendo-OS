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
}
