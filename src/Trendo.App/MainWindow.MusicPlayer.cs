using Avalonia;
using Avalonia.Controls;
using RangeBase = Avalonia.Controls.Primitives.RangeBase;
using Track = Trendo.Core.Media.Track;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Medien › Musik & Videos als Player (Masterprompt 131): Bibliothek nach Alben mit Cover, Titel, Künstler, Wiedergabelisten, Videos;
/// Player-Leiste mit Fortschritt zum Springen, Zufall, Wiederholen, Lautstärke und Warteschlange. Ton über mpv im Hintergrund.</summary>
public partial class MainWindow
{
    public List<string>? MusicFoldersOverride { get; set; }
    public Func<IAudioOut>? AudioOverride { get; set; }
    public Playlists MusicLists => new(_core.DataRoot);
    List<string> MusicFolders => MusicFoldersOverride ?? [UserFolder(Environment.SpecialFolder.MyMusic), UserFolder(Environment.SpecialFolder.MyVideos), ProjectsRoot];
    List<Track>? _tracks; Task? _trackLoad;
    readonly PlayQueue _queue = new(); IAudioOut? _audio;
    double _mPos, _mDur = double.NaN; bool _mPaused, _mQueueOpen, _mSeeking; int _mVol = 80;
    string _musicView = "alben", _musicFind = ""; string? _musicListPick;
    readonly Dictionary<string, Bitmap?> _covers = new(StringComparer.OrdinalIgnoreCase);
    Slider? _mSeek; TextBlock? _mTimeL, _mTimeR;
    public IReadOnlyList<Track> MusicTracks => _tracks ?? [];
    public PlayQueue MusicQueue => _queue;
    public bool MusicPaused => _mPaused;
    /// <summary>Für Vorschaubilder: Bereich, Ansicht und Fortschritt setzen.</summary>
    public void MediaTabForPreview(string tab) => _mediaTab = tab;
    public void MusicViewForPreview(string view) => _musicView = view;
    public void MusicProgressForPreview(double pos, double dur) { _mPos = pos; _mDur = dur; MusicTick(); }

    public Task MusicLoadAsync()
    {
        var folders = MusicFolders;
        return _trackLoad = Task.Run(() => MusicLibrary.Scan(folders)).ContinueWith(t => _ui.Post(() =>
        { _tracks = t.IsCompletedSuccessfully ? t.Result : []; _trackLoad = null; if (CurrentView == "medien") Show("medien"); }), TaskScheduler.Default);
    }

