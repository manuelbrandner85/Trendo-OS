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

public class SPEC03_Engineering
{
    [Fact(DisplayName = "SPEC-03: Balken (4 Lagerungen), Querschnitte, Wärmedehnung, Einheiten")]
    public void Calc()
    {
        var s = Engineering.Rect(100, 200); Assert.Equal(100.0 * 200 * 200 * 200 / 12, s.I); Assert.Equal(666666.67, s.W, 2);
        var steel = Engineering.Mat("Baustahl");
        // F=10 kN, L=4 m: M = 10 kNm; f = F L³/(48 E I)
        var r = Engineering.Beam(BeamCase.EinfeldMitte, 4, 10, s, steel);
        Assert.Equal(10, r.MaxMoment_kNm); Assert.Equal(15, r.Stress_MPa); Assert.Equal(Math.Round(10000 * Math.Pow(4000, 3) / (48 * 210000 * s.I), 2), r.Deflection_mm);
        Assert.True(r.Ok);
        Assert.Equal(20, Engineering.Beam(BeamCase.EinfeldStrecke, 4, 10, s, steel).MaxMoment_kNm);
        Assert.Equal(40, Engineering.Beam(BeamCase.KragarmEnde, 4, 10, s, steel).MaxMoment_kNm);
        Assert.False(Engineering.Beam(BeamCase.KragarmStrecke, 4, 10, Engineering.Rect(40, 40), Engineering.Mat("Fichte")).Ok);
        var t = Engineering.Tube(50, 5); Assert.True(t.I < Engineering.Tube(50).I);
        Assert.Equal(2.76, Engineering.ThermalExpansion(Engineering.Mat("Aluminium"), 3, 40));
        Assert.Equal(6.894757, Engineering.Convert(100, "psi", "bar"), 5);
        Assert.Throws<ArgumentException>(() => Engineering.Convert(1, "m", "bar"));
    }
}

public static class Glb
{
    /// <summary>Echte, minimale GLB-Datei mit einem Dreiecksnetz (tris Dreiecke).</summary>
    public static string Write(string dir, long tris, int textures = 0)
    {
        var json = $"{{\"asset\":{{\"version\":\"2.0\",\"generator\":\"Test\"}},\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":0}},\"indices\":1,\"material\":0}}]}}],\"materials\":[{{}}],\"accessors\":[{{\"count\":{tris * 3}}},{{\"count\":{tris * 3}}}],\"textures\":[{string.Join(",", Enumerable.Repeat("{}", textures))}],\"nodes\":[{{\"mesh\":0}}]}}";
        while (json.Length % 4 != 0) json += " ";
        var j = System.Text.Encoding.UTF8.GetBytes(json);
        var p = Path.Combine(dir, $"m{tris}.glb");
        using var w = new BinaryWriter(File.Create(p));
        w.Write(0x46546C67u); w.Write(2u); w.Write((uint)(12 + 8 + j.Length)); w.Write((uint)j.Length); w.Write(0x4E4F534Au); w.Write(j);
        return p;
    }
}

public class SPEC04_Xr
{
    [Fact(DisplayName = "SPEC-04: GLB lesen, Dreiecke zählen, Geräte-Grenzen und Bildzeit prüfen")]
    public void Inspect()
    {
        using var f = new Fixture(); Directory.CreateDirectory(f.Root);
        var small = XrInspector.Inspect(Glb.Write(f.Root, 1000));
        Assert.Equal(1000, small.Triangles); Assert.Equal(1, small.Primitives); Assert.Equal("Test", small.Generator);
        Assert.All(XrInspector.Budgets, b => Assert.True(XrInspector.Check(small, b).Ok));
        var big = XrInspector.Inspect(Glb.Write(f.Root, 1_000_000, 50));
        var q3 = XrInspector.Check(big, XrInspector.Budgets[0]);
        Assert.False(q3.Ok); Assert.Contains(q3.Problems, p => p.Contains("Dreiecke")); Assert.Contains(q3.Problems, p => p.Contains("Texturen"));
        Assert.True(XrInspector.Check(big, XrInspector.Budgets[3]).Ok);
        var bad = Path.Combine(f.Root, "x.glb"); File.WriteAllText(bad, "kaputt");
        Assert.Throws<InvalidOperationException>(() => XrInspector.Inspect(bad));
        Assert.Equal((13.89, true, 13.6), XrInspector.FrameBudget(72, 12));
    }
}

public class SPEC05_Science
{
    [Fact(DisplayName = "SPEC-05: CSV mit deutschen Zahlen, Kennzahlen, Regression, Welch-t-Test mit p-Wert")]
    public void Analyse()
    {
        var t = DataTable.Parse("Zeit;Temp;B\n1;2,0;5\n2;4,1;6\n3;5,9;5\n4;8,0;7\n5;10,0;\nx;y;6");
        Assert.Equal(["Zeit", "Temp", "B"], t.Columns);
        var d = t.Describe(); Assert.Equal(5, d[0].N); Assert.Equal(3, d[0].Mean); Assert.Equal(3, d[0].Median); Assert.Equal(Math.Sqrt(2.5), d[0].StdDev, 9);
        var r = t.Fit("Zeit", "Temp"); Assert.Equal(1.99, r.Slope, 9); Assert.True(r.R2 > 0.99);
        // Referenz (R: t.test(c(1,2,3,4,5), c(6,7,8,9,10))): t = -5, df = 8, p = 0,001053
        var w = Stats.Welch([1, 2, 3, 4, 5], [6, 7, 8, 9, 10]);
        Assert.Equal(-5, w.T, 9); Assert.Equal(8, w.Df, 9); Assert.Equal(0.001053, w.P, 5); Assert.True(w.Significant);
        Assert.False(Stats.Welch([1, 2, 3], [1.5, 2.5, 3.2]).Significant);
        Assert.Contains("| Temp | 5 |", Stats.Report(t));
        Assert.Throws<ArgumentException>(() => t.Values("Gibts"));
    }
}
