using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.App.Tests;

public class Sys13_Qualitaet_Ui
{
    [AvaloniaFact(DisplayName = "SYS-13-P13: Jede Seite gehört zu einem Modul aus requirements.yaml")]
    public void Modules()
    {
        var (w, l, root) = Ui.Open();
        Assert.True(w.OrphanPages().Count == 0, string.Join(" ", w.OrphanPages()));
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "requirements.yaml"));
        var unknown = w.PageKeys.Select(k => w.ModuleOf(k)!).Distinct().Where(m => !yaml.Contains("id: " + m)).ToList();
        Assert.Empty(unknown); w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-13-P14: Fehler in einer Seite zeigt freundliche Fehlerseite statt Absturz")]
    public void ErrorPage()
    {
        var (w, l, root) = Ui.Open();
        w.FailNextPage = "werkzeuge"; w.Show("werkzeuge");
        Assert.NotNull(w.TryFind<Border>("error-page")); Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, "logs"), "absturz-*.txt"));
        w.Find<Button>("error-retry").Press(); Assert.Null(w.TryFind<Border>("error-page"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-13-P15/P16: Start < 3 s, Seitenwechsel im Median < 300 ms, 100 Wechsel ohne > 50 MB Zuwachs")]
    public void Performance()
    {
        Ui.Open().W.Close(); // Aufwärmen (JIT)
        var sw = Stopwatch.StartNew(); var (w, l, root) = Ui.Open(); sw.Stop();
        Assert.True(sw.Elapsed < Quality.StartBudget, $"Start {sw.ElapsedMilliseconds} ms");
        string[] keys = ["start", "studios", "projects", "system", "settings", "werkzeuge", "office", "prog-pruefen", "bedien-sehen", "doku-ordnen"];
        w.MeasurePages(keys);
        var m = w.MeasurePages(keys); var med = Quality.Median(m.Select(x => x.Took));
        Assert.True(med < Quality.PageBudget, $"Median {med.TotalMilliseconds} ms");
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); GC.Collect(); GC.WaitForPendingFinalizers(); var before = GC.GetTotalMemory(true);
        // Wie in der echten App wird nach jedem Wechsel gezeichnet; ohne Zeichentakt sammelt der Headless-Renderer nur Aufträge an (kein Leck der App)
        for (var i = 0; i < 100; i++) { w.Show(keys[i % keys.Length]); Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
        GC.Collect(); GC.WaitForPendingFinalizers(); var after = GC.GetTotalMemory(true);
        Assert.False(Quality.Leaks(before, after), $"Zuwachs {(after - before) >> 20} MB");
        w.Show("qual-leistung"); w.Find<Button>("perf-run").Press(); Assert.Contains("Seiten", w.Find<TextBlock>("perf-out").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SYS-13 (UI): Rückfall-Sicherung und vorige Version; Protokoll-Prüfung")]
    public void Back()
    {
        var (w, l, root) = Ui.Open();
        w.Show("qual-rueckfall"); w.Find<Button>("rb-prepare").Press();
        Assert.Single(Directory.GetDirectories(Path.Combine(root, "sicherungen"), "vor-rueckfall-*"));
        w.Find<TextBox>("rb-versions").Text = "0.0.1, 0.0.2"; w.Find<Button>("rb-find").Press();
        Assert.Contains("releases/tag/v0.0.2", w.Find<TextBlock>("rb-out").Text);
        w.Show("qual-leistung"); w.Find<Button>("audit-check").Press();
        w.Close();
    }
}
