using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 3 – KI-Team: Zwischenstand und Fortsetzen eines abgebrochenen Team-Laufs (Konzept aus LangGraph, eigener Code im AgentCenter).</summary>
public class AgentResumeTests
{
    static (AgentCenter C, IntelligenceCore Ki, FakeKi Fake, Fixture F) Setup(Func<int, string> reply)
    {
        var f = new Fixture(); f.Core.Projects.Create("p1", "P1"); var n = 0;
        var fake = new FakeKi("A", reply: m => reply(++n));
        return (new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects), new IntelligenceCore(f.Root, f.Core.Audit, () => [fake]), fake, f);
    }

    [Fact(DisplayName = "Fortsetzen +1: Lauf scheitert bei Rolle 2 → fertige Rolle bleibt, Rolle 2 läuft mit dem Zwischenstand neu, dann Rolle 3")]
    public async Task Resume_after_failure()
    {
        var fail = true; var (c, ki, fake, f) = Setup(n => n == 2 && fail ? throw new HttpRequestException("weg") : $"Ergebnis {n}");
        using (f)
        {
            var first = await c.RunTeamAsync("text", "Flyer", ki, "p1");
            Assert.Equal(2, first.Count); Assert.Equal(AgentTaskState.Fehler, first[1].State);
            var run = first[0].TeamId!; Assert.True(c.CanResume(run));
            fail = false; var calls = fake.Calls.Count;
            var r = await c.ResumeTeamAsync(run, ki);
            Assert.Equal(3, r.Count); Assert.All(r, t => Assert.Equal(AgentTaskState.Fertig, t.State));
            Assert.Equal(first[0].Id, r[0].Id); Assert.Equal(first[1].Id, r[1].Id); // keine doppelten Aufgaben
            Assert.Contains("Ergebnis 1", fake.Calls[calls][^1].Content); // Rolle 2 bekommt den Zwischenstand von Rolle 1
            Assert.Equal(2, fake.Calls.Count - calls); // Rolle 1 wird nicht wiederholt
            Assert.False(c.CanResume(run)); Assert.Equal(3, c.RunTasks(run).Count);
        }
    }

    [Fact(DisplayName = "Fortsetzen +2: nach Trendo-Neustart „Unterbrochen“ → Lauf lässt sich fortsetzen")]
    public async Task Resume_after_restart()
    {
        var (c, ki, _, f) = Setup(n => $"E{n}");
        using (f)
        {
            var t = c.Create("planer", "Flyer", "p1", null, "text-ab12cd"); t.State = AgentTaskState.Laeuft;
            await c.RunAsync(c.Create("texter", "Flyer", "p1", "x", "dummy-zz"), ki); // speichert auch t mit „läuft“
            var c2 = new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects); // Neustart
            Assert.Equal(AgentTaskState.Fehler, c2.Get(t.Id)!.State); Assert.True(c2.CanResume("text-ab12cd"));
            var r = await c2.ResumeTeamAsync("text-ab12cd", ki);
            Assert.Equal(3, r.Count); Assert.All(r, x => Assert.Equal(AgentTaskState.Fertig, x.State));
        }
    }

    [Fact(DisplayName = "Fortsetzen −1: fertiger, unbekannter oder gerade laufender Lauf wird abgelehnt")]
    public async Task Refuse()
    {
        var (c, ki, _, f) = Setup(n => $"E{n}");
        using (f)
        {
            var r = await c.RunTeamAsync("text", "Flyer", ki, "p1");
            Assert.False(c.CanResume(r[0].TeamId!)); await Assert.ThrowsAsync<InvalidOperationException>(() => c.ResumeTeamAsync(r[0].TeamId!, ki));
            Assert.False(c.CanResume("gibtsnicht-123")); Assert.False(c.CanResume("ohne"));
            var t = c.Create("planer", "x", "p1", null, "code-zz99"); t.State = AgentTaskState.Laeuft; Assert.False(c.CanResume("code-zz99"));
        }
    }
}
