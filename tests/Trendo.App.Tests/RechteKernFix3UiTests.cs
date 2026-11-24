using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 3. Prüfrunde an der Oberfläche: eigene Verknüpfungen (P3-2) und gespeicherte Programmpfade aus tools.json (P3-4).
/// Startet nichts echt (FakeLauncher, Ersatz für Blender).</summary>
public class RechteKernFix3Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var l = new FakeLauncher();
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert);
        w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, l, root);
    }

    [AvaloniaFact(DisplayName = "T-0764 P3-2 UI: Kinderprofil – Verknüpfung mit eigenen Parametern oder fremdem Programm wird nicht angelegt; Erwachsene wie bisher")]
    public void Verknuepfung()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, _, root) = Open(child);
            try
            {
                var py = Path.Combine(root, "werkzeuge", "python", "python.exe"); Directory.CreateDirectory(Path.GetDirectoryName(py)!); File.WriteAllText(py, "MZ");
                ProgramTrust.WriteMarker(Path.GetDirectoryName(py)!, "python", "test");
                w.Show("prog-start"); Settle();
                w.Find<TextBox>("sc-name").Text = "pip"; w.Find<TextBox>("sc-exe").Text = py; w.Find<TextBox>("sc-args").Text = "-m pip install etwas";
                w.Find<Button>("sc-add").Press(); Settle();
                Assert.Equal(child ? 0 : 1, Trendo.Core.Sys.Software.Shortcuts(w.Core.DataRoot).Count);
                if (child) Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 P3-4 UI: Blender-Pfad aus tools.json – im Kinderprofil nur von festem Ort; ein umgebogener Pfad wird nicht gestartet")]
    public async Task Werkzeugpfad_tools_json()
    {
        var prev = MainWindow.KreaturRunOverride;
        try
        {
            foreach (var child in new[] { true, false })
            {
                var (w, _, root) = Open(child);
                try
                {
                    var foreign = Path.Combine(root, "fremd", "blender.exe"); Directory.CreateDirectory(Path.GetDirectoryName(foreign)!); File.WriteAllText(foreign, "MZ");
                    File.WriteAllText(Path.Combine(root, "tools.json"), JsonSerializer.Serialize(new Dictionary<string, string> { ["blender"] = foreign }));
                    string? used = null;
                    MainWindow.KreaturRunOverride = (exe, _, _) => { used = exe; return Task.FromResult(""); };
                    await w.KreaturBuild(sichtbar: false);
                    if (child) Assert.NotEqual(foreign, used); else Assert.Equal(foreign, used);
                }
                finally { w.Close(); }
            }
        }
        finally { MainWindow.KreaturRunOverride = prev; }
    }
}
