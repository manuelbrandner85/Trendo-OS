using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core.Sys;
using Xunit;
using S = Trendo.Core.Sys.TaskManager.Sample;

namespace Trendo.App.Tests;

/// Runde 7: Leistung als Task-Manager – Kurven, Prozessliste sortieren/suchen, Beenden nur für Trendos eigene Programme (mit Rückfrage), nie fremde, nie Trendo selbst.
public class TaskManager_Ui
{
    [AvaloniaFact(DisplayName = "Task-Manager: Reiter Leistung mit Kurven, Prozesse sortierbar und durchsuchbar, Beenden nur für eigenes Trendo-Werkzeug nach Rückfrage")]
    public void Task_manager()
    {
        var (w, _, root) = Ui.Open();
        var tool = Path.Combine(root, "werkzeuge", "gh-x", "werkzeug.exe");
        var t = TimeSpan.Zero; var killed = new List<int>();
        w.TaskMeasureOverride = () => { t += TimeSpan.FromMilliseconds(1); return [new S(4001, "werkzeug", t, 300 << 20, tool), new S(4002, "browser", t * 3, 900 << 20, @"C:\Programme\Browser\browser.exe"), new S(Environment.ProcessId, "Trendo", t, 200 << 20, Path.Combine(root, "TrendoOS.exe"))]; };
        w.TaskKillOverride = pid => killed.Add(pid);
        w.Show("ressourcen"); w.TaskTick(); w.Show("ressourcen");
        Assert.NotNull(w.TryFind<Border>("res-chart-cpu")); Assert.NotNull(w.TryFind<TextBlock>("res-cpu"));
        w.Find<Button>("res-tab-prozesse").Press();
        // nach Prozessor absteigend: browser zuerst
        Assert.StartsWith("browser", ((TextBlock)w.Find<Grid>("tm-row-0").Children[0]).Text);
        w.Find<Button>("tm-sort-name").Press();
        string Names() => string.Join(", ", Enumerable.Range(0, 3).Select(i => w.TryFind<Grid>($"tm-row-{i}") is { } g ? ((TextBlock)g.Children[0]).Text : "-"));
        Assert.True(((TextBlock)w.Find<Grid>("tm-row-0").Children[0]).Text!.StartsWith("browser"), Names()); // Name aufsteigend: browser, Trendo, werkzeug
        w.Find<Button>("tm-sort-ram").Press();
        Assert.StartsWith("browser", ((TextBlock)w.Find<Grid>("tm-row-0").Children[0]).Text);
        // nur das Trendo-Werkzeug hat „Beenden“ – nicht der Browser, nicht Trendo selbst
        Assert.Null(w.TryFind<Button>("tm-kill-4002")); Assert.Null(w.TryFind<Button>($"tm-kill-{Environment.ProcessId}"));
        Assert.Contains("· Trendo", w.GetLogicalTexts().First(x => x.StartsWith("werkzeug")));
        w.Find<Button>("tm-kill-4001").Press(); Assert.Empty(killed); // erst Rückfrage
        Assert.Equal("Wirklich beenden?", w.Find<Button>("tm-kill-4001").Content);
        w.Find<Button>("tm-kill-4001").Press();
        Assert.Equal([4001], killed); Assert.Contains("beendet", w.Find<TextBlock>("tm-msg").Text);
        Assert.False(w.TaskKill(4002)); Assert.Equal([4001], killed); // fremdes Programm: nie
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "taskmanager.beendet");
        // Suche
        var search = w.Find<TextBox>("tm-search"); search.Text = "werk"; search.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = Avalonia.Input.Key.K });
        Assert.StartsWith("werkzeug", ((TextBlock)w.Find<Grid>("tm-row-0").Children[0]).Text); Assert.Null(w.TryFind<Grid>("tm-row-1"));
        w.Find<Button>("res-tab-speicher").Press(); Assert.NotNull(w.TryFind<Button>("res-refresh"));
    }
}
