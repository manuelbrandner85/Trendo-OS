using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
using Cal = Trendo.Core.Sys.Calendar;

namespace Trendo.App.Tests;

/// <summary>O5 / Masterprompt 131: Aufgaben als Tafel – Karten zwischen Offen, In Arbeit und Erledigt ziehen, bearbeiten, Listen filtern, Rückgängig.</summary>
public class TodoBoard_Ui
{
    static Point Mid(MainWindow w, Control c) { c.BringIntoView(); Settle(); return c.TranslatePoint(new Point(c.Bounds.Width / 2, Math.Min(c.Bounds.Height / 2, 20)), w)!.Value; }
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }
    static void Drag(MainWindow w, Point a, Point b) { w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(a + (b - a) * (i / 5.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle(); }

    [AvaloniaFact(DisplayName = "Aufgaben-Tafel: anlegen, Karte nach „In Arbeit“ und „Erledigt“ ziehen, Klick öffnet Bearbeiten (Beschreibung, Liste), Listenfilter, Häkchen, Rückgängig")]
    public void Board_drag_edit()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Show("aufgaben"); Settle();
        foreach (var (t, l) in new[] { ("Noten kopieren", "Chor"), ("Steuer abgeben", "Haushalt") })
        { w.Find<TextBox>("todo-title").Text = t; w.Find<TextBox>("todo-list").Text = l; Click(w, "todo-add"); }
        Assert.Equal(2, w.Cal.Todos().Count); Assert.All(w.Cal.Todos(), t => Assert.Equal(0, t.Stage));
        // Karte „Noten kopieren“ nach „In Arbeit“ ziehen
        var card = w.GetVisualDescendants().OfType<Border>().First(b => b.Name?.StartsWith("todo-card-") == true && b.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "Noten kopieren"));
        { var to = Mid(w, V<Border>(w, "todo-col-1")); Drag(w, Mid(w, card), to); }
        var noten = w.Cal.Todos().Single(t => t.Title == "Noten kopieren"); Assert.Equal(1, noten.Stage); Assert.True(noten.Working);
        Assert.Contains("In Arbeit", w.Find<TextBlock>("todo-msg").Text);
        // weiter nach „Erledigt“
        card = w.GetVisualDescendants().OfType<Border>().First(b => b.Name?.StartsWith("todo-card-") == true && b.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "Noten kopieren"));
        { var to = Mid(w, V<Border>(w, "todo-col-2")); Drag(w, Mid(w, card), to); }
        Assert.True(w.Cal.GetTodo(noten.Uid)!.Done);
        // Klick auf „Steuer abgeben“ → Bearbeiten
        card = w.GetVisualDescendants().OfType<Border>().First(b => b.Name?.StartsWith("todo-card-") == true && b.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "Steuer abgeben"));
        var p = Mid(w, card); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
        w.Find<TextBox>("todo-edit-notes").Text = "Belege im Ordner 2025"; w.Find<TextBox>("todo-edit-due").Text = "15.11.2026";
        Click(w, "todo-edit-save");
        var st = w.Cal.Todos().Single(t => t.Title == "Steuer abgeben"); Assert.Equal("Belege im Ordner 2025", st.Notes); Assert.Equal(new DateTime(2026, 11, 15), st.Due);
        // Listenfilter
        Click(w, "todo-filter-" + 0); // „Chor“ (alphabetisch zuerst)
        Assert.DoesNotContain(w.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "Steuer abgeben" && x.IsEffectivelyVisible && x.FindAncestorOfType<Border>()?.Name?.StartsWith("todo-card") == true);
        Click(w, "todo-filter-alle");
        // Rückgängig: Bearbeiten, dann „Erledigt“
        Click(w, "todo-undo"); Assert.Equal("", w.Cal.GetTodo(st.Uid)!.Notes);
        Click(w, "todo-undo"); Assert.Equal(1, w.Cal.GetTodo(noten.Uid)!.Stage);
    }

    [Fact(DisplayName = "Aufgaben-Kern: Spalte setzen, erledigt beendet „in Arbeit“, ICS behält In-Arbeit und Beschreibung")]
    public void Core_stage_and_ics()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-todo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var c = new Cal(root); c.AddTodo(new TodoItem("a", "Probe", Notes: "Zeile"));
            Assert.Equal(1, c.SetStage("a", 1).Stage); c.SetDone("a", true); Assert.False(c.GetTodo("a")!.Working); Assert.Equal(2, c.GetTodo("a")!.Stage);
            c.SetStage("a", 1);
            var ics = Ics.Export([], c.Todos()); Assert.Contains("STATUS:IN-PROCESS", ics);
            var back = Ics.ImportTodos(ics).Single(); Assert.True(back.Working); Assert.Equal("Zeile", back.Notes);
            Assert.Throws<InvalidOperationException>(() => { c.SetStage("fehlt", 2); });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
