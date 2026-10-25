using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
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
    private readonly TextBox _search = new() { Name = "txt-search", PlaceholderText = "Studios, Dateien und Einstellungen suchen", Height = 36 };
    private readonly TextBlock _weekday = new() { Foreground = Muted, FontSize = 13 };
    private readonly TextBlock _netDown = new() { Name = "metric-net-down", Foreground = Text, FontSize = 12.5 };
    private readonly TextBlock _netUp = new() { Name = "metric-net-up", Foreground = Text, FontSize = 12.5 };
    private readonly WrapPanel _pinsPanel = new();
    private Border? _pinCard;
    private bool _kiWired;
    private readonly Dictionary<string, Button> _tabs = new();
    private string _notBuilt = "";
    private IReadOnlyList<StudioEntry> _studios = [];
    public IReadOnlyList<StudioEntry> Studios => _studios;
    private Trendo.Api.LocalApi? _api;
    private Dictionary<string, Func<Control>> _showMap = new();
    private string? _studioFilter;
    private string _studioQuery = "";
    private int _studioRebuild;

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

        // T-0218 (Referenzbild 1): Kopfleiste | linke Spalte 192 · Mitte · rechte Spalte 322, Dock unter der Mitte. Designfläche 1536 × 800, proportional skaliert.
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        root.Children.Add(Wallpaper()); Grid.SetRowSpan(root.Children[^1], 2);
        var ui = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        if (BlurredWallpaper(ui) is { } blur) { root.Children.Add(blur); Grid.SetRowSpan(blur, 2); }
        Grid.SetRowSpan(ui, 2); root.Children.Add(ui);
        StartupTrace.Mark("ctor.wallpaper");
        ui.Children.Add(TopBar());
        var body = new Grid { Name = "shell", ColumnDefinitions = new ColumnDefinitions("192,*,322"), RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(8, 10, 8, 4) };
        Grid.SetRow(body, 1); ui.Children.Add(body);
        var left = LeftColumn(); Grid.SetRowSpan(left, 2); body.Children.Add(left);
        Grid.SetColumn(_center, 1); _center.Margin = new Thickness(12, 0, 12, 0); body.Children.Add(_center);
        var dock = Dock(); Grid.SetColumn(dock, 1); Grid.SetRow(dock, 1); dock.ZIndex = 5; body.Children.Add(dock);
        var right = RightColumn(); Grid.SetColumn(right, 2); Grid.SetRowSpan(right, 2); body.Children.Add(right);
        StartupTrace.Mark("ctor.left+dock");
        Grid.SetColumnSpan(_workArea, 3); Grid.SetRowSpan(_workArea, 2); _workArea.ZIndex = 30; body.Children.Add(_workArea);
        _status.IsVisible = false;
        _search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _query = _search.Text ?? ""; Show("suche"); } };
        WireKi();
        var scaler = new LayoutTransformControl { Child = root };
        Content = scaler;
        void DoRescale()
        {
            var cs = ClientSize; if (cs.Width <= 0 || cs.Height <= 0) return;
            var k = Math.Clamp(Math.Min(cs.Width / DesignW, cs.Height / DesignH), 0.5, 3.0) * UserSettings.ClampScale(_settings.UiScale);
            scaler.LayoutTransform = Math.Abs(k - 1) < 0.01 ? null : new ScaleTransform(k, k);
            root.Width = cs.Width / k; root.Height = cs.Height / k;
        }
        Rescale = DoRescale;
        PropertyChanged += (_, e) => { if (e.Property == ClientSizeProperty) DoRescale(); };
        DoRescale();

        _showMap = new()
        {
            ["start"] = DesktopView, ["studios"] = StudiosView, ["projects"] = ProjectsView,
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
        if (_reopenSection is { } keepSection) { _settingsSection = keepSection; _reopenSection = null; }
        if (Environment.GetEnvironmentVariable("TRENDO_SETTINGS") is { Length: > 0 } settingsSection) _settingsSection = settingsSection;
        Show(opt.Page ?? (NeedsSetup(opt) ? "einrichtung" : "start"));
        StartupTrace.Mark("ctor.show");
        RefreshStatusBar();
        StartupTrace.Mark("ctor.statusbar");

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => Tick();
        // Uhr sofort; erste Messung (CPU/GPU-Zähler, ~65 ms) erst nach dem ersten Bild
        _clock.Text = DateTime.Now.ToString("HH:mm", De); _weekday.Text = DateTime.Now.ToString("dddd", De); _date.Text = DateLine(DateTime.Now);
        OwnTimer(timer); Dispatcher.UIThread.Post(Tick, DispatcherPriority.Background); StartupTrace.Mark("ctor.tick");

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
        Closing += (_, _) => { StartupTrace.Mark("window.closing"); StartupTrace.Flush(); foreach (var t in _ownedTimers) t.Stop(); if (_reopening) return; _api?.DisposeAsync().AsTask().Wait(2000); _core.Shutdown(); };
        InitLook(root, opt);
        StartupTrace.Mark("window.ready");
    }

    private string _apiInfo = "";
    private readonly List<DispatcherTimer> _ownedTimers = [];
    /// <summary>Timer gehören zum Fenster. Beim Schließen stoppen, sonst bleibt jedes Testfenster samt Hintergrundbild im Speicher.</summary>
    private void OwnTimer(DispatcherTimer t) { _ownedTimers.Add(t); t.Start(); }
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
        _weekday.Text = now.ToString("dddd", De);
        _date.Text = DateLine(now);
        if (_tick++ % 5 == 0) _ = RefreshKi();
        // Messung (CPU/GPU-Zähler, Dateisystem, Netzwerk) im Hintergrund, Anzeige danach im UI-Thread (#280)
        if (_sampling) return;
        _sampling = true;
        _ = Task.Run(() =>
        {
            try
            {
                MetricsSnapshot m; (double? UsedGb, double? TotalGb) mem, disk; (double? DownBps, double? UpBps) net;
                lock (_metrics) { m = _metrics.Sample(); mem = _metrics.MemorySize(); disk = _metrics.DiskSize(); net = _metrics.NetworkSpeed(); }
                Dispatcher.UIThread.Post(() => { _sampling = false; ApplyMetrics(m, mem, disk, net); });
            }
            catch (Exception) { Dispatcher.UIThread.Post(() => _sampling = false); }
        });
    }

    private bool _sampling;
    private IBrush? _brushHigh, _brushOk;
    private IBrush BrushHigh => _brushHigh ??= new ImmutableSolidColorBrush(Color.Parse(C("#FACC15", "#B45309")));
    private IBrush BrushOk => _brushOk ??= new ImmutableSolidColorBrush(Color.Parse(C("#4ADE80", "#15803D")));

    private void ApplyMetrics(MetricsSnapshot m, (double? UsedGb, double? TotalGb) mem, (double? UsedGb, double? TotalGb) disk, (double? DownBps, double? UpBps) net)
    {
        _lastCpu = m.CpuPercent; _lastRam = m.RamPercent;
        SetRing("CPU", m.CpuPercent, null);
        SetRing("RAM", m.RamPercent, mem.UsedGb is { } mu && mem.TotalGb is { } mt ? $"{mu:0.0} / {mt:0.0} GB" : null);
        SetRing("SSD", m.DiskPercent, disk.UsedGb is { } du && disk.TotalGb is { } dt ? $"{du:0} / {dt:0} GB" : null);
        SetRing("GPU", m.GpuPercent, m.GpuPercent is null ? "Nicht verfügbar" : m.GpuSource);
        _netDown.Text = net.DownBps is { } down ? "↓ " + Rate(down) : _tick < 2 ? "↓ Messung läuft …" : "↓ Nicht verfügbar";
        _netUp.Text = net.UpBps is { } up ? "↑ " + Rate(up) : _tick < 2 ? "↑ Messung läuft …" : "↑ Nicht verfügbar";
        var vals = new[] { m.CpuPercent, m.RamPercent, m.DiskPercent, m.GpuPercent }.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var high = vals.Count > 0 && vals.Max() >= 90;
        _sysState.Text = vals.Count == 0 ? "Messung läuft …" : high ? "Hohe Auslastung" : "Alles in Ordnung";
        _sysDot.Fill = _sysState.Foreground = high ? BrushHigh : BrushOk;
    }

    private static string Rate(double bps) => bps < 1024 ? $"{bps:0} B/s" : bps < 1024 * 1024 ? $"{bps / 1024:0.0} KB/s" : $"{bps / 1024 / 1024:0.0} MB/s";

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
        // D7: sofort schreiben – kommt kein Bild (Fenster verdeckt/minimiert), fehlte die Datei sonst ganz
        StartupTrace.Flush();
        DispatcherTimer.RunOnce(StartupTrace.Flush, TimeSpan.FromSeconds(5));
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
        if (_settings.Theme == "kontrast" || LowPower) return null;
        try
        {
            // Glas: Unschärfe wird einmal vorab berechnet (Start/Bildwechsel), nicht mehr live pro Bild.
            var img = _blurImg; img.Stretch = Stretch.UniformToFill; img.HorizontalAlignment = HorizontalAlignment.Center; img.VerticalAlignment = VerticalAlignment.Center;
            img.Source = BlurredWallpaperBitmap(_wallName);
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

    // ---------- Suchbereiche (früher die linke Leiste; die Fläche selbst bleibt leer) ----------
    private static readonly (string Label, string Icon, string C1, string C2, string Filter)[] Categories =
    [
        ("Alle Studios", StudioIcons.Areas["area-all"].Path, StudioIcons.Areas["area-all"].C1, StudioIcons.Areas["area-all"].C2, ""), ("Online Business", StudioIcons.Areas["area-online"].Path, StudioIcons.Areas["area-online"].C1, StudioIcons.Areas["area-online"].C2, "STUDIO-ONLINE"),
        ("3D & Games", StudioIcons.Areas["area-games"].Path, StudioIcons.Areas["area-games"].C1, StudioIcons.Areas["area-games"].C2, "CORE-05|CORE-06"), ("Kreativ & Medien", StudioIcons.Areas["area-kreativ"].Path, StudioIcons.Areas["area-kreativ"].C1, StudioIcons.Areas["area-kreativ"].C2, "CORE-08|SPEC-15"),
        ("Office & Dokumente", StudioIcons.Areas["area-office"].Path, StudioIcons.Areas["area-office"].C1, StudioIcons.Areas["area-office"].C2, "SYS-10"), ("Briefstudio", StudioIcons.Areas["area-brief"].Path, StudioIcons.Areas["area-brief"].C1, StudioIcons.Areas["area-brief"].C2, "STUDIO-LETTER"),
        ("Kommunikation", StudioIcons.Areas["area-komm"].Path, StudioIcons.Areas["area-komm"].C1, StudioIcons.Areas["area-komm"].C2, "SYS-09"), ("Tools & Entwickler", StudioIcons.Areas["area-tools"].Path, StudioIcons.Areas["area-tools"].C1, StudioIcons.Areas["area-tools"].C2, "CORE-07|X-06|X-03"),
        ("System & Sicherheit", StudioIcons.Areas["area-sicher"].Path, StudioIcons.Areas["area-sicher"].C1, StudioIcons.Areas["area-sicher"].C2, "CORE-11|SYS-13|SPEC-12"), ("KI Assistent", StudioIcons.Areas["area-ki"].Path, StudioIcons.Areas["area-ki"].C1, StudioIcons.Areas["area-ki"].C2, "CORE-02|CORE-03|CORE-12"),
    ];

    /// <summary>Frühere Startkacheln: Reihenfolge in den Einstellungen, die Fläche selbst bleibt ruhig.</summary>
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

    private Button Quiet(string name, string label, Action go, string? icon = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (icon is not null) row.Children.Add(Ico(icon, 16, Muted));
        row.Children.Add(new TextBlock { Text = label, Foreground = Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var b = Flat(name, row, label);
        b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left;
        b.Padding = new Thickness(10, 5); b.Click += (_, _) => go();
        return b;
    }

    /// <summary>„Di, 21. Okt. 2026“ wie in der Kopfleiste der Referenz.</summary>
    /// <summary>Sparsames Leistungsprofil (T-0107): selbst gewählt oder automatisch auf ARM (Raspberry Pi).</summary>
    /// <summary>TRENDO_PROFILE=pi bzw. voll (#268) übersteuert die Einstellung, etwa für das Pi-Image.</summary>
    public bool LowPower => Environment.GetEnvironmentVariable("TRENDO_PROFILE")?.Trim().ToLowerInvariant() switch
    {
        "pi" => true,
        "voll" => false,
        _ => _settings.IsLowPower(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture is System.Runtime.InteropServices.Architecture.Arm64 or System.Runtime.InteropServices.Architecture.Arm),
    };
    /// <summary>Keine Animationen: „Bewegung reduzieren“ oder sparsames Profil.</summary>
    private bool Motionless => _settings.ReduceMotion || LowPower;

    private static string DateLine(DateTime d) => d.ToString("ddd", De).TrimEnd('.') + ", " + d.ToString("d. MMM yyyy", De);

    private void MarkTab(string key)
    {
        foreach (var (k, b) in _tabs)
        {
            var on = k == key;
            b.Background = on ? Brush.Parse("#59123466") : Brushes.Transparent;
            b.BorderBrush = on ? Brush.Parse("#995AA9FF") : Brushes.Transparent;
            b.BorderThickness = new Thickness(on ? 1 : 0);
        }
    }

    private void MarkSide(string key)
    {
        if (_sideStart is null) return;
        _sideStart.Background = key == "start" ? Grad("#2F86F6", "#1F5FD0") : Brushes.Transparent;
    }

    private static TextBlock Badge(StudioEntry? st)
    {
        var txt = st is null ? "" : StudioRegistry.FriendlyLabel(st.Capability) ?? "";
        var col = st?.Capability switch { Capability.Ready => "#4ADE80", Capability.Blocked => "#FACC15", _ => "#93A4C3" };
        return new TextBlock { Text = txt, IsVisible = txt.Length > 0, Foreground = Brush.Parse(col), FontSize = 9.5, Opacity = 0.9, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
    }

    // ---------- Schwebende Widgets: Uhr, echte Messwerte, eigene Projekte, selbst angeheftete Studios ----------
    private readonly TextBlock _sysState = new() { Name = "sys-state", Text = "…", Foreground = Brush.Parse("#4ADE80"), FontSize = 12 };
    private readonly Ellipse _sysDot = new() { Width = 9, Height = 9, Fill = Brush.Parse("#4ADE80") };

    /// <summary>Nur Studios, die der Besitzer selbst angeheftet hat. Ohne Auswahl bleibt die Fläche leer.</summary>
    private void RefreshPins()
    {
        _pinsPanel.Children.Clear();
        foreach (var id in _settings.PinnedStudios.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var known = StudioCatalog.For(id) is not null || _studios.Any(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            if (!known) continue;
            var name = StudioCatalog.NameOf(id, _studios.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))?.Name ?? id);
            var icon = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#33283C5C", "#E6FFFFFF")), HorizontalAlignment = HorizontalAlignment.Center, Child = IconFor(id, 20) ?? Ico(Mdi.FileDocumentOutline, 20, Accent) };
            var label = new TextBlock { Text = name, Foreground = Text, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextTrimming = TextTrimming.CharacterEllipsis };
            var b = new Button { Name = "desk-pin-" + id, Background = Brushes.Transparent, Padding = new Thickness(2), Margin = new Thickness(2), Width = 84, Content = new StackPanel { Spacing = 4, Children = { icon, label } } };
            Avalonia.Automation.AutomationProperties.SetName(b, name);
            var sid = id; var sname = name;
            b.Click += async (_, _) => await ActivateOrOpenStudio(sid, sname);
            _pinsPanel.Children.Add(b);
        }
        if (_pinCard is not null) _pinCard.IsVisible = _pinsPanel.Children.Count > 0;
        Rescale?.Invoke();
    }

    // ---------- Dock: nur vorhandene Programme ----------
    private sealed record DockApp(string Id, string Icon, string C1, string C2, string Label, Func<string?> Target, IBrush? Fg = null);

    private bool ProgramInstalled(string id) => id switch
    {
        "files" => true,
        "browser" => Browsers.Find().Count > 0 || new[] { "google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "firefox", "microsoft-edge" }.Any(n => Cli.Find(n) is not null),
        "mail" => new[] { "thunderbird", "evolution" }.Any(n => Cli.Find(n) is not null),
        "blender" or "unreal" => true, // Sichtbarkeit hängt am aufgelösten Pfad
        _ => false,
    };

    private Control Dock()
    {
        string? Tool(string id) { var c = DependencyCatalog.Get(id); return _deps.Integrated(id) ?? _deps.ResolvePath(c); }
        var apps = new List<DockApp>
        {
            new("files", Mdi.Folder, "#FFD54F", "#F9A825", "Dateien", () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            new("browser", Mdi.Web, "#4FC3F7", "#1565C0", "Browser", () => _core.Offline ? null : "https://github.com/manuelbrandner85/Trendo-OS"),
            new("mail", Mdi.Email, "#42A5F5", "#1565C0", "E-Mail", () => _core.Offline ? null : "mailto:"),
            new("ki", Mdi.StarFourPoints, "#5C9DFF", "#283593", "Trendo KI", () => null),
            new("media", Mdi.Image, "#64B5F6", "#1E40AF", "Medien", () => null),
            new("settings", Mdi.Cog, "#90A4AE", "#455A64", "Einstellungen", () => null),
            new("blender", Mdi.Blender, "#FFB74D", "#EF6C00", "Blender", () => Tool("blender")),
            new("unreal", Mdi.Unreal, "#FFFFFF", "#CFD8DC", "Unreal Engine", () => Tool("unreal")),
            new("components", Mdi.Cart, "#4DD0E1", "#00838F", "Zusatzprogramme", () => null),
            new("code", Mdi.CodeTags, "#64B5F6", "#1565C0", "Webseite bauen", () => null),
            new("office", Mdi.FileDocument, "#ECEFF1", "#B0BEC5", "Texte & Tabellen", () => null, Brush.Parse("#37474F")),
            new("security", Mdi.ShieldCheck, "#66BB6A", "#1B5E20", "Daten schützen", () => null),
        };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        foreach (var a in UserSettings.Ordered(apps, x => x.Id, _settings.DockOrder).Where(x => !_settings.HiddenTiles.Contains("dock-" + x.Id)))
        {
            var installed = a.Id switch
            {
                "files" => true,
                "browser" => ProgramInstalled("browser"),
                "mail" => ProgramInstalled("mail"),
                "blender" => a.Target() is not null,
                "unreal" => a.Target() is not null,
                _ => true, // eingebaute Trendo-Bereiche
            };
            var b = new Button { Name = "dock-" + a.Id, Width = 48, Height = 48, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), IsVisible = installed, Content = Ico(a.Icon, 30, a.Fg ?? Grad(a.C1, a.C2), a.C2) };
            ToolTip.SetTip(b, a.Label); Avalonia.Automation.AutomationProperties.SetName(b, a.Label);
            ToolTip.SetPlacement(b, Avalonia.Controls.PlacementMode.Top); ToolTip.SetVerticalOffset(b, -6);
            b.Click += async (_, _) =>
            {
                switch (a.Id)
                {
                    case "ki": Show("ki"); break;
                    case "browser": Show("downloads"); break;
                    case "media": await OpenStudio("CORE-08", "Videos & Fotos bearbeiten"); break;
                    case "settings": Show("settings"); break;
                    case "components": Show("components"); break;
                    case "code": await OpenStudio("CORE-07", "Webseite bauen"); break;
                    case "office": Show("office"); break;
                    case "security": await OpenStudio("CORE-11", "Daten schützen"); break;
                    case "blender": if (a.Target() is { } bl) _core.Integrations.LaunchDirect(Principal.LocalOwner, bl, _core.Audit); break;
                    case "unreal": if (a.Target() is { } ue) _core.Integrations.LaunchDirect(Principal.LocalOwner, ue, _core.Audit); break;
                    default:
                        if (a.Target() is { } t) _core.Integrations.LaunchDirect(Principal.LocalOwner, t, _core.Audit);
                        break;
                }
            };
            if (a.Id is "browser" or "mail" && _core.Offline) { b.IsEnabled = false; ToolTip.SetTip(b, $"{a.Label} – offline deaktiviert"); }
            if (!installed) b.IsEnabled = false;
            Hover(b); bar.Children.Add(b);
        }
        var trash = new Button { Name = "dock-trash", Width = 46, Height = 46, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = Ico(Mdi.Delete, 28, Grad("#ECEFF1", "#90A4AE")) };
        ToolTip.SetTip(trash, "Papierkorb"); Avalonia.Automation.AutomationProperties.SetName(trash, "Papierkorb");
        ToolTip.SetPlacement(trash, Avalonia.Controls.PlacementMode.Top); ToolTip.SetVerticalOffset(trash, -6);
        trash.Click += (_, _) => _core.Integrations.LaunchDirect(Principal.LocalOwner, OperatingSystem.IsWindows() ? "shell:RecycleBinFolder" : "trash://", _core.Audit);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { StartButton(), new Border { Width = 1, Height = 36, Background = Brush.Parse("#553F7FD0"), VerticalAlignment = VerticalAlignment.Center }, bar, new Border { Width = 1, Height = 36, Background = Brush.Parse("#553F7FD0"), VerticalAlignment = VerticalAlignment.Center }, trash } };
        var dockBar = new Border
        {
            Background = Brush.Parse(C("#C0101C33", "#E6F4F7FC")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(22), Padding = new Thickness(14, 8),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 6), Child = row, BoxShadow = BoxShadows.Parse("0 10 28 0 #66000818")
        };
        _taskbar.IsVisible = false;
        return new StackPanel { Children = { _taskbar, dockBar }, HorizontalAlignment = HorizontalAlignment.Center };
    }

    private static Border ShellCard(Control child) => new()
    {
        Background = Glass, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18),
        Padding = new Thickness(14, 12), Child = child, BoxShadow = BoxShadows.Parse("0 12 32 0 #66000818"),
    };

    private void WireKi()
    {
        if (_kiWired) return;
        _kiWired = true;
        _kiSetup.Click += (_, _) => Show("components");
        _kiInput.KeyDown += async (_, e) => { if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(_kiInput.Text)) { e.Handled = true; await AskKi(_kiInput.Text!); } };
    }

    private static Control Pos(Control c, int col) { Grid.SetColumn(c, col); return c; }

    private void NotBuilt(string what) { _notBuilt = what; _core.Audit.Append(Principal.LocalOwner.Name, "ui.not_built", what); Show("notbuilt"); }
    private Control NotBuiltView() => Page(_notBuilt, P($"{_notBuilt}: noch nicht gebaut. Diese Funktion ist genehmigt, aber noch nicht implementiert.", Text));


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

    private string StudioHeading(string fallback)
    {
        if (CurrentView == "studio-deps" && _depStudio.Length > 0)
            return StudioCatalog.NameOf(_depStudio, string.IsNullOrWhiteSpace(_depStudioTitle) ? fallback : _depStudioTitle);
        return StudioIdForPage(CurrentView) is { } id ? StudioCatalog.NameOf(id, fallback) : fallback;
    }

    /// <summary>Seite gehört zu einem konkreten Studio-Arbeitsbereich (nicht zu Einstellungen, Start oder der Übersicht).</summary>
    private bool IsStudioWorkspace(string key) =>
        PageModules.ContainsKey(key)
        || key is "brief" or "akten" or "akte" or "ausgabe" or "briefkoepfe" or "office"
        || StudioPages.Any(kv => string.Equals(kv.Value, key, StringComparison.OrdinalIgnoreCase));

    private Control Page(string title, Control content, Control? dock = null)
    {
        var sp = new StackPanel { Spacing = 8, Children = { PageHead(title), content } };
        Detach(_hintHost); sp.Children.Insert(1, _hintHost);
        var scroll = new ScrollViewer { Content = sp };
        if (dock is null) return Panel(scroll, 18);
        Detach(dock); dock.Margin = new Thickness(0, 10, 0, 0);
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        grid.Children.Add(scroll); Grid.SetRow(dock, 1); grid.Children.Add(dock);
        return Panel(grid, 18);
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
        var titleCol = new StackPanel { Spacing = 2, Children = { heading } };
        if (StudioIdForPage(CurrentView) is { } sid && StudioCatalog.For(sid) is { } info)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (StudioIcons.For(sid) is { } si) line.Children.Add(Chip(si.Path, si.C1, si.C2, 22, 0.62));
            line.Children.Add(new TextBlock { Text = info.Sentence, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            titleCol.Children.Add(line);
        }
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { titleCol, Pos(help, 1) } };
        return head;
    }

    private static string StudioFilterId(string? area) => area switch
    {
        null or "" => "alle",
        "Arbeiten & Büro" => "arbeiten",
        "Business & Geld" => "business",
        "Kreativ & Medien" => "kreativ",
        "3D, Spiele & VR" => "spiele",
        "Technik & Fachgebiete" => "technik",
        "Wissen & Lernen" => "wissen",
        "KI & Automatik" => "ki",
        "Entwickeln" => "entwickeln",
        "System & Sicherheit" => "system",
        _ => "weitere",
    };

    private bool StudioHit(StudioEntry s)
    {
        var info = StudioCatalog.For(s.Id);
        var name = info?.Name ?? s.Name;
        var q = _studioQuery.Trim();
        if (q.Length > 0 && !name.Contains(q, StringComparison.OrdinalIgnoreCase) && !(info?.Sentence.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) && !s.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
            return false;
        var f = _studioFilter;
        if (string.IsNullOrWhiteSpace(f)) return true;
        if (string.Equals(info?.Area, f, StringComparison.OrdinalIgnoreCase)) return true;
        return f.Split('|').Any(x => s.Id.StartsWith(x, StringComparison.OrdinalIgnoreCase)) || name.Contains(f, StringComparison.OrdinalIgnoreCase) || s.Name.Contains(f, StringComparison.OrdinalIgnoreCase);
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
        if (HaltBanner() is { } banner) sp.Children.Add(banner);
        var pending = _core.Approvals.Pending();
        if (pending.Count == 0) sp.Children.Add(P("Keine offenen Freigaben."));
        foreach (var r in pending)
        {
            var live = r.Action == LiveBroadcast.Action;
            var yes = new Button { Name = "btn-approve-" + r.Id, Content = live ? "Freigeben, noch nicht senden" : "Freigeben und ausführen", Background = Accent };
            var no = new Button { Name = "btn-deny-" + r.Id, Content = "Ablehnen" };
            if (_core.Approvals.Halted) { yes.IsEnabled = false; ToolTip.SetTip(yes, "Not-Stopp ist aktiv"); ToolTip.SetShowOnDisabled(yes, true); }
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
                new TextBlock { Text = r.Description, Foreground = Text, TextWrapping = TextWrapping.Wrap }, P($"Aktion {r.Action} · Ziel {r.Target} · gültig bis {r.ExpiresAt:HH:mm}"),
                // T-0005 Änderungsvorschau: genau das, was passiert
                r.Preview is { Length: > 0 } pv
                    ? new Border { Name = "approval-preview-" + r.Id, Background = Brush.Parse(C("#66091428", "#F1F5FB")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6),
                        Child = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Was passiert:", Foreground = Muted, FontSize = 11 }, new SelectableTextBlock { Text = pv.Length > 2000 ? pv[..2000] + " …" : pv, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } }
                    : P("Keine Änderungsvorschau für diese Aktion.", null, 11),
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

    private Control KiView()
    {
        WireKi();
        Detach(_kiStatus); Detach(_kiDot); Detach(_kiText); Detach(_kiInput); Detach(_kiSetup);
        _kiDot.Width = 9; _kiDot.Height = 9; _kiStatus.FontSize = 12;
        var state = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { _kiDot, _kiStatus } };
        _kiInput.Background = Brushes.Transparent; _kiInput.BorderThickness = new Thickness(0); _kiInput.Margin = new Thickness(0);
        _kiInput.VerticalContentAlignment = VerticalAlignment.Center; _kiInput.FontSize = 13;
        var send = new Button { Name = "btn-ki-send", Width = 34, Height = 34, Padding = new Thickness(0), Background = Brush.Parse("#33FFFFFF"), CornerRadius = new CornerRadius(8), Content = Ico(Mdi.Send, 15, Text) };
        Avalonia.Automation.AutomationProperties.SetName(send, "Senden");
        send.Click += async (_, _) => { if (_kiInput.IsEnabled && !string.IsNullOrWhiteSpace(_kiInput.Text)) await AskKi(_kiInput.Text!); };
        var inputBox = new Border { Height = 42, CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#990A1630", "#CCFFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), Padding = new Thickness(8, 0),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = { _kiInput, Pos(_kiSetup, 1), Pos(send, 2) } } };
        _kiSetup.Margin = new Thickness(0, 0, 4, 0); _kiSetup.VerticalAlignment = VerticalAlignment.Center; _kiSetup.Background = Accent; _kiSetup.Padding = new Thickness(8, 2);
        _kiText.FontSize = 13; _kiText.TextWrapping = TextWrapping.Wrap;
        var chat = new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14), MinHeight = 160, Child = new ScrollViewer { Content = _kiText } };
        var note = P(_kiConnected ? $"Die KI läuft auf diesem PC (Modell {KiModelName}). Nichts geht ins Internet, nichts kostet extra." : "Die KI ist noch nicht eingerichtet. Unter „Zusatzprogramme“ installiert Trendo sie kostenlos mit einem Klick.", Text);
        var center = new StackPanel { Spacing = 10, Children = { chat, note, Nav2("Zusatzprogramme öffnen", "components") } };
        var side = new Border { Background = Brush.Parse(C("#55101830", "#CCFFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Child = new StackPanel { Spacing = 8, Children = { H("Modell und Status", 14), state, KiModelChooser() } } };
        var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*,240"), ColumnSpacing = 12 };
        var navBox = new Border { Background = Brush.Parse(C("#55101830", "#CCFFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(8), Child = KiNav(vertical: true) };
        cols.Children.Add(navBox); Grid.SetColumn(center, 1); cols.Children.Add(center); Grid.SetColumn(side, 2); cols.Children.Add(side);
        return Page("KI", cols, inputBox);
    }

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

    private bool _kiBusy;
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

    /// <summary>Eigenes Fenster, nicht die Studio-Übersicht und nicht der nackte Startbildschirm.</summary>
    public bool HasStudioWorkspace(string id)
    {
        var page = PageForStudio(id);
        return page is not null && page is not "start" and not "studios";
    }

    /// <summary>Seite, die das Startmenü für dieses Studio öffnet. Ohne Arbeitsbereich: ehrliche Statusseite im eigenen Fenster.</summary>
    public string StudioLaunchPage(string id) => HasStudioWorkspace(id) ? PageForStudio(id)! : EnsureStudioStatus(id);

    private string EnsureStudioStatus(string id)
    {
        var key = "studio-status-" + id;
        if (!_showMap.ContainsKey(key))
        {
            var sid = id;
            _showMap[key] = () => StudioStatusView(sid);
            _windowGroups[key] = key;
        }
        return key;
    }

    private StudioEntry? StudioEntryOf(string id) =>
        _studios.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Kurzer ehrlicher Status. Nicht gebaute Studios heißen „Noch nicht verfügbar“.</summary>
    public string StudioStatusLabel(string id) => StudioEntryOf(id) switch
    {
        null => "Noch nicht verfügbar",
        { Capability: Capability.Ready } => "Bereit",
        { Capability: Capability.InProgress } => "In Arbeit",
        { Capability: Capability.Blocked } => "Braucht ein Zusatzprogramm",
        _ => "Noch nicht verfügbar",
    };

    private Control StudioStatusView(string id)
    {
        var info = StudioCatalog.For(id);
        var st = StudioEntryOf(id);
        var name = info?.Name ?? st?.Name ?? id;
        var label = new TextBlock { Name = "studio-status-label", Text = StudioStatusLabel(id), Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold };
        var sentence = new TextBlock { Name = "studio-status-sentence", Text = info?.Sentence ?? "", Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        var reason = new TextBlock { Name = "studio-status-reason", Text = st?.Reason ?? "Für dieses Studio gibt es noch keinen eigenen Arbeitsbereich.", Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        var box = new Border { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child = new StackPanel { Spacing = 8, Children = {
            label, sentence, reason,
            P("Es öffnet sich hier keine leere Anwendung und nicht die Liste aller Studios.", null, 12) } } };
        ToolTip.SetTip(box, st is null ? name : $"{id} · {st.ImplementationStatus} · {st.ApprovalStatus}");
        return Page(name, box);
    }

    /// <summary>Startmenü und angeheftete Taskleiste: offenes Studio nach vorn holen, sonst öffnen. Kein zweites Fenster, keine verworfene Eingabe.</summary>
    public async Task ActivateOrOpenStudio(string id, string title)
    {
        var page = StudioLaunchPage(id);
        var group = WindowGroupOf(page);
        if (Windows.Get(group) is not null && _frames.ContainsKey(group))
        {
            Windows.Activate(group);
            AfterWindowChange();
            if (Windows.Get(group) is { } open) NoteRecent(open.Page);
            return;
        }
        await OpenStudio(id, title);
    }

    public async Task OpenStudio(string id, string title)
    {
        _core.Audit.Append(Principal.LocalOwner.Name, "studio.open", id);
        if (!HasStudioWorkspace(id)) { Show(EnsureStudioStatus(id)); return; }
        var page = PageForStudio(id)!;
        // 3D lässt sich ohne Blender bauen und ansehen. Blender wird erst beim Öffnen gefragt.
        if (string.Equals(id, "CORE-05", StringComparison.OrdinalIgnoreCase)) { Show(page); return; }
        if (!StudioTools.Required.TryGetValue(id, out var need)) { Show(page); return; }
        _depReturn = page;
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
