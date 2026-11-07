using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>U1–U6 im Spiele-Prompt: Rat-Modus mit Fachleuten, Befehle über die kostenlosen KI-Werkzeuge mit Prüfschleife, Team-Lauf mit Protokoll.</summary>
public class GameUgs_Ui
{
    const string GK = "spiele-programm";
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "U1/U2: Rat-Modus umschalten, „Am Auftrag“ zeigt Fachleute, die Team-Anweisung enthält sie")]
    public async Task Council()
    {
        var (w, _, _) = Ui.Open(); var sys = "";
        MainWindow.GamePromptOverride = (s, p) => { sys = s; return Task.FromResult("```json\n{\"title\":\"Jagd\"}\n```"); };
        try
        {
            w.Show(GK); Pump();
            w.Find<Button>("game-council-rat").Press(); Pump(); Assert.Equal("rat", w.GameCouncilMode);
            Assert.StartsWith("Am Auftrag:", w.Find<TextBlock>("game-council-who").Text);
            w.Find<TextBox>("game-prompt").Text = "Gegner sollen den Spieler verfolgen und im Rudel angreifen";
            w.Find<Button>("game-prompt-new").Press(); Pump(); await Task.Delay(50); Pump();
            Assert.Contains("Fachleute für diesen Auftrag", sys); Assert.Contains("KI-Programmierer", sys); Assert.Contains("Prüfschleife für Bilder", sys);
        }
        finally { MainWindow.GamePromptOverride = null; }
    }

    [AvaloniaFact(DisplayName = "U4/U5/U6: /sprite erzeugt ein Spritesheet über das Bild-Werkzeug, die Prüfschleife bessert nach; Modus /musik-start … -ende")]
    public async Task Commands()
    {
        var (w, _, _) = Ui.Open(); var calls = new List<(string Kind, string Input)>(); int judged = 0;
        MainWindow.KiToolOverride = (k, i, t) => { calls.Add((k, i)); return Task.FromResult(true); };
        MainWindow.GameJudgeOverride = (f, q) => Task.FromResult(++judged < 2 ? "{\"pass\":false,\"score\":50,\"maengel\":[\"Figur wechselt die Farbe\"]}" : "{\"pass\":true,\"score\":88,\"maengel\":[]}");
        try
        {
            w.Show(GK); Pump();
            Assert.True(await w.GameCommandRun("/sprite Ritter mit Schild laufen")); Pump();
            Assert.Equal(2, calls.Count); Assert.All(calls, c => Assert.Equal("bild", c.Kind));
            Assert.Contains("sprite sheet", calls[0].Input); Assert.Contains("Vermeiden: Figur wechselt die Farbe", calls[1].Input);
            Assert.True(await w.GameCommandRun("/musik-start")); Assert.True(await w.GameCommandRun("ruhige Waldmusik am Morgen"));
            Assert.Equal(("musik", "ruhige Waldmusik am Morgen"), calls[^1]);
            Assert.True(await w.GameCommandRun("/musik-ende")); Assert.False(await w.GameCommandRun("mach den Wald dichter"));
        }
        finally { MainWindow.KiToolOverride = null; MainWindow.GameJudgeOverride = null; }
    }

    [AvaloniaFact(DisplayName = "U3: Team-Lauf mit Ablaufplan – Gegenprüfung, Abnahme, Protokoll im Spielordner")]
    public async Task Harness()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.GamePromptOverride = (role, q) => Task.FromResult(q.Contains("JSON") ? "{\"ok\":true,\"maengel\":[]}" : "Plan: Speicherpunkt vor dem Boss");
        try
        {
            w.Show(GK); Pump();
            await w.GameHarnessRun("Prüfe die Speicherpunkte im Spiel"); Pump();
            Assert.NotNull(w.GameHarnessLog); Assert.True(File.Exists(Path.Combine(w.GameHarnessLog!, "ledger.jsonl")));
            Assert.Contains("bestanden", File.ReadAllText(Path.Combine(w.GameHarnessLog!, "ergebnis.md")));
        }
        finally { MainWindow.GamePromptOverride = null; }
    }
}
