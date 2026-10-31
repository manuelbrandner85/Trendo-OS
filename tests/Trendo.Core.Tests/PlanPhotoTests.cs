using Trendo.Core.Arch;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0280: Grundriss mit echten Möbelmodellen (3D-Ansicht, OBJ) und Foto-Szene für Blender.</summary>
public class T0280_PlanModels
{
    static string Glb(string type) { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return Path.Combine(d, "src", "Trendo.App", "Assets", "moebel", type + ".glb"); }

    [Fact(DisplayName = "T-0280: Modell steht auf dem Stellmaß des Plans – Grundfläche, Bodenhöhe, Lehne hinten (Plan −y), Drehung 90°")]
    public void Place_matches_item()
    {
        var m = Mesh3D.ReadMeshFile(Glb("sofa"));
        var i = new Item { Type = "sofa", X = 300, Y = 200, W = 220, D = 90 };
        var parts = Plan3D.Place(i, m, 50); var vs = parts.SelectMany(p => p.Solid.Polys.SelectMany(q => q.Verts)).ToList();
        Assert.All(parts, p => Assert.True(p.Solid.Smooth));
        Assert.InRange(vs.Min(v => v.X), 189, 191); Assert.InRange(vs.Max(v => v.X), 409, 411);
        Assert.InRange(vs.Min(v => v.Y), -246, -244); Assert.InRange(vs.Max(v => v.Y), -156, -154); // Plan y nach unten → Modell y nach oben
        Assert.InRange(vs.Min(v => v.Z), 49.9, 50.1);
        var high = vs.Where(v => v.Z > 50 + 70).ToList(); Assert.NotEmpty(high);
        Assert.True(high.Average(v => v.Y) > -200 + 20, "Lehne muss zur Plan-Rückseite (−y im Plan = +y im Modell) zeigen");
        i.Rot = 90; vs = Plan3D.Place(i, m, 0).SelectMany(p => p.Solid.Polys.SelectMany(q => q.Verts)).ToList();
        Assert.InRange(vs.Max(v => v.X) - vs.Min(v => v.X), 89, 91); Assert.InRange(vs.Max(v => v.Y) - vs.Min(v => v.Y), 219, 221);
    }

    [Fact(DisplayName = "T-0280: Vorschau-Vereinfachung – weniger Dreiecke, gleiche Außenmaße, Farben bleiben")]
    public void Simplify_keeps_shape()
    {
        var m = Mesh3D.ReadMeshFile(Glb("sessel")); var s = Plan3D.Simplify(m, 1.5);
        Assert.True(s.Triangles < m.Triangles && s.Triangles > 100, $"{m.Triangles} → {s.Triangles}");
        Assert.InRange(s.V.Max(v => v.X) - s.V.Min(v => v.X), (m.V.Max(v => v.X) - m.V.Min(v => v.X)) - 3, (m.V.Max(v => v.X) - m.V.Min(v => v.X)) + 3);
        Assert.Equal(s.Triangles, s.R.Count);
    }

    [Fact(DisplayName = "T-0280: glTF-Farben linear ↔ Bildschirm (sRGB) – Anthrazit-Stoff wird nicht schwarz, Hin und Zurück verlustfrei")]
    public void Colors_are_srgb()
    {
        var sofa = Mesh3D.ReadMeshFile(Glb("sofa"));
        var fabric = Enumerable.Range(0, sofa.Triangles).GroupBy(t => (sofa.R[t], sofa.G[t], sofa.B[t])).OrderByDescending(g => g.Count()).First().Key;
        Assert.InRange((int)fabric.Item1, 60, 90); // linear 0,07 ≈ sRGB 74 (vorher 18 = fast schwarz)
        var m = new Mesh { CR = 75, CG = 140, CB = 200 }; m.Add(new(0, 0, 0), new(10, 0, 0), new(0, 10, 0));
        var f = Path.Combine(Path.GetTempPath(), $"trendo-t0280-{Guid.NewGuid():N}.glb");
        try { Mesh3D.WriteGlb(m, f); var r = Mesh3D.ReadMeshFile(f); Assert.Equal((75, 140, 200), ((int)r.R[0], (int)r.G[0], (int)r.B[0])); }
        finally { File.Delete(f); }
    }

