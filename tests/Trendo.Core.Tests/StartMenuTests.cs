using Trendo.Core.Shell;
using Xunit;
namespace Trendo.Core.Tests;

public class StartMenu_Logik
{
    [Fact(DisplayName = "Startmenü: Anheften/Lösen ohne Doppelte, Zuletzt benutzt (neueste zuerst, max. 8), A–Z deutsch")]
    public void Rules()
    {
        var p = StartMenu.Pin([], "A"); p = StartMenu.Pin(p, "A"); p = StartMenu.Pin(p, "B");
        Assert.Equal(["A", "B"], p); Assert.Equal(["B"], StartMenu.Unpin(p, "A"));
        var r = new List<string>(); foreach (var k in new[] { "a", "b", "start", "a", "c", "d", "e", "f", "g", "h", "i" }) r = StartMenu.Touch(r, k);
        Assert.Equal(8, r.Count); Assert.Equal("i", r[0]); Assert.DoesNotContain("start", r); Assert.Single(r, x => x == "a");
        var az = StartMenu.AtoZ(["Zeit", "Ärger", "apfel", "3D Studio", "Office"], s => s);
        Assert.Equal(["#", "A", "O", "Z"], az.Select(g => g.Letter)); Assert.Equal(["apfel", "Ärger"], az[1].Items);
    }
}
