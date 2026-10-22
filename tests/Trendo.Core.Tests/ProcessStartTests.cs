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
}
