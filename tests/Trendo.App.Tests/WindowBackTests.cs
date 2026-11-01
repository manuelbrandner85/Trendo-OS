using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.App;
using Trendo.Core.Shell;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

/// <summary>Wunsch 01.11.2026: In jedem Fenster ein Zurück-Knopf; Navigation bleibt im Fenster statt neue Fenster zu öffnen.</summary>
public class WindowBackTests
{
    [AvaloniaFact(DisplayName = "Fenster: Zurück-Knopf führt im selben Fenster zur vorigen Seite; ohne vorige Seite ausgegraut; kein neues Fenster")]
    public void Back_button()
    {
        var (w, _, _) = Ui.Open();
        w.Show("alltag-start"); Settle();
        var id = w.Windows.Active!.Id;
        var back = V<Button>(w, "win-back-" + id);
        Assert.False(back.IsEnabled);
        RealClick(w, V<Button>(w, "alltag-app-rechner"));
        Assert.Equal("rechner", w.Windows.Active!.Page);
        Assert.True(back.IsEnabled);
        Assert.Contains("Zurück zu", ToolTip.GetTip(back) as string);
        // dieselbe Seite neu zeichnen (z. B. nach Speichern) ist kein neuer Schritt
        w.Show("rechner"); Settle();
        Assert.Single(w.Windows.Get(id)!.History);
        RealClick(w, back);
        Assert.Equal(("alltag-start", id), (w.Windows.Active!.Page, w.Windows.Active.Id));
        Assert.Single(w.Windows.TaskOrder);
        Assert.False(back.IsEnabled);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Fenster: Alt+← geht im aktiven Fenster zurück; mehrere Schritte nacheinander")]
    public void Alt_left()
    {
        var (w, _, _) = Ui.Open();
        w.Show("alltag-start"); Settle();
        var id = w.Windows.Active!.Id;
        RealClick(w, V<Button>(w, "alltag-app-rechner"));
        var first = w.Windows.Active!.Page;
        w.GoBack(id); Settle();
        Assert.Equal("alltag-start", w.Windows.Active!.Page);
        RealClick(w, V<Button>(w, "alltag-app-rechner")); Settle();
        Assert.Equal(first, w.Windows.Active!.Page);
        w.Key(PhysicalKey.ArrowLeft, RawInputModifiers.Alt); Settle();
        Assert.Equal(("alltag-start", id), (w.Windows.Active!.Page, w.Windows.Active.Id));
        Assert.Single(w.Windows.TaskOrder);
        w.Close();
    }

    [Fact(DisplayName = "Fenster-Verlauf: merkt Seitenwechsel (höchstens 30), nicht beim Neuzeichnen oder Zurückgehen")]
    public void History_core()
    {
        var m = new WindowManager();
        m.Open("a", "p0", "A");
        for (var i = 1; i <= 40; i++) m.Open("a", "p" + i, "A");
        var a = m.Get("a")!;
        Assert.Equal(WindowManager.HistoryMax, a.History.Count);
        Assert.Equal("p39", m.Back("a"));
        m.Open("a", "p39", "A", back: true);
        Assert.Equal(29, a.History.Count);
        m.Open("a", "p39", "A"); // gleiche Seite: kein Schritt
        Assert.Equal(29, a.History.Count);
        Assert.Null(m.Back("fehlt"));
        var b = new WindowManager(); b.Open("b", "x", "B");
        Assert.False(b.Get("b")!.CanGoBack); Assert.Null(b.Back("b"));
    }
}
