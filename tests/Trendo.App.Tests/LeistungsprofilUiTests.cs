using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0107: Sparsames Leistungsprofil schaltet Unschärfe, Schatten und Animationen ab.</summary>
public class Leistungsprofil_Ui
{
    [AvaloniaFact(DisplayName = "T-0107: Sparsam – keine Hintergrund-Unschärfe, keine Schatten und Effekte, keine Übergänge; Auswahl in den Einstellungen")]
    public void Sparsam()
    {
        var (w, _, root) = Ui.Open();
        Assert.False(w.LowPower); // Test-PC ist x64, Profil „auto“
        Assert.NotNull(w.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "backdrop-blur"));
        w.Settings.ReduceMotion = false; w.Show("settings");
        w.Find<Button>("settings-nav-bedienung").Press();
        Assert.True(w.Find<Button>("power-sparsam").IsVisible);
        w.Close();

        var s = UserSettings.Load(root); s.PowerProfile = "sparsam"; s.SetupDone = true; s.Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w2.Show(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(w2.LowPower);
        Assert.Null(w2.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "backdrop-blur"));
        w2.Show("start");
        var desk = w2.ActivePageRoot!;
        Assert.DoesNotContain(desk.GetLogicalDescendants().OfType<Control>(), c => c.Effect is not null || c.Transitions is { Count: > 0 });
        w2.Show("studios");
        var frame = w2.GetVisualDescendants().OfType<Border>().First(b => b.Name == "win-studios");
        Assert.Equal(0, frame.BoxShadow.Count);
        w2.Close();
    }
}
