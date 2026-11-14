using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Tv;

namespace Trendo.App;

/// <summary>TV & Live – „Mediatheken & Streaming“ (Wunsch 05.11.2026): legale Quellen nach Art und Kategorie.
/// Mediatheken: in Trendo suchen und im Trendo-Player abspielen. Kostenlos mit Werbung und Abo-Dienste: Kachel öffnet die offizielle Seite.</summary>
public partial class MainWindow
{
    LegalSources.Kind _tvLegalKind = LegalSources.Kind.Mediathek;
    string _tvLegalCat = "Alle", _tvLegalQuery = "", _tvLegalChannel = "";
    List<LegalSources.Video>? _tvLegalResults;
    string _tvLegalStatus = "";
    public IReadOnlyList<LegalSources.Video>? TvLegalResults => _tvLegalResults;
    public string TvLegalStatus => _tvLegalStatus;
    /// <summary>Für Tests: Suche ohne Netz.</summary>
    public Func<string, string, string?, Task<List<LegalSources.Video>>>? TvLegalSearchOverride { get; set; }
    /// <summary>Für Tests: statt den Browser zu öffnen.</summary>
    public Action<string>? TvLegalOpenOverride { get; set; }

    public void TvLegalSelect(LegalSources.Kind kind, string category = "Alle") { _tvLegalKind = kind; _tvLegalCat = category; Show("tv"); }

