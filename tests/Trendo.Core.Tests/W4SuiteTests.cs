using Trendo.Core;
using Xunit;

public class W4_X11_Suite
{
    [Fact(DisplayName = "X-11: Jedes Vorhaben legt Ordner, Startdateien und Schrittliste an; Abhaken und Fortschritt")]
    public void Kits()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-suite-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        foreach (var k in CreationSuite.Kits)
        {
            var d = CreationSuite.Create(root, k.Id, "V " + k.Id);
            Assert.Equal(k.Steps.Length, CreationSuite.Steps(d).Count);
            Assert.All(k.Folders, f => Assert.True(Directory.Exists(Path.Combine(d, f))));
        }
        Assert.True(File.Exists(Path.Combine(root, "V-game", "Spiel", "game.js")));
        Assert.True(File.Exists(Path.Combine(root, "V-app", "Programm", "main.py")));
        Assert.Equal("V web", SitePage.Load(Path.Combine(root, "V-web", "seite.json")).Title);
        var v = Path.Combine(root, "V-video");
        Assert.Equal(0, CreationSuite.Percent(v));
        var s = CreationSuite.Steps(v);
        CreationSuite.Toggle(v, s[0].Line); CreationSuite.Toggle(v, s[1].Line);
        Assert.Equal(2 * 100 / 7, CreationSuite.Percent(v));
        CreationSuite.Toggle(v, s[1].Line); Assert.True(CreationSuite.Steps(v)[0].Done); Assert.False(CreationSuite.Steps(v)[1].Done);
        CreationSuite.Toggle(v, 0); // Überschrift – keine Änderung
        CreationSuite.AddStep(v, "Musik aussuchen"); Assert.Equal(8, CreationSuite.Steps(v).Count);
        Assert.Throws<IOException>(() => CreationSuite.Create(root, "video", "V video"));
    }
}
