using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 10. Prüfrunde an der Oberfläche: Kinder entpacken nicht in Trendos eigene Ordner (M c), Kinder fügen keine eigenen Fernseh-Quellen
/// hinzu (M d). Ohne statische Ersatz-Hooks; startet nichts echt.</summary>
public class RechteKernFix10Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, root);
    }

    [AvaloniaFact(DisplayName = "T-0764 M c: Kind – ein ZIP im Werkzeugordner wird nicht dorthin entpackt (Trendos eigener Ordner); Erwachsene wie bisher")]
    public void Entpacken_in_Trendo()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, root) = Open(child);
            try
            {
                var zip = Path.Combine(root, "werkzeuge", "mpv", "paket.zip"); Directory.CreateDirectory(Path.GetDirectoryName(zip)!);
                using (var a = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var s = new StreamWriter(a.CreateEntry("bild.png").Open())) s.Write("x");
                w.Show("alltag-dateien"); Settle();
                w.GetLogicalDescendants().OfType<TextBox>().First(t => t.Name == "zip-file").Text = zip;
                w.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "zip-unpack").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
                Assert.Equal(!child, File.Exists(Path.Combine(root, "werkzeuge", "mpv", "paket", "bild.png")));
                if (child) Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("schreiben"));
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 M d: Kind – eigene Fernseh-Quelle (M3U) hinzufügen geht nicht; Erwachsene wie bisher")]
    public async Task Tv_Quelle_Kind()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, root) = Open(child);
            try
            {
                var m3u = Path.Combine(root, "liste.m3u"); File.WriteAllText(m3u, "#EXTM3U\n#EXTINF:-1,Testsender\nhttps://example.org/a.m3u8\n");
                var n = await w.TvAddSource("Test", "datei", m3u);
                if (child) { Assert.Equal(0, n); Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("tv.quelle")); }
                else Assert.True(n > 0);
            }
            finally { w.Close(); }
        }
    }
}
