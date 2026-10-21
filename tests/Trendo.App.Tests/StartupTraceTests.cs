using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

public class StartupTraceTests
{
    [Fact]
    public void Messpunkte_steigen_und_Log_wird_geschrieben()
    {
        StartupTrace.Mark("t.a"); Thread.Sleep(5); StartupTrace.Mark("t.b");
        Assert.True(StartupTrace.Of("t.b") > StartupTrace.Of("t.a"));
        var f = Path.Combine(Path.GetTempPath(), "trendo-st-" + Guid.NewGuid().ToString("N") + ".txt");
        StartupTrace.LogPath = f; StartupTrace.Flush(); StartupTrace.LogPath = null;
        Assert.Contains(File.ReadAllLines(f), l => l.StartsWith("t.b"));
        File.Delete(f);
    }
}
