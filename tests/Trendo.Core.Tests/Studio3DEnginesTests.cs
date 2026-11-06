using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S2-17: Export für Unity, Unreal, Godot, Web mit Rundlauf-Vergleich (Messwerte wie aus dem echten Blender-5.2-Lauf am 06.11.2026).</summary>
public class Studio3DEnginesTests
{
    // Bericht eines echten Laufs (Figur mit 2 Teilen, 2 Materialien, 2 Knochen, Animation „Winken“, Formschlüssel „Lächeln“); Pfad neutralisiert
    const string Unity = """{"target": "unity", "file": "C:/Spiele/figur-unity.fbx", "source": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}, "result": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 2, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}}""";
    const string Godot = """{"target": "godot", "file": "C:/Spiele/figur-godot.glb", "source": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}, "result": {"meshes": 2, "vertices": 2008, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}}""";

    [Fact(DisplayName = "3D S2-17: vier Ziele mit Format (Unity/Unreal FBX, Godot/Web GLB); unbekanntes Ziel abgelehnt; Blender ohne Fenster mit Fehler-Exitcode")]
    public void Profiles_and_args()
    {
        Assert.Equal([".fbx", ".fbx", ".glb", ".glb"], Studio3DEngines.Profiles.Select(p => p.Ext));
        Assert.Throws<ArgumentException>(() => Studio3DEngines.Profile("cryengine"));
        var a = Studio3DEngines.Args("s.py", "in.blend", "unity", "o.fbx", "r.json");
        Assert.Equal(["--background", "--factory-startup", "--python-exit-code", "1"], a.Take(4));
        Assert.Equal(["in.blend", "unity", "o.fbx", "r.json"], a.Skip(a.IndexOf("--") + 1));
        Assert.Contains("add_leaf_bones=False", Studio3DEngines.Script); Assert.Contains("export_format=\"GLB\"", Studio3DEngines.Script);
        Assert.Contains("custom_shape", Studio3DEngines.Script); // Knochen-Formen des glTF-Imports zählen nicht als Teil
        Assert.Throws<IOException>(() => Studio3DEngines.Run("blender", Path.Combine(Path.GetTempPath(), "gibt-es-nicht.blend"), "web", Path.GetTempPath()));
    }

    [Fact(DisplayName = "3D S2-17: echter Rundlauf FBX (Unity) und GLB (Godot) ohne Verlust – mehr Animationen und andere Punktzahl (Nähte) sind kein Fehler")]
    public void Real_roundtrips_ok()
    {
        var u = Studio3DEngines.Parse(Unity);
        Assert.True(u.Ok); Assert.Equal("unity", u.Target); Assert.Equal(972, u.Result.Triangles);
        var g = Studio3DEngines.Parse(Godot);
        Assert.True(g.Ok); Assert.Empty(g.Findings);
        Assert.StartsWith("✓ Godot: figur-godot.glb", Studio3DEngines.Text(g));
        Assert.Contains("Keine Abweichung", Studio3DEngines.Text(g));
    }

    static SceneStats S(int meshes = 2, long tris = 972, int mats = 2, int uv = 2, int bones = 2, int anims = 1, int keys = 1, double x = 1, double y = 0.6, double z = 2.7) => new(meshes, 0, tris, mats, uv, bones, anims, keys, x, y, z);

    [Fact(DisplayName = "3D S2-17: Verluste werden Fehler – Materialien, UV, Knochen, Animationen, Formschlüssel, Teile, Größe (z. B. Zentimeter statt Meter); leerer Export")]
    public void Losses_are_errors()
    {
        var s = S();
        Assert.Contains(Studio3DEngines.Compare(s, S(mats: 1)), f => f.Error && f.Text.StartsWith("Materialien verloren"));
        Assert.Contains(Studio3DEngines.Compare(s, S(uv: 1)), f => f.Error && f.Text.Contains("UV"));
        Assert.Contains(Studio3DEngines.Compare(s, S(bones: 0)), f => f.Error && f.Text.StartsWith("Knochen verloren"));
        Assert.Contains(Studio3DEngines.Compare(s, S(anims: 0)), f => f.Error && f.Text.StartsWith("Animationen verloren"));
        Assert.Contains(Studio3DEngines.Compare(s, S(keys: 0)), f => f.Error && f.Text.StartsWith("Formschlüssel"));
        Assert.Contains(Studio3DEngines.Compare(s, S(meshes: 1)), f => f.Error && f.Text.StartsWith("Teile verloren"));
        Assert.Contains(Studio3DEngines.Compare(s, S(x: 100, y: 60, z: 270)), f => f.Error && f.Text.Contains("Faktor 100"));
        Assert.Contains(Studio3DEngines.Compare(s, S(tris: 400)), f => f.Error && f.Text.StartsWith("Dreiecke stark"));
        Assert.Single(Studio3DEngines.Compare(s, S(meshes: 0, tris: 0)));
        Assert.Contains("✗", Studio3DEngines.Text(new("web", "a.glb", s, S(mats: 0), Studio3DEngines.Compare(s, S(mats: 0)))));
    }

    [Fact(DisplayName = "3D S2-17: gewollte Änderungen sind Hinweise – Achsen Y↔Z getauscht, Wurzelknochen, leicht veränderte Dreiecke")]
    public void Changes_are_hints()
    {
        var swap = Studio3DEngines.Compare(S(), S(y: 2.7, z: 0.6));
        Assert.All(swap, f => Assert.False(f.Error)); Assert.Contains(swap, f => f.Text.StartsWith("Achsen getauscht"));
        Assert.Contains(Studio3DEngines.Compare(S(), S(bones: 3)), f => !f.Error && f.Text.Contains("Wurzelknochen"));
        Assert.Contains(Studio3DEngines.Compare(S(), S(tris: 980)), f => !f.Error && f.Text.StartsWith("Dreiecke leicht"));
    }
}
