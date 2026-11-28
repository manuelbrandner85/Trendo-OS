using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0788, 5. Prüfrunde: Aufräumen der Zwischendateien von <see cref="AtomicFile"/> trifft nur das eigene Muster „Name.&lt;8 Hexziffern&gt;.tmp“.</summary>
public class T0788AtomicTempTests
{
    [Theory(DisplayName = "T-0788 R5: AtomicFile erkennt nur die eigenen Zwischendateien")]
    [InlineData("a.tdoc.0a1b2c3d.tmp", true)]
    [InlineData("a.tdoc.notiz.tmp", false)]
    [InlineData("a.tdoc.0A1B2C3D.tmp", false)]
    [InlineData("a.tdoc.0a1b2c3.tmp", false)]
    [InlineData("a.tdoc.0a1b2c3d4.tmp", false)]
    [InlineData("b.tdoc.0a1b2c3d.tmp", false)]
    [InlineData("a.tdoc.0a1b2c3d.tmp.bak", false)]
    public void Own_temp_pattern(string file, bool own) => Assert.Equal(own, AtomicFile.IsOwnTemp(file, @"C:\x\a.tdoc"));

    [Fact(DisplayName = "T-0788 R5: AtomicFile räumt alte eigene Zwischendateien weg, eine fremde „Name.notiz.tmp“ bleibt")]
    public void Sweep_keeps_foreign()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-t0788-atomic-").FullName;
        try
        {
            var target = Path.Combine(dir, "a.tdoc");
            var own = target + ".0a1b2c3d.tmp"; var foreign = target + ".notiz.tmp";
            File.WriteAllText(own, "alt"); File.WriteAllText(foreign, "fremd");
            var old = DateTime.UtcNow - AtomicFile.StaleAfter - TimeSpan.FromMinutes(5);
            File.SetLastWriteTimeUtc(own, old); File.SetLastWriteTimeUtc(foreign, old);
            AtomicFile.WriteAllText(target, "neu");
            Assert.Equal("neu", File.ReadAllText(target));
            Assert.False(File.Exists(own));
            Assert.Equal("fremd", File.ReadAllText(foreign));
        }
        finally { Directory.Delete(dir, true); }
    }
}
