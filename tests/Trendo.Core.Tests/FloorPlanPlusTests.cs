using System.Text.Json.Nodes;
using Trendo.Core.Arch;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0257: Grundriss-Ausbau nach Vorbild-Analyse (Sweet Home 3D, Clean-Room) – Geschosse, Dachschräge, Vorlage-Bild,
/// Bemaßung, Texte, Vieleck-Raum, Wand teilen, Spiegeln, Winkelfang, OBJ.</summary>
public class T0257_FloorPlanPlus
{
    [Fact(DisplayName = "T-0257: Geschosse – Obergeschoss und Keller anlegen, aktives Geschoss wechseln, Wohnfläche gesamt, löschen")]
    public void Levels()
    {
        var data = new JsonObject(); FloorPlan.Template("wohnung").WriteTo(data);
        var eg = FloorPlan.From(data).LivingArea();
        var og = FloorPlan.AddLevel(data);
        Assert.Equal("Obergeschoss", og.LevelName); Assert.Equal(270, og.Elevation);
        Assert.Equal(2, FloorPlan.Levels(data).Count); Assert.Equal(1, FloorPlan.ActiveLevel(data));
        var p = FloorPlan.From(data); Assert.Empty(p.Rooms);
        p.AddRectRoom(0, 0, 500, 400, "Kind", "kind"); p.WriteTo(data);
        Assert.Equal(eg + 20, FloorPlan.LivingAreaAll(data), 6);
        FloorPlan.SetActiveLevel(data, 0); Assert.Equal("Erdgeschoss", FloorPlan.From(data).LevelName);
        var keller = FloorPlan.AddLevel(data, below: true);
        Assert.Equal("Keller", keller.LevelName); Assert.True(keller.Elevation < 0);
        Assert.Equal(["Keller", "Erdgeschoss", "Obergeschoss"], FloorPlan.Levels(data).Select(l => l.LevelName));
        FloorPlan.RemoveLevel(data, 0); FloorPlan.RemoveLevel(data, 1);
        Assert.Single(FloorPlan.Levels(data)); Assert.NotNull(data["plan"]); Assert.Null(data["levels"]);
        Assert.Throws<InvalidOperationException>(() => FloorPlan.RemoveLevel(data, 0));
        // 3D: Obergeschoss liegt auf seiner Höhe
        var two = new JsonObject(); var a = new FloorPlan(); a.AddWall(0, 0, 400, 0); a.WriteTo(two);
        var b = FloorPlan.AddLevel(two); b.AddWall(0, 0, 400, 0); b.WriteTo(two);
        var parts = Plan3D.BuildAll(FloorPlan.Levels(two));
        Assert.Contains(parts, x => x.Solid.Bounds().Min.Z >= 269);
    }

    [Fact(DisplayName = "T-0257: Dachschräge – Kniestock 1 m, Höhe 2,5 m, Schräge 3 m tief: Streifen 1–2 m zählt halb (WoFlV)")]
    public void Slope()
    {
        var p = new FloorPlan(); var r = p.AddRectRoom(0, 0, 500, 400, "Dachzimmer");
        Assert.Equal(20, p.RoomLivingArea(r), 6);
        r.SlopeSides = 1; r.Knee = 100; r.Height = 250; r.SlopeDepth = 300;
        var (under1, oneTwo) = FloorPlan.SlopeZones(r);
        Assert.Equal(0, under1, 6);                  // Kniestock 1 m: nichts unter 1 m
        Assert.Equal(5 * 2.0, oneTwo, 6);            // bis 2 m Höhe: 2/3 von 3 m = 2 m Streifen × 5 m Wand
        Assert.Equal(20 - 10 / 2.0, p.RoomLivingArea(r), 6);
        r.Knee = 50; r.SlopeSides = 2; r.SlopeDepth = 150; // Satteldach: beide 5-m-Wände, je 1,5 m Schräge (Raum ist 4 m breit)
        var (u, h) = FloorPlan.SlopeZones(r);
        Assert.Equal(2 * 5 * 0.375, u, 6);           // 0,5 → 1 m: 0,5/2,0 × 1,5 m = 0,375 m je Seite
        Assert.Equal(2 * 5 * (1.125 - 0.375), h, 6); // bis 2 m: 1,5/2,0 × 1,5 m = 1,125 m
        Assert.Equal(20 - 3.75 - 7.5 / 2.0, p.RoomLivingArea(r), 6);
        r.SlopeDepth = 300; // unmöglich tief: nie mehr abziehen als der Raum hat
        Assert.True(p.RoomLivingArea(r) >= 0 && FloorPlan.SlopeZones(r).Under1m + FloorPlan.SlopeZones(r).From1to2m <= 20 + 1e-9);
    }

    [Fact(DisplayName = "T-0257: Vorlage-Bild maßstäblich – zwei Punkte, echte Länge; der erste Punkt bleibt liegen")]
    public void Background_calibration()
    {
        var p = new FloorPlan { Background = new PlanImage { Path = "plan.png", PxW = 1000, PxH = 800, CmPerPx = 1, X = 0, Y = 0 } };
        p.CalibrateBackground(100, 100, 300, 100, 500); // 200 cm im Bild sind in Wirklichkeit 5 m
        Assert.Equal(2.5, p.Background!.CmPerPx, 6);
        Assert.Equal(100 - 100 * 2.5, p.Background.X, 6); Assert.Equal(100 - 100 * 2.5, p.Background.Y, 6);
        Assert.Throws<ArgumentException>(() => p.CalibrateBackground(0, 0, 0.2, 0, 100));
        Assert.Throws<InvalidOperationException>(() => new FloorPlan().CalibrateBackground(0, 0, 100, 0, 100));
        var data = new JsonObject(); p.WriteTo(data);
        Assert.Equal(2.5, FloorPlan.From(data).Background!.CmPerPx, 6);
    }

