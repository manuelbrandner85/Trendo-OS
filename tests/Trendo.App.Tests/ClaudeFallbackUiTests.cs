using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Claude ist nie weg (0.9.35): Abo aufgebraucht → kostenlose Ersatz-KI übernimmt (sichtbar), danach automatisch zurück zu Claude;
/// kostenlose Medien-KIs als Werkzeuge (von Claude bestellt oder per Knopf), Diktieren. Claude, Ersatz-KI und Werkzeuge sind nachgebildet.
[Collection("Claude")]
public class ClaudeFallback_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    [AvaloniaFact(DisplayName = "Claude-Abo aufgebraucht: Ersatz-KI übernimmt dieselbe Nachricht (sichtbar „Aktive KI“), bis zur Freigabe ohne Claude, danach automatisch wieder Claude")]
    public async Task Limit_fallback_and_back()
    {
        var (w, _, _) = Ui.Open();
        var claudeCalls = 0; var fbCalls = new List<string>(); var limitHit = true;
        var reset = DateTimeOffset.Now.AddHours(2);
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = (_, dir, line, _) =>
        {
            claudeCalls++;
            if (limitHit) line(J(new { type = "result", subtype = "error", is_error = true, result = $"Claude AI usage limit reached|{reset.ToUnixTimeSeconds()}", session_id = "", num_turns = 0 }));
            else { line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Claude ist zurück." } } } })); line(J(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s2", num_turns = 1 })); }
            return Task.FromResult(0);
        };
        MainWindow.ClaudeFallbackOverride = (ask, _, _) =>
        {
            fbCalls.Add(ask);
            var page = ClaudeChat_Ui.Page("<h1>Ersatz</h1>");
            return Task.FromResult(new KiAnswer(true, J(new { zusammenfassung = "Seite angelegt.", dateien = new[] { new { pfad = "index.html", inhalt = page } } }), "OpenRouter", "deepseek:free", [], TimeSpan.Zero));
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("Aktive KI: " + MainWindow.ClaudeKi, w.Find<TextBlock>("claude-chat-active").Text);
            await Run(w.ClaudeChatSend("Baue eine Startseite"));
            var chat = w.ClaudeChatCurrent()!;
            Assert.Equal(1, claudeCalls); Assert.Single(fbCalls); Assert.Contains("Baue eine Startseite", fbCalls[0]);
            var ans = chat.Messages[1];
            Assert.Equal("Seite angelegt.", ans.Text); Assert.Contains("OpenRouter", ans.Ki); Assert.Contains("deepseek:free", ans.Ki);
            Assert.Contains(ans.Steps, s => s.Text.StartsWith("Claude-Abo aufgebraucht")); Assert.Contains(ans.Steps, s => s.Text.StartsWith("schreibt index.html"));
            Assert.True(File.Exists(Path.Combine(chat.Folder, "index.html")));
            Assert.True(w.CLimit.Active(DateTimeOffset.Now)); Assert.Equal(reset.ToUnixTimeSeconds(), w.CLimit.Until!.Value.ToUnixTimeSeconds());
            Assert.Contains("aufgebraucht", w.Find<TextBlock>("claude-chat-active").Text); Assert.Contains("OpenRouter", w.Find<TextBlock>("claude-chat-active").Text);
            Assert.Contains("OpenRouter", w.Find<TextBlock>("claude-chat-ki").Text);

            await Run(w.ClaudeChatSend("Und eine Kontaktseite"));
            Assert.Equal(1, claudeCalls); Assert.Equal(2, fbCalls.Count); // solange aufgebraucht: gleich die Ersatz-KI

            w.CLimit.Set(DateTimeOffset.Now.AddSeconds(-1)); limitHit = false; // Abo wieder frei
            await Run(w.ClaudeChatSend("Noch ein Foto dazu"));
            Assert.Equal(2, claudeCalls); Assert.Equal(2, fbCalls.Count);
            Assert.Equal(MainWindow.ClaudeKi, w.ClaudeChatCurrent()!.Messages[^1].Ki); Assert.Null(w.CLimit.Until);
            Assert.Equal("Aktive KI: " + MainWindow.ClaudeKi, w.Find<TextBlock>("claude-chat-active").Text);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeFallbackOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Kostenlose KI-Werkzeuge: Claude bestellt ein Bild, Trendo erzeugt es ins Projekt; Knöpfe für Bild und Vorlesen; Diktieren füllt das Eingabefeld")]
    public async Task Tools_and_buttons()
    {
        var (w, _, _) = Ui.Open();
        var tools = new List<(string Kind, string Input, string File)>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = (_, _, line, _) =>
        {
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Ich lasse ein Titelbild malen.\n[[trendo:bild | Ein Ritter im Sonnenuntergang | bilder/titel.png]]" } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        MainWindow.KiToolOverride = (kind, input, file) => { tools.Add((kind, input, file)); File.WriteAllBytes(file, [1, 2, 3]); return Task.FromResult(true); };
        MainWindow.DictateOverride = () => Task.FromResult<string?>("Bau mir ein Quiz");
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("spiele"); w.ClaudeDockToggle("CORE-06"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ein Ritterspiel"));
            var chat = w.ClaudeChatCurrent()!;
            Assert.Equal(("bild", "Ein Ritter im Sonnenuntergang"), (tools[0].Kind, tools[0].Input)); Assert.EndsWith(Path.Combine("bilder", "titel.png"), tools[0].File);
            Assert.True(File.Exists(Path.Combine(chat.Folder, "bilder", "titel.png")));
            var ans = chat.Messages[1];
            Assert.Contains(ans.Steps, s => s.Text == "Bild erzeugen: bilder/titel.png"); Assert.Contains("bilder/titel.png (neu)", ans.Changed!);
            Assert.DoesNotContain("[[trendo:", string.Join("|", w.GetLogicalTexts())); // Bestellzeile wird nicht angezeigt

            w.Find<TextBox>("claude-chat-input").Text = "Ein Leuchtturm";
            w.Find<Button>("claude-tool-bild").Press(); while (w.ClaudeChatRunning() || tools.Count < 2) await Run(Task.Delay(20));
            Assert.Equal(("bild", "Ein Leuchtturm"), (tools[1].Kind, tools[1].Input));
            Assert.Contains("KI-Bibliothek", w.ClaudeChatCurrent()!.Messages[^1].Ki);
            w.Find<Button>("claude-tool-vorlesen").Press(); while (w.ClaudeChatRunning() || tools.Count < 3) await Run(Task.Delay(20));
            Assert.Equal("vorlesen", tools[2].Kind); Assert.StartsWith("Bild erzeugen: fertig", tools[2].Input);

            w.Find<TextBox>("claude-chat-input").Text = "";
            await Run(w.ClaudeDictate()); Assert.Equal("Bau mir ein Quiz", w.Find<TextBox>("claude-chat-input").Text);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.KiToolOverride = null; MainWindow.DictateOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude wie im Original: Skills, Plugins, Connectoren und Agenten aus dem eigenen Claude sichtbar; genutzter Skill/Connector unter der Antwort")]
    public async Task Own_skills_and_connectors_visible()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = (_, _, line, _) =>
        {
            line("""{"type":"system","subtype":"init","session_id":"s1","model":"claude","skills":["gameforge:gameforge-studio","gameforge:blender-asset","pdf"],"mcp_servers":[{"name":"claude.ai Gmail","status":"connected"}],"plugins":[{"name":"gameforge"}],"agents":["Explore"]}""");
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Skill", input = new { skill = "gameforge:gameforge-studio" } } } } }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "mcp__claude_ai_Gmail__search_threads", input = new { query = "x" } } } } }));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Erledigt." } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s1", num_turns = 2 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("spiele"); w.ClaudeDockToggle("CORE-06"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("Aus deinem Claude: deine Skills", w.Find<TextBlock>("claude-chat-caps").Text);
            await Run(w.ClaudeChatSend("Plan das Spiel"));
            var caps = w.Find<TextBlock>("claude-chat-caps").Text!;
            Assert.Contains("3 Skills", caps); Assert.Contains("claude.ai Gmail", caps); Assert.Contains("gameforge", caps);
            var m = w.ClaudeChatCurrent()!.Messages[^1];
            Assert.Equal(["🧩 Skill gameforge:gameforge-studio", "🔌 Connector Gmail"], m.Used);
            Assert.Contains(m.Steps, st => st.Text == "🧩 nutzt den Skill „gameforge:gameforge-studio“");
            Assert.Contains("gameforge:gameforge-studio", w.Find<TextBlock>("claude-chat-used").Text);
            w.Find<Button>("claude-dock-close").Press(); w.ClaudeDockToggle("CORE-06"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Contains("3 Skills", w.Find<TextBlock>("claude-chat-caps").Text); // gemerkt
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
