using Trendo.Core;
using Xunit;

public class Scene3DTests
{
    static Mesh3D Welt(params string[] formen)
    {
        var s = new Scene3D();
        foreach (var f in formen) s.Hinzu(f);
        return s.Welt();
    }

    [Fact(DisplayName = "3D: Grundformen haben Volumen und die erwarteten Maße")]
    public void Grundformen()
    {
        var cube = Poly3D.Box(10, 10, 10).ToMesh();
        Assert.InRange(cube.Volume(), 999, 1001);
        var (min, max) = cube.Bounds();
        Assert.Equal(10, max.X - min.X, 3);
        Assert.InRange(Poly3D.Sphere(10, 10, 10).ToMesh().Volume(), 400, 600);
        Assert.InRange(Poly3D.Cylinder(6, 6, 12).ToMesh().Volume(), 300, 360);
        Assert.InRange(Poly3D.Cone(8, 8, 12).ToMesh().Volume(), 150, 220);
        var torus = Poly3D.Torus(12, 12, 3).ToMesh();
        Assert.True(torus.Volume() > 20, "Torus-Volumen " + torus.Volume());
        Assert.True(Poly3D.Text("Hallo", 12, 4, 1).ToMesh().T.Count > 20);
        Assert.Equal(6, Poly3D.Box(1, 1, 1).F.Count);
    }

    [Fact(DisplayName = "3D: Kanten abrunden schneidet die Ecke ab und bleibt geschlossen")]
    public void Abrunden()
    {
        var roh = Poly3D.Box(10, 10, 10);
        var fein = Poly3D.Bevel(roh, 1).ToMesh().Weld();
        Assert.True(fein.Volume() > 700 && fein.Volume() < 1000, "Volumen " + fein.Volume());
        Assert.DoesNotContain(fein.V, v => Math.Abs(v.X) > 4.6 && Math.Abs(v.Y) > 4.6 && Math.Abs(v.Z) > 4.6);
        Assert.Empty(fein.BoundaryLoops());
    }

    [Fact(DisplayName = "3D: Loch bohren geht durch den Würfel und lässt den Rand stehen")]
    public void Loch()
    {
        var mesh = Poly3D.Drill(Poly3D.Box(10, 10, 10), 'z', 2, 0, 0).ToMesh().Weld();
        Assert.Equal(0, mesh.RayHits(new Vec3(0, 0, -20), new Vec3(0, 0, 1)));
        Assert.Equal(2, mesh.RayHits(new Vec3(4, 0.4, -20), new Vec3(0, 0, 1)));
        Assert.InRange(mesh.Volume(), 700, 950);
        Assert.Empty(mesh.BoundaryLoops());
    }

    [Fact(DisplayName = "3D: Glätten vermehrt Flächen, Aushöhlen gibt einer Fläche Dicke")]
    public void Glaetten_und_Wand()
    {
        var vorher = Poly3D.Box(10, 10, 10).ToMesh().T.Count;
        var nachher = Poly3D.Smooth(Poly3D.Box(10, 10, 10)).ToMesh();
        Assert.True(nachher.T.Count > vorher);
        var platte = Poly3D.Solidify(Poly3D.Plane(10, 8), 1).ToMesh();
        var (a, b) = platte.Bounds();
        Assert.InRange(b.Z - a.Z, 0.9, 1.1);
        Assert.True(platte.Volume() > 70);
        Assert.Empty(platte.BoundaryLoops());
    }

    [Fact(DisplayName = "3D: Teile zusammenbauen, ändern, spiegeln, rückgängig")]
    public void Szene()
    {
        var s = new Scene3D { Name = "Tisch" };
        Assert.Contains("hinzugefügt", s.Hinzu("wuerfel"));
        Assert.Contains("hinzugefügt", s.Hinzu("zylinder"));
        Assert.Equal(2, s.Teile.Count);
        Assert.Contains("übernommen", s.Setzen(0, "Platte", 20, 10, 2, 0, 0, 1, 0, 0, 0, "#C4A574", null));
        Assert.Equal(20, s.Teile[0].Bx);
        Assert.Contains("abgerundet", s.Werkzeug(0, "bevel", 0.3, ""));
        Assert.Contains("Gespiegelt", s.Werkzeug(1, "spiegel", 0, "x"));
        Assert.Equal(3, s.Teile.Count);
        Assert.NotEqual(s.Teile[1].X, s.Teile[2].X);
        var json = s.ToJson();
        var wieder = Scene3D.FromJson(json);
        Assert.Equal("Tisch", wieder.Name);
        Assert.Equal(3, wieder.Teile.Count);
        Assert.Equal(20, wieder.Teile[0].Bx);
        Assert.Contains("Größe", s.Setzen(0, "x", 0, 1, 1, 0, 0, 0, 0, 0, 0, "#112233", null));
        Assert.Contains("rückgängig", s.Rueckgaengig());
        Assert.Equal(2, s.Teile.Count);
        while (s.Teile.Count > 0) s.Rueckgaengig();
        Assert.Contains("Nichts", s.Rueckgaengig());
    }

