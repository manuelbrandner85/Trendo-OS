using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.PromptAction;

namespace Trendo.App;

/// <summary>Universal Studio Intelligence: Fachwissen, Programmprüfung und Projektkontext für Claude, Aufgaben auf der Startseite.</summary>
public partial class MainWindow
{
    IReadOnlyList<string> _mcpConnected = [];
    /// <summary>Für Tests: Startprüfung eines Programms nachstellen.</summary>
    public Func<string, string, bool?>? ProgramStartsOverride { get; set; }

    static readonly string[] MemoryKinds = ["Entscheidung", "Fortschritt", "Schritt", "Problem", "Qualität", "Freigabe", "Programm", "Datei"];

    /// <summary>Prüft, was wirklich da ist. Nichts wird installiert, Unreal wird nicht gestartet.</summary>
    public IReadOnlyList<ProgramCheck> ProgramChecks(IEnumerable<string>? only = null)
    {
        var wanted = only?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = EngineCheck();
        return found.Where(e => wanted is null || wanted.Contains(e.Id)).Select(e =>
        {
            var starts = e.Installed && e.Exe is { } exe ? BridgeStarts(e.Id, exe) : (e.Installed ? null : false);
            var note = !e.Installed ? e.Hint : starts is null ? "Vorhanden. Ob es startet, zeigt erst ein echter Lauf." : starts == true ? (e.Version is { Length: > 0 } v ? "Version " + v : "Startet") : "Gefunden, startet aber nicht.";
            return new ProgramCheck(e.Id, e.Name, e.Installed, e.Installed ? starts : false, ProgramCheck.InterfaceOf(e.Id, e.Installed, e.Installed ? starts : false, _mcpConnected),
                ProgramCheck.KnownActions.TryGetValue(e.Id, out var a) ? a : [], "Claude darf es nur im Rahmen des Auftrags sichtbar starten. Installieren braucht deine Zustimmung.", note);
        }).ToList();
    }

