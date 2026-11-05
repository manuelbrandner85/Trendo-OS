using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Tv;

namespace Trendo.App;

/// <summary>TV & Live – „Video-Plattformen“ (Wunsch 05.11.2026): YouTube und ähnliche Dienste als eigene Kategorie.
/// Aufbau wie YouTube: Plattform oben, Kategorie-Leiste, Suche (auch Link einfügen), Kacheln mit Vorschaubild, Kanal, Aufrufen, Datum und Länge.
/// PeerTube und Internet Archive laufen im Trendo-Player; Dailymotion wird in Trendo durchsucht und auf der offiziellen Seite angesehen;
/// YouTube, Vimeo, Twitch und TED öffnen die offizielle Seite bzw. den offiziellen Einbett-Player. Kein Herunterladen.</summary>
public partial class MainWindow
{
    string _tvVidPlatform = "peertube", _tvVidCat = "alle", _tvVidQuery = "", _tvVidStatus = "";
    List<VideoPlatforms.Video>? _tvVidResults;
    public IReadOnlyList<VideoPlatforms.Video>? TvVideoResults => _tvVidResults;
    public string TvVideoStatus => _tvVidStatus;
    public string TvVideoPlatform => _tvVidPlatform;
    /// <summary>Für Tests: Suche ohne Netz.</summary>
    public Func<string, string, string, Task<List<VideoPlatforms.Video>>>? TvVideoSearchOverride { get; set; }
    /// <summary>Für Tests: Wiedergabe-Adresse ohne Netz.</summary>
    public Func<VideoPlatforms.Video, Task<string?>>? TvVideoResolveOverride { get; set; }

