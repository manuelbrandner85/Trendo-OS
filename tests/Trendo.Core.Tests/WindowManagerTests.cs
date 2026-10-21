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
