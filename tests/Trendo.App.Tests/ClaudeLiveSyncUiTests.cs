using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.44 – Alles live mitverfolgen: Kern der Live-Wiedergabe, Wände erscheinen nacheinander (ein Rückgängig-Schritt), Änderungen ohne Schreibwerkzeug kommen trotzdem live an,
/// Trendo Text öffnet den Text noch während Claude arbeitet. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeLiveSync_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    static JsonObject W(int i) => new() { ["x1"] = i, ["y1"] = 0, ["x2"] = i + 1, ["y2"] = 0 };

    [Fact(DisplayName = "Live-Wiedergabe: neue Elemente auch in verschachtelten Listen, je Zwischenstand eins mehr; geänderte Werte gelten sofort; nichts Neues → keine Zwischenstände")]
    public void Replay_steps()
    {
        var before = JsonNode.Parse("""{"name":"alt","plan":{"walls":[{"a":1}],"rooms":[]}}""")!.AsObject();
        var after = JsonNode.Parse("""{"name":"neu","plan":{"walls":[{"a":1},{"a":2},{"a":3}],"rooms":[{"r":1}]}}""")!.AsObject();
        var s = LiveReplay.Steps(before, after);
        Assert.Equal(2, s.Count); // 3 neue Elemente → 2 Zwischenstände + Endstand
        Assert.Equal("neu", (string?)s[0]["name"]);
        Assert.Equal(2, s[0]["plan"]!["walls"]!.AsArray().Count); Assert.Empty(s[0]["plan"]!["rooms"]!.AsArray());
        Assert.Equal(3, s[1]["plan"]!["walls"]!.AsArray().Count);
        Assert.Empty(LiveReplay.Steps(after, after));
        Assert.Equal(3, LiveReplay.Steps(new JsonObject(), after).Count); // leeres Dokument: neu angelegter Plan erscheint auch nacheinander
        Assert.Empty(LiveReplay.Steps(before, JsonNode.Parse("""{"name":"x","plan":{"walls":[{"a":9}],"rooms":[]}}""")!.AsObject())); // nur geändert, nichts hinzugekommen
        var many = new JsonObject { ["l"] = new JsonArray(Enumerable.Range(0, 200).Select(i => (JsonNode?)i).ToArray()) };
        Assert.True(LiveReplay.Steps(new JsonObject { ["l"] = new JsonArray() }, many, 60).Count <= 60);
    }

    [AvaloniaFact(DisplayName = "Grundriss live: Claude setzt die Wände einer 2-Raum-Wohnung auf einmal – sie erscheinen nacheinander im Programm, Rückgängig ist ein Schritt")]
    public async Task Walls_appear_one_by_one()
    {
        var (w, _, _) = Ui.Open();
        var before = MainWindow.LiveStepDelay; MainWindow.LiveStepDelay = 60;
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = (args, dir, line, _) =>
        {
            var tdoc = Directory.GetFiles(dir, "*.tdoc").Single(); var o = JsonNode.Parse(File.ReadAllText(tdoc))!.AsObject();
            var data = o["data"]!.AsObject(); var plan = data["plan"] as JsonObject ?? new JsonObject(); data["plan"] = plan;
            var walls = plan["walls"] as JsonArray ?? new JsonArray(); plan["walls"] = walls;
            for (var i = 0; i < 6; i++) walls.Add(W(i));
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = tdoc, content = "…" } } } } }));
            File.WriteAllText(tdoc, o.ToJsonString());
            line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
            line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig.", session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            var page = MainWindow.Programs.Keys.First(k => k == "architektur" || MainWindow.Programs[k].Studio == "SPEC-01");
            w.Show(page); w.ClaudeDockToggle("SPEC-01"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var seen = new HashSet<int>(); var stepsBefore = MainWindow.LiveSteps;
            var send = w.ClaudeChatSend("Zeichne eine 2-Raum-Wohnung");
            for (var i = 0; i < 300 && (!send.IsCompleted || w.ClaudeLiveAnimating(page)); i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (w.ProgramActive(page)?.Data["plan"]?["walls"] is JsonArray a) seen.Add(a.Count);
                await Task.Delay(15);
            }
            await send;
            var doc = w.ProgramActive(page)!;
            Assert.Equal(6, doc.Data["plan"]!["walls"]!.AsArray().Count);
            Assert.True(MainWindow.LiveSteps - stepsBefore >= 3, "Zwischenstände: " + (MainWindow.LiveSteps - stepsBefore)); // nacheinander, nicht nur 0 → 6
            Assert.True(doc.Undo()); Assert.Equal(0, (doc.Data["plan"]?["walls"] as JsonArray)?.Count ?? 0); // ein Schritt zurück
        }
        finally { MainWindow.LiveStepDelay = before; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Live ohne Schreibwerkzeug (z. B. per Befehl): Trendo sieht die Änderung im Projektordner und zeigt sie noch während Claude arbeitet – auch in Trendo Text")]
    public async Task Changes_without_tool_events()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = St;
        var liveBeforeResult = false;
        MainWindow.ClaudeRunOverride = async (args, dir, line, _) =>
        {
            var start = MainWindow.LiveSyncs;
            File.WriteAllText(Path.Combine(dir, "einladung.md"), "# Einladung\n\nZum Sommerfest am Samstag."); // kein Werkzeug-Ereignis
            for (var i = 0; i < 100 && MainWindow.LiveSyncs == start; i++) await Task.Delay(30);
            liveBeforeResult = MainWindow.LiveSyncs > start;
            line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig.", session_id = "s1", num_turns = 1 }));
            return 0;
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("text"); w.ClaudeDockToggle("SYS-10"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Schreib eine Einladung zum Sommerfest"));
            Assert.True(liveBeforeResult); // schon während der Arbeit im Programm
            Assert.Contains("Sommerfest", string.Join(" ", w.TextDocument.Blocks.OfType<Trendo.Core.Office.Para>().Select(b => b.Text))); // Text ist in Trendo Text offen
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
