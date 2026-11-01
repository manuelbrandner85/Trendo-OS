using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Iciclecreek.Terminal;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude Code im Studio-Fenster (Wunsch 01.11.2026): Das echte Claude Code des Nutzers läuft als Terminal-Fläche rechts im Studio – wie das Terminal in
/// VS Code. Trendo nutzt das Abo dabei nicht selbst: Es startet nur das installierte Programm sichtbar im Projektordner (mit Rolle und Skills des Studios),
/// der Nutzer arbeitet und meldet sich dort selbst an. Kostenpflichtige API-Variablen werden geleert, ein Abo-Token wird mitgegeben.
/// Die Fläche bleibt beim Neuzeichnen der Seite erhalten und endet mit dem Fenster.</summary>
public partial class MainWindow
{
    sealed class ClaudeDock
    {
        public required Border Panel; public TerminalControl? Term; public required string Folder; public string? Studio; public Grid? Split; public ContentControl? PageHost;
        public string Tab = "code"; public Border CodeHost = new(); public Border? WebPane; public Border? AgentPane; public StackPanel? WebBar; public NativeWebView? Web; public string WebUrl = "";
    }
    readonly Dictionary<string, ClaudeDock> _claudeDocks = [];
    /// <summary>Für Tests: statt Claude Code dieses Programm im Terminal starten (Programm, Argumente). Im Testmodus startet sonst nichts.</summary>
    public static (string Exe, string[] Args)? ClaudeTerminalOverride { get; set; }
    public bool ClaudeDockOpen(string winId) => _claudeDocks.ContainsKey(winId);
    public TerminalControl? ClaudeDockTerminal(string winId) => _claudeDocks.TryGetValue(winId, out var d) ? d.Term : null;
    public string? ClaudeDockTab(string winId) => _claudeDocks.TryGetValue(winId, out var d) ? d.Tab : null;
    public string? ClaudeDockWebUrl(string winId) => _claudeDocks.TryGetValue(winId, out var d) ? d.WebUrl : null;
    public const string ClaudeWebChat = "https://claude.ai/new", ClaudeWebCode = "https://claude.ai/code";
    public string? ActiveWindowId => Windows.Active?.Id;

    /// <summary>Seiteninhalt mit der Claude-Fläche rechts daneben (wenn für dieses Fenster geöffnet). Das Raster bleibt je Fenster dasselbe,
    /// nur der Seiteninhalt wechselt – so wird das Terminal beim Neuzeichnen nie aus dem Baum gelöst (sonst endet Claude Code).</summary>
    Control WithClaudeDock(string winId, Control content)
    {
        if (!_claudeDocks.TryGetValue(winId, out var dock)) return content;
        if (dock.Split is null)
        {
            dock.PageHost = new ContentControl();
            var split = new GridSplitter { Width = 6, Background = Brush.Parse("#22FFFFFF"), ResizeDirection = GridResizeDirection.Columns };
            Grid.SetColumn(split, 1); Grid.SetColumn(dock.Panel, 2);
            dock.Split = new Grid { Name = "claude-split", ColumnDefinitions = new ColumnDefinitions("*,6,560"), Children = { dock.PageHost, split, dock.Panel } };
        }
        dock.PageHost!.Content = content;
        return dock.Split;
    }

    /// <summary>Claude-Fläche im aktiven Fenster öffnen oder schließen (Knopf in der Auftragsleiste).</summary>
    public void ClaudeDockToggle(string? studio, string? prompt)
    {
        if (Windows.Active?.Id is not { } id) return;
        if (_claudeDocks.Remove(id, out var d)) { CloseDock(d); Show(CurrentView); return; }
        var folder = ClaudeFolderFor(studio);
        var dock = new ClaudeDock { Folder = folder, Studio = studio, Panel = new Border { Name = "claude-dock", Background = Brush.Parse("#FF0C1222"), BorderBrush = EdgeLit, BorderThickness = new Thickness(1, 0, 0, 0) } };
        _claudeDocks[id] = dock;
        BuildShell(dock);
        BuildDock(dock, prompt);
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.dock", $"{studio} {Path.GetFileName(folder)}");
        Show(CurrentView);
    }

