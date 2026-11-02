using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.39 – Claude arbeitet direkt im Programm des Studios: neues Projekt → Dokument im Programm (benannt wie das Projekt, im Projektordner), Programm wird gezeigt,
/// Claude ändert das Dokument live; Projekt fortsetzen öffnet sein Dokument wieder. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeProgramDoc_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    /// <summary>Claude schreibt in das Studio-Dokument im Projektordner (falls es eins gibt) einen Wert und antwortet.</summary>
    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls) => (args, dir, line, _) =>
    {
        calls.Add(args);
        line(J(new { type = "system", subtype = "init", session_id = "s" + calls.Count, model = "claude" }));
        if (Directory.GetFiles(dir, "*.tdoc").FirstOrDefault() is { } tdoc)
        {
            var o = JsonNode.Parse(File.ReadAllText(tdoc))!.AsObject();
            o["data"]!.AsObject()["claude"] = "Runde " + calls.Count;
            var content = o.ToJsonString();
            line(J(new { type = "assistant", message = new { content = new object[] { new { type = "tool_use", name = "Write", input = new { file_path = tdoc, content } } } } }));
            File.WriteAllText(tdoc, content);
            line(J(new { type = "user", message = new { content = new object[] { new { type = "tool_result", content = "ok", is_error = false } } } }));
        }
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Erledigt." } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = "Erledigt.", session_id = "s" + calls.Count, num_turns = 1 }));
        return Task.FromResult(0);
    };

    [AvaloniaFact(DisplayName = "Claude im Programm: neues Projekt im Spiele-Studio legt ein Dokument in „Spiele bauen“ an, zeigt das Programm, Claude arbeitet darin; Fortsetzen öffnet es wieder")]
    public async Task Game_project_in_program()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls);
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show(w.PageForStudio("CORE-06") ?? "spiele"); w.ClaudeDockToggle("CORE-06"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue ein Jump and Run mit einem Fuchs"));
            var chat = w.ClaudeChatCurrent()!;
            Assert.Equal("spiele-programm", w.CurrentView); // das Programm ist offen
            var doc = w.ProgramActive("spiele-programm")!;
            Assert.Equal(chat.Title, doc.Title); Assert.Equal(chat.Doc, doc.Path);
            Assert.StartsWith(chat.Folder, doc.Path!); Assert.True(File.Exists(doc.Path));
            Assert.Equal("Runde 1", (string?)doc.Data["claude"]); // Claude hat im Programm-Dokument gearbeitet
            var sys = calls[0].ToList(); var system = sys[sys.IndexOf("--append-system-prompt") + 1];
            Assert.Contains("Programm „Spiele bauen“", system); Assert.Contains("index.html", system); // Dokument und ergänzend spielbarer Prototyp
            Assert.NotNull(w.TryFind<TextBox>("claude-chat-input")); // Claude ist neben dem Programm offen
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.programm.dokument");

            // zweites Projekt: eigenes Dokument
            w.Find<Button>("claude-chat-new").Press();
            await Run(w.ClaudeChatSend("Ein Rennspiel mit Seifenkisten"));
            var second = w.ProgramActive("spiele-programm")!;
            Assert.NotSame(doc, second); Assert.Equal(w.ClaudeChatCurrent()!.Title, second.Title);
            // erstes Projekt fortsetzen: sein Dokument kommt zurück
            w.ClaudeContinue("CORE-06", chat.Id);
            Assert.Same(doc, w.ProgramActive("spiele-programm"));
            Assert.Equal(("spiele-programm", chat.Doc), w.ClaudeProjectProgram());
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Claude im Programm: Studio ohne eigenes Programm (Webseiten) bleibt auf seiner Seite, kein Dokument")]
    public async Task Studio_without_program()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake(calls);
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue eine Startseite"));
            Assert.Equal("dev", w.CurrentView); Assert.Null(w.ClaudeChatCurrent()!.Doc);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
