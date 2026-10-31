using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>SYS-05 / Masterprompt 131: Abläufe als sichtbarer Ablaufplan – Bausteine anhängen, per Maus umsortieren, rechts einstellen, Rückgängig, Freigabe nach Änderung.</summary>
public class WorkflowFlow_Ui
{
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }
    static Point Mid(MainWindow w, Control c) { c.BringIntoView(); Settle(); return c.TranslatePoint(new Point(c.Bounds.Width / 2, 20), w)!.Value; }

    [AvaloniaFact(DisplayName = "Ablaufplan: Bausteine anhängen, Schritt per Maus nach oben ziehen, einstellen, Probelauf, Rückgängig; Änderung an freigegebenem Ablauf verlangt neue Freigabe")]
    public void Flow_edit()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(Path.Combine(home, "Downloads")); MainWindow.WorkflowHome = home;
        w.Show("ablaeufe"); Settle();
        Click(w, "wf-tpl-pdf"); var wf = w.Workflows.All().Single(); Assert.Equal(wf.Id, w.WfSelected);
        var n0 = wf.Steps.Count;
        Assert.NotNull(V<Border>(w, "wf-node-trigger"));
        // Freigeben, dann Baustein „Als ZIP packen“ anhängen → neue Freigabe nötig
        Click(w, "wf-approve-0"); Assert.True(w.Workflows.Get(wf.Id)!.IsApproved);
        Click(w, "wf-add-zippen");
        wf = w.Workflows.Get(wf.Id)!; Assert.Equal(n0 + 1, wf.Steps.Count); Assert.Equal(StepKind.Zippen, wf.Steps[^1].Kind); Assert.Equal(n0, w.WfNode);
        Assert.False(wf.IsApproved); Assert.Contains("neue Freigabe", w.Find<TextBlock>("wf-msg").Text);
        // rechts einstellen
        w.Find<TextBox>("wf-edit-to").Text = Path.Combine(home, "sicherung.zip"); Click(w, "wf-edit-save");
        Assert.EndsWith("sicherung.zip", w.Workflows.Get(wf.Id)!.Steps[^1].To);
        // letzten Schritt per Maus auf den ersten ziehen
        var last = V<Border>(w, $"wf-node-{n0}"); var first = V<Border>(w, "wf-node-0");
        var to = Mid(w, first); var from = Mid(w, last); to = Mid(w, first);
        w.MouseDown(from, MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(from + (to - from) * (i / 5.0)); Settle(); } w.MouseUp(to, MouseButton.Left); Settle();
        wf = w.Workflows.Get(wf.Id)!; Assert.Equal(StepKind.Zippen, wf.Steps[0].Kind); Assert.Equal(0, w.WfNode);
        // Klick auf den Start öffnet die Start-Einstellungen
        var trig = V<Border>(w, "wf-node-trigger"); var tp = Mid(w, trig); w.MouseDown(tp, MouseButton.Left); w.MouseUp(tp, MouseButton.Left); Settle();
        Assert.Equal(-1, w.WfNode); Assert.NotNull(w.TryFind<ComboBox>("wf-edit-trigger"));
        w.Find<ComboBox>("wf-edit-trigger").SelectedIndex = 1; w.Find<TextBox>("wf-edit-at").Text = "7:30"; Click(w, "wf-edit-save");
        Assert.Equal((TriggerKind.Taeglich, "07:30"), (w.Workflows.Get(wf.Id)!.Trigger, w.Workflows.Get(wf.Id)!.At));
        // falsche Uhrzeit: Meldung, nichts geändert
        w.Find<TextBox>("wf-edit-at").Text = "25 Uhr"; Click(w, "wf-edit-save"); Assert.Equal("07:30", w.Workflows.Get(wf.Id)!.At); Assert.Contains("HH:MM", w.Find<TextBlock>("wf-msg").Text);
        // Probelauf ändert nichts
        Click(w, "wf-dry-0"); Assert.True(w.LastWfRun!.DryRun);
        // Rückgängig: Start, Umsortieren
        Click(w, "wf-undo"); Assert.Equal(TriggerKind.NeueDatei, w.Workflows.Get(wf.Id)!.Trigger);
        Click(w, "wf-undo"); Assert.Equal(StepKind.Zippen, w.Workflows.Get(wf.Id)!.Steps[^1].Kind);
    }
}
