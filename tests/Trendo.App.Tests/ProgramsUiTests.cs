using Avalonia; using Avalonia.Input; using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

/// <summary>T-0203: externe Programme mit der aktuellen Datei – Fake-Launcher prüft Programmpfad UND Dateiargument.</summary>
public class T0203_Programme_Ui
{
    static Button B(Window w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); return b; }

    [AvaloniaFact(DisplayName = "T-0203: „In LibreOffice öffnen“ startet die Datei, wenn --version schnell antwortet")]
    public void LibreOfficeGetsFile()
    {
        var (w, l, _) = Ui.Open();
        var runner = new ExitRunner { Output = "LibreOffice 24.8" };
        w.Runner = runner;
        w.AppPathOverride = id => "/fake/" + id;
        w.Show("office"); Settle();
        RealClick(w, B(w, "office-new-privat"));
        var n = l.Opened.Count;
        w.Show("office"); Settle();
        RealClick(w, B(w, "open-libreoffice-office"));
        Settle();
        Assert.Equal(n + 1, l.Opened.Count);
        Assert.Contains(l.Opened, o => o.Contains("/fake/libreoffice \"") && o.Contains(".docx"));
        Assert.Contains(runner.Calls, c => c.Contains("/fake/libreoffice") && c.Contains("--version"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Geöffnet"));
    }

    [AvaloniaFact(DisplayName = "T-0203: LibreOffice bleibt zu, wenn die Versionsabfrage zu lange dauert")]
    public async Task LibreOfficeZeitLaeuftAb()
    {
        var (w, l, _) = Ui.Open();
        var runner = new WartenderRunner();
        w.Runner = runner;
        w.VersionspruefungDauer = TimeSpan.FromMilliseconds(80);
        w.AppPathOverride = id => "/fake/" + id;
        w.Show("office"); Settle();
        RealClick(w, B(w, "office-new-privat"));
        var n = l.Opened.Count;
        w.Show("office"); Settle();
        RealClick(w, B(w, "open-libreoffice-office"));
        for (var i = 0; i < 40 && !w.GetLogicalTexts().Any(t => t.Contains("zu lange gedauert")); i++)
        {
            await Task.Delay(40);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.Equal(n, l.Opened.Count);
        Assert.DoesNotContain(l.Opened, o => o.Contains("libreoffice"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("zu lange gedauert") && t.Contains("Nichts geöffnet"));
        Assert.Contains(runner.Calls, c => c.Contains("--version"));
    }

    [AvaloniaFact(DisplayName = "T-0203: „Einrichten“ fragt, „Ja“ lädt nichts, solange die Prüfung nicht besteht")]
    public void SetupAsksThenOpens()
    {
        var (w, l, _) = Ui.Open();
        var installs = 0;
        w.AppPathOverride = _ => null;
        w.InstallOverride = _ => { installs++; return Task.FromResult(true); };
        Directory.CreateDirectory(Path.Combine(w.DevFolder, "meine-seite"));
        w.Show("dev"); Settle();
        RealClick(w, B(w, "setup-vscode-dev-0"));
        Assert.Equal(0, installs);
        Assert.Contains("bestandene Prüfung", w.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "ask-vscode-dev-0").Text);
        RealClick(w, B(w, "setup-yes-vscode-dev-0"));
        Settle(); Settle();
        Assert.Equal(0, installs);
        Assert.Empty(l.Opened);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("nichts installiert") && t.Contains("Nichts geöffnet"));
        Assert.Null(w.TryFind<Button>("open-vscode-dev-0"));
        Assert.NotNull(V<Button>(w, "setup-vscode-dev-0"));
    }

    [AvaloniaFact(DisplayName = "T-0203: Abbrechen installiert nichts")]
    public void CancelInstallsNothing()
    {
        var (w, _, _) = Ui.Open();
        var installs = 0; w.AppPathOverride = _ => null; w.InstallOverride = _ => { installs++; return Task.FromResult(true); };
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "setup-obs-prog-extern"));
        RealClick(w, B(w, "setup-no-obs-prog-extern"));
        Assert.Equal(0, installs);
        Assert.NotNull(V<Button>(w, "setup-obs-prog-extern"));
    }

    [AvaloniaFact(DisplayName = "T-0203: „In OBS öffnen“ startet die Sammlung, wenn --version schnell antwortet")]
    public void ObsGetsCollection()
    {
        var (w, l, root) = Ui.Open();
        var runner = new ExitRunner { Output = "OBS Studio 30.2.3" };
        w.Runner = runner;
        w.ObsPruefungWieWindows = false;
        w.AppPathOverride = id => "/fake/" + id; w.ObsDirOverride = Path.Combine(root, "obs");
        w.Show("sendung"); Settle();
        RealClick(w, B(w, "bc-add-run"));
        var n = l.Opened.Count;
        RealClick(w, B(w, "open-obs-sendung"));
        Settle();
        Assert.Equal(n + 1, l.Opened.Count);
        Assert.Contains(l.Opened, o => o.Contains("/fake/obs") && o.Contains("--collection") && o.Contains("Trendo"));
        Assert.Contains(runner.Calls, c => c.Contains("/fake/obs") && c.Contains("--version"));
        Assert.True(Directory.Exists(w.ObsDirOverride));
        Assert.NotEmpty(Directory.GetFiles(w.ObsDirOverride!, "*.json"));
    }

    [AvaloniaFact(DisplayName = "T-0203: OBS unter Windows liest die Dateiversion und öffnet ohne Prozess in der Prüfung")]
    public void ObsWindowsDateiversionOeffnet()
    {
        var (w, l, root) = Ui.Open();
        var runner = new ExitRunner { Exit = 9, Output = "darf nicht laufen" };
        w.Runner = runner;
        w.ObsPruefungWieWindows = true;
        w.DateiversionLesen = _ => "30.0.0";
        w.AppPathOverride = id => "/fake/" + id;
        w.ObsDirOverride = Path.Combine(root, "obs");
        w.Show("sendung"); Settle();
        RealClick(w, B(w, "bc-add-run"));
        RealClick(w, B(w, "open-obs-sendung"));
        Settle();
        Assert.Empty(runner.Calls);
        Assert.Contains(l.Opened, o => o.Contains("/fake/obs") && o.Contains("--collection"));
    }

    [AvaloniaFact(DisplayName = "T-0203: OBS startet nicht, wenn die Dateiversion fehlt")]
    public void ObsOhneDateiversionBleibtZu()
    {
        var (w, l, root) = Ui.Open();
        var runner = new ExitRunner();
        w.Runner = runner;
        w.ObsPruefungWieWindows = true;
        w.DateiversionLesen = _ => null;
        w.AppPathOverride = id => "/fake/" + id;
        w.ObsDirOverride = Path.Combine(root, "obs");
        w.Show("sendung"); Settle();
        RealClick(w, B(w, "bc-add-run"));
        var n = l.Opened.Count;
        RealClick(w, B(w, "open-obs-sendung"));
        Settle();
        Assert.Equal(n, l.Opened.Count);
        Assert.Empty(runner.Calls);
        Assert.False(Directory.Exists(w.ObsDirOverride));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("nicht lesbar") && t.Contains("Nichts geöffnet"));
    }

    [AvaloniaFact(DisplayName = "T-0203: „Starten“ öffnet LibreOffice und Blender erst nach bestandener Prüfung und Freigabe")]
    public void CentralList()
    {
        var (w, l, _) = Ui.Open();
        var runner = new ExitRunner();
        w.Runner = runner;
        w.AppPathOverride = id => "/fake/" + id;
        w.Show("prog-extern"); Settle();
        foreach (var a in ExternalApps.All) Assert.NotNull(V<Button>(w, "prog-run-" + a.Id));
        RealClick(w, B(w, "prog-run-libreoffice"));
        Settle();
        Assert.Equal("/fake/libreoffice ", l.Opened.Single());
        RealClick(w, B(w, "prog-run-blender"));
        Settle();
        Assert.Equal(["/fake/libreoffice ", "/fake/blender "], l.Opened);
        Assert.Contains(runner.Calls, c => c.Contains("/fake/libreoffice") && c.Contains("--version"));
        Assert.Contains(runner.Calls, c => c.Contains("/fake/blender") && c.Contains("--version"));
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "fachprogramm.aufruf");
    }

    [AvaloniaFact(DisplayName = "T-0203: „In VS Code öffnen“ startet den Ordner, wenn --version schnell antwortet")]
    public void VscodeOeffnetOrdner()
    {
        var (w, l, _) = Ui.Open();
        var runner = new ExitRunner { Output = "1.90.0" };
        w.Runner = runner;
        w.AppPathOverride = id => "/fake/" + id;
        var ordner = Path.Combine(w.DevFolder, "meine-seite");
        Directory.CreateDirectory(ordner);
        w.Show("dev"); Settle();
        RealClick(w, B(w, "open-vscode-dev-0"));
        Settle();
        Assert.Contains(l.Opened, o => o.Contains("/fake/vscode \"") && o.Contains("meine-seite"));
        Assert.Contains(runner.Calls, c => c.Contains("/fake/vscode") && c.Contains("--version"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Geöffnet"));
    }

    [AvaloniaFact(DisplayName = "T-0203: „Starten“ öffnet nicht, wenn die Prüfung scheitert")]
    public void StartBleibtZuWennPruefungScheitert()
    {
        var (w, l, _) = Ui.Open();
        w.Runner = new ExitRunner { Exit = 3, Output = "nein" };
        w.AppPathOverride = id => id == "blender" ? "/fake/blender" : null;
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "prog-run-blender"));
        Settle();
        Assert.Empty(l.Opened);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("nicht geklappt") && t.Contains("Nichts geöffnet"));
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Action == "fachprogramm.aufruf");
    }

    [AvaloniaFact(DisplayName = "T-0203: „Ja, einrichten“ öffnet keine Herstellerseite, solange die Prüfung nicht besteht")]
    public void EinrichtenOeffnetKeineHerstellerseite()
    {
        var (w, l, _) = Ui.Open();
        var installs = 0;
        w.AppPathOverride = _ => null;
        w.InstallOverride = _ => { installs++; return Task.FromResult(true); };
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "setup-unreal-prog-extern"));
        RealClick(w, B(w, "setup-yes-unreal-prog-extern"));
        Settle();
        Assert.Equal(0, installs);
        Assert.Empty(l.Opened);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Nichts geöffnet"));
    }

    [AvaloniaFact(DisplayName = "Spiele: Unreal öffnen und die Herstellerseite bleiben der alte Weg")]
    public void SpieleBleibenBeimAltenWeg()
    {
        var (w, l, _) = Ui.Open();
        var runner = new ExitRunner { Exit = 9, Output = "nicht prüfen" };
        w.Runner = runner;
        w.EngineFinder = () => [new GameStudio.Engine("5.5", "/nicht/da")];
        w.AppPathOverride = id => id == "unreal" ? "/fake/unreal" : null;
        w.Show("spiele"); Settle();
        w.Find<Button>("game-ue-create").Press();
        Settle();
        w.Show("spiele"); Settle();
        RealClick(w, B(w, "open-unreal-spiele-0"));
        Assert.Contains(l.Opened, o => o.Contains("/fake/unreal"));
        Assert.Empty(runner.Calls);
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Action.StartsWith("fachprogramm"));

        l.Opened.Clear();
        w.AppPathOverride = _ => null;
        w.Show("spiele"); Settle();
        RealClick(w, B(w, "setup-unreal-spiele-0"));
        RealClick(w, B(w, "setup-yes-unreal-spiele-0"));
        Settle();
        Assert.Contains(l.Opened, o => o.Contains("unrealengine.com"));
        Assert.Empty(runner.Calls);
    }

    sealed class ExitRunner : IProcessRunner
    {
        public int Exit { get; set; }
        public string Output { get; set; } = "Blender 4.2.0";
        public List<string> Calls { get; } = [];
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add(file + " " + args);
            return Task.FromResult((Exit, Output));
        }
    }

    sealed class WartenderRunner : IProcessRunner
    {
        public List<string> Calls { get; } = [];
        public async Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add(file + " " + args);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (0, "");
        }
    }
}
