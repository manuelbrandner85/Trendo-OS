using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Modernisierung G10 (freigegeben 07.11.2026): Fortschritt als schwebende Karten statt Textzeilen. Jeder Auftrag der Grafikkarten-Warteschlange
/// (Musik, Bild, Video, Spuren, Stimme …) erscheint links unten als Glas-Karte mit Art, Titel, Laufzeit bzw. Warteplatz und einem laufenden Balken.
/// Die Karten zeigen nur echte Zustände der Warteschlange – keine erfundenen Prozentwerte.</summary>
public partial class MainWindow
{
    readonly StackPanel _taskCards = new() { Name = "task-cards", Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(18, 0, 0, 90), ZIndex = 3900, Width = 320 };
    DispatcherTimer? _taskTimer;
    /// <summary>Für Tests: Uhr ersetzen.</summary>
    public Func<DateTimeOffset>? TaskClockOverride { get; set; }

    static (string Icon, string Label) TaskKind(string kind) => kind switch
    {
        "musik" => ("♪", "Musik-KI"), "bild" => ("◩", "Bild-KI"), "video" => ("▶", "Video-KI"), "verbessern" => ("✦", "Verbessern"),
        "stimme" => ("◉", "Stimme"), _ when kind == nameof(KiRuntime.Spuren) => ("≡", "Spuren trennen"), _ => ("⚙", "KI-Auftrag"),
    };

    static string Elapsed(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} min" : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}:{t.Seconds:00} min" : $"{Math.Max(0, (int)t.TotalSeconds)} s";

    void InitTaskCards(Grid root)
    {
        Grid.SetRowSpan(_taskCards, 2); root.Children.Add(_taskCards);
        Gpu.Changed += () => _ui.Post(PaintTaskCards);
    }

    /// <summary>Karten neu zeichnen; läuft etwas, tickt die Laufzeit jede Sekunde.</summary>
    public void PaintTaskCards()
    {
        var now = TaskClockOverride?.Invoke() ?? DateTimeOffset.Now;
        var items = Gpu.Items;
        _taskCards.Children.Clear();
        var waitNo = 0;
        foreach (var t in items.Take(4))
        {
            var (icon, label) = TaskKind(t.Kind);
            var state = t.Running ? "läuft seit " + Elapsed(now - (t.Started ?? t.Queued)) : $"wartet · Platz {++waitNo}";
            var bar = new ProgressBar { IsIndeterminate = t.Running && !Motionless, Height = 3, MinHeight = 3, Margin = new Thickness(0, 6, 0, 0), Foreground = Accent, Background = Brush.Parse("#22FFFFFF"), IsVisible = t.Running };
            var badge = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = t.Running ? Accent : Brush.Parse("#26FFFFFF"), VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = icon, FontSize = 15, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var body = new StackPanel { Spacing = 1, Children = {
                new TextBlock { Text = t.Title, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Name = $"task-state-{t.Id}", Text = $"{label} · {state}", Foreground = Muted, FontSize = 11.5 }, bar } };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Children = { badge, Pos(body, 1) } };
            _taskCards.Children.Add(new Border { Name = $"task-card-{t.Id}", Child = grid, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12), Background = GlassStrong,
                BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Opacity = t.Running ? 1 : 0.85 });
        }
        if (items.Count > 4) _taskCards.Children.Add(new TextBlock { Name = "task-more", Text = $"+ {items.Count - 4} weitere warten", Foreground = Muted, FontSize = 11.5, Margin = new Thickness(6, 0) });
        if (items.Any(t => t.Running)) { if (_taskTimer is null) { _taskTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _taskTimer.Tick += (_, _) => PaintTaskCards(); OwnTimer(_taskTimer); } _taskTimer.Start(); }
        else _taskTimer?.Stop();
    }
}
