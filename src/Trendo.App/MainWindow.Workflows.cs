using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-05: Abläufe – Trendo erledigt wiederkehrende Aufgaben automatisch.</summary>
public partial class MainWindow
{
    private WorkflowEngine? _wf;
    public static string WorkflowHome { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public WorkflowEngine Workflows => _wf ??= new WorkflowEngine(_core.DataRoot, _core.Audit, _core.Approvals, [WorkflowHome]) { Summarize = async t => { var a = await Intel.AskAsync(new KiRequest("Fasse diesen Text in 5 Stichpunkten zusammen:\n\n" + (t.Length > 8000 ? t[..8000] : t), PreferredModel: KiModelName, UseKnowledge: false)); return a.Ok ? a.Text : null; } };
    public WfRun? LastWfRun { get; private set; }
    /// <summary>Letzter Fehler des Zeitplans (null = keiner). Früher wurden Fehler hier stumm verschluckt (Prüfung 04.11.2026).</summary>
    public string? LastWfError { get; private set; }
    /// <summary>Tests: fällige Abläufe ersetzen.</summary>
    public Func<Task<IReadOnlyList<WfRun>>>? WfDueOverride { get; set; }

    /// <summary>Fällige Abläufe ausführen (jede Minute). Fehler kommen ins Protokoll und werden bei den Abläufen angezeigt.</summary>
    public async Task WfTickAsync()
    {
        try { foreach (var r in await (WfDueOverride?.Invoke() ?? Workflows.RunDueAsync(DateTimeOffset.Now))) LastWfRun = r; LastWfError = null; }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LastWfError = $"{DateTime.Now:HH:mm}: {e.Message}";
            _core.Audit.Append(Principal.LocalOwner.Name, "ablauf.zeitplan.fehler", $"{e.GetType().Name}: {e.Message}");
            try { CrashLog.Write(_core.DataRoot, e, "Abläufe (Zeitplan)"); } catch (IOException) { }
            if (CurrentView == "ablaeufe") Show("ablaeufe");
        }
    }
    // Ablauf-Editor (Masterprompt 131, Runde 7): Probelauf mit sichtbarem Fortschritt an den Bausteinen
    WfRun? _wfProbe; int _wfProbeStep = -1; bool _wfProbing; string? _wfProbeId;
    public WfRun? WfProbe => _wfProbe;
    public int WfProbeStep => _wfProbeStep;
    /// <summary>Pause je Baustein, damit man dem Probelauf zusehen kann (Tests: 0).</summary>
    public int WfProbeDelayMs { get; set; } = 350;
    /// <summary>Probelauf mit Anzeige: jeder Baustein leuchtet nacheinander auf, danach Ergebnis am Baustein. Ändert nichts.</summary>
    public async Task WfProbeRun(Workflow w)
    {
        if (_wfProbing) return;
        _wfProbing = true; _wfProbe = null; _wfProbeId = w.Id; _wfProbeStep = -1; Show("ablaeufe");
        try
        {
            var r = await Workflows.RunAsync(w, dryRun: true, onStep: async i => { _wfProbeStep = i; Show("ablaeufe"); if (WfProbeDelayMs > 0) await Task.Delay(WfProbeDelayMs); });
            _wfProbe = r; LastWfRun = r;
        }
        finally { _wfProbing = false; _wfProbeStep = -1; Show("ablaeufe"); }
    }
    private string _wfMsg = ""; private bool _wfNew;
    private readonly List<WfStep> _wfDraft = [];

    private void InitSys05()
    {
        _showMap["ablaeufe"] = WorkflowsView; KiTabs.Add(("ablaeufe", "Abläufe"));
        StudioPages["SYS-05"] = "ablaeufe";
        ExtraPages.Add(("Abläufe (Automatisch erledigen)", "ablaeufe", ["automatisch", "ablauf", "workflow", "aufraeumen", "zeitplan", "regel"]));
        var t = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        t.Tick += async (_, _) => await WfTickAsync();
        OwnTimer(t);
    }

