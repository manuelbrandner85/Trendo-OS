using System.Collections.Concurrent;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Karten;

namespace Trendo.App;

/// <summary>T-0260: OpenStreetMap-Kacheln nach der Nutzungsrichtlinie – eigene Kennung (User-Agent), Zwischenspeicher auf der Platte (30 Tage),
/// höchstens 2 gleichzeitige Abrufe, nur beim Ansehen (kein Massen-Download), im Offline-Modus nur aus dem Speicher.</summary>
public sealed class TileCache(string dir, bool online)
{
    static readonly HttpClient Http = CreateHttp();
    /// <summary>T-0274: derselbe Client mit Trendo-Kennung für Höhen- und Routing-Abrufe.</summary>
    public static HttpClient Shared => Http;
    static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd($"TrendoOS/{typeof(TileCache).Assembly.GetName().Version?.ToString(3) ?? "1"} (+https://github.com/manuelbrandner85/Trendo-OS)");
        return h;
    }
    readonly ConcurrentDictionary<(int, int, int), Bitmap?> _mem = new();
    readonly ConcurrentDictionary<(int, int, int), byte> _loading = new();
    readonly SemaphoreSlim _gate = new(2);
    public bool Online { get; set; } = online;
    public event Action? Loaded;
    /// <summary>T-0274b: Offline-Kartenpaket des Nutzers – hat Vorrang vor Zwischenspeicher und Internet.</summary>
    public Trendo.Core.Karten.TilePack? Pack { get => _pack; set { _pack?.Dispose(); _pack = value; _mem.Clear(); } }
    Trendo.Core.Karten.TilePack? _pack;

    public Bitmap? Get(int z, int x, int y)
    {
        var n = 1 << z; if (y < 0 || y >= n) return null; x = ((x % n) + n) % n;
        var key = (z, x, y);
        if (_mem.TryGetValue(key, out var b)) return b;
        if (_pack?.Get(z, x, y) is { } packed)
        {
            try { using var ms = new MemoryStream(packed); b = new Bitmap(ms); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { b = null; }
            Remember(key, b); return b;
        }
        var file = Path.Combine(dir, z.ToString(), x.ToString(), y + ".png");
        if (File.Exists(file) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(file)).TotalDays < 30 || File.Exists(file) && !Online)
        {
            try { b = new Bitmap(file); } catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { b = null; }
            Remember(key, b); return b;
        }
        if (Online && _loading.TryAdd(key, 0)) _ = Fetch(key, file);
        return null;
    }

    void Remember((int, int, int) key, Bitmap? b) { if (_mem.Count > 300) _mem.Clear(); _mem[key] = b; }

    async Task Fetch((int Z, int X, int Y) k, string file)
    {
        await _gate.WaitAsync();
        try
        {
            var bytes = await Http.GetByteArrayAsync($"https://tile.openstreetmap.org/{k.Z}/{k.X}/{k.Y}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllBytesAsync(file, bytes);
            Dispatcher.UIThread.Post(() => { try { using var ms = new MemoryStream(bytes); Remember(k, new Bitmap(ms)); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException) { } Loaded?.Invoke(); });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException) { }
        finally { _gate.Release(); _loading.TryRemove(k, out _); }
    }
}

/// <summary>Zustand der Kartenansicht (überlebt das Neuzeichnen).</summary>
public sealed class MapViewState
{
    public string Tool = "auswahl";
    public string? Selected;
    public double Lat = double.NaN, Lon, Zoom;
    public bool Tiles = true, Focus, Fitted;
    public string DocId = "";
    public readonly List<(double Lat, double Lon)> Draft = [];
    public string Hint = "";
    /// <summary>T-0719: nach „Umbenennen“ im Rechtsklick-Menü springt der Cursor ins Namensfeld.</summary>
    public bool FocusName;
}

