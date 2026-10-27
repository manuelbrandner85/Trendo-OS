using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Updates wie bei Windows (U1–U6, 27.10.2026): Zeitpunkt, Startwächter mit Rückkehr, „Was ist neu?“.</summary>
public class UpdatesTests
{
    [Fact(DisplayName = "Updates: sofort / beim nächsten Start / nachts in der gewählten Stunde")]
    public void Schedule()
    {
        var day = new DateTime(2026, 10, 28, 14, 0, 0);
        Assert.True(UpdateSchedule.ApplyNow("sofort", 3, day));
        Assert.False(UpdateSchedule.ApplyNow("start", 3, day));
        Assert.False(UpdateSchedule.ApplyNow("nacht", 3, day));
        Assert.True(UpdateSchedule.ApplyNow("nacht", 3, day.Date.AddHours(3).AddMinutes(20)));
        Assert.False(UpdateSchedule.ApplyNow("nacht", 3, day.Date.AddHours(6)));
    }

    [Fact(DisplayName = "Startwächter: neue Version, die dreimal nicht stabil startet → zurück zur letzten guten; gute Version bleibt")]
    public void Boot_guard()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-upd-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var g = new BootGuard(d);
            Assert.Null(g.Begin("0.9.11")); g.MarkOk("0.9.11");
            Assert.Equal("0.9.11", g.LastGood);
            Assert.Null(g.Begin("0.9.12")); // erster Start der neuen Version
            Assert.Null(g.Begin("0.9.12")); // zweiter, wieder abgestürzt
            Assert.Equal("0.9.11", g.Begin("0.9.12")); // dritter → zurück
            // läuft die neue Version einmal stabil, gibt es keinen Rückweg mehr
            var g2 = new BootGuard(Path.Combine(d, "b"));
            g2.Begin("0.9.11"); g2.MarkOk("0.9.11"); g2.Begin("0.9.12"); g2.MarkOk("0.9.12");
            Assert.Null(g2.Begin("0.9.12")); Assert.Null(g2.Begin("0.9.12")); Assert.Null(g2.Begin("0.9.12"));
            Assert.Equal("0.9.12", g2.LastGood);
        }
        finally { if (Directory.Exists(d)) Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Was ist neu?: Punkte aus den Versionsnotizen, nur nach einem echten Versionswechsel")]
    public void Notes()
    {
        const string md = "## 0.9.13\n\n- **TV** besser\n- Neue Sachen\n\n## 0.9.12\n- Alt\n";
        Assert.Equal(["TV besser", "Neue Sachen"], ReleaseNotes.Points(md, "0.9.13"));
        Assert.Equal(["Alt"], ReleaseNotes.Points(md, "0.9.12"));
        Assert.True(ReleaseNotes.ShowFor("0.9.12", "0.9.13", md));
        Assert.False(ReleaseNotes.ShowFor("0.9.13", "0.9.13", md));
        Assert.False(ReleaseNotes.ShowFor("", "0.9.13", md)); // Erstinstallation: keine „Neu“-Seite
        Assert.False(ReleaseNotes.ShowFor("0.9.12", "0.9.13", null));
    }
}
