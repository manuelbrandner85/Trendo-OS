using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

/// <summary>Vorgabe 27.10.2026: Ein Klick in einem Fenster öffnet das Ziel in demselben Fenster; maximiert bis an den Rand, Taskleiste bleibt.</summary>
public class WindowStayTests
{
    [AvaloniaFact(DisplayName = "Fenster: Klick auf eine Kategorie im Fenster bleibt im selben Fenster; ohne Klick eigenes Fenster; offene Seite wird wiederverwendet")]
    public void Stays_in_window()
    {
        var (w, _, _) = Ui.Open();
        w.Show("alltag-start"); Settle();
        Assert.Single(w.Windows.TaskOrder);
        var id = w.Windows.Active!.Id;
        // echter Mausklick auf die Kachel „Rechner“ im Fenster
        RealClick(w, V<Button>(w, "alltag-app-rechner"));
        Assert.Single(w.Windows.TaskOrder);
        Assert.Equal(("rechner", id), (w.Windows.Active!.Page, w.Windows.Active.Id));
        // Kachel eines anderen Studios im selben Fenster: Texte & Tabellen → Klick auf Trendo Tabelle
        w.Show("doku-start"); Settle();
        var docs = w.Windows.Active!.Id;
        RealClick(w, V<Button>(w, "doku-app-tabelle"));
        Assert.Equal(("tabelle", docs), (w.Windows.Active!.Page, w.Windows.Active.Id));
        var count = w.Windows.TaskOrder.Count;
        // dieselbe Seite erneut zeigen (z. B. nach Speichern) öffnet kein weiteres Fenster
        w.Show("tabelle"); Settle();
        Assert.Equal(count, w.Windows.TaskOrder.Count);
        Assert.Equal(docs, w.Windows.Active!.Id);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Fenster: maximiert füllt den ganzen Bildschirm – über Kopfleiste und Taskleiste, ohne Rand; Wiederherstellen bringt die Leisten zurück")]
    public void Maximize_full_area()
    {
        var (w, _, _) = Ui.Open();
        w.Show("settings"); Settle();
        RealClick(w, V<Button>(w, "win-max-settings"));
        Settle();
        var frame = V<Border>(w, "win-settings");
        var p = frame.TranslatePoint(default, w)!.Value;
        Assert.True(p.X <= 1 && p.Y <= 1, $"oben/links bleibt ein Rand: {p}");
        Assert.True(p.X + frame.Bounds.Width >= w.Bounds.Width - 2, "rechts bleibt ein Rand");
        Assert.True(p.Y + frame.Bounds.Height >= w.Bounds.Height - 2, "unten bleibt ein Rand");
        Assert.Equal(0, frame.BorderThickness.Left);
        var taskbar = w.GetVisualDescendants().OfType<Control>().First(c => c.Name == "task-settings");
        bool Free() { if (!taskbar.IsEffectivelyVisible || taskbar.TranslatePoint(new Avalonia.Point(taskbar.Bounds.Width / 2, taskbar.Bounds.Height / 2), w) is not { } c) return false; var hit = w.InputHitTest(c) as Avalonia.Visual; return hit is not null && (hit == taskbar || hit.GetVisualAncestors().Contains(taskbar)); }
        Assert.False(Free(), "maximiert liegt das Studio über der Taskleiste");
        RealClick(w, V<Button>(w, "win-max-settings")); Settle();
        taskbar = w.GetVisualDescendants().OfType<Control>().First(c => c.Name == "task-settings"); // die Taskleiste wird neu gezeichnet
        Assert.True(Free(), "nach dem Wiederherstellen ist die Taskleiste wieder frei");
        w.Close();
    }
}
