using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Vorschlag 32: Wochenrückblick im Wissen.</summary>
public class WeekReviewTests
{
    [Fact(DisplayName = "Wochenrückblick: neue Seiten der letzten 7 Tage, offene Verweise, Lerntage, schwierige Karten, Themen mit Umlauten, Markdown mit Verweisen")]
    public void Build()
    {
        var today = new DateTime(2026, 11, 4);
        DateTimeOffset At(int daysAgo) => new(today.AddDays(-daysAgo).AddHours(10));
        var items = new List<KnowledgeItem>
        {
            new("a", "Photosynthese", "Pflanzen wandeln Licht um. Siehe [[Chlorophyll]] und [[Zellatmung]]. Pflanzen brauchen Wasser.", ["biologie"], "eigene Notiz", At(1)),
            new("b", "Zellatmung", "Mitochondrien und Pflanzen", ["biologie"], "eigene Notiz", At(3)),
            new("c", "Alte Seite", "von früher", [], "eigene Notiz", At(20)),
            new("d", "Frage", "Antwort", [], "Rückmeldung", At(1)),
        };
        var cards = new CardStore();
        cards.Cards.Add(new Flashcard { Front = "Was ist ATP?", Deck = "Bio", Lapses = 3, Reps = 4, Ease = 1.8, Due = today.AddDays(-1), FirstReviewed = today.AddDays(-2) });
        cards.Cards.Add(new Flashcard { Front = "Hauptstadt?", Deck = "Erdkunde", Reps = 2, Ease = 2.6, Due = today.AddDays(5), FirstReviewed = today.AddDays(-30) });
        cards.ReviewLog["2026-11-04"] = 12; cards.ReviewLog["2026-11-01"] = 5; cards.ReviewLog["2026-10-20"] = 99;
        var r = WeekReview.Build(items, cards, today);
        Assert.Equal(new DateTime(2026, 10, 29), r.From); Assert.Equal(45, r.Week);
        Assert.Equal(["Photosynthese", "Zellatmung"], r.NewPages.Select(p => p.Title));
        Assert.Equal(["Chlorophyll"], r.OpenLinks); // Zellatmung gibt es schon
        Assert.Equal(17, r.Reviews); Assert.Equal(2, r.ActiveDays); Assert.Equal(1, r.FirstTimeCards); Assert.Equal(1, r.DueNow);
        Assert.Single(r.Hard); Assert.Contains("biologie", r.Topics); Assert.Contains("Pflanzen", r.Topics, StringComparer.OrdinalIgnoreCase);
        var md = WeekReview.Markdown(r, "Viel über Pflanzen gelernt.");
        Assert.Contains("# Wochenrückblick KW 45", md); Assert.Contains("- [[Photosynthese]]", md); Assert.Contains("[[Chlorophyll]] – Seite fehlt noch", md);
        Assert.Contains("17 Karten wiederholt an 2 von 7 Tagen", md); Assert.Contains("Was ist ATP?", md); Assert.Contains("Viel über Pflanzen gelernt.", md);
        Assert.Contains("erfinde nichts", WeekReview.Prompt(r));
        Assert.True(WeekReview.Build([], new CardStore(), today).Empty);
    }
}
