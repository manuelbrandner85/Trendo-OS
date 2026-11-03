using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// Rückmeldung 03.11.2026 „Trendo schließt sich einfach“: ein abgefangener Fehler wird kurz gemeldet, das Fenster bleibt offen.
public class CrashGuard_Ui
{
    [AvaloniaFact(DisplayName = "Absturzschutz: abgefangener Oberflächenfehler zeigt einen Hinweis „Trendo läuft weiter“ – nur einmal bei Folgefehlern, Fenster bleibt bedienbar")]
    public void Ui_error_shows_hint()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            Program.RaiseUiError(new InvalidOperationException("Test"));
            for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Trendo läuft weiter"));
            Program.RaiseUiError(new InvalidOperationException("Folgefehler"));
            for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Single(w.GetLogicalTexts(), t => t.Contains("Trendo läuft weiter"));
            w.Show("settings"); Assert.Equal("settings", w.CurrentView);
        }
        finally { w.Close(); }
    }
}
