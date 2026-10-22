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
public sealed partial class MainWindow : Window
{
    // Farben: dunkles Blau, Glas-Panels, kühle Akzente
    private static IBrush Glass = new SolidColorBrush(Color.Parse("#B30B1730"));
    private static IBrush GlassStrong = new SolidColorBrush(Color.Parse("#D90A1428"));
    private static IBrush TileBg = new SolidColorBrush(Color.Parse("#99142445"));
    private static IBrush Edge = new SolidColorBrush(Color.Parse("#3355A8FF"));
    private static IBrush Accent = Brush.Parse("#3BA7FF");
    private static IBrush Muted = Brush.Parse("#9DB0CF");
    private static IBrush Text = Brush.Parse("#EEF3FB");
    private static readonly CultureInfo De = new("de-DE");

    private readonly TrendoCore _core;
    private readonly AppOptions _opt;
    private readonly IToolProbe _probe;
    private readonly DependencyManager _deps;
    private readonly OllamaClient _ollama = new();
    public IProcessRunner Runner { get => _deps.Runner; set => _deps.Runner = value; }
    private readonly TextBlock _kiStatus = new() { Name = "ki-status", Text = "Nicht verbunden", Foreground = Brush.Parse("#93A4C3"), FontSize = 11 };
    private readonly Ellipse _kiDot = new() { Width = 8, Height = 8, Fill = Brushes.Gray };
    private readonly TextBox _kiInput = new() { Name = "ki-input", PlaceholderText = "Deine Nachricht an Trendo …", IsEnabled = false, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _kiText = new() { Text = "Hallo! Ich bin noch nicht verbunden. Mit „KI einrichten“ installierst du die kostenlose lokale KI – danach kannst du mir Aufgaben geben, Studios öffnen oder Fragen stellen.", Foreground = Brush.Parse(C("#E6EDF7", "#1B2A44")), FontSize = 12, TextWrapping = TextWrapping.Wrap };
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
    private readonly TextBlock _bellBadge = new() { Foreground = _theme == "hell" ? Brush.Parse("#0F1B2E") : Brushes.White, FontSize = 10, FontWeight = FontWeight.Bold };
    private readonly StackPanel _projectsPanel = new() { Spacing = 6 };
    private readonly Dictionary<string, (Arc Arc, TextBlock Value)> _rings = new();
    private readonly TextBox _search = new() { Name = "txt-search", PlaceholderText = "Suchen in Trendo OS …", Width = 220, Height = 30 };
    private IReadOnlyList<StudioEntry> _studios = [];
    public IReadOnlyList<StudioEntry> Studios => _studios;
    private Trendo.Api.LocalApi? _api;
    private Dictionary<string, Func<Control>> _showMap = new();
    private string? _studioFilter;

    public TrendoCore Core => _core;
    public string CurrentView { get; private set; } = "";
    public DependencyManager Deps => _deps;

    public MainWindow() : this(new AppOptions(null, null, 0)) { }
    public MainWindow(AppOptions opt)
        : this(new TrendoCore(opt.DataRoot ?? TrendoCore.DefaultDataRoot(), offline: opt.Offline), opt, new DefaultToolProbe()) { }

    public MainWindow(TrendoCore core, AppOptions opt, IToolProbe probe)
    {
        StartupTrace.Mark("window.ctor");
        _opt = opt; _core = core; _probe = probe;
        // Isolated: nichts außerhalb des Datenordners erkennen (Tests sind unabhängig von installierten Programmen/laufendem Ollama)
        Func<string, bool>? exists = opt.Isolated ? p => Path.GetFullPath(p).StartsWith(Path.GetFullPath(core.DataRoot), StringComparison.OrdinalIgnoreCase) && File.Exists(p) : null;
        _deps = new DependencyManager(core.Audit, new RealProcessRunner(), fileExists: exists) { ConsentFile = Path.Combine(core.DataRoot, "deps-consent.txt"), ToolsFile = Path.Combine(core.DataRoot, "tools.json") };
        _deps.OllamaUp = opt.Isolated ? () => Task.FromResult(false) : _ollama.IsUpAsync;
        StartupTrace.Mark("ctor.deps");
        _settings = UserSettings.Load(core.DataRoot); ApplyTheme(_settings);
        // Felder wurden vor ApplyTheme erzeugt: Schriftfarben nachziehen
        _clock.Foreground = Text; _kiText.Foreground = Brush.Parse(C("#E6EDF7", "#1B2A44")); _kiStatus.Foreground = Muted; _depLog.Foreground = Muted;
        FirstStartDefaults();
        Title = "Trendo OS";
        try { using var ico = Avalonia.Platform.AssetLoader.Open(new Uri("avares://TrendoOS/Assets/logo/trendo.ico")); Icon = new WindowIcon(ico); } catch (Exception) { /* Symbol fehlt: Standard */ }
        Width = 1440; Height = 900; MinWidth = 960; MinHeight = 600;
        Background = Brush.Parse("#050B18");

        StartupTrace.Mark("ctor.theme");
        var req = TrendoCore.FindRequirements(AppContext.BaseDirectory);
        _studios = req is null ? [] : StudioCatalog.Friendly(_core.Studios.Load(req, probe));
        StartupTrace.Mark("studios.loaded");

        // Raster nach Konzept (Designfläche 1536 × 800): Topbar | Seitenleiste 192 · Mitte · rechte Spalte 322, Dock unter der Mitte
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        root.Children.Add(Wallpaper()); Grid.SetRowSpan(root.Children[^1], 2);
        var ui = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        // Echter Hintergrund-Blur: unscharfe Kopie des Wallpapers, sichtbar nur dort, wo Glas-Panels liegen (Maske = Alpha der Oberfläche)
        if (BlurredWallpaper(ui) is { } blur) { root.Children.Add(blur); Grid.SetRowSpan(blur, 2); }
        Grid.SetRowSpan(ui, 2); root.Children.Add(ui);
        StartupTrace.Mark("ctor.wallpaper");
        var top = TopBar(); ui.Children.Add(top);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("192,*,322"), RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(8, 10, 8, 4) };
        Grid.SetRow(body, 1); ui.Children.Add(body);
        var left = LeftColumn(); Grid.SetRowSpan(left, 2); body.Children.Add(left);
        Grid.SetColumn(_center, 1); _center.Margin = new Thickness(12, 0, 12, 0); body.Children.Add(_center);
        var dock = Dock(); Grid.SetColumn(dock, 1); Grid.SetRow(dock, 1); body.Children.Add(dock);
        StartupTrace.Mark("ctor.left+dock");
        var right = RightColumn(); Grid.SetColumn(right, 2); Grid.SetRowSpan(right, 2); body.Children.Add(right);
        Grid.SetColumnSpan(_workArea, 3); Grid.SetRowSpan(_workArea, 2); _workArea.ZIndex = 30; body.Children.Add(_workArea);
        _status.IsVisible = false; // Statusinfos stehen unter System/Einstellungen und als Tooltip
        // Designfläche skaliert proportional (Konzept ist für ~1440 px Breite gestaltet), damit kleine Bildschirme nicht abschneiden
        var scaler = new LayoutTransformControl { Child = root };
        Content = scaler;
        void DoRescale()
        {
            var cs = ClientSize; if (cs.Width <= 0 || cs.Height <= 0) return;
            var k = Math.Min(1.0, Math.Min(cs.Width / 1536.0, cs.Height / 800.0)) * UserSettings.ClampScale(_settings.UiScale);
            scaler.LayoutTransform = new ScaleTransform(k, k);
            root.Width = cs.Width / k; root.Height = cs.Height / k;
        }
        Rescale = DoRescale;
        PropertyChanged += (_, e) => { if (e.Property == ClientSizeProperty) DoRescale(); };
        DoRescale();

        _showMap = new()
        {
            ["start"] = DesktopView, ["studios"] = StudiosView, ["projects"] = ProjectsView, ["arbeitsplatz"] = WorkspaceView,
            ["approvals"] = ApprovalsView, ["system"] = SystemView, ["apps"] = AppsView, ["ki"] = KiView, ["settings"] = SettingsView, ["components"] = ComponentsView, ["studio-deps"] = StudioDepsView, ["keys"] = KeysView, ["notbuilt"] = NotBuiltView, ["fortschritt"] = ProgressView, ["suche"] = SearchView, ["werkzeuge"] = ToolsView, ["medien"] = MediaView, ["ressourcen"] = ResourcesView, ["office"] = OfficeView, ["brief"] = LetterView, ["akten"] = CasesView, ["akte"] = CaseView, ["ausgabe"] = OutputView, ["briefkoepfe"] = LetterheadsView, ["downloads"] = DownloadsView,
        };
        string[] order = ["start", "studios", "projects", "approvals", "system"];
        for (var i = 0; i < order.Length; i++)
        {
            var key = order[i];
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D1 + i, KeyModifiers.Control), Command = new RelayCommand(() => Show(key)) });
        }
        StartupTrace.Mark("ctor.layout");
        InitWave1(); InitWave45(); InitWave23(); InitW7(); StartupTrace.Mark("ctor.initwaves"); _query = opt.Query ?? "";
        InitSetup(opt);
        Show(opt.Page ?? (NeedsSetup(opt) ? "einrichtung" : "start"));
        StartupTrace.Mark("ctor.show");
        RefreshStatusBar();
        StartupTrace.Mark("ctor.statusbar");

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => Tick();
        // Uhr sofort; erste Messung (CPU/GPU-Zähler, ~65 ms) erst nach dem ersten Bild
        _clock.Text = DateTime.Now.ToString("HH:mm", De); _date.Text = DateTime.Now.ToString("ddd, d. MMM yyyy", De);
        timer.Start(); Dispatcher.UIThread.Post(Tick, DispatcherPriority.Background); StartupTrace.Mark("ctor.tick");

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
        Closing += (_, _) => { if (_reopening) return; _api?.DisposeAsync().AsTask().Wait(2000); _core.Shutdown(); };
        InitLook(root, opt);
        StartupTrace.Mark("window.ready");
    }

    private string _apiInfo = "";
    private void RefreshStatusBar() =>
        _status.Text = $"Core {_core.Version}{(_core.Offline ? " · OFFLINE" : "")} · Secrets: {_core.Secrets.BackendName} · Audit-Kette: {(_core.Audit.Verify() ? "intakt" : "FEHLER")}{_apiInfo} · Hintergrund: KI-generiert";

    public void Show(string key)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        CurrentView = key;
        MarkTab(key); MarkSide(key);
        PresentPage(key);
        NoteRecent(key);
        AfterShow(key);
        _bellBadge.Text = _core.Approvals.Pending().Count is var n && n > 0 ? n.ToString() : "";
        RefreshProjects();
        StartupTrace.Took("page." + key, sw.Elapsed.TotalMilliseconds);
    }

    private void Tick()
    {
        var now = DateTime.Now;
        _clock.Text = now.ToString("HH:mm", De);
        _date.Text = now.ToString("ddd, d. MMM yyyy", De);
        if (_tick++ % 5 == 0) _ = RefreshKi();
        var m = _metrics.Sample();
        SetRing("CPU", m.CpuPercent); SetRing("RAM", m.RamPercent); SetRing("SSD", m.DiskPercent); SetRing("GPU", m.GpuPercent);
        // Gesamtzustand aus echten Messwerten (keine Mockup-Angabe)
        var vals = new[] { m.CpuPercent, m.RamPercent, m.DiskPercent, m.GpuPercent }.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var high = vals.Count > 0 && vals.Max() >= 90;
        _sysState.Text = vals.Count == 0 ? "Messung läuft …" : high ? "Hohe Auslastung" : "Alles in Ordnung";
        _sysDot.Fill = _sysState.Foreground = Brush.Parse(high ? "#FACC15" : "#4ADE80");
    }

    private void SetRing(string k, double? v)
    {
        if (!_rings.TryGetValue(k, out var r)) return;
        r.Arc.SweepAngle = v is { } x ? 360 * x / 100 : 0;
        r.Value.Text = v is { } y ? $"{y:0}%" : "n. v.";
    }

    /// <summary>Hauptfenster nach vorn holen. Velopack startet den Prozess nach der Installation oft ohne sichtbares Fenster.</summary>
    private bool _revealed;
    public void Reveal()
    {
        var hidden = !IsVisible || WindowState == WindowState.Minimized;
        if (!hidden && _revealed) return;
        var first = !_revealed;
        _revealed = true;
        ShowInTaskbar = true;
        if (!IsVisible) Show();
        if (hidden || first)
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            WindowReveal.Force(this);
        }
        Activate();
    }

    private async void OnOpened(object? s, EventArgs e)
    {
        StartupTrace.Mark("window.opened");
        Reveal();
        Dispatcher.UIThread.Post(Reveal, DispatcherPriority.Loaded);
        // erstes gezeichnetes Bild = sichtbarer Start
        RequestAnimationFrame(_ => { StartupTrace.Mark("first.frame"); StartupTrace.Flush(); });
        // KI-Laufzeit im Hintergrund prüfen/starten – blockiert weder Start noch Bildschirmfoto
        var ollama = EnsureOllamaRunning();
        if (_opt.KiAsk is { } q)
        {
            await ollama;
            await RefreshKi();
            if (_kiConnected) await AskKi(q);
        }
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
        // Effekte (Schatten/Glow) werden bei DPI-Skalierung im Offscreen-Render falsch zwischengespeichert → logische Auflösung.
        var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
        bmp.Render(this);
        bmp.Save(path, new PngBitmapEncoderOptions());
    }

    // ======================= Design-Bausteine (nach Konzept) =======================
    private static IBrush Grad(string a, string b) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse(a), 0), new GradientStop(Color.Parse(b), 1) },
    };

    /// <summary>Vektor-Icon (Material Design Icons).</summary>
    private static Control Ico(string data, double size, IBrush fill, string? glow = null)
    {
        var p = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Fill = fill, Stretch = Stretch.Uniform, Width = size, Height = size };
        if (glow is not null) p.Effect = new DropShadowEffect { Color = Color.Parse(glow), BlurRadius = size * 0.6, OffsetX = 0, OffsetY = 0, Opacity = 0.9 };
        return p;
    }

    /// <summary>Farbiges, abgerundetes Quadrat mit weißem Symbol (Seitenleiste, Schnellzugriff, Projekte, Dock).</summary>
    private static Border Chip(string data, string c1, string c2, double size = 26, double icon = 0.58, IBrush? fg = null) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.26), Background = Grad(c1, c2),
        Child = Ico(data, size * icon, fg ?? Brushes.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Kartenkopf „Alle Studios“: Studio-Symbol (zentrale Karte StudioIcons) + Name.</summary>
    private Control CardHead(string id, string name)
    {
        var t = new TextBlock { Text = name, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        if (StudioIcons.For(id) is not { } si) return t;
        var chip = Chip(si.Path, si.C1, si.C2, 38, 0.6); chip.Name = "studio-icon-" + id; chip.Margin = new Thickness(0, 0, 10, 4);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { chip, t } };
        Grid.SetColumn(t, 1); return g;
    }

    private static Border Panel(Control c, double pad = 12) => new()
    {
        Background = Glass, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(pad), Child = c, Margin = new Thickness(0, 0, 0, 10),
        BoxShadow = BoxShadows.Parse("0 8 24 0 #66000814"),
    };
    private static TextBlock H(string t, double size = 15) => new() { Text = t, Foreground = Text, FontSize = size, FontWeight = FontWeight.SemiBold };
    private static TextBlock P(string t, IBrush? c = null, double size = 12) => new() { Text = t, Foreground = c ?? Muted, FontSize = size, TextWrapping = TextWrapping.Wrap };
    private static Button Flat(string name, Control content, string? automation = null)
    {
        var b = new Button { Name = name, Content = content, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 5), IsTabStop = true, CornerRadius = new CornerRadius(8) };
        if (automation is not null) Avalonia.Automation.AutomationProperties.SetName(b, automation);
        return b;
    }
    private static Button SmallOutline(string name, string label)
    {
        var b = new Button { Name = name, Padding = new Thickness(10, 3), FontSize = 11, Foreground = Text, Background = Brush.Parse("#33102A55"), BorderBrush = Brush.Parse("#664C8DFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Ico(Mdi.ChevronRight, 10, Accent), new TextBlock { Text = label, FontSize = 11, Foreground = Text } } } };
        return b;
    }
    private static TextBlock Glyph(string glyph, double size = 18) => new() { Text = glyph, FontSize = size, HorizontalAlignment = HorizontalAlignment.Center };

    /// <summary>Trendo-Logo A „Obsidian-Kachel“ (Vektor-Nachbau von Assets/logo/trendo-logo-a.svg, viewBox 64×64).</summary>
    internal static Control Logo(double size, bool glow = false)
    {
        static LinearGradientBrush Lg(string a, string b, bool diag) => new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(diag ? 1 : 0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse(a), 0), new GradientStop(Color.Parse(b), 1) },
        };
        static Border R(double x, double y, double w, double h, double r, IBrush fill, IBrush? stroke = null, double st = 0)
        {
            var e = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(r), Background = fill, BorderBrush = stroke, BorderThickness = new Thickness(st) };
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y); return e;
        }
        var canvas = new Canvas { Width = 64, Height = 64, Children =
        {
            R(1, 1, 62, 62, 16, Lg("#1B3150", "#060D1A", true), Brush.Parse("#335578"), 2),
            R(13, 14, 38, 10, 5, Brush.Parse("#F0F6FF")),
            R(27, 14, 10, 36, 5, Lg("#7FD8FF", "#2A7FFF", false)),
        } };
        var box = new Viewbox { Name = "trendo-logo", Width = size, Height = size, Stretch = Stretch.Uniform, Child = canvas };
        if (glow) box.Effect = new DropShadowEffect { Color = Color.Parse("#2E8BFF"), BlurRadius = size * 0.35, OffsetX = 0, OffsetY = 0, Opacity = 0.8 };
        ToolTip.SetTip(box, "Trendo-Logo");
        return box;
    }

    private Control? BlurredWallpaper(Visual mask)
    {
        if (_settings.Theme == "kontrast") return null;
        try
        {
            var img = _blurImg; img.Source = LoadWallpaper(_wallName); img.Stretch = Stretch.UniformToFill; img.HorizontalAlignment = HorizontalAlignment.Center; img.VerticalAlignment = VerticalAlignment.Center; img.Effect = new BlurEffect { Radius = 36 };
            var brush = new VisualBrush { Visual = mask, Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, TileMode = TileMode.None };
            // Quelle/Ziel absolut auf die Fläche der Oberfläche festlegen – sonst verschiebt überstehender Inhalt (Glow) die Maske
            void Fit() { var b = mask.Bounds; if (b.Width > 0) brush.SourceRect = brush.DestinationRect = new RelativeRect(0, 0, b.Width, b.Height, RelativeUnit.Absolute); }
            mask.PropertyChanged += (_, e) => { if (e.Property == BoundsProperty) Fit(); };
            return new Border { Name = "backdrop-blur", ClipToBounds = true, IsHitTestVisible = false, Child = img, OpacityMask = brush };
        }
        catch { return null; }
    }

    private Control Wallpaper()
    {
        var grid = new Grid();
        _wallImg.Source = LoadWallpaper(_wallName = _settings.CurrentWallpaper(DateTime.Now));
        _wallImg.Stretch = Stretch.UniformToFill; _wallImg.HorizontalAlignment = HorizontalAlignment.Center; _wallImg.VerticalAlignment = VerticalAlignment.Center;
        _wallImg.IsVisible = _settings.Theme != "kontrast";
        grid.Children.Add(_wallImg);
        grid.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#33040A18"), 0), new GradientStop(Color.Parse("#00040A18"), 0.35), new GradientStop(Color.Parse("#10040A18"), 0.7), new GradientStop(Color.Parse("#66040A18"), 1) } } });
        return grid;
    }

    // ---------- Topbar ----------
    private Control TopBar()
    {
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Height = 52,
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
            b.Click += (_, _) => Show(key);
            tabs.Children.Add(b);
        }
        Grid.SetColumn(tabs, 1); bar.Children.Add(tabs);
        _search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _query = _search.Text ?? ""; Show("suche"); } };
        var search = new Border { Width = 220, Height = 34, CornerRadius = new CornerRadius(9), Background = Brush.Parse(C("#990A1630", "#CCFFFFFF")), BorderBrush = Brush.Parse(C("#5541618F", "#5541618F")), BorderThickness = new Thickness(1),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { new Border { Margin = new Thickness(10, 0, 0, 0), Child = Ico(Mdi.Magnify, 16, Text) }, Pos(_search, 1) } } };
        _search.Background = Brushes.Transparent; _search.BorderThickness = new Thickness(0); _search.Width = double.NaN; _search.VerticalContentAlignment = VerticalAlignment.Center; _search.FontSize = 13;
        Border Circle(Control c) => new() { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Brush.Parse(C("#990A1630", "#CCFFFFFF")), BorderBrush = Brush.Parse(C("#5541618F", "#5541618F")), BorderThickness = new Thickness(1), Child = c };
        var bell = Flat("nav-approvals", new Grid { Children = { Circle(Ico(Mdi.Bell, 17, Text)),
            new Border { Background = Brush.Parse("#FF3B4A"), CornerRadius = new CornerRadius(6), MinWidth = 10, Height = 10, Padding = new Thickness(2, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 6, 0),
                Child = new Viewbox { Child = _bellBadge } } } }, "Freigaben");
        bell.Padding = new Thickness(0);
        ToolTip.SetTip(bell, "Freigaben (Strg+4)");
        bell.Click += (_, _) => Show("approvals");
        var cloud = Flat("btn-cloud", Circle(Ico(Mdi.CloudOutline, 18, Text)), "Cloud"); cloud.Padding = new Thickness(0);
        ToolTip.SetTip(cloud, "Cloud: nicht verbunden (kein Cloud-Konto eingerichtet)"); cloud.Click += (_, _) => Show("keys");
        var user = Flat("btn-user", Circle(Ico(Mdi.AccountCircleOutline, 20, Brush.Parse("#D4E157"))), "Benutzer"); user.Padding = new Thickness(0);
        ToolTip.SetTip(user, "Lokaler Besitzer"); user.Click += (_, _) => Show("settings");
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 36, 0), Children = { search, bell, cloud, user } };
        Grid.SetColumn(tools, 2); bar.Children.Add(tools);
        _clock.FontSize = 22; _clock.FontWeight = FontWeight.SemiBold; _date.FontSize = 13; _date.Foreground = Brush.Parse(C("#C9D6EA", "#3A4A66"));
        var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0), Children = { _clock, _date } };
        Grid.SetColumn(clock, 3); bar.Children.Add(clock);
        return bar;
    }

    private readonly Dictionary<string, Button> _tabs = new();
    private void MarkTab(string key)
    {
        foreach (var (k, b) in _tabs)
        {
            var on = k == key || (k == "start" && key == "start");
            b.Background = on ? Brush.Parse("#59123466") : Brushes.Transparent;
            b.BorderBrush = on ? Brush.Parse("#995AA9FF") : Brushes.Transparent;
            b.BorderThickness = new Thickness(on ? 1 : 0);
        }
    }

    // ---------- Seitenleiste ----------
    private static readonly (string Label, string Icon, string C1, string C2, string Filter)[] Categories =
    [
        ("Alle Studios", StudioIcons.Areas["area-all"].Path, StudioIcons.Areas["area-all"].C1, StudioIcons.Areas["area-all"].C2, ""), ("Online Business", StudioIcons.Areas["area-online"].Path, StudioIcons.Areas["area-online"].C1, StudioIcons.Areas["area-online"].C2, "STUDIO-ONLINE"),
        ("3D & Games", StudioIcons.Areas["area-games"].Path, StudioIcons.Areas["area-games"].C1, StudioIcons.Areas["area-games"].C2, "CORE-05|CORE-06"), ("Kreativ & Medien", StudioIcons.Areas["area-kreativ"].Path, StudioIcons.Areas["area-kreativ"].C1, StudioIcons.Areas["area-kreativ"].C2, "CORE-08|SPEC-15"),
        ("Office & Dokumente", StudioIcons.Areas["area-office"].Path, StudioIcons.Areas["area-office"].C1, StudioIcons.Areas["area-office"].C2, "SYS-10"), ("Briefstudio", StudioIcons.Areas["area-brief"].Path, StudioIcons.Areas["area-brief"].C1, StudioIcons.Areas["area-brief"].C2, "STUDIO-LETTER"),
        ("Kommunikation", StudioIcons.Areas["area-komm"].Path, StudioIcons.Areas["area-komm"].C1, StudioIcons.Areas["area-komm"].C2, "SYS-09"), ("Tools & Entwickler", StudioIcons.Areas["area-tools"].Path, StudioIcons.Areas["area-tools"].C1, StudioIcons.Areas["area-tools"].C2, "CORE-07|X-06|X-03"),
        ("System & Sicherheit", StudioIcons.Areas["area-sicher"].Path, StudioIcons.Areas["area-sicher"].C1, StudioIcons.Areas["area-sicher"].C2, "CORE-11|SYS-13|SPEC-12"), ("KI Assistent", StudioIcons.Areas["area-ki"].Path, StudioIcons.Areas["area-ki"].C1, StudioIcons.Areas["area-ki"].C2, "CORE-02|CORE-03|CORE-12"),
    ];

    private Control LeftColumn()
    {
        var nav = new StackPanel { Spacing = 3 };
        _sideStart = SideItem("side-start", Chip(Mdi.Home, "#64B5F6", "#1E88E5", 26), "Start", () => Show("start"));
        nav.Children.Add(_sideStart);
        foreach (var (label, icon, c1, c2, filter) in Categories)
            nav.Children.Add(SideItem("side-" + label.ToLowerInvariant().Replace(' ', '-').Replace("&", "und"), Chip(icon, c1, c2), StudioCatalog.SideLabels.GetValueOrDefault(label, label), () => { if (filter == "STUDIO-LETTER") { Show("brief"); return; } if (filter == "SYS-10") { Show("office"); return; } _studioFilter = filter; Show("studios"); }));
        nav.Children.Add(SideItem("side-dateien", Chip(Mdi.Folder, "#42A5F5", "#1565C0"), "Dateien", () => Show("projects")));
        nav.Children.Add(SideItem("side-einstellungen", Chip(Mdi.Cog, "#78909C", "#37474F"), "Einstellungen", () => Show("settings")));
        var quick = new StackPanel { Spacing = 1 };
        var qh = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(6, 2, 6, 6), Children = { H("Schnellzugriff", 13), Pos(new TextBlock { Text = "→", Foreground = Muted, FontSize = 13 }, 1) } };
        quick.Children.Add(qh);
        quick.Children.Add(SideItem("quick-new-task", Chip(Mdi.CheckboxMarkedOutline, "#29B6F6", "#0277BD", 22), "Neue Aufgabe", () => NotBuilt("Aufgaben"), 12.5));
        quick.Children.Add(SideItem("quick-new-project", Chip(Mdi.FolderPlus, "#42A5F5", "#1565C0", 22), "Neues Projekt", () => Show("projects"), 12.5));
        quick.Children.Add(SideItem("quick-new-letter", Chip(Mdi.EmailPlus, "#FB8C00", "#E65100", 22), "Neuer Brief", () => Show("brief"), 12.5));
        quick.Children.Add(SideItem("quick-approvals", Chip(Mdi.ChartLine, "#AB47BC", "#6A1B9A", 22), "Offene Freigaben", () => Show("approvals"), 12.5));
        quick.Children.Add(SideItem("quick-system-check", Chip(Mdi.ShieldSearch, "#66BB6A", "#2E7D32", 22), "System-Check", () => Show("system"), 12.5));
        var navPanel = Panel(nav, 8); var quickPanel = Panel(quick, 8);
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        g.Children.Add(navPanel); Grid.SetRow(quickPanel, 2); g.Children.Add(quickPanel);
        return g;
    }
    private Button? _sideStart;

    private string _notBuilt = "";
    private void NotBuilt(string what) { _notBuilt = what; _core.Audit.Append(Principal.LocalOwner.Name, "ui.not_built", what); Show("notbuilt"); }
    private Control NotBuiltView() => Page(_notBuilt, P($"{_notBuilt}: noch nicht gebaut. Diese Funktion ist genehmigt, aber noch nicht implementiert.", Text));

    private static Button SideItem(string name, Control icon, string label, Action onClick, double font = 13)
    {
        var b = Flat(name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { icon, new TextBlock { Text = label, Foreground = Text, FontSize = font, VerticalAlignment = VerticalAlignment.Center } } }, label);
        b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(8, 5); b.MinHeight = 0;
        b.Click += (_, _) => onClick();
        return b;
    }

    private void MarkSide(string key)
    {
        if (_sideStart is null) return;
        var on = key == "start";
        _sideStart.Background = on ? Grad("#2F86F6", "#1F5FD0") : Brushes.Transparent;
    }

    // ---------- Desktop (Startansicht) ----------
    private static readonly (string Id, string Title, string Sub, string Icon, string C1, string C2, string Glow)[] FeaturedStudios =
    [
        ("STUDIO-ONLINE-BUSINESS-01", "Online Business", "Gründen & Verkaufen", Mdi.RocketLaunch, "#7FD3FF", "#3F6BFF", "#3F8BFF"),
        ("STUDIO-LETTER-01", "Briefstudio", "Briefe, Ämter & Recht", Mdi.FileDocumentEdit, "#FFD39A", "#F08A3C", "#F0A050"),
        ("CORE-05", "3D Studio", "Blender & Assets", Mdi.CubeOutline, "#8FB8FF", "#3D5BFF", "#4D6BFF"),
        ("CORE-06", "Game Studio", "Unreal Engine 5", Mdi.Unreal, "#FFFFFF", "#C9D6EA", "#9FB8FF"),
        ("SYS-10", "Office Studio", "Dokumente & Tabellen", Mdi.FileDocumentOutline, "#F2F6FF", "#B8C7E6", "#8FB0FF"),
        ("CORE-02", "KI Studio", "Trendo Assistent", Mdi.Brain, "#8FD8FF", "#2F86F6", "#2F9BFF"),
        ("CORE-08", "Medien Studio", "Video, Audio, Grafik", Mdi.PlayBox, "#FF6FB5", "#7C4DFF", "#C04DFF"),
        ("CORE-07", "Web & App Studio", "Full-Stack Entwicklung", Mdi.CodeTags, "#7FD3FF", "#2F86F6", "#2F9BFF"),
        ("CORE-11", "Security Studio", "Schutz & Privatsphäre", Mdi.ShieldCheck, "#9CF5A8", "#22B35E", "#2FD07A"),
    ];

    private Control DesktopView()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("500,*"), RowDefinitions = new RowDefinitions("*,Auto") };
        // Studio-Kacheln 3x3
        var tiles = new UniformGrid { Columns = 3, Rows = 3 };
        foreach (var (id, title0, sub, icon, c1, c2, glow) in UserSettings.Ordered(FeaturedStudios, f => f.Item1, _settings.TileOrder).Where(f => !_settings.HiddenTiles.Contains("tile-" + f.Item1) && (!_settings.SimpleMode || Trendo.Core.Sys.A11y.SimpleTiles.Contains(f.Item1))))
        {
            var st = _studios.FirstOrDefault(s => s.Id == id);
            var title = StudioCatalog.For(id)?.Name ?? title0;
            var badge = Badge(st);
            var t = new Button
            {
                Name = "tile-" + id, Height = 132, Margin = new Thickness(5), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Grad(C("#B3122648", "#E6FFFFFF"), C("#B30B1A36", "#D9EEF3FA")), BorderBrush = Brush.Parse(C("#4D3F7FD0", "#663F7FD0")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Content = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new Border { Height = 58, Child = StudioIcons.For(id) is { } si ? Ico(si.Path, 46, Grad(si.C1, si.C2), si.C2) : Ico(icon, 48, Grad(c1, c2), glow) },
                    new TextBlock { Text = title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center },
                    new TextBlock { Text = sub, Foreground = Brush.Parse(C("#C9D6EA", "#3A4A66")), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center }, badge } }
            };
            Avalonia.Automation.AutomationProperties.SetName(t, $"{title}: {badge.Text}");
            ToolTip.SetTip(t, $"{badge.Text} – {st?.Reason ?? "Status unbekannt"}");
            t.Click += async (_, _) => await OpenStudio(id, title);
            Hover(t); tiles.Children.Add(t);
        }
        var all = SmallOutline("btn-all-studios", "Alle anzeigen"); all.HorizontalAlignment = HorizontalAlignment.Right;
        all.Click += (_, _) => { _studioFilter = null; Show("studios"); };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(6, 2, 6, 10),
            Children = { new TextBlock { Text = "Trendo Studios", Foreground = Brush.Parse(C("#E8F3FF", "#0F1B2E")), FontSize = 19, FontWeight = FontWeight.SemiBold } } };
        Grid.SetColumn(all, 1); head.Children.Add(all);
        var studiosPanel = Panel(new StackPanel { Children = { head, tiles } }, 14);
        studiosPanel.Name = "widget-studios"; studiosPanel.IsVisible = !_settings.HiddenTiles.Contains("widget-studios");
        studiosPanel.VerticalAlignment = VerticalAlignment.Top; studiosPanel.Margin = new Thickness(0, 6, 0, 0);
        g.Children.Add(studiosPanel);

        // Hero: Logo, Schriftzug, Slogan, Aktionen, Lichtring
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 26, 0, 0) };
        foreach (var (icon, label, key) in new[] { (Mdi.Pencil, "Erstellen", "projects"), (Mdi.CodeTags, "Entwickeln", "studios"), (Mdi.Magnify, "Gestalten", "studios"), (Mdi.CalendarMonth, "Organisieren", "projects"), (Mdi.Tools, "Automatisieren", "approvals") })
        {
            var b = new Button { Name = "action-" + label.ToLowerInvariant(), Width = 76, Height = 64, Padding = new Thickness(2, 6), CornerRadius = new CornerRadius(10),
                Background = Brush.Parse(C("#99101F3D", "#CCFFFFFF")), BorderBrush = Brush.Parse("#664C8DFF"), BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Center,
                Content = new StackPanel { Spacing = 6, Children = { Ico(icon, 22, Text), new TextBlock { Text = label, Foreground = Text, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center } } } };
            if (label is "Gestalten") b.Click += (_, _) => { _studioFilter = "CORE-08|CORE-05"; Show("studios"); };
            else if (label is "Entwickeln") b.Click += (_, _) => { _studioFilter = "CORE-07|CORE-06"; Show("studios"); };
            else b.Click += (_, _) => Show(key);
            actions.Children.Add(b);
        }
        var word = new TextBlock { Text = "Trendo OS", FontSize = 64, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -4, 0, 0),
            Foreground = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#FFFFFF"), 0), new GradientStop(Color.Parse("#CFE6FF"), 0.6), new GradientStop(Color.Parse("#8EC5FF"), 1) } },
            Effect = new DropShadowEffect { Color = Color.Parse("#2E8BFF"), BlurRadius = 22, OffsetX = 0, OffsetY = 0, Opacity = 0.85 } };
        var hero = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 180, 0, 0),
            Children = {
                new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = Logo(128, glow: true) },
                word,
                new TextBlock { Text = "DEIN SYSTEM. DEINE IDEEN. KEINE GRENZEN.", Foreground = Brush.Parse(C("#DCE9FA", "#1B2A44")), FontSize = 12.5, LetterSpacing = 1.4, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) },
                actions,
                GlowRing(),
            }
        };
        Grid.SetColumn(hero, 1); g.Children.Add(hero);
        DesktopMenu(g);
        return g;
    }

    /// <summary>Leuchtender Ring auf dem See (rein dekorativ).</summary>
    private static Control GlowRing()
    {
        var g = new Grid { Width = 560, Height = 110, Margin = new Thickness(0, 4, 0, 0), IsHitTestVisible = false };
        g.Children.Add(new Ellipse { Width = 560, Height = 110, Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#AA35C8FF"), 0), new GradientStop(Color.Parse("#4424A0FF"), 0.55), new GradientStop(Color.Parse("#0024A0FF"), 1) } } });
        foreach (var (w, h, o, t) in new[] { (540.0, 100.0, 0.45, 1.5), (440.0, 80.0, 0.7, 2.0), (330.0, 60.0, 0.9, 2.5), (220.0, 40.0, 1.0, 2.0) })
            g.Children.Add(new Ellipse { Width = w, Height = h, Stroke = Brush.Parse("#7FE0FF"), StrokeThickness = t, Opacity = o,
                Effect = new DropShadowEffect { Color = Color.Parse("#39B6FF"), BlurRadius = 12, OffsetX = 0, OffsetY = 0, Opacity = 1 } });
        g.Children.Add(new Ellipse { Width = 170, Height = 26, Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#EEDFF8FF"), 0), new GradientStop(Color.Parse("#0039B6FF"), 1) } } });
        return g;
    }

    private static TextBlock Badge(StudioEntry? st)
    {
        var txt = st is null ? "" : StudioRegistry.FriendlyLabel(st.Capability) ?? "";
        var col = st?.Capability switch { Capability.Ready => "#4ADE80", Capability.Blocked => "#FACC15", _ => "#93A4C3" };
        return new TextBlock { Text = txt, IsVisible = txt.Length > 0, Foreground = Brush.Parse(col), FontSize = 9.5, Opacity = 0.9, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
    }

    // ---------- Rechte Spalte ----------
    private readonly TextBlock _sysState = new() { Name = "sys-state", Text = "…", Foreground = Brush.Parse("#4ADE80"), FontSize = 12 };
    private readonly Ellipse _sysDot = new() { Width = 9, Height = 9, Fill = Brush.Parse("#4ADE80") };

    private Control RightColumn()
    {
        var sp = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        string[] keys = ["widget-ki", "widget-system", "widget-projekte", "widget-tools"];
        void Add(Control c) { var k = keys[sp.Children.Count]; c.Name = k; c.IsVisible = !_settings.HiddenTiles.Contains(k); Grid.SetRow(c, sp.Children.Count); sp.Children.Add(c); }
        // KI-Assistent – echter Zustand
        var avatar = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Grad("#2F86F6", "#0B2A6B"), BorderBrush = Brush.Parse("#887FD3FF"), BorderThickness = new Thickness(1), Child = Ico(Mdi.RobotHappy, 20, Brushes.White) };
        var kiHead = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { avatar, Pos(new TextBlock { Text = "Trendo KI Assistent", Foreground = Text, FontSize = 14.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis }, 1) } };
        _kiDot.Width = 9; _kiDot.Height = 9; _kiStatus.FontSize = 11.5;
        var kiState = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center, Children = { _kiDot, _kiStatus } };
        Grid.SetColumn(kiState, 2); kiHead.Children.Add(kiState);
        if (!_kiWired)
        {
            // Nur einmal verdrahten: die Felder leben so lange wie das Fenster, die Spalte kann neu gebaut werden
            _kiWired = true;
            _kiSetup.Click += (_, _) => Show("components");
            _kiInput.KeyDown += async (_, e) => { if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(_kiInput.Text)) { e.Handled = true; await AskKi(_kiInput.Text!); } };
        }
        _kiInput.Background = Brushes.Transparent; _kiInput.BorderThickness = new Thickness(0); _kiInput.Margin = new Thickness(0); _kiInput.VerticalContentAlignment = VerticalAlignment.Center; _kiInput.FontSize = 12.5;
        var send = new Button { Name = "btn-ki-send", Width = 30, Height = 30, Padding = new Thickness(0), Background = Brush.Parse("#33FFFFFF"), CornerRadius = new CornerRadius(6), Content = Ico(Mdi.Send, 15, Text) };
        send.Click += async (_, _) => { if (_kiInput.IsEnabled && !string.IsNullOrWhiteSpace(_kiInput.Text)) await AskKi(_kiInput.Text!); };
        var inputBox = new Border { Margin = new Thickness(0, 10, 0, 0), Height = 38, CornerRadius = new CornerRadius(8), Background = Brush.Parse(C("#990A1630", "#CCFFFFFF")), BorderBrush = Brush.Parse(C("#5541618F", "#5541618F")), BorderThickness = new Thickness(1), Padding = new Thickness(4, 0, 4, 0),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = { _kiInput, Pos(_kiSetup, 1), Pos(send, 2) } } };
        _kiSetup.Margin = new Thickness(0, 0, 4, 0); _kiSetup.Padding = new Thickness(8, 2); _kiSetup.FontSize = 11; _kiSetup.VerticalAlignment = VerticalAlignment.Center; _kiSetup.Background = Accent;
        _kiText.FontSize = 13; _kiText.LineHeight = 19;
        Add(Panel(new StackPanel { Children = { kiHead,
            new Border { Background = Brush.Parse(C("#80142849", "#B3FFFFFF")), BorderBrush = Brush.Parse("#333F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10), Margin = new Thickness(0, 12, 0, 0), MaxHeight = 150,
                Child = new ScrollViewer { Content = _kiText } },
            inputBox } }, 14));
        // Systemstatus – echte Messwerte
        var rings = new UniformGrid { Columns = 4, Margin = new Thickness(0, 12, 0, 2) };
        foreach (var k in new[] { "CPU", "RAM", "SSD", "GPU" }) rings.Children.Add(Ring(k));
        var stateRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center, Children = { _sysDot, _sysState } };
        Add(Panel(new StackPanel { Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("System Status", 14.5), Pos(stateRow, 1) } }, rings } }, 14));
        // Aktuelle Projekte – echt
        var allP = SmallOutline("btn-all-projects", "Alle anzeigen"); allP.Click += (_, _) => Show("projects");
        Add(Panel(new StackPanel { Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Aktuelle Projekte", 14.5), Pos(allP, 1) } }, new Border { Height = 8 }, _projectsPanel } }, 12));
        // Schnell-Tools
        var tools = new UniformGrid { Columns = 4, Margin = new Thickness(0, 10, 0, 0) };
        var shot = ToolButton("tool-screenshot", Chip(Mdi.Camera, "#1E2A44", "#0F1A30", 30, 0.6), "Screenshot", true);
        shot.Click += (_, _) =>
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Trendo");
            Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, $"trendo-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            SaveScreenshot(f); _core.Audit.Append(Principal.LocalOwner.Name, "tool.screenshot", Path.GetFileName(f));
        };
        tools.Children.Add(shot);
        tools.Children.Add(ToolButton("tool-aufnahme", Chip(Mdi.Record, "#FF5252", "#C62828", 30, 0.6), "Aufnahme", false));
        tools.Children.Add(ToolButton("tool-notiz", Chip(Mdi.NoteEdit, "#ECEFF1", "#B0BEC5", 30, 0.6, Brush.Parse("#263238")), "Notiz", true));
        ((Button)tools.Children[^1]).Click += (_, _) => Show("werkzeuge");
        tools.Children.Add(ToolButton("tool-uebersetzen", Chip(Mdi.Translate, "#42A5F5", "#1565C0", 30, 0.6), "Übersetzen", false));
        Add(Panel(new StackPanel { Children = { H("Schnell-Tools", 14.5), tools } }, 12));
        return sp;
    }

    private static Control Pos(Control c, int col) { Grid.SetColumn(c, col); return c; }

    private static Button ToolButton(string name, Control icon, string label, bool enabled)
    {
        // Nicht gebaute Werkzeuge sehen gleich aus (Design), sind aber nicht bedienbar und so beschriftet (Tooltip/Screenreader).
        var b = new Button { Name = name, IsEnabled = enabled, Background = Brush.Parse(C("#80142849", "#B3FFFFFF")), BorderBrush = Brush.Parse("#333F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Margin = new Thickness(3), Padding = new Thickness(2, 8), Height = 66, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new StackPanel { Spacing = 5, Children = { icon, new TextBlock { Text = label, Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
        ToolTip.SetTip(b, enabled ? label : $"{label}: noch nicht gebaut");
        ToolTip.SetShowOnDisabled(b, true);
        Avalonia.Automation.AutomationProperties.SetName(b, enabled ? label : $"{label} (noch nicht gebaut)");
        return b;
    }

    private Control Ring(string key)
    {
        var bg = new Ellipse { Width = 60, Height = 60, Stroke = Brush.Parse("#2AFFFFFF"), StrokeThickness = 5 };
        var arc = new Arc { Width = 60, Height = 60, StrokeThickness = 5, StartAngle = -90, SweepAngle = 0, StrokeLineCap = PenLineCap.Round,
            Stroke = Grad("#34E3C3", "#18B892"), Effect = new DropShadowEffect { Color = Color.Parse("#22D3A6"), BlurRadius = 8, OffsetX = 0, OffsetY = 0, Opacity = 0.8 } };
        var val = new TextBlock { Name = "metric-" + key.ToLowerInvariant(), Text = "…", Foreground = Text, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center };
        _rings[key] = (arc, val);
        return new Grid { Width = 64, Height = 64, Children = { new Ellipse { Width = 60, Height = 60, Fill = Brush.Parse("#66091428") }, bg, arc,
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = key, Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }, val } } } };
    }

    private void RefreshProjects()
    {
        _projectsPanel.Children.Clear();
        var list = _core.Projects.List().OrderByDescending(p => p.CreatedAt).Take(4).ToList();
        Control Row(Control chip, string title, string sub, Control? right = null) => new Border { Background = Brush.Parse("#66142849"), BorderBrush = Brush.Parse("#263F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Margin = new Thickness(0, 0, 0, 6),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { chip,
                Pos(new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 12.5 }, new TextBlock { Text = sub, Foreground = Muted, FontSize = 11 } } }, 1),
                Pos(right ?? Ico(Mdi.DotsVertical, 14, Muted), 2) } } };
        if (list.Count == 0)
        {
            var b = new Button { Name = "btn-first-project", Content = "＋ Erstes Projekt", FontSize = 11, Padding = new Thickness(8, 3), VerticalAlignment = VerticalAlignment.Center };
            b.Click += (_, _) => Show("projects");
            _projectsPanel.Children.Add(Row(Chip(Mdi.FolderPlus, "#42A5F5", "#1565C0", 30), "Noch keine Projekte", "Lege dein erstes Projekt an", b));
            return;
        }
        var colors = new[] { ("#1E88E5", "#0D47A1"), ("#FB8C00", "#E65100"), ("#F4511E", "#BF360C"), ("#ECEFF1", "#B0BEC5") };
        var i = 0;
        foreach (var p in list)
        {
            var (c1, c2) = colors[i++ % colors.Length];
            _projectsPanel.Children.Add(Row(Chip(Mdi.Folder, c1, c2, 30), p.Name, $"Angelegt {p.CreatedAt:dd.MM.yyyy HH:mm}"));
        }
    }

    // ---------- Dock ----------
    private sealed record DockApp(string Id, string Icon, string C1, string C2, string Label, Func<string?> Target, IBrush? Fg = null);

    private Control Dock()
    {
        string? Tool(string id) { var c = DependencyCatalog.Get(id); return _deps.Integrated(id) ?? _deps.ResolvePath(c); }
        var apps = new List<DockApp>
        {
            new("start", "M18,14 H46 A5,5 0 0 1 46,24 H37 V45 A5,5 0 0 1 27,45 V24 H18 A5,5 0 0 1 18,14 Z", "#3BA7FF", "#1565C0", "Start", () => null),
            new("files", Mdi.Folder, "#FFD54F", "#F9A825", "Dateien", () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            new("browser", Mdi.Web, "#4FC3F7", "#1565C0", "Browser", () => _core.Offline ? null : "https://github.com/manuelbrandner85/Trendo-OS"),
            new("mail", Mdi.Email, "#42A5F5", "#1565C0", "E-Mail", () => _core.Offline ? null : "mailto:"),
            new("ki", Mdi.StarFourPoints, "#5C9DFF", "#283593", "Trendo KI", () => null),
            new("media", Mdi.Image, "#64B5F6", "#1E40AF", "Medien Studio", () => null),
            new("settings", Mdi.Cog, "#90A4AE", "#455A64", "Einstellungen", () => null),
            new("blender", Mdi.Blender, "#FFB74D", "#EF6C00", "Blender", () => Tool("blender")),
            new("unreal", Mdi.Unreal, "#FFFFFF", "#CFD8DC", "Unreal Engine", () => Tool("unreal")),
            new("components", Mdi.Cart, "#4DD0E1", "#00838F", "Zusatzprogramme", () => null),
            new("code", Mdi.CodeTags, "#64B5F6", "#1565C0", "Web & App Studio", () => null),
            new("office", Mdi.FileDocument, "#ECEFF1", "#B0BEC5", "Office Studio", () => null, Brush.Parse("#37474F")),
            new("security", Mdi.ShieldCheck, "#66BB6A", "#1B5E20", "Security Studio", () => null),
        };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center };
        foreach (var a in UserSettings.Ordered(apps, x => x.Id, _settings.DockOrder).Where(x => x.Id != "start" && !_settings.HiddenTiles.Contains("dock-" + x.Id)))
        {
            var b = new Button { Name = "dock-" + a.Id, Width = 50, Height = 50, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = a.Id is "mail" or "media" or "office" ? Chip(a.Icon, a.C1, a.C2, 38, 0.62, a.Fg) : Ico(a.Icon, 36, Grad(a.C1, a.C2), a.C2) };
            ToolTip.SetTip(b, a.Label); Avalonia.Automation.AutomationProperties.SetName(b, a.Label);
            b.Click += async (_, _) =>
            {
                switch (a.Id)
                {
                    case "start": Show("start"); break;
                    case "ki": Show("ki"); break;
                    case "browser": Show("downloads"); break;
                    case "media": await OpenStudio("CORE-08", "Medien Studio"); break;
                    case "settings": Show("settings"); break;
                    case "components": Show("components"); break;
                    case "code": await OpenStudio("CORE-07", "Web & App Studio"); break;
                    case "office": Show("office"); break;
                    case "security": _studioFilter = "CORE-11"; Show("studios"); break;
                    case "blender": if (a.Target() is { } bl) _core.Integrations.LaunchDirect(Principal.LocalOwner, bl, _core.Audit); else await OpenStudio("CORE-05", "3D Studio"); break;
                    case "unreal": if (a.Target() is { } ue) _core.Integrations.LaunchDirect(Principal.LocalOwner, ue, _core.Audit); else await OpenStudio("CORE-06", "Game Studio"); break;
                    default:
                        if (a.Target() is { } t) _core.Integrations.LaunchDirect(Principal.LocalOwner, t, _core.Audit);
                        break;
                }
            };
            if (a.Id is "browser" or "mail" && _core.Offline) { b.IsEnabled = false; ToolTip.SetTip(b, $"{a.Label} – offline deaktiviert"); }
            Hover(b); bar.Children.Add(b);
        }
        var trash = new Button { Name = "dock-trash", Width = 46, Height = 46, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = Ico(Mdi.Delete, 34, Grad("#ECEFF1", "#90A4AE")) };
        ToolTip.SetTip(trash, "Papierkorb"); trash.Click += (_, _) => _core.Integrations.LaunchDirect(Principal.LocalOwner, "shell:RecycleBinFolder", _core.Audit);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { StartButton(), new Border { Width = 1, Height = 40, Background = Brush.Parse("#553F7FD0") }, bar, new Border { Width = 1, Height = 40, Background = Brush.Parse("#553F7FD0") }, trash } };
        var dockBar = new Border { Background = Brush.Parse(C("#B30A1630", "#CCF2F6FC")), BorderBrush = Brush.Parse("#664C8DFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(18, 9),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 26), Child = row, BoxShadow = BoxShadows.Parse("0 0 24 0 #442F86F6") };
        _taskbar.IsVisible = false;
        return new StackPanel { Children = { _taskbar, dockBar } };
    }

    // ---------- Inhaltsansichten (im Glaspanel) ----------
    /// <summary>Modul einer Studio-Seite: PageModules (RegisterTab), sonst StudioPages. Brief und Dokumente sind die Einstiege aus PageForStudio.</summary>
    private string? StudioIdForPage(string key)
    {
        if (PageModules.TryGetValue(key, out var fromPage)) return fromPage;
        foreach (var (id, page) in StudioPages)
            if (string.Equals(page, key, StringComparison.OrdinalIgnoreCase)) return id;
        if (key is "brief" or "akten" or "akte" or "ausgabe" or "briefkoepfe") return "STUDIO-LETTER-01";
        if (string.Equals(key, "office", StringComparison.OrdinalIgnoreCase)) return "SYS-10";
        return null;
    }

    private string StudioHeading(string fallback) =>
        StudioIdForPage(CurrentView) is { } id ? StudioCatalog.NameOf(id, fallback) : fallback;

    private Control Page(string title, Control content)
    {
        var sp = new StackPanel { Spacing = 8, Children = { PageHead(title), content } };
        Detach(_hintHost); sp.Children.Insert(1, _hintHost);
        return Panel(new ScrollViewer { Content = sp }, 18);
    }

    /// <summary>Seite ohne äußeres Scrollen: Kopf bleibt stehen, <paramref name="fill"/> füllt die restliche Höhe.</summary>
    private Control FitPage(string title, Control header, Control fill)
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), MinHeight = 0, VerticalAlignment = VerticalAlignment.Stretch };
        root.Children.Add(PageHead(title));
        var hint = Detach(_hintHost); Grid.SetRow(hint, 1); root.Children.Add(hint);
        Grid.SetRow(header, 2); root.Children.Add(header);
        fill.VerticalAlignment = VerticalAlignment.Stretch; fill.MinHeight = 0;
        Grid.SetRow(fill, 3); root.Children.Add(fill);
        return Panel(root, 12);
    }

    private Control PageHead(string title)
    {
        title = StudioHeading(title);
        var help = new Button { Name = "btn-help", Content = "?", Width = 28, Height = 28, Padding = new Thickness(0), CornerRadius = new CornerRadius(14), HorizontalContentAlignment = HorizontalAlignment.Center, FontWeight = FontWeight.Bold, Background = Brush.Parse("#33FFFFFF"), Foreground = Text };
        ToolTip.SetTip(help, "Was kann ich hier tun?"); var key = CurrentView;
        help.Click += (_, _) =>
        {
            if (_hintHost.Children.Count > 0) { _hintHost.Children.Clear(); return; }
            ShowHelp(key, force: true);
        };
        var heading = H(title, 22); heading.Name = "page-title";
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { heading, Pos(help, 1) } };
    }

    private Control StudiosView()
    {
        var f = _studioFilter;
        var list = string.IsNullOrWhiteSpace(f) ? _studios
            : _studios.Where(s => f.Split('|').Any(x => s.Id.StartsWith(x, StringComparison.OrdinalIgnoreCase)) || s.Name.Contains(f, StringComparison.OrdinalIgnoreCase)).ToList();
        var body = new StackPanel { Spacing = 4 };
        var groups = list.GroupBy(s => StudioCatalog.For(s.Id)?.Area ?? "Weitere")
            .OrderBy(g => Array.IndexOf(StudioCatalog.Areas, g.Key) is var k && k < 0 ? 99 : k);
        foreach (var g in groups)
        {
            var wrap = new WrapPanel();
            foreach (var st in g)
            {
                var info = StudioCatalog.For(st.Id);
                var name = info?.Name ?? st.Name;
                var badge = Badge(st); badge.HorizontalAlignment = HorizontalAlignment.Left;
                // Karten sind echte Knöpfe: Klick öffnet das Studio
                var card = new Button { Name = "studio-card-" + st.Id, Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 10, 10), Width = 230,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                    Content = new StackPanel { Spacing = 4, Children = { CardHead(st.Id, name), P(info?.Sentence ?? "", null, 11.5), badge } } };
                Avalonia.Automation.AutomationProperties.SetName(card, name);
                // Technische Angaben nur für Entwickler im Tooltip
                ToolTip.SetTip(card, $"{st.Id} · {st.ApprovalStatus} · {st.Reason}");
                var sid = st.Id;
                card.Click += async (_, _) => await OpenStudio(sid, name);
                Hover(card); wrap.Children.Add(card);
            }
            var head = H(g.Key, 16); head.Name = "studio-area-" + g.Key; head.Margin = new Thickness(0, 8, 0, 4);
            body.Children.Add(head); body.Children.Add(wrap);
        }
        var count = P(string.IsNullOrWhiteSpace(f) ? $"{list.Count} Studios" : $"{list.Count} Studios in diesem Bereich");
        if (_studios.Count == 0) count = P("Die Studio-Liste wurde nicht gefunden.", Brushes.OrangeRed);
        return Page("Alle Studios", new StackPanel { Spacing = 8, Children = { count, body } });
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
            var box = new StackPanel { Spacing = 6 };
            box.Children.Add(new TextBlock { Text = p.Name, Foreground = Text });
            box.Children.Add(P($"Angelegt {p.CreatedAt:dd.MM.yyyy HH:mm}"));
            var scene3d = Path.Combine(_core.Projects.ProjectFilesRoot(p.Id), "3d", "szene.json");
            if (File.Exists(scene3d))
            {
                var open3 = new Button { Name = "btn-open-3d-" + p.Id, Content = "Im 3D-Studio öffnen" };
                open3.Click += (_, _) => Open3DProject(p.Id);
                box.Children.Add(open3);
            }
            box.Children.Add(open);
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = box });
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
            var live = r.Action == LiveBroadcast.Action;
            var yes = new Button { Name = "btn-approve-" + r.Id, Content = live ? "Freigeben, noch nicht senden" : "Freigeben und ausführen", Background = Accent };
            var no = new Button { Name = "btn-deny-" + r.Id, Content = "Ablehnen" };
            yes.Click += (_, _) =>
            {
                _core.Approvals.Approve(r.Id, Principal.LocalOwner);
                if (live) { NoteLiveApproved(r.Id); Show("sendung"); return; }
                _core.Integrations.Execute(r); Show("approvals");
            };
            no.Click += (_, _) =>
            {
                _core.Approvals.Deny(r.Id, Principal.LocalOwner);
                if (live) { NoteLiveDenied(r.Id); Show("sendung"); return; }
                Show("approvals");
            };
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = r.Description, Foreground = Text }, P($"Aktion {r.Action} · gültig bis {r.ExpiresAt:HH:mm}"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { yes, no } } } } });
        }
        return Page("Freigaben", sp);
    }

    // ---------- Fortschritt: Status live aus requirements.yaml, Reihenfolge aus docs/buildplan.json ----------
    private Control ProgressView()
    {
        var sp = new StackPanel { Spacing = 6 };
        var req = TrendoCore.FindRequirements(AppContext.BaseDirectory);
        var plan = req is null ? null : Path.Combine(Path.GetDirectoryName(req)!, "buildplan.json");
        if (plan is null || !File.Exists(plan)) { sp.Children.Add(P("Bauplan nicht gefunden (requirements.yaml / buildplan.json).", Text)); return Page("Fortschritt", sp); }
        var mods = Progress.Load(req!, plan);
        var done = mods.Count(m => m.Done); var subs = mods.SelectMany(m => m.Subs).ToList();
        sp.Children.Add(P($"{done} von {mods.Count} Modulen fertig und getestet · Teilanforderungen (Brief/Business): {subs.Count(x => Progress.GermanStatus(x) == "Fertig & getestet")} von {subs.Count}. Quelle: requirements.yaml (keine geschönten Angaben).", Text));
        foreach (var w in mods.GroupBy(m => m.Wave))
        {
            var list = w.ToList();
            sp.Children.Add(H($"{w.Key} · {list[0].WaveName}  ({list.Count(m => m.Done)}/{list.Count})", 15));
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*,150") };
            var r = 0;
            foreach (var m in list)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var st = Progress.GermanStatus(m.Module);
                var cells = new Control[] { P(m.Module.Id, null, 11), P(m.Module.Name + (m.Subs.Count > 0 ? $"  ·  {m.Subs.Count} Teilanforderungen" : ""), Text, 12),
                    new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1), Margin = new Thickness(0, 1), HorizontalAlignment = HorizontalAlignment.Left,
                        Background = Brush.Parse(st switch { "Fertig & getestet" => "#3322C55E", "In Arbeit" or "Gebaut, Test offen" => "#33FACC15", _ => "#22FFFFFF" }),
                        Child = P(st, Text, 11) } };
                for (var c = 0; c < 3; c++) { Grid.SetRow(cells[c], r); Grid.SetColumn(cells[c], c); grid.Children.Add(cells[c]); }
                r++;
            }
            sp.Children.Add(grid);
        }
        return Page("Fortschritt", sp);
    }

    private Control SystemView()
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Nav2("Schutz & Geräte", "schutz"), Nav2("Systemcheck", "systemcheck"), Nav2("Sicherung", "sicherung"), Nav2("Bau-Fortschritt", "fortschritt"), Nav2("Einrichtung erneut", "einrichtung") } });
        var crashes = CrashLog.List(_core.DataRoot);
        sp.Children.Add(P($"Trendo OS {CrashLog.AppVersion()} · Updates kommen nur aus geprüften Versionen (main) · nicht code-signiert", Text));
        sp.Children.Add(P(crashes.Count == 0 ? "Absturzprotokolle: keine – alles stabil." : $"Absturzprotokolle: {crashes.Count}, zuletzt {File.GetLastWriteTime(crashes[0]):dd.MM.yyyy HH:mm} (Ordner logs, ohne Benutzernamen)", crashes.Count == 0 ? null : Brush.Parse("#FACC15")));
        sp.Children.Add(P($"Benutzerdaten: {_core.DataRoot} (bleiben bei Update und Deinstallation erhalten)"));
        sp.Children.Add(P($"Passwörter und Schlüssel: sicher gespeichert ({_core.Secrets.BackendName})"));
        sp.Children.Add(P($"Protokoll: {_core.Audit.ReadAll().Count} Einträge, {(_core.Audit.Verify() ? "vollständig und unverändert" : "BESCHÄDIGT – bitte prüfen")}"));
        foreach (var e in _core.Audit.ReadAll().TakeLast(15).Reverse())
            sp.Children.Add(P($"{e.At:HH:mm:ss}  {e.Actor}  {e.Action}  {e.Detail}", null, 11));
        return Page("System & Sicherheit", sp);
    }

    private static long TotalRam => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    private string KiModelName => KiModel.Current(_core.DataRoot, TotalRam, _deps.HasModel);

    private Control KiModelChooser()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(P($"Modell: {KiModelName}. Arbeitsspeicher: {TotalRam / (1024 * 1024 * 1024.0):0} GB – {(KiModel.RecommendLarge(TotalRam) ? "Qwen2.5 3B empfohlen" : "Qwen2.5 0.5B empfohlen")}.", Text));
        foreach (var (m, id, label) in new[] { (DependencyCatalog.LocalModel, "model", "Qwen2.5 0.5B (klein, schnell)"), (DependencyCatalog.LargeModel, "model-3b", "Qwen2.5 3B (besseres Deutsch, ~1,9 GB)") })
        {
            var have = _deps.HasModel(m);
            var b = new Button { Name = "btn-model-" + id, Content = have ? (KiModelName == m ? $"✓ {label}" : $"{label} verwenden") : $"{label} herunterladen (Zustimmung)", IsEnabled = !_depBusy, Background = KiModelName == m ? Accent : null };
            b.Click += async (_, _) =>
            {
                if (!_deps.IsInstalled(DependencyCatalog.Get(id))) { _deps.GiveConsent(Principal.LocalOwner); await InstallOne(DependencyCatalog.Get(id), refresh: false); }
                if (_deps.HasModel(m)) { KiModel.Select(_core.DataRoot, m, _core.Audit); _kiConnected = false; await RefreshKi(); }
                Show("ki");
            };
            sp.Children.Add(b);
        }
        return sp;
    }

    private Control KiView() => Page("KI", new StackPanel { Spacing = 6, Children = {
        KiNav(), KiModelChooser(),
        P(_kiConnected ? $"Die KI läuft auf diesem PC (Modell {KiModelName}). Nichts geht ins Internet, nichts kostet extra." : "Die KI ist noch nicht eingerichtet. Unter „Zusatzprogramme“ installiert Trendo sie kostenlos mit einem Klick.", Text),
        Nav2("Zusatzprogramme öffnen", "components") } });

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

    /// <summary>Startet den lokalen Ollama-Dienst (nur 127.0.0.1), falls installiert aber nicht aktiv. Startet nichts anderes.</summary>
    private async Task EnsureOllamaRunning()
    {
        if (await _ollama.IsUpAsync()) return;
        var ch = await _deps.CheckAsync(DependencyCatalog.Get("ollama"));
        if (ch.State != ToolState.Working || ch.Path is null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ch.Path, "serve") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(ch.Path)! });
            _core.Audit.Append("app", "ki.runtime_started", "ollama serve (127.0.0.1)");
            for (var i = 0; i < 20 && !await _ollama.IsUpAsync(); i++) await Task.Delay(500);
        }
        catch (Exception ex) { _core.Audit.Append("app", "ki.runtime_failed", ex.GetType().Name); }
        await RefreshKi();
    }

    private async Task RefreshKi()
    {
        var ok = !_opt.Isolated && await _ollama.HasModelAsync(KiModelName);
        if (ok == _kiConnected && _kiStatus.Text != "…") return;
        _kiConnected = ok;
        _kiStatus.Text = ok ? "Online (lokal)" : "Nicht verbunden";
        _kiDot.Fill = ok ? Brush.Parse("#4ADE80") : Brushes.Gray;
        _kiInput.IsEnabled = ok; _kiSetup.IsVisible = !ok;
        _kiInput.PlaceholderText = "Deine Nachricht an Trendo …";
        if (ok) _kiText.Text = "Hallo! Wie kann ich dir heute helfen? Du kannst mir eine Aufgabe geben, ein Studio öffnen, ein Projekt starten oder eine Frage stellen. (Lokales Modell, läuft auf diesem PC.)";
        if (CurrentView == "ki") Show("ki");
    }

    private bool _kiWired, _kiBusy;
    /// <summary>Für Tests: ersetzt die lokale KI (kein Netz).</summary>
    public Func<string, Task<string>>? KiChatOverride { get; set; }
    public int KiAskCount { get; private set; }

    private async Task AskKi(string q)
    {
        if (_kiBusy) return; // eine Frage nach der anderen – doppelte Auslöser schicken nichts doppelt
        _kiBusy = true; KiAskCount++;
        try { await AskKiCore(q); } finally { _kiBusy = false; }
    }

    private async Task AskKiCore(string q)
    {
        _kiInput.Text = ""; _kiInput.IsEnabled = false; _kiText.Text = $"Du: {q}\n\nTrendo KI denkt …";
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.ask", $"{q.Length} Zeichen");
        var a = KiChatOverride is { } o ? await o(q) : await _ollama.ChatAsync(KiModelName, q);
        _kiText.Text = $"Du: {q}\n\nTrendo KI: {a}"; _kiInput.IsEnabled = true;
    }

    // ---------- Komponenten: Zustimmung, Auto-Installation, geführte Installation ----------
    private Control ComponentsView()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(P("Alles, was im Setup enthalten ist, ist sofort nutzbar. Weitere freie Programme installiert Trendo OS nach einmaliger Zustimmung automatisch pro Benutzer (ohne Admin). Nicht weitergebbare Software kann nur über den offiziellen Herstellerweg installiert werden.", Text));
        var consent = new Button { Name = "btn-deps-consent", Content = _deps.HasConsent ? "Fehlende kostenlose Programme installieren" : "Zustimmen & fehlende kostenlose Programme installieren", IsEnabled = !_depBusy, Background = Accent };
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
        return Page("Zusatzprogramme", sp);
    }

    // ---------- Studio-Klick: erkennen → prüfen → einmal fragen → installieren ----------
    private string _depStudio = "", _depStudioTitle = "", _depReturn = "studios";
    private List<ToolCheck> _depChecks = [];
    public Func<string, Task<bool>>? KeyValidatorOverride { get; set; }

    /// <summary>Seite eines Studios: eigener Bereich, erste Registerkarte des Moduls oder null (noch kein Bereich).</summary>
    public string? PageForStudio(string id)
    {
        if (id.StartsWith("STUDIO-LETTER", StringComparison.OrdinalIgnoreCase)) return "brief";
        if (id.Equals("SYS-10", StringComparison.OrdinalIgnoreCase)) return "office";
        if (StudioPages.TryGetValue(id, out var sp) && _showMap.ContainsKey(sp)) return sp;
        var tab = PageModules.FirstOrDefault(kv => string.Equals(kv.Value, id, StringComparison.OrdinalIgnoreCase) && _showMap.ContainsKey(kv.Key)).Key;
        if (tab is not null) return tab;
        return BasePageModules.FirstOrDefault(kv => string.Equals(kv.Value, id, StringComparison.OrdinalIgnoreCase) && _showMap.ContainsKey(kv.Key)).Key;
    }

    public async Task OpenStudio(string id, string title)
    {
        _core.Audit.Append(Principal.LocalOwner.Name, "studio.open", id);
        var page = PageForStudio(id);
        if (page is null)
        {
            // Kein eigener Bereich: ehrlich sagen statt still auf dieselbe Liste zurückzuspringen
            _notBuilt = title; Show("notbuilt"); return;
        }
        // 3D lässt sich ohne Blender bauen und ansehen. Blender wird erst beim Öffnen gefragt.
        if (string.Equals(id, "CORE-05", StringComparison.OrdinalIgnoreCase)) { Show(page); return; }
        if (!StudioTools.Required.TryGetValue(id, out var need)) { Show(page); return; }
        _depReturn = "studios";
        _depStudio = id; _depStudioTitle = title; _depChecks = [];
        _depLog.Text = $"{title}: prüfe benötigte Programme …"; Show("studio-deps");
        foreach (var t in need) _depChecks.Add(await _deps.CheckAsync(DependencyCatalog.Get(t)));
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.check", $"{id}: " + string.Join(", ", _depChecks.Select(c => $"{c.Component.Id}={c.State}")));
        _depLog.Text = _depChecks.All(c => c.Ok) ? $"{title}: alle benötigten Programme gefunden und funktionsfähig." : "";
        if (_depChecks.All(c => c.Ok)) { ReloadStudios(); Show(page); } else Show("studio-deps");
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
            later.Click += (_, _) => { _core.Audit.Append(Principal.LocalOwner.Name, "deps.declined", comp.Id); var back = _depReturn; _depReturn = "studios"; Show(back); };
            btns.Children.Add(later);
            row.Children.Add(btns); ask.Child = row; sp.Children.Add(ask);
        }
        sp.Children.Add(_depProgress); sp.Children.Add(_depLog);
        return Page(_depStudioTitle, sp);
    }

    // ---------- API-Schlüssel (optional, nie Pflicht) ----------
    private static readonly (string Id, string Name, string KeyUrl, string ValidateUrl, string Note)[] KeyServices =
    [
        ("xai", "xAI / Grok (optional, kostet Geld)", "https://console.x.ai", "https://api.x.ai/v1/models", "Optional – Trendo KI läuft lokal ohne Schlüssel."),
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
            rq.Headers.UserAgent.ParseAdd("TrendoOS/0.4");
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
            _depLog.Text = ok ? $"{c.Name}: installiert und erkannt ✓" : $"{c.Name}: hat nicht geklappt – im Systemcheck kannst du es erneut versuchen";
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
        if (req is not null) { _studios = StudioCatalog.Friendly(_core.Studios.Load(req, _probe)); }
    }

}

internal sealed class RelayCommand(Action run) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => run();
}