/// <summary>T-0260: Karte des Programms „Karten & Touren“ – Mausrad zoomt, Ziehen verschiebt; Route Punkt für Punkt, Wegpunkt, Strecke messen, Fläche messen;
/// Spuren, Wegpunkte und Flächen anklicken und bearbeiten. Maßstab und Namensnennung immer sichtbar.</summary>
public sealed class MapCanvas : Control
{
    readonly MapDoc _m; readonly MapViewState _s; readonly TileCache _tiles; readonly Action<string, Action<MapDoc>> _commit; readonly Action<string?> _select;
    Point? _drag; Point _dragStart; bool _dragged; (double Lat, double Lon) _mouse; bool _over;
    // T-0719: direkte Bearbeitung – Wegpunkt ziehen ("wpt"), Punkt einer Route/Fläche ziehen ("vtx"), neuen Punkt aus der Mitte ziehen ("ins")
    (string Kind, string Id, int Index)? _edit; bool _editMoved; Vector _grab; // _grab: Abstand Zeiger ↔ Punkt beim Greifen (kein Sprung)
    /// <summary>Mehr Punkte zeigt die Karte nicht als Griffe (aufgezeichnete Spuren haben Tausende).</summary>
    public const int MaxHandles = 400;

    public MapCanvas(MapDoc m, MapViewState s, TileCache tiles, Action<string, Action<MapDoc>> commit, Action<string?> select)
    {
        _m = m; _s = s; _tiles = tiles; _commit = commit; _select = select;
        if (double.IsNaN(_s.Lat)) (_s.Lat, _s.Lon, _s.Zoom) = (m.CenterLat, m.CenterLon, m.Zoom);
        ClipToBounds = true; Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        _tiles.Loaded += InvalidateVisual;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { base.OnDetachedFromVisualTree(e); _tiles.Loaded -= InvalidateVisual; }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); if (_s.Focus) Dispatcher.UIThread.Post(() => Focus()); }
    /// <summary>Beim ersten Anzeigen alles einpassen – mit der echten Größe der Karte.</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e) { base.OnSizeChanged(e); if (!_s.Fitted && Bounds.Width > 10) { _s.Fitted = true; FitAll(); } }

    (double X, double Y) Center => WebMercator.ToPixel(_s.Lat, _s.Lon, _s.Zoom);
    public Point Screen(double lat, double lon) { var (cx, cy) = Center; var (x, y) = WebMercator.ToPixel(lat, lon, _s.Zoom); return new(x - cx + Bounds.Width / 2, y - cy + Bounds.Height / 2); }
    public (double Lat, double Lon) Geo(Point p) { var (cx, cy) = Center; return WebMercator.ToLatLon(cx + p.X - Bounds.Width / 2, cy + p.Y - Bounds.Height / 2, _s.Zoom); }

    public void FitAll()
    {
        var pts = _m.Tracks.SelectMany(t => t.Points.Select(p => (p.Lat, p.Lon))).Concat(_m.Waypoints.Select(w => (w.Lat, w.Lon))).Concat(_m.Areas.SelectMany(a => a.Points.Select(p => (p.Lat, p.Lon)))).ToList();
        if (pts.Count == 0) return; var tmp = new MapDoc(); tmp.FitTo(pts, Math.Max(200, Bounds.Width), Math.Max(200, Bounds.Height));
        (_s.Lat, _s.Lon, _s.Zoom) = (tmp.CenterLat, tmp.CenterLon, tmp.Zoom); InvalidateVisual();
    }

    // ---------- Eingabe ----------
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); var p = e.GetPosition(this); var before = Geo(p);
        _s.Zoom = Math.Clamp(_s.Zoom + (e.Delta.Y > 0 ? 0.5 : -0.5), 2, 19);
        // Punkt unter dem Zeiger bleibt stehen
        var (bx, by) = WebMercator.ToPixel(before.Lat, before.Lon, _s.Zoom);
        (_s.Lat, _s.Lon) = WebMercator.ToLatLon(bx - (p.X - Bounds.Width / 2), by - (p.Y - Bounds.Height / 2), _s.Zoom);
        InvalidateVisual(); e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); _s.Focus = true;
        var pt = e.GetCurrentPoint(this); var p = pt.Position;
        if (pt.Properties.IsRightButtonPressed) { _drag = null; _edit = null; ContextMenu = Menu(p); return; } // T-0719: Rechtsklick-Menü an der Stelle
        if (!pt.Properties.IsLeftButtonPressed) return;
        _drag = p; _dragStart = p; _dragged = false; e.Pointer.Capture(this);
        if (e.ClickCount == 2 && _s.Tool is "route" or "flaeche" or "messen") { Finish(); _drag = null; return; }
        _edit = null; _editMoved = false;
        if (_s.Tool is "auswahl" or "wegpunkt")
        {
            _grab = default;
            if (VertexAt(p) is { } v) { _edit = ("vtx", v.Id, v.Index); var q = Pts(v.Id)![v.Index]; _grab = p - Screen(q.Lat, q.Lon); }
            else if (MidAt(p) is { } mid) { _edit = ("ins", mid.Id, mid.Index); var g = Geo(p); Pts(mid.Id)!.Insert(mid.Index, Between(Pts(mid.Id)!, mid.Index, g)); InvalidateVisual(); }
            else if (WaypointAt(p) is { } w) { _edit = ("wpt", w.Id, -1); _grab = p - Screen(w.Lat, w.Lon); }
        }
    }

    /// <summary>Punkte einer Route/Spur oder Fläche.</summary>
    List<MapPoint>? Pts(string id) => _m.Tracks.FirstOrDefault(t => t.Id == id)?.Points ?? _m.Areas.FirstOrDefault(a => a.Id == id)?.Points;
    /// <summary>Punkte der gewählten Route/Spur oder Fläche, wenn ihre Griffe sichtbar sind.</summary>
    List<MapPoint>? SelectedPts() => _s.Selected is { } id && Pts(id) is { Count: <= MaxHandles } l ? l : null;
    bool SelectedIsArea => _s.Selected is { } id && _m.Areas.Any(a => a.Id == id);

    /// <summary>Neuer Punkt zwischen zwei Nachbarn – Höhe gemittelt, falls beide eine haben.</summary>
    static MapPoint Between(List<MapPoint> pts, int at, (double Lat, double Lon) g)
    {
        var a = pts[(at - 1 + pts.Count) % pts.Count]; var b = pts[at % pts.Count];
        return new MapPoint { Lat = g.Lat, Lon = g.Lon, Ele = a.Ele is { } ea && b.Ele is { } eb ? (ea + eb) / 2 : null };
    }

    public (string Id, int Index)? VertexAt(Point p)
    {
        if (SelectedPts() is not { } pts) return null;
        for (var i = pts.Count - 1; i >= 0; i--) { var q = Screen(pts[i].Lat, pts[i].Lon); if (Math.Abs(q.X - p.X) <= 8 && Math.Abs(q.Y - p.Y) <= 8) return (_s.Selected!, i); }
        return null;
    }

    /// <summary>Mittengriff eines Abschnitts: Ziehen fügt dort einen Punkt ein (Index = Einfügestelle).</summary>
    public (string Id, int Index)? MidAt(Point p)
    {
        if (SelectedPts() is not { } pts || pts.Count < 2) return null;
        var n = SelectedIsArea ? pts.Count : pts.Count - 1;
        for (var i = 0; i < n; i++) { var m = MidPoint(pts, i); if (Math.Abs(m.X - p.X) <= 7 && Math.Abs(m.Y - p.Y) <= 7) return (_s.Selected!, i + 1); }
        return null;
    }

    Point MidPoint(List<MapPoint> pts, int i) { var a = Screen(pts[i].Lat, pts[i].Lon); var b = Screen(pts[(i + 1) % pts.Count].Lat, pts[(i + 1) % pts.Count].Lon); return new((a.X + b.X) / 2, (a.Y + b.Y) / 2); }

    public MapWaypoint? WaypointAt(Point p)
    {
        for (var i = _m.Waypoints.Count - 1; i >= 0; i--) { var w = _m.Waypoints[i]; var q = Screen(w.Lat, w.Lon); if (Math.Abs(q.X - p.X) < 10 && p.Y > q.Y - 26 && p.Y < q.Y + 4) return w; }
        return null;
    }

    /// <summary>Bildschirmpunkt des Mittengriffs zwischen Punkt <paramref name="index"/> und dem nächsten (für Tests).</summary>
    public Point MidOf(string id, int index) => MidPoint(Pts(id)!, index);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this); _mouse = Geo(p); _over = true;
        if (_drag is not null && _edit is { } ed && (_editMoved || Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y) > 3))
        {
            // T-0719: Vorschau beim Ziehen nur in dieser Kopie der Karte – ins Dokument (ein Rückgängig-Schritt) erst beim Loslassen
            _editMoved = true; var g = Geo(p - _grab);
            if (ed.Kind == "wpt" && _m.Waypoints.FirstOrDefault(w => w.Id == ed.Id) is { } wp) (wp.Lat, wp.Lon) = g;
            else if (Pts(ed.Id) is { } pts && ed.Index < pts.Count) (pts[ed.Index].Lat, pts[ed.Index].Lon) = g;
            Cursor = new Cursor(StandardCursorType.SizeAll); InvalidateVisual(); return;
        }
        if (_drag is { } d && (Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y) > 4 || _dragged))
        {
            _dragged = true; var (cx, cy) = Center; (_s.Lat, _s.Lon) = WebMercator.ToLatLon(cx - (p.X - d.X), cy - (p.Y - d.Y), _s.Zoom); _drag = p;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); e.Pointer.Capture(null);
        if (_drag is null) return; var wasDrag = _dragged; _drag = null;
        var p = e.GetPosition(this); var g = Geo(p);
        if (_edit is { } ed)
        {
            _edit = null; Cursor = new Cursor(StandardCursorType.Hand);
            if (_editMoved)
            {
                _editMoved = false; var (id, i) = (ed.Id, ed.Index); g = Geo(p - _grab);
                switch (ed.Kind)
                {
                    case "wpt": _commit("Wegpunkt verschoben", m => { var w = m.Waypoints.First(x => x.Id == id); (w.Lat, w.Lon) = g; w.Ele = null; _s.Selected = id; }); break;
                    case "vtx": _commit("Punkt verschoben", m => { var q = Of(m, id)[i]; (q.Lat, q.Lon) = g; q.Ele = null; q.Time = null; }); break;
                    default: _commit("Punkt eingefügt", m => { var l = Of(m, id); l.Insert(i, Between(l, i, g)); }); break;
                }
                return;
            }
            if (ed.Kind == "ins") Pts(ed.Id)?.RemoveAt(ed.Index); // nur geklickt: nichts einfügen
            if (ed.Kind == "vtx") { InvalidateVisual(); return; } // Klick auf einen Griff ändert die Auswahl nicht
        }
        if (wasDrag) return;
        switch (_s.Tool)
        {
            case "route": case "messen": case "flaeche": _s.Draft.Add(g); InvalidateVisual(); return;
            case "wegpunkt": var n = $"Wegpunkt {_m.Waypoints.Count + 1}"; _commit("Wegpunkt", m => _s.Selected = m.AddWaypoint(g.Lat, g.Lon, n).Id); return;
        }
        var hit = Hit(p);
        if (hit != _s.Selected) { _s.Selected = hit; _select(hit); }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { if (_s.Draft.Count > 0) _s.Draft.Clear(); else { _s.Tool = "auswahl"; _s.Selected = null; _select(null); } InvalidateVisual(); e.Handled = true; }
        else if (e.Key == Key.Enter) { Finish(); e.Handled = true; }
        else if (e.Key == Key.F2 && _s.Selected is { } ren) { _s.FocusName = true; _select(ren); e.Handled = true; }
        else if (e.Key is Key.Delete or Key.Back && _s.Selected is { } sel) { _s.Selected = null; _commit("Löschen", m => m.Remove(sel)); e.Handled = true; }
        else if (e.Key == Key.Back && _s.Draft.Count > 0) { _s.Draft.RemoveAt(_s.Draft.Count - 1); InvalidateVisual(); }
    }

    static List<MapPoint> Of(MapDoc m, string id) => m.Tracks.FirstOrDefault(t => t.Id == id)?.Points ?? m.Areas.First(a => a.Id == id).Points;

    /// <summary>T-0719: Rechtsklick-Menü an der Stelle – Punkt löschen/einfügen, Wegpunkt umbenennen/löschen, Route umkehren, hier einen Wegpunkt setzen oder eine Route beginnen.</summary>
    public ContextMenu Menu(Point p)
    {
        var g = Geo(p); var m = new ContextMenu { Name = "mk-menu" };
        void I(string id, string header, Action go, bool on = true, string gesture = "") { var i = new MenuItem { Name = "mk-menu-" + id, Header = header, IsEnabled = on, InputGesture = gesture.Length > 0 ? KeyGesture.Parse(gesture) : null }; i.Click += (_, _) => go(); m.Items.Add(i); }
        void Pick(string? id) { _s.Selected = id; _select(id); }
        if (VertexAt(p) is { } v)
        {
            var min = SelectedIsArea ? 3 : 2; var (id, i) = v;
            I("punkt-loeschen", "Punkt löschen", () => _commit("Punkt gelöscht", x => Of(x, id).RemoveAt(i)), Pts(id)!.Count > min);
            m.Items.Add(new Separator());
        }
        else if (WaypointAt(p) is { } w)
        {
            var id = w.Id;
            I("umbenennen", "Umbenennen …", () => { _s.FocusName = true; Pick(id); }, gesture: "F2");
            I("loeschen", "Wegpunkt löschen", () => { _s.Selected = null; _commit("Löschen", x => x.Remove(id)); }, gesture: "Delete");
            m.Items.Add(new Separator());
        }
        else if (Hit(p) is { } hit)
        {
            var tr = _m.Tracks.FirstOrDefault(t => t.Id == hit);
            if (tr is not null && tr.Points.Count <= MaxHandles)
                I("einfuegen", "Punkt hier einfügen", () => _commit("Punkt eingefügt", x => { var l = Of(x, hit); var at = Nearest(l, p) + 1; l.Insert(at, Between(l, at, g)); _s.Selected = hit; }));
            I("umbenennen", "Umbenennen …", () => { _s.FocusName = true; Pick(hit); }, gesture: "F2");
            if (tr is not null) I("umkehren", "Richtung umkehren", () => _commit("Umkehren", x => x.Tracks.First(t => t.Id == hit).Points.Reverse()));
            I("loeschen", tr is null ? "Fläche löschen" : tr.Planned ? "Route löschen" : "Spur löschen", () => { _s.Selected = null; _commit("Löschen", x => x.Remove(hit)); }, gesture: "Delete");
            m.Items.Add(new Separator());
        }
        I("wegpunkt-hier", "Wegpunkt hier setzen", () => { var n = $"Wegpunkt {_m.Waypoints.Count + 1}"; _commit("Wegpunkt", x => _s.Selected = x.AddWaypoint(g.Lat, g.Lon, n).Id); });
        I("route-hier", "Route hier beginnen", () => { _s.Tool = "route"; _s.Draft.Clear(); _s.Draft.Add(g); _s.Hint = ""; Pick(null); });
        I("alles", "Alles zeigen", FitAll, _m.Tracks.Count + _m.Waypoints.Count + _m.Areas.Count > 0);
        return m;
    }

    /// <summary>Abschnitt der Linie, der dem Bildschirmpunkt am nächsten liegt (eingefügt wird nach diesem Index).</summary>
    int Nearest(List<MapPoint> pts, Point p)
    {
        var best = 0; var bd = double.MaxValue;
        for (var i = 1; i < pts.Count; i++)
        {
            var a = Screen(pts[i - 1].Lat, pts[i - 1].Lon); var b = Screen(pts[i].Lat, pts[i].Lon);
            var dd = Trendo.Core.Arch.Geo.SegDist(p.X, p.Y, a.X, a.Y, b.X, b.Y); if (dd < bd) { bd = dd; best = i - 1; }
        }
        return best;
    }

    /// <summary>Angefangene Route/Fläche abschließen (Doppelklick oder Enter). Messen bleibt nur Anzeige.</summary>
    public void Finish()
    {
        var pts = _s.Draft.Distinct().ToList();
        if (_s.Tool == "route" && pts.Count >= 2) { _s.Draft.Clear(); var n = $"Route {_m.Tracks.Count(t => t.Planned) + 1}"; _commit("Route zeichnen", m => _s.Selected = m.AddRoute(pts, n).Id); }
        else if (_s.Tool == "flaeche" && pts.Count >= 3) { _s.Draft.Clear(); var n = $"Fläche {_m.Areas.Count + 1}"; _commit("Fläche messen", m => _s.Selected = m.AddArea(pts, n).Id); }
        else if (_s.Tool == "messen") { /* Messlinie bleibt sichtbar, bis Esc */ }
        InvalidateVisual();
    }

    string? Hit(Point p)
    {
        if (WaypointAt(p) is { } wp) return wp.Id;
        foreach (var t in _m.Tracks)
            for (var i = 1; i < t.Points.Count; i++)
            {
                var a = Screen(t.Points[i - 1].Lat, t.Points[i - 1].Lon); var b = Screen(t.Points[i].Lat, t.Points[i].Lon);
                if (Trendo.Core.Arch.Geo.SegDist(p.X, p.Y, a.X, a.Y, b.X, b.Y) < 7) return t.Id;
            }
        foreach (var ar in _m.Areas) if (Trendo.Core.Arch.Geo.Inside(ar.Points.Select(q => { var s = Screen(q.Lat, q.Lon); return (s.X, s.Y); }).ToList(), p.X, p.Y)) return ar.Id;
        return null;
    }

    // ---------- Zeichnen ----------
    static readonly Typeface Face = new("Inter");
    static IBrush B(string hex, byte a = 255) { var c = Color.Parse(hex); return new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size; if (size.Width < 2) return;
        var tilesOn = _s.Tiles;
        ctx.FillRectangle(tilesOn ? B("#E9E5DC") : new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FF2C2C2C"), 0), new GradientStop(Color.Parse("#FF1C1C1C"), 1) } }, new Rect(size));
        var loadedAny = false;
        if (tilesOn)
        {
            var z = Math.Clamp((int)Math.Floor(_s.Zoom), 0, 19); var scale = Math.Pow(2, _s.Zoom - z); var ts = 256 * scale;
            var (cx, cy) = WebMercator.ToPixel(_s.Lat, _s.Lon, z);
            double ox = size.Width / 2 - cx * scale, oy = size.Height / 2 - cy * scale;
            int x0 = (int)Math.Floor(-ox / ts), y0 = (int)Math.Floor(-oy / ts), x1 = (int)Math.Floor((size.Width - ox) / ts), y1 = (int)Math.Floor((size.Height - oy) / ts);
            for (var tx = x0; tx <= x1; tx++) for (var ty = y0; ty <= y1; ty++)
                if (_tiles.Get(z, tx, ty) is { } bmp) { loadedAny = true; ctx.DrawImage(bmp, new Rect(ox + tx * ts, oy + ty * ts, ts + 0.5, ts + 0.5)); }
        }
        if (!loadedAny) DrawGraticule(ctx, size, tilesOn);
        // Flächen
        foreach (var a in _m.Areas)
        {
            var pts = a.Points.Select(p => Screen(p.Lat, p.Lon)).ToList(); var sel = a.Id == _s.Selected;
            ctx.DrawGeometry(B("#3B82F6", 60), new Pen(B(sel ? "#F5A524" : "#3B82F6"), sel ? 3 : 2), new PolylineGeometry(pts, true));
            var c = new Point(pts.Average(q => q.X), pts.Average(q => q.Y));
            Label(ctx, $"{a.Name}: {FormatArea(a.AreaM2)}", c, "#FFFFFF", "#CC1E3A8A", true);
        }
        // Spuren
        foreach (var t in _m.Tracks)
        {
            if (t.Points.Count < 2) continue;
            var pts = t.Points.Select(p => Screen(p.Lat, p.Lon)).ToList(); var sel = t.Id == _s.Selected;
            ctx.DrawGeometry(null, new Pen(Brushes.White, sel ? 9 : 7) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round }, new PolylineGeometry(pts, false));
            ctx.DrawGeometry(null, new Pen(B(t.Color), sel ? 6 : 4, t.Planned ? new DashStyle([2, 1.2], 0) : null) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round }, new PolylineGeometry(pts, false));
            ctx.DrawEllipse(B("#22C55E"), new Pen(Brushes.White, 2), pts[0], 6, 6);
            ctx.DrawEllipse(B("#E11D48"), new Pen(Brushes.White, 2), pts[^1], 6, 6);
            if (sel) Label(ctx, $"{t.Name} · {t.Stats().DistanceKm:0.0} km", pts[pts.Count * 3 / 4] + new Point(12, 6), "#1C1C1C", "#F5A524");
        }
        // Wegpunkte
        foreach (var w in _m.Waypoints)
        {
            var q = Screen(w.Lat, w.Lon); var sel = w.Id == _s.Selected;
            var pin = new PolylineGeometry([q, new Point(q.X - 8, q.Y - 14), new Point(q.X - 8, q.Y - 22), new Point(q.X + 8, q.Y - 22), new Point(q.X + 8, q.Y - 14)], true);
            ctx.DrawGeometry(B(sel ? "#F5A524" : "#7C3AED"), new Pen(Brushes.White, 2), pin);
            Label(ctx, w.Name, new Point(q.X + 12, q.Y - 26), "#FFFFFF", "#CC333333");
        }
        // T-0719: Griffe der gewählten Route/Fläche – Punkte ziehen, aus der Mitte neue Punkte ziehen
        if (SelectedPts() is { Count: > 0 } hp)
        {
            var n = SelectedIsArea ? hp.Count : hp.Count - 1;
            for (var i = 0; i < n && hp.Count > 1; i++) ctx.DrawEllipse(B("#FFFFFF", 150), new Pen(B("#1C1C1C", 160), 1), MidPoint(hp, i), 3.5, 3.5);
            foreach (var q in hp) ctx.DrawEllipse(Brushes.White, new Pen(B("#F5A524"), 2.5), Screen(q.Lat, q.Lon), 5, 5);
        }
        // Entwurf (Route, Messen, Fläche)
        if (_s.Draft.Count > 0)
        {
            var pts = _s.Draft.Select(p => Screen(p.Lat, p.Lon)).ToList(); if (_over) pts.Add(Screen(_mouse.Lat, _mouse.Lon));
            var closed = _s.Tool == "flaeche" && pts.Count >= 3;
            ctx.DrawGeometry(closed ? B("#F5A524", 50) : null, new Pen(B("#F5A524"), 3, new DashStyle([3, 2], 0)), new PolylineGeometry(pts, closed));
            foreach (var q in pts.Take(_s.Draft.Count)) ctx.DrawEllipse(Brushes.White, new Pen(B("#F5A524"), 2), q, 4, 4);
            var all = _s.Draft.Concat(_over ? [_mouse] : []).Select(p => new GeoPoint(p.Lat, p.Lon)).ToList();
            var km = all.Zip(all.Skip(1), Trendo.Core.Geo.Haversine).Sum();
            var text = _s.Tool == "flaeche" && all.Count >= 3 ? $"Fläche {FormatArea(GeoPlus.AreaM2(all))} · Umfang {km + Trendo.Core.Geo.Haversine(all[^1], all[0]):0.00} km" : $"{km:0.000} km" + (_s.Tool == "route" ? $" · Gehzeit ca. {Fmt(GeoPlus.HikingTime(km, 0, 0))}" : "");
            Label(ctx, text, pts[^1] + new Point(12, 10), "#1C1C1C", "#F5A524");
        }
        // Maßstab, Koordinaten, Namensnennung, Hinweis
        DrawScale(ctx, size);
        if (_over) Label(ctx, $"{_mouse.Lat.ToString("0.00000", CultureInfo.InvariantCulture)}, {_mouse.Lon.ToString("0.00000", CultureInfo.InvariantCulture)} · Zoom {_s.Zoom:0.#}", new Point(10, size.Height - 48), "#FFFFFF", "#AA1C1C1C");
        if (tilesOn) Label(ctx, "© OpenStreetMap-Mitwirkende", new Point(size.Width - 196, size.Height - 22), "#1A1A1A", "#CCFFFFFF");
        var hint = _s.Hint.Length > 0 ? _s.Hint : ToolHint();
        if (hint.Length > 0) Label(ctx, hint, new Point(10, 10), "#FFFFFF", "#CC2C2C2C");
    }

    string ToolHint() => _s.Tool switch
    {
        "route" => _s.Draft.Count == 0 ? "Route: Punkt für Punkt klicken – Doppelklick oder Enter beendet, Esc bricht ab." : $"Route: {_s.Draft.Count} Punkt(e) · Doppelklick/Enter = fertig · Rücktaste nimmt den letzten Punkt zurück",
        "messen" => "Strecke messen: Punkte klicken – die Länge steht am Zeiger. Esc löscht die Messung.",
        "flaeche" => "Fläche messen: Eckpunkte klicken – Doppelklick oder Enter speichert die Fläche.",
        "wegpunkt" => "Wegpunkt: auf die Karte klicken – Name rechts eintragen. Vorhandene Wegpunkte lassen sich ziehen.",
        _ => SelectedPts() is not null ? "Punkte ziehen · aus der Mitte ziehen fügt einen Punkt ein · Rechtsklick: Punkt löschen" : "",
    };

    void DrawGraticule(DrawingContext ctx, Size size, bool tilesOn)
    {
        var tl = Geo(new Point(0, 0)); var br = Geo(new Point(size.Width, size.Height));
        var span = Math.Max(1e-6, br.Lon - tl.Lon); var step = new[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20 }.First(s => span / s <= 12);
        var pen = new Pen(B(tilesOn ? "#9CA3AF" : "#7FD3FF", 50), 1);
        for (var lon = Math.Floor(tl.Lon / step) * step; lon <= br.Lon; lon += step) { var x = Screen(tl.Lat, lon).X; ctx.DrawLine(pen, new Point(x, 0), new Point(x, size.Height)); }
        for (var lat = Math.Floor(br.Lat / step) * step; lat <= tl.Lat; lat += step) { var y = Screen(lat, tl.Lon).Y; ctx.DrawLine(pen, new Point(0, y), new Point(size.Width, y)); }
        {
            var msg = !_s.Tiles ? "Kartenhintergrund aus – Gitter in Grad" : _tiles.Online ? "Kartenkacheln werden geladen …" : "Ohne Karte aus dem Internet: Zwischenspeicher oder Gitter"; // T-0726
            var ft = new FormattedText(msg, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 12, B(tilesOn ? "#4B5563" : "#9FB4D3"));
            ctx.DrawText(ft, new Point(size.Width - ft.Width - 12, 12));
        }
    }

    void DrawScale(DrawingContext ctx, Size size)
    {
        var mpp = WebMercator.MetersPerPixel(_s.Lat, _s.Zoom); var target = 120 * mpp;
        var nice = new[] { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000, 500000, 1000000 }.Last(v => v <= Math.Max(1, target));
        var w = nice / mpp; var y = size.Height - 18; var x = 12.0;
        ctx.DrawRectangle(B("#FFFFFF", 200), null, new Rect(x - 4, y - 18, w + 8, 24), 4, 4);
        ctx.DrawLine(new Pen(Brushes.Black, 2), new Point(x, y), new Point(x + w, y));
        ctx.DrawLine(new Pen(Brushes.Black, 2), new Point(x, y - 5), new Point(x, y)); ctx.DrawLine(new Pen(Brushes.Black, 2), new Point(x + w, y - 5), new Point(x + w, y));
        var ft = new FormattedText(nice >= 1000 ? $"{nice / 1000} km" : $"{nice} m", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 11, Brushes.Black);
        ctx.DrawText(ft, new Point(x + w / 2 - ft.Width / 2, y - 17));
    }

    public static string FormatArea(double m2) => m2 >= 10000 ? $"{m2 / 10000:0.00} ha" : $"{m2:0} m²";
    static string Fmt(TimeSpan t) => $"{(int)t.TotalHours}:{t.Minutes:00} h";

    static void Label(DrawingContext ctx, string text, Point at, string color, string bg, bool center = false)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 12, B(color));
        if (center) at = new Point(at.X - ft.Width / 2, at.Y - ft.Height / 2);
        var c = Color.Parse(bg.Length == 9 ? "#" + bg[1..] : bg);
        ctx.DrawRectangle(new SolidColorBrush(c), null, new Rect(at.X - 6, at.Y - 3, ft.Width + 12, ft.Height + 6), 6, 6); ctx.DrawText(ft, at);
    }
}