    void CloseDock(ClaudeDock d)
    {
        try { if (d.Term is { IsLive: true }) d.Term.Kill(); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    /// <summary>Fenster geschlossen: seine Claude-Fläche beenden.</summary>
    void ClaudeDockWindowClosed(string winId) { if (_claudeDocks.Remove(winId, out var d)) CloseDock(d); }

    /// <summary>Rahmen der Fläche: Kopf mit den Reitern „Claude Code“ (Terminal) und „Claude“ (claude.ai im Web), darunter beide Bereiche übereinander –
    /// der unsichtbare bleibt bestehen (Terminal und Anmeldung laufen weiter).</summary>
    void BuildShell(ClaudeDock dock)
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(10, 8, 10, 4) };
        var tabs = new StackPanel { Name = "claude-tabs", Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var (k, l, tip) in new[] { ("code", "✳ Claude Code", "Dein Claude Code auf diesem PC – arbeitet direkt in den Trendo-Projektordnern"), ("web", "Claude", "claude.ai mit deinem Abo – Chat, Bilder, Recherche und Claude Code im Web (GitHub-Projekte)"), ("agent", "Trendo-Agent", "Arbeitet wie Claude Code mit der KI, die Trendo hat – auch ohne Abo: Wunsch eintippen, „Los“, am Ende nur Ja oder Nein") })
        {
            var tab = k;
            var b = new Button { Name = "claude-tab-" + k, Content = l, Padding = new Thickness(12, 4), CornerRadius = new CornerRadius(12), Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => ClaudeDockShowTab(dock, tab);
            tabs.Children.Add(b);
        }
        var where = new TextBlock { Name = "claude-dock-folder", Text = Path.GetFileName(dock.Folder.TrimEnd('\\', '/')), Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(where, dock.Folder);
        var close = new Button { Name = "claude-dock-close", Content = "✕", Padding = new Thickness(8, 2), Background = Brushes.Transparent };
        ToolTip.SetTip(close, "Claude schließen"); close.Click += (_, _) => ClaudeDockToggle(dock.Studio, null);
        head.Children.Add(tabs); head.Children.Add(Pos(where, 1)); head.Children.Add(Pos(close, 2));
        DockPanel.SetDock(head, Avalonia.Controls.Dock.Top);
        dock.WebPane = new Border { Name = "claude-web-pane", IsVisible = false };
        dock.AgentPane = new Border { Name = "claude-agent-pane", IsVisible = false };
        dock.Panel.Child = new DockPanel { Children = { head, new Grid { Children = { dock.CodeHost, dock.WebPane, dock.AgentPane } } } };
        ClaudeDockShowTab(dock, dock.Tab);
    }

    void ClaudeDockShowTab(ClaudeDock dock, string tab)
    {
        dock.Tab = tab;
        dock.CodeHost.IsVisible = tab == "code"; dock.WebPane!.IsVisible = tab == "web"; dock.AgentPane!.IsVisible = tab == "agent";
        if (tab == "web" && dock.WebPane.Child is null) BuildWeb(dock);
        if (tab == "agent" && dock.AgentPane.Child is null) BuildAgentPane(dock);
        if (dock.Panel.Child is DockPanel dp && dp.Children.OfType<Grid>().FirstOrDefault()?.Children.OfType<StackPanel>().FirstOrDefault(c => c.Name == "claude-tabs") is { } tabs)
            foreach (var b in tabs.Children.OfType<Button>()) b.Background = b.Name == "claude-tab-" + tab ? Accent : Brushes.Transparent;
        if (tab == "web") _core.Audit.Append(Principal.LocalOwner.Name, "claude.web", dock.Studio ?? "");
    }

    /// <summary>Öffentlich für Tests und die Auftragsleiste: Reiter im aktiven Fenster wählen.</summary>
    public void ClaudeDockSelectTab(string tab) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) ClaudeDockShowTab(d, tab); }

    public ClaudeWeb CWeb => new(_core.DataRoot);

    /// <summary>Webansicht für claude.ai mit dem gemeinsamen, dauerhaften Trendo-Profil (einmal anmelden, dann in allen Studios angemeldet).
    /// Neue Fenster (Links) öffnen im normalen Browser. <paramref name="navigated"/> bekommt jede fertig geladene Adresse.</summary>
    NativeWebView ClaudeWebView(string name, Action<Uri> navigated)
    {
        var web = new NativeWebView { Name = name };
        var profile = CWeb.Profile;
        web.EnvironmentRequested += (_, e) => { if (e is WindowsWebView2EnvironmentRequestedEventArgs w) { Directory.CreateDirectory(profile); w.UserDataFolder = profile; } };
        web.NewWindowRequested += (_, e) => { if (e.Request is { } u && u.Scheme is "https" or "http") Launch(u.AbsoluteUri); e.Handled = true; };
        web.NavigationCompleted += (_, _) => { if (web.Source is { } u) navigated(u); };
        return web;
    }

