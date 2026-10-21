using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class SPEC01_Rooms
{
    [Fact(DisplayName = "SPEC-01: Raumplaner rechnet Fläche, Farbe, Boden, Leisten und speichert dauerhaft")]
    public void Plan()
    {
        using var f = new Fixture();
        var p = new RoomPlanner(f.Root);
        var r = p.Add("Wohnzimmer", 5, 4, 2.5, 1, 2);
        Assert.Equal(20, r.Floor); Assert.Equal(50, r.Volume);
        Assert.Equal(45 - 2 - 3, r.WallNet, 6); Assert.Equal(17.1, r.Skirting, 6);
        var m = RoomPlanner.Need(p.Project.Rooms, 2.2);
        Assert.Equal(10, m.FloorPacks); // 22 m² / 2,2
        Assert.Equal(12, m.PaintLiters); Assert.Equal(2, m.PaintBuckets);
        Assert.Throws<ArgumentException>(() => p.Add("x", 0, 3));
        Assert.Single(new RoomPlanner(f.Root).Project.Rooms);
        var svg = File.ReadAllText(p.ExportSvg());
        Assert.Contains("<rect", svg); Assert.Contains("Wohnzimmer", svg);
        Assert.Equal(2.5, Fach.Num("2,5")); Assert.Equal(1234.5, Fach.Num("1.234,5"));
    }
}
