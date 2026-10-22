using Trendo.Core;
using Xunit;

public class W4_Core05_3D
{
    [Fact(DisplayName = "CORE-05: Echtes Blender – Modell anlegen, Vorschau rendern, in glTF/OBJ/STL/FBX umwandeln, Inhalt prüfen")]
    public void Blender_roundtrip()
    {
        var bl = Cli.Find("blender");
        Assert.SkipUnless(bl is not null, "Übersprungen: Blender ist nicht installiert.");
        var dir = Path.Combine(Path.GetTempPath(), "trendo-3d-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var (r, blend) = Studio3D.Create(bl, dir, "monkey", "Affe");
        Assert.True(r.Ok, r.Err + r.Out); Assert.True(File.Exists(blend));
        var (p, png) = Studio3D.Preview(bl, blend);
        Assert.True(p.Ok, p.Err); Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(png)[..4]);
        foreach (var ext in new[] { ".glb", ".obj", ".stl", ".fbx" })
        {
            var (c, o) = Studio3D.Convert(bl, blend, ext);
            Assert.True(c.Ok && new FileInfo(o).Length > 0, ext + c.Err);
        }
        var info = Studio3D.Inspect(bl, Path.Combine(dir, "Affe.glb"));
        Assert.NotNull(info); Assert.Equal(1, info!.Objects); Assert.Equal(968, info.Faces); // Suzanne trianguliert im glTF
        var (t, txt) = Studio3D.Create(bl, dir, "text", "Logo", ".stl", "#FF0000", "Hallo");
        Assert.True(t.Ok, t.Err); Assert.True(new FileInfo(txt).Length > 1000);
        var (_, again) = Studio3D.Create(bl, dir, "cube", "Affe");
        Assert.EndsWith("Affe-2.blend", again);
    }

    [Fact(DisplayName = "CORE-05: Unbekannte Formate und Formen werden abgewiesen")]
    public void Rejects()
    {
        Assert.False(Studio3D.Convert("blender", "/x/datei.exe", ".glb").R.Ok);
        Assert.Throws<ArgumentException>(() => Studio3D.Create("blender", "/tmp", "drache", "x"));
        Assert.Contains("sys.argv", Studio3D.Script);
    }
}
