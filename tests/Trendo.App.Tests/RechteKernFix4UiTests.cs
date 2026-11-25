using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 4. Prüfrunde an der Oberfläche: Code-Werkstatt und weitere Befehlswege (H), Ordner im eigenen Dateimanager (M3),
/// sichtbarer Hinweis zu den Grenzen (M1). Startet nichts echt (Testmodus, FakeLauncher, Ersatz für Claude/n8n).</summary>
public class RechteKernFix4Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var l = new FakeLauncher();
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, l, root);
    }

    [AvaloniaFact(DisplayName = "T-0764 H: Code-Werkstatt – Kind: Ausführen, Terminal verlangen das Recht (vor dem Testmodus), deutlicher Hinweis; Erwachsene unverändert")]
    public void Code_Werkstatt()
    {
        foreach (var child in new[] { true, false })
        {
            var (w, _, root) = Open(child);
            try
            {
                var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj); File.WriteAllText(Path.Combine(proj, "start.ps1"), "Write-Output hi");
                w.CodeOpenFolder(proj); Settle();
                w.CodeRunProject();
                w.CodeTerminal("dir");
                var denied = w.Core.Audit.ReadAll().Count(a => a.Action == "rbac.denied" && a.Detail.Contains("code "));
                if (child)
                {
                    Assert.Equal(2, denied);
                    Assert.Contains("Erwachsenen-Profil", w.CodeOutput);
                    Assert.Contains("Erwachsenen-Profil", w.CodeTerminalText);
                }
                else
                {
                    Assert.Equal(0, denied);
                    Assert.Contains("Testmodus", w.CodeOutput); // Erwachsene: wie bisher (im Testmodus startet nichts)
                }
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 H: Weitere Befehlswege – Claude Code im Terminal, n8n starten – im Kinderprofil nicht")]
    public async Task Weitere_Befehlswege()
    {
        var (w, _, root) = Open(true);
        try
        {
            // ohne statische Ersatz-Hooks (parallel laufende Klassen nutzen sie mit): die Prüfung steht vor jedem Start und vor dem Protokolleintrag „claude.open“
            w.ClaudeOpen(root);
            await w.AutomationStart("n8n");
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action is "claude.open");
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("claude.terminal"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("automation.start"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 M3: Ordner öffnen – Kind: Trendos Dateimanager statt Windows-Explorer; Erwachsene: wie bisher")]
    public void Ordner_im_Dateimanager()
    {
        var (c, cl, croot) = Open(true);
        try
        {
            var dir = Path.Combine(c.Files.Home, "Bilder"); Directory.CreateDirectory(dir); // 11. Prüfrunde: ein Ordner der Freigabe (andere zeigt der Dateimanager Kindern nicht)
            Assert.True(c.LaunchAs(dir)); Settle();
            Assert.Empty(cl.Opened);
            Assert.Equal("dateien", c.CurrentView);
            Assert.Equal(dir, c.FilesDir);
        }
        finally { c.Close(); }
        var (w, wl, wroot) = Open(false);
        try
        {
            var dir = Path.Combine(wroot, "Bilder"); Directory.CreateDirectory(dir);
            Assert.True(w.LaunchAs(dir));
            Assert.Equal([dir], wl.Opened);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 M1: Personen-Seite zeigt im Kinderprofil den ehrlichen Hinweis zu den Grenzen (eigenes Windows-Konto)")]
    public void Hinweis_sichtbar()
    {
        var (w, _, _) = Open(true);
        try
        {
            w.Show("personen"); Settle();
            Assert.Contains("eigenes Windows-Konto", w.Find<TextBlock>("kinderschutz-hinweis").Text);
        }
        finally { w.Close(); }
    }
}
