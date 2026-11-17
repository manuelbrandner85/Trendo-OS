using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0729: <see cref="MediaStudio.RunInto"/> lässt nach Fehler oder Zeitüberschreitung keine „.~xxxx.teil.&lt;Endung&gt;“ im Ordner des Nutzers liegen.</summary>
public class MediaRunIntoTests
{
    /// <summary>Ersatz für FFmpeg: schreibt die Zwischendatei (letztes Argument) und scheitert dann bzw. hängt.</summary>
    static string FakeFfmpeg(string dir, bool hang)
    {
        var f = Path.Combine(dir, hang ? "haengt.cmd" : "scheitert.cmd");
        File.WriteAllText(f, "@echo off\r\necho halb>\"%~1\"\r\n" + (hang ? "ping -n 30 127.0.0.1 >nul\r\n" : "") + "exit /b 3\r\n");
        return f;
    }

    [Theory(DisplayName = "T-0729: RunInto räumt die Zwischendatei nach Fehler und Zeitüberschreitung weg")]
    [InlineData(false)]
    [InlineData(true)]
    public void Leaves_no_part_file(bool hang)
    {
        if (!OperatingSystem.IsWindows()) return; // Ersatz-FFmpeg ist ein Windows-Skript
        using var t = TempDir.Create("trendo-ui-");
        var ff = FakeFfmpeg(t.Path, hang);
        var output = Path.Combine(t.Path, "ergebnis.mp4");
        var r = MediaStudio.RunInto(ff, [output], output, t.Path, timeoutMs: hang ? 1500 : 30000);
        Assert.False(r.Ok);
        if (hang) Assert.Equal(-2, r.Exit);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(t.Path, "*.teil*"));
    }
}
