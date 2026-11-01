using Trendo.Core.Sys;
using Xunit;
using S = Trendo.Core.Sys.TaskManager.Sample;

namespace Trendo.Core.Tests;

/// Task-Manager: Prozessorlast je Prozess aus zwei Messungen, eigene Programme erkennen, sortieren/suchen, Verlauf fester Länge.
public class TaskManagerTests
{
    static readonly string[] Own = [@"C:\Users\nutzer\AppData\Local\TrendoWerkzeuge", @"C:\Users\nutzer\AppData\Roaming\TrendoOS"];

    [Fact(DisplayName = "Task-Manager: Prozessorlast = Prozessorzeit / (Zeit × Kerne); neue oder wiederverwendete Kennung zählt 0 %; Werte zwischen 0 und 100")]
    public void Cpu_from_two_samples()
    {
        var before = new[] { new S(10, "editor", TimeSpan.FromSeconds(5), 100, null), new S(11, "spiel", TimeSpan.FromSeconds(1), 200, null), new S(12, "alt", TimeSpan.FromSeconds(9), 1, null) };
        var now = new[] { new S(10, "editor", TimeSpan.FromSeconds(6), 120, null), new S(11, "spiel", TimeSpan.FromSeconds(9), 300, null), new S(12, "neu", TimeSpan.FromSeconds(30), 1, null), new S(13, "frisch", TimeSpan.FromSeconds(4), 1, null) };
        var rows = TaskManager.Rows(before, now, 2, 4, Own).ToDictionary(r => r.Pid);
        Assert.Equal(12.5, rows[10].CpuPercent); // 1 s / (2 s × 4 Kerne)
        Assert.Equal(100, rows[11].CpuPercent);  // 8 s / 8 s
        Assert.Equal(0, rows[12].CpuPercent);    // Kennung von anderem Programm wiederverwendet
        Assert.Equal(0, rows[13].CpuPercent);
        Assert.Equal(120, rows[10].Bytes);
    }

    [Fact(DisplayName = "Task-Manager: nur Programme aus Trendos eigenen Ordnern gelten als eigene (beendbar) – ganze Ordner, Groß-/Kleinschreibung egal, ohne Pfad nie")]
    public void Own_programs()
    {
        Assert.True(TaskManager.IsOwn(@"c:\users\nutzer\appdata\local\trendowerkzeuge\gh-x\tool.exe", Own));
        Assert.True(TaskManager.IsOwn(@"C:\Users\nutzer\AppData\Local\TrendoWerkzeuge\laufzeiten\node-26\node.exe", Own));
        Assert.False(TaskManager.IsOwn(@"C:\Users\nutzer\AppData\Local\TrendoWerkzeugeFremd\x.exe", Own)); // nur ganze Ordner
        Assert.False(TaskManager.IsOwn(@"C:\Windows\explorer.exe", Own));
        Assert.False(TaskManager.IsOwn(null, Own)); Assert.False(TaskManager.IsOwn("", Own));
        Assert.False(TaskManager.IsOwn(@"C:\Windows\x.exe", ["", " "]));
    }

    [Fact(DisplayName = "Task-Manager: sortieren nach Prozessor/Speicher/Name/Kennung, suchen nach Name oder Kennung; Verlauf behält nur die letzten Werte")]
    public void Sort_search_history()
    {
        var rows = new[] { new TaskManager.Proc(3, "Beta", 5, 900, null, false), new TaskManager.Proc(1, "alpha", 50, 100, null, false), new TaskManager.Proc(2, "Gamma", 5, 500, null, true) };
        Assert.Equal([1, 2, 3], TaskManager.Sort(rows, "cpu", true).Select(r => r.Pid));
        Assert.Equal([3, 2, 1], TaskManager.Sort(rows, "ram", true).Select(r => r.Pid));
        Assert.Equal(["alpha", "Beta", "Gamma"], TaskManager.Sort(rows, "name", false).Select(r => r.Name));
        Assert.Equal([2], TaskManager.Sort(rows, "pid", false, "gam").Select(r => r.Pid));
        Assert.Equal([3], TaskManager.Sort(rows, "cpu", true, "3").Select(r => r.Pid));
        var h = new History(3); foreach (var v in new double?[] { 10, 20, null, 40 }) h.Add(v);
        Assert.Equal([20, 0, 40], h.Values); Assert.Equal(40, h.Last); Assert.Equal(20, h.Average); Assert.Equal(40, h.Max);
        Assert.Contains(TaskManager.Measure(), s => s.Pid == Environment.ProcessId); // echte Messung findet sich selbst
    }
}
