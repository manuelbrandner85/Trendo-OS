using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Arch;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0256: Grundriss-Programm (SPEC-01) – Modell, Geometrie, Prüfung, Vorlagen und Exporte.</summary>
public class T0256_FloorPlan
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-plan-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0256: Raum aufziehen baut 4 Wände außen herum; Nachbarraum teilt die gemeinsame Wand")]
    public void Rect_rooms_share_walls()
    {
        var p = new FloorPlan();
        var a = p.AddRectRoom(0, 0, 500, 420, "Wohnen");
        Assert.Equal(21.0, a.Area / 10000, 6);
        Assert.Equal(4, p.Walls.Count);
        Assert.All(p.Walls, w => Assert.Equal(24, w.Thickness));
        Assert.Contains(p.Walls, w => w.X1 == -12 && w.Y1 == -12 && w.X2 == 512 && w.Y2 == -12); // obere Wand außen, Achse 12 cm vor dem Innenmaß
        p.AddRectRoom(524, 0, 884, 420, "Schlafen");
        Assert.Equal(7, p.Walls.Count); // die Trennwand gibt es nur einmal
        p.AddRectRoom(0, 444, 300, 724, "Küche");
        Assert.Equal(10, p.Walls.Count); // oben teilweise belegt: nur Seiten und unten neu
        Assert.Throws<ArgumentException>(() => p.AddRectRoom(0, 0, 30, 30));
    }

    [Fact(DisplayName = "T-0256: Tür und Fenster setzen sich in die nächste Wand; Wandfläche netto, Fensteranteil, Sockelleiste")]
    public void Openings_and_figures()
    {
        var p = new FloorPlan(); var r = p.AddRectRoom(0, 0, 500, 400, "Zimmer");
        var win = p.AddOpening("fenster", 200, -5)!; var door = p.AddOpening("tuer", 495, 200)!;
        Assert.Equal("fenster", win.Kind); Assert.Equal(200 + 12, win.At, 1); // Wand beginnt bei x = −12
        Assert.Equal("tuer", door.Kind);
        Assert.Null(p.AddOpening("tuer", 250, 200)); // Raummitte: keine Wand in der Nähe
        var f = p.Figures(r);
        Assert.Equal(20.0, f.Area, 6); Assert.Equal(18.0, f.Perimeter, 6);
        Assert.Equal(1, f.Doors); Assert.Equal(1, f.Windows);
        Assert.Equal(1.2 * 1.35, f.WindowArea, 6);
        Assert.Equal(18 * 2.5 - 1.62 - 0.885 * 2.01, f.WallNet, 4);
        Assert.Equal(18 - 0.885, f.Skirting, 4);
        Assert.Contains(p.Check(), i => i.Message.Contains("Fensterfläche")); // 1,62 m² < 20/8
        win.Width = 200; Assert.DoesNotContain(p.Check(), i => i.Message.Contains("Fensterfläche"));
        // am Wandende eingeklemmt
        var edge = p.AddOpening("fenster", 505, -8)!; var wall = p.WallOf(edge.WallId)!;
        Assert.True(edge.At + edge.Width / 2 <= wall.Length - 5);
    }

    [Fact(DisplayName = "T-0256: Doppelklick erkennt einen von Wänden umschlossenen Raum (lichte Fläche), offene Fläche nicht")]
    public void Detect_room()
    {
        var p = new FloorPlan();
        p.AddWall(0, 0, 600, 0, 20); p.AddWall(600, 0, 600, 400, 20); p.AddWall(600, 400, 0, 400, 20); p.AddWall(0, 400, 0, 0, 20);
        var r = p.DetectRoom(300, 200, "Halle")!;
        Assert.NotNull(r);
        Assert.InRange(r.Area / 10000, 580 * 380 / 10000.0 - 0.15, 580 * 380 / 10000.0 + 0.15); // 22,04 m² lichte Fläche
        Assert.InRange(r.Points.Count, 4, 8);
        var open = new FloorPlan(); open.AddWall(0, 0, 600, 0); open.AddWall(600, 0, 600, 400); open.AddWall(600, 400, 0, 400);
        Assert.Null(open.DetectRoom(300, 200));
    }

    [Fact(DisplayName = "T-0256: Fangen an Wandenden und Raster, Achsfang; Wandende ziehen nimmt angeschlossene Wand mit; Länge setzen")]
    public void Snap_and_edit()
    {
        var p = new FloorPlan(); var a = p.AddWall(0, 0, 300, 0); var b = p.AddWall(300, 0, 300, 200);
        Assert.Equal((300, 0, true), p.Snap(307, 4));
        Assert.Equal((150, 70, false), p.Snap(152, 68));
        Assert.Equal((250, 0), FloorPlan.AxisLock(0, 0, 250, 12));
        Assert.Equal((0, 200), FloorPlan.AxisLock(0, 0, 5, 200)); Assert.Equal((100, 120), FloorPlan.AxisLock(0, 0, 100, 120));
        p.MoveWallEnd(a.Id, false, 350, 0);
        Assert.Equal(350, b.X1); Assert.Equal(350, a.X2);
        p.SetWallLength(a.Id, 400); Assert.Equal(400, a.Length, 6); Assert.Equal(400, b.X1, 6);
        p.MoveWall(b.Id, 20, 0); Assert.Equal(420, b.X1, 6); Assert.Equal(420, a.X2, 6);
        Assert.Equal(("wall", a.Id), p.HitTest(100, 3));
        var it = p.AddItem("sofa", 200, 100); Assert.Equal(("item", it.Id), p.HitTest(200, 100));
        Assert.True(p.Remove(a.Id)); Assert.DoesNotContain(p.Walls, w => w.Id == a.Id);
    }

    [Fact(DisplayName = "T-0256: Prüfung findet Möbel in der Wand, überlappende Möbel, Raum ohne Tür und blockierte Tür")]
    public void Checks()
    {
        var p = new FloorPlan(); var r = p.AddRectRoom(0, 0, 400, 400, "Zimmer");
        Assert.Contains(p.Check(), i => i.Level == "warnung" && i.Message.Contains("keine Tür"));
        var door = p.AddOpening("tuer", 200, 412)!; // untere Wand
        door.Inward = p.WallOf(door.WallId)!.Project(200, 350).Side >= 0; // nach innen öffnen
        Assert.DoesNotContain(p.Check(), i => i.Message.Contains("keine Tür"));
        p.AddItem("schrank", 200, 5); // ragt in die obere Wand
        Assert.Contains(p.Check(), i => i.Level == "fehler" && i.Message.Contains("in einer Wand"));
        p.AddItem("sessel", 100, 200); p.AddItem("couchtisch", 120, 210);
        Assert.Contains(p.Check(), i => i.Message.Contains("überschneiden sich"));
        p.AddItem("kommode", 200, 370);
        Assert.Contains(p.Check(), i => i.Message.Contains("nicht ganz öffnen"));
    }

    [Fact(DisplayName = "T-0256: Vorlagen sind fehlerfrei (keine Möbel in Wänden oder vor Türen); Wohnfläche nach WoFlV mit Balkon 25 %")]
    public void Templates_and_living_area()
    {
        foreach (var id in new[] { "wohnung", "haus", "bad" })
        {
            var p = FloorPlan.Template(id);
            Assert.NotEmpty(p.Rooms); Assert.NotEmpty(p.Walls); Assert.NotEmpty(p.Openings);
            var bad = p.Check().Where(i => i.Level != "hinweis").ToList();
            Assert.True(bad.Count == 0, id + ": " + string.Join(" | ", bad.Select(b => b.Message)));
        }
        var q = new FloorPlan(); q.AddRectRoom(0, 0, 400, 300, "Zimmer"); q.AddRectRoom(0, -224, 400, -24, "Balkon", "balkon", walls: false);
        var dach = q.AddRectRoom(500, 0, 700, 100, "Spitzboden"); dach.Height = 150;
        Assert.Equal(12 + 8 * 0.25 + 2 * 0.5, q.LivingArea(), 6);
        var w = FloorPlan.Template("wohnung");
        Assert.InRange(w.LivingArea(), 50, 70);
        Assert.True(w.Need().FloorPacks > 10);
    }

    [Fact(DisplayName = "T-0256: Speichern im Programmdokument (Formatversion), alte Raumliste übernehmen, verwaiste Öffnungen entfernt")]
    public void Json_roundtrip()
    {
        var p = FloorPlan.Template("wohnung"); var data = new JsonObject(); p.WriteTo(data);
        var back = FloorPlan.From(data);
        Assert.Equal(FloorPlan.FormatVersion, back.Version);
        Assert.Equal(p.Walls.Count, back.Walls.Count); Assert.Equal(p.Items.Count, back.Items.Count);
        Assert.Equal(p.LivingArea(), back.LivingArea(), 6);
        ((JsonArray)data["plan"]!["walls"]!).RemoveAt(0);
        Assert.True(FloorPlan.From(data).Openings.All(o => FloorPlan.From(data).Walls.Any(w => w.Id == o.WallId)));
        var old = FloorPlan.FromRoomList([new Room("Küche", 3.5, 3), new Room("Bad", 2, 2)]);
        Assert.Equal(2, old.Rooms.Count); Assert.Equal(10.5, old.Rooms[0].Area / 10000, 6);
        Assert.Empty(FloorPlan.From(new JsonObject()).Walls);
    }

    [Fact(DisplayName = "T-0256: Exporte – PDF, SVG, DXF (Ebenen, Wände an Türen unterbrochen), Raumbuch, Materialliste; 3D mit Aussparungen")]
    public void Exports()
    {
        var d = Tmp();
        try
        {
            var p = FloorPlan.Template("wohnung");
            var svg = PlanDrawing.Svg(p);
            Assert.StartsWith("<svg", svg); Assert.Contains("Wohnzimmer", svg); Assert.Contains("<polygon", svg); Assert.Contains("1 m", svg);
            var dxf = PlanDrawing.Dxf(p);
            Assert.Contains("\nLAYER\n2\nWAENDE\n", dxf); Assert.Contains("\nARC\n", dxf); Assert.Contains("\nTEXT\n", dxf); Assert.EndsWith("EOF\n", dxf);
            // eine einzelne Wand mit Tür: 2 Kanten × 2 Abschnitte + 2 Leibungen + 2 freie Enden = 8 Linien auf WAENDE
            var one = new FloorPlan(); one.AddWall(0, 0, 400, 0); one.AddOpening("tuer", 200, 0);
            Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(PlanDrawing.Dxf(one), "\nLINE\n8\nWAENDE\n").Count);
            var pdf = PlanDrawing.Pdf(p, Path.Combine(d, "plan.pdf"));
            var bytes = File.ReadAllBytes(pdf); Assert.True(bytes.Length > 2000); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            var csv = PlanDrawing.RoomBookCsv(p);
            Assert.StartsWith("Raum;Art;Fläche", csv); Assert.Contains("Bad;Bad;", csv); Assert.Contains("Wohnfläche (WoFlV)", csv);
            Assert.Contains("Pakete", PlanDrawing.MaterialList(p));
            var parts = Plan3D.Build(one);
            Assert.Equal(3, parts.Count); // links, rechts, Sturz über der Tür
            var win = new FloorPlan(); win.AddWall(0, 0, 400, 0); win.AddOpening("fenster", 200, 0);
            Assert.Equal(4, Plan3D.Build(win).Count); // links, rechts, Brüstung, Sturz
        }
        finally { Directory.Delete(d, true); }
    }
}
