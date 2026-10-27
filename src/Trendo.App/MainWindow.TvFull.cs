using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026: Live-TV, Filme und Serien im echten Vollbild – über ganz Trendo und den ganzen Bildschirm, mit Esc oder
/// „Verkleinern“ zurück. Die Steuerleiste blendet sich nach 4 Sekunden aus; jede Taste holt sie zurück.
/// Tasten: Leertaste Pause, ←/→ spulen, ↑/↓ Sender, F Vollbild, Esc zurück.</summary>
public partial class MainWindow
{
    Grid? _tvFullLayer;
    Border? _tvFullBar;
    DispatcherTimer? _tvFullHide;
    WindowState _tvPrevState = WindowState.Normal;
    bool _tvKeysWired;
    public bool TvIsFullscreen => _tvFullLayer is { IsVisible: true };

    public void TvFullscreen(bool on)
    {
        if (Content is not LayoutTransformControl { Child: Grid root }) return;
        _tvPlayer ??= new MpvView("main");
        WireTvKeys();
        if (on)
        {
            if (TvIsFullscreen) return;
            _tvFullLayer ??= new Grid { Name = "tv-fullscreen", ZIndex = 9000, Background = Brushes.Black, RowDefinitions = new RowDefinitions("*,Auto") };
            if (_tvFullLayer.Parent is null)
            {
                Grid.SetRowSpan(_tvFullLayer, Math.Max(1, root.RowDefinitions.Count)); Grid.SetColumnSpan(_tvFullLayer, Math.Max(1, root.ColumnDefinitions.Count));
                root.Children.Add(_tvFullLayer);
            }
            _tvFullLayer.Children.Clear();
            Detach(_tvPlayer);
            _tvFullLayer.Children.Add(_tvPlayer);
            _tvFullBar = TvFullBar();
            Grid.SetRow(_tvFullBar, 1);
            _tvFullLayer.Children.Add(_tvFullBar);
            _tvFullLayer.IsVisible = true;
            _tvPrevState = WindowState == WindowState.FullScreen ? WindowState.Maximized : WindowState;
            WindowState = WindowState.FullScreen;
            TvShowBar();
            Focus();
            TvWatchStart();
            _core.Audit.Append("app", "tv.fullscreen", "an");
        }
        else
        {
            if (!TvIsFullscreen) return;
            _tvFullHide?.Stop();
            TvWatchStop();
            _tvFullLayer!.Children.Clear();
            _tvFullLayer.IsVisible = false;
            WindowState = _tvPrevState;
            _tvTab = "live";
            Show("tv"); // das Bild kehrt in das Studio zurück und läuft weiter
        }
    }