    [Fact(DisplayName = "3D: KI-JSON wird zur Teileliste, ohne JSON gibt es einen klaren Fehler")]
    public void Ki_lesen()
    {
        var json = """
        ```json
        {"teile":[
          {"form":"wuerfel","name":"Platte","bx":120,"by":80,"bz":4,"x":0,"y":0,"z":40,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":52,"y":32,"z":19,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":-52,"y":32,"z":19,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":52,"y":-32,"z":19,"farbe":"#C4A574"},
          {"form":"zylinder","name":"Bein","bx":5,"by":5,"bz":38,"x":-52,"y":-32,"z":19,"farbe":"#C4A574"}
        ]}
        ```
        """;
        Assert.True(Scene3DAi.TryLesen(json, out var teile, out var fehler), fehler);
        Assert.Equal(5, teile.Count);
        Assert.Equal(120, teile[0].Bx);
        var s = new Scene3D();
        s.Ersetzen(teile);
        var (a, b) = s.Welt().Bounds();
        Assert.InRange(b.X - a.X, 110, 140);
        Assert.InRange(b.Z - a.Z, 40, 50);
        Assert.Contains("Zentimeter", Scene3DAi.Auftrag("ein Tisch"));
        Assert.False(Scene3DAi.TryLesen("kein json", out _, out var warum));
        Assert.False(string.IsNullOrWhiteSpace(warum));
    }

    [Fact(DisplayName = "3D: OBJ, STL, GLB und FBX hin und zurück, Vorschau ist ein PNG")]
    public void Dateien()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-3d-io-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var s = new Scene3D();
        s.Hinzu("wuerfel");
        s.Setzen(0, "Klotz", 10, 6, 4, 0, 0, 2, 0, 0, 0, "#E11D48", null);
        var mesh = s.Welt();
        var obj = Path.Combine(dir, "klotz.obj");
        MeshIo.WriteObj(obj, mesh);
        var gelesen = MeshIo.ReadObj(obj);
        Assert.Single(gelesen);
        Assert.Equal(mesh.T.Count, gelesen[0].Mesh.T.Count);
        Assert.Equal(0xE11D48, gelesen[0].Farbe);

        var stl = Path.Combine(dir, "klotz.stl");
        MeshIo.WriteStl(stl, mesh);
        var st = MeshIo.ReadStl(stl, true);
        Assert.Equal(mesh.T.Count, st.Mesh.T.Count);
        Assert.InRange(st.Mesh.Bounds().Max.X, 4.9, 5.1); // 10 cm → 100 mm → wieder 5 cm halbe Breite? center at x=0 size 10, half is 5. STL *10 then /10.

        var glb = Path.Combine(dir, "klotz.glb");
        MeshIo.WriteGlb(glb, mesh);
        var g = MeshIo.ReadGlb(glb);
        Assert.NotEmpty(g);
        Assert.Equal(mesh.T.Count, g.Sum(x => x.Mesh.T.Count));
        Assert.InRange(g[0].Mesh.Bounds().Max.X - g[0].Mesh.Bounds().Min.X, 9.5, 10.5);

        var fbx = Path.Combine(dir, "klotz.fbx");
        MeshIo.WriteFbx(fbx, mesh);
        var f = MeshIo.ReadFbx(fbx);
        Assert.Equal(mesh.T.Count, f.Mesh.T.Count);
        var bin = Path.Combine(dir, "bin.fbx");
        File.WriteAllBytes(bin, "Kaydara FBX Binary  \0"u8.ToArray());
        Assert.Throws<MeshIo.ImportException>(() => MeshIo.ReadFbx(bin));

        var png = MeshIo.RenderPng(mesh, 0);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]);
        Assert.True(png.Distinct().Count() > 8);
        Directory.Delete(dir, true);
    }
}
