using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Einrichtung im Vollbild (Vorgabe 2026-10-26): Nach „Zustimmen & installieren“ deckt eine animierte Fläche das ganze
/// Fenster ab, zeigt Schritt und Fortschritt und lässt sich nicht wegklicken, mit Tasten umgehen oder schließen, bis alles eingerichtet ist.</summary>
public partial class MainWindow
{
    private readonly Grid _setupOverlay = new() { Name = "setup-overlay", IsVisible = false, ZIndex = 5000, Focusable = true,
        Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#FF0E1F45"), 0), new GradientStop(Color.Parse("#FF050B1A"), 0.6), new GradientStop(Color.Parse("#FF02060F"), 1) } } };
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
    // Wunsch 27.10.2026: laufende Installation abbrechen können; danach schließt der Bildschirm.
    private CancellationTokenSource? _setupCts;
    private Button? _setupCancel;
    private Control? _setupSpark, _setupHalo;
    private readonly Canvas _setupWaves = new() { Name = "setup-waves" };
    private double _setupLastT;
    private readonly List<Avalonia.Controls.Shapes.Path> _setupWaveLines = [];

    /// <summary>Eine Welle als Linienzug über die ganze Breite: Höhe, Wellenlänge und Tempo je Linie verschieden, langsam bewegt.</summary>
    static Geometry Wave(double width, double height, int i, double t)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            var baseY = height * (0.62 + i * 0.07); var amp = 18 + i * 7; var len = 520 + i * 140; var speed = 0.25 + i * 0.07;
            for (var x = 0.0; x <= width + 20; x += 16)
            {
                var y = baseY + Math.Sin(x / len * Math.PI * 2 + t * speed + i) * amp + Math.Sin(x / (len * 0.43) - t * speed * 0.6) * amp * 0.35;
                if (x == 0) c.BeginFigure(new Point(x, y), false); else c.LineTo(new Point(x, y));
            }
            c.EndFigure(false);
        }
        return g;
    }
    /// <summary>Abbruch der laufenden Einrichtung (Downloads und Paket-Schleifen fragen das ab).</summary>
    public CancellationToken SetupToken => _setupCts?.Token ?? CancellationToken.None;
    public bool SetupCancelRequested => _setupCts?.IsCancellationRequested == true;

    /// <summary>„Abbrechen“: Token setzen, Bildschirm schließen, kurze Meldung. Schon fertig Eingerichtetes bleibt.</summary>
    public void CancelSetup()
    {
        if (_setupCts is null || _setupCts.IsCancellationRequested || !_setupOverlay.IsVisible) return;
        _setupCts.Cancel();
        _core.Audit.Append(Principal.LocalOwner.Name, "setup.canceled", _setupTitle.Text ?? "");
        EndSetupOverlay();
        Toast("Installation abgebrochen. Was schon fertig war, bleibt eingerichtet – unter „Programme“ kannst du später weitermachen.");
    }
    private WindowState _setupPrevState = WindowState.Normal;

    /// <summary>Läuft gerade eine Einrichtung im Vollbild? Dann sind Schließen, Klicks und Tasten gesperrt.</summary>
    public bool SetupOverlayOpen => _setupOverlay.IsVisible;

    /// <summary>Schließen durch den Nutzer (Alt+F4, X) wartet, bis die Einrichtung fertig ist; Trendo selbst darf schließen.</summary>
    public bool MayClose(bool programmatic) => programmatic || !SetupOverlayOpen || SetupFinishShown;

    /// <summary>Goldverlauf für Schriftzüge (Logo C „Gold-Orbit“).</summary>
    internal static IBrush GoldText() => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#FFF1C4"), 0), new GradientStop(Color.Parse("#E8B64C"), 0.5), new GradientStop(Color.Parse("#A8741E"), 1) },
    };

    private void InitSetupOverlay(Grid root)
    {
        Grid.SetRowSpan(_setupOverlay, 2);
        // Wunsch 27.10.2026: kein Hintergrundbild, sondern sanft bewegte Wellen in Gold und Blau
        if (_theme != "kontrast")
        {
            _setupWaves.IsHitTestVisible = false;
            foreach (var (color, opacity, thick) in new[] { ("#3B82F6", 0.22, 1.6), ("#F5C451", 0.18, 1.2), ("#5BA8FF", 0.14, 2.2), ("#E8B64C", 0.12, 1.0), ("#2563EB", 0.10, 2.8) })
            {
                var w = new Avalonia.Controls.Shapes.Path { Stroke = Brush.Parse(color), StrokeThickness = thick, Opacity = opacity, IsHitTestVisible = false };
                _setupWaveLines.Add(w); _setupWaves.Children.Add(w);
            }
            _setupOverlay.Children.Add(_setupWaves);
            _setupOverlay.SizeChanged += (_, _) => AnimateSetup(_setupLastT); // Standbild (Bewegung reduziert) bekommt die Wellen in voller Breite
        }
        if (_theme != "kontrast") _setupTitle.Foreground = GoldText();
        if (_theme != "kontrast") _setupBar.Foreground = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#3B82F6"), 0), new GradientStop(Color.Parse("#F5C451"), 0.7), new GradientStop(Color.Parse("#FFF1B8"), 1) } };
        root.Children.Add(_setupOverlay);
        var rnd = new Random(7);
        // Gold-Blau wie Logo C: pulsierendes Goldleuchten, zwei feine Kreise, ein schräger Orbit wie im Logo mit kreisendem Lichtfunken
        _setupHalo = new Ellipse { Width = 260, Height = 260, IsHitTestVisible = false,
            Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#66F5C451"), 0), new GradientStop(Color.Parse("#333B82F6"), 0.55), new GradientStop(Color.Parse("#003B82F6"), 1) } } };
        Canvas.SetLeft(_setupHalo, 50); Canvas.SetTop(_setupHalo, 50); _setupScene.Children.Add(_setupHalo);
        foreach (var (r, speed, w) in new[] { (158.0, 0.18, 1.0), (126.0, -0.3, 0.8) })
        {
            var ring = new Ellipse { Width = r * 2, Height = r * 2, Stroke = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#AAF5C451"), 0), new GradientStop(Color.Parse("#103B82F6"), 0.5), new GradientStop(Color.Parse("#AA5BA8FF"), 1) },
            }, StrokeThickness = w, StrokeDashArray = speed > 0 ? null : [6, 8], RenderTransform = new RotateTransform(0), RenderTransformOrigin = RelativePoint.Center };
            Canvas.SetLeft(ring, 180 - r); Canvas.SetTop(ring, 180 - r);
            _setupScene.Children.Add(ring); _setupRings.Add((ring, speed));
        }
        var orbit = new Ellipse { Width = 330, Height = 96, StrokeThickness = 2.2, IsHitTestVisible = false, RenderTransform = new RotateTransform(-12), RenderTransformOrigin = RelativePoint.Center,
            Stroke = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#CC3B82F6"), 0), new GradientStop(Color.Parse("#CCF5C451"), 0.55), new GradientStop(Color.Parse("#EEFFF1B8"), 1) } } };
        Canvas.SetLeft(orbit, 15); Canvas.SetTop(orbit, 132); _setupScene.Children.Add(orbit);
        for (var i = 0; i < 48; i++)
        {
            var size = 2 + rnd.NextDouble() * 3.5;
            var dot = new Ellipse { Width = size, Height = size, Fill = Brush.Parse(i % 3 == 0 ? "#FFF1B8" : i % 3 == 1 ? "#F5C451" : "#5BA8FF"), IsHitTestVisible = false };
            _setupScene.Children.Add(dot);
            _setupDots.Add((dot, 70 + rnd.NextDouble() * 105, (0.25 + rnd.NextDouble() * 0.9) * (i % 2 == 0 ? 1 : -1), rnd.NextDouble() * Math.PI * 2, 0.35 + rnd.NextDouble() * 0.6));
        }
        _setupLogo = Logo(170, glow: false, tile: false);
        Canvas.SetLeft(_setupLogo, 95); Canvas.SetTop(_setupLogo, 95);
        _setupLogo.RenderTransform = new ScaleTransform(1, 1); _setupLogo.RenderTransformOrigin = RelativePoint.Center;
        _setupScene.Children.Add(_setupLogo);
        // vorderer Teil des Orbits liegt vor dem Logo (wie im Logo), Funke kreist darauf
        // vordere Hälfte des Orbits, gleiche Ellipse wie oben (165 × 48, −12°), aus Punkten berechnet
        var pts = new List<Point>();
        for (var k = 0; k <= 48; k++) { var a = Math.PI * k / 48; pts.Add(OrbitPoint(a)); }
        var front = new Polyline { Points = pts, StrokeThickness = 2.6, IsHitTestVisible = false, StrokeLineCap = PenLineCap.Round,
            Stroke = new LinearGradientBrush { StartPoint = new RelativePoint(1, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#FFFFF1B8"), 0), new GradientStop(Color.Parse("#EEF5C451"), 0.4), new GradientStop(Color.Parse("#DD3B82F6"), 1) } } };
        _setupScene.Children.Add(front);
        _setupSpark = new Ellipse { Width = 9, Height = 9, Fill = Brushes.White, IsHitTestVisible = false, Effect = new DropShadowEffect { Color = Color.Parse("#F5C451"), BlurRadius = 14, OffsetX = 0, OffsetY = 0, Opacity = 1 } };
        _setupScene.Children.Add(_setupSpark);
        _setupCancel = new Button { Name = "setup-overlay-cancel", Content = "Abbrechen", FontSize = 13, Padding = new Thickness(22, 7), CornerRadius = new CornerRadius(10), HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brush.Parse("#22FFFFFF"), Foreground = Brush.Parse("#E6EDF7"), BorderBrush = Brush.Parse("#66F5C451"), BorderThickness = new Thickness(1) };
        ToolTip.SetTip(_setupCancel, "Laufende Installation abbrechen – schon Eingerichtetes bleibt");
        _setupCancel.Click += (_, _) => CancelSetup();
        _setupOverlay.Children.Add(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 14,
            Children =
            {
                _setupScene, _setupTitle, _setupStep, _setupBar, _setupPercent,
                new TextBlock { Text = "Bitte warte kurz – Trendo richtet alles für dich ein. Mit „Abbrechen“ hörst du jederzeit auf.", FontSize = 12, Foreground = Brush.Parse("#8093B3"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
                _setupCancel,
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
        _setupCts?.Dispose(); _setupCts = new CancellationTokenSource();
        if (_setupCancel is not null) _setupCancel.IsVisible = true;
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
        if (!_setupOverlay.IsVisible || SetupCancelRequested) return;
        if (_setupCancel is not null) _setupCancel.IsVisible = false;
        _setupTitle.Text = title; _setupStep.Text = summary;
        _setupBar.IsIndeterminate = false; _setupBar.Value = 1; _setupPercent.Text = problems.Count == 0 ? "Fertig · 100 %" : $"Fertig · {problems.Count} Hinweis(e)";
        if (_setupSummary is null)
        {
            _setupSummary = new TextBlock { Name = "setup-overlay-problems", FontSize = 12.5, Foreground = Brush.Parse("#FFD28A"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 620 };
            _setupDoneButton = new Button { Name = "setup-overlay-done", Content = "Los geht's", FontSize = 15, Padding = new Thickness(28, 10), CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Center,
                Background = GoldText(), Foreground = Brush.Parse("#1A1206"), FontWeight = FontWeight.Bold };
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

    /// <summary>Punkt auf dem schrägen Orbit (Ellipse 165 × 48 um (180|180), −12° gedreht); a in Bogenmaß, 0…π ist die vordere Hälfte.</summary>
    static Point OrbitPoint(double a)
    {
        double ex = Math.Cos(a) * 165, ey = Math.Sin(a) * 48, r = -12 * Math.PI / 180;
        return new(180 + ex * Math.Cos(r) - ey * Math.Sin(r), 180 + ex * Math.Sin(r) + ey * Math.Cos(r));
    }

    private void AnimateSetup(double t)
    {
        _setupLastT = t;
        var (ww, wh) = _setupOverlay.Bounds.Width > 0 ? (_setupOverlay.Bounds.Width, _setupOverlay.Bounds.Height) : (Bounds.Width, Bounds.Height);
        if (ww > 0 && wh > 0) for (var i = 0; i < _setupWaveLines.Count; i++) _setupWaveLines[i].Data = Wave(ww, wh, i, t);
        foreach (var (ring, speed) in _setupRings) ((RotateTransform)ring.RenderTransform!).Angle = t * speed * 60;
        foreach (var (dot, r, speed, phase, tilt) in _setupDots)
        {
            var a = phase + t * speed;
            var x = 180 + Math.Cos(a) * r; var y = 180 + Math.Sin(a) * r * tilt;
            Canvas.SetLeft(dot, x - dot.Width / 2); Canvas.SetTop(dot, y - dot.Height / 2);
            dot.Opacity = 0.35 + 0.65 * (0.5 + 0.5 * Math.Sin(a)); // vorne hell, hinten blass: Tiefe
        }
        if (_setupLogo?.RenderTransform is ScaleTransform s) s.ScaleX = s.ScaleY = 1 + 0.035 * Math.Sin(t * 2.2);
        if (_setupHalo is not null) _setupHalo.Opacity = 0.65 + 0.35 * Math.Sin(t * 1.6);
        if (_setupSpark is not null)
        {
            // Funke auf dem schrägen Orbit (Ellipse 165 × 48 um die Mitte, −12° gedreht)
            var a = t * 1.3; var p = OrbitPoint(a);
            Canvas.SetLeft(_setupSpark, p.X - 4.5); Canvas.SetTop(_setupSpark, p.Y - 4.5);
            _setupSpark.ZIndex = Math.Sin(a) > 0 ? 10 : 0; // vorne vor dem Logo, hinten dahinter
            _setupSpark.Opacity = 0.55 + 0.45 * (0.5 + 0.5 * Math.Sin(a));
        }
    }
}
