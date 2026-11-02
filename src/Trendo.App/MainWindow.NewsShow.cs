using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Nach einem Update (Wunsch 01.11.2026): Vollbild „Das ist neu“ – jede Neuerung als eigene Folie, animiert eingeblendet, läuft von selbst weiter.
/// Nicht wegklickbar: kein Esc, kein Klick daneben; mit ‹ › oder den Pfeiltasten schneller blättern, „Los geht's“ erst auf der letzten Folie.
/// Vor dem automatischen Neustart zeigt eine Leiste den Countdown – alles ist vorher gesichert.</summary>
public partial class MainWindow
{
    readonly Grid _news = new() { Name = "news-show", IsVisible = false, ZIndex = 5200, Focusable = true };
    readonly Border _newsCard = new() { Name = "news-slide", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 980, Padding = new Thickness(40, 20) };
    readonly TextBlock _newsCount = new() { Name = "news-count", Foreground = Brush.Parse("#9FB3D6"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
    readonly ProgressBar _newsTime = new() { Name = "news-timer", Minimum = 0, Maximum = 1, Height = 3, Width = 520, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Center };
    readonly ProgressBar _newsAll = new() { Name = "news-progress", Minimum = 0, Maximum = 1, Height = 5, Width = 520, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Center };
    readonly Button _newsPrev = new() { Name = "news-prev", Content = "‹", FontSize = 30, Width = 58, Height = 58, CornerRadius = new CornerRadius(29), Background = Brush.Parse("#22FFFFFF"), Foreground = Brushes.White, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0, -4, 0, 0) };
    readonly Button _newsNext = new() { Name = "news-next", Content = "›", FontSize = 30, Width = 58, Height = 58, CornerRadius = new CornerRadius(29), Background = Brush.Parse("#22FFFFFF"), Foreground = Brushes.White, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0, -4, 0, 0) };
    readonly Button _newsDone = new() { Name = "news-done", Content = "Los geht's", FontSize = 20, FontWeight = FontWeight.SemiBold, Padding = new Thickness(38, 12), CornerRadius = new CornerRadius(26), Foreground = Brush.Parse("#1B1404"), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center };
    readonly Canvas _newsGlow = new() { IsHitTestVisible = false };
    readonly List<(Ellipse E, double Speed, double Phase, double Radius)> _newsOrbs = [];
    DispatcherTimer? _newsTimer; DateTime _newsShown; TimeSpan _newsElapsed; WindowState _newsPrevState = WindowState.Normal;
    string _newsVersion = "";

    public bool NewsOpen => _news.IsVisible;
    /// <summary>-1 = Begrüßung, 0 … n-1 = Neuerungen.</summary>
    public int NewsIndex { get; private set; } = -1;
    public IReadOnlyList<NewsSlide> NewsSlides { get; private set; } = [];
    public bool NewsLast => NewsIndex >= NewsSlides.Count - 1;
    /// <summary>Für Tests: Zeit läuft nur, wenn sie hier weitergedreht wird (statt mit der Uhr).</summary>
    public bool NewsManualClock { get; set; }

