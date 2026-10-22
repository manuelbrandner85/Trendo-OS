using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

public class Startargumente
{
    [Fact(DisplayName = "Start: Velopack-Schalter kommen nicht in der Oberfläche an, eigene Schalter bleiben")]
    public void Velopack_args_werden_ausgefiltert()
    {
        var ui = AppStartup.WithoutVelopack(
        [
            "--veloapp-install", "0.9.6",
            "--velopack-restarted",
            "--squirrel-firstrun", "1",
            "--offline",
            "--data-root", "/tmp/trendo",
            "--no-first-run",
        ]);
        Assert.Equal(["--offline", "--data-root", "/tmp/trendo", "--no-first-run"], ui);
        Assert.False(AppStartup.VelopackFirstRun);
        AppStartup.NoteFirstRun();
        Assert.True(AppStartup.VelopackFirstRun);
    }
}
