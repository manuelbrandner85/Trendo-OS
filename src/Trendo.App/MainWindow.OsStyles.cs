using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Trendo.Core;
using Trendo.Core.Shell;

namespace Trendo.App;

/// <summary>Bedienstil wie Windows 11, macOS oder Linux (GNOME) – Wunsch 04.11.2026: Desktop, Leisten, Startmenü, Fenster und Farben wie beim
/// jeweiligen System, ohne eine Trendo-Funktion zu verlieren. Der Stil „Trendo“ bleibt unverändert.
/// Nachgebaut nach den öffentlichen Gestaltungsregeln (Fluent/Windows 11, Apple HIG, GNOME HIG/Adwaita): Maße, Anordnung, Farben, Verhalten.
/// Fremde Logos, Hintergrundbilder und Symbole werden nicht übernommen – an ihrer Stelle steht das Trendo-Logo bzw. eigene Symbole.</summary>
public sealed partial class MainWindow
{
    static bool OsStyle => _style is "windows" or "mac" or "linux";
    private readonly Border _osLauncher = new() { Name = "os-launcher", IsVisible = false, ZIndex = 1000 };
    private readonly Border _osWidgets = new() { Name = "os-widgets", IsVisible = false, ZIndex = 990 };
    private readonly StackPanel _osRunning = new() { Name = "os-running", Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly StackPanel _osDeskExtras = new() { Name = "os-desk-extras", Spacing = 10 };
    private readonly TextBox _osSearch = new() { Name = "os-search" };
    private readonly StackPanel _osResults = new() { Name = "os-results", Spacing = 2 };
    private readonly List<Action> _osClockUpdates = [];
    private readonly Dictionary<string, Button> _dockButtons = [];
    private List<DockApp> _dockApps = [];
    private TextBlock? _osAppTitle;
    private string _osMode = "";
    private (Control Main, Control Results)? _osViews;
    public bool OsLauncherOpen => _osLauncher.IsVisible;
    public bool OsWidgetsOpen => _osWidgets.IsVisible;
    public string OsLauncherMode => _osMode;

    /// <summary>Höhe der Leisten oben/unten je Stil (Bildpunkte bei 100 %): Windows-Taskleiste 48, Mac-Menüleiste 24 + Dock, GNOME-Leiste 32.</summary>
    static (double Top, double Bottom) OsBars => _style switch { "windows" => (0, 48), "mac" => (24, 76), "linux" => (32, 0), _ => (0, 0) };
    static bool OsLight => _theme == "hell";

    /// <summary>Farben wie im Original (Windows 11 Mica, macOS, Adwaita) – hell und dunkel; eigene Akzentfarbe des Nutzers hat Vorrang.</summary>
    private static void ApplyOsPalette(UserSettings s)
    {
        var l = OsLight;
        (string glass, string strong, string tile, string edge, string muted, string text, string accent, string shadow) p = (_style, l) switch
        {
            ("windows", false) => ("#F2272727", "#FA202020", "#FF2B2B2B", "#1FFFFFFF", "#FFC5C5C5", "#FFFFFFFF", "#FF005FB8", "0 8 16 0 #42000000"),
            ("windows", true) => ("#F2F9F9F9", "#FFF3F3F3", "#FFFFFFFF", "#1A000000", "#FF5C5C5C", "#FF1B1B1B", "#FF005FB8", "0 8 16 0 #24000000"),
            ("mac", false) => ("#E6282828", "#FA1E1E1E", "#FF2C2C2E", "#26FFFFFF", "#FF98989D", "#E5FFFFFF", "#FF0A84FF", "0 22 70 4 #59000000"),
            ("mac", true) => ("#E6F5F5F5", "#FFECECEC", "#FFFFFFFF", "#1A000000", "#FF6E6E73", "#D9000000", "#FF007AFF", "0 22 70 4 #40000000"),
            ("linux", false) => ("#FF2E2E32", "#FF222226", "#FF36363A", "#26FFFFFF", "#FF9A9A9E", "#FFFFFFFF", "#FF3584E4", "0 2 8 0 #4D000000"),
            _ => ("#FFFFFFFF", "#FFFAFAFB", "#FFFFFFFF", "#1F000000", "#FF5E5C64", "#CC000006", "#FF3584E4", "0 2 8 0 #26000000"),
        };
        Glass = Brush.Parse(p.glass); GlassStrong = Brush.Parse(p.strong); TileBg = Brush.Parse(p.tile); Edge = Brush.Parse(p.edge); EdgeLit = Edge;
        Muted = Brush.Parse(p.muted); Text = Brush.Parse(p.text); CardShadow = BoxShadows.Parse(p.shadow);
        var own = s.EffectiveAccent(DateTime.Now);
        Accent = own != "#C9A227" && AccentChoices.Any(a => a.Id == own) ? Brush.Parse(own) : Brush.Parse(p.accent);
    }

    /// <summary>Systemschrift: Segoe UI Variable (Windows), SF Pro (Mac, nur wenn vorhanden), Cantarell (GNOME) – sonst Inter.</summary>
    static FontFamily? OsFont => _style switch
    {
        "windows" => new FontFamily("Segoe UI Variable Text, Segoe UI, Inter"),
        "mac" => new FontFamily("SF Pro Text, .AppleSystemUIFont, Helvetica Neue, Inter"),
        "linux" => new FontFamily("Cantarell, Adwaita Sans, Ubuntu, Inter"),
        _ => null,
    };

    /// <summary>Baut die Hülle des Stils: Seitenspalten und Trendo-Kopf weg, dafür Taskleiste/Menüleiste/Dock/GNOME-Leiste; Widgets und KI behalten ihren Platz.</summary>
    private void ApplyOsShell(Grid root, Grid ui, Grid body, Control top, Control left, Control right, Control dock)
    {
        if (!OsStyle) return;
        if (OsFont is { } f) FontFamily = f;
        if (OsArrowCursor() is { } cur) Cursor = cur; // Mauszeiger wie im System (Windows weiß, Mac und GNOME schwarz)
        OsMenuStyles();
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key is not (Key.LWin or Key.RWin) && OsWindowKey(e.Key, e.KeyModifiers)) e.Handled = true; }, RoutingStrategies.Tunnel);
        top.IsVisible = false; left.IsVisible = false; dock.IsVisible = false;
        body.Children.Remove(right); // die Widgets ziehen in die Widget-Leiste / Mitteilungszentrale / Kalender-Ansicht
        body.ColumnDefinitions = new ColumnDefinitions("0,*,0"); body.Margin = new Thickness(0);
        _center.Margin = new Thickness(16, 12, 16, 8);
        var (t, b) = OsBars; ui.Margin = new Thickness(0, t, 0, b);
        switch (_style)
        {
            case "windows": root.Children.Add(WinTaskbar()); break;
            case "mac": root.Children.Add(MacMenuBar()); root.Children.Add(MacDock()); break;
            case "linux": root.Children.Add(GnomeTopBar()); break;
        }
        // Widgets: alles aus der rechten Spalte plus Schnellaktionen und Projekte vom Trendo-Desktop
        var scroll = new ScrollViewer { Content = new StackPanel { Spacing = 10, Children = { _osDeskExtras, right } }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        right.Width = double.NaN; right.HorizontalAlignment = HorizontalAlignment.Stretch;
        var head = new TextBlock { Text = _style switch { "windows" => "Widgets", "mac" => "Mitteilungen und Widgets", _ => "Mitteilungen" }, FontSize = _style == "windows" ? 20 : 15, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(4, 0, 0, 10) };
        _osWidgets.Child = new DockPanel { Children = { Top(head), scroll } };
        _osWidgets.Background = GlassStrong; _osWidgets.BorderBrush = Edge; _osWidgets.BorderThickness = new Thickness(1); _osWidgets.BoxShadow = CardShadow; _osWidgets.Padding = new Thickness(16);
        switch (_style)
        {
            case "windows": // Widget-Leiste links, über der Taskleiste
                _osWidgets.Width = 470; _osWidgets.CornerRadius = new CornerRadius(8); _osWidgets.HorizontalAlignment = HorizontalAlignment.Left; _osWidgets.VerticalAlignment = VerticalAlignment.Stretch;
                _osWidgets.Margin = new Thickness(12, 12, 0, 60); break;
            case "mac": // Mitteilungszentrale rechts unter der Menüleiste
                _osWidgets.Width = 360; _osWidgets.CornerRadius = new CornerRadius(18); _osWidgets.HorizontalAlignment = HorizontalAlignment.Right; _osWidgets.VerticalAlignment = VerticalAlignment.Stretch;
                _osWidgets.Margin = new Thickness(0, 32, 10, 10); _osWidgets.Background = Brush.Parse(OsLight ? "#E6F2F2F2" : "#D9262626"); break;
            default: // GNOME: Kalender und Mitteilungen mittig unter der Leiste
                _osWidgets.Width = 720; _osWidgets.CornerRadius = new CornerRadius(18); _osWidgets.HorizontalAlignment = HorizontalAlignment.Center; _osWidgets.VerticalAlignment = VerticalAlignment.Top;
                _osWidgets.MaxHeight = 720; _osWidgets.Margin = new Thickness(0, 38, 0, 0);
                head.Text = DateTime.Now.ToString("dddd, d. MMMM yyyy", CultureInfo.GetCultureInfo("de-DE")); break;
        }
        Grid.SetRowSpan(_osWidgets, 2); root.Children.Add(_osWidgets);
        Grid.SetRowSpan(_osLauncher, 2); root.Children.Add(_osLauncher);
        _osSearch.TextChanged += (_, _) => FillOsLauncher();
        _osSearch.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await OsSearchEnter(); } else if (e.Key == Key.Escape) { CloseOsLauncher(); e.Handled = true; } };
        // Klick daneben schließt Startmenü, Launchpad, Übersicht und Widgets
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is not Visual v) return;
            bool Inside(Control target) { for (Visual? p = v; p is not null; p = p.GetVisualParentSafe()) if (p == target || (p is Control c && c.Name?.StartsWith("os-toggle", StringComparison.Ordinal) == true)) return true; return false; }
            if (OsLauncherOpen && !Inside(_osLauncher)) CloseOsLauncher();
            if (OsWidgetsOpen && !Inside(_osWidgets)) _osWidgets.IsVisible = false;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => { foreach (var u in _osClockUpdates) u(); };
        timer.Start(); Closed += (_, _) => timer.Stop();
        foreach (var u in _osClockUpdates) u();
        RenderOsRunning();
        static Control Top(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Top); return c; }
    }

    /// <summary>Pfeil-Zeiger selbst gezeichnet: Windows weiß mit schwarzem Rand, Mac schwarz mit weißem Rand und Schatten, GNOME (Adwaita) schwarz mit weißem Rand.</summary>
    static Cursor? OsArrowCursor()
    {
        try
        {
            var white = _style == "windows";
            var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M2,2 L2,23 L7.2,18 L10.6,25.6 L13.8,24.2 L10.5,16.8 L17.6,16.8 Z"), Fill = white ? Brushes.White : Brushes.Black,
                Stroke = white ? Brushes.Black : Brushes.White, StrokeThickness = 1.4, StrokeJoin = PenLineJoin.Round };
            if (_style == "mac") path.Effect = new DropShadowEffect { BlurRadius = 3, OffsetX = 0, OffsetY = 1, Opacity = 0.45, Color = Colors.Black };
            var host = new Canvas { Width = 32, Height = 32, Children = { path } };
            host.Measure(new Size(32, 32)); host.Arrange(new Rect(0, 0, 32, 32));
            var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(32, 32)); rtb.Render(host);
            return new Cursor(rtb, new PixelPoint(2, 2));
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or PlatformNotSupportedException or ArgumentException or NullReferenceException) { return null; }
    }

    /// <summary>Menüs wie im System: Windows 11 Ecken 8, Mac 6 (schmale Ränder), GNOME 12; Hintergrund wie die Fenster.</summary>
    void OsMenuStyles()
    {
        var r = _style switch { "mac" => 6, "linux" => 12, _ => 8 };
        foreach (var t in new[] { typeof(ContextMenu), typeof(MenuFlyoutPresenter) })
        {
            var st = new Avalonia.Styling.Style(x => x.OfType(t));
            st.Setters.Add(new Avalonia.Styling.Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(r)));
            st.Setters.Add(new Avalonia.Styling.Setter(TemplatedControl.BackgroundProperty, GlassStrong));
            st.Setters.Add(new Avalonia.Styling.Setter(TemplatedControl.BorderBrushProperty, Edge));
            st.Setters.Add(new Avalonia.Styling.Setter(TemplatedControl.PaddingProperty, new Thickness(_style == "mac" ? 5 : 4)));
            Styles.Add(st);
        }
        var mi = new Avalonia.Styling.Style(x => x.OfType<MenuItem>());
        mi.Setters.Add(new Avalonia.Styling.Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(_style == "mac" ? 4 : _style == "linux" ? 6 : 4)));
        mi.Setters.Add(new Avalonia.Styling.Setter(TemplatedControl.PaddingProperty, _style == "mac" ? new Thickness(10, 3) : _style == "linux" ? new Thickness(12, 8) : new Thickness(12, 6)));
        Styles.Add(mi);
    }

    /// <summary>Fenster-Tastenkürzel wie im System (Mac ⌘W/⌘M/⌘Q/⌘Leer/⌘,; Windows Alt+F4, Win+↑/↓, Win+E/W/S/D; GNOME Alt+F4, Super+↑/↓/A).</summary>
    public bool OsWindowKey(Key key, KeyModifiers mods)
    {
        var act = OsBehavior.WindowKey(_style, key.ToString(), ModsOf(mods), OperatingSystem.IsMacOS());
        var w = Windows.Active;
        switch (act)
        {
            case "close" when w is not null && w.Page == "dateien" && key == Key.W && FilesTabCount > 1: FilesCloseTab(); return true; // ⌘W/Strg+W: erst die Registerkarte
            case "close" when w is not null: CloseWindow(w.Id); return true;
            case "minimize" when w is not null: Windows.Minimize(w.Id); AfterWindowChange(); return true;
            case "maximize" when w is not null && !w.Maximized: Windows.ToggleMaximize(w.Id); AfterWindowChange(); return true;
            case "restore" when w is not null && w.Maximized: Windows.ToggleMaximize(w.Id); AfterWindowChange(); return true;
            case "restore" when w is not null: Windows.Minimize(w.Id); AfterWindowChange(); return true;
            case "desktop": foreach (var x in Windows.TaskOrder.ToList()) Windows.Minimize(x.Id); AfterWindowChange(); return true;
            case "spotlight": ToggleOsLauncher(_style == "mac" ? "spotlight" : "search"); return true;
            case "search": ToggleOsLauncher("search"); return true;
            case "apps" or "overview": ToggleOsLauncher("start"); return true;
            case "settings": Show("settings"); return true;
            case "files": Show("dateien"); return true;
            case "widgets": ToggleOsWidgets(); return true;
            case "switch": CycleWindows(mods.HasFlag(KeyModifiers.Shift) ? -1 : 1); return true;
            default: return false;
        }
    }

    /// <summary>Desktop-Symbole wie im System: Spalte links oben (Windows, GNOME) bzw. rechts oben (Mac), Symbol mit Beschriftung darunter,
    /// Einfachklick wählt, Doppelklick öffnet. Windows zeigt „Dieser PC“ und „Papierkorb“ wie ab Werk.</summary>
    private Control OsDesktopIcons()
    {
        var col = new WrapPanel { Name = "os-desktop-icons", Orientation = Orientation.Vertical, MaxHeight = 760, HorizontalAlignment = _style == "mac" ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        Button? selected = null;
        void Ic(string name, Control icon, string label, Func<Task> open)
        {
            var text = new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 84, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect { BlurRadius = 4, OffsetX = 0, OffsetY = 1, Opacity = 0.9, Color = Colors.Black } };
            var b = new Button { Name = name, Width = 90, Height = 92, Margin = new Thickness(2), Padding = new Thickness(2, 4), Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent,
                CornerRadius = new CornerRadius(_style == "windows" ? 2 : 6), VerticalContentAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center,
                Content = new StackPanel { Spacing = 4, Children = { new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = icon }, text } } };
            Avalonia.Automation.AutomationProperties.SetName(b, label); ToolTip.SetTip(b, label);
            b.Click += (_, _) =>
            {
                if (selected is { } old) { old.Background = Brushes.Transparent; old.BorderBrush = Brushes.Transparent; }
                selected = b; b.Background = Brush.Parse(_style == "windows" ? "#4D99D9FF" : "#40FFFFFF"); b.BorderBrush = Brush.Parse(_style == "windows" ? "#9999D9FF" : "#00FFFFFF");
            };
            b.DoubleTapped += async (_, _) => await open();
            b.KeyDown += async (_, e) => { if (e.Key == Key.Enter && _style != "mac") await open(); };
            col.Children.Add(b);
        }
        if (_style == "windows")
        {
            Ic("os-desk-pc", new Border { Width = 48, Height = 40, CornerRadius = new CornerRadius(3), Background = Grad("#4FC3F7", "#0277BD"), Child = Ico(Mdi.Monitor, 30, Brushes.White) }, "Dieser PC", () => { FilesGo(PcPlace); return Task.CompletedTask; });
            Ic("os-desk-trash", Ico(Mdi.Delete, 44, Grad("#ECEFF1", "#90A4AE")), "Papierkorb", () => { if (_dockButtons.TryGetValue("trash", out var t)) t.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return Task.CompletedTask; });
        }
        foreach (var id in _settings.DesktopIcons)
        {
            if (DeskIconInfo(id) is not { } info) continue;
            var sid = id; var title = info.Title;
            Control icon = sid.StartsWith("page:", StringComparison.Ordinal) || sid.StartsWith("app:", StringComparison.Ordinal) ? Chip(Mdi.Apps, "#5C9DFF", "#283593", 46, 0.58)
                : StudioIcons.For(sid) is { } si ? Chip(si.Path, si.C1, si.C2, 46, 0.58) : Chip(Mdi.Apps, "#90A4AE", "#455A64", 46, 0.58);
            Ic("os-desk-" + sid.Replace(':', '-'), icon, title, async () => { if (sid.StartsWith("page:", StringComparison.Ordinal)) Show(sid[5..]); else if (sid.StartsWith("app:", StringComparison.Ordinal)) OpenWebApp(sid[4..]); else await OpenStudio(sid, title); });
            var rm = new MenuItem { Header = "Vom Desktop entfernen" }; rm.Click += (_, _) => SetOnDesktop(sid, false);
            ((Button)col.Children[^1]).ContextMenu = new ContextMenu { Items = { rm } };
        }
        return col;
    }

    // ---------- gemeinsame Bausteine ----------
    static readonly CultureInfo OsDe = CultureInfo.GetCultureInfo("de-DE");
    TextBlock OsClock(string name, Func<DateTime, string> fmt, double size, FontWeight weight = FontWeight.Normal, TextAlignment align = TextAlignment.Right)
    {
        var t = new TextBlock { Name = name, FontSize = size, FontWeight = weight, TextAlignment = align, VerticalAlignment = VerticalAlignment.Center };
        _osClockUpdates.Add(() => t.Text = fmt(DateTime.Now));
        return t;
    }

    static Button OsFlat(string name, Control content, string tip, Action click, double w = double.NaN, double h = double.NaN, double radius = 4)
    {
        var b = new Button { Name = name, Content = content, Width = w, Height = h, Padding = new Thickness(6, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(radius), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Foreground = Text };
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip); b.Click += (_, _) => click();
        return b;
    }

    /// <summary>Eigene, einfache Statussymbole (WLAN, Lautsprecher, Akku) – keine fremden Symbole.</summary>
    static Control StatusGlyphs(IBrush fg, double size = 15)
    {
        Control P(string d) => new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(d), Fill = fg, Stretch = Stretch.Uniform, Width = size, Height = size * 0.8, Margin = new Thickness(4, 0) };
        return new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = {
            P(Mdi.Wifi),
            P("M3 9 H7 L12 4 V20 L7 15 H3 Z M15 8 C17 9.5 17 14.5 15 16 L14 15 C15.3 13.8 15.3 10.2 14 9 Z M17.5 5.5 C21 8.5 21 15.5 17.5 18.5 L16.5 17.5 C19.3 15 19.3 9 16.5 6.5 Z"),
            P("M2 7 H19 V17 H2 Z M3.5 8.5 V15.5 H15 V8.5 Z M20 10 H22 V14 H20 Z") } };
    }

    /// <summary>Taskleisten-/Dock-Knopf für ein Trendo-Programm – ruft genau die Funktion des Trendo-Docks auf.</summary>
    Button OsAppButton(DockApp a, double box, double icon, double radius, bool squircle)
    {
        Control face = squircle
            ? new Border { Width = icon, Height = icon, CornerRadius = new CornerRadius(icon * 0.225), Background = Grad(a.C1, a.C2), BoxShadow = BoxShadows.Parse("0 1 3 0 #40000000"),
                Child = Ico(a.Icon, icon * 0.58, a.Fg ?? Brushes.White) }
            : Ico(a.Icon, icon, a.Fg ?? Grad(a.C1, a.C2));
        var b = OsFlat("os-app-" + a.Id, face, a.Label, () => { if (_dockButtons.TryGetValue(a.Id, out var d)) d.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }, box, box, radius);
        b.Padding = new Thickness(0);
        if (_dockButtons.TryGetValue(a.Id, out var orig)) { b.IsVisible = orig.IsVisible; b.IsEnabled = orig.IsEnabled; }
        ToolTip.SetPlacement(b, _style == "linux" ? PlacementMode.Top : PlacementMode.Top);
        return b;
    }

    // ---------- Windows 11 ----------
    private Control WinTaskbar()
    {
        var fg = Brush.Parse(OsLight ? "#FF1B1B1B" : "#FFFFFFFF");
        var start = OsFlat("os-start", Logo(26, tile: false), "Start", () => ToggleOsLauncher("start"), 40, 40);
        start.Name = "os-toggle-start";
        var search = new Button { Name = "os-toggle-search", Height = 32, Width = 210, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(16), HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brush.Parse(OsLight ? "#B3FFFFFF" : "#0FFFFFFF"), BorderBrush = Brush.Parse(OsLight ? "#14000000" : "#17FFFFFF"), BorderThickness = new Thickness(1), Margin = new Thickness(4, 0),
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ico(Mdi.Magnify, 15, fg), new TextBlock { Text = "Suche", Foreground = Brush.Parse(OsLight ? "#FF5C5C5C" : "#FFC5C5C5"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center } } } };
        ToolTip.SetTip(search, "Suchen und Trendo KI fragen"); search.Click += (_, _) => ToggleOsLauncher("search");
        var center = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Children = { start, search } };
        foreach (var a in _dockApps) center.Children.Add(OsAppButton(a, 40, 24, 4, false));
        center.Children.Add(_osRunning);
        var widgets = OsFlat("os-toggle-widgets", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
            Ico(Mdi.StarFourPoints, 22, Grad("#FFE08A", "#C9A227")),
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = "Trendo KI", FontSize = 12, Foreground = fg }, new TextBlock { Text = "Widgets", FontSize = 11, Foreground = Brush.Parse(OsLight ? "#FF5C5C5C" : "#FFC5C5C5") } } } } },
            "Widgets", ToggleOsWidgets, double.NaN, 40);
        widgets.Margin = new Thickness(8, 0, 0, 0); widgets.HorizontalAlignment = HorizontalAlignment.Left; widgets.Padding = new Thickness(8, 0);
        var halt = HaltCompact();
        var tray = OsFlat("os-tray", StatusGlyphs(fg), "Schnelleinstellungen – Netzwerk, Ton, Akku", () => Show("settings"), double.NaN, 40);
        var clockBox = OsFlat("os-toggle-clock", new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            Fg(OsClock("os-clock", d => d.ToString("HH:mm", OsDe), 12), fg), Fg(OsClock("os-date", d => d.ToString("dd.MM.yyyy", OsDe), 12), fg) } }, "Datum, Uhrzeit und Mitteilungen", ToggleOsWidgets, double.NaN, 40);
        var bell = OsFlat("os-bell", new Grid { Children = { Ico(Mdi.Bell, 16, fg), new Border { Background = Accent, CornerRadius = new CornerRadius(7), MinWidth = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -4, -6, 0), Child = new Viewbox { Child = OsBadge() } } } }, "Freigaben", () => Show("approvals"), 36, 40);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0),
            Children = { halt, OsFlat("os-cloud", Ico(Mdi.CloudOutline, 16, fg), "Online-Dienste", () => Show("keys"), 36, 40), tray, clockBox, bell } };
        var bar = new Border { Name = "os-taskbar", Height = 48, VerticalAlignment = VerticalAlignment.Bottom, ZIndex = 40,
            Background = Brush.Parse(OsLight ? "#EBF3F3F3" : "#EB1C1C1C"), BorderBrush = Brush.Parse(OsLight ? "#14000000" : "#1AFFFFFF"), BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new Grid { Children = { widgets, center, right } } };
        Grid.SetRowSpan(bar, 2);
        return bar;
    }

    static T Fg<T>(T t, IBrush b) where T : TextBlock { t.Foreground = b; return t; }
    TextBlock OsBadge() { var t = new TextBlock { FontSize = 10, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center }; _osClockUpdates.Add(() => { t.Text = _core.Approvals.Pending().Count is var n && n > 0 ? n.ToString(OsDe) : ""; }); return t; }

    /// <summary>Not-Stopp als kleines Symbol in Taskleiste/Menüleiste – gleiche Funktion wie der große Knopf.</summary>
    Button HaltCompact()
    {
        var dot = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8) };
        void Mark() { var on = _core.Approvals.Halted; dot.Background = Brush.Parse(on ? "#15803D" : "#DC2626"); dot.Child = Ico(on ? PlayIcon : StopIcon, 9, Brushes.White); }
        Mark(); _core.Approvals.HaltChanged += () => _ui.Post(Mark);
        var b = OsFlat("os-halt", dot, "Not-Stopp: alle Automationen sofort anhalten (Pause-Taste)", async () => { if (_core.Approvals.Halted) ResumeAll(); else await HaltAll("Not-Stopp (Leiste)"); }, 34, _style == "mac" ? 22 : _style == "linux" ? 28 : 40);
        return b;
    }

    // ---------- macOS ----------
    private Control MacMenuBar()
    {
        var fg = Brush.Parse(OsLight ? "#E6000000" : "#FFFFFFFF");
        MenuItem Mi(string header, Action a, string? gesture = null) { var m = new MenuItem { Header = header, InputGesture = gesture is null ? null : KeyGesture.Parse(gesture) }; m.Click += (_, _) => a(); return m; }
        Button Menu(string name, Control content, params MenuItem[] items)
        {
            var b = OsFlat("os-menu-" + name, content, name, () => { }, double.NaN, 22, 5); b.Padding = new Thickness(9, 0);
            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
            foreach (var i in items) flyout.Items.Add(i);
            b.Flyout = flyout; return b;
        }
        TextBlock T(string s, FontWeight w = FontWeight.Normal) => new() { Text = s, FontSize = 13, FontWeight = w, Foreground = fg, VerticalAlignment = VerticalAlignment.Center };
        _osAppTitle = T("Trendo OS", FontWeight.Bold);
        var left = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
            Menu("logo", Logo(15, tile: false), Mi("Über Trendo OS", () => Show("system")), Mi("Systemeinstellungen …", () => Show("settings"), "Ctrl+OemComma"), Mi("Programme hinzufügen …", () => OpenAddHub()), Mi("Not-Stopp", () => _ = HaltAll("Not-Stopp (Menü)"))),
            Menu("app", _osAppTitle, Mi("Studios", () => Show("studios")), Mi("Projekte", () => Show("projects")), Mi("Command Center", () => Show("ki"))),
            Menu("ablage", T("Ablage"), Mi("Neues Projekt", () => Show("projects")), Mi("Dateien", () => Show("akten")), Mi("Fenster schließen", () => { if (Windows.Active is { } w) CloseWindow(w.Id); }, "Ctrl+W")),
            Menu("bearbeiten", T("Bearbeiten"), Mi("Suchen …", () => ToggleOsLauncher("spotlight"), "Ctrl+F")),
            Menu("darstellung", T("Darstellung"), Mi("Mitteilungen und Widgets", ToggleOsWidgets), Mi("Launchpad", () => ToggleOsLauncher("start")), Mi("Aussehen …", () => Show("settings"))),
            Menu("gehezu", T("Gehe zu"), [.. SideNav.Select(n => Mi(n.Label, () => Show(n.Key)))]),
            Menu("fenster", T("Fenster"), Mi("Minimieren", () => { if (Windows.Active is { } w) { Windows.Minimize(w.Id); AfterWindowChange(); } }, "Ctrl+M"), Mi("Zoomen", () => { if (Windows.Active is { } w) { Windows.ToggleMaximize(w.Id); AfterWindowChange(); } })),
            Menu("hilfe", T("Hilfe"), Mi("Trendo KI fragen", () => Show("ki")), Mi("Hilfe zu Trendo", () => Show("hilfe"))) } };
        var spot = OsFlat("os-toggle-spotlight", Ico(Mdi.Magnify, 14, fg), "Spotlight – suchen und Trendo KI fragen", () => ToggleOsLauncher("spotlight"), 30, 22, 5);
        var cc = OsFlat("os-toggle-widgets", Ico("M7 6 A4 4 0 1 1 7 14 A4 4 0 1 1 7 6 M17 6 A4 4 0 1 1 17 14 A4 4 0 1 1 17 6 M4 16 H20 V20 H4 Z", 14, fg), "Kontrollzentrum und Mitteilungen", ToggleOsWidgets, 30, 22, 5);
        var clock = OsFlat("os-toggle-clock", Fg(OsClock("os-clock", d => d.ToString("ddd d. MMM  HH:mm", OsDe), 13), fg), "Mitteilungszentrale", ToggleOsWidgets, double.NaN, 22, 5);
        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
            Children = { HaltCompact(), OsFlat("os-bell", Ico(Mdi.Bell, 14, fg), "Freigaben", () => Show("approvals"), 30, 22, 5), OsFlat("os-tray", StatusGlyphs(fg, 14), "WLAN, Ton und Akku", () => Show("settings"), double.NaN, 22, 5), spot, cc, clock } };
        var bar = new Border { Name = "os-menubar", Height = 24, VerticalAlignment = VerticalAlignment.Top, ZIndex = 40, Background = Brush.Parse(OsLight ? "#B8F6F6F6" : "#B81E1E1E"),
            Child = new Grid { Children = { left, right } } };
        Grid.SetRowSpan(bar, 2);
        return bar;
    }

    private Control MacDock()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Bottom };
        var lp = new Border { Width = 50, Height = 50, CornerRadius = new CornerRadius(11), Background = Grad("#F4F4F6", "#B9BCC4"), Child = Ico(Mdi.ViewGrid, 28, Grad("#FF6B6B", "#4D7CFE")) };
        var launch = OsFlat("os-toggle-start", lp, "Launchpad", () => ToggleOsLauncher("start"), 54, 54, 12); launch.Padding = new Thickness(0);
        row.Children.Add(launch);
        foreach (var a in _dockApps) row.Children.Add(Dot(OsAppButton(a, 54, 50, 12, true), false));
        row.Children.Add(Sep()); row.Children.Add(_osRunning); row.Children.Add(Sep());
        var trash = OsFlat("os-trash", new Border { Width = 50, Height = 50, CornerRadius = new CornerRadius(11), Background = Grad("#ECEFF1", "#90A4AE"), Child = Ico(Mdi.Delete, 28, Brush.Parse("#455A64")) },
            "Papierkorb", () => { if (_dockButtons.TryGetValue("trash", out var t)) t.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }, 54, 54, 12);
        trash.Padding = new Thickness(0); row.Children.Add(trash);
        var dock = new Border { Name = "os-dock", ZIndex = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(5, 4, 5, 6),
            CornerRadius = new CornerRadius(18), Background = Brush.Parse(OsLight ? "#8CF6F6F6" : "#8C2A2A2C"), BorderBrush = Brush.Parse(OsLight ? "#66FFFFFF" : "#33FFFFFF"), BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 10 30 0 #59000000"), Child = row };
        Grid.SetRowSpan(dock, 2);
        return dock;
        Control Sep() => new Border { Width = 1, Height = 46, Margin = new Thickness(5, 0), VerticalAlignment = VerticalAlignment.Center, Background = Brush.Parse(OsLight ? "#33000000" : "#40FFFFFF") };
    }

    /// <summary>Kleiner Punkt unter dem Dock-Symbol, wenn das Programm läuft (wie im Mac-Dock).</summary>
    static Control Dot(Control c, bool running) => new StackPanel { Spacing = 1, Children = { c, new Border { Width = 4, Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Center, Background = running ? Text : Brushes.Transparent } } };

    // ---------- Linux (GNOME) ----------
    private Control GnomeTopBar()
    {
        var fg = Brushes.White;
        var pill = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Children = {
            new Border { Width = 32, Height = 8, CornerRadius = new CornerRadius(4), Background = fg },
            new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Brush.Parse("#99FFFFFF") } } };
        var act = OsFlat("os-toggle-activities", pill, "Aktivitäten – Übersicht, Programme und Suche", () => ToggleOsLauncher("start"), double.NaN, 24, 12);
        act.Padding = new Thickness(10, 0); act.Margin = new Thickness(6, 0, 0, 0); act.HorizontalAlignment = HorizontalAlignment.Left;
        var clock = OsFlat("os-toggle-clock", Fg(OsClock("os-clock", d => d.ToString("ddd", OsDe).TrimEnd('.') + ". " + d.ToString("HH:mm", OsDe), 14, FontWeight.Bold, TextAlignment.Center), fg), "Kalender und Mitteilungen", ToggleOsWidgets, double.NaN, 24, 12);
        clock.Padding = new Thickness(10, 0); clock.HorizontalAlignment = HorizontalAlignment.Center;
        var status = OsFlat("os-toggle-widgets", StatusGlyphs(fg, 14), "Schnelleinstellungen", ToggleOsWidgets, double.NaN, 24, 12);
        status.Padding = new Thickness(8, 0);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            Children = { HaltCompact(), OsFlat("os-bell", Ico(Mdi.Bell, 14, fg), "Freigaben", () => Show("approvals"), 30, 24, 12), OsFlat("os-settings", Ico(Mdi.CogOutline, 14, fg), "Einstellungen", () => Show("settings"), 30, 24, 12), status } };
        var bar = new Border { Name = "os-topbar", Height = 32, VerticalAlignment = VerticalAlignment.Top, ZIndex = 40, Background = Brushes.Black, Child = new Grid { Children = { act, clock, right } } };
        Grid.SetRowSpan(bar, 2);
        return bar;
    }

    // ---------- Laufende Fenster in Taskleiste/Dock ----------
    private void RenderOsRunning()
    {
        if (!OsStyle) return;
        _osRunning.Children.Clear();
        if (_style == "linux") return; // GNOME zeigt laufende Programme in der Übersicht
        var active = Windows.Active?.Id;
        foreach (var w in Windows.TaskOrder)
        {
            var id = w.Id; var on = id == active && !w.Minimized;
            var iconId = _windowStudio.TryGetValue(w.Id, out var sid) ? sid : ModuleOf(w.Page);
            Control? Own(double s) => w.Page == "dateien" ? FolderIcon(s) : IconFor(iconId, s);
            if (_style == "windows")
            {
                var ic = Own(22) ?? Ico(Mdi.Apps, 22, Text);
                var mark = new Border { Height = 3, Width = on ? 16 : 6, CornerRadius = new CornerRadius(1.5), Background = on ? Accent : Brush.Parse(OsLight ? "#FF8A8A8A" : "#FF9D9D9D"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1) };
                var b = OsFlat("os-task-" + id, new Grid { Width = 40, Height = 40, Children = { new Border { Child = ic, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, mark } }, w.Title, () => TaskbarClick(id), 40, 40);
                b.Padding = new Thickness(0); if (on) b.Background = Brush.Parse(OsLight ? "#B3FFFFFF" : "#0FFFFFFF");
                _osRunning.Children.Add(b);
            }
            else
            {
                var face = new Border { Width = 50, Height = 50, CornerRadius = new CornerRadius(11), Background = TileBg, Child = Own(30) ?? Ico(Mdi.Apps, 30, Text) };
                var b = OsFlat("os-task-" + id, face, w.Title, () => TaskbarClick(id), 54, 54, 12); b.Padding = new Thickness(0);
                _osRunning.Children.Add(Dot(b, true));
            }
        }
        if (_osAppTitle is not null) _osAppTitle.Text = Windows.Active is { Minimized: false } aw ? aw.Title : "Trendo OS";
    }

    // ---------- Startmenü (Windows), Launchpad/Spotlight (Mac), Übersicht (GNOME) ----------
    public void ToggleOsLauncher(string mode) { if (OsLauncherOpen && _osMode == mode) CloseOsLauncher(); else OpenOsLauncher(mode); }
    public void ToggleOsWidgets() { _osWidgets.IsVisible = !_osWidgets.IsVisible; if (_osWidgets.IsVisible) CloseOsLauncher(); }
    public void CloseOsLauncher() { _osLauncher.IsVisible = false; _osMode = ""; }

    public void OpenOsLauncher(string mode)
    {
        if (!OsStyle) { OpenStartMenu(); return; }
        _osWidgets.IsVisible = false; _osMode = mode; _osViews = null;
        _osSearch.Text = ""; Detach(_osSearch); Detach(_osResults);
        var light = OsLight;
        _osSearch.BorderThickness = new Thickness(0); _osSearch.Background = Brushes.Transparent;
        foreach (var k in new[] { "TextControlBackgroundFocused", "TextControlBackgroundPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushPointerOver" }) _osSearch.Resources[k] = Brushes.Transparent; // kein eigener Rahmen im Suchfeld _osSearch.VerticalContentAlignment = VerticalAlignment.Center; _osSearch.Foreground = Text;
        Border SearchBox(double w, double h, double radius, string placeholder, string bg, double font)
        {
            _osSearch.PlaceholderText = placeholder; _osSearch.FontSize = font; _osSearch.Height = h - 2;
            return new Border { Width = w, Height = h, CornerRadius = new CornerRadius(radius), Background = Brush.Parse(bg), BorderBrush = Edge, BorderThickness = new Thickness(1), Padding = new Thickness(10, 0),
                HorizontalAlignment = HorizontalAlignment.Center, Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { new Border { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), Child = Ico(Mdi.Magnify, font, Muted) }, Pos(_osSearch, 1) } } };
        }
        var grid = new WrapPanel { Name = "os-grid", HorizontalAlignment = HorizontalAlignment.Center };
        switch (_style)
        {
            case "windows":
            {
                var all = new Button { Name = "os-all-apps", Content = "Alle  ›", FontSize = 12, Padding = new Thickness(10, 3), CornerRadius = new CornerRadius(4), Background = Brush.Parse(light ? "#B3FFFFFF" : "#0FFFFFFF"), BorderBrush = Edge, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Right };
                all.Click += (_, _) => { _osSearch.Text = " "; };
                var pinnedHead = new Grid { Margin = new Thickness(28, 18, 28, 6), Children = { new TextBlock { Text = "Angeheftet", FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Text }, all } };
                grid.Width = 600; grid.HorizontalAlignment = HorizontalAlignment.Center;
                FillOsGrid(grid, 96, 32, 18, false);
                var recent = new WrapPanel { Width = 600, HorizontalAlignment = HorizontalAlignment.Center };
                foreach (var k in _settings.RecentPages.Take(6))
                {
                    var kk = k; var b = OsTile("os-recent-" + k, IconFor(ModuleOf(k), 28) ?? Ico(Mdi.FileDocumentOutline, 28, Accent), PageTitle(k), () => { CloseOsLauncher(); Show(kk); }, 290, 50, true);
                    recent.Children.Add(b);
                }
                if (recent.Children.Count == 0) recent.Children.Add(new TextBlock { Text = "Hier erscheint, was du zuletzt geöffnet hast.", Foreground = Muted, FontSize = 12, Margin = new Thickness(8) });
                var body = new StackPanel { Children = { pinnedHead, grid, new TextBlock { Text = "Empfohlen", FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Text, Margin = new Thickness(28, 14, 0, 6) }, recent } };
                var user = OsFlat("os-user", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                    new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Accent, Child = new TextBlock { Text = Initial(_settings.Name), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold } },
                    new TextBlock { Text = string.IsNullOrWhiteSpace(_settings.Name) ? "Besitzer" : _settings.Name, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } }, "Konto und Einstellungen", () => { CloseOsLauncher(); Show("settings"); }, double.NaN, 40);
                user.HorizontalAlignment = HorizontalAlignment.Left; user.Padding = new Thickness(10, 0);
                var power = OsFlat("os-power", Ico(Mdi.Power, 16, Text), "Ein/Aus", () => { }, 40, 40);
                var pf = new MenuFlyout(); var re = new MenuItem { Header = "Trendo neu starten" }; re.Click += (_, _) => Reopen("start"); var off = new MenuItem { Header = "Trendo beenden" }; off.Click += (_, _) => Close();
                pf.Items.Add(re); pf.Items.Add(off); power.Flyout = pf; power.HorizontalAlignment = HorizontalAlignment.Right;
                var foot = new Border { Height = 64, Background = Brush.Parse(light ? "#FFEEEEEE" : "#FF1F1F1F"), BorderBrush = Edge, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(40, 0), CornerRadius = new CornerRadius(0, 0, 8, 8),
                    Child = new Grid { Children = { user, power } } };
                var top = SearchBox(580, 36, 18, "Nach Apps, Einstellungen und Dokumenten suchen", light ? "#FFFFFFFF" : "#FF1F1F1F", 13);
                top.Margin = new Thickness(0, 28, 0, 0);
                var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
                var results = new ScrollViewer { Content = _osResults, Margin = new Thickness(28, 12, 28, 0) };
                var main = new ScrollViewer { Content = body };
                _osViews = (main, results);
                Grid.SetRow(results, 1); Grid.SetRow(main, 1);
                content.Children.Add(top); content.Children.Add(main); content.Children.Add(results);
                _osLauncher.Child = new DockPanel { Children = { Bottom(foot), content } };
                _osLauncher.Width = 650; _osLauncher.Height = 700; _osLauncher.CornerRadius = new CornerRadius(8); _osLauncher.Padding = new Thickness(0);
                _osLauncher.Background = Brush.Parse(light ? "#F2F3F3F3" : "#F22C2C2C"); _osLauncher.BorderBrush = Brush.Parse(light ? "#1A000000" : "#33757575"); _osLauncher.BorderThickness = new Thickness(1);
                _osLauncher.BoxShadow = BoxShadows.Parse("0 32 64 0 #47000000"); _osLauncher.HorizontalAlignment = HorizontalAlignment.Center; _osLauncher.VerticalAlignment = VerticalAlignment.Bottom;
                _osLauncher.Margin = new Thickness(0, 0, 0, 60);
                break;
            }
            case "mac" when mode == "spotlight":
            {
                var box = SearchBox(680, 52, 14, "Spotlight-Suche", "#00000000", 22); box.BorderThickness = new Thickness(0);
                _osLauncher.Child = new StackPanel { Children = { box, new ScrollViewer { Content = _osResults, MaxHeight = 420, Margin = new Thickness(6, 0, 6, 6) } } };
                _osLauncher.Width = 680; _osLauncher.Height = double.NaN; _osLauncher.CornerRadius = new CornerRadius(26); _osLauncher.Padding = new Thickness(0);
                _osLauncher.Background = Brush.Parse(light ? "#EBF6F6F6" : "#EB2A2A2A"); _osLauncher.BorderBrush = Brush.Parse(light ? "#26000000" : "#26FFFFFF"); _osLauncher.BorderThickness = new Thickness(1);
                _osLauncher.BoxShadow = BoxShadows.Parse("0 20 60 0 #66000000"); _osLauncher.HorizontalAlignment = HorizontalAlignment.Center; _osLauncher.VerticalAlignment = VerticalAlignment.Top;
                _osLauncher.Margin = new Thickness(0, 180, 0, 0);
                break;
            }
            case "mac": // Launchpad: Vollbild, abgedunkelt, Suchfeld oben, Raster mit großen Symbolen
            {
                var box = SearchBox(240, 28, 7, "Suchen", "#33FFFFFF", 13); box.Margin = new Thickness(0, 40, 0, 40); box.BorderThickness = new Thickness(0);
                _osSearch.Foreground = Brushes.White;
                grid.Width = 7 * 150; FillOsGrid(grid, 150, 72, 400, true, Brushes.White);
                var results = new ScrollViewer { Content = _osResults, Width = 680, HorizontalAlignment = HorizontalAlignment.Center };
                var main = new ScrollViewer { Content = grid };
                _osViews = (main, results);
                _osLauncher.Child = new DockPanel { Children = { Top(box), new Grid { Children = { main, results } } } };
                FullScreen("#B3101014");
                break;
            }
            default: // GNOME-Übersicht: Suche oben, Programmraster, Dash unten
            {
                var box = SearchBox(320, 36, 18, "Zum Suchen tippen", "#26FFFFFF", 14); box.Margin = new Thickness(0, 24, 0, 24); box.BorderThickness = new Thickness(0);
                _osSearch.Foreground = Brushes.White;
                grid.Width = 6 * 160; FillOsGrid(grid, 160, 72, 400, true, Brushes.White);
                var dashRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var a in _dockApps.Take(9)) dashRow.Children.Add(OsAppButton(a, 60, 48, 12, true));
                foreach (var w in Windows.TaskOrder) { var id = w.Id; var ic = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(11), Background = Brush.Parse("#FF36363A"), Child = IconFor(_windowStudio.TryGetValue(w.Id, out var sid) ? sid : ModuleOf(w.Page), 28) ?? Ico(Mdi.Apps, 28, Brushes.White) }; var b = OsFlat("os-task-" + id, ic, w.Title, () => { CloseOsLauncher(); TaskbarClick(id); }, 60, 60, 12); b.Padding = new Thickness(0); dashRow.Children.Add(Dot(b, true)); }
                dashRow.Children.Add(new Border { Width = 1, Height = 44, Background = Brush.Parse("#40FFFFFF"), Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center });
                var appsBtn = OsFlat("os-show-apps", new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(24), Background = Brush.Parse("#33FFFFFF"), Child = Ico(Mdi.ViewGrid, 24, Brushes.White) }, "Anwendungen anzeigen", () => _osSearch.Focus(), 60, 60, 30);
                dashRow.Children.Add(appsBtn);
                var dash = new Border { Name = "os-dash", Background = Brush.Parse("#FF2E2E32"), CornerRadius = new CornerRadius(24), Padding = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 18), Child = dashRow };
                var results = new ScrollViewer { Content = _osResults, Width = 680, HorizontalAlignment = HorizontalAlignment.Center };
                var main = new ScrollViewer { Content = grid };
                _osViews = (main, results);
                _osLauncher.Child = new DockPanel { Children = { Top(box), Bottom(dash), new Grid { Children = { main, results } } } };
                FullScreen("#F21D1D20");
                _osLauncher.Margin = new Thickness(0, 32, 0, 0);
                break;
            }
        }
        _osResults.IsVisible = false; ShowOsViews();
        _osLauncher.IsVisible = true;
        if (mode is "search" or "spotlight") _osSearch.Focus(); else _osSearch.Focus();
        static Control Top(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Top); return c; }
        static Control Bottom(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Bottom); return c; }
        void FullScreen(string bg)
        {
            _osLauncher.Width = double.NaN; _osLauncher.Height = double.NaN; _osLauncher.CornerRadius = new CornerRadius(0); _osLauncher.Padding = new Thickness(0); _osLauncher.BorderThickness = new Thickness(0);
            _osLauncher.Background = Brush.Parse(bg); _osLauncher.BoxShadow = default; _osLauncher.HorizontalAlignment = HorizontalAlignment.Stretch; _osLauncher.VerticalAlignment = VerticalAlignment.Stretch; _osLauncher.Margin = new Thickness(0);
        }
    }

    /// <summary>Alles, was der Launcher zeigt: Trendo-Bereiche (früher linke Spalte), Trendo KI, dann alle Studios.</summary>
    private List<(string Name, string Label, Func<double, Control> Icon, Func<Task> Open)> OsEntries()
    {
        var l = new List<(string, string, Func<double, Control>, Func<Task>)>
        {
            ("os-ki", "Trendo KI", s => Chip(Mdi.StarFourPoints, "#FFE08A", "#C9A227", s, 0.6), () => { Show("ki"); return Task.CompletedTask; }),
        };
        foreach (var (key, label, icon) in SideNav.Where(n => n.Key != "start"))
        {
            var k = key; l.Add(("os-nav-" + key, label, s => Chip(icon, "#5C9DFF", "#283593", s, 0.58), () => { if (k == "studios") { _studioFilter = null; _studioQuery = ""; } Show(k); return Task.CompletedTask; }));
        }
        foreach (var (id, name) in MenuStudios().OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var sid = id; var nm = name;
            l.Add(("os-studio-" + id, name, s => StudioIcons.For(sid) is { } i ? Chip(i.Path, i.C1, i.C2, s, 0.58) : Chip(Mdi.Apps, "#90A4AE", "#455A64", s, 0.58), () => ActivateOrOpenStudio(sid, nm)));
        }
        return l;
    }

    private void FillOsGrid(WrapPanel grid, double tileW, double icon, int max, bool bigLabels, IBrush? labelBrush = null)
    {
        grid.Children.Clear();
        foreach (var (name, label, ic, open) in OsEntries().Take(max))
        {
            var o = open;
            grid.Children.Add(OsTile(name, ic(icon), label, async () => { CloseOsLauncher(); await o(); }, tileW, bigLabels ? icon + 52 : icon + 50, false, labelBrush, bigLabels ? 13 : 11.5));
        }
    }

    static Button OsTile(string name, Control icon, string label, Action click, double w, double h, bool row, IBrush? labelBrush = null, double font = 12)
    {
        var text = new TextBlock { Text = label, FontSize = font, Foreground = labelBrush ?? Text, TextAlignment = row ? TextAlignment.Left : TextAlignment.Center, TextWrapping = row ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = font * 2.7, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = row ? HorizontalAlignment.Left : HorizontalAlignment.Center };
        Control content = row
            ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { icon, text } }
            : new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { new Border { Child = icon, HorizontalAlignment = HorizontalAlignment.Center }, text } };
        var b = OsFlat(name, content, label, click, w, h, 4);
        b.Padding = new Thickness(row ? 10 : 4, 4); b.HorizontalContentAlignment = row ? HorizontalAlignment.Left : HorizontalAlignment.Center; b.VerticalContentAlignment = row ? VerticalAlignment.Center : VerticalAlignment.Top;
        return b;
    }

    /// <summary>Suche: Treffer wie im Trendo-Startmenü, oben immer „Trendo KI fragen“ – Enter öffnet den ersten Treffer.</summary>
    private void FillOsLauncher()
    {
        var q = (_osSearch.Text ?? "").Trim();
        _osResults.Children.Clear();
        _osResults.IsVisible = (_osSearch.Text ?? "").Length > 0; ShowOsViews();
        if (!_osResults.IsVisible) return;
        if (q.Length > 0)
            _osResults.Children.Add(OsTile("os-hit-ki", Chip(Mdi.StarFourPoints, "#FFE08A", "#C9A227", 30, 0.6), $"Trendo KI fragen: „{q}“", () => _ = OsAskKi(q), double.NaN, 48, true, null, 13));
        var entries = OsEntries().Where(e => q.Length == 0 || e.Label.Contains(q, StringComparison.CurrentCultureIgnoreCase)).Take(q.Length == 0 ? 400 : 8).ToList();
        foreach (var (name, label, ic, open) in entries) { var o = open; _osResults.Children.Add(OsTile(name + "-hit", ic(30), label, async () => { CloseOsLauncher(); await o(); }, double.NaN, 46, true, null, 13)); }
        if (q.Length > 0)
            foreach (var h in StartHits(q).Where(h => !entries.Any(e => e.Label == h.Title)).Take(6))
            {
                var hit = h; _osResults.Children.Add(OsTile("os-hit-" + _osResults.Children.Count, Ico(Mdi.Magnify, 22, Muted), h.Title, async () => { CloseOsLauncher(); await OpenStartHit(hit); }, double.NaN, 42, true, null, 13));
            }
        foreach (var c in _osResults.Children.OfType<Button>()) c.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    void ShowOsViews() { if (_osViews is { } v) { v.Main.IsVisible = !_osResults.IsVisible; v.Results.IsVisible = _osResults.IsVisible; } }

    private async Task OsSearchEnter()
    {
        var q = (_osSearch.Text ?? "").Trim(); if (q.Length == 0) return;
        var first = OsEntries().FirstOrDefault(e => e.Label.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        if (first.Open is not null) { CloseOsLauncher(); await first.Open(); return; }
        if (StartHits(q).FirstOrDefault() is { } h) { CloseOsLauncher(); await OpenStartHit(h); return; }
        await OsAskKi(q);
    }

    private async Task OsAskKi(string q) { CloseOsLauncher(); Show("ki"); if (_kiInput.IsEnabled) await AskKi(q); }
}
