using Trendo.Core.Assist;
using Trendo.Core.Media;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>Masterprompt 131 / KI-Unterstützung: Vorschlagsregeln – nachvollziehbar und ohne Nebenwirkung.</summary>
public class SuggestTests
{
    static readonly DateTime Mo = new(2026, 11, 2, 8, 10, 0); // Montag

    [Fact(DisplayName = "Vorschläge: freie Zeit nur werktags 9–18 Uhr, nach belegten Terminen, nicht über den Feierabend")]
    public void Free_slot()
    {
        var busy = new List<(DateTime, DateTime)> { (Mo.Date.AddHours(9), Mo.Date.AddHours(10.5)), (Mo.Date.AddHours(10.5), Mo.Date.AddHours(17.5)) };
        Assert.Equal(Mo.Date.AddDays(1).AddHours(9), Suggest.FreeSlot(busy, Mo, TimeSpan.FromHours(1))); // Montag voll → Dienstag 9 Uhr
        Assert.Equal(Mo.Date.AddHours(17.5), Suggest.FreeSlot(busy, Mo, TimeSpan.FromMinutes(30)));
        var fri = new DateTime(2026, 11, 6, 17, 40, 0); Assert.Equal(new DateTime(2026, 11, 9, 9, 0, 0), Suggest.FreeSlot([], fri, TimeSpan.FromHours(1))); // Wochenende übersprungen
    }

    [Fact(DisplayName = "Vorschläge: Arbeitsblöcke für fällige Aufgaben (nicht doppelt), Aufgaben aus E-Mail-Betreffen mit Frist oder Bitte")]
    public void Tasks_and_mail()
    {
        var todos = new List<TodoItem> { new("a", "Steuer abgeben", Mo.Date.AddDays(3), Priority: 1), new("b", "Irgendwann", Mo.Date.AddDays(30)), new("c", "Erledigt", Mo.Date, Done: true) };
        var blocks = Suggest.TaskBlocks(todos, [], Mo);
        var one = Assert.Single(blocks); Assert.Equal("a", one.Todo.Uid); Assert.Equal(TimeSpan.FromMinutes(90), one.End - one.Start); Assert.Equal(Mo.Date.AddHours(9), one.Start);
        var planned = new List<(CalEvent, DateTime, DateTime)> { (new CalEvent("x", one.Start, one.End, "Arbeiten an: Steuer abgeben", Notes: "aufgabe:a"), one.Start, one.End) };
        Assert.Empty(Suggest.TaskBlocks(todos, planned, Mo));
        var mails = new[] { (1L, "Vermietung Muster <info@example.de>", "Nebenkostenabrechnung – Widerspruch bis 15.11. möglich"), (2L, "Schule", "Bitte Elternbrief unterschreiben"), (3L, "Newsletter", "Neue Angebote"), (4L, "Amt", "Re: Steuer abgeben") };
        var t = Suggest.TasksFromMail(mails, todos, Mo);
        Assert.Equal(2, t.Count); Assert.Equal(new DateTime(2026, 11, 15), t[0].Due); Assert.Contains("Vermietung Muster", t[0].S.Detail); Assert.Null(t[1].Due);
    }

    [Fact(DisplayName = "Vorschläge: Ablauf für volle Downloads; Stille aus FFmpeg-Ausgabe; Stille herausschneiden teilt das Stück")]
    public void Workflows_and_silence()
    {
        var home = Path.Combine(Path.GetTempPath(), "trendo-sg-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(home, "Downloads"));
            for (var i = 0; i < 6; i++) File.WriteAllText(Path.Combine(home, "Downloads", $"r{i}.pdf"), "x");
            Assert.Equal("pdf", Assert.Single(Suggest.Workflows(home, [])).Template);
            Assert.Empty(Suggest.Workflows(home, ["pdf"]));
        }
        finally { Directory.Delete(home, true); }
        var s = Suggest.Silences("[silencedetect @ 0x1] silence_start: 2.5\n[silencedetect @ 0x1] silence_end: 4 | silence_duration: 1.5\nsilence_start: 7.25\nsilence_end: 9.0 | x");
        Assert.Equal([(2.5, 4.0), (7.25, 9.0)], s);
        var p = VideoEdit.NewProject(); VideoEdit.Add(p, new EditClip("v", "v.mp4", 0, 10, 10));
        Assert.Equal(2, VideoEdit.CutOut(p, 0, s));
        var c = VideoEdit.Clips(p); Assert.Equal(3, c.Count); Assert.Equal(10 - 1.5 - 1.75, VideoEdit.Duration(p), 3);
        Assert.Equal((0.0, 2.5, 4.0, 7.25, 9.0, 10.0), (c[0].In, c[0].Out, c[1].In, c[1].Out, c[2].In, c[2].Out));
    }
}
