using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Trendo.Core;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Trendo-OS-Desktop nach Designkonzept (docs/design). Zeigt ausschließlich echte Zustände und Messwerte.</summary>
public sealed class MainWindow : Window
{
    // Farben: dunkles Blau, Glas-Panels, kühle Akzente
    private static readonly IBrush Glass = new SolidColorBrush(Color.Parse("#B30B1730"));
    private static readonly IBrush GlassStrong = new SolidColorBrush(Color.Parse("#D90A1428"));
    private static readonly IBrush TileBg = new SolidColorBrush(Color.Parse("#99142445"));
    private static readonly IBrush Edge = new SolidColorBrush(Color.Parse("#3355A8FF"));
    private static readonly IBrush Accent = Brush.Parse("#3BA7FF");
    private static readonly IBrush Muted = Brush.Parse("#9DB0CF");
    private static readonly IBrush Text = Brush.Parse("#EEF3FB");
    private static readonly CultureInfo De = new("de-DE");

    private readonly TrendoCore _core;
    private readonly AppOptions _opt;
    private readonly IToolProbe _probe;
    private readonly SystemMetrics _metrics = new();
    private readonly Grid _center = new();
    private readonly TextBlock _clock = new() { Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _date = new() { Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _status = new() { Foreground = Muted, FontSize = 11 };
    private readonly TextBlock _bellBadge = new() { Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeight.Bold };
    private readonly StackPanel _projectsPanel = new() { Spacing = 6 };
    private readonly Dictionary<string, (Arc Arc, TextBlock Value)> _rings = new();
    private readonly TextBox _search = new() { Name = "txt-search", PlaceholderText = "Suchen in Trendo OS …", Width = 220, Height = 30 };
    private IReadOnlyList<StudioEntry> _studios = [];
    private Trendo.Api.LocalApi? _api;
    private Dictionary<string, Func<Control>> _showMap = new();
    private string? _studioFilter;

    public TrendoCore Core => _core;
    public string CurrentView { get; private set; } = "";

    public MainWindow() : this(new AppOptions(null, null, 0)) { }
    public MainWindow(AppOptions opt)
        : this(new TrendoCore(opt.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: opt.Offline), opt, new DefaultToolProbe()) { }

    public MainWindow(TrendoCore core, AppOptions opt, IToolProbe probe)
    {
        _opt = opt; _core = core; _probe = probe;
        Title = "Trendo OS";
        Width = 1440; Height = 900; MinWidth = 960; MinHeight = 600; WindowState = WindowState.Maximized;
        Background = Brush.Parse("#050B18");

        var req = TrendoCore.FindRequirements(AppContext.BaseDirectory);
        _studios = req is null ? [] : _core.Studios.Load(req, probe);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
        root.Children.Add(Wallpaper()); Grid.SetRowSpan(root.Children[^1], 4);
        var top = TopBar(); root.Children.Add(top);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*,320"), Margin = new Thickness(10, 6, 10, 0) };
        Grid.SetRow(body, 1); root.Children.Add(body);
        var left = LeftColumn(); body.Children.Add(left);
        Grid.SetColumn(_center, 1); _center.Margin = new Thickness(10, 0); body.Children.Add(_center);
        var right = RightColumn(); Grid.SetColumn(right, 2); body.Children.Add(right);
        var dock = Dock(); Grid.SetRow(dock, 2); root.Children.Add(dock);
        var status = new Border { Background = GlassStrong, Padding = new Thickness(12, 4), Child = _status }; Grid.SetRow(status, 3); root.Children.Add(status);
        // Designfläche skaliert proportional (Konzept ist für ~1440 px Breite gestaltet), damit kleine Bildschirme nicht abschneiden
        var scaler = new LayoutTransformControl { Child = root };
        Content = scaler;
        void Rescale()
        {
            var cs = ClientSize; if (cs.Width <= 0 || cs.Height <= 0) return;
            var k = Math.Min(1.0, Math.Min(cs.Width / 1440.0, cs.Height / 860.0));
            scaler.LayoutTransform = new ScaleTransform(k, k);
            root.Width = cs.Width / k; root.Height = cs.Height / k;
        }
        PropertyChanged += (_, e) => { if (e.Property == ClientSizeProperty) Rescale(); };
        Rescale();

        _showMap = new()
        {
            ["start"] = DesktopView, ["studios"] = StudiosView, ["projects"] = ProjectsView,
            ["approvals"] = ApprovalsView, ["system"] = SystemView, ["apps"] = AppsView, ["ki"] = KiView, ["settings"] = SettingsView,
        };
        string[] order = ["start", "studios", "projects", "approvals", "system"];
        for (var i = 0; i < order.Length; i++)
        {
            var key = order[i];
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D1 + i, KeyModifiers.Control), Command = new RelayCommand(() => Show(key)) });
        }
        Show("start");
        RefreshStatusBar();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => Tick();
        timer.Start(); Tick();

