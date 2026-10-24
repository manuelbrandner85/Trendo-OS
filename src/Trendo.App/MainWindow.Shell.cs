using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0218: Startseite nach Referenzbild 1 – Kopfleiste, linke Bereiche mit Schnellzugriff, Studio-Kacheln und Logo in der Mitte,
/// rechts KI-Assistent, Systemstatus, Projekte und Schnell-Tools. Alles zeigt echte Zustände; jede Taste führt auf eine vorhandene Funktion.</summary>
public partial class MainWindow
{
    /// <summary>Designfläche der Referenz. Die Oberfläche wird darauf proportional skaliert.</summary>
    public const double DesignW = 1536, DesignH = 800;

    // ---------- Kopfleiste ----------
    private Control TopBar()
    {
        var bar = new Grid { Name = "top-bar", ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Height = 54,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse(C("#F2081226", "#F2F2F6FC")), 0), new GradientStop(Color.Parse(C("#E00A1730", "#E0E6EDF8")), 1) } } };
        var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#002F7BFF"), 0), new GradientStop(Color.Parse("#AA3FA0FF"), 0.5), new GradientStop(Color.Parse("#002F7BFF"), 1) } } };
        Grid.SetColumnSpan(line, 4); bar.Children.Add(line);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(16, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
            Children = { Logo(34, glow: true), new TextBlock { Text = "Trendo OS", Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "Ultimate", Foreground = Brush.Parse(C("#C9D6EA", "#3A4A66")), FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-2, 3, 0, 0) } } };
        bar.Children.Add(brand);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(26, 0, 0, 0) };
        foreach (var (key, name, icon, col, label) in new[] {
                     ("start", "nav-start", Mdi.Monitor, "#4FB3FF", "Desktop"), ("studios", "nav-studios", Mdi.ViewGridOutline, "#5AA9FF", "Studios"), ("apps", "nav-apps", Mdi.Apps, "#4FC3F7", "Apps"),
                     ("projects", "nav-projects", Mdi.FolderPlus, "#B388FF", "Projekte"), ("ki", "nav-ki", Mdi.Brain, "#FF6FB5", "KI"), ("system", "nav-system", Mdi.CogOutline, "#64B5F6", "System"), ("settings", "nav-settings", Mdi.Cog, "#64B5F6", "Einstellungen") })
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ico(icon, 16, Brush.Parse(col)), new TextBlock { Text = label, Foreground = Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center } } };
            var b = Flat(name, content, label); b.Padding = new Thickness(11, 6);
            _tabs[key] = b;
            var target = key;
            b.Click += (_, _) => { if (target == "studios") { _studioFilter = null; _studioQuery = ""; } Show(target); };
            tabs.Children.Add(b);
        }
        Grid.SetColumn(tabs, 1); bar.Children.Add(tabs);
        Detach(_search);
        _search.Background = Brushes.Transparent; _search.BorderThickness = new Thickness(0); _search.Width = double.NaN; _search.Height = 32; _search.FontSize = 13;
        _search.VerticalContentAlignment = VerticalAlignment.Center; _search.PlaceholderText = "Suchen in Trendo OS …";
        var search = new Border { Width = 220, Height = 34, CornerRadius = new CornerRadius(9), Background = Brush.Parse(C("#990A1630", "#CCFFFFFF")), BorderBrush = Brush.Parse("#5541618F"), BorderThickness = new Thickness(1),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { new Border { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ico(Mdi.Magnify, 16, Text) }, Pos(_search, 1) } } };
        Border Circle(Control c) => new() { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Brush.Parse(C("#990A1630", "#CCFFFFFF")), BorderBrush = Brush.Parse("#5541618F"), BorderThickness = new Thickness(1), Child = c };
        var bell = Flat("nav-approvals", new Grid { Children = { Circle(Ico(Mdi.Bell, 17, Text)),
            new Border { Background = Brush.Parse("#FF3B4A"), CornerRadius = new CornerRadius(6), MinWidth = 10, Height = 12, Padding = new Thickness(2, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 2, 0),
                Child = new Viewbox { Child = _bellBadge } } } }, "Freigaben");
        bell.Padding = new Thickness(0);
        ToolTip.SetTip(bell, "Freigaben (Strg+4)");
        bell.Click += (_, _) => Show("approvals");
        var cloud = Flat("btn-cloud", Circle(Ico(Mdi.CloudOutline, 18, Text)), "Online-Dienste"); cloud.Padding = new Thickness(0);
        ToolTip.SetTip(cloud, "Online-Dienste und Schlüssel"); cloud.Click += (_, _) => Show("keys");
        var user = Flat("btn-user", Circle(Ico(Mdi.AccountCircleOutline, 20, Brush.Parse("#D4E157"))), "Benutzer"); user.Padding = new Thickness(0);
        ToolTip.SetTip(user, "Lokaler Besitzer – Einstellungen"); user.Click += (_, _) => Show("settings");
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 30, 0), Children = { search, bell, cloud, user } };
        Grid.SetColumn(tools, 2); bar.Children.Add(tools);
        _clock.FontSize = 20; _clock.FontWeight = FontWeight.SemiBold; _clock.HorizontalAlignment = HorizontalAlignment.Right; _clock.Foreground = Text;
        _date.FontSize = 12.5; _date.Foreground = Brush.Parse(C("#C9D6EA", "#3A4A66")); _date.HorizontalAlignment = HorizontalAlignment.Right;
        var clock = new StackPanel { Name = "desk-clock", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0), Children = { _clock, _date } };
        Grid.SetColumn(clock, 3); bar.Children.Add(clock);
        return bar;
    }

    // ---------- Linke Spalte: Bereiche und Schnellzugriff ----------
    private Button? _sideStart;

    private Control LeftColumn()
    {
        var nav = new StackPanel { Spacing = 2 };
        _sideStart = SideItem("side-start", Chip(Mdi.Home, "#64B5F6", "#1E88E5", 26), "Start", () => Show("start"));
        nav.Children.Add(_sideStart);
        foreach (var (label, icon, c1, c2, filter) in Categories)
            nav.Children.Add(SideItem("side-" + label.ToLowerInvariant().Replace(' ', '-').Replace("&", "und"), Chip(icon, c1, c2), StudioCatalog.SideLabels.GetValueOrDefault(label, label), () =>
            {
                if (filter == "STUDIO-LETTER") { Show("brief"); return; }
                if (filter == "SYS-10") { Show("office"); return; }
                _studioFilter = filter.Length == 0 ? null : filter; _studioQuery = ""; Show("studios");
            }));
        nav.Children.Add(SideItem("side-dateien", Chip(Mdi.Folder, "#42A5F5", "#1565C0"), "Dateien", () => Show("alltag-dateien")));
        nav.Children.Add(SideItem("side-einstellungen", Chip(Mdi.Cog, "#78909C", "#37474F"), "Einstellungen", () => Show("settings")));
        var quick = new StackPanel { Spacing = 1 };
        var qh = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(6, 2, 6, 6), Children = { H("Schnellzugriff", 13), Pos(new TextBlock { Text = "→", Foreground = Muted, FontSize = 13 }, 1) } };
        quick.Children.Add(qh);
        quick.Children.Add(SideItem("quick-new-task", Chip(Mdi.CheckboxMarkedOutline, "#29B6F6", "#0277BD", 22), "Neue Aufgabe", () => Show("ablaeufe"), 12.5));
        quick.Children.Add(SideItem("quick-new-project", Chip(Mdi.FolderPlus, "#42A5F5", "#1565C0", 22), "Neues Projekt", () => Show("projects"), 12.5));
        quick.Children.Add(SideItem("quick-new-letter", Chip(Mdi.EmailPlus, "#FB8C00", "#E65100", 22), "Neuer Brief", () => Show("brief"), 12.5));
        quick.Children.Add(SideItem("quick-analyse", Chip(Mdi.ChartLine, "#AB47BC", "#6A1B9A", 22), "Analyse starten", () => Show("system"), 12.5));
        quick.Children.Add(SideItem("quick-system-check", Chip(Mdi.ShieldSearch, "#66BB6A", "#2E7D32", 22), "System-Check", () => Show("systemcheck"), 12.5));
        var navPanel = Panel(nav, 8); var quickPanel = Panel(quick, 8);
        var pins = PinsCard(); pins.Margin = new Thickness(0, 0, 0, 10);
        var g = new Grid { Name = "left-column", RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
        g.Children.Add(navPanel);
        Grid.SetRow(pins, 2); g.Children.Add(pins);
        Grid.SetRow(quickPanel, 3); g.Children.Add(quickPanel);
        _leftCol = g;
        return g;
    }

    private static Button SideItem(string name, Control icon, string label, Action onClick, double font = 12.5)
    {
        var b = Flat(name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { icon, new TextBlock { Text = label, Foreground = Text, FontSize = font, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 128 } } }, label);
        ToolTip.SetTip(b, label);
        b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(8, 5); b.MinHeight = 0;
        b.Click += (_, _) => onClick();
        return b;
    }

    private Control PinsCard()
    {
        var head = new TextBlock { Text = "Angeheftet", Foreground = Muted, FontSize = 12, Margin = new Thickness(2, 0, 0, 2) };
        var card = Panel(new StackPanel { Spacing = 4, Children = { head, new ScrollViewer { Content = _pinsPanel, MaxHeight = 168 } } }, 8);
        card.Name = "widget-pins";
        card.IsVisible = false;
        _pinCard = card;
        return card;
    }

    // ---------- Mitte: Studio-Kacheln, Logo, Aktionen, Lichtring ----------
    private Control DesktopView()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("496,*") };
        var tiles = new UniformGrid { Columns = 3 };
        foreach (var (id, title0, sub, icon, c1, c2, glow) in UserSettings.Ordered(FeaturedStudios, f => f.Item1, _settings.TileOrder)
                     .Where(f => !_settings.HiddenTiles.Contains("tile-" + f.Item1) && (!_settings.SimpleMode || Trendo.Core.Sys.A11y.SimpleTiles.Contains(f.Item1))))
        {
            var st = _studios.FirstOrDefault(s => s.Id == id);
            var title = StudioCatalog.For(id)?.Name ?? title0;
            var badge = Badge(st);
            var t = new Button
            {
                Name = "tile-" + id, Height = 128, Margin = new Thickness(5), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Grad(C("#B3122648", "#E6FFFFFF"), C("#B30B1A36", "#D9EEF3FA")), BorderBrush = Brush.Parse(C("#4D3F7FD0", "#663F7FD0")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Content = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new Border { Height = 56, Child = StudioIcons.For(id) is { } si ? Ico(si.Path, 44, Grad(si.C1, si.C2), si.C2) : Ico(icon, 46, Grad(c1, c2), glow) },
                    new TextBlock { Text = title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 146 },
                    new TextBlock { Text = sub, Foreground = Brush.Parse(C("#C9D6EA", "#3A4A66")), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center }, badge } }
            };
            Avalonia.Automation.AutomationProperties.SetName(t, badge.Text is { Length: > 0 } ? $"{title}: {badge.Text}" : title);
            ToolTip.SetTip(t, st is null ? title : $"{badge.Text} – {st.Reason}");
            var sid = id;
            t.Click += async (_, _) => await OpenStudio(sid, title);
            Hover(t); tiles.Children.Add(t);
        }
        var all = SmallOutline("btn-all-studios", "Alle anzeigen"); all.HorizontalAlignment = HorizontalAlignment.Right; all.VerticalAlignment = VerticalAlignment.Center;
        all.Click += (_, _) => { _studioFilter = null; _studioQuery = ""; Show("studios"); };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(6, 2, 6, 10),
            Children = { new TextBlock { Text = "Trendo Studios", Foreground = Brush.Parse(C("#E8F3FF", "#0F1B2E")), FontSize = 19, FontWeight = FontWeight.SemiBold }, Pos(all, 1) } };
        var studiosPanel = Panel(new StackPanel { Children = { head, tiles } }, 14);
        studiosPanel.Name = "widget-studios"; studiosPanel.IsVisible = !_settings.HiddenTiles.Contains("widget-studios");
        studiosPanel.VerticalAlignment = VerticalAlignment.Top; studiosPanel.Margin = new Thickness(0, 6, 0, 0);
        g.Children.Add(studiosPanel);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0) };
        foreach (var (icon, label, go) in new (string, string, Action)[] {
                     (Mdi.Pencil, "Erstellen", () => Show("projects")),
                     (Mdi.CodeTags, "Entwickeln", () => { _studioFilter = "CORE-07|CORE-06|X-06"; _studioQuery = ""; Show("studios"); }),
                     (Mdi.Magnify, "Gestalten", () => { _studioFilter = "CORE-08|CORE-05|SPEC-08"; _studioQuery = ""; Show("studios"); }),
                     (Mdi.CalendarMonth, "Organisieren", () => Show("alltag-zeit")),
                     (Mdi.Tools, "Automatisieren", () => Show("ablaeufe")) })
        {
            var b = new Button { Name = "action-" + label.ToLowerInvariant(), Width = 88, Height = 66, Padding = new Thickness(2, 6), CornerRadius = new CornerRadius(10),
                Background = Brush.Parse(C("#99101F3D", "#CCFFFFFF")), BorderBrush = Brush.Parse("#664C8DFF"), BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Center,
                Content = new StackPanel { Spacing = 6, Children = { Ico(icon, 22, Text), new TextBlock { Text = label, Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
            Avalonia.Automation.AutomationProperties.SetName(b, label);
            b.Click += (_, _) => go();
            Hover(b); actions.Children.Add(b);
        }
        var word = new TextBlock { Text = "Trendo OS", FontSize = 66, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -2, 0, 0),
            Foreground = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#FFFFFF"), 0), new GradientStop(Color.Parse("#CFE6FF"), 0.6), new GradientStop(Color.Parse("#8EC5FF"), 1) } },
            Effect = new DropShadowEffect { Color = Color.Parse("#2E8BFF"), BlurRadius = 22, OffsetX = 0, OffsetY = 0, Opacity = 0.85 } };
        var hero = new StackPanel
        {
            Name = "desk-hero", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 150, 0, 0),
            Children = {
                new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = Logo(124, glow: true) },
                word,
                new TextBlock { Text = "DEIN SYSTEM. DEINE IDEEN. KEINE GRENZEN.", Foreground = Brush.Parse("#EAF2FF"), FontSize = 13, LetterSpacing = 1.6, HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = new DropShadowEffect { Color = Color.Parse("#040A18"), BlurRadius = 8, OffsetX = 0, OffsetY = 1, Opacity = 0.9 } }, // liegt immer über dem Foto
                actions,
                GlowRing(),
            }
        };
        Grid.SetColumn(hero, 1); g.Children.Add(hero);
        DesktopMenu(g);
        return g;
    }

    /// <summary>Leuchtender Ring auf dem See (rein dekorativ, bei „Bewegung reduzieren“ ebenso still wie sonst).</summary>
    private static Control GlowRing()
    {
        var g = new Grid { Width = 560, Height = 110, Margin = new Thickness(0, 6, 0, 0), IsHitTestVisible = false };
        g.Children.Add(new Ellipse { Width = 560, Height = 110, Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#AA35C8FF"), 0), new GradientStop(Color.Parse("#4424A0FF"), 0.55), new GradientStop(Color.Parse("#0024A0FF"), 1) } } });
        foreach (var (w, h, o, t) in new[] { (540.0, 100.0, 0.45, 1.5), (440.0, 80.0, 0.7, 2.0), (330.0, 60.0, 0.9, 2.5), (220.0, 40.0, 1.0, 2.0) })
            g.Children.Add(new Ellipse { Width = w, Height = h, Stroke = Brush.Parse("#7FE0FF"), StrokeThickness = t, Opacity = o,
                Effect = new DropShadowEffect { Color = Color.Parse("#39B6FF"), BlurRadius = 12, OffsetX = 0, OffsetY = 0, Opacity = 1 } });
        g.Children.Add(new Ellipse { Width = 170, Height = 26, Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#EEDFF8FF"), 0), new GradientStop(Color.Parse("#0039B6FF"), 1) } } });
        return g;
    }

    // ---------- Rechte Spalte ----------
    private readonly Dictionary<string, (Arc Arc, TextBlock Value)> _rings = [];
    private readonly TextBlock _toolsMsg = new() { Name = "tools-msg", FontSize = 11, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(4, 4, 4, 0) };

    private Control? _rightCol, _leftCol;

    private Control RightColumn()
    {
        var sp = new Grid { Name = "right-column", RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        string[] keys = ["widget-ki", "widget-system", "widget-projekte", "widget-tools"];
        void Add(Border c) { var k = keys[sp.Children.Count]; c.Name = k; c.IsVisible = !_settings.HiddenTiles.Contains(k); Grid.SetRow(c, sp.Children.Count); sp.Children.Add(c); }
        // KI-Assistent: echter Zustand
        var avatar = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = Grad("#2F86F6", "#0B2A6B"), BorderBrush = Brush.Parse("#887FD3FF"), BorderThickness = new Thickness(1), Child = Ico(Mdi.RobotHappy, 20, Brushes.White) };
        var kiHead = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { avatar, Pos(new TextBlock { Text = "Trendo KI Assistent", Foreground = Text, FontSize = 14.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis }, 1) } };
        _kiDot.Width = 9; _kiDot.Height = 9; _kiStatus.FontSize = 12;
        Detach(_kiDot); Detach(_kiStatus); Detach(_kiText); Detach(_kiInput); Detach(_kiSetup);
        var kiState = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center, Children = { _kiDot, _kiStatus } };
        Grid.SetColumn(kiState, 2); kiHead.Children.Add(kiState);
        _kiInput.Background = Brushes.Transparent; _kiInput.BorderThickness = new Thickness(0); _kiInput.Margin = new Thickness(0); _kiInput.VerticalContentAlignment = VerticalAlignment.Center; _kiInput.FontSize = 12.5;
        var send = new Button { Name = "btn-ki-send", Width = 30, Height = 30, Padding = new Thickness(0), Background = Brush.Parse("#33FFFFFF"), CornerRadius = new CornerRadius(6), Content = Ico(Mdi.Send, 15, Text), VerticalAlignment = VerticalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(send, "Senden");
        send.Click += async (_, _) => { if (_kiInput.IsEnabled && !string.IsNullOrWhiteSpace(_kiInput.Text)) await AskKi(_kiInput.Text!); else Show("ki"); };
        _kiSetup.Margin = new Thickness(0, 0, 4, 0); _kiSetup.Padding = new Thickness(8, 2); _kiSetup.FontSize = 11; _kiSetup.VerticalAlignment = VerticalAlignment.Center; _kiSetup.Background = Accent;
        var inputBox = new Border { Margin = new Thickness(0, 10, 0, 0), Height = 40, CornerRadius = new CornerRadius(8), Background = Brush.Parse(C("#990A1630", "#CCFFFFFF")), BorderBrush = Brush.Parse("#5541618F"), BorderThickness = new Thickness(1), Padding = new Thickness(6, 0, 4, 0),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = { _kiInput, Pos(_kiSetup, 1), Pos(send, 2) } } };
        _kiText.FontSize = 13; _kiText.LineHeight = 19;
        Add(Panel(new StackPanel { Children = { kiHead,
            new Border { Background = Brush.Parse(C("#80142849", "#B3FFFFFF")), BorderBrush = Brush.Parse("#333F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10), Margin = new Thickness(0, 12, 0, 0), MaxHeight = 120,
                Child = new ScrollViewer { Content = _kiText } },
            inputBox } }, 14));
        // Systemstatus: echte Messwerte
        var rings = new UniformGrid { Columns = 4, Margin = new Thickness(0, 12, 0, 2) };
        foreach (var k in new[] { "CPU", "RAM", "SSD", "GPU" }) rings.Children.Add(Ring(k));
        Detach(_sysDot); Detach(_sysState); Detach(_netDown); Detach(_netUp);
        var stateRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center, Children = { _sysDot, _sysState } };
        _netDown.FontSize = 11; _netUp.FontSize = 11; _netDown.Foreground = _netUp.Foreground = Muted;
        var net = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0), Children = { _netDown, _netUp } };
        Add(Panel(new StackPanel { Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("System Status", 14.5), Pos(stateRow, 1) } }, rings, net } }, 14));
        // Aktuelle Projekte: echt
        var allP = SmallOutline("btn-all-projects", "Alle anzeigen"); allP.Click += (_, _) => Show("projects");
        Detach(_projectsPanel);
        Add(Panel(new StackPanel { Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Aktuelle Projekte", 14.5), Pos(allP, 1) } }, new Border { Height = 8 }, _projectsPanel } }, 12));
        // Schnell-Tools: jede Taste führt auf eine vorhandene Funktion
        var tools = new UniformGrid { Columns = 4, Margin = new Thickness(0, 10, 0, 0) };
        var shot = ToolButton("tool-screenshot", Chip(Mdi.Camera, "#1E2A44", "#0F1A30", 30, 0.6), "Screenshot");
        shot.Click += (_, _) =>
        {
            var dir = _opt.Isolated ? Path.Combine(_core.DataRoot, "Bilder") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Trendo");
            try
            {
                Directory.CreateDirectory(dir);
                var f = Path.Combine(dir, $"trendo-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                SaveScreenshot(f); _core.Audit.Append(Principal.LocalOwner.Name, "tool.screenshot", Path.GetFileName(f));
                ToolsSay("Bildschirmfoto gespeichert: " + Path.GetFileName(f), ok: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ToolsSay("Bildschirmfoto nicht gespeichert: " + ex.Message, ok: false); }
        };
        tools.Children.Add(shot);
        var rec = ToolButton("tool-aufnahme", Chip(Mdi.Record, "#FF5252", "#C62828", 30, 0.6), "Aufnahme"); rec.Click += (_, _) => Show("alltag-bilder"); tools.Children.Add(rec);
        var note = ToolButton("tool-notiz", Chip(Mdi.NoteEdit, "#ECEFF1", "#B0BEC5", 30, 0.6, Brush.Parse("#263238")), "Notiz"); note.Click += (_, _) => Show("werkzeuge"); tools.Children.Add(note);
        var tr = ToolButton("tool-uebersetzen", Chip(Mdi.Translate, "#42A5F5", "#1565C0", 30, 0.6), "Übersetzen"); tr.Click += (_, _) => Show("recherche"); tools.Children.Add(tr);
        Detach(_toolsMsg);
        Add(Panel(new StackPanel { Children = { H("Schnell-Tools", 14.5), tools, _toolsMsg } }, 12));
        _rightCol = sp;
        return sp;
    }

    private void ToolsSay(string msg, bool ok)
    {
        _toolsMsg.Text = msg; _toolsMsg.Foreground = Brush.Parse(ok ? "#4ADE80" : "#FACC15"); _toolsMsg.IsVisible = true;
    }

    private static Button ToolButton(string name, Control icon, string label)
    {
        var b = new Button { Name = name, Background = Brush.Parse(C("#80142849", "#B3FFFFFF")), BorderBrush = Brush.Parse("#333F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Margin = new Thickness(3), Padding = new Thickness(2, 8), Height = 66, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new StackPanel { Spacing = 5, Children = { icon, new TextBlock { Text = label, Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
        ToolTip.SetTip(b, label);
        Avalonia.Automation.AutomationProperties.SetName(b, label);
        return b;
    }

    private Control Ring(string key)
    {
        var bg = new Ellipse { Width = 60, Height = 60, Stroke = Brush.Parse(C("#2AFFFFFF", "#22000000")), StrokeThickness = 5 };
        var arc = new Arc { Width = 60, Height = 60, StrokeThickness = 5, StartAngle = -90, SweepAngle = 0, StrokeLineCap = PenLineCap.Round,
            Stroke = Grad("#34E3C3", "#18B892"), Effect = new DropShadowEffect { Color = Color.Parse("#22D3A6"), BlurRadius = 8, OffsetX = 0, OffsetY = 0, Opacity = 0.8 } };
        var val = new TextBlock { Name = "metric-" + key.ToLowerInvariant(), Text = "…", Foreground = Text, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center };
        _rings[key] = (arc, val);
        var ring = new Grid { Name = "ring-" + key.ToLowerInvariant(), Width = 64, Height = 64, Children = { new Ellipse { Width = 60, Height = 60, Fill = Brush.Parse(C("#66091428", "#E6FFFFFF")) }, bg, arc,
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = key, Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }, val } } } };
        return ring;
    }

    private void SetRing(string k, double? v, string? detail)
    {
        if (!_rings.TryGetValue(k, out var r)) return;
        var pct = v is { } x ? Math.Clamp(x, 0, 100) : 0;
        r.Arc.SweepAngle = 360 * pct / 100;
        r.Value.Text = v is { } y ? $"{y:0}%" : "n. v.";
        var tip = v is null ? $"{k}: Nicht verfügbar" : detail is null ? $"{k}: {v:0} %" : $"{k}: {v:0} % · {detail}";
        if (r.Arc.Parent is Grid g) { ToolTip.SetTip(g, tip); Avalonia.Automation.AutomationProperties.SetName(g, tip); }
    }

    private void RefreshProjects()
    {
        _projectsPanel.Children.Clear();
        var list = _core.Projects.List().OrderByDescending(p => p.CreatedAt).Take(4).ToList();
        Control Row(Control chip, string title, string sub, Control? right = null) => new Border { Background = Brush.Parse(C("#66142849", "#99FFFFFF")), BorderBrush = Brush.Parse("#263F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Margin = new Thickness(0, 0, 0, 6),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { chip,
                Pos(new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = sub, Foreground = Muted, FontSize = 11 } } }, 1),
                Pos(right ?? new Border(), 2) } } };
        if (list.Count == 0)
        {
            var b = new Button { Name = "btn-first-project", Content = "＋ Anlegen", FontSize = 11, Padding = new Thickness(8, 3), VerticalAlignment = VerticalAlignment.Center };
            b.Click += (_, _) => Show("projects");
            _projectsPanel.Children.Add(Row(Chip(Mdi.FolderPlus, "#42A5F5", "#1565C0", 30), "Noch keine Projekte", "Lege dein erstes Projekt an", b));
        }
        else
        {
            var colors = new[] { ("#1E88E5", "#0D47A1"), ("#FB8C00", "#E65100"), ("#F4511E", "#BF360C"), ("#ECEFF1", "#B0BEC5") };
            var i = 0;
            foreach (var p in list)
            {
                var (c1, c2) = colors[i++ % colors.Length];
                var open = new Button { Name = "project-open-" + p.Id, Padding = new Thickness(6, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = Ico(Mdi.DotsVertical, 14, Muted), VerticalAlignment = VerticalAlignment.Center };
                ToolTip.SetTip(open, "Projekte öffnen"); Avalonia.Automation.AutomationProperties.SetName(open, $"{p.Name} öffnen");
                open.Click += (_, _) => Show("projects");
                _projectsPanel.Children.Add(Row(Chip(Mdi.Folder, c1, c2, 30), p.Name, $"Angelegt {p.CreatedAt:dd.MM.yyyy HH:mm}", open));
            }
        }
        RefreshPins();
        // Schriftgröße und einfacher Modus gelten auch für die festen Spalten der Startseite
        if (_rightCol is not null) ApplyAccessibility(_rightCol);
        if (_leftCol is not null) ApplyAccessibility(_leftCol);
    }
}
