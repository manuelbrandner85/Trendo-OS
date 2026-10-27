using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Teil 4, Arbeiten & Büro: Termin aus E-Mail (SYS-09 F2), Vorhaben mit Zeitplan und Kanban (X-11).</summary>
public class Teil4BueroTests
{
    static readonly DateTime Tue = new(2026, 10, 27); // Dienstag

    [Fact(DisplayName = "SYS-09 F2: Datum, Monatsname, Wochentag und morgen erkennen – mit Uhrzeit")]
    public void Mail_dates()
    {
        var a = MailDates.Find("Termin am Freitag\nPasst 10 Uhr?", Tue);
        Assert.Equal(new DateTime(2026, 10, 30, 10, 0, 0), Assert.Single(a).Start);
        var b = MailDates.Find("Treffen am 05.11. um 14:30 Uhr im Büro", Tue);
        Assert.Equal(new DateTime(2026, 11, 5, 14, 30, 0), Assert.Single(b).Start);
        var c = MailDates.Find("Die Feier ist am 3. Januar.", Tue);
        Assert.Equal(new DateTime(2027, 1, 3), Assert.Single(c).Start);
        Assert.False(c[0].HasTime);
        Assert.Equal(Tue.AddDays(1), MailDates.Find("Können wir morgen telefonieren?", Tue).Single().Start);
        Assert.Empty(MailDates.Find("Guten Morgen, danke für die Rechnung 12.50 €.", Tue));
        Assert.Equal(new DateTime(2026, 12, 24), MailDates.Find("Bis zum 24.12.2026 bitte.", Tue).Single().Start);
    }

    [Fact(DisplayName = "X-11 F1/U1: Vorhaben mit Startdatum – Fälligkeiten, Meilenstein, Kanban-Zustände, alte Listen lesbar")]
    public void Suite_plan()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-x11-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var d = CreationSuite.Create(root, "event", "Sommerfest", new DateTime(2026, 11, 1));
            var s = CreationSuite.Steps(d);
            Assert.All(s, x => Assert.NotNull(x.Due));
            Assert.Equal(new DateTime(2026, 12, 1), s[^1].Due);
            Assert.True(s[^1].Milestone); Assert.False(s[0].Milestone);
            CreationSuite.SetState(d, s[0].Line, 1);
            Assert.Equal(1, CreationSuite.Steps(d)[0].State);
            Assert.Equal(0, CreationSuite.Percent(d));
            CreationSuite.SetState(d, s[0].Line, 2);
            Assert.True(CreationSuite.Steps(d)[0].Done);
            CreationSuite.Toggle(d, s[0].Line);
            var back = CreationSuite.Steps(d)[0];
            Assert.Equal((false, 0, s[0].Due), (back.Done, back.State, back.Due)); // Datum bleibt beim Abhaken erhalten
            CreationSuite.SetDue(d, s[1].Line, null); CreationSuite.SetMilestone(d, s[1].Line, true);
            Assert.Equal((null, true), (CreationSuite.Steps(d)[1].Due, CreationSuite.Steps(d)[1].Milestone));
            // ohne Datum wie bisher
            var old = CreationSuite.Create(root, "video", "Alt");
            Assert.All(CreationSuite.Steps(old), x => Assert.Null(x.Due));
            File.WriteAllText(Path.Combine(old, "Aufnahmen", "clip.txt"), "x");
            Assert.Single(CreationSuite.Files(old));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
