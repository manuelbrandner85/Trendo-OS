using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 11. Prüfrunde an der Oberfläche: Dateimanager und Downloads im Kinderprofil – keine ausführbaren oder auslösenden Dateien, nur
/// freigegebene Orte, kein „Dieser PC“. Ohne statische Ersatz-Hooks; startet und lädt nichts echt.</summary>
public class RechteKernFix11Ui
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

    [AvaloniaFact(DisplayName = "T-0764 M2/M4: Kind – Dateimanager benennt nicht in .exe um und kopiert keine setup.exe/.lnk; Bilder weiter; „Dieser PC“ führt in die Freigabe; Erwachsene wie bisher")]
    public void Dateimanager_Kind()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, _) = Open(child);
            try
            {
                var home = w.Files.Home; var src = Path.Combine(home, "quelle"); var dst = Path.Combine(home, "ziel");
                Directory.CreateDirectory(src); Directory.CreateDirectory(dst);
                foreach (var n in new[] { "setup.exe", "spiel.lnk", "bild.png", "a.txt" }) File.WriteAllText(Path.Combine(src, n), "x");
                foreach (var n in new[] { "setup.exe", "spiel.lnk" })
                {
                    if (child) Assert.Throws<UnauthorizedAccessException>(() => w.Files.Paste(Path.Combine(src, n), dst, move: false));
                    else w.Files.Paste(Path.Combine(src, n), dst, move: false);
                    Assert.Equal(!child, File.Exists(Path.Combine(dst, n)));
                }
                Assert.True(File.Exists(w.Files.Paste(Path.Combine(src, "bild.png"), dst, move: false)));
                if (child) Assert.Throws<UnauthorizedAccessException>(() => w.Files.Rename(Path.Combine(src, "a.txt"), "a.exe"));
                else w.Files.Rename(Path.Combine(src, "a.txt"), "a.exe");
                Assert.Equal(!child, File.Exists(Path.Combine(src, "a.exe")));
                w.FilesGo(MainWindow.PcPlace); Settle();
                if (child) Assert.NotEqual(MainWindow.PcPlace, w.FilesDir); else Assert.Equal(MainWindow.PcPlace, w.FilesDir);
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 M3: Kind – Download einer setup.exe bricht mit Meldung ab; ein Bild geht in die Warteschlange")]
    public void Download_Kind()
    {
        var (w, _) = Open(true);
        try
        {
            Assert.Equal(0, w.DownloadAdd("https://example.org/setup.exe"));
            Assert.Equal(0, w.DownloadAdd("https://example.org/start.lnk"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("setup.exe"));
            Assert.Equal(1, w.DownloadAdd("https://example.org/bild.png"));
        }
        finally { w.Close(); }
    }
}
