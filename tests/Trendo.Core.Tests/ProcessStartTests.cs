using System.Diagnostics;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class Programmstart
{
    [Fact(DisplayName = "Blender: Startinfo ohne Konsolenfenster, die Programmoberfläche bleibt normal")]
    public void Blender_ohne_Konsole()
    {
        var psi = ShellProcessLauncher.ForProgram("blender", ["modell.blend"]);
        Assert.True(psi.CreateNoWindow);
        Assert.False(psi.UseShellExecute);
        Assert.Equal(ProcessWindowStyle.Normal, psi.WindowStyle);
        Assert.Equal("modell.blend", Assert.Single(psi.ArgumentList));
        Assert.False(psi.RedirectStandardOutput);
        Assert.False(psi.RedirectStandardError);
    }

    [Fact(DisplayName = "Blender: Windows nimmt blender-launcher.exe, sonst blender.exe ohne Konsole")]
    public void Blender_launcher_wenn_vorhanden()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-blender-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var exe = Path.Combine(dir, "blender.exe");
            var launcher = Path.Combine(dir, BlenderGui.LauncherName);
            File.WriteAllBytes(exe, []);
            File.WriteAllBytes(launcher, []);

            var withLauncher = ShellProcessLauncher.ForGui(exe, ["modell.blend"], windows: true);
            Assert.Equal(launcher, withLauncher.FileName);
            Assert.True(withLauncher.CreateNoWindow);
            Assert.False(withLauncher.UseShellExecute);
            Assert.Equal(ProcessWindowStyle.Normal, withLauncher.WindowStyle);
            Assert.Equal("modell.blend", Assert.Single(withLauncher.ArgumentList));

            File.Delete(launcher);
            var fallback = ShellProcessLauncher.ForGui(exe, ["modell.blend"], windows: true);
            Assert.Equal(exe, fallback.FileName);
            Assert.True(fallback.CreateNoWindow);
            Assert.False(fallback.UseShellExecute);

            File.WriteAllBytes(launcher, []);
            var linux = ShellProcessLauncher.ForGui(exe, ["modell.blend"], windows: false);
            Assert.Equal(exe, linux.FileName);
            Assert.True(linux.CreateNoWindow);

            var other = Path.Combine(dir, "notepad.exe");
            File.WriteAllBytes(other, []);
            Assert.Equal(other, BlenderGui.ForWindow(other, windows: true));
            Assert.False(BlenderGui.IsBlenderExe(other));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
