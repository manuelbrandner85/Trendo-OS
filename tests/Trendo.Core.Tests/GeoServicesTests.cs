using System.Net;
using Trendo.Core.Karten;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0274: Höhen (OpenTopoData) und Routing (OSRM) – ohne echtes Netz, mit nachgebauten Antworten.</summary>
public class T0274_GeoServices
{
    sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Urls.Add(r.RequestUri!.ToString()); return Task.FromResult(f(r)); }
    }

    static HttpResponseMessage Heights(HttpRequestMessage r)
    {
        var locs = Uri.UnescapeDataString(r.RequestUri!.Query).Split("locations=")[1].Split('|');
        var json = "{\"status\":\"OK\",\"results\":[" + string.Join(",", locs.Select(l => { var lat = double.Parse(l.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture); return $"{{\"elevation\":{(500 + (lat - 47) * 10000).ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"; })) + "]}";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    }

    [Fact(DisplayName = "T-0274: Höhen – Route mit 2 Klickpunkten wird alle 100 m verdichtet, in 100er-Blöcken mit Pause abgefragt, danach aus dem Zwischenspeicher")]
    public async Task Elevation_chunks_and_cache()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0274-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var fake = new Fake(Heights); var svc = new ElevationService(new TestFetch(new HttpClient(fake)), Path.Combine(dir, "hoehen.json")); var pauses = 0;
            var route = new List<GeoPoint> { new(47.0, 11.0), new(47.2, 11.0) }; // ~22 km nach Norden
            var res = await svc.AddElevation(route, _ => { pauses++; return Task.CompletedTask; });
            Assert.InRange(res.Count, 220, 225); Assert.All(res, p => Assert.NotNull(p.Ele));
            Assert.Equal(3, fake.Urls.Count); Assert.Equal(2, pauses); Assert.All(fake.Urls, u => Assert.Contains("/v1/eudem25m?", u));
            Assert.Equal(500, res[0].Ele!.Value, 3); Assert.Equal(2500, res[^1].Ele!.Value, 3);
            var again = new ElevationService(new TestFetch(new HttpClient(fake)), Path.Combine(dir, "hoehen.json"));
            await again.AddElevation(route, _ => Task.CompletedTask); Assert.Equal(3, fake.Urls.Count); // nichts erneut abgefragt
            Assert.Equal("srtm30m", ElevationService.Dataset([new(40.7, -74)]));
            var busy = new ElevationService(new TestFetch(new HttpClient(new Fake(_ => new HttpResponseMessage((HttpStatusCode)429)))), Path.Combine(dir, "x.json"));
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => busy.AddElevation([new(48, 11), new(48.001, 11)]));
            Assert.Contains("Tageslimit", ex.Message);
            Assert.Throws<InvalidOperationException>(() => ElevationService.Parse("{\"status\":\"INVALID_REQUEST\",\"error\":\"Too many locations\"}"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0274: Routing – OSRM-Adresse (lon,lat), Antwort mit Linie, Länge und Dauer; ohne Server-Adresse klare Meldung")]
    public void Routing()
    {
        var url = RouteService.Url("https://osrm.example/", "foot", [new(47.1, 11.2), new(47.3, 11.4)]);
        Assert.Equal("https://osrm.example/route/v1/foot/11.2,47.1;11.4,47.3?overview=full&geometries=geojson", url);
        Assert.Throws<ArgumentException>(() => RouteService.Url("", "foot", [new(1, 1), new(2, 2)]));
        var (pts, m, s) = RouteService.Parse("{\"code\":\"Ok\",\"routes\":[{\"distance\":1234.5,\"duration\":900,\"geometry\":{\"type\":\"LineString\",\"coordinates\":[[11.2,47.1],[11.25,47.15],[11.4,47.3]]}}]}");
        Assert.Equal(3, pts.Count); Assert.Equal(47.15, pts[1].Lat); Assert.Equal(1234.5, m); Assert.Equal(900, s);
        Assert.Throws<InvalidOperationException>(() => RouteService.Parse("{\"code\":\"NoRoute\",\"routes\":[]}"));
    }
}
