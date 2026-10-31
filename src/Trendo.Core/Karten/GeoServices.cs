using System.Globalization;
using System.Text.Json;

namespace Trendo.Core.Karten;

/// <summary>T-0274: Höhen für gezeichnete Routen über OpenTopoData (freier Dienst, ohne Schlüssel; EU-DEM 25 m in Europa, sonst SRTM 30 m).
/// Nur nach Zustimmung (Koordinaten verlassen den Rechner), höchstens 100 Punkte je Abruf, eine Sekunde Abstand, Ergebnisse auf der Platte zwischengespeichert.</summary>
public sealed class ElevationService(HttpClient http, string cacheFile)
{
    public const string Host = "api.opentopodata.org";
    readonly Dictionary<string, double> _cache = Load(cacheFile);

    static Dictionary<string, double> Load(string f) { try { return File.Exists(f) ? JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(f)) ?? [] : []; } catch (JsonException) { return []; } }
    static string Key(double lat, double lon) => FormattableString.Invariant($"{lat:0.0000},{lon:0.0000}");

    /// <summary>Punkte so verdichten, dass zwischen zwei Punkten höchstens <paramref name="stepM"/> Meter liegen (für Routen mit wenigen Klickpunkten).</summary>
    public static List<GeoPoint> Densify(IReadOnlyList<GeoPoint> pts, double stepM = 100, int max = 500)
    {
        var res = new List<GeoPoint>();
        for (var i = 0; i < pts.Count; i++)
        {
            if (i > 0)
            {
                var d = Geo.Haversine(pts[i - 1], pts[i]) * 1000; var n = (int)Math.Ceiling(d / stepM); // Haversine liefert km
                for (var k = 1; k < n; k++) res.Add(new(pts[i - 1].Lat + (pts[i].Lat - pts[i - 1].Lat) * k / n, pts[i - 1].Lon + (pts[i].Lon - pts[i - 1].Lon) * k / n));
            }
            res.Add(pts[i]);
        }
        if (res.Count <= max) return res;
        var stride = (double)(res.Count - 1) / (max - 1); return Enumerable.Range(0, max).Select(k => res[(int)Math.Round(k * stride)]).ToList();
    }

    public static string Dataset(IEnumerable<GeoPoint> pts) => pts.All(p => p.Lat is > 34 and < 72 && p.Lon is > -25 and < 45) ? "eudem25m" : "srtm30m";

    public static string Url(string dataset, IEnumerable<GeoPoint> pts) => $"https://{Host}/v1/{dataset}?locations=" + string.Join("|", pts.Select(p => FormattableString.Invariant($"{p.Lat:0.######},{p.Lon:0.######}")));

    /// <summary>Antwort von OpenTopoData lesen: je Punkt die Höhe oder null (z. B. über dem Meer).</summary>
    public static List<double?> Parse(string json)
    {
        using var d = JsonDocument.Parse(json); var root = d.RootElement;
        if (root.TryGetProperty("status", out var st) && st.GetString() != "OK") throw new InvalidOperationException("Der Höhendienst meldet: " + (root.TryGetProperty("error", out var er) ? er.GetString() : st.GetString()));
        return root.GetProperty("results").EnumerateArray().Select(r => r.TryGetProperty("elevation", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : (double?)null).ToList();
    }

    /// <summary>Höhen abrufen (Zwischenspeicher zuerst). <paramref name="pause"/> hält die Regel „ein Abruf je Sekunde“ ein.</summary>
    public async Task<List<GeoPoint>> AddElevation(IReadOnlyList<GeoPoint> pts, Func<TimeSpan, Task>? pause = null, CancellationToken ct = default)
    {
        var dense = Densify(pts); var missing = dense.Where(p => !_cache.ContainsKey(Key(p.Lat, p.Lon))).ToList(); var ds = Dataset(dense);
        for (var i = 0; i < missing.Count; i += 100)
        {
            if (i > 0) await (pause ?? (t => Task.Delay(t, ct)))(TimeSpan.FromSeconds(1.1));
            var chunk = missing.Skip(i).Take(100).ToList();
            using var resp = await http.GetAsync(Url(ds, chunk), ct);
            if ((int)resp.StatusCode == 429) throw new InvalidOperationException("Der Höhendienst ist für heute ausgelastet (Tageslimit). Bitte später erneut versuchen.");
            resp.EnsureSuccessStatusCode();
            var h = Parse(await resp.Content.ReadAsStringAsync(ct));
            for (var k = 0; k < chunk.Count && k < h.Count; k++) if (h[k] is { } e) _cache[Key(chunk[k].Lat, chunk[k].Lon)] = e;
        }
        try { Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!); File.WriteAllText(cacheFile, JsonSerializer.Serialize(_cache)); } catch (IOException) { }
        return dense.Select(p => p with { Ele = _cache.TryGetValue(Key(p.Lat, p.Lon), out var e) ? e : p.Ele }).ToList();
    }
}

/// <summary>T-0274: Route entlang von Wegen über einen OSRM-kompatiblen Server, den der Nutzer selbst einträgt (eigener Server oder ein Anbieter
/// mit passenden Nutzungsbedingungen). Trendo bringt keinen fest eingebauten fremden Dienst mit.</summary>
public static class RouteService
{
    public static readonly string[] Profiles = ["foot", "bike", "car"];

    public static string Url(string server, string profile, IEnumerable<GeoPoint> via)
    {
        var s = server.Trim().TrimEnd('/');
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http")) throw new ArgumentException("Bitte die Adresse des Routing-Servers eintragen, z. B. https://mein-server.example/osrm.");
        return $"{s}/route/v1/{profile}/" + string.Join(";", via.Select(p => FormattableString.Invariant($"{p.Lon:0.######},{p.Lat:0.######}"))) + "?overview=full&geometries=geojson";
    }

    /// <summary>OSRM-Antwort lesen: Linienpunkte, Länge (m) und Dauer (s) der ersten Route.</summary>
    public static (List<GeoPoint> Points, double Meters, double Seconds) Parse(string json)
    {
        using var d = JsonDocument.Parse(json); var root = d.RootElement;
        if (root.GetProperty("code").GetString() != "Ok") throw new InvalidOperationException("Keine Route gefunden (" + root.GetProperty("code").GetString() + ").");
        var r = root.GetProperty("routes")[0];
        var pts = r.GetProperty("geometry").GetProperty("coordinates").EnumerateArray().Select(c => new GeoPoint(c[1].GetDouble(), c[0].GetDouble())).ToList();
        return (pts, r.GetProperty("distance").GetDouble(), r.GetProperty("duration").GetDouble());
    }
}
