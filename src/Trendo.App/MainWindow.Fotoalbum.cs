using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Medien › Fotos (Masterprompt 131): Fotos nach Monaten als Miniaturen, Vollbild mit Pfeiltasten, Favoriten und Alben,
/// Mehrfachauswahl, Doppelte finden – Löschen nur in den Papierkorb und nur nach „Ja“. Die Fotos selbst bleiben, wo sie sind.</summary>
public partial class MainWindow
{
    public Trendo.Core.Sys.PhotoAlbum Albums => new(_core.DataRoot);
    /// <summary>Für Tests und Vorschaubilder: diese Ordner statt „Bilder“ und Projektordner.</summary>
    public List<string>? PhotoFoldersOverride { get; set; }
    List<string> PhotoFolders => PhotoFoldersOverride ?? [UserFolder(Environment.SpecialFolder.MyPictures), ProjectsRoot, .. _settings.PhotoFolders];
    string _mediaTab = "fotos", _photoView = "alle"; string? _photoOpen; bool _photoAskTrash, _photoDupAsk;
    double _photoSize = 150; int _photoLimit = 300;
    readonly HashSet<string> _photoSel = new(StringComparer.OrdinalIgnoreCase);
    List<Photo>? _photos; Task? _photoLoad; List<List<Photo>>? _photoDups;
    readonly Dictionary<string, Bitmap?> _photoThumbs = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<Photo> PhotosLoaded => _photos ?? [];
    public string? PhotoOpen => _photoOpen;
    /// <summary>Foto groß öffnen (Vorschaubilder, KI).</summary>
    public void PhotoOpenForPreview(string path) { _photoOpen = path; _photoAskTrash = false; }
    public IReadOnlyCollection<string> PhotoSelection => _photoSel;

    /// <summary>Fotos (neu) einlesen – im Hintergrund, die Seite bleibt bedienbar.</summary>
    public Task PhotosLoadAsync()
    {
        var folders = PhotoFolders;
        return _photoLoad = Task.Run(() => Trendo.Core.Sys.PhotoAlbum.Scan(folders)).ContinueWith(t =>
        {
            _ui.Post(() => { _photos = t.IsCompletedSuccessfully ? t.Result : []; _photoLoad = null; _photoDups = null; if (CurrentView == "medien") Show("medien"); });
        }, TaskScheduler.Default);
    }

    void Thumb(Image img, string path, int width)
    {
        if (_photoThumbs.TryGetValue(path, out var bmp)) { img.Source = bmp; return; }
        _ = Task.Run(() =>
        {
            Bitmap? b = null;
            try { using var s = File.OpenRead(path); b = Bitmap.DecodeToWidth(s, width, BitmapInterpolationMode.MediumQuality); } catch (Exception) { /* kein lesbares Bild → Platzhalter */ }
            _ui.Post(() => { _photoThumbs[path] = b; img.Source = b; });
        });
    }

    Control MediaTabs()
    {
        var bar = new WrapPanel { Name = "media-tabs" };
        foreach (var (id, label) in new[] { ("fotos", "🖼 Fotos"), ("musik", "🎵 Musik & Videos") })
        {
            var on = _mediaTab == id; var b = Act("media-tab-" + id, label, () => { _mediaTab = id; Show("medien"); }, on); b.Margin = new Thickness(0, 0, 6, 6); bar.Children.Add(b);
        }
        return bar;
    }