    /// <summary>Navigation im Claude-Reiter verarbeiten (öffentlich für Tests): Anmeldung erkennen, Chat dem Studio zuordnen.</summary>
    public void ClaudeWebNavigated(string url)
    {
        if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) WebNavigated(d, new Uri(url));
    }

    void WebNavigated(ClaudeDock dock, Uri u)
    {
        dock.WebUrl = u.AbsoluteUri;
        var was = CWeb.Connected;
        if (CWeb.Navigated(u, dock.Studio) && !was && CWeb.Connected) { _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.verbunden", ""); BuildWebBar(dock); }
    }

    /// <summary>Claude-Reiter: angemeldet → Chat dieses Studios (oder neu); sonst die Anmeldung von claude.ai (einmalig). Im Testmodus wird nichts geladen.</summary>
    void BuildWeb(ClaudeDock dock)
    {
        dock.WebBar = new StackPanel { Spacing = 4, Margin = new Thickness(10, 4) };
        Control view;
        if (_opt.Isolated) view = new Border { Name = "claude-web", Child = P("Testmodus: claude.ai wird nicht geladen.", Muted, 12) };
        else { dock.Web = ClaudeWebView("claude-web", u => WebNavigated(dock, u)); view = dock.Web; }
        DockPanel.SetDock(dock.WebBar, Avalonia.Controls.Dock.Top);
        dock.WebPane!.Child = new DockPanel { Children = { dock.WebBar, view } };
        BuildWebBar(dock);
        WebGo(dock, CWeb.StartUrl(dock.Studio));
    }

    void WebGo(ClaudeDock dock, string url) { dock.WebUrl = url; if (dock.Web is { } w) w.Navigate(new Uri(url)); }

    void BuildWebBar(ClaudeDock dock)
    {
        if (dock.WebBar is null) return;
        dock.WebBar.Children.Clear();
        var connected = CWeb.Connected;
        dock.WebBar.Children.Add(new TextBlock { Name = "claude-web-state", Text = connected ? $"✓ Mit Claude verbunden – dein Chat für „{StudioCatalog.NameOf(dock.Studio ?? "", "dieses Studio")}“" : "Einmal anmelden: Melde dich unten mit deinem Claude-Abo an – danach ist Claude in allen Studios verbunden.",
            Foreground = connected ? Brush.Parse("#86EFAC") : GoldText(), FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var row = new WrapPanel();
        void B(string name, string label, string tip, Action a, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 4, 4), Background = primary ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => a(); row.Children.Add(b);
        }
        if (connected)
        {
            B("claude-web-copy", "Auftrag kopieren", "Deinen Auftrag aus der Auftragsleiste kopieren – im neuen Chat mit Rolle und Fachregeln dieses Studios. Einfügen (Strg+V) und abschicken tust du selbst.", () => _ = ClaudeWebCopy(dock), true);
            B("claude-web-chat", "Neuer Chat", "Neuen Chat für dieses Studio beginnen", () => { if (dock.Studio is { } st) CWeb.NewStudioChat(st); WebGo(dock, ClaudeWeb.NewChat); });
            B("claude-web-code", "Claude Code im Web", "Claude Code im Browser – arbeitet mit deinen GitHub-Projekten", () => WebGo(dock, ClaudeWebCode));
            B("claude-web-skills", "Fachregeln für Claude", "Die Fachregeln dieses Studios als Skill-Paket speichern – einmal in Claude unter Einstellungen → Fähigkeiten hochladen", () => Try(CurrentView, () => ClaudeSkillPackage(dock.Studio)));
        }
        else B("claude-web-login", "Zur Anmeldung", "Anmeldeseite von claude.ai öffnen", () => WebGo(dock, ClaudeWeb.Login), true);
        B("claude-web-back", "←", "Zurück", () => dock.Web?.GoBack());
        B("claude-web-reload", "↻", "Neu laden", () => dock.Web?.Refresh());
        dock.WebBar.Children.Add(row);
        if (_claudeMsg.Length > 0 && (_claudeMsg.StartsWith("Fachregeln") || _claudeMsg.StartsWith("Kopiert"))) dock.WebBar.Children.Add(new TextBlock { Name = "claude-web-msg", Text = _claudeMsg, Foreground = _claudeMsg.StartsWith("Kopiert") ? GoldText() : Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
    }

    /// <summary>Fachregeln eines Studios als Skill-Paket für Claude speichern und den Ordner zeigen (öffentlich für Tests).</summary>
    public string ClaudeSkillPackage(string? studio)
    {
        var zip = ClaudeWeb.SkillPackage(Skills.For(null, studio), studio ?? "trendo", WorkFolder("Claude"));
        _claudeMsg = $"Fachregeln gespeichert: {Path.GetFileName(zip)} – in Claude unter Einstellungen → Fähigkeiten hochladen (einmalig).";
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.skills.paket", Path.GetFileName(zip));
        Launch(Path.GetDirectoryName(zip)!);
        foreach (var d in _claudeDocks.Values) BuildWebBar(d);
        return zip;
    }

    /// <summary>Auftrag in die Zwischenablage – im neuen Studio-Chat mit Rolle und Fachregeln davor. Entscheidung 01.11.2026: Trendo bedient die Seite von claude.ai
    /// nie selbst (kein Skript in der Seite); Einfügen und Abschicken macht der Nutzer (öffentlich für Tests: Rückgabe = kopierter Text).</summary>
    public async Task<string?> ClaudeWebCopy(string? text = null)
    {
        if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) return await ClaudeWebCopy(d, text);
        return null;
    }

    async Task<string?> ClaudeWebCopy(ClaudeDock dock, string? text = null)
    {
        text ??= this.GetLogicalDescendantsOf<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt")?.Text;
        if (string.IsNullOrWhiteSpace(text)) return null;
        var msg = CWeb.Message(dock.Studio, StudioCatalog.NameOf(dock.Studio ?? "", "Trendo"), ClaudeRoleFor(dock.Studio), Skills.For(null, dock.Studio), text);
        LastClipboardText = msg;
        try { if (Clipboard is { } cb) await cb.SetTextAsync(msg); _claudeMsg = "Kopiert – klick unten ins Eingabefeld von Claude, drück Strg+V und schick ab."; }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException) { _claudeMsg = "Der Auftrag ließ sich nicht kopieren: " + e.Message; }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.auftrag", dock.Studio ?? "");
        BuildWebBar(dock);
        return msg;
    }

    void BuildDock(ClaudeDock dock, string? prompt)
    {
        Control body;
        var s = _claude;
        var testMode = _opt.Isolated && ClaudeTerminalOverride is null;
        if (ClaudeTerminalOverride is null && !s.Installed)
            body = DockNotice("Claude Code ist auf diesem PC noch nicht installiert.", "Trendo richtet es nach deiner Zustimmung aus der offiziellen Quelle ein. Danach meldest du dich einmal mit deinem Claude-Abo an.",
                Act("claude-dock-install", "Claude Code einrichten", () => _ = DockAfter(ClaudeInstall(), dock, prompt), true));
        else if (ClaudeTerminalOverride is null && !s.Ready)
            body = DockNotice("Claude Code ist installiert, aber noch nicht angemeldet.", "Melde dich einmal mit deinem Claude-Abo an (nicht mit einem API-Schlüssel). Danach läuft Claude Code hier im Studio.",
                Act("claude-dock-login", "Mit dem Abo anmelden", () => _ = DockAfter(ClaudeConnect(), dock, prompt), true));
        else if (testMode)
            body = DockNotice("Testmodus", "Hier würde dein Claude Code im Projektordner starten – im Testmodus startet nichts.", null);
        else
        {
            var term = new TerminalControl { Name = "claude-term", FontSize = 13.5, BufferSize = 5000, Background = Brush.Parse("#FF0C1222"), Foreground = Brush.Parse("#FFE6EDF7"), ShortcutMode = ShortcutMode.Terminal };
            dock.Term = term;
            StartClaudeTerminal(dock, term, prompt);
            body = term;
        }
        var send = Act("claude-dock-type", "Auftrag eintippen", () => _ = ClaudeDockType(dock));
        ToolTip.SetTip(send, "Den Text aus der Auftragsleiste in Claude Code eintippen – abschicken mit Enter tust du selbst");
        var restart = Act("claude-dock-restart", "Neu starten", () => { CloseDock(dock); dock.Term = null; BuildDock(dock, null); });
        var external = Act("claude-dock-window", "Im eigenen Fenster", () => ClaudeOpen(dock.Folder, null, ClaudeRoleFor(dock.Studio), dock.Studio));
        var foot = new WrapPanel { Margin = new Thickness(10, 6), Children = { send, new Border { Width = 6 }, restart, new Border { Width = 6 }, external } };
        var hint = new TextBlock { Text = "Dein echtes Claude Code mit deinem Abo – Trendo zeigt es nur an und schickt nie selbst ab. Abschicken: Enter · Kopieren: Strg+Umschalt+C · Einfügen: Strg+Umschalt+V", Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 0, 10, 8) };
        DockPanel.SetDock(hint, Avalonia.Controls.Dock.Bottom); DockPanel.SetDock(foot, Avalonia.Controls.Dock.Bottom);
        dock.CodeHost.Child = new DockPanel { Children = { hint, foot, body } };
    }

    Control DockNotice(string title, string text, Button? action)
    {
        var sp = new StackPanel { Name = "claude-dock-notice", Spacing = 10, Margin = new Thickness(18), VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, P(text, Muted, 12.5) } };
        if (action is not null) sp.Children.Add(action);
        if (_claudeMsg.Length > 0) sp.Children.Add(P(_claudeMsg, Text, 12));
        return sp;
    }

    async Task DockAfter(Task step, ClaudeDock dock, string? prompt)
    {
        await step; await ClaudeCheck(true);
        BuildDock(dock, prompt);
    }

    /// <summary>Terminal mit Claude Code im Projektordner starten: Rolle und Skills des Studios, API-Variablen geleert (nur Abo), Auftrag nach dem Start eingetippt (ohne Enter – abschicken tut der Nutzer).</summary>
    void StartClaudeTerminal(ClaudeDock dock, TerminalControl term, string? prompt)
    {
        Directory.CreateDirectory(dock.Folder);
        if (dock.Studio is not null) try { Skills.ExportForClaude(dock.Folder, null, dock.Studio); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var env = ClaudeCode.TerminalEnvironment(_core.Secrets.Get(ClaudeTokenKey));
        var (exe, args) = ClaudeTerminalOverride is { } o ? (o.Exe, o.Args) : (ClaudeExe() ?? "claude", ClaudeCode.OpenArgs(null, ClaudeRoleFor(dock.Studio)).ToArray());
        term.EnvironmentVariables = env;
        term.TemplateApplied += (_, _) =>
        {
            try { term.LaunchProcess(dock.Folder, exe, args); }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { _claudeMsg = "Claude Code ließ sich nicht starten: " + e.Message; return; }
            _core.Audit.Append(Principal.LocalOwner.Name, "claude.terminal", Path.GetFileName(dock.Folder));
            if (prompt is { Length: > 0 } && prompt.Trim().Length > 0)
                DispatcherTimer.RunOnce(() => _ = term.SendInputAsync(ClaudeCode.OneLine(prompt)), TimeSpan.FromSeconds(3));
        };
    }

    /// <summary>„Auftrag eintippen“: Text der Auftragsleiste in die Eingabezeile von Claude Code – ohne Enter, abschicken tut der Nutzer
    /// (Entscheidung 01.11.2026). Öffentlich für Tests: Rückgabe = eingetippter Text.</summary>
    public async Task<string?> ClaudeDockType(string? text = null)
    {
        if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) return await ClaudeDockType(d, text);
        return null;
    }

    async Task<string?> ClaudeDockType(ClaudeDock d, string? text = null)
    {
        text ??= this.GetLogicalDescendantsOf<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt")?.Text;
        if (d.Term is not { IsLive: true } term || string.IsNullOrWhiteSpace(text)) return null;
        var line = ClaudeCode.OneLine(text);
        await term.SendInputAsync(line);
        return line;
    }
}

static class LogicalHelp
{
    public static IEnumerable<T> GetLogicalDescendantsOf<T>(this Avalonia.LogicalTree.ILogical l) => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(l).OfType<T>();
}
