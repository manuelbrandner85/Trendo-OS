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
        MediaHits = await lib.Search(kind, query, _mbLicense, _mbFormat); MediaSel = MediaHits.Count > 0 ? 0 : -1;
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
        _core.Audit.Append(Principal.LocalOwner.Name, "medien.download", $"{MediaLibrary.Source}/{h.Id} ({h.License})");
        return $"✓ „{item.Name}“ liegt in deiner Bibliothek – {(h.Free ? "frei nutzbar" : "bitte den Urheber nennen")} ({h.License}).";
    });

    private Control AssetView()
    {
        var tabs = new WrapPanel { Name = "mb-tabs" };
        foreach (var (k, l) in new[] { ("bilder", "Bilder"), ("klaenge", "Klänge"), ("3d", "3D & Texturen"), ("bibliothek", "Meine Bibliothek") })
        {
            var b = new Button { Name = "mb-tab-" + k, Content = l, Padding = new Thickness(14, 5), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(14), Foreground = Text,
                Background = MediaTab == k ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { if (MediaTab != k && k is "bilder" or "klaenge") { MediaHits = []; MediaSel = -1; _mbFormat = ""; } MediaTab = k; _mbMsg = ""; Show(MB); };
            tabs.Children.Add(b);
        }
        Control body = MediaTab switch { "3d" => AssetPolyHaven(), "bibliothek" => MediaLibraryView(), _ => MediaBrowse() };
        var sp = new StackPanel { Spacing = 8, Children = { P("Freie Bilder, Klänge, Modelle und Texturen. Alles hier darfst du kostenlos nutzen, auch für Geld – bei manchen musst du den Urheber nennen, das steht immer dabei."), tabs } };
        if (_mbMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "mb-msg", Text = _mbMsg, Foreground = _mbMsg.StartsWith('⚠') ? Brush.Parse("#F97316") : GoldText(), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(body);
        return KreativPage("Freie Bilder & Klänge", sp);
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
        var bar = new WrapPanel { Children = { q, new Border { Width = 6 }, lic, new Border { Width = 6 }, fmt, new Border { Width = 6 }, search } };

        // Raster
        var grid = new WrapPanel { Name = "mb-grid" };
        for (var i = 0; i < MediaHits.Count && i < 50; i++)
        {
            var h = MediaHits[i]; var idx = i; var on = i == MediaSel;
            Control pic = _mbThumbs.TryGetValue(h.Id, out var tp) && tp is not null && BibBitmap(tp) is { } bmp
                ? new Image { Source = bmp, Width = 150, Height = 100, Stretch = Stretch.UniformToFill }
                : new Border { Width = 150, Height = 100, Background = Brush.Parse(sound ? "#1F2F86F6" : "#14000000"), Child = new TextBlock { Text = sound ? "♪" : "▧", FontSize = 34, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var tile = new Button { Name = $"mb-hit-{i}", Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 8), Width = 152, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = TileBg, BorderBrush = on ? Brush.Parse("#2F86F6") : Edge, BorderThickness = new Thickness(on ? 2 : 1),
                Content = new StackPanel { Children = { pic, new StackPanel { Margin = new Thickness(6, 4), Children = {
                    new TextBlock { Text = h.Title, Foreground = Text, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = (h.Free ? "frei · " : "Nennung · ") + h.Format.ToUpperInvariant() + (sound && h.Seconds > 0 ? $" · {TimeSpan.FromSeconds(h.Seconds):m\\:ss}" : ""), Foreground = h.Free ? Brush.Parse("#22B35E") : GoldText(), FontSize = 10 } } } } } };
            tile.Click += (_, _) => { MediaSel = idx; Show(MB); };
            tile.DoubleTapped += (_, _) => { if (sound) _ = MediaListen(h); };
            grid.Children.Add(tile);
        }
        Control left = MediaHits.Count == 0
            ? P(_mbBusy ? "Suche läuft …" : sound ? "Such nach einem Klang – z. B. „Regen“. Mit Doppelklick hörst du ihn vor." : "Such nach einem Bild – z. B. „Berge“.", Muted, 12)
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
            acts.Children.Add(Act("mb-page", "Auf Commons ansehen", () => Launch(h.PageUrl)));
            right.Children.Add(acts);
            right.Children.Add(P("Namensnennung:", Muted, 11));
            right.Children.Add(new SelectableTextBlock { Name = "mb-credit", Text = h.Credit, Foreground = Text, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            right.Children.Add(Act("mb-credit-copy", "Namensnennung kopieren", () => MediaCopy(h.Credit, "Namensnennung")));
        }
        else right.Children.Add(P("Tippe ein Bild oder einen Klang an – hier siehst du Urheber, Lizenz und Format.", Muted, 11.5));
        var rightCard = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), VerticalAlignment = VerticalAlignment.Top, Child = right };
        var split = new Grid { Name = "mb-window", ColumnDefinitions = new ColumnDefinitions("*,12,300"), Children = { left, Pos(rightCard, 2) } };
        return new StackPanel { Spacing = 8, Children = { bar, split, P("Quelle: Wikimedia Commons – nur freie Lizenzen, nicht-kommerzielle fallen heraus. Gesucht wird erst, wenn du „Suchen“ antippst.", Muted, 10.5) } };
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
            sp.Children.Add(b);
        }
        return Card(sp);
    }
}
