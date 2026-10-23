using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

public class W4_Core08_Ui
{
    [AvaloniaFact(DisplayName = "CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei")]
    public void Extract_audio()
    {
        Assert.SkipUnless(Cli.Find("ffmpeg") is not null, "Übersprungen: FFmpeg ist nicht installiert.");
        var src = W4_Core08_Media_Ui.Sample();
        Assert.NotNull(src);
        var (w, _, _) = Ui.Open();
        w.MediaPick(src);
        w.Find<Button>("media-job-mp3").Press();
        Assert.True(File.Exists(Path.ChangeExtension(src, null) + "-mp3.mp3"));
        Assert.Contains("Fertig", ((TextBlock)w.Find<Border>("w45-msg").Child!).Text);
        w.Close();
    }
}

static class W4_Core08_Media_Ui
{
    public static string? Sample()
    {
        if (Cli.Find("ffmpeg") is null) return null;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var p = Path.Combine(dir, "clip.mp4");
        return Cli.Run("ffmpeg", ["-loglevel", "error", "-f", "lavfi", "-i", "testsrc=duration=2:size=320x240", "-f", "lavfi", "-i", "sine=duration=2", "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-shortest", p], dir).Ok ? p : null;
    }
}
