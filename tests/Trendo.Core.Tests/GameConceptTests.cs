using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0610 D1/D2: Spielideen mit und ohne KI, Bewertungen, geführter Weg.</summary>
public class GameConceptTests
{
    [Fact(DisplayName = "Ideen: Genre und Stil stehen in mindestens drei Ideen, auch ohne KI")]
    public void Local_respects_options()
    {
        var ideas = GameConcept.Local(new GameConcept.Options(Genre: "Puzzle", Style: "Comic"), 1);
        Assert.True(ideas.Count >= 3);
        Assert.All(ideas, i => { Assert.Equal("Puzzle", i.Genre); Assert.Equal("Comic", i.Style); Assert.NotEmpty(i.Pitch); });
    }

    [Fact(DisplayName = "Ideen: ein abgelehntes Thema kommt nicht wieder, ein gemochtes kommt zuerst")]
    public void Ratings_change_result()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var ideas = GameConcept.Local(new GameConcept.Options(), seed, liked: ["katzen"], disliked: ["moor"]);
            Assert.Equal("katzen", ideas[0].Theme);
            Assert.DoesNotContain(ideas, i => i.Theme == "moor");
        }
    }

    [Fact(DisplayName = "Ideen: Stimmung wählt passende Themen; sind sie abgelehnt, bleiben trotzdem drei Ideen")]
    public void Mood_filter()
    {
        Assert.All(GameConcept.Local(new GameConcept.Options(Mood: "lustig"), 3), i => Assert.Contains(i.Theme, new[] { "putzroboter", "katzen", "imbiss" }));
        Assert.True(GameConcept.Local(new GameConcept.Options(Mood: "lustig"), 3, disliked: ["putzroboter", "katzen", "imbiss"]).Count >= 3);
    }

    [Fact(DisplayName = "Ideen: neu würfeln ohne KI zeigt Ungesehenes, auch wenn die Stimmung nur drei Themen hat; Gemochtes darf wiederkommen")]
    public void Reroll_shows_new()
    {
        var o = new GameConcept.Options(Mood: "lustig");
        var first = GameConcept.Local(o, 1).Select(i => i.Theme).ToList();
        var second = GameConcept.Local(o, 2, seen: first);
        Assert.True(second.Count >= 3);
        Assert.DoesNotContain(second, i => first.Contains(i.Theme));
        Assert.Contains(GameConcept.Local(o, 3, liked: ["katzen"], seen: first), i => i.Theme == "katzen");
        var all = GameConcept.Local(new GameConcept.Options(), 4, count: 12).Select(i => i.Theme).ToList();
        Assert.Equal(12, all.Count);
        Assert.True(GameConcept.Local(o, 5, seen: all).Count >= 3); // alles gesehen: trotzdem Ideen
    }

    [Fact(DisplayName = "Ideen: KI-Antwort wird gelesen, Einstellungen gelten, Abgelehntes und Unbrauchbares fällt weg")]
    public void Parse_answer()
    {
        const string answer = """
            Hier sind Ideen:
            ```json
            [{"title":"Eins","pitch":"Text eins","loop":"A → B","theme":"wald"},
             {"title":"Zwei","pitch":"Text zwei","theme":"meer"},
             {"title":"","pitch":"ohne Titel"},
             {"title":"Eins","pitch":"doppelt"}]
            ```
            """;
        var ideas = GameConcept.Parse(answer, new GameConcept.Options(Genre: "Horror", Style: "Comic"), disliked: ["meer"]);
        var idea = Assert.Single(ideas);
        Assert.Equal("Eins", idea.Title); Assert.Equal("Horror", idea.Genre); Assert.Equal("Comic", idea.Style); Assert.Equal("A → B", idea.Loop);
    }

    [Theory(DisplayName = "Ideen: unbrauchbare KI-Antworten ergeben keine Ideen und keinen Fehler")]
    [InlineData("Leider keine Ideen.")]
    [InlineData("```json\n[{\"title\":\n```")]
    [InlineData("[1, 2, 3]")]
    public void Parse_bad(string answer) => Assert.Empty(GameConcept.Parse(answer, new GameConcept.Options()));

    [Fact(DisplayName = "Geführter Weg: Antworten werden zu Einstellungen, die Idee zu einer gültigen Spielbibel")]
    public void Path_to_bible()
    {
        var answers = GameConcept.Path.ToDictionary(s => s.Id, s => s.Options[0].Value);
        var o = GameConcept.PathOptions(answers);
        Assert.Equal("entspannt", o.Mood); Assert.Equal("retro", o.Scale); Assert.Equal("Browser", o.Platform);
        var idea = GameConcept.Local(o, 5)[0];
        var bible = GameConcept.ToBible(idea);
        Assert.Equal(idea.Title, (string?)bible["title"]); Assert.Equal("retro", (string?)bible["scale"]); Assert.Equal(idea.Pitch, (string?)bible["logline"]);
        Assert.NotEmpty((string?)bible["browser"] ?? ""); Assert.NotEmpty(bible["milestones"]!.AsArray());
        Assert.Contains("Keine Schrift", GameConcept.ImagePrompt(idea));
    }
}
