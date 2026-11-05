using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo CAD – Vorbild-Funktionen (Fusion 360, Onshape, FreeCAD; 04.11.2026): Eigenschaften, Querschnitt, Normbohrungen.</summary>
public class CadMeasureTests
{
    [Fact(DisplayName = "CORE-05: Eigenschaften: Quader 40×30×10 mm – Volumen 12 cm³, Oberfläche 38 cm², Masse je Werkstoff, Schwerpunkt in der Mitte")]
    public void Properties()
    {
        var box = CadShapes.Box(new Vec3(0, 0, 0), 40, 30, 10);
        var p = CadMeasure.Properties([box]);
        Assert.Equal(12, p.VolumeCm3, 3); Assert.Equal(38, p.AreaCm2, 3); Assert.Equal(12 * 1.24, p.MassG, 2);
        Assert.Equal((20.0, 15.0, 5.0), (p.Center.X, p.Center.Y, p.Center.Z));
        Assert.Equal(12 * 7.85, CadMeasure.Properties([box], "stahl").MassG, 2);
        Assert.Equal((40.0, 30.0, 10.0), (p.Size.X, p.Size.Y, p.Size.Z));
        Assert.Throws<CadError>(() => CadMeasure.Properties([]));
        // zwei Körper: Schwerpunkt verschiebt sich zum schwereren
        var two = CadMeasure.Properties([box, CadShapes.Box(new Vec3(100, 0, 0), 10, 30, 10)]);
        Assert.InRange(two.Center.X, 20, 45);
    }

    [Fact(DisplayName = "CORE-05: Querschnitt: Quader mit Bohrung – Fläche ohne das Loch; über dem Körper 0")]
    public void Section()
    {
        var plate = CadShapes.Box(new Vec3(0, 0, 0), 40, 30, 10).Subtract(CadShapes.Cylinder(new Vec3(20, 15, -1), 5, 12, 64));
        var a = CadMeasure.SectionArea([plate], 5);
        Assert.InRange(a, 1200 - Math.PI * 25 - 2, 1200 - Math.PI * 25 + 2);
        Assert.Equal(0, CadMeasure.SectionArea([plate], 50));
    }

    [Fact(DisplayName = "CORE-05: Normbohrungen: M3 Durchgang 3,4 mm, M6 Kernloch 5 mm, M8 Senkschraube 9/16,4 mm; eigenes Maß zuerst")]
    public void Holes()
    {
        Assert.Equal("frei", CadMeasure.Holes[0].Id);
        Assert.Equal(3.4, CadMeasure.Holes.Single(h => h.Id == "M3-durch").Diameter);
        Assert.Equal(5.0, CadMeasure.Holes.Single(h => h.Id == "M6-kern").Diameter);
        var m8 = CadMeasure.Holes.Single(h => h.Id == "M8-senk"); Assert.Equal((9.0, 16.4), (m8.Diameter, m8.Countersink));
        Assert.All(CadMeasure.Holes.Skip(1), h => Assert.True(h.Countersink == 0 || h.Countersink > h.Diameter));
    }
}
