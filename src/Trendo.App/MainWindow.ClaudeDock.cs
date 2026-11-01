using Avalonia;
using Avalonia.Controls;
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
        public string Tab = "code"; public Border CodeHost = new(); public Border? WebPane; public NativeWebView? Web; public string WebUrl = "";
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
        foreach (var (k, l, tip) in new[] { ("code", "✳ Claude Code", "Dein Claude Code auf diesem PC – arbeitet direkt in den Trendo-Projektordnern"), ("web", "Claude", "claude.ai mit deinem Abo – Chat, Bilder, Recherche und Claude Code im Web (GitHub-Projekte)") })
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
        dock.Panel.Child = new DockPanel { Children = { head, new Grid { Children = { dock.CodeHost, dock.WebPane } } } };
        ClaudeDockShowTab(dock, dock.Tab);
    }

    void ClaudeDockShowTab(ClaudeDock dock, string tab)
    {
        dock.Tab = tab;
        dock.CodeHost.IsVisible = tab == "code"; dock.WebPane!.IsVisible = tab == "web";
        if (tab == "web" && dock.Web is null) BuildWeb(dock);
        if (dock.Panel.Child is DockPanel dp && dp.Children.OfType<Grid>().FirstOrDefault()?.Children.OfType<StackPanel>().FirstOrDefault(c => c.Name == "claude-tabs") is { } tabs)
            foreach (var b in tabs.Children.OfType<Button>()) b.Background = b.Name == "claude-tab-" + tab ? Accent : Brushes.Transparent;
        if (tab == "web") _core.Audit.Append(Principal.LocalOwner.Name, "claude.web", dock.Studio ?? "");
    }

    /// <summary>Öffentlich für Tests und die Auftragsleiste: Reiter im aktiven Fenster wählen.</summary>
    public void ClaudeDockSelectTab(string tab) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) ClaudeDockShowTab(d, tab); }

    /// <summary>claude.ai als eingebettete Webansicht (Edge WebView2) mit eigenem, dauerhaftem Profil in Trendo – Anmeldung bleibt, getrennt vom Browser.
    /// Neue Fenster (z. B. Links) öffnen im normalen Browser. Im Testmodus wird nichts geladen.</summary>
    void BuildWeb(ClaudeDock dock)
    {
        if (_opt.Isolated) // Testmodus: keine echte Webansicht (WebView2 braucht ein echtes Fenster), nichts wird geladen
        {
            dock.WebUrl = ClaudeWebChat;
            var tb = new WrapPanel { Margin = new Thickness(10, 4) };
            foreach (var (n, l, u) in new[] { ("claude-web-chat", "Chat", ClaudeWebChat), ("claude-web-code", "Claude Code im Web", ClaudeWebCode) }) { var url = u; var b = new Button { Name = n, Content = l, Margin = new Thickness(0, 0, 4, 0) }; b.Click += (_, _) => dock.WebUrl = url; tb.Children.Add(b); }
            DockPanel.SetDock(tb, Avalonia.Controls.Dock.Top);
            dock.WebPane!.Child = new DockPanel { Children = { tb, new Border { Name = "claude-web", Child = P("Testmodus: claude.ai wird nicht geladen.", Muted, 12) } } };
            return;
        }
        var web = new NativeWebView { Name = "claude-web" };
        var profile = Path.Combine(_core.DataRoot, "claude-web");
        web.EnvironmentRequested += (_, e) => { if (e is WindowsWebView2EnvironmentRequestedEventArgs w) { Directory.CreateDirectory(profile); w.UserDataFolder = profile; } };
        web.NewWindowRequested += (_, e) => { if (e.Request is { } u && u.Scheme is "https" or "http") Launch(u.AbsoluteUri); e.Handled = true; };
        dock.Web = web;
        void Go(string url) { dock.WebUrl = url; if (!_opt.Isolated) web.Navigate(new Uri(url)); }
        var bar = new WrapPanel { Margin = new Thickness(10, 4) };
        void B(string name, string label, string tip, Action a) { var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 4, 0), Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) }; ToolTip.SetTip(b, tip); b.Click += (_, _) => a(); bar.Children.Add(b); }
        B("claude-web-chat", "Chat", "Neuer Chat mit Claude", () => Go(ClaudeWebChat));
        B("claude-web-code", "Claude Code im Web", "Claude Code im Browser – arbeitet mit deinen GitHub-Projekten", () => Go(ClaudeWebCode));
        B("claude-web-back", "←", "Zurück", () => web.GoBack());
        B("claude-web-reload", "↻", "Neu laden", () => web.Refresh());
        B("claude-web-send", "Auftrag einfügen", "Den Text aus der Auftragsleiste ins Eingabefeld von Claude schreiben (abschicken tust du selbst)", () => _ = ClaudeWebInsert(dock));
        var note = new TextBlock { Text = _opt.Isolated ? "Testmodus: claude.ai wird nicht geladen." : "claude.ai mit deinem Abo – du meldest dich hier einmal selbst an. Claude im Web sieht deine Dateien auf dem PC nicht; dafür ist der Reiter „Claude Code“ da.",
            Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 0, 10, 6) };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); DockPanel.SetDock(note, Avalonia.Controls.Dock.Bottom);
        dock.WebPane!.Child = new DockPanel { Children = { bar, note, web } };
        Go(ClaudeWebChat);
    }

    /// <summary>Auftrag ins Eingabefeld von claude.ai schreiben – nicht absenden (das entscheidet der Nutzer).</summary>
    async Task ClaudeWebInsert(ClaudeDock dock)
    {
        var text = this.GetLogicalDescendantsOf<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt")?.Text;
        if (dock.Web is null || string.IsNullOrWhiteSpace(text)) return;
        try { await dock.Web.InvokeScript(ClaudeCode.WebInsertScript(text)); }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException) { _claudeMsg = "Der Auftrag ließ sich nicht einfügen: " + e.Message; }
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
        var send = Act("claude-dock-send", "Auftrag senden", () => _ = ClaudeDockSend(dock));
        ToolTip.SetTip(send, "Den Text aus der Auftragsleiste an Claude Code schicken");
        var restart = Act("claude-dock-restart", "Neu starten", () => { CloseDock(dock); dock.Term = null; BuildDock(dock, null); });
        var external = Act("claude-dock-window", "Im eigenen Fenster", () => ClaudeOpen(dock.Folder, null, ClaudeRoleFor(dock.Studio), dock.Studio));
        var foot = new WrapPanel { Margin = new Thickness(10, 6), Children = { send, new Border { Width = 6 }, restart, new Border { Width = 6 }, external } };
        var hint = new TextBlock { Text = "Dein echtes Claude Code mit deinem Abo – Trendo zeigt es nur an. Kopieren: Strg+Umschalt+C · Einfügen: Strg+Umschalt+V", Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 0, 10, 8) };
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

    /// <summary>Terminal mit Claude Code im Projektordner starten: Rolle und Skills des Studios, API-Variablen geleert (nur Abo), Auftrag nach dem Start eingetippt.</summary>
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
                DispatcherTimer.RunOnce(() => _ = term.SendInputAsync(ClaudeCode.OneLine(prompt) + "\r"), TimeSpan.FromSeconds(3));
        };
    }

    /// <summary>„Auftrag senden“: Text der Auftragsleiste als Eingabe an Claude Code (öffentlich für Tests).</summary>
    public async Task ClaudeDockSend(string? text = null)
    {
        if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) await ClaudeDockSend(d, text);
    }

    async Task ClaudeDockSend(ClaudeDock d, string? text = null)
    {
        text ??= this.GetLogicalDescendantsOf<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt")?.Text;
        if (d.Term is not { IsLive: true } term || string.IsNullOrWhiteSpace(text)) return;
        await term.SendInputAsync(ClaudeCode.OneLine(text) + "\r");
    }
}

static class LogicalHelp
{
    public static IEnumerable<T> GetLogicalDescendantsOf<T>(this Avalonia.LogicalTree.ILogical l) => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(l).OfType<T>();
}
