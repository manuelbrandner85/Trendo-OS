using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Jede Trendo-Person bindet ihr eigenes Claude an (Wunsch 01.11.2026): eigene Schlüssel, eigene Claude-Code-Anmeldung, eigenes claude.ai-Profil.
public class PersonScopeTests
{
    static string Root() { var d = Path.Combine(Path.GetTempPath(), "trendo-person-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Personen: Kennung aus dem Datenordner – Hauptperson ohne, weitere Person mit eigener Kennung")]
    public void Id_of_data_root()
    {
        var b = Root();
        Assert.Null(PersonStore.IdOf(b));
        Assert.Equal("p1", PersonStore.IdOf(Path.Combine(b, "personen", "p1")));
        Assert.Equal("p1", PersonStore.IdOf(Path.Combine(b, "personen", "p1") + Path.DirectorySeparatorChar));
        Assert.Equal(b, PersonStore.BaseOf(new PersonStore(b).RootOf("p1")));
    }

    [Fact(DisplayName = "Personen: Schlüssel (z. B. Claude-Abo-Token) getrennt – eine weitere Person sieht die der Hauptperson nicht und umgekehrt")]
    public void Secrets_are_per_person()
    {
        var b = Root(); var store = new InMemorySecretStore();
        var main = new TrendoCore(b, store);
        var other = new TrendoCore(new PersonStore(b).RootOf("p1"), store);
        main.Secrets.Set("claude-abo-token", "haupt-token");
        Assert.Null(other.Secrets.Get("claude-abo-token"));
        other.Secrets.Set("claude-abo-token", "eigener-token");
        Assert.Equal("haupt-token", main.Secrets.Get("claude-abo-token"));
        Assert.Equal("eigener-token", other.Secrets.Get("claude-abo-token"));
        Assert.True(other.Secrets.Delete("claude-abo-token")); Assert.Equal("haupt-token", main.Secrets.Get("claude-abo-token"));
    }

    [Fact(DisplayName = "Personen: weitere Person bekommt eigene Claude-Code-Anmeldung und eigenes claude.ai-Profil im eigenen Datenordner")]
    public void Claude_per_person()
    {
        var b = Root(); var p = new PersonStore(b).RootOf("p1");
        Assert.Null(ClaudeCode.PersonConfigDir(b));
        Assert.Equal(Path.Combine(p, "claude-code"), ClaudeCode.PersonConfigDir(p));
        Assert.NotEqual(new ClaudeWeb(b).Profile, new ClaudeWeb(p).Profile);
        new ClaudeWeb(b).Navigated(new Uri("https://claude.ai/new"), null);
        Assert.True(new ClaudeWeb(b).Connected); Assert.False(new ClaudeWeb(p).Connected); // Anmeldung der Hauptperson gilt nicht für andere
    }
}
