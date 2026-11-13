using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0640: Prüfstufe und Torurteil in der Phasenleiste, Stories mit Abnahmekriterien auf dem Board, Aufbau des KI-Teams.</summary>
public class GameFlow_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "Produktion: Prüfstufe wählen, Urteil sehen, Bedenken nur mit Risiko abnehmen; Team-Aufbau sichtbar")]
    public void Rigor_and_gate()
    {
        var (w, _, _) = Ui.Open();
        var d = w.ProgramNew(GK, "topdown");
        d.Edit("Zielgruppe", x => x["audience"] = "Kinder ab 8");
        w.GameSection = "Produktion"; w.Show(GK); Pump();
        Assert.NotNull(w.Find<StackPanel>("game-team-tree"));
        Assert.Equal("Urteil: bestanden", w.Find<TextBlock>("game-gate-verdict").Text); // schlank: wie bisher
        w.Find<Button>("game-rigor-normal").Press(); Pump();
        Assert.Equal("normal", GameFlow.RigorOf(d.Data));
        d.Edit("Stories", x => GameFlow.Generate(x)); w.Show(GK); Pump();
        Assert.Equal("Urteil: nicht geprüft", w.Find<TextBlock>("game-gate-verdict").Text);
        Assert.False(w.Find<Button>("game-phase-accept").IsEnabled);

        var i = GameBoard.Tasks(d.Data).FindIndex(t => ((string?)t["title"])!.StartsWith("Kernspiel", StringComparison.Ordinal));
        d.Edit("Prüfen", x => { for (var k = 0; k < 3; k++) GameFlow.Check(x, i, k, k != 2); GameFlow.Complete(x, i, GameFlow.DoneWithNotes, "zwei Mechaniken"); });
        w.Show(GK); Pump();
        Assert.Equal("Urteil: bedenken", w.Find<TextBlock>("game-gate-verdict").Text);
        w.Find<Button>("game-phase-accept").Press(); Pump();
        Assert.False(GamePro.Accepted(d.Data, "konzept")); // ohne Risiko-Notiz nicht
        w.Find<TextBox>("game-gate-risk").Text = "Wird im Prototyp ergänzt";
        w.Find<Button>("game-phase-accept").Press(); Pump();
        Assert.True(GamePro.Accepted(d.Data, "konzept"));

        w.Find<Button>("game-rigor-streng").Press(); Pump();
        Assert.Equal("streng", GameFlow.RigorOf(d.Data));
        w.Find<Button>("prog-undo").Press(); Pump();
        Assert.Equal("normal", GameFlow.RigorOf(d.Data));
    }

    [AvaloniaFact(DisplayName = "Board: Stories aus der Spielbibel, Kriterien abhaken, erst dann fertig; Blockiert braucht einen Grund")]
    public void Stories_on_board()
    {
        var (w, _, _) = Ui.Open();
        var d = w.ProgramNew(GK, "topdown");
        w.GameSection = "Übersicht"; w.Show(GK); Pump();
        w.Find<Button>("game-task-stories").Press(); Pump();
        var i = GameBoard.Tasks(d.Data).FindIndex(t => ((string?)t["title"])!.StartsWith("Kernspiel", StringComparison.Ordinal));
        Assert.True(i >= 0);
        w.Find<Button>($"game-task-{i}-done").Press(); Pump();
        Assert.Equal("Offen", GameBoard.StateOf(GameBoard.Tasks(d.Data)[i])); // Kriterien noch offen
        for (var k = 0; k < 3; k++) { w.Find<Button>($"game-task-{i}-c{k}-ok").Press(); Pump(); }
        Assert.StartsWith("✓", w.Find<TextBlock>($"game-task-{i}-c0").Text);
        w.Find<Button>($"game-task-{i}-done").Press(); Pump();
        Assert.Equal("Fertig", GameBoard.StateOf(GameBoard.Tasks(d.Data)[i]));
        Assert.Contains("Fertig", w.Find<TextBlock>($"game-task-{i}-result").Text);

        var j = GameBoard.Tasks(d.Data).FindIndex(t => ((string?)t["title"])!.StartsWith("Figur", StringComparison.Ordinal));
        w.Find<Button>($"game-task-{j}-block").Press(); Pump();
        Assert.Equal("", GameFlow.ResultOf(GameBoard.Tasks(d.Data)[j]));
        w.Find<TextBox>($"game-task-{j}-note").Text = "Animationen fehlen";
        w.Find<Button>($"game-task-{j}-block").Press(); Pump();
        Assert.Equal(GameFlow.Blocked, GameFlow.ResultOf(GameBoard.Tasks(d.Data)[j]));
        Assert.Contains("Animationen fehlen", w.Find<TextBlock>($"game-task-{j}-result").Text);
    }

    [AvaloniaFact(DisplayName = "Team: KI-Antwort nur mit Fragen ändert nichts, bis eine Antwort gewählt ist")]
    public async Task Decision_waits_for_choice()
    {
        var (w, _, _) = Ui.Open();
        var calls = 0;
        MainWindow.GamePromptOverride = (_, _) => Task.FromResult(++calls == 1 ? "Frage: Welcher Stil? [Pixel | Comic | Aquarell]" : "```json\n{\"title\":\"Wahl\",\"scale\":\"retro\",\"genre\":\"Puzzle\"}\n```");
        try
        {
            w.Show(GK); Pump();
            w.Find<TextBox>("game-prompt").Text = "Ein Rätselspiel";
            w.Find<Button>("game-prompt-new").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Null(w.ProgramActive(GK));
            Assert.Null(w.TryFind<Button>("game-prompt-accept"));
            w.Find<Button>("game-prompt-q0-1").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Null(w.ProgramActive(GK)); // erst die Vorschau, noch kein Dokument
            w.Find<Button>("game-prompt-accept").Press(); Pump();
            Assert.Equal("Wahl", w.ProgramActive(GK)!.Text("title"));
        }
        finally { MainWindow.GamePromptOverride = null; }
    }
}
