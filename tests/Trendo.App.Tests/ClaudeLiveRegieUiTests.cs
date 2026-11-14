using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude 25 (05.11.2026): Claude als Regie in Live streamen – sieht Szenen, Ton und Ton-Wache, schlägt Szenen und Sendeplan vor, Plan mit Vorschau, erst „Ausführen“ übernimmt. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeLiveRegie_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    [AvaloniaFact(DisplayName = "SPEC-15: Claude als Regie – Briefing mit Szenen, Ton und Ton-Wache, Plan zeigt neue Szenen, „Ausführen“ legt Szenen mit Bauchbinde an und setzt den Sendeplan; ungültige Quelle fällt weg")]
    public async Task Claude_regie()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = (args, _, line, _) =>
        {
            calls.Add(args);
            const string reply = "Regie-Vorschlag: Begrüßung mit Kamera und Bauchbinde, dann Demo am Bildschirm.\n[[trendo:feld | live-claude-szenen | Begrüßung: kamera + text=Willkommen zur Sendung / Demo: bildschirm]]\n[[trendo:feld | live-plan-text | 00:00 Begrüßung – Hallo sagen / 03:00 Demo – Funktion zeigen]]\n[[trendo:klick | live-plan-set]]\n[[trendo:feld | live-claude-szenen | Kaputt: drohne]]";
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = reply } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = reply, session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.BcMode = "studio"; w.Show("sendung");
            var state = w.ClaudeReadPage("sendung")!;
            Assert.Contains(state.Fields, f => f.Name == "live-claude-szenen" && f.Value.Contains("Gleich geht's los: bildschirm + text=Gleich geht's los"));
            Assert.Contains("du bist die Regie", state.Title); Assert.Contains("Ton-Wache: keine", state.Title);
            w.ClaudeDockToggle("SPEC-15"); w.Show("sendung"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (w.Windows.Active?.Id is { } wid && w.ClaudeDockIsMax(wid)) w.Find<Button>("claude-dock-max").Press();
            w.Show("sendung");
            await Run(w.ClaudeChatSend("Bau mir Szenen und einen Sendeplan für eine Produkt-Demo"));
            Assert.Contains("live-claude-szenen", calls[^1][1]);
            Assert.True(w.ClaudePlanPending());
            Assert.NotNull(w.TryFind<WrapPanel>("claude-plan-live"));
            Assert.Contains(w.GetLogicalDescendantsText(), t => t.StartsWith("⚠ So nicht möglich") && t.Contains("drohne"));
            Assert.Equal(4, w.LiveShowState.Scenes.Count); // noch nichts geändert
            w.Find<Button>("claude-plan-run").Press(); await Run(Task.Delay(150));
            var show = w.LiveShowState;
            var greet = show.Scenes.Single(s => s.Name == "Begrüßung");
            Assert.Equal(VideoKind.Camera, greet.Main); Assert.Equal("Willkommen zur Sendung", greet.Overlays.Single().Value);
            Assert.Contains(show.Scenes, s => s.Name == "Demo" && s.Main == VideoKind.Screen);
            Assert.Equal(2, show.Plan.Count); Assert.Equal(greet.Id, show.Plan[0].Scene);
            Assert.Contains("Nicht möglich", w.ClaudeChatCurrent()!.Messages[^1].Text);
        }
        finally { MainWindow.ClaudeStepDelay = 350; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
