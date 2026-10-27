using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Tv;

namespace Trendo.App;

/// <summary>TV & Live – Filme und Serien (Wunsch 27.10.2026: „Filme, Serien und TV-Sender kategorisiert wählbar“). Eigene Bereiche mit
/// Kategorien, Suche und Cover-Kacheln; Serien mit Staffeln und Folgen. Quelle: die Wiedergabeliste (automatisch erkannt) oder bei Xtream
/// die Anbieter-Schnittstelle (Cover, Beschreibung, Staffeln).</summary>
public partial class MainWindow
{
    string _tvVodCat = "Alle", _tvSerCat = "Alle", _tvVodQuery = "", _tvSerQuery = "";
    string? _tvVodSel, _tvSerSel, _tvPlayingTitle;
    int _tvSeason;
    List<TvVod>? _tvVod;
    List<TvSeries>? _tvSeries;
    static readonly Dictionary<string, Bitmap?> TvImages = [];
    static readonly SemaphoreSlim TvImageGate = new(6);
    public List<TvVod> TvVodList() => _tvVod ??= Tv.Vod();
    /// <summary>Für Tests: gerade gezeigter Bereich.</summary>
    public string CurrentTvTab => _tvTab;
    public List<TvSeries> TvSeriesList() => _tvSeries ??= Tv.Series();

    /// <summary>Xtream: Filme und Serien direkt vom Anbieter holen (mit Cover und Beschreibung).</summary>
    async Task TvRefreshXtreamVod(TvSource src)
    {
        if (Xtream.FromPlaylistUrl(src.Url) is not { } x) return;
        try
        {
            async Task<string> Get(string action) => System.Text.Encoding.UTF8.GetString(await TvFetch(Xtream.Api(x.Server, x.User, x.Pass, action)));
            var vod = Xtream.ParseVod(await Get("get_vod_streams"), await Get("get_vod_categories"), x.Server, x.User, x.Pass, src.Id);
            var ser = Xtream.ParseSeries(await Get("get_series"), await Get("get_series_categories"), src.Id);
            if (vod.Count > 0) Tv.StoreVod(src.Id, vod);
            if (ser.Count > 0) Tv.StoreSeries(src.Id, ser);
            _tvMsg += $" Filme: {vod.Count}, Serien: {ser.Count}.";
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or TaskCanceledException)
        {
            _tvMsg += " Filme/Serien vom Anbieter ließen sich nicht laden: " + e.Message;
        }
        _tvVod = null; _tvSeries = null;
    }

