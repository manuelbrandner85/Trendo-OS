using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Bibliothek;
using Principal = Trendo.Core.Principal;

namespace Trendo.App;

/// <summary>Freie Bilder & Klänge als Medienbrowser (Masterprompt 131, Runde 7): Reiter Bilder, Klänge, 3D & Texturen und Bibliothek.
/// Raster mit Vorschaubildern, rechts die große Vorschau mit Urheber, Lizenz und Format, Vorhören über den Player, Filter nach Lizenz und Format;
/// geladen wird erst auf Klick – mit Herkunftsdatei und Namensnennung zum Kopieren.</summary>
public partial class MainWindow
{
    const string MB = "assets";
    public string MediaTab { get; set; } = "bilder";
    public Trendo.Core.IWebFetch? MediaFetchOverride { get; set; }
    MediaLibrary MediaLib => new(MediaFetchOverride ?? new HttpFetch(BibShared), Path.Combine(AssetFolder, "Medien"));
    bool MediaOnline => !_core.Offline && (!_opt.Isolated || MediaFetchOverride is not null);
    public List<MediaHit> MediaHits { get; private set; } = [];
    public int MediaSel { get; set; } = -1;
    string _mbQuery = "", _mbLicense = "alle", _mbFormat = "", _mbMsg = ""; bool _mbBusy;
    /// <summary>Quelle (commons, openverse, beide), Ausrichtung und Format der Namensnennung – wie Openverse, Unsplash, Pexels.</summary>
    public string MediaProvider { get; set; } = "beide";
    public string MediaOrient { get; set; } = "alle";
    string _mbCreditFmt = "text";
    List<MediaHit> _mbAll = [];
    public List<FontHit> FontHits { get; private set; } = [];
    string _fontQuery = "", _fontCat = "";
    FreeFonts Fonts => new(MediaFetchOverride ?? new HttpFetch(BibShared), Path.Combine(AssetFolder, "Medien"));
    readonly Dictionary<string, string?> _mbThumbs = [];
    /// <summary>Für Tests: zuletzt zum Vorhören geladene Datei.</summary>
    public string? MediaListening { get; private set; }

    async void MediaCopy(string text, string what)
    {
        LastCopied = text;
        try { if (Clipboard is { } cb) await cb.SetTextAsync(text); _mbMsg = $"{what} kopiert."; }
        catch (Exception) { _mbMsg = $"⚠ {what} ließ sich nicht kopieren."; }
        Show(MB);
    }

    async Task MediaGuard(string busy, Func<Task<string>> work)
    {
        _mbBusy = true; _mbMsg = busy; Show(MB);
        try { _mbMsg = await work(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or InvalidDataException or InvalidOperationException or IOException or ArgumentException
            or UnauthorizedAccessException or System.Text.Json.JsonException) { _mbMsg = "⚠ " + ex.Message; }
        _mbBusy = false; Show(MB);
    }

    /// <summary>Suche in Wikimedia Commons (öffentlich für Tests und die KI); danach Vorschaubilder nachladen.</summary>
    public Task MediaSearch(string query) => MediaGuard("Suche läuft …", async () =>
    {
        _mbQuery = query;
        if (!MediaOnline) throw new InvalidOperationException("Offline – die Suche braucht Internet. Deine Bibliothek bleibt nutzbar.");
        var lib = MediaLib; var kind = MediaTab == "klaenge" ? "klang" : "bild";
        _mbAll = await lib.Search(kind, query, _mbLicense, _mbFormat, default, MediaProvider);
        MediaHits = MediaLibrary.Filter(_mbAll, MediaOrient); MediaSel = MediaHits.Count > 0 ? 0 : -1;
        Show(MB);
        foreach (var h in MediaHits.Take(50).Where(h => h.ThumbUrl.Length > 0 && !_mbThumbs.ContainsKey(h.Id)))
            try { _mbThumbs[h.Id] = await lib.Thumb(h); } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException) { _mbThumbs[h.Id] = null; }
        return MediaHits.Count == 0 ? "Nichts gefunden – anderes Wort oder weniger Filter versuchen (Deutsch oder Englisch)." : $"{MediaHits.Count} Treffer";
    });