    bool? BridgeStarts(string id, string exe)
    {
        if (ProgramStartsOverride is { } o) return o(id, exe);
        if (_opt.Isolated) return null;
        var args = id switch { "blender" or "godot" => "--version", "unity" => "-version", _ => null };
        if (args is null) return null; // Unreal kennt keinen schnellen Prüfaufruf
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p is null) return false;
            p.OutputDataReceived += (_, _) => { }; p.ErrorDataReceived += (_, _) => { }; p.BeginOutputReadLine(); p.BeginErrorReadLine();
            if (p.WaitForExit(20000)) return p.ExitCode == 0;
            p.Kill(true); return false;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { return false; }
    }

    IEnumerable<McpTool> StudioIntelTools(BridgeRun run)
    {
        McpTool S(string name, string text, JsonObject schema, Func<JsonObject, McpResult> body) => new(name, text, schema, a => BridgeUi(name, x => Task.FromResult(body(x)), a));
        yield return S("studio_wissen", "Fachwissen eines Studios: Zweck, Aufgaben, Programme, Ein- und Ausgaben, Formate, Abläufe, Qualitätsregeln, helfende Studios, Grenzen. Mit auftrag zeigt es die passenden Aufgaben.", TrendoMcp.Schema(("studio", "string", "Studio-Kennung, ohne Angabe das aktuelle", false), ("auftrag", "string", "Wunsch des Nutzers", false)), a =>
        {
            var id = a["studio"].AsText() ?? BridgeStudio(run);
            return StudioKnowledgeBase.For(id) is { } k ? new McpResult(StudioKnowledgeBase.ToJson(k, a["auftrag"].AsText()).ToJsonString()) : McpResult.Fail("Dieses Studio ist nicht bekannt.");
        });
        yield return S("programme_pruefen", "Prüft echte Fachprogramme auf diesem PC: installiert, startbar, Schnittstelle, Aktionen, Rechte. Fehlt etwas, sage es dem Nutzer und tu nicht so, als wäre es da.", TrendoMcp.Schema(("programme", "string", "Kennungen mit Komma, ohne Angabe die des Studios", false)), a =>
        {
            var list = a["programme"].AsText()?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<string>? only = list ?? (StudioKnowledgeBase.For(BridgeStudio(run)) is { Programs.Count: > 0 } k ? k.Programs : null);
            return new McpResult(new JsonArray(ProgramChecks(only).Select(c => (JsonNode)new JsonObject
            {
                ["programm"] = c.Id, ["name"] = c.Name, ["installiert"] = c.Installed, ["startet"] = c.Starts, ["schnittstelle"] = c.Interface,
                ["aktionen"] = new JsonArray(c.Actions.Select(x => (JsonNode)x).ToArray()), ["rechte"] = c.Rights, ["hinweis"] = c.Note,
            }).ToArray()).ToJsonString());
        });
        yield return S("projekt_kontext", "Stand des Projekts: Studio, Dokument, Fortschritt, Dateien, Gedächtnis mit früheren Entscheidungen, Rechte. Zu Beginn jeder Arbeit lesen.", TrendoMcp.Schema(), _ =>
        {
            var doc = BridgeDoc(run); var key = BridgeKey(run);
            var files = new JsonArray();
            if (Directory.Exists(run.Folder))
                foreach (var f in Directory.EnumerateFiles(run.Folder, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".trendo" + Path.DirectorySeparatorChar)).Take(200))
                    files.Add(Path.GetRelativePath(run.Folder, f));
            return new McpResult(new JsonObject
            {
                ["studio"] = BridgeStudio(run), ["seite"] = CurrentView, ["projektordner"] = run.Folder,
                ["dokument"] = doc is null ? null : new JsonObject { ["titel"] = doc.Title, ["datei"] = doc.Path, ["ungespeichert"] = doc.Dirty, ["kann_zuruecknehmen"] = doc.CanUndo, ["status"] = key is null ? null : Programs[key].Status?.Invoke(doc) },
                ["dateien"] = files,
                ["gedaechtnis"] = new JsonArray((run.ChatId is { } c ? CMemory.Items(c) : []).Select(x => (JsonNode)x).ToArray()),
                ["not_stopp"] = _core.Approvals.Halted,
            }.ToJsonString());
        });
        yield return S("projekt_merken", "Hält etwas für dieses Projekt fest. Der Nutzer kann es sehen, ändern und löschen. Keine privaten Daten.", TrendoMcp.Schema(("art", "string", string.Join(", ", MemoryKinds), true), ("text", "string", "Kurz und überprüfbar", true)), a =>
        {
            var kind = Need(a, "art");
            if (!MemoryKinds.Contains(kind, StringComparer.OrdinalIgnoreCase)) return McpResult.Fail("Erlaubt sind: " + string.Join(", ", MemoryKinds));
            if (run.ChatId is not { } chat) return McpResult.Fail("Dieser Lauf gehört zu keinem Projekt.");
            return CMemory.Add(chat, $"{kind}: {Need(a, "text")}") ? new McpResult("Gemerkt.") : McpResult.Fail("Nicht gemerkt: Schon vorhanden oder das Gedächtnis ist voll.");
        });
    }

    string? BridgeStudio(BridgeRun run) => run.Dock?.Studio ?? (BridgeKey(run) is { } k ? Programs[k].Studio : ClaudeBelongs(CurrentView) ? ClaudeStudioOf(CurrentView) : null);

    /// <summary>Startseite: „Was möchtest du machen?“ mit den wichtigsten Aufgaben des Studios. Der Klick füllt die Eingabe oben, die Person ergänzt nur noch das Thema.</summary>
    Control? StudioTaskCard(string studio)
    {
        if (StudioKnowledgeBase.For(studio) is not { Detailed: true } k) return null;
        var row = new WrapPanel { Name = "studio-tasks" };
        foreach (var t in k.Tasks.Take(5))
        {
            var task = t;
            var b = new Button { Name = "studio-task-" + task.Id, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(14, 10), Width = 230, CornerRadius = new CornerRadius(10), HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel { Spacing = 3, Children = {
                    new TextBlock { Text = task.Label, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Text, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = task.Hint, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => StudioTaskStart(task);
            row.Children.Add(b);
        }
        return Card(new StackPanel { Spacing = 8, Children = { H("Was möchtest du machen?", 17), row } });
    }

    /// <summary>Aufgabe gewählt: Satzanfang in die Eingabe des Studios, Fokus dorthin.</summary>
    public bool StudioTaskStart(StudioTask task)
    {
        var box = (ActivePageRoot ?? this).GetLogicalDescendantsOfType<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt" && t.IsVisible);
        if (box is null) return false;
        box.Text = task.Starter; box.Focus(); box.CaretIndex = task.Starter.Length;
        return true;
    }
}

static class LogicalFind
{
    public static IEnumerable<T> GetLogicalDescendantsOfType<T>(this Control c) where T : Control => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(c).OfType<T>();
}
