using Avalonia; using Avalonia.Input; using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

/// <summary>T-0203: externe Programme mit der aktuellen Datei – Fake-Launcher prüft Programmpfad UND Dateiargument.</summary>
public class T0203_Programme_Ui
{
    static Button B(Window w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); return b; }

    [AvaloniaFact(DisplayName = "T-0203: Office – neues Dokument, dann „In LibreOffice öffnen“ übergibt genau diese Datei")]
    public void LibreOfficeGetsFile()
    {
        var (w, l, _) = Ui.Open();
        w.AppPathOverride = id => "/fake/" + id;
        w.Show("office"); Settle();
        RealClick(w, B(w, "office-new-privat"));
        var doc = l.Opened.Last(); // Vorlage wird angelegt und geöffnet
        w.Show("office"); Settle();
        RealClick(w, B(w, "open-libreoffice-office"));
        Assert.Equal($"/fake/libreoffice \"{doc}\"", l.Opened.Last());
    }

    [AvaloniaFact(DisplayName = "T-0203: fehlt das Programm – „Einrichten“ fragt einmal, installiert erst nach „Ja“, danach ist „Öffnen“ ohne Neustart da")]
    public void SetupAsksThenOpens()
    {
        var (w, l, root) = Ui.Open();
        var installed = false; var installs = 0;
        w.AppPathOverride = id => installed ? "/fake/" + id : null;
        w.InstallOverride = c => { installs++; installed = true; return Task.FromResult(true); };
        Directory.CreateDirectory(Path.Combine(w.DevFolder, "meine-seite"));
        w.Show("dev"); Settle();
        RealClick(w, B(w, "setup-vscode-dev-0"));
        Assert.Equal(0, installs);
        Assert.Contains("Jetzt einrichten", w.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "ask-vscode-dev-0").Text);
        RealClick(w, B(w, "setup-yes-vscode-dev-0"));
        Settle(); Settle();
        Assert.Equal(1, installs);
        RealClick(w, B(w, "open-vscode-dev-0"));
        Assert.Equal($"/fake/vscode \"{Path.Combine(w.DevFolder, "meine-seite")}\"", l.Opened.Last());
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

    [AvaloniaFact(DisplayName = "T-0203: Sendung – „In OBS öffnen“ legt die Szenensammlung an und startet OBS damit")]
    public void ObsGetsCollection()
    {
        var (w, l, root) = Ui.Open();
        w.AppPathOverride = id => "/fake/" + id; w.ObsDirOverride = Path.Combine(root, "obs");
        w.Show("sendung"); Settle();
        RealClick(w, B(w, "bc-add-run"));
        RealClick(w, B(w, "open-obs-sendung"));
        Assert.StartsWith("/fake/obs \"--collection\" \"Trendo ", l.Opened.Last());
        Assert.Single(Directory.GetFiles(w.ObsDirOverride, "*.json"));
    }

    [AvaloniaFact(DisplayName = "T-0203: zentrale Liste zeigt alle externen Programme mit „Starten“")]
    public void CentralList()
    {
        var (w, l, _) = Ui.Open();
        w.AppPathOverride = id => "/fake/" + id;
        w.Show("prog-extern"); Settle();
        foreach (var a in ExternalApps.All) Assert.NotNull(V<Button>(w, "prog-run-" + a.Id));
        RealClick(w, B(w, "prog-run-blender"));
        Assert.Equal("/fake/blender ", l.Opened.Last());
    }
}
