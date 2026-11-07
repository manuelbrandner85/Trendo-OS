using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>S4: Abdeckung beim Einscannen – Richtungen und Höhen der Kameras, Lücken mit konkretem Rat.</summary>
public class ScanCoverageTests
{
    // Kameras auf einem Kreis (Radius 2) in der Höhe h um die Mitte, Hochachse beliebig gedreht (Fotogrammetrie kennt kein festes Oben)
    static Vec3 Rot(Vec3 v) => new(v.X, v.Y * 0.6 - v.Z * 0.8, v.Y * 0.8 + v.Z * 0.6);
    static IEnumerable<Vec3> Ring(double h, IEnumerable<int> degrees) => degrees.Select(d => Rot(new Vec3(2 * Math.Cos(d * Math.PI / 180), 2 * Math.Sin(d * Math.PI / 180), h)));

    [Fact(DisplayName = "S4 Abdeckung: volle schräge Runde mit Lücke bei 150° und 180° → Lücke mit Fotoanzahl, Hochachse aus den Kameras")]
    public void Coverage_with_gap()
    {
        var cams = Ring(1.6, Enumerable.Range(0, 12).Select(i => i * 30 + 5).Where(d => d < 150 || d > 210)).Concat(Ring(0.2, [5, 95, 185, 275])).ToList(); // schräg (~39°) mit Lücke, flach 4 Fotos
        var r = ScanCoverage.Compute(cams, Rot(new Vec3(0, 0, 0)))!;
        var up = ScanCoverage.UpAxis(cams); Assert.True(Math.Abs(Vec3.Dot(up, Rot(new Vec3(0, 0, 1)))) > 0.99); // gedrehte Hochachse erkannt
        Assert.Equal(cams.Count, r.Cameras);
        Assert.Equal(0, r.Count(1, 5)); Assert.Equal(0, r.Count(1, 6)); Assert.True(r.Count(1, 0) > 0); // Richtungen 150° und 180° leer
        Assert.Contains(r.Gaps, g => g.StartsWith("schräg: 150°–180° fehlt") && g.Contains("noch etwa 4 Fotos"));
        Assert.Contains(r.Gaps, g => g.StartsWith("von oben: 4 von 4 Seiten fehlen"));
        Assert.InRange(r.Percent, 40, 60);
    }

    [Fact(DisplayName = "S4 Abdeckung: rundum in drei Höhen → 100 %, keine Lücken; unter 3 Kameras keine Aussage")]
    public void Full_coverage()
    {
        var all = Enumerable.Range(0, 12).Select(i => i * 30 + 10).ToList();
        var cams = Ring(0.3, all).Concat(Ring(1.4, all)).Concat(Ring(3.0, all)).ToList();
        var r = ScanCoverage.Compute(cams, Rot(new Vec3(0, 0, 0)))!;
        Assert.Equal(100, r.Percent); Assert.Empty(r.Gaps);
        Assert.Null(ScanCoverage.Compute(cams.Take(2).ToList(), new Vec3(0, 0, 0)));
        Assert.Equal(34, ScanCoverage.PlanTotal);
    }
}
