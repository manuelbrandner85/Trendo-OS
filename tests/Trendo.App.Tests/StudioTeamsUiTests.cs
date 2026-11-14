using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Wunsch 03.11.2026: Profi-Team je Studio, Rollen fest zugewiesen, „👥 Team“ im Studio, Team als Claude-Code-Agenten im Claude-Chat.
[Collection("Claude")]
public class StudioTeams_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }

    [AvaloniaFact(DisplayName = "Teams: jedes Studio (außer dem abgelehnten SPEC-07) hat ein Profi-Team; „Team“ im 3D-Studio öffnet „KI einsetzen“ mit dem 3D-Team; eigene Rolle nur mit Studio")]
    public void Teams_in_studios()
    {
        foreach (var (id, _) in StudioCatalog.ById.Where(kv => kv.Key != "SPEC-07")) Assert.True(StudioTeams.For(id) is not null, "Ohne Team: " + id);
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("3d"); Pump();
            w.Find<Button>("page-team").Press(); Pump();
            Assert.Equal("ki-rollen", w.CurrentView);
            Assert.Equal("3D-Modelle erstellen", w.Find<ComboBox>("agent-studio").SelectedItem);
            Assert.NotNull(w.TryFind<Button>("role-core-05.modellierer")); Assert.NotNull(w.TryFind<Button>("role-core-05.pruefer"));
            Assert.Null(w.TryFind<Button>("role-planer")); // allgemeine Rollen nur ohne Studio
            Assert.NotNull(w.TryFind<Button>("team-studio:CORE-05"));
            Assert.Contains("3D-Modellierer → Material-Artist", string.Join("|", w.GetLogicalTexts()));

            // eigene Rolle: ohne Name abgelehnt, dann im gewählten Studio
            w.Find<Button>("agent-role-new").Press(); Pump();
            Assert.Equal("3D-Modelle erstellen", w.Find<ComboBox>("agent-role-studio").SelectedItem);
            w.Find<Button>("agent-role-save").Press(); Pump();
            Assert.Contains("Namen", w.Find<TextBlock>("agent-msg").Text);
            Assert.NotNull(w.TryFind<Border>("agent-role-card") ?? (Control?)w.TryFind<StackPanel>("agent-role-card")); // Formular bleibt offen
            w.Find<TextBox>("agent-role-name").Text = "Druck-Prüfer"; w.Find<TextBox>("agent-role-does").Text = "prüft, ob das Modell druckbar ist";
            w.Find<Button>("agent-role-save").Press(); Pump();
            Assert.Contains("gehört jetzt zum Studio „3D-Modelle erstellen“", w.Find<TextBlock>("agent-msg").Text);
            var mine = Assert.Single(w.Agents.CustomRoles()); Assert.Equal("CORE-05", mine.Studio);
            Assert.NotNull(w.TryFind<Button>("role-" + mine.Id));
            w.Find<Button>("agent-role-del").Press(); Pump(); Assert.Empty(w.Agents.CustomRoles());

            // ohne Studio: die allgemeinen Rollen und Teams
            w.Find<ComboBox>("agent-studio").SelectedIndex = 0; Pump();
            Assert.NotNull(w.TryFind<Button>("role-planer")); Assert.NotNull(w.TryFind<Button>("team-text"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Teams im Claude-Chat: vor dem Lauf liegt das Studio-Team als .claude/agents/trendo-*.md im Projektordner – Claude kann die Fachrollen hinzuziehen")]
    public async Task Team_as_claude_agents()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
        string? seen = null;
        MainWindow.ClaudeRunOverride = (args, dir, line, _) =>
        {
            seen = string.Join(",", Directory.GetFiles(Path.Combine(dir, ".claude", "agents"), "trendo-*.md").Select(Path.GetFileName).Order());
            line(JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = "Fertig", session_id = "s", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Pump();
            await Run(w.ClaudeChatSend("Baue eine Seite"));
            Assert.Equal("trendo-core-07-frontend.md,trendo-core-07-motion.md,trendo-core-07-pruefer.md,trendo-core-07-seo.md,trendo-core-07-ux.md", seen);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