/// <summary>Höhenprofil einer Spur (Höhe über Strecke) für die Eigenschaften.</summary>
public sealed class ElevationProfile(IReadOnlyList<MapPoint> pts, string color) : Control
{
    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width; var h = Bounds.Height; if (w < 10 || pts.Count < 2 || pts.All(p => p.Ele is null)) return;
        var d = new List<(double Km, double Ele)>(); double km = 0;
        for (var i = 0; i < pts.Count; i++) { if (i > 0) km += Trendo.Core.Geo.Haversine(pts[i - 1].Geo(), pts[i].Geo()); d.Add((km, pts[i].Ele ?? 0)); }
        double e0 = d.Min(x => x.Ele), e1 = Math.Max(e0 + 1, d.Max(x => x.Ele)), total = Math.Max(1e-9, km);
        Point P((double Km, double Ele) x) => new(4 + x.Km / total * (w - 8), h - 16 - (x.Ele - e0) / (e1 - e0) * (h - 30));
        var line = d.Select(P).ToList(); var area = new List<Point>(line) { new(w - 4, h - 16), new(4, h - 16) };
        var c = Color.Parse(color);
        ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(70, c.R, c.G, c.B)), null, new PolylineGeometry(area, true));
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(c), 2), new PolylineGeometry(line, false));
        var f = new Typeface("Inter"); var brush = new SolidColorBrush(Color.Parse("#C9D6EA"));
        ctx.DrawText(new FormattedText($"{e1:0} m", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, f, 10, brush), new Point(6, 2));
        ctx.DrawText(new FormattedText($"{e0:0} m · {total:0.0} km", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, f, 10, brush), new Point(6, h - 14));
    }
}
