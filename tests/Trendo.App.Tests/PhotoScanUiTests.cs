using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class PhotoScanUiTests
{
    static string Texts(MainWindow w) => string.Join("\n", w.GetLogicalTexts());

    static string Photos(string root, int n)
    {
        var d = Path.Combine(root, "fotos");
        Directory.CreateDirectory(d);
        for (var i = 0; i < n; i++) File.WriteAllText(Path.Combine(d, "bild-" + i + ".jpg"), "x");
        return d;
    }

    [AvaloniaFact(DisplayName = "Scan: drei Fotos werden ein Modell und öffnen das 3D-Studio")]
    public async Task Three_photos_open_the_3d_studio()
    {
        var (w, _, root) = Ui.Open();
        var folder = Photos(root, 3);
        w.ScanRun = (photos, work) =>
        {
            Assert.Equal(3, photos.Count);
            var obj = Path.Combine(work, "modell.obj");
            File.WriteAllText(obj, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
            return (0, "", obj);
        };
        w.Show("scan3d");
        w.Find<TextBox>("scan-photos-folder").Text = folder;
        w.Find<Button>("scan-go").Press();
        for (var i = 0; i < 50 && w.CurrentView != "3d"; i++)
        {
            await Task.Delay(20);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        Assert.Equal("3d", w.CurrentView);
        Assert.Contains("Dreiecke", Texts(w));
        Assert.Contains("scan.model", w.Core.Audit.ReadAll().Select(e => e.Action));
    }

    [AvaloniaFact(DisplayName = "Scan: zwei Fotos rechnen kein Modell")]
    public void Two_photos_do_not_invent_a_model()
    {
        var (w, _, root) = Ui.Open();
        var ran = false;
        w.ScanRun = (_, _) => { ran = true; return (0, "", null); };
        w.Show("scan3d");
        w.Find<TextBox>("scan-photos-folder").Text = Photos(root, 2);
        w.Find<Button>("scan-go").Press();
        Assert.False(ran);
        Assert.Equal("scan3d", w.CurrentView);
        Assert.Contains("mindestens 3", Texts(w));
    }

    [AvaloniaFact(DisplayName = "Scan: ohne Meshroom und COLMAP öffnet sich die Herstellerseite, nichts wird installiert")]
    public void Missing_tools_open_the_official_page()
    {
        var (w, l, root) = Ui.Open();
        w.ScanToolOverride = _ => null;
        w.Show("scan3d");
        w.Find<TextBox>("scan-photos-folder").Text = Photos(root, 3);
        w.Find<Button>("scan-go").Press();
        Assert.Equal("studio-deps", w.CurrentView);
        Assert.Equal("Offiziellen Installer öffnen", w.Find<Button>("btn-guide-meshroom").Content);
        Assert.Equal("Offiziellen Installer öffnen", w.Find<Button>("btn-guide-colmap").Content);
        w.Find<Button>("btn-guide-meshroom").Press();
        Assert.Contains("https://github.com/alicevision/Meshroom/releases", l.Opened);
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Action.StartsWith("deps.install"));
        w.Find<Button>("btn-later-colmap").Press();
        Assert.Equal("scan3d", w.CurrentView);
    }

    [AvaloniaFact(DisplayName = "Scan: ein Video wird zu Fotos, ohne FFmpeg entsteht nichts")]
    public void Video_becomes_photos_or_says_ffmpeg_is_missing()
    {
        var (w, _, root) = Ui.Open();
        var video = Path.Combine(root, "film.mp4");
        File.WriteAllText(video, "x");
        w.Show("scan3d");
        w.Find<TextBox>("scan-video").Text = video;
        w.Find<Button>("scan-frames").Press();
        Assert.Contains("FFmpeg", Texts(w));
        Assert.Contains("keine Bilder", Texts(w));
        w.ScanFrames = (_, args) =>
        {
            Assert.Contains("fps=1/1", args);
            var dir = Path.GetDirectoryName(args[^1])!;
            for (var i = 1; i <= 3; i++) File.WriteAllText(Path.Combine(dir, "bild-" + i.ToString("0000") + ".jpg"), "x");
            return (0, "");
        };
        w.Find<TextBox>("scan-video").Text = video;
        w.Find<Button>("scan-frames").Press();
        Assert.Contains("3 Fotos aus dem Video", Texts(w));
        Assert.True(Directory.Exists(w.Find<TextBox>("scan-photos-folder").Text));
    }
}
