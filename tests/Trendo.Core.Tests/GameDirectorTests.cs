using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Spiele-Prompt: Spiel aus einem Satz (Spielbibel aus der KI-Antwort, geprüft und vervollständigt), Rückfragen als Knöpfe, Vorschläge, Ändern mit Vorschau.</summary>
public class GameDirectorTests
{
    const string Answer = """
        Hier ist dein Spiel.
        ```json
        {"title":"Funkenwald","scale":"aa","genre":"Action-Adventure","logline":"Eine Försterin rettet den Wald vor einem Feuerdämon.","loop":"Erkunden → Kämpfen → Retten",
         "mechanics":["Schwertkampf","Klettern","Löschen"],"characters":[{"name":"Mira","role":"Held","text":"Försterin","phases":0},{"name":"Glutherz","role":"Boss","text":"Feuerdämon","phases":3},{"name":"Kater","role":"Quatsch","text":"Begleiter"}],
         "world":[{"name":"Lichtung","text":"Start"}],"quests":[{"title":"Erster Funke","kind":"Haupt","goal":"Feuer löschen","needs":"","gives":"Eimer","giver":"Mira"}],
         "weapon":"laser","abilities":["sprint","fliegen"],"unbekannt":"weg"}
        ```
        Frage: Wie soll das Spiel aussehen? [Fotorealistisch | Comic | Pixel]
        Frage: Für wen? [Kinder | Erwachsene]
        - Vorschlag: Ein zweiter Boss in der Mitte der Geschichte.
        Vorschlag: Tag-Nacht-Wechsel für Stimmung.
        """;

    [Fact(DisplayName = "Spiel aus einem Satz: Spielbibel aus der KI-Antwort, ungültige Werte abgesichert, Unreal-Welt gewählt, Meilensteine ergänzt")]
    public void Parse_new_game()
    {
        var r = GameDirector.Parse(Answer);
        Assert.Null(r.Error); var d = r.Doc!;
        Assert.Equal("Funkenwald", d["title"]!.ToString()); Assert.Equal("aa", d["scale"]!.ToString());
        Assert.Equal("Nebenfigur", d["characters"]![2]!["role"]!.ToString()); // unbekannte Rolle abgesichert
        Assert.Equal("", d["weapon"]!.ToString()); Assert.Single(d["abilities"]!.AsArray()); // nur bekannte Fähigkeiten
        Assert.Null(d["unbekannt"]); Assert.Equal("level", d["world3d"]!.ToString()); Assert.True(d["milestones"]!.AsArray().Count > 3);
        Assert.Equal(2, r.Questions.Count); Assert.Equal(["Fotorealistisch", "Comic", "Pixel"], r.Questions[0].Options);
        Assert.Equal(2, r.Suggestions.Count); Assert.StartsWith("Ein zweiter Boss", r.Suggestions[0]);
    }

    [Fact(DisplayName = "Browser-Spiel: Spielart und Level werden ergänzt, falsche Level-Zeichen ersetzt; AAA/Open World wählt die offene Welt")]
    public void Normalize_scales()
    {
        var retro = GameDirector.Normalize(new JsonObject { ["title"] = "Hüpfer", ["scale"] = "retro", ["genre"] = "Jump'n'Run", ["level"] = new JsonArray("S..xx..Z") });
        Assert.Equal("jump", retro["browser"]!.ToString()); Assert.Equal(GameStudio.DefaultJump, retro["level"]!.AsArray().Select(x => x!.ToString()));
        var open = GameDirector.Normalize(new JsonObject { ["genre"] = "Open World RPG" });
        Assert.Equal("aaa", open["scale"]!.ToString()); Assert.Equal("openworld", open["world3d"]!.ToString()); Assert.Equal("Neues Spiel", open["title"]!.ToString());
        Assert.Equal("openworld", UnrealGame.WorldOf(open)); // Bauen nutzt die Wahl
        // Gewalt-Stufe: ausdrücklich gewählt, sonst nach Zielgruppe (Kinder → keine), Unsinn → normal
        Assert.Equal("keine", GameDirector.Normalize(new JsonObject { ["audience"] = "Kinder ab 6" })["violence"]!.ToString());
        Assert.Equal("brutal", GameDirector.Normalize(new JsonObject { ["violence"] = "brutal", ["audience"] = "Erwachsene" })["violence"]!.ToString());
        Assert.Equal("normal", GameDirector.Normalize(new JsonObject { ["violence"] = "extrem" })["violence"]!.ToString());
        Assert.Contains("violence", GameDirector.Instructions(false)); Assert.Contains("Geländewagen", GameDirector.Instructions(false)); // passend zur Welt
    }

    [Fact(DisplayName = "Ändern per Satz: weggelassene Felder bleiben, Vorschau zeigt Änderungen, Auftrag enthält die aktuelle Spielbibel")]
    public void Change_and_diff()
    {
        var old = GameDirector.Parse(Answer).Doc!;
        var change = "```json\n{\"title\":\"Funkenwald II\",\"characters\":[{\"name\":\"Mira\",\"role\":\"Held\",\"text\":\"Försterin\"},{\"name\":\"Asche\",\"role\":\"Boss\",\"phases\":2}]}\n```";
        var neu = GameDirector.Parse(change, old).Doc!;
        Assert.Equal("Action-Adventure", neu["genre"]!.ToString()); // nicht geliefert → bleibt
        var diff = GameDirector.Diff(old, neu);
        Assert.Contains("Titel: Funkenwald → Funkenwald II", diff); Assert.Contains("+ Figur „Asche“", diff); Assert.Contains("− Figur „Glutherz“", diff);
        var p = GameDirector.Prompt("mach den Boss schwerer", old);
        Assert.Contains("mach den Boss schwerer", p); Assert.Contains("\"title\": \"Funkenwald\"", p); Assert.DoesNotContain("milestones", p);
        Assert.StartsWith("Entwickle dieses Spiel:", GameDirector.Prompt("Ein Rennspiel", null));
    }

    [Fact(DisplayName = "Fehlerfälle: keine Spielbibel, kaputtes JSON, nur Rückfragen")]
    public void Errors()
    {
        Assert.NotNull(GameDirector.Parse("Ich weiß nicht.").Error);
        Assert.Contains("fehlerhaft", GameDirector.Parse("```json\n{kaputt\n```").Error);
        var q = GameDirector.Parse("Frage: Welche Spielart? [Rennspiel | Puzzle]");
        Assert.Null(q.Error); Assert.Null(q.Doc); Assert.Single(q.Questions);
    }

    [Fact(DisplayName = "Profi-Team: Anweisungen nennen alle Rollen und das Format; mit Claude volle Fähigkeiten; Vorschläge ohne KI aus Prüfung und Prüfbericht")]
    public void Team_and_suggestions()
    {
        var local = GameDirector.Instructions(false); var claude = GameDirector.Instructions(true);
        foreach (var t in GameDirector.Team) Assert.Contains(t.Label, local);
        Assert.Contains("```json", local); Assert.Contains("Frage:", local); Assert.Contains("Vorschlag:", local);
        Assert.DoesNotContain("alle Fähigkeiten von Claude", local); Assert.Contains("alle Fähigkeiten von Claude", claude);
        var d = GameDirector.Parse(Answer).Doc!;
        var s = GameDirector.Suggest(d, "✓ Kompiliert\n✗ Spiel: Figur fällt durch den Boden");
        Assert.Contains(s, x => x.Contains("Figur fällt durch den Boden")); Assert.Contains(s, x => x.StartsWith("Test (QA):"));
        Assert.True(s.Count <= 10);
    }
}
