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
    private readonly DependencyManager _deps;
    private readonly OllamaClient _ollama = new();
    public IProcessRunner Runner { get => _deps.Runner; set => _deps.Runner = value; }
    private readonly TextBlock _kiStatus = new() { Name = "ki-status", Text = "Nicht verbunden", Foreground = Brush.Parse("#93A4C3"), FontSize = 11 };
    private readonly Ellipse _kiDot = new() { Width = 8, Height = 8, Fill = Brushes.Gray };
    private readonly TextBox _kiInput = new() { Name = "ki-input", PlaceholderText = "KI-Modell noch nicht eingerichtet", IsEnabled = false, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _kiText = new() { Text = "Der Assistent ist noch nicht verbunden. Unter „KI einrichten“ werden die freie lokale Laufzeit Ollama und ein kleines freies Modell automatisch installiert – ohne kostenpflichtige API.", Foreground = Brush.Parse("#E6EDF7"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button _kiSetup = new() { Name = "btn-ki-setup", Content = "KI einrichten", Margin = new Thickness(0, 6, 0, 0) };
    private bool _kiConnected; private int _tick;
    private readonly ProgressBar _depProgress = new() { Name = "deps-progress", IsIndeterminate = false, IsVisible = false, Height = 6, Margin = new Thickness(0, 6) };
    private readonly TextBlock _depLog = new() { Name = "deps-log", Foreground = Brush.Parse("#93A4C3"), FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private bool _depBusy;
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
    private string? _filesProject;
    private string _filesDir = "";
    private string? _filesPreview;
    private string _filesMsg = "";

    public TrendoCore Core => _core;
    public string CurrentView { get; private set; } = "";

    public MainWindow() : this(new AppOptions(null, null, 0)) { }
    public MainWindow(AppOptions opt)
        : this(new TrendoCore(opt.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: opt.Offline), opt, new DefaultToolProbe()) { }

    public MainWindow(TrendoCore core, AppOptions opt, IToolProbe probe)
    {
        _opt = opt; _core = core; _probe = probe;
        _deps = new DependencyManager(core.Audit, new RealProcessRunner()) { ConsentFile = Path.Combine(core.DataRoot, "deps-consent.txt"), ToolsFile = Path.Combine(core.DataRoot, "tools.json") };
        FirstStartDefaults();
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
            ["start"] = DesktopView, ["studios"] = StudiosView, ["projects"] = ProjectsView, ["files"] = FilesView,
            ["approvals"] = ApprovalsView, ["system"] = SystemView, ["apps"] = AppsView, ["ki"] = KiView, ["settings"] = SettingsView, ["components"] = ComponentsView, ["studio-deps"] = StudioDepsView, ["keys"] = KeysView,
        };
        string[] order = ["start", "studios", "projects", "approvals", "system"];
        for (var i = 0; i < order.Length; i++)
        {
            var key = order[i];
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D1 + i, KeyModifiers.Control), Command = new RelayCommand(() => Show(key)) });
        }
        Show(opt.Page ?? "start");
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
        if (_tick++ % 5 == 0) _ = RefreshKi();
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
        nav.Children.Add(SideItem("side-dateien", "🗂", "Dateien", () => Show("files")));
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
            t.Click += async (_, _) => await OpenStudio(id, title);
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
        var kiState = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { _kiDot, _kiStatus } };
        Grid.SetColumn(kiState, 1); kiHead.Children.Add(kiState);
        _kiSetup.Click += (_, _) => Show("components");
        _kiInput.KeyDown += async (_, e) => { if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(_kiInput.Text)) await AskKi(_kiInput.Text!); };
        sp.Children.Add(Panel(new StackPanel { Children = { kiHead,
            new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 0), MaxHeight = 160,
                Child = new ScrollViewer { Content = _kiText } },
            _kiSetup, _kiInput } }));
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
            new("files", "📁", "Dateien", () => null),
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
                    case "files": Show("files"); break;
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
            var filesBtn = new Button { Name = "btn-project-files-" + p.Id, Content = "Dateien" };
            var pid = p.Id;
            filesBtn.Click += (_, _) => { _filesProject = pid; _filesDir = ""; _filesPreview = null; _filesMsg = ""; Show("files"); };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12),
                Child = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = p.Name, Foreground = Text }, P($"Angelegt {p.CreatedAt:dd.MM.yyyy HH:mm}"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { filesBtn, open } } } } });
        }
        var url = new Button { Name = "btn-open-url", Content = "Trendo-Repository im Browser öffnen (Freigabe nötig)", IsEnabled = !_core.Offline };
        if (_core.Offline) ToolTip.SetTip(url, "Offline-Modus: deaktiviert");
        url.Click += (_, _) => { try { _core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://github.com/manuelbrandner85/Trendo-OS"); Show("approvals"); } catch (OfflineException ex) { msg.Text = ex.Message; } };
        sp.Children.Add(url);
        return Page("Projekte", sp);
    }

    private Control FilesView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var projects = _core.Projects.List();
        if (projects.Count == 0)
        {
            sp.Children.Add(P("Noch kein Projekt. Dateien liegen nur innerhalb eines Projekts."));
            var go = new Button { Name = "btn-files-new-project", Content = "Projekt anlegen" };
            go.Click += (_, _) => Show("projects");
            sp.Children.Add(go);
            return Page("Dateien", sp);
        }
        if (_filesProject is null || projects.All(p => p.Id != _filesProject))
            _filesProject = projects[0].Id;

        var pick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var p in projects)
        {
            var id = p.Id;
            var b = new Button { Name = "btn-files-project-" + id, Content = p.Name, Background = id == _filesProject ? Accent : Brushes.Transparent };
            b.Click += (_, _) => { _filesProject = id; _filesDir = ""; _filesPreview = null; _filesMsg = ""; Show("files"); };
            pick.Children.Add(b);
        }
        sp.Children.Add(pick);
        sp.Children.Add(P(string.IsNullOrEmpty(_filesDir) ? _filesProject! : _filesProject + " / " + _filesDir, Text));

        if (!string.IsNullOrEmpty(_filesDir))
        {
            var up = new Button { Name = "btn-files-up", Content = "Eine Ebene höher" };
            up.Click += (_, _) =>
            {
                var t = _filesDir.Replace('\\', '/').Trim('/');
                var i = t.LastIndexOf('/');
                _filesDir = i < 0 ? "" : t[..i];
                _filesPreview = null;
                Show("files");
            };
            sp.Children.Add(up);
        }

        IReadOnlyList<ProjectStore.ProjectNode> nodes = [];
        try { nodes = _core.Projects.List(_filesProject!, _filesDir); }
        catch (Exception ex)
        {
            _filesMsg = ex.Message;
            if (_filesDir.Length > 0) { _filesDir = ""; nodes = _core.Projects.List(_filesProject!, ""); }
        }

        if (nodes.Count == 0) sp.Children.Add(P("Dieser Ordner ist leer."));
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            var rel = n.RelativePath;
            var isDir = n.IsDirectory;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var open = new Button { Name = "btn-files-item-" + i, Content = (isDir ? "📁 " : "📄 ") + n.Name };
            open.Click += (_, _) =>
            {
                try
                {
                    if (isDir) { _filesDir = rel; _filesPreview = null; }
                    else _filesPreview = _core.Projects.ReadPreview(_filesProject!, rel);
                    _filesMsg = "";
                }
                catch (Exception ex) { _filesMsg = ex.Message; }
                Show("files");
            };
            var del = new Button { Name = "btn-files-del-" + i, Content = "Löschen" };
            del.Click += (_, _) =>
            {
                try { _core.Projects.Delete(_filesProject!, rel); _filesPreview = null; _filesMsg = ""; }
                catch (Exception ex) { _filesMsg = ex.Message; }
                Show("files");
            };
            row.Children.Add(open);
            row.Children.Add(del);
            if (!isDir) row.Children.Add(P(n.Bytes + " B", null, 11));
            sp.Children.Add(row);
        }

        var folderName = new TextBox { Name = "txt-files-folder", PlaceholderText = "neuer-ordner", Width = 200 };
        var mkdir = new Button { Name = "btn-files-mkdir", Content = "Ordner anlegen" };
        mkdir.Click += (_, _) =>
        {
            try
            {
                var name = (folderName.Text ?? "").Trim();
                if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name is "." or "..")
                    _filesMsg = "Ordnername ohne Pfadtrenner angeben.";
                else
                {
                    var rel = string.IsNullOrEmpty(_filesDir) ? name : _filesDir.TrimEnd('/') + "/" + name;
                    _core.Projects.CreateFolder(_filesProject!, rel);
                    _filesMsg = "";
                }
            }
            catch (Exception ex) { _filesMsg = ex.Message; }
            Show("files");
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { folderName, mkdir } });

        var fileName = new TextBox { Name = "txt-files-new", PlaceholderText = "notiz.txt", Width = 200 };
        var mk = new Button { Name = "btn-files-new", Content = "Textdatei anlegen" };
        mk.Click += (_, _) =>
        {
            try
            {
                var name = (fileName.Text ?? "").Trim();
                if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name is "." or "..")
                    _filesMsg = "Dateiname ohne Pfadtrenner angeben.";
                else
                {
                    var rel = string.IsNullOrEmpty(_filesDir) ? name : _filesDir.TrimEnd('/') + "/" + name;
                    _core.Projects.WriteFile(_filesProject!, rel, "");
                    _filesMsg = "";
                }
            }
            catch (Exception ex) { _filesMsg = ex.Message; }
            Show("files");
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { fileName, mk } });

        var exp = new Button { Name = "btn-files-explorer", Content = "Projektordner im Explorer öffnen (Freigabe nötig)" };
        var current = _filesProject!;
        exp.Click += (_, _) => { _core.Integrations.PlanOpenProjectFolder(Principal.LocalOwner, current); Show("approvals"); };
        sp.Children.Add(exp);
        if (!string.IsNullOrEmpty(_filesMsg)) sp.Children.Add(new TextBlock { Name = "txt-files-msg", Text = _filesMsg, Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap });
        if (_filesPreview is not null)
            sp.Children.Add(new TextBox { Name = "txt-files-preview", Text = _filesPreview, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 });
        return Page("Dateien", sp);
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
        foreach (var c in DependencyCatalog.All.Where(c => c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Guided))
            sp.Children.Add(P($"{c.Name}: {(_deps.IsInstalled(c) ? "vorhanden" : "nicht gefunden")}", Text));
        sp.Children.Add(Nav2("Komponenten verwalten", "components"));
        return Page("Apps", sp);
    }

    private Control KiView() => Page("KI", new StackPanel { Spacing = 6, Children = {
        P(_kiConnected ? $"Verbunden mit lokaler Ollama-Laufzeit (127.0.0.1:11434), Modell {DependencyCatalog.LocalModel}. Keine Cloud, keine kostenpflichtige API." : "Trendo KI ist nicht verbunden. Lokale Laufzeit und freies Modell lassen sich unter „Komponenten“ automatisch installieren.", Text),
        Nav2("Komponenten öffnen", "components") } });

    private Button Nav2(string label, string view) { var b = new Button { Content = label }; b.Click += (_, _) => Show(view); return b; }

    private void FirstStartDefaults()
    {
        var cfg = Path.Combine(_core.DataRoot, "config.json");
        if (!File.Exists(cfg))
        {
            File.WriteAllText(cfg, "{\n  \"language\": \"de-DE\",\n  \"theme\": \"trendo-dark-glass\",\n  \"ki\": { \"runtime\": \"ollama\", \"endpoint\": \"http://127.0.0.1:11434\", \"model\": \"" + DependencyCatalog.LocalModel + "\" }\n}\n");
            _core.Audit.Append("app", "app.first_start", "Standardkonfiguration angelegt");
        }
        try { var t = new Trendo.Api.ApiTokens(_core.Secrets); foreach (var r in new[] { Role.Guest, Role.User, Role.Owner }) t.GetOrCreate(r); }
        catch (Exception ex) { _core.Audit.Append("app", "app.tokens_failed", ex.GetType().Name); }
    }

    private async Task RefreshKi()
    {
        var ok = await _ollama.HasModelAsync(DependencyCatalog.LocalModel);
        if (ok == _kiConnected && _kiStatus.Text != "…") return;
        _kiConnected = ok;
        _kiStatus.Text = ok ? "Verbunden (lokal)" : "Nicht verbunden";
        _kiDot.Fill = ok ? Brush.Parse("#4ADE80") : Brushes.Gray;
        _kiInput.IsEnabled = ok; _kiSetup.IsVisible = !ok;
        _kiInput.PlaceholderText = ok ? "Frag Trendo KI … (Enter)" : "KI-Modell noch nicht eingerichtet";
        if (ok) _kiText.Text = $"Lokales Modell {DependencyCatalog.LocalModel} bereit. Antworten werden auf diesem PC erzeugt.";
    }

    private async Task AskKi(string q)
    {
        _kiInput.Text = ""; _kiInput.IsEnabled = false; _kiText.Text = $"Du: {q}\n\nTrendo KI denkt …";
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.ask", $"{q.Length} Zeichen");
        var a = await _ollama.ChatAsync(DependencyCatalog.LocalModel, q);
        _kiText.Text = $"Du: {q}\n\nTrendo KI: {a}"; _kiInput.IsEnabled = true;
    }

    // ---------- Komponenten: Zustimmung, Auto-Installation, geführte Installation ----------
    private Control ComponentsView()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(P("Alles, was im Setup enthalten ist, ist sofort nutzbar. Weitere freie Programme installiert Trendo OS nach einmaliger Zustimmung automatisch pro Benutzer (ohne Admin). Nicht weitergebbare Software kann nur über den offiziellen Herstellerweg installiert werden.", Text));
        var consent = new Button { Name = "btn-deps-consent", Content = _deps.HasConsent ? "Fehlende freie Komponenten installieren (ohne Admin)" : "Lizenzen zustimmen & fehlende freie Komponenten installieren (ohne Admin)", IsEnabled = !_depBusy, Background = Accent };
        consent.Click += async (_, _) => await InstallAll();
        (_depProgress.Parent as Panel)?.Children.Remove(_depProgress); (_depLog.Parent as Panel)?.Children.Remove(_depLog);
        sp.Children.Add(consent); sp.Children.Add(_depProgress); sp.Children.Add(_depLog);
        foreach (var grp in DependencyCatalog.All.GroupBy(c => c.Mode))
        {
            sp.Children.Add(H(grp.Key switch
            {
                DeliveryMode.Bundled => "Im Setup enthalten", DeliveryMode.AutoUser => "Automatisch pro Benutzer (ohne Admin)",
                DeliveryMode.AutoElevated => "Automatisch, Windows fragt nach Admin-Rechten", DeliveryMode.Guided => "Nur offizieller Herstellerweg (geführt)", _ => "Nicht erforderlich",
            }, 13));
            foreach (var c in grp)
            {
                var inst = _deps.IsInstalled(c);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 2) };
                row.Children.Add(new StackPanel { Children = {
                    new TextBlock { Text = $"{c.Name}  ·  {c.License}", Foreground = Text, FontWeight = FontWeight.SemiBold },
                    P($"{c.Purpose} – {c.Reason}{(c.ApproxMb > 0 ? $" · ca. {c.ApproxMb} MB" : "")}", null, 11) } });
                Control right;
                if (c.Mode is DeliveryMode.Bundled or DeliveryMode.NotNeeded) right = P(c.Mode == DeliveryMode.Bundled ? "enthalten" : "—", Brush.Parse("#4ADE80"));
                else if (inst) right = P("✓ erkannt", Brush.Parse("#4ADE80"));
                else if (c.Mode == DeliveryMode.Guided)
                {
                    var b = new Button { Name = "btn-guide-" + c.Id, Content = "Installieren (offiziell)", IsEnabled = !_core.Offline && c.OfficialUrl is not null };
                    b.Click += (_, _) => { _core.Integrations.LaunchDirect(Principal.LocalOwner, c.OfficialUrl!, _core.Audit); _depLog.Text = $"{c.Name}: offizielle Seite geöffnet. Nach der Installation hier neu prüfen."; };
                    right = b;
                }
                else
                {
                    var b = new Button { Name = "btn-install-" + c.Id, Content = c.Mode == DeliveryMode.AutoElevated ? "Installieren (Admin)" : "Installieren", IsEnabled = !_depBusy && !_core.Offline };
                    b.Click += async (_, _) => await InstallOne(c);
                    right = b;
                }
                Grid.SetColumn(right, 1); row.Children.Add(right); sp.Children.Add(row);
            }
        }
        var re = new Button { Name = "btn-deps-recheck", Content = "Neu prüfen" }; re.Click += (_, _) => { ReloadStudios(); Show("components"); };
        sp.Children.Add(re);
        return Page("Komponenten", sp);
    }

    // ---------- Studio-Klick: erkennen → prüfen → einmal fragen → installieren ----------
    private string _depStudio = "", _depStudioTitle = "";
    private List<ToolCheck> _depChecks = [];
    public Func<string, Task<bool>>? KeyValidatorOverride { get; set; }

    public async Task OpenStudio(string id, string title)
    {
        _studioFilter = id;
        if (!StudioTools.Required.TryGetValue(id, out var need)) { Show("studios"); return; }
        _depStudio = id; _depStudioTitle = title; _depChecks = [];
        _depLog.Text = $"{title}: prüfe benötigte Programme …"; Show("studio-deps");
        foreach (var t in need) _depChecks.Add(await _deps.CheckAsync(DependencyCatalog.Get(t)));
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.check", $"{id}: " + string.Join(", ", _depChecks.Select(c => $"{c.Component.Id}={c.State}")));
        _depLog.Text = _depChecks.All(c => c.Ok) ? $"{title}: alle benötigten Programme gefunden und funktionsfähig." : "";
        if (_depChecks.All(c => c.Ok)) { ReloadStudios(); Show("studios"); } else Show("studio-deps");
    }

    private Control StudioDepsView()
    {
        var sp = new StackPanel { Spacing = 8 };
        (_depProgress.Parent as Panel)?.Children.Remove(_depProgress); (_depLog.Parent as Panel)?.Children.Remove(_depLog);
        foreach (var c in _depChecks)
        {
            var comp = c.Component;
            var line = c.State switch
            {
                ToolState.Working => $"✓ {comp.Name} – {c.Detail}{(c.Version is { } v ? $" ({v})" : "")}",
                ToolState.Broken => $"⚠ {comp.Name} ist installiert, startet aber nicht: {c.Detail}",
                _ => $"✗ {comp.Name} fehlt",
            };
            sp.Children.Add(P(line, Text, 13));
            if (c.Ok) continue;
            var ask = new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12) };
            var row = new StackPanel { Spacing = 6 };
            row.Children.Add(new TextBlock { Name = "ask-" + comp.Id, Text = $"{_depStudioTitle} braucht {comp.Name}. Jetzt installieren?", Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(P($"Lizenz: {comp.License} · {comp.Reason}{(comp.ApproxMb > 0 ? $" · ca. {comp.ApproxMb} MB" : "")}", null, 11));
            var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (comp.Mode == DeliveryMode.Guided)
            {
                var g = new Button { Name = "btn-guide-" + comp.Id, Content = "Offiziellen Installer öffnen", IsEnabled = !_core.Offline };
                g.Click += (_, _) => { _core.Integrations.LaunchDirect(Principal.LocalOwner, comp.OfficialUrl!, _core.Audit); _depLog.Text = $"{comp.Name}: Herstellerseite geöffnet. Nach der Installation das Studio erneut öffnen – der Pfad wird dann automatisch übernommen."; };
                btns.Children.Add(g);
            }
            else
            {
                var y = new Button { Name = "btn-yes-" + comp.Id, Content = comp.Mode == DeliveryMode.AutoElevated ? "Ja, installieren (Windows fragt nach Admin)" : "Ja, installieren", Background = Accent, IsEnabled = !_depBusy && !_core.Offline };
                y.Click += async (_, _) =>
                {
                    // Freigabe protokolliert über das Approval-Gate (einmalig, zielgebunden)
                    var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", $"{comp.Name} für {_depStudioTitle} installieren", comp.Id);
                    _core.Approvals.Approve(req.Id, Principal.LocalOwner);
                    if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
                    if (comp.Id == "model" && !_deps.IsInstalled(DependencyCatalog.Get("ollama"))) await InstallOne(DependencyCatalog.Get("ollama"), refresh: false);
                    await InstallOne(comp, refresh: false);
                    await OpenStudio(_depStudio, _depStudioTitle);
                };
                btns.Children.Add(y);
            }
            var later = new Button { Name = "btn-later-" + comp.Id, Content = "Später" };
            later.Click += (_, _) => { _core.Audit.Append(Principal.LocalOwner.Name, "deps.declined", comp.Id); Show("studios"); };
            btns.Children.Add(later);
            row.Children.Add(btns); ask.Child = row; sp.Children.Add(ask);
        }
        sp.Children.Add(_depProgress); sp.Children.Add(_depLog);
        return Page(_depStudioTitle, sp);
    }

    // ---------- API-Schlüssel (optional, nie Pflicht) ----------
    private static readonly (string Id, string Name, string KeyUrl, string ValidateUrl, string Note)[] KeyServices =
    [
        ("xai", "xAI API (optional, kostenpflichtig)", "https://console.x.ai", "https://api.x.ai/v1/models", "Optional – Trendo KI läuft lokal ohne Schlüssel."),
        ("github", "GitHub (optional)", "https://github.com/settings/tokens", "https://api.github.com/user", "Für Web & App Studio. GitHub bietet zusätzlich ein offizielles Geräte-Login (OAuth Device Flow) – noch nicht eingebaut."),
    ];

    private Control KeysView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Schlüssel werden nur im Windows-Anmeldeinformationsspeicher abgelegt und vor dem Speichern live geprüft. Konten und Schlüssel kann Trendo OS nicht selbst anlegen (Registrierung, Nutzungsbedingungen, 2FA erfordern dich).", Text) } };
        foreach (var k in KeyServices)
        {
            var has = _core.Secrets.Get("TrendoOS:key-" + k.Id) is not null;
            var box = new TextBox { Name = "txt-key-" + k.Id, PasswordChar = '•', PlaceholderText = "Schlüssel hier einfügen", Width = 320 };
            var res = new TextBlock { Name = "key-state-" + k.Id, Text = has ? "gespeichert ✓" : "kein Schlüssel", Foreground = Muted, FontSize = 11 };
            var get = new Button { Name = "btn-getkey-" + k.Id, Content = "Schlüssel holen", IsEnabled = !_core.Offline };
            get.Click += (_, _) => _core.Integrations.LaunchDirect(Principal.LocalOwner, k.KeyUrl, _core.Audit);
            var save = new Button { Name = "btn-savekey-" + k.Id, Content = "Prüfen & speichern", IsEnabled = !_core.Offline };
            save.Click += async (_, _) =>
            {
                var key = box.Text?.Trim() ?? ""; if (key.Length == 0) return;
                res.Text = "prüfe …";
                var ok = KeyValidatorOverride is { } f ? await f(key) : await ValidateKey(k.ValidateUrl, key);
                if (ok) { _core.Secrets.Set("TrendoOS:key-" + k.Id, key); box.Text = ""; }
                _core.Audit.Append(Principal.LocalOwner.Name, ok ? "key.saved" : "key.invalid", k.Id); // Schlüssel selbst nie im Log
                res.Text = ok ? "gültig und gespeichert ✓" : "ungültig – nicht gespeichert";
            };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = k.Name, Foreground = Text, FontWeight = FontWeight.SemiBold }, P(k.Note, null, 11),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { get, box, save } }, res } } });
        }
        return Page("Dienste & Schlüssel", sp);
    }

    private static async Task<bool> ValidateKey(string url, string key)
    {
        try
        {
            using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var rq = new HttpRequestMessage(HttpMethod.Get, url);
            rq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            rq.Headers.UserAgent.ParseAdd("TrendoOS/0.5");
            using var r = await h.SendAsync(rq);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task InstallAll()
    {
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        foreach (var c in DependencyCatalog.All.Where(c => c.Mode == DeliveryMode.AutoUser && !_deps.IsInstalled(c)).ToList())
            if (!await InstallOne(c, refresh: false)) break;
        ReloadStudios(); Show("components");
    }

    private async Task<bool> InstallOne(Component c, bool refresh = true)
    {
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        _depBusy = true; _depProgress.IsVisible = true; _depProgress.IsIndeterminate = true;
        _depLog.Text = $"Installiere {c.Name} …";
        bool ok;
        try
        {
            ok = await Task.Run(() => _deps.InstallAsync(Principal.LocalOwner, c, line =>
                Dispatcher.UIThread.Post(() => { if (!string.IsNullOrWhiteSpace(line)) _depLog.Text = $"{c.Name}: {line.Trim()}"; })));
            _depLog.Text = ok ? $"{c.Name}: installiert und erkannt ✓" : $"{c.Name}: Installation nicht erfolgreich (siehe Audit-Log)";
        }
        catch (Exception ex) { ok = false; _depLog.Text = $"{c.Name}: {ex.Message}"; }
        _depBusy = false; _depProgress.IsIndeterminate = false; _depProgress.IsVisible = false;
        if (refresh) { ReloadStudios(); Show("components"); }
        _ = RefreshKi();
        return ok;
    }

    private void ReloadStudios()
    {
        var req = TrendoCore.FindRequirements(AppContext.BaseDirectory);
        if (req is not null) { _studios = _core.Studios.Load(req, _probe); }
    }

    private Control SettingsView() => Page("Einstellungen", new StackPanel { Spacing = 6, Children = {
        P($"Version {_core.Version}", Text), P(_core.Offline ? "Offline-Modus aktiv" : "Online-Modus", Text),
        P("Logo: Konzeptvariante, nicht freigegeben. Hintergrundbild: KI-generiert für Trendo OS."),
        P("Dock-Anpassung und Themes: noch nicht gebaut."), Nav2("Komponenten", "components"), Nav2("Dienste & Schlüssel", "keys") } });
}

internal sealed class RelayCommand(Action run) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => run();
}