    /// <summary>Folgen einer Serie: aus der Liste – oder bei Xtream einmal vom Anbieter holen und merken.</summary>
    public async Task<List<TvEpisode>> TvEpisodes(TvSeries s)
    {
        var eps = Tv.Episodes(s.Id);
        if (eps.Count > 0 || s.XtreamId is null) return eps;
        var src = Tv.Sources.FirstOrDefault(x => s.Id.StartsWith(x.Id + ":"));
        if (src is null || Xtream.FromPlaylistUrl(src.Url) is not { } x) return eps;
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(await TvFetch(Xtream.Api(x.Server, x.User, x.Pass, "get_series_info", "&series_id=" + Uri.EscapeDataString(s.XtreamId))));
            eps = Xtream.ParseEpisodes(json, s.Id, x.Server, x.User, x.Pass);
            if (eps.Count > 0) Tv.StoreEpisodes(s.Id, eps);
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or TaskCanceledException) { _tvMsg = "Die Folgen ließen sich nicht laden: " + e.Message; }
        return eps;
    }

    /// <summary>Film oder Folge abspielen – im großen Bild unter „Live-TV“.</summary>
    public bool TvPlayVideo(string url, string title)
    {
        _tvPlayingTitle = title; _tvTab = "live";
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.vod.play", title.Length > 60 ? title[..60] : title);
        Show("tv");
        var ok = TvPlayUrl(url, "main");
        if (_tvNowTitle is not null) _tvNowTitle.Text = "🎬 " + title;
        if (_tvNowInfo is not null) _tvNowInfo.Text = "Film/Folge – mit ⏪ ⏩ spulen, ⏯ pausieren";
        return ok;
    }

    /// <summary>Cover/Senderlogo laden (zwischengespeichert, höchstens 6 gleichzeitig). Ohne Bild bleibt die Kachel mit Titel.</summary>
    void TvImage(Image target, string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (TvImages.TryGetValue(url, out var known)) { target.Source = known; return; }
        _ = Task.Run(async () =>
        {
            Bitmap? bmp = null;
            await TvImageGate.WaitAsync();
            try
            {
                var file = Path.Combine(_core.DataRoot, "tv", "bilder", Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(url))) + ".img");
                byte[] bytes;
                if (File.Exists(file)) bytes = await File.ReadAllBytesAsync(file);
                else { bytes = await TvFetch(url); Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllBytesAsync(file, bytes); }
                using var ms = new MemoryStream(bytes);
                bmp = Bitmap.DecodeToWidth(ms, 240);
            }
            catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or ArgumentException or NotSupportedException or TaskCanceledException) { }
            finally { TvImageGate.Release(); }
            Dispatcher.UIThread.Post(() => { TvImages[url] = bmp; target.Source = bmp; });
        });
    }

    Control TvPoster(string name, string title, string? sub, string? cover, Action click, bool selected)
    {
        var img = new Image { Stretch = Stretch.UniformToFill };
        TvImage(img, cover);
        var face = new Grid { Height = 210, Width = 140, ClipToBounds = true, Children = {
            new Border { Background = Grad("#1A3A6B", "#0A1A36"), Child = new TextBlock { Text = "🎬", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.5 } },
            img,
            new Border { VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(8, 18, 8, 6), Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#00040A18"), 0), new GradientStop(Color.Parse("#F0040A18"), 1) } },
                Child = new StackPanel { Children = { new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 34 },
                    new TextBlock { Text = sub ?? "", Foreground = Brush.Parse("#C9D6EA"), FontSize = 10.5, IsVisible = !string.IsNullOrEmpty(sub) } } } } } };
        var b = new Button { Name = name, Content = face, Padding = new Thickness(0), Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(10), ClipToBounds = true,
            BorderBrush = selected ? Brush.Parse("#D4A84B") : EdgeLit, BorderThickness = new Thickness(selected ? 2 : 1), Background = Brushes.Transparent };
        ToolTip.SetTip(b, title);
        b.Click += (_, _) => click();
        Hover(b);
        return b;
    }

    static string Safe(string id) => new(id.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    Control TvCategoryBar(string name, IEnumerable<string> cats, string current, Action<string> pick, string query, Action<string> search)
    {
        var all = new List<string> { "Alle" }; all.AddRange(cats.Distinct().OrderBy(c => c));
        var combo = new ComboBox { Name = name + "-cat", ItemsSource = all, SelectedItem = all.Contains(current) ? current : "Alle", MinWidth = 220 };
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string c && c != current) pick(c); };
        var box = new TextBox { Name = name + "-search", Text = query, PlaceholderText = "Titel suchen …", MinWidth = 220 };
        box.TextChanged += (_, _) => search(box.Text ?? "");
        // Schnellwahl: die größten Kategorien als Knöpfe
        var chips = new WrapPanel();
        foreach (var c in all.Take(9))
        {
            var on = c == current;
            var chip = new Button { Name = $"{name}-chip-{Safe(c)}", Content = c, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(12), Background = on ? Accent : Brush.Parse("#1AFFFFFF") };
            var cc = c; chip.Click += (_, _) => pick(cc);
            chips.Children.Add(chip);
        }
        return new StackPanel { Spacing = 6, Children = { new WrapPanel { Children = { Sp(combo), Sp(box) } }, chips } };
    }

    Control TvFilms()
    {
        var sp = new StackPanel { Spacing = 8, Name = "tv-films" };
        var all = TvVodList();
        if (all.Count == 0) { sp.Children.Add(P("Keine Filme gefunden. Filme kommen aus deiner Senderliste (Einträge wie „/movie/…“ oder Videodateien) oder vom Xtream-Zugang.", Text, 12.5)); return sp; }
        var grid = new WrapPanel { Name = "tv-film-grid" };
        void Fill()
        {
            grid.Children.Clear();
            var q = _tvVodQuery.Trim();
            foreach (var v in all.Where(v => (_tvVodCat == "Alle" || v.Category == _tvVodCat) && (q.Length == 0 || v.Name.Contains(q, StringComparison.OrdinalIgnoreCase))).Take(240))
            {
                var vv = v;
                grid.Children.Add(TvPoster("tv-film-item-" + Safe(v.Id), v.Name, string.Join(" · ", new[] { v.Year, v.Rating is { Length: > 0 } r ? "★ " + r : null }.Where(x => !string.IsNullOrEmpty(x))), v.Cover,
                    () => { _tvVodSel = vv.Id; Show("tv"); }, _tvVodSel == v.Id));
            }
            if (grid.Children.Count == 0) grid.Children.Add(P("Nichts gefunden.", null, 12));
        }
        sp.Children.Add(TvCategoryBar("tv-film", all.Select(v => v.Category), _tvVodCat, c => { _tvVodCat = c; Show("tv"); }, _tvVodQuery, q => { _tvVodQuery = q; Fill(); }));
        if (_tvVodSel is { } sel && all.FirstOrDefault(v => v.Id == sel) is { } film)
        {
            var img = new Image { Width = 160, Height = 240, Stretch = Stretch.UniformToFill }; TvImage(img, film.Cover);
            sp.Children.Add(WCard(new Grid { Name = "tv-film-detail", ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = {
                new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = img, Margin = new Thickness(0, 0, 14, 0) },
                Pos(new StackPanel { Spacing = 6, Children = {
                    H(film.Name, 17),
                    P(string.Join(" · ", new[] { film.Category, film.Year, film.Rating is { Length: > 0 } r ? "★ " + r : null }.Where(x => !string.IsNullOrEmpty(x))), null, 12),
                    P(film.Plot ?? "Keine Beschreibung.", Text, 12.5),
                    new WrapPanel { Children = { TvButton("tv-film-play", "▶ Abspielen", () => TvPlayVideo(film.Url, film.Name), true), TvButton("tv-film-close", "Schließen", () => { _tvVodSel = null; Show("tv"); }) } } } }, 1) } }));
        }
        Fill();
        sp.Children.Add(grid);
        return sp;
    }

    List<TvEpisode>? _tvEps;
    string? _tvEpsFor;

    Control TvSeriesView()
    {
        var sp = new StackPanel { Spacing = 8, Name = "tv-series" };
        var all = TvSeriesList();
        if (all.Count == 0) { sp.Children.Add(P("Keine Serien gefunden. Serien kommen aus deiner Senderliste (Einträge wie „Name S01 E02“) oder vom Xtream-Zugang.", Text, 12.5)); return sp; }
        if (_tvSerSel is { } sel && all.FirstOrDefault(s => s.Id == sel) is { } ser)
        {
            // Serie gewählt: Staffeln und Folgen
            var img = new Image { Width = 140, Height = 210, Stretch = Stretch.UniformToFill }; TvImage(img, ser.Cover);
            var eps = _tvEpsFor == ser.Id ? _tvEps ?? [] : [];
            if (_tvEpsFor != ser.Id) { _tvEpsFor = ser.Id; _tvEps = null; _ = Task.Run(async () => { var e = await Dispatcher.UIThread.InvokeAsync(() => TvEpisodes(ser)); Dispatcher.UIThread.Post(() => { _tvEps = e; if (CurrentView == "tv" && _tvTab == "serie") Show("tv"); }); }); }
            var seasons = eps.Select(e => e.Season).Distinct().OrderBy(x => x).ToList();
            if (seasons.Count > 0 && !seasons.Contains(_tvSeason)) _tvSeason = seasons[0];
            var sw = new WrapPanel { Name = "tv-seasons" };
            foreach (var s in seasons)
            {
                var on = s == _tvSeason;
                var b = new Button { Name = "tv-season-" + s, Content = $"Staffel {s}", Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(12, 5), CornerRadius = new CornerRadius(12), Background = on ? Accent : Brush.Parse("#1AFFFFFF") };
                var ss = s; b.Click += (_, _) => { _tvSeason = ss; Show("tv"); };
                sw.Children.Add(b);
            }
            var list = new StackPanel { Name = "tv-episodes", Spacing = 4 };
            foreach (var e in eps.Where(e => e.Season == _tvSeason))
            {
                var ee = e;
                var row = new Button { Name = $"tv-ep-{e.Season}-{e.Episode}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(8), Background = Brush.Parse("#14FFFFFF"),
                    Content = new StackPanel { Children = { new TextBlock { Text = $"▶  Folge {e.Episode}: {e.Title}", Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = (e.Seconds > 0 ? $"{e.Seconds / 60} Min. · " : "") + (e.Plot ?? ""), Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, IsVisible = e.Seconds > 0 || e.Plot is not null } } } };
                row.Click += (_, _) => TvPlayVideo(ee.Url, $"{ser.Name} – S{ee.Season:00}E{ee.Episode:00} {ee.Title}");
                list.Children.Add(row);
            }
            if (eps.Count == 0) list.Children.Add(P(_tvEps is null ? "Folgen werden geladen …" : "Keine Folgen gefunden.", null, 12));
            sp.Children.Add(TvButton("tv-series-back", "← Alle Serien", () => { _tvSerSel = null; Show("tv"); }));
            sp.Children.Add(WCard(new Grid { Name = "tv-series-detail", ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = {
                new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = img, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top },
                Pos(new StackPanel { Spacing = 6, Children = { H(ser.Name, 17),
                    P(string.Join(" · ", new[] { ser.Category, ser.Year, ser.Rating is { Length: > 0 } r ? "★ " + r : null, seasons.Count > 0 ? $"{seasons.Count} Staffel(n)" : null }.Where(x => !string.IsNullOrEmpty(x))), null, 12),
                    P(ser.Plot ?? "", Text, 12.5), sw, list } }, 1) } }));
            return sp;
        }
        var grid = new WrapPanel { Name = "tv-series-grid" };
        void Fill()
        {
            grid.Children.Clear();
            var q = _tvSerQuery.Trim();
            foreach (var s in all.Where(s => (_tvSerCat == "Alle" || s.Category == _tvSerCat) && (q.Length == 0 || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase))).Take(240))
            {
                var ss = s;
                grid.Children.Add(TvPoster("tv-serie-item-" + Safe(s.Id), s.Name, string.Join(" · ", new[] { s.Year, s.Rating is { Length: > 0 } r ? "★ " + r : null }.Where(x => !string.IsNullOrEmpty(x))), s.Cover,
                    () => { _tvSerSel = ss.Id; _tvSeason = 0; Show("tv"); }, false));
            }
            if (grid.Children.Count == 0) grid.Children.Add(P("Nichts gefunden.", null, 12));
        }
        sp.Children.Add(TvCategoryBar("tv-serie", all.Select(s => s.Category), _tvSerCat, c => { _tvSerCat = c; Show("tv"); }, _tvSerQuery, q => { _tvSerQuery = q; Fill(); }));
        Fill();
        sp.Children.Add(grid);
        return sp;
    }
}
