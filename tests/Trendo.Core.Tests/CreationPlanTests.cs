using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0308: Vorhaben als Zeitplan – Beginn/Ende, Abhängigkeiten, Nachrücken, kritischer Pfad, Kreise abgewiesen, altes Format bleibt lesbar.
public class CreationPlanTests
{
    static string Make(string root, string text)
    {
        var d = Path.Combine(root, "Plan"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, CreationSuite.ListFile), "# Plan\n\n" + text); return d;
    }
    static DateTime D(int m, int d) => new(2026, m, d);

    [Fact(DisplayName = "T-0308: Format – altes „@Ende“ bleibt, „@Beginn..Ende“ und „^Vorgänger“ werden gelesen und geschrieben")]
    public void Format()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-plan-" + Guid.NewGuid().ToString("N"));
        try
        {
            var d = Make(root, "- [ ] Planen @2026-11-03\n- [~] Bauen @2026-11-04..2026-11-10 ^1\n- [x] ◆ Fertig @2026-11-12 ^1,2\n");
            var s = CreationSuite.Steps(d);
            Assert.Equal((null, D(11, 3), (int[]?)null), (s[0].Start, s[0].Due, s[0].After));
            Assert.Equal((D(11, 4), D(11, 10), 1), (s[1].Start!.Value, s[1].Due!.Value, s[1].State)); Assert.Equal([1], s[1].After!);
            Assert.Equal((true, true), (s[2].Milestone, s[2].Done)); Assert.Equal([1, 2], s[2].After!);
            CreationSuite.Toggle(d, s[1].Line); // Schreiben erhält Beginn und Abhängigkeit
            Assert.Contains("- [x] Bauen @2026-11-04..2026-11-10 ^1", File.ReadAllText(Path.Combine(d, CreationSuite.ListFile)));
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0308: Balken, Verschieben mit Nachrücken der Nachfolger, Dauer ändern (mind. 1 Tag), Kreise abgewiesen, kritischer Pfad mit Puffer")]
    public void Schedule()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-plan-" + Guid.NewGuid().ToString("N"));
        try
        {
            var d = Make(root, "- [ ] A @2026-11-02..2026-11-04\n- [ ] B @2026-11-05..2026-11-06 ^1\n- [ ] C @2026-11-05..2026-11-05 ^1\n- [ ] ◆ D @2026-11-08 ^2,3\n- [ ] E @2026-11-10\n");
            var bars = CreationSuite.Bars(CreationSuite.Steps(d));
            Assert.Equal([3, 2, 1, 1, 2], bars.Select(b => b.Days)); // E ohne Beginn: Tag nach dem vorigen Ende (8.) → 9.–10.
            // kritisch: A→B→D…, E endet zuletzt (Projektende 10.); C hat Puffer
            var c = bars.Single(b => b.No == 3); Assert.True(c.Slack > 0); Assert.False(c.Critical);
            Assert.True(bars.Single(b => b.No == 5).Critical);
            Assert.Equal(bars.Single(b => b.No == 2).Slack + 1, c.Slack); // C ist einen Tag kürzer als B
            // A um 2 Tage verschieben → B und C rücken nach, D auch
            var moved = CreationSuite.MoveBy(d, 1, 2);
            var after = CreationSuite.Bars(CreationSuite.Steps(d)).ToDictionary(b => b.No);
            Assert.Equal((D(11, 4), D(11, 6)), (after[1].Start, after[1].End));
            Assert.Equal(D(11, 7), after[2].Start); Assert.Equal(D(11, 7), after[3].Start); Assert.True(after[4].Start > after[2].End);
            Assert.Equal(3, moved);
            // Dauer ändern: Ende −10 Tage → bleibt 1 Tag
            CreationSuite.ResizeBy(d, 2, -10); Assert.Equal(1, CreationSuite.Bars(CreationSuite.Steps(d)).Single(b => b.No == 2).Days);
            CreationSuite.ResizeBy(d, 2, 3); Assert.Equal(4, CreationSuite.Bars(CreationSuite.Steps(d)).Single(b => b.No == 2).Days);
            // Kreis: A nach D geht nicht (D hängt über B an A)
            Assert.Throws<InvalidOperationException>(() => CreationSuite.SetAfter(d, 1, [4]));
            Assert.Throws<ArgumentException>(() => CreationSuite.SetAfter(d, 1, [9]));
            CreationSuite.AddAfter(d, 5, 4); Assert.Equal([4], CreationSuite.Steps(d)[4].After!);
            CreationSuite.SetAfter(d, 5, []); Assert.Null(CreationSuite.Steps(d)[4].After);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
