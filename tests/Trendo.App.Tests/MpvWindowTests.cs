using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Fehler 07.11.2026: Beim Abspielen in Trendo Musik öffnete sich ein eigenes mpv-Fenster – Trendo soll selbst abspielen.</summary>
public class MpvWindowTests
{
    [Fact(DisplayName = "Abspielen ohne fremdes Fenster: mpv startet mit force-window=no und vo=null; echter mpv spielt eine WAV, ohne ein Fenster zu öffnen")]
    public void No_player_window()
    {
        var args = MpvAudio.StartArgs("test");
        Assert.Contains("--force-window=no", args); Assert.Contains("--vo=null", args); Assert.Contains("--no-video", args);
        var mpv = new[] { Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\TrendoWerkzeuge\mpv\mpv.exe"), Cli.Find("mpv") ?? "" }.FirstOrDefault(File.Exists);
        var ff = Cli.Find("ffmpeg");
        if (mpv is null || ff is null || !OperatingSystem.IsWindows()) return;
        var wav = Path.Combine(Path.GetTempPath(), "trendo-mpvwin-" + Guid.NewGuid().ToString("N")[..6] + ".wav");
        try
        {
            Assert.True(Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "sine=f=440:d=3", wav], null, 30000).Ok);
            using var a = new MpvAudio(mpv);
            a.SetVolume(0); a.Load(wav);
            Thread.Sleep(1500);
            var p = a.ProcessForTests!; p.Refresh();
            Assert.False(p.HasExited);
            Assert.Equal(IntPtr.Zero, p.MainWindowHandle); // kein eigenes Player-Fenster
        }
        finally { try { File.Delete(wav); } catch (IOException) { } }
    }
}