    public async Task TvLegalSearchAsync(string query, string category, string? channel)
    {
        _tvLegalQuery = query; _tvLegalCat = category; _tvLegalChannel = channel ?? "";
        _tvLegalStatus = "Suche läuft …"; Show("tv");
        try
        {
            _tvLegalResults = TvLegalSearchOverride is { } o ? await o(query, category, channel) :
                _core.Offline ? throw new InvalidOperationException("Trendo ist offline – die Mediathek-Suche braucht Internet.") : await LegalSources.SearchAsync(TvPostText, query, category, channel);
            _tvLegalStatus = _tvLegalResults.Count == 0 ? "Nichts gefunden – anderen Suchbegriff oder andere Kategorie probieren." : $"{_tvLegalResults.Count} Sendungen";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { _tvLegalResults = []; _tvLegalStatus = e is InvalidOperationException ? e.Message : "Die Mediathek-Suche ist gerade nicht erreichbar."; }
        Show("tv");
    }

    void TvLegalOpen(string url)
    {
        if (TvLegalOpenOverride is { } o) { o(url); return; }
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.legal.open", url);
        _core.Integrations.LaunchDirect(Principal.LocalOwner, url, _core.Audit);
    }

    Control TvLegalView()
    {
        var sp = new StackPanel { Name = "tv-legal", Spacing = 10 };
        if (_tvTab == "mediathek" && TvVodStage() is { } stage) sp.Children.Add(stage); // läuft gerade eine Sendung: Player oben
        // Art: Mediatheken · Kostenlos mit Werbung · Abo-Dienste
        var kinds = new WrapPanel { Name = "tv-legal-kinds" };
        foreach (var k in Enum.GetValues<LegalSources.Kind>())
        {
            var kk = k;
            kinds.Children.Add(TvChip("tv-legal-kind-" + k.ToString().ToLowerInvariant(), k switch { LegalSources.Kind.Mediathek => "Mediatheken", LegalSources.Kind.Kostenlos => "Kostenlos mit Werbung", _ => "Abo-Dienste" },
                _tvLegalKind == k, () => { _tvLegalKind = kk; Show("tv"); }));
        }
        sp.Children.Add(kinds);
        var cats = new WrapPanel { Name = "tv-legal-cats" };
        foreach (var c in new[] { "Alle" }.Concat(LegalSources.Categories))
        {
            var cc = c;
            cats.Children.Add(TvChip("tv-legal-cat-" + c.ToLowerInvariant(), c, _tvLegalCat == c, () =>
            {
                _tvLegalCat = cc;
                if (_tvLegalKind == LegalSources.Kind.Mediathek && cc != "Alle") _ = TvLegalSearchAsync("", cc, _tvLegalChannel.Length > 0 ? _tvLegalChannel : null);
                else Show("tv");
            }));
        }
        sp.Children.Add(cats);

        if (_tvLegalKind == LegalSources.Kind.Mediathek)
        {
            // Suche über alle Mediatheken oder einen Sender
            var box = new TextBox { Name = "tv-legal-search", Text = _tvLegalQuery, PlaceholderText = "Sendung, Film oder Thema suchen – z. B. Tatort, Terra X, Sportschau", MinWidth = 360, FontSize = 13.5 };
            var senders = new ComboBox { Name = "tv-legal-channel", MinWidth = 180, ItemsSource = new[] { "Alle Sender" }.Concat(LegalSources.Of(LegalSources.Kind.Mediathek).Select(p => p.Name)).ToList() };
            var pick = LegalSources.Of(LegalSources.Kind.Mediathek).ToList();
            senders.SelectedIndex = Math.Max(0, pick.FindIndex(p => p.Channel == _tvLegalChannel) + 1);
            string? Ch() => senders.SelectedIndex > 0 ? pick[senders.SelectedIndex - 1].Channel : null;
            var go = TvButton("tv-legal-go", "Suchen", () => _ = TvLegalSearchAsync(box.Text ?? "", _tvLegalCat, Ch()), true);
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _ = TvLegalSearchAsync(box.Text ?? "", _tvLegalCat, Ch()); } };
            sp.Children.Add(new WrapPanel { Children = { box, new Border { Width = 8 }, senders, new Border { Width = 8 }, go } });
            if (_tvLegalStatus.Length > 0) sp.Children.Add(new TextBlock { Name = "tv-legal-status", Text = _tvLegalStatus, Foreground = Muted, FontSize = 12.5 });
            if (_tvLegalResults is { Count: > 0 } res)
            {
                var grid = new WrapPanel { Name = "tv-legal-results" };
                for (var i = 0; i < res.Count; i++)
                {
                    var v = res[i]; var color = LegalSources.All.FirstOrDefault(p => p.Channel is { } ch && v.Channel.StartsWith(ch, StringComparison.OrdinalIgnoreCase))?.Color ?? "#475569";
                    var play = TvButton("tv-legal-play-" + i, "Abspielen", () => TvPlayVideo(v.UrlHd ?? v.Url, v.Title), true);
                    var web = TvButton("tv-legal-web-" + i, "Seite", () => TvLegalOpen(v.Website.StartsWith("https://", StringComparison.Ordinal) ? v.Website : v.Url));
                    grid.Children.Add(new Border
                    {
                        Name = "tv-legal-item-" + i, Width = 300, Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12),
                        Child = new StackPanel { Spacing = 5, Children = {
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                                new Border { CornerRadius = new CornerRadius(6), Background = Brush.Parse(color), Padding = new Thickness(7, 2), Child = new TextBlock { Text = v.Channel, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeight.SemiBold } },
                                new TextBlock { Text = $"{LegalSources.DurationText(v.Duration)} · {v.Date:dd.MM.yyyy}", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } } },
                            new TextBlock { Text = v.Title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 40, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = v.Topic, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = v.Description, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 48, TextTrimming = TextTrimming.CharacterEllipsis },
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { play, web } } } }
                    });
                }
                sp.Children.Add(grid);
            }
            else if (_tvLegalResults is null)
                sp.Children.Add(P("Wähle oben eine Kategorie oder suche nach einer Sendung. Gespielt wird direkt in Trendo – kostenlos und legal aus den Mediatheken von ARD, ZDF, arte, 3sat, phoenix, KiKA, ORF und SRF.", Muted, 12.5));
            // Mediatheken auch als Kacheln zum Öffnen der Original-Seite
            sp.Children.Add(new TextBlock { Text = "Die Mediatheken im Original", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        }
        // Anbieter-Kacheln der gewählten Art (Mediathek: zusätzlich zur Suche; sonst die Hauptansicht)
        var tiles = new WrapPanel { Name = "tv-legal-providers" };
        foreach (var p in LegalSources.Of(_tvLegalKind, _tvLegalCat))
        {
            var pp = p;
            var b = new Button { Name = "tv-legal-provider-" + p.Id, Width = 210, Height = 118, Padding = new Thickness(0), Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, ClipToBounds = true,
                Content = new Border { Padding = new Thickness(14, 12), Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(Color.Parse(p.Color), 0), new GradientStop(Color.Parse("#E6101418"), 1) } },
                    Child = new DockPanel { Children = {
                        DockBottom(new TextBlock { Text = string.Join(" · ", p.Categories.Take(3)), Foreground = Brush.Parse("#CCFFFFFF"), FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis }),
                        new StackPanel { Spacing = 3, Children = {
                            new TextBlock { Text = p.Name, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap },
                            new TextBlock { Text = p.Note, Foreground = Brush.Parse("#DDFFFFFF"), FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextTrimming = TextTrimming.CharacterEllipsis } } } } } } };
            ToolTip.SetTip(b, p.Kind == LegalSources.Kind.Mediathek ? $"{p.Name} im Browser öffnen – oder oben in Trendo suchen" : $"{p.Name} öffnen – offizielle Seite{(p.Kind == LegalSources.Kind.Abo ? ", Anmeldung mit deinem Abo" : "")}");
            b.Click += (_, _) => TvLegalOpen(pp.Url);
            Hover(b); tiles.Children.Add(b);
        }
        if (tiles.Children.Count == 0) sp.Children.Add(P($"In „{_tvLegalCat}“ gibt es hier keinen Anbieter – oben „Alle“ wählen.", Muted, 12.5));
        if (_tvLegalKind == LegalSources.Kind.Mediathek) sp.Children.Add(Card(LiveChannelsCard()));
        sp.Children.Add(tiles);
        sp.Children.Add(P(_tvLegalKind switch
        {
            LegalSources.Kind.Mediathek => "Mediatheken der öffentlich-rechtlichen Sender – kostenlos und legal. Manche Sendungen sind nur eine Zeit lang oder nur in einem Land verfügbar.",
            LegalSources.Kind.Kostenlos => "Kostenlose, legale Dienste mit Werbung – Trendo öffnet die offizielle Seite.",
            _ => "Abo-Dienste – Trendo öffnet die offizielle Seite oder App; du meldest dich dort selbst mit deinem Abo an.",
        }, Muted, 11.5));
        return sp;
        static Control DockBottom(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Bottom); return c; }
    }
}
