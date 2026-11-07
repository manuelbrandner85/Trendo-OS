using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 4 – SPEC-06: FSRS-Planer (FSRS-5, eigener C#-Code nach Open Spaced Repetition, MIT) im Karteikasten.</summary>
public class FsrsTests
{
    static (LearnBox Box, string Root, Action<DateTime> Move) New()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-fsrs-" + Guid.NewGuid().ToString("N"));
        var now = new DateTime(2026, 11, 2, 9, 0, 0);
        return (new LearnBox(root, () => now), root, d => now = d);
    }
    static void Clean(string root) { try { Directory.Delete(root, true); } catch (IOException) { } }

    [Fact(DisplayName = "FSRS +1: Behaltensrate 90 % nach genau einer Stabilität (Vergessenskurve)")]
    public void Retrievability_at_stability()
    {
        Assert.Equal(0.9, Fsrs.Retrievability(10, 10), 6);
        Assert.Equal(1.0, Fsrs.Retrievability(0, 10), 6);
        Assert.True(Fsrs.Retrievability(30, 10) < Fsrs.Retrievability(20, 10));
    }

    [Fact(DisplayName = "FSRS +2: Abstand bei 90 % = Stabilität, bei 80 % länger, bei 95 % kürzer")]
    public void Interval_follows_retention()
    {
        Assert.Equal(10, Fsrs.Interval(10, 0.9));
        Assert.True(Fsrs.Interval(10, 0.8) > 10); Assert.True(Fsrs.Interval(10, 0.95) < 10);
        Assert.Equal(1, Fsrs.Interval(0.1)); Assert.Equal(Fsrs.MaxDays, Fsrs.Interval(1e9));
    }

    [Fact(DisplayName = "FSRS +3: neue Karte – Nochmal 1, Schwer 1, Gut 3, Leicht 16 Tage (Standardgewichte FSRS-5)")]
    public void New_card_intervals()
    {
        Assert.Equal(1, Fsrs.Step(null, null, 0, Grade.Falsch).Days);
        Assert.Equal(1, Fsrs.Step(null, null, 0, Grade.Schwer).Days);
        Assert.Equal(3, Fsrs.Step(null, null, 0, Grade.Gut).Days);
        Assert.Equal(16, Fsrs.Step(null, null, 0, Grade.Leicht).Days);
        Assert.Equal(5.28, Fsrs.Step(null, null, 0, Grade.Gut).D, 2);
    }

    [Fact(DisplayName = "FSRS +4: zweite Wiederholung „Gut“ nach 3 Tagen → etwa 11 Tage (von Hand nachgerechnet)")]
    public void Second_review_hand_checked()
    {
        var first = Fsrs.Step(null, null, 0, Grade.Gut);
        var second = Fsrs.Step(first.S, first.D, 3, Grade.Gut);
        Assert.InRange(second.S, 10.6, 10.9); Assert.Equal(11, second.Days);
        Assert.InRange(second.D, 5.26, 5.29); // „Gut“ ändert die Schwierigkeit kaum
    }

    [Fact(DisplayName = "FSRS +5: Reihenfolge der Abstände Schwer < Gut < Leicht, Schwierigkeit steigt bei Schwer und sinkt bei Leicht")]
    public void Grades_ordered()
    {
        var (s, d, _) = Fsrs.Step(null, null, 0, Grade.Gut);
        var hard = Fsrs.Step(s, d, 3, Grade.Schwer); var good = Fsrs.Step(s, d, 3, Grade.Gut); var easy = Fsrs.Step(s, d, 3, Grade.Leicht);
        Assert.True(hard.Days < good.Days && good.Days < easy.Days, $"{hard.Days} {good.Days} {easy.Days}");
        Assert.True(hard.D > d && easy.D < d);
    }

    [Fact(DisplayName = "FSRS +6: Vergessen senkt die Stabilität nie über den alten Wert, Karte kommt morgen wieder")]
    public void Forget_lowers_stability()
    {
        var r = Fsrs.Step(40, 6, 45, Grade.Falsch);
        Assert.True(r.S < 40); Assert.Equal(1, r.Days); Assert.True(r.D > 6);
        Assert.True(Fsrs.ForgetStability(5, 2, 0.5) <= 2);
    }

    [Fact(DisplayName = "FSRS +7: Karteikasten mit FSRS – Bewertung speichert Stabilität, Abstand und Vorschau stimmen überein")]
    public void Box_uses_fsrs()
    {
        var (box, root, move) = New();
        try
        {
            box.SetScheduler("FSRS"); var c = box.Add("Englisch", "Haus", "house");
            Assert.Equal(16, box.PreviewDays(c.Id, Grade.Leicht)); Assert.Equal(3, box.PreviewDays(c.Id, Grade.Gut));
            box.Answer(c.Id, Grade.Gut);
            Assert.Equal(3, c.IntervalDays); Assert.NotNull(c.Stability); Assert.Equal(new DateTime(2026, 11, 5), c.Due);
            move(new DateTime(2026, 11, 5, 9, 0, 0)); Assert.Equal(11, box.PreviewDays(c.Id, Grade.Gut));
            box.Answer(c.Id, Grade.Gut); Assert.Equal(11, c.IntervalDays); Assert.Equal(2, c.Reps);
        }
        finally { Clean(root); }
    }

    [Fact(DisplayName = "FSRS +8: Planer und Behaltensrate bleiben nach Neustart erhalten")]
    public void Scheduler_persists()
    {
        var (box, root, _) = New();
        try
        {
            box.SetScheduler("FSRS", 0.85); var c = box.Add("A", "1", "eins"); box.Answer(c.Id, Grade.Gut);
            var again = new LearnBox(root, () => new DateTime(2026, 11, 2, 10, 0, 0));
            Assert.True(again.UsesFsrs); Assert.Equal(0.85, again.Store.Retention);
            Assert.Equal(c.Stability, again.Store.Cards[0].Stability);
        }
        finally { Clean(root); }
    }

    [Fact(DisplayName = "FSRS +9: bisherige SM-2-Karte wird übernommen – langer Abstand bleibt lang, kein Neustart bei 1 Tag")]
    public void Sm2_card_migrates()
    {
        var (box, root, move) = New();
        try
        {
            var c = box.Add("A", "Frage", "Antwort");
            box.Answer(c.Id, Grade.Gut); move(new DateTime(2026, 11, 3, 9, 0, 0)); box.Answer(c.Id, Grade.Gut); // SM-2: 1, dann 6 Tage
            Assert.Equal(6, c.IntervalDays); Assert.Null(c.Stability);
            move(new DateTime(2026, 11, 9, 9, 0, 0)); box.SetScheduler("FSRS");
            var days = box.PreviewDays(c.Id, Grade.Gut); Assert.True(days > 6, $"{days}");
            box.Answer(c.Id, Grade.Gut); Assert.NotNull(c.Stability); Assert.True(c.Stability > 6);
        }
        finally { Clean(root); }
    }

    [Fact(DisplayName = "FSRS +10: ohne Umschalten bleibt SM-2 unverändert (1 → 6 Tage)")]
    public void Default_stays_sm2()
    {
        var (box, root, move) = New();
        try
        {
            Assert.False(box.UsesFsrs); var c = box.Add("A", "x", "y");
            box.Answer(c.Id, Grade.Gut); Assert.Equal(1, c.IntervalDays);
            move(new DateTime(2026, 11, 3, 9, 0, 0)); box.Answer(c.Id, Grade.Gut); Assert.Equal(6, c.IntervalDays); Assert.Null(c.Stability);
        }
        finally { Clean(root); }
    }

    [Fact(DisplayName = "FSRS −1: unbekannter Planer oder unsinnige Behaltensrate wird abgelehnt, Einstellung bleibt")]
    public void Refuse_bad_settings()
    {
        var (box, root, _) = New();
        try
        {
            Assert.Throws<ArgumentException>(() => box.SetScheduler("Leitner"));
            Assert.Throws<ArgumentException>(() => box.SetScheduler("FSRS", 0.5));
            Assert.Throws<ArgumentException>(() => box.SetScheduler("FSRS", 0.99));
            Assert.False(box.UsesFsrs); Assert.Equal(0.9, box.Store.Retention);
            Assert.Throws<ArgumentOutOfRangeException>(() => Fsrs.Interval(5, 1.0));
        }
        finally { Clean(root); }
    }

    [Fact(DisplayName = "FSRS −2: Wiederholung am selben Tag springt nicht weit (Kurzzeit-Formel), Stabilität bleibt in Grenzen")]
    public void Same_day_small()
    {
        var (s, d, _) = Fsrs.Step(null, null, 0, Grade.Gut);
        var again = Fsrs.Step(s, d, 0.1, Grade.Gut);
        Assert.InRange(again.S, s, s * 2); Assert.InRange(again.Days, 1, 6);
        var huge = Fsrs.Step(30000, 1, 100000, Grade.Leicht); Assert.Equal(Fsrs.MaxDays, huge.Days);
    }
}