        if (opt.ApiPort is { } port)
        {
            var reqPath = req;
            _ = Task.Run(async () =>
            {
                try
                {
                    _api = await Trendo.Api.LocalApi.StartAsync(_core, new Trendo.Api.ApiTokens(_core.Secrets), reqPath ?? "", probe, port);
                    Dispatcher.UIThread.Post(() => { _apiInfo = $" · API: 127.0.0.1:{_api.Port}"; RefreshStatusBar(); });
                }
                catch (Exception ex) { Dispatcher.UIThread.Post(() => { _apiInfo = $" · API-Fehler: {ex.Message}"; RefreshStatusBar(); }); }
            });
        }
        Opened += OnOpened;
        Closing += (_, _) => { _api?.DisposeAsync().AsTask().Wait(2000); _core.Shutdown(); };
    }

    private string _apiInfo = "";
    private void RefreshStatusBar() =>
        _status.Text = $"Core {_core.Version}{(_core.Offline ? " · OFFLINE" : "")} · Secrets: {_core.Secrets.BackendName} · Audit-Kette: {(_core.Audit.Verify() ? "intakt" : "FEHLER")}{_apiInfo} · Logo: Konzept, nicht freigegeben · Hintergrund: KI-generiert";

    public void Show(string key)
    {
        CurrentView = key;
        _center.Children.Clear();
        _center.Children.Add(_showMap[key]());
        _bellBadge.Text = _core.Approvals.Pending().Count is var n && n > 0 ? n.ToString() : "";
        RefreshProjects();
    }

    private void Tick()
    {
        var now = DateTime.Now;
        _clock.Text = now.ToString("HH:mm", De);
        _date.Text = now.ToString("ddd, d. MMM yyyy", De);
        var m = _metrics.Sample();
        SetRing("CPU", m.CpuPercent); SetRing("RAM", m.RamPercent); SetRing("SSD", m.DiskPercent); SetRing("GPU", m.GpuPercent);
    }

    private void SetRing(string k, double? v)
    {
        if (!_rings.TryGetValue(k, out var r)) return;
        r.Arc.SweepAngle = v is { } x ? 360 * x / 100 : 0;
        r.Value.Text = v is { } y ? $"{y:0}%" : "n. v.";
    }

    private async void OnOpened(object? s, EventArgs e)
    {
        if (_opt.ScreenshotPath is { } path)
        {
            await Task.Delay(2500);
            await Dispatcher.UIThread.InvokeAsync(() => SaveScreenshot(path));
            _core.Audit.Append("app", "app.screenshot", Path.GetFileName(path));
        }
        if (_opt.AutoExitSeconds > 0) { await Task.Delay(TimeSpan.FromSeconds(_opt.AutoExitSeconds)); Close(); }
    }

    private void SaveScreenshot(string path)
    {
        var scale = RenderScaling;
        var size = new PixelSize((int)(Bounds.Width * scale), (int)(Bounds.Height * scale));
        using var bmp = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bmp.Render(this);
        bmp.Save(path, new PngBitmapEncoderOptions());
    }

    // ---------- Bausteine ----------
    private static Border Panel(Control c, double pad = 12) => new()
    { Background = Glass, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(pad), Child = c, Margin = new Thickness(0, 0, 0, 10) };
    private static TextBlock H(string t, double size = 15) => new() { Text = t, Foreground = Text, FontSize = size, FontWeight = FontWeight.SemiBold };
    private static TextBlock P(string t, IBrush? c = null, double size = 12) => new() { Text = t, Foreground = c ?? Muted, FontSize = size, TextWrapping = TextWrapping.Wrap };
    private static Button Flat(string name, Control content, string? automation = null)
    {
        var b = new Button { Name = name, Content = content, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 5), IsTabStop = true };
        if (automation is not null) Avalonia.Automation.AutomationProperties.SetName(b, automation);
        return b;
    }
    private static TextBlock Glyph(string glyph, double size = 18) => new() { Text = glyph, FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Segoe UI Emoji, Noto Color Emoji, Segoe UI Symbol, sans-serif") };

    private static Control Logo(double size)
    {
        // Vektor-Nachbau des V/T-Konzeptsymbols (NICHT freigegeben)
        var g = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#7FD3FF"), 0), new GradientStop(Color.Parse("#1D6BFF"), 1) } };
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M0,0 L100,0 L50,90 Z M22,12 L50,62 L60,44 L44,12 Z M56,12 L78,12 L64,37 Z"),
            Fill = g, Stretch = Stretch.Uniform, Width = size, Height = size * 0.9,
        };
        ToolTip.SetTip(path, "Logo-Konzept – nicht freigegeben");
        return path;
    }

    private static Control Wallpaper()
    {
        var grid = new Grid();
        try
        {
            using var s = AssetLoader.Open(new Uri("avares://TrendoOS/Assets/wallpaper.jpg"));
            grid.Children.Add(new Image { Source = new Bitmap(s), Stretch = Stretch.UniformToFill });
        }
        catch { /* ohne Hintergrundbild weiter */ }
        grid.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#99050B18"), 0), new GradientStop(Color.Parse("#33050B18"), 0.45), new GradientStop(Color.Parse("#CC050B18"), 1) } } });
        return grid;
    }

    private Control TopBar()
    {
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Background = GlassStrong, Height = 52 };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(14, 0), VerticalAlignment = VerticalAlignment.Center,
            Children = { Logo(26), new TextBlock { Text = "Trendo OS", Foreground = Text, FontSize = 18, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center } } };
        bar.Children.Add(brand);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0) };
        foreach (var (key, name, glyph, label) in new[] {
                     ("start", "nav-start", "🖥", "Desktop"), ("studios", "nav-studios", "▦", "Studios"), ("apps", "nav-apps", "⬚", "Apps"),
                     ("projects", "nav-projects", "📁", "Projekte"), ("ki", "nav-ki", "✦", "KI"), ("system", "nav-system", "⚙", "System"), ("settings", "nav-settings", "☰", "Einstellungen") })
        {
            var b = Flat(name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Glyph(glyph, 13), new TextBlock { Text = label, Foreground = Text, FontSize = 13 } } }, label);
            b.Click += (_, _) => Show(key);
            tabs.Children.Add(b);
        }
        Grid.SetColumn(tabs, 1); bar.Children.Add(tabs);
        _search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _studioFilter = _search.Text; Show("studios"); } };
        var bell = Flat("nav-approvals", new Grid { Children = { Glyph("🔔", 16), new Border { Background = Brushes.Crimson, CornerRadius = new CornerRadius(7), Padding = new Thickness(4, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -4, -8, 0), Child = _bellBadge } } }, "Freigaben");
        ToolTip.SetTip(bell, "Freigaben (Strg+4)");
        bell.Click += (_, _) => Show("approvals");
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), Children = { _search, bell } };
        Grid.SetColumn(tools, 2); bar.Children.Add(tools);
        var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0), Children = { _clock, _date } };
        Grid.SetColumn(clock, 3); bar.Children.Add(clock);
        return bar;
    }

    private static readonly (string Label, string Glyph, string Filter)[] Categories =
    [
        ("Alle Studios", "▦", ""), ("Online Business", "🌐", "STUDIO-ONLINE"), ("3D & Games", "🎮", "CORE-05|CORE-06"), ("Kreativ & Medien", "🎨", "CORE-08|SPEC-15"),
        ("Office & Dokumente", "📄", "SYS-10"), ("Briefstudio", "✉", "STUDIO-LETTER"), ("Kommunikation", "💬", "SYS-09"), ("Tools & Entwickler", "🛠", "CORE-07|X-06|X-03"),
        ("System & Sicherheit", "🛡", "CORE-11|SYS-13|SPEC-12"), ("KI Assistent", "✦", "CORE-02|CORE-03|CORE-12"),
    ];

    private Control LeftColumn()
    {
        var nav = new StackPanel { Spacing = 1 };
        var start = SideItem("side-start", "⌂", "Start", () => Show("start")); nav.Children.Add(start);
        foreach (var (label, glyph, filter) in Categories)
            nav.Children.Add(SideItem("side-" + label.ToLowerInvariant().Replace(' ', '-').Replace("&", "und"), glyph, label, () => { _studioFilter = filter; Show("studios"); }));
        nav.Children.Add(SideItem("side-dateien", "🗂", "Dateien", () => Show("projects")));
        nav.Children.Add(SideItem("side-einstellungen", "⚙", "Einstellungen", () => Show("settings")));
        var quick = new StackPanel { Spacing = 1, Children = { H("Schnellzugriff", 13) } };
        quick.Children.Add(SideItem("quick-new-project", "＋", "Neues Projekt", () => Show("projects")));
        quick.Children.Add(SideItem("quick-approvals", "✔", "Offene Freigaben", () => Show("approvals")));
        quick.Children.Add(SideItem("quick-system-check", "🩺", "System-Check", () => Show("system")));
        return new StackPanel { Children = { Panel(nav, 6), Panel(quick, 8) } };
    }

    private static Button SideItem(string name, string glyph, string label, Action onClick)
    {
        var b = Flat(name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { Glyph(glyph, 14), new TextBlock { Text = label, Foreground = Text, FontSize = 13 } } }, label);
        b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left;
        b.Click += (_, _) => onClick();
        return b;
    }

    // ---------- Desktop (Startansicht) ----------
    private static readonly (string Id, string Title, string Sub, string Glyph)[] FeaturedStudios =
    [
        ("STUDIO-ONLINE-BUSINESS-01", "Online Business", "Creation & Innovation", "🚀"),
        ("STUDIO-LETTER-01", "Letter, Business & Legal", "Correspondence", "📝"),
        ("CORE-05", "3D Studio", "Blender & Assets", "🧊"),
        ("CORE-06", "Game Studio", "Unreal Engine 5", "🎮"),
        ("SYS-10", "Office Studio", "Dokumente & Tabellen", "📄"),
        ("CORE-02", "KI Studio", "Trendo Assistent", "🧠"),
        ("CORE-08", "Medien Studio", "Video, Audio, Grafik", "🎬"),
        ("CORE-07", "Web & App Studio", "Full-Stack Entwicklung", "</>"),
        ("CORE-11", "Security Studio", "Schutz & Privatsphäre", "🛡"),
    ];

    private Control DesktopView()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,*") };
        // Studio-Kacheln 3x3
        var tiles = new UniformGrid { Columns = 3, Rows = 3 };
        foreach (var (id, title, sub, glyph) in FeaturedStudios)
        {
            var st = _studios.FirstOrDefault(s => s.Id == id);
            var badge = Badge(st);
            var t = new Button
            {
                Name = "tile-" + id, Width = 150, Height = 112, Margin = new Thickness(4), Padding = new Thickness(6), Background = TileBg,
                BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), HorizontalContentAlignment = HorizontalAlignment.Center,
                Content = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    Glyph(glyph, 26), new TextBlock { Text = title, Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = sub, Foreground = Muted, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center }, badge } }
            };
            Avalonia.Automation.AutomationProperties.SetName(t, $"{title}: {badge.Text}");
            ToolTip.SetTip(t, st?.Reason ?? "Status unbekannt");
            t.Click += (_, _) => { _studioFilter = id; Show("studios"); };
            tiles.Children.Add(t);
        }
        var all = new Button { Name = "btn-all-studios", Content = "Alle anzeigen", FontSize = 11, Padding = new Thickness(10, 3), HorizontalAlignment = HorizontalAlignment.Right };
        all.Click += (_, _) => { _studioFilter = null; Show("studios"); };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(4, 0, 4, 8), Children = { H("Trendo Studios") } };
        Grid.SetColumn(all, 1); head.Children.Add(all);
        var studiosPanel = Panel(new StackPanel { Children = { head, tiles } });
        studiosPanel.VerticalAlignment = VerticalAlignment.Top;
        g.Children.Add(studiosPanel);

        // Hero
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 0) };
        foreach (var (glyph, label, key) in new[] { ("✏", "Erstellen", "projects"), ("</>", "Entwickeln", "studios"), ("🔍", "Gestalten", "studios"), ("🗓", "Organisieren", "projects"), ("⚙", "Automatisieren", "approvals") })
        {
            var b = new Button { Name = "action-" + label.ToLowerInvariant(), Width = 92, Height = 74, Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                HorizontalContentAlignment = HorizontalAlignment.Center, Content = new StackPanel { Spacing = 4, Children = { Glyph(glyph, 20), new TextBlock { Text = label, Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
            if (label is "Gestalten") b.Click += (_, _) => { _studioFilter = "CORE-08|CORE-05"; Show("studios"); };
            else if (label is "Entwickeln") b.Click += (_, _) => { _studioFilter = "CORE-07|CORE-06"; Show("studios"); };
            else b.Click += (_, _) => Show(key);
            actions.Children.Add(b);
        }
        var hero = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 40, 0, 0),
            Children = {
                new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = Logo(120) },
                new TextBlock { Text = "Trendo OS", Foreground = Text, FontSize = 56, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "DEIN SYSTEM. DEINE IDEEN. KEINE GRENZEN.", Foreground = Brush.Parse("#BFD6F5"), FontSize = 14, LetterSpacing = 2, HorizontalAlignment = HorizontalAlignment.Center },
                actions,
            }
        };
        Grid.SetColumn(hero, 1); Grid.SetRowSpan(hero, 2); g.Children.Add(hero);
        return g;
    }

    private static TextBlock Badge(StudioEntry? st)
    {
        var (txt, col) = st?.Capability switch
        {
            Capability.Ready => ("Bereit", "#4ADE80"), Capability.NotConnected => ("Nicht verbunden", "#FACC15"),
            Capability.Blocked => ("Blockiert", "#F87171"), Capability.Rejected => ("Abgelehnt", "#9CA3AF"),
            Capability.SpecOnly => ("Noch nicht gebaut", "#93A4C3"), _ => ("Status unbekannt", "#9CA3AF"),
        };
        return new TextBlock { Text = txt, Foreground = Brush.Parse(col), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
    }

    // ---------- Rechte Spalte ----------
    private Control RightColumn()
    {
        var sp = new StackPanel();
        // KI-Assistent – ehrlich: nicht verbunden
        var kiHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Trendo KI Assistent", 13) } };
        var kiState = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { new Ellipse { Width = 8, Height = 8, Fill = Brushes.Gray }, new TextBlock { Name = "ki-status", Text = "Nicht verbunden", Foreground = Muted, FontSize = 11 } } };
        Grid.SetColumn(kiState, 1); kiHead.Children.Add(kiState);
        var kiInput = new TextBox { Name = "ki-input", PlaceholderText = "KI-Modell noch nicht eingerichtet", IsEnabled = false, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(Panel(new StackPanel { Children = { kiHead,
            new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 0),
                Child = P("Der Assistent ist noch nicht verbunden. Sobald ein lokales Modell eingerichtet ist, kannst du hier Aufgaben geben, Studios öffnen oder Projekte starten.", Text) },
            kiInput } }));
        // Systemstatus – echte Messwerte
        var rings = new UniformGrid { Columns = 4, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var k in new[] { "CPU", "RAM", "SSD", "GPU" }) rings.Children.Add(Ring(k));
        sp.Children.Add(Panel(new StackPanel { Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("System Status", 13), Pos(new TextBlock { Text = "Live gemessen", Foreground = Brush.Parse("#4ADE80"), FontSize = 11 }, 1) } }, rings } }));
        // Aktuelle Projekte – echt
        var allP = new Button { Content = "Alle anzeigen", FontSize = 11, Padding = new Thickness(8, 2) }; allP.Click += (_, _) => Show("projects");
        sp.Children.Add(Panel(new StackPanel { Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Aktuelle Projekte", 13), Pos(allP, 1) } }, new Border { Height = 6 }, _projectsPanel } }));
        // Schnell-Tools
        var tools = new UniformGrid { Columns = 4, Margin = new Thickness(0, 8, 0, 0) };
        var shot = ToolButton("tool-screenshot", "📷", "Screenshot", true);
        shot.Click += (_, _) =>
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Trendo");
            Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, $"trendo-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            SaveScreenshot(f); _core.Audit.Append(Principal.LocalOwner.Name, "tool.screenshot", Path.GetFileName(f));
        };
        tools.Children.Add(shot);
        tools.Children.Add(ToolButton("tool-aufnahme", "⏺", "Aufnahme", false));
        tools.Children.Add(ToolButton("tool-notiz", "🗒", "Notiz", false));
        tools.Children.Add(ToolButton("tool-uebersetzen", "🌐", "Übersetzen", false));
        sp.Children.Add(Panel(new StackPanel { Children = { H("Schnell-Tools", 13), tools } }));
        return new ScrollViewer { Content = sp };
    }

    private static Control Pos(Control c, int col) { Grid.SetColumn(c, col); return c; }

    private static Button ToolButton(string name, string glyph, string label, bool enabled)
    {
        var b = new Button { Name = name, IsEnabled = enabled, Background = TileBg, Margin = new Thickness(3), Padding = new Thickness(4, 6), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new StackPanel { Spacing = 2, Children = { Glyph(glyph, 16), new TextBlock { Text = label, Foreground = Text, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center } } } };
        ToolTip.SetTip(b, enabled ? label : $"{label}: noch nicht gebaut");
        Avalonia.Automation.AutomationProperties.SetName(b, enabled ? label : $"{label} (noch nicht gebaut)");
        return b;
    }

    private Control Ring(string key)
    {
        var bg = new Ellipse { Width = 54, Height = 54, Stroke = Brush.Parse("#22FFFFFF"), StrokeThickness = 5 };
        var arc = new Arc { Width = 54, Height = 54, Stroke = Accent, StrokeThickness = 5, StartAngle = -90, SweepAngle = 0, StrokeLineCap = PenLineCap.Round };
        var val = new TextBlock { Name = "metric-" + key.ToLowerInvariant(), Text = "…", Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _rings[key] = (arc, val);
        var g = new Grid { Width = 58, Height = 58, Children = { bg, arc, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = key, Foreground = Muted, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center }, val } } } };
        return g;
    }

    private void RefreshProjects()
    {
        _projectsPanel.Children.Clear();
        var list = _core.Projects.List().OrderByDescending(p => p.CreatedAt).Take(4).ToList();
        if (list.Count == 0)
        {
            var b = new Button { Name = "btn-first-project", Content = "＋ Erstes Projekt anlegen", FontSize = 11 };
            b.Click += (_, _) => Show("projects");
            _projectsPanel.Children.Add(P("Noch keine Projekte."));
            _projectsPanel.Children.Add(b);
            return;
        }
        foreach (var p in list)
            _projectsPanel.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6),
                Child = new StackPanel { Children = { new TextBlock { Text = p.Name, Foreground = Text, FontSize = 12 }, P($"Angelegt {p.CreatedAt:dd.MM.yyyy HH:mm}", null, 10) } } });
    }

    // ---------- Dock ----------
    private sealed record DockApp(string Id, string Glyph, string Label, Func<string?> Target);

    private Control Dock()
    {
        var apps = new List<DockApp>
        {
            new("files", "📁", "Dateien", () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            new("browser", "🌐", "Browser", () => _core.Offline ? null : "https://github.com/manuelbrandner85/Trendo-OS"),
            new("studios", "▦", "Studios", () => null),
            new("projects", "🗂", "Projekte", () => null),
            new("settings", "⚙", "Einstellungen", () => null),
        };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var a in apps)
        {
            var b = new Button { Name = "dock-" + a.Id, Width = 52, Height = 46, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = Glyph(a.Glyph, 24) };
            ToolTip.SetTip(b, a.Label); Avalonia.Automation.AutomationProperties.SetName(b, a.Label);
            b.Click += (_, _) =>
            {
                switch (a.Id)
                {
                    case "studios": _studioFilter = null; Show("studios"); break;
                    case "projects": Show("projects"); break;
                    case "settings": Show("settings"); break;
                    default:
                        if (a.Target() is { } t) _core.Integrations.LaunchDirect(Principal.LocalOwner, t, _core.Audit);
                        break;
                }
            };
            if (a.Id == "browser" && _core.Offline) { b.IsEnabled = false; ToolTip.SetTip(b, "Browser – offline deaktiviert"); }
            bar.Children.Add(b);
        }
        return new Border { Background = GlassStrong, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 4),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 8), Child = bar };
    }

    // ---------- Inhaltsansichten (im Glaspanel) ----------
    private Control Page(string title, Control content)
    {
        var sp = new StackPanel { Spacing = 8, Children = { H(title, 22), content } };
        return Panel(new ScrollViewer { Content = sp }, 18);
    }

    private Control StudiosView()
    {
        var wrap = new WrapPanel();
        var f = _studioFilter;
        var list = string.IsNullOrWhiteSpace(f) ? _studios
            : _studios.Where(s => f.Split('|').Any(x => s.Id.StartsWith(x, StringComparison.OrdinalIgnoreCase)) || s.Name.Contains(f, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var st in list)
            wrap.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 10, 10), Width = 230,
                Child = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = st.Name, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, P($"{st.Id} · {st.ApprovalStatus}"), Badge(st), P(st.Reason, null, 11) } } });
        var info = P(string.IsNullOrWhiteSpace(f) ? $"{list.Count} Studios/Module" : $"Filter „{f}“: {list.Count} Treffer");
        if (_studios.Count == 0) info = P("requirements.yaml nicht gefunden – Status unbekannt.", Brushes.OrangeRed);
        return Page("Alle Studios", new StackPanel { Spacing = 8, Children = { info, wrap } });
    }

    private Control ProjectsView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var id = new TextBox { Name = "txt-project-id", PlaceholderText = "projekt-id (a-z, 0-9, -)", Width = 260 };
        var create = new Button { Name = "btn-create-project", Content = "Projekt anlegen" };
        var msg = P(""); msg.Name = "txt-project-msg";
        create.Click += (_, _) =>
        {
            try { _core.Projects.Create(id.Text ?? "", id.Text ?? ""); Show("projects"); }
            catch (Exception ex) { msg.Text = ex.Message; }
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { id, create } });
        sp.Children.Add(msg);
        foreach (var p in _core.Projects.List())
        {
            var open = new Button { Name = "btn-open-folder-" + p.Id, Content = "Ordner im Explorer öffnen (Freigabe nötig)" };
            open.Click += (_, _) => { _core.Integrations.PlanOpenProjectFolder(Principal.LocalOwner, p.Id); Show("approvals"); };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12),
                Child = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = p.Name, Foreground = Text }, P($"Angelegt {p.CreatedAt:dd.MM.yyyy HH:mm}"), open } } });
        }
        var url = new Button { Name = "btn-open-url", Content = "Trendo-Repository im Browser öffnen (Freigabe nötig)", IsEnabled = !_core.Offline };
        if (_core.Offline) ToolTip.SetTip(url, "Offline-Modus: deaktiviert");
        url.Click += (_, _) => { try { _core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://github.com/manuelbrandner85/Trendo-OS"); Show("approvals"); } catch (OfflineException ex) { msg.Text = ex.Message; } };
        sp.Children.Add(url);
        return Page("Projekte", sp);
    }

    private Control ApprovalsView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var pending = _core.Approvals.Pending();
        if (pending.Count == 0) sp.Children.Add(P("Keine offenen Freigaben."));
        foreach (var r in pending)
        {
            var yes = new Button { Name = "btn-approve-" + r.Id, Content = "Freigeben und ausführen", Background = Accent };
            var no = new Button { Name = "btn-deny-" + r.Id, Content = "Ablehnen" };
            yes.Click += (_, _) => { _core.Approvals.Approve(r.Id, Principal.LocalOwner); _core.Integrations.Execute(r); Show("approvals"); };
            no.Click += (_, _) => { _core.Approvals.Deny(r.Id, Principal.LocalOwner); Show("approvals"); };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = r.Description, Foreground = Text }, P($"Aktion {r.Action} · gültig bis {r.ExpiresAt:HH:mm}"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { yes, no } } } } });
        }
        return Page("Freigaben", sp);
    }

    private Control SystemView()
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Datenordner: lokal im Benutzerprofil (TrendoOS)"));
        sp.Children.Add(P($"Secrets-Backend: {_core.Secrets.BackendName}"));
        sp.Children.Add(P($"Audit-Log: {_core.Audit.ReadAll().Count} Einträge, Hash-Kette {(_core.Audit.Verify() ? "intakt" : "BESCHÄDIGT")}"));
        foreach (var e in _core.Audit.ReadAll().TakeLast(15).Reverse())
            sp.Children.Add(P($"{e.At:HH:mm:ss}  {e.Actor}  {e.Action}  {e.Detail}", null, 11));
        return Page("System & Sicherheit", sp);
    }

    private Control AppsView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Nur tatsächlich gefundene Programme werden als vorhanden angezeigt.") } };
        foreach (var (tool, label) in new[] { ("browser", "Browser"), ("libreoffice", "LibreOffice"), ("blender", "Blender"), ("unreal", "Unreal Engine") })
            sp.Children.Add(P($"{label}: {(_probe.IsAvailable(tool) ? "vorhanden" : "nicht gefunden")}", Text));
        return Page("Apps", sp);
    }

    private Control KiView() => Page("KI", P("Trendo KI ist noch nicht verbunden. Es ist kein lokales Modell eingerichtet; es wird keine kostenpflichtige KI-API vorausgesetzt.", Text));

    private Control SettingsView() => Page("Einstellungen", new StackPanel { Spacing = 6, Children = {
        P($"Version {_core.Version}", Text), P(_core.Offline ? "Offline-Modus aktiv" : "Online-Modus", Text),
        P("Logo: Konzeptvariante, nicht freigegeben. Hintergrundbild: KI-generiert für Trendo OS."),
        P("Dock-Anpassung und Themes: noch nicht gebaut.") } });
}

internal sealed class RelayCommand(Action run) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => run();
}
