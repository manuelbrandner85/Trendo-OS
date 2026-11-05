using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>3D-Modelle erstellen wie Profis: Arbeitsschritte, PBR-Materialien, Licht und Kamera in der Blender-Szene, Gruppen, Render-Verlauf.</summary>
public class Studio3DProTests
{
    [Fact(DisplayName = "CORE-05: Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render – Stand aus der Szene, ausgeblendete Teile zählen nicht")]
    public void Steps_progress()
    {
        var s = new Scene3D();
        Assert.Equal(["modell", "uv", "material", "licht", "kamera", "render"], Studio3DPro.Steps.Select(x => x.Id));
        Assert.All(Studio3DPro.Progress(s, 0), x => Assert.False(x.Done));
        s.Parts.Add(new Part3D { Name = "Platte" }); s.Parts.Add(new Part3D { Name = "Versteckt", Hidden = true });
        var p = Studio3DPro.Progress(s, 0);
        Assert.True(p[0].Done && p[1].Done); Assert.False(p[2].Done); Assert.Equal("1 Teil(e) ohne Material", p[2].Why);
        s.Parts[0].Material = "holz"; s.Light = "studio"; s.CameraLens = 85;
        Assert.True(Studio3DPro.Progress(s, 1).All(x => x.Done));
    }

    [Fact(DisplayName = "CORE-05: PBR-Materialien (Metall binär, Rauheit 0–1, Glas mit Durchlässigkeit) und Gruppen; Blender-Szene trägt Material, Licht, Kamera – ausgeblendete Teile fehlen; JSON übersteht Speichern")]
    public void Materials_groups_spec()
    {
        Assert.All(Studio3DPro.Materials, m => { Assert.True(m.Metallic is 0 or 1); Assert.InRange(m.Roughness, 0, 1); });
        Assert.Equal(1, Studio3DPro.MaterialOf("glas").Transmission);
        Assert.Equal("kunststoff-matt", Studio3DPro.MaterialOf("unbekannt").Id);
        var s = new Scene3D { Light = "abend", CameraLens = 35, CameraHeight = 120 };
        s.Parts.Add(new Part3D { Name = "Fuß", Material = "chrom", Group = "Lampe" });
        s.Parts.Add(new Part3D { Name = "Schirm", Material = "glas", Group = "Lampe" });
        s.Parts.Add(new Part3D { Name = "Hilfsklotz", Hidden = true });
        Assert.Equal(["Lampe"], Studio3DPro.Groups(s)); Assert.Equal(2, Studio3DPro.InGroup(s, "Lampe").Count); Assert.Single(Studio3DPro.InGroup(s, ""));
        var back = Scene3D.FromJson(s.ToJson());
        Assert.Equal(("abend", 35d, "Lampe", true), (back.Light, back.CameraLens, back.Parts[0].Group, back.Parts[2].Hidden));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-3dpro-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            using var j = JsonDocument.Parse(File.ReadAllText(s.WriteBlenderSpec(dir)));
            var parts = j.RootElement.GetProperty("parts");
            Assert.Equal(2, parts.GetArrayLength()); // ausgeblendet = nicht im Render
            Assert.Equal(1, parts[0].GetProperty("metallic").GetDouble()); Assert.Equal(1, parts[1].GetProperty("transmission").GetDouble());
            Assert.Equal(("abend", 35, 120), (j.RootElement.GetProperty("light").GetString(), j.RootElement.GetProperty("cameraLens").GetDouble(), j.RootElement.GetProperty("cameraHeight").GetDouble()));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "CORE-05: Render-Verlauf – neue Bilder bekommen eindeutige Namen, neuestes zuerst; Blender-Skript (Bauen + Cycles-Render) ist gültiges Python")]
    public void Render_history_and_script()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-3dren-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var t = new DateTime(2026, 11, 5, 10, 0, 0);
            var a = Studio3DPro.NextRenderPath(dir, t); File.WriteAllBytes(a, [1]);
            var b = Studio3DPro.NextRenderPath(dir, t); File.WriteAllBytes(b, [2]);
            var c = Studio3DPro.NextRenderPath(dir, t.AddMinutes(1)); File.WriteAllBytes(c, [3]);
            Assert.NotEqual(a, b);
            Assert.Equal(c, Studio3DPro.Renders(dir)[0]); Assert.Equal(3, Studio3DPro.Renders(dir).Count);
            var py = Path.Combine(dir, "szene.py"); File.WriteAllText(py, Studio3D.SceneScript + Studio3DPro.RenderTail);
            Assert.Contains("smart_project", Studio3D.SceneScript); Assert.Contains("Principled BSDF", Studio3D.SceneScript); Assert.Contains("CYCLES", Studio3DPro.RenderTail);
            if (Cli.Find("python") is { } python)
            {
                var r = Cli.Run(python, ["-I", "-c", "import ast,sys; ast.parse(open(sys.argv[1], encoding='utf-8').read())", py], dir, 30000);
                Assert.True(r.Ok, r.Err);
            }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
