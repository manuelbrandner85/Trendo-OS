using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Karten;

/// <summary>T-0260 (SPEC-13): Kartenprojekt – Spuren (aufgezeichnet oder selbst gezeichnete Routen), Wegpunkte und Messflächen.
/// Koordinaten in Grad (WGS84), Höhen in m.</summary>
public sealed class MapDoc
{
    public const int FormatVersion = 1;
    public int Version { get; set; } = FormatVersion;
    public string Name { get; set; } = "Karte";
    public double CenterLat { get; set; } = 51.1;
    public double CenterLon { get; set; } = 10.4;
    public double Zoom { get; set; } = 6;
    public List<MapTrack> Tracks { get; set; } = [];
    public List<MapWaypoint> Waypoints { get; set; } = [];
    public List<MapArea> Areas { get; set; } = [];

    static readonly JsonSerializerOptions Opt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
    public static MapDoc From(JsonObject data) { var m = data["map"] is JsonObject o ? o.Deserialize<MapDoc>(Opt) ?? new() : new(); m.Tracks.RemoveAll(t => t.Points.Count == 0); return m; }
    public void WriteTo(JsonObject data) => data["map"] = JsonSerializer.SerializeToNode(this, Opt);
    public MapDoc Clone() => JsonSerializer.Deserialize<MapDoc>(JsonSerializer.Serialize(this, Opt), Opt)!;

    public bool Remove(string id) => Tracks.RemoveAll(t => t.Id == id) + Waypoints.RemoveAll(w => w.Id == id) + Areas.RemoveAll(a => a.Id == id) > 0;

    static readonly string[] Palette = ["#F97316", "#3B82F6", "#22C55E", "#E11D48", "#A855F7", "#14B8A6", "#EAB308"];

    /// <summary>GPX-Datei übernehmen (Spur + Wegpunkte); die Ansicht springt auf die Spur.</summary>
    public MapTrack? ImportGpx(string path)
    {
        var (trk, wpt) = Geo.ReadGpx(path);
        MapTrack? t = null;
        if (trk.Count > 0) { t = new MapTrack { Name = Path.GetFileNameWithoutExtension(path), Color = Palette[Tracks.Count % Palette.Length], Points = trk.Select(MapPoint.From).ToList() }; Tracks.Add(t); }
        foreach (var w in wpt) Waypoints.Add(new MapWaypoint { Name = w.Name ?? "Wegpunkt", Lat = w.Lat, Lon = w.Lon, Ele = w.Ele });
        if (t is null && wpt.Count == 0) throw new InvalidDataException("In der Datei sind weder Spur noch Wegpunkte.");
        FitTo(t?.Points.Select(p => (p.Lat, p.Lon)) ?? wpt.Select(w => (w.Lat, w.Lon)));
        return t;
    }

    public MapTrack AddRoute(IEnumerable<(double Lat, double Lon)> pts, string name = "Route")
    {
        var list = pts.Select(p => new MapPoint { Lat = p.Lat, Lon = p.Lon }).ToList();
        if (list.Count < 2) throw new ArgumentException("Eine Route braucht mindestens zwei Punkte.");
        var t = new MapTrack { Name = name, Color = Palette[Tracks.Count % Palette.Length], Points = list, Planned = true }; Tracks.Add(t); return t;
    }

    public MapWaypoint AddWaypoint(double lat, double lon, string name = "Wegpunkt") { var w = new MapWaypoint { Lat = lat, Lon = lon, Name = name }; Waypoints.Add(w); return w; }
    public MapArea AddArea(IEnumerable<(double Lat, double Lon)> pts, string name = "Fläche")
    {
        var list = pts.Select(p => new MapPoint { Lat = p.Lat, Lon = p.Lon }).ToList();
        if (list.Count < 3) throw new ArgumentException("Eine Fläche braucht mindestens drei Punkte.");
        var a = new MapArea { Name = name, Points = list }; Areas.Add(a); return a;
    }

    public void FitTo(IEnumerable<(double Lat, double Lon)> pts, double widthPx = 900, double heightPx = 600)
    {
        var l = pts.ToList(); if (l.Count == 0) return;
        double la0 = l.Min(p => p.Lat), la1 = l.Max(p => p.Lat), lo0 = l.Min(p => p.Lon), lo1 = l.Max(p => p.Lon);
        CenterLat = (la0 + la1) / 2; CenterLon = (lo0 + lo1) / 2;
        var (x0, y0) = WebMercator.ToPixel(la1, lo0, 0); var (x1, y1) = WebMercator.ToPixel(la0, lo1, 0);
        var span = Math.Max((x1 - x0) / widthPx, (y1 - y0) / heightPx) * 1.25;
        Zoom = span <= 0 ? 15 : Math.Clamp(Math.Log2(1 / span), 2, 17);
    }

