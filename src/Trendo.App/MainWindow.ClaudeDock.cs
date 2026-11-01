using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude im Studio-Fenster (Wunsch 01.11.2026): sieht aus und verhält sich wie Claude im Web oder in der App – Trendo zeigt das echte claude.ai
/// mit dem Abo des Nutzers, kein Terminal, nichts zu kopieren: Man tippt direkt in Claude. Links stehen die Claude-Projekte des Studios (fortsetzen, neu,
/// entfernen); ihre Anweisungen legen Rolle, Fachregeln und Fachgebiet fest – im Spiele-Studio also Spieleentwicklung, kein Webdesign. Trendo bedient die
/// Seite nie selbst. Die Fläche bleibt beim Neuzeichnen der Seite erhalten.</summary>
public partial class MainWindow
{
    sealed class ClaudeDock
    {
        public required Border Panel; public required string Folder; public string? Studio; public Grid? Split; public ContentControl? PageHost;
        public bool Max = true; public string Tab = "chat"; public Border? ChatPane; public Border? WebPane; public Border? AgentPane; public StackPanel? WebBar; public StackPanel? ProjectList;
        public NativeWebView? Web; public string WebUrl = "";
    }
    readonly Dictionary<string, ClaudeDock> _claudeDocks = [];
    /// <summary>Farben der Claude-App (dunkles Design): warmes Anthrazit, helle Schrift, Claude-Orange für Aktives.</summary>
    static readonly IBrush ClaudeBg = Brush.Parse("#FF262624"), ClaudeBar = Brush.Parse("#FF1F1E1D"), ClaudeFg = Brush.Parse("#FFF5F4EF"), ClaudeMuted = Brush.Parse("#FFB7B5A9"),
        ClaudeAccent = Brush.Parse("#FFD97757"), ClaudeLine = Brush.Parse("#FF3E3E38"), ClaudeItem = Brush.Parse("#FF3A3935");
    public bool ClaudeDockOpen(string winId) => _claudeDocks.ContainsKey(winId);
    public string? ClaudeDockTab(string winId) => _claudeDocks.TryGetValue(winId, out var d) ? d.Tab : null;
    public string? ClaudeDockWebUrl(string winId) => _claudeDocks.TryGetValue(winId, out var d) ? d.WebUrl : null;
    public const string ClaudeWebChat = "https://claude.ai/new", ClaudeWebCode = "https://claude.ai/code";
    public string? ActiveWindowId => Windows.Active?.Id;

    /// <summary>Seiteninhalt mit der Claude-Fläche (wenn für dieses Fenster geöffnet). Das Raster bleibt je Fenster dasselbe, nur der Seiteninhalt wechselt –
    /// so bleibt claude.ai beim Neuzeichnen geladen.</summary>
    Control WithClaudeDock(string winId, Control content)
    {
        if (!_claudeDocks.TryGetValue(winId, out var dock)) return content;
        if (dock.Split is null)
        {
            dock.PageHost = new ContentControl();
            var split = new GridSplitter { Width = 6, Background = ClaudeLine, ResizeDirection = GridResizeDirection.Columns };
            Grid.SetColumn(split, 1); Grid.SetColumn(dock.Panel, 2);
            dock.Split = new Grid { Name = "claude-split", ColumnDefinitions = new ColumnDefinitions("*,6,620"), Children = { dock.PageHost, split, dock.Panel } };
        }
        dock.PageHost!.Content = content;
        if (dock.Split.ColumnDefinitions[2].Width.IsAbsolute == dock.Max) ClaudeDockSetMax(dock, dock.Max); // beim ersten Mal: groß wie in der Claude-App
        return dock.Split;
    }

    /// <summary>Claude-Fläche im aktiven Fenster öffnen oder schließen (Knopf in der Auftragsleiste).</summary>
    public void ClaudeDockToggle(string? studio, string? prompt = null)
    {
        if (Windows.Active?.Id is not { } id) return;
        if (_claudeDocks.Remove(id, out var old)) { CloseChat(old); Show(CurrentView); return; }
        var dock = new ClaudeDock { Folder = ClaudeFolderFor(studio), Studio = studio, Panel = new Border { Name = "claude-dock", Background = ClaudeBg, BorderBrush = ClaudeLine, BorderThickness = new Thickness(1, 0, 0, 0) } };
        _claudeDocks[id] = dock;
        BuildShell(dock);
        if (prompt is { Length: > 0 } && prompt.Trim().Length > 0 && _chatUis.TryGetValue(dock, out var ui)) ui.Input.Text = prompt.Trim(); // Auftrag steht im Eingabefeld, abschicken tust du
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.dock", studio ?? "");
        Show(CurrentView);
    }

