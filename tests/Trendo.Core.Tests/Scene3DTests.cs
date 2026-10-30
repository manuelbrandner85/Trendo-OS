using Trendo.Core;
using Xunit;

public class Scene3D_Teile
{
    static Part3D Würfel(double s = 10, params PartOp[] ops) => new() { Name = "Klotz", Shape = "cube", Sx = s, Sy = s, Sz = s, Color = "#2F86F6", Ops = [.. ops] };

    [Fact(DisplayName = "3D: Würfel hat Volumen, die Mitte liegt innen, ein Punkt daneben außen")]
    public void Cube_contains_center()
    {
        var m = Mesh3D.BuildWorld(Würfel());
        Assert.Equal(12, m.Triangles);
        Assert.True(Mesh3D.Contains(m, new(0, 0, 0)));
        Assert.True(Mesh3D.Contains(m, new(4.5, 0, 0)));
        Assert.False(Mesh3D.Contains(m, new(6, 0, 0)));
    }

    [Fact(DisplayName = "3D: Kugel, Zylinder, Kegel und Torus sind geschlossen und haben eine Mitte bzw. einen Ring")]
    public void Primitives()
    {
        foreach (var shape in new[] { "sphere", "cylinder", "cone" })
        {
            var m = Mesh3D.BuildWorld(new Part3D { Shape = shape, Sx = 10, Sy = 10, Sz = 10 });
            Assert.True(m.Triangles > 12, shape);
            Assert.True(Mesh3D.Contains(m, new(0, 0, 0)), shape);
            Assert.False(Mesh3D.Contains(m, new(20, 0, 0)), shape);
        }
        var torus = Mesh3D.BuildWorld(new Part3D { Shape = "torus", Sx = 10, Sy = 2, Sz = 2 });
        Assert.True(torus.Triangles > 20);
        Assert.False(Mesh3D.Contains(torus, new(0, 0, 0)));
        Assert.True(Mesh3D.Contains(torus, new(4, 0, 0)));
    }

    [Fact(DisplayName = "3D: Kanten abrunden schneidet die Ecke ab")]
    public void Bevel_cuts_corner()
    {
        var m = Mesh3D.BuildWorld(Würfel(10, new PartOp { Kind = "bevel", Amount = 1 }));
        Assert.True(Mesh3D.Contains(m, new(0, 0, 0)));
        Assert.True(Mesh3D.Contains(m, new(4.2, 0, 0)));
        Assert.False(Mesh3D.Contains(m, new(4.9, 4.9, 4.9)));
    }

    [Fact(DisplayName = "3D: Loch durch den Würfel, die Mitte ist leer, der Rand bleibt")]
    public void Hole_drills_cube()
    {
        var m = Mesh3D.BuildWorld(Würfel(10, new PartOp { Kind = "hole", Diameter = 4, Axis = "z" }));
        Assert.False(Mesh3D.Contains(m, new(0, 0, 0)));
        Assert.True(Mesh3D.Contains(m, new(3.5, 0, 0)));
        Assert.False(Mesh3D.Contains(m, new(0, 0, 8)));
    }

    [Fact(DisplayName = "3D: Spiegeln verdoppelt die Breite")]
    public void Mirror_doubles()
    {
        var m = Mesh3D.BuildWorld(Würfel(10, new PartOp { Kind = "mirror", Axis = "x" }));
        var min = m.V.Min(v => v.X); var max = m.V.Max(v => v.X);
        Assert.InRange(max - min, 19.5, 20.5);
    }

    [Fact(DisplayName = "3D: Aushöhlen lässt die Mitte leer und die Wand stehen")]
    public void Shell_hollows()
    {
        var m = Mesh3D.BuildWorld(Würfel(10, new PartOp { Kind = "shell", Amount = 1 }));
        Assert.False(Mesh3D.Contains(m, new(0, 0, 0)));
        Assert.True(Mesh3D.Contains(m, new(4.5, 0, 0)));
    }

    [Fact(DisplayName = "3D: Text wird zu Flächen, OBJ/STL/GLB kommen wieder herein")]
    public void Text_and_roundtrip()
    {
        var text = Mesh3D.BuildWorld(new Part3D { Shape = "text", Text = "Hi", Sx = 8, Sy = 1, Sz = 3 });
        Assert.True(text.Triangles > 10);
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mesh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cube = Mesh3D.BuildWorld(Würfel());
        Mesh3D.WriteObj(cube, Path.Combine(dir, "k.obj"));
        var back = Mesh3D.Import(Path.Combine(dir, "k.obj"));
        Assert.Equal("mesh", back.Shape);
        Assert.InRange(back.Sx, 9.5, 10.5);
        Mesh3D.WriteStl(cube, Path.Combine(dir, "k.stl"));
        var stl = File.ReadAllBytes(Path.Combine(dir, "k.stl"));
        Assert.Equal(84 + 12 * 50, stl.Length);
        var x = BitConverter.ToSingle(stl, 84 + 12 + 0);
        Assert.InRange(Math.Abs(x), 40, 60);
        Mesh3D.WriteGlb(cube, Path.Combine(dir, "k.glb"));
        var glb = Mesh3D.Import(Path.Combine(dir, "k.glb"));
        Assert.InRange(glb.Sx, 9.5, 10.5);
        Assert.Equal("#2F86F6", glb.Color);
        var objBack = Mesh3D.Import(Path.Combine(dir, "k.obj"));
        Assert.Equal("#2F86F6", objBack.Color);
        Mesh3D.WriteFbx(cube, Path.Combine(dir, "k.fbx"));
        var fbx = Mesh3D.Import(Path.Combine(dir, "k.fbx"));
        Assert.InRange(fbx.Sx, 9.5, 10.5);
        Assert.Equal(cube.Triangles, Mesh3D.FromStored(fbx).Triangles);
        File.WriteAllBytes(Path.Combine(dir, "bin.fbx"), "Kaydara FBX Binary  \0"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() => Mesh3D.Import(Path.Combine(dir, "bin.fbx")));
        Assert.Throws<InvalidOperationException>(() => Mesh3D.Import(Path.Combine(dir, "k.obj") + ".exe"));
    }

