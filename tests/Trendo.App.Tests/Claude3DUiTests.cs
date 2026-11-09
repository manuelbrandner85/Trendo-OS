using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude-Ausbau 1 (05.11.2026): Claude baut im 3D-Studio aus einem Satz ein Modell mit echten Maßen, Materialien, Licht und Kamera – Plan mit 3D-Vorschau,
/// erst „Ausführen“ ändert die Szene, ungültige Teile fallen mit Grund weg, „Rückgängig“ holt den alten Stand. Claude ist nachgebildet.
[Collection("Claude")]
public class Claude3D_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    const string Lampe = "Fuß;cylinder;14;14;2;0;0;1;0;0;0;chrom;Lampe / Stange;cylinder;2;2;30;0;0;17;0;0;0;metall;Lampe / Schirm;cone;24;24;16;0;0;38;0;0;0;stoff;Lampe";

    [AvaloniaFact(DisplayName = "CORE-05: Claude baut ein 3D-Modell – sieht Szene, Formen und Materialien, Plan zeigt das Modell mit Gesamtmaß, erst „Ausführen“ baut Teile, Licht und Kamera; ungültiges Material fällt weg; Rückgängig stellt zurück")]
    public async Task Claude_baut_modell()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = (args, _, line, _) =>
        {
            calls.Add(args);
            var text = $"Ich baue eine Stehlampe für den Tisch.\n[[trendo:feld | 3d-teile | {Lampe}]]\n[[trendo:feld | 3d-licht | abend]]\n[[trendo:feld | 3d-kamera | 85;0]]";
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("3d"); w.Studio3DAddShape("cube");
            var state = w.ClaudeReadPage("3d")!;
            Assert.Contains(state.Fields, f => f.Name == "3d-teile" && f.Value.Contains(";cube;"));
            Assert.Contains("Materialien (PBR)", state.Title); Assert.Contains("chrom", state.Title);
            w.ClaudeDockToggle("CORE-05"); w.Show("3d"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (w.Windows.Active?.Id is { } wid && w.ClaudeDockIsMax(wid)) w.Find<Button>("claude-dock-max").Press();
            w.Show("3d");
            await Run(w.ClaudeChatSend("Bau mir eine Tischlampe"));
            Assert.Contains("3d-teile", calls[0][1]); Assert.Contains("Boden bei Z = 0", calls[0][1]);
            Assert.True(w.ClaudePlanPending());
            Assert.NotNull(w.TryFind<CadView>("claude-plan-3d"));
            Assert.Contains("3 Teile", w.Find<TextBlock>("claude-plan-3d-info").Text);
            Assert.Single(w.Studio3DParts); // noch nichts geändert
            w.Find<Button>("claude-plan-run").Press(); await Run(Task.Delay(100));
            Assert.Equal(["Fuß", "Stange", "Schirm"], w.Studio3DParts.Select(p => p.Name));
            Assert.All(w.Studio3DParts, p => Assert.Equal("Lampe", p.Group));
            Assert.Equal(("chrom", 30d), (w.Studio3DParts[0].Material, w.Studio3DParts[1].Sz));
            var scene = Scene3D.FromJson(w.Studio3DSceneJson);
            Assert.Equal(("abend", 85d), (scene.Light, scene.CameraLens));
            Assert.Contains("Im Programm erledigt", w.ClaudeChatCurrent()!.Messages[^1].Text);
            // ungültige Vorschläge
            Assert.Null(MainWindow.Parse3DParts("Klotz;cube;10;10;10;0;0;5;0;0;0;quark").Parts);
            Assert.Contains("Form", MainWindow.Parse3DParts("Klotz;pyramide;10;10;10").Error);
            Assert.Contains("Maße", MainWindow.Parse3DParts("Klotz;cube;0;10;10").Error);
            // Rückgängig (Kamera, Licht, Teile – je ein Schritt)
            w.Find<Button>("3d-undo").Press(); w.Find<Button>("3d-undo").Press(); w.Find<Button>("3d-undo").Press();
            Assert.Single(w.Studio3DParts); Assert.Equal("cube", w.Studio3DParts[0].Shape);
        }
        finally { MainWindow.ClaudeStepDelay = 350; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
