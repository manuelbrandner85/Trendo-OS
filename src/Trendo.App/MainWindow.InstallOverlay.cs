using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Trendo.App;

/// <summary>Einrichtung im Vollbild (Vorgabe 2026-10-26): Nach „Zustimmen & installieren“ deckt eine animierte Fläche das ganze
/// Fenster ab, zeigt Schritt und Fortschritt und lässt sich nicht wegklicken, mit Tasten umgehen oder schließen, bis alles eingerichtet ist.</summary>
public partial class MainWindow
{
    private readonly Grid _setupOverlay = new() { Name = "setup-overlay", IsVisible = false, ZIndex = 5000, Focusable = true,
        Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#FA0E1F45"), 0), new GradientStop(Color.Parse("#FC050B1A"), 0.6), new GradientStop(Color.Parse("#FE02060F"), 1) } } };
    private readonly TextBlock _setupTitle = new() { Name = "setup-overlay-title", FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _setupStep = new() { Name = "setup-overlay-step", FontSize = 14, Foreground = Brush.Parse("#BFD4F5"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 560 };
    private readonly TextBlock _setupPercent = new() { Name = "setup-overlay-percent", FontSize = 13, Foreground = Brush.Parse("#7CB8FF"), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ProgressBar _setupBar = new() { Name = "setup-overlay-bar", Minimum = 0, Maximum = 1, Height = 8, Width = 460, CornerRadius = new CornerRadius(4), Foreground = Brush.Parse("#3B82F6"), Background = Brush.Parse("#22FFFFFF") };
    private readonly Canvas _setupScene = new() { Width = 360, Height = 360, ClipToBounds = false };
    private readonly List<(Control Dot, double Radius, double Speed, double Phase, double Tilt)> _setupDots = [];
    private readonly List<(Control Ring, double Speed)> _setupRings = [];
    private Control? _setupLogo;
    private DispatcherTimer? _setupTimer;
    private DateTime _setupStart;
    private WindowState _setupPrevState = WindowState.Normal;

    /// <summary>Läuft gerade eine Einrichtung im Vollbild? Dann sind Schließen, Klicks und Tasten gesperrt.</summary>
    public bool SetupOverlayOpen => _setupOverlay.IsVisible;

    /// <summary>Schließen durch den Nutzer (Alt+F4, X) wartet, bis die Einrichtung fertig ist; Trendo selbst darf schließen.</summary>
    public bool MayClose(bool programmatic) => programmatic || !SetupOverlayOpen || SetupFinishShown;

    private void InitSetupOverlay(Grid root)
    {
        Grid.SetRowSpan(_setupOverlay, 2);
        root.Children.Add(_setupOverlay);
        var rnd = new Random(7);
        foreach (var (r, speed, w) in new[] { (150.0, 0.22, 1.2), (118.0, -0.35, 0.8), (86.0, 0.5, 1.6) })
        {
            var ring = new Ellipse { Width = r * 2, Height = r * 2, Stroke = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#AA7CB8FF"), 0), new GradientStop(Color.Parse("#103B82F6"), 0.5), new GradientStop(Color.Parse("#AA60A5FA"), 1) },
            }, StrokeThickness = w, StrokeDashArray = speed > 0 ? null : [6, 8], RenderTransform = new RotateTransform(0), RenderTransformOrigin = RelativePoint.Center };
            Canvas.SetLeft(ring, 180 - r); Canvas.SetTop(ring, 180 - r);
            _setupScene.Children.Add(ring); _setupRings.Add((ring, speed));
        }
        for (var i = 0; i < 48; i++)
        {
            var size = 2 + rnd.NextDouble() * 3.5;
            var dot = new Ellipse { Width = size, Height = size, Fill = Brush.Parse(i % 3 == 0 ? "#E0F2FF" : i % 3 == 1 ? "#7CB8FF" : "#3B82F6"), IsHitTestVisible = false };
            _setupScene.Children.Add(dot);
            _setupDots.Add((dot, 70 + rnd.NextDouble() * 105, (0.25 + rnd.NextDouble() * 0.9) * (i % 2 == 0 ? 1 : -1), rnd.NextDouble() * Math.PI * 2, 0.35 + rnd.NextDouble() * 0.6));
        }
        _setupLogo = Logo(120, glow: true);
        Canvas.SetLeft(_setupLogo, 120); Canvas.SetTop(_setupLogo, 120);
        _setupLogo.RenderTransform = new ScaleTransform(1, 1); _setupLogo.RenderTransformOrigin = RelativePoint.Center;
        _setupScene.Children.Add(_setupLogo);
        _setupOverlay.Children.Add(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 14,
            Children =
            {
                _setupScene, _setupTitle, _setupStep, _setupBar, _setupPercent,
                new TextBlock { Text = "Bitte warte kurz – Trendo richtet alles für dich ein. Das Fenster lässt sich schließen, sobald alles fertig ist.", FontSize = 12, Foreground = Brush.Parse("#8093B3"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
            },
        });
        // Nichts geht durch: Zeiger und Tasten enden an der Fläche, Schließen wartet bis zum Ende.
        _setupOverlay.PointerPressed += (_, e) => e.Handled = true;
        _setupOverlay.PointerWheelChanged += (_, e) => e.Handled = true;
        AddHandler(KeyDownEvent, (_, e) => { if (SetupOverlayOpen && !SetupFinishShown) e.Handled = true; }, RoutingStrategies.Tunnel, handledEventsToo: true);
        Closing += (_, e) => { if (!MayClose(e.IsProgrammatic)) e.Cancel = true; };
    }

    private void BeginSetupOverlay(string title)
    {
        if (_setupDoneButton is not null) { _setupDoneButton.IsVisible = false; _setupSummary!.IsVisible = false; }
        _setupTitle.Text = title; _setupStep.Text = "Vorbereitung …"; _setupBar.Value = 0; _setupBar.IsIndeterminate = true; _setupPercent.Text = "";
        _setupOverlay.IsVisible = true; _setupOverlay.Focus();
        if (WindowState != WindowState.FullScreen) { _setupPrevState = WindowState; WindowState = WindowState.FullScreen; } // echtes Vollbild
        CloseStartMenu();
        _setupStart = DateTime.UtcNow;
        if (Motionless) { AnimateSetup(0.8); return; } // ruhiges Standbild bei „Bewegung reduzieren“ und schwachen Geräten
        _setupTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => AnimateSetup((DateTime.UtcNow - _setupStart).TotalSeconds));
        _setupTimer.Start();
    }

    /// <summary>Fortschritt: Schritt <paramref name="done"/> von <paramref name="total"/> (0 = noch unbekannt) mit Text.</summary>
    private void SetupOverlayProgress(int done, int total, string text)
    {
        _setupStep.Text = text;
        if (total <= 0) { _setupBar.IsIndeterminate = true; _setupPercent.Text = ""; return; }
        _setupBar.IsIndeterminate = false;
        var v = Math.Clamp(done / (double)total, 0, 1);
        _setupBar.Value = v; _setupPercent.Text = $"{done} von {total} · {v:P0}";
    }

    /// <summary>Für Bildschirmfotos und Tests: Einrichtung mit festem Stand zeigen, Animation an Zeitpunkt <paramref name="t"/>.</summary>
    public void ShowSetupOverlay(string title, int done, int total, string text, double t = 2.4)
    {
        BeginSetupOverlay(title); _setupTimer?.Stop(); SetupOverlayProgress(done, total, text); AnimateSetup(t);
    }

    public void HideSetupOverlay() => EndSetupOverlay();

    // Befund 27.10.2026: Der Einrichtungsbildschirm war zu kurz zu sehen. Am Ende bleibt er mit dem Ergebnis stehen, bis „Los geht's“ gedrückt wird.
    private Button? _setupDoneButton;
    private TextBlock? _setupSummary;
    public bool SetupFinishShown => _setupDoneButton?.IsVisible == true && _setupOverlay.IsVisible;

    /// <summary>Ergebnis zeigen: Fortschritt auf 100 %, Zusammenfassung und was nicht geklappt hat. Schließen per Knopf (oder Enter).</summary>
    private void FinishSetupOverlay(string title, string summary, IReadOnlyList<string> problems)
    {
        if (!_setupOverlay.IsVisible) return;
        _setupTitle.Text = title; _setupStep.Text = summary;
        _setupBar.IsIndeterminate = false; _setupBar.Value = 1; _setupPercent.Text = problems.Count == 0 ? "Fertig · 100 %" : $"Fertig · {problems.Count} Hinweis(e)";
        if (_setupSummary is null)
        {
            _setupSummary = new TextBlock { Name = "setup-overlay-problems", FontSize = 12.5, Foreground = Brush.Parse("#FFD28A"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 620 };
            _setupDoneButton = new Button { Name = "setup-overlay-done", Content = "Los geht's", FontSize = 15, Padding = new Thickness(28, 10), CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brush.Parse("#3B82F6"), Foreground = Brushes.White };
            _setupDoneButton.Click += (_, _) => { _setupDoneButton.IsVisible = false; _setupSummary.IsVisible = false; EndSetupOverlay(); };
            if (_setupOverlay.Children.OfType<StackPanel>().FirstOrDefault() is { } col) { col.Children.Add(_setupSummary); col.Children.Add(_setupDoneButton); }
        }
        _setupSummary.Text = problems.Count == 0 ? "" : "Nicht eingerichtet:\n" + string.Join("\n", problems.Take(8)) + (problems.Count > 8 ? $"\n… und {problems.Count - 8} weitere" : "") + "\n\nUnter „Programme“ kannst du es später erneut versuchen.";
        _setupSummary.IsVisible = problems.Count > 0;
        _setupDoneButton!.IsVisible = true; _setupDoneButton.Focus();
    }

    private void EndSetupOverlay()
    {
        _setupTimer?.Stop();
        if (!_setupOverlay.IsVisible) return;
        _setupOverlay.IsVisible = false;
        if (WindowState == WindowState.FullScreen) WindowState = _setupPrevState;
    }

    private void AnimateSetup(double t)
    {
        foreach (var (ring, speed) in _setupRings) ((RotateTransform)ring.RenderTransform!).Angle = t * speed * 60;
        foreach (var (dot, r, speed, phase, tilt) in _setupDots)
        {
            var a = phase + t * speed;
            var x = 180 + Math.Cos(a) * r; var y = 180 + Math.Sin(a) * r * tilt;
            Canvas.SetLeft(dot, x - dot.Width / 2); Canvas.SetTop(dot, y - dot.Height / 2);
            dot.Opacity = 0.35 + 0.65 * (0.5 + 0.5 * Math.Sin(a)); // vorne hell, hinten blass: Tiefe
        }
        if (_setupLogo?.RenderTransform is ScaleTransform s) s.ScaleX = s.ScaleY = 1 + 0.04 * Math.Sin(t * 2.2);
    }
}