    Control PhotoView()
    {
        if (_photos is null)
        {
            _photoLoad ??= PhotosLoadAsync();
            return new StackPanel { Name = "photo-loading", Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 50), Children = {
                new TextBlock { Text = "Deine Fotos werden eingelesen …", FontSize = 18, Foreground = GoldText(), HorizontalAlignment = HorizontalAlignment.Center },
                new ProgressBar { IsIndeterminate = true, Width = 260 } } };
        }
        if (_photoOpen is not null) return PhotoViewer();
        var albums = Albums.Albums();
        // Seitenleiste
        var side = new StackPanel { Name = "photo-side", Spacing = 3, Width = 220 };
        Button Nav(string name, string label, string view, int count)
        {
            var on = _photoView == view;
            var b = new Button { Name = name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = on ? Brush.Parse("#D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(1),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = label, Foreground = Text, FontSize = 13, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis },
                    Pos(new TextBlock { Text = count.ToString(), Foreground = Muted, FontSize = 12 }, 1) } } };
            b.Click += (_, _) => { _photoView = view; _photoSel.Clear(); _photoLimit = 300; Show("medien"); }; Hover(b); return b;
        }
        side.Children.Add(Nav("photo-nav-alle", "Alle Fotos", "alle", _photos.Count));
        var ai = 0;
        foreach (var (name, count) in albums) side.Children.Add(Nav($"photo-nav-album-{ai++}", name == Trendo.Core.Sys.PhotoAlbum.Favorites ? "★ Favoriten" : "▣ " + name, "album:" + name, count));
        side.Children.Add(Nav("photo-nav-dups", "⧉ Doppelte", "doppelte", _photoDups?.Sum(g => g.Count - 1) ?? 0));
        var newName = new TextBox { Name = "photo-album-name", PlaceholderText = "Neues Album …", Margin = new Thickness(0, 8, 0, 0) };
        newName.KeyDown += (_, e) => { if (e.Key == Key.Enter) Try("medien", () => { Albums.Create(newName.Text ?? ""); _photoView = "album:" + newName.Text!.Trim(); Show("medien"); }); };
        side.Children.Add(newName);
        side.Children.Add(Act("photo-album-create", "Album anlegen", () => Try("medien", () => { Albums.Create(newName.Text ?? ""); _photoView = "album:" + newName.Text!.Trim(); Show("medien"); })));
        side.Children.Add(H("Ordner", 12));
        foreach (var f in PhotoFolders.Where(Directory.Exists).Distinct()) side.Children.Add(new TextBlock { Text = "📁 " + Path.GetFileName(f.TrimEnd('\\', '/')), Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
        var folder = new TextBox { Name = "photo-add-folder", PlaceholderText = "Weiteren Ordner hinzufügen" };
        side.Children.Add(folder);
        side.Children.Add(Act("photo-add-folder-ok", "Ordner aufnehmen", () => Try("medien", () =>
        {
            var f = (folder.Text ?? "").Trim('"', ' '); if (!Directory.Exists(f)) throw new ArgumentException("Diesen Ordner gibt es nicht.");
            if (!_settings.PhotoFolders.Contains(f, StringComparer.OrdinalIgnoreCase)) { _settings.PhotoFolders.Add(f); SaveSettings(); }
            _photos = null; Show("medien");
        })));
        side.Children.Add(Act("photo-reload", "↻ Neu einlesen", () => { _photos = null; Show("medien"); }));

        var main = _photoView == "doppelte" ? PhotoDuplicates() : PhotoGrid();
        return new Grid { Name = "photo-album", ColumnDefinitions = new ColumnDefinitions("Auto,14,*"), Children = { Glass2(side, 10), Pos(main, 2) } };
    }

    List<Photo> PhotoCurrent()
    {
        if (!_photoView.StartsWith("album:")) return _photos!;
        var set = Albums.In(_photoView[6..]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _photos!.Where(p => set.Contains(p.Path)).ToList();
    }

    Control PhotoGrid()
    {
        var list = PhotoCurrent(); var album = _photoView.StartsWith("album:") ? _photoView[6..] : null;
        var favs = Albums.In(Trendo.Core.Sys.PhotoAlbum.Favorites).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sp = new StackPanel { Spacing = 8 };
        var size = new Slider { Name = "photo-size", Minimum = 90, Maximum = 280, Value = _photoSize, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        size.PropertyChanged += (_, e) => { if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && Math.Abs(size.Value - _photoSize) > 8) { _photoSize = size.Value; Show("medien"); } };
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
            new StackPanel { Children = { H(album is null ? "Alle Fotos" : album == Trendo.Core.Sys.PhotoAlbum.Favorites ? "★ Favoriten" : album, 18), new TextBlock { Name = "photo-count", Text = $"{list.Count} Fotos" + (list.Count > 0 ? $" · {list[^1].Taken:MMMM yyyy} bis {list[0].Taken:MMMM yyyy}" : ""), Foreground = Muted, FontSize = 12 } } },
            Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Größe", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, size } }, 1) } });
        // Auswahlleiste – wird bei Klicks nur neu befüllt (kein Neuaufbau der Seite, sonst käme ein Doppelklick nie an)
        var selHost = new StackPanel(); sp.Children.Add(selHost);
        void FillSel()
        {
            selHost.Children.Clear(); if (_photoSel.Count == 0) return;
            var names = Albums.Albums().Select(a => a.Name).Where(n => n != album).ToList();
            var pick = new ComboBox { Name = "photo-sel-album", ItemsSource = names, SelectedIndex = names.Count > 0 ? 0 : -1, Width = 170 };
            var bar = new WrapPanel { Name = "photo-selbar", Children = {
                new TextBlock { Text = $"{_photoSel.Count} ausgewählt", Foreground = GoldText(), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) },
                Act("photo-sel-fav", "★ Zu Favoriten", () => { Albums.Add(Trendo.Core.Sys.PhotoAlbum.Favorites, _photoSel); Say($"{_photoSel.Count} Fotos bei den Favoriten.", "medien"); }),
                pick, Act("photo-sel-add", "Zum Album", () => Try("medien", () => { if (pick.SelectedItem is not string a) throw new ArgumentException("Erst links ein Album anlegen."); var n = Albums.Add(a, _photoSel); Say($"{n} Fotos zu „{a}“ hinzugefügt.", "medien"); })) } };
            if (album is not null) bar.Children.Add(Act("photo-sel-remove", "Aus diesem Album nehmen", () => { Albums.Remove(album, _photoSel); _photoSel.Clear(); Show("medien"); }));
            bar.Children.Add(Act("photo-sel-none", "Auswahl aufheben", () => { _photoSel.Clear(); Show("medien"); }));
            foreach (var c in bar.Children.OfType<Button>()) c.Margin = new Thickness(0, 0, 6, 0);
            selHost.Children.Add(Card(bar));
        }
        FillSel();
        var tiles = new Dictionary<string, (Border Tile, Border Badge)>(StringComparer.OrdinalIgnoreCase);
        void Mark() { foreach (var (pth, (t, b)) in tiles) { var on = _photoSel.Contains(pth); t.BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"); t.BorderThickness = new Thickness(on ? 3 : 1); b.IsVisible = on; } FillSel(); }
        if (list.Count == 0) sp.Children.Add(P(album is null ? "Keine Fotos gefunden. Links einen Ordner mit Fotos aufnehmen." : "Dieses Album ist noch leer. Fotos auswählen (Klick, mit Strg mehrere) und „Zum Album“.", Muted, 13));
        var shown = 0; var idx = 0; var all = list;
        foreach (var (title, photos) in Trendo.Core.Sys.PhotoAlbum.ByMonth(list))
        {
            if (shown >= _photoLimit) break;
            sp.Children.Add(new TextBlock { Text = $"{title}  ·  {photos.Count}", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
            var wrap = new WrapPanel();
            foreach (var p in photos)
            {
                if (shown++ >= _photoLimit) break;
                var i = idx++; var path = p.Path; var sel = _photoSel.Contains(path);
                var img = new Image { Stretch = Stretch.UniformToFill, Width = _photoSize, Height = _photoSize * 0.75 };
                Thumb(img, path, (int)Math.Max(160, _photoSize * 1.4));
                var cell = new Grid { Children = { new Border { Background = Brush.Parse("#FF2D2D2D") }, new TextBlock { Text = "🖼", FontSize = 22, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, img } };
                if (favs.Contains(path)) cell.Children.Add(new TextBlock { Text = "★", FontSize = 16, Foreground = Brush.Parse("#FACC15"), Margin = new Thickness(6, 3), HorizontalAlignment = HorizontalAlignment.Right });
                var badge = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brush.Parse("#D4A84B"), Margin = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsVisible = sel,
                    Child = new TextBlock { Text = "✓", Foreground = Brush.Parse("#1B1406"), FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                cell.Children.Add(badge);
                var tile = new Border { Name = $"photo-{i}", Child = cell, CornerRadius = new CornerRadius(8), ClipToBounds = true, Margin = new Thickness(0, 0, 6, 6), Cursor = new Cursor(StandardCursorType.Hand),
                    BorderBrush = sel ? Brush.Parse("#D4A84B") : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(sel ? 3 : 1), BoxShadow = BoxShadows.Parse("0 3 8 0 #66000000") };
                ToolTip.SetTip(tile, $"{Path.GetFileName(path)}\n{p.Taken:dd.MM.yyyy HH:mm}{(p.FromExif ? " (Aufnahme)" : "")}");
                tile.PointerPressed += (_, e) =>
                {
                    if (!e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed) return; e.Handled = true;
                    if (e.ClickCount >= 2) { _photoOpen = path; _photoSel.Clear(); _photoAskTrash = false; Show("medien"); return; }
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) { if (!_photoSel.Remove(path)) _photoSel.Add(path); }
                    else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _photoSel.Count > 0) { var a = all.FindIndex(x => _photoSel.Contains(x.Path)); var b = all.FindIndex(x => x.Path == path); foreach (var x in all.Skip(Math.Min(a, b)).Take(Math.Abs(a - b) + 1)) _photoSel.Add(x.Path); }
                    else { var only = _photoSel.Count == 1 && _photoSel.Contains(path); _photoSel.Clear(); if (!only) _photoSel.Add(path); }
                    Mark();
                };
                tiles[path] = (tile, badge);
                wrap.Children.Add(tile);
            }
            sp.Children.Add(wrap);
        }
        if (list.Count > _photoLimit) sp.Children.Add(Act("photo-more", $"Weitere {Math.Min(300, list.Count - _photoLimit)} Fotos zeigen", () => { _photoLimit += 300; Show("medien"); }));
        sp.Children.Add(P("Klick wählt aus (Strg: mehrere, Umschalt: Bereich) · Doppelklick öffnet groß · Die Fotos bleiben in ihren Ordnern.", Muted, 11.5));
        return sp;
    }

    Control PhotoViewer()
    {
        var list = PhotoCurrent(); var i = list.FindIndex(p => string.Equals(p.Path, _photoOpen, StringComparison.OrdinalIgnoreCase));
        if (i < 0) { _photoOpen = null; return PhotoGrid(); }
        var p = list[i]; var fav = Albums.IsFavorite(p.Path);
        void Go(int d) { if (list.Count == 0) return; _photoOpen = list[(i + d + list.Count) % list.Count].Path; _photoAskTrash = false; Show("medien"); }
        var dim = PhotoSize(p.Path);
        Bitmap? big = null; try { using var s = File.OpenRead(p.Path); big = Bitmap.DecodeToWidth(s, Math.Min(1800, dim?.W ?? 1800), BitmapInterpolationMode.HighQuality); } catch (Exception) { }
        var img = new Image { Name = "photo-big", Source = big, Stretch = Stretch.Uniform, MaxHeight = 640 };
        var stage = new Border { Background = Brush.Parse("#FF05080F"), CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinHeight = 420, Child = big is null ? new TextBlock { Text = "Dieses Bild kann Trendo nicht anzeigen.", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : img };
        var info = new StackPanel { Name = "photo-info", Spacing = 4, Width = 300, Children = {
            new TextBlock { Text = Path.GetFileName(p.Path), Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            P($"{(p.FromExif ? "Aufgenommen" : "Datei vom")} {p.Taken:dddd, dd.MM.yyyy HH:mm}", Text, 12.5),
            P((dim is { } wh ? $"{wh.W} × {wh.H} px · {wh.W * (double)wh.H / 1e6:0.0} MP · " : "") + DiskMapView.Size(p.Bytes), Muted, 12),
            P(Path.GetDirectoryName(p.Path) ?? "", Muted, 11.5),
            P($"Foto {i + 1} von {list.Count}", Muted, 11.5) } };
        var acts = new StackPanel { Spacing = 6 };
        acts.Children.Add(Act("photo-fav", fav ? "★ Aus Favoriten nehmen" : "☆ Zu Favoriten", () => { Albums.ToggleFavorite(p.Path); Show("medien"); }, !fav));
        acts.Children.Add(Act("photo-edit", "In Bildbearbeitung öffnen", () => Try("medien", () => { ImgAddImage(p.Path); Show("bild"); })));
        acts.Children.Add(Act("photo-folder", "Im Ordner zeigen", () => Launch(Path.GetDirectoryName(p.Path)!)));
        if (!_photoAskTrash) acts.Children.Add(Act("photo-trash", "In den Papierkorb …", () => { _photoAskTrash = true; Show("medien"); }));
        else
        {
            var yes = Act("photo-trash-yes", "Ja, in den Papierkorb", () => Try("medien", () =>
            {
                var next = list.Count > 1 ? list[(i + 1) % list.Count].Path : null;
                if (TrashOverride is { } fake) fake(p.Path); else RecycleBin.Move(p.Path);
                Albums.Forget([p.Path]); _photos!.RemoveAll(x => x.Path == p.Path); _photoOpen = next; _photoAskTrash = false;
                _core.Audit.Append(Principal.LocalOwner.Name, "photo.trash", Path.GetFileName(p.Path)); Say("In den Papierkorb verschoben – dort lässt es sich wiederherstellen.", "medien");
            }), true);
            acts.Children.Add(new Border { Name = "photo-trash-ask", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "Dieses Foto in den Papierkorb verschieben?", Foreground = Text, TextWrapping = TextWrapping.Wrap },
                new WrapPanel { Children = { yes, new Border { Width = 6 }, Act("photo-trash-no", "Nein", () => { _photoAskTrash = false; Show("medien"); }) } } } } });
        }
        info.Children.Add(acts);
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
            Act("photo-back", "← Zurück zur Übersicht", () => { _photoOpen = null; Show("medien"); }),
            Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { Act("photo-prev", "◀", () => Go(-1)), Act("photo-next", "▶", () => Go(1)) } }, 1),
            Pos(new TextBlock { Text = "← → blättern · Esc schließt", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, 2) } };
        var root = new Grid { Name = "photo-viewer", Focusable = true, RowDefinitions = new RowDefinitions("Auto,10,*"), ColumnDefinitions = new ColumnDefinitions("*,14,Auto"), Children = { top, Pos(stage, 0), Pos(Glass2(info, 12), 2) } };
        Grid.SetColumnSpan(top, 3); Grid.SetRow(stage, 2); Grid.SetRow(root.Children[2], 2);
        root.KeyDown += (_, e) => { if (e.Key == Key.Right) { Go(1); e.Handled = true; } else if (e.Key == Key.Left) { Go(-1); e.Handled = true; } else if (e.Key == Key.Escape) { _photoOpen = null; Show("medien"); e.Handled = true; } };
        root.AttachedToVisualTree += (_, _) => _ui.Post(() => root.Focus());
        return root;
    }

    /// <summary>Echte Bildgröße aus dem Dateikopf (ohne das ganze Bild zu laden).</summary>
    static (int W, int H)? PhotoSize(string path)
    {
        try { using var c = SkiaSharp.SKCodec.Create(path); return c is null ? null : (c.Info.Width, c.Info.Height); } catch (Exception) { return null; }
    }

    Control PhotoDuplicates()
    {
        var sp = new StackPanel { Name = "photo-dups", Spacing = 8 };
        sp.Children.Add(H("⧉ Doppelte Fotos", 18));
        if (_photoDups is null)
        {
            sp.Children.Add(P("Trendo vergleicht den Inhalt (nicht nur den Namen). Das älteste Foto jeder Gruppe bleibt.", Muted, 12.5));
            sp.Children.Add(Act("photo-dups-find", "Doppelte suchen", () => { _photoDups = Trendo.Core.Sys.PhotoAlbum.Duplicates(_photos!); Show("medien"); }, true));
            return sp;
        }
        var extra = _photoDups.Sum(g => g.Count - 1); var bytes = _photoDups.Sum(g => g.Skip(1).Sum(x => x.Bytes));
        sp.Children.Add(new TextBlock { Name = "photo-dups-sum", Text = extra == 0 ? "Keine doppelten Fotos gefunden." : $"{_photoDups.Count} Gruppen · {extra} Kopien · {DiskMapView.Size(bytes)} frei, wenn die Kopien gehen", Foreground = Text, FontSize = 13 });
        var gi = 0;
        foreach (var g in _photoDups)
        {
            var row = new WrapPanel();
            for (var k = 0; k < g.Count; k++)
            {
                var img = new Image { Width = 120, Height = 90, Stretch = Stretch.UniformToFill }; Thumb(img, g[k].Path, 180);
                row.Children.Add(new StackPanel { Width = 130, Margin = new Thickness(0, 0, 8, 0), Children = { new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, BorderBrush = Brush.Parse(k == 0 ? "#2FBF71" : "#F0644B"), BorderThickness = new Thickness(2), Child = img },
                    new TextBlock { Text = (k == 0 ? "bleibt · " : "Kopie · ") + Path.GetFileName(Path.GetDirectoryName(g[k].Path)), Foreground = k == 0 ? Brush.Parse("#2FBF71") : Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } });
            }
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { new TextBlock { Text = $"{Path.GetFileName(g[0].Path)} · {g.Count}× · {DiskMapView.Size(g[0].Bytes)}", Foreground = Text, FontWeight = FontWeight.SemiBold }, row } }));
            if (++gi >= 50) break;
        }
        if (extra > 0)
        {
            if (!_photoDupAsk) sp.Children.Add(Act("photo-dups-trash", $"{extra} Kopien in den Papierkorb …", () => { _photoDupAsk = true; Show("medien"); }));
            else sp.Children.Add(new Border { Name = "photo-dups-ask", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = $"{extra} Kopien in den Papierkorb verschieben? Das älteste Foto jeder Gruppe bleibt.", Foreground = Text },
                new WrapPanel { Children = { Act("photo-dups-yes", "Ja, verschieben", () => Try("medien", () =>
                {
                    var gone = new List<string>();
                    foreach (var x in _photoDups.SelectMany(g => g.Skip(1))) { if (TrashOverride is { } fake) fake(x.Path); else RecycleBin.Move(x.Path); gone.Add(x.Path); }
                    Albums.Forget(gone); _photos!.RemoveAll(p => gone.Contains(p.Path)); _photoDups = null; _photoDupAsk = false;
                    _core.Audit.Append(Principal.LocalOwner.Name, "photo.dups_trash", gone.Count.ToString()); Say($"{gone.Count} Kopien im Papierkorb.", "medien");
                }), true), new Border { Width = 6 }, Act("photo-dups-no", "Nein", () => { _photoDupAsk = false; Show("medien"); }) } } } } });
        }
        return sp;
    }
}
