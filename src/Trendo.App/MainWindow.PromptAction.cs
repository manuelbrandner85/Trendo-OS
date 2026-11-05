using System.Net;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.PromptAction;

namespace Trendo.App;

/// <summary>Auftragsleiste für Verbesserung 42. Liegt über dem bestehenden Seiteninhalt und ändert die Studio-Layouts nicht.</summary>
public partial class MainWindow
{
    /// <summary>Auftragsleiste: ein fertiger Plan ohne offene Freigabe oder Rückfrage wird gleich erledigt (Tests können das abschalten).</summary>
    public bool AutoRunPlans { get; set; } = true;

    private StudioWorkflow? _promptFlow;
    private string? _promptProject;
    private string? _promptRun;
    private WorkflowPhase? _promptPhase;

    private StudioWorkflow PromptFlow => _promptFlow ??= new StudioWorkflow(_core, model: new IntelligencePromptModel(Intel, BackendsAreLocal), services: PromptHostServices());

    private PromptServices PromptHostServices() => new()
    {
        Offline = _core.Offline,
        Ask = (question, system, token) => Intel.AskAsync(new KiRequest(question, system, UseKnowledge: false), token),
        Launch = LaunchCatalog,
        StartShare = StartApprovedShare,
        Runner = _deps.Runner,
        FindAdb = () => MobileBridge.FindAdb(File.Exists, Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("PATH")),
        ReadBytes = ReadOnlyWeb.GetAsync,
        Probe = ReadOnlyWeb.ProbeLoopback,
    };

    private string LaunchCatalog(string exe, IReadOnlyList<string> args)
    {
        new ShellProcessLauncher().OpenWithArgs(exe, args);
        return "Gestartet: " + exe;
    }

    private string StartApprovedShare(string folder, TimeSpan duration)
    {
        var addresses = ShareServer.LanAddresses();
        var ip = addresses.Count > 0 ? addresses[0].Ip : IPAddress.Loopback;
        var server = new ShareServer(new FolderShare(folder, duration), ip, _core.Audit);
        // Freigabe läuft im Hintergrund (Task.Run): ActiveShare nur im UI-Thread tauschen, den Timer dort liest es auch
        ShareServer? previous = null;
        void Swap() { previous = ActiveShare; ActiveShare = server; }
        if (Dispatcher.UIThread.CheckAccess()) Swap(); else Dispatcher.UIThread.Invoke(Swap);
        if (previous is not null) previous.DisposeAsync().AsTask().Wait(1000);
        return "Geteilt, nur lesen, bis " + server.Share.Expires.ToString("HH:mm") + " Uhr: " + server.Url;
    }

    private bool BackendsAreLocal()
    {
        var list = KiBackendsOverride?.Invoke() ?? DefaultKiBackends();
        // KI-Dienste außerhalb des PCs fragt die Eingabeleiste nie (KiRequest.AllowRemote bleibt aus) – sie zählen hier nicht
        return list.Where(b => b is not IRemoteKiBackend).All(b => IntelligencePromptModel.Loopback(b.BaseUrl));
    }

