using Avalonia;
using Avalonia.Controls;
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
    private readonly TextBox _smSearch = new() { Name = "startmenu-search", PlaceholderText = "Studios, Seiten und Funktionen suchen" };
    private readonly StackPanel _smBody = new() { Spacing = 10 };
    public bool StartMenuOpen => _startMenu.IsVisible;

    private void InitStartMenu()
    {
        foreach (var k in new[] { "brief", "akten", "akte", "ausgabe", "briefkoepfe" }) _windowGroups[k] = "brief";
        if (_windowGroups.TryGetValue("doku-pdf", out var doku)) _windowGroups["office"] = doku;
        _startMenu.Width = 560; _startMenu.MaxHeight = 640; _startMenu.Background = GlassStrong; _startMenu.BorderBrush = Edge; _startMenu.BorderThickness = new Thickness(1);
        _startMenu.CornerRadius = new CornerRadius(14); _startMenu.Padding = new Thickness(16); _startMenu.BoxShadow = BoxShadows.Parse("0 8 32 0 #66000000");
        _startMenu.HorizontalAlignment = HorizontalAlignment.Left; _startMenu.VerticalAlignment = VerticalAlignment.Bottom; _startMenu.Margin = new Thickness(0, 0, 0, 92);
        _startMenu.ZIndex = 1000;
        _smSearch.Height = 34; _smSearch.FontSize = 13;
        _smSearch.TextChanged += (_, _) => FillStartMenu();
        _smSearch.KeyDown += async (_, e) => { if (e.Key == Key.Enter && StartHits(_smSearch.Text ?? "").FirstOrDefault() is { } h) { CloseStartMenu(); await OpenStartHit(h); } };
        _startMenu.Child = new DockPanel { Children = { Top(_smSearch), new ScrollViewer { Content = _smBody, Margin = new Thickness(0, 10, 0, 0) } } };
        if (_center.Parent is Grid body) { Grid.SetColumnSpan(_startMenu, 3); Grid.SetRowSpan(_startMenu, 2); body.Children.Add(_startMenu); }
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
    public void OpenStartMenu() { _smSearch.Text = ""; FillStartMenu(); _startMenu.IsVisible = true; _smSearch.Focus(); }
    public void CloseStartMenu() => _startMenu.IsVisible = false;

    /// <summary>Startknopf ganz links in der Taskleiste (Logo A „Obsidian-Kachel“).</summary>
    private Button StartButton()
    {
        var b = new Button { Name = "start-button", Width = 50, Height = 50, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = Logo(38) };
        ToolTip.SetTip(b, "Start"); Avalonia.Automation.AutomationProperties.SetName(b, "Start");
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

    public void PinStudio(string id) { _settings.PinnedStudios = StartMenu.Pin(_settings.PinnedStudios, id); SaveSettings(); FillStartMenu(); }
    public void UnpinStudio(string id) { _settings.PinnedStudios = StartMenu.Unpin(_settings.PinnedStudios, id); SaveSettings(); FillStartMenu(); }

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

    private void FillStartMenu()
    {
        _smBody.Children.Clear();
        var q = _smSearch.Text ?? "";
        if (q.Trim().Length > 0)
        {
            var hits = StartHits(q);
            _smBody.Children.Add(H("Suchergebnisse", 14));
            if (hits.Count == 0) _smBody.Children.Add(P("Nichts gefunden. Versuch ein anderes Wort.", Muted));
            foreach (var (h, i) in hits.Select((h, i) => (h, i))) _smBody.Children.Add(SmItem("sm-hit-" + i, h.Title, h.Kind, () => OpenStartHit(h), HitStudio(h)));
            return;
        }
        var byId = MenuStudios().ToDictionary(s => s.Id, s => s.Name, StringComparer.OrdinalIgnoreCase);
        var pins = new WrapPanel();
        foreach (var id in _settings.PinnedStudios.Where(byId.ContainsKey))
        {
            var name = byId[id];
            var open = SmItem("sm-pin-" + id, name, UnavailableHint(id), () => ActivateOrOpenStudio(id, name), id); open.Width = 200;
            var un = new Button { Name = "sm-unpin-" + id, Content = "Lösen", FontSize = 10.5, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
            Avalonia.Automation.AutomationProperties.SetName(un, name + " lösen"); un.Click += (_, _) => UnpinStudio(id);
            pins.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 4), Children = { open, un } });
        }
        _smBody.Children.Add(H("Angeheftet", 14));
        _smBody.Children.Add(pins.Children.Count > 0 ? pins : P("Noch nichts angeheftet. Unten in der Liste bei einem Studio auf „Anheften“ klicken.", Muted, 11.5));

        _smBody.Children.Add(H("Zuletzt benutzt", 14));
        var rec = _settings.RecentPages.Where(_showMap.ContainsKey).ToList();
        if (rec.Count == 0) _smBody.Children.Add(P("Hier erscheint, was du zuletzt geöffnet hast.", Muted, 11.5));
        foreach (var k in rec) _smBody.Children.Add(SmItem("sm-recent-" + k, PageTitle(k), null, () => { Show(k); return Task.CompletedTask; }, ModuleOf(k)));

        _smBody.Children.Add(H("Alle Studios", 14));
        foreach (var (letter, items) in StartMenu.AtoZ(MenuStudios(), s => s.Name))
        {
            _smBody.Children.Add(new TextBlock { Name = "sm-letter-" + letter, Text = letter, Foreground = Accent, FontWeight = FontWeight.Bold, Margin = new Thickness(4, 6, 0, 0) });
            foreach (var s in items)
            {
                var pinned = StartMenu.IsPinned(_settings.PinnedStudios, s.Id);
                var pin = new Button { Name = "sm-pintoggle-" + s.Id, Content = pinned ? "Lösen" : "Anheften", FontSize = 10.5, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
                Avalonia.Automation.AutomationProperties.SetName(pin, s.Name + (pinned ? " lösen" : " anheften"));
                var sid = s.Id; var sname = s.Name; pin.Click += (_, _) => { if (StartMenu.IsPinned(_settings.PinnedStudios, sid)) UnpinStudio(sid); else PinStudio(sid); };
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                g.Children.Add(SmItem("sm-az-" + s.Id, s.Name, UnavailableHint(s.Id), () => ActivateOrOpenStudio(sid, sname), s.Id)); g.Children.Add(Pos(pin, 1));
                _smBody.Children.Add(g);
            }
        }
        var foot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        foot.Children.Add(SmItem("sm-desktop", "Startbildschirm", null, () => { Show("start"); return Task.CompletedTask; }));
        foot.Children.Add(SmItem("sm-settings", "Einstellungen", null, () => { Show("settings"); return Task.CompletedTask; }));
        _smBody.Children.Add(foot);
    }

    private string? UnavailableHint(string id) => HasStudioWorkspace(id) || StudioStatusLabel(id) != "Noch nicht verfügbar" ? null : "Noch nicht verfügbar";
}

internal static class VisualExt
{
    public static Visual? GetVisualParentSafe(this Visual v) => Avalonia.VisualTree.VisualExtensions.GetVisualParent(v);
}
