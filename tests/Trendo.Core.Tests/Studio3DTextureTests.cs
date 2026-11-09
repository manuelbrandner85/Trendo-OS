using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>CORE-05: echte Oberflächen im Render des 3D-Studios.</summary>
public class Studio3DTextureTests
{
    [Fact(DisplayName = "3D-Material: echte Oberfläche landet im Blender-Auftrag, ohne geladene Oberfläche bleiben Farbe und Rauheit")]
    public void Spec_contains_textures()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-tex-" + Guid.NewGuid().ToString("N"));
        try
        {
            var s = new Scene3D(); s.Parts.Add(new Part3D { Name = "Tisch", Material = "holz" }); s.Parts.Add(new Part3D { Name = "Fuß", Material = "chrom" });
            var with = JsonDocument.Parse(File.ReadAllText(s.WriteBlenderSpec(dir, new Dictionary<string, string> { ["Wood092"] = "C:/t" }))).RootElement.GetProperty("parts");
            Assert.EndsWith("Wood092_1K-JPG_Color.jpg", with[0].GetProperty("textures").GetProperty("color").GetString());
            Assert.Equal(0.5, with[0].GetProperty("tile_m").GetDouble(), 3);
            Assert.Equal(JsonValueKind.Null, with[1].GetProperty("textures").ValueKind);
            var none = JsonDocument.Parse(File.ReadAllText(s.WriteBlenderSpec(dir))).RootElement.GetProperty("parts");
            Assert.Equal(JsonValueKind.Null, none[0].GetProperty("textures").ValueKind);
            Assert.True(none[0].GetProperty("roughness").GetDouble() > 0);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "3D-Material: jede genannte Oberfläche hat eine sinnvolle Kachelgröße; Blender-Skript rechnet Farben von sRGB in linear um")]
    public void Library()
    {
        foreach (var m in Studio3DPro.Materials.Where(x => x.TextureId is not null)) Assert.InRange(m.TileM, 0.05, 2);
        Assert.Equal(Studio3DPro.Materials.Length, Studio3DPro.Materials.Select(x => x.Id).Distinct().Count());
        Assert.Contains("** 2.4", Studio3D.SceneScript); Assert.Contains("def textured(", Studio3D.SceneScript);
    }

    [Fact(DisplayName = "3D-Material: Skript enthält keine Backslash-Falle und bleibt gültig für die Texturknoten")]
    public void Script_nodes() => Assert.Contains("ShaderNodeNormalMap", Studio3D.SceneScript);
}
