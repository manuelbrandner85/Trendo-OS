using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0772-fix1 (Gegenprüfung HOCH 1): Bahnstrecken der Länge 0 (oder fast 0) fanden in der neuen Abstandsprüfung kein Hindernis – eine
/// Null-Gravur neben einem Loch wurde gefräst statt ausgelassen. Nutzt nur Schnittstellen, die es auf main 542dc1ce7 gibt (Rotlauf möglich).</summary>
public class T0772Fix1Tests
{
    static readonly List<(double X, double Y)> Plate = [(100, 0), (120, 0), (120, 20), (100, 20)]; // weit weg, wird immer gefräst

    static CamResult Run(List<(double X, double Y)> grav, bool closedGrav = false)
    {
        var hole = Enumerable.Range(0, 64).Select(k => (51.5 + Math.Cos(k * Math.PI / 32), 50 + Math.Sin(k * Math.PI / 32))).ToList();
        return CncCam.Generate([new(Plate, true, "0"), new(hole, true, "0"), new(grav, closedGrav, "0")], new CamSettings { ToolD = 3, Mode = "auf" });
    }

    [Theory(DisplayName = "T-0772-fix1: Gravur der Länge 0 (auch drei gleiche Punkte, 1e-12) neben einem Loch – beide ausgelassen wie in 0.9.110")]
    [InlineData(0.0, 2)]
    [InlineData(0.0, 3)]
    [InlineData(1e-12, 2)]
    [InlineData(1e-9, 2)]
    public void Null_gravur_neben_loch(double len, int points)
    {
        var grav = Enumerable.Range(0, points).Select(k => (50 + (k == points - 1 ? len : 0), 50.0)).ToList();
        var r = Run(grav);
        Assert.Equal(0, Assert.Single(r.Paths).Source); // nur die Platte
        Assert.Contains(r.Warnings, w => w.Contains("zu nah an Kontur") && w.Contains("0,5 mm"));
    }

    [Fact(DisplayName = "T-0772-fix1: Null-Gravur weit weg vom Loch wird weiter gefräst (keine neue Absage)")]
    public void Null_gravur_frei()
    {
        var hole = Enumerable.Range(0, 64).Select(k => (51.5 + Math.Cos(k * Math.PI / 32), 50 + Math.Sin(k * Math.PI / 32))).ToList();
        var r = CncCam.Generate([new(Plate, true, "0"), new(hole, true, "0"), new([(80.0, 80.0), (80.0, 80.0)], false, "0")], new CamSettings { ToolD = 3, Mode = "auf" });
        Assert.Equal(3, r.Paths.Count);
    }
}
