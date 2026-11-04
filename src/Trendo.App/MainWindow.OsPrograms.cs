using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Programme in den Studios im Aufbau des gewählten Systems (Wunsch 04.11.2026) – gleiche Befehle, andere Anordnung:
/// Windows 11: Menüzeile im Fenster (Datei · Bearbeiten · Gruppen · Hilfe) mit Tastenkürzeln, darunter flaches Menüband mit Trennstrichen.
/// macOS: keine Menüs im Fenster – die Menüs des aktiven Programms stehen oben in der Menüleiste (Programmname · Ablage · Bearbeiten · Gruppen · Darstellung · Fenster · Hilfe), im Fenster eine schlanke Werkzeugleiste.
/// GNOME: Kopfleiste mit „Neu“ links, „Speichern“ (blau, empfohlene Aktion) und Hauptmenü ☰ rechts, darunter die Werkzeuge; Reiter erst ab zwei Dokumenten.</summary>
public sealed partial class MainWindow
{
    sealed record OsMenuItem(string Name, string Label, string Shortcut, Action Run);
    readonly Dictionary<string, (string Title, List<(string Group, List<OsMenuItem> Items)> Menus)> _osAppMenus = [];
    StackPanel? _macMenuHost;
    /// <summary>Menüeintrag eines Programms auslösen (wie ein Klick im Menü; für Tests und die KI).</summary>
    public bool OsRunMenu(string key, string name)
    {
        if (!_osAppMenus.TryGetValue(key, out var m) || m.Menus.SelectMany(x => x.Items).FirstOrDefault(i => i.Name == name) is not { } it) return false;
        it.Run(); return true;
    }
    public IReadOnlyList<string> OsProgramMenus(string key) => _osAppMenus.TryGetValue(key, out var m) ? [.. m.Menus.Select(x => x.Group)] : [];

    static string ButtonLabel(Control c) => c is Button { Content: string s } ? s.Trim() : c is Button { Content: TextBlock t } ? (t.Text ?? "").Trim() : c.Name ?? "";
    static string CleanLabel(string s) => s.TrimStart('↶', '↷', '?', '＋', '+', ' ', '▲', '▼', '✎', '✂').Trim();

    /// <summary>Tastenkürzel wie im System anzeigen (Windows „Strg+S“, Mac „⌘S“, GNOME „Strg+S“).</summary>
    static string OsShortcut(string name) => (name, _style) switch
    {
        ("prog-new", "mac") => "⌘N", ("prog-new", _) => "Strg+N",
        ("prog-save", "mac") => "⌘S", ("prog-save", _) => "Strg+S",
        ("prog-saveas" or "prog-saveas-open", "mac") => "⇧⌘S", ("prog-saveas" or "prog-saveas-open", _) => "Strg+Umschalt+S",
        ("prog-close", "mac") => "⌘W", ("prog-close", _) => "Strg+W",
        ("prog-undo", "mac") => "⌘Z", ("prog-undo", _) => "Strg+Z",
        ("prog-redo", "mac") => "⇧⌘Z", ("prog-redo", "linux") => "Strg+Umschalt+Z", ("prog-redo", _) => "Strg+Y",
        ("prog-help", "mac") => "⌘?", ("prog-help", _) => "F1",
        _ => "",
    };

    static string OsGroupName(string g) => (_style, g) switch { ("mac", "Datei") => "Ablage", _ => g };

    /// <summary>Menü-Modell aus den Knöpfen des Menübands: jeder Eintrag drückt genau den Knopf (gleiche Funktion, gleiche Prüfung).</summary>
    static List<(string Group, List<OsMenuItem> Items)> OsMenuModel(List<(string Group, List<Control> Items)> groups)
    {
        var l = new List<(string, List<OsMenuItem>)>();
        foreach (var (g, items) in groups)
        {
            var mi = new List<OsMenuItem>();
            foreach (var c in items.OfType<Button>())
            {
                var b = c;
                Action run = b.Flyout is { } f ? () => f.ShowAt(b) : () => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                mi.Add(new OsMenuItem(b.Name ?? "", CleanLabel(ButtonLabel(b)) + (b.Flyout is not null && !ButtonLabel(b).EndsWith('…') ? " …" : ""), OsShortcut(b.Name ?? ""), run));
            }
            if (mi.Count > 0) l.Add((OsGroupName(g), mi));
        }
        // Mac: Exportieren gehört ins Menü „Ablage“
        if (_style == "mac" && l.FindIndex(x => x.Item1 == "Exportieren") is var ex and >= 0 && l.FindIndex(x => x.Item1 == "Ablage") is var ab and >= 0)
        { l[ab].Item2.AddRange(l[ex].Item2.Select(i => i with { Label = "Exportieren: " + i.Label })); l.RemoveAt(ex); }
        return l;
    }