    Track? TrackOf(string path) => _tracks?.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));

    IAudioOut? Audio()
    {
        if (_audio is not null) return _audio;
        if (AudioOverride is { } o) _audio = o();
        else if (MpvExe is { } mpv) _audio = new MpvAudio(mpv);
        else return null;
        _audio.Progress += (pos, dur) => _ui.Post(() => { _mPos = pos; _mDur = dur; MusicTick(); });
        _audio.Ended += () => _ui.Post(MusicEnded);
        _audio.SetVolume(_mVol);
        Closed += (_, _) => _audio?.Dispose();
        return _audio;
    }

    /// <summary>Liste ab einem Titel abspielen (öffentlich für Tests und die KI).</summary>
    public void MusicPlay(IReadOnlyList<string> paths, int start = 0, string page = "medien") { _mPage = page; _queue.Play(paths, start); MusicStart(); }
    /// <summary>Seite, die den Player zeigt (Medien oder Trendo Musik) – sie wird nach Start, Pause und Ende neu gezeichnet.</summary>
    string _mPage = "medien";
    void MusicShow() => Show(_mPage == MS && CurrentView != "medien" ? MS : "medien");
    void MusicStart()
    {
        if (_queue.Current is not { } cur) { _audio?.Stop(); MusicShow(); return; }
        if (Audio() is not { } a) { Say("Zum Abspielen braucht Trendo den freien Player mpv – unten „Player einrichten“ antippen.", "medien"); Show("medien"); return; }
        a.Load(cur); _mPaused = false; _mPos = 0; _mDur = double.NaN;
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.play", Path.GetFileName(cur));
        MusicShow();
    }
    /// <summary>Titel zu Ende (vom Player gemeldet) – öffentlich für Tests.</summary>
    public void MusicEnded() { if (_queue.Next(auto: true) is null) { _mPaused = true; _audio?.Stop(); MusicShow(); } else MusicStart(); }
    public void MusicNext() { if (_queue.Next() is not null) MusicStart(); }
    public void MusicPrev() { if (_mPos > 4 && _queue.Current is not null) { _audio?.Seek(0); return; } if (_queue.Prev() is not null) MusicStart(); }
    public void MusicTogglePause() { if (_queue.Current is null) return; _mPaused = !_mPaused; _audio?.SetPause(_mPaused); MusicShow(); }

    void MusicTick()
    {
        if (_mSeek is null || _mSeeking) { MsFollowTick(); return; }
        _mSeek.Maximum = double.IsNaN(_mDur) || _mDur <= 0 ? 1 : _mDur; _mSeek.Value = Math.Min(_mSeek.Maximum, Math.Max(0, _mPos));
        if (_mTimeL is not null) _mTimeL.Text = MusicLibrary.Time(_mPos); if (_mTimeR is not null) _mTimeR.Text = MusicLibrary.Time(_mDur);
        MsFollowTick(); // Trendo Musik: mitlaufender Liedtext und Taktraster
        MsDawTick(); // Trendo Musik: Arrangement und Takt-Anzeige
    }

    void CoverInto(Image img, string trackPath, int width)
    {
        if (_covers.TryGetValue(trackPath, out var c)) { img.Source = c; return; }
        _ = Task.Run(() =>
        {
            Bitmap? b = null; try { if (MusicLibrary.Cover(trackPath) is { } bytes) b = Bitmap.DecodeToWidth(new MemoryStream(bytes), width); } catch (Exception) { }
            _ui.Post(() => { _covers[trackPath] = b; img.Source = b; });
        });
    }

    /// <summary>Cover-Kachel: eingebettetes Bild, sonst ein Verlauf mit Anfangsbuchstaben.</summary>
    Control CoverTile(string trackPath, string title, double size)
    {
        var h = StableHash(title); var hue = h % 360;
        Color Hsl(double l) { var hh = hue / 60.0; var x = 1 - Math.Abs(hh % 2 - 1); var (r, g, b) = hh switch { < 1 => (1.0, x, 0.0), < 2 => (x, 1.0, 0.0), < 3 => (0.0, 1.0, x), < 4 => (0.0, x, 1.0), < 5 => (x, 0.0, 1.0), _ => (1.0, 0.0, x) }; return Color.FromRgb((byte)(r * 255 * l), (byte)(g * 255 * l), (byte)(b * 255 * l)); }
        var initials = string.Concat(title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
        var img = new Image { Width = size, Height = size, Stretch = Stretch.UniformToFill };
        CoverInto(img, trackPath, (int)Math.Max(120, size * 1.5));
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size > 80 ? 10 : 6), ClipToBounds = true, BoxShadow = BoxShadows.Parse("0 6 16 -4 #88000000"),
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Hsl(0.75), 0), new GradientStop(Hsl(0.28), 1) } },
            Child = new Grid { Children = { new TextBlock { Text = initials, FontSize = size * 0.32, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#DDFFFFFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, img } } };
    }

    Control MusicView()
    {
        if (_tracks is null)
        {
            _trackLoad ??= MusicLoadAsync();
            return new StackPanel { Name = "music-loading", Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 50), Children = {
                new TextBlock { Text = "Deine Musik wird eingelesen …", FontSize = 18, Foreground = GoldText(), HorizontalAlignment = HorizontalAlignment.Center }, new ProgressBar { IsIndeterminate = true, Width = 260 } } };
        }
        var music = _tracks.Where(t => !t.Video).ToList(); var videos = _tracks.Where(t => t.Video).ToList();
        var albums = MusicLibrary.Albums(music); var artists = MusicLibrary.Artists(music); var lists = MusicLists.All();
        // Seitenleiste
        var side = new StackPanel { Name = "music-side", Spacing = 3, Width = 210 };
        Button Nav(string name, string label, string view, int count)
        {
            var on = _musicView == view || view == "alben" && _musicView.StartsWith("album:") || view == "kuenstler" && _musicView.StartsWith("artist:");
            var b = new Button { Name = name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = on ? Brush.Parse("#D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(1),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = label, Foreground = Text, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis }, Pos(new TextBlock { Text = count.ToString(), Foreground = Muted, FontSize = 12 }, 1) } } };
            b.Click += (_, _) => { _musicView = view; Show("medien"); }; Hover(b); return b;
        }
        side.Children.Add(Nav("music-nav-alben", "Alben", "alben", albums.Count));
        side.Children.Add(Nav("music-nav-titel", "Alle Titel", "titel", music.Count));
        side.Children.Add(Nav("music-nav-kuenstler", "Künstler", "kuenstler", artists.Count));
        side.Children.Add(Nav("music-nav-videos", "Videos", "videos", videos.Count));
        side.Children.Add(H("Wiedergabelisten", 12));
        for (var i = 0; i < lists.Count; i++) side.Children.Add(Nav($"music-nav-list-{i}", "≡ " + lists[i].Name, "liste:" + lists[i].Name, lists[i].Count));
        var newList = new TextBox { Name = "music-list-name", PlaceholderText = "Neue Liste …" };
        side.Children.Add(newList);
        side.Children.Add(Act("music-list-create", "Liste anlegen", () => Try("medien", () => { MusicLists.Create(newList.Text ?? ""); _musicView = "liste:" + newList.Text!.Trim(); _musicListPick = newList.Text.Trim(); Show("medien"); })));
        side.Children.Add(Act("music-reload", "Neu einlesen", () => { _tracks = null; Show("medien"); }));

        Control main = _musicView switch
        {
            "alben" => MusicAlbums(albums),
            "kuenstler" => MusicArtists(artists),
            "videos" => MusicVideos(videos),
            var v when v.StartsWith("album:") => albums.FirstOrDefault(a => "album:" + a.Artist + "|" + a.Album == v) is { Tracks: not null } al && al.Tracks.Count > 0 ? MusicAlbum(al.Artist, al.Album, al.Tracks) : MusicAlbums(albums),
            var v when v.StartsWith("artist:") => MusicTrackList($"{v[7..]}", music.Where(t => t.Artist == v[7..]).ToList(), null),
            var v when v.StartsWith("liste:") => MusicTrackList("≡ " + v[6..], MusicLists.Get(v[6..]).Select(p => TrackOf(p) ?? MusicLibrary.Read(p)).ToList(), v[6..]),
            _ => MusicTrackList("Alle Titel", MusicLibrary.Search(music, _musicFind), null, search: true),
        };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,14,*"), Children = { Glass2(side, 10), Pos(new ScrollViewer { Content = main, MaxHeight = 560 }, 2) } };
        var root = new StackPanel { Name = "music", Spacing = 10, Children = { body } };
        if (_mQueueOpen && _queue.Items.Count > 0) root.Children.Add(MusicQueuePanel());
        root.Children.Add(MusicPlayerBar());
        return root;
    }

    Control MusicAlbums(List<(string Artist, string Album, List<Track> Tracks)> albums)
    {
        var wrap = new WrapPanel { Name = "music-albums" };
        for (var i = 0; i < albums.Count && i < 400; i++)
        {
            var (artist, album, tracks) = albums[i]; var key = "album:" + artist + "|" + album; var playing = tracks.Any(t => t.Path == _queue.Current);
            var b = new Button { Name = $"music-album-{i}", Width = 176, Padding = new Thickness(8), Margin = new Thickness(0, 0, 10, 12), CornerRadius = new CornerRadius(12), Background = playing ? Brush.Parse("#22C9A227") : Brushes.Transparent,
                BorderBrush = Brush.Parse(playing ? "#D4A84B" : "#00000000"), BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 4, Children = { CoverTile(tracks[0].Path, album, 158),
                    new TextBlock { Text = album, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{artist} · {tracks.Count} Titel", Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            b.Click += (_, _) => { _musicView = key; Show("medien"); }; Hover(b);
            wrap.Children.Add(b);
        }
        if (albums.Count == 0) wrap.Children.Add(P("Keine Musik gefunden. Lege Titel in deinen Ordner „Musik“ (oder in den Projektordner) und tippe links „Neu einlesen“.", Muted, 13));
        return new StackPanel { Spacing = 8, Children = { H("Alben", 18), wrap } };
    }

    Control MusicArtists(List<(string Artist, int Albums, int Tracks)> artists)
    {
        var sp = new StackPanel { Name = "music-artists", Spacing = 2, Children = { H("Künstler", 18) } };
        for (var i = 0; i < artists.Count; i++)
        {
            var (a, al, n) = artists[i];
            var b = new Button { Name = $"music-artist-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(8, 5),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = a, Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold }, Pos(new TextBlock { Text = $"{al} Alben · {n} Titel", Foreground = Muted, FontSize = 12 }, 1) } } };
            b.Click += (_, _) => { _musicView = "artist:" + a; Show("medien"); }; Hover(b); sp.Children.Add(b);
        }
        return sp;
    }

    Control MusicVideos(List<Track> videos)
    {
        var sp = new StackPanel { Name = "music-videos", Spacing = 4, Children = { H("Videos", 18), P(MpvExe is not null ? "Videos öffnen im Player-Fenster (mpv)." : "Videos öffnen mit dem Standardprogramm.", Muted, 12) } };
        var i = 0; foreach (var v in videos.OrderByDescending(v => v.Modified).Take(200)) sp.Children.Add(FileRow(new FileInfo(v.Path), i++, "media", MpvExe));
        if (videos.Count == 0) sp.Children.Add(P("Keine Videos gefunden.", Muted, 12.5));
        return sp;
    }

    Control MusicAlbum(string artist, string album, List<Track> tracks)
    {
        var year = tracks.Select(t => t.Year).FirstOrDefault(y => y.Length > 0) ?? "";
        var paths = tracks.Select(t => t.Path).ToList();
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,18,*"), Children = { CoverTile(tracks[0].Path, album, 190),
            Pos(new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom, Children = {
                new TextBlock { Text = "ALBUM", Foreground = Muted, FontSize = 11, FontWeight = FontWeight.SemiBold },
                new TextBlock { Name = "music-album-title", Text = album, Foreground = Text, FontSize = 30, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = $"{artist}{(year.Length > 0 ? " · " + year : "")} · {tracks.Count} Titel", Foreground = GoldText(), FontSize = 14 },
                new WrapPanel { Children = { Act("music-album-play", "Abspielen", () => MusicPlay(paths), true), new Border { Width = 6 },
                    Act("music-album-shuffle", "Zufällig", () => { _queue.SetShuffle(true); MusicPlay(paths); }), new Border { Width = 6 },
                    Act("music-album-queue", "+ Warteschlange", () => { foreach (var p in paths) _queue.Enqueue(p); Say($"{paths.Count} Titel in der Warteschlange.", "medien"); if (_queue.Items.Count == paths.Count) MusicStart(); }), new Border { Width = 6 },
                    Act("music-back", "← Alle Alben", () => { _musicView = "alben"; Show("medien"); }) } } } }, 2) } };
        return new StackPanel { Spacing = 14, Children = { head, MusicTable(tracks, null, showAlbum: false) } };
    }

    Control MusicTrackList(string title, List<Track> tracks, string? list, bool search = false)
    {
        var sp = new StackPanel { Spacing = 8 };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H(title, 18) } };
        if (tracks.Count > 0) top.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Act("music-list-play", "Alle abspielen", () => MusicPlay(tracks.Select(t => t.Path).ToList()), true),
            Act("music-list-shuffle", "Zufällig", () => { _queue.SetShuffle(true); MusicPlay(tracks.Select(t => t.Path).ToList()); }) } }, 1));
        sp.Children.Add(top);
        if (search)
        {
            var find = new TextBox { Name = "music-find", Text = _musicFind, PlaceholderText = "Titel, Künstler oder Album …", Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
            find.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _musicFind = find.Text ?? ""; Show("medien"); } };
            sp.Children.Add(find);
        }
        if (list is not null)
            sp.Children.Add(new WrapPanel { Children = { Act("music-list-delete", "Liste löschen", () => { MusicLists.Delete(list); _musicView = "alben"; Show("medien"); }) } });
        sp.Children.Add(tracks.Count == 0 ? P(list is not null ? "Die Liste ist leer. In „Alle Titel“ oder einem Album bei einem Titel auf „＋ Liste“ tippen." : "Keine Titel.", Muted, 12.5) : MusicTable(tracks.Take(800).ToList(), list, showAlbum: true));
        return sp;
    }

    Control MusicTable(List<Track> tracks, string? list, bool showAlbum)
    {
        var lists = MusicLists.All().Select(l => l.Name).Where(n => n != list).ToList();
        _musicListPick = lists.Contains(_musicListPick ?? "") ? _musicListPick : lists.FirstOrDefault();
        var pick = new ComboBox { Name = "music-list-pick", ItemsSource = lists, SelectedItem = _musicListPick, Width = 180, PlaceholderText = "Liste wählen" };
        pick.SelectionChanged += (_, _) => _musicListPick = pick.SelectedItem as string;
        var sp = new StackPanel { Name = "music-tracks", Spacing = 1 };
        if (lists.Count > 0) sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 6), Children = { new TextBlock { Text = "„＋ Liste“ legt in:", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, pick } });
        var paths = tracks.Select(t => t.Path).ToList();
        for (var i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i]; var idx = i; var cur = t.Path == _queue.Current;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions(showAlbum ? "34,*,180,180,Auto,Auto" : "34,*,180,Auto,Auto"), Background = Brush.Parse(cur ? "#26C9A227" : i % 2 == 0 ? "#08FFFFFF" : "#00000000") };
            row.Children.Add(new TextBlock { Text = cur ? (_mPaused ? "❚❚" : "♪") : (t.No > 0 ? t.No.ToString() : (i + 1).ToString()), Foreground = cur ? GoldText() : Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            var play = new Button { Name = $"music-track-{i}", Background = Brushes.Transparent, Padding = new Thickness(6, 5), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new TextBlock { Text = t.Title, Foreground = cur ? GoldText() : Text, FontSize = 13, FontWeight = cur ? FontWeight.Bold : FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis } };
            ToolTip.SetTip(play, "Ab hier abspielen"); play.Click += (_, _) => MusicPlay(paths, idx);
            row.Children.Add(Pos(play, 1));
            row.Children.Add(Pos(new TextBlock { Text = t.Artist, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 2));
            var c = 3;
            if (showAlbum) row.Children.Add(Pos(new TextBlock { Text = t.Album, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, c++));
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = {
                Act($"music-next-{i}", "Als Nächstes", () => { _queue.PlayNext(t.Path); Say($"„{t.Title}“ kommt als Nächstes.", "medien"); if (_queue.Items.Count == 1) MusicStart(); }) } };
            if (list is null) tools.Children.Add(Act($"music-addlist-{i}", "＋ Liste", () => Try("medien", () => { if (_musicListPick is not { } l) throw new ArgumentException("Erst links eine Wiedergabeliste anlegen."); MusicLists.Add(l, [t.Path]); Say($"„{t.Title}“ in „{l}“.", "medien"); })));
            else tools.Children.Add(Act($"music-listdel-{i}", "✕", () => { MusicLists.RemoveAt(list, idx); Show("medien"); }));
            row.Children.Add(Pos(tools, c));
            sp.Children.Add(row);
        }
        return sp;
    }

    Control MusicQueuePanel()
    {
        var sp = new StackPanel { Name = "music-queue-panel", Spacing = 2 };
        sp.Children.Add(H($"Warteschlange · {_queue.Items.Count} Titel", 14));
        for (var i = 0; i < _queue.Items.Count && i < 200; i++)
        {
            var p = _queue.Items[i]; var t = TrackOf(p); var idx = i; var cur = i == _queue.Index;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*,Auto"), Opacity = i < _queue.Index ? 0.5 : 1, Children = {
                new TextBlock { Text = cur ? "♪" : (i + 1).ToString(), Foreground = cur ? GoldText() : Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
            var b = new Button { Name = $"music-q-{i}", Background = Brushes.Transparent, Padding = new Thickness(6, 3), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new TextBlock { Text = (t?.Title ?? Path.GetFileNameWithoutExtension(p)) + (t is null ? "" : " · " + t.Artist), Foreground = cur ? GoldText() : Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis } };
            b.Click += (_, _) => { _queue.Jump(idx); MusicStart(); };
            row.Children.Add(Pos(b, 1));
            row.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = {
                Act($"music-q-up-{i}", "↑", () => { _queue.Move(idx, idx - 1); Show("medien"); }), Act($"music-q-del-{i}", "✕", () => { var wasCur = idx == _queue.Index; _queue.RemoveAt(idx); if (wasCur) MusicStart(); else Show("medien"); }) } }, 2));
            sp.Children.Add(row);
        }
        return Card(new ScrollViewer { Content = sp, MaxHeight = 240 });
    }

    Control MusicPlayerBar()
    {
        var cur = _queue.Current is { } p ? TrackOf(p) ?? MusicLibrary.Read(p) : null;
        var bar = new Grid { Name = "music-player", ColumnDefinitions = new ColumnDefinitions("300,*,300") };
        // links: Titel
        var info = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        if (cur is not null) info.Children.Add(CoverTile(cur.Path, cur.Album, 56));
        info.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220, Children = {
            new TextBlock { Name = "music-now-title", Text = cur?.Title ?? "Nichts in der Wiedergabe", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
            new TextBlock { Text = cur is null ? "Album oder Titel antippen" : $"{cur.Artist} · {cur.Album}", Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } });
        bar.Children.Add(info);
        // Mitte: Steuerung + Fortschritt
        var rep = _queue.Repeat switch { "alle" => "🔁", "eins" => "🔂", _ => "↻" };
        var ctl = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, Children = {
            MusicRound("music-shuffle", "⤮", _queue.Shuffle ? "Zufall an" : "Zufall aus", () => { _queue.SetShuffle(!_queue.Shuffle); Show("medien"); }, _queue.Shuffle, 36),
            MusicRound("music-prev", "⏮", "Zurück", MusicPrev, false, 40),
            MusicRound("music-play", _mPaused || cur is null ? "▶" : "❚❚", _mPaused ? "Weiter" : "Pause", MusicTogglePause, true, 50),
            MusicRound("music-next", "⏭", "Weiter", MusicNext, false, 40),
            MusicRound("music-repeat", rep, "Wiederholen: " + _queue.Repeat, () => { _queue.Repeat = _queue.Repeat switch { "aus" => "alle", "alle" => "eins", _ => "aus" }; Show("medien"); }, _queue.Repeat != "aus", 36) } };
        _mTimeL = new TextBlock { Name = "music-time", Text = MusicLibrary.Time(_mPos), Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Width = 44, TextAlignment = TextAlignment.Right };
        _mTimeR = new TextBlock { Name = "music-duration", Text = MusicLibrary.Time(_mDur), Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Width = 44 };
        _mSeek = new Slider { Name = "music-seek", Minimum = 0, Maximum = double.IsNaN(_mDur) || _mDur <= 0 ? 1 : _mDur, Value = Math.Max(0, _mPos), Margin = new Thickness(8, 0), IsEnabled = cur is not null };
        var seek = _mSeek;
        seek.AddHandler(PointerPressedEvent, (_, _) => _mSeeking = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        seek.AddHandler(PointerReleasedEvent, (_, _) => { _mSeeking = false; _mPos = seek.Value; _audio?.Seek(seek.Value); }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        var progress = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { _mTimeL, Pos(seek, 1), Pos(_mTimeR, 2) } };
        bar.Children.Add(Pos(new StackPanel { Spacing = 2, Children = { ctl, progress } }, 1));
        // rechts: Lautstärke, Warteschlange, Player einrichten
        var vol = new Slider { Name = "music-volume", Minimum = 0, Maximum = 100, Value = _mVol, Width = 120, VerticalAlignment = VerticalAlignment.Center };
        vol.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) { _mVol = (int)vol.Value; _audio?.SetVolume(_mVol); } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Children = {
            new TextBlock { Text = "🔊", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, vol,
            Act("music-queue", (_mQueueOpen ? "• " : "") + $"Warteschlange ({_queue.Items.Count})", () => { _mQueueOpen = !_mQueueOpen; Show("medien"); }, _mQueueOpen) } };
        if (MpvExe is null && AudioOverride is null) right.Children.Insert(0, Act("music-setup", "Player einrichten", () => _ = MusicSetupPlayer(), true));
        bar.Children.Add(Pos(right, 2));
        return new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#F0363636"), 0), new GradientStop(Color.Parse("#F01C1C1C"), 1) } },
            BorderBrush = Brush.Parse("#44D4A84B"), BorderThickness = new Thickness(0, 1, 0, 0), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10), BoxShadow = BoxShadows.Parse("0 -6 20 -8 #AA000000"), Child = bar };
    }

    Button MusicRound(string name, string icon, string tip, Action click, bool gold, double size) => TvRound(name, icon, tip, click, gold, size);

    async Task MusicSetupPlayer()
    {
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        Say("Player wird eingerichtet …", "medien"); await InstallOne(DependencyCatalog.Get("mpv"));
        Say(MpvExe is not null ? "✓ Player ist eingerichtet." : "✗ Der Player ließ sich nicht einrichten. Ist Internet da? Sonst das Trendo-Vollpaket anschließen.", "medien"); Show("medien");
    }
}
