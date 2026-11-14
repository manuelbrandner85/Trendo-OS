using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik V16/V17 wie Suno (07.11.2026): Idee des Tages mit Wochen-Kalender auf der Seite „Erstellen“ (jeder Tag hat seine feste Idee,
/// Tage mit eigenen Liedern sind markiert) und Kurzvideo 9:16 direkt im Remix-Menü.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: heutiges Datum ersetzen.</summary>
    public DateOnly? TodayOverride { get; set; }
    DateOnly MsToday => TodayOverride ?? DateOnly.FromDateTime(DateTime.Now);
    DateOnly? _msIdeaDay;
    static readonly string[] DayShort = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

    /// <summary>Idee eines Tages in die Beschreibung (Einfach) oder den Stil (Erweitert) übernehmen.</summary>
    public void MsTakeDailyIdea(DateOnly day)
    {
        var idea = MusicSuno.DailyIdea(day);
        if (_msPro) _msStyle = idea; else _msDesc = idea;
        _msMsg = day == MsToday ? "Idee des Tages übernommen – anpassen oder gleich „Erstellen“." : $"Idee vom {day:dd.MM.} übernommen.";
        Show(MS);
    }

    Control MsDailyIdeaCard()
    {
        var today = MsToday; var day = _msIdeaDay ?? today;
        var made = Songs.All().Select(s => DateOnly.FromDateTime(s.Created.LocalDateTime)).ToHashSet();
        var week = new UniformGrid { Name = "ms-idea-week", Columns = 7, Margin = new Thickness(0, 4, 0, 0) };
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i); var sel = d == day;
            var b = new Button { Name = $"ms-idea-day-{i}", Padding = new Thickness(2, 3), Margin = new Thickness(1), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center, Background = sel ? Brush.Parse("#55D4AF37") : Brush.Parse("#14FFFFFF"),
                Content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new TextBlock { Text = i == 0 ? "Heute" : DayShort[(int)d.DayOfWeek], FontSize = 9.5, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = d.Day.ToString(), FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = made.Contains(d) ? "●" : " ", FontSize = 8, Foreground = Brush.Parse("#D4AF37"), HorizontalAlignment = HorizontalAlignment.Center } } } };
            ToolTip.SetTip(b, made.Contains(d) ? "An diesem Tag hast du Lieder erstellt" : "Idee dieses Tages zeigen");
            var dd = d; b.Click += (_, _) => { _msIdeaDay = dd; Show(MS); };
            week.Children.Add(b);
        }
        var take = new Button { Name = "ms-idea-take", Content = "Übernehmen", FontSize = 11.5, Padding = new Thickness(10, 3), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(0), Background = Brush.Parse("#33D4AF37"), VerticalAlignment = VerticalAlignment.Center };
        take.Click += (_, _) => MsTakeDailyIdea(day);
        var head = new DockPanel { Children = { Dk(take, Avalonia.Controls.Dock.Right), new TextBlock { Text = day == today ? "IDEE DES TAGES" : $"IDEE VOM {day:dd.MM.}", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center } } };
        return new Border { Name = "ms-idea", Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 6),
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#26D4AF37"), 0), new GradientStop(Color.Parse("#0AFFFFFF"), 1) } },
            Child = new StackPanel { Spacing = 4, Children = { head, new TextBlock { Name = "ms-idea-text", Text = MusicSuno.DailyIdea(day), Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap }, week } } };
    }

    /// <summary>V16: Kurzvideo 9:16 aus dem Remix-Menü – stärkster Teil, 30 s, im Ordner „Kurzclips“.</summary>
    public async Task<string?> MsShortVideo(string songId)
    {
        _msSel = songId; _msDetailTab = "profi";
        var f = await MsHookClip(songId, 30);
        _msMsg = f is null ? MusicProMessage : $"✓ Kurzvideo 9:16 fertig: {Path.GetFileName(f)} (Ordner „Musik/Kurzclips“).";
        Show(MS); return f;
    }
}
