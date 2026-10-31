using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0245: Die Fortschrittsansicht zeigt getrennt, wie viele „getestet“-Einträge einen geprüften Beleg haben.</summary>
public class ProgressEvidenceUiTests
{
    [AvaloniaFact(DisplayName = "T-0245 UI: Fortschritt zählt Belege getrennt (geprüft, erfasst, ohne Prüfsumme, ohne Beleg, verändert)")]
    public void Evidence_counts_shown()
    {
        var (w, _, _) = Ui.Open();
        w.Show(PageKey); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var t = w.Find<TextBlock>("progress-evidence").Text ?? "";
        if (Environment.GetEnvironmentVariable("TRENDO_SHOW_EVIDENCE") is "1") Assert.Fail(t); // nur zum Ablesen der Zahlen
        Assert.StartsWith("Belege zu ", t);
        foreach (var part in new[] { "geprüft", "erfasst", "ohne Prüfsumme", "ohne Beleg", "fehlend oder verändert" }) Assert.Contains(part, t);
        Assert.Contains(" 0 fehlend oder verändert", t); // nach dem Eintragen der Prüfsummen ist kein Beleg verändert
        w.Close();
    }

    const string PageKey = "fortschritt";
}