    [Fact(DisplayName = "T-0280: Foto-Szene – geschlossene Wandkörper, Fenster/Türen, Räume mit Belag, Katalogmöbel; Bibliotheksmodell mit Datei, ohne Datei weggelassen")]
    public void Scene_for_blender()
    {
        var p = FloorPlan.Template("wohnung");
        p.Items.Add(new Item { Type = "bib:Poly Haven/stuhl", Name = "Stuhl", X = 100, Y = 100, W = 45, D = 50 });
        p.Items.Add(new Item { Type = "bib:Eigene Datei/fehlt", Name = "Fehlt", X = 120, Y = 100, W = 45, D = 50 });
        var s = PlanPhoto.Scene([p], t => t == "bib:Poly Haven/stuhl" ? "/bib/stuhl.gltf" : null, "raum", p.Rooms[0].Id, 800, 500, 32);
        var walls = s["walls"]!.AsArray(); Assert.True(walls.Count >= p.Walls.Count);
        Assert.All(walls, w => Assert.True(w!.AsArray().Count >= 6)); // jeder Körper geschlossen (mindestens 6 Flächen)
        Assert.Equal(p.Openings.Count(o => o.Kind == "fenster"), s["openings"]!.AsArray().Count(o => (string)o!["kind"]! == "fenster"));
        Assert.Equal(p.Rooms.Count, s["rooms"]!.AsArray().Count);
        var items = s["items"]!.AsArray(); Assert.Equal(p.Items.Count - 1, items.Count);
        Assert.Equal("/bib/stuhl.gltf", (string)items.Single(i => (string)i!["type"]! == "bib:Poly Haven/stuhl")!["file"]!);
        Assert.DoesNotContain(items, i => i!["type"]!.ToString().Contains("fehlt"));
        var sofa = items.First(i => (string)i!["type"]! == "sofa")!; Assert.Equal(0.85, (double)sofa["h"]!, 3); // Katalog-Höhe in Metern
        Assert.Equal("raum", (string)s["mode"]!); Assert.Equal(32, (int)s["samples"]!);
    }

    [Fact(DisplayName = "T-0280: Fortschritt aus Cycles-Zeilen; Bibliotheksmöbel lassen sich wie Katalogmöbel stellen")]
    public void Progress_and_library_type()
    {
        Assert.Equal(0.25, PlanPhoto.Progress("Fra:1 Mem:120.5M (Peak 300M) | Time:00:03.12 | Remaining:00:09.01 | Mem:80M, Peak:90M | Scene, ViewLayer | Sample 64/256"));
        Assert.Null(PlanPhoto.Progress("Fra:1 Mem:12M | Synchronizing object | Sofa"));
        FurnitureCatalog.Register(new FurnitureType("bib:Test/hocker", "Hocker", "3D-Bibliothek", 38, 41, 58));
        var p = new FloorPlan(); var i = p.AddItem("bib:Test/hocker", 100, 100);
        Assert.Equal((38.0, 41.0, "Hocker"), (i.W, i.D, i.Name));
    }
}

/// <summary>T-0281: Modell-Prüfer misst mehrteilige Modelle richtig (Knoten-Verschiebungen eingerechnet).</summary>
public class T0281_XrSize
{
    [Fact(DisplayName = "T-0281: Größe eines mehrteiligen Blender-Modells (Sofa 220 × 90 × 85 cm) stimmt – glTF-Reihenfolge Breite, Höhe, Tiefe in m")]
    public void Sofa_size()
    {
        var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!;
        var det = XrPlus.Detail(Path.Combine(d, "src", "Trendo.App", "Assets", "moebel", "sofa.glb"));
        Assert.InRange(det.SizeX, 2.15, 2.30); Assert.InRange(det.SizeY, 0.80, 0.92); Assert.InRange(det.SizeZ, 0.85, 0.98);
    }
}
