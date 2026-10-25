using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 44 in der Liste „Externe Programme“: Prüfung erst auf Knopfdruck, Öffnen erst danach.</summary>
public class Paket44_FachprogrammUi
{
    static Button B(Window w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); return b; }

    sealed class Runner : IProcessRunner
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

    static TextBlock Stand(Window w, string id) =>
        w.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "fach-stand-" + id);

    [AvaloniaFact(DisplayName = "Paket 44: die Liste lädt nichts, eine fehlgeschlagene Prüfung öffnet nicht")]
    public async Task Pruefung_fehlgeschlagen_oeffnet_nicht()
    {
        var (w, l, _) = Ui.Open();
        var runner = new Runner { Exit = 3, Output = "nein" };
        w.Runner = runner;
        w.AppPathOverride = id => id == "blender" ? "/fake/blender" : null;
        w.Show("prog-extern"); Settle();
        Assert.Contains("Nicht geladen", Stand(w, "blender").Text);
        Assert.Contains("nicht vorinstalliert", w.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "fach-hinweis").Text);
        Assert.Null(w.TryFind<Button>("fach-start-blender"));
        Assert.Empty(runner.Calls);
        Assert.Empty(l.Opened);

        RealClick(w, B(w, "fach-probe-blender"));
        for (var i = 0; i < 50 && !(Stand(w, "blender").Text ?? "").Contains("nicht geklappt"); i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.Contains("nicht geklappt", Stand(w, "blender").Text);
        Assert.Contains("Fehlgeschlagen", Stand(w, "blender").Text);
        Assert.Null(w.TryFind<Button>("fach-start-blender"));
        Assert.Empty(l.Opened);
        Assert.Single(runner.Calls);
        Assert.DoesNotContain(runner.Calls, c => c.Contains("winget") || c.Contains("curl"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Paket 44: nach bestandener Prüfung öffnet der zweite Knopf genau einmal")]
    public async Task Pruefung_dann_oeffnen()
    {
        var (w, l, _) = Ui.Open();
        var runner = new Runner();
        w.Runner = runner;
        w.AppPathOverride = id => id == "blender" ? "/fake/blender" : null;
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "fach-probe-blender"));
        for (var i = 0; i < 50 && w.TryFind<Button>("fach-start-blender") is null; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.Contains("kein Extra-Prozess", Stand(w, "blender").Text);
        Assert.Contains("Bestanden", Stand(w, "blender").Text);
        Assert.Empty(l.Opened);
        RealClick(w, B(w, "fach-start-blender"));
        Settle();
        Assert.Contains("/fake/blender", l.Opened.Single());
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Detail.Contains("/fake/blender") && e.Action.StartsWith("fachprogramm.probe"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Paket 44: die Seite listet den Katalog, Suchen startet nichts")]
    public void Katalog_und_Suchen()
    {
        var (w, l, _) = Ui.Open();
        var runner = new Runner();
        w.Runner = runner;
        w.AppPathOverride = id => id == "blender" ? "/fake/blender" : null;
        w.Show("prog-extern"); Settle();
        foreach (var p in FachprogrammKatalog.Alle)
        {
            Assert.Contains(p.Name, Stand(w, p.Id).Text);
            Assert.Contains("Nicht geladen", Stand(w, p.Id).Text);
        }
        Assert.Empty(runner.Calls);
        Assert.Empty(l.Opened);
        RealClick(w, B(w, "fach-suche-blender"));
        Settle();
        Assert.Contains("Gefunden, nicht geprüft", Stand(w, "blender").Text);
        Assert.Empty(runner.Calls);
        Assert.Empty(l.Opened);
        Assert.Null(w.TryFind<Button>("fach-start-blender"));
        RealClick(w, B(w, "fach-suche-unreal"));
        Settle();
        Assert.Contains("Nicht geladen", Stand(w, "unreal").Text);
        Assert.Contains("nichts installiert", Stand(w, "unreal").Text);
        Assert.Empty(runner.Calls);
        w.Close();
    }
}
