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

public class SPEC02_Electronics
{
    [Fact(DisplayName = "SPEC-02: Farbcode, Ohm, LED-Vorwiderstand, Spannungsteiler, Stückliste")]
    public void Calc()
    {
        Assert.Equal((4700, 5), Electronics.DecodeBands("gelb violett rot gold"));
        Assert.Equal(10000, Electronics.DecodeBands("braun schwarz schwarz rot braun").Ohm);
        Assert.Equal("gelb violett rot gold", Electronics.EncodeBands(4700));
        Assert.Equal("braun schwarz gold gold", Electronics.EncodeBands(1));
        Assert.Throws<ArgumentException>(() => Electronics.DecodeBands("lila blau"));
        var o = Electronics.OhmsLaw(12, 0, 240); Assert.Equal(0.05, o.I, 6); Assert.Equal(0.6, o.P, 6);
        var led = Electronics.LedResistor(5, 2, 20); Assert.Equal(150, led.Exact, 6); Assert.Equal(150, led.E12);
        Assert.Equal(180, Electronics.LedResistor(5, 1.9, 18).E12);
        var d = Electronics.Divider(12, 3.3); Assert.True(d.ErrorPct < 3);
        var bom = Electronics.ParseBom("R1, 10k, 0805, 1, 0,02\nR2,10k,0805,2,0,02\n# Kommentar\nC1;100n;0603");
        Assert.Equal(2, bom.Count); Assert.Equal(3, bom[0].Qty); Assert.Equal("R1 R2", bom[0].Ref);
        Assert.Contains("0,06", Electronics.BomCsv(bom));
    }
}
