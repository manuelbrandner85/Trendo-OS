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
            _core.Audit.Append("app", "tv.fullscreen", "an");
        }
        else
        {
            if (!TvIsFullscreen) return;
            _tvFullHide?.Stop();
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
}
