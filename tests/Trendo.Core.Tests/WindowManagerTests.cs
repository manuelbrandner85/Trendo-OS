using Trendo.Core.Shell;
using Xunit;
namespace Trendo.Core.Tests;

public class WindowManager_Logik
{
    [Fact(DisplayName = "Fenster: öffnen/nach vorn, minimieren, maximieren, schließen, Taskleisten-Klick, Verschieben bleibt im Bereich, max. 8")]
    public void Rules()
    {
        var m = new WindowManager { AreaW = 1000, AreaH = 600 };
        m.Open("a", "a", "A"); m.Open("b", "b", "B");
        Assert.Equal("b", m.Active!.Id); m.Open("a", "a2", "A"); Assert.Equal(("a", "a2"), (m.Active!.Id, m.Active.Page)); Assert.Equal(2, m.TaskOrder.Count);
        Assert.Equal(["a", "b"], m.TaskOrder.Select(w => w.Id));
        m.TaskbarClick("a"); Assert.True(m.Get("a")!.Minimized); Assert.Equal("b", m.Active!.Id);
        m.TaskbarClick("a"); Assert.Equal("a", m.Active!.Id);
        m.ToggleMaximize("a"); Assert.Equal((0d, 0d, 1000d, 600d), m.Rect(m.Get("a")!));
        m.Move("a", 50, 50); Assert.Equal((0d, 0d, 1000d, 600d), m.Rect(m.Get("a")!)); m.ToggleMaximize("a");
        m.Move("b", 5000, 5000); var b = m.Get("b")!; Assert.True(b.X <= 920 && b.Y <= 600 - WindowManager.TitleBar);
        m.Move("b", -9000, -9000); Assert.True(b.X + b.W >= 80 && b.Y >= 0);
        m.Resize(2000, 1000); Assert.Equal(1800, m.Get("a")!.W); Assert.NotEqual(1800, b.W);
        m.MinimizeAll(); Assert.Null(m.Active);
        m.Close("a"); Assert.Null(m.Get("a"));
        for (var i = 0; i < 10; i++) m.Open("w" + i, "p", "T");
        Assert.Equal(WindowManager.MaxOpen, m.Stack.Count); Assert.Null(m.Get("b"));
    }
}

public class WindowManager_Groesse
{
    [Fact(DisplayName = "Fenster: Größe an Rändern/Ecken ändern mit Mindestgröße; am Rand einrasten auf halbe Fläche")]
    public void ResizeSnap()
    {
        var m = new WindowManager { AreaW = 1000, AreaH = 600 }; var w = m.Open("a", "a", "A").Window;
        w.X = 100; w.Y = 100; w.W = 500; w.H = 300;
        m.ResizeEdge("a", "BR", 50, 40); Assert.Equal((550d, 340d), (w.W, w.H));
        m.ResizeEdge("a", "L", -30, 0); Assert.Equal((70d, 580d), (w.X, w.W));
        m.ResizeEdge("a", "T", 0, 1000); Assert.Equal(WindowManager.MinH, w.H); Assert.Equal(100 + 340 - WindowManager.MinH, w.Y);
        m.ResizeEdge("a", "R", 5000, 0); Assert.Equal(1000 - w.X, w.W);
        m.ToggleMaximize("a"); m.ResizeEdge("a", "B", 0, -10); Assert.False(w.Maximized);
        Assert.Null(m.SnapAt("a", 500));
        Assert.Equal("links", m.SnapAt("a", 3)); Assert.Equal((0d, 0d, 500d, 600d), m.Rect(w));
        Assert.Equal("rechts", m.SnapAt("a", 995)); Assert.Equal((500d, 0d, 500d, 600d), m.Rect(w));
    }
}

public class WindowManager_Behalten
{
    [Fact(DisplayName = "Fenster: Zustand sichern und auf neuer Verwaltung wiederherstellen (Lage, Größe, minimiert, Reihenfolge)")]
    public void Roundtrip()
    {
        var a = new WindowManager { AreaW = 1200, AreaH = 700 };
        a.Open("x", "x", "X"); a.Open("y", "y2", "Y"); a.Open("z", "z", "Z");
        a.Move("x", 40, 30); a.Minimize("z"); a.ToggleMaximize("y"); a.Activate("x");
        var s = a.Snapshot();
        var b = new WindowManager { AreaW = 1200, AreaH = 700 };
        b.Open("z", "z", "Z"); b.Open("x", "x", "X"); b.Open("y", "y2", "Y");
        b.Apply(s);
        Assert.Equal(["x", "y", "z"], b.TaskOrder.Select(w => w.Id)); Assert.Equal(a.Stack.Select(w => w.Id), b.Stack.Select(w => w.Id));
        Assert.Equal((a.Get("x")!.X, a.Get("x")!.Y), (b.Get("x")!.X, b.Get("x")!.Y));
        Assert.True(b.Get("y")!.Maximized); Assert.True(b.Get("z")!.Minimized); Assert.Equal("x", b.Active!.Id);
    }
}
