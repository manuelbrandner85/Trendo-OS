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
        var front = Mesh3D.RenderPng(scene, "front", 80, 60)!;
        var top = Mesh3D.RenderPng(scene, "top", 80, 60)!;
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, front[..4]);
        Assert.NotEqual(front, top);
        Assert.Null(Mesh3D.RenderPng(new Scene3D(), "front", 80, 60));
    }
}
