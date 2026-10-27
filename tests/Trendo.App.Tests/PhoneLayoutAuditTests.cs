using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 27.10.2026: „jeder Bereich auf dem Handy muss sauber dargestellt sein“. Hier: jede App und jeder Systembereich lässt sich
/// auf schmalen und großen Handys aufbauen und umbrechen, ohne Fehlerseite. Die Rand-Prüfung braucht echte Schrift und läuft mit dem Render-Werkzeug:
/// TRENDO_PHONE_AUDIT_ALL=1 dotnet run --project tools/Trendo.Render -c Release -- audit.txt (Stand 27.10.2026: 0 Stellen bei 360 und 430 Pixeln).</summary>
public class PhoneLayoutAuditTests
{
    [AvaloniaFact(DisplayName = "Handy: jede App und jeder Bereich baut sich auf schmalen und großen Handys fehlerfrei auf")]
    public void Every_area_builds()
    {
        var (w, _, root) = Ui.Open();
        var failed = new List<string>();
        foreach (var app in w.PhoneApps())
        {
            var page = app.Id.StartsWith("page:") ? app.Id[5..] : w.StudioLaunchPage(app.Id);
            foreach (var width in new[] { 360, 430 })
            {
                w.PresentPhoneForTests(page);
                w.PhoneScreen(width, 780, 1, false);
                if (w.TryFind<Avalonia.Controls.Control>("error-page") is not null) failed.Add($"{app.Name} [{page}] @{width}");
            }
        }
        Assert.True(failed.Count == 0, "Fehlerseite auf dem Handy: " + string.Join(", ", failed));
        Assert.Empty(CrashLog.List(root));
        w.Close();
    }
}