    public Task MediaListen(MediaHit h) => MediaGuard("Klang wird geholt …", async () =>
    {
        if (!MediaOnline) throw new InvalidOperationException("Offline – Vorhören braucht Internet.");
        var f = await MediaLib.Preview(h);
        if (Audio() is not { } a) return "Zum Vorhören braucht Trendo den freien Player mpv – unter Medien › Musik „Player einrichten“.";
        a.Load(f); MediaListening = f;
        return $"▶ „{h.Title}“ ({h.Seconds:0.#} s)";
    });

    public Task MediaGet(MediaHit h) => MediaGuard($"„{h.Title}“ wird geladen …", async () =>
    {
        if (!MediaOnline) throw new InvalidOperationException("Offline – Laden geht erst mit Internet.");
        var item = await MediaLib.Download(h);
        _core.Audit.Append(Principal.LocalOwner.Name, "medien.download", $"{h.Provider}/{h.Id} ({h.License})");
        return $"✓ „{item.Name}“ liegt in deiner Bibliothek – {(h.Free ? "frei nutzbar" : "bitte den Urheber nennen")} ({h.License}).";
    });

    /// <summary>Freie Schriften suchen (Fontsource, wie Google Fonts).</summary>
    public Task FontSearch(string query) => MediaGuard("Schriften werden gesucht …", async () =>
    {
        _fontQuery = query;
        if (!MediaOnline) throw new InvalidOperationException("Offline – die Suche braucht Internet.");
        FontHits = await Fonts.Search(query, _fontCat);
        _ = FontPreviewLoad(FontHits); // Profi 29: Vorschau mit eigenem Text
        return FontHits.Count == 0 ? "Keine Schrift gefunden – anderen Namen oder „Alle Arten“ versuchen." : $"{FontHits.Count} Schriften";
    });

    public Task FontGet(FontHit f) => MediaGuard($"„{f.Family}“ wird geladen …", async () =>
    {
        if (!MediaOnline) throw new InvalidOperationException("Offline – Laden geht erst mit Internet.");
        await Fonts.Download(f);
        _core.Audit.Append(Principal.LocalOwner.Name, "medien.schrift", $"{f.Id} ({f.License})");
        return $"✓ Schrift „{f.Family}“ liegt unter Medien › Schriften ({f.License}, frei nutzbar).";
    });

    /// <summary>Geladenes Bild in der Bildbearbeitung bzw. Klang im Audio-Editor öffnen (wie Adobe Stock in Photoshop).</summary>
    public void MediaEdit(LibraryItem it)
    {
        if (!File.Exists(it.File)) throw new FileNotFoundException("Die Datei ist nicht mehr da.");
        if (it.Kind == "klang") { AuOpen(it.File); Show("ton"); }
        else { ProgramNew("bild"); ImgAddImage(it.File); Show("bild"); }
    }