    // ---------- Vorlagen ----------
    public static MapDoc Template(string id)
    {
        var m = new MapDoc();
        if (id == "tour")
        {
            // Beispieltour (frei erfunden, kein echter Weg): Rundweg mit Anstieg und Abstieg, eine Messung pro Minute
            m.Name = "Beispieltour (erfunden)";
            var pts = new List<MapPoint>(); var start = new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i <= 120; i++)
            {
                var a = i / 120.0 * 2 * Math.PI;
                pts.Add(new MapPoint { Lat = 47.47 + 0.018 * Math.Sin(a), Lon = 11.06 + 0.03 * (1 - Math.Cos(a)) / 2 + 0.006 * Math.Sin(2 * a), Ele = 720 + 480 * Math.Pow(Math.Sin(a / 2), 2), Time = start.AddMinutes(i * 2) });
            }
            m.Tracks.Add(new MapTrack { Name = "Rundweg", Color = Palette[0], Points = pts });
            m.Waypoints.Add(new MapWaypoint { Name = "Start / Parkplatz", Lat = 47.47, Lon = 11.06, Ele = 720 });
            m.Waypoints.Add(new MapWaypoint { Name = "Gipfelkreuz", Lat = 47.47, Lon = 11.09, Ele = 1200 });
            m.FitTo(pts.Select(p => (p.Lat, p.Lon)));
        }
        return m;
    }

    // ---------- Export ----------
    public string Gpx()
    {
        var I = CultureInfo.InvariantCulture; string E(string s) => System.Security.SecurityElement.Escape(s);
        var sb = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<gpx version=\"1.1\" creator=\"Trendo Karten\" xmlns=\"http://www.topografix.com/GPX/1/1\">\n<metadata><name>{E(Name)}</name></metadata>\n");
        foreach (var w in Waypoints) sb.Append($"<wpt lat=\"{w.Lat.ToString("0.0000000", I)}\" lon=\"{w.Lon.ToString("0.0000000", I)}\">{(w.Ele is { } e ? $"<ele>{e.ToString("0.#", I)}</ele>" : "")}<name>{E(w.Name)}</name></wpt>\n");
        foreach (var t in Tracks)
        {
            var tag = t.Planned ? "rte" : "trk"; var pt = t.Planned ? "rtept" : "trkpt";
            sb.Append($"<{tag}><name>{E(t.Name)}</name>{(t.Planned ? "" : "<trkseg>")}\n");
            foreach (var p in t.Points) sb.Append($"<{pt} lat=\"{p.Lat.ToString("0.0000000", I)}\" lon=\"{p.Lon.ToString("0.0000000", I)}\">{(p.Ele is { } e ? $"<ele>{e.ToString("0.#", I)}</ele>" : "")}{(p.Time is { } tm ? $"<time>{tm.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}</time>" : "")}</{pt}>\n");
            sb.Append($"{(t.Planned ? "" : "</trkseg>")}</{tag}>\n");
        }
        return sb.Append("</gpx>\n").ToString();
    }

    public string GeoJson()
    {
        var I = CultureInfo.InvariantCulture; string C(double lon, double lat) => $"[{lon.ToString("0.0000000", I)},{lat.ToString("0.0000000", I)}]";
        var f = new List<string>();
        foreach (var t in Tracks) f.Add($"{{\"type\":\"Feature\",\"properties\":{{\"name\":{JsonSerializer.Serialize(t.Name)},\"stroke\":\"{t.Color}\"}},\"geometry\":{{\"type\":\"LineString\",\"coordinates\":[{string.Join(",", t.Points.Select(p => C(p.Lon, p.Lat)))}]}}}}");
        foreach (var a in Areas) f.Add($"{{\"type\":\"Feature\",\"properties\":{{\"name\":{JsonSerializer.Serialize(a.Name)}}},\"geometry\":{{\"type\":\"Polygon\",\"coordinates\":[[{string.Join(",", a.Points.Append(a.Points[0]).Select(p => C(p.Lon, p.Lat)))}]]}}}}");
        foreach (var w in Waypoints) f.Add($"{{\"type\":\"Feature\",\"properties\":{{\"name\":{JsonSerializer.Serialize(w.Name)}}},\"geometry\":{{\"type\":\"Point\",\"coordinates\":{C(w.Lon, w.Lat)}}}}}");
        return $"{{\"type\":\"FeatureCollection\",\"features\":[{string.Join(",", f)}]}}";
    }

    public string Kml()
    {
        var I = CultureInfo.InvariantCulture; string E(string s) => System.Security.SecurityElement.Escape(s);
        string Co(IEnumerable<MapPoint> ps) => string.Join(" ", ps.Select(p => $"{p.Lon.ToString("0.0000000", I)},{p.Lat.ToString("0.0000000", I)},{(p.Ele ?? 0).ToString("0.#", I)}"));
        var sb = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document><name>{E(Name)}</name>\n");
        foreach (var t in Tracks) sb.Append($"<Placemark><name>{E(t.Name)}</name><Style><LineStyle><color>ff{t.Color[5..7]}{t.Color[3..5]}{t.Color[1..3]}</color><width>4</width></LineStyle></Style><LineString><coordinates>{Co(t.Points)}</coordinates></LineString></Placemark>\n");
        foreach (var a in Areas) sb.Append($"<Placemark><name>{E(a.Name)}</name><Polygon><outerBoundaryIs><LinearRing><coordinates>{Co(a.Points.Append(a.Points[0]))}</coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>\n");
        foreach (var w in Waypoints) sb.Append($"<Placemark><name>{E(w.Name)}</name><Point><coordinates>{w.Lon.ToString("0.0000000", I)},{w.Lat.ToString("0.0000000", I)}</coordinates></Point></Placemark>\n");
        return sb.Append("</Document></kml>\n").ToString();
    }
}

public sealed class MapPoint
{
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double? Ele { get; set; }
    public DateTime? Time { get; set; }
    public static MapPoint From(GeoPoint p) => new() { Lat = p.Lat, Lon = p.Lon, Ele = p.Ele, Time = p.Time };
    public GeoPoint Geo() => new(Lat, Lon, Ele, Time);
}

public sealed class MapTrack
{
    public string Id { get; set; } = MapDoc.NewId();
    public string Name { get; set; } = "Spur";
    public string Color { get; set; } = "#F97316";
    /// <summary>true = selbst gezeichnete Route (wird als GPX-Route gespeichert), false = aufgezeichnete Spur.</summary>
    public bool Planned { get; set; }
    public List<MapPoint> Points { get; set; } = [];
    public TrackStats Stats() => Trendo.Core.Geo.Analyse(Points.Select(p => p.Geo()).ToList());
}

public sealed class MapWaypoint
{
    public string Id { get; set; } = MapDoc.NewId();
    public string Name { get; set; } = "Wegpunkt";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double? Ele { get; set; }
}

public sealed class MapArea
{
    public string Id { get; set; } = MapDoc.NewId();
    public string Name { get; set; } = "Fläche";
    public List<MapPoint> Points { get; set; } = [];
    public double AreaM2 => GeoPlus.AreaM2(Points.Select(p => p.Geo()).ToList());
    public double PerimeterKm => Points.Count < 2 ? 0 : Points.Select((p, i) => Trendo.Core.Geo.Haversine(p.Geo(), Points[(i + 1) % Points.Count].Geo())).Sum();
}

/// <summary>Web-Mercator (wie OpenStreetMap-Kacheln): Weltpixel bei Zoom z = 256·2^z.</summary>
public static class WebMercator
{
    public const double MaxLat = 85.05112878;
    public static (double X, double Y) ToPixel(double lat, double lon, double zoom)
    {
        var size = 256 * Math.Pow(2, zoom); lat = Math.Clamp(lat, -MaxLat, MaxLat);
        var x = (lon + 180) / 360 * size;
        var s = Math.Sin(lat * Math.PI / 180);
        var y = (0.5 - Math.Log((1 + s) / (1 - s)) / (4 * Math.PI)) * size;
        return (x, y);
    }
    public static (double Lat, double Lon) ToLatLon(double x, double y, double zoom)
    {
        var size = 256 * Math.Pow(2, zoom);
        var lon = x / size * 360 - 180;
        var n = Math.PI - 2 * Math.PI * y / size;
        var lat = 180 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
        return (lat, lon);
    }
    /// <summary>Meter je Bildpunkt an einer Breite.</summary>
    public static double MetersPerPixel(double lat, double zoom) => 156543.03392 * Math.Cos(lat * Math.PI / 180) / Math.Pow(2, zoom);
}
