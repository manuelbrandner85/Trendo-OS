using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 1 – 3D-Studio: „In GLB umwandeln (assimp)“ (eigener Prozess nachgebildet; echte Läufe in AssimpConvertTests).</summary>
public class AssimpUi
{
    [AvaloniaFact(DisplayName = "assimp-Oberfläche: ohne Einrichtung Knopf; FBX im 3D-Ordner → „→ GLB“ startet Trendo mit --assimp-convert, Ergebnis in 3D/umgewandelt")]
    public void Convert_to_glb()
    {
        var dll = Path.Combine(Path.GetTempPath(), "trendo-fake-assimp-" + Guid.NewGuid().ToString("N") + ".dll"); File.WriteAllText(dll, "x");
        var (w, _, _) = Ui.Open();
        try
        {
            MainWindow.AssimpDllOverride = ""; w.Show("3d");
            Assert.NotNull(w.Find<Button>("3d-assimp-setup"));
            var dir = w.Folder3D; Directory.CreateDirectory(dir); var fbx = Path.Combine(dir, "stuhl.fbx"); File.WriteAllText(fbx, "Kaydara FBX");
            IReadOnlyList<string>? seen = null;
            MainWindow.AssimpDllOverride = dll;
            MainWindow.AssimpRunOverride = (_, a, _) => { seen = a; File.WriteAllText(a[3], "glTF"); return new Cli.Result(0, "", ""); };
            w.Show("3d"); w.Find<Button>("3d-assimp-0").Press();
            Assert.Equal(["--assimp-convert", dll, fbx], seen!.Take(3));
            Assert.Equal(Path.Combine(dir, "umgewandelt", "stuhl.glb"), seen![3]);
            Assert.StartsWith("✓ Umgewandelt: stuhl.glb", w.AssimpMessage);
            MainWindow.AssimpRunOverride = (_, _, _) => new Cli.Result(-1073741819, "", "");
            w.Show("3d"); w.Find<Button>("3d-assimp-0").Press(); Assert.Contains("abgestürzt", w.AssimpMessage);
        }
        finally { MainWindow.AssimpDllOverride = null; MainWindow.AssimpRunOverride = null; w.Close(); File.Delete(dll); }
    }
}
