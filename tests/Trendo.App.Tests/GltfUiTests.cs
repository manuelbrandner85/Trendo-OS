using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 5 – VR-Studio: „Prüfen &amp; für Web packen“ (Khronos-Validator und glTF-Transform nachgebildet; echte Läufe in GltfToolsTests).</summary>
public class GltfUi
{
    const string Bericht = "{\"validatorVersion\":\"2.0.0-dev.3.10\",\"issues\":{\"numErrors\":0,\"numWarnings\":0,\"numInfos\":0,\"numHints\":0,\"messages\":[]},\"info\":{\"materialCount\":1,\"totalTriangleCount\":12,\"extensionsUsed\":[\"EXT_meshopt_compression\"]}}";

    [AvaloniaFact(DisplayName = "glTF-Oberfläche: ohne Programme Einrichten-Knöpfe; mit ihnen offiziell prüfen und für Web packen – neue …-web.glb, Original bleibt, Hinweis auf Blender/Unreal")]
    public void Validate_and_pack()
    {
        var (w, _, _) = Ui.Open(); var calls = new List<IReadOnlyList<string>>();
        try
        {
            MainWindow.GltfValidatorOverride = ""; MainWindow.GltfTransformOverride = null;
            w.Show("xr"); w.Find<Button>("xr-sample").Press();
            Assert.EndsWith(".glb", w.XrFile); var original = w.XrFile;
            Assert.NotNull(w.Find<Button>("xr-gltf-validator-setup"));
            var tool = Path.Combine(Path.GetTempPath(), "trendo-gltf-fake-" + Guid.NewGuid().ToString("N") + ".exe"); File.WriteAllText(tool, "x");
            MainWindow.GltfValidatorOverride = tool; MainWindow.GltfTransformOverride = (tool, tool);
            MainWindow.GltfRunOverride = (_, a, _) => { calls.Add(a); if (a.Count > 1 && a[1] == "optimize") File.Copy(a[2], a[3], true); return new Cli.Result(0, a[0] == "-o" ? Bericht : "", ""); };
            w.Show("xr");
            w.Find<Button>("xr-gltf-validate").Press();
            Assert.StartsWith("✓ Khronos-Prüfung bestanden", w.GltfMessage); Assert.True(w.GltfLastReport!.Ok);
            w.Find<Button>("xr-gltf-pack").Press();
            Assert.EndsWith("-web.glb", w.XrFile); Assert.True(File.Exists(original)); Assert.Contains("Blender und Unreal das Original", w.GltfMessage);
            Assert.Contains(calls, a => a.Count > 1 && a[1] == "optimize" && a.Contains("meshopt"));
            Assert.Equal("-o", calls[^1][0]); // nach dem Packen gleich wieder geprüft
            File.Delete(tool);
        }
        finally { MainWindow.GltfValidatorOverride = null; MainWindow.GltfTransformOverride = null; MainWindow.GltfRunOverride = null; w.Close(); }
    }
}
