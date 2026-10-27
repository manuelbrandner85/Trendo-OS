using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Shell;

namespace Trendo.App;

/// <summary>Startmenü wie bei Windows: Startknopf unten links, Suche, Angeheftet, Zuletzt benutzt, alle Studios A–Z. Windows-Taste öffnet, Esc schließt.</summary>
public partial class MainWindow
{
    private readonly Border _startMenu = new() { Name = "start-menu", IsVisible = false };
    private readonly TextBox _smSearch = new() { Name = "startmenu-search", PlaceholderText = "Programme, Studios, Dateien und Einstellungen suchen" };
    private readonly StackPanel _smBody = new() { Spacing = 12 };
    private readonly StackPanel _smCats = new() { Spacing = 4, Width = 196 };
    private string _smFilter = "alle";
    public bool StartMenuOpen => _startMenu.IsVisible;

    private void InitStartMenu()
    {
        foreach (var k in new[] { "brief", "akten", "akte", "ausgabe", "briefkoepfe" }) _windowGroups[k] = "brief";
        if (_windowGroups.TryGetValue("doku-pdf", out var doku)) _windowGroups["office"] = doku;
        _startMenu.Width = 980; _startMenu.MaxHeight = 680; _startMenu.Background = GlassStrong; _startMenu.BorderBrush = Edge; _startMenu.BorderThickness = new Thickness(1);
        _startMenu.CornerRadius = new CornerRadius(22); _startMenu.Padding = new Thickness(16); _startMenu.BoxShadow = BoxShadows.Parse("0 24 60 0 #88000000");
        _startMenu.HorizontalAlignment = HorizontalAlignment.Center; _startMenu.VerticalAlignment = VerticalAlignment.Center; _startMenu.Margin = new Thickness(0, 0, 0, 24);
        _startMenu.ZIndex = 1000;
        _smSearch.Height = 40; _smSearch.FontSize = 14; _smSearch.Background = Brushes.Transparent; _smSearch.BorderThickness = new Thickness(0); _smSearch.VerticalContentAlignment = VerticalAlignment.Center;
        _smSearch.TextChanged += (_, _) => FillStartMenu();
        _smSearch.KeyDown += async (_, e) => { if (e.Key == Key.Enter && StartHits(_smSearch.Text ?? "").FirstOrDefault() is { } h) { CloseStartMenu(); await OpenStartHit(h); } };
        var search = new Border { Height = 44, CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#66101830", "#CCFFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), Padding = new Thickness(12, 0), Margin = new Thickness(0, 0, 0, 12),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { new Border { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ico(Mdi.Magnify, 16, Muted) }, Pos(_smSearch, 1) } } };
        var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*,210") };
        cols.Children.Add(_smCats);
        var mid = new ScrollViewer { Content = _smBody, Margin = new Thickness(8, 0, 8, 0) };
        Grid.SetColumn(mid, 1); cols.Children.Add(mid);
        var user = SmUserColumn(); Grid.SetColumn(user, 2); cols.Children.Add(user);
        _startMenu.Child = new DockPanel { Children = { Top(search), cols } };
        if (_center.Parent is Grid body) { Grid.SetColumnSpan(_startMenu, Math.Max(1, body.ColumnDefinitions.Count)); Grid.SetRowSpan(_startMenu, 2); body.Children.Add(_startMenu); }
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is Key.LWin or Key.RWin) { ToggleStartMenu(); e.Handled = true; }
            else if (e.Key == Key.Escape && StartMenuOpen) { CloseStartMenu(); e.Handled = true; }
            else if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !StartMenuOpen)
            { CycleWindows(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // Klick daneben schließt das Menü
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!StartMenuOpen || e.Source is not Visual v) return;
            for (Visual? p = v; p is not null; p = p.GetVisualParentSafe()) if (p == _startMenu || (p is Control c && c.Name == "start-button")) return;
            CloseStartMenu();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        static Control Top(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Top); return c; }
    }

    public void ToggleStartMenu() { if (StartMenuOpen) CloseStartMenu(); else OpenStartMenu(); }
    public void OpenStartMenu()
    {
        _smFilter = "alle";
        var w = Bounds.Width > 0 ? Math.Clamp(Bounds.Width - 160, 640, 1100) : 980;
        var h = Bounds.Height > 0 ? Math.Clamp(Bounds.Height - 96, 480, 760) : 680;
        _startMenu.Width = w; _startMenu.MaxHeight = h;
        _smSearch.Text = ""; FillStartMenu(); _startMenu.IsVisible = true; _smSearch.Focus();
    }
    public void CloseStartMenu() => _startMenu.IsVisible = false;

    /// <summary>Startknopf ganz links in der Taskleiste (Logo A „Obsidian-Kachel“).</summary>
    private Button StartButton()
    {
        var b = new Button { Name = "start-button", Width = 50, Height = 50, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = Logo(38) };
        ToolTip.SetTip(b, "Start"); Avalonia.Automation.AutomationProperties.SetName(b, "Start");
        // Dock liegt am unteren Rand: Hinweis über dem Knopf, nie darauf
        ToolTip.SetPlacement(b, PlacementMode.Top); ToolTip.SetVerticalOffset(b, -6);
        if (b.Content is Control logo) ToolTip.SetTip(logo, null);
        b.Click += (_, _) => ToggleStartMenu();
        return b;
    }

    public void NoteRecent(string key)
    {
        if (key is "start" or "einrichtung" || key.StartsWith("einrichtung-")) return;
        var l = StartMenu.Touch(_settings.RecentPages, key);
        if (!l.SequenceEqual(_settings.RecentPages)) { _settings.RecentPages = l; SaveSettings(); }
    }

    public string PageTitle(string key)
    {
        if (key.StartsWith("studio-status-", StringComparison.OrdinalIgnoreCase))
            return StudioCatalog.NameOf(key["studio-status-".Length..], key);
        if (IsStudioWorkspace(key) && ModuleOf(key) is { } mid && StudioCatalog.For(mid) is { } info) return info.Name;
        return Pages.Concat(ExtraPages).FirstOrDefault(p => p.Key == key).Title
            ?? _studios.FirstOrDefault(s => PageForStudio(s.Id) == key)?.Name
            ?? key switch { "notbuilt" => "Noch nicht gebaut", "studio-deps" => "Benötigte Programme", "akte" => "Akte", "ausgabe" => "Ausgabe", _ => char.ToUpper(key[0]) + key[1..] };
    }

    /// <summary>Alle Katalog-Studios, plus Einträge aus der Modulliste, die der Katalog noch nicht kennt.</summary>
    private List<(string Id, string Name)> MenuStudios()
    {
        var list = new List<(string Id, string Name)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, info) in StudioCatalog.ById) { seen.Add(id); list.Add((id, info.Name)); }
        foreach (var s in _studios) if (seen.Add(s.Id)) list.Add((s.Id, s.Name));
        return list;
    }

    private List<SearchHit> StartHits(string q) =>
        BuildSearch().Find(q).Where(h => !h.Target.StartsWith("filter:", StringComparison.OrdinalIgnoreCase) && !string.Equals(h.Target, "page:studios", StringComparison.OrdinalIgnoreCase)).Take(12).ToList();

    private async Task OpenStartHit(SearchHit h)
    {
        if (h.Target.StartsWith("studio:", StringComparison.OrdinalIgnoreCase))
        { await ActivateOrOpenStudio(h.Target[(h.Target.IndexOf(':') + 1)..], h.Title); return; }
        await OpenHit(h);
    }

    private static string? HitStudio(SearchHit h)
    {
        var i = h.Target.IndexOf(':');
        if (i < 0) return null;
        return h.Target.StartsWith("studio:", StringComparison.OrdinalIgnoreCase) ? h.Target[(i + 1)..] : null;
    }

    public void PinStudio(string id) { _settings.PinnedStudios = StartMenu.Pin(_settings.PinnedStudios, id); SaveSettings(); FillStartMenu(); RefreshPins(); }
    public void UnpinStudio(string id) { _settings.PinnedStudios = StartMenu.Unpin(_settings.PinnedStudios, id); SaveSettings(); FillStartMenu(); RefreshPins(); }

    /// <summary>Symbol aus der zentralen Studio-Symbolkarte (StudioIcons), sonst keins.</summary>
    private static Control? IconFor(string? studioId, double size) => StudioIcons.For(studioId) is { } i ? Ico(i.Path, size, Grad(i.C1, i.C2)) : null;

    private Button SmItem(string name, string text, string? sub, Func<Task> go, string? studioId = null)
    {
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = text, Foreground = Text, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis } } };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (IconFor(studioId, 18) is { } ic) row.Children.Add(ic);
        row.Children.Add(lines);
        var b = new Button { Name = name, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5), Content = row };
        if (sub is not null) lines.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 11 });
        Avalonia.Automation.AutomationProperties.SetName(b, text);
        b.Click += async (_, _) => { CloseStartMenu(); await go(); };
        return b;
    }

    private Control SmUserColumn()
    {
        var name = string.IsNullOrWhiteSpace(_settings.Name) ? "Lokaler Besitzer" : _settings.Name;
        var sp = new StackPanel { Spacing = 6, Margin = new Thickness(8, 0, 0, 0) };
        sp.Children.Add(new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Background = Brush.Parse("#332F6BFF"), HorizontalAlignment = HorizontalAlignment.Center, Child = Ico(Mdi.AccountCircleOutline, 36, Brushes.White) });
        sp.Children.Add(new TextBlock { Text = name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
        Button Act(string n, string label, string icon, Action a, string? color = null)
        {
            var fg = color is null ? Text : Brush.Parse(color);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ico(icon, 16, fg), new TextBlock { Text = label, Foreground = fg, FontSize = 13, VerticalAlignment = VerticalAlignment.Center } } };
            var b = new Button { Name = n, Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(8, 6) };
            Avalonia.Automation.AutomationProperties.SetName(b, label);
            b.Click += (_, _) => { CloseStartMenu(); a(); };
            return b;
        }
        sp.Children.Add(Act("sm-profile", "Profil", Mdi.AccountCircleOutline, () => Show("settings")));
        sp.Children.Add(Act("sm-settings", "Einstellungen", Mdi.Cog, () => Show("settings")));
        sp.Children.Add(Act("sm-desktop", "Startbildschirm", Mdi.Monitor, () => Show("start")));
        sp.Children.Add(Act("sm-help", "Hilfe", Mdi.HelpCircle, () => { Show("start"); ShowHelp("start", force: true); }));
        sp.Children.Add(Act("sm-restart", "Neu starten", Mdi.Restart, () => Reopen("start")));
        sp.Children.Add(Act("sm-exit", "Trendo beenden", Mdi.Power, () => Close(), "#F07178"));
        return sp;
    }

    private void FillStartMenu()
    {
        _smCats.Children.Clear();
        void Cat(string id, string label, string icon)
        {
            var on = _smFilter == id && string.IsNullOrWhiteSpace(_smSearch.Text);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ico(icon, 16, Text), new TextBlock { Text = label, Foreground = Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center } } };
            var b = new Button { Name = "sm-cat-" + id.Replace(':', '-').Replace(' ', '-').Replace('&', '-'), Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 7), Background = on ? Accent : Brushes.Transparent, Foreground = Text, CornerRadius = new CornerRadius(10) };
            Avalonia.Automation.AutomationProperties.SetName(b, label);
            b.Click += (_, _) => { _smFilter = id; if (!string.IsNullOrEmpty(_smSearch.Text)) _smSearch.Text = ""; else FillStartMenu(); };
            _smCats.Children.Add(b);
        }
        Cat("alle", "Alle", Mdi.Apps);
        Cat("pin", "Angeheftet", Mdi.StarFourPoints);
        Cat("recent", "Zuletzt verwendet", Mdi.CalendarMonth);
        Cat("studios", "Studios", Mdi.ViewGridOutline);
        foreach (var area in StudioCatalog.Areas) Cat("area:" + area, area, AreaIcon(area));

        _smBody.Children.Clear();
        var q = _smSearch.Text ?? "";
        if (q.Trim().Length > 0)
        {
            var hits = StartHits(q);
            _smBody.Children.Add(H("Suchergebnisse", 15));
            if (hits.Count == 0) _smBody.Children.Add(P("Nichts gefunden. Versuch ein anderes Wort.", Muted));
            foreach (var (h, i) in hits.Select((h, i) => (h, i))) _smBody.Children.Add(SmItem("sm-hit-" + i, h.Title, h.Kind, () => OpenStartHit(h), HitStudio(h)));
            return;
        }
        var studios = MenuStudios();
        var byId = studios.ToDictionary(s => s.Id, s => s.Name, StringComparer.OrdinalIgnoreCase);
        var showPins = _smFilter is "alle" or "pin";
        var showRecent = _smFilter is "alle" or "recent";
        var showAz = _smFilter is "alle" or "studios" || _smFilter.StartsWith("area:", StringComparison.Ordinal);
        if (showPins)
        {
            var pins = new WrapPanel();
            foreach (var id in _settings.PinnedStudios.Where(byId.ContainsKey))
                pins.Children.Add(PinTile(id, byId[id]));
            _smBody.Children.Add(H("Angeheftete Apps und Studios", 15));
            _smBody.Children.Add(pins.Children.Count > 0 ? pins : P("Noch nichts angeheftet. In der Liste bei einem Studio auf „Anheften“ klicken.", Muted, 12));
        }
        if (showRecent)
        {
            _smBody.Children.Add(H("Zuletzt verwendet", 15));
            var rec = _settings.RecentPages.Where(_showMap.ContainsKey).ToList();
            if (rec.Count == 0) _smBody.Children.Add(P("Hier erscheint, was du zuletzt geöffnet hast.", Muted, 12));
            foreach (var k in rec) _smBody.Children.Add(SmItem("sm-recent-" + k, PageTitle(k), null, () => { Show(k); return Task.CompletedTask; }, ModuleOf(k)));
        }
        if (_smFilter == "alle") _smBody.Children.Add(StartMenuApps());
        if (!showAz) return;
        IEnumerable<(string Id, string Name)> az = studios;
        if (_smFilter.StartsWith("area:", StringComparison.Ordinal))
        {
            var area = _smFilter["area:".Length..];
            az = studios.Where(s => string.Equals(StudioCatalog.For(s.Id)?.Area, area, StringComparison.Ordinal));
        }
        _smBody.Children.Add(H(_smFilter == "alle" ? "Alle Studios" : "Studios", 15));
        var list = az.ToList();
        if (list.Count == 0) _smBody.Children.Add(P("In diesem Bereich ist kein Studio.", Muted, 12));
        foreach (var (letter, items) in StartMenu.AtoZ(list, s => s.Name))
        {
            _smBody.Children.Add(new TextBlock { Name = "sm-letter-" + letter, Text = letter, Foreground = Accent, FontWeight = FontWeight.Bold, Margin = new Thickness(4, 6, 0, 0) });
            foreach (var s in items)
            {
                var pinned = StartMenu.IsPinned(_settings.PinnedStudios, s.Id);
                var pin = new Button { Name = "sm-pintoggle-" + s.Id, Content = pinned ? "Lösen" : "Anheften", FontSize = 10.5, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
                Avalonia.Automation.AutomationProperties.SetName(pin, s.Name + (pinned ? " lösen" : " anheften"));
                var sid = s.Id; var sname = s.Name; pin.Click += (_, _) => { if (StartMenu.IsPinned(_settings.PinnedStudios, sid)) UnpinStudio(sid); else PinStudio(sid); };
                var onDesk = OnDesktop(s.Id);
                var desk = new Button { Name = "sm-desk-" + s.Id, Content = onDesk ? "Vom Desktop" : "Auf den Desktop", FontSize = 10.5, Padding = new Thickness(6, 2), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                Avalonia.Automation.AutomationProperties.SetName(desk, s.Name + (onDesk ? " vom Desktop nehmen" : " auf den Desktop legen"));
                desk.Click += (_, _) => { SetOnDesktop(sid, !OnDesktop(sid)); FillStartMenu(); };
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
                g.Children.Add(SmItem("sm-az-" + s.Id, s.Name, UnavailableHint(s.Id), () => ActivateOrOpenStudio(sid, sname), s.Id)); g.Children.Add(Pos(pin, 1)); g.Children.Add(Pos(desk, 2));
                _smBody.Children.Add(g);
            }
        }
    }

    private Control PinTile(string id, string name)
    {
        var icon = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(14), Background = Brush.Parse(C("#33283C5C", "#E6FFFFFF")), HorizontalAlignment = HorizontalAlignment.Center, Child = IconFor(id, 28) ?? Ico(Mdi.FileDocumentOutline, 28, Accent) };
        var stack = new StackPanel { Width = 96, Spacing = 4, Children = { icon, new TextBlock { Text = name, Foreground = Text, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxHeight = 32 } } };
        if (UnavailableHint(id) is { } hint) stack.Children.Add(new TextBlock { Text = hint, Foreground = Muted, FontSize = 10, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        var b = new Button { Name = "sm-pin-" + id, Background = Brushes.Transparent, Padding = new Thickness(4), Margin = new Thickness(4), Content = stack };
        Avalonia.Automation.AutomationProperties.SetName(b, name);
        var sid = id; var sname = name;
        b.Click += async (_, _) => { CloseStartMenu(); await ActivateOrOpenStudio(sid, sname); };
        var un = new Button { Name = "sm-unpin-" + id, Content = "Lösen", FontSize = 10, Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(un, name + " lösen");
        un.Click += (_, _) => UnpinStudio(id);
        return new StackPanel { Children = { b, un } };
    }

    private string? UnavailableHint(string id) => HasStudioWorkspace(id) || StudioStatusLabel(id) != "Noch nicht verfügbar" ? null : "Noch nicht verfügbar";

    private static string AreaIcon(string area) => area switch
    {
        "Arbeiten & Büro" => Mdi.FileDocumentOutline,
        "Business & Geld" => Mdi.Cart,
        "Kreativ & Medien" => Mdi.Palette,
        "3D, Spiele & VR" => Mdi.CubeOutline,
        "Technik & Fachgebiete" => Mdi.Tools,
        "Wissen & Lernen" => Mdi.Translate,
        "KI & Automatik" => Mdi.Robot,
        "Entwickeln" => Mdi.CodeTags,
        "System & Sicherheit" => Mdi.ShieldCheck,
        _ => Mdi.ViewGridOutline,
    };
}

internal static class VisualExt
{
    public static Visual? GetVisualParentSafe(this Visual v) => Avalonia.VisualTree.VisualExtensions.GetVisualParent(v);
}