    static Control MenuHeader(string label, string shortcut) => shortcut.Length == 0 ? new TextBlock { Text = label } :
        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinWidth = 210, Children = { new TextBlock { Text = label }, Pos(new TextBlock { Text = shortcut, Opacity = 0.6, Margin = new Thickness(24, 0, 0, 0) }, 1) } };

    static MenuItem ToMenuItem(OsMenuItem i) { var m = new MenuItem { Name = "menu-" + i.Name, Header = MenuHeader(i.Label, i.Shortcut) }; m.Click += (_, _) => i.Run(); return m; }

    /// <summary>Knopf im flachen Stil des Systems (kein Kartenhintergrund; GNOME: empfohlene Aktion blau).</summary>
    static void Flatten(Control c, bool suggested = false)
    {
        if (c is not Button b) return;
        b.CornerRadius = new CornerRadius(_style == "linux" ? 6 : _style == "mac" ? 6 : 4);
        if (suggested) { b.Background = Accent; b.Foreground = Brushes.White; b.BorderThickness = new Thickness(0); return; }
        b.Background = Brushes.Transparent; b.BorderThickness = new Thickness(0); b.Foreground = Text;
    }

    /// <summary>Kopf eines Programms im Stil des Systems; liefert, was über die Arbeitsfläche kommt.</summary>
    Control OsProgramChrome(ProgramSpec spec, List<(string Group, List<Control> Items)> groups, Control tabs, int docCount)
    {
        var model = OsMenuModel(groups);
        _osAppMenus[spec.Key] = (spec.Title, model);
        var light = _theme == "hell";
        Control Group(string g, List<Control> items, bool label)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            foreach (var it in items) { it.Margin = new Thickness(0); it.VerticalAlignment = VerticalAlignment.Center; Flatten(it, _style == "linux" && it.Name == "prog-save"); row.Children.Add(it); }
            return label ? new StackPanel { Spacing = 1, Children = { row, new TextBlock { Text = g, FontSize = 10.5, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center } } } : row;
        }
        Control Sep(double h) => new Border { Width = 1, Height = h, Background = Edge, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center };
        tabs.IsVisible = _style == "windows" || docCount > 1; // Mac und GNOME: Reiter erst ab zwei Dokumenten
        switch (_style)
        {
            case "windows":
            {
                var menu = new Menu { Name = "prog-menubar", Margin = new Thickness(0, 0, 0, 2) };
                foreach (var (g, items) in model) { var top = new MenuItem { Name = "menubar-" + g, Header = g }; foreach (var i in items) top.Items.Add(ToMenuItem(i)); menu.Items.Add(top); }
                var band = new StackPanel { Name = "prog-ribbon", Orientation = Orientation.Horizontal };
                foreach (var (g, items) in groups.Where(x => x.Group is not ("Datei" or "Hilfe")))
                { if (band.Children.Count > 0) band.Children.Add(Sep(40)); band.Children.Add(Group(g, items, true)); }
                var ribbon = new Border { Background = Brush.Parse(light ? "#FFF9F9F9" : "#FF272727"), BorderBrush = Edge, BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(8, 4),
                    Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = band } };
                // Datei-Befehle bleiben als Knöpfe erreichbar (Testbarkeit, schnelle Maus) – unsichtbar klein links im Band
                var file = groups.FirstOrDefault(x => x.Group == "Datei").Items ?? []; var help = groups.FirstOrDefault(x => x.Group == "Hilfe").Items ?? [];
                var quick = new StackPanel { Name = "prog-quickaccess", Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                foreach (var it in file.Concat(help)) { it.Margin = new Thickness(0); Flatten(it); quick.Children.Add(it); }
                band.Children.Insert(0, quick); if (band.Children.Count > 1) band.Children.Insert(1, Sep(40));
                return new StackPanel { Children = { menu, tabs, ribbon } };
            }
            case "mac":
            {
                var bar = new StackPanel { Name = "prog-ribbon", Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var (g, items) in groups) { if (bar.Children.Count > 0) bar.Children.Add(new Border { Width = 10 }); bar.Children.Add(Group(g, items, false)); }
                _ui.Post(RefreshMacMenus);
                return new StackPanel { Children = { new Border { Name = "prog-toolbar", Padding = new Thickness(8, 6), Background = Brush.Parse(light ? "#FFECECEC" : "#FF2A2A2C"), BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = bar } }, tabs } };
            }
            default: // GNOME
            {
                var file = groups.FirstOrDefault(x => x.Group == "Datei").Items ?? [];
                Control? Take(string name) { var c = file.FirstOrDefault(x => x.Name == name); if (c is not null) { file.Remove(c); c.Margin = new Thickness(0); } return c; }
                var neu = Take("prog-new"); var save = Take("prog-save");
                if (neu is Button nb) { Flatten(nb); nb.Content = "＋ Neu"; }
                if (save is Button sb) Flatten(sb, true);
                var main = new Button { Name = "prog-mainmenu", Content = "☰", Width = 34, Height = 34, Padding = new Thickness(0), CornerRadius = new CornerRadius(6), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                Flatten(main); ToolTip.SetTip(main, "Hauptmenü");
                var fl = new MenuFlyout();
                foreach (var (g, items) in model) { var sub = new MenuItem { Header = g }; foreach (var i in items) sub.Items.Add(ToMenuItem(i)); fl.Items.Add(sub); }
                main.Flyout = fl;
                var tools = new StackPanel { Name = "prog-ribbon", Orientation = Orientation.Horizontal, Spacing = 4 };
                foreach (var (g, items) in groups.Where(x => x.Group != "Datei")) { if (tools.Children.Count > 0) tools.Children.Add(Sep(24)); tools.Children.Add(Group(g, items, false)); }
                var restFile = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 }; foreach (var f in file) { Flatten(f); restFile.Children.Add(f); }
                var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { neu ?? new Border(), restFile } },
                    Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { save ?? new Border(), main } }, 2) } };
                return new StackPanel { Children = {
                    new Border { Name = "prog-headerbar", Height = 46, Padding = new Thickness(8, 0), Background = Brush.Parse(light ? "#FFEBEBED" : "#FF2E2E32"), Child = head },
                    tabs,
                    new Border { Padding = new Thickness(8, 4), BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 0, 1), Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = tools } } } };
            }
        }
    }

    /// <summary>Mac: Menüleiste zeigt die Menüs des aktiven Programms (sonst die Trendo-Menüs).</summary>
    void RefreshMacMenus()
    {
        if (_style != "mac" || _macMenuHost is null) return;
        var key = Windows.Active is { Minimized: false } aw ? aw.Page : null;
        var prog = key is not null && _osAppMenus.TryGetValue(key, out var m) && ProgramActive(key) is not null ? m : default;
        _macMenuHost.Children.Clear();
        if (prog.Menus is null) { foreach (var c in _macDefaultMenus) _macMenuHost.Children.Add(c); return; }
        if (_osAppTitle is not null) _osAppTitle.Text = prog.Title;
        foreach (var (g, items) in prog.Menus.Where(x => x.Group != "Hilfe")) _macMenuHost.Children.Add(MacMenu("prog-" + g, g, [.. items.Select(ToMenuItem)]));
        foreach (var c in _macDefaultMenus.Where(c => c.Name is "os-menu-darstellung" or "os-menu-fenster")) _macMenuHost.Children.Add(c);
        var help = prog.Menus.FirstOrDefault(x => x.Group == "Hilfe").Items ?? [];
        _macMenuHost.Children.Add(MacMenu("prog-Hilfe", "Hilfe", [.. help.Select(ToMenuItem), MenuAction("Trendo KI fragen", () => Show("ki"))]));
    }
    readonly List<Control> _macDefaultMenus = [];
    static MenuItem MenuAction(string header, Action a) { var m = new MenuItem { Header = header }; m.Click += (_, _) => a(); return m; }
    Button MacMenu(string name, string title, MenuItem[] items)
    {
        var fg = Brush.Parse(_theme == "hell" ? "#E6000000" : "#FFFFFFFF");
        var b = OsFlat("os-menu-" + name, new TextBlock { Text = title, FontSize = 13, Foreground = fg, VerticalAlignment = VerticalAlignment.Center }, title, () => { }, double.NaN, 22, 5); b.Padding = new Thickness(9, 0);
        var f = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft }; foreach (var i in items) f.Items.Add(i); b.Flyout = f; return b;
    }
}
