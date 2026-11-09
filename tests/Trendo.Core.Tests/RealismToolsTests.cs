using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Statik;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Fräsen: Abtrag in 3D. Statik: Tragwerk als Körper. Wohnung: echter Sonnenstand.</summary>
public class RealismToolsTests
{
    static CncSeg Cut(double x0, double y0, double x1, double y1, double z) => new(x0, y0, z, x1, y1, z, false, 1, 1);

    [Fact(DisplayName = "Fräsen: Nut hat Tiefe und Breite des Fräsers; daneben bleibt das Rohteil; Volumen stimmt")]
    public void Groove()
    {
        var s = CncStock.Cut([Cut(0, 0, 50, 0, -3)], 6);
        double H(double x, double y) => s.HeightAt((int)Math.Round((x - s.X0) / s.Cell), (int)Math.Round((y - s.Y0) / s.Cell));
        Assert.Equal(-3, H(25, 0), 3); Assert.Equal(-3, H(25, 2.4), 3); Assert.Equal(0, H(25, 5), 3);
        var expected = (50 * 6 + Math.PI * 9) * 3 / 1000; // Nut mit runden Enden, in cm³
        Assert.InRange(s.RemovedVolume, expected * 0.93, expected * 1.07);
        Assert.Equal(6, s.Thickness, 3);
    }

    [Fact(DisplayName = "Fräsen: Simulation trägt schrittweise ab; Eilgang und Fahrt über dem Teil tragen nichts ab")]
    public void Steps()
    {
        List<CncSeg> path = [new(0, 0, 5, 40, 0, 5, true, 1, 1), Cut(0, 0, 20, 0, -2), Cut(20, 0, 40, 0, -2), new(40, 0, 5, 0, 0, 5, false, 3, 1)];
        var one = CncStock.Cut(path, 4, 1).RemovedVolume; var all = CncStock.Cut(path, 4).RemovedVolume;
        Assert.True(one > 0); Assert.InRange(all / one, 1.6, 2.3);
        Assert.Throws<InvalidOperationException>(() => CncStock.Cut([new CncSeg(0, 0, 5, 40, 0, 5, false, 1, 1)], 4));
    }

    [Fact(DisplayName = "Fräsen: Netz ist ein Körper mit Boden auf 0; Teil für Blender trägt Material; Dichte je Werkstoff")]
    public void Mesh_and_part()
    {
        var s = CncStock.Cut([Cut(0, 0, 50, 0, -3)], 6);
        var m = s.ToMesh((1, 2, 3));
        Assert.True(m.Triangles > 1000 && m.Triangles < 70000);
        Assert.Equal(0, m.V.Min(v => v.Z), 6); Assert.Equal(0.6, m.V.Max(v => v.Z), 6);
        var p = s.ToPart("metall");
        Assert.Equal("mesh", p.Shape); Assert.Equal("metall", p.Material); Assert.InRange(p.Positions!.Length / 3, s.Nx * s.Ny, s.Nx * s.Ny + 2 * (s.Nx + s.Ny)); Assert.Equal(m.I.Count, p.Indices!.Length);
        Assert.Equal(2.7, CncStock.Density("Aluminium")); Assert.Equal(0.6, CncStock.Density("Holz"));
    }

    [Fact(DisplayName = "Statik 3D: Körper entsteht, biegt sich überhöht nach unten, Farbe zeigt die Belastung, Grundfarbe je Werkstoff")]
    public void Frame_body()
    {
        var model = FrameModel.Template("kragarm");
        var r = FrameSolver.Solve(model);
        var mesh = Frame3D.Build(model, r);
        Assert.True(mesh.Triangles > 20);
        Assert.True(Frame3D.Exaggeration(model, r) > 1);
        Assert.Contains(mesh.R.Select((v, i) => (v, i)), x => x.v >= 220 && mesh.G[x.i] < 70); // rot am größten Moment
        Assert.Equal(Frame3D.MaterialColor("Baustahl S235"), Frame3D.Stress(Frame3D.MaterialColor("Baustahl S235"), 0));
        Assert.NotEqual(Frame3D.MaterialColor("Fichte/Nadelholz C24"), Frame3D.MaterialColor("Baustahl S235"));
    }

    [Theory(DisplayName = "Sonne: Mittag steht sie im Süden am höchsten, im Sommer höher als im Winter")]
    [InlineData(7, 80, 13)]
    public void Sun_position(int summer, int _, int noon)
    {
        var s = PlanPhoto.Sun(summer, noon, 51); var w = PlanPhoto.Sun(1, noon, 51); var m = PlanPhoto.Sun(summer, 8, 51);
        Assert.InRange(s.Elevation, 50, 65); Assert.InRange(w.Elevation, 10, 25); Assert.True(m.Elevation < s.Elevation);
        Assert.InRange(s.Azimuth, 170, 215); Assert.InRange(m.Azimuth, 80, 135);
        Assert.True(PlanPhoto.Sun(7, 19, 51).Azimuth > 240);
    }

    [Fact(DisplayName = "Sonne: Szene für Blender nennt Sonnenstand, sonst bleibt die Standard-Beleuchtung")]
    public void Sun_scene()
    {
        var plain = PlanPhoto.Scene([FloorPlan.Template("bad")], _ => null);
        Assert.Null(plain["sun"]);
        var j = PlanPhoto.Scene([FloorPlan.Template("bad")], _ => null, sunMonth: 7, sunHour: 13);
        Assert.InRange((double)j["sun"]!["elevation"]!, 50, 65);
    }
}