    private string? NewestProject() => _core.Projects.List().OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).FirstOrDefault()?.Id;

    /// <summary>Gehört auf diese Seite der Claude-Knopf? Jede Seite eines Studios (öffentlich für Tests); nicht Startbildschirm und Studio-Übersicht (eigene Eingabe).</summary>
    public bool ClaudeBelongs(string key) => key is not ("start" or "studios" or "suche" or "claude-verbinden") && ClaudeStudioOf(key) is { } m && StudioCatalog.For(m) is not null;

    /// <summary>Studio, für das Claude auf dieser Seite arbeitet: das Modul der Seite; die Business-Seiten (BUS-…) gehören zu „Online verkaufen“.</summary>
    public string? ClaudeStudioOf(string key) => ModuleOf(key) is { } m ? (m.StartsWith("BUS-", StringComparison.Ordinal) ? "STUDIO-ONLINE-BUSINESS-01" : m) : null;

    /// <summary>Schlanke Zeile nur mit „✳ Claude“ (rechts oben) – für den Fokusmodus, die Studio-Übersicht und die Suche.</summary>
    Control ClaudeSlimRow(string key, Control view)
    {
        var studio = ClaudeBelongs(key) ? ClaudeStudioOf(key) : null;
        var cc = new Button { Name = "auftrag-claude", Content = Windows.Active?.Id is { } aw && ClaudeDockOpen(aw) ? "Claude ✕" : "✳ Claude", Height = 30 };
        ToolTip.SetTip(cc, studio is null ? "Claude öffnen – allgemein, mit deinem Abo" : $"Claude für „{StudioCatalog.NameOf(studio, studio)}“ öffnen");
        cc.Click += (_, _) => ClaudeDockToggle(studio, null);
        if (studio is not null) ClaudeAcceptDrop(cc, () => studio);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(8, 4, 8, 2), Children = { cc } };
        DockPanel.SetDock(row, Avalonia.Controls.Dock.Top);
        view.HorizontalAlignment = HorizontalAlignment.Stretch; view.VerticalAlignment = VerticalAlignment.Stretch;
        return new DockPanel { LastChildFill = true, Children = { row, view } };
    }

    /// <summary>Hängt die Leiste an. Der Startbildschirm bleibt unverändert, damit der parallele Desktop-Auftrag dort frei bleibt.</summary>
    private Control WithPromptBar(string key, Control view)
    {
        // start und studios haben eine eigene Eingabezeile (T-0218). 3d: die Seite füllt die Höhe; die Leiste sitzt dort im linken Rollbereich.
        // Claude auch hier (Wunsch 02.11.2026): Fokusmodus (statt der KI-Zeile nur ein schlanker Claude-Knopf), Studio-Übersicht und Suche (Claude allgemein)
        // 3d: Claude oben rechts statt nur unten in der schmalen rechten Spalte (dort war der Knopf ohne Scrollen nicht zu sehen)
        if (key == "dateien") return view;
        if (key is "studios" or "suche" or "3d" || FocusMode && key is not "start") return ClaudeSlimRow(key, view);
        if (key is "start" or "studios" or "3d" or "dateien") return view; // Dateimanager wie Explorer/Finder/Dateien: ohne KI-Zeile (KI über Suche und Startmenü)
        var studio = ModuleOf(key);
        if (studio is null) return view;
        if (CapabilityCatalog.Find(studio) is null)
        {
            // keine Auftragsleiste für dieses Modul – Claude gehört trotzdem in jedes Studio (Wunsch 02.11.2026): schlanke Leiste nur mit Claude
            if (!ClaudeBelongs(key) || ClaudeStudioOf(key) is not { } cs) return view;
            var cc = new Button { Name = "auftrag-claude", Content = Windows.Active?.Id is { } aw && ClaudeDockOpen(aw) ? "Claude ✕" : "✳ Claude", Height = 32, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(8, 6, 8, 2) };
            ToolTip.SetTip(cc, $"Claude für „{StudioCatalog.NameOf(cs, cs)}“ öffnen – mit deinem Abo, passend zu diesem Studio");
            cc.Click += (_, _) => ClaudeDockToggle(cs, null);
            ClaudeAcceptDrop(cc, () => cs); // Dateien oder Text auf „✳ Claude“ ziehen
            cc.Margin = new Avalonia.Thickness(0);
            var slim = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(8, 6, 8, 2) };
            foreach (var x in ClaudeBarExtras(key, cs)) slim.Children.Add(x);
            slim.Children.Add(cc);
            DockPanel.SetDock(slim, Avalonia.Controls.Dock.Top);
            view.HorizontalAlignment = HorizontalAlignment.Stretch; view.VerticalAlignment = VerticalAlignment.Stretch;
            return new DockPanel { LastChildFill = true, Children = { slim, view } };
        }
        var dock = new DockPanel { LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var bar = BuildPromptBar(studio, key);
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        dock.Children.Add(bar);
        view.HorizontalAlignment = HorizontalAlignment.Stretch;
        view.VerticalAlignment = VerticalAlignment.Stretch;
        dock.Children.Add(view);
        return dock;
    }

    private Control BuildPromptBar(string studio, string? key = null)
    {
        var box = new TextBox
        {
            Name = "auftrag-prompt",
            PlaceholderText = "Was soll dieses Studio für dich tun? Sag es in deinen Worten und drück Enter – am Ende entscheidest du: behalten oder nicht.",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 32,
            MaxHeight = 72,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(box, "Auftrag an dieses Studio");
        // T-0315: Programm-Assistent – versteht das offene Programm den Auftrag, arbeitet er im Dokument (Plan, Rückfragen, Vorschau); sonst der Studio-Ablauf
        var assistHost = new ContentControl { Name = "assist-host" };
        var assistTarget = AssistTargetFor(key);
        if (assistTarget is not null) box.PlaceholderText = $"Was soll ich in „{assistTarget.Title}“ für dich tun? Sag es in deinen Worten – ich zeige dir zuerst Plan und Vorschau.";
        var liveThumb = new ContentControl { Name = "assist-live" };
        DispatcherTimer? liveTimer = null;
        if (key is not null && _assistTyped.TryGetValue(key, out var typed)) box.Text = typed.Prompt;
        var status = new TextBlock { Name = "auftrag-status", Text = "Noch nicht gestartet", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220 };
        AutomationProperties.SetName(status, "Stand des Auftrags");
        var preview = new TextBlock { Name = "auftrag-vorschau", Text = "Noch keine Vorschau.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 36, TextTrimming = TextTrimming.CharacterEllipsis };
        AutomationProperties.SetName(preview, "Vorschau");
        var detail = new TextBlock { Name = "auftrag-detail", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var pro = new TextBlock { Name = "auftrag-profi", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var proOn = false;
        StudioExecutionResult? last = null;
        var ability = P(CapabilityCatalog.UiLine(studio, PromptFlow.Adapters) + " Cloud-KI ist aus.", null, 12);
        ability.Name = "auftrag-faehigkeit";
        ability.TextTrimming = TextTrimming.CharacterEllipsis;
        ability.MaxHeight = 18;

        // Wunsch 01.11.2026: einfach – Wunsch eintippen, „Los“ (oder Enter); Trendo plant und erledigt es, fragt nur bei Bedarf, am Ende „Behalten? Ja / Nein“
        var check = new Button { Name = "auftrag-pruefen", Content = "Los", Height = 32, MinWidth = 88, Background = Accent, Foreground = Brushes.White };
        ToolTip.SetTip(check, "Trendo plant und erledigt deinen Wunsch. Rückfragen nur, wenn nötig – am Ende entscheidest du: behalten oder nicht.");
        AutomationProperties.SetName(check, "Auftrag prüfen");
        var cancel = new Button { Name = "auftrag-abbrechen", Content = "Abbrechen", Height = 32, MinWidth = 104 };
        AutomationProperties.SetName(cancel, "Auftrag abbrechen");
        var approve = new Button { Name = "auftrag-freigabe", Content = "Einmal freigeben", Height = 32, MinWidth = 148, IsVisible = false };
        AutomationProperties.SetName(approve, "Diesen einen Vorgang freigeben");
        var answer = new Button { Name = "auftrag-antworten", Content = "Antwort übernehmen", Height = 32, MinWidth = 156, IsVisible = false };
        AutomationProperties.SetName(answer, "Antwort auf die Rückfrage übernehmen");
        var go = new Button { Name = "auftrag-los", Content = "Los", Height = 32, MinWidth = 72, Background = Accent, Foreground = Brushes.White, IsVisible = false };
        AutomationProperties.SetName(go, "Plan ausführen");
        var undo = new Button { Name = "auftrag-rueckgaengig", Content = "Rückgängig", Height = 32, MinWidth = 110 };
        AutomationProperties.SetName(undo, "Letzte Fassung zurückholen");
        var mode = new Button { Name = "auftrag-modus", Content = "Profi", Height = 32, MinWidth = 64, Background = Brushes.Transparent, BorderThickness = new Avalonia.Thickness(0), Foreground = Muted, IsVisible = false };
        ToolTip.SetTip(mode, "Alle Einzelheiten des Plans zeigen");
        AutomationProperties.SetName(mode, "Einfach oder Profi");

        void FillPro()
        {
            if (!proOn) { pro.IsVisible = false; return; }
            pro.IsVisible = true;
            if (last is null) { pro.Text = "Profi: noch kein Plan."; return; }
            var lines = new List<string> { "Fassung " + last.Version };
            if (last.RoutingReason.Length > 0) lines.Add(last.RoutingReason);
            lines.AddRange(last.StepEvidence.Select(s => s.Label + ": " + s.Detail));
            lines.AddRange(last.Artifacts.Select(f => f.RelativePath));
            lines.AddRange(last.QaResults.Select(q => (q.Passed ? "Prüfung ok: " : "Prüfung nicht ok: ") + q.Detail));
            if (last.Receipt is not null) lines.Add("Beleg: " + last.Receipt);
            pro.Text = string.Join("\n", lines);
        }

        void ShowResult(StudioExecutionResult result)
        {
            last = result;
            _promptRun = result.RunId;
            _promptPhase = result.Phase;
            status.Text = Trendo.Core.PromptAction.StatusText.For(result);
            preview.Text = string.IsNullOrWhiteSpace(result.Preview) ? "Noch keine Vorschau." : (result.Preview.Length > 500 ? result.Preview[..500] : result.Preview);
            var lines = new List<string>();
            // Vor der Freigabe steht genau da, was freigegeben wird (Aktion und Ziel), nicht nur die Vorschau
            if (result.Status == RunStatus.AwaitingApproval)
                foreach (var req in _core.Approvals.Pending().Where(r => result.Approvals.Contains(r.Id))) lines.Add("Freigabe: " + req.Description);
            lines.AddRange(result.Questions.Select(q => q.Question));
            lines.AddRange(result.Errors);
            lines.AddRange(result.Warnings);
            detail.Text = string.Join("\n", lines.Where(l => l.Length > 0));
            detail.IsVisible = detail.Text is { Length: > 0 };
            approve.IsVisible = result.Status == RunStatus.AwaitingApproval;
            answer.IsVisible = result.Status == RunStatus.AwaitingInput;
            go.IsVisible = result.Phase == WorkflowPhase.Planned;
            FillPro();
            Sync();
            if (result.Artifacts.Count > 0) RefreshStudioBody();
        }

        void RefreshStudioBody()
        {
            if (CurrentView is "start" or "3d" or "") return;
            if (!_showMap.ContainsKey(CurrentView)) return;
            foreach (var frame in _frames.Values)
            {
                if (frame.Body.Content is not DockPanel dock || dock.Children.Count < 2) continue;
                if (dock.Children[0] is not Border { Name: "auftrag-leiste" }) continue;
                var view = _showMap[CurrentView]();
                view.HorizontalAlignment = HorizontalAlignment.Stretch;
                view.VerticalAlignment = VerticalAlignment.Stretch;
                dock.Children.RemoveAt(dock.Children.Count - 1);
                dock.Children.Add(view);
                return;
            }
        }

        StudioExecutionContext Ctx(string project) => new(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);

        check.Click += async (_, _) =>
        {
            if (AssistTargetFor(key) is { } at && (box.Text ?? "").Trim().Length > 0)
            {
                check.IsEnabled = false;
                try { liveThumb.Content = null; if (await AssistStart(at, box.Text ?? "", assistHost)) return; }
                finally { check.IsEnabled = true; }
            }
            status.Text = "Plan wird erstellt";
            check.IsEnabled = false;
            try
            {
                var project = NewestProject();
                if (project is null)
                {
                    status.Text = "Rückfrage";
                    detail.Text = "Lege zuerst ein Projekt an.";
                    detail.IsVisible = true;
                    answer.IsVisible = false;
                    approve.IsVisible = false;
                    go.IsVisible = false;
                    return;
                }
                _promptProject = project;
                var ctx = Ctx(project);
                StudioExecutionResult result;
                if (_promptRun is not null && _promptPhase is WorkflowPhase.Planned or WorkflowPhase.Verified or WorkflowPhase.Failed or WorkflowPhase.NeedsApproval or WorkflowPhase.Produced or WorkflowPhase.Qa)
                    result = await Task.Run(() => PromptFlow.Revise(project, _promptRun, box.Text ?? "", ctx));
                else if (_promptRun is not null && _promptPhase is WorkflowPhase.NeedsClarification or WorkflowPhase.RevisionRequested)
                    result = await Task.Run(() => PromptFlow.Answer(project, _promptRun, PromptFields.Read(box.Text), ctx));
                else
                {
                    var id = "ui-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    var request = new StudioPromptRequest(id, project, studio, box.Text ?? "", [], "de", null, PrivacyMode.Local, new Provenance("oberflaeche", null, null));
                    result = await Task.Run(() => PromptFlow.Start(request, ctx));
                }
                await _ui.InvokeAsync(() => ShowResult(result));
                if (result.Phase == WorkflowPhase.Planned && AutoRunPlans) await RunLos(); // nichts offen: gleich erledigen
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
            finally { check.IsEnabled = true; }
        };
        cancel.Click += (_, _) =>
        {
            if (_promptProject is null || _promptRun is null)
            {
                status.Text = "Nichts zum Abbrechen.";
                detail.Text = "";
                detail.IsVisible = false;
                return;
            }
            try
            {
                var result = PromptFlow.Cancel(_promptProject, _promptRun, Principal.LocalOwner, Ctx(_promptProject));
                ShowResult(result);
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
        };
        approve.Click += async (_, _) =>
        {
            if (_promptProject is null || _promptRun is null) return;
            approve.IsEnabled = false;
            try
            {
                var result = await Task.Run(() => PromptFlow.Approve(_promptProject, _promptRun, Principal.LocalOwner, Ctx(_promptProject)));
                await _ui.InvokeAsync(() => ShowResult(result));
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
            finally { approve.IsEnabled = true; }
        };
        answer.Click += (_, _) =>
        {
            if (_promptProject is null || _promptRun is null) return;
            try
            {
                var result = PromptFlow.Answer(_promptProject, _promptRun, PromptFields.Read(box.Text), Ctx(_promptProject));
                ShowResult(result);
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
        };

        async Task RunLos()
        {
            if (_promptProject is null || _promptRun is null) { status.Text = "Zuerst den Wunsch eintippen."; return; }
            status.Text = "Wird erledigt …";
            go.IsEnabled = false;
            try
            {
                var result = await Task.Run(() => PromptFlow.Los(_promptProject, _promptRun, Ctx(_promptProject)));
                await _ui.InvokeAsync(() => ShowResult(result));
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
            finally { go.IsEnabled = true; }
        }
        go.Click += async (_, _) => await RunLos();
        // Enter schickt ab (Umschalt+Enter = neue Zeile)
        box.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && check.IsEnabled) { e.Handled = true; check.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        undo.Click += (_, _) =>
        {
            if (_promptProject is null || _promptRun is null)
            {
                status.Text = "Nichts zum Rückgängigmachen.";
                return;
            }
            try { ShowResult(PromptFlow.Undo(_promptProject, _promptRun, Ctx(_promptProject))); }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
        };
        mode.Click += (_, _) =>
        {
            proOn = !proOn;
            mode.Content = proOn ? "Einfach" : "Profi";
            FillPro();
        };

        // Ruhige Leiste (Vorgabe 2026-10-26 „schönere, verständlichere Oberfläche“): eine Zeile mit Eingabe und „Plan erstellen“.
        // Stand, Vorschau, Rückgängig, Abbrechen und Profi erscheinen erst, wenn es etwas dazu gibt. Die Fähigkeiten stehen im Tooltip.
        ability.IsVisible = false;
        ToolTip.SetTip(box, ability.Text);
        void Sync()
        {
            var active = status.Text != "Noch nicht gestartet";
            status.IsVisible = active;
            cancel.IsVisible = (active && last is null) || _promptPhase is WorkflowPhase.Planned or WorkflowPhase.NeedsApproval or WorkflowPhase.NeedsClarification;
            undo.IsVisible = last is not null;
            mode.IsVisible = last is not null;
            preview.IsVisible = preview.Text is { Length: > 0 } pv && pv != "Noch keine Vorschau.";
        }
        status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) Sync(); };
        if (key is not null && _assistTyped.TryGetValue(key, out var lastLive) && lastLive.Live.Length > 0) preview.Text = lastLive.Live;
        // T-0315 Echtzeit-Vorschau beim Tippen: was der Assistent im offenen Programm tun würde (ohne KI, ohne etwas zu ändern)
        box.TextChanged += (_, _) =>
        {
            if (AssistTargetFor(key) is null) return;
            liveTimer?.Stop();
            liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AssistLiveDelayMs) };
            liveTimer.Tick += (_, _) =>
            {
                liveTimer!.Stop();
                if (AssistTargetFor(key) is not { } lt || _assistRuns.TryGetValue(assistHost, out var busy) && busy.Phase is "plan" or "fragen" or "läuft" or "vorschau") return;
                var (text, thumb) = AssistLive(lt, box.Text ?? "");
                preview.Text = text.Length > 0 ? text : "Noch keine Vorschau."; liveThumb.Content = thumb;
                if (key is not null) _assistTyped[key] = (box.Text ?? "", preview.Text);
            };
            liveTimer.Start();
        };
        preview.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) Sync(); };
        Sync();
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { check, go, answer, approve, undo, cancel, mode }
        };
        // A2: Studios, die Programme brauchen, bekommen „Hinzufügen …“ direkt in ihrer Leiste
        if (StudioTools.Required.ContainsKey(studio))
        {
            var add = new Button { Name = "auftrag-hinzufuegen", Content = "Hinzufügen …", Height = 32 };
            ToolTip.SetTip(add, "Programme und KI, die dieses Studio braucht, finden und installieren");
            add.Click += (_, _) => OpenAddHubFor(studio);
            buttons.Children.Add(add);
        }
        // Wunsch 01.11.2026: in jedem Studio – dein echtes Claude Code rechts im Studio-Fenster, im Projektordner, mit dem eingetippten Auftrag
        var cc = new Button { Name = "auftrag-claude", Content = Windows.Active?.Id is { } aw && ClaudeDockOpen(aw) ? "Claude ✕" : "✳ Claude", Height = 32 };
        ToolTip.SetTip(cc, "Claude mit deinem Abo öffnen – wie im Web und in der App, mit eigenem Projekt für dieses Studio");
        cc.Click += (_, _) => ClaudeDockToggle(studio, box.Text);
        if (key is not null && ClaudeStudioOf(key) is { } ccs) { ClaudeAcceptDrop(cc, () => ccs); foreach (var x in ClaudeBarExtras(key, ccs)) buttons.Children.Add(x); }
        if (key is not null) buttons.Children.Add(cc); // ohne Seite (3D-Spalte): Claude sitzt schon oben rechts
        var spark = new TextBlock { Text = "✨", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
        ToolTip.SetTip(spark, "Sag der Trendo KI in deinen Worten, was dieses Studio für dich tun soll.");
        box.MaxHeight = 64; box.MinHeight = 34;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(spark, 0);
        Grid.SetColumn(box, 1);
        Grid.SetColumn(buttons, 2);
        Grid.SetColumn(status, 3);
        row.Children.Add(spark);
        row.Children.Add(box);
        row.Children.Add(buttons);
        row.Children.Add(status);
        // Mac/GNOME: die KI-Zeile ist zugleich die Titelleiste des Studio-Fensters (Ampel links bzw. Schließen rechts, Titel, ziehbar)
        if (key is not null && _style is "mac" or "linux")
        {
            var newPage = _unifiedPages.Add(key);
            var winTitle = new TextBlock { Name = "head-title", Text = WindowOfPage(key)?.Title ?? StudioCatalog.NameOf(studio, studio), FontWeight = FontWeight.Bold, FontSize = 13, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(4, 0, 8, 0), MaxWidth = 260, TextTrimming = TextTrimming.CharacterEllipsis };
            var caps = OsHeaderCaps(key);
            // Zurück zur vorigen Seite im Fenster (sonst in der weggefallenen Titelleiste)
            var backWin = WindowOfPage(key);
            var hb = new Button { Name = "head-back", Content = "‹", FontSize = 18, Width = 30, Height = 30, Padding = new Avalonia.Thickness(0, -3, 0, 0), Background = Brushes.Transparent, BorderThickness = new Avalonia.Thickness(0), CornerRadius = new Avalonia.CornerRadius(6),
                Foreground = Text, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsEnabled = backWin?.CanGoBack ?? false, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(hb, "Zurück"); hb.Click += (_, _) => { if (WindowOfPage(key) is { } bw) GoBack(bw.Id); };
            caps = _style == "mac" ? new StackPanel { Orientation = Orientation.Horizontal, Children = { caps, hb } } : caps;
            row.ColumnDefinitions = new ColumnDefinitions(_style == "mac" ? "Auto,Auto,Auto,*,Auto,Auto" : "Auto,Auto,*,Auto,Auto,Auto");
            if (_style == "mac") { Grid.SetColumn(caps, 0); Grid.SetColumn(winTitle, 1); Grid.SetColumn(spark, 2); Grid.SetColumn(box, 3); Grid.SetColumn(buttons, 4); Grid.SetColumn(status, 5); }
            else { Grid.SetColumn(winTitle, 0); Grid.SetColumn(spark, 1); Grid.SetColumn(box, 2); Grid.SetColumn(buttons, 3); Grid.SetColumn(status, 4); Grid.SetColumn(caps, 5); }
            row.Children.Add(caps);
            if (_style == "linux") { var tl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { hb, winTitle } }; Grid.SetColumn(tl, 0); row.Children.Add(tl); }
            else row.Children.Add(winTitle);
            var head = new Border
            {
                Name = "auftrag-leiste", Background = Brush.Parse(_style == "mac" ? (_theme == "hell" ? "#FFECECEC" : "#FF2A2A2C") : (_theme == "hell" ? "#FFEBEBED" : "#FF2E2E32")),
                BorderBrush = Edge, BorderThickness = new Avalonia.Thickness(0, 0, 0, 1), Padding = new Avalonia.Thickness(10, 6), Margin = new Avalonia.Thickness(0, 0, 0, 6),
                Child = new StackPanel { Spacing = 4, Children = { row, preview, liveThumb, AssistAttached(key, assistHost), detail, pro } }
            };
            ability.IsVisible = false; // wie im System: keine Zusatzzeile über der Titelleiste (steht im Tooltip)
            ToolTip.SetTip(box, ability.Text);
            preview.IsVisible = false; // Vorschau-Zeile erst, wenn es eine Vorschau gibt – die Leiste bleibt einzeilig wie eine Titelleiste
            preview.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) preview.IsVisible = preview.Text is { Length: > 0 } pt && pt != "Noch keine Vorschau."; };
            OsHeaderDrag(head, key);
            if (newPage) _ui.Post(LayoutWindows); // Titelleiste des Fensters ausblenden
            return head;
        }
        return new Border
        {
            Name = "auftrag-leiste",
            Background = TileBg,
            BorderBrush = Edge,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(12),
            Padding = new Avalonia.Thickness(8, 6),
            Margin = new Avalonia.Thickness(0, 0, 0, 6),
            Child = new StackPanel { Spacing = 4, Children = { ability, row, preview, liveThumb, AssistAttached(key, assistHost), detail, pro } }
        };
    }
}
