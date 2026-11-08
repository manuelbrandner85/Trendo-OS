using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 6 – Kalender: „ICS einlesen …“ mit einer fremden Datei (Regel mit Ende) und einer kaputten Datei.</summary>
public class IcsForeignUi
{
    [AvaloniaFact(DisplayName = "Kalender-Oberfläche: fremde .ics mit „6× jeden Montag“ → 6 Termine und Hinweis; kaputte Datei → Meldung statt Absturz")]
    public void Import()
    {
        var (w, _, _) = Ui.Open();
        var dir = Path.Combine(Path.GetTempPath(), "trendo-icsui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var start = DateTime.Today.AddDays(7 - (int)DateTime.Today.DayOfWeek + 1);
            var f = Path.Combine(dir, "fremd.ics");
            File.WriteAllText(f, "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:yoga@example.org\r\nDTSTART:" + start.ToString("yyyyMMdd") + "T180000\r\nDTEND:" + start.ToString("yyyyMMdd")
                + "T190000\r\nSUMMARY:Yoga\r\nRRULE:FREQ=WEEKLY;COUNT=6\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");
            Assert.Equal((6, 0), w.CalendarImportFile(f));
            Assert.Contains("6 Termine", w.PimMessage); Assert.Contains("Wiederholungen mit Ende", w.PimMessage);
            Assert.Equal(6, w.Cal.All().Count(e => e.Title == "Yoga"));
            w.Show("kalender");
            var bad = Path.Combine(dir, "kaputt.ics"); File.WriteAllText(bad, "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART:2026\r\nRRULE:FREQ=WEEKLY;COUNT=2\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");
            var r = w.CalendarImportFile(bad);
            if (r is null) Assert.StartsWith("Kalenderdatei ließ sich nicht einlesen", w.PimMessage);
            Assert.Null(w.CalendarImportFile(Path.Combine(dir, "gibtsnicht.ics")));
            w.Show("kalender");
        }
        finally { w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