    /// <summary>Pfade unter dem Benutzerordner kurz zeigen (~\Downloads statt des vollen Pfads).</summary>
    static string HomePath(string p) => p.StartsWith(WorkflowHome, StringComparison.OrdinalIgnoreCase) && WorkflowHome.Length > 3 ? "~" + p[WorkflowHome.Length..] : p;
    private static string TriggerText(Workflow w) => w.Trigger switch
    {
        TriggerKind.Taeglich => $"jeden Tag um {w.At}", TriggerKind.Intervall => w.EveryMinutes % 1440 == 0 ? $"alle {w.EveryMinutes / 1440} Tag(e)" : $"alle {w.EveryMinutes} Minuten",
        TriggerKind.NeueDatei => $"wenn in {HomePath(w.WatchFolder)} eine neue Datei ({w.WatchPattern}) auftaucht", _ => "nur von Hand",
    };

    private Control WorkflowsView()
    {
        var left = new StackPanel { Spacing = 6, Children = { P("Abläufe erledigen Wiederkehrendes von allein. „Probelauf“ zeigt vorher, was passieren würde – ohne etwas zu ändern.", null, 12) } };
        if (LastWfError is { } err) left.Children.Add(new TextBlock { Name = "wf-error", Text = "⚠ Ein geplanter Ablauf ist fehlgeschlagen – " + err, Foreground = Brush.Parse("#FACC15"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var tpl = new WrapPanel();
        foreach (var (id, name, make) in WorkflowEngine.Templates)
        {
            var b = new Button { Name = "wf-tpl-" + id, Content = "+ " + name, Margin = new Thickness(0, 0, 6, 6) };
            b.Click += (_, _) => { var w = Workflows.Add(make(WorkflowHome)); WfSelected = w.Id; WfNode = -1; _wfMsg = $"„{w.Name}“ angelegt. Mach zuerst einen Probelauf."; Show("ablaeufe"); };
            tpl.Children.Add(b);
        }
        var nb = new Button { Name = "wf-new", Content = "+ Eigener Ablauf", Margin = new Thickness(0, 0, 6, 6) };
        nb.Click += (_, _) => { _wfNew = !_wfNew; _wfDraft.Clear(); Show("ablaeufe"); };
        tpl.Children.Add(nb);
        left.Children.Add(tpl);
        if (SuggestionCard("ablaeufe", WorkflowSuggestions()) is { } sugg) left.Children.Add(WCard(sugg));
        var all = Workflows.All();
        if (WfSelected is null || all.All(x => x.Id != WfSelected)) { WfSelected = all.FirstOrDefault()?.Id; WfNode = -1; }
        var i = 0;
        foreach (var w in all)
        {
            var n = i++; var on = w.Id == WfSelected;
            var row = new WrapPanel();
            Button B(string name, string label, Func<Task> act) { var b = new Button { Name = $"wf-{name}-{n}", Content = label, Padding = new Thickness(8, 2), Margin = new Thickness(0, 4, 6, 0), FontSize = 12 }; b.Click += async (_, _) => { WfSelected = w.Id; await act(); Show("ablaeufe"); }; row.Children.Add(b); return b; }
            B("dry", "Probelauf", async () => { LastWfRun = await Workflows.RunAsync(w, true); _wfMsg = ""; });
            B("run", "Ausführen", async () => { LastWfRun = await Workflows.RunAsync(w, false); _wfMsg = ""; });
            if (!w.IsApproved) B("approve", "Freigeben …", async () => { var dry = await Workflows.RunAsync(w, dryRun: true); var r = Workflows.RequestApproval(w, string.Join("\n", dry.Lines)); _core.Approvals.Approve(r.Id, Principal.LocalOwner); Workflows.ApplyApproval(w, r.Id); _wfMsg = $"„{w.Name}“ ist freigegeben. Änderst du den Ablauf, fragt Trendo erneut."; });
            B("toggle", w.Enabled ? "Pausieren" : "Einschalten", () => { w.Enabled = !w.Enabled; Workflows.Update(w); return Task.CompletedTask; });
            B("del", "Löschen", () => { Workflows.Remove(w.Id); if (WfSelected == w.Id) WfSelected = null; return Task.CompletedTask; });
            var last = Workflows.History(w.Id, 1).FirstOrDefault();
            var sel = new Button { Name = $"wf-sel-{n}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(0),
                Content = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = w.Name + (w.Enabled ? "" : " (pausiert)"), Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = $"Startet {TriggerText(w)}{(w.NeedsApproval ? w.IsApproved ? " · freigegeben" : " · braucht Freigabe" : "")}", Foreground = w.IsApproved ? Muted : Brush.Parse("#FACC15"), FontSize = 11, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = last is null ? "Noch nie gelaufen." : $"Zuletzt {last.At:dd.MM. HH:mm}: {(last.Ok ? "ok" : "mit Fehler")}", Foreground = Muted, FontSize = 10.5 } } } };
            sel.Click += (_, _) => { WfSelected = w.Id; WfNode = -1; _wfNew = false; Show("ablaeufe"); };
            var card = WCard(new StackPanel { Spacing = 2, Children = { sel, row } });
            if (on && card is Border cb) { cb.BorderBrush = Brush.Parse("#FACC15"); cb.BorderThickness = new Thickness(1.5); }
            left.Children.Add(card);
        }
        if (all.Count == 0) left.Children.Add(P("Noch keine Abläufe. Starte mit einer Vorlage oben."));

        var center = new StackPanel { Spacing = 8 };
        var undo = new Button { Name = "wf-undo", Content = "↶ Rückgängig", IsEnabled = _wfUndo.Count > 0 }; undo.Click += (_, _) => { WfUndo(); Show("ablaeufe"); };
        center.Children.Add(new DockPanel { Children = { undo, new TextBlock { Name = "wf-msg", Text = _wfMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) } } });
        DockPanel.SetDock(undo, Avalonia.Controls.Dock.Left);
        var selW = WfSelected is { } sid ? Workflows.Get(sid) : null;
        if (_wfNew) center.Children.Add(WorkflowEditor());
        else if (selW is not null) center.Children.Add(WfFlow(selW));
        if (LastWfRun is { } lr)
        {
            var box = new StackPanel { Name = "wf-run", Spacing = 2, Children = { new TextBlock { Text = lr.DryRun ? "Probelauf – nichts wurde geändert:" : lr.Ok ? "Ausgeführt:" : "Nicht ganz geklappt:", Foreground = lr.Ok ? Text : Brush.Parse("#FACC15"), FontWeight = FontWeight.SemiBold } } };
            foreach (var l in lr.Lines.Take(30)) box.Children.Add(P("• " + l, null, 11));
            center.Children.Add(WCard(box));
        }
        var right = selW is not null && !_wfNew ? WfInspector(selW) : new Panel();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,12,*,12,320") };
        grid.Children.Add(left); grid.Children.Add(Pos(center, 2)); grid.Children.Add(Pos(right, 4));
        grid.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Z && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control) { WfUndo(); Show("ablaeufe"); e.Handled = true; } };
        return KiPage("Abläufe", grid);
    }

    private Control WorkflowEditor()
    {
        var name = new TextBox { Name = "wfe-name", PlaceholderText = "Name des Ablaufs" };
        var trig = new ComboBox { Name = "wfe-trigger", SelectedIndex = 0, ItemsSource = new[] { "Nur von Hand", "Jeden Tag", "Alle X Minuten", "Neue Datei im Ordner" } };
        var at = new TextBox { Name = "wfe-at", PlaceholderText = "Uhrzeit oder Minuten oder Ordner", Width = 300 };
        var kind = new ComboBox { Name = "wfe-kind", SelectedIndex = 0, ItemsSource = Enum.GetValues<StepKind>().Select(WorkflowEngine.StepLabel).ToList() };
        var from = new TextBox { Name = "wfe-from", PlaceholderText = "Von Ordner", Width = 220 };
        var to = new TextBox { Name = "wfe-to", PlaceholderText = "Nach (Ordner/Datei), {datum} erlaubt", Width = 220 };
        var pat = new TextBox { Name = "wfe-pattern", PlaceholderText = "Muster, z. B. *.pdf", Width = 120 };
        var text = new TextBox { Name = "wfe-text", PlaceholderText = "Text (für „Textdatei schreiben“)" };
        var steps = new StackPanel { Spacing = 2 };
        foreach (var s in _wfDraft) steps.Children.Add(P($"• {WorkflowEngine.StepLabel(s.Kind)} {s.From} {s.To} {s.Pattern}", null, 11));
        var add = new Button { Name = "wfe-add", Content = "Schritt hinzufügen" };
        add.Click += (_, _) => { _wfDraft.Add(new WfStep { Kind = (StepKind)kind.SelectedIndex, From = from.Text ?? "", To = to.Text ?? "", Pattern = string.IsNullOrWhiteSpace(pat.Text) ? "*" : pat.Text, Text = text.Text ?? "" }); var nm = name.Text; Show("ablaeufe"); };
        var save = new Button { Name = "wfe-save", Content = "Ablauf speichern", Background = Accent };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || _wfDraft.Count == 0) { _wfMsg = "Bitte Namen eintragen und mindestens einen Schritt hinzufügen."; Show("ablaeufe"); return; }
            var w = new Workflow { Name = name.Text.Trim(), Trigger = (TriggerKind)trig.SelectedIndex, Steps = _wfDraft.ToList() };
            var a = at.Text?.Trim() ?? "";
            if (w.Trigger == TriggerKind.Taeglich) w.At = TimeOnly.TryParse(a, out var t) ? t.ToString("HH:mm") : "08:00";
            if (w.Trigger == TriggerKind.Intervall) w.EveryMinutes = int.TryParse(a, out var m) && m > 0 ? m : 60;
            if (w.Trigger == TriggerKind.NeueDatei) { w.WatchFolder = a; w.WatchPattern = _wfDraft[0].Pattern; }
            Workflows.Add(w); _wfDraft.Clear(); _wfNew = false; _wfMsg = $"„{w.Name}“ gespeichert. Mach zuerst einen Probelauf."; Show("ablaeufe");
        };
        return WCard(new StackPanel { Spacing = 6, Children = { name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { trig, at } },
            new WrapPanel { Children = { kind, from, to, pat } }, text, steps, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, save } } } });
    }
    // ---------- Ablaufplan (Masterprompt 131): Auslöser und Schritte als verbundene Bausteine ----------
    public string? WfSelected { get; set; }
    /// <summary>−1 = Auslöser, sonst Schritt-Nummer.</summary>
    public int WfNode { get; set; } = -1;
    private readonly List<(string Id, string Json)> _wfUndo = [];

    static string WfJson(Workflow w) => System.Text.Json.JsonSerializer.Serialize(new { w.Name, w.Trigger, w.At, w.EveryMinutes, w.WatchFolder, w.WatchPattern, w.Steps });
    void WfRestore(Workflow w, string json)
    {
        var o = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        w.Name = (string?)o["Name"] ?? w.Name; w.Trigger = (TriggerKind)(int)o["Trigger"]!; w.At = (string?)o["At"] ?? "08:00"; w.EveryMinutes = (int)o["EveryMinutes"]!;
        w.WatchFolder = (string?)o["WatchFolder"] ?? ""; w.WatchPattern = (string?)o["WatchPattern"] ?? "*";
        w.Steps = System.Text.Json.JsonSerializer.Deserialize<List<WfStep>>(o["Steps"]!.ToJsonString()) ?? [];
    }

    /// <summary>Ablauf ändern: vorher merken (Rückgängig), speichern; ändert sich der Inhalt, braucht ein freigabepflichtiger Ablauf eine neue Freigabe.</summary>
    public void WfChange(Workflow w, Action<Workflow> change, string msg)
    {
        var before = WfJson(w);
        try { change(w); }
        catch (InvalidOperationException ex) { WfRestore(w, before); _wfMsg = ex.Message; Show("ablaeufe"); return; }
        if (WfJson(w) == before) { Show("ablaeufe"); return; }
        _wfUndo.Add((w.Id, before)); if (_wfUndo.Count > 50) _wfUndo.RemoveAt(0);
        Workflows.Update(w); _wfMsg = msg + (w.NeedsApproval && !w.IsApproved ? " Der Ablauf braucht jetzt eine neue Freigabe." : ""); Show("ablaeufe");
    }

    public void WfUndo()
    {
        if (_wfUndo.Count == 0) { _wfMsg = "Es gibt nichts rückgängig zu machen."; return; }
        var (id, json) = _wfUndo[^1]; _wfUndo.RemoveAt(_wfUndo.Count - 1);
        if (Workflows.Get(id) is { } w) { WfRestore(w, json); Workflows.Update(w); WfSelected = id; WfNode = Math.Min(WfNode, w.Steps.Count - 1); _wfMsg = "Rückgängig gemacht."; }
    }

    /// <summary>Schritt verschieben (Ziehen im Ablaufplan oder ↑/↓).</summary>
    public void WfMoveStep(Workflow w, int from, int to)
    {
        to = Math.Clamp(to, 0, w.Steps.Count - 1); if (from == to || from < 0 || from >= w.Steps.Count) return;
        WfChange(w, x => { var s = x.Steps[from]; x.Steps.RemoveAt(from); x.Steps.Insert(to, s); }, $"„{WorkflowEngine.StepLabel(w.Steps[from].Kind)}“ ist jetzt Schritt {to + 1}.");
        WfNode = to;
    }

    Control WfFlow(Workflow w)
    {
        var flow = new StackPanel { Name = "wf-flow", Spacing = 0, HorizontalAlignment = HorizontalAlignment.Center, Width = 420 };
        var probe = _wfProbeId == w.Id ? _wfProbe : null; var probing = _wfProbing && _wfProbeId == w.Id;
        (string? Text, string Color) Badge(int i)
        {
            if (probing) return i == _wfProbeStep ? ("◐ wird geprüft …", "#FACC15") : i < _wfProbeStep ? ("✓ geprüft", "#86EFAC") : (null, "");
            if (probe is null) return (null, "");
            var r = probe.Steps.FirstOrDefault(x => x.Step == i);
            return r is null ? ("– nicht erreicht", "#94A3B8") : r.Ok ? ("✓ " + r.Summary, r.Count == 0 ? "#CBD5E1" : "#86EFAC") : ("✕ " + r.Summary, "#FCA5A5");
        }
        var probeBtn = new Button { Name = "wf-probe", Content = probing ? "Probelauf läuft …" : "▶ Probelauf mit Anzeige", IsEnabled = !probing, Background = Accent, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
        probeBtn.Click += async (_, _) => await WfProbeRun(w);
        flow.Children.Add(probeBtn);
        Border Node(string name, int idx, string icon, string title, string sub, string c1, bool selected, string? badge = null, string badgeColor = "#CFE0F5", bool live = false)
        {
            var b = new Border { Name = name, Tag = idx, Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(14), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(c1), 0), new GradientStop(Color.Parse("#313131"), 1) } },
                BorderBrush = Brush.Parse(selected ? "#FACC15" : "#44FFFFFF"), BorderThickness = new Thickness(selected ? 2 : 1), BoxShadow = CardShadow,
                Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = {
                    new TextBlock { Text = icon, FontSize = 22, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) },
                    Pos(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = sub, Foreground = Brush.Parse("#CFE0F5"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap },
                        badge is null ? new Border() : new TextBlock { Name = name + "-badge", Text = badge, Foreground = Brush.Parse(badgeColor), FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) } } }, 1) } } };
            if (live) { b.BorderBrush = Brush.Parse("#FACC15"); b.BorderThickness = new Thickness(3); b.BoxShadow = BoxShadows.Parse("0 0 18 2 #88FACC15"); }
            return b;
        }
        Control Arrow() => new TextBlock { Text = "↓", Foreground = Muted, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2) };
        var trig = Node("wf-node-trigger", -1, w.Trigger switch { TriggerKind.Taeglich => "⏰", TriggerKind.Intervall => "🔁", TriggerKind.NeueDatei => "📥", _ => "▶" }, "Start", TriggerText(w), "#7C3AED", WfNode == -1);
        trig.PointerReleased += (_, _) => { WfNode = -1; Show("ablaeufe"); };
        flow.Children.Add(trig);
        for (var i = 0; i < w.Steps.Count; i++)
        {
            var s = w.Steps[i]; var idx = i;
            flow.Children.Add(Arrow());
            var sub = s.Kind switch
            {
                StepKind.OrdnerAnlegen => HomePath(s.To), StepKind.TextSchreiben => HomePath(s.To), StepKind.Zippen => $"{HomePath(s.From)} → {HomePath(s.To)}",
                StepKind.DatumVoranstellen => $"{s.Pattern} in {HomePath(s.From)}", _ => $"{s.Pattern} aus {HomePath(s.From)} → {HomePath(s.To)}",
            };
            var risky = s.Kind is StepKind.Verschieben or StepKind.DatumVoranstellen;
            var node = Node($"wf-node-{i}", i, s.Kind switch { StepKind.OrdnerAnlegen => "📁", StepKind.Kopieren => "📄", StepKind.Verschieben => "🚚", StepKind.DatumVoranstellen => "📅", StepKind.TextSchreiben => "✏", StepKind.Zippen => "🗜", _ => "✨" },
                $"{i + 1}. {WorkflowEngine.StepLabel(s.Kind)}" + (risky ? "  · braucht Freigabe" : ""), sub.Length == 0 ? "– noch nicht eingestellt –" : sub, risky ? "#B45309" : "#1D4ED8", WfNode == i, Badge(i).Text, Badge(i).Color, probing && _wfProbeStep == i);
            // ziehen = umsortieren, Klick = auswählen
            Point? from = null; var dragged = false;
            node.PointerPressed += (_, e) => { from = e.GetPosition(this); dragged = false; e.Pointer.Capture(node); e.Handled = true; };
            node.PointerMoved += (_, e) => { if (from is { } f && Math.Abs(e.GetPosition(this).Y - f.Y) > 8) { dragged = true; node.Opacity = 0.6; } };
            node.PointerReleased += (_, e) =>
            {
                if (from is null) return; from = null; e.Pointer.Capture(null); node.Opacity = 1;
                if (!dragged) { WfNode = idx; Show("ablaeufe"); return; }
                var hit = Avalonia.Input.InputExtensions.InputHitTest(this, e.GetPosition(this)) as Visual;
                while (hit is not null && !(hit is Border { Tag: int } bb && bb.Name?.StartsWith("wf-node") == true)) hit = Avalonia.VisualTree.VisualExtensions.GetVisualParent(hit);
                if (hit is Border { Tag: int to }) WfMoveStep(w, idx, Math.Max(0, to));
            };
            flow.Children.Add(node);
        }
        flow.Children.Add(Arrow());
        var endText = probe is null ? "Ende · Protokoll unter „Verlauf“"
            : probe.Ok ? $"Ergebnis: {probe.Steps.Sum(x => x.Count)} Änderungen wären nötig – im Probelauf wurde nichts geändert. Passt alles? Dann „Ausführen“."
            : "Ergebnis: Der Ablauf bricht an dem roten Baustein ab – dort einstellen und neu prüfen.";
        flow.Children.Add(new Border { Name = "wf-node-end", Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(10), BorderBrush = Brush.Parse(probe is null ? "#44FFFFFF" : probe.Ok ? "#2FBF71" : "#F87171"), BorderThickness = new Thickness(probe is null ? 1 : 2), HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 420,
            Child = new TextBlock { Name = "wf-end-text", Text = endText, Foreground = probe is null ? Muted : Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center } });
        // Bausteine einfügen
        var palette = new WrapPanel { Name = "wf-palette", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        palette.Children.Add(new TextBlock { Text = "Baustein anhängen:", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4) });
        foreach (var k in Enum.GetValues<StepKind>())
        {
            var kk = k; var b = new Button { Name = "wf-add-" + k.ToString().ToLowerInvariant(), Content = WorkflowEngine.StepLabel(k), FontSize = 12, Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 4, 4) };
            b.Click += (_, _) => { WfChange(w, x => x.Steps.Add(new WfStep { Kind = kk, From = WorkflowHome, To = kk is StepKind.OrdnerAnlegen ? Path.Combine(WorkflowHome, "Neuer Ordner") : kk is StepKind.TextSchreiben ? Path.Combine(WorkflowHome, "notiz-{datum}.txt") : kk is StepKind.Zippen ? Path.Combine(WorkflowHome, "archiv-{datum}.zip") : WorkflowHome, Pattern = "*" }), $"Baustein „{WorkflowEngine.StepLabel(kk)}“ angehängt – rechts einstellen."); WfNode = w.Steps.Count - 1; Show("ablaeufe"); };
            palette.Children.Add(b);
        }
        return new StackPanel { Spacing = 6, Children = { palette, new ScrollViewer { Content = flow, MaxHeight = 620 } } };
    }

    Control WfInspector(Workflow w)
    {
        var sp = new StackPanel { Name = "wf-inspector", Spacing = 8 };
        Control L(string label, Control c) => new StackPanel { Spacing = 2, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 }, c } };
        var name = new TextBox { Name = "wf-edit-name", Text = w.Name };
        if (WfNode < 0 || WfNode >= w.Steps.Count)
        {
            var trig = new ComboBox { Name = "wf-edit-trigger", ItemsSource = new[] { "Nur von Hand", "Jeden Tag", "Alle X Minuten", "Neue Datei im Ordner" }, SelectedIndex = (int)w.Trigger, HorizontalAlignment = HorizontalAlignment.Stretch };
            var at = new TextBox { Name = "wf-edit-at", Text = w.At }; var every = new TextBox { Name = "wf-edit-every", Text = w.EveryMinutes.ToString() };
            var folder = new TextBox { Name = "wf-edit-folder", Text = w.WatchFolder, PlaceholderText = "Ordner, der beobachtet wird" }; var pat = new TextBox { Name = "wf-edit-watch", Text = w.WatchPattern };
            var save = new Button { Name = "wf-edit-save", Content = "Übernehmen", Background = Accent, Foreground = Brushes.White };
            save.Click += (_, _) => WfChange(w, x =>
            {
                if ((name.Text ?? "").Trim().Length == 0) throw new InvalidOperationException("Bitte einen Namen eintragen.");
                x.Name = name.Text!.Trim(); x.Trigger = (TriggerKind)Math.Max(0, trig.SelectedIndex);
                if (x.Trigger == TriggerKind.Taeglich) x.At = TimeOnly.TryParse((at.Text ?? "").Trim(), out var t) ? t.ToString("HH:mm") : throw new InvalidOperationException("Uhrzeit bitte als HH:MM.");
                if (x.Trigger == TriggerKind.Intervall) x.EveryMinutes = int.TryParse(every.Text, out var m) && m > 0 ? m : throw new InvalidOperationException("Minuten bitte als ganze Zahl größer 0.");
                if (x.Trigger == TriggerKind.NeueDatei) { x.WatchFolder = (folder.Text ?? "").Trim(); x.WatchPattern = string.IsNullOrWhiteSpace(pat.Text) ? "*" : pat.Text.Trim(); }
            }, "Start geändert.");
            sp.Children.Add(H("Start", 14)); sp.Children.Add(L("Name", name)); sp.Children.Add(L("Wann", trig));
            sp.Children.Add(L("Uhrzeit (jeden Tag)", at)); sp.Children.Add(L("Alle … Minuten", every)); sp.Children.Add(L("Ordner (neue Datei)", folder)); sp.Children.Add(L("Muster", pat));
            sp.Children.Add(save);
        }
        else
        {
            var s = w.Steps[WfNode]; var idx = WfNode;
            var kind = new ComboBox { Name = "wf-edit-kind", ItemsSource = Enum.GetValues<StepKind>().Select(WorkflowEngine.StepLabel).ToList(), SelectedIndex = (int)s.Kind, HorizontalAlignment = HorizontalAlignment.Stretch };
            var from = new TextBox { Name = "wf-edit-from", Text = s.From, PlaceholderText = "Von Ordner" };
            var to = new TextBox { Name = "wf-edit-to", Text = s.To, PlaceholderText = "Nach (Ordner/Datei), {datum} erlaubt" };
            var pat = new TextBox { Name = "wf-edit-pattern", Text = s.Pattern, PlaceholderText = "Muster, z. B. *.pdf" };
            var text = new TextBox { Name = "wf-edit-text", Text = s.Text, AcceptsReturn = true, MinHeight = 50, TextWrapping = TextWrapping.Wrap, PlaceholderText = "Text (für „Textdatei schreiben“)" };
            var save = new Button { Name = "wf-edit-save", Content = "Übernehmen", Background = Accent, Foreground = Brushes.White };
            save.Click += (_, _) => WfChange(w, x => x.Steps[idx] = new WfStep { Kind = (StepKind)Math.Max(0, kind.SelectedIndex), From = from.Text?.Trim() ?? "", To = to.Text?.Trim() ?? "", Pattern = string.IsNullOrWhiteSpace(pat.Text) ? "*" : pat.Text.Trim(), Text = text.Text ?? "" }, $"Schritt {idx + 1} geändert.");
            var up = new Button { Name = "wf-edit-up", Content = "↑", IsEnabled = idx > 0 }; up.Click += (_, _) => WfMoveStep(w, idx, idx - 1);
            var down = new Button { Name = "wf-edit-down", Content = "↓", IsEnabled = idx < w.Steps.Count - 1 }; down.Click += (_, _) => WfMoveStep(w, idx, idx + 1);
            var del = new Button { Name = "wf-edit-delete", Content = "Schritt löschen" };
            del.Click += (_, _) => { WfChange(w, x => x.Steps.RemoveAt(idx), $"Schritt {idx + 1} gelöscht."); WfNode = Math.Min(idx, w.Steps.Count - 1); Show("ablaeufe"); };
            sp.Children.Add(H($"Schritt {idx + 1}", 14)); sp.Children.Add(L("Was", kind));
            if (s.Kind is not (StepKind.OrdnerAnlegen or StepKind.TextSchreiben)) sp.Children.Add(L("Von Ordner", from));
            if (s.Kind is not StepKind.DatumVoranstellen) sp.Children.Add(L(s.Kind is StepKind.OrdnerAnlegen ? "Ordner" : s.Kind is StepKind.TextSchreiben or StepKind.Zippen ? "Datei" : "Nach", to));
            if (s.Kind is not (StepKind.OrdnerAnlegen or StepKind.TextSchreiben)) sp.Children.Add(L("Welche Dateien", pat));
            if (s.Kind is StepKind.TextSchreiben) sp.Children.Add(L("Text", text));
            sp.Children.Add(new WrapPanel { Children = { save, new Border { Width = 6 }, up, new Border { Width = 4 }, down, new Border { Width = 6 }, del } });
            sp.Children.Add(P("Erlaubt sind nur Ordner unter deinem Benutzerordner. Verschieben und Umbenennen laufen erst nach deiner Freigabe.", Muted, 11));
        }
        return WCard(sp);
    }
}
