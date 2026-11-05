using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Fehler 05.11.2026: Im Windows-Stil lag ein verschwommenes Viereck über dem Hintergrundbild – die Glas-Unschärfe des Trendo-Stils
/// wurde auch in den System-Stilen gezeichnet. Sie gehört nur zum Stil „Trendo“.</summary>
public class DesktopBlur_Ui
{
    static MainWindow Open(string style, out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var s = UserSettings.Load(root); s.Style = style; s.Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser")); w.Show(); Dispatcher.UIThread.RunJobs();
        return w;
    }

    static bool HasBlur(MainWindow w) => w.GetVisualDescendants().OfType<Border>().Any(b => b.Name == "backdrop-blur");

    [AvaloniaTheory(DisplayName = "CORE-01: Desktop im Windows-, Mac- und GNOME-Stil ohne verschwommene Glasfläche über dem Hintergrund")]
    [InlineData("windows")]
    [InlineData("mac")]
    [InlineData("linux")]
    public void No_blur_in_system_styles(string style)
    {
        var w = Open(style, out _);
        try { Assert.False(HasBlur(w), $"Stil {style}: Glas-Unschärfe darf nicht über dem Hintergrund liegen"); }
        finally { w.Close(); Open("trendo", out _).Close(); } // Stil zurück auf Trendo für die übrigen Tests
    }

    [AvaloniaFact(DisplayName = "CORE-01: Stil Trendo behält sein Milchglas hinter den Flächen")]
    public void Trendo_keeps_glass()
    {
        var w = Open("trendo", out _);
        try { Assert.True(HasBlur(w)); }
        finally { w.Close(); }
    }
}
