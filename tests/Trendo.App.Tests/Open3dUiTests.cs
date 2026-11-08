using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 2 – Scan-Studio: „Glätten und vereinfachen (Open3D)“ (Open3D nachgebildet; echter Lauf in Open3dTests/open3d-e2e.md).</summary>
public class Open3dUi
{
    static string Cube(string dir)
    {
        var f = Path.Combine(dir, "modell.obj");
        File.WriteAllText(f, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nv 0 0 1\nv 1 0 1\nv 1 1 1\nv 0 1 1\nf 1 2 3\nf 1 3 4\nf 5 6 7\nf 5 7 8\nf 1 2 6\nf 1 6 5\nf 2 3 7\nf 2 7 6\nf 3 4 8\nf 3 8 7\nf 4 1 5\nf 4 5 8\n");
        return f;
    }

    [AvaloniaFact(DisplayName = "Open3D-Oberfläche: ohne Einrichtung Knopf; Glätten ersetzt das Modell mit Rückgängig; Fehler von Open3D wird gemeldet, Modell bleibt")]
    public async Task Smooth_and_undo()
    {
        var (w, _, root) = Ui.Open(); var dir = Path.Combine(root, "scan"); Directory.CreateDirectory(dir);
        try
        {
            var model = Cube(dir); w.ScanSetModel(model);
            Assert.NotNull(w.Find<Button>("scan-o3d-setup"));
            (string Mode, int N)? seen = null;
            MainWindow.Open3dRunOverride = (mode, src, outFile, n) => { seen = (mode, n); File.WriteAllText(outFile, File.ReadAllText(src).Replace("v 1 1 1", "v 0.9 0.9 0.9")); return new Cli.Result(0, "{}", ""); };
            w.Show("scan3d"); Assert.NotNull(w.Find<Button>("scan-o3d-smooth"));
            Assert.False(w.Find<Button>("scan-o3d-simplify").IsEnabled); // 12 Dreiecke – schon schlank
            await w.ScanOpen3d("glaetten", 10);
            Assert.Equal(("glaetten", 10), seen); Assert.Contains("0.9 0.9 0.9", File.ReadAllText(model).Replace(",", "."));
            Assert.True(File.Exists(Path.Combine(dir, "modell-vorher.obj"))); Assert.False(File.Exists(Path.Combine(dir, "open3d-ergebnis.obj")));
            w.ScanUndo(); Assert.DoesNotContain("0.9 0.9 0.9", File.ReadAllText(model));
            MainWindow.Open3dRunOverride = (_, _, _, _) => new Cli.Result(3, "", "Das Netz hat keine Dreiecke.");
            await w.ScanOpen3d("glaetten", 10); Assert.DoesNotContain("0.9 0.9 0.9", File.ReadAllText(model));
            await w.ScanOpen3d("glaetten", 99); // außerhalb des Bereichs: gar kein Lauf
        }
        finally { MainWindow.Open3dRunOverride = null; w.Close(); }
    }
}
