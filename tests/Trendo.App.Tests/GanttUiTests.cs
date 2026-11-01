using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0308: Vorhaben als Zeitplan mit echter Maus – Balken verschieben, Ende ziehen, Abhängigkeit per Punkt ziehen (Nachfolger rückt nach), Rückgängig, Schritt-Feld.
public class Gantt_Ui
{
    [AvaloniaFact(DisplayName = "T-0308: Gantt – Balken ziehen verschiebt um Tage, Kante ziehen verlängert, Punkt auf anderen Balken = Abhängigkeit mit Nachrücken, Rückgängig, Beginn/Ende/„kommt nach“ im Feld")]
    public void Ui_gantt()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1700; w.Height = 1200;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        void Drag(Point a, Point b) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b - a) * (i / 6.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle(); }
        w.Show("vorhaben"); Settle();
        w.Find<TextBox>("suite-name").Text = "Gartenfest"; w.Find<TextBox>("suite-start").Text = DateTime.Today.ToString("dd.MM.yyyy");
        Click("suite-kit-event"); Click("suite-mode-zeitplan");
        var dir = Path.Combine(w.SuiteFolder, "Gartenfest");
        List<PlanBar> B() => CreationSuite.Bars(CreationSuite.Steps(dir));
        var g = w.GanttForTests!; g.BringIntoView(); Settle();
        Point At(GanttCanvas c, Point p) => c.TranslatePoint(p, w)!.Value;
        // Balken 1 um 2 Tage nach rechts ziehen
        var b0 = B()[0]; var r0 = g.BarRect(0);
        Drag(At(g, r0.Center), At(g, r0.Center + new Vector(2 * g.DayW, 0)));
        Assert.Equal((b0.Start.AddDays(2), b0.End.AddDays(2)), (B()[0].Start, B()[0].End));
        Assert.Equal("Verschieben", w.PlanUndoLabel);
        // Ende von Balken 2 um 1 Tag verlängern
        g = w.GanttForTests!; g.BringIntoView(); Settle(); var b1 = B()[1]; var r1 = g.BarRect(1);
        Drag(At(g, new Point(r1.Right - 2, r1.Center.Y)), At(g, new Point(r1.Right - 2 + g.DayW, r1.Center.Y)));
        Assert.Equal((b1.Start, b1.End.AddDays(1)), (B()[1].Start, B()[1].End));
        // Abhängigkeit: Punkt am Ende von Balken 2 auf Balken 3 ziehen → 3 kommt nach 2 und rückt hinter dessen Ende
        g = w.GanttForTests!; g.BringIntoView(); Settle(); g.Selected = 1; g.InvalidateVisual(); Settle();
        Drag(At(g, g.LinkDot(1)), At(g, g.BarRect(2).Center));
        var s3 = CreationSuite.Steps(dir)[2]; Assert.Equal([2], s3.After!);
        Assert.True(B()[2].Start > B()[1].End);
        // Rückgängig nimmt die Abhängigkeit zurück
        Click("suite-plan-undo"); Assert.Null(CreationSuite.Steps(dir)[2].After);
        // Feld des gewählten Schritts: Beginn/Ende und „kommt nach“
        Click("suite-plan-row-3");
        var start = DateTime.Today.AddDays(20); w.Find<TextBox>("suite-plan-start").Text = start.ToString("dd.MM.yyyy"); w.Find<TextBox>("suite-plan-end").Text = start.AddDays(2).ToString("dd.MM.yyyy");
        w.Find<CheckBox>("suite-plan-after-1").IsChecked = true; Click("suite-plan-apply");
        var b3 = B()[3]; Assert.Equal((start, start.AddDays(2)), (b3.Start, b3.End)); Assert.Equal([1], b3.Step.After!);
        Assert.Contains("kritischen Pfad", w.Find<TextBlock>("suite-plan-info").Text);
    }
}
