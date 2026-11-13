using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: Abläufe als Ablauf-Editor – Probelauf mit sichtbarem Fortschritt: Bausteine leuchten nacheinander auf, danach Ergebnis je Baustein und am Ende.
public class AblaufEditor_Ui
{
    static async Task Until(Func<bool> ok, int ms = 6000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "Ablauf-Editor: Probelauf mit Anzeige – jeder Baustein leuchtet vorher auf, danach mit Anzahl bzw. bei Fehler, Ergebnis am Ende; nichts wird geändert")]
    public async Task Probelauf_mit_Anzeige()
    {
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); var dl = Path.Combine(home, "Downloads"); Directory.CreateDirectory(dl); MainWindow.WorkflowHome = home;
        foreach (var n in new[] { "a.pdf", "b.pdf" }) File.WriteAllText(Path.Combine(dl, n), n);
        w.WfProbeDelayMs = 0;
        w.Show("ablaeufe"); w.Find<Button>("wf-tpl-pdf").Press();
        var wf = w.Workflows.All().Single();
        var lit = new List<int>();
        w.Workflows.Get(wf.Id)!.Steps.Insert(0, new WfStep { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "Sammlung") }); w.Workflows.Update(wf); w.Show("ablaeufe");
        var task = w.WfProbeRun(wf);
        // während des Laufs: der aktuelle Baustein leuchtet
        await Until(() => task.IsCompleted || w.WfProbeStep >= 0);
        if (!task.IsCompleted) lit.Add(w.WfProbeStep);
        await task; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var probe = w.WfProbe!; Assert.True(probe.DryRun); Assert.Equal(wf.Steps.Count, probe.Steps.Count);
        Assert.StartsWith("Ordner anlegen", w.Find<TextBlock>("wf-node-0-badge").Text);
        Assert.Contains(Enumerable.Range(1, wf.Steps.Count - 1), i => w.Find<TextBlock>($"wf-node-{i}-badge").Text!.Contains("2 Dateien"));
        Assert.Contains("im Probelauf wurde nichts geändert", w.Find<TextBlock>("wf-end-text").Text);
        Assert.False(Directory.Exists(Path.Combine(home, "Sammlung")));
        Assert.True(File.Exists(Path.Combine(dl, "a.pdf")));
        // Fehler: Ordner außerhalb → roter Baustein, Ende erklärt
        wf.Steps.Insert(1, new WfStep { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(root, "fremd") }); w.Workflows.Update(wf);
        await w.WfProbeRun(wf); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("✕", w.Find<TextBlock>("wf-node-1-badge").Text);
        Assert.StartsWith("– nicht erreicht", w.Find<TextBlock>("wf-node-2-badge").Text);
        Assert.Contains("roten Baustein", w.Find<TextBlock>("wf-end-text").Text);
        Assert.NotNull(w.TryFind<Button>("wf-probe"));
    }
}
