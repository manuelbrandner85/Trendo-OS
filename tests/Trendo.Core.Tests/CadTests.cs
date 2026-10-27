using System.IO.Compression;
using System.Xml.Linq;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>N23 Trendo CAD: Kern, Bauschritte, Parameter, Export.</summary>
public class CadTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-cad-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static CadDoc Bracket()
    {
        var d = new CadDoc { Params = { ["Breite"] = 60, ["Tiefe"] = 40, ["Dicke"] = 8, ["Loch"] = 6 } };
        d.Bodies[0].Name = "Winkel";
        d.Bodies[0].Features.Add(CadDoc.Feature(CadOp.Quader, ("breite", "Breite"), ("tiefe", "Tiefe"), ("hoehe", "Dicke")));
        d.Bodies[0].Features.Add(CadDoc.Feature(CadOp.Bohrung, ("durchmesser", "Loch"), ("x", "Breite/4"), ("y", "Tiefe/2")));
        d.Bodies[0].Features.Add(CadDoc.Feature(CadOp.Bohrung, ("durchmesser", "Loch"), ("x", "Breite*3/4"), ("y", "Tiefe/2")));
        return d;
    }

    [Fact(DisplayName = "N23: Boolesche Operationen – Rauminhalt von Quader minus Bohrung, Vereinigung und Schnitt stimmen")]
    public void Csg_volumes()
    {
        var box = CadShapes.Box(new Vec3(0, 0, 0), 20, 20, 10);
        Assert.Equal(4000, box.Volume(), 6);
        var hole = CadShapes.Cylinder(new Vec3(10, 10, -1), 3, 12, 96);
        Assert.InRange(box.Subtract(hole).Volume(), 4000 - Math.PI * 90 - 2, 4000 - Math.PI * 90 + 2); // Vieleck-Näherung
        var other = CadShapes.Box(new Vec3(10, 0, 0), 20, 20, 10);
        Assert.Equal(6000, box.Union(other).Volume(), 3);
        Assert.Equal(2000, box.Intersect(other).Volume(), 3);
        Assert.InRange(CadShapes.Sphere(new Vec3(0, 0, 0), 10, 96).Volume(), 4000 * Math.PI / 3 * 0.99, 4000 * Math.PI / 3 * 1.001);
    }

    [Fact(DisplayName = "N23: Parametrisch – Maße als Formeln, Änderung eines Parameters rechnet alles neu; Fehler werden verständlich gemeldet")]
    public void Parametric()
    {
        var d = Bracket();
        var full = 60 * 40 * 8 - 2 * Math.PI * 9 * 8;
        Assert.InRange(d.Build()[0].Solid.Volume(), full - 5, full + 5);
        d.Params["Breite"] = 80;
        var s = d.Build()[0].Solid;
        Assert.Equal(80, s.Bounds().Max.X, 6);
        Assert.InRange(s.Volume(), full + 20 * 40 * 8 - 5, full + 20 * 40 * 8 + 5);
        Assert.Equal(13, d.Eval("Breite/8 + 3"), 6);
        Assert.Equal(1, d.Eval("sin(90)"), 6);
        d.Bodies[0].Features[1]["durchmesser"] = "Unbekannt*2";
        var err = d.Build()[0].Error;
        Assert.Contains("Unbekanntes Maß „Unbekannt“", err);
        Assert.Contains("Schritt 2", err);
        var dir = Temp();
        try { var f = Path.Combine(dir, "winkel.tcad"); Bracket().Save(f); var back = CadDoc.Load(f); Assert.Equal(3, back.Bodies[0].Features.Count); Assert.Equal(60, back.Params["Breite"]); Assert.Equal(CadOp.Bohrung, back.Bodies[0].Features[1].Op); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "N23: Skizze extrudieren, Tasche, Drehteil, Rundung, Fase, Muster, Spiegeln, Drehen; Baugruppe mit Lage")]
    public void Features()
    {
        var d = new CadDoc();
        var b = d.Bodies[0];
        b.Features.Add(new CadFeature { Op = CadOp.Extrusion, Sketch = new CadSketch { Kind = "vieleck", Points = ["0;0", "30;0", "30;10", "10;10", "10;30", "0;30"] }, P = { ["hoehe"] = "5" } });
        Assert.Equal((30 * 10 + 10 * 20) * 5, d.BuildBody(b).Volume(), 3); // L-Form
        b.Features.Add(CadDoc.Feature(CadOp.Tasche, ("x", "2"), ("y", "2"), ("breite", "6"), ("tiefe", "6"), ("tiefe2", "2")));
        Assert.Equal(2500 - 72, d.BuildBody(b).Volume(), 3);
        var r = new CadBody { Features = [new CadFeature { Op = CadOp.Drehteil, Sketch = new CadSketch { X = "0", Y = "0", W = "10", H = "20" } }] };
        Assert.InRange(d.BuildBody(r).Volume(), Math.PI * 2000 * 0.99, Math.PI * 2000 * 1.001); // Zylinder aus gedrehtem Rechteck
        var q = new CadBody { Features = [CadDoc.Feature(CadOp.Quader, ("breite", "40"), ("tiefe", "20"), ("hoehe", "10")), CadDoc.Feature(CadOp.Rundung, ("radius", "5"))] };
        var expectR = 8000 - (4 - Math.PI) * 25 * 10;
        Assert.InRange(d.BuildBody(q).Volume(), expectR - 5, expectR + 5);
        q.Features[1].Op = CadOp.Fase;
        Assert.InRange(d.BuildBody(q).Volume(), 8000 - 2 * 25 * 10 - 1, 8000 - 2 * 25 * 10 + 1);
        var m = new CadBody { Features = [CadDoc.Feature(CadOp.Quader, ("breite", "10"), ("tiefe", "10"), ("hoehe", "10")), CadDoc.Feature(CadOp.Muster, ("anzahl", "4"), ("dx", "15"))] };
        Assert.Equal(4000, d.BuildBody(m).Volume(), 3); Assert.Equal(55, d.BuildBody(m).Bounds().Max.X, 6);
        m.Features.Add(CadDoc.Feature(CadOp.Spiegeln, ("ebene", "X"), ("bei", "0")));
        Assert.Equal(8000, d.BuildBody(m).Volume(), 3); Assert.Equal(-55, d.BuildBody(m).Bounds().Min.X, 6);
        m.Features.Add(CadDoc.Feature(CadOp.Drehen, ("achse", "Z"), ("winkel", "90")));
        Assert.Equal(55, d.BuildBody(m).Bounds().Max.Y, 5);
        d.Bodies.Add(new CadBody { Name = "Deckel", X = "100", Features = [CadDoc.Feature(CadOp.Zylinder, ("radius", "5"), ("hoehe", "2"))] });
        var built = d.Build();
        Assert.Equal(2, built.Count); Assert.Equal(95, built[1].Solid.Bounds().Min.X, 3);
    }

    [Fact(DisplayName = "N23: Export – STL (mm), 3MF mit Namen (geschlossenes Netz), STEP (facettiert), Zeichnung als PDF; ohne Programmname")]
    public void Export()
    {
        var dir = Temp();
        try
        {
            var solid = Bracket().Build()[0].Solid;
            var stl = Path.Combine(dir, "winkel.stl"); CadExport.WriteStl([solid], stl);
            var bytes = File.ReadAllBytes(stl);
            Assert.Equal(84 + 50 * BitConverter.ToInt32(bytes, 80), bytes.Length);
            Assert.DoesNotContain("Trendo", System.Text.Encoding.ASCII.GetString(bytes, 0, 80));
            var tmf = Path.Combine(dir, "winkel.3mf"); CadExport.Write3mf([("Winkel", "#2F86F6", solid)], tmf);
            using (var z = ZipFile.OpenRead(tmf))
            {
                var model = XDocument.Load(z.GetEntry("3D/3dmodel.model")!.Open());
                XNamespace ns = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
                Assert.Equal("millimeter", (string?)model.Root!.Attribute("unit"));
                var obj = model.Descendants(ns + "object").Single(); Assert.Equal("Winkel", (string?)obj.Attribute("name"));
                var tris = obj.Descendants(ns + "triangle").Select(t => new[] { (int)t.Attribute("v1")!, (int)t.Attribute("v2")!, (int)t.Attribute("v3")! }).ToList();
                var edgeCount = new Dictionary<(int, int), int>();
                foreach (var t in tris) for (var i = 0; i < 3; i++) { var a = t[i]; var c = t[(i + 1) % 3]; var k = a < c ? (a, c) : (c, a); edgeCount[k] = edgeCount.GetValueOrDefault(k) + 1; }
                var open = edgeCount.Count(e => e.Value != 2);
                Assert.True(open == 0, $"{open} von {edgeCount.Count} Kanten offen");
            }
            var step = Path.Combine(dir, "winkel.step"); CadExport.WriteStep([("Winkel", solid)], step);
            var st = File.ReadAllText(step);
            Assert.StartsWith("ISO-10303-21;", st); Assert.Contains("FACETED_BREP", st); Assert.Contains("SI_UNIT(.MILLI.,.METRE.)", st); Assert.DoesNotContain("Trendo", st);
            var pdf = Path.Combine(dir, "winkel.pdf"); CadExport.WriteDrawing([("Winkel", solid)], "Winkel", pdf);
            var raw = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdf)); Assert.StartsWith("%PDF", raw); Assert.DoesNotContain("Trendo", raw);
            Assert.Equal(12, CadExport.FeatureEdges(CadShapes.Box(new Vec3(0, 0, 0), 1, 1, 1)).Count);
        }
        finally { Directory.Delete(dir, true); }
    }
}
