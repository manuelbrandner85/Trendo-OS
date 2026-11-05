using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude-Ausbau 2 (05.11.2026): Claude baut Level in „Spiele bauen“ – Level als Text für Claude, Plan mit Level-Vorschau, erst „Ausführen“ ändert,
/// ungültige Vorschläge fallen mit Grund weg, „Verwerfen“ holt den alten Stand zurück. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeGame_Ui
{
    const string GK = "spiele-programm";
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    [AvaloniaFact(DisplayName = "CORE-06: Claude baut Level – sieht alle Level voll, Plan zeigt das neue Level als Vorschau, erst „Ausführen“ legt Level 2 und den Hintergrund an, falsches Zeichen fällt weg, „Verwerfen“ stellt alles zurück")]
    public async Task Claude_baut_level()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeStepDelay = 0;
        const string neu = "..........Z/S...o..E..#/###########";
        MainWindow.ClaudeRunOverride = (args, _, line, _) =>
        {
            calls.Add(args);
            var text = calls.Count == 1 ? "Gern – welche Spielart?" : $"Ich baue ein zweites Level mit Wolken.\n[[trendo:feld | game-level-2 | {neu}]]\n[[trendo:feld | game-decor-1 | ..w...w...]]\n[[trendo:feld | game-level-1 | S..X..Z/#######]]";
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show(GK); w.ProgramNew(GK, "retro-plattform"); w.GameSection = "Level"; w.Show(GK);
            var d = w.ProgramActive(GK)!;
            var level1 = string.Join("/", MainWindow.GameLevelRows(d, 0));
            var state = w.ClaudeReadPage(GK)!;
            Assert.Contains(state.Fields, f => f.Name == "game-level-1" && f.Value == level1);
            Assert.Contains(state.Fields, f => f.Name == "game-level-2" && f.Value == ""); // Platz für ein neues Level
            Assert.Contains(level1, state.Title); // voller Text für Claude, nicht gekürzt
            w.ClaudeDockToggle("CORE-06"); w.Show(GK); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (w.Windows.Active?.Id is { } wid && w.ClaudeDockIsMax(wid)) w.Find<Button>("claude-dock-max").Press();
            w.Show(GK);
            await Run(w.ClaudeChatSend("Bau mir ein Spiel")); // Claude legt sein Projekt-Dokument an
            d = w.ProgramActive(GK)!;
            d.Edit("Spielart", x => { x["browser"] = "plattform"; x["level"] = new System.Text.Json.Nodes.JsonArray([.. GameStudio.DefaultPlatform.Select(r => (System.Text.Json.Nodes.JsonNode)r)]); });
            level1 = string.Join("/", MainWindow.GameLevelRows(d, 0)); w.Show(GK);
            await Run(w.ClaudeChatSend("Bau mir ein zweites Level"));
            Assert.True(calls[1][1].Contains("game-level-2"), calls[1][1]); Assert.Contains("S Start", calls[1][1]); Assert.Contains(level1, calls[1][1]);
            Assert.True(w.ClaudePlanPending());
            Assert.NotNull(w.TryFind<LevelCanvas>("claude-plan-level")); // Vorschau des neuen Levels
            Assert.True(w.Find<TextBlock>("claude-plan-warn").Text!.Contains("„X“"), w.Find<TextBlock>("claude-plan-warn").Text); // „X“ gibt es nicht
            d = w.ProgramActive(GK)!;
            Assert.Equal(1, MainWindow.GameLevelCount(d)); // noch nichts geändert
            w.Find<Button>("claude-plan-run").Press(); await Run(Task.Delay(100));
            d = w.ProgramActive(GK)!;
            Assert.Equal(2, MainWindow.GameLevelCount(d));
            Assert.Equal(neu.Split('/'), MainWindow.GameLevelRows(d, 1));
            Assert.Equal('w', MainWindow.GameDecorRows(d, 0)[0][2]);
            Assert.Equal(level1, string.Join("/", MainWindow.GameLevelRows(d, 0))); // ungültiger Vorschlag nicht übernommen
            var msg = w.ClaudeChatCurrent()!.Messages[^1].Text;
            Assert.Contains("Im Programm erledigt", msg); Assert.Contains("Nicht möglich", msg);
            w.Show(GK);
            w.Find<Button>("claude-change-drop").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            d = w.ProgramActive(GK)!;
            Assert.Equal(1, MainWindow.GameLevelCount(d));
            Assert.DoesNotContain('w', string.Concat(MainWindow.GameDecorRows(d, 0)));
        }
        finally { MainWindow.ClaudeStepDelay = 350; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
