using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Arch;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 3 – SPEC-01: Grundriss → IFC4 über IfcOpenShell. Echter Lauf nur mit TRENDO_IFC_PYTHON (Python einer Umgebung mit ifcopenshell),
/// sonst übersprungen. Beleg: reports/alle-studios/ifc-e2e.md.</summary>
public class IfcExchangeTests
{
    static FloorPlan Box()
    {
        var p = new FloorPlan { Name = "Probe" };
        p.Walls.Add(new Wall { Id = "a", X1 = 0, Y1 = 0, X2 = 500, Y2 = 0, Thickness = 24, Height = 250 });
        p.Walls.Add(new Wall { Id = "b", X1 = 500, Y1 = 0, X2 = 500, Y2 = 400, Thickness = 24, Height = 250 });
        p.Openings.Add(new Opening { WallId = "a", Kind = "tuer", At = 150, Width = 90, Height = 201 });
        p.Openings.Add(new Opening { WallId = "b", Kind = "fenster", At = 200, Width = 120, Height = 120, Sill = 90 });
        p.Rooms.Add(new PlanRoom { Name = "Wohnen", Points = [new() { X = 12, Y = 12 }, new() { X = 488, Y = 12 }, new() { X = 488, Y = 388 }, new() { X = 12, Y = 388 }] });
        return p;
    }
    static JsonObject J(FloorPlan p) => (JsonObject)JsonNode.Parse(IfcExchange.ToJson(p))!;

    [Fact(DisplayName = "IFC +1: Zentimeter → Meter, y nach oben, Projektname und Geschoss")]
    public void Units_and_axes()
    {
        var j = J(Box()); var w = j["waende"]![1]!;
        Assert.Equal("Probe", (string)j["projekt"]!); Assert.Equal("Erdgeschoss", (string)j["geschoss"]!);
        Assert.Equal(2.5, (double)w["hoehe"]!); Assert.Equal(0.24, (double)w["staerke"]!);
        Assert.Equal(-4.0, (double)w["y2"]!); // y nach unten (Bildschirm) → nach oben (IFC)
    }

    [Fact(DisplayName = "IFC +2: Wandachse um halbe Dicke verschoben – IfcOpenShell-Körper links davon sitzt mittig auf Trendos Achse")]
    public void Axis_shift()
    {
        var w = J(Box())["waende"]![0]!; // Trendo (0,0)→(500,0): IFC-Richtung +x, Körper links (+y) → Achse bei y = −0,12
        Assert.Equal(0.0, (double)w["x1"]!); Assert.Equal(-0.12, (double)w["y1"]!, 6); Assert.Equal(-0.12, (double)w["y2"]!, 6);
        var w2 = J(Box())["waende"]![1]!; // (500,0)→(500,400) wird (5,0)→(5,−4): Richtung −y, Körper links (+x) → Achse bei x = 4,88
        Assert.Equal(4.88, (double)w2["x1"]!, 6);
    }

    [Fact(DisplayName = "IFC +3: Öffnungen – Anfang statt Mitte, Art, Brüstung, Bezug auf die Wandnummer")]
    public void Openings()
    {
        var o = J(Box())["oeffnungen"]!.AsArray();
        Assert.Equal(2, o.Count);
        Assert.Equal((0, "tuer", 1.05, 0.9, 0.0), ((int)o[0]!["wand"]!, (string)o[0]!["art"]!, (double)o[0]!["bei"]!, (double)o[0]!["breite"]!, (double)o[0]!["bruestung"]!));
        Assert.Equal((1, "fenster", 1.4, 0.9), ((int)o[1]!["wand"]!, (string)o[1]!["art"]!, (double)o[1]!["bei"]!, (double)o[1]!["bruestung"]!));
    }

    [Fact(DisplayName = "IFC +4: Räume als Umriss in Metern mit Höhe")]
    public void Rooms()
    {
        var r = J(Box())["raeume"]![0]!;
        Assert.Equal("Wohnen", (string)r["name"]!); Assert.Equal(2.5, (double)r["hoehe"]!);
        Assert.Equal(4, r["umriss"]!.AsArray().Count); Assert.Equal(-3.88, (double)r["umriss"]![2]![1]!, 6);
    }

    [Fact(DisplayName = "IFC −1: Nullwand, Öffnung ohne Wand, Öffnung außerhalb der Wand und Raum mit 2 Punkten werden ausgelassen")]
    public void Skip_invalid()
    {
        var p = Box();
        p.Walls.Add(new Wall { Id = "null", X1 = 10, Y1 = 10, X2 = 10, Y2 = 10 });
        p.Openings.Add(new Opening { WallId = "gibtsnicht", At = 50 });
        p.Openings.Add(new Opening { WallId = "a", At = 495, Width = 90 });
        p.Rooms.Add(new PlanRoom { Name = "Strich", Points = [new() { X = 0, Y = 0 }, new() { X = 1, Y = 1 }] });
        var j = J(p);
        Assert.Equal(2, j["waende"]!.AsArray().Count); Assert.Equal(2, j["oeffnungen"]!.AsArray().Count); Assert.Single(j["raeume"]!.AsArray());
    }

