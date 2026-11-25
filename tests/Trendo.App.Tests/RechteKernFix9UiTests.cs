using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 9. Prüfrunde an der Oberfläche: „Datei wählen …“ an Pfad-Feldern öffnet im Kinderprofil Trendos Auswahl (M); Fernseh-Player mit
/// „--“ vor der Adresse und nur erlaubten Schemata (N2). Ohne statische Ersatz-Hooks; startet nichts echt.</summary>
public class RechteKernFix9Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "T-0764 M: Kind – „Datei wählen …“ an einem Pfad-Feld öffnet Trendos eigene Auswahl, nicht den Windows-Dialog")]
    public async Task Pfadfeld_Kind()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = true, Name = "Kind" }.Save(root);
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.Show(); Settle();
        try
        {
            w.Show("alltag-dateien"); Settle(); await Task.Delay(50); Settle();
            var pick = w.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "zip-file-pick");
            pick.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
            var dlg = w.KidPickWindow;
            Assert.NotNull(dlg); // Trendos Auswahl statt StorageProvider
            dlg!.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "kp-cancel").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "datei.auswahl.trendo");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 N2: Fernseh-Player – Adresse hinter „--“; andere Schemata (memory:, file:, lavfi) abgelehnt")]
    public void Tv_Adresse()
    {
        var a = MpvView.Args("1", "t", false, "https://example.org/a.m3u8");
        Assert.Equal(["--", "https://example.org/a.m3u8"], a[^2..]);
        foreach (var bad in new[] { "memory://x", "file:///C:/x.mp4", "av://lavfi:sine", "--script=x.lua" })
            Assert.ThrowsAny<ArgumentException>(() => MpvView.Args("1", "t", false, bad));
    }
}
