using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Lernkarten mit Lernplan: Prüfungstermin und Tagesziel je Stapel, neue Karten im Tempo des Plans, Kalender mit Prüfungstag, Verlauf, Erinnerung.
public class LearnPlanTests
{
    static (LearnBox Box, string Root, Func<DateTime> SetNow, Action<DateTime> Move) New()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-plan-" + Guid.NewGuid().ToString("N"));
        var now = new DateTime(2026, 11, 2, 9, 0, 0);
        var box = new LearnBox(root, () => now);
        return (box, root, () => now, d => now = d);
    }

    [Fact(DisplayName = "Lernplan: 30 neue Karten, Prüfung in 13 Tagen → 3 neue pro Tag (fertig 3 Tage vorher); Tagesplan und Lern-Reihenfolge halten das Tempo")]
    public void Plan_limits_new_cards()
    {
        var (box, root, _, move) = New();
        try
        {
            for (var i = 0; i < 30; i++) box.Add("Englisch", "Wort " + i, "word " + i);
            for (var i = 0; i < 5; i++) box.Add("Mathe", "Aufgabe " + i, "Lösung " + i);
            Assert.Equal(35, box.SessionQueue().Count); // ohne Plan: alles
            var p = box.SetPlan("Englisch", new DateTime(2026, 11, 15), null);
            Assert.Equal((true, 3), (p.Auto, p.NewPerDay));
            var (rev, neu, done, goal, min) = box.Today("Englisch");
            Assert.Equal((0, 3, 0, 3, 1), (rev, neu, done, goal, min));
            var q = box.SessionQueue("Englisch"); Assert.Equal(3, q.Count);
            foreach (var c in q) box.Answer(c.Id, Grade.Gut);
            Assert.Equal((0, 0, 3), (box.Today("Englisch").Reviews, box.Today("Englisch").NewAllowed, box.Today("Englisch").DoneToday));
            Assert.Empty(box.SessionQueue("Englisch")); // Tagesziel geschafft
            move(new DateTime(2026, 11, 3, 9, 0, 0));
            Assert.Equal((3, 3), (box.Today("Englisch").Reviews, box.Today("Englisch").NewAllowed)); // gestern gelernte fällig + 3 neue
            Assert.Equal(6, box.SessionQueue("Englisch").Count); Assert.Equal(1, box.SessionQueue("Englisch")[0].Reps); // Wiederholungen zuerst
            Assert.Equal(5, box.SessionQueue("Mathe").Count); // Stapel ohne Plan unverändert
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Lernplan: feste Zahl pro Tag, Prüfungstermin muss in der Zukunft liegen, unbekannter Stapel abgelehnt, Plan entfernbar")]
    public void Plan_validation()
    {
        var (box, root, _, _) = New();
        try
        {
            box.Add("Geschichte", "1789", "Französische Revolution");
            Assert.Equal((false, 15), (box.SetPlan("Geschichte", null, 15) is var p ? (p.Auto, p.NewPerDay) : default));
            Assert.Throws<ArgumentException>(() => box.SetPlan("Geschichte", new DateTime(2026, 11, 1), null));
            Assert.Throws<ArgumentException>(() => box.SetPlan("Gibtsnicht", null, 5));
            Assert.Throws<ArgumentException>(() => box.SetPlan("Geschichte", null, 900));
            Assert.True(box.RemovePlan("Geschichte")); Assert.Null(box.Plan("Geschichte"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Lernplan: Kalender – neue Karten verteilt, letzte 3 Tage vor der Prüfung nur Wiederholung, Prüfungstag markiert; Verlauf und Erinnerung")]
    public void Calendar_history_reminder()
    {
        var (box, root, _, _) = New();
        try
        {
            for (var i = 0; i < 12; i++) box.Add("Bio", "Frage " + i, "Antwort " + i);
            box.SetPlan("Bio", new DateTime(2026, 11, 9), null); // 7 Tage, 4 Lerntage → 3 pro Tag
            var cal = box.Calendar(10, "Bio");
            Assert.Equal([3, 3, 3, 3, 0, 0, 0, 0, 0, 0], cal.Select(c => c.New));
            Assert.True(cal.Single(c => c.Exam).Day == new DateTime(2026, 11, 9));
            Assert.Contains("Prüfung „Bio“ in 7 Tagen", box.Reminder());
            foreach (var c in box.SessionQueue("Bio")) box.Answer(c.Id, Grade.Gut);
            Assert.Equal(3, box.History(7)[^1].Count); Assert.Equal(0, box.History(7)[0].Count);
            Assert.Null(box.Reminder()); // heute geschafft
            var again = new LearnBox(root, () => new DateTime(2026, 11, 2, 18, 0, 0));
            Assert.Equal(3, again.Store.ReviewLog["2026-11-02"]); Assert.Single(again.Store.Plans); // gespeichert
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
