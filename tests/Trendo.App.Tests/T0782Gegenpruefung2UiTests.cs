using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Lernen;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0782, 2. Gegenprüfung (App): H1b – kein Claude-Menü, kein „Claude fragen“, kein Claude-Bereich, Brücke lehnt ab; M6-Rest – Positivliste und verdeckte Lösungen in Plan, Meldung, Protokoll.</summary>
[Collection("Claude")]
public class T0782Gegenpruefung2_Ui
{
    const string Tu = MainWindow.TutorKey;

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9"; public int Calls;
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult(reply(messages[^1].Content)); }
    }
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static async Task Until(Func<bool> ok, int ms = 15000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static ProgramDocument Open(MainWindow w)
    {
        w.Width = 1600; w.Height = 1200; w.ProgramSetLevel(UserLevel.Profi); w.Show(Tu); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
        return w.ProgramActive(Tu)!;
    }

    [AvaloniaFact(DisplayName = "H1b: Nachhilfe – kein Claude-Menü (ClaudeBelongs false), kein Claude-Bereich, kein „Claude fragen“ an Fehlern; Brücke lehnt dokument_lesen/setzen, feld_setzen, befehl, klicken ab; Gegenprobe anderes Programm")]
    public async Task H1b_no_claude_in_tutor()
    {
        var (w, _, root) = Ui.Open();
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
        try
        {
            await Run(w.ClaudeCheck(true));
            Assert.True(w.ClaudeBelongs("text")); // Gegenprobe: normales Programm hat Claude
            var d = Open(w);
            Nachhilfe.NaechsteUebung(d, new Random(2));
            Assert.False(w.ClaudeBelongs(Tu));
            w.ClaudeDockToggle("STUDIO-TUTOR-01"); Settle();
            Assert.Null(w.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "claude-dock"));
            Assert.Contains("nur die KI auf diesem PC", w.ProgramMessage(Tu));
            // Fehler mit „Claude fragen“: hier gibt es keinen solchen Knopf
            var leer = Path.Combine(root, "leer.csv"); File.WriteAllText(leer, "");
            BueroAusbau_Ui.Drop(w, "nh-thema-karte", leer);
            Assert.NotNull(w.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "error-card"));
            Assert.Null(w.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "error-fix"));
            // Brücke
            var m = w.ClaudeBridgeForTest(Path.Combine(root, "bruecke"), Tu);
            var antwort = Nachhilfe.Uebung(d)!["antworten"]![0]!.GetValue<string>();
            foreach (var (tool, args) in new (string, JsonObject)[]
            {
                ("dokument_lesen", new JsonObject { ["programm"] = Tu }), ("dokument_lesen", new JsonObject()),
                ("dokument_setzen", new JsonObject { ["programm"] = Tu, ["pfad"] = "sokratisch", ["wert"] = false }),
                ("feld_setzen", new JsonObject { ["ziel"] = "nh-eingabe", ["wert"] = "1" }), ("befehl", new JsonObject { ["programm"] = Tu, ["name"] = "nh-solution" }),
                ("klicken", new JsonObject { ["ziel"] = "nh-loesung" }), ("seite_lesen", new JsonObject()),
            })
            {
                var r = await Call(m, tool, args);
                Assert.True(Failed(r), tool); Assert.Contains("nur mit der KI auf diesem PC", Text(r)); Assert.DoesNotContain(antwort, Text(r));
            }
            Assert.True(d.Flag("sokratisch"));
            Assert.False(Failed(await Call(m, "programme")));
        }
        finally { MainWindow.ClaudeStatusOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "M6-Rest: dokument.setzen nur für Fach, Klasse, Thema (Positivliste); Plan, Meldung und Protokoll zeigen keine Lösung")]
    public async Task M6_allowlist_and_hidden_answers()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var reply = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "dokument.setzen", ["args"] = new JsonObject { ["pfad"] = "notiz", ["wert"] = "x" } }) }.ToJsonString();
        var ki = new FixedKi(_ => reply);
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = Open(w);
            var before = d.Data.ToJsonString();
            w.Find<TextBox>("auftrag-prompt").Text = "Schreib bitte eine Notiz in das Dokument hinein";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > 0 && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            Assert.Equal("fehler", w.AssistLast!.Value.Phase); Assert.Contains("erlaubt sind nur", w.AssistLast.Value.Message);
            Assert.Equal(before, d.Data.ToJsonString());
            // erlaubtes Feld
            reply = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "dokument.setzen", ["args"] = new JsonObject { ["pfad"] = "klasse", ["wert"] = "6" } }) }.ToJsonString();
            var c0 = ki.Calls;
            w.Find<TextBox>("auftrag-prompt").Text = "Stell bitte die Klasse auf sechs um, danke";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > c0 && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            Assert.Equal("vorschau", w.AssistLast!.Value.Phase);
            Tap(w, "assist-discard");
            // Plan einer neuen Übung: Antwort, Lösungsweg und Hinweis sind verdeckt
            reply = Plan("uebung.erzeugen", new JsonObject { ["frage"] = "Welcher Vogel ist klein und bunt?", ["antwort"] = "Zebrafink", ["erklaerung"] = "Der Zebrafink ist klein.", ["hinweis"] = "Er hat Streifen." });
            var c1 = ki.Calls;
            w.Find<TextBox>("auftrag-prompt").Text = "Gib mir eine neue Übung über Vögel bitte jetzt";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > c1 && w.AssistLast?.Phase is "vorschau" or "fehler");
            var panel = w.GetLogicalDescendants().OfType<StackPanel>().First(s => s.Name == "assist-panel");
            var all = string.Join("\n", panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("Welcher Vogel", all); Assert.DoesNotContain("Zebrafink", all); Assert.DoesNotContain("Streifen", all); Assert.Contains("(verdeckt)", all);
            Tap(w, "assist-apply"); await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            // KI-Antwort, die die Lösung der offenen Übung nennt → im Plan-Text ausgeblendet; Protokoll ohne Lösung
            reply = "Die Lösung ist Zebrafink. " + Plan("uebung.erzeugen", new JsonObject { ["frage"] = "Zebrafink oder Spatz – welcher ist bunter, Zebrafink?", ["antwort"] = "Zebrafink" });
            var c2 = ki.Calls;
            w.Find<TextBox>("auftrag-prompt").Text = "Gib mir bitte noch eine neue Übung über bunte Vögel jetzt";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > c2 && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            panel = w.GetLogicalDescendants().OfType<StackPanel>().First(s => s.Name == "assist-panel");
            all = string.Join("\n", panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.DoesNotContain("Zebrafink", all);
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Detail.Contains("Zebrafink"));
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }
}
