using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026: Egal welche KI arbeitet – oben unter der KI-Leiste steht, was sie gerade tut: Phase, Wörter, Zeit,
/// geschätzter Fortschritt (nur wenn die Antwort Stück für Stück kommt, sonst ein laufender Balken) und „Abbrechen“.</summary>
public partial class MainWindow
{
    private readonly StackPanel _kiProgress = new() { Name = "ki-progress", Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 74, 0, 0), ZIndex = 4500, Width = 560, IsVisible = false };
    private DispatcherTimer? _kiProgressTimer;

    private void InitKiProgress(Grid root)
    {
        Grid.SetRowSpan(_kiProgress, 2); root.Children.Add(_kiProgress);
        KiActivityHub.Changed += () => Dispatcher.UIThread.Post(RefreshKiProgress);
        _kiProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _kiProgressTimer.Tick += (_, _) => RefreshKiProgress();
    }

    /// <summary>Für Tests: sichtbare Einträge der Anzeige.</summary>
    public int KiProgressRows => _kiProgress.IsVisible ? _kiProgress.Children.Count : 0;

    public void RefreshKiProgress()
    {
        var running = KiActivityHub.Running;
        _kiProgress.IsVisible = running.Count > 0;
        if (running.Count == 0) { _kiProgressTimer?.Stop(); _kiProgress.Children.Clear(); return; }
        if (_kiProgressTimer is { IsEnabled: false } t) t.Start();
        _kiProgress.Children.Clear();
        foreach (var a in running.Take(3))
        {
            var pct = a.Percent;
            var bar = new ProgressBar { Name = "ki-progress-bar", Minimum = 0, Maximum = 1, Height = 6, IsIndeterminate = pct is null, Value = pct ?? 0, CornerRadius = new CornerRadius(3),
                Foreground = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.Parse("#3B82F6"), 0), new GradientStop(Color.Parse("#F5C451"), 0.75), new GradientStop(Color.Parse("#FFF1B8"), 1) } } };
            var secs = (int)a.Elapsed.TotalSeconds;
            var info = $"{(pct is { } p ? $"ca. {p:P0} · " : "")}{(secs < 60 ? $"{secs} s" : $"{secs / 60} min {secs % 60} s")}{(a.Model is { } m ? " · " + m : "")}";
            var stop = new Button { Name = "ki-progress-cancel-" + a.Id, Content = "Abbrechen", FontSize = 11.5, Padding = new Thickness(10, 3), CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent, Foreground = Text, BorderBrush = Brush.Parse("#66F5C451"), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
            var act = a; stop.Click += (_, _) => act.Cancel();
            _kiProgress.Children.Add(new Border
            {
                Name = "ki-progress-row", Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(14), Background = GlassStrong, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                BoxShadow = BoxShadows.Parse("0 12 30 -8 #AA000000"),
                Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                    new Viewbox { Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = TrendoLogo.Build(tile: false) },
                    Pos(new StackPanel { Spacing = 4, Children = {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                            new TextBlock { Name = "ki-progress-label", Text = $"{a.Label}: {a.Phase}", Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                            Pos(new TextBlock { Name = "ki-progress-info", Text = info, Foreground = Muted, FontSize = 11.5, Margin = new Thickness(8, 0, 0, 0) }, 1) } },
                        bar } }, 1),
                    Pos(new Border { Margin = new Thickness(10, 0, 0, 0), Child = stop }, 2) } },
            });
        }
        if (running.Count > 3) _kiProgress.Children.Add(new TextBlock { Text = $"… und {running.Count - 3} weitere KI-Aufgaben", Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center });
    }
}