    private void InitNewsShow(Grid root)
    {
        Grid.SetRowSpan(_news, 2);
        _news.Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#070E22"), 0), new GradientStop(Color.Parse("#0F2350"), 0.55), new GradientStop(Color.Parse("#1A1238"), 1) } };
        // ruhiges Licht im Hintergrund: Gold und Blau, langsam kreisend
        foreach (var (c, size, speed, phase, r) in new[] { ("#55F5C451", 620.0, 0.05, 0.0, 0.32), ("#663B82F6", 760.0, -0.04, 2.1, 0.36), ("#335BA8FF", 520.0, 0.07, 4.0, 0.28) })
        {
            var e = new Ellipse { Width = size, Height = size, Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse(c), 0), new GradientStop(Color.Parse("#00000000"), 1) } } };
            _newsGlow.Children.Add(e); _newsOrbs.Add((e, speed, phase, r));
        }
        _news.Children.Add(_newsGlow);
        if (_theme != "kontrast")
        {
            _newsDone.Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#FFF1C4"), 0), new GradientStop(Color.Parse("#E8B64C"), 0.55), new GradientStop(Color.Parse("#B8862A"), 1) } };
            _newsAll.Foreground = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#3B82F6"), 0), new GradientStop(Color.Parse("#F5C451"), 1) } };
        }
        else _newsDone.Background = Brushes.White;
        _newsTime.Foreground = Brush.Parse("#88F5C451"); _newsTime.Background = Brush.Parse("#14FFFFFF"); _newsAll.Background = Brush.Parse("#1FFFFFFF");
        _newsPrev.Click += (_, _) => NewsGo(NewsIndex - 1);
        _newsNext.Click += (_, _) => NewsGo(NewsIndex + 1);
        _newsDone.Click += (_, _) => NewsClose();
        ToolTip.SetTip(_newsPrev, "Zurück (←)"); ToolTip.SetTip(_newsNext, "Weiter (→ oder Leertaste)");
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(36, 26, 36, 0), VerticalAlignment = VerticalAlignment.Top,
            Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { Logo(34, glow: false, tile: false), new TextBlock { Name = "news-head", Text = "Das ist neu", Foreground = Brush.Parse("#DCE6F5"), FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } },
                Pos(_newsCount, 2) } };
        var bottom = new StackPanel { Spacing = 14, Margin = new Thickness(0, 0, 0, 34), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22, HorizontalAlignment = HorizontalAlignment.Center, Children = { _newsPrev, _newsDone, _newsNext } },
            _newsTime, _newsAll,
            new TextBlock { Text = "Läuft von selbst weiter · ← → zum Blättern", Foreground = Brush.Parse("#6F84A8"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center } } };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var fit = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(24, 10), Child = _newsCard }; // viele Punkte: Folie wird kleiner statt abgeschnitten
        Grid.SetRow(fit, 1); Grid.SetRow(bottom, 2); bottom.Margin = new Thickness(0, 10, 0, 30);
        layout.Children.Add(top); layout.Children.Add(fit); layout.Children.Add(bottom);
        _news.Children.Add(layout);
        // nicht wegklickbar: Zeiger enden an der Fläche, Tasten nur zum Blättern
        _news.PointerPressed += (_, e) => e.Handled = true;
        _news.PointerWheelChanged += (_, e) => { if (e.Delta.Y < 0) NewsGo(NewsIndex + 1); else if (e.Delta.Y > 0) NewsGo(NewsIndex - 1); e.Handled = true; };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!NewsOpen) return;
            switch (e.Key)
            {
                case Key.Right or Key.Space or Key.PageDown or Key.Down: NewsGo(NewsIndex + 1); break;
                case Key.Left or Key.PageUp or Key.Up: NewsGo(NewsIndex - 1); break;
                case Key.Enter: if (NewsLast) NewsClose(); else NewsGo(NewsIndex + 1); break;
            }
            e.Handled = true;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        _news.SizeChanged += (_, _) => NewsGlow(0);
        root.Children.Add(_news);
        InitUpdateCountdown(root);
    }

    /// <summary>Vorstellung öffnen (nach dem Update; öffentlich für Tests).</summary>
    public void ShowNews(string version, string notes)
    {
        NewsSlides = NewsShow.Slides(notes, version);
        if (NewsSlides.Count == 0) NewsSlides = NewsShow.Slides(notes);
        if (NewsSlides.Count == 0) return;
        _newsVersion = version;
        _news.IsVisible = true; _news.Focus();
        if (WindowState != WindowState.FullScreen) { _newsPrevState = WindowState; WindowState = WindowState.FullScreen; }
        CloseStartMenu();
        _core.Audit.Append("app", "news.show", $"{version} folien={NewsSlides.Count}");
        NewsIndex = -2; NewsGo(-1);
        if (_newsTimer is null)
        {
            _newsTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) => { if (!NewsManualClock) NewsTick(DateTime.UtcNow - _newsShown - _newsElapsed); });
            _newsTimer.Start();
        }
    }

    /// <summary>Zeit weiterdrehen: Fortschritt der Folie, nach ihrer Dauer automatisch weiter (auf der letzten bleibt sie stehen).</summary>
    public void NewsTick(TimeSpan by)
    {
        if (!NewsOpen) return;
        _newsElapsed += by;
        var dur = NewsIndex < 0 ? TimeSpan.FromSeconds(4) : NewsSlides[NewsIndex].Duration;
        _newsTime.Value = Math.Clamp(_newsElapsed / dur, 0, 1);
        if (!Motionless) NewsGlow((DateTime.UtcNow - _newsShown).TotalSeconds + NewsIndex * 3);
        if (_newsElapsed >= dur && !NewsLast) NewsGo(NewsIndex + 1);
    }

    void NewsGlow(double t)
    {
        double w = _news.Bounds.Width, h = _news.Bounds.Height; if (w <= 0) return;
        foreach (var (e, speed, phase, r) in _newsOrbs)
        {
            var a = phase + t * speed * Math.PI * 2;
            Canvas.SetLeft(e, w / 2 + Math.Cos(a) * w * r - e.Width / 2); Canvas.SetTop(e, h / 2 + Math.Sin(a) * h * r - e.Height / 2);
        }
    }

    /// <summary>Zur Folie springen (-1 = Begrüßung) – mit Einblenden: Karte gleitet von unten herein, Symbol springt auf, Punkte folgen nacheinander.</summary>
    public void NewsGo(int index)
    {
        index = Math.Clamp(index, -1, NewsSlides.Count - 1);
        if (index == NewsIndex) return;
        NewsIndex = index; _newsShown = DateTime.UtcNow; _newsElapsed = TimeSpan.Zero; _newsTime.Value = 0;
        _newsCount.Text = index < 0 ? $"{NewsSlides.Count} Neuerungen" : $"{index + 1} / {NewsSlides.Count}";
        _newsAll.Value = (index + 1) / (double)NewsSlides.Count;
        _newsPrev.IsEnabled = index > -1; _newsNext.IsVisible = !NewsLast; _newsDone.IsVisible = NewsLast;
        var parts = new List<Control>();
        var sp = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center };
        if (index < 0)
        {
            var logo = Logo(150, glow: false, tile: false); logo.HorizontalAlignment = HorizontalAlignment.Center; sp.Children.Add(logo); parts.Add(logo);
            sp.Children.Add(Part(new TextBlock { Name = "news-title", Text = $"Trendo OS {_newsVersion}", Foreground = _theme == "kontrast" ? Brushes.White : GoldText(), FontSize = 52, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center }));
            sp.Children.Add(Part(new TextBlock { Name = "news-text", Text = $"Dein Update ist da – {NewsSlides.Count} Neuerungen warten auf dich.", Foreground = Brush.Parse("#DCE6F5"), FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }));
        }
        else
        {
            var s = NewsSlides[index];
            var icon = new Border { Width = 150, Height = 150, CornerRadius = new CornerRadius(75), HorizontalAlignment = HorizontalAlignment.Center,
                Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#44F5C451"), 0), new GradientStop(Color.Parse("#223B82F6"), 0.7), new GradientStop(Color.Parse("#003B82F6"), 1) } },
                BorderBrush = Brush.Parse("#55F5C451"), BorderThickness = new Thickness(1.5),
                Child = new TextBlock { Name = "news-icon", Text = s.Icon, FontSize = 70, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            sp.Children.Add(icon); parts.Add(icon);
            sp.Children.Add(Part(new TextBlock { Name = "news-title", Text = s.Title, Foreground = _theme == "kontrast" ? Brushes.White : GoldText(), FontSize = 42, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 900 }));
            if (s.Text.Length > 0) sp.Children.Add(Part(new TextBlock { Name = "news-text", Text = s.Points.Count > 0 ? s.Text.TrimEnd(':', ' ') : s.Text, Foreground = Brush.Parse("#DCE6F5"), FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 860, LineHeight = 32 }));
            if (s.Points.Count > 0)
            {
                var list = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
                var k = 0;
                foreach (var pt in s.Points.Take(7))
                    list.Children.Add(Part(new Border { Name = $"news-point-{k++}", Background = Brush.Parse("#14FFFFFF"), BorderBrush = Brush.Parse("#22F5C451"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 8), MaxWidth = 820,
                        Child = new TextBlock { Text = "✓  " + pt, Foreground = Brush.Parse("#EAF1FB"), FontSize = 17, TextWrapping = TextWrapping.Wrap } }));
                if (s.Points.Count > 7) list.Children.Add(Part(new TextBlock { Text = $"… und {s.Points.Count - 7} weitere", Foreground = Brush.Parse("#9FB3D6"), FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center }));
                sp.Children.Add(list);
            }
        }
        _newsCard.Child = sp;
        Control Part(Control c) { parts.Add(c); return c; }
        if (Motionless) return;
        // Einblenden: Startzustand ohne Übergang setzen, im nächsten Bild Übergänge an und Endzustand – nacheinander mit kleinem Versatz
        for (var i = 0; i < parts.Count; i++)
        {
            var c = parts[i]; var first = i == 0;
            c.Opacity = 0; c.RenderTransformOrigin = RelativePoint.Center;
            c.RenderTransform = TransformOperations.Parse(first ? "scale(0.55) rotate(-8deg)" : "translateY(36px)");
            var delay = TimeSpan.FromMilliseconds(first ? 0 : 140 + 110 * i);
            _ui.Post(() =>
            {
                c.Transitions = [
                    new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(520), Delay = delay, Easing = new CubicEaseOut() },
                    new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(first ? 760 : 620), Delay = delay, Easing = first ? new BackEaseOut() : new CubicEaseOut() } ];
                c.Opacity = 1; c.RenderTransform = TransformOperations.Parse("none");
            }, DispatcherPriority.Render);
        }
    }

    /// <summary>„Los geht's“: Vorstellung schließen (nur auf der letzten Folie), Vollbild zurück.</summary>
    public void NewsClose()
    {
        if (!NewsOpen || !NewsLast) return;
        _news.IsVisible = false; _newsCard.Child = null;
        _newsTimer?.Stop(); _newsTimer = null;
        if (WindowState == WindowState.FullScreen && !SetupOverlayOpen) WindowState = _newsPrevState;
        _core.Audit.Append(Principal.LocalOwner.Name, "news.done", _newsVersion);
    }

    // ---------- Countdown vor dem automatischen Update ----------
    readonly Border _updBar = new() { Name = "upd-countdown", IsVisible = false, ZIndex = 4900, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 80, 0, 0),
        CornerRadius = new CornerRadius(16), Padding = new Thickness(20, 12), BorderThickness = new Thickness(1), BorderBrush = Brush.Parse("#66F5C451"),
        Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#F0102347"), 0), new GradientStop(Color.Parse("#F01A1238"), 1) } },
        BoxShadow = BoxShadows.Parse("0 12 32 -8 #AA000000") };
    readonly TextBlock _updBarText = new() { Name = "upd-countdown-text", Foreground = Brushes.White, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
    DispatcherTimer? _updCountTimer;
    /// <summary>Restsekunden bis zum Neustart; -1 = kein Countdown.</summary>
    public int UpdateCountdownLeft { get; private set; } = -1;

    void InitUpdateCountdown(Grid root)
    {
        Grid.SetRowSpan(_updBar, 2);
        var now = new Button { Name = "upd-countdown-now", Content = "Jetzt neu starten", FontSize = 16, Padding = new Thickness(16, 6), CornerRadius = new CornerRadius(10), Background = Accent, Foreground = Brushes.White, Margin = new Thickness(16, 0, 0, 0) };
        now.Click += (_, _) => { StopUpdateCountdown(); InstallUpdateNow(); };
        _updBar.Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "⬆", FontSize = 22, Foreground = GoldText(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) }, _updBarText, now } };
        root.Children.Add(_updBar);
    }

    /// <summary>Update ist geladen: alles sichern, Countdown zeigen, danach neu starten (öffentlich für Tests).</summary>
    public void StartUpdateCountdown(int seconds = UpdateSchedule.Countdown)
    {
        if (_updateReady is null || UpdateCountdownLeft >= 0) return;
        ProgramAutosaveAll();
        UpdateCountdownLeft = seconds; UpdateCountdownText();
        _updBar.IsVisible = true;
        _core.Audit.Append("app", "update.countdown", _updateReady);
        _updCountTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _updCountTimer.Tick += (_, _) => UpdateCountdownStep();
        _updCountTimer.Start();
    }

    /// <summary>Eine Sekunde weiter (öffentlich für Tests); bei 0 wird installiert.</summary>
    public void UpdateCountdownStep()
    {
        if (UpdateCountdownLeft < 0) return;
        UpdateCountdownLeft--;
        if (UpdateCountdownLeft > 0) { UpdateCountdownText(); return; }
        StopUpdateCountdown(); InstallUpdateNow();
    }

    void UpdateCountdownText() => _updBarText.Text = $"Trendo {_updateReady} wird in {UpdateCountdownLeft} Sekunden installiert – deine Arbeit ist gesichert, deine Fenster kommen wieder.";
    void StopUpdateCountdown() { _updCountTimer?.Stop(); _updCountTimer = null; UpdateCountdownLeft = -1; _updBar.IsVisible = false; }
}
