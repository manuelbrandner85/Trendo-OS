using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik V4 wie Suno (07.11.2026): Arbeitsbereiche, Playlists, Alben; Schnellfilter, Sortierung, Listen- oder Rasteransicht, Seiten.
/// Auf der Seite „Bibliothek“ links die Sammlungen zum Anlegen, Umbenennen und Löschen (Lieder bleiben erhalten).</summary>
public partial class MainWindow
{
    public MusicCollections MusicColl => new(_core.DataRoot);
    string? _msWs;      // Arbeitsbereich: null = „Mein Arbeitsbereich“, „*“ = alle Lieder
    string? _msColl;    // gewählte Playlist/Album
    string _msFilter = "alle", _msSort = "neu", _msNewColl = "";
    bool _msGridView; int _msPageNo;

    /// <summary>Für Tests und die KI: Ansicht der Bibliothek setzen.</summary>
    public void MsLibraryView(string? workspace = null, string? collection = null, string filter = "alle", string sort = "neu", bool grid = false, int page = 0)
    { _msWs = workspace; _msColl = collection; _msFilter = filter; _msSort = sort; _msGridView = grid; _msPageNo = page; if (CurrentView == MS) Show(MS); }

    public (List<MusicSong> Page, int Total, int Pages) MsQuery() =>
        SongQuery.Run(Songs.Visible(), MusicColl, _msColl is null ? _msWs : null, _msColl, _msFav ? "favoriten" : _msFilter, _msSort, _msSearch, _msPageNo);

    Control MsLibraryToolbar(Control import, Control search, Control fav)
    {
        var col = MusicColl; var ws = col.OfKind(MusicCollections.Workspace); var pls = col.OfKind(MusicCollections.Playlist); var albs = col.OfKind(MusicCollections.Album);
        // Bereich: Arbeitsbereiche, dann Playlists und Alben (wie Sunos „Workspaces ›“)
        var items = new List<(string Label, string? Ws, string? Coll)> { (MusicCollections.DefaultWorkspace, null, null) };
        items.AddRange(ws.Select(w => (w.Name, (string?)w.Id, (string?)null)));
        items.Add(("Alle Lieder", "*", null));
        items.AddRange(pls.Select(p => ("▶ " + p.Name, (string?)null, (string?)p.Id)));
        items.AddRange(albs.Select(a => ("◉ " + a.Name, (string?)null, (string?)a.Id)));
        var where = new ComboBox { Name = "ms-where", FontSize = 12, ItemsSource = items.Select(x => x.Label).ToList(), MinWidth = 150,
            SelectedIndex = Math.Max(0, items.FindIndex(x => x.Coll is not null ? x.Coll == _msColl : _msColl is null && x.Ws == _msWs)) };
        where.SelectionChanged += (_, _) => { if (where.SelectedIndex < 0) return; var it = items[where.SelectedIndex]; _msWs = it.Ws; _msColl = it.Coll; _msPageNo = 0; Show(MS); };
        var filter = new ComboBox { Name = "ms-filter", FontSize = 12, ItemsSource = SongQuery.Filters.Select(f => f.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(SongQuery.Filters, f => f.Id == _msFilter)) };
        filter.SelectionChanged += (_, _) => { _msFilter = SongQuery.Filters[Math.Max(0, filter.SelectedIndex)].Id; _msPageNo = 0; Show(MS); };
        var sort = new ComboBox { Name = "ms-sort", FontSize = 12, ItemsSource = SongQuery.Sorts.Select(f => f.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(SongQuery.Sorts, f => f.Id == _msSort)) };
        sort.SelectionChanged += (_, _) => { _msSort = SongQuery.Sorts[Math.Max(0, sort.SelectedIndex)].Id; Show(MS); };
        var view = new Button { Name = "ms-view", Content = _msGridView ? "☰ Liste" : "▦ Raster", FontSize = 12, Padding = new Thickness(10, 4) };
        ToolTip.SetTip(view, "Zwischen Liste und Raster (große Cover) wechseln");
        view.Click += (_, _) => { _msGridView = !_msGridView; Show(MS); };
        return new StackPanel { Spacing = 6, Children = {
            new WrapPanel { Children = { Gap(where), Gap(filter), Gap(sort), view } },
            new WrapPanel { Children = { Gap(import), Gap(search), fav } } } };
        static Control Gap(Control c) { c.Margin = new Thickness(0, 0, 6, 0); return c; }
    }

