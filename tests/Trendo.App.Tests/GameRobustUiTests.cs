using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// Befund 02.11.2026 (absturz-…165035): Claude schrieb in der Spielbibel Einträge mit Name und Beschreibung statt kurzer Texte – „Bibel“ stürzte ab.
/// Programme lesen Werte jetzt sicher; neue Spielregeln: Claude fragt nach dem Ziel und füllt alle Felder.
public class GameRobust_Ui
{
    [Fact(DisplayName = "Sichere Lesehilfe: Text, Zahl, Wahrheitswert, Eintrag mit Feldern, Liste – nie ein Absturz")]
    public void Json_text()
    {
        Assert.Equal("Springen", JsonValue.Create("Springen").AsText());
        Assert.Equal("3", JsonValue.Create(3).AsText());
        Assert.Equal("Doppelsprung – Zweiter Sprung in der Luft", JsonNode.Parse("""{"name":"Doppelsprung","text":"Zweiter Sprung in der Luft"}""").AsText());
        Assert.Equal("a, b", JsonNode.Parse("""["a","b"]""").AsText());
        Assert.Null(((JsonNode?)null).AsText());
        Assert.True(JsonValue.Create("ja").AsBool()); Assert.False(JsonValue.Create("nein").AsBool(true)); Assert.True(((JsonNode?)null).AsBool(true));
    }

    [AvaloniaFact(DisplayName = "Spielbibel mit Einträgen statt Texten (wie von Claude geschrieben) – Programm und Bibel öffnen ohne Absturz und zeigen den Inhalt")]
    public void Bible_with_objects()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var page = MainWindow.Programs.Keys.First(k => MainWindow.Programs[k].Studio == "CORE-06");
            w.Show(page); var d = w.ProgramNew(page);
            d.ReplaceData("""{"title":"Frontlinie","scale":"aaa","mechanics":[{"name":"Deckung","text":"Hinter Wänden in Deckung gehen"},{"name":"Sprinten"}],"milestones":[{"name":{"de":"Prototyp"},"done":"ja"}],"level":[123,"#..#"],"characters":[{"name":"Mara","role":3}]}""");
            foreach (var section in new[] { "Übersicht", "Spielbibel", "Figuren", "Welt", "Missionen", "Level", "Produktion", "Bauen" })
            {
                w.GameSection = section; w.Show(page);
                Assert.True(w.TryFind<Button>("error-retry") is null, "Absturz im Abschnitt " + section);
            }
            w.GameSection = "Spielbibel"; w.Show(page);
            Assert.Contains("Deckung – Hinter Wänden in Deckung gehen", w.Find<TextBox>("game-f-mechanics").Text);
        }
        finally { w.Close(); }
    }

    [Fact(DisplayName = "Spielregeln: Claude fragt nach dem Ziel (Browser, Windows, Mac, Steam, itch.io, Handy), füllt alle Felder Schritt für Schritt, arbeitet wie ein Profi-Studio")]
    public void Game_rules_more()
    {
        Assert.Contains("Wie soll dein Spiel erscheinen? [Browser-Spiel | Windows-Setup | Windows und Mac | Steam | itch.io | Handy]", GameRules.Text);
        Assert.Contains("Alle Formulare ausfüllen", GameRules.Text); Assert.Contains("milestones", GameRules.Text);
        Assert.Contains("Konzept → Vorproduktion", GameRules.Text);
    }
}
