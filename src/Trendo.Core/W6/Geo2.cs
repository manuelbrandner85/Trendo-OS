using System.Globalization;
using System.Security;
using System.Text;

namespace Trendo.Core;

/// <summary>SPEC-13 Ausbau: UTM zurück, Peilung, Zielpunkt, Fläche, Höhenprofil, Wanderzeit, Vereinfachen, GPX schreiben.</summary>
public static class GeoPlus
{
    static string I(double v, string f = "0.0000000") => v.ToString(f, CultureInfo.InvariantCulture);

    /// <summary>F13: UTM (Zone, Ost, Nord, Nordhalbkugel?) → WGS84.</summary>
    public static (double Lat, double Lon) UtmToLatLon(int zone, double easting, double northing, bool north = true)
    {
        if (zone < 1 || zone > 60) throw new ArgumentException("Die UTM-Zone liegt zwischen 1 und 60.");
        const double a = 6378137, f = 1 / 298.257223563, k0 = 0.9996; var e2 = f * (2 - f); var ep2 = e2 / (1 - e2); var e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));
        var x = easting - 500000; var y = north ? northing : northing - 10000000;
        var m = y / k0; var mu = m / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));
        var p1 = mu + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu) + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu) + 151 * Math.Pow(e1, 3) / 96 * Math.Sin(6 * mu) + 1097 * Math.Pow(e1, 4) / 512 * Math.Sin(8 * mu);
        var n1 = a / Math.Sqrt(1 - e2 * Math.Sin(p1) * Math.Sin(p1)); var t1 = Math.Tan(p1) * Math.Tan(p1); var c1 = ep2 * Math.Cos(p1) * Math.Cos(p1);
        var r1 = a * (1 - e2) / Math.Pow(1 - e2 * Math.Sin(p1) * Math.Sin(p1), 1.5); var d = x / (n1 * k0);
        var lat = p1 - n1 * Math.Tan(p1) / r1 * (d * d / 2 - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * ep2) * Math.Pow(d, 4) / 24 + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * ep2 - 3 * c1 * c1) * Math.Pow(d, 6) / 720);
        var lon = (d - (1 + 2 * t1 + c1) * Math.Pow(d, 3) / 6 + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * ep2 + 24 * t1 * t1) * Math.Pow(d, 5) / 120) / Math.Cos(p1);
        return (lat * 180 / Math.PI, ((zone - 1) * 6 - 180 + 3) + lon * 180 / Math.PI);
    }

    /// <summary>F14: Anfangspeilung in Grad (0 = Nord, im Uhrzeigersinn).</summary>
    public static double Bearing(GeoPoint a, GeoPoint b)
    {
        double p1 = a.Lat * Math.PI / 180, p2 = b.Lat * Math.PI / 180, dl = (b.Lon - a.Lon) * Math.PI / 180;
        var t = Math.Atan2(Math.Sin(dl) * Math.Cos(p2), Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dl));
        return (t * 180 / Math.PI + 360) % 360;
    }
    public static string Compass(double deg) => new[] { "N", "NNO", "NO", "ONO", "O", "OSO", "SO", "SSO", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" }[(int)Math.Round(((deg % 360) + 360) % 360 / 22.5) % 16];

    /// <summary>F15: Zielpunkt aus Start, Peilung (Grad) und Entfernung (km).</summary>
    public static GeoPoint Destination(GeoPoint a, double bearingDeg, double km)
    {
        double d = km / Geo.R, t = bearingDeg * Math.PI / 180, p1 = a.Lat * Math.PI / 180, l1 = a.Lon * Math.PI / 180;
        var p2 = Math.Asin(Math.Sin(p1) * Math.Cos(d) + Math.Cos(p1) * Math.Sin(d) * Math.Cos(t));
        var l2 = l1 + Math.Atan2(Math.Sin(t) * Math.Sin(d) * Math.Cos(p1), Math.Cos(d) - Math.Sin(p1) * Math.Sin(p2));
        return new(p2 * 180 / Math.PI, (l2 * 180 / Math.PI + 540) % 360 - 180);
    }

    /// <summary>F16: Fläche eines Polygons auf der Kugel in m².</summary>
    public static double AreaM2(IReadOnlyList<GeoPoint> poly)
    {
        if (poly.Count < 3) throw new ArgumentException("Für eine Fläche braucht es mindestens 3 Punkte.");
        var r = Geo.R * 1000; var s = 0.0;
        for (var i = 0; i < poly.Count; i++) { var p = poly[i]; var q = poly[(i + 1) % poly.Count]; s += (q.Lon - p.Lon) * Math.PI / 180 * (2 + Math.Sin(p.Lat * Math.PI / 180) + Math.Sin(q.Lat * Math.PI / 180)); }
        return Math.Abs(s * r * r / 2);
    }

    /// <summary>F17: Höhenprofil (x = km, y = Höhe) als SVG.</summary>
    public static string ProfileSvg(IReadOnlyList<GeoPoint> t, int w = 800, int h = 240)
    {
        var pts = t.Where(p => p.Ele is not null).ToList(); if (pts.Count < 2) throw new InvalidOperationException("Die Strecke hat keine Höhenangaben.");
        var km = new List<double> { 0 }; for (var i = 1; i < pts.Count; i++) km.Add(km[^1] + Geo.Haversine(pts[i - 1], pts[i]));
        double lo = pts.Min(p => p.Ele!.Value), hi = pts.Max(p => p.Ele!.Value), span = Math.Max(1, hi - lo), total = Math.Max(1e-9, km[^1]);
        var path = string.Join(" ", pts.Select((p, i) => $"{I(km[i] / total * w, "0.0")},{I(h - 20 - (p.Ele!.Value - lo) / span * (h - 40), "0.0")}"));
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" width=\"{w}\" height=\"{h}\"><rect width=\"100%\" height=\"100%\" fill=\"#fff\"/><polyline fill=\"none\" stroke=\"#2563eb\" stroke-width=\"2\" points=\"{path}\"/><text x=\"4\" y=\"14\" font-size=\"12\">{I(hi, "0")} m</text><text x=\"4\" y=\"{h - 4}\" font-size=\"12\">{I(lo, "0")} m · {I(total, "0.0")} km</text></svg>";
    }

    /// <summary>F18: Gehzeit nach DIN 33466 (4 km/h, 300 Hm/h hoch, 500 Hm/h runter; größerer + halber kleinerer Wert).</summary>
    public static TimeSpan HikingTime(double km, double up, double down)
    {
        var horiz = km / 4; var vert = up / 300 + down / 500;
        return TimeSpan.FromHours(Math.Max(horiz, vert) + Math.Min(horiz, vert) / 2);
    }

    /// <summary>F19: Douglas-Peucker mit Toleranz in Metern.</summary>
    public static List<GeoPoint> Simplify(IReadOnlyList<GeoPoint> t, double tolM)
    {
        if (t.Count < 3) return [.. t];
        var lat0 = t.Average(p => p.Lat) * Math.PI / 180; var k = Geo.R * 1000 * Math.PI / 180;
        var xy = t.Select(p => (X: p.Lon * k * Math.Cos(lat0), Y: p.Lat * k)).ToArray(); var keep = new bool[t.Count]; keep[0] = keep[^1] = true;
        var stack = new Stack<(int, int)>(); stack.Push((0, t.Count - 1));
        while (stack.Count > 0)
        {
            var (s, e) = stack.Pop(); double dx = xy[e].X - xy[s].X, dy = xy[e].Y - xy[s].Y, len = Math.Sqrt(dx * dx + dy * dy); var best = -1.0; var bi = -1;
            for (var i = s + 1; i < e; i++) { var d = len < 1e-9 ? Math.Sqrt(Math.Pow(xy[i].X - xy[s].X, 2) + Math.Pow(xy[i].Y - xy[s].Y, 2)) : Math.Abs(dy * xy[i].X - dx * xy[i].Y + xy[e].X * xy[s].Y - xy[e].Y * xy[s].X) / len; if (d > best) { best = d; bi = i; } }
            if (best > tolM) { keep[bi] = true; stack.Push((s, bi)); stack.Push((bi, e)); }
        }
        return t.Where((_, i) => keep[i]).ToList();
    }

    /// <summary>F20: Strecke und Wegpunkte als GPX 1.1.</summary>
    public static string WriteGpx(IReadOnlyList<GeoPoint> track, IReadOnlyList<GeoPoint> wpts, string name)
    {
        string Pt(string tag, GeoPoint p) => $"<{tag} lat=\"{I(p.Lat)}\" lon=\"{I(p.Lon)}\">" + (p.Ele is { } e ? $"<ele>{I(e, "0.0")}</ele>" : "") + (p.Time is { } tm ? $"<time>{tm.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}</time>" : "") + (p.Name is { } n ? $"<name>{SecurityElement.Escape(n)}</name>" : "") + $"</{tag}>";
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<gpx version=\"1.1\" creator=\"Trendo OS\" xmlns=\"http://www.topografix.com/GPX/1/1\">\n");
        foreach (var w in wpts) sb.Append(Pt("wpt", w)).Append('\n');
        if (track.Count > 0) { sb.Append($"<trk><name>{SecurityElement.Escape(name)}</name><trkseg>\n"); foreach (var p in track) sb.Append(Pt("trkpt", p)).Append('\n'); sb.Append("</trkseg></trk>\n"); }
        return sb.Append("</gpx>\n").ToString();
    }
}
