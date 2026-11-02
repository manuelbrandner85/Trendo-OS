using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

/// Dunkel-Gold-Look (Wunsch 02.11.2026): Trendo ist dunkel wie Windows, Akzent ist Gold statt Blau.
public class DunkelGoldLook
{
    [Fact(DisplayName = "Dunkel-Gold-Look: neue Einstellungen sind dunkel mit Gold-Akzent; der Stand des Aussehens wird gespeichert")]
    public void Defaults()
    {
        var s = new UserSettings();
        Assert.Equal("dunkel", s.Theme);
        Assert.Equal("#C9A227", s.Accent);
        Assert.Equal("#C9A227", s.EffectiveAccent(DateTime.Now));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-gold-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            s.LookVersion = 2; s.Save(dir);
            Assert.Equal(2, UserSettings.Load(dir).LookVersion);
        }
        finally { Directory.Delete(dir, true); }
    }
}
