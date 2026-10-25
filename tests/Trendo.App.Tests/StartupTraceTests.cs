using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>D7: --startup-log schreibt die Zeitpunkte, auch wenn noch kein Bild gezeichnet wurde.</summary>
public class StartupTraceTests
{
    [Fact(DisplayName = "D7: Startprotokoll wird geschrieben und enthält die gesetzten Zeitpunkte")]
    public void Flush_schreibt_datei()
    {
        var file = Path.Combine(Path.GetTempPath(), "trendo-startlog-" + Guid.NewGuid().ToString("N") + ".txt");
        var old = StartupTrace.LogPath;
        try
        {
            StartupTrace.LogPath = file;
            StartupTrace.Mark("test.marke");
            StartupTrace.Flush();
            Assert.Contains(File.ReadAllLines(file), l => l.StartsWith("test.marke") && l.TrimEnd().EndsWith("ms"));
        }
        finally { StartupTrace.LogPath = old; File.Delete(file); }
    }
}