    Control FontsView()
    {
        var q = new TextBox { Name = "mb-font-query", Text = _fontQuery, PlaceholderText = "Name, z. B. Lora, Roboto – leer = alle", Width = 240 };
        q.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _ = FontSearch(q.Text ?? ""); e.Handled = true; } };
        var cat = new ComboBox { Name = "mb-font-cat", ItemsSource = FreeFonts.Categories.Select(c => c.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(FreeFonts.Categories, c => c.Id == _fontCat)), MinWidth = 190 };
        cat.SelectionChanged += (_, _) => _fontCat = FreeFonts.Categories[Math.Max(0, cat.SelectedIndex)].Id;
        var search = Act("mb-font-search", _mbBusy ? "Sucht …" : "Suchen", () => _ = FontSearch(q.Text ?? ""), true); search.IsEnabled = !_mbBusy;
        var sp = new StackPanel { Name = "mb-fonts", Spacing = 6, Children = { new WrapPanel { Children = { q, new Border { Width = 6 }, cat, new Border { Width = 6 }, search } }, FontSampleBar() } };
        if (FontHits.Count == 0) sp.Children.Add(P("Freie Schriften wie bei Google Fonts – alle mit offener Lizenz (SIL OFL, Apache), auch für Geld nutzbar. Geladen wird als TTF mit Lizenzdatei.", Muted, 12));
        var i = 0;
        foreach (var f in FontHits.Take(60))
        {
            var ff = f;
            var idx = i;
            sp.Children.Add(Card(Row(new StackPanel { Children = { new TextBlock { Text = f.Family, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                P($"{f.Kategorie} · {f.License} · {f.Weights.Length} Stärken{(f.Variable ? " · variabel" : "")}", null, 10.5), FontSample(f, idx) } }, Act($"mb-font-get-{i++}", "Laden", () => _ = FontGet(ff)))));
        }
        sp.Children.Add(Act("mb-font-folder", "Schriften-Ordner öffnen", () => { var d = Path.Combine(AssetFolder, "Medien", "Schriften"); Directory.CreateDirectory(d); Launch(d); }));
        return sp;
    }

    private Control AssetView()
    {
        var tabs = new WrapPanel { Name = "mb-tabs" };
        foreach (var (k, l) in new[] { ("bilder", "Bilder"), ("klaenge", "Klänge"), ("schriften", "Schriften"), ("symbole", "Symbole"), ("3d", "3D & Texturen"), ("merklisten", "☆ Merklisten"), ("bibliothek", "Meine Bibliothek") })
        {
            var b = new Button { Name = "mb-tab-" + k, Content = l, Padding = new Thickness(14, 5), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(14), Foreground = Text,
                Background = MediaTab == k ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { if (MediaTab != k && k is "bilder" or "klaenge") { MediaHits = []; MediaSel = -1; _mbFormat = ""; } MediaTab = k; _mbMsg = ""; Show(MB); };
            tabs.Children.Add(b);
        }
        Control body = MediaTab switch { "3d" => AssetPolyHaven(), "bibliothek" => MediaLibraryView(), "merklisten" => MediaCollectionsView(), "schriften" => FontsView(), "symbole" => IconsView(), _ => MediaBrowse() }; // Alle Studios, Runde 8: Iconify
        var sp = new StackPanel { Spacing = 8, Children = { P("Freie Bilder, Klänge, Modelle und Texturen. Alles hier darfst du kostenlos nutzen, auch für Geld – bei manchen musst du den Urheber nennen, das steht immer dabei."), tabs } };
        if (_mbMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "mb-msg", Text = _mbMsg, Foreground = _mbMsg.StartsWith('⚠') ? Brush.Parse("#F97316") : GoldText(), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(body);
        return KreativPage("Freie Bilder & Klänge", sp);
    }

    /// <summary>Start ohne Suche: Themen-Kacheln, ein Klick sucht sofort (statt leerer Seite).</summary>
    Control MediaThemes(bool sound)
    {
        var themes = sound
            ? new[] { ("🌧", "Regen", "#1E3A5F"), ("🌲", "Wald", "#14532D"), ("🌊", "Meer", "#0C4A6E"), ("🐦", "Vögel", "#3F6212"), ("🏙", "Stadt", "#3F3F46"), ("🎹", "Klavier", "#4C1D95"), ("👏", "Applaus", "#78350F"), ("🔥", "Feuer", "#7C2D12") }
            : new[] { ("🏔", "Berge", "#1E3A5F"), ("🌊", "Meer", "#0C4A6E"), ("🌳", "Natur", "#14532D"), ("🏙", "Stadt", "#3F3F46"), ("🐾", "Tiere", "#78350F"), ("🌸", "Blumen", "#831843"), ("🍎", "Essen", "#7F1D1D"), ("✨", "Weltall", "#1E1B4B") };
        var wp = new WrapPanel { Name = "mb-themes" };
        var i = 0;
        foreach (var (icon, name, col) in themes)
        {
            var n = name;
            var b = new Button { Name = $"mb-theme-{i++}", Width = 170, Height = 104, Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(12), Padding = new Thickness(0), BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(col), 0), new GradientStop(Color.Parse("#0B1424"), 1) } },
                Content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 4, Children = {
                    new TextBlock { Text = icon, FontSize = 30, HorizontalAlignment = HorizontalAlignment.Center }, new TextBlock { Text = n, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center } } } };
            b.Click += (_, _) => { _mbQuery = n; _ = MediaSearch(n); };
            wp.Children.Add(b);
        }
        return new StackPanel { Spacing = 8, Children = { new TextBlock { Text = sound ? "Klänge entdecken" : "Bilder entdecken", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold },
            P(sound ? "Thema antippen oder oben suchen. Mit Doppelklick hörst du einen Klang vor." : "Thema antippen oder oben suchen – alles mit freier Lizenz.", Muted, 12), wp } };
    }

    Control MediaBrowse()
    {
        var sound = MediaTab == "klaenge";
        var q = new TextBox { Name = "mb-query", Text = _mbQuery, PlaceholderText = sound ? "z. B. Regen, Vogel, Klavier" : "z. B. Berge, Apfel, Stadt", Width = 240 };
        q.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _ = MediaSearch(q.Text ?? ""); e.Handled = true; } };
        var lic = new ComboBox { Name = "mb-license", ItemsSource = MediaLibrary.LicenseFilters.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(MediaLibrary.LicenseFilters, x => x.Id == _mbLicense)), MinWidth = 200 };
        lic.SelectionChanged += (_, _) => _mbLicense = MediaLibrary.LicenseFilters[Math.Max(0, lic.SelectedIndex)].Id;
        var formats = new List<string> { "" }; formats.AddRange(sound ? MediaLibrary.SoundFormats : MediaLibrary.ImageFormats);
        var fmt = new ComboBox { Name = "mb-format", ItemsSource = formats.Select(f => f.Length == 0 ? "Alle Formate" : f.ToUpperInvariant()).ToList(), SelectedIndex = Math.Max(0, formats.IndexOf(_mbFormat)), MinWidth = 110 };
        fmt.SelectionChanged += (_, _) => _mbFormat = formats[Math.Max(0, fmt.SelectedIndex)];
        var search = Act("mb-search", _mbBusy ? "Sucht …" : "Suchen", () => _ = MediaSearch(q.Text ?? ""), true); search.IsEnabled = !_mbBusy;
        var prov = new ComboBox { Name = "mb-provider", ItemsSource = MediaLibrary.Providers.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(MediaLibrary.Providers, x => x.Id == MediaProvider)), MinWidth = 150 };
        prov.SelectionChanged += (_, _) => MediaProvider = MediaLibrary.Providers[Math.Max(0, prov.SelectedIndex)].Id;
        var bar = new WrapPanel { Children = { q, new Border { Width = 6 }, prov, new Border { Width = 6 }, lic, new Border { Width = 6 }, fmt, new Border { Width = 6 } } };
        if (!sound)
        {
            // Ausrichtung wie bei Unsplash/Pexels – filtert sofort ohne neue Suche
            var ori = new ComboBox { Name = "mb-orient", ItemsSource = MediaLibrary.Orientations.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(MediaLibrary.Orientations, x => x.Id == MediaOrient)), MinWidth = 140 };
            ori.SelectionChanged += (_, _) => { var id = MediaLibrary.Orientations[Math.Max(0, ori.SelectedIndex)].Id; if (id == MediaOrient) return; MediaOrient = id; MediaHits = MediaLibrary.Filter(_mbAll, MediaOrient); MediaSel = MediaHits.Count > 0 ? 0 : -1; Show(MB); };
            bar.Children.Add(ori); bar.Children.Add(new Border { Width = 6 });
        }
        bar.Children.Add(search);

        // Raster
        var grid = new WrapPanel { Name = "mb-grid" };
        for (var i = 0; i < MediaHits.Count && i < 50; i++)
        {
            var h = MediaHits[i]; var idx = i; var on = i == MediaSel;
            Control pic = _mbThumbs.TryGetValue(h.Id, out var tp) && tp is not null && BibBitmap(tp) is { } bmp
                ? new Image { Source = bmp, Width = 150, Height = 100, Stretch = Stretch.UniformToFill }
                : new Border { Width = 150, Height = 100, Background = Brush.Parse(sound ? "#1FC9A227" : "#14000000"), Child = new TextBlock { Text = sound ? "♪" : "▧", FontSize = 34, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var tile = new Button { Name = $"mb-hit-{i}", Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 8), Width = 152, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = TileBg, BorderBrush = on ? Brush.Parse("#C9A227") : Edge, BorderThickness = new Thickness(on ? 2 : 1),
                Content = new StackPanel { Children = { pic, new StackPanel { Margin = new Thickness(6, 4), Children = {
                    new TextBlock { Text = h.Title, Foreground = Text, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = (h.Free ? "frei · " : "Nennung · ") + h.Format.ToUpperInvariant() + (sound && h.Seconds > 0 ? $" · {TimeSpan.FromSeconds(h.Seconds):m\\:ss}" : ""), Foreground = h.Free ? Brush.Parse("#22B35E") : GoldText(), FontSize = 10 } } } } } };
            tile.Click += (_, _) => { MediaSel = idx; Show(MB); };
            tile.DoubleTapped += (_, _) => { if (sound) _ = MediaListen(h); };
            grid.Children.Add(tile);
        }
        Control left = MediaHits.Count == 0
            ? (_mbBusy ? P("Suche läuft …", Muted, 12) : MediaThemes(sound)) // Startseite mit Themen wie auf Foto- und Klang-Plattformen
            : new ScrollViewer { Content = grid, MaxHeight = 640, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        // Vorschau rechts
        var right = new StackPanel { Name = "mb-preview", Spacing = 6 };
        if (MediaSel >= 0 && MediaSel < MediaHits.Count)
        {
            var h = MediaHits[MediaSel];
            if (!sound && _mbThumbs.TryGetValue(h.Id, out var tp2) && tp2 is not null && BibBitmap(tp2) is { } big)
                right.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = new Image { Name = "mb-big", Source = big, MaxHeight = 240, Stretch = Stretch.Uniform } });
            right.Children.Add(H(h.Title, 14));
            right.Children.Add(new Border { Name = "mb-license-badge", HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2),
                Background = Brush.Parse(h.Free ? "#3322B35E" : "#33E0A82E"),
                Child = new TextBlock { Text = h.Free ? $"{h.License} – frei, ohne Bedingungen" : $"{h.License} – Urheber nennen", Foreground = Text, FontSize = 11.5 } });
            if (h.Creator.Length > 0) right.Children.Add(P("Urheber: " + h.Creator, Text, 12));
            right.Children.Add(P($"{h.Format.ToUpperInvariant()} · {(h.Bytes > 0 ? (h.Bytes >= 1 << 20 ? $"{h.Bytes / 1048576.0:0.0} MB" : $"{h.Bytes / 1024} KB") : "Größe unbekannt")}"
                + (sound ? (h.Seconds > 0 ? $" · {TimeSpan.FromSeconds(h.Seconds):m\\:ss} min" : "") : h.Width > 0 ? $" · {h.Width} × {h.Height} Pixel" : ""), Muted, 11.5));
            var acts = new WrapPanel();
            if (sound)
            {
                acts.Children.Add(Act("mb-listen", "▶ Vorhören", () => _ = MediaListen(h), true)); acts.Children.Add(new Border { Width = 6 });
                acts.Children.Add(Act("mb-stop", "■ Stopp", () => { _audio?.Stop(); MediaListening = null; _mbMsg = ""; Show(MB); })); acts.Children.Add(new Border { Width = 6 });
            }
            var get = Act("mb-get", "In die Bibliothek", () => _ = MediaGet(h), !sound); get.IsEnabled = !_mbBusy;
            acts.Children.Add(get); acts.Children.Add(new Border { Width = 6 });
            acts.Children.Add(Act("mb-page", "Auf der Quelle ansehen", () => Launch(h.PageUrl)));
            right.Children.Add(acts);
            right.Children.Add(MediaHitTools(h)); // Profi 29: ähnliche suchen, merken
            right.Children.Add(P("Quelle: " + h.Provider, Muted, 11));
            var cf = new ComboBox { Name = "mb-credit-fmt", ItemsSource = MediaLibrary.CreditFormats.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(MediaLibrary.CreditFormats, x => x.Id == _mbCreditFmt)), MinWidth = 140 };
            cf.SelectionChanged += (_, _) => { var id = MediaLibrary.CreditFormats[Math.Max(0, cf.SelectedIndex)].Id; if (id != _mbCreditFmt) { _mbCreditFmt = id; Show(MB); } };
            right.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Namensnennung als ", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, cf } });
            var credit = MediaLibrary.CreditAs(h, _mbCreditFmt);
            right.Children.Add(new SelectableTextBlock { Name = "mb-credit", Text = credit, Foreground = Text, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            right.Children.Add(Act("mb-credit-copy", "Namensnennung kopieren", () => MediaCopy(credit, "Namensnennung")));
        }
        else right.Children.Add(P("Tippe ein Bild oder einen Klang an – hier siehst du Urheber, Lizenz und Format.", Muted, 11.5));
        var rightCard = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), VerticalAlignment = VerticalAlignment.Top, Child = right };
        var split = new Grid { Name = "mb-window", ColumnDefinitions = new ColumnDefinitions("*,12,300"), Children = { left, Pos(rightCard, 2) } };
        return new StackPanel { Spacing = 8, Children = { bar, split, P("Quellen: Wikimedia Commons und Openverse (Flickr, Freesound, Jamendo …) – nur freie Lizenzen, nicht-kommerzielle und „keine Bearbeitung“ fallen heraus. Gesucht wird erst, wenn du „Suchen“ antippst.", Muted, 10.5) } };
    }

    Control MediaLibraryView()
    {
        var lib = MediaLib; var items = lib.Items().OrderByDescending(i => i.Added).ToList();
        var sp = new StackPanel { Name = "mb-library", Spacing = 4 };
        sp.Children.Add(new WrapPanel { Children = { Act("mb-lib-folder", "Ordner öffnen", () => { Directory.CreateDirectory(Path.Combine(AssetFolder, "Medien")); Launch(Path.Combine(AssetFolder, "Medien")); }), new Border { Width = 6 },
            Act("mb-credits-copy", "Alle Namensnennungen kopieren", () => MediaCopy(lib.Credits(), "Namensnennungen")), new Border { Width = 6 }, Act("asset-folder", "Assets-Ordner öffnen", () => Launch(AssetFolder)) } });
        if (items.Count == 0) sp.Children.Add(P("Noch nichts geladen. Such unter „Bilder“ oder „Klänge“ und tippe „In die Bibliothek“.", Muted, 12));
        var i = 0;
        foreach (var it in items)
        {
            var file = it.File;
            var b = new Button { Name = $"mb-lib-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(6, 4),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                    new TextBlock { Text = it.Kind == "klang" ? "♪" : "▧", Foreground = Muted, FontSize = 16, Width = 24, VerticalAlignment = VerticalAlignment.Center },
                    Pos(new StackPanel { Children = { new TextBlock { Text = it.Name, Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = (it.Author.Length > 0 ? it.Author + " · " : "") + it.License + " · " + it.Source, Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
                    Pos(P(it.Added.ToString("dd.MM.yyyy", De), null, 10.5), 2) } } };
            ToolTip.SetTip(b, "Öffnen"); b.Click += (_, _) => Try(MB, () => { if (File.Exists(file)) Launch(file); else throw new FileNotFoundException("Die Datei ist nicht mehr da."); });
            var item = it;
            var edit = Act($"mb-lib-edit-{i - 1}", it.Kind == "klang" ? "Im Audio-Editor" : "In Bildbearbeitung", () => Try(MB, () => MediaEdit(item)));
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { b, Pos(edit, 1) } });
        }
        return Card(sp);
    }
}
