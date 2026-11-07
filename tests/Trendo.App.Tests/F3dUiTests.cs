using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>3D-Studio: Karte „Vorschaubilder (f3d)“ – ohne f3d der Einrichten-Knopf, mit f3d je 3D-Datei ein Knopf, Bild erscheint.</summary>
public class F3d_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "f3d im 3D-Studio: Einrichten ohne Werkzeug; mit Werkzeug Knopf je Modell, Vorschaubild erscheint im Studio")]
    public void Preview_card()
    {
        var (w, _, _) = Ui.Open();
        var exe = Path.Combine(Path.GetTempPath(), "trendo-f3d-ui-" + Guid.NewGuid().ToString("N") + ".exe"); File.WriteAllBytes(exe, [0]);
        try
        {
            MainWindow.F3dExeOverride = ""; w.Show("3d"); Pump(); // nicht eingerichtet
            Assert.NotNull(w.Find<Button>("3d-f3d-setup"));
            MainWindow.F3dExeOverride = exe;
            MainWindow.F3dRunOverride = (e, a, t) => { File.WriteAllBytes(a[0]["--output=".Length..], Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")); return new(0, "", ""); };
            Directory.CreateDirectory(w.Folder3D); var model = Path.Combine(w.Folder3D, "wuerfel.glb"); File.WriteAllBytes(model, [1, 2, 3]);
            w.Show("3d"); Pump();
            w.Find<Button>("3d-f3d-0").Press(); Pump();
            Assert.StartsWith("Vorschaubild: wuerfel.png", w.F3dMessage);
            Assert.NotNull(w.Find<Image>("3d-f3d-image"));
        }
        finally { MainWindow.F3dExeOverride = null; MainWindow.F3dRunOverride = null; File.Delete(exe); }
    }
}
