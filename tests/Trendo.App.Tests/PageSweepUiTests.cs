using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Prüfung 04.11.2026 (Wunsch „alles muss highend laufen“): jede Seite von Trendo öffnen – keine darf abstürzen; Aufbauzeit kalt und warm messen.
/// Der Bericht landet in %TEMP%\trendo-seiten-pruefung.txt (nicht im Repo).</summary>
public class PageSweep_Ui(ITestOutputHelper log)
{
    [AvaloniaFact(DisplayName = "Seiten-Prüflauf: jede Seite öffnet ohne Absturz; Aufbauzeiten werden gemessen und berichtet")]
    public void Every_page_opens()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var rows = new List<(string Key, double Cold, double Warm, string? Error, string Parts)>();
        try
        {
            foreach (var key in w.PageKeys.OrderBy(k => k).ToList())
            {
                double cold = 0, warm = 0; string? err = null; var parts = "";
                // wie im echten Betrieb mit wenigen offenen Fenstern: ältere schließen, die drei jüngsten bleiben
                foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
                Dispatcher.UIThread.RunJobs();
                try
                {
                    var sw = Stopwatch.StartNew(); w.Show(key); var showCold = sw.Elapsed.TotalMilliseconds; Dispatcher.UIThread.RunJobs(); cold = sw.Elapsed.TotalMilliseconds;
                    parts = $"Seite {StartupTrace.Of("page.present." + key):0} · danach {StartupTrace.Of("page.after." + key):0} · Projekte {StartupTrace.Of("page.projects"):0} · Leiste {StartupTrace.Of("page.tabs"):0} · Zeichnen {cold - showCold:0}";
                    sw.Restart(); w.Show(key); Dispatcher.UIThread.RunJobs(); warm = sw.Elapsed.TotalMilliseconds;
                }
                catch (Exception e) { err = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
                rows.Add((key, cold, warm, err, parts));
            }
        }
        finally { w.Close(); }
        var report = string.Join("\n", rows.OrderByDescending(r => r.Error is null ? r.Cold : 1e9).Select(r => $"{r.Key,-24} kalt {r.Cold,6:0} ms  warm {r.Warm,5:0} ms  [{r.Parts}]  {r.Error}"));
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "trendo-seiten-pruefung.txt"), $"{rows.Count} Seiten\n" + report);
        log.WriteLine(report);
        Assert.True(rows.Count > 40, $"nur {rows.Count} Seiten gefunden");
        Assert.Empty(rows.Where(r => r.Error is not null).Select(r => $"{r.Key}: {r.Error}"));
    }
}
