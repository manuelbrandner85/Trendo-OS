using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.37 „besser als Claude“: schöne Antworten (Tabellen, Code mit Kopieren), Projekt-Gedächtnis, Zeitreise, Gutachter mit Note,
/// echt testen bis es läuft und Claude schaut hin, freihändig sprechen. Claude, Browser, Mikrofon und Vorlesen sind nachgebildet.
[Collection("Claude")]
public class ClaudeExtras_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    static string Arg(IReadOnlyList<string> a, string name) { var l = a.ToList(); return l[l.IndexOf(name) + 1]; }

    /// <summary>Nachgebildetes Claude Code: schreibt (wenn angegeben) index.html und antwortet.</summary>
    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<int, (string? Html, string Answer)> step) => (args, dir, line, _) =>
    {
        calls.Add(args);
        var (html, answer) = step(calls.Count);
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        if (html is not null)
        {
            var path = Path.Combine(dir, "index.html");
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = path, content = html } } } } }));
            File.WriteAllText(path, html);
            line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
        }
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = answer } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = answer, session_id = "s1", num_turns = 1 }));
        return Task.FromResult(0);
    };

    static async Task<MainWindow> Start(List<IReadOnlyList<string>> calls, Func<int, (string? Html, string Answer)> step)
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls, step);
        await Run(w.ClaudeCheck(true));
        w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return w;
    }
    static void End(MainWindow w) { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ProbeOverride = null; MainWindow.ReviewOverride = null; MainWindow.DictateOverride = null; MainWindow.SpeakOverride = null; w.Close(); }
    static async Task Send(MainWindow w, string text) { await Run(w.ClaudeChatSend(text)); while (w.ClaudeChatRunning()) await Run(Task.Delay(20)); }

    [AvaloniaFact(DisplayName = "Schöne Antworten: Überschrift, Tabelle als Raster, Code mit Farben und „Kopieren“; Glas-Design im Chat")]
    public async Task Rich_answers()
    {
        var calls = new List<IReadOnlyList<string>>();
        var w = await Start(calls, _ => (null, "## Preise\n\n| Artikel | Preis |\n|---|---|\n| Brot | 2,50 € |\n\n```js\nconst preis = 2.5; // Euro\n```\n\nMehr unter [Doku](https://example.org)."));
        try
        {
            await Send(w, "Zeig mir eine Preisliste");
            Assert.NotNull(w.TryFind<Grid>("claude-md-table"));
            var texts = string.Join("|", w.GetLogicalTexts());
            Assert.Contains("Preise", texts); Assert.DoesNotContain("## Preise", texts); Assert.DoesNotContain("|---|", texts);
            var copy = w.GetLogicalDescendants().OfType<Button>().First(b => b.Name?.StartsWith("claude-code-copy-") == true);
            copy.Press(); await Run(Task.Delay(30));
            Assert.Equal("const preis = 2.5; // Euro", w.LastClipboardText);
            Assert.IsType<Avalonia.Media.LinearGradientBrush>(w.Find<Border>("claude-code").BorderBrush); // Lichtkante Gold-Blau
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Projekt-Gedächtnis: Claude merkt sich „Merken:“-Zeilen, sie gehen bei jeder Nachricht mit; ansehen, ergänzen, vergessen")]
    public async Task Project_memory()
    {
        var calls = new List<IReadOnlyList<string>>();
        var w = await Start(calls, n => (null, n == 1 ? "Alles klar.\nMerken: Farben sind Dunkelblau und Gold" : "Gern."));
        try
        {
            await Send(w, "Meine Farben sind immer Dunkelblau und Gold");
            Assert.Equal(["Farben sind Dunkelblau und Gold"], w.ClaudeMemoryItems());
            Assert.DoesNotContain("Merken:", w.ClaudeChatCurrent()!.Messages[^1].Text);
            Assert.Contains(w.ClaudeChatCurrent()!.Messages[^1].Steps, s => s.Text.StartsWith("Gemerkt"));
            await Send(w, "Mach eine Kopfzeile");
            Assert.Contains("- Farben sind Dunkelblau und Gold", Arg(calls[1], "--append-system-prompt"));
            w.Find<Button>("claude-chat-memory").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            w.Find<TextBox>("claude-memory-input").Text = "Schrift: Inter"; w.Find<Button>("claude-memory-add").Press();
            Assert.Equal(2, w.ClaudeMemoryItems().Count);
            w.Find<Button>("claude-memory-del-0").Press();
            Assert.Equal(["Schrift: Inter"], w.ClaudeMemoryItems());
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Zeitreise: jede Antwort mit Änderungen wird ein Versionspunkt; zurück auf einen älteren Stand und wieder vor")]
    public async Task Time_travel()
    {
        var calls = new List<IReadOnlyList<string>>();
        var w = await Start(calls, n => (ClaudeChat_Ui.Page($"<h1>Version {n}</h1>"), "Fertig."));
        try
        {
            await Send(w, "Baue eine Startseite"); await Send(w, "Mach die Überschrift größer");
            var pts = w.ClaudeTimelinePoints();
            Assert.Equal(["Mach die Überschrift größer", "Baue eine Startseite"], pts.Select(p => p.Label));
            var index = Path.Combine(w.ClaudeChatCurrent()!.Folder, "index.html");
            w.Find<Button>("claude-chat-timeline").Press(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            w.Find<Button>("claude-timeline-go-0").Press(); // vor „größer“
            Assert.Contains("Version 1", File.ReadAllText(index));
            Assert.Contains("Zeitreise", w.ClaudeChatCurrent()!.Messages[^1].Text);
            Assert.Equal("Vor der Zeitreise", w.ClaudeTimelinePoints()[0].Label);
            w.ClaudeTravel(0); // wieder vor
            Assert.Contains("Version 2", File.ReadAllText(index));
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Gutachter: zweite Meinung mit Note, ändert nichts; „Verbessern lassen“ gibt die Punkte an Claude")]
    public async Task Reviewer()
    {
        var calls = new List<IReadOnlyList<string>>(); string? asked = null;
        var w = await Start(calls, n => (ClaudeChat_Ui.Page("<h1>Hallo</h1>"), "Fertig."));
        MainWindow.ReviewOverride = p => { asked = p; return Task.FromResult("Note: 6/10\nStärken:\n- klar\nSchwächen:\n- zu blass\nVerbesserungen:\n- kräftigere Farben"); };
        try
        {
            await Send(w, "Baue eine Startseite");
            w.Find<Button>("claude-chat-review").Press(); while (w.ClaudeChatCurrent()!.Messages[^1].Role != "review") await Run(Task.Delay(20));
            Assert.Contains("„Baue eine Startseite“", asked);
            Assert.Equal("6/10", w.Find<TextBlock>("claude-review-grade").Text);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.gutachten" && a.Detail!.Contains("Note 6"));
            w.Find<Button>("claude-review-improve").Press(); while (calls.Count < 2 || w.ClaudeChatRunning()) await Run(Task.Delay(20));
            Assert.StartsWith("Ein Gutachter hat", calls[1][1]); Assert.Contains("kräftigere Farben", calls[1][1]);
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Echt testen bis es läuft: Skriptfehler gehen zurück an Claude; ohne Fehler sieht Claude das Bildschirmfoto an; abschaltbar")]
    public async Task Probe_and_look()
    {
        var calls = new List<IReadOnlyList<string>>(); var probes = 0;
        var w = await Start(calls, n => (ClaudeChat_Ui.Page($"<h1>V{n}</h1>"), n == 4 ? "Passt." : "Fertig."));
        MainWindow.ProbeOverride = (dir, files) =>
        {
            probes++;
            Directory.CreateDirectory(Path.Combine(dir, ".trendo")); File.WriteAllBytes(Path.Combine(dir, ".trendo", "ansicht.png"), [1, 2, 3]); // kein echtes Bild – die Karte zeigt es dann nicht
            return Task.FromResult(new ClaudeProbe.Result(probes == 1 ? ["Skriptfehler in index.html Zeile 9: start is not defined"] : [], ClaudeProbe.Shot));
        };
        try
        {
            await Send(w, "Baue ein Spiel");
            Assert.Equal(2, calls.Count); // Fehler → Nachbesserung; danach läuft es (im 2. Durchgang wird nicht mehr extra hingeschaut)
            Assert.Contains("start is not defined", calls[1][1]); Assert.Contains(ClaudeProbe.Shot, calls[1][1]);
            Assert.Equal([ClaudeProbe.Shot], w.ClaudeChatCurrent()!.Messages[2].Attachments);
            Assert.NotNull(w.TryFind<Border>("claude-chat-check"));

            await Send(w, "Mach den Himmel blau"); // ohne Fehler: Claude sieht sich das Bildschirmfoto an
            Assert.Equal(4, calls.Count); Assert.Contains("Sieh dir das Bildschirmfoto", calls[3][1]);
            Assert.Equal("Passt.", w.ClaudeChatCurrent()!.Messages[^1].Text);

            KiSourceSettings.Update(w.Core.DataRoot, x => x.ClaudeLooks = false);
            var before = probes; await Send(w, "Noch ein Stern");
            Assert.Equal(5, calls.Count); Assert.Equal(before, probes); // abgeschaltet: kein Start, kein Hinschauen
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Freihändig sprechen: zuhören, abschicken, Antwort vorlesen, bis man es ausschaltet")]
    public async Task Hands_free()
    {
        var calls = new List<IReadOnlyList<string>>(); var spoken = new List<string>(); var listens = 0;
        var w = await Start(calls, _ => (null, "## Erledigt\n\nDie **Startseite** steht."));
        MainWindow.SpeakOverride = spoken.Add;
        MainWindow.DictateOverride = async () =>
        {
            if (++listens == 1) return "Baue eine Startseite";
            await w.ClaudeHandsFreeToggle(); return ""; // beim zweiten Zuhören schaltet man es aus
        };
        try
        {
            await Run(w.ClaudeHandsFreeToggle());
            Assert.False(w.ClaudeHandsFree());
            Assert.Single(calls); Assert.StartsWith("Baue eine Startseite", calls[0][1]);
            Assert.Equal(["Erledigt\nDie Startseite steht."], spoken); // ohne Markdown-Zeichen vorgelesen
            Assert.Equal("Freihändig", w.Find<Button>("claude-tool-freihaendig").Content);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.freihaendig.an");
        }
        finally { End(w); }
    }
}
