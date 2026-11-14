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
    /// <summary>Für Tests: Programmdatei außerhalb der Spiele-Engines finden.</summary>
    public Func<string, string?>? ProgramFindOverride { get; set; }

    static readonly string[] MemoryKinds = ["Entscheidung", "Fortschritt", "Schritt", "Problem", "Qualität", "Freigabe", "Programm", "Datei"];

    /// <summary>Prüft, was wirklich da ist. Nichts wird installiert, Unreal wird nicht gestartet.</summary>
    public IReadOnlyList<ProgramCheck> ProgramChecks(IEnumerable<string>? only = null)
    {
        var wanted = only?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = EngineCheck();
        foreach (var id in wanted?.Where(w => found.All(e => !e.Id.Equals(w, StringComparison.OrdinalIgnoreCase))) ?? [])
        {
            var exe = ProgramFindOverride is { } find ? find(id) : _opt.Isolated ? null : ToolPath(id);
            var installed = exe is not null && (ProgramFindOverride is not null || File.Exists(exe));
            found.Add(new EngineFound(id, DependencyCatalog.All.FirstOrDefault(c => c.Id == id)?.Name ?? id, null, exe, installed, installed ? "vorhanden" : "fehlt", "Dieses Programm fehlt. Es lässt sich unter „Programme einrichten“ hinzufügen."));
        }
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
        var args = id switch { "blender" or "godot" or "git" or "f3d" or "qpdf" or "tesseract" => "--version", "unity" or "ffmpeg" => "-version", _ => null };
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
        McpTool S(string name, string text, JsonObject schema, Func<JsonObject, McpResult> body) => new(name, text, schema, a => BridgeUi(run, name, x => Task.FromResult(body(x)), a));
        yield return S("studio_wissen", "Fachwissen eines Studios: Zweck, Aufgaben, Programme, Ein- und Ausgaben, Formate, Abläufe, Qualitätsregeln, helfende Studios, Grenzen. Mit auftrag zeigt es die passenden Aufgaben.", TrendoMcp.Schema(("studio", "string", "Studio-Kennung, ohne Angabe das aktuelle", false), ("auftrag", "string", "Wunsch des Nutzers", false)), a =>
        {
            var id = a["studio"].AsText() ?? BridgeStudio(run);
            return StudioKnowledgeBase.For(id) is { } k ? new McpResult(StudioKnowledgeBase.ToJson(k, a["auftrag"].AsText()).ToJsonString()) : McpResult.Fail("Dieses Studio ist nicht bekannt.");
        });
        yield return new("programme_pruefen", "Prüft echte Fachprogramme auf diesem PC: installiert, startbar, Schnittstelle, Aktionen, Rechte. Fehlt etwas, sage es dem Nutzer und tu nicht so, als wäre es da.", TrendoMcp.Schema(("programme", "string", "Kennungen mit Komma, ohne Angabe die des Studios", false)), args => BridgeUi(run, "programme_pruefen", async a =>
        {
            var list = a["programme"].AsText()?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<string>? only = list ?? (StudioKnowledgeBase.For(BridgeStudio(run)) is { Programs.Count: > 0 } k ? k.Programs : null);
            var checks = await Task.Run(() => ProgramChecks(only)); // Startprüfungen blockieren die Oberfläche und den Not-Stopp nicht
            return new McpResult(new JsonArray(checks.Select(c => (JsonNode)new JsonObject
            {
                ["programm"] = c.Id, ["name"] = c.Name, ["installiert"] = c.Installed, ["startet"] = c.Starts, ["schnittstelle"] = c.Interface,
                ["aktionen"] = new JsonArray(c.Actions.Select(x => (JsonNode)x).ToArray()), ["rechte"] = c.Rights, ["hinweis"] = c.Note,
            }).ToArray()).ToJsonString());
        }, args));
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

    IEnumerable<McpTool> HandoffTools(BridgeRun run)
    {
        McpTool S(string name, string text, JsonObject schema, Func<JsonObject, McpResult> body) => new(name, text, schema, a => BridgeUi(run, name, x => Task.FromResult(body(x)), a));
        yield return S("uebergeben", "Gibt eine Datei aus dem Projektordner an ein anderes Studio weiter.", TrendoMcp.Schema(("datei", "string", "Pfad im Projektordner", true), ("an_studio", "string", "Studio-Kennung des Empfängers", true), ("art", "string", "Zum Beispiel figur, ton, modell", true), ("notiz", "string", "Hinweis für das nächste Studio", false)), a =>
        {
            var to = Need(a, "an_studio");
            if (CapabilityCatalog.Find(to) is null) return McpResult.Fail("Dieses Studio gibt es nicht.");
            var item = new ProjectHandoff(run.Folder).Add(BridgeStudio(run) ?? "", to, Need(a, "datei"), Need(a, "art"), a["notiz"].AsText() ?? "");
            return new McpResult(new JsonObject { ["uebergabe"] = item.Id, ["datei"] = item.File, ["an"] = item.To }.ToJsonString());
        });
        yield return S("uebergaben", "Offene Übergaben an das aktuelle Studio in diesem Projekt.", TrendoMcp.Schema(), _ => new McpResult(new JsonArray(new ProjectHandoff(run.Folder).OpenFor(BridgeStudio(run)).Select(h => (JsonNode)new JsonObject
            { ["uebergabe"] = h.Id, ["von"] = h.From, ["datei"] = h.File, ["art"] = h.Kind, ["notiz"] = h.Note }).ToArray()).ToJsonString()));
        yield return S("uebernehmen", "Nimmt eine Übergabe an. In „Spiele bauen“ setzt verwendung die Datei direkt ein (figur, hintergrund, anim-…, ton-…). Sonst kommt der Pfad zurück.", TrendoMcp.Schema(("uebergabe", "string", "Kennung aus uebergaben", true), ("verwendung", "string", "Einsatzzweck im Spiel", false)), a =>
        {
            var studio = BridgeStudio(run) ?? throw new ArgumentException("Es ist kein Studio offen.");
            var use = a["verwendung"].AsText();
            if (use is { Length: > 0 } && studio == "CORE-06")
            {
                var open = new ProjectHandoff(run.Folder).OpenFor(studio).FirstOrDefault(h => h.Id == Need(a, "uebergabe"));
                if (open is not null && !GameAssets.Fits(use, open.File)) return McpResult.Fail($"„{open.File}“ passt nicht zur Verwendung „{use}“.");
            }
            var file = new ProjectHandoff(run.Folder).Take(Need(a, "uebergabe"), studio);
            _core.Audit.Append(Principal.LocalOwner.Name, "projekt.uebergabe", $"{studio} {Path.GetFileName(file)}");
            if (use is not { Length: > 0 } || studio != "CORE-06") return new McpResult(file);
            var doc = BridgeOpenDoc(Programs[GK]); run.Page = GK; ClaudeChangeBegin(GK, doc);
            doc.Edit("Übergabe: " + use, d => GameAssets.Assign(d, GameAssets.Add(d, Path.Combine(run.Folder, "assets"), file, "aus dem Projekt", "eigene Arbeit", "", "", use)["file"]!.GetValue<string>(), use, GameAssets.ImageSize(file) is { } sz ? GameAssets.FramesOf(sz.W, sz.H) : 1));
            ClaudeChangeAfter(GK, doc); Show(GK);
            return new McpResult($"Eingesetzt als {use}: {file}");
        });
    }

    string? BridgeStudio(BridgeRun run) => run.Dock?.Studio ?? (BridgeKey(run) is { } k ? Programs[k].Studio : ClaudeBelongs(CurrentView) ? ClaudeStudioOf(CurrentView) : null);

    /// <summary>Startseite: „Was möchtest du machen?“ mit den wichtigsten Aufgaben des Studios. Der Klick füllt die Eingabe oben, die Person ergänzt nur noch das Thema.</summary>
    Control? StudioTaskCard(string studio, string? program = null)
    {
        if (StudioKnowledgeBase.For(studio) is not { Detailed: true } k) return null;
        var row = new WrapPanel { Name = "studio-tasks" };
        foreach (var t in k.Tasks.Where(x => x.Program is null || x.Program == program).Take(5))
        {
            var task = t;
            var b = new Button { Name = "studio-task-" + task.Id, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(14, 10), Width = 230, CornerRadius = new CornerRadius(10), HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel { Spacing = 3, Children = {
                    new TextBlock { Text = task.Label, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Text, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = task.Hint, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => StudioTaskStart(task);
            row.Children.Add(b);
        }
        return Card(new StackPanel { Spacing = 8, Children = { H("Was möchtest du machen?", 17), row, new ContentControl { Name = "studio-task-ask" } } });
    }

    /// <summary>E6 (T-0616): Aufgabe gewählt – Satzanfang in die Eingabe des Studios. Braucht die Aufgabe noch Angaben (Thema, offenes Dokument …), fragt Trendo kurz
    /// direkt unter den Aufgaben nach; „Los“ startet sie dann mit der Antwort. Braucht sie nichts, startet sie sofort über den normalen Ablauf
    /// (Plan → Rückfrage nur bei Bedarf → Freigabe bei sensiblen Schritten → am Ende „Behalten?“). Ohne Eingabe auf der Seite: false.</summary>
    public bool StudioTaskStart(StudioTask task)
    {
        var root = ActivePageRoot ?? this;
        var box = root.GetLogicalDescendantsOfType<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt" && t.IsVisible);
        if (box is null) return false;
        box.Text = task.Starter; box.Focus(); box.CaretIndex = task.Starter.Length;
        var open = TaskNeeds(task);
        var ask = root.GetLogicalDescendantsOfType<ContentControl>().FirstOrDefault(c => c.Name == "studio-task-ask");
        if (open.Count == 0) { if (ask is not null) ask.Content = null; return RunPromptBox(box); }
        if (ask is null) return true; // ohne Platz für die Rückfrage: wie bisher, die Person ergänzt in der Eingabe
        var answer = new TextBox { Name = "studio-task-answer", PlaceholderText = string.Join(", ", open), MinWidth = 320, FontSize = 13 };
        var go = new Button { Name = "studio-task-go", Content = "Los", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(14, 5) };
        void Go()
        {
            var a = (answer.Text ?? "").Trim();
            if (a.Length == 0) { answer.Focus(); return; }
            box.Text = task.Starter + a; ask.Content = null; RunPromptBox(box);
        }
        go.Click += (_, _) => Go();
        answer.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; Go(); } };
        var cancel = new Button { Name = "studio-task-cancel", Content = "Abbrechen", Padding = new Thickness(12, 5), Background = Brushes.Transparent };
        cancel.Click += (_, _) => ask.Content = null;
        ask.Content = new Border { Name = "studio-task-question", Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 8), Margin = new Thickness(0, 4, 0, 0),
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Name = "studio-task-q", Text = $"„{task.Label}“ – kurz noch: {string.Join(", ", open)}?", Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { answer, go, cancel } } } } };
        return true;
    }

    /// <summary>Angaben, die eine Aufgabe noch braucht: „offenes …“ ist erledigt, wenn ein Dokument offen ist; alles andere fragt Trendo nach.</summary>
    List<string> TaskNeeds(StudioTask task)
    {
        var open = task.Needs.Where(n => !(n.StartsWith("offene", StringComparison.OrdinalIgnoreCase) && Programs.ContainsKey(CurrentView) && ProgramActive(CurrentView) is not null)).ToList();
        // Satzanfang wie „Baue ein Level mit “ oder „Figur: “: ohne Ergänzung ergibt der Auftrag keinen Sinn – Trendo fragt „Was genau?“
        if (open.Count == 0 && (task.Starter.EndsWith(' ') || task.Starter.TrimEnd().EndsWith(':'))) open.Add("Was genau");
        return open;
    }

    /// <summary>Den Auftrag in der Eingabe abschicken – wie „Los“ (Enter).</summary>
    static bool RunPromptBox(TextBox box)
    {
        if (Avalonia.LogicalTree.LogicalExtensions.GetLogicalAncestors(box).OfType<Control>().FirstOrDefault(c => c.Name == "auftrag-leiste") is not { } bar) return false;
        if (bar.GetLogicalDescendantsOfType<Button>().FirstOrDefault(b => b.Name == "auftrag-pruefen") is not { IsEnabled: true } los) return false;
        los.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        return true;
    }
}

static class LogicalFind
{
    public static IEnumerable<T> GetLogicalDescendantsOfType<T>(this Control c) where T : Control => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(c).OfType<T>();
}
