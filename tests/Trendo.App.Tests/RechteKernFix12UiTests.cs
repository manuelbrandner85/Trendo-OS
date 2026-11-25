using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 12. Prüfrunde an der Oberfläche: Code-Werkstatt und Notiz-Editor legen im Kinderprofil keine ausführbaren oder auslösenden
/// Dateien an (Befund: start.bat, .ps1, .url, .hta im Desktop-Projektordner). Ohne statische Ersatz-Hooks; startet nichts echt.</summary>
public class RechteKernFix12Ui
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

    [AvaloniaFact(DisplayName = "T-0764 M: Kind – Code-Werkstatt legt start.bat, .ps1, .url, .hta nicht an (Meldung, keine Datei); README.md geht; Erwachsene wie bisher")]
    public void Code_Werkstatt_Kind()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, _) = Open(child);
            try
            {
                var proj = Path.Combine(w.Files.Home, "Desktop-Projekt"); Directory.CreateDirectory(proj); // steht für den Desktop (freigegeben)
                w.CodeOpenFolder(proj);
                foreach (var n in new[] { "start.bat", "boese.ps1", "seite.url", "app.hta" })
                {
                    if (child) Assert.Throws<UnauthorizedAccessException>(() => w.CodeNewFile(n));
                    else w.CodeNewFile(n);
                    Assert.Equal(!child, File.Exists(Path.Combine(proj, n)));
                }
                w.CodeNewFile("README.md");
                Assert.True(File.Exists(Path.Combine(proj, "README.md")));
                if (child) Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("start.bat"));
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 M: Kind – Code-Werkstatt öffnet keinen Projektordner außerhalb der Freigabe; Notiz-Editor speichert nicht als .bat")]
    public void Ort_und_Notiz_Kind()
    {
        var (w, root) = Open(true);
        try
        {
            var outside = Path.Combine(root, "werkzeuge", "projekt"); Directory.CreateDirectory(outside); // Trendos Datenordner – nicht freigegeben
            Assert.Throws<UnauthorizedAccessException>(() => w.CodeOpenFolder(outside));
            var bat = Path.Combine(w.Files.Home, "notiz.bat");
            Assert.Throws<UnauthorizedAccessException>(() => w.EditorSave(bat));
            Assert.False(File.Exists(bat));
        }
        finally { w.Close(); }
    }
}