    [Fact(DisplayName = "3D: Szene speichern und lesen, Bauplan der KI, Projekt-ID")]
    public void Scene_and_plan()
    {
        var scene = new Scene3D { Name = "Tisch" };
        scene.Parts.Add(Würfel());
        var again = Scene3D.FromJson(scene.ToJson());
        Assert.Equal("Tisch", again.Name);
        Assert.Equal("Klotz", again.Parts[0].Name);
        Assert.Contains("Würfel", again.Parts[0].Line());
        Assert.Equal("mein-tisch", Scene3D.Slug("Mein Tisch"));
        Assert.True(ScenePlan.TryRead("{\"parts\":[{\"name\":\"Platte\",\"shape\":\"cube\",\"sx\":120,\"sy\":80,\"sz\":4,\"z\":40,\"color\":\"#8B5A2B\"}]}", out var parts, out var err), err);
        Assert.Equal(120, parts[0].Sx);
        Assert.Equal("#8B5A2B", parts[0].Color);
        Assert.Equal(40, parts[0].Z);
        Assert.True(ScenePlan.TryRead("{\"parts\":[{\"shape\":\"cube\",\"sx\":10,\"sy\":10,\"sz\":8}]}", out var ohneZ, out var zErr), zErr);
        Assert.Equal(4, ohneZ[0].Z);
        Assert.Contains("Boden", ScenePlan.Prompt("Tisch"));
        Assert.False(ScenePlan.TryRead("kein json", out _, out var why));
        Assert.Contains("Bauplan", why);
        var spec = scene.WriteBlenderSpec(Path.Combine(Path.GetTempPath(), "trendo-spec-" + Guid.NewGuid().ToString("N")));
        Assert.Contains("Klotz", File.ReadAllText(spec));
        Assert.Contains("sys.argv", Studio3D.SceneScript);
        Assert.DoesNotContain("Klotz", Studio3D.SceneScript);
    }

    [Fact(DisplayName = "3D: Vorschau ist ein PNG und ändert sich mit der Ansicht")]
    public void Preview_png()
    {
        var scene = new Scene3D();
        scene.Parts.Add(Würfel());
        scene.Parts.Add(new Part3D { Shape = "cube", Sx = 4, Sy = 4, Sz = 12, X = 10, Color = "#E23B3B" });
        var front = Mesh3D.RenderPng(scene, "front", 80, 60)!;
        var top = Mesh3D.RenderPng(scene, "top", 80, 60)!;
        var back = Mesh3D.RenderPng(scene, "back", 80, 60)!;
        var angle = Mesh3D.RenderPng(scene, "angle", 80, 60)!;
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, front[..4]);
        Assert.NotEqual(front, top);
        Assert.NotEqual(front, back);
        Assert.NotEqual(front, angle);
        Assert.Null(Mesh3D.RenderPng(new Scene3D(), "front", 80, 60));
    }

    [Fact(DisplayName = "T-0242: glTF mit Y oben in Metern – ein 10×20×40-cm-Quader (40 cm hoch) steht im GLB 0,4 m hoch und kommt unverändert zurück")]
    public void Gltf_axes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-gltf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Asymmetrisch: Breite X 10 cm, Tiefe Y 20 cm, Höhe Z 40 cm (Trendo: Z oben)
            var box = Mesh3D.BuildWorld(new Part3D { Name = "Quader", Shape = "cube", Sx = 10, Sy = 20, Sz = 40, Color = "#2F86F6" });
            var file = Path.Combine(dir, "q.glb");
            Mesh3D.WriteGlb(box, file);
            if (Environment.GetEnvironmentVariable("TRENDO_GLTF_OUT") is { Length: > 0 } outFile) File.Copy(file, outFile, true); // für die Gegenprobe in Blender
            // GLB-JSON lesen: Ausdehnung der Positionen je Achse (glTF: X rechts, Y oben, Z nach vorn)
            var bytes = File.ReadAllBytes(file);
            var len = BitConverter.ToInt32(bytes, 12);
            using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, len));
            var acc = doc.RootElement.GetProperty("accessors")[0];
            double Ext(int k) => acc.GetProperty("max")[k].GetDouble() - acc.GetProperty("min")[k].GetDouble();
            Assert.InRange(Ext(0), 0.099, 0.101); // Breite
            Assert.InRange(Ext(1), 0.399, 0.401); // Höhe → glTF Y
            Assert.InRange(Ext(2), 0.199, 0.201); // Tiefe → glTF Z
            // Rundreise: Maße und Höhe bleiben, nichts gespiegelt
            var back = Mesh3D.Import(file);
            Assert.InRange(back.Sx, 9.9, 10.1);
            Assert.InRange(back.Sy, 19.9, 20.1);
            Assert.InRange(back.Sz, 39.9, 40.1);
            var m = Mesh3D.FromStored(back);
            Assert.Equal(box.Triangles, m.Triangles);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