    [Fact(DisplayName = "T-0257: Bemaßung und Text – anlegen, treffen, in Zeichnung/SVG/DXF, löschen")]
    public void Dimensions_and_labels()
    {
        var p = new FloorPlan(); p.AddRectRoom(0, 0, 400, 300, "Zimmer");
        var d = p.AddDimension(0, 0, 400, 0, -60);
        Assert.Equal(400, d.Length, 6);
        Assert.Equal(-60, DimLine.OffsetFor(0, 0, 400, 0, 200, -60), 6);
        var (_, ly, _, _) = d.Line(); Assert.Equal(-60, ly, 6);
        var l = p.AddLabel(200, 150, "Küche neu");
        Assert.Equal(("dim", d.Id), p.HitTest(200, -60));
        Assert.Equal(("label", l.Id), p.HitTest(200, 150));
        var shapes = PlanDrawing.Build(p, PlanTheme.Paper);
        Assert.Contains(shapes, x => x is DText { Text: "4,00", Layer: "MASSE" });
        Assert.Contains(shapes, x => x is DText { Text: "Küche neu" });
        Assert.Contains("Küche neu", PlanDrawing.Svg(p)); Assert.Contains("Küche neu", PlanDrawing.Dxf(p));
        Assert.True(p.Remove(d.Id)); Assert.True(p.Remove(l.Id)); Assert.Empty(p.Dimensions); Assert.Empty(p.Labels);
        Assert.Throws<ArgumentException>(() => p.AddDimension(1, 1, 1.2, 1, 10));
    }

    [Fact(DisplayName = "T-0257: Raum Punkt für Punkt (L-Form), Wand teilen mit Öffnungen, Möbel spiegeln, Winkelfang 15°")]
    public void Polygon_split_mirror_angle()
    {
        var p = new FloorPlan();
        var l = p.AddPolygonRoom([(0, 0), (600, 0), (600, 300), (300, 300), (300, 500), (0, 500)], "Flur L");
        Assert.Equal((600 * 300 + 300 * 200) / 10000.0, l.Area / 10000, 6);
        Assert.Throws<ArgumentException>(() => p.AddPolygonRoom([(0, 0), (10, 0), (0, 10)]));
        var w = p.AddWall(0, 0, 800, 0); var win = p.AddOpening("fenster", 600, 0)!;
        var b = p.SplitWall(w.Id);
        Assert.Equal(400, w.Length, 6); Assert.Equal(400, b.Length, 6);
        Assert.Equal(b.Id, win.WallId); Assert.Equal(200, win.At, 6);
        var door = p.AddOpening("tuer", 200, 0)!; // in der ersten Hälfte
        var c = p.SplitWall(w.Id, 200); // mitten durch die Tür → Schnitt wandert hinter die Tür
        Assert.Equal(w.Id, door.WallId); Assert.True(w.Length >= door.At + door.Width / 2, "Tür bleibt ganz auf ihrer Wand");
        Assert.Throws<ArgumentException>(() => p.SplitWall(c.Id, 2));
        var sofa = p.AddItem("sofa", 300, 300); var normal = PlanDrawing.Build(p, PlanTheme.Paper).OfType<DPoly>().Count();
        sofa.Mirror = true;
        Assert.Equal(normal, PlanDrawing.Build(p, PlanTheme.Paper).OfType<DPoly>().Count()); // gleiche Teile, gespiegelt
        Assert.Equal((100.0, 0.0), FloorPlan.AngleSnap(0, 0, 99.8, 6));
        var (x, y) = FloorPlan.AngleSnap(0, 0, 100, 60); // 31° → 30°
        Assert.Equal(30, Math.Atan2(y, x) * 180 / Math.PI, 1);
        Assert.Equal(Math.Sqrt(100 * 100 + 60 * 60), Math.Sqrt(x * x + y * y), 0);
    }

    [Fact(DisplayName = "T-0257: OBJ-Export aller Geschosse mit Materialien; PDF mit einer Seite je Geschoss; Raumbuch mit Geschoss-Spalte")]
    public void Obj_and_multi_level_exports()
    {
        var data = new JsonObject(); FloorPlan.Template("bad").WriteTo(data);
        var og = FloorPlan.AddLevel(data); og.AddRectRoom(0, 0, 300, 300, "Abstell", "abstell"); og.WriteTo(data);
        var levels = FloorPlan.Levels(data);
        var (obj, mtl) = PlanDrawing.Obj(levels, "plan.mtl");
        Assert.StartsWith("# Trendo Grundriss\nmtllib plan.mtl\n", obj);
        Assert.Contains("\nv ", obj); Assert.Contains("\nf ", obj); Assert.Contains("usemtl farbe_", obj);
        Assert.Contains("newmtl farbe_E9EDF3", mtl);
        var maxY = obj.Split('\n').Where(s => s.StartsWith("v ")).Max(s => double.Parse(s.Split(' ')[2], System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(maxY > 5, "Obergeschoss liegt oben (y in m)");
        var dir = Path.Combine(Path.GetTempPath(), "trendo-plan-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var pdf = File.ReadAllText(PlanDrawing.Pdf(levels, Path.Combine(dir, "plan.pdf")), System.Text.Encoding.Latin1);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(pdf, @"/Type\s*/Page[^s]").Count);
        }
        finally { Directory.Delete(dir, true); }
        var csv = PlanDrawing.RoomBookCsv(levels);
        Assert.Contains(";Geschoss;", csv); Assert.Contains("Abstell;Abstellraum;9,00", csv); Assert.Contains(";Obergeschoss;", csv);
    }
}
