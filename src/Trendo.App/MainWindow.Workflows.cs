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
    public WorkflowEngine Workflows => _wf ??= new WorkflowEngine(_core.DataRoot, _core.Audit, _core.Approvals, [WorkflowHome]) { Summarize = async t => { var a = await Intel.AskAsync(new KiRequest("Fasse diesen Text in 5 Stichpunkten zusammen:\n\n" + (t.Length > 8000 ? t[..8000] : t), PreferredModel: KiModelName, UseKnowledge: false)); return a.Ok ? a.Text : null; },
        // T-0698: Schritt „Agent ausführen“ – Agent läuft im Takt des Ablaufs, ohne Person (Wirkungen nach außen bleiben blockiert)
        RunAgent = (id, file, ct) => { var ki = AgentKi(Agents.Definition(id)?.Model ?? "", out var pref); return Agents.RunForWorkflowAsync(id, file, ki, AgentSvc(null), ct, pref); },
        AgentName = id => Agents.Definition(id)?.Name };
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

    // ---------- T-0742: Ablauf aus einem Satz (KI legt einen echten Ablauf an) und Windows-Aufgabe ----------
    private string _wfKiSatz = ""; private bool _wfKiBusy;
    public bool WfKiBusy => _wfKiBusy;
    /// <summary>Windows-Aufgabenplanung (schtasks im eigenen Konto). Tests ersetzen <see cref="WorkflowTaskPlan.Run"/>; der Beleg nimmt das echte schtasks.exe.</summary>
    public WorkflowTaskPlan WfTasks { get; } = new();
    /// <summary>Programm, das die Windows-Aufgabe startet (Standard: dieses Trendo). Tests/Beleg setzen das gebaute Trendo.App.exe.</summary>
    public static string? WfTaskExe { get; set; }
    readonly Dictionary<string, (bool Exists, DateTime At)> _wfTaskSeen = [];

    /// <summary>Gibt es die Windows-Aufgabe zu diesem Ablauf? (kurz zwischengespeichert, damit die Seite nicht bei jedem Neuzeichnen schtasks startet)</summary>
    public bool WfTaskExists(Workflow w)
    {
        if (!WorkflowTaskPlan.Supported) return false;
        if (_wfTaskSeen.TryGetValue(w.Id, out var s) && DateTime.UtcNow - s.At < TimeSpan.FromSeconds(20)) return s.Exists;
        var e = WfTasks.Exists(_core.DataRoot, w.Id); _wfTaskSeen[w.Id] = (e, DateTime.UtcNow); return e;
    }

    /// <summary>Satz → KI → geprüfter Ablauf in der vorhandenen Engine. Der Ablauf ist danach AUS, steht im Editor, „Rückgängig“ entfernt ihn.</summary>
    public async Task WfFromSentenceAsync(string sentence)
    {
        if (_wfKiBusy) return;
        sentence = (sentence ?? "").Trim();
        if (sentence.Length < 5) { _wfMsg = "Beschreib in einem Satz, was automatisch passieren soll – zum Beispiel „Jeden Freitag um 17 Uhr meine Dokumente als ZIP sichern“."; Show("ablaeufe"); return; }
        _wfKiBusy = true; _wfKiSatz = sentence; _wfMsg = "Die KI baut den Ablauf …"; Show("ablaeufe");
        try
        {
            var a = await Intel.AskAsync(new KiRequest(WorkflowSentence.Prompt(sentence), SystemPrompt: WorkflowSentence.System, PreferredModel: KiModelName, UseKnowledge: false));
            if (!a.Ok) { _wfMsg = "Die KI hat nicht geantwortet – es wurde nichts angelegt. " + a.Text; return; }
            var r = WorkflowSentence.FromAnswer(a.Text, WorkflowHome, sentence);
            if (r.Workflow is not { } w) { _wfMsg = "Daraus ließ sich kein sicherer Ablauf bauen – es wurde nichts angelegt. " + string.Join(" ", r.Problems); return; }
            Workflows.Add(w);
            Items(WfLink).Record($"„{w.Name}“ von der KI angelegt", w.Id, null, WfJson(w)); // Rückgängig entfernt ihn wieder
            _core.Audit.Append(CurrentPrincipal.Name, "workflow.ki.angelegt", $"{w.Id} {a.Backend}");
            WfSelected = w.Id; WfNode = -1; _wfNew = false; _wfKiSatz = "";
            _wfMsg = $"Die KI ({a.Backend}) hat „{w.Name}“ angelegt. Er ist noch AUS: prüf die Bausteine, mach einen Probelauf und schalte ihn dann ein. „Rückgängig“ entfernt ihn wieder.";
        }
        finally { _wfKiBusy = false; Show("ablaeufe"); }
    }

    /// <summary>Ablauf einschalten. Einen Ablauf aus der KI schaltest erst du ein – mit einer Rückfrage, die zeigt, was er tun würde.</summary>
    public async Task WfEnableAsync(Workflow w)
    {
        if (w.Origin is null) { w.Enabled = true; Workflows.Update(w); return; }
        var dry = await Workflows.RunAsync(w, dryRun: true); var preview = string.Join("\n", dry.Lines);
        var wk = new Wirkung("workflow.enable", $"{w.Id}:{w.Hash()}", $"Ablauf „{w.Name}“ einschalten?",
            $"Die KI hat diesen Ablauf aus deinem Satz angelegt:\n„{w.Origin}“\n\nStartet: {TriggerText(w)}\nSchritte: {string.Join(" → ", w.Steps.Select(s => WorkflowEngine.StepLabel(s.Kind)))}\n\nNach dem Einschalten läuft er von allein – so wie im Probelauf unten.", "Einschalten", preview);
        if (await FreigabeAsync(wk, () => Workflows.RequestEnable(w, preview, CurrentPrincipal)) is not { } id) { _wfMsg = $"„{w.Name}“ bleibt aus."; return; }
        try { Workflows.ApplyEnable(w, id); _wfMsg = $"„{w.Name}“ ist eingeschaltet."; }
        catch (ApprovalException ex) { _wfMsg = $"„{w.Name}“ bleibt aus: {ex.Message}"; }
    }

    /// <summary>Windows-Aufgabe anlegen – nur nach ausdrücklicher Zustimmung in der Rückfrage.</summary>
    public async Task WfCreateTaskAsync(Workflow w)
    {
        if (WorkflowTaskPlan.WhyNot(w) is { } why) { _wfMsg = why; return; }
        var exe = WfTaskExe ?? Environment.ProcessPath ?? "";
        var name = WorkflowTaskPlan.TaskName(_core.DataRoot, w.Id); var key = WorkflowTaskPlan.ScheduleKey(w);
        var wk = new Wirkung("workflow.windows-aufgabe", $"{w.Id}:{key}", $"„{w.Name}“ auch bei geschlossenem Trendo ausführen?",
            $"Trendo legt dafür in der Windows-Aufgabenplanung die Aufgabe „{name}“ an – nur für dein Benutzerkonto, ohne Administratorrechte.\n" +
            $"Sie startet zur geplanten Zeit ({TriggerText(w)}) nur Trendo selbst, ohne Fenster; Trendo führt dann diesen einen Ablauf aus, wenn er dran ist.\n" +
            "Sie läuft, solange du am PC angemeldet bist. Du siehst sie hier und in der Aufgabenplanung und entfernst sie hier mit einem Klick.", "Aufgabe anlegen",
            "Befehl der Aufgabe:\n" + WorkflowTaskPlan.Command(exe, w.Id, _core.DataRoot).Replace(Environment.UserName, "<user>"));
        if (await FreigabeAsync(wk) is not { } id) { _wfMsg = "Keine Windows-Aufgabe angelegt – der Ablauf läuft nur, solange Trendo offen ist."; return; }
        string? err;
        try { err = _core.Approvals.Execute(id, wk.Action, wk.Target, () => WfTasks.Create(w, exe, _core.DataRoot, _core.Audit, CurrentPrincipal)); }
        catch (ApprovalException ex) { err = ex.Message; }
        _wfTaskSeen.Remove(w.Id);
        if (err is not null) { _wfMsg = err; return; }
        Workflows.NoteWinTask(w, key);
        _wfMsg = $"Windows-Aufgabe „{name}“ angelegt. „{w.Name}“ läuft jetzt auch, wenn Trendo geschlossen ist.";
    }

    /// <summary>Windows-Aufgabe entfernen (ein Klick; auch beim Löschen des Ablaufs).</summary>
    public void WfRemoveTask(Workflow w)
    {
        var ok = WfTasks.Delete(w, _core.DataRoot, _core.Audit, CurrentPrincipal); _wfTaskSeen.Remove(w.Id);
        if (ok && Workflows.Get(w.Id) is not null) Workflows.NoteWinTask(w, null);
        _wfMsg = ok ? $"Windows-Aufgabe zu „{w.Name}“ entfernt – der Ablauf läuft nur noch, solange Trendo offen ist." : "Die Windows-Aufgabe ließ sich nicht entfernen – bitte in der Aufgabenplanung nachsehen.";
    }

    /// <summary>Unter dem Ablaufplan: läuft er auch bei geschlossenem Trendo? Deutlich sichtbar, mit Knopf zum Anlegen bzw. Entfernen.</summary>
    Control WfScheduleCard(Workflow w)
    {
        var sp = new StackPanel { Name = "wf-zeitplan", Spacing = 6 };
        var warn = Brush.Parse("#FACC15");
        if (WorkflowTaskPlan.WhyNot(w) is { } why)
        {
            sp.Children.Add(new TextBlock { Name = "wf-zeitplan-text", Text = (w.Trigger == TriggerKind.Manuell ? "▶ " : "⏸ Läuft nur, solange Trendo offen ist. ") + why, Foreground = w.Trigger == TriggerKind.Manuell ? Muted : warn, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (w.WinTask is not null || (w.Trigger != TriggerKind.Manuell && WfTaskExists(w))) { var rm = new Button { Name = "wf-task-remove", Content = "Alte Windows-Aufgabe entfernen" }; rm.Click += (_, _) => { WfRemoveTask(w); Show("ablaeufe"); }; sp.Children.Add(rm); }
            return WCard(sp);
        }
        var exists = WfTaskExists(w); var name = WorkflowTaskPlan.TaskName(_core.DataRoot, w.Id);
        var current = exists && w.WinTask == WorkflowTaskPlan.ScheduleKey(w);
        sp.Children.Add(new TextBlock { Name = "wf-zeitplan-text", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = current ? Text : warn,
            Text = current ? $"✓ Läuft auch, wenn Trendo geschlossen ist – Windows-Aufgabe „{name}“ (Aufgabenplanung, nur dein Benutzerkonto, solange du angemeldet bist)."
                : exists ? $"⚠ Der Zeitplan hat sich geändert – die Windows-Aufgabe „{name}“ startet noch nach dem alten. Bitte erneuern."
                : "⚠ Läuft im Moment nur, solange Trendo offen ist." });
        var row = new WrapPanel();
        if (!current)
        {
            var mk = new Button { Name = "wf-task-create", Content = exists ? "Windows-Aufgabe erneuern …" : "Auch bei geschlossenem Trendo ausführen …", Margin = new Thickness(0, 0, 6, 0) };
            mk.Click += async (_, _) => { await WfCreateTaskAsync(w); Show("ablaeufe"); };
            row.Children.Add(mk);
        }
        if (exists) { var rm = new Button { Name = "wf-task-remove", Content = "Windows-Aufgabe entfernen" }; rm.Click += (_, _) => { WfRemoveTask(w); Show("ablaeufe"); }; row.Children.Add(rm); }
        sp.Children.Add(row);
        if (!w.Enabled) sp.Children.Add(P("Der Ablauf ist aus – auch die Windows-Aufgabe führt ihn dann nicht aus.", Muted, 11));
        return WCard(sp);
    }

    /// <summary>Start durch die Windows-Aufgabe (ohne Fenster): genau diesen Ablauf ausführen, wenn er dran ist. Ergebnis ins Protokoll der Abläufe
    /// und in „ablaeufe/windows-aufgabe.log“. Rückgabe 0 = gelaufen oder nicht dran, 1 = Lauf fehlgeschlagen.</summary>
    public static int RunWorkflowHeadless(TrendoCore core, string id, TextWriter output)
    {
        var engine = new WorkflowEngine(core.DataRoot, core.Audit, core.Approvals, [WorkflowHome]);
        var (run, why) = engine.RunOneIfDueAsync(id, DateTimeOffset.Now).GetAwaiter().GetResult();
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} {id} {(run is null ? "nicht gestartet: " + why : run.Ok ? "ok: " + string.Join(" | ", run.Lines) : "Fehler: " + string.Join(" | ", run.Lines))}";
        try { File.AppendAllText(Path.Combine(core.DataRoot, "ablaeufe", "windows-aufgabe.log"), line + "\n"); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        output.WriteLine(line);
        core.Audit.Append("ablauf", "workflow.windows-aufgabe.start", $"{id} {(run is null ? why : run.Ok ? "ok" : "fehler")}");
        return run is { Ok: false } ? 1 : 0;
    }

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
        TriggerKind.Taeglich => w.Days.Count > 0 ? $"nur {WorkflowSentence.DaysText(w.Days)} um {w.At}" : $"jeden Tag um {w.At}", TriggerKind.Intervall => w.EveryMinutes % 1440 == 0 ? $"alle {w.EveryMinutes / 1440} Tag(e)" : $"alle {w.EveryMinutes} Minuten",
        TriggerKind.NeueDatei => $"wenn in {HomePath(w.WatchFolder)} eine neue Datei ({w.WatchPattern}) auftaucht", _ => "nur von Hand",
    };

    private Control WorkflowsView()
    {
        var left = new StackPanel { Spacing = 6, Children = { P("Abläufe erledigen Wiederkehrendes von allein. „Probelauf“ zeigt vorher, was passieren würde – ohne etwas zu ändern.", null, 12) } };
        if (Workflows.LoadProblem is { } lp) left.Children.Add(new TextBlock { Name = "wf-load-problem", Text = "⚠ " + lp, Foreground = Brush.Parse("#FACC15"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (LastWfError is { } err) left.Children.Add(new TextBlock { Name = "wf-error", Text = "⚠ Ein geplanter Ablauf ist fehlgeschlagen – " + err, Foreground = Brush.Parse("#FACC15"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        // T-0742: ein Satz → die KI legt einen echten Ablauf an (aus, bis du ihn einschaltest). Im Leerzustand ist das die goldene Hauptaktion.
        var kiSatz = new TextBox { Name = "wf-ki-prompt", Text = _wfKiSatz, AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, IsEnabled = !_wfKiBusy,
            PlaceholderText = "Zum Beispiel: Jeden Freitag um 17 Uhr meine Dokumente als ZIP sichern" };
        var kiGo = new Button { Name = "wf-ki-anlegen", Content = _wfKiBusy ? "Die KI baut den Ablauf …" : "Ablauf aus dem Satz anlegen", IsEnabled = !_wfKiBusy, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (Workflows.All().Count == 0) { kiGo.Background = Accent; kiGo.Foreground = Brushes.White; }
        kiSatz.TextChanged += (_, _) => _wfKiSatz = kiSatz.Text ?? "";
        kiSatz.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; await WfFromSentenceAsync(kiSatz.Text ?? ""); } };
        kiGo.Click += async (_, _) => await WfFromSentenceAsync(kiSatz.Text ?? "");
        ToolTip.SetTip(kiGo, "Die KI legt Auslöser, Bedingung und Schritte an. Der Ablauf ist danach aus – du prüfst ihn und schaltest ihn selbst ein.");
        left.Children.Add(WCard(new StackPanel { Name = "wf-ki", Spacing = 6, Children = { new TextBlock { Text = "Mit einem Satz beschreiben", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold }, kiSatz, kiGo } }));
        var tpl = new WrapPanel();
        foreach (var (id, name, make) in WorkflowEngine.Templates)
        {
            // I4 (07.11.2026): Vorlagen als Karten mit Symbol und Auslöser
            var (ic, c1, c2, trig) = id switch
            {
                "pdf" => (Mdi.Folder, "#F5D27A", "#9C7419", "neue Datei"), "notiz" => (Mdi.NoteEdit, "#8FC2FF", "#2F6FE0", "täglich"),
                "fotos" => (Mdi.Image, "#86EFAC", "#15803D", "von Hand"), "archiv" => (Mdi.ShieldCheck, "#C4B5FD", "#6D28D9", "wöchentlich"), _ => (Mdi.Cogs, "#CBD5E1", "#475569", ""),
            };
            var b = new Button { Name = "wf-tpl-" + id, Width = 280, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Children = { Chip(ic, c1, c2, 32),
                    Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                        new TextBlock { Text = name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = "Vorlage · startet " + trig, Foreground = Muted, FontSize = 10.5 } } }, 1) } } };
            ToolTip.SetTip(b, "Als neuen Ablauf anlegen"); Hover(b);
            b.Click += (_, _) => { var w = Workflows.Add(make(WorkflowHome)); WfSelected = w.Id; WfNode = -1; _wfMsg = $"„{w.Name}“ angelegt. Mach zuerst einen Probelauf."; Show("ablaeufe"); };
            tpl.Children.Add(b);
        }
        var nb = new Button { Name = "wf-new", Content = "+ Eigener Ablauf", Margin = new Thickness(0, 0, 6, 6) };
        nb.Click += (_, _) => { _wfNew = !_wfNew; if (!_wfNew) { _wfDraft.Clear(); LinkClose(WfDraftLink); } Show("ablaeufe"); }; // T-0717: Schließen verwirft den Entwurf (wie vorher), samt Notsicherung
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
            // B4 (T-0706): Probelauf zeigt, was passiert; freigegeben wird erst mit dem Ja der angemeldeten Person in der Rückfrage – nicht schon mit diesem Klick
            if (!w.IsApproved) B("approve", "Freigeben …", async () =>
            {
                var dry = await Workflows.RunAsync(w, dryRun: true); var preview = string.Join("\n", dry.Lines);
                var wk = new Wirkung("workflow.approve", $"{w.Id}:{w.Hash()}", $"Ablauf „{w.Name}“ freigeben?",
                    $"Ablauf: {w.Name}\nStartet: {TriggerText(w)}\n\nDer Ablauf darf danach von allein Dateien verschieben und umbenennen – so wie im Probelauf unten. Änderst du den Ablauf, fragt Trendo erneut.", "Freigeben", preview);
                if (await FreigabeAsync(wk, () => Workflows.RequestApproval(w, preview, CurrentPrincipal)) is not { } id) { _wfMsg = $"„{w.Name}“ ist nicht freigegeben – er verschiebt und benennt nichts um."; return; }
                try { Workflows.ApplyApproval(w, id); }
                catch (ApprovalException ex) { _wfMsg = $"„{w.Name}“ ist nicht freigegeben: {ex.Message}"; return; }
                _wfMsg = $"„{w.Name}“ ist freigegeben. Änderst du den Ablauf, fragt Trendo erneut.";
            });
            B("toggle", w.Enabled ? "Pausieren" : w.Origin is null ? "Einschalten" : "Einschalten …", async () => { if (w.Enabled) { w.Enabled = false; Workflows.Update(w); } else await WfEnableAsync(w); });
            B("del", "Löschen", () => { if (w.WinTask is not null || WfTaskExists(w)) WfRemoveTask(w); Workflows.Remove(w.Id); if (WfSelected == w.Id) WfSelected = null; return Task.CompletedTask; }); // T-0742: Windows-Aufgabe geht mit
            var last = Workflows.History(w.Id, 1).FirstOrDefault();
            var sel = new Button { Name = $"wf-sel-{n}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(0),
                Content = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = w.Name + (w.Enabled ? "" : w.Origin is not null && w.LastAttempt is null ? " (Entwurf der KI – aus)" : " (pausiert)"), Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = $"Startet {TriggerText(w)}{(w.NeedsApproval ? w.IsApproved ? " · freigegeben" : " · braucht Freigabe" : "")}", Foreground = w.IsApproved ? Muted : Brush.Parse("#FACC15"), FontSize = 11, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = last is null ? "Noch nie gelaufen." : $"Zuletzt {last.At:dd.MM. HH:mm}: {(last.Ok ? "ok" : "mit Fehler")}" + (w.LastRun is { } good && !last.Ok ? $" · zuletzt geklappt {good:dd.MM. HH:mm}" : ""), Foreground = Muted, FontSize = 10.5 } } } };
            // T-0681: sichtbarer Fehlerstand – ein Fehlschlag gilt nicht als gelaufen
            if (WorkflowEngine.FailText(w) is { } fail && sel.Content is StackPanel selBox) selBox.Children.Add(new TextBlock { Name = $"wf-fail-{n}", Text = "⚠ " + fail, Foreground = Brush.Parse("#F87171"), FontSize = 10.5, TextWrapping = TextWrapping.Wrap });
            sel.Click += (_, _) => { WfSelected = w.Id; WfNode = -1; _wfNew = false; Show("ablaeufe"); };
            var card = WCard(new StackPanel { Spacing = 2, Children = { sel, row } });
            if (on && card is Border cb) { cb.BorderBrush = Brush.Parse("#FACC15"); cb.BorderThickness = new Thickness(1.5); }
            left.Children.Add(card);
        }
        if (all.Count == 0) left.Children.Add(P("Noch keine eigenen Abläufe – oben eine Vorlage antippen.", null, 12)); // Leerzustand groß in der Mitte (G6)

        var center = new StackPanel { Spacing = 8 };
        // T-0717: Rückgängig/Wiederholen der Hülle (100 Schritte, vorher eigener Stapel mit 50 ohne Wiederholen)
        var wfLink = WfLink;
        var undo = new Button { Name = "wf-undo", Content = "Rückgängig", IsEnabled = wfLink.Link.CanUndo }; undo.Click += (_, _) => { WfUndo(); Show("ablaeufe"); };
        var redo = ItemRedoButton(wfLink, WfRedo, "ablaeufe");
        var undoRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { undo, redo } };
        if (LinkRecoveryCard(WfDraftLink) is { } wfCard) center.Children.Add(wfCard); // T-0717: nicht gespeicherter neuer Ablauf aus einer früheren Sitzung
        center.Children.Add(new DockPanel { Children = { undoRow, new TextBlock { Name = "wf-msg", Text = _wfMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) } } });
        DockPanel.SetDock(undoRow, Avalonia.Controls.Dock.Left);
        var selW = WfSelected is { } sid ? Workflows.Get(sid) : null;
        if (_wfNew) center.Children.Add(WorkflowEditor());
        else if (selW is not null) { center.Children.Add(WfFlow(selW)); center.Children.Add(WfScheduleCard(selW)); }
        else center.Children.Add(EmptyState("wf-empty", "neu-laden", "Noch kein Ablauf", "Abläufe erledigen Wiederkehrendes von allein: Downloads sortieren, Notizen anlegen, Ordner sichern. Links in einem Satz beschreiben, was passieren soll, oder eine Vorlage antippen – du siehst den Ablauf dann hier als Kette aus Auslöser und Schritten."));
        if (LastWfRun is { } lr)
        {
            var box = new StackPanel { Name = "wf-run", Spacing = 2, Children = { new TextBlock { Text = lr.DryRun ? "Probelauf – nichts wurde geändert:" : lr.Ok ? "Ausgeführt:" : "Nicht ganz geklappt:", Foreground = lr.Ok ? Text : Brush.Parse("#FACC15"), FontWeight = FontWeight.SemiBold } } };
            foreach (var l in lr.Lines.Take(30)) box.Children.Add(P("• " + l, null, 11));
            center.Children.Add(WCard(box));
        }
        var right = selW is not null && !_wfNew ? WfInspector(selW) : new Panel();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,12,*,12,320") };
        grid.Children.Add(left); grid.Children.Add(Pos(center, 2)); grid.Children.Add(Pos(right, 4));
        ItemKeys(grid, WfUndo, WfRedo, () => Show("ablaeufe")); // Strg+Z, Strg+Y
        return KiPage("Abläufe", new DockPanel { Children = { Dk(WfModeBar(), Avalonia.Controls.Dock.Top), WfPro ? N8nWorkspace() : grid } }); // Profi-Ebene n8n/Node-RED
    }

    /// <summary>Umschalter Einfach (Trendo-Abläufe) / Profi (n8n und Node-RED) – Freigabe 06.11.2026.</summary>
    private Control WfModeBar()
    {
        var mode = new WrapPanel { Name = "wf-mode", Margin = new Thickness(0, 0, 0, 8) };
        // V-15 (T-0709): „Einfach/Profi“ ist allein die Bedienstufe – hier heißen die beiden Werkzeuge nur noch beim Namen
        foreach (var (pro, label) in new[] { (false, "Trendo-Abläufe"), (true, "n8n und Node-RED") })
        {
            var on = WfPro == pro; var pp = pro;
            var mb = new Button { Name = pro ? "wf-mode-pro" : "wf-mode-simple", Content = label, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(14), Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text };
            mb.Click += (_, _) => { WfPro = pp; if (pp) _ = N8nRefresh(); Show("ablaeufe"); };
            mode.Children.Add(mb);
        }
        return mode;
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
        // T-0717: der neue Ablauf ist ein Entwurf an der Notsicherung der Hülle (Name, Auslöser, Zeit/Ordner, Schritte) – Rückgängig, 20-s-Takt, Wiederherstellen-Karte
        var dl = WfDraftLink;
        var stepsField = new LinkField("steps", () => _wfDraft.Count == 0 ? "" : System.Text.Json.JsonSerializer.Serialize(_wfDraft),
            v => { _wfDraft.Clear(); if (v.Length > 0) try { _wfDraft.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<WfStep>>(v) ?? []); } catch (System.Text.Json.JsonException) { } }, _ => { });
        LinkFormFields(dl, "neu", [LinkField.Of("name", name), ComboField("trigger", trig), LinkField.Of("at", at), stepsField]);
        var steps = new StackPanel { Spacing = 2 };
        foreach (var s in _wfDraft) steps.Children.Add(P($"• {WorkflowEngine.StepLabel(s.Kind)} {s.From} {s.To} {s.Pattern}", null, 11));
        var add = new Button { Name = "wfe-add", Content = "Schritt hinzufügen" };
        add.Click += (_, _) =>
        {
            _wfDraft.Add(new WfStep { Kind = (StepKind)kind.SelectedIndex, From = from.Text ?? "", To = to.Text ?? "", Pattern = string.IsNullOrWhiteSpace(pat.Text) ? "*" : pat.Text, Text = text.Text ?? "" });
            if (dl.Live is not null) { dl.Live["steps"] = stepsField.Read(); LinkCommit(dl, "Schritt hinzugefügt"); }
            Show("ablaeufe");
        };
        var save = new Button { Name = "wfe-save", Content = "Ablauf speichern", Background = Accent };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || _wfDraft.Count == 0) { _wfMsg = "Bitte Namen eintragen und mindestens einen Schritt hinzufügen."; Show("ablaeufe"); return; }
            var w = new Workflow { Name = name.Text.Trim(), Trigger = (TriggerKind)trig.SelectedIndex, Steps = _wfDraft.ToList() };
            var a = at.Text?.Trim() ?? "";
            if (w.Trigger == TriggerKind.Taeglich) w.At = TimeOnly.TryParse(a, out var t) ? t.ToString("HH:mm") : "08:00";
            if (w.Trigger == TriggerKind.Intervall) w.EveryMinutes = int.TryParse(a, out var m) && m > 0 ? m : 60;
            if (w.Trigger == TriggerKind.NeueDatei) { w.WatchFolder = a; w.WatchPattern = _wfDraft[0].Pattern; }
            Workflows.Add(w); _wfDraft.Clear(); _wfNew = false; LinkClose(dl); _wfMsg = $"„{w.Name}“ gespeichert. Mach zuerst einen Probelauf."; Show("ablaeufe");
        };
        return WCard(new StackPanel { Spacing = 6, Children = { LinkUndoRow(dl), name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { trig, at } },
            new WrapPanel { Children = { kind, from, to, pat } }, text, steps, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, save } } } });
    }
    // ---------- Ablaufplan (Masterprompt 131): Auslöser und Schritte als verbundene Bausteine ----------
    public string? WfSelected { get; set; }
    /// <summary>−1 = Auslöser, sonst Schritt-Nummer.</summary>
    public int WfNode { get; set; } = -1;

    static string WfJson(Workflow w) => System.Text.Json.JsonSerializer.Serialize(new { w.Name, w.Trigger, w.At, w.EveryMinutes, w.WatchFolder, w.WatchPattern, w.Steps, w.Days, w.Origin });
    void WfRestore(Workflow w, string json)
    {
        var o = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        // T-0742: Bedingung (Wochentage) und Herkunft – ältere Rückgängig-Einträge ohne diese Felder bleiben lesbar
        w.Days = o["Days"] is { } dn ? System.Text.Json.JsonSerializer.Deserialize<List<DayOfWeek>>(dn.ToJsonString()) ?? [] : [];
        if (o.ContainsKey("Origin")) w.Origin = (string?)o["Origin"];
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
        Items(WfLink).Record(msg, w.Id, before, WfJson(w)); // T-0717: Rückgängig der Hülle
        Workflows.Update(w); _wfMsg = msg + (w.NeedsApproval && !w.IsApproved ? " Der Ablauf braucht jetzt eine neue Freigabe." : ""); Show("ablaeufe");
    }

    /// <summary>T-0717: Rückgängig/Wiederholen der Hülle (100 Schritte). Wie vorher: der Ablauf bekommt seinen früheren Inhalt; ändert sich dadurch der Inhalt eines
    /// freigabepflichtigen Ablaufs, braucht er eine neue Freigabe (Freigabe-Regeln unverändert: sie prüfen den Inhalt).</summary>
    public void WfUndo() { ItemStep(WfLink, true, out var m); _wfMsg = m; }
    public void WfRedo() { ItemStep(WfLink, false, out var m); _wfMsg = m; }

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
            if (probing) return i == _wfProbeStep ? ("wird geprüft …", "#FACC15") : i < _wfProbeStep ? ("✓ geprüft", "#86EFAC") : (null, "");
            if (probe is null) return (null, "");
            var r = probe.Steps.FirstOrDefault(x => x.Step == i);
            return r is null ? ("– nicht erreicht", "#94A3B8") : r.Ok ? ("✓ " + r.Summary, r.Count == 0 ? "#CBD5E1" : "#86EFAC") : ("✕ " + r.Summary, "#FCA5A5");
        }
        var probeBtn = new Button { Name = "wf-probe", Content = probing ? "Probelauf läuft …" : "Probelauf mit Anzeige", IsEnabled = !probing, Background = Accent, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
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
                StepKind.DatumVoranstellen => $"{s.Pattern} in {HomePath(s.From)}",
                StepKind.AgentAusfuehren => Agents.Definition(s.Text)?.Name is { } an ? $"Agent „{an}“" : "",
                _ => $"{s.Pattern} aus {HomePath(s.From)} → {HomePath(s.To)}",
            };
            var risky = s.Kind is StepKind.Verschieben or StepKind.DatumVoranstellen or StepKind.AgentAusfuehren;
            var node = Node($"wf-node-{i}", i, s.Kind switch { StepKind.OrdnerAnlegen => "📁", StepKind.Kopieren => "📄", StepKind.Verschieben => "🚚", StepKind.DatumVoranstellen => "📅", StepKind.TextSchreiben => "✏", StepKind.Zippen => "🗜", StepKind.AgentAusfuehren => "🤖", _ => "✨" },
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
            var at = new TextBox { Name = "wf-edit-at", Text = w.At };
            var days = new TextBox { Name = "wf-edit-days", Text = WorkflowSentence.DaysText(w.Days), PlaceholderText = "leer = jeden Tag, sonst z. B. Mo, Fr" }; // T-0742: Bedingung
            var every = new TextBox { Name = "wf-edit-every", Text = w.EveryMinutes.ToString() };
            var folder = new TextBox { Name = "wf-edit-folder", Text = w.WatchFolder, PlaceholderText = "Ordner, der beobachtet wird" }; var pat = new TextBox { Name = "wf-edit-watch", Text = w.WatchPattern };
            var save = new Button { Name = "wf-edit-save", Content = "Übernehmen", Background = Accent, Foreground = Brushes.White };
            save.Click += (_, _) => WfChange(w, x =>
            {
                if ((name.Text ?? "").Trim().Length == 0) throw new InvalidOperationException("Bitte einen Namen eintragen.");
                x.Name = name.Text!.Trim(); x.Trigger = (TriggerKind)Math.Max(0, trig.SelectedIndex);
                if (x.Trigger == TriggerKind.Taeglich) x.At = TimeOnly.TryParse((at.Text ?? "").Trim(), out var t) ? t.ToString("HH:mm") : throw new InvalidOperationException("Uhrzeit bitte als HH:MM.");
                if (x.Trigger == TriggerKind.Taeglich) x.Days = WorkflowSentence.ParseDays(days.Text) ?? throw new InvalidOperationException("Wochentage bitte als Mo, Di, Mi, Do, Fr, Sa, So – leer heißt jeden Tag.");
                if (x.Trigger == TriggerKind.Intervall) x.EveryMinutes = int.TryParse(every.Text, out var m) && m > 0 ? m : throw new InvalidOperationException("Minuten bitte als ganze Zahl größer 0.");
                if (x.Trigger == TriggerKind.NeueDatei) { x.WatchFolder = (folder.Text ?? "").Trim(); x.WatchPattern = string.IsNullOrWhiteSpace(pat.Text) ? "*" : pat.Text.Trim(); }
            }, "Start geändert.");
            sp.Children.Add(H("Start", 14)); sp.Children.Add(L("Name", name)); sp.Children.Add(L("Wann", trig));
            sp.Children.Add(L("Uhrzeit (jeden Tag)", at)); sp.Children.Add(L("Nur an diesen Tagen", days));
            if (w.Origin is { } origin) sp.Children.Add(P($"Von der KI angelegt aus: „{origin}“", Muted, 11));
sp.Children.Add(L("Alle … Minuten", every)); sp.Children.Add(L("Ordner (neue Datei)", folder)); sp.Children.Add(L("Muster", pat));
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
            if (s.Kind is StepKind.AgentAusfuehren)
            {
                // T-0698: Agent wählen (aus „KI-Agenten bauen“); die Kennung steht im Textfeld des Schritts
                var defs = Agents.Definitions().ToList();
                var pick = new ComboBox { Name = "wf-edit-agent", ItemsSource = defs.Select(x => x.Name).ToList(), SelectedIndex = defs.FindIndex(x => x.Id == s.Text), PlaceholderText = defs.Count == 0 ? "Noch kein Agent – erst unter „KI-Agenten bauen“ anlegen" : "Agent wählen", HorizontalAlignment = HorizontalAlignment.Stretch };
                pick.SelectionChanged += (_, _) => { if (pick.SelectedIndex >= 0) text.Text = defs[pick.SelectedIndex].Id; };
                sp.Children.Add(L("Welcher Agent", pick));
                sp.Children.Add(P("Der Agent arbeitet in seinem eigenen Projekt. Bei „Neue Datei“ bekommt er die Datei in „eingang/“. Etwas nach außen geben kann er hier nicht – dafür fehlt deine Zustimmung im Moment des Laufs.", Muted, 11));
            }
            if (s.Kind is not (StepKind.OrdnerAnlegen or StepKind.TextSchreiben or StepKind.AgentAusfuehren)) sp.Children.Add(L("Von Ordner", from));
            if (s.Kind is not (StepKind.DatumVoranstellen or StepKind.AgentAusfuehren)) sp.Children.Add(L(s.Kind is StepKind.OrdnerAnlegen ? "Ordner" : s.Kind is StepKind.TextSchreiben or StepKind.Zippen ? "Datei" : "Nach", to));
            if (s.Kind is not (StepKind.OrdnerAnlegen or StepKind.TextSchreiben or StepKind.AgentAusfuehren)) sp.Children.Add(L("Welche Dateien", pat));
            if (s.Kind is StepKind.TextSchreiben) sp.Children.Add(L("Text", text));
            sp.Children.Add(new WrapPanel { Children = { save, new Border { Width = 6 }, up, new Border { Width = 4 }, down, new Border { Width = 6 }, del } });
            sp.Children.Add(P("Erlaubt sind nur Ordner unter deinem Benutzerordner. Verschieben und Umbenennen laufen erst nach deiner Freigabe.", Muted, 11));
        }
        return WCard(sp);
    }
}
