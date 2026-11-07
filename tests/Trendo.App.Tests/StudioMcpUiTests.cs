using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>C5: Programme sichtbar mit dem Projekt starten (Startweg nachgebildet); Unity und Unbekanntes werden nicht gestartet.</summary>
public class StudioMcp_Ui
{
    [AvaloniaFact(DisplayName = "C5: Godot startet im Editor mit dem Projektordner; Unity und fremde Namen werden abgewiesen")]
    public void Launch()
    {
        var (w, _, _) = Ui.Open(); var calls = new List<(string Exe, IReadOnlyList<string> Args)>();
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-c5-" + Guid.NewGuid().ToString("N"))).FullName;
        MainWindow.StudioLaunchOverride = (e, a) => calls.Add((e, a));
        try
        {
            File.WriteAllText(Path.Combine(d, "project.godot"), "");
            Assert.Null(w.StudioLaunchProgram("godot", d));
            Assert.Equal(["--editor", "--path", d], Assert.Single(calls).Args);
            Assert.Contains("nicht automatisch", w.StudioLaunchProgram("unity", d));
            Assert.Contains("nicht automatisch", w.StudioLaunchProgram("cmd", d));
            Assert.Single(calls);
        }
        finally { MainWindow.StudioLaunchOverride = null; Directory.Delete(d, true); }
    }
}