    /// <summary>Netzzugang für die TV-Quellen (der Kern bleibt ohne Netz-Bibliothek, QA-019): Text holen bzw. senden, Fehler als deutsche Meldung.</summary>
    static async Task<string> TvGetText(string url, string what)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url); req.Headers.UserAgent.ParseAdd("TrendoOS");
        using var res = await TvHttp.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"{what} nicht erreichbar ({(int)res.StatusCode}).");
        return await res.Content.ReadAsStringAsync();
    }
    static async Task<string> TvPostText(string url, string body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, System.Text.Encoding.UTF8, "text/plain") };
        req.Headers.UserAgent.ParseAdd("TrendoOS");
        using var res = await TvHttp.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Mediathek-Suche nicht erreichbar ({(int)res.StatusCode}).");
        return await res.Content.ReadAsStringAsync();
    }

    public void TvVideoSelect(string platform)
    {
        VideoPlatforms.Get(platform); _tvVidPlatform = platform; _tvVidResults = null; _tvVidStatus = ""; _tvTab = "videos";
        if (VideoPlatforms.Get(platform).Mode != VideoPlatforms.Mode.Original) { _ = TvVideoSearchAsync(_tvVidQuery, _tvVidCat); return; }
        Show("tv");
    }

    public async Task TvVideoSearchAsync(string query, string category)
    {
        _tvVidQuery = query.Trim(); _tvVidCat = category; var pf = VideoPlatforms.Get(_tvVidPlatform);
        // eingefügter YouTube-Link: offiziellen Player öffnen
        if (VideoPlatforms.YouTubeId(_tvVidQuery) is { } yid) { TvLegalOpen(VideoPlatforms.YouTubeEmbed(yid)); _tvVidQuery = ""; _tvVidStatus = "YouTube-Video im offiziellen Player geöffnet."; Show("tv"); return; }
        if (pf.Mode == VideoPlatforms.Mode.Original)
        {
            var q = _tvVidQuery.Length > 0 ? _tvVidQuery : VideoPlatforms.CategoryQuery(category);
            TvLegalOpen(VideoPlatforms.SearchPage(pf.Id, q)); _tvVidStatus = $"{pf.Name} geöffnet{(q.Length > 0 ? $" – Suche „{q}“" : "")}."; Show("tv"); return;
        }
        _tvVidStatus = "Suche läuft …"; Show("tv");
        try
        {
            _tvVidResults = TvVideoSearchOverride is { } o ? await o(pf.Id, _tvVidQuery, category) :
                _core.Offline ? throw new InvalidOperationException("Trendo ist offline – die Suche braucht Internet.") : await VideoPlatforms.SearchAsync(TvGetText, pf.Id, _tvVidQuery, category);
            _tvVidStatus = _tvVidResults.Count == 0 ? "Nichts gefunden – anderen Suchbegriff oder andere Kategorie probieren." : $"{_tvVidResults.Count} Videos auf {pf.Name}";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        { _tvVidResults = []; _tvVidStatus = e is InvalidOperationException ? e.Message : $"{pf.Name} ist gerade nicht erreichbar."; }
        Show("tv");
    }

    /// <summary>Video ansehen: im Trendo-Player (PeerTube, Archiv) oder auf der offiziellen Seite.</summary>
    public async Task TvVideoPlayAsync(VideoPlatforms.Video v)
    {
        if (VideoPlatforms.Get(v.Platform).Mode != VideoPlatforms.Mode.InTrendo) { TvLegalOpen(v.PageUrl); return; }
        _tvVidStatus = "Video wird geladen …"; Show("tv");
        string? url = null;
        try { url = TvVideoResolveOverride is { } o ? await o(v) : await VideoPlatforms.PlayUrlAsync(TvGetText, v); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException) { _tvVidStatus = "Das Video ließ sich nicht laden – " + e.Message; }
        if (url is null) { _tvVidStatus = "Hier gibt es keine abspielbare Datei – „Seite“ öffnet das Original."; Show("tv"); return; }
        _tvVidStatus = "";
        TvPlayVideo(url, v.Title);
    }

    Control TvVideosView()
    {
        var sp = new StackPanel { Name = "tv-videos", Spacing = 10 };
        if (TvVodStage() is { } stage) sp.Children.Add(stage); // läuft gerade ein Video: Player oben
        // Plattformen als Leiste mit Farbpunkt
        var plats = new WrapPanel { Name = "tv-vid-platforms" };
        foreach (var p in VideoPlatforms.All)
        {
            var pp = p; var on = _tvVidPlatform == p.Id;
            var b = TvChip("tv-vid-platform-" + p.Id, (p.Mode == VideoPlatforms.Mode.InTrendo ? "▶ " : p.Mode == VideoPlatforms.Mode.Suche ? "🔍 " : "↗ ") + p.Name, on, () => TvVideoSelect(pp.Id));
            ToolTip.SetTip(b, p.Note); plats.Children.Add(b);
        }
        sp.Children.Add(plats);
        var pf = VideoPlatforms.Get(_tvVidPlatform);
        // Kopf der Plattform
        sp.Children.Add(new Border { Name = "tv-vid-head", CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10), BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(pf.Color), 0), new GradientStop(Color.Parse("#E6101418"), 0.7) } },
            Child = new StackPanel { Spacing = 2, Children = {
                new TextBlock { Text = pf.Name, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeight.Bold },
                new TextBlock { Text = pf.Note, Foreground = Brush.Parse("#DDFFFFFF"), FontSize = 12, TextWrapping = TextWrapping.Wrap } } } });
        // Kategorien wie bei YouTube
        var cats = new WrapPanel { Name = "tv-vid-cats" };
        foreach (var (id, label, _) in VideoPlatforms.Categories)
        {
            var cid = id;
            cats.Children.Add(TvChip("tv-vid-cat-" + id, label, _tvVidCat == id, () => _ = TvVideoSearchAsync("", cid)));
        }
        sp.Children.Add(cats);
        // Suche – auch einen YouTube-Link einfügen
        var box = new TextBox { Name = "tv-vid-search", Text = _tvVidQuery, MinWidth = 380, FontSize = 13.5,
            PlaceholderText = pf.Id == "youtube" ? "Auf YouTube suchen – oder einen YouTube-Link einfügen" : $"Auf {pf.Name} suchen – oder einen YouTube-Link einfügen" };
        void Go() => _ = TvVideoSearchAsync(box.Text ?? "", _tvVidCat);
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Go(); } };
        sp.Children.Add(new WrapPanel { Children = { box, new Border { Width = 8 }, TvButton("tv-vid-go", pf.Mode == VideoPlatforms.Mode.Original ? $"↗ Auf {pf.Name} suchen" : "🔍 Suchen", Go, true),
            new Border { Width = 8 }, TvButton("tv-vid-home", $"{pf.Name} öffnen", () => TvLegalOpen(pf.Home)) } });
        if (_tvVidStatus.Length > 0) sp.Children.Add(new TextBlock { Name = "tv-vid-status", Text = _tvVidStatus, Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });

        if (pf.Mode == VideoPlatforms.Mode.Original)
            sp.Children.Add(P(pf.Id == "youtube"
                ? "YouTube läuft im offiziellen Player von YouTube – Trendo öffnet Suche, Kategorie oder eingefügten Link direkt dort (datensparsam über youtube-nocookie). Herunterladen ist nicht vorgesehen. Werbefrei und direkt in Trendo: PeerTube oben wählen."
                : $"{pf.Name} öffnet Trendo auf der offiziellen Seite – Suche und Kategorie werden mitgegeben.", Muted, 12.5));
        else if (_tvVidResults is { Count: > 0 } res)
        {
            var grid = new WrapPanel { Name = "tv-vid-results" };
            for (var i = 0; i < res.Count; i++)
            {
                var v = res[i];
                var thumb = new Image { Name = "tv-vid-thumb-" + i, Stretch = Stretch.UniformToFill }; TvImage(thumb, v.Thumbnail);
                var meta = string.Join(" · ", new[] { VideoPlatforms.ViewsText(v.Views), v.Published is { } d ? d.ToString("dd.MM.yyyy") : null }.Where(x => !string.IsNullOrEmpty(x)));
                var cover = new Grid { Height = 158, Children = { new Border { Background = Brush.Parse(pf.Color), Opacity = 0.35 }, thumb } };
                if (v.Duration > TimeSpan.Zero)
                    cover.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6), CornerRadius = new CornerRadius(5), Background = Brush.Parse("#CC000000"), Padding = new Thickness(5, 1),
                        Child = new TextBlock { Text = TvClock(v.Duration.TotalSeconds), Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeight.SemiBold } });
                var vv = v;
                var play = TvButton("tv-vid-play-" + i, pf.Mode == VideoPlatforms.Mode.InTrendo ? "▶ Abspielen" : $"↗ Auf {pf.Name} ansehen", () => _ = TvVideoPlayAsync(vv), true);
                var web = TvButton("tv-vid-web-" + i, "Seite", () => TvLegalOpen(vv.PageUrl));
                var card = new Border
                {
                    Name = "tv-vid-item-" + i, Width = 282, Margin = new Thickness(0, 0, 12, 14), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), ClipToBounds = true,
                    Child = new StackPanel { Children = {
                        cover,
                        new StackPanel { Spacing = 4, Margin = new Thickness(12, 10, 12, 12), Children = {
                            new TextBlock { Text = v.Title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 40, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = v.Author, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = meta, Foreground = Muted, FontSize = 11.5 },
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 4, 0, 0), Children = { play, web } } } } } }
                };
                card.PointerPressed += (_, e) => { if (e.ClickCount >= 2) _ = TvVideoPlayAsync(vv); };
                Hover(card); grid.Children.Add(card);
            }
            sp.Children.Add(grid);
        }
        else if (_tvVidResults is null)
            sp.Children.Add(P(pf.Mode == VideoPlatforms.Mode.InTrendo
                ? $"Kategorie wählen oder suchen – {pf.Name} spielt direkt im Trendo-Player, ohne Werbung."
                : $"Kategorie wählen oder suchen – Trendo zeigt die Treffer, angesehen wird auf {pf.Name}.", Muted, 12.5));
        sp.Children.Add(P("Alle Plattformen sind legal und kostenlos nutzbar. Trendo lädt nichts herunter und zeigt keine fremden Logos.", Muted, 11.5));
        return sp;
    }
}
