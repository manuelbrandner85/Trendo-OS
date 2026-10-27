using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0224: Desktop nach dem Referenzbild vom 25.10.2026 (dunkles Navy-Glas). Oben die große KI-Eingabe mit Leuchtkugel,
/// links Bereiche, Erdkugel und Ökosystem-Werte, in der Mitte Studios als Bildkacheln, Systemmodule und Projekte, rechts KI-Router,
/// Geräte, Agenten, Studio Manager und Open Source. Alles zeigt echte Zustände; jede Taste führt auf eine vorhandene Funktion.</summary>
public partial class MainWindow
{
    /// <summary>Designfläche der Referenz (16:9). Die Oberfläche wird darauf proportional skaliert.</summary>
    public const double DesignW = 1536, DesignH = 864;

    private static readonly IBrush NavyGlass = new SolidColorBrush(Color.Parse("#C7081329"));
    private static readonly IBrush NavyEdge = new SolidColorBrush(Color.Parse("#4D3B7BD6"));

    /// <summary>Dunkle Glasfläche wie im Referenzbild (heller im hellen Design).</summary>
    private static Border Glass2(Control c, double pad = 12) => new()
    {
        Background = Brush.Parse(C("#C7081329", "#E6F4F7FC")), BorderBrush = Brush.Parse(C("#4D3B7BD6", "#663F7FD0")), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12), Padding = new Thickness(pad), Child = c, Margin = new Thickness(0, 0, 0, 7), BoxShadow = BoxShadows.Parse("0 6 22 0 #80000814"),
    };

    private static Control Head(string title, string? sub = null, Control? right = null, string? icon = null)
    {
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null) left.Children.Add(Ico(icon, 16, Brush.Parse("#8FC3FF")));
        left.Children.Add(new TextBlock { Text = title, Foreground = Text, FontSize = 14.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (sub is not null) left.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 1, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(2, 0, 2, 6), Children = { left } };
        if (right is not null) g.Children.Add(Pos(right, 1));
        return g;
    }

    private static TextBlock StateBadge(string name, string text, bool on) => new()
    {
        Name = name, Text = text, FontSize = 9.5, FontWeight = FontWeight.SemiBold, Padding = new Thickness(7, 2), VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brush.Parse(on ? "#4ADE80" : "#C9D6EA"), Background = Brush.Parse(on ? "#2622C55E" : "#22FFFFFF"),
    };

    private static Border Pill(TextBlock badge) => new() { CornerRadius = new CornerRadius(8), Background = badge.Background, Child = badge, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };

    /// <summary>Leuchtende Kugel der KI (rein dekorativ).</summary>
    private static Control Orb(double size) => new Border
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
        Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#FFFFFF"), 0), new GradientStop(Color.Parse("#8FD3FF"), 0.25), new GradientStop(Color.Parse("#2F7BFF"), 0.6), new GradientStop(Color.Parse("#0A1E55"), 1) } },
        BorderBrush = Brush.Parse("#997FD3FF"), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse($"0 0 {size * 0.55:0} 2 #993BA7FF"),
    };

    // ---------- Kopfleiste: Marke · KI-Eingabe · Not-Stopp, Suche, Freigaben, Uhr, Benutzer ----------
    private readonly TextBox _prompt = new() { Name = "desk-prompt-input", PlaceholderText = "Was möchtest du heute erstellen?" };

    private Control TopBar()
    {
        var bar = new Grid { Name = "top-bar", ColumnDefinitions = new ColumnDefinitions("212,*,Auto"), Height = 68, Background = Brushes.Transparent };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Children = { Logo(38, glow: true), new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = "Trendo OS", Foreground = _theme == "kontrast" ? Text : GoldText(), FontSize = 21, FontWeight = FontWeight.Bold, LetterSpacing = 1.2 },
                new TextBlock { Text = "DEIN SYSTEM. DEINE IDEEN.", Foreground = Brush.Parse(C("#9FB4D6", "#3A4A66")), FontSize = 7.5, LetterSpacing = 1.2 } } } } };
        bar.Children.Add(brand);

        // KI-Eingabe: Enter fragt die Trendo KI (Antwort auf der KI-Seite), ohne KI öffnet sich die Einrichtung
        Detach(_prompt);
        _prompt.Background = Brushes.Transparent; _prompt.BorderThickness = new Thickness(0); _prompt.FontSize = 17; _prompt.Foreground = Text;
        _prompt.VerticalContentAlignment = VerticalAlignment.Center; _prompt.Padding = new Thickness(4, 0);
        async Task Go()
        {
            var q = _prompt.Text?.Trim() ?? "";
            if (q.Length == 0) { _prompt.Focus(); return; }
            _prompt.Text = "";
            Show("ki");
            if (_kiInput.IsEnabled) await AskKi(q);
        }
        _prompt.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await Go(); } };
        var go = new Button { Name = "desk-prompt-go", Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Padding = new Thickness(0), Background = Brush.Parse("#332F86F6"),
            BorderBrush = Brush.Parse("#AA7FD3FF"), BorderThickness = new Thickness(1.5), Content = Ico(Mdi.ArrowRight, 18, Text), VerticalAlignment = VerticalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(go, "Trendo KI fragen"); ToolTip.SetTip(go, "Trendo KI fragen");
        go.Click += async (_, _) => await Go();
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Children = {
            Orb(40),
            Pos(new TextBlock { Text = "Trendo KI –", Foreground = Text, FontSize = 19, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 6, 0) }, 1),
            Pos(_prompt, 2), Pos(go, 3) } };
        var prompt = new Border { Name = "desk-prompt", Height = 54, MaxWidth = 820, Margin = new Thickness(8, 6, 12, 0), CornerRadius = new CornerRadius(27), Padding = new Thickness(8, 6, 10, 6),
            Background = Brush.Parse(C("#D9081329", "#F2FFFFFF")), BorderThickness = new Thickness(1.5), VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#AA5AA9FF"), 0), new GradientStop(Color.Parse("#FF9FD8FF"), 0.5), new GradientStop(Color.Parse("#AA5AA9FF"), 1) } },
            BoxShadow = BoxShadows.Parse("0 0 26 0 #553BA7FF"), Child = row };
        Grid.SetColumn(prompt, 1); bar.Children.Add(prompt);

        Detach(_search);
        _search.Background = Brushes.Transparent; _search.BorderThickness = new Thickness(0); _search.Width = double.NaN; _search.Height = 30; _search.FontSize = 12;
        _search.VerticalContentAlignment = VerticalAlignment.Center; _search.PlaceholderText = "Suchen …";
        var search = new Border { Width = 138, Height = 32, CornerRadius = new CornerRadius(16), Background = Brush.Parse(C("#99081329", "#CCFFFFFF")), BorderBrush = NavyEdge, BorderThickness = new Thickness(1),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { new Border { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ico(Mdi.Magnify, 15, Text) }, Pos(_search, 1) } } };
        Border Circle(Control c) => new() { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Brush.Parse(C("#99081329", "#CCFFFFFF")), BorderBrush = NavyEdge, BorderThickness = new Thickness(1), Child = c };
        var bell = Flat("nav-approvals", new Grid { Children = { Circle(Ico(Mdi.Bell, 16, Text)),
            new Border { Background = Brush.Parse("#FF3B4A"), CornerRadius = new CornerRadius(6), MinWidth = 10, Height = 12, Padding = new Thickness(2, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 1, 0),
                Child = new Viewbox { Child = _bellBadge } } } }, "Freigaben");
        bell.Padding = new Thickness(0);
        ToolTip.SetTip(bell, "Freigaben (Strg+4)");
        bell.Click += (_, _) => Show("approvals");
        var cloud = Flat("btn-cloud", Circle(Ico(Mdi.CloudOutline, 17, Text)), "Online-Dienste"); cloud.Padding = new Thickness(0);
        ToolTip.SetTip(cloud, "Online-Dienste und Schlüssel"); cloud.Click += (_, _) => Show("keys");
        _clock.FontSize = 18; _clock.FontWeight = FontWeight.SemiBold; _clock.HorizontalAlignment = HorizontalAlignment.Right; _clock.Foreground = Text;
        _date.FontSize = 11; _date.Foreground = Brush.Parse(C("#C9D6EA", "#3A4A66")); _date.HorizontalAlignment = HorizontalAlignment.Right;
        var clock = new StackPanel { Name = "desk-clock", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Children = { _date, _clock } };
        var user = Flat("btn-user", new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Background = Grad("#2F86F6", "#0B2A6B"), BorderBrush = Brush.Parse("#AA7FD3FF"), BorderThickness = new Thickness(1.5),
            Child = new TextBlock { Name = "user-initial", Text = Initial(_settings.Name), Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, "Benutzer");
        user.Padding = new Thickness(0);
        ToolTip.SetTip(user, "Lokaler Besitzer – Einstellungen"); user.Click += (_, _) => Show("settings");
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 14, 0), Children = { HaltButton(), search, bell, cloud, clock, user } };
        Grid.SetColumn(tools, 2); bar.Children.Add(tools);
        _shellParts.Add(bar);
        return bar;
    }

    private static string Initial(string? name) => string.IsNullOrWhiteSpace(name) ? "T" : name.Trim()[..1].ToUpperInvariant();

    // ---------- Linke Spalte: Bereiche, Erdkugel mit Ökosystem, Besitzer ----------
    private Button? _sideStart;
    /// <summary>Feste Teile des Desktops (Kopf, Spalten, Mitte): Barrierefreiheit und Sparmodus gelten auch hier, nicht nur auf der offenen Seite.</summary>
    private readonly List<Control> _shellParts = [];
    private Control? _deskCenter;
    private void ApplyShellAccessibility() { foreach (var c in _shellParts) ApplyAccessibility(c); }
    private readonly TextBlock _ecoDevices = new() { Name = "eco-devices" }, _ecoAgents = new() { Name = "eco-agents" }, _ecoProjects = new() { Name = "eco-projects" }, _ecoState = new() { Name = "eco-state" };

    private static readonly (string Key, string Label, string Icon)[] SideNav =
    [
        ("start", "Start", Mdi.Home), ("ki", "Command Center", Mdi.StarFourPoints), ("studios", "Studios", Mdi.ViewGridOutline), ("projects", "Projekte", Mdi.Folder),
        ("ki-rollen", "Agenten", Mdi.RobotHappy), ("ki-quellen", "Assets", Mdi.Image), ("dev", "GitHub & Code", Mdi.CodeTags), ("geraete", "Geräte", Mdi.Monitor),
        ("handy", "Mobile", Mdi.Cellphone), ("settings", "Einstellungen", Mdi.CogOutline),
    ];

    private Control LeftColumn()
    {
        var nav = new StackPanel { Spacing = 1 };
        foreach (var (key, label, icon) in SideNav)
        {
            var b = SideItem("side-" + key, Ico(icon, 17, Brush.Parse("#9FC8FF")), label, () => { if (key == "studios") { _studioFilter = null; _studioQuery = ""; } Show(key); }, 13);
            _tabs[key] = b;
            if (key == "start") _sideStart = b;
            nav.Children.Add(b);
        }
        var navPanel = Glass2(nav, 6);

        // Erdkugel mit echten Werten
        var earth = new Image { Source = DesktopPicture("erde"), Stretch = Stretch.UniformToFill, Width = 260, Height = 260, Margin = new Thickness(-70, 0, 0, 120), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
            OpacityMask = new RadialGradientBrush { GradientStops = { new GradientStop(Colors.Black, 0.6), new GradientStop(Colors.Transparent, 0.72) } } };
        Control Stat(Control chip, string label, TextBlock value) { value.Foreground = Text; value.FontSize = 12; value.FontWeight = FontWeight.SemiBold; return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, 7), Children = { chip,
            Pos(new StackPanel { Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 10.5 }, value } }, 1) } }; }
        var eco = new StackPanel { Children = {
            new TextBlock { Text = "Trendo Ökosystem", Foreground = Brush.Parse("#8FC3FF"), FontSize = 12.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) },
            Stat(Chip(Mdi.Wifi, "#29B6F6", "#0277BD", 26), "Verbunden", _ecoDevices),
            Stat(Chip(Mdi.RobotHappy, "#66BB6A", "#2E7D32", 26), "Aktive Agenten", _ecoAgents),
            Stat(Chip(Mdi.Folder, "#42A5F5", "#1565C0", 26), "Laufende Projekte", _ecoProjects),
            Stat(Chip(Mdi.ShieldCheck, "#22C55E", "#15803D", 26), "System-Status", _ecoState) } };
        var ecoPanel = Glass2(eco, 10); ecoPanel.VerticalAlignment = VerticalAlignment.Bottom; ecoPanel.Margin = new Thickness(0);
        ecoPanel.Background = Brush.Parse(C("#A6081329", "#E6F4F7FC"));
        var earthBox = new Grid { Name = "widget-eco", ClipToBounds = true, Children = { earth, ecoPanel } };

        var owner = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
            new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Grad("#2F86F6", "#0B2A6B"), Child = new TextBlock { Text = Initial(_settings.Name), Foreground = Brushes.White, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
            Pos(new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = string.IsNullOrWhiteSpace(_settings.Name) ? "Trendo" : _settings.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = "Besitzer · lokal", Foreground = Muted, FontSize = 10.5 } } }, 1) } };
        var ownerBtn = Flat("btn-owner", owner, "Besitzer – Einstellungen"); ownerBtn.HorizontalAlignment = HorizontalAlignment.Stretch; ownerBtn.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ownerBtn.Click += (_, _) => Show("settings");
        var ownerPanel = Glass2(ownerBtn, 4); ownerPanel.Margin = new Thickness(0, 4, 0, 0);

        var pins = PinsCard();
        var g = new Grid { Name = "left-column", RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        g.Children.Add(navPanel);
        Grid.SetRow(pins, 1); g.Children.Add(pins);
        Grid.SetRow(earthBox, 2); g.Children.Add(earthBox);
        Grid.SetRow(ownerPanel, 3); g.Children.Add(ownerPanel);
        _shellParts.Add(g);
        return g;
    }

    private static readonly Dictionary<string, Bitmap?> DeskPictures = [];
    /// <summary>Bilder aus Assets/desktop/&lt;name&gt;.jpg (Herkunft docs/design/DESKTOPBILDER.json).</summary>
    private static Bitmap? DesktopPicture(string name)
    {
        lock (DeskPictures)
        {
            if (DeskPictures.TryGetValue(name, out var b)) return b;
            try { using var st = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/desktop/{name}.jpg")); b = Bitmap.DecodeToWidth(st, 384); }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or ArgumentException or InvalidOperationException) { b = null; }
            return DeskPictures[name] = b;
        }
    }

    private static Button SideItem(string name, Control icon, string label, Action onClick, double font = 12.5)
    {
        var b = Flat(name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { icon, new TextBlock { Text = label, Foreground = Text, FontSize = font, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 140 } } }, label);
        ToolTip.SetTip(b, label);
        b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(12, 7); b.MinHeight = 0;
        b.Click += (_, _) => onClick();
        return b;
    }

    private Control PinsCard()
    {
        var head = new TextBlock { Text = "Angeheftet", Foreground = Muted, FontSize = 12, Margin = new Thickness(2, 0, 0, 2) };
        var card = Glass2(new StackPanel { Spacing = 4, Children = { head, new ScrollViewer { Content = _pinsPanel, MaxHeight = 150 } } }, 8);
        card.Name = "widget-pins";
        card.IsVisible = false;
        _pinCard = card;
        return card;
    }

    // ---------- Mitte: Aufgaben-Chips, Studios, Systemmodule, Projekte ----------
    private readonly StackPanel _deskProjects = new() { Name = "desk-projects", Orientation = Orientation.Horizontal, Spacing = 10 };

    private IEnumerable<(string Id, string Title, string Sub)> DesktopStudios()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in UserSettings.Ordered(FeaturedStudios, f => f.Item1, _settings.TileOrder))
            if (seen.Add(f.Id) && !_settings.HiddenTiles.Contains("tile-" + f.Id)) yield return (f.Id, StudioCatalog.NameOf(f.Id, f.Title), f.Sub);
        foreach (var s in _studios)
            if (seen.Add(s.Id) && !_settings.HiddenTiles.Contains("tile-" + s.Id) && StudioCatalog.For(s.Id) is { } info) yield return (s.Id, info.Name, info.Sentence);
    }

    private Control DesktopView()
    {
        var chips = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
        foreach (var (label, go) in new (string, Action)[] {
                     ("Erstellen", () => Show("projects")),
                     ("Analysieren", () => Show("system")),
                     ("Recherchieren", () => Show("recherche")),
                     ("Automatisieren", () => Show("ablaeufe")),
                     ("Planen", () => Show("alltag-zeit")),
                     ("Programmieren", () => { _studioFilter = "CORE-07|CORE-06|X-06"; _studioQuery = ""; Show("studios"); }),
                     ("Visualisieren", () => { _studioFilter = "CORE-08|CORE-05|SPEC-08"; _studioQuery = ""; Show("studios"); }),
                     ("Mehr …", () => Show("apps")) })
        {
            var b = new Button { Name = "action-" + label.TrimEnd('.', ' ', '…').ToLowerInvariant(), Content = label, Margin = new Thickness(0, 0, 8, 4), Padding = new Thickness(16, 5), CornerRadius = new CornerRadius(14), FontSize = 12,
                Foreground = Text, Background = Brush.Parse(C("#99081329", "#CCFFFFFF")), BorderBrush = Brush.Parse("#665AA9FF"), BorderThickness = new Thickness(1) };
            b.Click += (_, _) => go();
            Hover(b); chips.Children.Add(b);
        }

        // Studios als Bildkacheln (6 × 4)
        var tiles = new UniformGrid { Columns = 6 };
        foreach (var (id, title, sub) in DesktopStudios().Where(s => !_settings.SimpleMode || Trendo.Core.Sys.A11y.SimpleTiles.Contains(s.Id)).Take(24))
        {
            var st = _studios.FirstOrDefault(s => s.Id == id);
            var badge = Badge(st);
            var pic = StudioPicture(id);
            var face = new Grid { ClipToBounds = true };
            if (pic is not null) face.Children.Add(new Image { Source = pic, Stretch = Stretch.UniformToFill });
            else face.Children.Add(new Border { Background = Grad("#1A3A6B", "#0A1A36") });
            face.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0.35, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#00040A18"), 0), new GradientStop(Color.Parse("#E6040A18"), 1) } } });
            face.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 6, 6), Children = {
                new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 11.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = sub, Foreground = Brush.Parse("#C9D6EA"), FontSize = 9.5, TextTrimming = TextTrimming.CharacterEllipsis } } });
            var t = new Button { Name = "tile-" + id, Height = 78, Margin = new Thickness(3), Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = Brushes.Transparent, BorderBrush = Brush.Parse("#553F7FD0"), BorderThickness = new Thickness(1), Content = face };
            Avalonia.Automation.AutomationProperties.SetName(t, badge.Text is { Length: > 0 } ? $"{title}: {badge.Text}" : title);
            ToolTip.SetTip(t, st is null ? title : $"{title} – {badge.Text}");
            var sid = id;
            t.Click += async (_, _) => await OpenStudio(sid, title);
            Hover(t); tiles.Children.Add(t);
        }
        var all = SmallOutline("btn-all-studios", "Alle anzeigen");
        all.Click += (_, _) => { _studioFilter = null; _studioQuery = ""; Show("studios"); };
        var studiosPanel = Glass2(new StackPanel { Children = { Head("Studios", "Alle kreativen und technischen Studios auf einen Blick", all, Mdi.ViewGridOutline), tiles } }, 10);
        studiosPanel.Name = "widget-studios"; studiosPanel.IsVisible = !_settings.HiddenTiles.Contains("widget-studios");

        // Systemmodule: Kernfunktionen von Trendo OS
        var mods = new UniformGrid { Columns = 5 };
        foreach (var (key, title, sub, icon, c1, c2) in new[] {
                     ("ki", "Trendo KI", "Verstehen, planen, erstellen", Mdi.StarFourPoints, "#8FD8FF", "#2F86F6"),
                     ("ki-rollen", "Agenten-Zentrale", "12 Rollen, Teams, Aufgaben", Mdi.RobotHappy, "#9CF5A8", "#22B35E"),
                     ("ablaeufe", "Abläufe", "Schritte automatisch ausführen", Mdi.Tools, "#FFD39A", "#F08A3C"),
                     ("ki-wissen", "Wissen & Gedächtnis", "Eigene Dokumente, Kontext", Mdi.Brain, "#C7A6FF", "#7C4DFF"),
                     ("components", "Programme", "Vollpaket, ohne Download", Mdi.Apps, "#7FD3FF", "#3F6BFF"),
                     ("ki-quellen", "Freie Quellen", "Kostenlose Modelle & Assets", Mdi.Magnify, "#8FB8FF", "#3D5BFF"),
                     ("dev", "Code & GitHub", "Projekte, Versionen", Mdi.CodeTags, "#ECEFF1", "#90A4AE"),
                     ("approvals", "Freigaben", "Du entscheidest vorher", Mdi.ShieldCheck, "#9CF5A8", "#15803D"),
                     ("ressourcen", "Ressourcen", "Leistung gezielt verteilen", Mdi.ChartLine, "#FF8A80", "#E53935"),
                     ("handy", "Handy-Verbindung", "Dateien, Bildschirm, Benachrichtigungen", Mdi.Cellphone, "#80DEEA", "#00838F") })
        {
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { Chip(icon, c1, c2, 30, 0.58),
                Pos(new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = title, Foreground = Text, FontSize = 11.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = sub, Foreground = Muted, FontSize = 9.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1) } };
            var b = new Button { Name = "module-" + key, Height = 46, Margin = new Thickness(3), Padding = new Thickness(8, 4), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(8), Background = Brush.Parse(C("#80102347", "#CCFFFFFF")), BorderBrush = Brush.Parse("#403F7FD0"), BorderThickness = new Thickness(1), Content = content };
            Avalonia.Automation.AutomationProperties.SetName(b, title); ToolTip.SetTip(b, $"{title} – {sub}");
            var target = key;
            b.Click += (_, _) => Show(target);
            Hover(b); mods.Children.Add(b);
        }
        var modPanel = Glass2(new StackPanel { Children = { Head("Systemmodule", "Kernfunktionen von Trendo OS", null, Mdi.CubeOutline), mods } }, 10);
        modPanel.Name = "widget-module"; modPanel.IsVisible = !_settings.HiddenTiles.Contains("widget-module");

        // Aktuelle Projekte: echte Projekte als Bildkarten
        var allP = SmallOutline("btn-all-projects", "Alle Projekte anzeigen"); allP.Click += (_, _) => Show("projects");
        Detach(_deskProjects);
        var projPanel = Glass2(new StackPanel { Children = { Head("Aktuelle Projekte", "Zuletzt angelegt", allP, Mdi.Folder),
            new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _deskProjects } } }, 10);
        projPanel.Name = "widget-projekte"; projPanel.IsVisible = !_settings.HiddenTiles.Contains("widget-projekte");
        RefreshProjects();

        var stack = new StackPanel { Children = { chips, DesktopZettel(), studiosPanel, modPanel, projPanel } };
        var g = new Grid { Children = { new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } } };
        DesktopMenu(g);
        if (_deskCenter is not null) _shellParts.Remove(_deskCenter);
        _deskCenter = g; _shellParts.Add(g);
        return g;
    }

    // ---------- Rechte Spalte: KI-Router, Geräte, Agenten Live, Studio Manager, Open Source ----------
    private readonly Dictionary<string, (Arc Arc, TextBlock Value)> _rings = [];
    private readonly TextBlock _routerLocal = new() { Name = "router-local" }, _routerLarge = new() { Name = "router-large" };
    private readonly TextBlock _agentsCount = new() { Name = "agents-count" };
    private readonly StackPanel _agentRows = new() { Name = "agent-rows", Spacing = 4 };
    private readonly TextBlock _ossCount = new() { Name = "oss-count" };

    private Control RightColumn()
    {
        var sp = new StackPanel { Name = "right-column" };
        void Add(Border c, string key) { c.Name = key; c.IsVisible = !_settings.HiddenTiles.Contains(key); sp.Children.Add(c); }
        Control Line(Control chip, string title, string sub, Control right) => new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 6), Children = { chip,
            Pos(new StackPanel { Margin = new Thickness(9, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = title, Foreground = Text, FontSize = 11.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = sub, Foreground = Muted, FontSize = 9.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1), Pos(right, 2) } };

        // KI-Router: welche KI arbeitet – echt
        var router = new StackPanel { Children = { Head("KI-Router", "Passende KI je Aufgabe", null, Mdi.Brain),
            Line(Chip(Mdi.StarFourPoints, "#8FD8FF", "#2F86F6", 30), "Trendo KI (lokal)", "Läuft auf diesem PC, ohne Internet", Pill(_routerLocal)),
            Line(Chip(Mdi.Brain, "#C7A6FF", "#7C4DFF", 30), "Großes Modell (3B)", "Besseres Deutsch, ab 16 GB RAM", Pill(_routerLarge)),
            Line(Chip(Mdi.ShieldCheck, "#FFD39A", "#F08A3C", 30), "Kostenlimit 0 €", "Keine kostenpflichtige KI", Pill(StateBadge("router-cost", "Aktiv", true))) } };
        Add(Glass2(router, 10), "widget-ki");

        // Geräte: dieser PC mit echten Messwerten, Handy und Pi über ihre Seiten
        var rings = new UniformGrid { Columns = 4, Margin = new Thickness(0, 2, 0, 6) };
        foreach (var k in new[] { "CPU", "RAM", "SSD", "GPU" }) rings.Children.Add(Ring(k));
        Detach(_sysDot); Detach(_sysState);
        var allD = SmallOutline("btn-all-devices", "Alle anzeigen"); allD.Click += (_, _) => Show("geraete");
        Button DevLink(string name, string target, Control chip, string title, string sub)
        {
            var b = Flat(name, Line(chip, title, sub, Ico(Mdi.ChevronRight, 12, Muted)), title); b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Stretch; b.Padding = new Thickness(0);
            b.Click += (_, _) => Show(target); return b;
        }
        var devices = new StackPanel { Children = { Head("Geräte", null, allD, Mdi.Monitor),
            new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { Chip(Mdi.Monitor, "#42A5F5", "#1565C0", 30),
                Pos(new StackPanel { Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = "Dieser PC", Foreground = Text, FontSize = 11.5, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = "Windows · Arbeitsplatz", Foreground = Muted, FontSize = 9.5 } } }, 1),
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { _sysDot, _sysState } }, 2) } },
            rings,
            DevLink("dev-handy", "handy", Chip(Mdi.Cellphone, "#80DEEA", "#00838F", 30), "Handy", "Per Kabel oder WLAN koppeln"),
            DevLink("dev-pi", "verbund", Chip(Mdi.Raspberry, "#FF8A80", "#C62828", 30), "Raspberry Pi", "Geräteverbund einrichten") } };
        _sysState.FontSize = 10.5;
        Add(Glass2(devices, 10), "widget-system");

        // Agenten Live: Rollen mit echtem Stand aus dem Agenten-Zentrum
        var allA = SmallOutline("btn-all-agents", "Alle anzeigen"); allA.Click += (_, _) => Show("ki-rollen");
        _agentsCount.FontSize = 10.5; _agentsCount.Foreground = Muted;
        Detach(_agentRows);
        var agentsHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Head("Agenten Live", null, null, Mdi.RobotHappy), Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _agentsCount, allA } }, 1) } };
        Add(Glass2(new StackPanel { Children = { agentsHead, _agentRows } }, 10), "widget-agenten");

        // Studio Manager: Studios auf dem Desktop zeigen oder ausblenden (gespeichert)
        var mgr = new StackPanel { Children = { Head("Studio Manager", "Was der Desktop zeigt", null, Mdi.CogOutline) } };
        foreach (var f in FeaturedStudios.Take(4))
        {
            var key = "tile-" + f.Id; var name = StudioCatalog.NameOf(f.Id, f.Title);
            var sw = new ToggleSwitch { Name = "mgr-" + f.Id, IsChecked = !_settings.HiddenTiles.Contains(key), OnContent = "", OffContent = "", MinWidth = 0, Margin = new Thickness(0, -6, -8, -6), VerticalAlignment = VerticalAlignment.Center };
            Avalonia.Automation.AutomationProperties.SetName(sw, $"{name} auf dem Desktop zeigen");
            sw.IsCheckedChanged += (_, _) =>
            {
                if (sw.IsChecked == true) _settings.HiddenTiles.Remove(key); else if (!_settings.HiddenTiles.Contains(key)) _settings.HiddenTiles.Add(key);
                SaveSettings(); if (CurrentView == "start") Show("start");
            };
            mgr.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 2), Children = {
                new Ellipse { Width = 7, Height = 7, Fill = Brush.Parse(sw.IsChecked == true ? "#4ADE80" : "#64748B"), VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Text = name, Foreground = Text, FontSize = 11, Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 1), Pos(sw, 2) } });
        }
        Add(Glass2(mgr, 10), "widget-studiomanager");

        // Open Source: mitgelieferte freie Programme – echt gezählt
        var progs = SmallOutline("btn-oss-programs", "Programme ansehen"); progs.Click += (_, _) => Show("components");
        _ossCount.FontSize = 11.5; _ossCount.Foreground = Text; _ossCount.FontWeight = FontWeight.SemiBold;
        Add(Glass2(new StackPanel { Children = { Head("Open Source", null, progs, Mdi.CodeTags),
            new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { Chip(Mdi.Apps, "#7FD3FF", "#3F6BFF", 30),
                Pos(new StackPanel { Margin = new Thickness(9, 0, 0, 0), Children = { _ossCount, new TextBlock { Text = "Freie Programme aus dem Vollpaket, ohne Download", Foreground = Muted, FontSize = 9.5, TextWrapping = TextWrapping.Wrap } } }, 1) } } } }, 10), "widget-github");
        var right = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _shellParts.Add(right);
        return right;
    }

    private Control Ring(string key)
    {
        var bg = new Ellipse { Width = 46, Height = 46, Stroke = Brush.Parse(C("#2AFFFFFF", "#22000000")), StrokeThickness = 4 };
        var arc = new Arc { Width = 46, Height = 46, StrokeThickness = 4, StartAngle = -90, SweepAngle = 0, StrokeLineCap = PenLineCap.Round,
            Stroke = Grad("#34E3C3", "#18B892"), Effect = new DropShadowEffect { Color = Color.Parse("#22D3A6"), BlurRadius = 6, OffsetX = 0, OffsetY = 0, Opacity = 0.8 } };
        var val = new TextBlock { Name = "metric-" + key.ToLowerInvariant(), Text = "…", Foreground = Text, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center };
        _rings[key] = (arc, val);
        return new Grid { Name = "ring-" + key.ToLowerInvariant(), Width = 50, Height = 50, Children = { new Ellipse { Width = 46, Height = 46, Fill = Brush.Parse(C("#66091428", "#E6FFFFFF")) }, bg, arc,
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = key, Foreground = Muted, FontSize = 8.5, HorizontalAlignment = HorizontalAlignment.Center }, val } } } };
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

    /// <summary>Echte Werte für Ökosystem, KI-Router, Agenten und Open Source (Projekte, Freigaben und Messwerte kommen über die eigenen Wege).</summary>
    private void RefreshDesk()
    {
        var ki = _kiInput.IsEnabled;
        void Set(TextBlock b, string text, bool on) { b.Text = text; b.FontSize = 9.5; b.FontWeight = FontWeight.SemiBold; b.Padding = new Thickness(7, 2); b.Foreground = Brush.Parse(on ? "#4ADE80" : "#C9D6EA"); if (b.Parent is Border p) p.Background = Brush.Parse(on ? "#2622C55E" : "#22FFFFFF"); }
        Set(_routerLocal, ki ? "Aktiv" : "Einrichten", ki);
        var large = _deps.HasModel(DependencyCatalog.LargeModel);
        Set(_routerLarge, large ? "Bereit" : "Optional", large);
        var tasks = Agents.Tasks();
        var running = tasks.Count(t => t.State == AgentTaskState.Laeuft);
        _agentsCount.Text = $"{running} aktiv";
        _agentRows.Children.Clear();
        foreach (var id in new[] { "planer", "recherche", "texter", "programmierer" })
        {
            var role = AgentCenter.Role(id);
            var last = tasks.FirstOrDefault(t => t.RoleId == id);
            var (state, on) = last?.State switch { AgentTaskState.Laeuft => ("Aktiv", true), AgentTaskState.WartetAufFreigabe => ("Freigabe", false), AgentTaskState.Fertig or AgentTaskState.Gespeichert => ("Fertig", true), _ => ("Bereit", false) };
            _agentRows.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = Grad("#1E3A6B", "#0B1A36"), BorderBrush = Brush.Parse("#553F7FD0"), BorderThickness = new Thickness(1),
                    Child = new TextBlock { Text = role.Name[..1], Foreground = Text, FontSize = 11, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                Pos(new StackPanel { Margin = new Thickness(9, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = role.Name, Foreground = Text, FontSize = 11, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = last?.Goal is { Length: > 0 } g ? g : role.Does, Foreground = Muted, FontSize = 9.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
                Pos(Pill(StateBadge("agent-state-" + id, state, on)), 2) } });
        }
        var included = DependencyCatalog.All.Where(c => c.Mode == DeliveryMode.Included).ToList();
        _ossCount.Text = $"{included.Count(_deps.IsInstalled)} von {included.Count} Programmen eingerichtet";
        _ecoDevices.Text = Devices.Count > 0 ? $"Dieser PC · {Devices.Count} Geräte" : "Dieser PC";
        _ecoAgents.Text = running.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ecoProjects.Text = _core.Projects.List().Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ecoState.Text = _sysState.Text ?? "…";
        _ecoState.Foreground = _sysState.Foreground;
        ApplyAccessibility(_agentRows);
    }

    private void RefreshProjects()
    {
        _projectsPanel.Children.Clear();
        _deskProjects.Children.Clear();
        var list = _core.Projects.List().OrderByDescending(p => p.CreatedAt).Take(5).ToList();
        if (list.Count == 0)
        {
            var b = new Button { Name = "btn-first-project", Content = "＋ Erstes Projekt anlegen", FontSize = 12, Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(8), Background = Brush.Parse("#332F86F6"), BorderBrush = Brush.Parse("#665AA9FF"), BorderThickness = new Thickness(1) };
            b.Click += (_, _) => Show("projects");
            _deskProjects.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { new TextBlock { Text = "Noch keine Projekte.", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, b } });
        }
        else
        {
            string[] pics = ["CORE-05", "CORE-08", "STUDIO-LETTER-01", "CORE-07", "SPEC-01"];
            var i = 0;
            foreach (var p in list)
            {
                var face = new Grid { ClipToBounds = true };
                if (StudioPicture(pics[i++ % pics.Length]) is { } pic) face.Children.Add(new Image { Source = pic, Stretch = Stretch.UniformToFill, Opacity = 0.85 });
                face.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0.2, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.Parse("#00040A18"), 0), new GradientStop(Color.Parse("#F0040A18"), 1) } } });
                face.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 8, 8), Children = {
                    new TextBlock { Text = p.Name, Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"Angelegt {p.CreatedAt:dd.MM.yyyy}", Foreground = Brush.Parse("#C9D6EA"), FontSize = 10 } } });
                var card = new Button { Name = "project-open-" + p.Id, Width = 176, Height = 84, Padding = new Thickness(0), CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brushes.Transparent,
                    BorderBrush = Brush.Parse("#553F7FD0"), BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Content = face };
                ToolTip.SetTip(card, "Projekte öffnen"); Avalonia.Automation.AutomationProperties.SetName(card, $"{p.Name} öffnen");
                card.Click += (_, _) => Show("projects");
                Hover(card); _deskProjects.Children.Add(card);
                // dieselben Projekte auch in der bisherigen Liste (z. B. für andere Seiten)
                _projectsPanel.Children.Add(new TextBlock { Text = p.Name, Foreground = Text, FontSize = 12 });
            }
        }
        RefreshPins();
        RefreshDesk();
        ApplyAccessibility(_deskProjects);
    }
}
