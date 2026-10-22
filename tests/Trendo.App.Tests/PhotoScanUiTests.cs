using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class PhotoScanUiTests
{
    [AvaloniaFact(DisplayName = "SPEC-09 UI: ohne Fotos keine Installation, mit Fotos fragt Trendo vor COLMAP")]
    public void Fragt_vor_dem_Werkzeug()
    {
        var (w, _, root) = Ui.Open(offline: true);
        Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["SPEC-09"]);
        Assert.NotNull(w.TryFind<Button>("scan-photos-folder-pickdir"));
        Assert.NotNull(w.TryFind<Button>("scan-video-file-pick"));
        w.Find<Button>("scan-run").Press();
        Assert.Contains("mindestens 3", w.Find<TextBlock>("scan-status").Text);
        Assert.Equal("scan3d", w.CurrentView);

        w.Find<Button>("scan-open").Press();
        Assert.Contains("Noch kein Modell", w.Find<TextBlock>("scan-status").Text);

        w.Find<Button>("scan-stop").Press();
        Assert.Contains("keine Rechnung", w.Find<TextBlock>("scan-status").Text);

        var fotos = Path.Combine(root, "fotos"); Directory.CreateDirectory(fotos);
        foreach (var n in new[] { "a.jpg", "b.jpg", "c.jpg" }) File.WriteAllText(Path.Combine(fotos, n), "x");
        w.PickOverride = req => Task.FromResult<string[]?>(req.Folder ? [fotos] : null);
        w.Find<Button>("scan-photos-folder-pickdir").Press();
        Assert.Equal(fotos, w.Find<TextBox>("scan-photos-folder").Text);

        w.Find<Button>("scan-run").Press();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("studio-deps", w.CurrentView);
        Assert.Contains("Jetzt installieren", w.Find<TextBlock>("ask-colmap").Text);
        Assert.False(w.Find<Button>("btn-yes-colmap").IsEnabled);
        Assert.NotNull(w.TryFind<Button>("btn-later-colmap"));

        w.Find<Button>("btn-later-colmap").Press();
        Assert.Equal("scan3d", w.CurrentView);
        w.Find<Button>("scan-use-meshroom").Press();
        Assert.Contains("Meshroom", w.Find<TextBlock>("scan-engine").Text);
        w.Find<Button>("scan-check").Press();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("studio-deps", w.CurrentView);
        Assert.Contains("Jetzt installieren", w.Find<TextBlock>("ask-meshroom").Text);
        Assert.NotNull(w.TryFind<Button>("btn-guide-meshroom"));
    }

    [AvaloniaFact(DisplayName = "SPEC-09 UI: Rechnung zeigt den Fortschritt und öffnet das Modell im 3D-Studio")]
    public void Rechnet_und_oeffnet()
    {
        var (w, _, root) = Ui.Open();
        Directory.CreateDirectory(root);
        var fotos = Path.Combine(root, "fotos"); Directory.CreateDirectory(fotos);
        foreach (var n in new[] { "a.jpg", "b.jpg", "c.jpg" }) File.WriteAllText(Path.Combine(fotos, n), "x");
        var tool = Path.Combine(root, "colmap-test"); File.WriteAllText(tool, "x");
        w.ScanToolOverride = tool;
        w.ScanRunner = new ScanUiFake((file, args) =>
        {
            if (args.Contains("mapper"))
            {
                var dir = NewestScan(root);
                var sparse = Path.Combine(dir, "sparse", "0");
                Directory.CreateDirectory(sparse);
                File.WriteAllText(Path.Combine(sparse, "cameras.bin"), "cams");
            }
            if (args.Contains("delaunay_mesher"))
            {
                var dir = NewestScan(root);
                Directory.CreateDirectory(Path.Combine(dir, "dicht"));
                File.WriteAllText(Path.Combine(dir, "dicht", "meshed-delaunay.ply"), Tetra());
            }
            return (0, "ok");
        });
        w.Show("scan3d");
        w.Find<TextBox>("scan-photos-folder").Text = fotos;
        w.Find<Button>("scan-check").Press();
        Assert.Contains("COLMAP ist da", w.Find<TextBlock>("scan-status").Text);
        w.Find<Button>("scan-run").Press();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("Dreiecke", w.Find<TextBlock>("scan-status").Text);
        Assert.Contains("3D-Studio", w.Find<TextBlock>("scan-status").Text);
        Assert.NotEmpty(Directory.GetFiles(root, "modell.obj", SearchOption.AllDirectories));
        w.Find<Button>("scan-open").Press();
        Assert.Equal("3d", w.CurrentView);
        Assert.Contains("Eingelesen", PageText(w));
        Assert.Contains("4 Dreiecke", PageText(w));
    }

    [AvaloniaFact(DisplayName = "SPEC-09 UI: Video ohne FFmpeg fragt, bevor etwas installiert wird")]
    public void Video_fragt_ffmpeg()
    {
        var (w, _, root) = Ui.Open(offline: true);
        Directory.CreateDirectory(root);
        var video = Path.Combine(root, "rundgang.mp4"); File.WriteAllText(video, "x");
        var tool = Path.Combine(root, "colmap-test"); File.WriteAllText(tool, "x");
        w.ScanToolOverride = tool;
        w.Show("scan3d");
        w.Find<TextBox>("scan-video-file").Text = video;
        w.Find<Button>("scan-run").Press();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("studio-deps", w.CurrentView);
        Assert.Contains("FFmpeg", w.Find<TextBlock>("ask-ffmpeg").Text);
        Assert.False(w.Find<Button>("btn-yes-ffmpeg").IsEnabled);
    }

    static string NewestScan(string root)
    {
        var scan = Path.Combine(root, "Projekte", "Scan");
        return Directory.GetDirectories(scan).OrderByDescending(d => d).First();
    }

    static string PageText(MainWindow w) => string.Join("\n", w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    static string Tetra() => """
        ply
        format ascii 1.0
        element vertex 4
        property float x
        property float y
        property float z
        element face 4
        property list uchar int vertex_indices
        end_header
        0 0 0
        1 0 0
        0 1 0
        0 0 1
        3 0 1 2
        3 0 2 3
        3 0 3 1
        3 1 3 2
        """;

    sealed class ScanUiFake(Func<string, string, (int, string)> run) : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            line?.Invoke("ok");
            return Task.FromResult(run(file, args));
        }
    }
}
