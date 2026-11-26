using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0770: lokale Update-Quelle für Abnahmen nur über eine Umgebungsvariable mit vorhandenem Ordner.</summary>
public class T0770UpdateQuelleTests
{
    [Fact(DisplayName = "T-0770: lokale Update-Quelle zählt nur als vorhandener Ordner mit vollem Pfad")]
    public void Local_source_only_existing_full_path()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-t0770-quelle-").FullName;
        try
        {
            Assert.Equal(Path.GetFullPath(dir), VelopackUpdater.LocalSourceDir(dir));
            Assert.Equal(Path.GetFullPath(dir), VelopackUpdater.LocalSourceDir($"\"{dir}\""));
            Assert.Null(VelopackUpdater.LocalSourceDir(Path.Combine(dir, "fehlt")));
            Assert.Null(VelopackUpdater.LocalSourceDir("relativ"));
            Assert.Null(VelopackUpdater.LocalSourceDir("https://example.org/feed"));
            Assert.Null(VelopackUpdater.LocalSourceDir(""));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0770: ohne Umgebungsvariable bleibt GitHub die Quelle")]
    public void Without_variable_no_local_source()
    {
        if (Environment.GetEnvironmentVariable(VelopackUpdater.LocalSourceVar) is { Length: > 0 }) return;
        Assert.Null(VelopackUpdater.LocalSourceDir());
    }
}
