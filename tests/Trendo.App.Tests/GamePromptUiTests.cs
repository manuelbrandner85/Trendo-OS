using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Spiele-Prompt: Spiel aus einem Satz mit Vorschau und Übernehmen (rückgängig), Rückfragen als Knöpfe, Ändern per Satz, Team-Vorschläge; ohne Claude die lokale KI.</summary>
public class GamePrompt_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    const string First = """
        ```json
        {"title":"Funkenwald","scale":"aa","genre":"Action-Adventure","logline":"Eine Försterin rettet den Wald.","loop":"Erkunden → Kämpfen",
         "mechanics":["Schwertkampf","Klettern","Löschen"],"characters":[{"name":"Mira","role":"Held","text":"Försterin"},{"name":"Glutherz","role":"Boss","text":"Feuerdämon","phases":3}],
         "world":[{"name":"Lichtung","text":"Start"}],"quests":[{"title":"Erster Funke","kind":"Haupt","goal":"Feuer löschen","giver":"Mira"}]}
        ```
        Frage: Wie soll es aussehen? [Fotorealistisch | Comic]
        Vorschlag: Ein Begleiter-Tier für Mira.
        """;
    const string Second = "```json\n{\"title\":\"Funkenwald\",\"characters\":[{\"name\":\"Mira\",\"role\":\"Held\"},{\"name\":\"Glutherz\",\"role\":\"Boss\",\"phases\":4},{\"name\":\"Funke\",\"role\":\"Begleiter\",\"text\":\"Fuchs\"}]}\n```";

    [AvaloniaFact(DisplayName = "Spiele-Prompt: Satz → Vorschau mit KI-Name → Übernehmen legt das Spiel an (Strg+Z-fähig); Rückfrage-Knöpfe; Ändern zeigt nur die Änderung; Team-Vorschläge mit Ja")]
    public async Task Prompt_to_game()
    {
        var (w, _, _) = Ui.Open();
        var asked = new List<(string Sys, string Prompt)>();
        MainWindow.GamePromptOverride = (sys, p) => { asked.Add((sys, p)); return Task.FromResult(asked.Count == 1 ? First : Second); };
        try
        {
            w.Show(GK); Pump();
            Assert.Contains("Lokale KI", w.Find<TextBlock>("game-prompt-ki").Text); // ohne Claude: lokale KI
            w.Find<TextBox>("game-prompt").Text = "Ein Action-Abenteuer im brennenden Wald";
            w.Find<Button>("game-prompt-new").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Contains("Ein Action-Abenteuer im brennenden Wald", asked[0].Prompt); Assert.Contains("Game Director", asked[0].Sys);
            Assert.Contains("+ Figur „Glutherz“", w.GamePromptPreview);
            Assert.NotNull(w.Find<Button>("game-prompt-q0-0")); Assert.NotNull(w.Find<Button>("game-prompt-s0"));
            Assert.Null(w.ProgramActive(GK));
            w.Find<Button>("game-prompt-accept").Press(); Pump();
            var d = w.ProgramActive(GK)!;
            Assert.Equal("Funkenwald", d.Text("title")); Assert.Equal("aa", d.Text("scale")); Assert.Equal(2, d.List("characters").Count);
            // Ändern per Satz: Vorschau zeigt nur die Änderung, die aktuelle Spielbibel geht mit
            w.Find<TextBox>("game-prompt").Text = "Gib Mira einen Fuchs als Begleiter und mach den Boss schwerer";
            w.Find<Button>("game-prompt-change").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Contains("\"title\": \"Funkenwald\"", asked[1].Prompt);
            Assert.Contains("+ Figur „Funke“", w.GamePromptPreview); Assert.Contains("~ Figur „Glutherz“ geändert", w.GamePromptPreview); Assert.DoesNotContain(w.GamePromptPreview, l => l.StartsWith("Titel"));
            w.Find<Button>("game-prompt-accept").Press(); Pump();
            Assert.Equal(3, d.List("characters").Count); Assert.Equal("Action-Adventure", d.Text("genre")); // nicht gelieferte Felder bleiben
            w.Find<Button>("prog-undo").Press(); Pump();
            Assert.Equal(2, d.List("characters").Count); // ein Schritt zurück
            Assert.NotNull(w.Find<StackPanel>("game-prompt-team")); // Team-Vorschläge ohne KI
        }
        finally { MainWindow.GamePromptOverride = null; }
    }
}