    /// <summary>Fenster geschlossen: seine Claude-Fläche vergessen.</summary>
    void ClaudeDockWindowClosed(string winId) { if (_claudeDocks.Remove(winId, out var d)) CloseChat(d); }

    /// <summary>Laufende Antwort anhalten und den Chat dieses Bereichs vergessen.</summary>
    void CloseChat(ClaudeDock d) { if (_chatUis.Remove(d, out var ui)) ui.Cts?.Cancel(); }

    /// <summary>Rahmen wie in der Claude-App: schmale Kopfleiste mit „Claude“ und dem Trendo-Agenten, rechts „Groß“ und Schließen.</summary>
    void BuildShell(ClaudeDock dock)
    {
        var head = new Grid { Name = "claude-head", ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var tabs = new StackPanel { Name = "claude-tabs", Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (k, l, tip) in new[] { ("chat", "✳ Claude", "Claude mit deinem Abo – arbeitet wie in Claude direkt in den Projekten dieses Studios"), ("web", "claude.ai", "claude.ai mit deinem Abo – Chats, Projekte und Claude Code im Web"), ("agent", "Trendo-Agent", "Arbeitet mit der KI, die Trendo hat – auch ohne Abo: Wunsch eintippen, „Los“, am Ende nur Ja oder Nein") })
        {
            var tab = k;
            var b = new Button { Name = "claude-tab-" + k, Content = l, Padding = new Thickness(12, 4), CornerRadius = new CornerRadius(8), Foreground = ClaudeFg, BorderThickness = new Thickness(0), FontSize = 13 };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => ClaudeDockShowTab(dock, tab);
            tabs.Children.Add(b);
        }
        var where = new TextBlock { Name = "claude-dock-studio", Text = StudioCatalog.NameOf(dock.Studio ?? "", ""), Foreground = ClaudeMuted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        var close = new Button { Name = "claude-dock-close", Content = "✕", Padding = new Thickness(8, 2), Background = Brushes.Transparent, Foreground = ClaudeFg };
        ToolTip.SetTip(close, "Claude schließen"); close.Click += (_, _) => ClaudeDockToggle(dock.Studio, null);
        var max = new Button { Name = "claude-dock-max", Content = dock.Max ? "⤡ Studio zeigen" : "⤢ Groß", Padding = new Thickness(8, 2), Background = Brushes.Transparent, Foreground = ClaudeFg };
        ToolTip.SetTip(max, "Claude im ganzen Fenster – wie in der Claude-App; noch einmal klicken holt das Studio zurück");
        max.Click += (_, _) => { ClaudeDockSetMax(dock, !dock.Max); max.Content = dock.Max ? "⤡ Studio zeigen" : "⤢ Groß"; };
        head.Children.Add(tabs); head.Children.Add(Pos(where, 1)); head.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { max, close } }, 2));
        var headBar = new Border { Background = ClaudeBar, BorderBrush = ClaudeLine, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 6), Child = head };
        DockPanel.SetDock(headBar, Avalonia.Controls.Dock.Top);
        dock.ChatPane = new Border { Name = "claude-chat-pane", IsVisible = false };
        dock.WebPane = new Border { Name = "claude-web-pane", IsVisible = false };
        dock.AgentPane = new Border { Name = "claude-agent-pane", IsVisible = false, Background = Brush.Parse("#FF0C1222") };
        dock.Panel.Child = new DockPanel { Children = { headBar, new Grid { Children = { dock.ChatPane, dock.WebPane, dock.AgentPane } } } };
        ClaudeDockShowTab(dock, dock.Tab);
    }

    /// <summary>Claude im ganzen Studiofenster (Studio bleibt dahinter erhalten) oder wieder daneben.</summary>
    void ClaudeDockSetMax(ClaudeDock dock, bool on)
    {
        dock.Max = on;
        if (dock.Split is not { } g) return;
        g.ColumnDefinitions[0].Width = on ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        g.ColumnDefinitions[1].Width = new GridLength(on ? 0 : 6);
        g.ColumnDefinitions[2].Width = on ? new GridLength(1, GridUnitType.Star) : new GridLength(620);
        if (dock.PageHost is { } ph) ph.IsVisible = !on;
    }

    /// <summary>Öffentlich für Tests: Ist Claude im Fenster groß?</summary>
    public bool ClaudeDockIsMax(string winId) => _claudeDocks.TryGetValue(winId, out var d) && d.Max;

    void ClaudeDockShowTab(ClaudeDock dock, string tab)
    {
        dock.Tab = tab;
        dock.ChatPane!.IsVisible = tab == "chat"; dock.WebPane!.IsVisible = tab == "web"; dock.AgentPane!.IsVisible = tab == "agent";
        if (tab == "chat" && dock.ChatPane.Child is null) BuildChatPane(dock);
        if (tab == "web" && dock.WebPane.Child is null) BuildWeb(dock);
        if (tab == "agent" && dock.AgentPane.Child is null) BuildAgentPane(dock);
        foreach (var b in dock.Panel.GetLogicalDescendantsOf<Button>().Where(b => b.Name?.StartsWith("claude-tab-") == true))
            b.Background = b.Name == "claude-tab-" + tab ? ClaudeItem : Brushes.Transparent;
        if (tab == "web") _core.Audit.Append(Principal.LocalOwner.Name, "claude.web", dock.Studio ?? "");
    }

    /// <summary>Öffentlich für Tests und die Auftragsleiste: Reiter im aktiven Fenster wählen.</summary>
    public void ClaudeDockSelectTab(string tab) { if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) ClaudeDockShowTab(d, tab); }

    public ClaudeWeb CWeb => new(_core.DataRoot);

    /// <summary>Webansicht für claude.ai mit dem dauerhaften Trendo-Profil der Person (einmal anmelden, dann in allen Studios angemeldet).
    /// Anmeldefenster (Google, Apple, SSO) öffnet WebView2 selbst mit demselben Profil, damit die Anmeldung in Trendo ankommt; andere Links öffnen im normalen Browser.
    /// <paramref name="navigated"/> bekommt jede fertig geladene Adresse.</summary>
    NativeWebView ClaudeWebView(string name, Action<Uri> navigated)
    {
        var web = new NativeWebView { Name = name };
        var profile = CWeb.Profile;
        web.EnvironmentRequested += (_, e) => { if (e is WindowsWebView2EnvironmentRequestedEventArgs w) { Directory.CreateDirectory(profile); w.UserDataFolder = profile; } };
        web.NewWindowRequested += (_, e) =>
        {
            if (e.Request is { } u && ClaudeWeb.IsAuthPopup(u)) { e.Handled = false; return; } // Anmeldefenster in Trendo lassen
            if (e.Request is { } l && l.Scheme is "https" or "http") Launch(l.AbsoluteUri);
            e.Handled = true;
        };
        web.NavigationCompleted += (_, _) => { if (web.Source is { } u) navigated(u); };
        return web;
    }

    /// <summary>Navigation im Claude-Reiter verarbeiten (öffentlich für Tests): Anmeldung erkennen, Chat und neues Projekt dem Studio zuordnen.</summary>
    public void ClaudeWebNavigated(string url)
    {
        if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) WebNavigated(d, new Uri(url));
    }

    void WebNavigated(ClaudeDock dock, Uri u)
    {
        dock.WebUrl = u.AbsoluteUri;
        var w = CWeb; var was = w.Connected; var pending = w.PendingOf(dock.Studio);
        if (!w.Navigated(u, dock.Studio)) { BuildWebBar(dock); BuildProjectList(dock); return; } // z. B. fremdes Projekt: „übernehmen“ anbieten
        if (!was && w.Connected) _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.verbunden", "");
        if (pending is not null && w.PendingOf(dock.Studio) is null) { _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.projekt", dock.Studio ?? ""); _claudeMsg = $"✓ Projekt „{pending}“ gehört jetzt zu diesem Studio – du findest es links und kannst es jederzeit fortsetzen."; }
        BuildWebBar(dock); BuildProjectList(dock);
    }

    /// <summary>Claude-Reiter: links die Projekte des Studios, rechts claude.ai (angemeldet: zuletzt geöffnetes Projekt mit seinen Chats; sonst Anmeldung).
    /// Im Testmodus wird nichts geladen.</summary>
    void BuildWeb(ClaudeDock dock)
    {
        dock.WebBar = new StackPanel { Name = "claude-web-bar", Spacing = 6 };
        dock.ProjectList = new StackPanel { Name = "claude-projects", Spacing = 4 };
        Control view;
        if (_opt.Isolated) view = new Border { Name = "claude-web", Background = ClaudeBg, Child = new TextBlock { Text = "Testmodus: claude.ai wird nicht geladen.", Foreground = ClaudeMuted, Margin = new Thickness(16) } };
        else { dock.Web = ClaudeWebView("claude-web", u => WebNavigated(dock, u)); view = dock.Web; }
        var bar = new Border { Background = ClaudeBar, BorderBrush = ClaudeLine, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 8), Child = dock.WebBar };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        var side = new Border { Name = "claude-side", Background = ClaudeBar, BorderBrush = ClaudeLine, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(10),
            Child = new ScrollViewer { Content = dock.ProjectList } };
        var main = new DockPanel { Background = ClaudeBg, Children = { bar, view } };
        Grid.SetColumn(main, 1);
        dock.WebPane!.Child = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*"), Children = { side, main } };
        BuildWebBar(dock); BuildProjectList(dock);
        WebGo(dock, CWeb.StartUrl(dock.Studio));
    }

    void WebGo(ClaudeDock dock, string url) { dock.WebUrl = url; if (dock.Web is { } w) w.Navigate(new Uri(url)); }

    /// <summary>Linke Spalte wie in Claude: „Projekte in diesem Studio“ – anklicken setzt fort, ✕ entfernt aus der Liste, „＋ Neues Projekt“ legt eins an.</summary>
    void BuildProjectList(ClaudeDock dock)
    {
        if (dock.ProjectList is not { } list) return;
        list.Children.Clear();
        var w = CWeb;
        list.Children.Add(new TextBlock { Text = "Projekte in diesem Studio", Foreground = ClaudeFg, FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 0, 0, 4) });
        if (!w.Connected) { list.Children.Add(new TextBlock { Text = "Nach der Anmeldung stehen hier deine Projekte.", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap }); return; }
        if (dock.Studio is null) { list.Children.Add(new TextBlock { Text = "Öffne Claude in einem Studio, um Projekte dafür anzulegen.", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap }); return; }
        var name = new TextBox { Name = "claude-newproj-name", PlaceholderText = "Name, z. B. Mein Jump-and-Run", FontSize = 12 };
        var add = new Button { Name = "claude-newproj-go", Content = "＋ Neues Projekt", HorizontalAlignment = HorizontalAlignment.Stretch, Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), BorderThickness = new Thickness(0) };
        ToolTip.SetTip(add, "Neues Claude-Projekt für dieses Studio – mit Rolle, Fachregeln und Fachgebiet");
        add.Click += (_, _) => _ = ClaudeProjectSetup(dock, name.Text);
        list.Children.Add(name); list.Children.Add(add);
        var projects = w.ProjectsOf(dock.Studio);
        var current = ClaudeWeb.ProjectId(new Uri(dock.WebUrl.Length > 0 ? dock.WebUrl : ClaudeWeb.NewChat));
        for (var i = 0; i < projects.Count; i++)
        {
            var p = projects[i];
            var open = new Button { Name = $"claude-proj-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), BorderThickness = new Thickness(0),
                Background = p.Id == current ? ClaudeItem : Brushes.Transparent, Foreground = ClaudeFg,
                Content = new StackPanel { Children = { new TextBlock { Text = p.Name, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = "zuletzt " + p.Last.LocalDateTime.ToString("dd.MM. HH:mm", De), FontSize = 10.5, Foreground = ClaudeMuted } } } };
            ToolTip.SetTip(open, "Fortsetzen – Claude zeigt die Chats dieses Projekts");
            open.Click += (_, _) => { WebGo(dock, ClaudeWeb.ProjectUrl(p.Id)); CWeb.Navigated(new Uri(ClaudeWeb.ProjectUrl(p.Id)), dock.Studio); BuildProjectList(dock); };
            var del = new Button { Name = $"claude-proj-del-{i}", Content = "✕", Padding = new Thickness(6, 2), Background = Brushes.Transparent, Foreground = ClaudeMuted, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(del, "Aus der Liste entfernen (ganz löschen geht in Claude im Menü des Projekts)");
            del.Click += (_, _) => { CWeb.RemoveProject(dock.Studio!, p.Id); _claudeMsg = $"„{p.Name}“ aus der Liste entfernt. Bei Claude bleibt es, bis du es dort im Menü des Projekts löschst."; BuildProjectList(dock); BuildWebBar(dock); };
            DockPanel.SetDock(del, Avalonia.Controls.Dock.Right);
            list.Children.Add(new DockPanel { Children = { del, open } });
        }
        if (projects.Count == 0) list.Children.Add(new TextBlock { Text = "Noch kein Projekt. Leg eins an – Claude bleibt darin immer beim Fachgebiet dieses Studios.", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (ClaudeWeb.ProjectId(new Uri(dock.WebUrl.Length > 0 ? dock.WebUrl : ClaudeWeb.NewChat)) is { } shown && projects.All(p => p.Id != shown))
        {
            var take = new Button { Name = "claude-proj-take", Content = "Dieses Projekt übernehmen", HorizontalAlignment = HorizontalAlignment.Stretch, Background = ClaudeItem, Foreground = ClaudeFg, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 0) };
            ToolTip.SetTip(take, "Das gerade geöffnete Claude-Projekt zu diesem Studio hinzufügen");
            take.Click += (_, _) => { CWeb.AddProject(dock.Studio!, new Uri(dock.WebUrl), name.Text is { Length: > 0 } n ? n : "Projekt"); BuildProjectList(dock); };
            list.Children.Add(take);
        }
    }

    /// <summary>Schmale Leiste über claude.ai: Stand und die wenigen Knöpfe, die Trendo dazugibt.</summary>
    void BuildWebBar(ClaudeDock dock)
    {
        if (dock.WebBar is null) return;
        dock.WebBar.Children.Clear();
        var w = CWeb; var connected = w.Connected; var studioName = StudioCatalog.NameOf(dock.Studio ?? "", "dieses Studio");
        var pending = w.PendingOf(dock.Studio);
        var stateText = !connected ? "Einmal anmelden: Melde dich unten mit deinem Claude-Abo an – danach ist Claude in allen Studios verbunden."
            : pending is not null ? $"Neues Projekt „{pending}“: In Claude oben „Neues Projekt“, als Namen „{ClaudeWeb.ProjectName(studioName, pending)}“, bei „Anweisungen“ Strg+V, dann erstellen."
            : w.ProjectOf(dock.Studio) is not null ? $"✳ Claude für {studioName} – tipp einfach unten in Claude."
            : $"✓ Verbunden. Leg links ein Projekt an – dann bleibt Claude bei {studioName}.";
        dock.WebBar.Children.Add(new TextBlock { Name = "claude-web-state", Text = stateText, Foreground = connected && pending is null ? ClaudeFg : ClaudeAccent, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        var row = new WrapPanel();
        void B(string name, string label, string tip, Action a, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(8),
                Background = primary ? ClaudeAccent : ClaudeItem, Foreground = primary ? Brushes.White : ClaudeFg, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => a(); row.Children.Add(b);
        }
        if (connected)
        {
            B("claude-web-chat", "Neuer Chat", "Neuen Chat beginnen – im geöffneten Projekt dieses Studios", () => { if (dock.Studio is { } st) CWeb.NewStudioChat(st); WebGo(dock, w.ProjectOf(dock.Studio) is { } p ? ClaudeWeb.ProjectUrl(p) : ClaudeWeb.NewChat); });
            B("claude-web-code", "Claude Code", "Claude Code im Web – baut mit dir an deinen GitHub-Projekten", () => WebGo(dock, ClaudeWebCode));
            B("claude-web-skills", "Fachregeln", "Die Fachregeln dieses Studios als Skill-Paket speichern – einmal in Claude unter Einstellungen → Fähigkeiten hochladen; deine eigenen Claude-Skills gelten sowieso", () => Try(CurrentView, () => ClaudeSkillPackage(dock.Studio)));
        }
        else B("claude-web-login", "Zur Anmeldung", "Anmeldeseite von claude.ai öffnen", () => WebGo(dock, ClaudeWeb.Login), true);
        B("claude-web-back", "←", "Zurück", () => dock.Web?.GoBack());
        B("claude-web-reload", "↻", "Neu laden", () => dock.Web?.Refresh());
        dock.WebBar.Children.Add(row);
        if (!connected) dock.WebBar.Children.Add(ClaudeLoginLinkRow("claude-web-link", u => WebGo(dock, u.AbsoluteUri)));
        if (_claudeMsg.Length > 0 && (_claudeMsg.StartsWith("Fachregeln") || _claudeMsg.StartsWith("✓ Projekt") || _claudeMsg.Contains("aus der Liste entfernt")))
            dock.WebBar.Children.Add(new TextBlock { Name = "claude-web-msg", Text = _claudeMsg, Foreground = ClaudeAccent, FontSize = 12, TextWrapping = TextWrapping.Wrap });
    }

    /// <summary>Neues Projekt für das Studio (öffentlich für Tests: Rückgabe = Anweisungen): Name merken, Anweisungen (Rolle, Fachregeln, Fachgebiet)
    /// einmal in die Zwischenablage, Projektliste von Claude öffnen. Sobald der Nutzer das Projekt erstellt hat, steht es links in der Liste.</summary>
    public async Task<string?> ClaudeProjectSetup(string? name = null)
    {
        if (Windows.Active?.Id is { } id && _claudeDocks.TryGetValue(id, out var d)) return await ClaudeProjectSetup(d, name);
        return null;
    }

    async Task<string?> ClaudeProjectSetup(ClaudeDock dock, string? name)
    {
        if (dock.Studio is null) return null;
        var studioName = StudioCatalog.NameOf(dock.Studio, "Trendo");
        var n = string.IsNullOrWhiteSpace(name) ? $"Projekt {CWeb.ProjectsOf(dock.Studio).Count + 1}" : name.Trim();
        CWeb.BeginProject(dock.Studio, n);
        var text = ClaudeWeb.ProjectInstructions(studioName, ClaudeRoleFor(dock.Studio), Skills.For(null, dock.Studio));
        LastClipboardText = text;
        try { if (Clipboard is { } cb) await cb.SetTextAsync(text); }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException) { _claudeMsg = "Kopieren ging nicht: " + e.Message; }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.projekt.start", dock.Studio);
        WebGo(dock, ClaudeWeb.Projects);
        BuildWebBar(dock); BuildProjectList(dock);
        return text;
    }

    /// <summary>Feld für den Anmeldelink aus der E-Mail von Claude: Bei „Mit E-Mail fortfahren“ schickt Claude einen Link. Öffnet man ihn im normalen Browser,
    /// ist man nur dort angemeldet – hier eingefügt, meldet er Trendo an.</summary>
    Control ClaudeLoginLinkRow(string name, Action<Uri> go)
    {
        var box = new TextBox { Name = name, PlaceholderText = "Anmeldelink aus der E-Mail von Claude hier einfügen", MinWidth = 260, FontSize = 12 };
        var msg = new TextBlock { Name = name + "-msg", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
            Text = "Mit E-Mail anmelden: Claude schickt dir einen Link. Nicht anklicken – in der E-Mail mit Rechtsklick „Link kopieren“ und hier einfügen. Klappt Google nicht, nimm „Mit E-Mail fortfahren“ mit derselben Adresse." };
        void Open()
        {
            if (ClaudeWeb.LoginLink(box.Text) is { } u) { box.Text = ""; msg.Text = "Link geöffnet – einen Moment …"; _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.link", ""); go(u); }
            else msg.Text = "Das ist kein Anmeldelink von claude.ai. Bitte den ganzen Link aus der E-Mail einfügen (beginnt mit https://claude.ai/).";
        }
        box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { Open(); e.Handled = true; } };
        var open = Act(name + "-open", "Öffnen", Open, true);
        DockPanel.SetDock(open, Avalonia.Controls.Dock.Right);
        return new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0), Children = { new DockPanel { Children = { open, box } }, msg } };
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
}

static class LogicalHelp
{
    public static IEnumerable<T> GetLogicalDescendantsOf<T>(this Avalonia.LogicalTree.ILogical l) => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(l).OfType<T>();
}
