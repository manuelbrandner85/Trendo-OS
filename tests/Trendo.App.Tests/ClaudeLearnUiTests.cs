using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.41 – Spiele mit GAMEFORGE-Regeln (eingebaut für alle, GAMEFORGE wenn da) und selbstlernende Regeln in allen Studios (gelten erst nach Ja). Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeLearn_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    static string Sys(IReadOnlyList<string> a) { var l = a.ToList(); return l[l.IndexOf("--append-system-prompt") + 1]; }

    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<int, string> answer) => (args, _, line, _) =>
    {
        calls.Add(args);
        var text = answer(calls.Count);
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
        return Task.FromResult(0);
    };
    static void End(MainWindow w) { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.GameForgeDirs = null; w.Close(); }

    [AvaloniaFact(DisplayName = "Spiele-Studio: eingebaute Spielregeln; mit GAMEFORGE dessen Regeln, Skills und Agenten (im sicheren Modus als Projekt-Skills); Regeln-Feld zeigt den Stand")]
    public async Task Game_rules_and_gameforge()
    {
        var (w, _, root) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls, _ => "Fertig.");
        var claudeDir = Path.Combine(root, "fake-claude");
        var plugin = Path.Combine(claudeDir, "plugins", "cache", "lokal", "gameforge", "3.9.1");
        foreach (var s in new[] { "gameforge-studio", "neues-spiel" }) { Directory.CreateDirectory(Path.Combine(plugin, "skills", s)); File.WriteAllText(Path.Combine(plugin, "skills", s, "SKILL.md"), "# " + s); }
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show(w.PageForStudio("CORE-06") ?? "spiele"); w.ClaudeDockToggle("CORE-06"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ein kleines Rennspiel"));
            Assert.Contains("Regeln für Spieleentwicklung in Trendo", Sys(calls[0])); Assert.DoesNotContain("GAMEFORGE", Sys(calls[0])); // ohne GAMEFORGE: eingebaute Regeln
            MainWindow.GameForgeDirs = () => [claudeDir];
            w.Find<Button>("claude-chat-full").Press(); w.Find<Button>("claude-chat-full-off").Press(); // dieses Projekt auf „sicher“
            await Run(w.ClaudeChatSend("Mehr Strecken"));
            Assert.Contains("GAMEFORGE 3.9.1", Sys(calls[^1])); Assert.Contains("„neues-spiel“", Sys(calls[^1]));
            Assert.True(File.Exists(Path.Combine(w.ClaudeChatCurrent()!.Folder, ".claude", "skills", "gameforge-neues-spiel", "SKILL.md"))); // sicherer Modus: als Projekt-Skill
            w.Find<Button>("claude-chat-rules").Press();
            Assert.Contains("GAMEFORGE 3.9.1 erkannt", w.Find<TextBlock>("claude-rules-gameforge").Text);
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Selbstlernend in allen Studios: Claudes „Lernregel“ wird Vorschlag; nach Ja gilt sie für alle Projekte des Studios; Nein und Zurücknehmen; Gutachten lehrt mit")]
    public async Task Learning_with_approval()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = Fake(calls, n => n switch
        {
            1 => "Fertig.\nLernregel: Jede Seite bekommt einen sichtbaren Fokus-Rahmen für Tastaturbedienung",
            2 => "Gemacht.\nLernregel: Bilder immer mit Alternativtext ausliefern",
            _ => "Ok.",
        });
        MainWindow.ReviewOverride = _ => Task.FromResult("Note: 7/10\nSchwächen:\n- Kontrast zu schwach\nLernregel: Textkontrast immer mindestens 4,5 zu 1");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Startseite"));
            Assert.DoesNotContain("Lernregel:", w.ClaudeChatCurrent()!.Messages[^1].Text);
            Assert.Equal("Jede Seite bekommt einen sichtbaren Fokus-Rahmen für Tastaturbedienung", w.Find<TextBlock>("claude-learn-text-0").Text);
            Assert.Empty(w.ClaudeStudioRulesFor("CORE-07")); // noch nicht wirksam
            w.Find<Button>("claude-learn-yes-0").Press();
            Assert.Single(w.ClaudeStudioRulesFor("CORE-07"));

            w.Find<Button>("claude-chat-new").Press(); // neues Projekt im selben Studio
            await Run(w.ClaudeChatSend("Baue eine Galerie"));
            Assert.Contains("- Jede Seite bekommt einen sichtbaren Fokus-Rahmen", Sys(calls[1])); // gilt für alle Projekte des Studios
            w.Find<Button>("claude-learn-no-0").Press(); // „Alternativtext“ abgelehnt
            Assert.Empty(w.ClaudeStudioRulesFor("CORE-07", pending: true));

            w.Find<Button>("claude-chat-review").Press(); while (w.ClaudeChatCurrent()!.Messages[^1].Role != "review") await Run(Task.Delay(20));
            Assert.Equal("Gutachter", w.ClaudeStudioRulesFor("CORE-07", pending: true).Single().Source);

            w.Find<Button>("claude-chat-rules").Press();
            w.Find<Button>("claude-learn-retract-0").Press();
            Assert.Empty(w.ClaudeStudioRulesFor("CORE-07"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "studio.regel.an");
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "studio.regel.zurueck");
        }
        finally { MainWindow.ReviewOverride = null; End(w); }
    }
}