    [Fact(DisplayName = "IFC +5: Vorlage „2-Zimmer-Wohnung“ – alle Wände, Türen, Fenster und Räume kommen an")]
    public void Template_complete()
    {
        var p = FloorPlan.Template("wohnung"); var j = J(p);
        Assert.Equal(p.Walls.Count(w => w.Length >= 1), j["waende"]!.AsArray().Count);
        Assert.Equal(p.Rooms.Count, j["raeume"]!.AsArray().Count);
        Assert.True(j["oeffnungen"]!.AsArray().Count >= p.Openings.Count - 1);
    }

    [Fact(DisplayName = "IFC +6: Kurzbericht aus „info“ – Anzahl je Bauteil als Satz")]
    public void Parse_info()
    {
        var i = IfcExchange.ParseInfo("{\"schema\":\"IFC4\",\"projekt\":\"Musterwohnung\",\"anzahl\":{\"IfcWall\":4,\"IfcDoor\":1,\"IfcWindow\":1,\"IfcSpace\":1,\"IfcBuildingStorey\":1},\"geschosse\":[\"Erdgeschoss\"],\"raeume\":[\"Wohnzimmer\"]}");
        Assert.Equal("IFC4: 4 Wände, 1 Türen, 1 Fenster, 1 Räume, 1 Geschoss", i.Text); Assert.Equal(["Wohnzimmer"], i.Rooms);
        Assert.Throws<FormatException>(() => IfcExchange.ParseInfo("{\"schema\":\"STEP\",\"anzahl\":{}}"));
    }

    [Fact(DisplayName = "IFC −2: Dateiname nur aus sicheren Zeichen")]
    public void File_name()
    {
        Assert.Equal("Wohnung-Müller-2-OG.ifc", IfcExchange.FileName("Wohnung Müller / 2. OG"));
        Assert.Equal("grundriss.ifc", IfcExchange.FileName("../..")); Assert.Equal("grundriss.ifc", IfcExchange.FileName(""));
    }

    public sealed class IfcFactAttribute : FactAttribute
    {
        public IfcFactAttribute() { if (Python() is null) Skip = "Übersprungen – TRENDO_IFC_PYTHON nicht gesetzt (Python mit ifcopenshell)."; }
    }
    static string? Python() => Environment.GetEnvironmentVariable("TRENDO_IFC_PYTHON") is { Length: > 0 } p && File.Exists(p) ? p : null;
    static string Script() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent; return Path.Combine(d!.FullName, "src", "Trendo.App", "Assets", "ki", "trendo_ifc.py"); }

    [IfcFact(DisplayName = "IFC echt: Vorlage „2-Zimmer-Wohnung“ → IFC4 mit IfcOpenShell, wieder eingelesen, gleiche Anzahl Bauteile")]
    public void Real_template_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ifc-echt-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var p = FloorPlan.Template("wohnung"); var json = Path.Combine(dir, "plan.json"); var ifc = Path.Combine(dir, "wohnung.ifc"); var info = Path.Combine(dir, "info.json");
            File.WriteAllText(json, IfcExchange.ToJson(p, "Musterwohnung"));
            var env = new Dictionary<string, string> { ["PYTHONUTF8"] = "1", ["PYTHONIOENCODING"] = "utf-8" };
            var r = Cli.Run(Python()!, ["-I", Script(), "repo", "export", json, ifc], dir, 120_000, env); Assert.True(r.Ok, r.Err);
            var r2 = Cli.Run(Python()!, ["-I", Script(), "repo", "info", ifc, info], dir, 120_000, env); Assert.True(r2.Ok, r2.Err);
            var i = IfcExchange.ParseInfo(File.ReadAllText(info)); var j = J(p);
            Assert.Equal("IFC4", i.Schema); Assert.Equal("Musterwohnung", i.Project);
            Assert.Equal(j["waende"]!.AsArray().Count, i.Walls); Assert.Equal(j["raeume"]!.AsArray().Count, i.Spaces);
            Assert.Equal(j["oeffnungen"]!.AsArray().Count, i.Doors + i.Windows);
            Assert.Equal(p.Rooms.Select(x => x.Name).OrderBy(x => x), i.Rooms.OrderBy(x => x));
            if (Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE_BELEG") is { Length: > 0 } b) { Directory.CreateDirectory(b); File.Copy(ifc, Path.Combine(b, "ifc-musterwohnung.ifc"), true); File.WriteAllText(Path.Combine(b, "ifc-musterwohnung.info.txt"), i.Text); }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
