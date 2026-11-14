using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Vorschlag 32 (freigegeben 02.11.2026): Wochenrückblick im Wissen – neue Seiten, Lerntage, schwierige Karten, Offenes;
/// auf Klick eine kurze KI-Zusammenfassung (nur aus den eigenen Notizen) und „Als Seite speichern“.</summary>
public partial class MainWindow
{
    bool _wikiWeek; string? _weekSummary; bool _weekBusy;
    /// <summary>Für Tests: Ersatz für die KI-Zusammenfassung.</summary>
    public Func<string, Task<string?>>? WeekSummaryOverride { get; set; }
    public Func<DateTime>? WeekTodayOverride { get; set; }

    public WeekReview.Result WeekReviewNow() => WeekReview.Build(Wissen.All(), Learn.Store, (WeekTodayOverride ?? (() => DateTime.Now))());

    public async Task<string?> WeekSummarize()
    {
        var r = WeekReviewNow();
        if (r.NewPages.Count == 0) { _wissenMsg = "Diese Woche gibt es keine neuen Seiten zum Zusammenfassen."; Show("ki-wissen"); return null; }
        _weekBusy = true; _wissenMsg = "Die KI fasst deine Woche zusammen …"; Show("ki-wissen");
        try
        {
            var prompt = WeekReview.Prompt(r);
            _weekSummary = WeekSummaryOverride is { } o ? await o(prompt)
                : await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true)) is { Ok: true } a ? a.Text.Trim() : null;
            _wissenMsg = _weekSummary is null ? "Die KI hat gerade nicht geantwortet – der Rückblick unten ist trotzdem vollständig." : "";
            return _weekSummary;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { _wissenMsg = "Zusammenfassen ging nicht: " + Trendo.Core.FriendlyError.Say(e); return null; }
        finally { _weekBusy = false; Show("ki-wissen"); }
    }

    /// <summary>Rückblick als eigene Wissensseite speichern (gleiche Woche: Seite wird aktualisiert).</summary>
    public KnowledgeItem WeekSave()
    {
        var r = WeekReviewNow(); var md = WeekReview.Markdown(r, _weekSummary);
        var title = $"Wochenrückblick KW {r.Week} {r.To:yyyy}";
        var it = Wissen.ByTitle(title) is { } old ? Wissen.Update(old.Id, title, md) : Wissen.Add(title, md, "Wochenrückblick", "wochenrückblick");
        _wikiWeek = false; _wikiId = it.Id; _wissenMsg = $"„{title}“ gespeichert.";
        Show("ki-wissen"); return it;
    }

    Control WeekReviewPanel()
    {
        var r = WeekReviewNow(); var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        var sp = new StackPanel { Name = "week-panel", Spacing = 10 };
        sp.Children.Add(H($"Wochenrückblick KW {r.Week}", 17));
        sp.Children.Add(P($"{r.From.ToString("dddd, d. MMMM", de)} bis {r.To.ToString("dddd, d. MMMM yyyy", de)} – nur aus deinen Daten auf diesem PC.", Muted, 11.5));
        // Kacheln
        var tiles = new WrapPanel();
        foreach (var (k, v) in new[] { ("Neue Seiten", r.NewPages.Count.ToString()), ("Karten wiederholt", r.Reviews.ToString()), ("Lerntage", $"{r.ActiveDays} / 7"), ("Jetzt fällig", r.DueNow.ToString()) })
            tiles.Children.Add(new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 8, 8), MinWidth = 120,
                BoxShadow = CardShadow, Child = new StackPanel { Children = { new TextBlock { Text = v, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#F5B83D") }, new TextBlock { Text = k, Foreground = Muted, FontSize = 11.5 } } } });
        sp.Children.Add(tiles);
        // Lerntage als Balken
        var max = Math.Max(1, r.Days.Max(d => d.Reviews));
        var bars = new StackPanel { Name = "week-days", Orientation = Orientation.Horizontal, Spacing = 8, Height = 90 };
        foreach (var d in r.Days)
            bars.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Children = {
                new Border { Width = 26, Height = Math.Max(3, 60.0 * d.Reviews / max), CornerRadius = new CornerRadius(4, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
                    Background = d.Reviews > 0 ? new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(Color.Parse("#FFE08A"), 0), new GradientStop(Color.Parse("#B8860B"), 1) } } : Brush.Parse("#2A3140") },
                new TextBlock { Text = d.Date.ToString("ddd", de), Foreground = Muted, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center } } });
        sp.Children.Add(new StackPanel { Children = { H("Lernen an den Tagen", 13), bars } });
        // KI-Zusammenfassung
        if (_weekSummary is { Length: > 0 }) sp.Children.Add(new Border { Name = "week-summary", Background = Brush.Parse("#1A2A2410"), BorderBrush = Brush.Parse("#66F5B83D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8),
            Child = new StackPanel { Children = { new TextBlock { Text = "Kurz gesagt", Foreground = Brush.Parse("#F5B83D"), FontWeight = FontWeight.SemiBold, FontSize = 12 }, P(_weekSummary, Text, 12.5) } } });
        // Neue Seiten
        sp.Children.Add(H($"Neu im Wissen ({r.NewPages.Count})", 13));
        if (r.NewPages.Count == 0) sp.Children.Add(P("Diese Woche keine neuen Seiten.", Muted, 12));
        var i = 0;
        foreach (var p in r.NewPages)
        {
            var id = p.Id;
            var b = new Button { Name = $"week-page-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 3), Background = Brushes.Transparent,
                Content = $"{p.Title}  ·  {p.Created.LocalDateTime.ToString("ddd d.M.", de)}" };
            b.Click += (_, _) => { _wikiWeek = false; _wikiId = id; Show("ki-wissen"); };
            sp.Children.Add(b);
        }
        if (r.Hard.Count > 0) { sp.Children.Add(H("Noch schwierig", 13)); foreach (var c in r.Hard) sp.Children.Add(P($"• {c.Front}  ({c.Deck}, {c.Lapses}× vergessen)", Text, 12)); }
        if (r.OpenLinks.Count > 0) { sp.Children.Add(H("Noch offen", 13)); sp.Children.Add(P(string.Join(" · ", r.OpenLinks) + " – auf diese Seiten verweist du, sie fehlen noch.", Text, 12)); }
        if (r.Topics.Count > 0) sp.Children.Add(P("Themen der Woche: " + string.Join(", ", r.Topics), Muted, 11.5));
        var ki = new Button { Name = "week-ki", Content = _weekBusy ? "Fasst zusammen …" : "Mit KI zusammenfassen", IsEnabled = !_weekBusy && r.NewPages.Count > 0 };
        ki.Click += async (_, _) => await WeekSummarize();
        var save = new Button { Name = "week-save", Content = "Als Seite speichern", Background = Accent, Foreground = Brushes.White };
        save.Click += (_, _) => Try("ki-wissen", () => WeekSave());
        sp.Children.Add(new WrapPanel { Children = { ki, new Border { Width = 6 }, save } });
        return sp;
    }
}
