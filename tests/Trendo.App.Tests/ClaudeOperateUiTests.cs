using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.39 – Claude kennt und bedient jedes Programm: Seite lesen, Bedien-Plan erst nach „Ausführen“, Claude im Menüband, Rechtsklick „✳ Claude fragen“,
/// Claude auch in Studio-Übersicht, Suche und Fokusmodus. Claude ist nachgebildet.
[Collection("Claude")]
public class ClaudeOperate_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> Fake(List<IReadOnlyList<string>> calls, Func<int, string> answer) => (args, _, line, _) =>
    {
        calls.Add(args);
        var text = answer(calls.Count);
        line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
        return Task.FromResult(0);
    };
    static void End(MainWindow w) { MainWindow.ClaudeStepDelay = 350; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; if (w.FocusMode) w.ToggleFocusMode(); w.Close(); }
    static string Page() => MainWindow.Programs.Keys.First(k => k == "architektur" || MainWindow.Programs[k].Studio == "SPEC-01");

    [AvaloniaFact(DisplayName = "Claude kennt das Programm: offene Seite mit Feldern und Knöpfen geht mit; Claude bedient es erst nach „Ausführen“ – unbekannte Ziele fallen weg")]
    public async Task Knows_and_operates()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeStepDelay = 0; // ohne Pause zwischen den Schritten
        string field = "";
        MainWindow.ClaudeRunOverride = Fake(calls, n => n == 1
            ? $"Ich trage etwas ein und lege ein neues Dokument an.\n[[trendo:feld | {field} | Wohnung Erika Muster]]\n[[trendo:klick | prog-new]]\n[[trendo:klick | gibt-es-nicht]]"
            : "Ich lege noch eins an.\n[[trendo:klick | prog-new]]");
        try
        {
            await Run(w.ClaudeCheck(true));
            var page = Page();
            w.Show(page); var first = w.ProgramNew(page); w.Show(page);
            var state = w.ClaudeReadPage(page)!;
            Assert.True(state.Buttons.Any(b => b.Name == "prog-new"), "Knöpfe: " + string.Join(", ", state.Buttons.Take(15).Select(b => b.Name)));
            Assert.True(state.Fields.Count > 0, "Felder: keine");
            field = state.Fields[0].Name;
            Assert.DoesNotContain(state.Buttons, b => b.Name.StartsWith("claude-") || b.Name.StartsWith("auftrag-"));
            w.ClaudeDockToggle("SPEC-01"); w.Show(page); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (w.Windows.Active?.Id is { } wid && w.ClaudeDockIsMax(wid)) w.Find<Button>("claude-dock-max").Press();
            w.Show(page);
            await Run(w.ClaudeChatSend("Leg ein neues Dokument an"));
            Assert.Contains("[Offenes Programm:", calls[0][1]); Assert.Contains("prog-new", calls[0][1]);
            Assert.True(w.ClaudePlanPending());
            Assert.NotNull(w.TryFind<Border>("claude-plan")); Assert.NotNull(w.TryFind<TextBlock>("claude-plan-step-1")); Assert.Null(w.TryFind<TextBlock>("claude-plan-step-2")); // unbekanntes Ziel fällt weg
            var projectDoc = w.ProgramActive(page)!; Assert.Equal(w.ClaudeChatCurrent()!.Doc, projectDoc.Path); // Claude arbeitet im Dokument des Projekts – noch nichts ausgeführt
            w.Find<Button>("claude-plan-run").Press(); await Run(Task.Delay(100));
            Assert.True(!ReferenceEquals(projectDoc, w.ProgramActive(page)), w.ClaudeChatCurrent()!.Messages[^1].Text); // neues Dokument angelegt
            Assert.Contains("„Wohnung Erika Muster“", w.ClaudeChatCurrent()!.Messages[^1].Text); // Feld ausgefüllt
            Assert.Contains("Im Programm erledigt", w.ClaudeChatCurrent()!.Messages[^1].Text);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.bedient");

            await Run(w.ClaudeChatSend("Noch eins")); // Claude arbeitet wieder im Dokument des Projekts
            var now = w.ProgramActive(page);
            Assert.Equal(w.ClaudeChatCurrent()!.Doc, now!.Path);
            w.Find<Button>("claude-plan-no").Press();
            Assert.Same(now, w.ProgramActive(page)); Assert.False(w.ClaudePlanPending()); // „Nein“ – nichts geändert
            Assert.Contains("Nicht ausgeführt", w.ClaudeChatCurrent()!.Messages[^1].Text);
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Claude im Menüband jedes Fachprogramms und Rechtsklick „Claude fragen“ auf Elemente der Seite")]
    public async Task Ribbon_and_context()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.ClaudeStatusOverride = St; MainWindow.ClaudeRunOverride = Fake([], _ => "Ok.");
        try
        {
            await Run(w.ClaudeCheck(true));
            var page = Page();
            w.Show(page); w.ProgramNew(page); w.Show(page);
            Assert.NotNull(w.TryFind<Button>("ki-menu")); Assert.NotNull(w.TryFind<Button>("auftrag-claude")); Assert.NotNull(w.TryFind<Button>("auftrag-claude-markierung")); // E1 (T-0616): beides im Menü „KI ▾“
            Assert.Null(w.TryFind<Button>("prog-claude")); // kein zweites „Claude“ im Programmband
            // Rechtsklick auf einen Text der Seite
            var tb = w.ActivePageRootForTests!.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text is { Length: > 3 } && t.FindLogicalAncestorOfType<Button>() is null && !t.GetLogicalAncestors().OfType<Control>().Any(a => a.Name == "auftrag-leiste"));
            var args = new Avalonia.Input.ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent };
            w.UpdateLayout(); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); // wie im echten Fenster: Seite angeordnet, Text hängt im Fenster
            var seen = 0; w.AddHandler(Control.ContextRequestedEvent, (_, _) => seen++, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
            tb.RaiseEvent(args);
            Assert.True(seen > 0, "Ereignis kam nicht am Fenster an");
            // eigenes kleines Menü – oder in ein vorhandenes Menü der Seite eingefügt
            var injected = tb.GetSelfAndLogicalAncestors().OfType<Control>().Any(c => c.ContextMenu?.Items.OfType<MenuItem>().Any(m => m.Name == "claude-context-ask") == true || (c.ContextFlyout as MenuFlyout)?.Items.OfType<MenuItem>().Any(m => m.Name == "claude-context-ask") == true);
            Assert.True(args.Handled || injected, $"Text „{tb.Text}“, Vorfahren: " + string.Join(" > ", tb.GetLogicalAncestors().OfType<Control>().Take(8).Select(c => c.GetType().Name + (c.Name is { } n ? "#" + n : ""))));
            Assert.True(w.ClaudeContextAsk("Wohnzimmer 24 m²"));
            Assert.StartsWith("Zu „Wohnzimmer 24 m²“", w.Find<TextBox>("claude-chat-input").Text);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.rechtsklick");
            w.ClaudeDockToggle(null); w.Show(page);
            w.Find<Button>("auftrag-claude").Press();
            Assert.NotNull(w.TryFind<TextBox>("claude-chat-input")); // aus dem Menü „KI ▾“ geöffnet
        }
        finally { End(w); }
    }

    [AvaloniaFact(DisplayName = "Claude auch in der Studio-Übersicht, in der Suche und im Fokusmodus")]
    public void Overview_search_focus()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("studios"); Assert.NotNull(w.TryFind<Button>("auftrag-claude"));
            w.Show("suche"); Assert.NotNull(w.TryFind<Button>("auftrag-claude"));
            w.ToggleFocusMode(); w.Show("tabelle");
            Assert.NotNull(w.TryFind<Button>("auftrag-claude")); Assert.Null(w.TryFind<TextBox>("auftrag-prompt")); // Fokusmodus: nur der schlanke Claude-Knopf
        }
        finally { End(w); }
    }
}
