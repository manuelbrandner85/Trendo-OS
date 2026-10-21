using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Trendo.Core;

public sealed record GeoPoint(double Lat, double Lon, double? Ele = null, DateTime? Time = null, string? Name = null);
public sealed record TrackStats(int Points, double DistanceKm, double AscentM, double DescentM, TimeSpan? Duration, double? AvgKmh, double MinEle, double MaxEle, (double S, double W, double N, double E) Box);

/// <summary>SPEC-13: Karten & Geodaten – GPX lesen, Strecke auswerten, GeoJSON/SVG, Koordinaten (Grad, DMS, UTM).</summary>
public static class Geo
{
    public const double R = 6371.0088;

    public static double Haversine(GeoPoint a, GeoPoint b)
    {
        double rad(double d) => d * Math.PI / 180;
        var dLat = rad(b.Lat - a.Lat); var dLon = rad(b.Lon - a.Lon);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(rad(a.Lat)) * Math.Cos(rad(b.Lat)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static (List<GeoPoint> Track, List<GeoPoint> Waypoints) ReadGpx(string path)
    {
        if (!File.Exists(path)) throw new IOException("Diese Datei gibt es nicht.");
        XDocument d; try { d = XDocument.Load(path, LoadOptions.None); } catch (System.Xml.XmlException) { throw new InvalidOperationException("Das ist keine gültige GPX-Datei."); }
        GeoPoint P(XElement e) => new(D(e.Attribute("lat")?.Value), D(e.Attribute("lon")?.Value),
            e.Elements().FirstOrDefault(x => x.Name.LocalName == "ele") is { } el ? D(el.Value) : null,
            e.Elements().FirstOrDefault(x => x.Name.LocalName == "time") is { } t && DateTime.TryParse(t.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt) ? dt : null,
            e.Elements().FirstOrDefault(x => x.Name.LocalName == "name")?.Value);
        var trk = d.Descendants().Where(e => e.Name.LocalName is "trkpt" or "rtept").Select(P).ToList();
        var wpt = d.Descendants().Where(e => e.Name.LocalName == "wpt").Select(P).ToList();
        if (trk.Count == 0 && wpt.Count == 0) throw new InvalidOperationException("In der GPX-Datei sind keine Punkte.");
        return (trk, wpt);
    }

    private static double D(string? s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : throw new InvalidOperationException("Ungültige Koordinate in der Datei.");

    /// <summary>Auswertung; Höhenmeter mit 3-m-Schwelle gegen GPS-Rauschen.</summary>
    public static TrackStats Analyse(IReadOnlyList<GeoPoint> t, double noiseM = 3)
    {
        if (t.Count == 0) throw new ArgumentException("Keine Streckenpunkte.");
        double dist = 0, up = 0, down = 0; double? refEle = t[0].Ele;
        for (var i = 1; i < t.Count; i++)
        {
            dist += Haversine(t[i - 1], t[i]);
            if (t[i].Ele is { } e && refEle is { } r) { if (e - r >= noiseM) { up += e - r; refEle = e; } else if (r - e >= noiseM) { down += r - e; refEle = e; } }
            else refEle ??= t[i].Ele;
        }
        TimeSpan? dur = t[0].Time is { } a && t[^1].Time is { } b ? b - a : null;
        var eles = t.Where(p => p.Ele.HasValue).Select(p => p.Ele!.Value).DefaultIfEmpty(0).ToList();
        return new(t.Count, Math.Round(dist, 3), Math.Round(up), Math.Round(down), dur, dur is { TotalHours: > 0 } x ? Math.Round(dist / x.TotalHours, 2) : null, eles.Min(), eles.Max(),
            (t.Min(p => p.Lat), t.Min(p => p.Lon), t.Max(p => p.Lat), t.Max(p => p.Lon)));
    }

    public static string GeoJson(IReadOnlyList<GeoPoint> track, IReadOnlyList<GeoPoint> wpts, string name)
    {
        string C(GeoPoint p) => FormattableString.Invariant($"[{p.Lon:0.######},{p.Lat:0.######}{(p.Ele is { } e ? FormattableString.Invariant($",{e:0.#}") : "")}]");
        var f = new List<string>();
        if (track.Count > 0) f.Add($"{{\"type\":\"Feature\",\"properties\":{{\"name\":{System.Text.Json.JsonSerializer.Serialize(name)}}},\"geometry\":{{\"type\":\"LineString\",\"coordinates\":[{string.Join(",", track.Select(C))}]}}}}");
        f.AddRange(wpts.Select(w => $"{{\"type\":\"Feature\",\"properties\":{{\"name\":{System.Text.Json.JsonSerializer.Serialize(w.Name ?? "")}}},\"geometry\":{{\"type\":\"Point\",\"coordinates\":{C(w)}}}}}"));
        return $"{{\"type\":\"FeatureCollection\",\"features\":[{string.Join(",", f)}]}}";
    }

    /// <summary>Strecke als SVG (gleichwinklige Projektion, Nord oben).</summary>
    public static string Svg(IReadOnlyList<GeoPoint> t, int size = 600)
    {
        var s = Analyse(t); var k = Math.Cos((s.Box.S + s.Box.N) / 2 * Math.PI / 180);
        double w = Math.Max(1e-9, (s.Box.E - s.Box.W) * k), h = Math.Max(1e-9, s.Box.N - s.Box.S), sc = (size - 20) / Math.Max(w, h);
        var pts = string.Join(" ", t.Select(p => FormattableString.Invariant($"{10 + (p.Lon - s.Box.W) * k * sc:0.#},{10 + (s.Box.N - p.Lat) * sc:0.#}")));
        return FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w * sc + 20:0}\" height=\"{h * sc + 20:0}\"><polyline points=\"{pts}\" fill=\"none\" stroke=\"#e4572e\" stroke-width=\"3\"/></svg>");
    }

    /// <summary>„48.137, 11.575“, „48°8'13.2\"N 11°34'30\"E“ → Grad.</summary>
    public static (double Lat, double Lon) ParseCoord(string s)
    {
        s = s.Trim().Replace("′", "'").Replace("″", "\"").Replace("''", "\"");
        var m = System.Text.RegularExpressions.Regex.Matches(s, @"(-?\d+(?:[.,]\d+)?)\s*°?\s*(?:(\d+(?:[.,]\d+)?)\s*'\s*)?(?:(\d+(?:[.,]\d+)?)\s*""\s*)?([NSEOW])?", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Where(x => x.Value.Trim() != "").ToList();
        if (m.Count < 2) throw new FormatException("Bitte Breite und Länge angeben, z. B. „48,137 11,575“ oder „48°8'13\"N 11°34'30\"E“.");
        double V(System.Text.RegularExpressions.Match x)
        {
            var v = Math.Abs(Fach.Num(x.Groups[1].Value)) + (x.Groups[2].Success ? Fach.Num(x.Groups[2].Value) / 60 : 0) + (x.Groups[3].Success ? Fach.Num(x.Groups[3].Value) / 3600 : 0);
            return x.Groups[1].Value.StartsWith('-') || x.Groups[4].Value.ToUpperInvariant() is "S" or "W" ? -v : v;
        }
        if (m.Count > 2 && !s.Contains('°')) { var parts = s.Split([' ', ';'], StringSplitOptions.RemoveEmptyEntries); if (parts.Length == 2) return Check(Fach.Num(parts[0]), Fach.Num(parts[1])); }
        return Check(V(m[0]), V(m[1]));
    }

    private static (double, double) Check(double lat, double lon) => Math.Abs(lat) > 90 || Math.Abs(lon) > 180 ? throw new FormatException("Breite muss zwischen −90 und 90, Länge zwischen −180 und 180 liegen.") : (lat, lon);

    public static string Dms(double lat, double lon)
    {
        static string F(double v, string pos, string neg) { var a = Math.Abs(v); var d = (int)a; var mm = (a - d) * 60; var m = (int)mm; var sec = (mm - m) * 60; if (sec >= 59.95) { sec = 0; m++; } if (m == 60) { m = 0; d++; } return $"{d}°{m}'{sec.ToString("0.0", CultureInfo.InvariantCulture)}\"{(v < 0 ? neg : pos)}"; }
        return $"{F(lat, "N", "S")} {F(lon, "E", "W")}";
    }

    /// <summary>WGS84 → UTM (Zone, Ost, Nord in Metern).</summary>
    public static (int Zone, char Band, double Easting, double Northing) Utm(double lat, double lon)
    {
        if (lat < -80 || lat > 84) throw new ArgumentException("UTM gilt nur zwischen 80° Süd und 84° Nord.");
        var zone = (int)Math.Floor((lon + 180) / 6) + 1;
        if (lat >= 56 && lat < 64 && lon >= 3 && lon < 12) zone = 32;
        const double a = 6378137, f = 1 / 298.257223563, k0 = 0.9996; var e2 = f * (2 - f); var ep2 = e2 / (1 - e2);
        double phi = lat * Math.PI / 180, lam = lon * Math.PI / 180, lam0 = ((zone - 1) * 6 - 180 + 3) * Math.PI / 180;
        var n = a / Math.Sqrt(1 - e2 * Math.Sin(phi) * Math.Sin(phi)); var t = Math.Tan(phi) * Math.Tan(phi); var c = ep2 * Math.Cos(phi) * Math.Cos(phi); var A = Math.Cos(phi) * (lam - lam0);
        var M = a * ((1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256) * phi - (3 * e2 / 8 + 3 * e2 * e2 / 32 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(2 * phi) + (15 * e2 * e2 / 256 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(4 * phi) - 35 * e2 * e2 * e2 / 3072 * Math.Sin(6 * phi));
        var east = k0 * n * (A + (1 - t + c) * Math.Pow(A, 3) / 6 + (5 - 18 * t + t * t + 72 * c - 58 * ep2) * Math.Pow(A, 5) / 120) + 500000;
        var north = k0 * (M + n * Math.Tan(phi) * (A * A / 2 + (5 - t + 9 * c + 4 * c * c) * Math.Pow(A, 4) / 24 + (61 - 58 * t + t * t + 600 * c - 330 * ep2) * Math.Pow(A, 6) / 720));
        if (lat < 0) north += 10000000;
        var band = "CDEFGHJKLMNPQRSTUVWX"[Math.Min(19, (int)Math.Floor((lat + 80) / 8))];
        return (zone, band, Math.Round(east, 1), Math.Round(north, 1));
    }
}