    Border TvFullBar()
    {
        var title = new TextBlock { Name = "tv-full-title", Text = _tvNowTitle?.Text ?? "", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 18, 0) };
        Button B(string name, string text, Action a, bool primary = false)
        {
            var b = new Button { Name = name, Content = text, Foreground = Brushes.White, FontSize = 14, Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 6, 0), Background = primary ? Accent : Brush.Parse("#33FFFFFF") };
            b.Click += (_, _) => { a(); TvShowBar(); };
            return b;
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = {
            title,
            B("tv-full-prev", "◀ Sender", () => TvZap(-1)), B("tv-full-pause", "⏯", () => _tvPlayer?.Send("cycle", "pause")),
            B("tv-full-back", "⏪ 30 s", () => _tvPlayer?.Send("seek", -30)), B("tv-full-fwd", "⏩ 30 s", () => _tvPlayer?.Send("seek", 30)),
            B("tv-full-next", "Sender ▶", () => TvZap(1)), B("tv-full-mute", "🔇", () => _tvPlayer?.Send("cycle", "mute")),
            B("tv-full-exit", "⤡ Verkleinern (Esc)", () => TvFullscreen(false), true) } };
        return new Border { Name = "tv-full-bar", Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#E60A1428"), 0), new GradientStop(Color.Parse("#FF050B18"), 1) } }, BorderBrush = Brush.Parse("#55D4A84B"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 10), Child = row };
    }

    /// <summary>Leiste zeigen und nach 4 Sekunden wieder ausblenden (das Bild nutzt dann den ganzen Bildschirm).</summary>
    void TvShowBar()
    {
        if (_tvFullBar is null) return;
        _tvFullBar.IsVisible = true;
        if (_tvFullBar.Child is StackPanel sp && sp.Children.FirstOrDefault() is TextBlock t) t.Text = _tvNowTitle?.Text ?? t.Text;
        _tvFullHide ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _tvFullHide.Tick -= TvHideBar; _tvFullHide.Tick += TvHideBar;
        _tvFullHide.Stop(); _tvFullHide.Start();
    }

    void TvHideBar(object? s, EventArgs e) { _tvFullHide?.Stop(); if (TvIsFullscreen && _tvFullBar is not null) _tvFullBar.IsVisible = false; }

    void WireTvKeys()
    {
        if (_tvKeysWired) return;
        _tvKeysWired = true;
        AddHandler(KeyDownEvent, (_, e) =>
        {
            var full = TvIsFullscreen;
            if (!full && !(CurrentView == "tv" && _tvTab == "live" && e.Source is not TextBox and not Button and not ComboBox)) return;
            switch (e.Key)
            {
                case Key.Escape when full: TvFullscreen(false); break;
                case Key.F: TvFullscreen(!full); break;
                case Key.Space: _tvPlayer?.Send("cycle", "pause"); break;
                case Key.Left: _tvPlayer?.Send("seek", -10); break;
                case Key.Right: _tvPlayer?.Send("seek", 10); break;
                case Key.Up when full: TvZap(-1); break;
                case Key.Down when full: TvZap(1); break;
                case Key.M: _tvPlayer?.Send("cycle", "mute"); break;
                default: if (full) TvShowBar(); return;
            }
            if (full) TvShowBar();
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    // Wunsch 27.10.2026: Das Videobild (mpv) ist ein eigenes Windows-Fenster und fängt Maus und Esc ab. Deshalb im Vollbild
    // Maus und Esc direkt bei Windows abfragen und ein kleines Fenster „Verkleinern“ zeigen, das immer über dem Bild liegt.
    DispatcherTimer? _tvWatch;
    PixelPoint _tvLastCursor;
    bool _tvEscDown;
    Window? _tvMini;
    bool _tvMiniHooked;
    DateTime _tvMiniUntil;
    public bool TvMiniVisible => _tvMini is { IsVisible: true };

    void TvWatchStart()
    {
        _tvLastCursor = CursorNow();
        _tvEscDown = true; // eine noch gedrückte Taste vom Einschalten nicht als Esc werten
        _tvWatch ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _tvWatch.Tick -= TvWatchTick; _tvWatch.Tick += TvWatchTick;
        _tvWatch.Start();
        TvShowMini();
    }

    void TvWatchStop() { _tvWatch?.Stop(); _tvMini?.Close(); _tvMini = null; }

    void TvWatchTick(object? s, EventArgs e)
    {
        if (!TvIsFullscreen) { TvWatchStop(); return; }
        var c = CursorNow();
        if (c != _tvLastCursor) { _tvLastCursor = c; TvPointerMoved(); }
        var esc = OperatingSystem.IsWindows() && OurWindowActive() && (GetAsyncKeyState(0x1B) & 0x8000) != 0;
        if (esc && !_tvEscDown) { _tvEscDown = true; TvFullscreen(false); return; }
        _tvEscDown = esc;
        if (TvMiniVisible && DateTime.UtcNow > _tvMiniUntil) TvHideMini();
    }

    /// <summary>Maus bewegt: Steuerleiste und das kleine Fenster „Verkleinern“ für 3 Sekunden zeigen.</summary>
    public void TvPointerMoved() { if (!TvIsFullscreen) return; TvShowBar(); TvShowMini(); }

    void TvShowMini()
    {
        _tvMiniUntil = DateTime.UtcNow.AddSeconds(3);
        if (_tvMini is null)
        {
            var btn = new Button { Name = "tv-mini-exit", Content = "⤡  Verkleinern", Foreground = Brush.Parse("#1B1406"), FontSize = 15, FontWeight = FontWeight.SemiBold, Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(12),
                Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FFF1C870"), 0), new GradientStop(Color.Parse("#FFD4A84B"), 1) } } };
            btn.Click += (_, _) => TvFullscreen(false);
            if (!_tvMiniHooked) { _tvMiniHooked = true; Closed += (_, _) => { _tvWatch?.Stop(); _tvMini?.Close(); _tvMini = null; }; } // mit Trendo schließen, nicht nur verstecken
            _tvMini = new Window
            {
                Title = "Verkleinern", WindowDecorations = Avalonia.Controls.WindowDecorations.None, Topmost = true, ShowInTaskbar = false, CanResize = false, SizeToContent = SizeToContent.WidthAndHeight,
                Background = Brush.Parse("#E6081226"), TransparencyLevelHint = [WindowTransparencyLevel.Transparent], ShowActivated = false,
                Content = new Border { Padding = new Thickness(8), CornerRadius = new CornerRadius(14), BorderBrush = Brush.Parse("#88D4A84B"), BorderThickness = new Thickness(1), Child = btn },
            };
        }
        if (!_tvMini.IsVisible)
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            var area = screen?.Bounds ?? new PixelRect(0, 0, 1920, 1080);
            var scale = screen?.Scaling ?? 1;
            _tvMini.Position = new PixelPoint(area.Right - (int)(210 * scale), area.Y + (int)(24 * scale));
            _tvMini.Show();
        }
    }

    void TvHideMini() { if (_tvMini is { IsVisible: true }) _tvMini.Hide(); }

    PixelPoint CursorNow() => OperatingSystem.IsWindows() && GetCursorPos(out var p) ? new PixelPoint(p.X, p.Y) : _tvLastCursor;

    bool OurWindowActive()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        GetWindowThreadProcessId(fg, out var pid);
        return pid == (uint)Environment.ProcessId || IsActive;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint p);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
}
