using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Karten;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>SPEC-13 / T-0260: Karten & Geodaten als echtes Programm „Karten & Touren“ – Karte (OpenStreetMap, zwischengespeichert; offline Gitter),
/// GPX-Spuren mit Höhenprofil und Kennzahlen, Routen zeichnen, Wegpunkte, Strecken und Flächen messen, Export GPX/GeoJSON/KML.
/// Die früheren Rechner (Koordinaten, UTM, Richtung, Zielpunkt, Wanderzeit) rechnen live. Ersetzt die Formularseite.</summary>
public partial class MainWindow
{
    const string MK = "karten";
    public readonly MapViewState MapState = new();
    private TileCache? _tileCache;
    // Kacheln nur online und nicht im abgeschotteten Testbetrieb; das Screenshot-Werkzeug darf sie mit TRENDO_TILES=1 laden
    TileCache Tiles => _tileCache ??= MakeTiles();
    TileCache MakeTiles()
    {
        var t = new TileCache(Path.Combine(_core.DataRoot, "karten", "kacheln"), !_core.Offline && (!_opt.Isolated || Environment.GetEnvironmentVariable("TRENDO_TILES") == "1"));
        if (MapPackPath is { Length: > 0 } pp) try { t.Pack = new TilePack(pp); } catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException) { } // Paket fehlt inzwischen
        return t;
    }
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] MapColors = ["#F97316", "#3B82F6", "#22C55E", "#E11D48", "#A855F7", "#14B8A6", "#EAB308"];

    static readonly (string Id, string Label, string Glyph, string Tip)[] MapTools =
    [
        ("auswahl", "Auswahl", "M4,2 L4,20 L9,15 L12,22 L15,21 L12,14 L19,14 Z", "Karte ziehen, Spuren/Punkte anklicken (Esc)"),
        ("route", "Route", "M3,20 C8,4 14,22 21,5 M3,20 m-2,0 a2,2 0 1,0 4,0 a2,2 0 1,0 -4,0 M21,5 m-2,0 a2,2 0 1,0 4,0 a2,2 0 1,0 -4,0", "Route Punkt für Punkt zeichnen"),
        ("wegpunkt", "Punkt", "M12,22 L5,11 A7,7 0 1,1 19,11 Z M12,8 m-2,0 a2,2 0 1,0 4,0 a2,2 0 1,0 -4,0", "Wegpunkt setzen"),
        ("messen", "Messen", "M2,18 L22,6 M5,13 L7,16 M10,10 L12,13 M15,7 L17,10", "Strecke messen"),
        ("flaeche", "Fläche", "M3,18 L8,4 L20,7 L18,20 Z", "Fläche messen (z. B. Grundstück)"),
    ];

    private void InitSpec13() => RegisterProgram(FachTabs, new ProgramSpec(MK, "SPEC-13", "Karten & Touren")
    {
        Sentence = "Touren planen und auswerten – Karte, GPS-Spuren mit Höhenprofil, Routen, Wegpunkte, Strecken und Flächen messen.",
        Folder = "Karten",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leere Karte", "Deutschland – Route zeichnen oder GPX laden", () => MapDocNode(new MapDoc())),
            new("tour", "Beispieltour", "Erfundener Rundweg mit Höhenprofil und zwei Wegpunkten", () => MapDocNode(MapDoc.Template("tour"))),
        ],
        Ribbon =
        [
            ("Karte", [
                new ProgramCmd("mk-tiles", "Hintergrund an/aus", _ => MapState.Tiles = !MapState.Tiles, false, "OpenStreetMap-Karte oder nur Gitter"),
                new ProgramCmd("mk-fit", "Alles zeigen", _ => MapState.Fitted = false, false, "Ansicht auf alle Spuren und Punkte"),
            ]),
        ],
        Exports =
        [
            new("gpx", "GPX (Navi, Uhr)", ".gpx", (d, f) => { File.WriteAllText(f, MapDoc.From(d.Data).Gpx()); return f; }),
            new("geojson", "GeoJSON (QGIS)", ".geojson", (d, f) => { File.WriteAllText(f, MapDoc.From(d.Data).GeoJson()); return f; }),
            new("kml", "KML (Google Earth)", ".kml", (d, f) => { File.WriteAllText(f, MapDoc.From(d.Data).Kml()); return f; }),
        ],
        Workspace = MapWorkspace,
        Properties = MapProperties,
        Status = d => { var m = MapDoc.From(d.Data); var km = m.Tracks.Sum(t => t.Stats().DistanceKm); return $"{MapTools.FirstOrDefault(t => t.Id == MapState.Tool).Label} · {m.Tracks.Count} Spuren ({km:0.0} km) · {m.Waypoints.Count} Wegpunkte · {m.Areas.Count} Flächen · Karte {(MapState.Tiles ? (Tiles.Online ? "OpenStreetMap" : "offline (Zwischenspeicher)") : "aus")}"; },
        Help = "Mausrad zoomt, Ziehen verschiebt die Karte. Route/Fläche: Punkte klicken, Doppelklick oder Enter beendet. Messen: Punkte klicken, Esc löscht. "
             + "Spur oder Wegpunkt anklicken für Höhenprofil, Werte und Namen. GPX-Dateien rechts unter „GPX laden“. Kartendaten © OpenStreetMap-Mitwirkende.",
    }, "karte", "gps", "gpx", "wandern", "route", "strecke", "koordinaten", "utm", "geojson", "höhenmeter", "tour", "kml");

    static System.Text.Json.Nodes.JsonObject MapDocNode(MapDoc m) { var o = new System.Text.Json.Nodes.JsonObject(); m.WriteTo(o); return o; }

    public void MapEdit(string label, Action<MapDoc> change)
    {
        var d = ProgramActive(MK) ?? throw new InvalidOperationException("Es ist keine Karte offen.");
        MapState.Hint = "";
        ProgramRun(MK, () => d.Edit(label, x => { var m = MapDoc.From(x); change(m); m.WriteTo(x); }));
    }

    public void MapTool(string tool) { MapState.Tool = tool; MapState.Draft.Clear(); MapState.Hint = ""; if (tool != "auswahl") MapState.Selected = null; Show(MK); }
    public void MapSelect(string? id) { MapState.Selected = id; Show(MK); }

    public void MapImportGpx(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Diese GPX-Datei gibt es nicht.");
        MapTrack? t = null;
        MapEdit("GPX laden", m => { t = m.ImportGpx(path); });
        MapState.Fitted = false; // nach dem Laden auf die neue Spur einpassen
        MapState.Selected = t?.Id;
        _progMsg[MK] = t is null ? "Wegpunkte geladen." : $"„{t.Name}“ geladen – {t.Stats().DistanceKm:0.0} km.";
        Show(MK);
    }

    private Control MapWorkspace(ProgramDocument d)
    {
        var m = MapDoc.From(d.Data);
        if (MapState.DocId != d.Id) { MapState.DocId = d.Id; MapState.Fitted = false; MapState.Lat = double.NaN; MapState.Selected = null; MapState.Draft.Clear(); } // anderes Dokument: Ansicht neu
        var tools = new WrapPanel { Name = "mk-tools", Width = 112 };
        foreach (var (id, label, glyph, tip) in MapTools)
        {
            var on = MapState.Tool == id; var tid = id;
            var b = new Button { Name = "mk-tool-" + id, Width = 53, Height = 46, Padding = new Thickness(1), Margin = new Thickness(0, 0, 3, 3), CornerRadius = new CornerRadius(10), Focusable = false,
                Background = on ? Accent : Brush.Parse(C("#1AFFFFFF", "#0D000000")), BorderBrush = on ? Brush.Parse("#F5A524") : Edge, BorderThickness = new Thickness(on ? 1.5 : 1),
                Content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(glyph), Stroke = on ? Brushes.White : Text, StrokeThickness = 1.6, Fill = Brushes.Transparent, Stretch = Stretch.Uniform, Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = label, FontSize = 10, Foreground = on ? Brushes.White : Text, HorizontalAlignment = HorizontalAlignment.Center } } } };
            ToolTip.SetTip(b, tip); b.Click += (_, _) => MapTool(tid); tools.Children.Add(b);
        }
        var canvas = new MapCanvas(m, MapState, Tiles, MapEdit, MapSelect) { Name = "mk-canvas", MinHeight = 160 };
        var frame = new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = canvas };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,10,*"), Children = { new ScrollViewer { Content = tools }, Pos(frame, 2) } };
    }

    private Control MapProperties(ProgramDocument d)
    {
        var m = MapDoc.From(d.Data); var sp = new StackPanel { Spacing = 5 }; var sel = MapState.Selected;
        Button Act2(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        void Fact(string label, string value) => sp.Children.Add(Row(P(label, null, 12), new TextBlock { Text = value, Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold }));
        void NameBox(string current, Action<MapDoc, string> set)
        {
            var t = new TextBox { Name = "mk-p-name", Text = current };
            void Commit() { if (!string.IsNullOrWhiteSpace(t.Text) && t.Text != current) MapEdit("Umbenennen", x => set(x, t.Text!.Trim())); }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit); sp.Children.Add(t);
        }
        string Coord(double lat, double lon) { var u = Geo.Utm(lat, lon); return $"{lat.ToString("0.000000", Inv)}, {lon.ToString("0.000000", Inv)}\n{Geo.Dms(lat, lon)}\nUTM {u.Zone}{u.Band} {u.Easting:0} {u.Northing:0}"; }

        if (sel is not null && m.Tracks.FirstOrDefault(t => t.Id == sel) is { } tr)
        {
            var st = tr.Stats();
            sp.Children.Add(H(tr.Planned ? "Route" : "Spur", 13)); NameBox(tr.Name, (x, v) => x.Tracks.First(t => t.Id == sel).Name = v);
            var colors = new WrapPanel();
            foreach (var c in MapColors) { var cc = c; var b = new Button { Name = "mk-color-" + c.TrimStart('#'), Width = 22, Height = 22, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(0), Background = Brush.Parse(c), BorderBrush = Brushes.White, BorderThickness = new Thickness(c == tr.Color ? 2 : 0), CornerRadius = new CornerRadius(11) }; b.Click += (_, _) => MapEdit("Farbe", x => x.Tracks.First(t => t.Id == sel).Color = cc); colors.Children.Add(b); }
            sp.Children.Add(colors);
            Fact("Länge", $"{st.DistanceKm:0.00} km");
            if (st.MaxEle > st.MinEle || tr.Points.Any(p => p.Ele is not null)) { Fact("Aufstieg / Abstieg", $"↑ {st.AscentM:0} m · ↓ {st.DescentM:0} m"); Fact("Höhe", $"{st.MinEle:0} – {st.MaxEle:0} m"); }
            if (st.Duration is { } dur) Fact("Dauer / Schnitt", $"{(int)dur.TotalHours}:{dur.Minutes:00} h · {st.AvgKmh ?? 0:0.0} km/h");
            var hike = GeoPlus.HikingTime(st.DistanceKm, st.AscentM, st.DescentM); Fact("Gehzeit (DIN 33466)", $"{(int)hike.TotalHours}:{hike.Minutes:00} h ohne Pausen");
            Fact("Punkte", tr.Points.Count.ToString());
            if (tr.Points.Any(p => p.Ele is not null)) sp.Children.Add(new Border { Name = "mk-profile", Height = 110, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#33081329"), Child = new ElevationProfile(tr.Points, tr.Color) });
            sp.Children.Add(new WrapPanel { Children = {
                Act2("mk-simplify", "Vereinfachen (5 m)", () => MapEdit("Vereinfachen", x => { var t = x.Tracks.First(y => y.Id == sel); t.Points = GeoPlus.Simplify(t.Points.Select(p => p.Geo()).ToList(), 5).Select(MapPoint.From).ToList(); })),
                Act2("mk-reverse", "Umkehren", () => MapEdit("Umkehren", x => x.Tracks.First(y => y.Id == sel).Points.Reverse())),
                Act2("mk-zoom", "Hinzoomen", () => { var t = new MapDoc(); t.FitTo(tr.Points.Select(p => (p.Lat, p.Lon))); (MapState.Lat, MapState.Lon, MapState.Zoom) = (t.CenterLat, t.CenterLon, t.Zoom); Show(MK); }),
                Act2("mk-del", "Löschen", () => { MapState.Selected = null; MapEdit("Löschen", x => x.Remove(sel)); }) } });
            MapOnlineTools(sp, tr, sel);
        }
        else if (sel is not null && m.Waypoints.FirstOrDefault(w => w.Id == sel) is { } wp)
        {
            sp.Children.Add(H("Wegpunkt", 13)); NameBox(wp.Name, (x, v) => x.Waypoints.First(w => w.Id == sel).Name = v);
            sp.Children.Add(new SelectableTextBlock { Name = "mk-coord", Text = Coord(wp.Lat, wp.Lon), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (wp.Ele is { } e) Fact("Höhe", $"{e:0} m");
            sp.Children.Add(Act2("mk-del", "Löschen", () => { MapState.Selected = null; MapEdit("Löschen", x => x.Remove(sel)); }));
        }
        else if (sel is not null && m.Areas.FirstOrDefault(a => a.Id == sel) is { } ar)
        {
            sp.Children.Add(H("Fläche", 13)); NameBox(ar.Name, (x, v) => x.Areas.First(a => a.Id == sel).Name = v);
            Fact("Fläche", $"{ar.AreaM2:0} m² = {ar.AreaM2 / 10000:0.000} ha"); Fact("Umfang", $"{ar.PerimeterKm * 1000:0} m"); Fact("Eckpunkte", ar.Points.Count.ToString());
            sp.Children.Add(Act2("mk-del", "Löschen", () => { MapState.Selected = null; MapEdit("Löschen", x => x.Remove(sel)); }));
        }
        else
        {
            sp.Children.Add(H("Karte", 13));
            var k = 0;
            foreach (var t in m.Tracks) { var id = t.Id; var b = new Button { Name = $"mk-track-{k++}", HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(6, 3),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { new Border { Width = 12, Height = 4, Background = Brush.Parse(t.Color), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center }, Pos(new TextBlock { Text = t.Name, Foreground = Text, FontSize = 12 }, 1), Pos(P($"{t.Stats().DistanceKm:0.0} km", null, 11), 2) } } };
                b.Click += (_, _) => MapSelect(id); sp.Children.Add(b); }
            k = 0;
            foreach (var w in m.Waypoints) { var id = w.Id; var b = new Button { Name = $"mk-wpt-{k++}", HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(6, 3), Content = new TextBlock { Text = "📍 " + w.Name, Foreground = Text, FontSize = 12 } }; b.Click += (_, _) => MapSelect(id); sp.Children.Add(b); }
            if (m.Tracks.Count + m.Waypoints.Count == 0) sp.Children.Add(P("Noch leer – Route zeichnen, Wegpunkt setzen oder eine GPX-Datei laden.", null, 11));
            sp.Children.Add(H("GPX laden", 12));
            var gpx = Field("mk-gpx-path", "GPX-Datei (Uhr, Handy, Navi)", 140); sp.Children.Add(gpx);
            sp.Children.Add(Act2("mk-gpx-load", "Laden", () => ProgramRun(MK, () => MapImportGpx(Need(gpx, "die GPX-Datei")))));
            var calcs = new StackPanel { Spacing = 10 };
            LiveCalc(calcs, "geo-coord", "Koordinaten umrechnen", [("c", "Koordinate", "48,1372 11,5756", null)],
                v => { var (lat, lon) = Geo.ParseCoord(v("c")); return Coord(lat, lon); });
            LiveCalc(calcs, "geo-dist", "Entfernung & Richtung", [("a", "Von", "52,5200 13,4050", null), ("b", "Nach", "48,1372 11,5756", null)],
                v => { var (la, oa) = Geo.ParseCoord(v("a")); var (lb, ob) = Geo.ParseCoord(v("b")); var dg = GeoPlus.Bearing(new(la, oa), new(lb, ob)); return $"Luftlinie {Fach.F(Geo.Haversine(new(la, oa), new(lb, ob)), 1)} km · Richtung {Fach.F(dg, 1)}° ({GeoPlus.Compass(dg)})"; });
            LiveCalc(calcs, "geo-utm", "UTM → Grad", [("z", "Zone", "32", null), ("e", "Ost (m)", "691607", null), ("n", "Nord (m)", "5334760", null), ("h", "Halbkugel", "N", ["N", "S"])],
                v => { var (la, lo) = GeoPlus.UtmToLatLon((int)Fach.Num(v("z")), Fach.Num(v("e")), Fach.Num(v("n")), v("h") == "N"); return $"{la.ToString("0.000000", Inv)}, {lo.ToString("0.000000", Inv)}"; });
            LiveCalc(calcs, "geo-dest", "Zielpunkt", [("a", "Start", "48,1372 11,5756", null), ("deg", "Richtung (°)", "90", null), ("km", "Entfernung (km)", "10", null)],
                v => { var (la, oa) = Geo.ParseCoord(v("a")); var p = GeoPlus.Destination(new(la, oa), Fach.Num(v("deg")), Fach.Num(v("km"))); return $"Ziel: {p.Lat.ToString("0.000000", Inv)}, {p.Lon.ToString("0.000000", Inv)}"; });
            LiveCalc(calcs, "geo-time", "Wanderzeit (DIN 33466)", [("km", "Strecke (km)", "12", null), ("up", "Aufstieg (m)", "800", null), ("down", "Abstieg (m)", "800", null)],
                v => { var t = GeoPlus.HikingTime(Fach.Num(v("km")), Fach.Num(v("up")), Fach.Num(v("down"))); return $"Gehzeit ca. {(int)t.TotalHours}:{t.Minutes:00} h (ohne Pausen)"; });
            sp.Children.Add(new Expander { Name = "mk-calcs", Header = "Rechner (Koordinaten, UTM, Wanderzeit …)", IsExpanded = false, Content = calcs });
            sp.Children.Add(P(Tiles.Online ? "Kartendaten © OpenStreetMap-Mitwirkende (ODbL). Kacheln werden zwischengespeichert." : "Offline: Karte aus dem Zwischenspeicher, sonst Gitter.", null, 11));
            // T-0274b: Offline-Kartenpaket (Ordner oder ZIP mit z/x/y.png)
            sp.Children.Add(H("Offline-Kartenpaket", 12));
            var pack = Field("mk-pack-path", "Kartenpaket (.mbtiles, .zip) oder Kachel-Ordner", 150, MapPackPath); sp.Children.Add(pack);
            sp.Children.Add(new WrapPanel { Children = {
                Act2("mk-pack-load", "Laden", () => { try { MapLoadPack(Need(pack, "das Kartenpaket")); } catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException) { _mapNetMsg = "⚠ " + ex.Message; Show(MK); } }),
                Act2("mk-pack-off", "Entfernen", () => { MapPackPath = ""; Tiles.Pack = null; _mapNetMsg = "Kartenpaket entfernt."; Show(MK); }) } });
            if (Tiles.Pack is { } tp) sp.Children.Add(new TextBlock { Name = "mk-pack-info", Text = $"{tp.Count} Kacheln, Zoom {tp.MinZoom}–{tp.MaxZoom} · {Path.GetFileName(tp.Source.TrimEnd('/', '\\'))}", Foreground = Brush.Parse("#7FD3FF"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            if (_mapNetMsg.Length > 0 && sel is null) sp.Children.Add(new TextBlock { Name = "mk-net-msg", Text = _mapNetMsg, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(_mapNetMsg.StartsWith('⚠') ? "#F87171" : "#7FD3FF") });
        }
        return sp;
    }

    // ---------- T-0274: Höhen und Routing (Dienste im Internet – nur nach Zustimmung, offline gesperrt) ----------
    private string? _mapAsk; // "hoehen" oder "route", wartet auf Zustimmung
    private string _mapNetMsg = "";
    public HttpMessageHandler? MapHttpOverride { get; set; }
    HttpClient MapHttp => MapHttpOverride is { } h ? new HttpClient(h) : TileCache.Shared;
    string RoutingFile => Path.Combine(_core.DataRoot, "karten", "routing.txt");
    public string MapRoutingServer { get => File.Exists(RoutingFile) ? File.ReadAllText(RoutingFile).Trim() : ""; set { Directory.CreateDirectory(Path.GetDirectoryName(RoutingFile)!); File.WriteAllText(RoutingFile, value.Trim()); } }
    public string MapProfile = "foot";

    void MapOnlineTools(StackPanel sp, MapTrack tr, string sel)
    {
        var online = !_core.Offline && (!_opt.Isolated || MapHttpOverride is not null);
        sp.Children.Add(H("Aus dem Internet", 12));
        if (!online) { sp.Children.Add(P("Offline – Höhen und Routing brauchen eine Internetverbindung.", null, 11)); return; }
        Button B(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 4, 0) }; b.Click += (_, _) => go(); return b; }
        var noEle = tr.Points.Count >= 2 && tr.Points.All(p => p.Ele is null);
        sp.Children.Add(B("mk-ele", noEle ? "Höhen ergänzen" : "Höhen neu abrufen", () => { _mapAsk = "hoehen"; Show(MK); }));
        if (tr.Planned)
        {
            var srv = new TextBox { Name = "mk-route-server", Text = MapRoutingServer, PlaceholderText = "Routing-Server (OSRM), z. B. https://mein-server/osrm" };
            srv.LostFocus += (_, _) => MapRoutingServer = srv.Text ?? ""; OnEnter(srv, () => MapRoutingServer = srv.Text ?? "");
            var prof = new ComboBox { Name = "mk-route-profile", ItemsSource = new[] { "zu Fuß", "Fahrrad", "Auto" }, SelectedIndex = Array.IndexOf(RouteService.Profiles, MapProfile), Width = 110 };
            prof.SelectionChanged += (_, _) => MapProfile = RouteService.Profiles[Math.Max(0, prof.SelectedIndex)];
            sp.Children.Add(P("Route entlang von Wegen – über einen OSRM-Server, den du einträgst (eigener Server oder ein Anbieter, dessen Bedingungen das erlauben).", null, 11));
            sp.Children.Add(srv);
            sp.Children.Add(new WrapPanel { Children = { prof, B("mk-route", "Entlang von Wegen", () => { MapRoutingServer = srv.Text ?? ""; _mapAsk = "route"; Show(MK); }) } });
        }
        if (_mapAsk is { } ask)
        {
            var host = ask == "hoehen" ? ElevationService.Host : (Uri.TryCreate(MapRoutingServer, UriKind.Absolute, out var u) ? u.Host : "(keine Adresse)");
            sp.Children.Add(new Border { Name = "mk-consent", Background = Brush.Parse("#332F86F6"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Margin = new Thickness(0, 6, 0, 0), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = $"Dafür werden die Koordinaten dieser {(tr.Planned ? "Route" : "Spur")} an {host} gesendet. Ohne deine Zustimmung passiert nichts.", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                new WrapPanel { Children = { B("mk-consent-yes", "Ja, senden", () => { _mapAsk = null; _ = ask == "hoehen" ? MapFetchElevation(sel) : MapFetchRoute(sel); }), B("mk-consent-no", "Nein", () => { _mapAsk = null; _mapNetMsg = "Nichts gesendet."; Show(MK); }) } } } } });
        }
        if (_mapNetMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "mk-net-msg", Text = _mapNetMsg, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(_mapNetMsg.StartsWith('⚠') ? "#F87171" : "#7FD3FF") });
    }

    /// <summary>Höhen für die Spur/Route holen (nach Zustimmung) und als ein Bearbeitungsschritt übernehmen.</summary>
    public async Task MapFetchElevation(string sel)
    {
        var d = ProgramActive(MK); var tr = d is null ? null : MapDoc.From(d.Data).Tracks.FirstOrDefault(t => t.Id == sel); if (d is null || tr is null) return;
        _mapNetMsg = "Höhen werden abgerufen …"; Show(MK);
        try
        {
            var svc = new ElevationService(new HttpFetch(MapHttp), Path.Combine(_core.DataRoot, "karten", "hoehen.json"));
            var pts = await svc.AddElevation(tr.Points.Select(p => p.Geo()).ToList());
            MapEdit("Höhen ergänzen", x => x.Tracks.First(t => t.Id == sel).Points = pts.Select(MapPoint.From).ToList());
            _mapNetMsg = $"✓ {pts.Count(p => p.Ele is not null)} Höhen von OpenTopoData (EU-DEM/SRTM) übernommen.";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or System.Text.Json.JsonException) { _mapNetMsg = "⚠ " + ex.Message; }
        Show(MK);
    }

    /// <summary>Route über den eingetragenen OSRM-Server entlang von Wegen neu berechnen (die bisherigen Punkte sind die Zwischenziele).</summary>
    public async Task MapFetchRoute(string sel)
    {
        var d = ProgramActive(MK); var tr = d is null ? null : MapDoc.From(d.Data).Tracks.FirstOrDefault(t => t.Id == sel); if (d is null || tr is null) return;
        _mapNetMsg = "Route wird berechnet …"; Show(MK);
        try
        {
            var via = tr.Points.Select(p => p.Geo()).ToList(); if (via.Count > 25) via = Enumerable.Range(0, 25).Select(k => via[k * (via.Count - 1) / 24]).ToList();
            var json = await MapHttp.GetStringAsync(RouteService.Url(MapRoutingServer, MapProfile, via));
            var (pts, m, s) = RouteService.Parse(json);
            MapEdit("Route entlang von Wegen", x => x.Tracks.First(t => t.Id == sel).Points = pts.Select(MapPoint.From).ToList());
            _mapNetMsg = $"✓ Route entlang von Wegen: {m / 1000:0.0} km, etwa {TimeSpan.FromSeconds(s):h\\:mm} h.";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or ArgumentException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException) { _mapNetMsg = "⚠ " + ex.Message; }
        Show(MK);
    }

    string PackFile => Path.Combine(_core.DataRoot, "karten", "paket.txt");
    public string MapPackPath { get => File.Exists(PackFile) ? File.ReadAllText(PackFile).Trim() : ""; set { Directory.CreateDirectory(Path.GetDirectoryName(PackFile)!); File.WriteAllText(PackFile, value.Trim()); } }

    /// <summary>Offline-Kartenpaket laden, merken und die Karte auf sein Gebiet setzen.</summary>
    public void MapLoadPack(string path)
    {
        var p = new TilePack(path); Tiles.Pack = p; MapPackPath = path;
        var (s, w, n, e) = p.Bounds(); MapState.Lat = (s + n) / 2; MapState.Lon = (w + e) / 2; MapState.Zoom = p.MaxZoom; MapState.Tiles = true;
        _mapNetMsg = $"✓ Kartenpaket geladen: {p.Count} Kacheln, Zoom {p.MinZoom}–{p.MaxZoom}."; Show(MK);
    }
}