    /// <summary>Raster mit großen Covern (wie Sunos Kachelansicht).</summary>
    Control MsSongGrid(List<MusicSong> songs)
    {
        var wp = new WrapPanel { Name = "ms-grid-view" };
        var n = 0;
        foreach (var s in songs)
        {
            var song = s; var i = n++; var sel = s.Id == _msSel;
            var tile = new Button { Name = $"ms-tile-{i}", Width = 150, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(8), CornerRadius = new CornerRadius(12),
                Background = sel ? Brush.Parse("#26C9A227") : Brush.Parse("#10FFFFFF"), BorderBrush = sel ? Accent : Brushes.Transparent, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 4, Children = { MsCover(s, 132),
                    new TextBlock { Text = s.Title, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = SongTime(s.Seconds) + SongKindLabel(s.Kind), Foreground = Muted, FontSize = 10.5 } } } };
            tile.Click += (_, _) => { _msSel = song.Id; Show(MS); };
            tile.DoubleTapped += (_, _) => MsPlay(song);
            wp.Children.Add(tile);
        }
        return wp;
    }

    Control MsPager(int total, int pages)
    {
        if (pages <= 1) return new TextBlock { Name = "ms-count", Text = total == 1 ? "1 Lied" : $"{total} Lieder", Foreground = Muted, FontSize = 11, Margin = new Thickness(4, 6, 0, 0) };
        var prev = new Button { Name = "ms-page-prev", Content = "‹", IsEnabled = _msPageNo > 0, Padding = new Thickness(10, 2) };
        prev.Click += (_, _) => { _msPageNo = Math.Max(0, _msPageNo - 1); Show(MS); };
        var next = new Button { Name = "ms-page-next", Content = "›", IsEnabled = _msPageNo < pages - 1, Padding = new Thickness(10, 2) };
        next.Click += (_, _) => { _msPageNo = Math.Min(pages - 1, _msPageNo + 1); Show(MS); };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0), Children = {
            prev, new TextBlock { Name = "ms-count", Text = $"Seite {Math.Min(_msPageNo, pages - 1) + 1} von {pages} · {total} Lieder", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, next } };
    }

    /// <summary>„⋯“-Menü: Zu Playlist, Zu Album, In Arbeitsbereich (Untermenüs mit den vorhandenen Sammlungen und „Neue …“).</summary>
    IEnumerable<MenuItem> MsCollectionMenu(MusicSong s, int n)
    {
        var col = MusicColl;
        MenuItem Sub(string id, string label, string kind)
        {
            var mi = new MenuItem { Name = $"ms-menu-{n}-{id}", Header = label };
            foreach (var c in col.OfKind(kind))
            {
                var cc = c; var inIt = kind == MusicCollections.Workspace ? col.WorkspaceOf(s.Id) == c.Id : c.SongIds.Contains(s.Id);
                var item = new MenuItem { Header = (inIt ? "✓ " : "") + c.Name };
                item.Click += (_, _) => { if (kind == MusicCollections.Workspace) col.MoveToWorkspace(s.Id, cc.Id); else if (inIt) col.RemoveSong(cc.Id, s.Id); else col.AddSong(cc.Id, s.Id); _msMsg = $"„{s.Title}“ → {cc.Name}"; Show(MS); };
                mi.Items.Add(item);
            }
            if (kind == MusicCollections.Workspace)
            {
                var home = new MenuItem { Header = (col.WorkspaceOf(s.Id) is null ? "✓ " : "") + MusicCollections.DefaultWorkspace };
                home.Click += (_, _) => { col.MoveToWorkspace(s.Id, null); Show(MS); };
                mi.Items.Insert(0, home);
            }
            var neu = new MenuItem { Header = "Neu anlegen … (Seite Bibliothek)" };
            neu.Click += (_, _) => { _msSel = s.Id; MsPage("bibliothek"); };
            mi.Items.Add(neu);
            return mi;
        }
        yield return Sub("playlist", "♫ Zu Playlist", MusicCollections.Playlist);
        yield return Sub("album", "◉ Zu Album", MusicCollections.Album);
        yield return Sub("workspace", "▣ In Arbeitsbereich", MusicCollections.Workspace);
    }

    /// <summary>Seite „Bibliothek“: Sammlungen links (anlegen, umbenennen, löschen, Reihenfolge), Lieder rechts daneben.</summary>
    Control MsCollectionsPanel()
    {
        var col = MusicColl; var sp = new StackPanel { Name = "ms-collections", Spacing = 6 };
        sp.Children.Add(H("Sammlungen", 14));
        var name = new TextBox { Name = "ms-coll-name", Text = _msNewColl, Watermark = "Name", FontSize = 12 };
        name.TextChanged += (_, _) => _msNewColl = name.Text ?? "";
        sp.Children.Add(name);
        // Feld beim Klick direkt lesen – nicht auf das Änderungs-Ereignis warten
        void Make(string kind) { _msNewColl = name.Text ?? ""; try { var c = col.Create(kind, _msNewColl); _msNewColl = ""; if (kind == MusicCollections.Workspace) { _msWs = c.Id; _msColl = null; } else { _msColl = c.Id; } if (_msSel is { } sel && kind != MusicCollections.Workspace) col.AddSong(c.Id, sel); _msMsg = $"„{c.Name}“ angelegt."; } catch (ArgumentException e) { _msMsg = e.Message; } Show(MS); }
        sp.Children.Add(new WrapPanel { Children = {
            Act("ms-coll-new-playlist", "+ Playlist", () => Make(MusicCollections.Playlist)),
            Act("ms-coll-new-album", "+ Album", () => Make(MusicCollections.Album)),
            Act("ms-coll-new-workspace", "+ Arbeitsbereich", () => Make(MusicCollections.Workspace)) } });
        foreach (var (kind, title) in new[] { (MusicCollections.Workspace, "ARBEITSBEREICHE"), (MusicCollections.Playlist, "PLAYLISTS"), (MusicCollections.Album, "ALBEN") })
        {
            var list = col.OfKind(kind);
            sp.Children.Add(new TextBlock { Text = $"{title} ({list.Count})", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
            foreach (var c in list)
            {
                var cc = c; var on = kind == MusicCollections.Workspace ? _msColl is null && _msWs == c.Id : _msColl == c.Id;
                var open = new Button { Name = "ms-coll-" + c.Id, Content = $"{c.Name}  ·  {c.SongIds.Count}", HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12, Background = on ? Brush.Parse("#26C9A227") : Brushes.Transparent };
                open.Click += (_, _) => { if (kind == MusicCollections.Workspace) { _msWs = cc.Id; _msColl = null; } else _msColl = cc.Id; _msPageNo = 0; Show(MS); };
                var del = new Button { Name = "ms-coll-del-" + c.Id, Content = "🗑", FontSize = 11, Background = Brushes.Transparent };
                ToolTip.SetTip(del, "Sammlung löschen – die Lieder bleiben erhalten");
                del.Click += (_, _) => { col.Delete(cc.Id); if (_msColl == cc.Id) _msColl = null; if (_msWs == cc.Id) _msWs = null; _msMsg = $"„{cc.Name}“ gelöscht – die Lieder bleiben in der Bibliothek."; Show(MS); };
                sp.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), open } });
            }
        }
        if (_msColl is { } cid && col.Get(cid) is { } sel2 && _msSel is { } sid && sel2.SongIds.Contains(sid))
        {
            sp.Children.Add(new TextBlock { Text = "REIHENFOLGE", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
            sp.Children.Add(new WrapPanel { Children = {
                Act("ms-coll-up", "▲ nach oben", () => { col.Move(cid, sid, -1); Show(MS); }),
                Act("ms-coll-down", "▼ nach unten", () => { col.Move(cid, sid, 1); Show(MS); }),
                Act("ms-coll-remove", "aus der Sammlung nehmen", () => { col.RemoveSong(cid, sid); Show(MS); }) } });
        }
        return MsCard("ms-collections-card", new ScrollViewer { Content = sp });
    }
}
